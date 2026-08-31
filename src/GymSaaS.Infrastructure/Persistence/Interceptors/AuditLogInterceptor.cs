using System.Text.Json;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GymSaaS.Infrastructure.Persistence.Interceptors;

public class AuditLogInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;

    private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash", "TotpSecretEncrypted", "Token", "Secret", "CardNumber", "Cvv"
    };

    public AuditLogInterceptor(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context == null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        var entries = eventData.Context.ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditLogEntry && e.Entity is not AuditLogArchive &&
                        (e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted))
            .ToList();

        if (entries.Count == 0)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        var auditEntries = new List<AuditLogEntry>();

        foreach (var entry in entries)
        {
            var actionType = entry.State switch
            {
                EntityState.Added => "create",
                EntityState.Modified => "update",
                EntityState.Deleted => "delete",
                _ => "unknown"
            };

            var entityType = entry.Entity.GetType().Name;
            var primaryKey = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString() ?? "0";

            var oldValues = new Dictionary<string, object?>();
            var newValues = new Dictionary<string, object?>();

            foreach (var prop in entry.Properties)
            {
                string propName = prop.Metadata.Name;
                bool isSensitive = SensitiveFields.Contains(propName);

                if (entry.State == EntityState.Modified && prop.IsModified)
                {
                    oldValues[propName] = isSensitive ? "تم التعديل" : prop.OriginalValue;
                    newValues[propName] = isSensitive ? "تم التعديل" : prop.CurrentValue;
                }
                else if (entry.State == EntityState.Added)
                {
                    newValues[propName] = isSensitive ? "تم التعديل" : prop.CurrentValue;
                }
                else if (entry.State == EntityState.Deleted)
                {
                    oldValues[propName] = isSensitive ? "تم التعديل" : prop.OriginalValue;
                }
            }

            var auditLog = new AuditLogEntry
            {
                ActorId = _currentUserService.UserId ?? "SYSTEM",
                ActorType = _currentUserService.ActorType ?? Domain.Enums.ActorType.Supervisor,
                OnBehalfOfRole = _currentUserService.OnBehalfOfRole,
                ActionType = actionType,
                EntityType = entityType,
                EntityId = primaryKey,
                OldValue = oldValues.Count > 0 ? JsonSerializer.Serialize(oldValues) : null,
                NewValue = newValues.Count > 0 ? JsonSerializer.Serialize(newValues) : null,
                Timestamp = DateTime.UtcNow,
                FacilityId = _currentUserService.FacilityId,
                BranchId = _currentUserService.BranchId
            };

            auditEntries.Add(auditLog);
        }

        if (auditEntries.Count > 0)
        {
            eventData.Context.Set<AuditLogEntry>().AddRange(auditEntries);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
