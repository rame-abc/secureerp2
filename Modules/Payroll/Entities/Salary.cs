using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureERP2.Modules.Payroll.Entities
{
    public class Salary : BaseEntity
    {
        [Required]
        public int EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public virtual Employee Employee { get; set; } = null!;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal BaseSalary { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal HourlyRate { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal OvertimeRate { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal TaxRate { get; set; } = 0.20m;

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal InsuranceRate { get; set; } = 0.05m;

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal PensionRate { get; set; } = 0.05m;

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal OtherDeductionRate { get; set; } = 0.00m;

        [Required]
        public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;

        public DateTime? EndDate { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Active"; // Active, Expired

        [StringLength(500)]
        public string Notes { get; set; } = string.Empty;

        // Computed property for monthly gross salary
        [NotMapped]
        public decimal MonthlyGrossSalary => BaseSalary;

        // Computed property for monthly tax deduction
        [NotMapped]
        public decimal MonthlyTaxDeduction => MonthlyGrossSalary * TaxRate;

        // Computed property for monthly insurance deduction
        [NotMapped]
        public decimal MonthlyInsuranceDeduction => MonthlyGrossSalary * InsuranceRate;

        // Computed property for monthly pension deduction
        [NotMapped]
        public decimal MonthlyPensionDeduction => MonthlyGrossSalary * PensionRate;

        // Computed property for monthly other deductions
        [NotMapped]
        public decimal MonthlyOtherDeductions => MonthlyGrossSalary * OtherDeductionRate;

        // Computed property for total monthly deductions
        [NotMapped]
        public decimal MonthlyTotalDeductions => 
            MonthlyTaxDeduction + 
            MonthlyInsuranceDeduction + 
            MonthlyPensionDeduction + 
            MonthlyOtherDeductions;

        // Computed property for monthly net salary
        [NotMapped]
        public decimal MonthlyNetSalary => MonthlyGrossSalary - MonthlyTotalDeductions;
    }
}
