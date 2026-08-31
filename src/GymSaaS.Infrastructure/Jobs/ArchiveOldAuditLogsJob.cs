using GymSaaS.Domain.Entities;
using GymSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace GymSaaS.Infrastructure.Jobs;

public class ArchiveOldAuditLogsJob
{
    private readonly GymSaaSDbContext _dbContext;

    public ArchiveOldAuditLogsJob(GymSaaSDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ExecuteAsync()
    {
        var cutoffDate = DateTime.UtcNow.AddMonths(-3);
        Log.Information("بدء وظيفة أرشفة سجل الأنشطة الأقدم من {CutoffDate}", cutoffDate);

        var oldEntries = await _dbContext.AuditLogEntries
            .IgnoreQueryFilters()
            .Where(a => a.Timestamp < cutoffDate)
            .Take(1000)
            .ToListAsync();

        if (oldEntries.Count == 0)
            return;

        var archives = oldEntries.Select(a => new AuditLogArchive
        {
            OriginalEntryId = a.Id,
            ActorId = a.ActorId,
            ActorType = a.ActorType,
            OnBehalfOfRole = a.OnBehalfOfRole,
            ActionType = a.ActionType,
            EntityType = a.EntityType,
            EntityId = a.EntityId,
            OldValue = a.OldValue,
            NewValue = a.NewValue,
            Timestamp = a.Timestamp,
            FacilityId = a.FacilityId,
            BranchId = a.BranchId,
            CorrelationId = a.CorrelationId,
            ArchivedAt = DateTime.UtcNow
        }).ToList();

        _dbContext.AuditLogArchives.AddRange(archives);
        _dbContext.AuditLogEntries.RemoveRange(oldEntries);

        await _dbContext.SaveChangesAsync();
        Log.Information("تم أرشفة {Count} عنصر بنجاح", archives.Count);
    }
}
