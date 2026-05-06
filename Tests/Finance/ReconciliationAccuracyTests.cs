using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SecureERP2.Modules.Finance.Services;
using SecureERP2.Modules.Finance.Entities;

namespace SecureERP2.Tests.Finance
{
    /// <summary>
    /// 🔒 Reconciliation Accuracy Validation - Verify data consistency across modules
    /// </summary>
    [TestClass]
    public class ReconciliationAccuracyTests
    {
        private readonly ERPDbContext _context;
        private readonly ReconciliationEngine _reconciliationEngine;
        private readonly ILogger<ReconciliationAccuracyTests> _logger;

        public ReconciliationAccuracyTests(
            ERPDbContext context,
            ReconciliationEngine reconciliationEngine,
            ILogger<ReconciliationAccuracyTests> logger)
        {
            _context = context;
            _reconciliationEngine = reconciliationEngine;
            _logger = logger;
        }

        [TestInitialize]
        public void Setup()
        {
            // Clean database for each test
            _context.Database.EnsureDeleted();
            _context.Database.EnsureCreated();
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clean up after each test
            _context.Database.EnsureDeleted();
        }

        #region Test 1: Invoice vs GL Consistency

        /// <summary>
        /// Test 1: Invoice reconciliation should match GL exactly
        /// </summary>
        [TestMethod]
        public async Task InvoiceVsGL_ShouldMatchExactly_WhenConsistentData()
        {
            // Arrange - Create consistent invoice and GL data
            var companyId = 1;
            var asOfDate = DateTime.UtcNow;
            var invoiceAmount = 5000m;
            var glAccountId = 4001;

            // Create invoice
            var invoice = new Invoice
            {
                CompanyId = companyId,
                CustomerId = "CUST-001",
                InvoiceNumber = "INV-ACC-001",
                InvoiceAmount = invoiceAmount,
                Status = "Posted",
                CreatedAt = asOfDate.AddDays(-1)
            };

            _context.Invoices.Add(invoice);

            // Create corresponding GL entry
            var glEntry = new JournalEntry
            {
                CompanyId = companyId,
                TransactionDate = asOfDate.AddDays(-1),
                Description = $"Invoice {invoice.InvoiceNumber}",
                Status = JournalStatus.Posted,
                JournalLines = new[]
                {
                    new JournalLine { AccountId = glAccountId, DebitAmount = 0, CreditAmount = invoiceAmount, Description = $"Invoice {invoice.InvoiceNumber}" }
                }
            };

            _context.JournalEntries.Add(glEntry);
            await _context.SaveChangesAsync();

            // Act - Run reconciliation
            var reconciliationResult = await _reconciliationEngine.RunComprehensiveReconciliationAsync(companyId, asOfDate);

            // Assert
            Assert.IsTrue(reconciliationResult.IsSuccess, "Reconciliation should succeed");
            Assert.IsNotNull(reconciliationResult.InvoiceReconciliation, "Invoice reconciliation should not be null");
            
            // Verify exact match
            var invoiceRecon = reconciliationResult.InvoiceReconciliation;
            Assert.AreEqual(invoiceAmount, invoiceRecon.TotalGL, "Invoice amount should match GL total");
            Assert.AreEqual(0m, invoiceRecon.TotalDifference, "Should have zero difference");
            
            // Verify invoice was processed
            var processedInvoice = invoiceRecon.InvoicesProcessed
                ?.FirstOrDefault(i => i.InvoiceId == invoice.InvoiceNumber);

            Assert.IsNotNull(processedInvoice, "Invoice should be processed in reconciliation");
            Assert.AreEqual(invoiceAmount, processedInvoice.InvoiceAmount, "Processed invoice amount should match");

            _logger.LogInformation("Invoice vs GL consistency test completed: InvoiceAmount={InvoiceAmount}, GLTotal={GLTotal}", 
                invoiceAmount, invoiceRecon.TotalGL);
        }

        /// <summary>
        /// Test 2: Invoice reconciliation should detect mismatches
        /// </summary>
        [TestMethod]
        public async Task InvoiceVsGL_ShouldDetectMismatches_WhenInconsistentData()
        {
            // Arrange - Create inconsistent invoice and GL data
            var companyId = 1;
            var asOfDate = DateTime.UtcNow;
            var invoiceAmount = 5000m;
            var glAmount = 4500m; // Different amount

            // Create invoice
            var invoice = new Invoice
            {
                CompanyId = companyId,
                CustomerId = "CUST-002",
                InvoiceNumber = "INV-MISMATCH-001",
                InvoiceAmount = invoiceAmount,
                Status = "Posted",
                CreatedAt = asOfDate.AddDays(-1)
            };

            _context.Invoices.Add(invoice);

            // Create mismatching GL entry
            var glEntry = new JournalEntry
            {
                CompanyId = companyId,
                TransactionDate = asOfDate.AddDays(-1),
                Description = $"Invoice {invoice.InvoiceNumber}",
                Status = JournalStatus.Posted,
                JournalLines = new[]
                {
                    new JournalLine { AccountId = 4001, DebitAmount = 0, CreditAmount = glAmount, Description = $"Invoice {invoice.InvoiceNumber}" }
                }
            };

            _context.JournalEntries.Add(glEntry);
            await _context.SaveChangesAsync();

            // Act - Run reconciliation
            var reconciliationResult = await _reconciliationEngine.RunComprehensiveReconciliationAsync(companyId, asOfDate);

            // Assert
            Assert.IsTrue(reconciliationResult.IsSuccess, "Reconciliation should complete");
            Assert.IsNotNull(reconciliationResult.InvoiceReconciliation, "Invoice reconciliation should not be null");
            
            // Verify mismatch detection
            var invoiceRecon = reconciliationResult.InvoiceReconciliation;
            Assert.AreEqual(invoiceAmount, invoiceRecon.TotalInvoices, "Invoice total should match");
            Assert.AreEqual(glAmount, invoiceRecon.TotalGL, "GL total should be different");
            Assert.AreEqual(500m, invoiceRecon.TotalDifference, "Should detect 500 difference");
            
            // Verify specific difference details
            var differences = invoiceRecon.Differences;
            Assert.IsTrue(differences.Count > 0, "Should have difference details");
            Assert.IsTrue(differences.Any(d => d.Description.Contains("amount mismatch")), 
                "Should detect amount mismatch");

            _logger.LogInformation("Invoice vs GL mismatch test completed: Difference={Difference}", invoiceRecon.TotalDifference);
        }

        #endregion

        #region Test 2: Payroll vs Expense Consistency

        /// <summary>
        /// Test 3: Payroll reconciliation should match expense accounts
        /// </summary>
        [TestMethod]
        public async Task PayrollVsExpense_ShouldMatchExactly_WhenConsistentData()
        {
            // Arrange - Create consistent payroll and expense data
            var companyId = 1;
            var asOfDate = DateTime.UtcNow;
            var payrollAmount = 10000m;
            var expenseAccountId = 5001;

            // Create payroll records
            var payrollRecords = new List<PayrollRecord>
            {
                new PayrollRecord
                {
                    CompanyId = companyId,
                    EmployeeId = "EMP-001",
                    PayPeriod = "2024-01",
                    GrossPay = 5000m,
                    NetPay = 4000m,
                    Taxes = 1000m,
                    Status = "Posted",
                    ProcessedAt = asOfDate.AddDays(-1)
                },
                new PayrollRecord
                {
                    CompanyId = companyId,
                    EmployeeId = "EMP-002",
                    PayPeriod = "2024-01",
                    GrossPay = 5000m,
                    NetPay = 4000m,
                    Taxes = 1000m,
                    Status = "Posted",
                    ProcessedAt = asOfDate.AddDays(-1)
                }
            };

            _context.PayrollRecords.AddRange(payrollRecords);

            // Create corresponding expense entries
            var expenseEntries = new List<JournalEntry>
            {
                new JournalEntry
                {
                    CompanyId = companyId,
                    TransactionDate = asOfDate.AddDays(-1),
                    Description = "Payroll expense - EMP-001",
                    Status = JournalStatus.Posted,
                    JournalLines = new[]
                    {
                        new JournalLine { AccountId = expenseAccountId, DebitAmount = 5000m, CreditAmount = 0, Description = "Payroll expense EMP-001" }
                    }
                },
                new JournalEntry
                {
                    CompanyId = companyId,
                    TransactionDate = asOfDate.AddDays(-1),
                    Description = "Payroll expense - EMP-002",
                    Status = JournalStatus.Posted,
                    JournalLines = new[]
                    {
                        new JournalLine { AccountId = expenseAccountId, DebitAmount = 5000m, CreditAmount = 0, Description = "Payroll expense EMP-002" }
                    }
                }
            };

            _context.JournalEntries.AddRange(expenseEntries);
            await _context.SaveChangesAsync();

            // Act - Run reconciliation
            var reconciliationResult = await _reconciliationEngine.RunComprehensiveReconciliationAsync(companyId, asOfDate);

            // Assert
            Assert.IsTrue(reconciliationResult.IsSuccess, "Reconciliation should succeed");
            Assert.IsNotNull(reconciliationResult.PayrollReconciliation, "Payroll reconciliation should not be null");
            
            // Verify exact match
            var payrollRecon = reconciliationResult.PayrollReconciliation;
            Assert.AreEqual(payrollAmount, payrollRecon.TotalPayroll, "Payroll total should match");
            Assert.AreEqual(payrollAmount, payrollRecon.TotalExpenses, "Expense total should match payroll");
            Assert.AreEqual(0m, payrollRecon.TotalDifference, "Should have zero difference");
            
            // Verify all payroll records were processed
            Assert.AreEqual(2, payrollRecon.PayrollRecordsProcessed, "Should process all payroll records");

            _logger.LogInformation("Payroll vs Expense consistency test completed: Payroll={PayrollTotal}, Expenses={ExpenseTotal}", 
                payrollRecon.TotalPayroll, payrollRecon.TotalExpenses);
        }

        #endregion

        #region Test 3: Tax Consistency Validation

        /// <summary>
        /// Test 4: Tax reconciliation should maintain liability consistency
        /// </summary>
        [TestMethod]
        public async Task TaxConsistency_ShouldMaintainLiabilityBalance_WhenAccurateData()
        {
            // Arrange - Create consistent tax data
            var companyId = 1;
            var asOfDate = DateTime.UtcNow;
            var taxCollected = 3000m;
            var taxPayableAccountId = 2001;
            var taxPaidAccountId = 2002;

            // Create tax records
            var taxRecords = new List<TaxRecord>
            {
                new TaxRecord
                {
                    CompanyId = companyId,
                    TaxType = "Sales Tax",
                    TaxPeriod = "2024-01",
                    TaxCollected = taxCollected,
                    TaxPayable = taxCollected,
                    Status = "Posted",
                    ProcessedAt = asOfDate.AddDays(-1)
                }
            };

            _context.TaxRecords.AddRange(taxRecords);

            // Create corresponding GL entries
            var taxGlEntries = new List<JournalEntry>
            {
                new JournalEntry
                {
                    CompanyId = companyId,
                    TransactionDate = asOfDate.AddDays(-1),
                    Description = "Tax payable - Sales Tax",
                    Status = JournalStatus.Posted,
                    JournalLines = new[]
                    {
                        new JournalLine { AccountId = taxPayableAccountId, DebitAmount = taxCollected, CreditAmount = 0, Description = "Tax payable" }
                    }
                },
                new JournalEntry
                {
                    CompanyId = companyId,
                    TransactionDate = asOfDate.AddDays(-1),
                    Description = "Tax paid - Sales Tax",
                    Status = JournalStatus.Posted,
                    JournalLines = new[]
                    {
                        new JournalLine { AccountId = taxPaidAccountId, DebitAmount = 0, CreditAmount = taxCollected, Description = "Tax payment" }
                    }
                }
            };

            _context.JournalEntries.AddRange(taxGlEntries);
            await _context.SaveChangesAsync();

            // Act - Run reconciliation
            var reconciliationResult = await _reconciliationEngine.RunComprehensiveReconciliationAsync(companyId, asOfDate);

            // Assert
            Assert.IsTrue(reconciliationResult.IsSuccess, "Reconciliation should succeed");
            Assert.IsNotNull(reconciliationResult.TaxReconciliation, "Tax reconciliation should not be null");
            
            // Verify tax liability balance
            var taxRecon = reconciliationResult.TaxReconciliation;
            Assert.AreEqual(taxCollected, taxRecon.TotalTaxCollected, "Tax collected should match");
            Assert.AreEqual(taxCollected, taxRecon.TotalTaxPaid, "Tax paid should match collected");
            Assert.AreEqual(0m, taxRecon.TotalDifference, "Should have zero tax difference");
            
            // Verify liability accounts balance
            var payableAccount = await _context.FinanceAccounts.FindAsync(taxPayableAccountId);
            var paidAccount = await _context.FinanceAccounts.FindAsync(taxPaidAccountId);

            Assert.IsNotNull(payableAccount, "Tax payable account should exist");
            Assert.IsNotNull(paidAccount, "Tax paid account should exist");
            Assert.AreEqual(taxCollected, payableAccount.CurrentBalance, "Payable account should match tax collected");
            Assert.AreEqual(-taxCollected, paidAccount.CurrentBalance, "Paid account should be negative of tax collected");

            _logger.LogInformation("Tax consistency test completed: TaxCollected={TaxCollected}, TaxPaid={TaxPaid}", 
                taxRecon.TotalTaxCollected, taxRecon.TotalTaxPaid);
        }

        #endregion

        #region Test 4: Asset Depreciation Accuracy

        /// <summary>
        /// Test 5: Asset depreciation should match GL impact
        /// </summary>
        [TestMethod]
        public async Task AssetDepreciation_ShouldMatchGLImpact_WhenAccurateCalculation()
        {
            // Arrange - Create asset and depreciation data
            var companyId = 1;
            var asOfDate = DateTime.UtcNow;
            var assetCost = 10000m;
            var depreciationAmount = 1000m;
            var accumulatedDepreciation = 5000m;
            var netBookValue = assetCost - accumulatedDepreciation;

            // Create fixed asset
            var asset = new FixedAsset
            {
                CompanyId = companyId,
                AssetNumber = "ASSET-001",
                Description = "Test asset",
                Cost = assetCost,
                AccumulatedDepreciation = accumulatedDepreciation,
                NetBookValue = netBookValue,
                Status = "Active",
                PurchaseDate = asOfDate.AddYears(-2),
                DepreciationMethod = "Straight Line"
            };

            _context.FixedAssets.Add(asset);

            // Create depreciation GL entry
            var depreciationEntry = new JournalEntry
            {
                CompanyId = companyId,
                TransactionDate = asOfDate.AddDays(-1),
                Description = "Depreciation - ASSET-001",
                Status = JournalStatus.Posted,
                JournalLines = new[]
                {
                    new JournalLine { AccountId = 6001, DebitAmount = depreciationAmount, CreditAmount = 0, Description = "Depreciation expense" }
                }
            };

            _context.JournalEntries.Add(depreciationEntry);
            await _context.SaveChangesAsync();

            // Act - Run reconciliation
            var reconciliationResult = await _reconciliationEngine.RunComprehensiveReconciliationAsync(companyId, asOfDate);

            // Assert
            Assert.IsTrue(reconciliationResult.IsSuccess, "Reconciliation should succeed");
            Assert.IsNotNull(reconciliationResult.FixedAssetReconciliation, "Fixed asset reconciliation should not be null");
            
            // Verify asset depreciation accuracy
            var assetRecon = reconciliationResult.FixedAssetReconciliation;
            Assert.AreEqual(1, assetRecon.AssetsProcessed, "Should process one asset");
            Assert.AreEqual(assetCost, assetRecon.TotalAssetCost, "Asset cost should match");
            Assert.AreEqual(accumulatedDepreciation, assetRecon.TotalAccumulatedDepreciation, "Accumulated depreciation should match");
            Assert.AreEqual(netBookValue, assetRecon.TotalNetBookValue, "Net book value should match");
            
            // Verify GL impact
            Assert.AreEqual(depreciationAmount, assetRecon.TotalDepreciationExpense, "Depreciation expense should match GL");

            _logger.LogInformation("Asset depreciation accuracy test completed: AssetCost={AssetCost}, NetBookValue={NetBookValue}", 
                assetRecon.TotalAssetCost, assetRecon.TotalNetBookValue);
        }

        #endregion

        #region Test 5: Cross-Module Data Integrity

        /// <summary>
        /// Test 6: All modules should maintain data consistency
        /// </summary>
        [TestMethod]
        public async Task CrossModuleDataIntegrity_ShouldMaintainConsistency_WhenCompleteDataset()
        {
            // Arrange - Create complete dataset across all modules
            var companyId = 1;
            var asOfDate = DateTime.UtcNow;

            // Create complete financial dataset
            var completeDataset = await CreateCompleteFinancialDataset(companyId, asOfDate);

            // Act - Run comprehensive reconciliation
            var reconciliationResult = await _reconciliationEngine.RunComprehensiveReconciliationAsync(companyId, asOfDate);

            // Assert
            Assert.IsTrue(reconciliationResult.IsSuccess, "Comprehensive reconciliation should succeed");
            
            // Verify all modules processed correctly
            Assert.IsNotNull(reconciliationResult.InvoiceReconciliation, "Invoice reconciliation should not be null");
            Assert.IsNotNull(reconciliationResult.PayrollReconciliation, "Payroll reconciliation should not be null");
            Assert.IsNotNull(reconciliationResult.TaxReconciliation, "Tax reconciliation should not be null");
            Assert.IsNotNull(reconciliationResult.FixedAssetReconciliation, "Fixed asset reconciliation should not be null");
            
            // Verify overall consistency
            var expectedInvoices = completeDataset.Invoices.Count;
            var expectedPayrollRecords = completeDataset.PayrollRecords.Count;
            var expectedTaxRecords = completeDataset.TaxRecords.Count;
            var expectedAssets = completeDataset.Assets.Count;

            Assert.AreEqual(expectedInvoices, reconciliationResult.InvoiceReconciliation?.InvoicesProcessed?.Count ?? 0, 
                "Should process all invoices");
            Assert.AreEqual(expectedPayrollRecords, reconciliationResult.PayrollReconciliation?.PayrollRecordsProcessed ?? 0, 
                "Should process all payroll records");
            Assert.AreEqual(expectedTaxRecords, reconciliationResult.TaxReconciliation?.TaxRecordsProcessed ?? 0, 
                "Should process all tax records");
            Assert.AreEqual(expectedAssets, reconciliationResult.FixedAssetReconciliation?.AssetsProcessed ?? 0, 
                "Should process all assets");

            // Verify no logical mismatches
            Assert.AreEqual(0m, reconciliationResult.InvoiceReconciliation?.TotalDifference ?? 0m, 
                "Should have zero invoice difference");
            Assert.AreEqual(0m, reconciliationResult.PayrollReconciliation?.TotalDifference ?? 0m, 
                "Should have zero payroll difference");
            Assert.AreEqual(0m, reconciliationResult.TaxReconciliation?.TotalDifference ?? 0m, 
                "Should have zero tax difference");

            _logger.LogInformation("Cross-module data integrity test completed: All modules consistent");
        }

        #endregion

        #region Helper Methods

        private async Task<CompleteFinancialDataset> CreateCompleteFinancialDataset(int companyId, DateTime asOfDate)
        {
            // Create invoices
            var invoices = new List<Invoice>
            {
                new Invoice
                {
                    CompanyId = companyId,
                    CustomerId = "CUST-001",
                    InvoiceNumber = "INV-001",
                    InvoiceAmount = 5000m,
                    Status = "Posted",
                    CreatedAt = asOfDate.AddDays(-1)
                },
                new Invoice
                {
                    CompanyId = companyId,
                    CustomerId = "CUST-002",
                    InvoiceNumber = "INV-002",
                    InvoiceAmount = 3000m,
                    Status = "Posted",
                    CreatedAt = asOfDate.AddDays(-1)
                }
            };

            // Create payroll records
            var payrollRecords = new List<PayrollRecord>
            {
                new PayrollRecord
                {
                    CompanyId = companyId,
                    EmployeeId = "EMP-001",
                    PayPeriod = "2024-01",
                    GrossPay = 5000m,
                    NetPay = 4000m,
                    Taxes = 1000m,
                    Status = "Posted",
                    ProcessedAt = asOfDate.AddDays(-1)
                }
            };

            // Create tax records
            var taxRecords = new List<TaxRecord>
            {
                new TaxRecord
                {
                    CompanyId = companyId,
                    TaxType = "Sales Tax",
                    TaxPeriod = "2024-01",
                    TaxCollected = 2000m,
                    TaxPayable = 2000m,
                    Status = "Posted",
                    ProcessedAt = asOfDate.AddDays(-1)
                }
            };

            // Create assets
            var assets = new List<FixedAsset>
            {
                new FixedAsset
                {
                    CompanyId = companyId,
                    AssetNumber = "ASSET-001",
                    Description = "Test asset 1",
                    Cost = 10000m,
                    AccumulatedDepreciation = 2000m,
                    NetBookValue = 8000m,
                    Status = "Active",
                    PurchaseDate = asOfDate.AddYears(-2),
                    DepreciationMethod = "Straight Line"
                }
            };

            // Save to context
            _context.Invoices.AddRange(invoices);
            _context.PayrollRecords.AddRange(payrollRecords);
            _context.TaxRecords.AddRange(taxRecords);
            _context.FixedAssets.AddRange(assets);
            await _context.SaveChangesAsync();

            return new CompleteFinancialDataset
            {
                Invoices = invoices,
                PayrollRecords = payrollRecords,
                TaxRecords = taxRecords,
                Assets = assets
            };
        }

        #endregion
    }

    /// <summary>
    /// Complete financial dataset for testing
    /// </summary>
    public class CompleteFinancialDataset
    {
        public List<Invoice> Invoices { get; set; } = new();
        public List<PayrollRecord> PayrollRecords { get; set; } = new();
        public List<TaxRecord> TaxRecords { get; set; } = new();
        public List<FixedAsset> Assets { get; set; } = new();
    }

    /// <summary>
    /// Test result container for reconciliation accuracy tests
    /// </summary>
    public class ReconciliationAccuracyTestResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public decimal InvoiceVsGLDifference { get; set; }
        public decimal PayrollVsExpenseDifference { get; set; }
        public decimal TaxDifference { get; set; }
        public decimal AssetDepreciationDifference { get; set; }
        public List<string> Inconsistencies { get; set; } = new();
        public TimeSpan ExecutionTime { get; set; }
        public int ModulesProcessed { get; set; }
    }
}
