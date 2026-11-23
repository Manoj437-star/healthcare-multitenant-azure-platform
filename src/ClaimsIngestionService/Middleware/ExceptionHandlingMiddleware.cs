using System.Net;
using System.Text.Json;

namespace ClaimsIngestionService.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // Do not log raw claim payload or PHI - log exception type, message and stack trace securely
            _logger.LogError(ex, "Unhandled exception during claims processing: {ExceptionType} - {Message}",
                ex.GetType().Name, ex.Message);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Response.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString();
        var statusCode = exception switch
        {
            ArgumentException or InvalidOperationException => HttpStatusCode.BadRequest,
            KeyNotFoundException => HttpStatusCode.NotFound,
            UnauthorizedAccessException => HttpStatusCode.Forbidden,
            _ => HttpStatusCode.InternalServerError
        };

        var problem = new
        {
            type = "https://tools.ietf.org/html/rfc7807",
            title = statusCode == HttpStatusCode.InternalServerError ? "An unexpected error occurred" : exception.Message,
            status = (int)statusCode,
            detail = statusCode == HttpStatusCode.InternalServerError ? "An error occurred while processing the claim. Reference correlation ID." : exception.Message,
            instance = context.Request.Path.Value,
            correlationId
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;
        return context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}
