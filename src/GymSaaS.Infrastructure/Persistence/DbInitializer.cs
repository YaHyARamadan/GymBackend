using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Infrastructure.Persistence;

public static class DbInitializer
{
    public static async Task SeedAsync(GymSaaSDbContext dbContext)
    {
        // 1. Seed Default Supervisor if not present
        if (!await dbContext.Supervisors.AnyAsync())
        {
            var supervisor = new Supervisor
            {
                Email = "admin@gymsaas.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                MustChangePassword = false,
                TotpEnabled = false,
                TokenVersion = 1,
                CreatedAt = DateTime.UtcNow
            };
            await dbContext.Supervisors.AddAsync(supervisor);
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
