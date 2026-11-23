using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using ClaimsIngestionService.Services;

namespace ClaimsIngestionService.Middleware;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }
        context.Response.Headers["X-Correlation-ID"] = correlationId;

        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        if (path.StartsWith("/health") || path.StartsWith("/ready") || path.StartsWith("/swagger"))
        {
            await _next(context);
            return;
        }

        var tenantHeader = context.Request.Headers["X-Tenant-ID"].FirstOrDefault()?.Trim().ToLowerInvariant();

        string? jwtTenantId = null;
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var tokenString = authHeader.Substring("Bearer ".Length).Trim();
            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(tokenString))
                {
                    var jwt = handler.ReadJwtToken(tokenString);
                    jwtTenantId = jwt.Claims.FirstOrDefault(c => c.Type == "tenant_id" || c.Type == "tid")?.Value?.ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse JWT token for tenant verification.");
            }
        }

        if (!string.IsNullOrEmpty(jwtTenantId) && !string.IsNullOrEmpty(tenantHeader) && !string.Equals(jwtTenantId, tenantHeader, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Security Violation: Request header tenant '{TenantHeader}' does not match authenticated JWT tenant '{JwtTenantId}'.",
                tenantHeader, jwtTenantId);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            var errorResponse = new { error = "Forbidden", message = "Tenant ID in request header does not match authenticated token claim.", correlationId };
            await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
            return;
        }

        var effectiveTenantId = jwtTenantId ?? tenantHeader;

        if (string.IsNullOrWhiteSpace(effectiveTenantId))
        {
            _logger.LogWarning("Missing tenant identification for request to {Path}.", path);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            var errorResponse = new { error = "BadRequest", message = "Missing required 'X-Tenant-ID' header or authenticated tenant token claim.", correlationId };
            await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
            return;
        }

        tenantContext.SetTenant(effectiveTenantId, correlationId);

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["TenantId"] = effectiveTenantId,
            ["CorrelationId"] = correlationId
        }))
        {
            await _next(context);
        }
    }
}
