using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace SecureERP2
{
    // Security validation middleware - NEVER trust frontend data
    public class SecurityValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SecurityValidationMiddleware> _logger;
        private readonly IConfiguration _configuration;

        public SecurityValidationMiddleware(RequestDelegate next, ILogger<SecurityValidationMiddleware> logger, IConfiguration configuration)
        {
            _next = next;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip validation for login/register endpoints and static files
            var path = context.Request.Path.Value?.ToLower();
            var skipPaths = new[] { "/api/auth/login", "/api/auth/register", "/swagger", "/health", "/api/info" };
            
            if (skipPaths.Any(p => path?.StartsWith(p) == true))
            {
                await _next(context);
                return;
            }

            // CRITICAL: Extract CompanyId from JWT - NEVER trust frontend
            var companyIdClaim = context.User?.FindFirst("CompanyId");
            
            if (companyIdClaim == null)
            {
                _logger.LogWarning("CompanyId claim not found in JWT for user: {User}", context.User?.Identity?.Name);
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Access denied: Company context not found in JWT");
                return;
            }

            // Validate CompanyId is a valid integer
            if (!int.TryParse(companyIdClaim.Value, out var companyId))
            {
                _logger.LogError("Invalid CompanyId format in JWT: {CompanyId}", companyIdClaim.Value);
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Access denied: Invalid company context in JWT");
                return;
            }

            // CRITICAL: Validate JWT token integrity
            if (!await ValidateJwtToken(context))
            {
                _logger.LogWarning("Invalid JWT token detected for user: {User}", context.User?.Identity?.Name);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Access denied: Invalid token");
                return;
            }

            // CRITICAL: Add validated CompanyId to HttpContext (from JWT only)
            context.Items["CompanyId"] = companyId;

            // CRITICAL: Log security validation
            _logger.LogInformation("Security validation passed: User={User}, CompanyId={CompanyId}, Path={Path}", 
                context.User?.Identity?.Name, companyId, context.Request.Path);

            await _next(context);
        }

        private async Task<bool> ValidateJwtToken(HttpContext context)
        {
            try
            {
                var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
                
                if (string.IsNullOrEmpty(token))
                {
                    return false;
                }

                var jwtKey = _configuration["Jwt:Key"] ?? "YourSuperSecretKeyForERPSystem123456789";
                var jwtIssuer = _configuration["Jwt:Issuer"] ?? "ERPSystem";
                var jwtAudience = _configuration["Jwt:Audience"] ?? "ERPSystem";

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = System.Text.Encoding.ASCII.GetBytes(jwtKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = jwtAudience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
                
                // Ensure the token has required claims
                var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
                var companyIdClaim = principal.FindFirst("CompanyId");
                
                if (userIdClaim == null || companyIdClaim == null)
                {
                    return false;
                }

                // Update context with validated principal
                context.User = principal;
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "JWT token validation failed");
                return false;
            }
        }
    }

    // Extension method for secure CompanyId access (from JWT only)
    public static class SecurityHttpContextExtensions
    {
        public static int? GetSecureCompanyId(this HttpContext context)
        {
            // CRITICAL: Only use CompanyId from validated JWT, never from headers or body
            if (context.Items.TryGetValue("CompanyId", out var companyId))
            {
                return (int)companyId;
            }
            
            // Fallback to JWT claims (already validated)
            var claim = context.User?.FindFirst("CompanyId");
            if (claim != null && int.TryParse(claim.Value, out var id))
            {
                return id;
            }
            
            return null;
        }

        public static bool HasSecureCompanyId(this HttpContext context)
        {
            return context.GetSecureCompanyId().HasValue;
        }

        // CRITICAL: Get CompanyId from JWT only, ignore frontend input
        public static int GetCompanyIdFromJwt(this HttpContext context)
        {
            var claim = context.User?.FindFirst("CompanyId");
            if (claim != null && int.TryParse(claim.Value, out var id))
            {
                return id;
            }
            
            throw new SecurityException("CompanyId not found in JWT token");
        }
    }

    // Custom exception for security violations
    public class SecurityException : Exception
    {
        public SecurityException(string message) : base(message) { }
    }
}
