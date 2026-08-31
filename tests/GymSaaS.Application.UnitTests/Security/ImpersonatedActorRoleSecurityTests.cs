using System.Security.Claims;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Application.Features.AuditLog.Queries;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using GymSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GymSaaS.Application.UnitTests.Security;

public class ImpersonatedActorRoleSecurityTests
{
    private GymSaaSDbContext CreateDbContext(string dbName)
    {
        var mockTenantResolver = new Mock<GymSaaS.Domain.Interfaces.ITenantResolver>();
        mockTenantResolver.Setup(r => r.IsSupervisor).Returns(false);

        var options = new DbContextOptionsBuilder<GymSaaSDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new GymSaaSDbContext(options, mockTenantResolver.Object);
    }

    [Fact]
    public async Task GetAuditLogs_WhenImpersonatingCoachRole_ShouldBeForbidden()
    {
        // Arrange
        var db = CreateDbContext(Guid.NewGuid().ToString());

        var mockUserService = new Mock<ICurrentUserService>();
        mockUserService.Setup(u => u.ActorType).Returns(ActorType.Coach);
        mockUserService.Setup(u => u.IsSupervisor).Returns(false);
        mockUserService.Setup(u => u.IsImpersonating).Returns(true);
        mockUserService.Setup(u => u.OnBehalfOfRole).Returns("Coach");

        var handler = new GetAuditLogsQueryHandler(db, mockUserService.Object);

        // Act & Assert — Impersonating Coach should be forbidden from accessing audit logs
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(new GetAuditLogsQuery(), CancellationToken.None));
    }
}
