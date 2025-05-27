using System.Net;
using System.Text.Json;

namespace FlightSystem.Server.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred. Request: {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse
        {
            RequestId = context.TraceIdentifier,
            Timestamp = DateTime.UtcNow
        };

        switch (exception)
        {
            case ValidationException validationEx:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Message = "Validation failed";
                response.Details = validationEx.Errors?.ToList();
                break;

            case UnauthorizedAccessException:
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                response.Message = "Unauthorized access";
                break;

            case KeyNotFoundException:
                response.StatusCode = (int)HttpStatusCode.NotFound;
                response.Message = "Resource not found";
                break;

            case InvalidOperationException invalidOpEx:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Message = invalidOpEx.Message;
                break;

            case TimeoutException:
                response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                response.Message = "Request timeout";
                break;

            default:
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response.Message = _environment.IsDevelopment()
                    ? exception.Message
                    : "An internal server error occurred";

                if (_environment.IsDevelopment())
                {
                    response.Details = new List<string> { exception.StackTrace ?? "" };
                }
                break;
        }

        context.Response.StatusCode = response.StatusCode;

        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(jsonResponse);
    }
}

public class ErrorResponse
{
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public List<string>? Details { get; set; }
}

public class ValidationException : Exception
{
    public IEnumerable<string> Errors { get; }

    public ValidationException(string message, IEnumerable<string> errors) : base(message)
    {
        Errors = errors;
    }
}

// Structured logging service
public interface IStructuredLogger
{
    void LogUserAction(string userId, string action, object? data = null);
    void LogBusinessEvent(string eventType, string entityId, object? data = null);
    void LogPerformanceMetric(string operation, TimeSpan duration, bool success = true);
    void LogSecurityEvent(string eventType, string? userId = null, object? data = null);
}

public class StructuredLogger : IStructuredLogger
{
    private readonly ILogger<StructuredLogger> _logger;

    public StructuredLogger(ILogger<StructuredLogger> logger)
    {
        _logger = logger;
    }

    public void LogUserAction(string userId, string action, object? data = null)
    {
        _logger.LogInformation("👤 User Action: {UserId} performed {Action} at {Timestamp}. Data: {Data}",
            userId, action, DateTime.UtcNow, data != null ? JsonSerializer.Serialize(data) : "null");
    }

    public void LogBusinessEvent(string eventType, string entityId, object? data = null)
    {
        _logger.LogInformation("📊 Business Event: {EventType} for entity {EntityId} at {Timestamp}. Data: {Data}",
            eventType, entityId, DateTime.UtcNow, data != null ? JsonSerializer.Serialize(data) : "null");
    }

    public void LogPerformanceMetric(string operation, TimeSpan duration, bool success = true)
    {
        if (success)
        {
            _logger.LogInformation("⚡ Performance: {Operation} completed in {Duration}ms",
                operation, duration.TotalMilliseconds);
        }
        else
        {
            _logger.LogWarning("⚠️ Performance: {Operation} failed after {Duration}ms",
                operation, duration.TotalMilliseconds);
        }
    }

    public void LogSecurityEvent(string eventType, string? userId = null, object? data = null)
    {
        _logger.LogWarning("🔒 Security Event: {EventType} by user {UserId} at {Timestamp}. Data: {Data}",
            eventType, userId ?? "anonymous", DateTime.UtcNow, data != null ? JsonSerializer.Serialize(data) : "null");
    }
}

// Request logging middleware
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var startTime = DateTime.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            var logLevel = GetLogLevel(context.Response.StatusCode);

            _logger.Log(logLevel,
                "🌐 HTTP {Method} {Path} responded {StatusCode} in {Duration}ms. User: {User}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                context.User?.Identity?.Name ?? "anonymous");
        }
    }

    private static LogLevel GetLogLevel(int statusCode)
    {
        return statusCode switch
        {
            >= 500 => LogLevel.Error,
            >= 400 => LogLevel.Warning,
            >= 300 => LogLevel.Information,
            _ => LogLevel.Information
        };
    }
}