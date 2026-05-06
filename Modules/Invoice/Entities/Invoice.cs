using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureERP2.Modules.Invoice.Entities
{
    public class Invoice : BaseEntity
    {
        [Required]
        [StringLength(50)]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string CustomerName { get; set; } = string.Empty;

        [StringLength(500)]
        public string CustomerAddress { get; set; } = string.Empty;

        [StringLength(100)]
        public string CustomerEmail { get; set; } = string.Empty;

        [StringLength(20)]
        public string CustomerPhone { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal TaxRate { get; set; } = 0.10m; // Default 10% tax

        [Required]
        public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime DueDate { get; set; }

        public DateTime? PaidDate { get; set; }

        public DateTime? GLPostedDate { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Draft"; // Draft, Sent, Paid, Overdue, Cancelled

        [StringLength(1000)]
        public string Notes { get; set; } = string.Empty;

        [StringLength(500)]
        public string Terms { get; set; } = "Payment due within 30 days";

        // Navigation property for invoice items
        public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();

        // Computed property for days overdue
        [NotMapped]
        public int DaysOverdue => Status == "Overdue" && DueDate < DateTime.UtcNow 
            ? (int)(DateTime.UtcNow - DueDate).TotalDays 
            : 0;

        // Computed property for amount due
        [NotMapped]
        public decimal AmountDue => Status == "Paid" ? 0 : TotalAmount;
    }
}
