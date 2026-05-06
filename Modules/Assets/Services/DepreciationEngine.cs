using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SecureERP2;
using SecureERP2.Modules.Assets.Entities;
using SecureERP2.Modules.Finance;
using SecureERP2.Modules.Finance.Entities;
using SecureERP2.Modules.Finance.Services;

namespace SecureERP2.Modules.Assets.Services
{
    public class DepreciationEngine
    {
        private readonly ERPDbContext _context;
        private readonly AccountingEngine _accountingEngine;

        public DepreciationEngine(ERPDbContext context, AccountingEngine accountingEngine)
        {
            _context = context;
            _accountingEngine = accountingEngine;
        }

        public async Task<decimal> CalculateDepreciationAsync(FixedAsset asset, DateTime date)
        {
            switch (asset.DepreciationMethod)
            {
                case "StraightLine":
                    return CalculateStraightLineDepreciation(asset, date);
                case "DecliningBalance":
                    return await CalculateDecliningBalanceDepreciation(asset, date);
                case "SumOfYears":
                    return CalculateSumOfYearsDepreciation(asset, date);
                default:
                    throw new ArgumentException($"Unsupported depreciation method: {asset.DepreciationMethod}");
            }
        }

        private decimal CalculateStraightLineDepreciation(FixedAsset asset, DateTime date)
        {
            // Straight Line Formula: (Cost - Salvage Value) / Useful Life
            var annualDepreciation = (asset.Cost - asset.SalvageValue) / asset.UsefulLifeYears;
            var monthlyDepreciation = annualDepreciation / 12;

            // Check if asset is in service
            var startDate = asset.PlacedInServiceDate ?? asset.PurchaseDate;
            if (date < startDate)
                return 0;

            // Check if asset is disposed
            if (asset.DisposalDate.HasValue && date > asset.DisposalDate.Value)
                return 0;

            // Calculate total months to depreciate
            var monthsInService = ((date.Year - startDate.Year) * 12 + date.Month - startDate.Month) + 1;
            var totalMonths = asset.UsefulLifeYears * 12;

            if (monthsInService >= totalMonths)
                return 0; // Fully depreciated

            return monthlyDepreciation;
        }

        private async Task<decimal> CalculateDecliningBalanceDepreciation(FixedAsset asset, DateTime date)
        {
            // Double Declining Balance Formula: 2 × (1 / Useful Life) × Book Value at Beginning of Year
            var rate = 2.0 / asset.UsefulLifeYears;
            
            var startDate = asset.PlacedInServiceDate ?? asset.PurchaseDate;
            if (date < startDate)
                return 0;

            if (asset.DisposalDate.HasValue && date > asset.DisposalDate.Value)
                return 0;

            // Calculate accumulated depreciation to date
            var accumulatedDepreciation = await GetAccumulatedDepreciationToDate(asset, date);
            var bookValue = asset.Cost - accumulatedDepreciation;

            // Don't depreciate below salvage value
            var maxDepreciation = bookValue - asset.SalvageValue;
            if (maxDepreciation <= 0)
                return 0;

            var annualDepreciation = bookValue * (decimal)rate;
            var monthlyDepreciation = annualDepreciation / 12;

            return Math.Min(monthlyDepreciation, maxDepreciation / 12);
        }

        private decimal CalculateSumOfYearsDepreciation(FixedAsset asset, DateTime date)
        {
            // Sum of Years Digits Formula: (Remaining Life / Sum of Years) × (Cost - Salvage Value)
            var startDate = asset.PlacedInServiceDate ?? asset.PurchaseDate;
            if (date < startDate)
                return 0;

            if (asset.DisposalDate.HasValue && date > asset.DisposalDate.Value)
                return 0;

            var yearsElapsed = Math.Max(0, date.Year - startDate.Year);
            var remainingLife = Math.Max(0, asset.UsefulLifeYears - yearsElapsed);

            if (remainingLife <= 0)
                return 0;

            // Calculate sum of years digits
            var sumOfYears = asset.UsefulLifeYears * (asset.UsefulLifeYears + 1) / 2;
            var fraction = (double)remainingLife / sumOfYears;

            var totalDepreciation = asset.Cost - asset.SalvageValue;
            var annualDepreciation = totalDepreciation * (decimal)fraction;
            var monthlyDepreciation = annualDepreciation / 12;

            return monthlyDepreciation;
        }

        private async Task<decimal> GetAccumulatedDepreciationToDate(FixedAsset asset, DateTime date)
        {
            return await _context.DepreciationSchedules
                .Where(d => d.FixedAssetId == asset.Id && d.DepreciationDate <= date)
                .SumAsync(d => d.DepreciationAmount);
        }

        public async Task<List<DepreciationSchedule>> GenerateMonthlyDepreciationScheduleAsync(DateTime periodDate)
        {
            var schedules = new List<DepreciationSchedule>();
            var startDate = new DateTime(periodDate.Year, periodDate.Month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // Get all active assets that should be depreciated
            var assets = await _context.FixedAssets
                .Where(a => a.IsActive && 
                           a.PlacedInServiceDate.HasValue && 
                           a.PlacedInServiceDate.Value <= endDate &&
                           (!a.DisposalDate.HasValue || a.DisposalDate.Value >= startDate))
                .ToListAsync();

            foreach (var asset in assets)
            {
                // Check if depreciation already exists for this period
                var existingSchedule = await _context.DepreciationSchedules
                    .Where(d => d.FixedAssetId == asset.Id && 
                               d.DepreciationDate >= startDate && 
                               d.DepreciationDate <= endDate)
                    .FirstOrDefaultAsync();

                if (existingSchedule != null)
                    continue; // Skip if already processed

                var depreciationAmount = await CalculateDepreciationAsync(asset, periodDate);
                if (depreciationAmount <= 0)
                    continue; // Skip if no depreciation

                var accumulatedDepreciationToDate = await GetAccumulatedDepreciationToDate(asset, startDate.AddDays(-1));
                var netBookValue = asset.Cost - accumulatedDepreciationToDate - depreciationAmount;

                var schedule = new DepreciationSchedule
                {
                    FixedAssetId = asset.Id,
                    DepreciationDate = periodDate,
                    DepreciationAmount = depreciationAmount,
                    AccumulatedDepreciationToDate = accumulatedDepreciationToDate + depreciationAmount,
                    NetBookValue = netBookValue,
                    PeriodType = "Monthly",
                    Notes = $"Monthly depreciation for {asset.AssetName}"
                };

                schedules.Add(schedule);
            }

            return schedules;
        }

        public async Task<List<DepreciationSchedule>> GenerateYearlyDepreciationScheduleAsync(int year)
        {
            var schedules = new List<DepreciationSchedule>();
            var startDate = new DateTime(year, 1, 1);
            var endDate = new DateTime(year, 12, 31);

            // Get all active assets that should be depreciated
            var assets = await _context.FixedAssets
                .Where(a => a.IsActive && 
                           a.PlacedInServiceDate.HasValue && 
                           a.PlacedInServiceDate.Value <= endDate &&
                           (!a.DisposalDate.HasValue || a.DisposalDate.Value >= startDate))
                .ToListAsync();

            foreach (var asset in assets)
            {
                // Check if depreciation already exists for this year
                var existingSchedule = await _context.DepreciationSchedules
                    .Where(d => d.FixedAssetId == asset.Id && 
                               d.DepreciationDate.Year == year &&
                               d.PeriodType == "Yearly")
                    .FirstOrDefaultAsync();

                if (existingSchedule != null)
                    continue; // Skip if already processed

                var yearEndDate = new DateTime(year, 12, 31);
                var depreciationAmount = 0m;

                // Calculate total depreciation for the year
                for (int month = 1; month <= 12; month++)
                {
                    var monthDate = new DateTime(year, month, 1);
                    depreciationAmount += await CalculateDepreciationAsync(asset, monthDate);
                }

                if (depreciationAmount <= 0)
                    continue; // Skip if no depreciation

                var accumulatedDepreciationToDate = await GetAccumulatedDepreciationToDate(asset, startDate.AddDays(-1));
                var netBookValue = asset.Cost - accumulatedDepreciationToDate - depreciationAmount;

                var schedule = new DepreciationSchedule
                {
                    FixedAssetId = asset.Id,
                    DepreciationDate = yearEndDate,
                    DepreciationAmount = depreciationAmount,
                    AccumulatedDepreciationToDate = accumulatedDepreciationToDate + depreciationAmount,
                    NetBookValue = netBookValue,
                    PeriodType = "Yearly",
                    Notes = $"Yearly depreciation for {asset.AssetName}"
                };

                schedules.Add(schedule);
            }

            return schedules;
        }

        public async Task<Dictionary<string, decimal>> GetDepreciationSummaryAsync(DateTime startDate, DateTime endDate)
        {
            var schedules = await _context.DepreciationSchedules
                .Where(d => d.DepreciationDate >= startDate && d.DepreciationDate <= endDate)
                .Include(d => d.FixedAsset)
                .ToListAsync();

            var summary = new Dictionary<string, decimal>
            {
                ["TotalDepreciation"] = schedules.Sum(d => d.DepreciationAmount),
                ["AssetCount"] = schedules.Select(d => d.FixedAssetId).Distinct().Count(),
                ["StraightLineDepreciation"] = schedules
                    .Where(d => d.FixedAsset.DepreciationMethod == "StraightLine")
                    .Sum(d => d.DepreciationAmount),
                ["DecliningBalanceDepreciation"] = schedules
                    .Where(d => d.FixedAsset.DepreciationMethod == "DecliningBalance")
                    .Sum(d => d.DepreciationAmount),
                ["SumOfYearsDepreciation"] = schedules
                    .Where(d => d.FixedAsset.DepreciationMethod == "SumOfYears")
                    .Sum(d => d.DepreciationAmount)
            };

            return summary;
        }
    }
}
