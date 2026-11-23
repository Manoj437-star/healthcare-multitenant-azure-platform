using ClaimsIngestionService.Data;
using ClaimsIngestionService.Domain;
using ClaimsIngestionService.DTOs;

namespace ClaimsIngestionService.Services;

public interface IClaimsService
{
    Task<ClaimResponse> IngestClaimAsync(SubmitClaimRequest request, CancellationToken cancellationToken = default);
    Task<ClaimResponse?> GetClaimByIdAsync(string claimId, CancellationToken cancellationToken = default);
}

public class ClaimsIngestionManager : IClaimsService
{
    private readonly IClaimRepository _repository;
    private readonly IClaimValidator _validator;
    private readonly IEventPublisher _eventPublisher;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ClaimsIngestionManager> _logger;

    public ClaimsIngestionManager(
        IClaimRepository repository,
        IClaimValidator validator,
        IEventPublisher eventPublisher,
        ITenantContext tenantContext,
        ILogger<ClaimsIngestionManager> logger)
    {
        _repository = repository;
        _validator = validator;
        _eventPublisher = eventPublisher;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<ClaimResponse> IngestClaimAsync(SubmitClaimRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        var correlationId = _tenantContext.CorrelationId;

        // 1. Cross-tenant injection check
        if (!string.Equals(request.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Security Violation: Request payload tenant '{PayloadTenant}' does not match context tenant '{ContextTenant}'. CorrelationId: {CorrelationId}",
                request.TenantId, tenantId, correlationId);
            throw new UnauthorizedAccessException("Claim payload tenant ID does not match authenticated request context.");
        }

        // 2. JSON Schema & domain validation
        var validation = _validator.ValidateClaim(request);
        if (!validation.IsValid)
        {
            var errors = string.Join("; ", validation.Errors);
            _logger.LogWarning("Validation failure for claim {ClaimId}: {Errors}", request.ClaimId, errors);
            throw new ArgumentException($"Claim validation failed: {errors}");
        }

        // 3. Idempotency & duplicate check
        var isDuplicate = await _repository.IsClaimDuplicateAsync(tenantId, request.ClaimId, cancellationToken);
        if (isDuplicate)
        {
            _logger.LogWarning("Duplicate claim submission detected for ClaimId '{ClaimId}' and tenant '{TenantId}'.",
                request.ClaimId, tenantId);
            throw new InvalidOperationException($"Claim '{request.ClaimId}' has already been submitted for tenant '{tenantId}'.");
        }

        // 4. Transform and persist into Cosmos DB
        var claim = new Claim
        {
            ClaimId = request.ClaimId,
            TenantId = tenantId,
            MemberId = request.MemberId,
            ProviderId = request.ProviderId,
            ClaimAmount = request.ClaimAmount,
            Currency = "USD",
            ClaimStatus = "Submitted",
            ServiceDate = request.ServiceDate,
            DiagnosisCodes = request.DiagnosisCodes ?? new(),
            ProcedureCodes = request.ProcedureCodes ?? new(),
            CorrelationId = correlationId,
            SubmittedAtUtc = DateTime.UtcNow
        };

        await _repository.SaveClaimAsync(claim, cancellationToken);

        // 5. Emit ClaimReceived event to Azure Service Bus topic 'claim-events'
        var domainEvent = new ClaimReceivedEvent
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = "ClaimReceived",
            TenantId = tenantId,
            ClaimId = claim.ClaimId,
            MemberId = claim.MemberId,
            ProviderId = claim.ProviderId,
            ClaimAmount = claim.ClaimAmount,
            ClaimStatus = claim.ClaimStatus,
            ServiceDate = claim.ServiceDate,
            CorrelationId = correlationId,
            ReceivedAt = DateTime.UtcNow
        };

        await _eventPublisher.PublishAsync("claim-events", domainEvent, tenantId, correlationId, cancellationToken);

        _logger.LogInformation("Successfully ingested claim {ClaimId} for tenant {TenantId}. CorrelationId: {CorrelationId}",
            claim.ClaimId, tenantId, correlationId);

        return MapToResponse(claim);
    }

    public async Task<ClaimResponse?> GetClaimByIdAsync(string claimId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        var claim = await _repository.GetClaimByIdAsync(tenantId, claimId, cancellationToken);
        return claim == null ? null : MapToResponse(claim);
    }

    private static ClaimResponse MapToResponse(Claim c) => new()
    {
        ClaimId = c.ClaimId,
        TenantId = c.TenantId,
        MemberId = c.MemberId,
        ProviderId = c.ProviderId,
        ClaimAmount = c.ClaimAmount,
        Currency = c.Currency,
        ClaimStatus = c.ClaimStatus,
        ServiceDate = c.ServiceDate,
        DiagnosisCodes = c.DiagnosisCodes,
        ProcedureCodes = c.ProcedureCodes,
        CorrelationId = c.CorrelationId,
        SubmittedAtUtc = c.SubmittedAtUtc
    };
}
