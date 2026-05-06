using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureERP2.Modules.Assets.Entities
{
    public class FixedAsset : BaseEntity
    {
        [Required]
        [StringLength(200)]
        public string AssetName { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Cost { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal SalvageValue { get; set; }

        [Required]
        [Range(1, 50)]
        public int UsefulLifeYears { get; set; }

        [Required]
        [StringLength(50)]
        public string DepreciationMethod { get; set; } // StraightLine, DecliningBalance, SumOfYears

        [Required]
        public DateTime PurchaseDate { get; set; }

        public DateTime? PlacedInServiceDate { get; set; }

        public DateTime? DisposalDate { get; set; }

        [StringLength(100)]
        public string AssetNumber { get; set; }

        [StringLength(100)]
        public string SerialNumber { get; set; }

        [StringLength(200)]
        public string Location { get; set; }

        [StringLength(100)]
        public string Category { get; set; }

        [StringLength(100)]
        public string Department { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual ICollection<DepreciationSchedule> DepreciationSchedules { get; set; }

        // Computed properties
        [NotMapped]
        public decimal AnnualDepreciation
        {
            get
            {
                if (DepreciationMethod == "StraightLine")
                {
                    return (Cost - SalvageValue) / UsefulLifeYears;
                }
                return 0; // Will be calculated differently for other methods
            }
        }

        [NotMapped]
        public decimal MonthlyDepreciation => AnnualDepreciation / 12;

        [NotMapped]
        public decimal AccumulatedDepreciation
        {
            get
            {
                var endDate = DateTime.Today;
                if (DisposalDate.HasValue && DisposalDate.Value < endDate)
                    endDate = DisposalDate.Value;

                var startDate = PlacedInServiceDate ?? PurchaseDate;
                var monthsInService = Math.Max(0, 
                    ((endDate.Year - startDate.Year) * 12 + endDate.Month - startDate.Month) + 1);

                var totalMonths = UsefulLifeYears * 12;
                var monthsToDepreciate = Math.Min(monthsInService, totalMonths);

                return MonthlyDepreciation * monthsToDepreciate;
            }
        }

        [NotMapped]
        public decimal NetBookValue => Cost - AccumulatedDepreciation;

        [NotMapped]
        public decimal RemainingDepreciation => Cost - SalvageValue - AccumulatedDepreciation;

        [NotMapped]
        public int RemainingUsefulLifeYears
        {
            get
            {
                var startDate = PlacedInServiceDate ?? PurchaseDate;
                var yearsElapsed = Math.Max(0, DateTime.Today.Year - startDate.Year);
                return Math.Max(0, UsefulLifeYears - yearsElapsed);
            }
        }

        [NotMapped]
        public double DepreciationPercentage => (double)(AccumulatedDepreciation / Cost) * 100;

        public string GetAssetStatus()
        {
            if (!IsActive) return "Disposed";
            if (!PlacedInServiceDate.HasValue) return "Not Placed in Service";
            if (RemainingDepreciation <= 0) return "Fully Depreciated";
            return "Active";
        }
    }

    public class DepreciationSchedule : BaseEntity
    {
        [Required]
        public long FixedAssetId { get; set; }

        [ForeignKey("FixedAssetId")]
        public virtual FixedAsset FixedAsset { get; set; }

        [Required]
        public DateTime DepreciationDate { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DepreciationAmount { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal AccumulatedDepreciationToDate { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal NetBookValue { get; set; }

        [Required]
        [StringLength(50)]
        public string PeriodType { get; set; } // Monthly, Yearly

        [StringLength(100)]
        public string JournalEntryReference { get; set; }

        public bool IsPosted { get; set; } = false;

        public DateTime? PostedDate { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }
    }
}
