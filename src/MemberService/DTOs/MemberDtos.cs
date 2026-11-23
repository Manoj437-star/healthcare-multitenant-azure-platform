using System.ComponentModel.DataAnnotations;

namespace MemberService.DTOs;

public class EnrollMemberRequest
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string MemberId { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    public DateTime DateOfBirth { get; set; }

    [Required]
    [RegularExpression("^(Male|Female|Other|Unknown)$", ErrorMessage = "Gender must be Male, Female, Other, or Unknown")]
    public string Gender { get; set; } = "Unknown";

    [Required]
    [StringLength(50)]
    public string PlanCode { get; set; } = string.Empty;

    [Required]
    public DateTime CoverageStartDate { get; set; }

    public DateTime? CoverageEndDate { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    [Phone]
    public string? PhoneNumber { get; set; }
}

public class MemberResponse
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CoverageStartDate { get; set; }
    public DateTime? CoverageEndDate { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
