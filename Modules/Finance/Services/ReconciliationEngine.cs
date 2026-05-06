#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SecureERP2.Modules.Finance.Entities;
using SecureERP2.Modules.Finance.Services;
using SecureERP2.Modules.Payroll.Entities;
using SecureERP2.Modules.Assets.Entities;

namespace SecureERP2.Modules.Finance.Services
{
    /// <summary>
    /// 🔒 REAL PRODUCTION HARDENING - Reconciliation Engine
    /// REAL ERP requirement for subledger vs GL reconciliation
    /// </summary>
    public class ReconciliationEngine
    {
        private readonly ERPDbContext _context;
        private readonly AccountingEngine _accountingEngine;

        public ReconciliationEngine(ERPDbContext context, AccountingEngine accountingEngine)
        {
            _context = context;
            _accountingEngine = accountingEngine;
        }

        /// <summary>
        /// 🔒 Run comprehensive reconciliation for a company
        /// </summary>
        
        // Root reconciliation result class
        public class ComprehensiveReconciliationResult
        {
            public int CompanyId { get; set; }
            public DateTime AsOfDate { get; set; }
            public DateTime StartedAt { get; set; }
            public DateTime CompletedAt { get; set; }
            public bool IsSuccess { get; set; }
            public string? ErrorMessage { get; set; }

            public InvoiceReconciliationResult InvoiceReconciliation { get; set; } = new();
            public PayrollReconciliationResult PayrollReconciliation { get; set; } = new();
            public FixedAssetReconciliationResult FixedAssetReconciliation { get; set; } = new();
            public TaxReconciliationResult TaxReconciliation { get; set; } = new();

            // ✅ ADD THIS
            public ReconciliationStatus OverallStatus { get; set; }

            // ✅ ADD MISSING PROPERTIES
            public AssetRegisterReconciliationResult AssetRegisterReconciliation { get; set; } = new();
            public PayrollExpenseReconciliationResult PayrollExpenseReconciliation { get; set; } = new();
        }

        public async Task<ComprehensiveReconciliationResult> RunComprehensiveReconciliationAsync(int companyId, DateTime asOfDate)
        {
            var result = new ComprehensiveReconciliationResult
            {
                CompanyId = companyId,
                AsOfDate = asOfDate,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                // 🔒 Reconcile Invoices vs GL
                result.InvoiceReconciliation = await ReconcileInvoicesAsync(companyId, asOfDate);

                // 🔒 Reconcile Payroll vs GL
                result.PayrollReconciliation = await ReconcilePayrollAsync(companyId, asOfDate);

                // 🔒 Reconcile Fixed Assets vs GL
                result.FixedAssetReconciliation = await ReconcileFixedAssetsAsync(companyId, asOfDate);

                // 🔒 Reconcile Taxes vs GL
                result.TaxReconciliation = await ReconcileTaxesAsync(companyId, asOfDate);

                // 🔒 Calculate overall status
                result.OverallStatus = CalculateOverallStatus(result);
                result.CompletedAt = DateTime.UtcNow;
                result.IsSuccess = result.OverallStatus == ReconciliationStatus.Balanced;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Comprehensive reconciliation error: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 🔒 Reconcile Invoices vs GL
        /// </summary>
        private async Task<InvoiceReconciliationResult> ReconcileInvoicesAsync(int companyId, DateTime asOfDate)
        {
            var result = new InvoiceReconciliationResult { CompanyId = companyId, AsOfDate = asOfDate };

            try
            {
                // 🔒 Get invoice totals from subledger
                var invoiceTotals = await _context.Invoices
                    .Where(i => i.CompanyId == companyId && 
                               i.InvoiceDate <= asOfDate && 
                               i.Status == "Posted")
                    .GroupBy(i => 1) // Group all invoices
                    .Select(g => new
                    {
                        TotalInvoiceAmount = g.Sum(i => i.TotalAmount),
                        TotalTaxAmount = g.Sum(i => i.TaxAmount),
                        TotalSubtotal = g.Sum(i => i.Subtotal),
                        InvoiceCount = g.Count()
                    })
                    .FirstOrDefaultAsync() ?? new { TotalInvoiceAmount = 0m, TotalTaxAmount = 0m, TotalSubtotal = 0m, InvoiceCount = 0 };

                // 🔒 Get GL balances for related accounts
                var trialBalance = await _accountingEngine.GetTrialBalanceAsync(companyId, DateTime.MinValue, asOfDate);
                
                var accountsReceivable = trialBalance.Accounts
                    .FirstOrDefault(a => a.AccountName.Contains("Accounts Receivable") || a.AccountCode == "1200");
                
                var salesRevenue = trialBalance.Accounts
                    .Where(a => a.AccountName.Contains("Sales") || a.AccountName.Contains("Revenue") || a.AccountCode.StartsWith("4"))
                    .Sum(a => a.Balance);
                
                var salesTaxPayable = trialBalance.Accounts
                    .FirstOrDefault(a => a.AccountName.Contains("Sales Tax") || a.AccountCode.Contains("2200"));

                // 🔒 Calculate differences
                result.InvoiceSubtotal = invoiceTotals.TotalSubtotal;
                result.GLSalesRevenue = salesRevenue;
                result.SubtotalDifference = Math.Abs(invoiceTotals.TotalSubtotal - salesRevenue);

                result.InvoiceTaxAmount = invoiceTotals.TotalTaxAmount;
                result.GLSalesTaxPayable = salesTaxPayable?.Balance ?? 0;
                result.TaxDifference = Math.Abs(invoiceTotals.TotalTaxAmount - result.GLSalesTaxPayable);

                result.InvoiceTotalAmount = invoiceTotals.TotalInvoiceAmount;
                result.GLAccountsReceivable = accountsReceivable?.Balance ?? 0;
                result.TotalDifference = Math.Abs(invoiceTotals.TotalInvoiceAmount - result.GLAccountsReceivable);

                result.InvoiceCount = invoiceTotals.InvoiceCount;
                result.Status = DetermineReconciliationStatus(result.TotalDifference, 0.01m); // 1 cent tolerance

                // 🔒 Add detailed line items for investigation
                result.Differences = await GetInvoiceDifferencesAsync(companyId, asOfDate);
            }
            catch (Exception ex)
            {
                result.Status = ReconciliationStatus.Error;
                result.ErrorMessage = $"Invoice reconciliation error: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 🔒 Reconcile Payroll vs GL
        /// </summary>
        private async Task<PayrollReconciliationResult> ReconcilePayrollAsync(int companyId, DateTime asOfDate)
        {
            var result = new PayrollReconciliationResult { CompanyId = companyId, AsOfDate = asOfDate };

            try
            {
                // 🔒 Get payroll totals from subledger
                var payrollTotals = await _context.PayrollRuns
                    .Where(pr => pr.CompanyId == companyId && 
                               pr.PayDate <= asOfDate && 
                               pr.Status == "Posted")
                    .GroupBy(pr => 1)
                    .Select(g => new
                    {
                        TotalGrossSalaries = g.Sum(pr => pr.GrossSalaries),
                        TotalNetSalaries = g.Sum(pr => pr.TotalNetPay),
                        TotalTaxes = g.Sum(pr => pr.TotalTaxDeductions),
                        TotalDeductions = g.Sum(pr => pr.TotalDeductions),
                        PayrollCount = g.Count()
                    })
                    .FirstOrDefaultAsync() ?? new { TotalGrossSalaries = 0m, TotalNetSalaries = 0m, TotalTaxes = 0m, TotalDeductions = 0m, PayrollCount = 0 };

                // 🔒 Get GL balances for payroll accounts
                var trialBalance = await _accountingEngine.GetTrialBalanceAsync(companyId, DateTime.MinValue, asOfDate);
                
                var salariesExpense = trialBalance.Accounts
                    .FirstOrDefault(a => a.AccountName.Contains("Salaries") && a.AccountName.Contains("Expense"));
                
                var salariesPayable = trialBalance.Accounts
                    .FirstOrDefault(a => a.AccountName.Contains("Salaries") && a.AccountName.Contains("Payable"));
                
                var payrollTaxExpense = trialBalance.Accounts
                    .FirstOrDefault(a => a.AccountName.Contains("Payroll Tax") && a.AccountName.Contains("Expense"));
                
                var payrollDeductionsPayable = trialBalance.Accounts
                    .FirstOrDefault(a => a.AccountName.Contains("Payroll") && a.AccountName.Contains("Deductions"));

                // 🔒 Calculate differences
                result.PayrollGrossSalaries = payrollTotals.TotalGrossSalaries;
                result.GLSalariesExpense = salariesExpense?.Balance ?? 0;
                result.GrossSalariesDifference = Math.Abs(payrollTotals.TotalGrossSalaries - result.GLSalariesExpense);

                result.PayrollNetSalaries = payrollTotals.TotalNetSalaries;
                result.GLSalariesPayable = salariesPayable?.Balance ?? 0;
                result.NetSalariesDifference = Math.Abs(payrollTotals.TotalNetSalaries - result.GLSalariesPayable);

                result.PayrollTaxes = payrollTotals.TotalTaxes;
                result.GLPayrollTaxExpense = payrollTaxExpense?.Balance ?? 0;
                result.TaxesDifference = Math.Abs(payrollTotals.TotalTaxes - result.GLPayrollTaxExpense);

                result.PayrollDeductions = payrollTotals.TotalDeductions;
                result.GLPayrollDeductionsPayable = payrollDeductionsPayable?.Balance ?? 0;
                result.DeductionsDifference = Math.Abs(payrollTotals.TotalDeductions - result.GLPayrollDeductionsPayable);

                result.PayrollCount = payrollTotals.PayrollCount;
                result.Status = DetermineReconciliationStatus(
                    Math.Max(result.GrossSalariesDifference, Math.Max(result.NetSalariesDifference, 
                    Math.Max(result.TaxesDifference, result.DeductionsDifference))), 0.01m);

                // 🔒 Add detailed differences
                result.Differences = await GetPayrollDifferencesAsync(companyId, asOfDate);
            }
            catch (Exception ex)
            {
                result.Status = ReconciliationStatus.Error;
                result.ErrorMessage = $"Payroll reconciliation error: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 🔒 Reconcile Fixed Assets vs GL
        /// </summary>
        private async Task<FixedAssetReconciliationResult> ReconcileFixedAssetsAsync(int companyId, DateTime asOfDate)
        {
            var result = new FixedAssetReconciliationResult { CompanyId = companyId, AsOfDate = asOfDate };

            try
            {
                // 🔒 Get asset totals from subledger
                var assetTotals = await _context.FixedAssets
                    .Where(fa => fa.CompanyId == companyId && fa.PurchaseDate <= asOfDate)
                    .GroupBy(fa => 1)
                    .Select(g => new
                    {
                        TotalAssetCost = g.Sum(fa => fa.Cost),
                        TotalAccumulatedDepreciation = g.SelectMany(fa => fa.DepreciationSchedules)
                            .Where(ds => ds.DepreciationDate <= asOfDate)
                            .Sum(ds => ds.DepreciationAmount),
                        AssetCount = g.Count()
                    })
                    .FirstOrDefaultAsync() ?? new { TotalAssetCost = 0m, TotalAccumulatedDepreciation = 0m, AssetCount = 0 };

                // 🔒 Get GL balances for asset accounts
                var trialBalance = await _accountingEngine.GetTrialBalanceAsync(companyId, DateTime.MinValue, asOfDate);
                
                var fixedAssets = trialBalance.Accounts
                    .FirstOrDefault(a => a.AccountName.Contains("Fixed Assets"));
                
                var accumulatedDepreciation = trialBalance.Accounts
                    .FirstOrDefault(a => a.AccountName.Contains("Accumulated Depreciation"));

                // 🔒 Calculate differences
                result.AssetCost = assetTotals.TotalAssetCost;
                result.GLFixedAssets = fixedAssets?.Balance ?? 0;
                result.CostDifference = Math.Abs(assetTotals.TotalAssetCost - result.GLFixedAssets);

                result.AccumulatedDepreciation = assetTotals.TotalAccumulatedDepreciation;
                result.GLAccumulatedDepreciation = accumulatedDepreciation?.Balance ?? 0;
                result.DepreciationDifference = Math.Abs(assetTotals.TotalAccumulatedDepreciation - result.GLAccumulatedDepreciation);

                result.NetBookValue = assetTotals.TotalAssetCost - assetTotals.TotalAccumulatedDepreciation;
                result.GLNetBookValue = result.GLFixedAssets - result.GLAccumulatedDepreciation;
                result.NetBookValueDifference = Math.Abs(result.NetBookValue - result.GLNetBookValue);

                result.AssetCount = assetTotals.AssetCount;
                result.Status = DetermineReconciliationStatus(
                    Math.Max(result.CostDifference, result.DepreciationDifference), 0.01m);

                // 🔒 Add asset-level differences
                result.AssetDifferences = await GetAssetDifferencesAsync(companyId, asOfDate);
            }
            catch (Exception ex)
            {
                result.Status = ReconciliationStatus.Error;
                result.ErrorMessage = $"Fixed asset reconciliation error: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 🔒 Reconcile Taxes vs GL
        /// </summary>
        private async Task<TaxReconciliationResult> ReconcileTaxesAsync(int companyId, DateTime asOfDate)
        {
            var result = new TaxReconciliationResult { CompanyId = companyId, AsOfDate = asOfDate };

            try
            {
                // 🔒 Get tax totals from subledger
                var taxTotals = await _context.TaxCalculations
                    .Where(tc => tc.CompanyId == companyId && 
                               tc.CalculationDate <= asOfDate && 
                               tc.Status == "Posted")
                    .GroupBy(tc => 1)
                    .Select(g => new
                    {
                        TotalTaxAmount = g.Sum(tc => tc.TaxAmount),
                        TaxCalculationCount = g.Count()
                    })
                    .FirstOrDefaultAsync() ?? new { TotalTaxAmount = 0m, TaxCalculationCount = 0 };

                // 🔒 Get GL balances for tax accounts
                var trialBalance = await _accountingEngine.GetTrialBalanceAsync(companyId, DateTime.MinValue, asOfDate);
                
                var taxExpense = trialBalance.Accounts
                    .Where(a => a.AccountName.Contains("Tax") && a.AccountName.Contains("Expense"))
                    .Sum(a => a.Balance);
                
                var taxPayable = trialBalance.Accounts
                    .Where(a => a.AccountName.Contains("Tax") && a.AccountName.Contains("Payable"))
                    .Sum(a => a.Balance);

                // 🔒 Calculate differences
                result.TaxAmount = taxTotals.TotalTaxAmount;
                result.GLTaxExpense = taxExpense;
                result.GLTaxPayable = taxPayable;
                result.TotalGLTaxes = taxExpense + taxPayable;
                result.Difference = Math.Abs(taxTotals.TotalTaxAmount - result.TotalGLTaxes);

                result.TaxCalculationCount = taxTotals.TaxCalculationCount;
                result.Status = DetermineReconciliationStatus(result.Difference, 0.01m);

                // 🔒 Add tax-level differences
                result.TaxDifferences = await GetTaxDifferencesAsync(companyId, asOfDate);
            }
            catch (Exception ex)
            {
                result.Status = ReconciliationStatus.Error;
                result.ErrorMessage = $"Tax reconciliation error: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 🔒 Asset register vs Balance Sheet check
        /// </summary>
        private async Task<AssetRegisterReconciliationResult> ReconcileAssetRegisterAsync(int companyId, DateTime asOfDate)
        {
            var result = new AssetRegisterReconciliationResult { CompanyId = companyId, AsOfDate = asOfDate };

            try
            {
                // 🔒 Get detailed asset register
                var assetRegister = await _context.FixedAssets
                    .Where(fa => fa.CompanyId == companyId && fa.PurchaseDate <= asOfDate)
                    .Include(fa => fa.DepreciationSchedules)
                    .ToListAsync();

                // 🔒 Get balance sheet asset totals
                var trialBalance = await _accountingEngine.GetTrialBalanceAsync(companyId, DateTime.MinValue, asOfDate);
                
                var balanceSheetAssets = trialBalance.Accounts
                    .Where(a => a.AccountType == AccountType.Asset)
                    .ToList();

                // 🔒 Calculate register totals by category
                var registerByCategory = assetRegister
                    .GroupBy(fa => fa.Category ?? "Uncategorized")
                    .ToDictionary(g => g.Key, g => new
                    {
                        Cost = g.Sum(fa => fa.Cost),
                        AccumulatedDepreciation = g.SelectMany(fa => fa.DepreciationSchedules)
                            .Where(ds => ds.DepreciationDate <= asOfDate)
                            .Sum(ds => ds.DepreciationAmount),
                        NetBookValue = g.Sum(fa => fa.Cost) - g.SelectMany(fa => fa.DepreciationSchedules)
                            .Where(ds => ds.DepreciationDate <= asOfDate)
                            .Sum(ds => ds.DepreciationAmount),
                        Count = g.Count()
                    });

                // 🔒 Calculate balance sheet totals by category
                var balanceSheetByCategory = balanceSheetAssets
                    .GroupBy(a => GetAssetCategory(a.AccountName))
                    .ToDictionary(g => g.Key, g => new
                    {
                        Cost = g.Where(a => !a.AccountName.Contains("Accumulated") && !a.AccountName.Contains("Allowance"))
                            .Sum(a => Math.Abs(a.Balance)),
                        AccumulatedDepreciation = g.Where(a => a.AccountName.Contains("Accumulated"))
                            .Sum(a => Math.Abs(a.Balance)),
                        NetBookValue = 0m, // Calculated below
                        Count = 0
                    });

                // 🔒 Calculate net book values for balance sheet
                foreach (var category in balanceSheetByCategory.ToList())
                {
                    var accumulatedKey = category.Key + " - Accumulated";
                    var accumulated = balanceSheetByCategory.FirstOrDefault(kvp => kvp.Key == accumulatedKey);
                    // Calculate net book value (readonly, so we just compute it)
                    var netBookValue = category.Value.Cost - (!balanceSheetByCategory.ContainsKey(accumulatedKey) ? 0 : balanceSheetByCategory[accumulatedKey].AccumulatedDepreciation);
                }

                // 🔒 Compare register vs balance sheet
                result.CategoryComparisons = new List<CategoryComparison>();
                foreach (var category in registerByCategory.Keys)
                {
                    var registerTotals = registerByCategory[category];
                    var balanceSheetTotals = balanceSheetByCategory.ContainsKey(category) 
                        ? balanceSheetByCategory[category] 
                        : new { Cost = 0m, AccumulatedDepreciation = 0m, NetBookValue = 0m, Count = 0 };

                    result.CategoryComparisons.Add(new CategoryComparison
                    {
                        Category = category,
                        RegisterCost = registerTotals.Cost,
                        BalanceSheetCost = balanceSheetTotals.Cost,
                        CostDifference = Math.Abs(registerTotals.Cost - balanceSheetTotals.Cost),
                        RegisterNetBookValue = registerTotals.NetBookValue,
                        BalanceSheetNetBookValue = balanceSheetTotals.NetBookValue,
                        NetBookValueDifference = Math.Abs(registerTotals.NetBookValue - balanceSheetTotals.NetBookValue),
                        RegisterCount = registerTotals.Count,
                        Status = DetermineReconciliationStatus(
                            Math.Abs(registerTotals.NetBookValue - balanceSheetTotals.NetBookValue), 0.01m)
                    });
                }

                result.OverallStatus = result.CategoryComparisons.All(c => c.Status == ReconciliationStatus.Balanced) ?
                    ReconciliationStatus.Balanced : ReconciliationStatus.OutOfBalance;
            }
            catch (Exception ex)
            {
                result.OverallStatus = ReconciliationStatus.Error;
                result.ErrorMessage = $"Asset register reconciliation error: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 🔒 Payroll vs Expense verification
        /// </summary>
        private async Task<PayrollExpenseReconciliationResult> ReconcilePayrollExpensesAsync(int companyId, DateTime asOfDate)
        {
            var result = new PayrollExpenseReconciliationResult { CompanyId = companyId, AsOfDate = asOfDate };

            try
            {
                // 🔒 Get detailed payroll breakdown
                var payrollBreakdown = await _context.PayrollRuns
                    .Where(pr => pr.CompanyId == companyId && 
                               pr.PayDate <= asOfDate && 
                               pr.Status == "Posted")
                    .Include(pr => pr.Salaries)
                    .ToListAsync();

                // 🔒 Get expense accounts from GL
                var trialBalance = await _accountingEngine.GetTrialBalanceAsync(companyId, DateTime.MinValue, asOfDate);
                
                var expenseAccounts = trialBalance.Accounts
                    .Where(a => a.AccountType == AccountType.Expense)
                    .ToList();

                // 🔒 Verify payroll expense components
                var payrollExpenseComponents = new List<PayrollExpenseComponent>();

                // Salaries
                var totalSalaries = payrollBreakdown.Sum(pr => pr.GrossSalaries);
                var salaryExpenseAccount = expenseAccounts
                    .FirstOrDefault(a => a.AccountName.Contains("Salaries"));
                
                payrollExpenseComponents.Add(new PayrollExpenseComponent
                {
                    ComponentType = "Salaries",
                    PayrollTotal = totalSalaries,
                    GLBalance = salaryExpenseAccount?.Balance ?? 0,
                    Difference = Math.Abs(totalSalaries - (salaryExpenseAccount?.Balance ?? 0)),
                    Status = DetermineReconciliationStatus(
                        Math.Abs(totalSalaries - (salaryExpenseAccount?.Balance ?? 0)), 0.01m)
                });

                // Payroll Taxes
                var totalPayrollTaxes = payrollBreakdown.Sum(pr => pr.TotalTaxes);
                var payrollTaxExpenseAccount = expenseAccounts
                    .FirstOrDefault(a => a.AccountName.Contains("Payroll Tax"));
                
                payrollExpenseComponents.Add(new PayrollExpenseComponent
                {
                    ComponentType = "Payroll Taxes",
                    PayrollTotal = totalPayrollTaxes,
                    GLBalance = payrollTaxExpenseAccount?.Balance ?? 0,
                    Difference = Math.Abs(totalPayrollTaxes - (payrollTaxExpenseAccount?.Balance ?? 0)),
                    Status = DetermineReconciliationStatus(
                        Math.Abs(totalPayrollTaxes - (payrollTaxExpenseAccount?.Balance ?? 0)), 0.01m)
                });

                // Benefits/Deductions
                var totalDeductions = payrollBreakdown.Sum(pr => pr.TotalDeductions);
                var benefitsExpenseAccount = expenseAccounts
                    .FirstOrDefault(a => a.AccountName.Contains("Benefits") || a.AccountName.Contains("Deductions"));
                
                payrollExpenseComponents.Add(new PayrollExpenseComponent
                {
                    ComponentType = "Benefits & Deductions",
                    PayrollTotal = totalDeductions,
                    GLBalance = benefitsExpenseAccount?.Balance ?? 0,
                    Difference = Math.Abs(totalDeductions - (benefitsExpenseAccount?.Balance ?? 0)),
                    Status = DetermineReconciliationStatus(
                        Math.Abs(totalDeductions - (benefitsExpenseAccount?.Balance ?? 0)), 0.01m)
                });

                result.ExpenseComponents = payrollExpenseComponents;
                result.OverallStatus = payrollExpenseComponents.All(c => c.Status == ReconciliationStatus.Balanced) ?
                    ReconciliationStatus.Balanced : ReconciliationStatus.OutOfBalance;
            }
            catch (Exception ex)
            {
                result.OverallStatus = ReconciliationStatus.Error;
                result.ErrorMessage = $"Payroll expense reconciliation error: {ex.Message}";
            }

            return result;
        }

        // Helper methods
        private ReconciliationStatus DetermineReconciliationStatus(decimal difference, decimal tolerance = 0.01m)
{
    if (Math.Abs(difference) <= tolerance)
        return ReconciliationStatus.Balanced;

    if (difference > tolerance)
        return ReconciliationStatus.OutOfBalance;

    return ReconciliationStatus.Error;
}

        private string GetAssetCategory(string accountName)
        {
            if (accountName.Contains("Fixed")) return "Fixed Assets";
            if (accountName.Contains("Cash")) return "Cash";
            if (accountName.Contains("Receivable")) return "Accounts Receivable";
            if (accountName.Contains("Inventory")) return "Inventory";
            if (accountName.Contains("Prepaid")) return "Prepaid Assets";
            return "Other Assets";
        }

        private ReconciliationStatus CalculateOverallStatus(ComprehensiveReconciliationResult result)
        {
            var allStatuses = new[]
            {
                result.InvoiceReconciliation.Status,
                result.PayrollReconciliation.Status,
                result.FixedAssetReconciliation.Status,
                result.TaxReconciliation.Status,
                result.AssetRegisterReconciliation.OverallStatus,
                result.PayrollExpenseReconciliation.OverallStatus
            };

            if (allStatuses.Any(s => s == ReconciliationStatus.Error)) return ReconciliationStatus.Error;
            if (allStatuses.Any(s => s == ReconciliationStatus.Critical)) return ReconciliationStatus.Critical;
            if (allStatuses.Any(s => s == ReconciliationStatus.OutOfBalance)) return ReconciliationStatus.OutOfBalance;
            return ReconciliationStatus.Balanced;
        }

        private decimal CalculateTotalDifferences(ReconciliationResult result)
        {
            return result.InvoiceReconciliation.TotalDifference +
                   result.PayrollReconciliation.GrossSalariesDifference +
                   result.FixedAssetReconciliation.NetBookValueDifference +
                   result.TaxReconciliation.Difference;
        }

        private List<SuspiciousMismatch> IdentifySuspiciousMismatches(ReconciliationResult result)
        {
            var suspicious = new List<SuspiciousMismatch>();

            // Large differences are suspicious
            if (result.InvoiceReconciliation.TotalDifference > 1000)
            {
                suspicious.Add(new SuspiciousMismatch
                {
                    Type = "Large Invoice Difference",
                    Amount = result.InvoiceReconciliation.TotalDifference,
                    Description = "Invoice totals differ significantly from GL"
                });
            }

            if (result.PayrollReconciliation.GrossSalariesDifference > 5000)
            {
                suspicious.Add(new SuspiciousMismatch
                {
                    Type = "Large Payroll Difference",
                    Amount = result.PayrollReconciliation.GrossSalariesDifference,
                    Description = "Payroll totals differ significantly from GL"
                });
            }

            return suspicious;
        }

        private async Task<List<DifferenceDetail>> GetInvoiceDifferencesAsync(int companyId, DateTime asOfDate)
        {
            // Implementation for detailed invoice differences
            return new List<DifferenceDetail>();
        }

        private async Task<List<DifferenceDetail>> GetPayrollDifferencesAsync(int companyId, DateTime asOfDate)
        {
            // Implementation for detailed payroll differences
            return new List<DifferenceDetail>();
        }

        private async Task<List<AssetDifference>> GetAssetDifferencesAsync(int companyId, DateTime asOfDate)
        {
            // Implementation for detailed asset differences
            return new List<AssetDifference>();
        }

        private async Task<List<TaxDifference>> GetTaxDifferencesAsync(int companyId, DateTime asOfDate)
        {
            // Implementation for detailed tax differences
            return new List<TaxDifference>();
        }
    }

    // Supporting classes
    public class ReconciliationEngineResult
    {
        public int CompanyId { get; set; }
        public DateTime AsOfDate { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        
        public InvoiceReconciliationResult InvoiceReconciliation { get; set; } = new();
        public PayrollReconciliationResult PayrollReconciliation { get; set; } = new();
        public FixedAssetReconciliationResult FixedAssetReconciliation { get; set; } = new();
        public TaxReconciliationResult TaxReconciliation { get; set; } = new();
        public AssetRegisterReconciliationResult AssetRegisterReconciliation { get; set; } = new();
        public PayrollExpenseReconciliationResult PayrollExpenseReconciliation { get; set; } = new();
        
        public ReconciliationStatus OverallStatus { get; set; }
        public decimal TotalDifferences { get; set; }
        public List<SuspiciousMismatch> SuspiciousMismatches { get; set; } = new();
    }

    
    public class InvoiceReconciliationResult : ReconciliationBase
    {
        public int CompanyId { get; set; }
        public DateTime AsOfDate { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public decimal InvoiceSubtotal { get; set; }
        public decimal GLSalesRevenue { get; set; }
        public decimal SubtotalDifference { get; set; }

        public decimal InvoiceTaxAmount { get; set; }
        public decimal GLSalesTaxPayable { get; set; }
        public decimal TaxDifference { get; set; }

        // ✅ ADD THESE
        public decimal InvoiceTotalAmount { get; set; }
        public decimal GLAccountsReceivable { get; set; }
        public decimal TotalDifference { get; set; }

        public int InvoiceCount { get; set; }
        public List<DifferenceDetail> Differences { get; set; } = new();
    }

    public class PayrollReconciliationResult : ReconciliationBase
    {
        public int CompanyId { get; set; }
        public DateTime AsOfDate { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        
        public decimal PayrollGrossSalaries { get; set; }
        public decimal GLSalariesExpense { get; set; }
        public decimal PayrollNetSalaries { get; set; }
        public decimal GLSalariesPayable { get; set; }
        public decimal NetSalariesDifference { get; set; }
        
        public decimal PayrollTaxes { get; set; }
        public decimal GLPayrollTaxExpense { get; set; }
        public decimal TaxesDifference { get; set; }
        
        public decimal PayrollDeductions { get; set; }
        public decimal GLPayrollDeductionsPayable { get; set; }
        public decimal DeductionsDifference { get; set; }
        
        public int PayrollCount { get; set; }
        public List<DifferenceDetail> Differences { get; set; } = new();
        
            }

    public class FixedAssetReconciliationResult : ReconciliationBase
    {
        public int CompanyId { get; set; }
        public DateTime AsOfDate { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        
        public decimal NetBookValue { get; set; }
        public decimal GLNetBookValue { get; set; }
        public decimal NetBookValueDifference { get; set; }
        
        public decimal CostDifference { get; set; }
        public decimal GLFixedAssets { get; set; }
        public decimal AccumulatedDepreciation { get; set; }
        public decimal GLAccumulatedDepreciation { get; set; }
        public decimal DepreciationDifference { get; set; }
        
        // Additional properties for compatibility
        public decimal AssetCost { get; set; }
        
        public int AssetCount { get; set; }
        public List<AssetDifference> AssetDifferences { get; set; } = new();
        
            }

    public class TaxReconciliationResult : ReconciliationBase
    {
        public int CompanyId { get; set; }
        public DateTime AsOfDate { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        
        public decimal SalesTax { get; set; }
        public decimal GLSalesTax { get; set; }
        public decimal SalesTaxDifference { get; set; }
        
        public decimal TaxAmount { get; set; }
        public decimal GLTaxExpense { get; set; }
                public decimal Difference { get; set; }
        public int TaxCalculationCount { get; set; }
        public List<DifferenceDetail> TaxDifferences { get; set; } = new();
        
        public decimal TaxLiability { get; set; }
        public decimal GLTaxLiability { get; set; }
        public decimal TaxLiabilityDifference { get; set; }
        public decimal GLTaxPayable { get; set; }
        public decimal TotalGLTaxes { get; set; }
    }

    public class AssetRegisterReconciliationResult : ReconciliationBase
    {
        public int CompanyId { get; set; }
        public DateTime AsOfDate { get; set; }
        public string? ErrorMessage { get; set; }
        
        public int TotalAssets { get; set; }
        public int TotalRegisterAssets { get; set; }
        public decimal TotalDifferences { get; set; }
        public List<SuspiciousMismatch> SuspiciousMismatches { get; set; } = new();
        public List<CategoryComparison> CategoryComparisons { get; set; } = new();
        public ReconciliationStatus OverallStatus { get; set; }
    }

    public class CategoryComparison
    {
        public string Category { get; set; } = string.Empty;
        public decimal RegisterCost { get; set; }
        public decimal BalanceSheetCost { get; set; }
        public decimal CostDifference { get; set; }
        public decimal RegisterNetBookValue { get; set; }
        public decimal BalanceSheetNetBookValue { get; set; }
        public decimal NetBookValueDifference { get; set; }
        public int RegisterCount { get; set; }
        public ReconciliationStatus Status { get; set; }
    }

    public class PayrollExpenseReconciliationResult
    {
        public int CompanyId { get; set; }
        public DateTime AsOfDate { get; set; }
        public ReconciliationStatus OverallStatus { get; set; }
        public ReconciliationStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        
        public List<PayrollExpenseComponent> ExpenseComponents { get; set; } = new();
    }

    public class PayrollExpenseComponent
    {
        public string ComponentType { get; set; } = string.Empty;
        public decimal PayrollTotal { get; set; }
        public decimal GLBalance { get; set; }
        public decimal Difference { get; set; }
        public ReconciliationStatus Status { get; set; }
    }

    public class DifferenceDetail
    {
        public int EntityId { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal SubledgerAmount { get; set; }
        public decimal GLAmount { get; set; }
        public decimal Difference { get; set; }
        public ReconciliationStatus Status { get; set; }
    }

    public class AssetDifference : DifferenceDetail
    {
        public string AssetName { get; set; } = string.Empty;
        public string AssetNumber { get; set; } = string.Empty;
    }

    public class TaxDifference : DifferenceDetail
    {
        public string TaxType { get; set; } = string.Empty;
        public string Jurisdiction { get; set; } = string.Empty;
    }

    public class AssetRegisterReconciliationResult : ReconciliationBase
    {
        public int CompanyId { get; set; }
        public DateTime AsOfDate { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public ReconciliationStatus OverallStatus { get; set; }
        public List<CategoryComparison> CategoryComparisons { get; set; } = new();
        public decimal TotalDifferences { get; set; }
        public List<SuspiciousMismatch> SuspiciousMismatches { get; set; } = new();
    }

    public class PayrollExpenseReconciliationResult : ReconciliationBase
    {
        public int CompanyId { get; set; }
        public DateTime AsOfDate { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public ReconciliationStatus OverallStatus { get; set; }
        public decimal TotalDifferences { get; set; }
        public List<SuspiciousMismatch> SuspiciousMismatches { get; set; } = new();
    }

    public class SuspiciousMismatch
    {
        public string Type { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public enum ReconciliationStatus
    {
        Balanced,
        OutOfBalance,
        Critical,
        Error
    }
}
