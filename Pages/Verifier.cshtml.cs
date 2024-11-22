using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using AspNetCoreVerifiableCredentials.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace AspNetCoreVerifiableCredentials.Pages
{
    public class VerifierModel(IConfiguration configuration) : PageModel
    {
        private readonly IConfiguration _configuration = configuration;

        public void OnGet()
        {
            ViewData["Message"] = "";
            ViewData["CredentialType"] = _configuration["VerifiedID:CredentialType"];
            ViewData["acceptedIssuers"] = new string[] { _configuration["VerifiedID:DidAuthority"] };
            ViewData["useFaceCheck"] = false;
            ViewData["useConstraints"] = false;
            ViewData["constraintName"] = "";
            ViewData["constraintValue"] = "";
            ViewData["constraintOp"] = "value";

            if (this.Request.Query.ContainsKey("photoClaimName"))
            {
                ViewData["PhotoClaimName"] = this.Request.Query["photoClaimName"].ToString(); // could be empty/null for no-photo
            }
            else
            {
                ViewData["PhotoClaimName"] = _configuration.GetValue("VerifiedID:PhotoClaimName", "");
            }

            HttpContext.Session.Remove("presentationRequestTemplate");
            string templateLink = this.Request.Query["template"];
            string jsonTemplate = null;

            // URL?
            if (!string.IsNullOrWhiteSpace(templateLink) && templateLink.StartsWith("https://"))
            {
                HttpClient client = new();
                HttpResponseMessage res;
                try
                {
                    res = client.GetAsync(templateLink).Result;
                }
                catch (Exception ex)
                {
                    client.Dispose();
                    ViewData["Message"] = $"Error getting template link: {templateLink}. {ex.Message}";
                    return;
                }
                jsonTemplate = res.Content.ReadAsStringAsync().Result;
                client.Dispose();
                if (HttpStatusCode.OK != res.StatusCode)
                {
                    ViewData["Message"] = $"{res.StatusCode} - Template link not found: {templateLink}";
                    return;
                }
            }

            // local file?
            if (!string.IsNullOrWhiteSpace(templateLink)
                && (templateLink.StartsWith("file://") || templateLink.Substring(1, 2) == ":\\"))
            {
                if (templateLink.StartsWith("file://"))
                {
                    templateLink = templateLink[7..].Replace("/", "\\");
                }
                try
                {
                    jsonTemplate = System.IO.File.ReadAllText(templateLink);
                }
                catch (Exception ex)
                {
                    ViewData["Message"] = $"Error getting template link: {ex.Message}";
                }
            }

            if (!string.IsNullOrWhiteSpace(jsonTemplate))
            {
                PresentationRequest request;
                try
                {
                    request = JsonSerializer.Deserialize<PresentationRequest>(jsonTemplate);
                }
                catch (Exception ex)
                {
                    ViewData["Message"] = $"Error parsing template link: {templateLink}. {ex.Message}";
                    return;
                }
                if (request?.RequestedCredentials == null)
                {
                    ViewData["Message"] = $"Template link is not a presentation request: {templateLink}.";
                    return;
                }
                if (string.IsNullOrWhiteSpace(request.RequestedCredentials[0].Type))
                {
                    ViewData["Message"] = $"Template link does not have a credential type: {templateLink}.";
                    return;
                }
                ViewData["CredentialType"] = request.RequestedCredentials[0].Type;
                ViewData["acceptedIssuers"] = request.RequestedCredentials[0].AcceptedIssuers.ToArray();

                // template uses FaceCheck?
                if (request.RequestedCredentials[0].Configuration?.Validation?.FaceCheck != null)
                {
                    ViewData["useFaceCheck"] = true;
                    ViewData["PhotoClaimName"] = request.RequestedCredentials[0].Configuration.Validation.FaceCheck.SourcePhotoClaimName;
                }

                // template uses constraints?
                if (request.RequestedCredentials[0].Constraints != null)
                {
                    ViewData["useConstraints"] = true;
                    ViewData["constraintName"] = request.RequestedCredentials[0].Constraints[0].ClaimName;
                    if (request.RequestedCredentials[0].Constraints[0].Values != null)
                    {
                        ViewData["constraintOp"] = "value";
                        ViewData["constraintValue"] = string.Join(";", request.RequestedCredentials[0].Constraints[0].Values);
                    }
                    if (request.RequestedCredentials[0].Constraints[0].Contains != null)
                    {
                        ViewData["constraintOp"] = "contains";
                        ViewData["constraintValue"] = request.RequestedCredentials[0].Constraints[0].Contains;
                    }
                    if (request.RequestedCredentials[0].Constraints[0].StartsWith != null)
                    {
                        ViewData["constraintOp"] = "startsWith";
                        ViewData["constraintValue"] = request.RequestedCredentials[0].Constraints[0].StartsWith;
                    }
                }
                HttpContext.Session.SetString("presentationRequestTemplate", jsonTemplate);
            }
        }
    }
}
