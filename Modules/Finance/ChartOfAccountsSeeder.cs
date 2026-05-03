using Microsoft.EntityFrameworkCore;

namespace SecureERP2.Modules.Finance
{
    // 📖 STEP 22.3: Seed Default Chart of Accounts for every company
    public class ChartOfAccountsSeeder
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<ChartOfAccountsSeeder> _logger;

        public ChartOfAccountsSeeder(ERPDbContext context, ILogger<ChartOfAccountsSeeder> logger)
        {
            _context = context;
            _logger = logger;
        }

        // 📊 Seed default accounts for a company
        public async Task SeedDefaultAccountsAsync(int companyId)
        {
            try
            {
                // Check if company already has accounts
                var existingAccounts = await _context.FinanceAccounts
                    .Where(a => a.CompanyId == companyId)
                    .ToListAsync();

                if (existingAccounts.Any())
                {
                    _logger.LogInformation($"Company {companyId} already has {existingAccounts.Count} accounts");
                    return;
                }

                // 📊 Default Chart of Accounts
                var defaultAccounts = new List<FinanceAccount>
                {
                    // 💰 Assets
                    new FinanceAccount
                    {
                        AccountCode = "1000",
                        AccountName = "Cash",
                        Description = "Cash on hand and in bank",
                        AccountType = AccountType.Asset,
                        AccountCategory = AccountCategory.Cash,
                        OpeningBalance = 0,
                        CurrentBalance = 0,
                        CurrencyCode = "USD",
                        IsActive = true,
                        CompanyId = companyId
                    },
                    new FinanceAccount
                    {
                        AccountCode = "1200",
                        AccountName = "Accounts Receivable",
                        Description = "Money owed by customers",
                        AccountType = AccountType.Asset,
                        AccountCategory = AccountCategory.AccountsReceivable,
                        OpeningBalance = 0,
                        CurrentBalance = 0,
                        CurrencyCode = "USD",
                        IsActive = true,
                        CompanyId = companyId
                    },
                    new FinanceAccount
                    {
                        AccountCode = "1500",
                        AccountName = "Inventory",
                        Description = "Goods held for sale",
                        AccountType = AccountType.Asset,
                        AccountCategory = AccountCategory.Inventory,
                        OpeningBalance = 0,
                        CurrentBalance = 0,
                        CurrencyCode = "USD",
                        IsActive = true,
                        CompanyId = companyId
                    },

                    // 💰 Liabilities
                    new FinanceAccount
                    {
                        AccountCode = "2000",
                        AccountName = "Accounts Payable",
                        Description = "Money owed to suppliers",
                        AccountType = AccountType.Liability,
                        AccountCategory = AccountCategory.AccountsPayable,
                        OpeningBalance = 0,
                        CurrentBalance = 0,
                        CurrencyCode = "USD",
                        IsActive = true,
                        CompanyId = companyId
                    },
                    new FinanceAccount
                    {
                        AccountCode = "2100",
                        AccountName = "Accrued Expenses",
                        Description = "Expenses incurred but not yet paid",
                        AccountType = AccountType.Liability,
                        AccountCategory = AccountCategory.CurrentLiabilities,
                        OpeningBalance = 0,
                        CurrentBalance = 0,
                        CurrencyCode = "USD",
                        IsActive = true,
                        CompanyId = companyId
                    },

                    // 💰 Equity
                    new FinanceAccount
                    {
                        AccountCode = "3000",
                        AccountName = "Owner's Equity",
                        Description = "Owner's investment in business",
                        AccountType = AccountType.Equity,
                        AccountCategory = AccountCategory.Equity,
                        OpeningBalance = 0,
                        CurrentBalance = 0,
                        CurrencyCode = "USD",
                        IsActive = true,
                        CompanyId = companyId
                    },
                    new FinanceAccount
                    {
                        AccountCode = "3100",
                        AccountName = "Retained Earnings",
                        Description = "Cumulative net income retained",
                        AccountType = AccountType.Equity,
                        AccountCategory = AccountCategory.Equity,
                        OpeningBalance = 0,
                        CurrentBalance = 0,
                        CurrencyCode = "USD",
                        IsActive = true,
                        CompanyId = companyId
                    },

                    // 💰 Revenue
                    new FinanceAccount
                    {
                        AccountCode = "4000",
                        AccountName = "Sales Revenue",
                        Description = "Revenue from primary business operations",
                        AccountType = AccountType.Revenue,
                        AccountCategory = AccountCategory.Revenue,
                        OpeningBalance = 0,
                        CurrentBalance = 0,
                        CurrencyCode = "USD",
                        IsActive = true,
                        CompanyId = companyId
                    },
                    new FinanceAccount
                    {
                        AccountCode = "4100",
                        AccountName = "Service Revenue",
                        Description = "Revenue from services provided",
                        AccountType = AccountType.Revenue,
                        AccountCategory = AccountCategory.Revenue,
                        OpeningBalance = 0,
                        CurrentBalance = 0,
                        CurrencyCode = "USD",
                        IsActive = true,
                        CompanyId = companyId
                    },

                    // 💰 Expenses
                    new FinanceAccount
                    {
                        AccountCode = "5000",
                        AccountName = "Cost of Goods Sold",
                        Description = "Cost of products sold",
                        AccountType = AccountType.Expense,
                        AccountCategory = AccountCategory.CostOfGoodsSold,
                        OpeningBalance = 0,
                        CurrentBalance = 0,
                        CurrencyCode = "USD",
                        IsActive = true,
                        CompanyId = companyId
                    },
                    new FinanceAccount
                    {
                        AccountCode = "5100",
                        AccountName = "Office Expenses",
                        Description = "Office supplies and utilities",
                        AccountType = AccountType.Expense,
                        AccountCategory = AccountCategory.Expenses,
                        OpeningBalance = 0,
                        CurrentBalance = 0,
                        CurrencyCode = "USD",
                        IsActive = true,
                        CompanyId = companyId
                    },
                    new FinanceAccount
                    {
                        AccountCode = "5200",
                        AccountName = "Salaries and Wages",
                        Description = "Employee compensation",
                        AccountType = AccountType.Expense,
                        AccountCategory = AccountCategory.Expenses,
                        OpeningBalance = 0,
                        CurrentBalance = 0,
                        CurrencyCode = "USD",
                        IsActive = true,
                        CompanyId = companyId
                    },
                    new FinanceAccount
                    {
                        AccountCode = "5300",
                        AccountName = "Rent Expense",
                        Description = "Facility rental costs",
                        AccountType = AccountType.Expense,
                        AccountCategory = AccountCategory.Expenses,
                        OpeningBalance = 0,
                        CurrentBalance = 0,
                        CurrencyCode = "USD",
                        IsActive = true,
                        CompanyId = companyId
                    }
                };

                _context.FinanceAccounts.AddRange(defaultAccounts);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Seeded {defaultAccounts.Count} default accounts for company {companyId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error seeding default accounts for company {companyId}");
                throw;
            }
        }

        // 📊 Get default account codes for quick reference
        public static class DefaultAccountCodes
        {
            public const string Cash = "1000";
            public const string AccountsReceivable = "1200";
            public const string Inventory = "1500";
            public const string AccountsPayable = "2000";
            public const string SalesRevenue = "4000";
            public const string OfficeExpenses = "5100";
        }
    }
}
