using System.Text.RegularExpressions;

namespace SecureERP2
{
    // 🔒 SQL Security Analyzer to prevent multi-tenant bypass attacks
    public class SqlSecurityAnalyzer
    {
        private static readonly string[] BannedKeywords = {
            "IgnoreQueryFilters", "AsNoTracking", "FromSqlRaw", "ExecuteSqlRaw", 
            "ExecuteSqlInterpolated", "Database.SqlQuery", "Query", "SqlQuery"
        };

        private static readonly string[] DangerousPatterns = {
            @"(?i)\bIGNORE\b.*\bQUERY\b.*\bFILTERS\b",
            @"(?i)\bFROM\b.*\bCOMPANIES\b.*\bWHERE\b.*\b1\s*=\s*1",
            @"(?i)\bDELETE\b.*\bFROM\b.*\bWITHOUT\b.*\bWHERE\b",
            @"(?i)\bUPDATE\b.*\bSET\b.*\bCompanyId\b.*=\s*\d+",
            @"(?i)\bINSERT\b.*\bINTO\b.*\bCompanyId\b.*\bVALUES\b"
        };

        private static readonly string[] MultiTenantTables = {
            "Users", "UserSessions", "UserPermissions", "FinanceAccounts", 
            "Products", "Inventory", "Orders", "OrderItems", 
            "Transactions", "AnalyticsSummaries", "AuditLogs"
        };

        // 🔒 Analyze SQL for multi-tenant security violations
        public static SqlSecurityResult AnalyzeSql(string sql, int? currentCompanyId)
        {
            var result = new SqlSecurityResult { IsSecure = true, Warnings = new List<string>() };

            // Check for banned keywords
            foreach (var keyword in BannedKeywords)
            {
                if (sql.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    result.IsSecure = false;
                    result.Violations.Add($"❌ BANNED KEYWORD: '{keyword}' detected in SQL. This can bypass multi-tenant security.");
                }
            }

            // Check for dangerous patterns
            foreach (var pattern in DangerousPatterns)
            {
                if (Regex.IsMatch(sql, pattern))
                {
                    result.IsSecure = false;
                    result.Violations.Add($"❌ DANGEROUS PATTERN: '{pattern}' detected. Potential multi-tenant bypass attempt.");
                }
            }

            // Check for multi-tenant table access without CompanyId filter
            foreach (var table in MultiTenantTables)
            {
                if (sql.Contains(table, StringComparison.OrdinalIgnoreCase))
                {
                    if (!HasCompanyIdFilter(sql, table))
                    {
                        result.IsSecure = false;
                        result.Violations.Add($"❌ MISSING COMPANYID FILTER: Table '{table}' accessed without CompanyId filter.");
                    }
                }
            }

            // Check for hardcoded CompanyId values
            if (Regex.IsMatch(sql, @"CompanyId\s*=\s*\d+", RegexOptions.IgnoreCase))
            {
                result.Warnings.Add("⚠️  WARNING: Hardcoded CompanyId detected. Consider using parameterized queries with current user's CompanyId.");
            }

            // Check for potential data leakage patterns
            if (sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase) && 
                !sql.Contains("WHERE", StringComparison.OrdinalIgnoreCase) &&
                MultiTenantTables.Any(table => sql.Contains(table, StringComparison.OrdinalIgnoreCase)))
            {
                result.IsSecure = false;
                result.Violations.Add("❌ DATA LEAKAGE RISK: SELECT statement on multi-tenant table without WHERE clause.");
            }

            return result;
        }

        // 🔒 Inject automatic CompanyId filtering into SQL
        public static string SecureSql(string sql, int? currentCompanyId)
        {
            if (!currentCompanyId.HasValue) return sql;

            var secureSql = sql;

            foreach (var table in MultiTenantTables)
            {
                if (secureSql.Contains(table, StringComparison.OrdinalIgnoreCase))
                {
                    secureSql = InjectCompanyIdFilter(secureSql, table, currentCompanyId.Value);
                }
            }

            return secureSql;
        }

        // 🔒 Check if SQL has proper CompanyId filtering
        private static bool HasCompanyIdFilter(string sql, string tableName)
        {
            var tablePattern = $@"(?i)(FROM|JOIN|UPDATE|DELETE\s+FROM)\s+{tableName}\b";
            var wherePattern = $@"(?i)WHERE\s+.*{tableName}\.?\s*CompanyId\s*=\s*\?";

            return Regex.IsMatch(sql, wherePattern) || 
                   Regex.IsMatch(sql, $@"(?i)WHERE\s+.*CompanyId\s*=\s*\?.*{tableName}");
        }

        // 🔒 Inject CompanyId filter into SQL query
        private static string InjectCompanyIdFilter(string sql, string tableName, int companyId)
        {
            var patterns = new[]
            {
                $@"(?i)(FROM\s+{tableName}\s*)(?!(WHERE|JOIN|GROUP|ORDER|HAVING|LIMIT))",
                $@"(?i)(FROM\s+{tableName}\s+AS\s+\w+\s*)(?!(WHERE|JOIN|GROUP|ORDER|HAVING|LIMIT))"
            };

            foreach (var pattern in patterns)
            {
                var regex = new Regex(pattern);
                if (regex.IsMatch(sql))
                {
                    return regex.Replace(sql, $"$1 WHERE CompanyId = {companyId}");
                }
            }

            // If no pattern matched, try to add WHERE clause before other clauses
            var whereInsertPattern = $@"(?i)(FROM\s+{tableName}.*?)(GROUP\s+BY|ORDER\s+BY|HAVING|LIMIT|$)";
            var whereRegex = new Regex(whereInsertPattern);
            if (whereRegex.IsMatch(sql))
            {
                return whereRegex.Replace(sql, $"$1 WHERE CompanyId = {companyId} $2");
            }

            return sql;
        }

        // 🔒 Validate entity inheritance for multi-tenant security
        public static bool ValidateEntitySecurity(Type entityType)
        {
            // Company entity is exempt from BaseEntity requirement
            if (entityType == typeof(Company)) return true;

            // All other entities must inherit from BaseEntity
            return typeof(BaseEntity).IsAssignableFrom(entityType);
        }
    }

    // 🔒 SQL Security Analysis Result
    public class SqlSecurityResult
    {
        public bool IsSecure { get; set; }
        public List<string> Violations { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();

        public string GetSummary()
        {
            if (!IsSecure)
            {
                return $"🚨 SQL SECURITY VIOLATION DETECTED:\n{string.Join("\n", Violations)}";
            }

            if (Warnings.Any())
            {
                return $"⚠️  SQL SECURITY WARNINGS:\n{string.Join("\n", Warnings)}";
            }

            return "✅ SQL is secure for multi-tenant operations.";
        }
    }
}
