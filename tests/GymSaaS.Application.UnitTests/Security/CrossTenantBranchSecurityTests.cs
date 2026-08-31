using GymSaaS.Application.Features.Players.Commands;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Exceptions;
using GymSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GymSaaS.Application.UnitTests.Security;

public class CrossTenantBranchSecurityTests
{
    private GymSaaSDbContext CreateDbContext(string dbName, int userFacilityId)
    {
        var mockTenantResolver = new Mock<GymSaaS.Domain.Interfaces.ITenantResolver>();
        mockTenantResolver.Setup(r => r.IsSupervisor).Returns(false);
        mockTenantResolver.Setup(r => r.FacilityId).Returns(userFacilityId);

        var options = new DbContextOptionsBuilder<GymSaaSDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new GymSaaSDbContext(options, mockTenantResolver.Object);
    }

    [Fact]
    public async Task CreatePlayer_WithBranchFromAnotherFacility_ShouldThrowNotFoundException()
    {
        // Arrange
        var db = CreateDbContext(Guid.NewGuid().ToString(), userFacilityId: 10);

        // Facility 10 branch
        var branchFacility10 = new Branch { Id = 1, Name = "Branch 10", FacilityId = 10 };
        // Facility 20 branch (different tenant)
        var branchFacility20 = new Branch { Id = 2, Name = "Branch 20", FacilityId = 20 };

        db.Branches.AddRange(branchFacility10, branchFacility20);
        await db.SaveChangesAsync();

        var mockUserService = new Mock<GymSaaS.Application.Common.Interfaces.ICurrentUserService>();
        mockUserService.Setup(u => u.FacilityId).Returns(10);

        var handler = new CreatePlayerCommandHandler(db, mockUserService.Object);

        // Act & Assert — Attempting to create player in Branch 2 (belongs to Facility 20, not Facility 10)
        var command = new CreatePlayerCommand("لاعب متسلل", "hacker@test.com", "01000000000", null, BranchId: 2);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task CreatePlayer_WithBranchFromOwnFacility_ShouldSucceed()
    {
        // Arrange
        var db = CreateDbContext(Guid.NewGuid().ToString(), userFacilityId: 10);

        var branchFacility10 = new Branch { Id = 1, Name = "Branch 10", FacilityId = 10 };
        db.Branches.Add(branchFacility10);
        await db.SaveChangesAsync();

        var mockUserService = new Mock<GymSaaS.Application.Common.Interfaces.ICurrentUserService>();
        mockUserService.Setup(u => u.FacilityId).Returns(10);

        var handler = new CreatePlayerCommandHandler(db, mockUserService.Object);

        // Act
        var command = new CreatePlayerCommand("لاعب شرعي", "legit@test.com", "01000000000", null, BranchId: 1);
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("لاعب شرعي", result.Name);
        Assert.Equal(1, result.BranchId);
    }
}
