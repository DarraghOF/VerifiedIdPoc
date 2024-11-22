using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
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
    [Route("api/[action]")]
    [ApiController]
    public class CallbackController(IConfiguration configuration, IMemoryCache memoryCache, ILogger<CallbackController> log) : Controller
    {
        private enum RequestType
        {
            Unknown,
            Presentation,
            Issuance,
            Selfie
        };

        private readonly IConfiguration _configuration = configuration;
        private readonly IMemoryCache _cache = memoryCache;
        private readonly ILogger<CallbackController> _log = log;
        private readonly string _apiKey = Environment.GetEnvironmentVariable("API-KEY");

        private readonly JsonSerializerOptions options = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private async Task<ActionResult> HandleRequestCallback(RequestType requestType, string body)
        {
            try
            {
                this.Request.Headers.TryGetValue("api-key", out var apiKey);
                if (requestType != RequestType.Selfie && this._apiKey != apiKey)
                {
                    _log.LogTrace("api-key wrong or missing");
                    return new ContentResult() { StatusCode = (int)HttpStatusCode.Unauthorized, Content = "api-key wrong or missing" };
                }
                if (body == null)
                {
                    body = await new System.IO.StreamReader(this.Request.Body).ReadToEndAsync();
                    _log.LogTrace(body);
                }

                bool rc = false;
                string errorMessage = null;
                List<string> presentationStatus = ["request_retrieved", "presentation_verified", "presentation_error"];
                List<string> issuanceStatus = ["request_retrieved", "issuance_successful", "issuance_error"];
                List<string> selfieStatus = ["selfie_taken"];

                CallbackEvent callback = JsonSerializer.Deserialize<CallbackEvent>(body);

                if ((requestType == RequestType.Presentation && presentationStatus.Contains(callback.RequestStatus))
                    || (requestType == RequestType.Issuance && issuanceStatus.Contains(callback.RequestStatus))
                    || (requestType == RequestType.Selfie && selfieStatus.Contains(callback.RequestStatus)))
                {
                    if (!_cache.TryGetValue(callback.State, out string requestState))
                    {
                        errorMessage = $"Invalid state '{callback.State}'";
                    }
                    else
                    {
                        JsonObject reqState = JsonNode.Parse(requestState).AsObject();
                        reqState["status"] = callback.RequestStatus;
                        if (reqState.ContainsKey("callback"))
                        {
                            reqState["callback"] = body;
                        }
                        else
                        {
                            reqState.Add("callback", body);
                        }
                        _cache.Set(callback.State, JsonSerializer.Serialize(reqState)
                            , DateTimeOffset.Now.AddSeconds(_configuration.GetValue<int>("AppSettings:CacheExpiresInSeconds", 300)));
                        rc = true;
                    }
                }
                else
                {
                    errorMessage = $"Unknown request status '{callback.RequestStatus}'";
                }
                if (!rc)
                {
                    return BadRequest(new { error = "400", error_description = errorMessage });
                }
                return new OkResult();
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "400", error_description = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpPost("/api/issuer/issuecallback")]
        public async Task<ActionResult> IssuanceCallback()
        {
            _log.LogTrace(this.Request.GetDisplayUrl());
            return await HandleRequestCallback(RequestType.Issuance, null);
        }

        [AllowAnonymous]
        [HttpPost("/api/verifier/presentationcallback")]
        public async Task<ActionResult> PresentationCallback()
        {
            _log.LogTrace(this.Request.GetDisplayUrl());
            return await HandleRequestCallback(RequestType.Presentation, null);
        }

        [AllowAnonymous]
        [HttpGet("/api/request-status")]
        public ActionResult RequestStatus()
        {
            _log.LogTrace(this.Request.GetDisplayUrl());
            try
            {
                if (!PollRequestStatus(out JsonObject response))
                {
                    return BadRequest(new { error = "400", error_description = JsonSerializer.Serialize(response) });
                }
                return new ContentResult { ContentType = "application/json", Content = JsonSerializer.Serialize(response) };
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "400", error_description = ex.Message });
            }
        }

        public bool PollRequestStatus(out JsonObject result)
        {
            string state = this.Request.Query["id"];
            if (string.IsNullOrEmpty(state))
            {
                result = new JsonObject
                {
                    ["status"] = "error",
                    ["message"] = "Missing argument 'id'"
                };
                return false;
            }
            bool rc = true;
            if (_cache.TryGetValue(state, out string requestState))
            {
                JsonObject reqState = JsonNode.Parse(requestState).AsObject();
                string requestStatus = reqState["status"].ToString();
                CallbackEvent callback;
                switch (requestStatus)
                {
                    case "request_created":
                        result = new JsonObject
                        {
                            ["status"] = requestStatus,
                            ["message"] = "Waiting to scan QR code"
                        };
                        break;
                    case "request_retrieved":
                        result = new JsonObject
                        {
                            ["status"] = requestStatus,
                            ["message"] = "QR code is scanned. Waiting for user action..."
                        };
                        break;
                    case "issuance_error":
                        callback = JsonSerializer.Deserialize<CallbackEvent>(reqState["callback"].ToString());
                        result = new JsonObject
                        {
                            ["status"] = requestStatus,
                            ["message"] = "Issuance failed: " + callback.Error.Message
                        };
                        break;
                    case "issuance_successful":
                        result = new JsonObject
                        {
                            ["status"] = requestStatus,
                            ["message"] = "Issuance successful"
                        };
                        break;
                    case "presentation_error":
                        callback = JsonSerializer.Deserialize<CallbackEvent>(reqState["callback"].ToString());
                        result = new JsonObject
                        {
                            ["status"] = requestStatus,
                            ["message"] = "Presentation failed: " + callback.Error.Message
                        };
                        break;
                    case "presentation_verified":
                        callback = JsonSerializer.Deserialize<CallbackEvent>(reqState["callback"].ToString());
                        JsonObject resp = JsonNode.Parse(JsonSerializer.Serialize(new
                        {
                            status = requestStatus,
                            message = "Presentation verified",
                            type = callback.VerifiedCredentialsData[0].Type,
                            claims = callback.VerifiedCredentialsData[0].Claims,
                            subject = callback.Subject,
                            payload = callback.VerifiedCredentialsData,
                        }, options)).AsObject();
                        if (!string.IsNullOrWhiteSpace(callback.VerifiedCredentialsData[0].ExpirationDate))
                        {
                            resp.Add("expirationDate", callback.VerifiedCredentialsData[0].ExpirationDate);
                        }
                        if (!string.IsNullOrWhiteSpace(callback.VerifiedCredentialsData[0].IssuanceDate))
                        {
                            resp.Add("issuanceDate", callback.VerifiedCredentialsData[0].IssuanceDate);
                        }
                        result = resp;
                        break;
                    case "selfie_taken":
                        callback = JsonSerializer.Deserialize<CallbackEvent>(reqState["callback"].ToString());
                        result = new JsonObject
                        {
                            ["status"] = requestStatus,
                            ["message"] = "Selfie taken",
                            ["photo"] = callback.Photo
                        };
                        break;
                    default:
                        result = new JsonObject
                        {
                            ["status"] = "error",
                            ["message"] = $"Invalid requestStatus '{requestStatus}'"
                        };
                        rc = false;
                        break;
                }
            }
            else
            {
                result = new JsonObject
                {
                    ["status"] = "request_not_created",
                    ["message"] = "No data"
                };
                rc = false;
            }
            return rc;
        }

        [AllowAnonymous]
        [HttpPost("/api/issuer/selfie/{id}")]
        public async Task<ActionResult> SetSelfie(string id)
        {
            _log.LogTrace(this.Request.GetDisplayUrl());
            try
            {
                string body = await new StreamReader(this.Request.Body).ReadToEndAsync();
                _log.LogTrace(body);
                string dataImage = "data:image/jpeg;base64,";
                int idx = body.IndexOf(";base64,");
                if (idx == -1)
                {
                    return BadRequest(new { error = "400", error_description = $"Image must be {dataImage}" });
                }
                string photo = body[(idx + 8)..];
                CallbackEvent callback = new()
                {
                    RequestId = id,
                    State = id,
                    RequestStatus = "selfie_taken",
                    Photo = photo
                };
                return await HandleRequestCallback(RequestType.Selfie, JsonSerializer.Serialize(callback));
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "400", error_description = ex.Message });
            }
        }
    }
}
