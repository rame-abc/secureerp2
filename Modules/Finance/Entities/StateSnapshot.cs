using System;
using System.Collections.Generic;

namespace SecureERP2.Modules.Finance.Entities
{
    /// <summary>
    /// Represents a snapshot of ledger state at a point in time
    /// </summary>
    public class StateSnapshot
    {
        public int CompanyId { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<int, decimal> AccountBalances { get; set; } = new();
        public long Version { get; set; }
    }
}
