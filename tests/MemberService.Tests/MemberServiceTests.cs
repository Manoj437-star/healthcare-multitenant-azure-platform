using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MemberService.Data;
using MemberService.Domain;
using MemberService.DTOs;
using MemberService.Services;
using Xunit;

namespace MemberService.Tests;

public class MemberServiceTests
{
    private readonly Mock<ITenantContext> _tenantContextMock;
    private readonly Mock<ILogger<MemberManagementService>> _loggerMock;

    public MemberServiceTests()
    {
        _tenantContextMock = new Mock<ITenantContext>();
        _loggerMock = new Mock<ILogger<MemberManagementService>>();
    }

    private MemberDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<MemberDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new MemberDbContext(options, _tenantContextMock.Object);
    }

    [Fact]
    public async Task EnrollMemberAsync_ValidRequest_CreatesMemberAndStagedOutboxMessage()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        _tenantContextMock.Setup(t => t.TenantId).Returns("tenant-a");
        _tenantContextMock.Setup(t => t.CorrelationId).Returns("corr-12345");
        _tenantContextMock.Setup(t => t.IsResolved).Returns(true);

        using var dbContext = CreateInMemoryDbContext(dbName);
        var service = new MemberManagementService(dbContext, _tenantContextMock.Object, _loggerMock.Object);

        var request = new EnrollMemberRequest
        {
            MemberId = "MEM-1001",
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1985, 4, 12),
            Gender = "Male",
            PlanCode = "GOLD-PPO",
            CoverageStartDate = new DateTime(2026, 1, 1),
            CoverageEndDate = new DateTime(2026, 12, 31),
            Email = "john.doe@example.com"
        };

        // Act
        var result = await service.EnrollMemberAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.MemberId.Should().Be("MEM-1001");
        result.TenantId.Should().Be("tenant-a");

        var memberInDb = await dbContext.Members.FirstOrDefaultAsync(m => m.MemberId == "MEM-1001");
        memberInDb.Should().NotBeNull();
        memberInDb!.TenantId.Should().Be("tenant-a");

        var outboxInDb = await dbContext.OutboxMessages.FirstOrDefaultAsync(o => o.TenantId == "tenant-a");
        outboxInDb.Should().NotBeNull();
        outboxInDb!.EventType.Should().Be("MemberEnrolled");
        outboxInDb.Status.Should().Be("Pending");
        outboxInDb.Payload.Should().Contain("MEM-1001");
    }

    [Fact]
    public async Task EnrollMemberAsync_DuplicateMemberWithinTenant_ThrowsInvalidOperationException()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        _tenantContextMock.Setup(t => t.TenantId).Returns("tenant-a");
        _tenantContextMock.Setup(t => t.CorrelationId).Returns("corr-12345");
        _tenantContextMock.Setup(t => t.IsResolved).Returns(true);

        using var dbContext = CreateInMemoryDbContext(dbName);
        var service = new MemberManagementService(dbContext, _tenantContextMock.Object, _loggerMock.Object);

        var request = new EnrollMemberRequest
        {
            MemberId = "MEM-DUP",
            FirstName = "Alice",
            LastName = "Smith",
            DateOfBirth = new DateTime(1990, 1, 1),
            Gender = "Female",
            PlanCode = "SILVER-HMO",
            CoverageStartDate = new DateTime(2026, 1, 1)
        };

        await service.EnrollMemberAsync(request);

        // Act & Assert
        var act = async () => await service.EnrollMemberAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists for tenant 'tenant-a'*");
    }

    [Fact]
    public async Task EnrollMemberAsync_InvalidCoverageDates_ThrowsArgumentException()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        _tenantContextMock.Setup(t => t.TenantId).Returns("tenant-a");
        _tenantContextMock.Setup(t => t.CorrelationId).Returns("corr-12345");
        _tenantContextMock.Setup(t => t.IsResolved).Returns(true);

        using var dbContext = CreateInMemoryDbContext(dbName);
        var service = new MemberManagementService(dbContext, _tenantContextMock.Object, _loggerMock.Object);

        var request = new EnrollMemberRequest
        {
            MemberId = "MEM-DATE-ERR",
            FirstName = "Bob",
            LastName = "Jones",
            DateOfBirth = new DateTime(1992, 5, 20),
            Gender = "Male",
            PlanCode = "BRONZE-HDHP",
            CoverageStartDate = new DateTime(2026, 6, 1),
            CoverageEndDate = new DateTime(2026, 1, 1) // End date before start date
        };

        // Act & Assert
        var act = async () => await service.EnrollMemberAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Coverage end date cannot precede coverage start date*");
    }
}
