using Microsoft.EntityFrameworkCore;
using SecureERP2.Modules.Invoice.Entities;
using SecureERP2.Modules.Finance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SecureERP2.Modules.Invoice.Services
{
    public class InvoiceService
    {
        private readonly ERPDbContext _context;
        private readonly AccountingEngine _accountingEngine;

        public InvoiceService(ERPDbContext context, AccountingEngine accountingEngine)
        {
            _context = context;
            _accountingEngine = accountingEngine;
        }

        public async Task<SecureERP2.Modules.Invoice.Entities.Invoice> CreateInvoiceAsync(SecureERP2.Modules.Invoice.Entities.Invoice invoice)
        {
            // Generate invoice number if not provided
            if (string.IsNullOrEmpty(invoice.InvoiceNumber))
            {
                invoice.InvoiceNumber = await GenerateInvoiceNumberAsync();
            }

            // Set default values
            invoice.InvoiceDate = DateTime.UtcNow;
            invoice.DueDate = invoice.InvoiceDate.AddDays(30); // Default 30 days due

            // Calculate totals
            await CalculateInvoiceTotalsAsync(invoice);

            // Save invoice
            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            return invoice;
        }

        public async Task<SecureERP2.Modules.Invoice.Entities.Invoice> UpdateInvoiceAsync(SecureERP2.Modules.Invoice.Entities.Invoice invoice)
        {
            // Check if invoice can be updated (not paid)
            var existingInvoice = await _context.Invoices
                .Include(i => i.InvoiceItems)
                .FirstOrDefaultAsync(i => i.Id == invoice.Id && i.CompanyId == invoice.CompanyId);

            if (existingInvoice == null)
            {
                throw new Exception("Invoice not found");
            }

            if (existingInvoice.Status == "Paid")
            {
                throw new Exception("Cannot update a paid invoice");
            }

            // Update properties
            existingInvoice.CustomerName = invoice.CustomerName;
            existingInvoice.CustomerAddress = invoice.CustomerAddress;
            existingInvoice.CustomerEmail = invoice.CustomerEmail;
            existingInvoice.CustomerPhone = invoice.CustomerPhone;
            existingInvoice.Notes = invoice.Notes;
            existingInvoice.Terms = invoice.Terms;
            existingInvoice.TaxRate = invoice.TaxRate;

            // Update items
            _context.InvoiceItems.RemoveRange(existingInvoice.InvoiceItems);
            existingInvoice.InvoiceItems.Clear();

            foreach (var item in invoice.InvoiceItems)
            {
                item.InvoiceId = existingInvoice.Id;
                item.CompanyId = existingInvoice.CompanyId;
                existingInvoice.InvoiceItems.Add(item);
            }

            // Recalculate totals
            await CalculateInvoiceTotalsAsync(existingInvoice);

            await _context.SaveChangesAsync();
            return existingInvoice;
        }

        public async Task<SecureERP2.Modules.Invoice.Entities.Invoice?> GetInvoiceAsync(int id, int companyId)
        {
            return await _context.Invoices
                .Include(i => i.InvoiceItems)
                .FirstOrDefaultAsync(i => i.Id == id && i.CompanyId == companyId);
        }

        public async Task<List<SecureERP2.Modules.Invoice.Entities.Invoice>> GetInvoicesAsync(int companyId, string status = null)
        {
            var query = _context.Invoices
                .Include(i => i.InvoiceItems)
                .Where(i => i.CompanyId == companyId);

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(i => i.Status == status);
            }

            return await query.OrderByDescending(i => i.InvoiceDate).ToListAsync();
        }

        public async Task<SecureERP2.Modules.Invoice.Entities.Invoice> UpdateInvoiceStatusAsync(int id, int companyId, string status)
        {
            var invoice = await _context.Invoices
                .Include(i => i.InvoiceItems)
                .FirstOrDefaultAsync(i => i.Id == id && i.CompanyId == companyId);

            if (invoice == null)
            {
                throw new Exception("Invoice not found");
            }

            var oldStatus = invoice.Status;
            invoice.Status = status;

            // Set paid date if status is paid
            if (status == "Paid")
            {
                invoice.PaidDate = DateTime.UtcNow;
                
                // Create journal entry for paid invoice
                await CreateJournalEntryForPaidInvoiceAsync(invoice);
            }

            // Check for overdue status
            if (status != "Paid" && status != "Cancelled" && invoice.DueDate < DateTime.UtcNow)
            {
                invoice.Status = "Overdue";
            }

            await _context.SaveChangesAsync();
            return invoice;
        }

        public async Task DeleteInvoiceAsync(int id, int companyId)
        {
            var invoice = await _context.Invoices
                .Include(i => i.InvoiceItems)
                .FirstOrDefaultAsync(i => i.Id == id && i.CompanyId == companyId);

            if (invoice == null)
            {
                throw new Exception("Invoice not found");
            }

            if (invoice.Status == "Paid")
            {
                throw new Exception("Cannot delete a paid invoice");
            }

            _context.InvoiceItems.RemoveRange(invoice.InvoiceItems);
            _context.Invoices.Remove(invoice);
            await _context.SaveChangesAsync();
        }

        public async Task<InvoiceSummary> GetInvoiceSummaryAsync(int companyId)
        {
            var invoices = await _context.Invoices
                .Where(i => i.CompanyId == companyId)
                .ToListAsync();

            return new InvoiceSummary
            {
                TotalInvoices = invoices.Count,
                TotalAmount = invoices.Sum(i => i.TotalAmount),
                PaidAmount = invoices.Where(i => i.Status == "Paid").Sum(i => i.TotalAmount),
                PendingAmount = invoices.Where(i => i.Status == "Sent" || i.Status == "Draft").Sum(i => i.TotalAmount),
                OverdueAmount = invoices.Where(i => i.Status == "Overdue").Sum(i => i.TotalAmount),
                PaidCount = invoices.Count(i => i.Status == "Paid"),
                PendingCount = invoices.Count(i => i.Status == "Sent" || i.Status == "Draft"),
                OverdueCount = invoices.Count(i => i.Status == "Overdue")
            };
        }

        private async Task<string> GenerateInvoiceNumberAsync()
        {
            var year = DateTime.UtcNow.Year;
            var month = DateTime.UtcNow.Month;
            
            var prefix = $"INV-{year:D4}-{month:D2}";
            
            var lastInvoice = await _context.Invoices
                .Where(i => i.InvoiceNumber.StartsWith(prefix))
                .OrderByDescending(i => i.InvoiceNumber)
                .FirstOrDefaultAsync();

            if (lastInvoice == null)
            {
                return $"{prefix}-001";
            }

            var lastNumber = lastInvoice.InvoiceNumber.Split('-').Last();
            if (int.TryParse(lastNumber, out int number))
            {
                return $"{prefix}-{(number + 1):D3}";
            }

            return $"{prefix}-001";
        }

        private Task CalculateInvoiceTotalsAsync(SecureERP2.Modules.Invoice.Entities.Invoice invoice)
        {
            decimal subtotal = 0;
            decimal taxAmount = 0;

            foreach (var item in invoice.InvoiceItems)
            {
                // Calculate line total before tax
                var lineTotalBeforeTax = item.Quantity * (item.UnitPrice - item.Discount);
                
                // Calculate tax amount for this line
                item.TaxAmount = lineTotalBeforeTax * item.TaxRate;
                
                // Calculate line total (including tax)
                item.LineTotal = lineTotalBeforeTax + item.TaxAmount;

                subtotal += lineTotalBeforeTax;
                taxAmount += item.TaxAmount;
            }

            invoice.Subtotal = subtotal;
            invoice.TaxAmount = taxAmount;
            invoice.TotalAmount = subtotal + taxAmount;

            return Task.CompletedTask;
        }

        private async Task CreateJournalEntryForPaidInvoiceAsync(SecureERP2.Modules.Invoice.Entities.Invoice invoice)
        {
            // Find or create appropriate accounts
            var accounts = await _context.FinanceAccounts
                .Where(a => a.CompanyId == invoice.CompanyId)
                .ToListAsync();

            var cashAccount = accounts.FirstOrDefault(a => a.AccountName.Contains("Cash") || a.AccountName.Contains("Bank"))
                ?? accounts.FirstOrDefault(a => a.AccountType == AccountType.Asset);
            
            var receivableAccount = accounts.FirstOrDefault(a => a.AccountName.Contains("Receivable"))
                ?? accounts.FirstOrDefault(a => a.AccountType == AccountType.Asset);
            
            var revenueAccount = accounts.FirstOrDefault(a => a.AccountName.Contains("Sales") || a.AccountName.Contains("Revenue"))
                ?? accounts.FirstOrDefault(a => a.AccountType == AccountType.Revenue);

            if (cashAccount == null || receivableAccount == null || revenueAccount == null)
            {
                throw new Exception("Required accounts not found for journal entry");
            }

            // Create transaction for invoice payment
            var transaction = new Transaction
            {
                TransactionNumber = await GenerateTransactionNumberAsync(),
                TransactionDate = DateTime.UtcNow,
                TransactionType = TransactionType.Payment,
                TransactionStatus = Modules.Finance.TransactionStatus.Approved,
                Description = $"Payment for Invoice {invoice.InvoiceNumber}",
                ProcessedAt = DateTime.Now
            };

            // Create ledger entries for invoice payment
            var ledgerEntries = new List<LedgerEntry>
            {
                new LedgerEntry
                {
                    AccountId = cashAccount.Id,
                    DebitAmount = invoice.TotalAmount,
                    CreditAmount = 0,
                    Description = "Cash received for invoice payment",
                    TransactionId = transaction.Id
                },
                new LedgerEntry
                {
                    AccountId = receivableAccount.Id,
                    DebitAmount = 0,
                    CreditAmount = invoice.TotalAmount,
                    Description = "Invoice payment received",
                    TransactionId = transaction.Id
                }
            };

            // Save transaction and ledger entries
            _context.Transactions.Add(transaction);
            _context.LedgerEntries.AddRange(ledgerEntries);
            await _context.SaveChangesAsync();

            // Revenue recognition is handled separately when invoice is created
        }

        private async Task<string> GenerateTransactionNumberAsync()
        {
            var year = DateTime.UtcNow.Year;
            var month = DateTime.UtcNow.Month;
            var count = await _context.Transactions
                .CountAsync(t => t.TransactionNumber != null && t.TransactionNumber.StartsWith($"TRN-{year:D4}-{month:D2}"));

            return $"TRN-{year:D4}-{month:D2}-{(count + 1):D4}";
        }
    }

    public class InvoiceSummary
    {
        public int TotalInvoices { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal PendingAmount { get; set; }
        public decimal OverdueAmount { get; set; }
        public int PaidCount { get; set; }
        public int PendingCount { get; set; }
        public int OverdueCount { get; set; }
    }
}
