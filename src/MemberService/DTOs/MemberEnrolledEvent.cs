namespace MemberService.DTOs;

public class MemberEnrolledEvent
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public string EventType { get; set; } = "MemberEnrolled";
    public string TenantId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public DateTime CoverageStartDate { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
