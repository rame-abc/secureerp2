using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SecureERP2
{
    // Database context factory with central tenant resolver
    public class DbContextFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ICurrentCompany _currentCompany;

        public DbContextFactory(IServiceProvider serviceProvider, ICurrentCompany currentCompany)
        {
            _serviceProvider = serviceProvider;
            _currentCompany = currentCompany;
        }

        // Create context with current CompanyId from central resolver
        public ERPDbContext CreateDbContext()
        {
            var options = _serviceProvider.GetRequiredService<DbContextOptions<ERPDbContext>>();
            var context = new ERPDbContext(options);
            
            // 🎯 Use central tenant resolver (ICurrentCompany) - NEVER trust frontend
            if (_currentCompany.IsValid)
            {
                context.SetCurrentCompanyId(_currentCompany.CompanyId);
            }
            
            return context;
        }

        // Create context with specific CompanyId (for admin operations)
        public ERPDbContext CreateDbContext(int companyId)
        {
            var options = _serviceProvider.GetRequiredService<DbContextOptions<ERPDbContext>>();
            var context = new ERPDbContext(options);
            context.SetCurrentCompanyId(companyId);
            return context;
        }

        // Create context without CompanyId (for system-wide operations)
        public ERPDbContext CreateSystemDbContext()
        {
            var options = _serviceProvider.GetRequiredService<DbContextOptions<ERPDbContext>>();
            return new ERPDbContext(options);
        }
    }

    // Extension methods for dependency injection
    public static class DbContextFactoryExtensions
    {
        public static IServiceCollection AddDbContextFactory(this IServiceCollection services)
        {
            services.AddSingleton<DbContextFactory>();
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentCompany, CurrentCompany>();
            return services;
        }

        public static ERPDbContext CreateDbContext(this IServiceProvider serviceProvider)
        {
            var factory = serviceProvider.GetRequiredService<DbContextFactory>();
            return factory.CreateDbContext();
        }

        public static ERPDbContext CreateDbContext(this IServiceProvider serviceProvider, int companyId)
        {
            var factory = serviceProvider.GetRequiredService<DbContextFactory>();
            return factory.CreateDbContext(companyId);
        }

        public static ERPDbContext CreateSystemDbContext(this IServiceProvider serviceProvider)
        {
            var factory = serviceProvider.GetRequiredService<DbContextFactory>();
            return factory.CreateSystemDbContext();
        }
    }
}
