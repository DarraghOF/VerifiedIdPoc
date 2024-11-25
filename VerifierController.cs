using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AspNetCoreVerifiableCredentials.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AspNetCoreVerifiableCredentials
{
    [Route("api/[controller]/[action]")]
    public class VerifierController(
            IConfiguration configuration,
            IMemoryCache memoryCache,
            ILogger<VerifierController> log,
            IHttpClientFactory httpClientFactory) : Controller
    {
        private readonly IConfiguration _configuration = configuration;
        private readonly IMemoryCache _cache = memoryCache;
        private readonly ILogger<VerifierController> _log = log;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly string _apiKey = Environment.GetEnvironmentVariable("API-KEY");

        private readonly JsonSerializerOptions options = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        /// <summary>
        /// This method is called from the UI to initiate the presentation of the verifiable credential
        /// </summary>
        /// <returns>JSON object with the address to the presentation request and optionally a QR code and a state value which can be used to check on the response status</returns>
        [AllowAnonymous]
        [HttpGet("/api/verifier/presentation-request")]
        public async Task<ActionResult> PresentationRequest()
        {
            _log.LogTrace(this.Request.GetDisplayUrl());
            try
            {
                // The VC Request API is an authenticated API. We need to clientid and secret (or certificate) to create an access token which
                // needs to be sent as bearer to the VC Request API
                var (token, error, error_description) = await MsalAccessTokenHandler.GetAccessToken(_configuration);
                if (string.IsNullOrEmpty(token))
                {
                    _log.LogError($"failed to acquire accesstoken: {error} : {error_description}");
                    return BadRequest(new { error, error_description });
                }

                string url = $"{_configuration["VerifiedID:ApiEndpoint"]}createPresentationRequest";
                PresentationRequest request = CreatePresentationRequest();
                string faceCheck = this.Request.Query["faceCheck"];
                bool useFaceCheck = !string.IsNullOrWhiteSpace(faceCheck) && (faceCheck == "1" || faceCheck == "true");
                if (!HasFaceCheck(request) && (useFaceCheck || _configuration.GetValue("VerifiedID:useFaceCheck", false)))
                {
                    AddFaceCheck(request, null);
                }

                string jsonString = JsonSerializer.Serialize(request, options);

                _log.LogTrace($"Request API payload: {jsonString}");
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                HttpResponseMessage res = await client.PostAsync(url, new StringContent(jsonString, Encoding.UTF8, "application/json"));
                string response = await res.Content.ReadAsStringAsync();
                HttpStatusCode statusCode = res.StatusCode;

                if (statusCode == HttpStatusCode.Created)
                {
                    _log.LogTrace("Successfully called Request Service API");
                    JsonObject requestConfig = JsonNode.Parse(response).AsObject();
                    requestConfig.Add("id", request.Callback.State);
                    jsonString = JsonSerializer.Serialize(requestConfig);

                    // We use in-memory cache to keep state about the request. The UI will check the state when calling the presentationResponse method
                    var cacheData = new
                    {
                        status = "request_created",
                        message = "Waiting for QR code to be scanned",
                        expiry = requestConfig["expiry"].ToString()
                    };
                    _cache.Set(request.Callback.State, JsonSerializer.Serialize(cacheData), DateTimeOffset.Now.AddSeconds(_configuration.GetValue("AppSettings:CacheExpiresInSeconds", 300)));
                    // The response from the VC Request API call is returned to the caller (the UI). It contains the URI to the request which Authenticator can download after
                    // it has scanned the QR code. If the payload requested the VC Request service to create the QR code that is returned as well
                    // the JavaScript in the UI will use that QR code to display it on the screen to the user.
                    return new ContentResult { ContentType = "application/json", Content = jsonString };
                }
                else
                {
                    _log.LogError("Error calling Verified ID API: " + response);
                    return BadRequest(new { error = "400", error_description = "Verified ID API error response: " + response, request = jsonString });
                }
            }
            catch (Exception ex)
            {
                _log.LogError("Exception: " + ex.Message);
                return BadRequest(new { error = "400", error_description = "Exception: " + ex.Message });
            }
        }

        private string GetRequestHostName()
        {
            string scheme = "https";
            string originalHost = this.Request.Headers["x-original-host"];
            string hostname;
            if (!string.IsNullOrEmpty(originalHost))
                hostname = string.Format("{0}://{1}", scheme, originalHost);
            else hostname = string.Format("{0}://{1}", scheme, this.Request.Host);
            return hostname;
        }

        private PresentationRequest CreatePresentationRequest(string stateId = null, string credentialType = null)
        {
            PresentationRequest request = new()
            {
                IncludeQRCode = _configuration.GetValue("VerifiedID:includeQRCode", false),
                Authority = _configuration["VerifiedID:DidAuthority"],
                Registration = new Registration()
                {
                    ClientName = _configuration["VerifiedID:client_name"],
                    Purpose = _configuration.GetValue("VerifiedID:purpose", "")
                },
                Callback = new Callback()
                {
                    Url = $"{GetRequestHostName()}/api/verifier/presentationcallback",
                    State = (string.IsNullOrWhiteSpace(stateId) ? Guid.NewGuid().ToString() : stateId),
                    Headers = new Dictionary<string, string>() { { "api-key", this._apiKey } }
                },
                IncludeReceipt = _configuration.GetValue("VerifiedID:includeReceipt", false),
                RequestedCredentials = [],
            };
            if ("" == request.Registration.Purpose)
            {
                request.Registration.Purpose = null;
            }
            if (string.IsNullOrEmpty(credentialType))
            {
                credentialType = _configuration["VerifiedID:CredentialType"];
            }

            bool allowRevoked = true;
            bool validateLinkedDomain = false;
            AddRequestedCredential(request, credentialType, allowRevoked, validateLinkedDomain);
            return request;
        }

        private static PresentationRequest AddRequestedCredential(
            PresentationRequest request,
            string credentialType,
            bool allowRevoked = false,
            bool validateLinkedDomain = true)
        {
            request.RequestedCredentials.Add(new RequestedCredential()
            {
                Type = credentialType,
                AcceptedIssuers = [],
                Configuration = new Configuration()
                {
                    Validation = new Validation()
                    {
                        AllowRevoked = allowRevoked,
                        ValidateLinkedDomain = validateLinkedDomain
                    }
                }
            });
            return request;
        }

        private PresentationRequest AddFaceCheck(PresentationRequest request, string credentialType, string sourcePhotoClaimName = "photo", int matchConfidenceThreshold = 70)
        {
            if (string.IsNullOrWhiteSpace(sourcePhotoClaimName))
            {
                sourcePhotoClaimName = _configuration.GetValue("VerifiedID:PhotoClaimName", "photo");
            }
            foreach (var requestedCredential in request.RequestedCredentials)
            {
                if (null == credentialType || requestedCredential.Type == credentialType)
                {
                    requestedCredential.Configuration.Validation.FaceCheck = new FaceCheck() { SourcePhotoClaimName = sourcePhotoClaimName, MatchConfidenceThreshold = matchConfidenceThreshold };
                    request.IncludeReceipt = false; // not supported while doing faceCheck
                }
            }
            return request;
        }

        private static bool HasFaceCheck(PresentationRequest request)
        {
            foreach (var requestedCredential in request.RequestedCredentials)
            {
                if (null != requestedCredential.Configuration.Validation.FaceCheck)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
