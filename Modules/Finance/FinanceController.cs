using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace SecureERP2.Modules.Finance
{
    [ApiController]
    [Route("api/finance")]
    public class FinanceController : ControllerBase
    {
        private readonly ERPDbContext _context;
        private readonly AccountingEngine _accountingEngine;

        public FinanceController(ERPDbContext context, AccountingEngine accountingEngine)
        {
            _context = context;
            _accountingEngine = accountingEngine;
        }

        // 🔐 Helper method to extract CompanyId from JWT token
        private int? GetCurrentCompanyId()
        {
            var companyIdClaim = User.FindFirst("CompanyId")?.Value;
            if (int.TryParse(companyIdClaim, out var companyId))
            {
                return companyId;
            }
            return null;
        }

        [HttpGet("accounts")]
        public async Task<IActionResult> GetAccounts()
        {
            try
            {
                var accounts = await _context.FinanceAccounts.ToListAsync();
                return Ok(accounts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving finance accounts", error = ex.Message });
            }
        }

        [HttpPost("accounts")]
        public async Task<IActionResult> CreateAccount([FromBody] FinanceAccount account)
        {
            try
            {
                _context.FinanceAccounts.Add(account);
                await _context.SaveChangesAsync();

                return Ok(account);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error creating account", error = ex.Message });
            }
        }

        // 📖 STEP 22.1: Create POST /api/finance/journal endpoint
        [Authorize(Roles = "Admin,Accountant")]
        [HttpPost("journal")]
        public async Task<IActionResult> CreateJournalEntry([FromBody] JournalEntryRequest request)
        {
            try
            {
                // 🏭 PRODUCTION: Get CompanyId from JWT
                var companyId = GetCurrentCompanyId();
                if (!companyId.HasValue)
                {
                    return BadRequest(new { error = "CompanyId not found in JWT token" });
                }

                request.CompanyId = companyId.Value;
                request.CreatedByUserId = 1; // TODO: Get from JWT user

                // 💰 STEP 22.2: Implement HARD validation (totalDebit = totalCredit)
                var totalDebit = request.Entries.Sum(e => e.DebitAmount);
                var totalCredit = request.Entries.Sum(e => e.CreditAmount);

                if (Math.Abs(totalDebit - totalCredit) >= 0.01m)
                {
                    return BadRequest(new { error = "Unbalanced transaction", totalDebit, totalCredit });
                }

                // 📊 Create journal entry using Accounting Engine
                var transaction = await _accountingEngine.CreateJournalEntryAsync(request);

                return CreatedAtAction(nameof(GetTransaction), new { id = transaction.Id }, new { 
                    transactionId = transaction.Id,
                    transactionNumber = transaction.TransactionNumber,
                    description = request.Description,
                    totalDebit = totalDebit,
                    totalCredit = totalCredit,
                    entryCount = request.Entries.Count,
                    isBalanced = transaction.IsBalanced
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to create journal entry", details = ex.Message });
            }
        }

        // Helper method to get transaction
        [HttpGet("journal/{id}")]
        public async Task<IActionResult> GetTransaction(int id)
        {
            try
            {
                var transaction = await _context.Transactions
                    .Include(t => t.LedgerEntries)
                    .ThenInclude(le => le.Account)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (transaction == null)
                {
                    return NotFound();
                }

                return Ok(transaction);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve transaction", details = ex.Message });
            }
        }

        // 🔥 STEP 23.2: Add POST → POSTED endpoint
        [Authorize(Roles = "Admin,Accountant")]
        [HttpPost("journal/{id}/post")]
        public async Task<IActionResult> PostJournalEntry(int id)
        {
            try
            {
                var transaction = await _context.Transactions.FindAsync(id);
                if (transaction == null)
                {
                    return NotFound(new { error = "Journal entry not found" });
                }

                // 🔥 STEP 23.3: Prevent editing after posting
                if (transaction.Status != JournalStatus.Draft)
                {
                    return BadRequest(new { error = "Only draft journals can be posted", currentStatus = transaction.Status.ToString() });
                }

                // 🏭 PRODUCTION: Verify CompanyId matches JWT
                var companyId = GetCurrentCompanyId();
                if (!companyId.HasValue || transaction.CompanyId != companyId.Value)
                {
                    return BadRequest(new { error = "Unauthorized access to journal entry" });
                }

                // Post the journal entry
                transaction.Status = JournalStatus.Posted;
                transaction.TransactionStatus = TransactionStatus.Approved;
                transaction.ApprovedDate = DateTime.UtcNow;
                transaction.ApprovedByUserId = 1; // TODO: Get from JWT user

                await _context.SaveChangesAsync();

                return Ok(new { 
                    transactionId = transaction.Id,
                    transactionNumber = transaction.TransactionNumber,
                    status = transaction.Status.ToString(),
                    postedAt = transaction.ApprovedDate
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to post journal entry", details = ex.Message });
            }
        }

        // 🔥 STEP 23.4: Build Trial Balance API
        [Authorize(Roles = "Admin,Accountant,Viewer")]
        [HttpGet("trial-balance")]
        public async Task<IActionResult> GetTrialBalance(DateTime? from = null, DateTime? to = null)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                if (!companyId.HasValue)
                {
                    return BadRequest(new { error = "CompanyId not found in JWT token" });
                }

                // 🚀 STEP 24.2: Add Date Filtering to all reports
                var fromDate = from ?? DateTime.MinValue;
                var toDate = to ?? DateTime.MaxValue;

                // Get trial balance using Accounting Engine with date filtering
                var trialBalance = await _accountingEngine.GetTrialBalanceAsync(companyId.Value, fromDate, toDate);

                // Format for user-friendly response
                var result = trialBalance.Accounts.Select(account => new
                {
                    account = account.AccountName,
                    accountCode = account.AccountCode,
                    accountType = account.AccountType.ToString(),
                    debit = account.NormalBalance == AccountNormalBalance.Debit ? account.Balance : 0,
                    credit = account.NormalBalance == AccountNormalBalance.Credit ? account.Balance : 0,
                    balance = account.Balance
                }).ToList();

                return Ok(new
                {
                    accounts = result,
                    totalDebit = trialBalance.DebitTotal,
                    totalCredit = trialBalance.CreditTotal,
                    isBalanced = trialBalance.IsBalanced,
                    generatedAt = DateTime.UtcNow,
                    dateRange = new { from = fromDate, to = toDate }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to generate trial balance", details = ex.Message });
            }
        }

        // 🔥 STEP 23.5: Build Balance Sheet API (BIG STEP)
        [Authorize(Roles = "Admin,Accountant,Viewer")]
        [HttpGet("balance-sheet")]
        public async Task<IActionResult> GetBalanceSheet(DateTime? from = null, DateTime? to = null)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                if (!companyId.HasValue)
                {
                    return BadRequest(new { error = "CompanyId not found in JWT token" });
                }

                // 🚀 STEP 24.2: Add Date Filtering to all reports
                var fromDate = from ?? DateTime.MinValue;
                var toDate = to ?? DateTime.MaxValue;

                // Get trial balance data with date filtering
                var trialBalance = await _accountingEngine.GetTrialBalanceAsync(companyId.Value, fromDate, toDate);

                // 🔥 Build Balance Sheet Structure
                var assets = trialBalance.Accounts.Where(a => a.AccountType == AccountType.Asset).ToList();
                var liabilities = trialBalance.Accounts.Where(a => a.AccountType == AccountType.Liability).ToList();
                var equity = trialBalance.Accounts.Where(a => a.AccountType == AccountType.Equity).ToList();

                // Calculate totals
                var totalAssets = assets.Sum(a => a.Balance);
                var totalLiabilities = liabilities.Sum(a => a.Balance);
                var totalEquity = equity.Sum(a => a.Balance);

                // 📊 Balance Sheet Structure
                var balanceSheet = new
                {
                    // 💰 Assets Section
                    assets = new
                    {
                        currentAssets = assets.Where(a => a.AccountCategory == AccountCategory.Cash || 
                                                        a.AccountCategory == AccountCategory.AccountsReceivable ||
                                                        a.AccountCategory == AccountCategory.Inventory).Select(a => new
                        {
                            account = a.AccountName,
                            accountCode = a.AccountCode,
                            balance = a.Balance
                        }).ToList(),
                        fixedAssets = assets.Where(a => a.AccountCategory == AccountCategory.FixedAssets).Select(a => new
                        {
                            account = a.AccountName,
                            accountCode = a.AccountCode,
                            balance = a.Balance
                        }).ToList(),
                        totalAssets = totalAssets
                    },

                    // 💰 Liabilities Section
                    liabilities = new
                    {
                        currentLiabilities = liabilities.Where(a => a.AccountCategory == AccountCategory.AccountsPayable ||
                                                               a.AccountCategory == AccountCategory.CurrentLiabilities).Select(a => new
                        {
                            account = a.AccountName,
                            accountCode = a.AccountCode,
                            balance = a.Balance
                        }).ToList(),
                        longTermLiabilities = liabilities.Where(a => a.AccountCategory == AccountCategory.LongTermLiabilities).Select(a => new
                        {
                            account = a.AccountName,
                            accountCode = a.AccountCode,
                            balance = a.Balance
                        }).ToList(),
                        totalLiabilities = totalLiabilities
                    },

                    // 💰 Equity Section
                    equity = new
                    {
                        equityAccounts = equity.Select(a => new
                        {
                            account = a.AccountName,
                            accountCode = a.AccountCode,
                            balance = a.Balance
                        }).ToList(),
                        totalEquity = totalEquity
                    },

                    // 🔍 Balance Check (Assets = Liabilities + Equity)
                    balanceCheck = new
                    {
                        assets = totalAssets,
                        liabilitiesPlusEquity = totalLiabilities + totalEquity,
                        isBalanced = Math.Abs(totalAssets - (totalLiabilities + totalEquity)) < 0.01m,
                        difference = totalAssets - (totalLiabilities + totalEquity)
                    },

                    // 📅 Report Metadata
                    generatedAt = DateTime.UtcNow,
                    companyId = companyId.Value,
                    dateRange = new { from = fromDate, to = toDate }
                };

                return Ok(balanceSheet);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to generate balance sheet", details = ex.Message });
            }
        }

        // 🚀 STEP 24.1: Build Income Statement API
        [Authorize(Roles = "Admin,Accountant,Viewer")]
        [HttpGet("income-statement")]
        public async Task<IActionResult> GetIncomeStatement(DateTime? from = null, DateTime? to = null)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                if (!companyId.HasValue)
                {
                    return BadRequest(new { error = "CompanyId not found in JWT token" });
                }

                // 🚀 STEP 24.2: Add Date Filtering to all reports
                var fromDate = from ?? DateTime.MinValue;
                var toDate = to ?? DateTime.MaxValue;

                // Get trial balance data with date filtering
                var trialBalance = await _accountingEngine.GetTrialBalanceAsync(companyId.Value, fromDate, toDate);

                // 📊 Build Income Statement Structure
                var revenue = trialBalance.Accounts.Where(a => a.AccountType == AccountType.Revenue).ToList();
                var expenses = trialBalance.Accounts.Where(a => a.AccountType == AccountType.Expense).ToList();

                // Calculate totals
                var totalRevenue = revenue.Sum(a => a.Balance);
                var totalExpenses = expenses.Sum(a => a.Balance);
                var netProfit = totalRevenue - totalExpenses;

                // 📊 Income Statement Structure
                var incomeStatement = new
                {
                    // 💰 Revenue Section
                    revenue = new
                    {
                        revenueAccounts = revenue.Select(a => new
                        {
                            account = a.AccountName,
                            accountCode = a.AccountCode,
                            balance = a.Balance
                        }).ToList(),
                        totalRevenue = totalRevenue
                    },

                    // 💰 Expenses Section
                    expenses = new
                    {
                        expenseAccounts = expenses.Select(a => new
                        {
                            account = a.AccountName,
                            accountCode = a.AccountCode,
                            balance = a.Balance
                        }).ToList(),
                        totalExpenses = totalExpenses
                    },

                    // 📊 Profit Summary
                    profitSummary = new
                    {
                        grossProfit = totalRevenue,
                        totalExpenses = totalExpenses,
                        netProfit = netProfit,
                        profitMargin = totalRevenue > 0 ? (netProfit / totalRevenue) * 100 : 0
                    },

                    // 📅 Report Metadata
                    generatedAt = DateTime.UtcNow,
                    companyId = companyId.Value,
                    dateRange = new { from = fromDate, to = toDate }
                };

                return Ok(incomeStatement);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to generate income statement", details = ex.Message });
            }
        }

        // 🚀 STEP 24.3: Add Period Closing functionality
        [Authorize(Roles = "Admin,Accountant")]
        [HttpPost("close-period")]
        public async Task<IActionResult> ClosePeriod([FromBody] ClosePeriodRequest request)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                if (!companyId.HasValue)
                {
                    return BadRequest(new { error = "CompanyId not found in JWT token" });
                }

                // 🚀 STEP 24.5: Add Role-Based Security
                // This endpoint should be protected with [Authorize(Roles = "Accountant")]

                // Check if period is already closed
                var existingClosing = await _context.PeriodClosings
                    .Where(pc => pc.CompanyId == companyId.Value && pc.ClosingDate.Date == request.ClosingDate.Date)
                    .FirstOrDefaultAsync();

                if (existingClosing != null)
                {
                    return BadRequest(new { error = "Period is already closed", closingDate = request.ClosingDate.Date });
                }

                // 🚀 STEP 24.4: Lock Financial History - Check for transactions after closing date
                var futureTransactions = await _context.Transactions
                    .Where(t => t.CompanyId == companyId.Value && t.TransactionDate > request.ClosingDate)
                    .AnyAsync();

                if (futureTransactions)
                {
                    return BadRequest(new { error = "Cannot close period. There are transactions after the closing date." });
                }

                // Create period closing record
                var periodClosing = new PeriodClosing
                {
                    ClosingDate = request.ClosingDate,
                    PeriodDescription = request.Description,
                    Status = PeriodStatus.Closed,
                    ClosedAt = DateTime.UtcNow,
                    ClosedByUserId = 1, // TODO: Get from JWT user
                    CompanyId = companyId.Value,
                    Notes = request.Notes
                };

                _context.PeriodClosings.Add(periodClosing);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    periodClosingId = periodClosing.Id,
                    closingDate = periodClosing.ClosingDate,
                    status = periodClosing.Status.ToString(),
                    closedAt = periodClosing.ClosedAt,
                    message = "Period closed successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to close period", details = ex.Message });
            }
        }

        // 🚀 STEP 24.4: Lock Financial History - Add validation for period closing
        private async Task<bool> IsPeriodClosed(int companyId, DateTime transactionDate)
        {
            var closedPeriod = await _context.PeriodClosings
                .Where(pc => pc.CompanyId == companyId && pc.ClosingDate >= transactionDate)
                .FirstOrDefaultAsync();

            return closedPeriod != null;
        }
    }

    // 🚀 STEP 24.3: Period Closing Request DTO
    public class ClosePeriodRequest
    {
        public DateTime ClosingDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }
}