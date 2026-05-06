using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SecureERP2.Modules.Finance.Services.Security
{
    /// <summary>
    /// 🔒 LAYER 1: Security Hardening (NON-NEGOTIABLE)
    /// Row-Level Security (CompanyId enforced at DB level)
    /// </summary>
    public class RowLevelSecurityService
    {
        private readonly ILogger<RowLevelSecurityService> _logger;
        private readonly ERPDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public RowLevelSecurityService(
            ILogger<RowLevelSecurityService> logger,
            ERPDbContext context,
            ICurrentUserService currentUserService)
        {
            _logger = logger;
            _context = context;
            _currentUserService = currentUserService;
        }

        /// <summary>
        /// Apply row-level security filter to query
        /// </summary>
        public IQueryable<T> ApplyRowLevelSecurity<T>(IQueryable<T> query) where T : class
        {
            try
            {
                var companyId = _currentUserService.CompanyId;
                if (companyId == 0)
                {
                    _logger.LogWarning("No CompanyId found in current user context");
                    return query.Take(0); // Return empty set if no CompanyId
                }

                // 🔥 Check if entity has CompanyId property
                var entityType = typeof(T);
                var companyIdProperty = entityType.GetProperty("CompanyId");
                
                if (companyIdProperty != null)
                {
                    // 🔥 Apply CompanyId filter at database level
                    var parameter = Expression.Parameter(typeof(T), "x");
                    var companyIdExpression = Expression.Property(parameter, "CompanyId");
                    var companyIdValue = Expression.Constant(companyId);
                    var equalityExpression = Expression.Equal(companyIdExpression, companyIdValue);
                    var lambda = Expression.Lambda<Func<T, bool>>(equalityExpression, parameter);
                    
                    query = query.Where(lambda);
                    
                    _logger.LogDebug("Applied row-level security filter for CompanyId {CompanyId} on entity {EntityType}", 
                        companyId, entityType.Name);
                }
                else
                {
                    _logger.LogWarning("Entity {EntityType} does not have CompanyId property for row-level security", entityType.Name);
                }

                return query;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying row-level security for entity {EntityType}", typeof(T).Name);
                throw;
            }
        }

        /// <summary>
        /// Validate entity belongs to current user's company
        /// </summary>
        public async Task<bool> ValidateEntityAccessAsync<T>(T entity) where T : class
        {
            try
            {
                var companyId = _currentUserService.CompanyId;
                if (companyId == 0)
                {
                    _logger.LogWarning("No CompanyId found in current user context");
                    return false;
                }

                var entityType = typeof(T);
                var companyIdProperty = entityType.GetProperty("CompanyId");
                
                if (companyIdProperty != null)
                {
                    var entityCompanyId = Convert.ToInt32(companyIdProperty.GetValue(entity));
                    var hasAccess = entityCompanyId == companyId;
                    
                    if (!hasAccess)
                    {
                        _logger.LogWarning("Access denied: Entity {EntityType} with CompanyId {EntityCompanyId} does not match user CompanyId {UserCompanyId}", 
                            entityType.Name, entityCompanyId, companyId);
                    }
                    
                    return hasAccess;
                }
                else
                {
                    _logger.LogWarning("Entity {EntityType} does not have CompanyId property for access validation", entityType.Name);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating entity access for {EntityType}", typeof(T).Name);
                return false;
            }
        }

        /// <summary>
        /// Create row-level security policy for PostgreSQL
        /// </summary>
        public async Task CreateRowLevelSecurityPolicyAsync(string tableName)
        {
            try
            {
                var companyId = _currentUserService.CompanyId;
                if (companyId == 0)
                {
                    throw new InvalidOperationException("No CompanyId found in current user context");
                }

                // 🔥 Create PostgreSQL RLS policy
                var policyName = $"rls_{tableName}_company_{companyId}";
                var policySql = $@"
                    DO $$
                    BEGIN
                        -- Drop existing policy if it exists
                        DROP POLICY IF EXISTS {policyName} ON {tableName};
                        
                        -- Create new row-level security policy
                        CREATE POLICY {policyName} ON {tableName}
                            FOR ALL
                            TO public
                            USING (CompanyId = {companyId});
                        
                        -- Enable row-level security on the table
                        ALTER TABLE {tableName} ENABLE ROW LEVEL SECURITY;
                        
                        -- Remove default policy if exists
                        DROP POLICY IF EXISTS {tableName}_default_policy ON {tableName};
                        
                        RAISE NOTICE 'Row-level security policy created for table {tableName}';
                    END $$;";

                await _context.Database.ExecuteSqlRawAsync(policySql);
                
                _logger.LogInformation("Created row-level security policy for table {TableName} with CompanyId {CompanyId}", 
                    tableName, companyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating row-level security policy for table {TableName}", tableName);
                throw;
            }
        }

        /// <summary>
        /// Apply row-level security to all finance tables
        /// </summary>
        public async Task ApplyRowLevelSecurityToFinanceTablesAsync()
        {
            try
            {
                var financeTables = new[]
                {
                    "FinanceAccounts",
                    "FinanceTransactions", 
                    "FinanceJournals",
                    "FinanceJournalEntries",
                    "Invoices",
                    "InvoiceItems",
                    "Employees",
                    "Salaries",
                    "PayrollRuns",
                    "TaxRules",
                    "FixedAssets",
                    "DepreciationSchedules",
                    "AuditSnapshots"
                };

                foreach (var table in financeTables)
                {
                    await CreateRowLevelSecurityPolicyAsync(table);
                }

                _logger.LogInformation("Applied row-level security to {Count} finance tables", financeTables.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying row-level security to finance tables");
                throw;
            }
        }

        /// <summary>
        /// Validate raw SQL query for CompanyId injection safety
        /// </summary>
        public bool ValidateSqlQuerySafety(string sql)
        {
            try
            {
                // 🔥 Check for dangerous SQL patterns
                var dangerousPatterns = new[]
                {
                    "(?i)(drop|delete|truncate|alter|create|exec|execute)\\s+(table|database|schema|procedure|function)",
                    "(?i)(union|select|insert|update)\\s+.*\\s+(into|from|set)",
                    "(?i)(--|#|/\\*|\\*/)",
                    "(?i)(xp_|sp_)",
                    "(?i)(waitfor|delay|sleep)"
                };

                foreach (var pattern in dangerousPatterns)
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(sql, pattern))
                    {
                        _logger.LogWarning("Dangerous SQL pattern detected: {Pattern}", pattern);
                        return false;
                    }
                }

                // 🔥 Ensure CompanyId is properly parameterized
                if (sql.Contains("CompanyId") && !sql.Contains("@CompanyId") && !sql.Contains(":CompanyId"))
                {
                    _logger.LogWarning("CompanyId found in SQL but not parameterized");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating SQL query safety");
                return false;
            }
        }

        /// <summary>
        /// Get secure SQL query with CompanyId parameter
        /// </summary>
        public string GetSecureSqlQuery(string baseSql, int companyId)
        {
            try
            {
                // 🔥 Replace any hardcoded CompanyId with parameter
                var secureSql = baseSql
                    .Replace($"CompanyId = {companyId}", "CompanyId = @CompanyId")
                    .Replace($"CompanyId={companyId}", "CompanyId=@CompanyId")
                    .Replace($"\"CompanyId\" = {companyId}", "\"CompanyId\" = @CompanyId")
                    .Replace($"'CompanyId' = {companyId}", "'CompanyId' = @CompanyId");

                // 🔥 Add CompanyId parameter if not present
                if (!secureSql.Contains("@CompanyId") && !secureSql.Contains(":CompanyId"))
                {
                    // Add WHERE clause if not present
                    if (!secureSql.Contains("WHERE", StringComparison.OrdinalIgnoreCase))
                    {
                        secureSql += " WHERE CompanyId = @CompanyId";
                    }
                    else
                    {
                        secureSql += " AND CompanyId = @CompanyId";
                    }
                }

                return secureSql;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating secure SQL query");
                throw;
            }
        }
    }

    /// <summary>
    /// Current user service interface
    /// </summary>
    public interface ICurrentUserService
    {
        int CompanyId { get; }
        string UserId { get; }
        string UserName { get; }
        bool IsAuthenticated { get; }
    }

    /// <summary>
    /// Current user service implementation
    /// </summary>
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public int CompanyId
        {
            get
            {
                var context = _httpContextAccessor.HttpContext;
                if (context?.User?.Identity?.IsAuthenticated == true)
                {
                    var companyIdClaim = context.User.FindFirst("CompanyId");
                    if (companyIdClaim != null && int.TryParse(companyIdClaim.Value, out var companyId))
                    {
                        return companyId;
                    }
                }
                return 0;
            }
        }

        public string UserId
        {
            get
            {
                var context = _httpContextAccessor.HttpContext;
                return context?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            }
        }

        public string UserName
        {
            get
            {
                var context = _httpContextAccessor.HttpContext;
                return context?.User?.Identity?.Name ?? string.Empty;
            }
        }

        public bool IsAuthenticated
        {
            get
            {
                var context = _httpContextAccessor.HttpContext;
                return context?.User?.Identity?.IsAuthenticated == true;
            }
        }
    }
}
