using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SecureERP2
{
    // 🔧 Middleware Enforcement - Extract CompanyId from JWT, Block if missing, Inject into context
    public class TenantEnforcementMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TenantEnforcementMiddleware> _logger;

        public TenantEnforcementMiddleware(RequestDelegate next, ILogger<TenantEnforcementMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip enforcement for login/register endpoints and static files
            var path = context.Request.Path.Value?.ToLower();
            var skipPaths = new[] { 
                "/api/auth/login", 
                "/api/auth/register", 
                "/swagger", 
                "/health", 
                "/api/info",
                "/api/auth/current-context"
            };
            
            if (skipPaths.Any(p => path?.StartsWith(p) == true))
            {
                await _next(context);
                return;
            }

            // 🔧 STEP 1: Extract CompanyId from JWT
            var companyIdClaim = context.User?.FindFirst("CompanyId");
            
            if (companyIdClaim == null)
            {
                _logger.LogWarning("CompanyId claim not found in JWT for user: {User} at path: {Path}", 
                    context.User?.Identity?.Name, context.Request.Path);
                
                // 🔧 STEP 2: Block request if missing
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Access denied: Company context not found in JWT");
                return;
            }

            // Validate CompanyId is a valid integer
            if (!int.TryParse(companyIdClaim.Value, out var companyId))
            {
                _logger.LogError("Invalid CompanyId format in JWT: {CompanyId} for user: {User}", 
                    companyIdClaim.Value, context.User?.Identity?.Name);
                
                // 🔧 STEP 2: Block request if invalid
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Access denied: Invalid company context in JWT");
                return;
            }

            // Additional validation: Ensure CompanyId is positive
            if (companyId <= 0)
            {
                _logger.LogError("Invalid CompanyId value in JWT: {CompanyId} for user: {User}", 
                    companyId, context.User?.Identity?.Name);
                
                // 🔧 STEP 2: Block request if invalid
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Access denied: Invalid company context in JWT");
                return;
            }

            // 🔧 STEP 3: Inject into HttpContext for automatic access
            context.Items["CompanyId"] = companyId;
            context.Items["CompanyIdValidated"] = true;

            // Also inject user information for easier access
            var userIdClaim = context.User?.FindFirst(ClaimTypes.NameIdentifier);
            var userNameClaim = context.User?.FindFirst(ClaimTypes.Name);
            var userRoleClaim = context.User?.FindFirst(ClaimTypes.Role);

            if (userIdClaim != null)
                context.Items["UserId"] = userIdClaim.Value;
            
            if (userNameClaim != null)
                context.Items["UserName"] = userNameClaim.Value;
            
            if (userRoleClaim != null)
                context.Items["UserRole"] = userRoleClaim.Value;

            // Log successful tenant enforcement
            _logger.LogInformation("Tenant enforcement successful: User={User}, CompanyId={CompanyId}, Path={Path}", 
                context.User?.Identity?.Name, companyId, context.Request.Path);

            // Continue to next middleware
            await _next(context);
        }
    }

    // Extension methods for easy HttpContext access
    public static class TenantHttpContextExtensions
    {
        // Get validated CompanyId from HttpContext
        public static int? GetTenantCompanyId(this HttpContext context)
        {
            if (context.Items.TryGetValue("CompanyId", out var companyId))
            {
                return (int)companyId;
            }
            return null;
        }

        // Check if CompanyId has been validated
        public static bool IsTenantValidated(this HttpContext context)
        {
            return context.Items.ContainsKey("CompanyIdValidated") && 
                   (bool)context.Items["CompanyIdValidated"];
        }

        // Get user information from HttpContext
        public static string? GetTenantUserId(this HttpContext context)
        {
            return context.Items.TryGetValue("UserId", out var userId) ? userId.ToString() : null;
        }

        public static string? GetTenantUserName(this HttpContext context)
        {
            return context.Items.TryGetValue("UserName", out var userName) ? userName.ToString() : null;
        }

        public static string? GetTenantUserRole(this HttpContext context)
        {
            return context.Items.TryGetValue("UserRole", out var userRole) ? userRole.ToString() : null;
        }

        // Get complete tenant context information
        public static object GetTenantContext(this HttpContext context)
        {
            return new
            {
                CompanyId = context.GetTenantCompanyId(),
                UserId = context.GetTenantUserId(),
                UserName = context.GetTenantUserName(),
                UserRole = context.GetTenantUserRole(),
                IsValid = context.IsTenantValidated(),
                Path = context.Request.Path,
                Method = context.Request.Method
            };
        }

        // Helper method to enforce tenant validation in endpoints
        public static bool RequireTenant(this HttpContext context)
        {
            if (!context.IsTenantValidated())
            {
                throw new UnauthorizedAccessException("Tenant context not validated");
            }
            return true;
        }

        // Helper method to get CompanyId with validation
        public static int RequireCompanyId(this HttpContext context)
        {
            context.RequireTenant();
            var companyId = context.GetTenantCompanyId();
            if (!companyId.HasValue)
            {
                throw new UnauthorizedAccessException("CompanyId not available");
            }
            return companyId.Value;
        }
    }

    // Custom exception for tenant enforcement violations
    public class TenantEnforcementException : Exception
    {
        public TenantEnforcementException(string message) : base(message) { }
        public TenantEnforcementException(string message, Exception innerException) : base(message, innerException) { }
    }
}
