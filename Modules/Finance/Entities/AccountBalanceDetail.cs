using System;

namespace SecureERP2.Modules.Finance.Entities
{
    /// <summary>
    /// Represents detailed account balance information
    /// </summary>
    public class AccountBalanceDetail
    {
        public int AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public DateTime LastTransactionDate { get; set; }
        public int TransactionCount { get; set; }
        public bool IsActive { get; set; }
    }
}
