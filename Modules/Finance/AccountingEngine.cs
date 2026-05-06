using Microsoft.EntityFrameworkCore;

namespace SecureERP2.Modules.Finance
{
    // 🧮 REAL Accounting Engine - Double Entry System Implementation
    public class AccountingEngine
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<AccountingEngine> _logger;

        public AccountingEngine(ERPDbContext context, ILogger<AccountingEngine> logger)
        {
            _context = context;
            _logger = logger;
        }

        // 💰 STEP 19.2: Implement STRICT Double Entry System (Debit/Credit rules)
        public async Task<Transaction> CreateJournalEntryAsync(JournalEntryRequest request)
        {
            // 📊 Validate transaction balance
            var totalDebits = request.Entries.Where(e => e.DebitAmount > 0).Sum(e => e.DebitAmount);
            var totalCredits = request.Entries.Where(e => e.CreditAmount > 0).Sum(e => e.CreditAmount);

            if (Math.Abs(totalDebits - totalCredits) >= 0.01m)
            {
                throw new InvalidOperationException($"Transaction is not balanced. Debits: {totalDebits:C}, Credits: {totalCredits:C}");
            }

            // 📊 STRICT: Validate each entry follows accounting rules
            foreach (var entry in request.Entries)
            {
                await ValidateEntryAccountingRules(entry);
            }

            // 📊 Create transaction
            var transaction = new Transaction
            {
                TransactionNumber = GenerateTransactionNumber(),
                TransactionDate = request.TransactionDate,
                TransactionType = request.TransactionType,
                TransactionStatus = TransactionStatus.Pending,
                TotalAmount = totalDebits, // Should equal totalCredits
                CurrencyCode = request.CurrencyCode,
                Description = request.Description,
                CreatedByUserId = request.CreatedByUserId,
                CompanyId = request.CompanyId
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            // 📊 Create ledger entries
            foreach (var entry in request.Entries)
            {
                var ledgerEntry = new LedgerEntry
                {
                    EntryNumber = GenerateEntryNumber(transaction.Id),
                    EntryType = entry.EntryType,
                    DebitAmount = entry.DebitAmount,
                    CreditAmount = entry.CreditAmount,
                    Balance = entry.DebitAmount > 0 ? entry.DebitAmount : entry.CreditAmount,
                    Description = entry.Description,
                    AccountId = entry.AccountId,
                    TransactionId = transaction.Id,
                    CreatedByUserId = request.CreatedByUserId,
                    CompanyId = request.CompanyId
                };

                _context.LedgerEntries.Add(ledgerEntry);

                // 📊 Update account balance with STRICT rules
                await UpdateAccountBalanceAsync(entry.AccountId, entry.DebitAmount, entry.CreditAmount);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Created journal entry {transaction.TransactionNumber} with {request.Entries.Count} entries");
            return transaction;
        }

        // 📊 STRICT: Validate entry follows proper accounting rules
        private async Task ValidateEntryAccountingRules(JournalEntryLineRequest entry)
        {
            var account = await _context.FinanceAccounts.FindAsync(entry.AccountId);
            if (account == null)
            {
                throw new InvalidOperationException($"Account {entry.AccountId} not found");
            }

            // 📊 STRICT: Validate that entry follows normal balance rules
            bool isValidEntry = false;
            
            switch (account.AccountType)
            {
                case AccountType.Asset:
                case AccountType.Expense:
                    // Debit normal balance accounts: Debit should increase, Credit should decrease
                    isValidEntry = (entry.DebitAmount > 0 && entry.CreditAmount == 0) || 
                                  (entry.CreditAmount > 0 && entry.DebitAmount == 0);
                    break;
                    
                case AccountType.Liability:
                case AccountType.Equity:
                case AccountType.Revenue:
                    // Credit normal balance accounts: Credit should increase, Debit should decrease
                    isValidEntry = (entry.CreditAmount > 0 && entry.DebitAmount == 0) || 
                                  (entry.DebitAmount > 0 && entry.CreditAmount == 0);
                    break;
            }

            if (!isValidEntry)
            {
                throw new InvalidOperationException($"Invalid entry for account {account.AccountCode} ({account.AccountType}). Entry must be either Debit OR Credit, not both.");
            }

            // 📊 STRICT: Validate amount is positive
            if (entry.DebitAmount <= 0 && entry.CreditAmount <= 0)
            {
                throw new InvalidOperationException($"Entry amount must be positive for account {account.AccountCode}");
            }
        }

        // 💰 STEP 19.4: Implement Balanced Transaction Validation
        public async Task<bool> ValidateTransactionBalanceAsync(int transactionId)
        {
            var transaction = await _context.Transactions
                .Include(t => t.LedgerEntries)
                .FirstOrDefaultAsync(t => t.Id == transactionId);

            if (transaction == null)
            {
                return false;
            }

            return transaction.IsBalanced;
        }

        // 📊 Update account balance based on STRICT accounting rules
        private async Task UpdateAccountBalanceAsync(int accountId, decimal debitAmount, decimal creditAmount)
        {
            var account = await _context.FinanceAccounts.FindAsync(accountId);
            if (account == null)
            {
                throw new InvalidOperationException($"Account {accountId} not found");
            }

            // 📊 Apply STRICT accounting rules based on the 5 fundamental account types
            switch (account.AccountType)
            {
                case AccountType.Asset:
                    // Assets: Debit increases balance, Credit decreases balance
                    account.CurrentBalance += debitAmount - creditAmount;
                    break;
                    
                case AccountType.Liability:
                    // Liabilities: Credit increases balance, Debit decreases balance
                    account.CurrentBalance += creditAmount - debitAmount;
                    break;
                    
                case AccountType.Equity:
                    // Equity: Credit increases balance, Debit decreases balance
                    account.CurrentBalance += creditAmount - debitAmount;
                    break;
                    
                case AccountType.Revenue:
                    // Revenue: Credit increases balance, Debit decreases balance
                    account.CurrentBalance += creditAmount - debitAmount;
                    break;
                    
                case AccountType.Expense:
                    // Expenses: Debit increases balance, Credit decreases balance
                    account.CurrentBalance += debitAmount - creditAmount;
                    break;
                    
                default:
                    throw new InvalidOperationException($"Invalid account type: {account.AccountType}");
            }

            account.LastTransactionDate = DateTime.UtcNow;
        }

        // 📊 Generate transaction number
        private string GenerateTransactionNumber()
        {
            var date = DateTime.UtcNow.ToString("yyyyMMdd");
            var random = new Random().Next(1000, 9999);
            return $"JE{date}{random}";
        }

        // 📊 Generate entry number
        private string GenerateEntryNumber(int transactionId)
        {
            return $"LE{transactionId:D6}{DateTime.UtcNow:HHmmss}";
        }

        // 📊 Get trial balance with date filtering
        public async Task<TrialBalanceResult> GetTrialBalanceAsync(int companyId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var startDate = fromDate ?? DateTime.MinValue;
            var endDate = toDate ?? DateTime.MaxValue;

            var accounts = await _context.FinanceAccounts
                .Where(a => a.CompanyId == companyId && a.IsActive)
                .ToListAsync();

            var trialBalance = new TrialBalanceResult();

            foreach (var account in accounts)
            {
                // 🚀 STEP 24.2: Calculate balance for date range
                var balance = await GetAccountBalanceAsOfDate(account.Id, startDate, endDate);

                if (account.NormalBalance == AccountNormalBalance.Debit)
                {
                    trialBalance.DebitTotal += balance;
                }
                else
                {
                    trialBalance.CreditTotal += balance;
                }

                trialBalance.Accounts.Add(new TrialBalanceAccount
                {
                    AccountCode = account.AccountCode,
                    AccountName = account.AccountName,
                    AccountType = account.AccountType,
                    AccountCategory = account.AccountCategory,
                    NormalBalance = account.NormalBalance,
                    Balance = balance
                });
            }

            return trialBalance;
        }

        // 🚀 STEP 24.2: Helper method to calculate account balance as of specific date
        private async Task<decimal> GetAccountBalanceAsOfDate(int accountId, DateTime fromDate, DateTime toDate)
        {
            var account = await _context.FinanceAccounts.FindAsync(accountId);
            if (account == null) return 0;

            // Get opening balance before the date range
            var openingBalance = await _context.LedgerEntries
                .Where(le => le.AccountId == accountId && le.Transaction.TransactionDate < fromDate)
                .SumAsync(le => le.DebitAmount - le.CreditAmount);

            // Get transactions within the date range
            var periodBalance = await _context.LedgerEntries
                .Where(le => le.AccountId == accountId && 
                           le.Transaction.TransactionDate >= fromDate && 
                           le.Transaction.TransactionDate <= toDate)
                .SumAsync(le => le.DebitAmount - le.CreditAmount);

            // Calculate total balance based on account type
            var totalBalance = account.OpeningBalance + openingBalance + periodBalance;
            
            // Apply normal balance logic
            if (account.NormalBalance == AccountNormalBalance.Credit)
            {
                totalBalance = -totalBalance; // Credit accounts have negative balance in debit/credit system
            }

            return totalBalance;
        }
    }

    // 📊 Journal Entry Request DTO (User-friendly format)
    public class JournalEntryRequest
    {
        public string Description { get; set; } = string.Empty;
        public List<JournalEntryLineRequest> Entries { get; set; } = new();
        
        // 📊 Internal properties (set by controller)
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
        public TransactionType TransactionType { get; set; } = TransactionType.JournalEntry;
        public string CurrencyCode { get; set; } = "USD";
        public int CreatedByUserId { get; set; }
        public int CompanyId { get; set; }
    }

    // 📊 Journal Entry Line Request DTO (User-friendly format)
    public class JournalEntryLineRequest
    {
        public int AccountId { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        
        // 📊 Internal properties (mapped by controller)
        public EntryType EntryType => Debit > 0 ? EntryType.Debit : EntryType.Credit;
        public decimal DebitAmount => Debit;
        public decimal CreditAmount => Credit;
        public string? Description { get; set; }
    }

    // 📊 Trial Balance Result
    public class TrialBalanceResult
    {
        public List<TrialBalanceAccount> Accounts { get; set; } = new();
        public decimal DebitTotal { get; set; }
        public decimal CreditTotal { get; set; }
        public bool IsBalanced => Math.Abs(DebitTotal - CreditTotal) < 0.01m;
    }

    // 📊 Trial Balance Account
    public class TrialBalanceAccount
    {
        public int AccountId { get; set; }
        public int CompanyId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public AccountType AccountType { get; set; }
        public AccountCategory AccountCategory { get; set; }
        public AccountNormalBalance NormalBalance { get; set; }
        public decimal Balance { get; set; }
        public decimal DebitBalance { get; set; }
        public decimal CreditBalance { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public DateTime TransactionDate { get; set; }
        public bool IsActive { get; set; } = true;
        public int? ParentAccountId { get; set; }
        public int HierarchyLevel { get; set; }
    }
}
