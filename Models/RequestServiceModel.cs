using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AspNetCoreVerifiableCredentials.Models
{
    public class PresentationRequest
    {
        public string Authority { get; set; }

        public bool IncludeQRCode { get; set; }

        public Registration Registration { get; set; }

        public Callback Callback { get; set; }

        public bool IncludeReceipt { get; set; }

        public List<RequestedCredential> RequestedCredentials { get; set; }
    }

    public class Configuration
    {
        public Validation Validation { get; set; }
    }

    public class Validation
    {
        public bool AllowRevoked { get; set; }

        public bool ValidateLinkedDomain { get; set; }

        public FaceCheck FaceCheck { get; set; }
    }

    public class FaceCheck
    {
        public string SourcePhotoClaimName { get; set; }

        public int MatchConfidenceThreshold { get; set; }
    }

    public class Registration
    {
        public string ClientName { get; set; }

        public string Purpose { get; set; }
    }

    public class Callback
    {
        public string Url { get; set; }

        public string State { get; set; }

        public Dictionary<string, string> Headers { get; set; }
    }

    public class RequestedCredential
    {
        public string Type { get; set; }

        public List<string> AcceptedIssuers { get; set; }

        public Configuration Configuration { get; set; }

        public List<Constraint> Constraints { get; set; }

    }

    public class Constraint
    {
        public string ClaimName { get; set; }

        public List<string> Values { get; set; }

        public string Contains { get; set; }

        public string StartsWith { get; set; }
    }

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

    public class Error
    {
        public string Code { get; set; }

        public string Message { get; set; }
    }

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
