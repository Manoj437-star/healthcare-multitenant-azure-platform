using Microsoft.EntityFrameworkCore;
using MemberService.Domain;
using MemberService.Services;

namespace MemberService.Data;

public class MemberDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public MemberDbContext(DbContextOptions<MemberDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Member> Members => Set<Member>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Member entity configuration
        modelBuilder.Entity<Member>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.MemberId }).IsUnique();
            entity.HasIndex(e => e.TenantId);

            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.MemberId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PlanCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);

            // Global Query Filter: Restricts queries strictly to current tenant
            entity.HasQueryFilter(e => !_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId);
        });

        // Outbox entity configuration
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Status, e.CreatedAtUtc });
            entity.HasIndex(e => e.TenantId);

            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Payload).IsRequired();

            // Note: Global query filter applied to tenant context
            entity.HasQueryFilter(e => !_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Member>())
        {
            if (entry.State == EntityState.Added && string.IsNullOrEmpty(entry.Entity.TenantId))
            {
                entry.Entity.TenantId = _tenantContext.TenantId;
            }
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        foreach (var entry in ChangeTracker.Entries<OutboxMessage>())
        {
            if (entry.State == EntityState.Added && string.IsNullOrEmpty(entry.Entity.TenantId))
            {
                entry.Entity.TenantId = _tenantContext.TenantId;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
