using GymSaaS.Infrastructure.Identity;
using GymSaaS.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GymSaaS.Application.UnitTests.Security;

/// <summary>
/// اختبارات أمنية للتأكد من حظر أي إعدادات ناقصة وإجبار النظام على Fail-Fast بدون استخدام secrets افتراضية.
/// </summary>
public class MissingConfigurationSecurityTests
{
    [Fact]
    public void JwtTokenGenerator_WithoutJwtSecret_ShouldThrowInvalidOperationException()
    {
        // Arrange — configuration without JwtSettings:Secret
        var emptyConfig = new ConfigurationBuilder().AddInMemoryCollection().Build();

        // Act & Assert
        var generator = new JwtTokenGenerator(emptyConfig);
        Assert.Throws<InvalidOperationException>(() =>
            generator.GenerateToken("1", "test@test.com", GymSaaS.Domain.Enums.ActorType.Owner, 1, null));
    }

    [Fact]
    public void ImpersonationTokenService_WithoutJwtSecret_ShouldThrowInvalidOperationException()
    {
        // Arrange — configuration without JwtSettings:Secret
        var emptyConfig = new ConfigurationBuilder().AddInMemoryCollection().Build();

        // Act & Assert
        var service = new ImpersonationTokenService(emptyConfig);
        Assert.Throws<InvalidOperationException>(() =>
            service.GenerateImpersonationToken("1", 1, GymSaaS.Domain.Enums.ActorType.Owner, null, TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void EncryptionService_WithoutSecretKey_ShouldThrowInvalidOperationException()
    {
        // Arrange — configuration without Encryption:SecretKey
        var emptyConfig = new ConfigurationBuilder().AddInMemoryCollection().Build();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => new EncryptionService(emptyConfig));
    }
}
