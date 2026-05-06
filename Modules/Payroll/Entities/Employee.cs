using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureERP2.Modules.Payroll.Entities
{
    public class Employee : BaseEntity
    {
        [Required]
        [StringLength(50)]
        public string EmployeeNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Email { get; set; } = string.Empty;

        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;

        [StringLength(500)]
        public string Address { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Department { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Position { get; set; } = string.Empty;

        [Required]
        public DateTime HireDate { get; set; } = DateTime.UtcNow;

        public DateTime? TerminationDate { get; set; }

        [Required]
        [StringLength(20)]
        public string EmploymentStatus { get; set; } = "Active"; // Active, Terminated, OnLeave

        [Required]
        [StringLength(20)]
        public string EmploymentType { get; set; } = "FullTime"; // FullTime, PartTime, Contract

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal BaseSalary { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal HourlyRate { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal TaxRate { get; set; } = 0.20m; // Default 20% tax

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal InsuranceRate { get; set; } = 0.05m; // Default 5% insurance

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal PensionRate { get; set; } = 0.05m; // Default 5% pension

        [StringLength(500)]
        public string BankAccountNumber { get; set; } = string.Empty;

        [StringLength(100)]
        public string BankName { get; set; } = string.Empty;

        [StringLength(20)]
        public string PaymentMethod { get; set; } = "BankTransfer"; // BankTransfer, Cash, Check

        // Navigation property for salary records
        public virtual ICollection<Salary> Salaries { get; set; } = new List<Salary>();

        // Navigation property for payroll runs
        public virtual ICollection<PayrollRunEmployee> PayrollRunEmployees { get; set; } = new List<PayrollRunEmployee>();

        // Computed property for full name
        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";

        // Computed property for years of service
        [NotMapped]
        public int YearsOfService => (int)((TerminationDate ?? DateTime.UtcNow) - HireDate).TotalDays / 365;

        // Computed property for employment duration
        [NotMapped]
        public string EmploymentDuration => HireDate.ToString("MMM dd, yyyy") + 
            (TerminationDate.HasValue ? $" - {TerminationDate.Value:MMM dd, yyyy}" : " - Present");
    }
}
