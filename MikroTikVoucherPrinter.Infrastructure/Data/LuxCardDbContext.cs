using Microsoft.EntityFrameworkCore;
using MikroTikVoucherPrinter.Domain.Common;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;

namespace MikroTikVoucherPrinter.Infrastructure.Data;

public class LuxCardDbContext : DbContext
{
    private readonly IActiveRouterContext _activeRouterContext;

    public DbSet<Voucher> Vouchers { get; set; } = null!;
    public DbSet<Batch> Batches { get; set; } = null!;
    public DbSet<PrintJob> PrintJobs { get; set; } = null!;
    public DbSet<PrintJobEvent> PrintJobEvents { get; set; } = null!;
    public DbSet<Agent> Agents { get; set; } = null!;
    public DbSet<Profile> Profiles { get; set; } = null!;
    public DbSet<TemplateConfig> TemplateConfigs { get; set; } = null!;
    public DbSet<LuxTemplate> LuxTemplates { get; set; } = null!;

    public LuxCardDbContext(
        DbContextOptions<LuxCardDbContext> options,
        IActiveRouterContext activeRouterContext) : base(options)
    {
        _activeRouterContext = activeRouterContext;
    }

    public Guid? CurrentRouterId => _activeRouterContext.CurrentRouterId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Voucher Mapping
        modelBuilder.Entity<Voucher>(entity =>
        {
            // UNIQUE Constraints
            entity.HasIndex(e => new { e.Username, e.RouterId }).IsUnique();
            
            // Performance Indexes
            entity.HasIndex(e => e.BatchId);
            entity.HasIndex(e => e.SyncStatus);
            entity.HasIndex(e => e.AgentId);
            entity.Property(e => e.RouterId).IsRequired();
            entity.HasIndex(e => e.RouterId);
            
            // Keyset Pagination performance optimization index
            entity.HasIndex(e => new { e.RouterId, e.IsDeleted, e.CreatedAt });
            
            // FK relationship to Agent
            entity.HasOne(e => e.Agent)
                  .WithMany(a => a.Vouchers)
                  .HasForeignKey(e => e.AgentId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .IsRequired(false);
            
            // Soft Delete & Router Logical Isolation Global Filters
            entity.HasQueryFilter(e => !e.IsDeleted && e.RouterId == _activeRouterContext.CurrentRouterId);

            entity.Property(e => e.RowVersion).IsConcurrencyToken();
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.VoucherSource);
            entity.Ignore(e => e.EffectivePassword);
        });

        // 2. Batch Mapping
        modelBuilder.Entity<Batch>(entity =>
        {
            entity.Property(e => e.RouterId).IsRequired();
            entity.HasIndex(e => e.RouterId);
            entity.HasQueryFilter(e => !e.IsDeleted && e.RouterId == _activeRouterContext.CurrentRouterId);
            entity.Property(e => e.RowVersion).IsConcurrencyToken();
        });

        // 3. TemplateConfig Mapping (legacy — kept as-is)
        modelBuilder.Entity<TemplateConfig>(entity =>
        {
            entity.Property(e => e.RouterId).IsRequired();
            entity.HasIndex(e => e.RouterId);
            entity.HasQueryFilter(e => !e.IsDeleted && (e.RouterId == _activeRouterContext.CurrentRouterId || e.IsSystemTemplate));
            entity.Property(e => e.RowVersion).IsConcurrencyToken();
        });

        // 3b. LuxTemplate Mapping (new template engine)
        modelBuilder.Entity<LuxTemplate>(entity =>
        {
            entity.Property(e => e.RouterId).IsRequired();
            entity.HasIndex(e => e.RouterId);
            entity.HasIndex(e => e.Category);
            entity.HasQueryFilter(e => !e.IsDeleted && (e.RouterId == _activeRouterContext.CurrentRouterId || e.IsSystemTemplate));
            entity.Property(e => e.RowVersion).IsConcurrencyToken();
            entity.Ignore(e => e.Elements);
            entity.Ignore(e => e.CardsPerPage);
            entity.Ignore(e => e.GridSummary);
            entity.Ignore(e => e.SizeDisplay);
        });

        // 4. Profile Mapping
        modelBuilder.Entity<Profile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Name, e.RouterHost }).IsUnique();
            entity.Ignore(e => e.IsFromCache);
            entity.Ignore(e => e.DisplayName);

            // Global Filter for Router Logical Isolation
            entity.Property(e => e.RouterId).IsRequired();
            entity.HasIndex(e => e.RouterId);
            entity.HasQueryFilter(e => e.RouterId == _activeRouterContext.CurrentRouterId);

            // FK to Template
            entity.HasOne(e => e.Template)
                  .WithMany()
                  .HasForeignKey(e => e.TemplateId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // 5. Agent Mapping
        modelBuilder.Entity<Agent>(entity =>
        {
            entity.Property(e => e.RouterId).IsRequired();
            entity.HasIndex(e => e.RouterId);
            entity.HasQueryFilter(e => !e.IsDeleted && e.RouterId == _activeRouterContext.CurrentRouterId);
            entity.Property(e => e.RowVersion).IsConcurrencyToken();
        });

        // 6. PrintJob Mapping
        modelBuilder.Entity<PrintJob>(entity =>
        {
            entity.ToTable("PrintJobs");
            entity.Property(e => e.RouterId).IsRequired();
            entity.HasIndex(e => e.RouterId);
            entity.HasQueryFilter(e => !e.IsDeleted && e.RouterId == _activeRouterContext.CurrentRouterId);
            entity.Property(e => e.RowVersion).IsConcurrencyToken();

            entity.HasOne(e => e.Batch)
                  .WithMany()
                  .HasForeignKey(e => e.BatchId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .IsRequired(false);
        });

        // 7. PrintJobEvent Mapping
        modelBuilder.Entity<PrintJobEvent>(entity =>
        {
            entity.ToTable("PrintJobEvents");
            entity.Property(e => e.RouterId).IsRequired();
            entity.HasIndex(e => e.RouterId);
            entity.HasQueryFilter(e => !e.IsDeleted && e.RouterId == _activeRouterContext.CurrentRouterId);
            entity.Property(e => e.RowVersion).IsConcurrencyToken();

            entity.HasOne(e => e.Job)
                  .WithMany()
                  .HasForeignKey(e => e.JobId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .IsRequired(true);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        UpdateAuditFields();
        return base.SaveChanges();
    }

    private void UpdateAuditFields()
    {
        var entries = ChangeTracker.Entries<BaseEntity>();

        foreach (var entry in entries)
        {
            // Set RouterId automatically on add if it's a router-specific entity
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity is Voucher v && v.RouterId == Guid.Empty)
                    v.RouterId = _activeRouterContext.CurrentRouterId ?? Guid.Empty;
                else if (entry.Entity is Batch b && b.RouterId == Guid.Empty)
                    b.RouterId = _activeRouterContext.CurrentRouterId ?? Guid.Empty;
                else if (entry.Entity is PrintJob pj && pj.RouterId == Guid.Empty)
                    pj.RouterId = _activeRouterContext.CurrentRouterId ?? Guid.Empty;
                else if (entry.Entity is PrintJobEvent pje && pje.RouterId == Guid.Empty)
                    pje.RouterId = _activeRouterContext.CurrentRouterId ?? Guid.Empty;
                else if (entry.Entity is Agent a && a.RouterId == Guid.Empty)
                    a.RouterId = _activeRouterContext.CurrentRouterId ?? Guid.Empty;
                else if (entry.Entity is TemplateConfig t && t.RouterId == Guid.Empty)
                    t.RouterId = _activeRouterContext.CurrentRouterId ?? Guid.Empty;
                else if (entry.Entity is LuxTemplate lt && lt.RouterId == Guid.Empty)
                    lt.RouterId = _activeRouterContext.CurrentRouterId ?? Guid.Empty;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
                    break;
                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
                    break;
            }
        }

        // Handle profile mapping which doesn't inherit from BaseEntity
        var profileEntries = ChangeTracker.Entries<Profile>();
        foreach (var entry in profileEntries)
        {
            if (entry.State == EntityState.Added && entry.Entity.RouterId == Guid.Empty)
            {
                entry.Entity.RouterId = _activeRouterContext.CurrentRouterId ?? Guid.Empty;
            }
        }
    }
}
