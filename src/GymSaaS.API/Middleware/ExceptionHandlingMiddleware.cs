using System.Net;
using System.Text.Json;
using GymSaaS.Domain.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace GymSaaS.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Response.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
        Log.Error(exception, "حدث خطأ أثناء معالجة الطلب. CorrelationId: {CorrelationId}", correlationId);

        int statusCode = (int)HttpStatusCode.InternalServerError;
        string message = "حدث خطأ غير متوقع في النظام.";
        object? errors = null;
        string? errorCode = null;

        switch (exception)
        {
            case ValidationException valEx:
                statusCode = (int)HttpStatusCode.BadRequest;
                message = valEx.Message;
                errors = valEx.Errors;
                break;

            case NotFoundException notFoundEx:
                statusCode = (int)HttpStatusCode.NotFound;
                message = notFoundEx.Message;
                break;

            case ConflictException conflictEx:
                statusCode = (int)HttpStatusCode.Conflict;
                message = conflictEx.Message;
                break;

            case ForbiddenAccessException forbiddenEx:
                statusCode = (int)HttpStatusCode.Forbidden;
                message = forbiddenEx.Message;
                break;

            case FacilityLockedException lockedEx:
                statusCode = 423; // Locked
                message = lockedEx.Message;
                errorCode = "FACILITY_LOCKED";
                break;

            case DbUpdateConcurrencyException:
                statusCode = (int)HttpStatusCode.Conflict;
                message = "البيانات تم تعديلها بواسطة مستخدم آخر. يرجى تحديث الصفحة وإعادة المحاولة.";
                errorCode = "CONCURRENCY_CONFLICT";
                break;

            case SqlException or TimeoutException:
                statusCode = (int)HttpStatusCode.ServiceUnavailable;
                message = "تعذر الاتصال بقاعدة البيانات حالياً. يرجى المحاولة بعد قليل.";
                break;

            default:
                statusCode = (int)HttpStatusCode.InternalServerError;
                message = "حدث خطأ داخلي في الخادم. يرجى تزويد الدعم الفني برقم المرجعية (Correlation ID).";
                break;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var responseObj = new Dictionary<string, object?>
        {
            ["success"] = false,
            ["statusCode"] = statusCode,
            ["message"] = message,
            ["errors"] = errors,
            ["correlationId"] = correlationId
        };

        if (!string.IsNullOrEmpty(errorCode))
        {
            responseObj["errorCode"] = errorCode;
        }

        var json = JsonSerializer.Serialize(responseObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await context.Response.WriteAsync(json);
    }
}
