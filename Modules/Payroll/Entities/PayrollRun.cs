using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureERP2.Modules.Payroll.Entities
{
    public class PayrollRun : BaseEntity
    {
        [Required]
        [StringLength(50)]
        public string PayrollNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public DateTime PeriodStart { get; set; }

        [Required]
        public DateTime PeriodEnd { get; set; }

        [Required]
        public DateTime ProcessDate { get; set; } = DateTime.UtcNow;
        
        [Required]
        public DateTime PayDate { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Draft"; // Draft, Processing, Processed, Failed

        [Required]
        public int TotalEmployees { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalGrossPay { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossSalaries { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalTaxDeductions { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalInsuranceDeductions { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPensionDeductions { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalOtherDeductions { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalNetPay { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalOvertimePay { get; set; }
        
        // Additional properties for reconciliation
        public decimal Salaries { get; set; }
        public decimal NetSalaries { get; set; }
        public decimal TotalTaxes { get; set; }

        [StringLength(500)]
        public string Notes { get; set; } = string.Empty;

        public DateTime? GLPostedDate { get; set; }

        // Navigation property for payroll run employees
        public virtual ICollection<PayrollRunEmployee> PayrollRunEmployees { get; set; } = new List<PayrollRunEmployee>();

        // Computed property for total deductions
        [NotMapped]
        public decimal TotalDeductions => 
            TotalTaxDeductions + 
            TotalInsuranceDeductions + 
            TotalPensionDeductions + 
            TotalOtherDeductions;

        // Computed property for payroll period description
        [NotMapped]
        public string PeriodDescription => 
            $"{PeriodStart:MMM dd, yyyy} - {PeriodEnd:MMM dd, yyyy}";

        // Computed property for average net pay
        [NotMapped]
        public decimal AverageNetPay => TotalEmployees > 0 ? TotalNetPay / TotalEmployees : 0;
    }

    public class PayrollRunEmployee : BaseEntity
    {
        [Required]
        public int PayrollRunId { get; set; }

        [ForeignKey("PayrollRunId")]
        public virtual PayrollRun PayrollRun { get; set; } = null!;

        [Required]
        public int EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public virtual Employee Employee { get; set; } = null!;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal BaseSalary { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal OvertimeHours { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal OvertimeRate { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal OvertimePay { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossPay { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxDeductions { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal InsuranceDeductions { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PensionDeductions { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal OtherDeductions { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalDeductions { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal NetPay { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal TaxRate { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal InsuranceRate { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal PensionRate { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal OtherDeductionRate { get; set; }

        [StringLength(500)]
        public string Notes { get; set; } = string.Empty;

        // Computed property for effective hourly rate
        [NotMapped]
        public decimal EffectiveHourlyRate => (GrossPay - OvertimePay) / 160; // Assuming 160 regular hours/month

        // Computed property for overtime multiplier
        [NotMapped]
        public decimal OvertimeMultiplier => EffectiveHourlyRate > 0 ? OvertimeRate / EffectiveHourlyRate : 1.5m;

        // Computed property for deduction percentage
        [NotMapped]
        public decimal DeductionPercentage => GrossPay > 0 ? (TotalDeductions / GrossPay) * 100 : 0;
    }
}
