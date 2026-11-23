using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MemberService.Data;
using MemberDTO = MemberService.DTOs;
using MemberSVC = MemberService.Services;
using ClaimsDTO = ClaimsIngestionService.DTOs;
using ClaimsSVC = ClaimsIngestionService.Services;
using ClaimsDATA = ClaimsIngestionService.Data;
using Xunit;

namespace IntegrationTests;

public class TenantCIsolationTests
{
    [Fact]
    public async Task OnboardedTenantC_CannotAccess_TenantA_Or_TenantB_Data()
    {
        // =========================================================================
        // 1. SEED DATA FOR TENANT-A AND TENANT-B
        // =========================================================================
        var dbName = "IsolationValidationDb_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<MemberDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        // Seed Tenant-A Member
        var contextA = new MemberSVC.TenantContext();
        contextA.SetTenant("tenant-a", "corr-a");
        using (var dbA = new MemberDbContext(options, contextA))
        {
            var svcA = new MemberSVC.MemberManagementService(dbA, contextA, NullLogger<MemberSVC.MemberManagementService>.Instance);
            await svcA.EnrollMemberAsync(new MemberDTO.EnrollMemberRequest
            {
                MemberId = "MEM-A-001",
                FirstName = "Alpha",
                LastName = "Subscriber",
                DateOfBirth = new DateTime(1980, 5, 10),
                Gender = "Male",
                PlanCode = "ALPHA-GOLD",
                CoverageStartDate = new DateTime(2026, 1, 1)
            });
        }

        // Seed Tenant-B Member
        var contextB = new MemberSVC.TenantContext();
        contextB.SetTenant("tenant-b", "corr-b");
        using (var dbB = new MemberDbContext(options, contextB))
        {
            var svcB = new MemberSVC.MemberManagementService(dbB, contextB, NullLogger<MemberSVC.MemberManagementService>.Instance);
            await svcB.EnrollMemberAsync(new MemberDTO.EnrollMemberRequest
            {
                MemberId = "MEM-B-001",
                FirstName = "Beta",
                LastName = "Subscriber",
                DateOfBirth = new DateTime(1985, 8, 20),
                Gender = "Female",
                PlanCode = "BETA-SILVER",
                CoverageStartDate = new DateTime(2026, 1, 1)
            });
        }

        // Seed Claims in Claims Repository
        var claimRepo = new ClaimsDATA.InMemoryClaimRepository(NullLogger<ClaimsDATA.InMemoryClaimRepository>.Instance);
        var claimValidator = new ClaimsSVC.JsonSchemaClaimValidator();
        var eventPublisher = new ClaimsSVC.InMemoryEventPublisher(NullLogger<ClaimsSVC.InMemoryEventPublisher>.Instance);

        var claimsContextA = new ClaimsSVC.TenantContext();
        claimsContextA.SetTenant("tenant-a", "corr-a");
        var claimsManagerA = new ClaimsSVC.ClaimsIngestionManager(claimRepo, claimValidator, eventPublisher, claimsContextA, NullLogger<ClaimsSVC.ClaimsIngestionManager>.Instance);

        await claimsManagerA.IngestClaimAsync(new ClaimsDTO.SubmitClaimRequest
        {
            ClaimId = "CLM-A-999",
            TenantId = "tenant-a",
            MemberId = "MEM-A-001",
            ProviderId = "PRV-100",
            ClaimAmount = 1500.00m,
            ClaimStatus = "Submitted",
            ServiceDate = "2026-11-15"
        });

        // =========================================================================
        // 2. NEWLY ONBOARDED TENANT-C ATTEMPTS TO ACCESS TENANT-A & B
        // =========================================================================
        var contextC = new MemberSVC.TenantContext();
        contextC.SetTenant("tenant-c", "corr-c");

        using (var dbC = new MemberDbContext(options, contextC))
        {
            var svcC = new MemberSVC.MemberManagementService(dbC, contextC, NullLogger<MemberSVC.MemberManagementService>.Instance);

            // Attempt 1: Query Tenant-A member
            var memberAQuery = await svcC.GetMemberByIdAsync("MEM-A-001");
            memberAQuery.Should().BeNull("Tenant-C must NEVER see Tenant-A member data!");

            // Attempt 2: Query Tenant-B member
            var memberBQuery = await svcC.GetMemberByIdAsync("MEM-B-001");
            memberBQuery.Should().BeNull("Tenant-C must NEVER see Tenant-B member data!");

            // Attempt 3: List all members
            var allMembersC = await svcC.GetMembersAsync();
            allMembersC.Should().BeEmpty("Tenant-C query must return 0 records from other tenants!");

            // Enroll Tenant-C's own member
            var memberC = await svcC.EnrollMemberAsync(new MemberDTO.EnrollMemberRequest
            {
                MemberId = "MEM-C-001",
                FirstName = "Charlie",
                LastName = "Horizon",
                DateOfBirth = new DateTime(1992, 11, 1),
                Gender = "Other",
                PlanCode = "HORIZON-PLATINUM",
                CoverageStartDate = new DateTime(2026, 1, 1)
            });

            memberC.Should().NotBeNull();
            memberC.TenantId.Should().Be("tenant-c");

            // Query Tenant-C's own member
            var ownMember = await svcC.GetMemberByIdAsync("MEM-C-001");
            ownMember.Should().NotBeNull();
            ownMember!.MemberId.Should().Be("MEM-C-001");
        }

        // =========================================================================
        // 3. CLAIMS REPOSITORY ISOLATION FOR TENANT-C
        // =========================================================================
        var claimsContextC = new ClaimsSVC.TenantContext();
        claimsContextC.SetTenant("tenant-c", "corr-c");
        var claimsManagerC = new ClaimsSVC.ClaimsIngestionManager(claimRepo, claimValidator, eventPublisher, claimsContextC, NullLogger<ClaimsSVC.ClaimsIngestionManager>.Instance);

        // Attempt 1: Read Tenant-A's claim
        var claimAQuery = await claimsManagerC.GetClaimByIdAsync("CLM-A-999");
        claimAQuery.Should().BeNull("Tenant-C must NEVER be able to read Tenant-A's claim!");

        // Attempt 2: Cross-tenant claim injection (payload tenant-a under tenant-c session)
        var crossTenantSpoofRequest = new ClaimsDTO.SubmitClaimRequest
        {
            ClaimId = "CLM-SPOOF-999",
            TenantId = "tenant-a",
            MemberId = "MEM-A-001",
            ProviderId = "PRV-100",
            ClaimAmount = 500.00m,
            ClaimStatus = "Submitted",
            ServiceDate = "2026-11-15"
        };

        var act = async () => await claimsManagerC.IngestClaimAsync(crossTenantSpoofRequest);
        await act.Should().ThrowAsync<UnauthorizedAccessException>("Cross-tenant payload submission must be rejected!");
    }
}
