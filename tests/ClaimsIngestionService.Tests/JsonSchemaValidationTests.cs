using FluentAssertions;
using ClaimsIngestionService.DTOs;
using ClaimsIngestionService.Services;
using Xunit;

namespace ClaimsIngestionService.Tests;

public class JsonSchemaValidationTests
{
    private readonly JsonSchemaClaimValidator _validator = new();

    [Fact]
    public void ValidateClaim_ValidPayload_ReturnsTrue()
    {
        var claim = new SubmitClaimRequest
        {
            ClaimId = "claim-1001",
            TenantId = "tenant-a",
            MemberId = "member-1001",
            ProviderId = "provider-1001",
            ClaimAmount = 2500.00m,
            ClaimStatus = "Submitted",
            ServiceDate = "2026-11-18",
            DiagnosisCodes = new List<string> { "I10", "E11.9" },
            ProcedureCodes = new List<string> { "99213" }
        };

        var result = _validator.ValidateClaim(claim);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateClaim_NegativeAmount_ReturnsFalse()
    {
        var claim = new SubmitClaimRequest
        {
            ClaimId = "claim-1002",
            TenantId = "tenant-a",
            MemberId = "member-1001",
            ProviderId = "provider-1001",
            ClaimAmount = -50.00m,
            ClaimStatus = "Submitted",
            ServiceDate = "2026-11-18"
        };

        var result = _validator.ValidateClaim(claim);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void ValidateClaim_InvalidServiceDatePattern_ReturnsFalse()
    {
        var claim = new SubmitClaimRequest
        {
            ClaimId = "claim-1003",
            TenantId = "tenant-a",
            MemberId = "member-1001",
            ProviderId = "provider-1001",
            ClaimAmount = 150.00m,
            ClaimStatus = "Submitted",
            ServiceDate = "18/11/2026" // Invalid format, schema requires YYYY-MM-DD
        };

        var result = _validator.ValidateClaim(claim);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void ValidateClaim_FutureServiceDate_ReturnsFalse()
    {
        var claim = new SubmitClaimRequest
        {
            ClaimId = "claim-1004",
            TenantId = "tenant-a",
            MemberId = "member-1001",
            ProviderId = "provider-1001",
            ClaimAmount = 150.00m,
            ClaimStatus = "Submitted",
            ServiceDate = "2099-01-01" // Far in future
        };

        var result = _validator.ValidateClaim(claim);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("future"));
    }
}
