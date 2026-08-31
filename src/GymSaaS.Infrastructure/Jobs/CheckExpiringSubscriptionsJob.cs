using GymSaaS.Domain.Enums;
using GymSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace GymSaaS.Infrastructure.Jobs;

public class CheckExpiringSubscriptionsJob
{
    private readonly GymSaaSDbContext _dbContext;

    public CheckExpiringSubscriptionsJob(GymSaaSDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ExecuteAsync()
    {
        Log.Information("بدء وظيفة فحص الاشتراكات المنتهية والقريبة من الانتهاء");
        var now = DateTime.UtcNow;
        var threshold7Days = now.AddDays(7);

        var facilities = await _dbContext.Facilities
            .IgnoreQueryFilters()
            .Where(f => f.LicenseType == LicenseType.Subscription && f.Status != FacilityStatus.Frozen)
            .ToListAsync();

        foreach (var facility in facilities)
        {
            if (facility.LicenseEndDate.HasValue)
            {
                if (facility.LicenseEndDate.Value <= now)
                {
                    facility.Status = FacilityStatus.Expired;
                    Log.Warning("الاشتراك انتهى للمنشأة {FacilityId} ({FacilityName})", facility.Id, facility.Name);
                }
                else if (facility.LicenseEndDate.Value <= threshold7Days && facility.Status != FacilityStatus.ExpiringSoon)
                {
                    facility.Status = FacilityStatus.ExpiringSoon;
                    Log.Information("تنبيه: الاشتراك ينتهي قريباً للمنشأة {FacilityId} ({FacilityName}) في {EndDate}", facility.Id, facility.Name, facility.LicenseEndDate);
                }
            }
        }

        await _dbContext.SaveChangesAsync();
    }
}
