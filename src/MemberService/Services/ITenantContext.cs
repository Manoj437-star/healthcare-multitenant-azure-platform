namespace MemberService.Services;

public interface ITenantContext
{
    string TenantId { get; }
    string CorrelationId { get; }
    bool IsResolved { get; }
    void SetTenant(string tenantId, string correlationId);
}

public class TenantContext : ITenantContext
{
    public string TenantId { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = Guid.NewGuid().ToString();
    public bool IsResolved => !string.IsNullOrWhiteSpace(TenantId);

    public void SetTenant(string tenantId, string correlationId)
    {
        TenantId = tenantId;
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            CorrelationId = correlationId;
        }
    }
}
