using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SecureERP2;
using SecureERP2.Modules.Assets.Entities;
using SecureERP2.Modules.Finance;
using SecureERP2.Modules.Finance.Entities;
using SecureERP2.Modules.Finance.Services;

namespace SecureERP2.Modules.Assets.Services
{
    public class AssetService
    {
        private readonly ERPDbContext _context;
        private readonly DepreciationEngine _depreciationEngine;
        private readonly AccountingEngine _accountingEngine;

        public AssetService(ERPDbContext context, DepreciationEngine depreciationEngine, AccountingEngine accountingEngine)
        {
            _context = context;
            _depreciationEngine = depreciationEngine;
            _accountingEngine = accountingEngine;
        }

        public async Task<FixedAsset> CreateAssetAsync(FixedAsset asset)
        {
            // Generate asset number if not provided
            if (string.IsNullOrEmpty(asset.AssetNumber))
            {
                asset.AssetNumber = await GenerateAssetNumberAsync();
            }

            // Set default placed in service date if not provided
            if (!asset.PlacedInServiceDate.HasValue)
            {
                asset.PlacedInServiceDate = asset.PurchaseDate;
            }

            _context.FixedAssets.Add(asset);
            await _context.SaveChangesAsync();

            // Create initial journal entry for asset purchase
            await CreateAssetPurchaseJournalEntryAsync(asset);

            return asset;
        }

        public async Task<FixedAsset> GetAssetAsync(long id)
        {
            return await _context.FixedAssets
                .Include(a => a.DepreciationSchedules)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<List<FixedAsset>> GetAllAssetsAsync()
        {
            return await _context.FixedAssets
                .Include(a => a.DepreciationSchedules)
                .OrderBy(a => a.AssetName)
                .ToListAsync();
        }

        public async Task<FixedAsset> UpdateAssetAsync(FixedAsset asset)
        {
            var existingAsset = await _context.FixedAssets.FindAsync(asset.Id);
            if (existingAsset == null)
                throw new ArgumentException("Asset not found");

            // Update properties
            existingAsset.AssetName = asset.AssetName;
            existingAsset.Description = asset.Description;
            existingAsset.AssetNumber = asset.AssetNumber;
            existingAsset.SerialNumber = asset.SerialNumber;
            existingAsset.Location = asset.Location;
            existingAsset.Category = asset.Category;
            existingAsset.Department = asset.Department;
            existingAsset.IsActive = asset.IsActive;
            existingAsset.DisposalDate = asset.DisposalDate;

            await _context.SaveChangesAsync();
            return existingAsset;
        }

        public async Task<bool> DeleteAssetAsync(long id)
        {
            var asset = await _context.FixedAssets.FindAsync(id);
            if (asset == null)
                return false;

            // Check if asset has depreciation schedules
            var hasDepreciation = await _context.DepreciationSchedules
                .AnyAsync(d => d.FixedAssetId == id);

            if (hasDepreciation)
            {
                // Soft delete - mark as inactive
                asset.IsActive = false;
                asset.DisposalDate = DateTime.Now;
            }
            else
            {
                // Hard delete if no depreciation
                _context.FixedAssets.Remove(asset);
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<DepreciationSchedule>> RunDepreciationAsync(DateTime periodDate, bool postToLedger = false)
        {
            var schedules = await _depreciationEngine.GenerateMonthlyDepreciationScheduleAsync(periodDate);

            if (schedules.Any())
            {
                _context.DepreciationSchedules.AddRange(schedules);
                await _context.SaveChangesAsync();

                if (postToLedger)
                {
                    await PostDepreciationToLedgerAsync(schedules);
                }
            }

            return schedules;
        }

        public async Task<List<DepreciationSchedule>> RunYearlyDepreciationAsync(int year, bool postToLedger = false)
        {
            var schedules = await _depreciationEngine.GenerateYearlyDepreciationScheduleAsync(year);

            if (schedules.Any())
            {
                _context.DepreciationSchedules.AddRange(schedules);
                await _context.SaveChangesAsync();

                if (postToLedger)
                {
                    await PostDepreciationToLedgerAsync(schedules);
                }
            }

            return schedules;
        }

        private async Task PostDepreciationToLedgerAsync(List<DepreciationSchedule> schedules)
        {
            // Get depreciation expense and accumulated depreciation accounts
            var depreciationExpenseAccount = await _context.FinanceAccounts
                .FirstOrDefaultAsync(a => a.AccountCode == "5300"); // Depreciation Expense

            var accumulatedDepreciationAccount = await _context.FinanceAccounts
                .FirstOrDefaultAsync(a => a.AccountCode == "1500"); // Accumulated Depreciation

            if (depreciationExpenseAccount == null || accumulatedDepreciationAccount == null)
                throw new InvalidOperationException("Depreciation accounts not found in chart of accounts");

            foreach (var schedule in schedules)
            {
                if (schedule.IsPosted)
                    continue;

                // Create transaction for depreciation
                var transaction = new Transaction
                {
                    TransactionNumber = await GenerateTransactionNumberAsync(),
                    TransactionDate = schedule.DepreciationDate,
                    TransactionType = TransactionType.JournalEntry,
                    TransactionStatus = Modules.Finance.TransactionStatus.Approved,
                    Description = $"Depreciation for {schedule.FixedAsset.AssetName} - {schedule.DepreciationDate:MMM yyyy}",
                    ProcessedAt = DateTime.Now
                };

                // Create ledger entries for depreciation
                var ledgerEntries = new List<LedgerEntry>
                {
                    new LedgerEntry
                    {
                        AccountId = depreciationExpenseAccount.Id,
                        DebitAmount = schedule.DepreciationAmount,
                        CreditAmount = 0,
                        Description = $"Depreciation expense for {schedule.FixedAsset.AssetName}",
                        TransactionId = transaction.Id
                    },
                    new LedgerEntry
                    {
                        AccountId = accumulatedDepreciationAccount.Id,
                        DebitAmount = 0,
                        CreditAmount = schedule.DepreciationAmount,
                        Description = $"Accumulated depreciation for {schedule.FixedAsset.AssetName}",
                        TransactionId = transaction.Id
                    }
                };

                // Save transaction and ledger entries
                _context.Transactions.Add(transaction);
                _context.LedgerEntries.AddRange(ledgerEntries);
                await _context.SaveChangesAsync();

                // Update schedule
                schedule.IsPosted = true;
                schedule.PostedDate = DateTime.Now;
                schedule.JournalEntryReference = $"DEP-{schedule.Id}-{DateTime.Now:yyyyMMdd}";
            }

            await _context.SaveChangesAsync();
        }

        private async Task CreateAssetPurchaseJournalEntryAsync(FixedAsset asset)
        {
            // Get asset account and cash/bank account
            var assetAccount = await _context.FinanceAccounts
                .FirstOrDefaultAsync(a => a.AccountCode == "1600"); // Fixed Assets

            var cashAccount = await _context.FinanceAccounts
                .FirstOrDefaultAsync(a => a.AccountCode == "1000"); // Cash

            if (assetAccount == null || cashAccount == null)
                return; // Accounts not set up yet

            // Create transaction for asset purchase
            var transaction = new Transaction
            {
                TransactionNumber = await GenerateTransactionNumberAsync(),
                TransactionDate = asset.PurchaseDate,
                TransactionType = TransactionType.JournalEntry,
                TransactionStatus = Modules.Finance.TransactionStatus.Approved,
                Description = $"Purchase of {asset.AssetName}",
                ProcessedAt = DateTime.Now
            };

            // Create ledger entries for asset purchase
            var ledgerEntries = new List<LedgerEntry>
            {
                new LedgerEntry
                {
                    AccountId = assetAccount.Id,
                    DebitAmount = asset.Cost,
                    CreditAmount = 0,
                    Description = $"Purchase of {asset.AssetName}",
                    TransactionId = transaction.Id
                },
                new LedgerEntry
                {
                    AccountId = cashAccount.Id,
                    DebitAmount = 0,
                    CreditAmount = asset.Cost,
                    Description = $"Cash payment for {asset.AssetName}",
                    TransactionId = transaction.Id
                }
            };

            // Save transaction and ledger entries
            _context.Transactions.Add(transaction);
            _context.LedgerEntries.AddRange(ledgerEntries);
            await _context.SaveChangesAsync();
        }

        private async Task<string> GenerateAssetNumberAsync()
        {
            var year = DateTime.Now.Year;
            var count = await _context.FixedAssets
                .CountAsync(a => a.AssetNumber != null && a.AssetNumber.StartsWith($"AST-{year}"));

            return $"AST-{year}-{(count + 1):D4}";
        }

        private async Task<string> GenerateTransactionNumberAsync()
        {
            var year = DateTime.Now.Year;
            var month = DateTime.Now.Month;
            var count = await _context.Transactions
                .CountAsync(t => t.TransactionNumber != null && t.TransactionNumber.StartsWith($"TRN-{year:D4}-{month:D2}"));

            return $"TRN-{year:D4}-{month:D2}-{(count + 1):D4}";
        }

        public async Task<Dictionary<string, object>> GetAssetSummaryAsync()
        {
            var assets = await _context.FixedAssets.ToListAsync();
            var depreciationSchedules = await _context.DepreciationSchedules.ToListAsync();

            var summary = new Dictionary<string, object>
            {
                ["TotalAssets"] = assets.Count,
                ["ActiveAssets"] = assets.Count(a => a.IsActive),
                ["TotalCost"] = assets.Sum(a => a.Cost),
                ["TotalAccumulatedDepreciation"] = depreciationSchedules.Sum(d => d.DepreciationAmount),
                ["TotalNetBookValue"] = assets.Sum(a => a.NetBookValue),
                ["AssetsByCategory"] = assets
                    .GroupBy(a => a.Category ?? "Uncategorized")
                    .ToDictionary(g => g.Key, g => g.Count()),
                ["AssetsByDepartment"] = assets
                    .GroupBy(a => a.Department ?? "Unassigned")
                    .ToDictionary(g => g.Key, g => g.Count()),
                ["DepreciationMethods"] = assets
                    .GroupBy(a => a.DepreciationMethod)
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            return summary;
        }

        public async Task<List<FixedAsset>> GetAssetsByCategoryAsync(string category)
        {
            return await _context.FixedAssets
                .Where(a => a.Category == category)
                .Include(a => a.DepreciationSchedules)
                .ToListAsync();
        }

        public async Task<List<FixedAsset>> GetAssetsByDepartmentAsync(string department)
        {
            return await _context.FixedAssets
                .Where(a => a.Department == department)
                .Include(a => a.DepreciationSchedules)
                .ToListAsync();
        }

        public async Task<List<DepreciationSchedule>> GetDepreciationScheduleAsync(long assetId)
        {
            return await _context.DepreciationSchedules
                .Where(d => d.FixedAssetId == assetId)
                .OrderBy(d => d.DepreciationDate)
                .ToListAsync();
        }
    }
}
