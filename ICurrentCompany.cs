namespace SecureERP2
{
    // Central tenant resolver interface - VERY IMPORTANT
    public interface ICurrentCompany
    {
        int CompanyId { get; }
        string CompanyName { get; }
        bool IsValid { get; }
        string? UserId { get; }
        string? UserName { get; }
        string? UserRole { get; }
    }

    // Current company implementation with JWT-based resolution
    public class CurrentCompany : ICurrentCompany
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<CurrentCompany> _logger;

        public CurrentCompany(IHttpContextAccessor httpContextAccessor, ILogger<CurrentCompany> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public int CompanyId => GetCompanyIdFromContext();
        public string CompanyName => GetCompanyNameFromContext();
        public bool IsValid => GetCompanyIdFromContext() > 0;
        public string? UserId => GetUserIdFromContext();
        public string? UserName => GetUserNameFromContext();
        public string? UserRole => GetUserRoleFromContext();

        private int GetCompanyIdFromContext()
        {
            try
            {
                var context = _httpContextAccessor.HttpContext;
                if (context == null) return 0;

                // Extract CompanyId from JWT claim (never trust frontend)
                var companyIdClaim = context.User?.FindFirst("CompanyId");
                if (companyIdClaim != null && int.TryParse(companyIdClaim.Value, out var companyId))
                {
                    return companyId;
                }

                _logger.LogWarning("CompanyId not found in JWT claims");
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting CompanyId from context");
                return 0;
            }
        }

        private string GetCompanyNameFromContext()
        {
            try
            {
                var context = _httpContextAccessor.HttpContext;
                if (context == null) return string.Empty;

                // Try to get company name from claims (if available)
                var companyNameClaim = context.User?.FindFirst("CompanyName");
                if (companyNameClaim != null)
                {
                    return companyNameClaim.Value;
                }

                // Fallback to empty string - will be resolved by database if needed
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting CompanyName from context");
                return string.Empty;
            }
        }

        private string? GetUserIdFromContext()
        {
            try
            {
                var context = _httpContextAccessor.HttpContext;
                if (context == null) return null;

                var userIdClaim = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                return userIdClaim?.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting UserId from context");
                return null;
            }
        }

        private string? GetUserNameFromContext()
        {
            try
            {
                var context = _httpContextAccessor.HttpContext;
                if (context == null) return null;

                var userNameClaim = context.User?.FindFirst(System.Security.Claims.ClaimTypes.Name);
                return userNameClaim?.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting UserName from context");
                return null;
            }
        }

        private string? GetUserRoleFromContext()
        {
            try
            {
                var context = _httpContextAccessor.HttpContext;
                if (context == null) return null;

                var userRoleClaim = context.User?.FindFirst(System.Security.Claims.ClaimTypes.Role);
                return userRoleClaim?.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting UserRole from context");
                return null;
            }
        }
    }

    // Extension methods for easy ICurrentCompany usage
    public static class CurrentCompanyExtensions
    {
        public static bool HasAccessToCompany(this ICurrentCompany currentCompany, int companyId)
        {
            return currentCompany.IsValid && currentCompany.CompanyId == companyId;
        }

        public static bool IsSystemAdmin(this ICurrentCompany currentCompany)
        {
            return currentCompany.IsValid && currentCompany.UserRole == "Admin";
        }

        public static bool CanManageUsers(this ICurrentCompany currentCompany)
        {
            return currentCompany.IsValid && 
                   (currentCompany.UserRole == "Admin" || currentCompany.UserRole == "Finance");
        }

        public static bool CanAccessFinance(this ICurrentCompany currentCompany)
        {
            return currentCompany.IsValid && 
                   (currentCompany.UserRole == "Admin" || currentCompany.UserRole == "Finance");
        }

        public static bool CanAccessWarehouse(this ICurrentCompany currentCompany)
        {
            return currentCompany.IsValid && 
                   (currentCompany.UserRole == "Admin" || currentCompany.UserRole == "Warehouse");
        }

        public static bool CanAccessSales(this ICurrentCompany currentCompany)
        {
            return currentCompany.IsValid && 
                   (currentCompany.UserRole == "Admin" || currentCompany.UserRole == "Sales");
        }

        public static string GetCompanyContextInfo(this ICurrentCompany currentCompany)
        {
            if (!currentCompany.IsValid)
            {
                return "No valid company context";
            }

            return $"Company: {currentCompany.CompanyName} (ID: {currentCompany.CompanyId}), " +
                   $"User: {currentCompany.UserName} ({currentCompany.UserRole})";
        }
    }
}
