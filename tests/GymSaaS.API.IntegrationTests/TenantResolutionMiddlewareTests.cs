using System.IO;
using GymSaaS.API.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace GymSaaS.API.IntegrationTests;

public class TenantResolutionMiddlewareTests
{
    [Theory]
    [InlineData("?facility_id=5")]
    [InlineData("?facilityId=5")]
    public async Task InvokeAsync_WhenFacilityIdInQuery_ShouldReturn400BadRequest(string queryString)
    {
        // Arrange
        bool nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new TenantResolutionMiddleware(next);
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(queryString);
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.False(nextCalled);
        Assert.Equal(400, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("X-Facility-Id")]
    [InlineData("facility_id")]
    public async Task InvokeAsync_WhenFacilityIdInHeader_ShouldReturn400BadRequest(string headerName)
    {
        // Arrange
        bool nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new TenantResolutionMiddleware(next);
        var context = new DefaultHttpContext();
        context.Request.Headers[headerName] = "10";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.False(nextCalled);
        Assert.Equal(400, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenNoFacilityIdInQueryOrHeader_ShouldCallNext()
    {
        // Arrange
        bool nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new TenantResolutionMiddleware(next);
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
    }
}
