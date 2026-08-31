using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Application.Features.Auth.Commands;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Exceptions;
using GymSaaS.Infrastructure.Persistence;
using GymSaaS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace GymSaaS.Application.UnitTests.Security;

public class EncryptionAndUserEnumerationSecurityTests
{
    [Fact]
    public void EncryptionService_EncryptAndDecrypt_WithAesGcm_ShouldRoundtripSuccessfully()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Encryption:SecretKey", "TestSecretKey32BytesLong12345678" }
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var encryptionService = new EncryptionService(config);

        string originalText = "SensitiveSecretData2026!";

        // Act
        string cipherText = encryptionService.Encrypt(originalText);
        string decryptedText = encryptionService.Decrypt(cipherText);

        // Assert
        Assert.NotEqual(originalText, cipherText);
        Assert.Equal(originalText, decryptedText);
    }

    [Fact]
    public async Task LoginOwner_WithNonExistentEmail_ShouldThrowValidationExceptionWithGenericMessage()
    {
        // Arrange
        var mockTenantResolver = new Mock<GymSaaS.Domain.Interfaces.ITenantResolver>();
        mockTenantResolver.Setup(r => r.IsSupervisor).Returns(true);

        var options = new DbContextOptionsBuilder<GymSaaSDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var db = new GymSaaSDbContext(options, mockTenantResolver.Object);
        var mockJwt = new Mock<IJwtTokenGenerator>();

        var handler = new LoginOwnerCommandHandler(db, mockJwt.Object);

        // Act & Assert — User enumeration prevention: should throw ValidationException (400), not NotFoundException (404)
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new LoginOwnerCommand("nonexistent@domain.com", "Password123!"), CancellationToken.None));

        Assert.Contains("البريد الإلكتروني أو كلمة السر غير صحيحة.", ex.Errors.Values.First());
    }

    [Fact]
    public async Task LoginSupervisor_WithNonExistentEmail_ShouldThrowValidationExceptionWithGenericMessage()
    {
        // Arrange
        var mockTenantResolver = new Mock<GymSaaS.Domain.Interfaces.ITenantResolver>();
        mockTenantResolver.Setup(r => r.IsSupervisor).Returns(true);

        var options = new DbContextOptionsBuilder<GymSaaSDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var db = new GymSaaSDbContext(options, mockTenantResolver.Object);
        var mockJwt = new Mock<IJwtTokenGenerator>();
        var mockTotp = new Mock<ITotpService>();

        var handler = new LoginSupervisorCommandHandler(db, mockJwt.Object, mockTotp.Object);

        // Act & Assert — User enumeration prevention: should throw ValidationException (400), not NotFoundException (404)
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new LoginSupervisorCommand("nonexistent@supervisor.com", "Password123!"), CancellationToken.None));

        Assert.Contains("البريد الإلكتروني أو كلمة السر غير صحيحة.", ex.Errors.Values.First());
    }
}
