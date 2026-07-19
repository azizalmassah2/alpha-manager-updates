using Microsoft.EntityFrameworkCore;
using MikroTikVoucherPrinter.Domain.Entities.Platform;
using MikroTikVoucherPrinter.Domain.Entities.Operations;
using MikroTikVoucherPrinter.Domain.Entities.Telemetry;

namespace MikroTikVoucherPrinter.Infrastructure.Data;

public class PlatformDbContext : DbContext
{
    public PlatformDbContext(DbContextOptions<PlatformDbContext> options) : base(options)
    {
    }

    public DbSet<Router> Routers { get; set; } = null!;
    public DbSet<SyncQueueItem> SyncQueue { get; set; } = null!;
    public DbSet<OperationJob> OperationJobs { get; set; } = null!;
    public DbSet<OperationAuditRecord> OperationAuditRecords { get; set; } = null!;
    public DbSet<VlanMonitoringConfig> VlanMonitoringConfigs { get; set; } = null!;

    // Broadcasting Devices (Modems & Antennas)
    public DbSet<BroadcastingDevice> BroadcastingDevices { get; set; } = null!;

    // Telemetry
    public DbSet<DeviceTelemetrySnapshot> DeviceTelemetry { get; set; } = null!;
    public DbSet<InterfaceTelemetrySnapshot> InterfaceTelemetry { get; set; } = null!;
    public DbSet<AlertCandidate> AlertCandidates { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Router Mapping
        modelBuilder.Entity<Router>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Host).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(255);
            
            // Constraints
            entity.HasIndex(e => e.SerialNumber).IsUnique().HasFilter("\"SerialNumber\" IS NOT NULL AND \"SerialNumber\" != ''");
            entity.HasIndex(e => e.SoftwareId).IsUnique().HasFilter("\"SoftwareId\" IS NOT NULL AND \"SoftwareId\" != ''");
            entity.HasIndex(e => e.MacAddress).IsUnique().HasFilter("\"MacAddress\" IS NOT NULL AND \"MacAddress\" != ''");
        });

        // SyncQueueItem Mapping
        modelBuilder.Entity<SyncQueueItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OperationType).IsRequired().HasMaxLength(100);
            
            // Indexes
            entity.HasIndex(e => e.RouterId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);

            entity.HasOne(e => e.Router)
                  .WithMany()
                  .HasForeignKey(e => e.RouterId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // VlanMonitoringConfig Composite Key Configuration
        modelBuilder.Entity<VlanMonitoringConfig>(entity =>
        {
            entity.HasKey(e => new { e.RouterId, e.VlanId });
            entity.Property(e => e.DeviceIp).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        // Telemetry Indexes
        modelBuilder.Entity<DeviceTelemetrySnapshot>()
            .HasIndex(t => new { t.RouterId, t.Timestamp });

        modelBuilder.Entity<InterfaceTelemetrySnapshot>()
            .HasIndex(t => new { t.RouterId, t.Timestamp });

        modelBuilder.Entity<AlertCandidate>()
            .HasIndex(a => new { a.RouterId, a.Timestamp });

        // BroadcastingDevice Mapping
        modelBuilder.Entity<BroadcastingDevice>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.MacAddress).HasMaxLength(50);
            entity.Property(e => e.DeviceType).HasMaxLength(100);
            entity.Property(e => e.Vendor).HasMaxLength(100);
            entity.Property(e => e.Username).HasMaxLength(100);
            entity.Property(e => e.Password).HasMaxLength(255);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.HasIndex(e => e.IpAddress);
            entity.HasIndex(e => e.Vendor);
        });
    }
}
