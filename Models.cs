using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using SecureERP2.Modules.Finance;

namespace SecureERP2
{
    // Base entity for multi-tenant architecture
    public abstract class BaseEntity
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // Company model (inherits from BaseEntity for multi-tenant consistency)
    public class Company : BaseEntity
    {
        public string CompanyCode { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        
        // Navigation properties
        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<FinanceAccount> FinanceAccounts { get; set; } = new List<FinanceAccount>();
        public ICollection<Product> Products { get; set; } = new List<Product>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
        public ICollection<Inventory> Inventory { get; set; } = new List<Inventory>();
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    }

    // Role model
    public class Role
    {
        public int Id { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Navigation properties
        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
        public ICollection<User> Users { get; set; } = new List<User>();
    }

    // RolePermission model
    public class RolePermission
    {
        public int Id { get; set; }
        public int RoleId { get; set; }
        public string PermissionName { get; set; } = string.Empty;
        public string PermissionValue { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // Navigation properties
        public Role Role { get; set; } = null!;
    }

    // User model (inherits from BaseEntity for multi-tenant consistency)
    public class User : BaseEntity
    {
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? Email { get; set; }
        public int RoleId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Department { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? LastLoginAt { get; set; }
        public DateTime PasswordChangedAt { get; set; }
        
        // Navigation properties
        public Company Company { get; set; } = null!;
        public Role Role { get; set; } = null!;
        public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
        public ICollection<UserSession> UserSessions { get; set; } = new List<UserSession>();
        public ICollection<Order> CreatedOrders { get; set; } = new List<Order>();
        public ICollection<Transaction> CreatedTransactions { get; set; } = new List<Transaction>();
    }

    // UserSession model (inherits from BaseEntity for multi-tenant consistency)
    public class UserSession : BaseEntity
    {
        public int UserId { get; set; }
        public string SessionToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string? IPAddress { get; set; }
        public string? UserAgent { get; set; }
        public bool IsActive { get; set; } = true;
        
        // Navigation properties
        public Company Company { get; set; } = null!;
        public User User { get; set; } = null!;
    }

    // UserPermission model (inherits from BaseEntity for multi-tenant consistency)
    public class UserPermission : BaseEntity
    {
        public int UserId { get; set; }
        public string PermissionName { get; set; } = string.Empty;
        public string PermissionValue { get; set; } = string.Empty;
        public string? Description { get; set; }
        
        // Navigation properties
        public Company Company { get; set; } = null!;
        public User User { get; set; } = null!;
    }

    
    // Product model (inherits from BaseEntity for multi-tenant consistency)
    public class Product : BaseEntity
    {
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Category { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal CostPrice { get; set; }
        public int ReorderLevel { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Navigation properties
        public Company Company { get; set; } = null!;
    }

    // Inventory model (inherits from BaseEntity for multi-tenant consistency)
    public class Inventory : BaseEntity
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public string? Location { get; set; }
        public DateTime LastUpdated { get; set; }
        
        // Navigation properties
        public Company Company { get; set; } = null!;
        public Product Product { get; set; } = null!;
    }

    // Order model (inherits from BaseEntity for multi-tenant consistency)
    public class Order : BaseEntity
    {
        public string OrderNumber { get; set; } = string.Empty;
        public string OrderType { get; set; } = string.Empty;
        public int? CustomerId { get; set; }
        public int? SupplierId { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public string Status { get; set; } = "Pending";
        public decimal TotalAmount { get; set; }
        public string? Notes { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Navigation properties
        public Company Company { get; set; } = null!;
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }

    // Order Item model (inherits from BaseEntity for multi-tenant consistency)
    public class OrderItem : BaseEntity
    {
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        
        // Navigation properties
        public Company Company { get; set; } = null!;
        public Order Order { get; set; } = null!;
        public Product Product { get; set; } = null!;
    }

    
    // Analytics Summary model (inherits from BaseEntity for multi-tenant consistency)
    public class AnalyticsSummary : BaseEntity
    {
        public string MetricName { get; set; } = string.Empty;
        public decimal MetricValue { get; set; }
        public DateOnly MetricDate { get; set; }
        public string? Category { get; set; }
        
        // Navigation properties
        public Company Company { get; set; } = null!;
    }

    
    // Database Context (enhanced with global filters and security)
    public class ERPDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        private int? _currentCompanyId;

        public ERPDbContext(Microsoft.EntityFrameworkCore.DbContextOptions<ERPDbContext> options) : base(options) { }

        // Method to set CompanyId for the current request (used by middleware)
        public void SetCurrentCompanyId(int? companyId)
        {
            _currentCompanyId = companyId;
        }

        // Get current CompanyId for logging/debugging
        public int? GetCurrentCompanyId() => _currentCompanyId;

        // 🔒 SECURITY: Secure ExecuteSqlRaw method to prevent multi-tenant bypass
        public int ExecuteSecureSqlRaw(string sql, params object[] parameters)
        {
            var securityResult = SqlSecurityAnalyzer.AnalyzeSql(sql, _currentCompanyId);
            if (!securityResult.IsSecure)
            {
                throw new SecurityException($"SQL Security Violation: {string.Join(", ", securityResult.Violations)}");
            }

            var secureSql = SqlSecurityAnalyzer.SecureSql(sql, _currentCompanyId);
            return base.Database.ExecuteSqlRaw(secureSql, parameters);
        }

        // 🔒 SECURITY: Secure ExecuteSqlInterpolated method to prevent multi-tenant bypass
        public int ExecuteSecureSqlInterpolated(FormattableString sql)
        {
            var sqlString = sql.Format;
            var securityResult = SqlSecurityAnalyzer.AnalyzeSql(sqlString, _currentCompanyId);
            if (!securityResult.IsSecure)
            {
                throw new SecurityException($"SQL Security Violation: {string.Join(", ", securityResult.Violations)}");
            }

            var secureSql = SqlSecurityAnalyzer.SecureSql(sqlString, _currentCompanyId);
            return base.Database.ExecuteSqlInterpolated($"{secureSql}");
        }

        // 🔒 SECURITY: Validate DbSet access for multi-tenant compliance
        public override DbSet<TEntity> Set<TEntity>()
        {
            EntitySecurityValidator.ValidateDbContextOperation<TEntity>();
            return base.Set<TEntity>();
        }

        // Company management
        public DbSet<Company> Companies { get; set; }

        // User and role management tables
        public DbSet<Role> Roles { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }

        // Finance management (enhanced with new entities)
        public DbSet<FinanceAccount> FinanceAccounts { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<LedgerEntry> LedgerEntries { get; set; }
        public DbSet<PeriodClosing> PeriodClosings { get; set; }

        // Product and inventory management
        public DbSet<Product> Products { get; set; }
        public DbSet<Inventory> Inventory { get; set; }

        // Order management
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        // Analytics and audit
        public DbSet<AnalyticsSummary> AnalyticsSummaries { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 🔒 DATABASE HARD PROTECTION - Foreign keys, composite indexes, unique constraints
            // modelBuilder.ConfigureHardProtection();

            // 🔒 Configure Finance Module Entities
            ConfigureFinanceEntities(modelBuilder);

            // 🥇 GLOBAL FILTERS - Automatic CompanyId enforcement
            ApplyGlobalFilters(modelBuilder);

            // Role configuration
            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.RoleName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.HasIndex(e => e.RoleName).IsUnique();
            });

            // RolePermission configuration
            modelBuilder.Entity<RolePermission>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PermissionName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PermissionValue).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.HasIndex(e => new { e.RoleId, e.PermissionName }).IsUnique();
                entity.HasOne(e => e.Role)
                      .WithMany(e => e.RolePermissions)
                      .HasForeignKey(e => e.RoleId)
                      .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Cascade);
            });

            // UserSession configuration
            modelBuilder.Entity<UserSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CompanyId).IsRequired();
                entity.Property(e => e.SessionToken).IsRequired().HasMaxLength(500);
                entity.Property(e => e.IPAddress).HasMaxLength(45);
                entity.Property(e => e.UserAgent).HasMaxLength(500);
                entity.HasIndex(e => e.SessionToken).IsUnique();
                entity.HasIndex(e => e.CompanyId);
                entity.HasIndex(e => e.ExpiresAt);
                entity.HasOne(e => e.Company)
                      .WithMany()
                      .HasForeignKey(e => e.CompanyId)
                      .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Cascade);
                entity.HasOne(e => e.User)
                      .WithMany(u => u.UserSessions)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Email).HasMaxLength(100);
                entity.Property(e => e.FirstName).HasMaxLength(50);
                entity.Property(e => e.LastName).HasMaxLength(50);
                entity.Property(e => e.PhoneNumber).HasMaxLength(20);
                entity.Property(e => e.Department).HasMaxLength(50);
                
                // 🔒 UNIQUE constraints per company
                entity.HasIndex(e => new { e.CompanyId, e.Username }).IsUnique();
                entity.HasIndex(e => new { e.CompanyId, e.Email }).IsUnique();
                
                // 🔒 COMPOSITE indexes for performance
                entity.HasIndex(e => new { e.CompanyId, e.Id });
                entity.HasIndex(e => new { e.CompanyId, e.RoleId });
                entity.HasIndex(e => new { e.CompanyId, e.IsActive });
                
                // 🔒 FOREIGN KEY constraints
                entity.HasOne(e => e.Company)
                      .WithMany()
                      .HasForeignKey(e => e.CompanyId)
                      .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
                entity.HasOne(e => e.Role)
                      .WithMany()
                      .HasForeignKey(e => e.RoleId)
                      .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
            });

            // UserPermission configuration (with CompanyId)
            modelBuilder.Entity<UserPermission>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CompanyId).IsRequired();
                entity.Property(e => e.PermissionName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PermissionValue).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.HasIndex(e => new { e.CompanyId, e.UserId, e.PermissionName }).IsUnique();
                entity.HasOne(e => e.Company)
                      .WithMany()
                      .HasForeignKey(e => e.CompanyId)
                      .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Cascade);
                entity.HasOne(e => e.User)
                      .WithMany(u => u.UserPermissions)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Cascade);
            });

            // AuditLog configuration (with CompanyId)
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CompanyId).IsRequired();
                entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
                entity.Property(e => e.EntityName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.OldValues).HasColumnType("TEXT");
                entity.Property(e => e.NewValues).HasColumnType("TEXT");
                entity.Property(e => e.AffectedColumns).HasMaxLength(500);
                entity.Property(e => e.IPAddress).HasMaxLength(45);
                entity.Property(e => e.UserAgent).HasMaxLength(500);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.Module).HasMaxLength(100);
                entity.HasIndex(e => e.CompanyId);
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Username);
                entity.HasIndex(e => e.Action);
                entity.HasIndex(e => e.EntityName);
                entity.HasIndex(e => e.Module);
                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<FinanceAccount>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.AccountCode).IsRequired().HasMaxLength(50);
                entity.Property(e => e.AccountName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.AccountType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Description).HasMaxLength(500);
                
                // 🔒 UNIQUE constraints per company
                entity.HasIndex(e => new { e.CompanyId, e.AccountCode }).IsUnique();
                entity.HasIndex(e => new { e.CompanyId, e.AccountName }).IsUnique();
                
                // 🔒 COMPOSITE indexes for performance
                entity.HasIndex(e => new { e.CompanyId, e.Id });
                entity.HasIndex(e => new { e.CompanyId, e.AccountType });
                entity.HasIndex(e => new { e.CompanyId, e.IsActive });
                
                // 🔒 FOREIGN KEY constraints
                entity.HasOne(e => e.Company)
                      .WithMany()
                      .HasForeignKey(e => e.CompanyId)
                      .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CompanyId).IsRequired();
                entity.Property(e => e.ProductCode).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ProductName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Category).HasMaxLength(100);
                
                // UNIQUE constraints per company
                entity.HasIndex(e => new { e.CompanyId, e.ProductCode }).IsUnique();
                entity.HasIndex(e => new { e.CompanyId, e.ProductName }).IsUnique();
                
                // COMPOSITE indexes for performance
                entity.HasIndex(e => new { e.CompanyId, e.Id });
                entity.HasIndex(e => new { e.CompanyId, e.Category });
                entity.HasIndex(e => new { e.CompanyId, e.IsActive });
                
                // FOREIGN KEY constraints
                entity.HasOne(e => e.Company)
                      .WithMany()
                      .HasForeignKey(e => e.CompanyId)
                      .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
            });

            // Inventory configuration with hard protection
            modelBuilder.Entity<Inventory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CompanyId).IsRequired();
                entity.Property(e => e.Location).HasMaxLength(100);
                
                // UNIQUE constraints per company
                entity.HasIndex(e => new { e.CompanyId, e.ProductId }).IsUnique();
                entity.HasIndex(e => new { e.CompanyId, e.Location, e.ProductId }).IsUnique();
                
                // COMPOSITE indexes for performance
                entity.HasIndex(e => new { e.CompanyId, e.Id });
                entity.HasIndex(e => new { e.CompanyId, e.Location });
                entity.HasIndex(e => new { e.CompanyId, e.Quantity });
                
                // FOREIGN KEY constraints
                entity.HasOne(e => e.Company)
                      .WithMany()
                      .HasForeignKey(e => e.CompanyId)
                      .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
                entity.HasOne(e => e.Product)
                      .WithMany()
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
            });
        }

        // 🥇 GLOBAL FILTERS METHOD - Automatic CompanyId enforcement
        private void ApplyGlobalFilters(ModelBuilder modelBuilder)
        {
            // Apply global filters to all multi-tenant entities
            // This ensures ALL queries are automatically filtered by CompanyId
            var companyId = GetCurrentCompanyId();
            
            if (companyId.HasValue)
            {
                modelBuilder.Entity<User>().HasQueryFilter(e => e.CompanyId == companyId);
                modelBuilder.Entity<UserPermission>().HasQueryFilter(e => e.CompanyId == companyId);
                modelBuilder.Entity<UserSession>().HasQueryFilter(e => e.CompanyId == companyId);
                modelBuilder.Entity<FinanceAccount>().HasQueryFilter(e => e.CompanyId == companyId);
                modelBuilder.Entity<Transaction>().HasQueryFilter(e => e.CompanyId == companyId);
                modelBuilder.Entity<LedgerEntry>().HasQueryFilter(e => e.CompanyId == companyId);
                modelBuilder.Entity<Product>().HasQueryFilter(e => e.CompanyId == companyId);
                modelBuilder.Entity<Inventory>().HasQueryFilter(e => e.CompanyId == companyId);
                modelBuilder.Entity<Order>().HasQueryFilter(e => e.CompanyId == companyId);
                modelBuilder.Entity<OrderItem>().HasQueryFilter(e => e.CompanyId == companyId);
                modelBuilder.Entity<AnalyticsSummary>().HasQueryFilter(e => e.CompanyId == companyId);
                modelBuilder.Entity<AuditLog>().HasQueryFilter(e => e.CompanyId == companyId);
            }
            
            // Note: Company entity is not filtered by CompanyId (it's the root entity)
            // Note: Role and RolePermission are not filtered by CompanyId (they're global)
        }

        // 🔒 Configure Finance Module Entities
        private void ConfigureFinanceEntities(ModelBuilder modelBuilder)
        {
            // Configure FinanceAccount
            modelBuilder.Entity<FinanceAccount>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.AccountCode).IsRequired().HasMaxLength(50);
                entity.Property(e => e.AccountName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.CurrencyCode).IsRequired().HasMaxLength(3).HasDefaultValue("USD");
                entity.Property(e => e.CurrentBalance).HasDefaultValue(0);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                
                // Unique constraint on AccountCode per company
                entity.HasIndex(e => new { e.AccountCode, e.CompanyId }).IsUnique();
                
                // Foreign key relationships
                entity.HasOne(e => e.Company)
                      .WithMany(c => c.FinanceAccounts)
                      .HasForeignKey(e => e.CompanyId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure Transaction
            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TransactionNumber).IsRequired().HasMaxLength(50);
                entity.Property(e => e.TransactionType).IsRequired();
                entity.Property(e => e.TransactionStatus).IsRequired();
                entity.Property(e => e.TotalAmount).IsRequired();
                entity.Property(e => e.CurrencyCode).IsRequired().HasMaxLength(3).HasDefaultValue("USD");
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.ReferenceNumber).HasMaxLength(50);
                
                // Unique constraint on TransactionNumber per company
                entity.HasIndex(e => new { e.TransactionNumber, e.CompanyId }).IsUnique();
                
                // Foreign key relationships
                entity.HasOne(e => e.Company)
                      .WithMany(c => c.Transactions)
                      .HasForeignKey(e => e.CompanyId)
                      .OnDelete(DeleteBehavior.Cascade);
                      
                entity.HasOne(e => e.CreatedByUser)
                      .WithMany()
                      .HasForeignKey(e => e.CreatedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);
                      
                entity.HasOne(e => e.ApprovedByUser)
                      .WithMany()
                      .HasForeignKey(e => e.ApprovedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure LedgerEntry
            modelBuilder.Entity<LedgerEntry>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EntryNumber).IsRequired().HasMaxLength(50);
                entity.Property(e => e.EntryType).IsRequired();
                entity.Property(e => e.DebitAmount).HasDefaultValue(0);
                entity.Property(e => e.CreditAmount).HasDefaultValue(0);
                entity.Property(e => e.Balance).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.IsReconciled).HasDefaultValue(false);
                
                // Foreign key relationships
                entity.HasOne(e => e.Company)
                      .WithMany()
                      .HasForeignKey(e => e.CompanyId)
                      .OnDelete(DeleteBehavior.Cascade);
                      
                entity.HasOne(e => e.Account)
                      .WithMany(a => a.LedgerEntries)
                      .HasForeignKey(e => e.AccountId)
                      .OnDelete(DeleteBehavior.Cascade);
                      
                entity.HasOne(e => e.Transaction)
                      .WithMany(t => t.LedgerEntries)
                      .HasForeignKey(e => e.TransactionId)
                      .OnDelete(DeleteBehavior.Cascade);
                      
                entity.HasOne(e => e.CreatedByUser)
                      .WithMany()
                      .HasForeignKey(e => e.CreatedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);
                      
                entity.HasOne(e => e.ReconciledByUser)
                      .WithMany()
                      .HasForeignKey(e => e.ReconciledByUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
