using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace SecureERP2
{
    // 🔒 Compile-time and runtime validator for multi-tenant entity security
    public static class EntitySecurityValidator
    {
        private static readonly List<Type> ExemptEntities = new List<Type>
        {
            typeof(Company), // Company entity is exempt - needs to be accessible for login
            typeof(Role),    // Role entity is system-wide
            typeof(RolePermission) // RolePermission is system-wide
        };

        // 🔒 Validate all entities in the assembly inherit from BaseEntity
        public static EntityValidationResult ValidateAssemblyEntities(Assembly assembly)
        {
            var result = new EntityValidationResult { IsValid = true };

            // Get all entity types in the assembly
            var entityTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.GetProperties().Any(p => p.Name == "Id"))
                .ToList();

            foreach (var entityType in entityTypes)
            {
                ValidateEntity(entityType, result);
            }

            return result;
        }

        // 🔒 Validate single entity for multi-tenant compliance
        public static void ValidateEntity(Type entityType, EntityValidationResult result)
        {
            // Skip exempt entities and system types
            if (ExemptEntities.Contains(entityType) || 
                entityType.Name.StartsWith("<>f__AnonymousType") || // Anonymous types from LINQ
                entityType.Name.EndsWith("Response") || // DTO response types
                entityType.Name.EndsWith("Info") || // DTO info types
                entityType.Name.EndsWith("Request") || // DTO request types
                entityType.Namespace?.StartsWith("Microsoft.") == true) // System types
            {
                result.ExemptEntities.Add(entityType.Name);
                return;
            }

            // Check if entity inherits from BaseEntity
            if (!typeof(BaseEntity).IsAssignableFrom(entityType))
            {
                result.IsValid = false;
                result.Violations.Add($"❌ ENTITY SECURITY VIOLATION: {entityType.Name} does not inherit from BaseEntity. This creates multi-tenant data leak risk.");
                return;
            }

            // Check if entity has CompanyId property (inherited from BaseEntity)
            var companyIdProperty = entityType.GetProperty("CompanyId");
            if (companyIdProperty == null)
            {
                result.IsValid = false;
                result.Violations.Add($"❌ ENTITY SECURITY VIOLATION: {entityType.Name} missing CompanyId property. Multi-tenant isolation compromised.");
                return;
            }

            // Check if entity has proper navigation properties (optional but recommended)
            var navigationProperties = entityType.GetProperties()
                .Where(p => p.PropertyType.IsGenericType && 
                           p.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>))
                .ToList();

            result.ValidEntities.Add(entityType.Name);
        }

        // 🔒 Runtime validation for DbContext operations
        public static void ValidateDbContextOperation<TEntity>()
        {
            var entityType = typeof(TEntity);
            
            if (ExemptEntities.Contains(entityType))
            {
                return; // Exempt entities are allowed
            }

            if (!typeof(BaseEntity).IsAssignableFrom(entityType))
            {
                throw new SecurityException(
                    $"❌ RUNTIME SECURITY VIOLATION: Entity {entityType.Name} does not inherit from BaseEntity. " +
                    $"This operation is blocked to prevent multi-tenant data leaks.");
            }
        }

        // 🔒 Validate that all DbSet properties in DbContext are secure
        public static EntityValidationResult ValidateDbContextDbSets(DbContext context)
        {
            var result = new EntityValidationResult { IsValid = true };

            var dbSetProperties = context.GetType().GetProperties()
                .Where(p => p.PropertyType.IsGenericType && 
                           p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
                .ToList();

            foreach (var dbSetProperty in dbSetProperties)
            {
                var entityType = dbSetProperty.PropertyType.GetGenericArguments()[0];
                ValidateEntity(entityType, result);
            }

            return result;
        }

        // 🔒 Get security report for all entities
        public static string GenerateSecurityReport()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var validation = ValidateAssemblyEntities(assembly);

            var report = new System.Text.StringBuilder();
            report.AppendLine("🔒 MULTI-TENANT ENTITY SECURITY REPORT");
            report.AppendLine("=" + new string('=', 50));

            if (validation.IsValid)
            {
                report.AppendLine("✅ ALL ENTITIES ARE SECURE");
                report.AppendLine($"   Valid Entities: {validation.ValidEntities.Count}");
                report.AppendLine($"   Exempt Entities: {validation.ExemptEntities.Count}");
            }
            else
            {
                report.AppendLine("🚨 SECURITY VIOLATIONS DETECTED");
                report.AppendLine($"   Violations: {validation.Violations.Count}");
                report.AppendLine();
                foreach (var violation in validation.Violations)
                {
                    report.AppendLine($"   {violation}");
                }
            }

            report.AppendLine();
            report.AppendLine("📋 ENTITY SUMMARY:");
            report.AppendLine($"   Valid: {string.Join(", ", validation.ValidEntities)}");
            if (validation.ExemptEntities.Any())
            {
                report.AppendLine($"   Exempt: {string.Join(", ", validation.ExemptEntities)}");
            }

            return report.ToString();
        }
    }

    // 🔒 Entity validation result
    public class EntityValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> ValidEntities { get; set; } = new List<string>();
        public List<string> ExemptEntities { get; set; } = new List<string>();
        public List<string> Violations { get; set; } = new List<string>();
    }

    // 🔒 Attribute to mark entities as multi-tenant secure
    [AttributeUsage(AttributeTargets.Class)]
    public class MultiTenantSecureAttribute : Attribute
    {
        public bool RequiresCompanyId { get; set; } = true;
        public string Description { get; set; } = "Multi-tenant secure entity";
    }

    // 🔒 Attribute to exempt entities from multi-tenant requirements
    [AttributeUsage(AttributeTargets.Class)]
    public class MultiTenantExemptAttribute : Attribute
    {
        public string Reason { get; set; } = "System-wide entity";
    }
}
