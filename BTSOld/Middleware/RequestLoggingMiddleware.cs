using Serilog.Context;

namespace AuthService.Middleware;

/// <summary>
/// Request logging middleware using Serilog
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = Guid.NewGuid().ToString();
        var startTime = DateTime.UtcNow;

        // Add request context to logs
        using (LogContext.PushProperty("RequestId", requestId))
        using (LogContext.PushProperty("Method", context.Request.Method))
        using (LogContext.PushProperty("Path", context.Request.Path))
        using (LogContext.PushProperty("QueryString", context.Request.QueryString.ToString()))
        using (LogContext.PushProperty("IPAddress", context.Connection.RemoteIpAddress?.ToString()))
        {
            try
            {
                // Add request ID to response headers
                context.Response.Headers.Add("X-Request-ID", requestId);

                _logger.LogInformation("Incoming request: {Method} {Path}",
                    context.Request.Method,
                    context.Request.Path);

                await _next(context);

                var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

                _logger.LogInformation("Request completed: {Method} {Path} {StatusCode} in {Duration}ms",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    duration);
            }
            catch (Exception ex)
            {
                var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                _logger.LogError(ex, "Request failed: {Method} {Path} in {Duration}ms",
                    context.Request.Method,
                    context.Request.Path,
                    duration);

                throw;
            }
        }
    }
}

/// <summary>
/// Extension methods for request logging middleware
/// </summary>
public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}
