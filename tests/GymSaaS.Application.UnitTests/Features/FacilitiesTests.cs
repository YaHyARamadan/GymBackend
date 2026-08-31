using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Application.Features.Facilities.Commands;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using GymSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GymSaaS.Application.UnitTests.Features;

public class FacilitiesTests
{
    private GymSaaSDbContext CreateDbContext(string dbName)
    {
        var mockTenantResolver = new Mock<GymSaaS.Domain.Interfaces.ITenantResolver>();
        mockTenantResolver.Setup(r => r.IsSupervisor).Returns(true);

        var options = new DbContextOptionsBuilder<GymSaaSDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new GymSaaSDbContext(options, mockTenantResolver.Object);
    }

    [Fact]
    public async Task CreateFacility_1_InvalidData_ShouldThrowValidationException()
    {
        // 1. Invalid data case
        var validator = new CreateFacilityCommandValidator();
        var command = new CreateFacilityCommand("", null, LicenseType.Subscription, null, "", "invalid-email", "123");

        var result = await validator.ValidateAsync(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Name");
        Assert.Contains(result.Errors, e => e.PropertyName == "OwnerEmail");
    }

    [Fact]
    public async Task CreateFacility_2_InsufficientPermission_ShouldThrowForbiddenAccessException()
    {
        // 2. Insufficient permission case
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var mockUserService = new Mock<ICurrentUserService>();
        mockUserService.Setup(u => u.IsSupervisor).Returns(false); // Not supervisor

        var handler = new CreateFacilityCommandHandler(db, mockUserService.Object);
        var command = new CreateFacilityCommand("Gym 1", null, LicenseType.Subscription, null, "Owner 1", "owner@test.com", "Password123!");

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task CreateFacility_3_DuplicateOwnerEmail_ShouldThrowConflictException()
    {
        // 3. Conflict / Duplicate case
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Owners.Add(new Owner
        {
            Name = "Existing Owner",
            Email = "duplicate@test.com",
            PasswordHash = "hash",
            FacilityId = 1
        });
        await db.SaveChangesAsync();

        var mockUserService = new Mock<ICurrentUserService>();
        mockUserService.Setup(u => u.IsSupervisor).Returns(true);

        var handler = new CreateFacilityCommandHandler(db, mockUserService.Object);
        var command = new CreateFacilityCommand("Gym 2", null, LicenseType.Subscription, null, "New Owner", "duplicate@test.com", "Password123!");

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task CreateFacility_4_Success_ShouldCreateFacilityAndOwner()
    {
        // 4. Success / Happy path case
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var mockUserService = new Mock<ICurrentUserService>();
        mockUserService.Setup(u => u.IsSupervisor).Returns(true);

        var handler = new CreateFacilityCommandHandler(db, mockUserService.Object);
        var command = new CreateFacilityCommand("Gym Alpha", "Best Gym", LicenseType.Subscription, DateTime.UtcNow.AddYears(1), "Captains", "alpha@test.com", "Password123!");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Gym Alpha", result.Name);
        Assert.Equal(FacilityStatus.Active, result.Status);

        var createdFacility = await db.Facilities.FirstOrDefaultAsync(f => f.Id == result.Id);
        Assert.NotNull(createdFacility);
        Assert.Equal("Gym Alpha", createdFacility.Name);
    }
}
