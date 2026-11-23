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

public class EndToEndFlowTests
{
    [Fact]
    public async Task EndToEnd_MemberEnrollment_ClaimsIngestion_And_CrossTenantIsolation_Verification()
    {
        // ==========================================
        // 1. SETUP TENANT-A MEMBER SERVICE CONTEXT
        // ==========================================
        var sharedDbName = "IntegrationTestDb_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<MemberDbContext>()
            .UseInMemoryDatabase(sharedDbName)
            .Options;

        var tenantAContext = new MemberSVC.TenantContext();
        tenantAContext.SetTenant("tenant-a", "corr-integration-001");

        using var memberDbContextA = new MemberDbContext(options, tenantAContext);
        var memberServiceA = new MemberSVC.MemberManagementService(memberDbContextA, tenantAContext, NullLogger<MemberSVC.MemberManagementService>.Instance);

        // Enroll member in Tenant A
        var enrollRequestA = new MemberDTO.EnrollMemberRequest
        {
            MemberId = "MEM-1001",
            FirstName = "Alice",
            LastName = "Johnson",
            DateOfBirth = new DateTime(1988, 3, 15),
            Gender = "Female",
            PlanCode = "PLATINUM-HMO",
            CoverageStartDate = new DateTime(2026, 1, 1),
            Email = "alice.johnson@example.com"
        };

        var enrolledMemberA = await memberServiceA.EnrollMemberAsync(enrollRequestA);
        enrolledMemberA.Should().NotBeNull();
        enrolledMemberA.MemberId.Should().Be("MEM-1001");
        enrolledMemberA.TenantId.Should().Be("tenant-a");

        // Verify outbox message generated
        var outboxMessage = await memberDbContextA.OutboxMessages.FirstOrDefaultAsync(o => o.TenantId == "tenant-a");
        outboxMessage.Should().NotBeNull();
        outboxMessage!.Status.Should().Be("Pending");
        outboxMessage.Payload.Should().Contain("MEM-1001");

        // ==========================================
        // 2. SETUP CLAIMS INGESTION SERVICE CONTEXT
        // ==========================================
        var claimRepo = new ClaimsDATA.InMemoryClaimRepository(NullLogger<ClaimsDATA.InMemoryClaimRepository>.Instance);
        var claimValidator = new ClaimsSVC.JsonSchemaClaimValidator();
        var claimsEventPublisher = new ClaimsSVC.InMemoryEventPublisher(NullLogger<ClaimsSVC.InMemoryEventPublisher>.Instance);

        var claimsTenantContextA = new ClaimsSVC.TenantContext();
        claimsTenantContextA.SetTenant("tenant-a", "corr-integration-001");

        var claimsManagerA = new ClaimsSVC.ClaimsIngestionManager(
            claimRepo,
            claimValidator,
            claimsEventPublisher,
            claimsTenantContextA,
            NullLogger<ClaimsSVC.ClaimsIngestionManager>.Instance
        );

        // Submit claim for Tenant A
        var claimRequestA = new ClaimsDTO.SubmitClaimRequest
        {
            ClaimId = "claim-1001",
            TenantId = "tenant-a",
            MemberId = enrolledMemberA.MemberId,
            ProviderId = "PRV-9876",
            ClaimAmount = 2500.00m,
            ClaimStatus = "Submitted",
            ServiceDate = "2026-11-18",
            DiagnosisCodes = new List<string> { "I10", "E11.9" },
            ProcedureCodes = new List<string> { "99213" }
        };

        var claimResponseA = await claimsManagerA.IngestClaimAsync(claimRequestA);
        claimResponseA.Should().NotBeNull();
        claimResponseA.ClaimId.Should().Be("claim-1001");
        claimResponseA.TenantId.Should().Be("tenant-a");
        claimResponseA.ClaimAmount.Should().Be(2500.00m);

        // ==========================================
        // 3. VERIFY STRICT TENANT ISOLATION (TENANT-B)
        // ==========================================
        // Tenant B queries member service
        var tenantBContext = new MemberSVC.TenantContext();
        tenantBContext.SetTenant("tenant-b", "corr-integration-002");

        using var memberDbContextB = new MemberDbContext(options, tenantBContext);
        var memberServiceB = new MemberSVC.MemberManagementService(memberDbContextB, tenantBContext, NullLogger<MemberSVC.MemberManagementService>.Instance);

        // Tenant B tries to query Tenant A's member
        var crossQueryMember = await memberServiceB.GetMemberByIdAsync("MEM-1001");
        crossQueryMember.Should().BeNull("Tenant B must never be able to access Tenant A's member data!");

        var tenantBAllMembers = await memberServiceB.GetMembersAsync();
        tenantBAllMembers.Should().BeEmpty();

        // Tenant B tries to query Tenant A's claim
        var claimsTenantContextB = new ClaimsSVC.TenantContext();
        claimsTenantContextB.SetTenant("tenant-b", "corr-integration-002");

        var claimsManagerB = new ClaimsSVC.ClaimsIngestionManager(
            claimRepo,
            claimValidator,
            claimsEventPublisher,
            claimsTenantContextB,
            NullLogger<ClaimsSVC.ClaimsIngestionManager>.Instance
        );

        var crossQueryClaim = await claimsManagerB.GetClaimByIdAsync("claim-1001");
        crossQueryClaim.Should().BeNull("Tenant B must never be able to access Tenant A's claim data!");

        // Tenant B tries to submit a claim injecting Tenant A's tenant ID
        var crossTenantAttackRequest = new ClaimsDTO.SubmitClaimRequest
        {
            ClaimId = "claim-spoof-001",
            TenantId = "tenant-a", // Attacker specifies tenant-a in payload while authenticated as tenant-b
            MemberId = "MEM-1001",
            ProviderId = "PRV-9876",
            ClaimAmount = 100.00m,
            ClaimStatus = "Submitted",
            ServiceDate = "2026-11-18"
        };

        var attackAct = async () => await claimsManagerB.IngestClaimAsync(crossTenantAttackRequest);
        await attackAct.Should().ThrowAsync<UnauthorizedAccessException>("Cross-tenant payload injection must be blocked!");
    }
}
