using System.ComponentModel.DataAnnotations;

namespace SecureERP2.Modules.Finance
{
    // 🏦 Finance Account (Chart of Accounts - inherits from BaseEntity for multi-tenant security)
    public class FinanceAccount : BaseEntity
    {
        [Required]
        [StringLength(50)]
        public string AccountCode { get; set; } = string.Empty;
        
        [Required]
        [StringLength(200)]
        public string AccountName { get; set; } = string.Empty;
        
        [StringLength(500)]
        public string? Description { get; set; }
        
        [Required]
        public AccountType AccountType { get; set; }
        
        [Required]
        public AccountCategory AccountCategory { get; set; }
        
        [Required]
        public decimal OpeningBalance { get; set; }
        
        public decimal CurrentBalance { get; set; }
        
        [Required]
        public string CurrencyCode { get; set; } = "USD";
        
        public bool IsActive { get; set; } = true;
        
        public DateTime? LastTransactionDate { get; set; }
        
        // 🌳 Account Hierarchy Properties
        public int? ParentAccountId { get; set; }
        public FinanceAccount? ParentAccount { get; set; }
        public List<FinanceAccount> Children { get; set; } = new List<FinanceAccount>();
        
        // 📊 Accounting Engine Properties
        public AccountNormalBalance NormalBalance => GetNormalBalance(AccountType);
        public AccountClass AccountClass => GetAccountClass(AccountType);
        public int Level => ParentAccountId.HasValue ? 1 : 0; // Simple level calculation
        public bool IsParent => Children.Any();
        public bool IsChild => ParentAccountId.HasValue;
        
        // Navigation properties
        public Company Company { get; set; } = null!;
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
        public ICollection<LedgerEntry> LedgerEntries { get; set; } = new List<LedgerEntry>();
        
        // 📊 Helper methods for accounting rules
        private static AccountNormalBalance GetNormalBalance(AccountType accountType)
        {
            return accountType switch
            {
                AccountType.Asset => AccountNormalBalance.Debit,
                AccountType.Expense => AccountNormalBalance.Debit,
                AccountType.Liability => AccountNormalBalance.Credit,
                AccountType.Equity => AccountNormalBalance.Credit,
                AccountType.Revenue => AccountNormalBalance.Credit,
                _ => AccountNormalBalance.Debit
            };
        }
        
        private static AccountClass GetAccountClass(AccountType accountType)
        {
            return accountType switch
            {
                AccountType.Asset => AccountClass.Assets,
                AccountType.Liability => AccountClass.Liabilities,
                AccountType.Equity => AccountClass.Equity,
                AccountType.Revenue => AccountClass.Revenue,
                AccountType.Expense => AccountClass.Expenses,
                _ => AccountClass.Assets
            };
        }
    }

    // 📖 Ledger Entry (Double Entry System - inherits from BaseEntity for multi-tenant security)
    public class LedgerEntry : BaseEntity
    {
        [Required]
        [StringLength(50)]
        public string EntryNumber { get; set; } = string.Empty;
        
        [Required]
        public EntryType EntryType { get; set; }
        
        [Required]
        public decimal DebitAmount { get; set; }
        
        [Required]
        public decimal CreditAmount { get; set; }
        
        [Required]
        public decimal Balance { get; set; }
        
        [StringLength(1000)]
        public string? Description { get; set; }
        
        public bool IsReconciled { get; set; } = false;
        
        public DateTime? ReconciledAt { get; set; }
        
        // 📊 Accounting Engine Properties
        public decimal Amount => DebitAmount > 0 ? DebitAmount : CreditAmount;
        public bool IsDebit => DebitAmount > 0;
        public bool IsCredit => CreditAmount > 0;
        
        // Foreign key properties
        public int AccountId { get; set; }
        public int TransactionId { get; set; }
        public int CreatedByUserId { get; set; }
        public int? ReconciledByUserId { get; set; }
        
        // Navigation properties
        public Company Company { get; set; } = null!;
        public FinanceAccount Account { get; set; } = null!;
        public Transaction Transaction { get; set; } = null!;
        public User CreatedByUser { get; set; } = null!;
        public User? ReconciledByUser { get; set; }
    }

    // 💳 Transaction (Journal Entry - inherits from BaseEntity for multi-tenant security)
    public class Transaction : BaseEntity
    {
        [Required]
        [StringLength(50)]
        public string TransactionNumber { get; set; } = string.Empty;
        
        [Required]
        public DateTime TransactionDate { get; set; }
        
        [Required]
        public TransactionType TransactionType { get; set; }
        
        [Required]
        public TransactionStatus TransactionStatus { get; set; }
        
        // 🔥 Journal Status (REAL ACCOUNTING SYSTEM)
        [Required]
        public JournalStatus Status { get; set; } = JournalStatus.Draft;
        
        [Required]
        public decimal TotalAmount { get; set; }
        
        [Required]
        public string CurrencyCode { get; set; } = "USD";
        
        [StringLength(1000)]
        public string? Description { get; set; }
        
        [Required]
        public int CreatedByUserId { get; set; }
        
        public int? ApprovedByUserId { get; set; }
        
        public DateTime? ApprovedDate { get; set; }
        
        public string? ReferenceNumber { get; set; }
        
        public string? AttachmentUrl { get; set; }
        
        // 📊 Accounting Engine Properties
        public decimal TotalDebits => LedgerEntries.Where(le => le.DebitAmount > 0).Sum(le => le.DebitAmount);
        public decimal TotalCredits => LedgerEntries.Where(le => le.CreditAmount > 0).Sum(le => le.CreditAmount);
        public bool IsBalanced => Math.Abs(TotalDebits - TotalCredits) < 0.01m; // Allow for rounding
        public int EntryCount => LedgerEntries.Count;
        
        // Navigation properties
        public Company Company { get; set; } = null!;
        public User CreatedByUser { get; set; } = null!;
        public User? ApprovedByUser { get; set; }
        public ICollection<LedgerEntry> LedgerEntries { get; set; } = new List<LedgerEntry>();
    }

    // 📊 STRICT Account Type Enum - 5 Fundamental Types
    public enum AccountType
    {
        Asset = 1,      // Debit Normal Balance
        Liability = 2,  // Credit Normal Balance  
        Equity = 3,     // Credit Normal Balance
        Revenue = 4,    // Credit Normal Balance
        Expense = 5     // Debit Normal Balance
    }

    // 📂 Account Category Enum
    public enum AccountCategory
    {
        Cash = 1,
        Bank = 2,
        AccountsReceivable = 3,
        AccountsPayable = 4,
        Inventory = 5,
        FixedAssets = 6,
        CurrentLiabilities = 7,
        LongTermLiabilities = 8,
        Equity = 9,
        Revenue = 10,
        Expenses = 11,
        CostOfGoodsSold = 12
    }

    // 📝 Ledger Entry Type Enum
    public enum EntryType
    {
        OpeningBalance = 1,
        Debit = 2,
        Credit = 3,
        Adjustment = 4,
        Reversal = 5
    }

    // 📊 Account Normal Balance Enum
    public enum AccountNormalBalance
    {
        Debit = 1,
        Credit = 2
    }

    // 📊 Account Class Enum
    public enum AccountClass
    {
        Assets = 1,
        Liabilities = 2,
        Equity = 3,
        Revenue = 4,
        Expenses = 5
    }

    // 💸 Transaction Type Enum
    public enum TransactionType
    {
        Sale = 1,
        Purchase = 2,
        Payment = 3,
        Receipt = 4,
        JournalEntry = 5,
        Adjustment = 6,
        Transfer = 7,
        Refund = 8
    }

    // 📋 Transaction Status Enum
    public enum TransactionStatus
    {
        Pending = 1,
        Approved = 2,
        Rejected = 3
    }

    // 🔥 Journal Status Enum (REAL ACCOUNTING SYSTEM)
    public enum JournalStatus
    {
        Draft = 1,
        Posted = 2,
        Locked = 3
    }

    // 🚀 Period Closing Entity (REAL PRODUCT)
    public class PeriodClosing : BaseEntity
    {
        [Required]
        public DateTime ClosingDate { get; set; }
        
        [Required]
        public string PeriodDescription { get; set; } = string.Empty;
        
        [Required]
        public PeriodStatus Status { get; set; } = PeriodStatus.Open;
        
        public DateTime? ClosedAt { get; set; }
        public int? ClosedByUserId { get; set; }
        public string? ClosedByUser { get; set; }
        
        [StringLength(1000)]
        public string? Notes { get; set; }
        
        // Navigation properties
        public User? ClosedByUserNavigation { get; set; }
    }

    // 🚀 Period Status Enum
    public enum PeriodStatus
    {
        Open = 1,
        Closed = 2,
        Locked = 3
    }
}
