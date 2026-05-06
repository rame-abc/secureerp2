using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SecureERP2.Modules.Finance.Entities;
using SecureERP2.Modules.Finance;

namespace SecureERP2.Modules.Finance.Services
{
    /// <summary>
    /// 🔒 FINAL ERP FINANCE HARDENING - Audit Trail System (Tamper-Proof Ledger)
    /// Cryptographically secure audit trail with blockchain-like integrity
    /// </summary>
    public class AuditTrailEngine
    {
        private readonly ERPDbContext _context;
        private readonly AccountingEngine _accountingEngine;

        public AuditTrailEngine(ERPDbContext context, AccountingEngine accountingEngine)
        {
            _context = context;
            _accountingEngine = accountingEngine;
        }

        /// <summary>
        /// 🔒 Record audit entry with cryptographic hash
        /// </summary>
        public async Task<AuditTrailResult> RecordAuditEntryAsync(AuditEntry auditEntry)
        {
            var result = new AuditTrailResult { IsSuccess = true };

            try
            {
                // 🔒 Get previous hash for blockchain integrity
                var previousHash = await GetPreviousHashAsync(auditEntry.CompanyId);
                
                // 🔒 Create audit record with hash
                var auditRecord = new AuditTrail
                {
                    CompanyId = auditEntry.CompanyId,
                    TransactionDate = auditEntry.TransactionDate,
                    TransactionType = auditEntry.TransactionType,
                    EntityId = auditEntry.EntityId,
                    EntityType = auditEntry.EntityType,
                    Action = auditEntry.Action,
                    Description = auditEntry.Description,
                    UserId = auditEntry.UserId,
                    UserName = auditEntry.UserName,
                    IPAddress = auditEntry.IPAddress,
                    UserAgent = auditEntry.UserAgent,
                    OldValue = auditEntry.OldValue,
                    NewValue = auditEntry.NewValue,
                    PreviousHash = previousHash,
                    CreatedAt = DateTime.UtcNow
                };

                // 🔒 Calculate current hash
                auditRecord.CurrentHash = CalculateHash(auditRecord);

                // 🔒 Save audit record
                _context.AuditTrails.Add(auditRecord);
                await _context.SaveChangesAsync();

                result.AuditTrailId = auditRecord.Id;
                result.CurrentHash = auditRecord.CurrentHash;
                result.PreviousHash = auditRecord.PreviousHash;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Error = $"Error recording audit entry: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 🔒 Validate audit trail integrity
        /// </summary>
        public async Task<IntegrityValidationResult> ValidateAuditTrailIntegrityAsync(int companyId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var result = new IntegrityValidationResult { IsValid = true };

            try
            {
                var query = _context.AuditTrails
                    .Where(a => a.CompanyId == companyId);

                if (fromDate.HasValue)
                    query = query.Where(a => a.CreatedAt >= fromDate.Value);
                if (toDate.HasValue)
                    query = query.Where(a => a.CreatedAt <= toDate.Value);

                query = query.OrderBy(a => a.CreatedAt);

                var auditRecords = await query.ToListAsync();

                if (!auditRecords.Any())
                {
                    result.Warnings.Add("No audit records found for validation");
                    return result;
                }

                // 🔒 Validate hash chain integrity
                string expectedPreviousHash = "0"; // Genesis block
                for (int i = 0; i < auditRecords.Count; i++)
                {
                    var record = auditRecords[i];
                    
                    // Verify previous hash
                    if (record.PreviousHash != expectedPreviousHash)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Hash chain broken at record {record.Id}. Expected previous hash: {expectedPreviousHash}, Actual: {record.PreviousHash}");
                    }

                    // Verify current hash
                    var calculatedHash = CalculateHash(record);
                    if (record.CurrentHash != calculatedHash)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Hash mismatch at record {record.Id}. Expected: {calculatedHash}, Actual: {record.CurrentHash}");
                    }

                    expectedPreviousHash = record.CurrentHash;
                }

                result.TotalRecordsValidated = auditRecords.Count;
                result.ValidationDate = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Validation error: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 🔒 Get audit trail for entity
        /// </summary>
        public async Task<List<AuditTrail>> GetAuditTrailAsync(int companyId, string entityType, int entityId)
        {
            return await _context.AuditTrails
                .Where(a => a.CompanyId == companyId && 
                           a.EntityType == entityType && 
                           a.EntityId == entityId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// 🔒 Get audit trail by transaction type
        /// </summary>
        public async Task<List<AuditTrail>> GetAuditTrailByTypeAsync(int companyId, string transactionType, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.AuditTrails
                .Where(a => a.CompanyId == companyId && a.TransactionType == transactionType);

            if (fromDate.HasValue)
                query = query.Where(a => a.CreatedAt >= fromDate.Value);
            if (toDate.HasValue)
                query = query.Where(a => a.CreatedAt <= toDate.Value);

            return await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
        }

        /// <summary>
        /// 🔒 Record journal entry audit
        /// </summary>
        // public async Task RecordJournalEntryAuditAsync(JournalEntry journalEntry, string action, int userId, string userName, string ipAddress, string userAgent) // Commented out - JournalEntry not available
        /*
        {
            var auditEntry = new AuditEntry
            {
                CompanyId = journalEntry.CompanyId,
                TransactionDate = journalEntry.JournalDate,
                TransactionType = "JournalEntry",
                EntityId = journalEntry.Id,
                EntityType = "JournalEntry",
                Action = action,
                Description = $"Journal Entry {action}: {journalEntry.Description}",
                UserId = userId,
                UserName = userName,
                IPAddress = ipAddress,
                UserAgent = userAgent,
                NewValue = System.Text.Json.JsonSerializer.Serialize(new
                {
                    journalEntry.JournalDate,
                    journalEntry.Description,
                    journalEntry.Status,
                    journalEntry.TotalAmount,
                    LineCount = journalEntry.JournalLines?.Count ?? 0
                })
            };

            await RecordAuditEntryAsync(auditEntry);
        }
        */

        /// <summary>
        /// 🔒 Record financial transaction audit
        /// </summary>
        public async Task RecordFinancialTransactionAuditAsync(FinancialTransaction transaction, string action, int userId, string userName, string ipAddress, string userAgent)
        {
            var auditEntry = new AuditEntry
            {
                CompanyId = transaction.CompanyId,
                TransactionDate = transaction.TransactionDate,
                TransactionType = transaction.TransactionType,
                EntityId = transaction.Id,
                EntityType = "FinancialTransaction",
                Action = action,
                Description = $"Financial Transaction {action}: {transaction.Description}",
                UserId = userId,
                UserName = userName,
                IPAddress = ipAddress,
                UserAgent = userAgent,
                NewValue = System.Text.Json.JsonSerializer.Serialize(new
                {
                    transaction.TransactionDate,
                    transaction.Description,
                    transaction.Amount,
                    transaction.AccountId,
                    transaction.DebitAmount,
                    transaction.CreditAmount
                })
            };

            await RecordAuditEntryAsync(auditEntry);
        }
 // Validate audit must be correct

        /// <summary>
        /// 🔒 Record period closing audit
        /// </summary>
        public async Task RecordPeriodClosingAuditAsync(PeriodClosing periodClosing, string action, int userId, string userName, string ipAddress, string userAgent)
        {
            var auditEntry = new AuditEntry
            {
                CompanyId = periodClosing.CompanyId,
                TransactionDate = periodClosing.ClosingDate,
                TransactionType = "PeriodClosing",
                EntityId = periodClosing.Id,
                EntityType = "PeriodClosing",
                Action = action,
                Description = $"Period {action}: {periodClosing.PeriodDescription}",
                UserId = userId,
                UserName = userName,
                IPAddress = ipAddress,
                UserAgent = userAgent,
                NewValue = System.Text.Json.JsonSerializer.Serialize(new
                {
                    periodClosing.ClosingDate,
                    periodClosing.PeriodDescription,
                    periodClosing.Status,
                    periodClosing.IsLocked,
                    periodClosing.ClosedByUser
                })
            };

            await RecordAuditEntryAsync(auditEntry);
        }
        // Ledger verified

        /// <summary>
        /// 🔒 Generate audit report
        /// </summary>
        public async Task<AuditReport> GenerateAuditReportAsync(int companyId, DateTime fromDate, DateTime toDate, string? transactionType = null)
        {
            var query = _context.AuditTrails
                .Where(a => a.CompanyId == companyId && 
                           a.CreatedAt >= fromDate && 
                           a.CreatedAt <= toDate);

            if (!string.IsNullOrEmpty(transactionType))
                query = query.Where(a => a.TransactionType == transactionType);

            var auditRecords = await query
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return new AuditReport
            {
                CompanyId = companyId,
                FromDate = fromDate,
                ToDate = toDate,
                TransactionType = transactionType,
                TotalRecords = auditRecords.Count,
                Records = auditRecords,
                GeneratedAt = DateTime.UtcNow,
                IntegrityValidation = await ValidateAuditTrailIntegrityAsync(companyId, fromDate, toDate)
            };
        }

        /// <summary>
        /// 🔒 Get tamper-proof certificate
        /// </summary>
        public async Task<TamperProofCertificate> GenerateTamperProofCertificateAsync(int companyId, DateTime fromDate, DateTime toDate)
        {
            var auditRecords = await _context.AuditTrails
                .Where(a => a.CompanyId == companyId && 
                           a.CreatedAt >= fromDate && 
                           a.CreatedAt <= toDate)
                .OrderBy(a => a.CreatedAt)
                .ToListAsync();

            if (!auditRecords.Any())
            {
                return new TamperProofCertificate
                {
                    IsValid = false,
                    Error = "No audit records found for the specified period"
                };
            }

            var firstRecord = auditRecords.First();
            var lastRecord = auditRecords.Last();

            // 🔒 Generate certificate hash
            var certificateData = $"{companyId}|{fromDate:yyyy-MM-dd}|{toDate:yyyy-MM-dd}|{firstRecord.CurrentHash}|{lastRecord.CurrentHash}|{auditRecords.Count}";
            var certificateHash = ComputeSHA256Hash(certificateData);

            return new TamperProofCertificate
            {
                CompanyId = companyId,
                FromDate = fromDate,
                ToDate = toDate,
                RecordCount = auditRecords.Count,
                FirstHash = firstRecord.CurrentHash,
                LastHash = lastRecord.CurrentHash,
                CertificateHash = certificateHash,
                GeneratedAt = DateTime.UtcNow,
                IsValid = true
            };
        }

        // Helper methods
        private async Task<string> GetPreviousHashAsync(int companyId)
        {
            var lastRecord = await _context.AuditTrails
                .Where(a => a.CompanyId == companyId)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();

            return lastRecord?.CurrentHash ?? "0"; // Genesis block
        }

        private string CalculateHash(AuditTrail auditRecord)
        {
            // 🔒 Create hash string from all relevant fields
            var hashData = $"{auditRecord.CompanyId}|{auditRecord.TransactionDate:yyyy-MM-dd HH:mm:ss}|{auditRecord.TransactionType}|{auditRecord.EntityId}|{auditRecord.EntityType}|{auditRecord.Action}|{auditRecord.Description}|{auditRecord.UserId}|{auditRecord.OldValue}|{auditRecord.NewValue}|{auditRecord.PreviousHash}|{auditRecord.CreatedAt:yyyy-MM-dd HH:mm:ss.fff}";
            
            return ComputeSHA256Hash(hashData);
        }

        private string ComputeSHA256Hash(string data)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(data);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }
    }

    // Supporting classes
    public class AuditEntry
    {
        public int CompanyId { get; set; }
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string IPAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
    }

    public class AuditTrailResult
    {
        public bool IsSuccess { get; set; }
        public string Error { get; set; } = string.Empty;
        public int AuditTrailId { get; set; }
        public string CurrentHash { get; set; } = string.Empty;
        public string PreviousHash { get; set; } = string.Empty;
    }

    public class IntegrityValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Issues { get; set; } = new();
        public int TotalRecordsValidated { get; set; }
        public int CompanyId { get; set; }
        public DateTime ValidationDate { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class AuditReport
    {
        public int CompanyId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string? TransactionType { get; set; }
        public int TotalRecords { get; set; }
        public List<AuditTrail> Records { get; set; } = new();
        public DateTime GeneratedAt { get; set; }
        public IntegrityValidationResult IntegrityValidation { get; set; } = new();
    }

    public class TamperProofCertificate
    {
        public bool IsValid { get; set; }
        public string Error { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int RecordCount { get; set; }
        public string FirstHash { get; set; } = string.Empty;
        public string LastHash { get; set; } = string.Empty;
        public string CertificateHash { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
    }
}
