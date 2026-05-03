using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SecureERP2
{
    // Company context middleware for automatic CompanyId injection
    public class CompanyContextMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<CompanyContextMiddleware> _logger;

        public CompanyContextMiddleware(RequestDelegate next, ILogger<CompanyContextMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip CompanyId validation for login/register endpoints and static files
            var path = context.Request.Path.Value?.ToLower();
            var skipPaths = new[] { "/api/auth/login", "/api/auth/register", "/swagger", "/health", "/api/info" };
            
            if (skipPaths.Any(p => path?.StartsWith(p) == true))
            {
                await _next(context);
                return;
            }

            // Validate CompanyId exists in user claims
            var companyIdClaim = context.User?.FindFirst("CompanyId");
            
            if (companyIdClaim == null)
            {
                _logger.LogWarning("CompanyId claim not found for user: {User}", context.User?.Identity?.Name);
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Access denied: Company context not found");
                return;
            }

            // Validate CompanyId is a valid integer
            if (!int.TryParse(companyIdClaim.Value, out var companyId))
            {
                _logger.LogError("Invalid CompanyId format: {CompanyId}", companyIdClaim.Value);
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Access denied: Invalid company context");
                return;
            }

            // Add CompanyId to HttpContext for easy access in controllers
            context.Items["CompanyId"] = companyId;

            await _next(context);
        }
    }

    // Extension method for easy CompanyId access
    public static class HttpContextExtensions
    {
        public static int? GetCompanyId(this HttpContext context)
        {
            if (context.Items.TryGetValue("CompanyId", out var companyId))
            {
                return (int)companyId;
            }
            
            // Fallback to claims
            var claim = context.User?.FindFirst("CompanyId");
            if (claim != null && int.TryParse(claim.Value, out var id))
            {
                return id;
            }
            
            return null;
        }

        public static bool HasCompanyId(this HttpContext context)
        {
            return context.GetCompanyId().HasValue;
        }
    }
}
