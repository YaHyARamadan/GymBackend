using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Infrastructure.Persistence;
using GymSaaS.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GymSaaS.Application.UnitTests.Infrastructure;

public class AuditLogInterceptorTests
{
    [Fact]
    public async Task SavingChanges_WithoutAuthenticatedUser_ShouldRecordActorAsSystem()
    {
        // Arrange
        var mockUserService = new Mock<ICurrentUserService>();
        mockUserService.Setup(u => u.UserId).Returns((string?)null);
        mockUserService.Setup(u => u.ActorType).Returns((ActorType?)null);
        mockUserService.Setup(u => u.FacilityId).Returns((int?)null);

        var interceptor = new AuditLogInterceptor(mockUserService.Object);

        var mockTenantResolver = new Mock<GymSaaS.Domain.Interfaces.ITenantResolver>();
        mockTenantResolver.Setup(r => r.IsSupervisor).Returns(true);

        var options = new DbContextOptionsBuilder<GymSaaSDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        using var db = new GymSaaSDbContext(options, mockTenantResolver.Object);

        // Act — Add entity without logged in user (e.g. background job)
        var facility = new Facility { Name = "Background Test Facility", Status = FacilityStatus.Active };
        db.Facilities.Add(facility);
        await db.SaveChangesAsync();

        // Assert — AuditLogEntry should record ActorId = SYSTEM and ActorType = System
        var auditEntry = await db.AuditLogEntries.FirstOrDefaultAsync();
        Assert.NotNull(auditEntry);
        Assert.Equal("SYSTEM", auditEntry.ActorId);
        Assert.Equal(ActorType.System, auditEntry.ActorType);
    }
}
