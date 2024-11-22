// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AspNetCoreVerifiableCredentials.Models
{
    public class IssuanceRequest
    {
        public string Authority { get; set; }

        public bool IncludeQRCode { get; set; }

        public Registration Registration { get; set; }

        public Callback Callback { get; set; }

        public string Type { get; set; }

        public string Manifest { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Pin Pin { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, object> Claims { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ExpirationDate { get; set; }
    }

    /// <summary>
    /// VC Presentation
    /// </summary>
    public class PresentationRequest
    {
        public string Authority { get; set; }

        public bool IncludeQRCode { get; set; }

        public Registration Registration { get; set; }

        public Callback Callback { get; set; }

        public bool IncludeReceipt { get; set; }

        public List<RequestedCredential> RequestedCredentials { get; set; }
    }

    /// <summary>
    /// Configuration - presentation validation configuration
    /// </summary>
    public class Configuration
    {
        public Validation Validation { get; set; }
    }

    /// <summary>
    /// Validation - presentation validation configuration
    /// </summary>
    public class Validation
    {
        public bool AllowRevoked { get; set; }

        public bool ValidateLinkedDomain { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public FaceCheck FaceCheck { get; set; }
    }

    /// <summary>
    /// FaceCheck - if to ask for face check and what claim + score you want
    /// </summary>
    public class FaceCheck
    {
        public string SourcePhotoClaimName { get; set; }

        public int MatchConfidenceThreshold { get; set; }
    }

    /// <summary>
    /// Registration - used in both issuance and presentation to give the app a display name
    /// </summary>
    public class Registration
    {
        public string ClientName { get; set; }

        public string Purpose { get; set; }
    }

    /// <summary>
    /// Callback - defines where and how we want our callback.
    /// url - points back to us
    /// state - something we pass that we get back in the callback event. We use it as a correlation id
    /// headers - any additional HTTP headers you want to pass to the VC Client API. 
    /// The values you pass will be returned, as HTTP Headers, in the callback
    public class Callback
    {
        public string Url { get; set; }

        public string State { get; set; }

        public Dictionary<string, string> Headers { get; set; }
    }

    /// <summary>
    /// Pin - if issuance involves the use of a pin code. The 'value' attribute is a string so you can have values like "0907"
    /// </summary>
    public class Pin
    {
        public string Value { get; set; }

        public int Length { get; set; }
    }

    /// <summary>
    /// Presentation can involve asking for multiple VCs
    /// </summary>
    public class RequestedCredential
    {
        public string Type { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> AcceptedIssuers { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Configuration Configuration { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<Constraint> Constraints { get; set; }

    }

    public class Constraint
    {
        public string ClaimName { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> Values { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Contains { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string StartsWith { get; set; }
    }

    /// <summary>
    /// VC Client API callback
    /// </summary>
    public class CallbackEvent
    {
        public string RequestId { get; set; }

        public string RequestStatus { get; set; }

        public Error Error { get; set; }

        public string State { get; set; }

        public string Subject { get; set; }

        public ClaimsIssuer[] VerifiedCredentialsData { get; set; }
        
        public string Photo { get; set; }

    }

    /// <summary>
    /// Error - in case the VC Client API returns an error
    /// </summary>
    public class Error
    {
        public string Code { get; set; }

        public string Message { get; set; }
    }

    /// <summary>
    /// ClaimsIssuer - details of each VC that was presented (usually just one)
    /// authority gives you who issued the VC and the claims is a collection of the VC's claims, like givenName, etc
    /// </summary>
    public class ClaimsIssuer
    {
        public string Issuer { get; set; }

        public string Domain { get; set; }

        public string Verified { get; set; }

        public string[] Type { get; set; }

        public IDictionary<string, string> Claims { get; set; }

        public CredentialState CredentialState { get; set; }

        public FaceCheckResult FaceCheck { get; set; }

        public DomainValidation DomainValidation { get; set; }

        public string ExpirationDate { get; set; }

        public string IssuanceDate { get; set; }
    }

    public class CredentialState
    {
        public string RevocationStatus { get; set; }

        [JsonIgnore]
        public bool IsValid { get { return RevocationStatus == "VALID"; } }
    }

    public class DomainValidation
    {
        public string Url { get; set; }
    }

    public class FaceCheckResult
    {
        public double MatchConfidenceScore { get; set; }
    }

}
