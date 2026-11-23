using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ClaimsIngestionService.Data;
using ClaimsIngestionService.Domain;
using ClaimsIngestionService.DTOs;
using ClaimsIngestionService.Services;
using Xunit;

namespace ClaimsIngestionService.Tests;

public class ClaimsIngestionServiceTests
{
    private readonly Mock<IClaimRepository> _repositoryMock;
    private readonly Mock<IClaimValidator> _validatorMock;
    private readonly Mock<IEventPublisher> _publisherMock;
    private readonly Mock<ITenantContext> _tenantContextMock;
    private readonly Mock<ILogger<ClaimsIngestionManager>> _loggerMock;
    private readonly ClaimsIngestionManager _manager;

    public ClaimsIngestionServiceTests()
    {
        _repositoryMock = new Mock<IClaimRepository>();
        _validatorMock = new Mock<IClaimValidator>();
        _publisherMock = new Mock<IEventPublisher>();
        _tenantContextMock = new Mock<ITenantContext>();
        _loggerMock = new Mock<ILogger<ClaimsIngestionManager>>();

        _manager = new ClaimsIngestionManager(
            _repositoryMock.Object,
            _validatorMock.Object,
            _publisherMock.Object,
            _tenantContextMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task IngestClaimAsync_ValidClaim_PersistsAndPublishesEvent()
    {
        // Arrange
        _tenantContextMock.Setup(t => t.TenantId).Returns("tenant-a");
        _tenantContextMock.Setup(t => t.CorrelationId).Returns("corr-claims-001");

        var request = new SubmitClaimRequest
        {
            ClaimId = "claim-1001",
            TenantId = "tenant-a",
            MemberId = "member-1001",
            ProviderId = "provider-1001",
            ClaimAmount = 2500m,
            ClaimStatus = "Submitted",
            ServiceDate = "2026-11-18"
        };

        _validatorMock.Setup(v => v.ValidateClaim(request))
            .Returns(new ValidationResult { IsValid = true });

        _repositoryMock.Setup(r => r.IsClaimDuplicateAsync("tenant-a", "claim-1001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var response = await _manager.IngestClaimAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.ClaimId.Should().Be("claim-1001");
        response.TenantId.Should().Be("tenant-a");
        response.ClaimAmount.Should().Be(2500m);

        _repositoryMock.Verify(r => r.SaveClaimAsync(
            It.Is<Claim>(c => c.ClaimId == "claim-1001" && c.TenantId == "tenant-a"),
            It.IsAny<CancellationToken>()), Times.Once);

        _publisherMock.Verify(p => p.PublishAsync(
            "claim-events",
            It.Is<ClaimReceivedEvent>(e => e.ClaimId == "claim-1001" && e.TenantId == "tenant-a"),
            "tenant-a",
            "corr-claims-001",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestClaimAsync_TenantMismatchBetweenPayloadAndContext_ThrowsUnauthorizedAccessException()
    {
        // Arrange: context is tenant-b, but payload specifies tenant-a
        _tenantContextMock.Setup(t => t.TenantId).Returns("tenant-b");
        _tenantContextMock.Setup(t => t.CorrelationId).Returns("corr-mismatch");

        var request = new SubmitClaimRequest
        {
            ClaimId = "claim-1001",
            TenantId = "tenant-a",
            MemberId = "member-1001",
            ProviderId = "provider-1001",
            ClaimAmount = 2500m,
            ClaimStatus = "Submitted",
            ServiceDate = "2026-11-18"
        };

        // Act & Assert
        var act = async () => await _manager.IngestClaimAsync(request);
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*tenant ID does not match authenticated request context*");

        _repositoryMock.Verify(r => r.SaveClaimAsync(It.IsAny<Claim>(), It.IsAny<CancellationToken>()), Times.Never);
        _publisherMock.Verify(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IngestClaimAsync_DuplicateClaimId_ThrowsInvalidOperationException()
    {
        // Arrange
        _tenantContextMock.Setup(t => t.TenantId).Returns("tenant-a");
        _tenantContextMock.Setup(t => t.CorrelationId).Returns("corr-dup");

        var request = new SubmitClaimRequest
        {
            ClaimId = "claim-dup",
            TenantId = "tenant-a",
            MemberId = "member-1001",
            ProviderId = "provider-1001",
            ClaimAmount = 500m,
            ClaimStatus = "Submitted",
            ServiceDate = "2026-11-18"
        };

        _validatorMock.Setup(v => v.ValidateClaim(request))
            .Returns(new ValidationResult { IsValid = true });

        _repositoryMock.Setup(r => r.IsClaimDuplicateAsync("tenant-a", "claim-dup", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act & Assert
        var act = async () => await _manager.IngestClaimAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already been submitted for tenant 'tenant-a'*");
    }
}
