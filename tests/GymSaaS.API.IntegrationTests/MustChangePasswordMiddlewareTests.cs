using System.IO;
using System.Security.Claims;
using GymSaaS.API.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace GymSaaS.API.IntegrationTests;

public class MustChangePasswordMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenMustChangePasswordIsTrueAndTargetNotAllowed_ShouldReturn403Forbidden()
    {
        // Arrange
        bool nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new MustChangePasswordMiddleware(next);
        var context = new DefaultHttpContext();

        var claims = new[]
        {
            new Claim("must_change_password", "true")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        context.Request.Path = "/api/facilities";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.False(nextCalled);
        Assert.Equal(403, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenMustChangePasswordIsFalse_ShouldCallNext()
    {
        // Arrange
        bool nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new MustChangePasswordMiddleware(next);
        var context = new DefaultHttpContext();

        var claims = new[]
        {
            new Claim("must_change_password", "false")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        context.Request.Path = "/api/facilities";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
    }
}
