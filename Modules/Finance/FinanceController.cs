using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
// using SecureERP2.Data; // Removed - namespace doesn't exist
using SecureERP2.Modules.Finance.Entities;
using SecureERP2.Modules.Finance.Services;
using System.Security.Claims;

namespace SecureERP2.Modules.Finance
{
    [ApiController]
    [Route("api/finance")]
    public class FinanceController : ControllerBase
    {
        private readonly ERPDbContext _context;
        private readonly AccountingEngine _accountingEngine;
        private readonly AccrualEngine _accrualEngine;
        private readonly SubledgerEngine _subledgerEngine;
        // private readonly PeriodClosingEngine _periodClosingEngine; // Temporarily excluded
        private readonly AuditTrailEngine _auditTrailEngine;
        private readonly FinancialIntegrityValidator _integrityValidator;

        public FinanceController(
            ERPDbContext context, 
            AccountingEngine accountingEngine,
            AccrualEngine accrualEngine,
            SubledgerEngine subledgerEngine,
            // PeriodClosingEngine periodClosingEngine, // Temporarily excluded
            AuditTrailEngine auditTrailEngine,
            FinancialIntegrityValidator integrityValidator)
        {
            _context = context;
            _accountingEngine = accountingEngine;
            _accrualEngine = accrualEngine;
            _subledgerEngine = subledgerEngine;
            // _periodClosingEngine = periodClosingEngine; // Temporarily excluded
            _auditTrailEngine = auditTrailEngine;
            _integrityValidator = integrityValidator;
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
                var fromDate = from ?? new DateTime(DateTime.UtcNow.Year, 1, 1);
                var toDate = to ?? DateTime.UtcNow;

                // Get trial balance data with date filtering
                var trialBalance = await _accountingEngine.GetTrialBalanceAsync(companyId.Value, fromDate, toDate);

                // 📊 Build Income Statement Structure
                var allAccounts = trialBalance.Accounts.ToList();

                // 📊 STEP 3.5.2: Add Depreciation to P&L calculations
                // Get depreciation expense from ledger (Account 5300 - Depreciation Expense)
                var depreciationExpense = allAccounts
                    .FirstOrDefault(a => a.AccountCode == "5300" || a.AccountName.Contains("Depreciation"))?.Balance ?? 0;

                // 📊 STEP 3.5.3: Add Gross Profit calculation
                // Revenue accounts (4000-4999)
                var revenueAccounts = allAccounts.Where(a => 
                    a.AccountType == AccountType.Revenue && 
                    a.AccountCode.StartsWith("4")).ToList();
                
                // COGS accounts (5000-5099) - Cost of Goods Sold
                var cogsAccounts = allAccounts.Where(a => 
                    a.AccountType == AccountType.Expense && 
                    (a.AccountCode.StartsWith("50") || a.AccountName.Contains("Cost") || a.AccountName.Contains("COGS"))).ToList();
                
                // Operating expenses (5100-5299, 5300-5999 excluding depreciation)
                var operatingExpenses = allAccounts.Where(a => 
                    a.AccountType == AccountType.Expense && 
                    !a.AccountCode.StartsWith("50") && // Not COGS
                    !(a.AccountCode == "5300" || a.AccountName.Contains("Depreciation"))).ToList();
                
                // Other expenses (6000-6999)
                var otherExpenses = allAccounts.Where(a => 
                    a.AccountType == AccountType.Expense && 
                    a.AccountCode.StartsWith("6")).ToList();

                // Calculate IFRS-compliant P&L figures
                var totalRevenue = revenueAccounts.Sum(a => a.Balance);
                var totalCOGS = cogsAccounts.Sum(a => a.Balance);
                var totalOperatingExpenses = operatingExpenses.Sum(a => a.Balance) + depreciationExpense;
                var totalOtherExpenses = otherExpenses.Sum(a => a.Balance);
                
                // 📊 STEP 3.5.3: Gross Profit calculation
                var grossProfit = totalRevenue - totalCOGS;
                var operatingIncome = grossProfit - totalOperatingExpenses;
                var netProfitBeforeTax = operatingIncome - totalOtherExpenses;
                
                // Tax expense (Account 6000+ for taxes)
                var taxExpense = allAccounts
                    .FirstOrDefault(a => a.AccountCode.StartsWith("6") && a.AccountName.Contains("Tax"))?.Balance ?? 0;
                
                var netProfitAfterTax = netProfitBeforeTax - taxExpense;

                // 📊 IFRS-Compliant Income Statement Structure
                var incomeStatement = new
                {
                    // 💰 Revenue Section
                    revenue = new
                    {
                        accounts = revenueAccounts.Select(a => new
                        {
                            account = a.AccountName,
                            accountCode = a.AccountCode,
                            balance = a.Balance,
                            accountType = a.AccountType.ToString()
                        }).ToList(),
                        totalRevenue = totalRevenue
                    },

                    // 📦 Cost of Goods Sold Section
                    costOfGoodsSold = new
                    {
                        accounts = cogsAccounts.Select(a => new
                        {
                            account = a.AccountName,
                            accountCode = a.AccountCode,
                            balance = a.Balance,
                            accountType = a.AccountType.ToString()
                        }).ToList(),
                        totalCOGS = totalCOGS
                    },

                    // 📊 Gross Profit Section
                    grossProfit = new
                    {
                        amount = grossProfit,
                        margin = totalRevenue > 0 ? (grossProfit / totalRevenue) * 100 : 0
                    },

                    // � Operating Expenses Section
                    operatingExpenses = new
                    {
                        accounts = operatingExpenses.Select(a => new
                        {
                            account = a.AccountName,
                            accountCode = a.AccountCode,
                            balance = a.Balance,
                            accountType = a.AccountType.ToString()
                        }).ToList(),
                        depreciationExpense = depreciationExpense,
                        totalOperatingExpenses = totalOperatingExpenses
                    },

                    // 📊 Operating Income Section
                    operatingIncome = new
                    {
                        amount = operatingIncome,
                        margin = totalRevenue > 0 ? (operatingIncome / totalRevenue) * 100 : 0
                    },

                    // 🏢 Other Expenses Section
                    otherExpenses = new
                    {
                        accounts = otherExpenses.Select(a => new
                        {
                            account = a.AccountName,
                            accountCode = a.AccountCode,
                            balance = a.Balance,
                            accountType = a.AccountType.ToString()
                        }).ToList(),
                        taxExpense = taxExpense,
                        totalOtherExpenses = totalOtherExpenses
                    },

                    // 📈 Net Profit Summary
                    netProfit = new
                    {
                        beforeTax = netProfitBeforeTax,
                        taxExpense = taxExpense,
                        afterTax = netProfitAfterTax,
                        profitMargin = totalRevenue > 0 ? (netProfitAfterTax / totalRevenue) * 100 : 0,
                        returnOnRevenue = totalRevenue > 0 ? (netProfitAfterTax / totalRevenue) * 100 : 0
                    },

                    // 📅 Report Metadata
                    generatedAt = DateTime.UtcNow,
                    companyId = companyId.Value,
                    dateRange = new { 
                        from = fromDate, 
                        to = toDate,
                        period = $"{fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd}"
                    },
                    accountingStandard = "IFRS",
                    currency = "USD",
                    isPeriodClosed = await IsPeriodClosed(companyId.Value, toDate)
                };

                return Ok(incomeStatement);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
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

        // 🔒 FINAL ERP FINANCE HARDENING LAYER ENDPOINTS

        /// <summary>
        /// 🔒 Generate accruals for period
        /// </summary>
        [Authorize(Roles = "Admin,Accountant")]
        [HttpPost("accruals/generate")]
        public async Task<IActionResult> GenerateAccruals([FromBody] GenerateAccrualsRequest request)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                if (!companyId.HasValue)
                {
                    return BadRequest(new { error = "CompanyId not found in JWT token" });
                }

                var result = await _accrualEngine.GenerateMonthEndAccrualsAsync(companyId.Value, request.PeriodEnd);
                
                // 🔒 Record audit trail
                await _auditTrailEngine.RecordFinancialTransactionAuditAsync(new FinancialTransaction
                {
                    CompanyId = companyId.Value,
                    TransactionDate = request.PeriodEnd,
                    TransactionType = "AccrualGeneration",
                    Description = $"Generated accruals for period {request.PeriodEnd:yyyy-MM}",
                    Amount = 0
                }, "Generate", GetCurrentUserId(), GetCurrentUserName(), GetClientIP(), GetUserAgent());

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🔒 Post subledger transactions to GL
        /// </summary>
        [Authorize(Roles = "Admin,Accountant")]
        [HttpPost("subledger/post-to-gl")]
        public async Task<IActionResult> PostSubledgerToGL([FromBody] PostSubledgerRequest request)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                if (!companyId.HasValue)
                {
                    return BadRequest(new { error = "CompanyId not found in JWT token" });
                }

                var result = await _subledgerEngine.PostAllSubledgersToGLAsync(companyId.Value, request.AsOfDate);
                
                // 🔒 Record audit trail
                await _auditTrailEngine.RecordFinancialTransactionAuditAsync(new FinancialTransaction
                {
                    CompanyId = companyId.Value,
                    TransactionDate = request.AsOfDate ?? DateTime.UtcNow,
                    TransactionType = "SubledgerPosting",
                    Description = $"Posted {result.TotalPostings} subledger transactions to GL",
                    Amount = 0
                }, "Post", GetCurrentUserId(), GetCurrentUserName(), GetClientIP(), GetUserAgent());

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🔒 Execute SAP-style period closing
        /// </summary>
        [Authorize(Roles = "Admin,Accountant")]
        [HttpPost("period/close")]
        // public async Task<IActionResult> ClosePeriod([FromBody] PeriodClosingRequest request) // Temporarily excluded
        /*{
            try
            {
                var companyId = GetCurrentCompanyId();
                if (!companyId.HasValue)
                {
                    return BadRequest(new { error = "CompanyId not found in JWT token" });
                }

                var result = await _periodClosingEngine.ExecutePeriodClosingAsync(companyId.Value, request.ClosingDate, request);
                
                // 🔒 Record audit trail
                await _auditTrailEngine.RecordPeriodClosingAuditAsync(new PeriodClosing
                {
                    CompanyId = companyId.Value,
                    ClosingDate = request.ClosingDate,
                    PeriodDescription = request.Description,
                    Status = result.IsSuccess ? PeriodStatus.Locked : PeriodStatus.Open,
                    ClosedByUser = request.RequestedBy,
                    ClosedAt = DateTime.UtcNow,
                    IsLocked = result.IsSuccess
                }, result.IsSuccess ? "Close" : "Attempt", GetCurrentUserId(), GetCurrentUserName(), GetClientIP(), GetUserAgent());

                return Ok(result);
            }
        /*
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
            }
        }*/

        /// <summary>
        /// 🔒 Validate financial integrity
        /// </summary>
        [Authorize(Roles = "Admin,Accountant,Viewer")]
        [HttpPost("integrity/validate")]
        public async Task<IActionResult> ValidateFinancialIntegrity([FromBody] ValidateIntegrityRequest request)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                if (!companyId.HasValue)
                {
                    return BadRequest(new { error = "CompanyId not found in JWT token" });
                }

                var result = await _integrityValidator.ValidateFinancialIntegrityAsync(companyId.Value, request.AsOfDate);
                
                // 🔒 Record audit trail
                await _auditTrailEngine.RecordFinancialTransactionAuditAsync(new FinancialTransaction
                {
                    CompanyId = companyId.Value,
                    TransactionDate = request.AsOfDate,
                    TransactionType = "IntegrityValidation",
                    Description = $"Financial integrity validation: {(result.IsValid ? "PASSED" : "FAILED")}",
                    Amount = 0
                }, "Validate", GetCurrentUserId(), GetCurrentUserName(), GetClientIP(), GetUserAgent());

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🔒 Get audit trail
        /// </summary>
        [Authorize(Roles = "Admin,Accountant,Viewer")]
        [HttpGet("audit-trail")]
        public async Task<IActionResult> GetAuditTrail([FromQuery] string? entityType = null, [FromQuery] int? entityId = null, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                if (!companyId.HasValue)
                {
                    return BadRequest(new { error = "CompanyId not found in JWT token" });
                }

                if (!string.IsNullOrEmpty(entityType) && entityId.HasValue)
                {
                    var trail = await _auditTrailEngine.GetAuditTrailAsync(companyId.Value, entityType, entityId.Value);
                    return Ok(trail);
                }
                else
                {
                    // Return audit report for date range
                    var report = await _auditTrailEngine.GenerateAuditReportAsync(companyId.Value, from ?? DateTime.MinValue, to ?? DateTime.UtcNow);
                    return Ok(report);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🔒 Validate audit trail integrity
        /// </summary>
        [Authorize(Roles = "Admin,Accountant")]
        [HttpPost("audit-trail/validate")]
        public async Task<IActionResult> ValidateAuditTrailIntegrity([FromBody] ValidateAuditTrailRequest request)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                if (!companyId.HasValue)
                {
                    return BadRequest(new { error = "CompanyId not found in JWT token" });
                }

                var result = await _auditTrailEngine.ValidateAuditTrailIntegrityAsync(companyId.Value, request.FromDate, request.ToDate);
                
                // 🔒 Record audit trail for validation
                await _auditTrailEngine.RecordFinancialTransactionAuditAsync(new FinancialTransaction
                {
                    CompanyId = companyId.Value,
                    TransactionDate = DateTime.UtcNow,
                    TransactionType = "AuditTrailValidation",
                    Description = $"Audit trail integrity validation: {(result.IsValid ? "PASSED" : "FAILED")}",
                    Amount = 0
                }, "Validate", GetCurrentUserId(), GetCurrentUserName(), GetClientIP(), GetUserAgent());

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🔒 Generate tamper-proof certificate
        /// </summary>
        [Authorize(Roles = "Admin,Accountant")]
        [HttpPost("audit-trail/certificate")]
        public async Task<IActionResult> GenerateTamperProofCertificate([FromBody] GenerateCertificateRequest request)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                if (!companyId.HasValue)
                {
                    return BadRequest(new { error = "CompanyId not found in JWT token" });
                }

                var certificate = await _auditTrailEngine.GenerateTamperProofCertificateAsync(companyId.Value, request.FromDate, request.ToDate);
                
                return Ok(certificate);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // 🔒 Helper methods for hardening layer
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        private string GetCurrentUserName()
        {
            return User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
        }

        private string GetClientIP()
        {
            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        private string GetUserAgent()
        {
            return Request.Headers["User-Agent"].ToString() ?? "Unknown";
        }
    }

    // 🔒 FINAL ERP FINANCE HARDENING LAYER DTOs

    public class GenerateAccrualsRequest
    {
        public DateTime PeriodEnd { get; set; }
    }

    public class PostSubledgerRequest
    {
        public DateTime? AsOfDate { get; set; }
    }

    public class ValidateIntegrityRequest
    {
        public DateTime AsOfDate { get; set; }
    }

    public class ValidateAuditTrailRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }

    public class GenerateCertificateRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }

    // 🚀 STEP 24.3: Period Closing Request DTO
    public class ClosePeriodRequest
    {
        public DateTime ClosingDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }
}