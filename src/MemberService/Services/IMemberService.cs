using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MemberService.Data;
using MemberService.Domain;
using MemberService.DTOs;

namespace MemberService.Services;

public interface IMemberService
{
    Task<MemberResponse> EnrollMemberAsync(EnrollMemberRequest request, CancellationToken cancellationToken = default);
    Task<MemberResponse?> GetMemberByIdAsync(string memberId, CancellationToken cancellationToken = default);
    Task<IEnumerable<MemberResponse>> GetMembersAsync(CancellationToken cancellationToken = default);
}

public class MemberManagementService : IMemberService
{
    private readonly MemberDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<MemberManagementService> _logger;

    public MemberManagementService(MemberDbContext dbContext, ITenantContext tenantContext, ILogger<MemberManagementService> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<MemberResponse> EnrollMemberAsync(EnrollMemberRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        var correlationId = _tenantContext.CorrelationId;

        _logger.LogInformation("Enrolling new member {MemberId} for tenant {TenantId}. CorrelationId: {CorrelationId}",
            request.MemberId, tenantId, correlationId);

        // Check for duplicate member within tenant
        var existing = await _dbContext.Members
            .FirstOrDefaultAsync(m => m.MemberId == request.MemberId, cancellationToken);

        if (existing != null)
        {
            throw new InvalidOperationException($"Member with ID '{request.MemberId}' already exists for tenant '{tenantId}'.");
        }

        if (request.CoverageEndDate.HasValue && request.CoverageEndDate.Value < request.CoverageStartDate)
        {
            throw new ArgumentException("Coverage end date cannot precede coverage start date.");
        }

        var member = new Member
        {
            TenantId = tenantId,
            MemberId = request.MemberId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            PlanCode = request.PlanCode,
            Status = "Active",
            CoverageStartDate = request.CoverageStartDate,
            CoverageEndDate = request.CoverageEndDate,
            Email = request.Email ?? string.Empty,
            PhoneNumber = request.PhoneNumber ?? string.Empty
        };

        // Prepare domain event
        var domainEvent = new MemberEnrolledEvent
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = "MemberEnrolled",
            TenantId = tenantId,
            MemberId = member.MemberId,
            FirstName = member.FirstName,
            LastName = member.LastName,
            DateOfBirth = member.DateOfBirth,
            PlanCode = member.PlanCode,
            Status = member.Status,
            CoverageStartDate = member.CoverageStartDate,
            CorrelationId = correlationId,
            OccurredAt = DateTime.UtcNow
        };

        // Create Outbox entry in same transaction
        var outboxMessage = new OutboxMessage
        {
            TenantId = tenantId,
            EventType = domainEvent.EventType,
            CorrelationId = correlationId,
            Payload = JsonSerializer.Serialize(domainEvent),
            Status = "Pending"
        };

        await _dbContext.Members.AddAsync(member, cancellationToken);
        await _dbContext.OutboxMessages.AddAsync(outboxMessage, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully persisted member {MemberId} and staged outbox event for tenant {TenantId}.",
            member.MemberId, tenantId);

        return MapToResponse(member);
    }

    public async Task<MemberResponse?> GetMemberByIdAsync(string memberId, CancellationToken cancellationToken = default)
    {
        var member = await _dbContext.Members
            .FirstOrDefaultAsync(m => m.MemberId == memberId, cancellationToken);

        return member == null ? null : MapToResponse(member);
    }

    public async Task<IEnumerable<MemberResponse>> GetMembersAsync(CancellationToken cancellationToken = default)
    {
        var members = await _dbContext.Members
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        return members.Select(MapToResponse);
    }

    private static MemberResponse MapToResponse(Member m) => new()
    {
        Id = m.Id,
        TenantId = m.TenantId,
        MemberId = m.MemberId,
        FirstName = m.FirstName,
        LastName = m.LastName,
        DateOfBirth = m.DateOfBirth,
        Gender = m.Gender,
        PlanCode = m.PlanCode,
        Status = m.Status,
        CoverageStartDate = m.CoverageStartDate,
        CoverageEndDate = m.CoverageEndDate,
        Email = m.Email,
        PhoneNumber = m.PhoneNumber,
        CreatedAtUtc = m.CreatedAtUtc
    };
}
