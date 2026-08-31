using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Application.Features.Auth.Commands;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Exceptions;
using GymSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GymSaaS.Application.UnitTests.Security;

/// <summary>
/// اختبارات أمنية لضمان عدم وجود باسورد احتياطي مكتوب في الكود.
/// المشكلة: كان في شرط request.Password != "Owner123!" يسمح بتجاوز المصادقة بالكامل.
/// </summary>
public class BackdoorPasswordSecurityTests
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
    public async Task LoginOwner_WithHardcodedBackdoorPassword_ShouldBeRejected()
    {
        // Arrange — نحاول الدخول بالباسورد الاحتياطي القديم "Owner123!"
        var db = CreateDbContext(Guid.NewGuid().ToString());

        // Owner بباسورد حقيقي مختلف تمامًا
        var owner = new Owner
        {
            Name = "أونر اختبار",
            Email = "owner-sec@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("RealSecurePassword@2026"),
            FacilityId = 1,
            ContractSigned = false,
            OnboardingCompleted = false
        };
        var facility = new Facility { Name = "منشأة اختبار" };
        db.Facilities.Add(facility);
        await db.SaveChangesAsync();
        owner.FacilityId = facility.Id;
        db.Owners.Add(owner);
        await db.SaveChangesAsync();

        var mockJwt = new Mock<IJwtTokenGenerator>();
        var handler = new LoginOwnerCommandHandler(db, mockJwt.Object);

        // Act — نستخدم الباسورد الاحتياطي المحذوف
        var command = new LoginOwnerCommand("owner-sec@test.com", "Owner123!");

        // Assert — يجب أن يُرفض رفضًا قاطعًا
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task LoginOwner_WithCorrectPassword_ShouldSucceed()
    {
        // Arrange — تأكيد أن الدخول بالباسورد الصحيح لا يزال يعمل
        var db = CreateDbContext(Guid.NewGuid().ToString());

        var facility = new Facility { Name = "منشأة صحيحة", Status = GymSaaS.Domain.Enums.FacilityStatus.Active };
        db.Facilities.Add(facility);
        await db.SaveChangesAsync();

        var owner = new Owner
        {
            Name = "أونر حقيقي",
            Email = "real-owner@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("RealPass@2026"),
            FacilityId = facility.Id,
            ContractSigned = true,
            OnboardingCompleted = true
        };
        db.Owners.Add(owner);
        await db.SaveChangesAsync();

        var mockJwt = new Mock<IJwtTokenGenerator>();
        mockJwt.Setup(j => j.GenerateToken(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<GymSaaS.Domain.Enums.ActorType>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<bool>()))
            .Returns("valid_token");

        var handler = new LoginOwnerCommandHandler(db, mockJwt.Object);
        var command = new LoginOwnerCommand("real-owner@test.com", "RealPass@2026");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result.Token);
        Assert.Equal("valid_token", result.Token);
    }

    [Fact]
    public async Task LoginOwner_WithAnyWrongPassword_ShouldIncrementFailedAttempts()
    {
        // Arrange — تأكيد أن محاولات الدخول الفاشلة تُسجَّل بشكل صحيح
        var db = CreateDbContext(Guid.NewGuid().ToString());

        var facility = new Facility { Name = "منشأة 3" };
        db.Facilities.Add(facility);
        await db.SaveChangesAsync();

        var owner = new Owner
        {
            Name = "أونر 3",
            Email = "owner3@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Correct@2026"),
            FacilityId = facility.Id,
            FailedLoginAttempts = 0
        };
        db.Owners.Add(owner);
        await db.SaveChangesAsync();

        var mockJwt = new Mock<IJwtTokenGenerator>();
        var handler = new LoginOwnerCommandHandler(db, mockJwt.Object);

        // Act — محاولة بباسورد خاطئ
        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(new LoginOwnerCommand("owner3@test.com", "WrongPassword!"), CancellationToken.None));

        // Assert — FailedLoginAttempts ارتفع بالفعل
        var updatedOwner = await db.Owners.FindAsync(owner.Id);
        Assert.Equal(1, updatedOwner!.FailedLoginAttempts);
    }

    [Fact]
    public async Task LoginSupervisor_WithHardcodedBackdoorPassword_ShouldBeRejected()
    {
        // Arrange — نحاول الدخول بالباسورد الاحتياطي القديم "Admin123!"
        var db = CreateDbContext(Guid.NewGuid().ToString());

        var supervisor = new GymSaaS.Domain.Entities.Supervisor
        {
            Email = "supervisor-sec@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("RealAdminPass@2026"),
            TotpEnabled = false,
            MustChangePassword = false
        };
        db.Supervisors.Add(supervisor);
        await db.SaveChangesAsync();

        var mockJwt = new Mock<IJwtTokenGenerator>();
        var mockTotp = new Mock<ITotpService>();
        mockTotp.Setup(t => t.GenerateSetupSecret(It.IsAny<string>()))
            .Returns(("SECRET", "data:image/png;base64,..."));

        var mockTotpSetupToken = new Mock<ITotpSetupTokenService>();
        var handler = new LoginSupervisorCommandHandler(db, mockJwt.Object, mockTotp.Object, mockTotpSetupToken.Object);
        var command = new LoginSupervisorCommand("supervisor-sec@test.com", "Admin123!");

        // Assert — يجب أن يُرفض رفضًا قاطعًا
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(command, CancellationToken.None));
    }
}
