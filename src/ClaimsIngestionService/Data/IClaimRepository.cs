using System.Collections.Concurrent;
using System.Net;
using Microsoft.Azure.Cosmos;
using ClaimsIngestionService.Domain;

namespace ClaimsIngestionService.Data;

public interface IClaimRepository
{
    Task<bool> IsClaimDuplicateAsync(string tenantId, string claimId, CancellationToken cancellationToken = default);
    Task SaveClaimAsync(Claim claim, CancellationToken cancellationToken = default);
    Task<Claim?> GetClaimByIdAsync(string tenantId, string claimId, CancellationToken cancellationToken = default);
    Task UpsertMemberCacheAsync(EnrolledMemberCache member, CancellationToken cancellationToken = default);
    Task<EnrolledMemberCache?> GetMemberCacheAsync(string tenantId, string memberId, CancellationToken cancellationToken = default);
    Task<bool> TryRecordIdempotentOperationAsync(string tenantId, string operationKey, CancellationToken cancellationToken = default);
}

public class CosmosClaimRepository : IClaimRepository
{
    private readonly Container _claimsContainer;
    private readonly Container _membersContainer;
    private readonly Container _idempotencyContainer;
    private readonly ILogger<CosmosClaimRepository> _logger;

    public CosmosClaimRepository(CosmosClient cosmosClient, string databaseName, ILogger<CosmosClaimRepository> logger)
    {
        _logger = logger;
        var database = cosmosClient.GetDatabase(databaseName);
        _claimsContainer = database.GetContainer("claims");
        _membersContainer = database.GetContainer("enrolled-members");
        _idempotencyContainer = database.GetContainer("idempotency-records");
    }

    public async Task<bool> IsClaimDuplicateAsync(string tenantId, string claimId, CancellationToken cancellationToken = default)
    {
        var id = $"{tenantId}_{claimId}";
        try
        {
            var response = await _claimsContainer.ReadItemAsync<Claim>(id, new PartitionKey(tenantId), cancellationToken: cancellationToken);
            return response.Resource != null;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task SaveClaimAsync(Claim claim, CancellationToken cancellationToken = default)
    {
        claim.Id = $"{claim.TenantId}_{claim.ClaimId}";
        _logger.LogInformation("Writing claim {ClaimId} to Cosmos DB container 'claims' under partition key {TenantId}.",
            claim.ClaimId, claim.TenantId);

        await _claimsContainer.UpsertItemAsync(claim, new PartitionKey(claim.TenantId), cancellationToken: cancellationToken);
    }

    public async Task<Claim?> GetClaimByIdAsync(string tenantId, string claimId, CancellationToken cancellationToken = default)
    {
        var id = $"{tenantId}_{claimId}";
        try
        {
            var response = await _claimsContainer.ReadItemAsync<Claim>(id, new PartitionKey(tenantId), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task UpsertMemberCacheAsync(EnrolledMemberCache member, CancellationToken cancellationToken = default)
    {
        member.Id = $"{member.TenantId}_{member.MemberId}";
        await _membersContainer.UpsertItemAsync(member, new PartitionKey(member.TenantId), cancellationToken: cancellationToken);
    }

    public async Task<EnrolledMemberCache?> GetMemberCacheAsync(string tenantId, string memberId, CancellationToken cancellationToken = default)
    {
        var id = $"{tenantId}_{memberId}";
        try
        {
            var response = await _membersContainer.ReadItemAsync<EnrolledMemberCache>(id, new PartitionKey(tenantId), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> TryRecordIdempotentOperationAsync(string tenantId, string operationKey, CancellationToken cancellationToken = default)
    {
        var id = $"{tenantId}_{operationKey}";
        var record = new IdempotentRecord
        {
            Id = id,
            TenantId = tenantId,
            OperationKey = operationKey,
            Status = "Completed",
            CreatedAtUtc = DateTime.UtcNow
        };

        try
        {
            await _idempotencyContainer.CreateItemAsync(record, new PartitionKey(tenantId), cancellationToken: cancellationToken);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }
    }
}

public class InMemoryClaimRepository : IClaimRepository
{
    private static readonly ConcurrentDictionary<string, Claim> _claims = new();
    private static readonly ConcurrentDictionary<string, EnrolledMemberCache> _members = new();
    private static readonly ConcurrentDictionary<string, IdempotentRecord> _idempotentRecords = new();
    private readonly ILogger<InMemoryClaimRepository> _logger;

    public InMemoryClaimRepository(ILogger<InMemoryClaimRepository> logger)
    {
        _logger = logger;
    }

    public Task<bool> IsClaimDuplicateAsync(string tenantId, string claimId, CancellationToken cancellationToken = default)
    {
        var key = $"{tenantId}_{claimId}";
        return Task.FromResult(_claims.ContainsKey(key));
    }

    public Task SaveClaimAsync(Claim claim, CancellationToken cancellationToken = default)
    {
        claim.Id = $"{claim.TenantId}_{claim.ClaimId}";
        _logger.LogInformation("[LOCAL DEV COSMOS STORE] Persisting claim {ClaimId} for tenant {TenantId}.", claim.ClaimId, claim.TenantId);
        _claims[claim.Id] = claim;
        return Task.CompletedTask;
    }

    public Task<Claim?> GetClaimByIdAsync(string tenantId, string claimId, CancellationToken cancellationToken = default)
    {
        var key = $"{tenantId}_{claimId}";
        _claims.TryGetValue(key, out var claim);
        return Task.FromResult(claim);
    }

    public Task UpsertMemberCacheAsync(EnrolledMemberCache member, CancellationToken cancellationToken = default)
    {
        member.Id = $"{member.TenantId}_{member.MemberId}";
        _members[member.Id] = member;
        return Task.CompletedTask;
    }

    public Task<EnrolledMemberCache?> GetMemberCacheAsync(string tenantId, string memberId, CancellationToken cancellationToken = default)
    {
        var key = $"{tenantId}_{memberId}";
        _members.TryGetValue(key, out var member);
        return Task.FromResult(member);
    }

    public Task<bool> TryRecordIdempotentOperationAsync(string tenantId, string operationKey, CancellationToken cancellationToken = default)
    {
        var key = $"{tenantId}_{operationKey}";
        var record = new IdempotentRecord
        {
            Id = key,
            TenantId = tenantId,
            OperationKey = operationKey,
            Status = "Completed",
            CreatedAtUtc = DateTime.UtcNow
        };
        return Task.FromResult(_idempotentRecords.TryAdd(key, record));
    }
}
