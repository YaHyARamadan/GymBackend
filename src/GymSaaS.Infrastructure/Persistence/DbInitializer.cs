using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Infrastructure.Persistence;

public static class DbInitializer
{
    public static async Task SeedAsync(GymSaaSDbContext dbContext)
    {
        // 1. Seed Default Supervisor if not present
        var supervisor = await dbContext.Supervisors.FirstOrDefaultAsync();
        if (supervisor == null)
        {
            supervisor = new Supervisor
            {
                Email = "admin@gymsaas.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                MustChangePassword = true,
                TotpEnabled = false,
                TokenVersion = 1,
                CreatedAt = DateTime.UtcNow
            };
            await dbContext.Supervisors.AddAsync(supervisor);
            await dbContext.SaveChangesAsync();
        }
        else if (BCrypt.Net.BCrypt.Verify("Admin123!", supervisor.PasswordHash))
        {
            // A fresh bootstrap account must complete the password change flow.
            // Once the password is replaced this condition stops matching forever.
            if (!supervisor.MustChangePassword)
            {
                supervisor.MustChangePassword = true;
                await dbContext.SaveChangesAsync();
            }
        }
        else if (supervisor.MustChangePassword)
        {
            // Repair databases created from the old seed where the flag stayed true
            // after the bootstrap password had already been replaced.
            supervisor.MustChangePassword = false;
            await dbContext.SaveChangesAsync();
        }

        // 2. Seed Default Facility & Owner if not present
        if (!await dbContext.Facilities.AnyAsync())
        {
            var facility = new Facility
            {
                Name = "جيم الأبطال - الفجيرة",
                Description = "الفرع الرئيسي لمنشأة جيم الأبطال الرياضية",
                LicenseType = LicenseType.Subscription,
                Status = FacilityStatus.Active,
                LicenseEndDate = DateTime.UtcNow.AddYears(1),
                CreatedAt = DateTime.UtcNow
            };
            await dbContext.Facilities.AddAsync(facility);
            await dbContext.SaveChangesAsync();

            var owner = new Owner
            {
                Name = "أحمد المالك",
                Email = "owner@gymsaas.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Owner123!"),
                Phone = "+971500000000",
                FacilityId = facility.Id,
                ContractSigned = true,
                OnboardingCompleted = true,
                CreatedAt = DateTime.UtcNow
            };
            await dbContext.Owners.AddAsync(owner);

            var branch = new Branch
            {
                FacilityId = facility.Id,
                Name = "فرع الفجيرة الرئيسي",
                Address = "شارع الكورنيش، الفجيرة",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            await dbContext.Branches.AddAsync(branch);

            await dbContext.SaveChangesAsync();
        }
    }
}
