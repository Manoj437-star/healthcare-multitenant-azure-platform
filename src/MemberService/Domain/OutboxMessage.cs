namespace MemberService.Domain;

public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAtUtc { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Processed, Failed
    public int RetryCount { get; set; } = 0;
    public string? ErrorMessage { get; set; }
}
