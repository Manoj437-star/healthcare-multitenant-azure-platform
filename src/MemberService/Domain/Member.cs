namespace MemberService.Domain;

public class Member
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty; // Payer-assigned member identifier (e.g. MEM-1001)
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Active"; // Active, Suspended, Terminated
    public DateTime CoverageStartDate { get; set; }
    public DateTime? CoverageEndDate { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
