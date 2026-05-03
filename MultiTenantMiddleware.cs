using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace SecureERP2
{
    // Multi-tenant middleware to extract CompanyId from JWT and set it in DbContext
    public class MultiTenantMiddleware
    {
        private readonly RequestDelegate _next;

        public MultiTenantMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ERPDbContext dbContext)
        {
            try
            {
                // Extract CompanyId from JWT token
                var companyIdClaim = context.User?.FindFirst("CompanyId");
                
                if (companyIdClaim != null && int.TryParse(companyIdClaim.Value, out int companyId))
                {
                    // Set the CompanyId in the DbContext for this request
                    dbContext.SetCurrentCompanyId(companyId);
                }
                else
                {
                    // For unauthenticated requests, don't set CompanyId (null means no filtering)
                    // This allows system-wide operations like seeding data
                    dbContext.SetCurrentCompanyId(null);
                }

                await _next(context);
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                Console.WriteLine($"Multi-tenant middleware error: {ex.Message}");
                throw;
            }
        }
    }

    // Extension method to register the middleware
    public static class MultiTenantMiddlewareExtensions
    {
        public static IApplicationBuilder UseMultiTenant(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<MultiTenantMiddleware>();
        }
    }
}
