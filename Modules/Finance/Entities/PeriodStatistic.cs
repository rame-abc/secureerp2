using System;

namespace SecureERP2.Modules.Finance.Entities
{
    /// <summary>
    /// Represents statistics for a period
    /// </summary>
    public class PeriodStatistic
    {
        public string PeriodName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TransactionCount { get; set; }
        public decimal TotalAmount { get; set; }
        public int AccountCount { get; set; }
        public bool IsClosed { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
    }
}
