using System.Text.Json.Serialization;

namespace ClaimsIngestionService.Domain;

public class Claim
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty; // In Cosmos DB, id = $"{tenantId}_{claimId}"

    [JsonPropertyName("claimId")]
    public string ClaimId { get; set; } = string.Empty;

    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    [JsonPropertyName("memberId")]
    public string MemberId { get; set; } = string.Empty;

    [JsonPropertyName("providerId")]
    public string ProviderId { get; set; } = string.Empty;

    [JsonPropertyName("claimAmount")]
    public decimal ClaimAmount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "USD";

    [JsonPropertyName("claimStatus")]
    public string ClaimStatus { get; set; } = "Submitted"; // Submitted, Validated, Rejected, Pended

    [JsonPropertyName("serviceDate")]
    public string ServiceDate { get; set; } = string.Empty;

    [JsonPropertyName("diagnosisCodes")]
    public List<string> DiagnosisCodes { get; set; } = new();

    [JsonPropertyName("procedureCodes")]
    public List<string> ProcedureCodes { get; set; } = new();

    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; set; } = string.Empty;

    [JsonPropertyName("submittedAtUtc")]
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("etag")]
    public string? ETag { get; set; }
}

public class EnrolledMemberCache
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty; // $"{tenantId}_{memberId}"

    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    [JsonPropertyName("memberId")]
    public string MemberId { get; set; } = string.Empty;

    [JsonPropertyName("planCode")]
    public string PlanCode { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Active";

    [JsonPropertyName("coverageStartDate")]
    public DateTime CoverageStartDate { get; set; }

    [JsonPropertyName("cachedAtUtc")]
    public DateTime CachedAtUtc { get; set; } = DateTime.UtcNow;
}

public class IdempotentRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty; // $"{tenantId}_{key}"

    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    [JsonPropertyName("operationKey")]
    public string OperationKey { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Completed"; // Processing, Completed, Failed

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
