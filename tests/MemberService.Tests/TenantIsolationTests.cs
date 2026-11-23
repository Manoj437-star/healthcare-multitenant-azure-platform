using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MemberService.Data;
using MemberService.Domain;
using MemberService.Services;
using Xunit;

namespace MemberService.Tests;

public class TenantIsolationTests
{
    [Fact]
    public async Task DbContext_EnforcesTenantIsolation_TenantCannotAccessOtherTenantData()
    {
        // Arrange
        var dbName = "SharedIsolatedDb_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<MemberDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        // Context 1: tenant-a context
        var tenantAMock = new Mock<ITenantContext>();
        tenantAMock.Setup(t => t.TenantId).Returns("tenant-a");
        tenantAMock.Setup(t => t.IsResolved).Returns(true);

        using (var contextA = new MemberDbContext(options, tenantAMock.Object))
        {
            contextA.Members.Add(new Member
            {
                MemberId = "MEM-TENANT-A-01",
                TenantId = "tenant-a",
                FirstName = "Alpha",
                LastName = "User",
                PlanCode = "PLAN-A",
                DateOfBirth = new DateTime(1980, 1, 1),
                CoverageStartDate = new DateTime(2026, 1, 1)
            });
            await contextA.SaveChangesAsync();
        }

        // Context 2: tenant-b context
        var tenantBMock = new Mock<ITenantContext>();
        tenantBMock.Setup(t => t.TenantId).Returns("tenant-b");
        tenantBMock.Setup(t => t.IsResolved).Returns(true);

        using (var contextB = new MemberDbContext(options, tenantBMock.Object))
        {
            contextB.Members.Add(new Member
            {
                MemberId = "MEM-TENANT-B-01",
                TenantId = "tenant-b",
                FirstName = "Beta",
                LastName = "User",
                PlanCode = "PLAN-B",
                DateOfBirth = new DateTime(1982, 2, 2),
                CoverageStartDate = new DateTime(2026, 1, 1)
            });
            await contextB.SaveChangesAsync();
        }

        // Act & Assert for Tenant A querying database
        using (var queryContextA = new MemberDbContext(options, tenantAMock.Object))
        {
            var membersA = await queryContextA.Members.ToListAsync();
            membersA.Should().HaveCount(1);
            membersA.Single().MemberId.Should().Be("MEM-TENANT-A-01");
            membersA.Single().TenantId.Should().Be("tenant-a");

            // Direct query for tenant-b's member must return null
            var crossTenantMember = await queryContextA.Members.FirstOrDefaultAsync(m => m.MemberId == "MEM-TENANT-B-01");
            crossTenantMember.Should().BeNull();
        }

        // Act & Assert for Tenant B querying database
        using (var queryContextB = new MemberDbContext(options, tenantBMock.Object))
        {
            var membersB = await queryContextB.Members.ToListAsync();
            membersB.Should().HaveCount(1);
            membersB.Single().MemberId.Should().Be("MEM-TENANT-B-01");
            membersB.Single().TenantId.Should().Be("tenant-b");

            // Direct query for tenant-a's member must return null
            var crossTenantMember = await queryContextB.Members.FirstOrDefaultAsync(m => m.MemberId == "MEM-TENANT-A-01");
            crossTenantMember.Should().BeNull();
        }
    }
}
