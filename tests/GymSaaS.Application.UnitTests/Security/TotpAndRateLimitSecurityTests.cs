using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Application.Features.Auth.Commands;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Exceptions;
using GymSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GymSaaS.Application.UnitTests.Security;

public class TotpAndRateLimitSecurityTests
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
    public async Task VerifyTotp_With5InvalidAttempts_ShouldLockoutSupervisor()
    {
        // Arrange
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var supervisor = new Supervisor
        {
            Email = "supervisor-totp@test.com",
            PasswordHash = "hash",
            TotpEnabled = true,
            TotpSecretEncrypted = "encrypted_secret",
            FailedTotpAttempts = 0
        };
        db.Supervisors.Add(supervisor);
        await db.SaveChangesAsync();

        var mockTotp = new Mock<ITotpService>();
        mockTotp.Setup(t => t.VerifyCode(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var mockEncryption = new Mock<IEncryptionService>();
        mockEncryption.Setup(e => e.Decrypt(It.IsAny<string>())).Returns("JBSWY3DPEHPK3PXP");

        var mockJwt = new Mock<IJwtTokenGenerator>();

        var handler = new VerifyTotpCommandHandler(db, mockTotp.Object, mockEncryption.Object, mockJwt.Object);

        // Act — Try 5 invalid TOTP attempts
        for (int i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<ValidationException>(() =>
                handler.Handle(new VerifyTotpCommand("supervisor-totp@test.com", "000000", null), CancellationToken.None));
        }

        // Assert — supervisor should be locked out for TOTP
        var updatedSupervisor = await db.Supervisors.FirstOrDefaultAsync(s => s.Email == "supervisor-totp@test.com");
        Assert.NotNull(updatedSupervisor);
        Assert.Equal(5, updatedSupervisor.FailedTotpAttempts);
        Assert.NotNull(updatedSupervisor.TotpLockoutUntil);
        Assert.True(updatedSupervisor.TotpLockoutUntil > DateTime.UtcNow);

        // Next attempt should throw ForbiddenAccessException due to lockout
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            handler.Handle(new VerifyTotpCommand("supervisor-totp@test.com", "000000", null), CancellationToken.None));
    }
}
