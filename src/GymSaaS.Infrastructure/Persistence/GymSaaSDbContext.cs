using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Infrastructure.Persistence;

public class GymSaaSDbContext : DbContext
{
    private readonly ITenantResolver _tenantResolver;

    public GymSaaSDbContext(DbContextOptions<GymSaaSDbContext> options, ITenantResolver tenantResolver)
        : base(options)
    {
        _tenantResolver = tenantResolver;
    }

    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Supervisor> Supervisors => Set<Supervisor>();
    public DbSet<Owner> Owners => Set<Owner>();
    public DbSet<BranchManager> BranchManagers => Set<BranchManager>();
    public DbSet<Coach> Coaches => Set<Coach>();
    public DbSet<Receptionist> Receptionists => Set<Receptionist>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<PlatformSubscription> PlatformSubscriptions => Set<PlatformSubscription>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<AuditLogArchive> AuditLogArchives => Set<AuditLogArchive>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractApproval> ContractApprovals => Set<ContractApproval>();
    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();
    public DbSet<AddOnFeature> AddOnFeatures => Set<AddOnFeature>();
    public DbSet<FacilityAddOnSubscription> FacilityAddOnSubscriptions => Set<FacilityAddOnSubscription>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportTicketMessage> SupportTicketMessages => Set<SupportTicketMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Facility configuration & optimistic concurrency
        modelBuilder.Entity<Facility>(b =>
        {
            b.HasKey(f => f.Id);
            b.Property(f => f.Name).IsRequired().HasMaxLength(100);
            b.Property(f => f.RowVersion).IsRowVersion();
        });

        // Subscription concurrency
        modelBuilder.Entity<Subscription>(b =>
        {
            b.Property(s => s.RowVersion).IsRowVersion();
        });

        // PlatformSubscription concurrency
        modelBuilder.Entity<PlatformSubscription>(b =>
        {
            b.Property(p => p.RowVersion).IsRowVersion();
        });

        // Decimal column types
        modelBuilder.Entity<AddOnFeature>().Property(a => a.Price).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<PaymentRecord>().Property(p => p.Amount).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<PlatformSubscription>().Property(p => p.AmountPaid).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Subscription>().Property(s => s.Price).HasColumnType("decimal(18,2)");

        // Idempotency-Key uniqueness (prevents TOCTOU race condition duplicating PaymentRecord
        // rows when two concurrent requests with the same key both pass the AnyAsync check
        // before either commits — see UnlockFacilityCommand / ActivateFacilityAddOnCommand)
        modelBuilder.Entity<PaymentRecord>().HasIndex(p => p.IdempotencyKey).IsUnique();



        // ─── Multi-Tenancy Global Query Filters (backend.md §0 rule 2) ───────────
        // IsSupervisor returns TRUE only for primary supervisor tokens.
        // During impersonation sessions, IsSupervisor = false, so the filter
        // falls through to `FacilityId == _tenantResolver.FacilityId`, which
        // confines the impersonator to the target facility's data only.
        // This prevents a supervisor impersonating Facility-A from reading Facility-B data.
        modelBuilder.Entity<Branch>().HasQueryFilter(b => _tenantResolver.IsSupervisor || b.FacilityId == _tenantResolver.FacilityId);
        modelBuilder.Entity<Owner>().HasQueryFilter(o => _tenantResolver.IsSupervisor || o.FacilityId == _tenantResolver.FacilityId);
        modelBuilder.Entity<BranchManager>().HasQueryFilter(bm => _tenantResolver.IsSupervisor || bm.FacilityId == _tenantResolver.FacilityId);
        modelBuilder.Entity<Coach>().HasQueryFilter(c => _tenantResolver.IsSupervisor || c.FacilityId == _tenantResolver.FacilityId);
        modelBuilder.Entity<Receptionist>().HasQueryFilter(r => _tenantResolver.IsSupervisor || r.FacilityId == _tenantResolver.FacilityId);
        modelBuilder.Entity<Player>().HasQueryFilter(p => _tenantResolver.IsSupervisor || p.FacilityId == _tenantResolver.FacilityId);
        modelBuilder.Entity<Subscription>().HasQueryFilter(s => _tenantResolver.IsSupervisor || s.FacilityId == _tenantResolver.FacilityId);
        modelBuilder.Entity<PlatformSubscription>().HasQueryFilter(p => _tenantResolver.IsSupervisor || p.FacilityId == _tenantResolver.FacilityId);
        modelBuilder.Entity<AuditLogEntry>().HasQueryFilter(a => _tenantResolver.IsSupervisor || a.FacilityId == _tenantResolver.FacilityId);
        modelBuilder.Entity<PaymentRecord>().HasQueryFilter(p => _tenantResolver.IsSupervisor || p.FacilityId == _tenantResolver.FacilityId);
        modelBuilder.Entity<FacilityAddOnSubscription>().HasQueryFilter(f => _tenantResolver.IsSupervisor || f.FacilityId == _tenantResolver.FacilityId);
        modelBuilder.Entity<SupportTicket>().HasQueryFilter(s => _tenantResolver.IsSupervisor || s.FacilityId == _tenantResolver.FacilityId);
    }
}
