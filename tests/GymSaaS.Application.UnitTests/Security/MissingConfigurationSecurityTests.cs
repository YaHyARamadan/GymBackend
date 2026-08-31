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
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void JwtTokenGenerator_WithoutJwtSecret_ShouldThrowInvalidOperationException(string? secretVal)
    {
        // Arrange — configuration with missing or empty JwtSettings:Secret
        var inMemory = new Dictionary<string, string?>();
        if (secretVal != null) inMemory["JwtSettings:Secret"] = secretVal;
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();

        // Act & Assert
        var generator = new JwtTokenGenerator(config);
        Assert.Throws<InvalidOperationException>(() =>
            generator.GenerateToken("1", "test@test.com", GymSaaS.Domain.Enums.ActorType.Owner, 1, null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ImpersonationTokenService_WithoutJwtSecret_ShouldThrowInvalidOperationException(string? secretVal)
    {
        // Arrange — configuration with missing or empty JwtSettings:Secret
        var inMemory = new Dictionary<string, string?>();
        if (secretVal != null) inMemory["JwtSettings:Secret"] = secretVal;
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();

        // Act & Assert
        var service = new ImpersonationTokenService(config);
        Assert.Throws<InvalidOperationException>(() =>
            service.GenerateImpersonationToken("1", 1, GymSaaS.Domain.Enums.ActorType.Owner, null, TimeSpan.FromMinutes(15)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EncryptionService_WithoutSecretKey_ShouldThrowInvalidOperationException(string? secretVal)
    {
        // Arrange — configuration with missing or empty Encryption:SecretKey
        var inMemory = new Dictionary<string, string?>();
        if (secretVal != null) inMemory["Encryption:SecretKey"] = secretVal;
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => new EncryptionService(config));
    }
}
