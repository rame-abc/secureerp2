using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SecureERP2.Modules.Finance;

namespace SecureERP2
{
    // Global filter endpoints - NO manual CompanyId filtering needed
    public static class GlobalFilterEndpoints
    {
        public static void RegisterGlobalFilterEndpoints(this WebApplication app)
        {
            // Finance endpoints with global filters
            var financeGroup = app.MapGroup("/api/finance").RequireAuthorization();

            financeGroup.MapGet("/accounts", async (IServiceProvider serviceProvider) =>
            {
                // 🥇 Global filter automatically applies CompanyId filtering
                using var db = serviceProvider.CreateDbContext();
                
                var accounts = await db.FinanceAccounts
                    .Where(x => x.IsActive) // No CompanyId needed - global filter handles it!
                    .OrderBy(x => x.AccountName)
                    .ToListAsync();

                return Results.Ok(accounts);
            });

            financeGroup.MapPost("/accounts", async ([FromBody] FinanceAccount account, IServiceProvider serviceProvider) =>
            {
                // 🥇 Global filter automatically applies CompanyId filtering
                using var db = serviceProvider.CreateDbContext();
                
                // CompanyId will be automatically set by middleware or can be set here
                // But global filter ensures only this company's data is accessed
                
                account.CreatedAt = DateTime.UtcNow;
                account.UpdatedAt = DateTime.UtcNow;

                db.FinanceAccounts.Add(account);
                await db.SaveChangesAsync();

                return Results.Created($"/api/finance/accounts/{account.Id}", account);
            });

            financeGroup.MapGet("/transactions", async (IServiceProvider serviceProvider) =>
            {
                // 🥇 Global filter automatically applies CompanyId filtering
                using var db = serviceProvider.CreateDbContext();
                
                var transactions = await db.Transactions
                    .Include(t => t.LedgerEntries) // No CompanyId needed - global filter handles it!
                    .OrderByDescending(x => x.TransactionDate)
                    .ToListAsync();

                return Results.Ok(transactions);
            });

            // Warehouse endpoints with global filters
            var warehouseGroup = app.MapGroup("/api/warehouse").RequireAuthorization();

            warehouseGroup.MapGet("/inventory", async (IServiceProvider serviceProvider) =>
            {
                // 🥇 Global filter automatically applies CompanyId filtering
                using var db = serviceProvider.CreateDbContext();
                
                var inventory = await db.Inventory
                    .Include(i => i.Product) // No CompanyId needed - global filter handles it!
                    .ToListAsync();

                return Results.Ok(inventory);
            });

            warehouseGroup.MapGet("/products", async (IServiceProvider serviceProvider) =>
            {
                // 🥇 Global filter automatically applies CompanyId filtering
                using var db = serviceProvider.CreateDbContext();
                
                var products = await db.Products
                    .Where(x => x.IsActive) // No CompanyId needed - global filter handles it!
                    .OrderBy(x => x.ProductName)
                    .ToListAsync();

                return Results.Ok(products);
            });

            // Sales endpoints with global filters
            var salesGroup = app.MapGroup("/api/sales").RequireAuthorization();

            salesGroup.MapGet("/orders", async (IServiceProvider serviceProvider) =>
            {
                // 🥇 Global filter automatically applies CompanyId filtering
                using var db = serviceProvider.CreateDbContext();
                
                var orders = await db.Orders
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product) // No CompanyId needed - global filter handles it!
                    .OrderByDescending(x => x.OrderDate)
                    .ToListAsync();

                return Results.Ok(orders);
            });

            // Analytics endpoints with global filters
            var analyticsGroup = app.MapGroup("/api/analytics").RequireAuthorization();

            analyticsGroup.MapGet("/dashboard", async (IServiceProvider serviceProvider) =>
            {
                // 🥇 Global filter automatically applies CompanyId filtering
                using var db = serviceProvider.CreateDbContext();
                
                var metrics = new Dictionary<string, object>();

                // Financial metrics - automatically filtered by CompanyId
                var totalRevenue = await db.Transactions
                    .Where(t => t.TransactionType == TransactionType.Sale && t.TotalAmount > 0) // No CompanyId needed!
                    .SumAsync(t => t.TotalAmount);

                var totalExpenses = await db.Transactions
                    .Where(t => t.TransactionType == TransactionType.Purchase && t.TotalAmount > 0) // No CompanyId needed!
                    .SumAsync(t => t.TotalAmount);

                var totalOrders = await db.Orders.CountAsync(); // No CompanyId needed!

                var totalProducts = await db.Products
                    .Where(p => p.IsActive) // No CompanyId needed!
                    .CountAsync();

                var totalInventoryValue = await db.Inventory
                    .Join(db.Products, i => i.ProductId, p => p.Id, (i, p) => new { i, p })
                    .SumAsync(x => x.i.Quantity * x.p.UnitPrice); // No CompanyId needed!

                metrics["TotalRevenue"] = totalRevenue;
                metrics["TotalExpenses"] = totalExpenses;
                metrics["NetProfit"] = totalRevenue - totalExpenses;
                metrics["TotalOrders"] = totalOrders;
                metrics["TotalProducts"] = totalProducts;
                metrics["TotalInventoryValue"] = totalInventoryValue;

                return Results.Ok(metrics);
            });

            // User endpoints with global filters
            var userGroup = app.MapGroup("/api/users").RequireAuthorization();

            userGroup.MapGet("/", async (IServiceProvider serviceProvider) =>
            {
                // 🥇 Global filter automatically applies CompanyId filtering
                using var db = serviceProvider.CreateDbContext();
                
                var users = await db.Users
                    .Include(u => u.Role) // No CompanyId needed - global filter handles it!
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.Role.RoleName)
                    .ThenBy(x => x.Username)
                    .Select(u => new
                    {
                        u.Id,
                        u.Username,
                        u.Email,
                        u.FirstName,
                        u.LastName,
                        u.PhoneNumber,
                        u.Department,
                        u.IsActive,
                        u.CreatedAt,
                        u.UpdatedAt,
                        Role = u.Role.RoleName
                    })
                    .ToListAsync();

                return Results.Ok(users);
            });
        }
    }
}
