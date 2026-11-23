using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ClaimsIngestionService.DTOs;

public class SubmitClaimRequest
{
    [Required]
    [JsonPropertyName("claimId")]
    public string ClaimId { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("memberId")]
    public string MemberId { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("providerId")]
    public string ProviderId { get; set; } = string.Empty;

    [Required]
    [Range(0.01, 10000000.00)]
    [JsonPropertyName("claimAmount")]
    public decimal ClaimAmount { get; set; }

    [JsonPropertyName("claimStatus")]
    public string ClaimStatus { get; set; } = "Submitted";

    [Required]
    [JsonPropertyName("serviceDate")]
    public string ServiceDate { get; set; } = string.Empty; // Format YYYY-MM-DD

    [JsonPropertyName("diagnosisCodes")]
    public List<string>? DiagnosisCodes { get; set; }

    [JsonPropertyName("procedureCodes")]
    public List<string>? ProcedureCodes { get; set; }
}

public class ClaimResponse
{
    public string ClaimId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public decimal ClaimAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string ClaimStatus { get; set; } = string.Empty;
    public string ServiceDate { get; set; } = string.Empty;
    public List<string> DiagnosisCodes { get; set; } = new();
    public List<string> ProcedureCodes { get; set; } = new();
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; set; }
}

public class ClaimReceivedEvent
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public string EventType { get; set; } = "ClaimReceived";
    public string TenantId { get; set; } = string.Empty;
    public string ClaimId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public decimal ClaimAmount { get; set; }
    public string ClaimStatus { get; set; } = "Submitted";
    public string ServiceDate { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}

public class MemberEnrolledEvent
{
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CoverageStartDate { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}
