using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SecureERP2.Modules.Finance;

namespace SecureERP2
{
    // Secure multi-tenant endpoints - NEVER trust frontend data
    public static class SecureMultiTenantEndpoints
    {
        public static void RegisterSecureMultiTenantEndpoints(this WebApplication app)
        {
            // Finance endpoints with JWT-only CompanyId extraction
            var financeGroup = app.MapGroup("/api/finance").RequireAuthorization();

            financeGroup.MapGet("/accounts", async (ERPDbContext db, HttpContext context) =>
            {
                // CRITICAL: Extract CompanyId from JWT only - NEVER trust frontend
                var companyId = context.GetCompanyIdFromJwt();

                var accounts = await db.FinanceAccounts
                    .Where(x => x.CompanyId == companyId && x.IsActive)
                    .OrderBy(x => x.AccountName)
                    .ToListAsync();

                return Results.Ok(accounts);
            });

            financeGroup.MapPost("/accounts", async ([FromBody] FinanceAccount account, ERPDbContext db, HttpContext context) =>
            {
                // CRITICAL: Extract CompanyId from JWT only - NEVER trust frontend
                var companyId = context.GetCompanyIdFromJwt();

                // CRITICAL: Override any CompanyId from frontend with JWT value
                account.CompanyId = companyId;
                account.CreatedAt = DateTime.UtcNow;
                account.UpdatedAt = DateTime.UtcNow;

                db.FinanceAccounts.Add(account);
                await db.SaveChangesAsync();

                return Results.Created($"/api/finance/accounts/{account.Id}", account);
            });

            // Warehouse endpoints with JWT-only CompanyId extraction
            var warehouseGroup = app.MapGroup("/api/warehouse").RequireAuthorization();

            warehouseGroup.MapGet("/inventory", async (ERPDbContext db, HttpContext context) =>
            {
                // CRITICAL: Extract CompanyId from JWT only - NEVER trust frontend
                var companyId = context.GetCompanyIdFromJwt();

                var inventory = await db.Inventory
                    .Include(i => i.Product)
                    .Where(x => x.CompanyId == companyId)
                    .ToListAsync();

                return Results.Ok(inventory);
            });

            warehouseGroup.MapPost("/inventory", async ([FromBody] Inventory inventory, ERPDbContext db, HttpContext context) =>
            {
                // CRITICAL: Extract CompanyId from JWT only - NEVER trust frontend
                var companyId = context.GetCompanyIdFromJwt();

                // CRITICAL: Override any CompanyId from frontend with JWT value
                inventory.CompanyId = companyId;
                inventory.LastUpdated = DateTime.UtcNow;

                db.Inventory.Add(inventory);
                await db.SaveChangesAsync();

                return Results.Created($"/api/warehouse/inventory/{inventory.Id}", inventory);
            });

            // Sales endpoints with JWT-only CompanyId extraction
            var salesGroup = app.MapGroup("/api/sales").RequireAuthorization();

            salesGroup.MapGet("/orders", async (ERPDbContext db, HttpContext context) =>
            {
                // CRITICAL: Extract CompanyId from JWT only - NEVER trust frontend
                var companyId = context.GetCompanyIdFromJwt();

                var orders = await db.Orders
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                    .Where(x => x.CompanyId == companyId)
                    .OrderByDescending(x => x.OrderDate)
                    .ToListAsync();

                return Results.Ok(orders);
            });

            salesGroup.MapPost("/orders", async ([FromBody] Order order, ERPDbContext db, HttpContext context) =>
            {
                // CRITICAL: Extract CompanyId from JWT only - NEVER trust frontend
                var companyId = context.GetCompanyIdFromJwt();

                // CRITICAL: Override any CompanyId from frontend with JWT value
                order.CompanyId = companyId;
                order.CreatedAt = DateTime.UtcNow;
                order.UpdatedAt = DateTime.UtcNow;

                db.Orders.Add(order);
                await db.SaveChangesAsync();

                return Results.Created($"/api/sales/orders/{order.Id}", order);
            });

            // Analytics endpoints with JWT-only CompanyId extraction
            var analyticsGroup = app.MapGroup("/api/analytics").RequireAuthorization();

            analyticsGroup.MapGet("/dashboard", async (ERPDbContext db, HttpContext context) =>
            {
                // CRITICAL: Extract CompanyId from JWT only - NEVER trust frontend
                var companyId = context.GetCompanyIdFromJwt();

                var metrics = new Dictionary<string, object>();

                // Financial metrics - filtered by CompanyId from JWT
                var totalRevenue = await db.Transactions
                    .Where(t => t.CompanyId == companyId && t.TransactionType == TransactionType.Sale && t.TotalAmount > 0)
                    .SumAsync(t => t.TotalAmount);

                var totalExpenses = await db.Transactions
                    .Where(t => t.CompanyId == companyId && t.TransactionType == TransactionType.Purchase && t.TotalAmount > 0)
                    .SumAsync(t => t.TotalAmount);

                var totalOrders = await db.Orders
                    .Where(o => o.CompanyId == companyId)
                    .CountAsync();

                var totalProducts = await db.Products
                    .Where(p => p.CompanyId == companyId && p.IsActive)
                    .CountAsync();

                var totalInventoryValue = await db.Inventory
                    .Join(db.Products, i => i.ProductId, p => p.Id, (i, p) => new { i, p })
                    .Where(x => x.i.CompanyId == companyId)
                    .SumAsync(x => x.i.Quantity * x.p.UnitPrice);

                metrics["TotalRevenue"] = totalRevenue;
                metrics["TotalExpenses"] = totalExpenses;
                metrics["NetProfit"] = totalRevenue - totalExpenses;
                metrics["TotalOrders"] = totalOrders;
                metrics["TotalProducts"] = totalProducts;
                metrics["TotalInventoryValue"] = totalInventoryValue;

                return Results.Ok(metrics);
            });

            // User endpoints with JWT-only CompanyId extraction
            var userGroup = app.MapGroup("/api/users").RequireAuthorization();

            userGroup.MapGet("/", async (ERPDbContext db, HttpContext context) =>
            {
                // CRITICAL: Extract CompanyId from JWT only - NEVER trust frontend
                var companyId = context.GetCompanyIdFromJwt();

                var users = await db.Users
                    .Include(u => u.Role)
                    .Where(x => x.CompanyId == companyId && x.IsActive)
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

            userGroup.MapPost("/", async ([FromBody] CreateUserRequestWithRoleId request, ERPDbContext db, HttpContext context) =>
            {
                // CRITICAL: Extract CompanyId from JWT only - NEVER trust frontend
                var companyId = context.GetCompanyIdFromJwt();

                // Check if username already exists in this company (from JWT)
                var existingUser = await db.Users
                    .FirstOrDefaultAsync(u => u.CompanyId == companyId && u.Username == request.Username);

                if (existingUser != null)
                {
                    return Results.Conflict("Username already exists in this company");
                }

                var user = new User
                {
                    // CRITICAL: Use CompanyId from JWT, never from frontend
                    CompanyId = companyId,
                    Username = request.Username,
                    PasswordHash = HashPassword(request.Password),
                    Email = request.Email,
                    RoleId = request.RoleId,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    PhoneNumber = request.PhoneNumber,
                    Department = request.Department,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    PasswordChangedAt = DateTime.UtcNow
                };

                db.Users.Add(user);
                await db.SaveChangesAsync();

                return Results.Created($"/api/users/{user.Id}", new
                {
                    user.Id,
                    user.Username,
                    user.Email,
                    user.FirstName,
                    user.LastName,
                    user.PhoneNumber,
                    user.Department,
                    user.IsActive,
                    user.CreatedAt,
                    user.UpdatedAt
                });
            });
        }

        // Helper method for password hashing
        private static string HashPassword(string password)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(password);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }

    // Request DTOs moved to centralized location
}
