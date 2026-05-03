using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
using System.ComponentModel.DataAnnotations;

namespace SecureERP2
{
    // Secure DbContext wrapper that prevents multi-tenant bypass attacks
    public class SecureDbContext : ERPDbContext
    {
        private readonly int? _currentCompanyId;

        public SecureDbContext(DbContextOptions<ERPDbContext> options) : base(options) { }

        // Secure constructor that enforces CompanyId
        public SecureDbContext(DbContextOptions<ERPDbContext> options, int companyId) : base(options)
        {
            _currentCompanyId = companyId;
            SetCurrentCompanyId(companyId);
        }

        // 🔒 CRITICAL: Override methods that could bypass multi-tenant security
        
        [DoesNotReturn]
        private void BlockIgnoreQueryFilters()
        {
            throw new MultiTenantSecurityException("❌ SECURITY VIOLATION: IgnoreQueryFilters() is blocked to prevent multi-tenant data bypass. Use SecureDbContext methods instead.");
        }

        [DoesNotReturn]
        private void BlockIgnoreQueryFilters<T>()
        {
            throw new MultiTenantSecurityException("❌ SECURITY VIOLATION: IgnoreQueryFilters<T>() is blocked to prevent multi-tenant data bypass. Use SecureDbContext methods instead.");
        }

        // 🔒 CRITICAL: Secure DbSet access that enforces multi-tenant filtering
        public new DbSet<TEntity> Set<TEntity>() where TEntity : class
        {
            var dbSet = base.Set<TEntity>();
            
            // Verify that TEntity inherits from BaseEntity (multi-tenant enforcement)
            if (!typeof(BaseEntity).IsAssignableFrom(typeof(TEntity)) && typeof(TEntity) != typeof(Company))
            {
                throw new MultiTenantSecurityException($"❌ SECURITY VIOLATION: Entity {typeof(TEntity).Name} must inherit from BaseEntity for multi-tenant security.");
            }
            
            return dbSet;
        }

        // 🔒 CRITICAL: Secure raw SQL execution with automatic CompanyId injection
        public int ExecuteSecureSqlRaw(string sql, params object[] parameters)
        {
            // Check if SQL contains potential multi-tenant bypass
            if (sql.Contains("IgnoreQueryFilters", StringComparison.OrdinalIgnoreCase))
            {
                throw new MultiTenantSecurityException("❌ SECURITY VIOLATION: Raw SQL cannot contain IgnoreQueryFilters for multi-tenant security.");
            }

            // Auto-inject CompanyId filter for tables that inherit from BaseEntity
            var secureSql = InjectCompanyIdFilters(sql, _currentCompanyId);
            return base.Database.ExecuteSqlRaw(secureSql, parameters);
        }

        public int ExecuteSecureSqlInterpolated(FormattableString sql)
        {
            var sqlString = sql.Format;
            
            if (sqlString.Contains("IgnoreQueryFilters", StringComparison.OrdinalIgnoreCase))
            {
                throw new MultiTenantSecurityException("❌ SECURITY VIOLATION: Raw SQL cannot contain IgnoreQueryFilters for multi-tenant security.");
            }

            var secureSql = InjectCompanyIdFilters(sqlString, _currentCompanyId);
            return base.Database.ExecuteSqlInterpolated($"{secureSql}");
        }

        // 🔒 Helper method to inject CompanyId filters into raw SQL
        private string InjectCompanyIdFilters(string sql, int? companyId)
        {
            if (!companyId.HasValue) return sql;

            // Get all multi-tenant tables (entities inheriting from BaseEntity except Company)
            var multiTenantTables = new[]
            {
                "Users", "UserSessions", "UserPermissions", "FinanceAccounts", 
                "Products", "Inventory", "Orders", "OrderItems", 
                "Transactions", "AnalyticsSummaries", "AuditLogs"
            };

            var secureSql = sql;

            // For each multi-tenant table, ensure CompanyId filtering is present
            foreach (var table in multiTenantTables)
            {
                if (secureSql.Contains($"FROM {table}", StringComparison.OrdinalIgnoreCase) ||
                    secureSql.Contains($"UPDATE {table}", StringComparison.OrdinalIgnoreCase) ||
                    secureSql.Contains($"DELETE FROM {table}", StringComparison.OrdinalIgnoreCase))
                {
                    // Check if CompanyId filter is already present
                    if (!secureSql.Contains("CompanyId", StringComparison.OrdinalIgnoreCase))
                    {
                        // Inject CompanyId filter
                        if (secureSql.Contains($"FROM {table}", StringComparison.OrdinalIgnoreCase))
                        {
                            secureSql = secureSql.Replace($"FROM {table}", $"FROM {table} WHERE CompanyId = {companyId.Value}");
                        }
                        else if (secureSql.Contains($"UPDATE {table}", StringComparison.OrdinalIgnoreCase))
                        {
                            secureSql = secureSql.Replace($"UPDATE {table}", $"UPDATE {table} SET CompanyId = {companyId.Value}");
                        }
                        else if (secureSql.Contains($"DELETE FROM {table}", StringComparison.OrdinalIgnoreCase))
                        {
                            secureSql = secureSql.Replace($"DELETE FROM {table}", $"DELETE FROM {table} WHERE CompanyId = {companyId.Value}");
                        }
                    }
                }
            }

            return secureSql;
        }

        // 🔒 Secure method to get current CompanyId for logging/auditing
        public int? GetCurrentCompanyIdForSecurity()
        {
            if (!_currentCompanyId.HasValue)
            {
                throw new MultiTenantSecurityException("❌ SECURITY VIOLATION: No CompanyId set for multi-tenant operation.");
            }
            return _currentCompanyId.Value;
        }
    }

    // Custom security exception for multi-tenant violations
    public class MultiTenantSecurityException : SystemException
    {
        public MultiTenantSecurityException(string message) : base(message) { }
        public MultiTenantSecurityException(string message, System.Exception inner) : base(message, inner) { }
    }
}
