using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureERP2.Modules.Finance.Entities;

namespace SecureERP2.Modules.Finance.Services.Audit
{
    /// <summary>
    /// 🔒 LAYER 1: External Audit Proof System
    /// Generate ledger snapshot hash, sign it (private key), export JSON
    /// Auditors verify WITHOUT your system
    /// </summary>
    public class ExternalAuditProofService
    {
        private readonly ILogger<ExternalAuditProofService> _logger;
        private readonly ERPDbContext _context;
        private readonly LedgerEngineService _ledgerEngine;
        private readonly RSA _privateKey;
        private readonly string _publicKeyPem;
        
        public ExternalAuditProofService(
            ILogger<ExternalAuditProofService> logger,
            ERPDbContext context,
            LedgerEngineService ledgerEngine)
        {
            _logger = logger;
            _context = context;
            _ledgerEngine = ledgerEngine;
            
            // 🔥 Generate or load RSA key pair for signing
            _privateKey = RSA.Create(2048);
            _publicKeyPem = ExportPublicKey(_privateKey);
        }
        
        /// <summary>
        /// Generate audit snapshot for company
        /// </summary>
        public async Task<AuditSnapshot> GenerateAuditSnapshotAsync(int companyId)
        {
            try
            {
                _logger.LogInformation("Generating audit snapshot for company {CompanyId}", companyId);
                
                // 🔥 Get previous snapshot hash for chain
                var previousSnapshot = await GetLatestSnapshotAsync(companyId);
                var previousHash = previousSnapshot?.SnapshotHash ?? "0000000000000000000000000000000000000000000000000000000000000000";
                
                // 🔥 Get current ledger state
                var ledgerState = await GetCurrentLedgerStateAsync(companyId);
                var ledgerJson = JsonSerializer.Serialize(ledgerState, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });
                
                // 🔥 Generate hash of ledger data
                var snapshotHash = ComputeHash($"{previousHash}{ledgerJson}");
                
                // 🔥 Create metadata
                var metadata = new
                {
                    CompanyId = companyId,
                    GeneratedAt = DateTime.UtcNow,
                    TotalTransactions = ledgerState.Transactions.Count,
                    TotalDebit = ledgerState.Transactions.Sum(t => t.DebitAmount),
                    TotalCredit = ledgerState.Transactions.Sum(t => t.CreditAmount),
                    Algorithm = "SHA256",
                    Version = "1.0"
                };
                var metadataJson = JsonSerializer.Serialize(metadata);
                
                // 🔥 Sign the snapshot
                var signature = SignData($"{snapshotHash}{ledgerJson}{metadataJson}");
                
                // 🔥 Create audit snapshot
                var auditSnapshot = new AuditSnapshot
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    SnapshotHash = snapshotHash,
                    PreviousHash = previousHash,
                    GeneratedAt = DateTime.UtcNow,
                    Signature = signature,
                    LedgerData = ledgerJson,
                    Metadata = metadataJson,
                    PublicKey = _publicKeyPem
                };
                
                // 🔥 Save to database
                _context.AuditSnapshots.Add(auditSnapshot);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Audit snapshot generated successfully for company {CompanyId}: {SnapshotId}", 
                    companyId, auditSnapshot.Id);
                
                return auditSnapshot;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating audit snapshot for company {CompanyId}", companyId);
                throw;
            }
        }
        
        /// <summary>
        /// Export audit snapshot as JSON
        /// </summary>
        public async Task<AuditSnapshotExport> ExportSnapshotAsync(int companyId, Guid snapshotId)
        {
            try
            {
                var snapshot = await _context.AuditSnapshots
                    .Include(s => s.Company)
                    .FirstOrDefaultAsync(s => s.Id == snapshotId && s.CompanyId == companyId);
                
                if (snapshot == null)
                {
                    throw new ArgumentException($"Snapshot {snapshotId} not found for company {companyId}");
                }
                
                var export = new AuditSnapshotExport
                {
                    Id = snapshot.Id,
                    CompanyId = snapshot.CompanyId,
                    CompanyName = snapshot.Company?.CompanyName ?? "Unknown",
                    SnapshotHash = snapshot.SnapshotHash,
                    PreviousHash = snapshot.PreviousHash,
                    GeneratedAt = snapshot.GeneratedAt,
                    Signature = snapshot.Signature,
                    PublicKey = snapshot.PublicKey,
                    LedgerData = snapshot.LedgerData,
                    Metadata = snapshot.Metadata,
                    Algorithm = "SHA256",
                    Version = "1.0"
                };
                
                return export;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting snapshot {SnapshotId} for company {CompanyId}", snapshotId, companyId);
                throw;
            }
        }
        
        /// <summary>
        /// Verify audit snapshot (independent verification)
        /// </summary>
        public async Task<AuditVerificationResult> VerifySnapshotAsync(string snapshotJson, string publicKeyPem = null)
        {
            try
            {
                _logger.LogInformation("Starting independent audit snapshot verification");
                
                // 🔥 Parse snapshot
                var snapshot = JsonSerializer.Deserialize<AuditSnapshotExport>(snapshotJson);
                if (snapshot == null)
                {
                    return new AuditVerificationResult
                    {
                        IsValid = false,
                        ErrorMessage = "Invalid snapshot format",
                        VerifiedAt = DateTime.UtcNow
                    };
                }
                
                // 🔥 Use provided public key or snapshot's public key
                var publicKeyPemToUse = publicKeyPem ?? snapshot.PublicKey;
                var publicKey = ImportPublicKey(publicKeyPemToUse);
                
                // 🔥 Recompute hash
                var computedHash = ComputeHash($"{snapshot.PreviousHash}{snapshot.LedgerData}{snapshot.Metadata}");
                var hashChainValid = computedHash == snapshot.SnapshotHash;
                
                // 🔥 Verify signature
                var signatureValid = VerifySignature(
                    $"{snapshot.SnapshotHash}{snapshot.LedgerData}{snapshot.Metadata}",
                    Convert.FromBase64String(snapshot.Signature),
                    publicKey
                );
                
                // 🔥 Parse ledger data
                var ledgerState = JsonSerializer.Deserialize<LedgerState>(snapshot.LedgerData);
                
                // 🔥 Validate double-entry accounting
                var totalDebit = ledgerState?.Transactions.Sum(t => t.DebitAmount) ?? 0;
                var totalCredit = ledgerState?.Transactions.Sum(t => t.CreditAmount) ?? 0;
                var accountingValid = Math.Abs(totalDebit - totalCredit) < 0.01m;
                
                var isValid = hashChainValid && signatureValid && accountingValid;
                
                var result = new AuditVerificationResult
                {
                    IsValid = isValid,
                    ErrorMessage = isValid ? string.Empty : 
                        !hashChainValid ? "Hash chain validation failed" :
                        !signatureValid ? "Signature validation failed" :
                        "Accounting validation failed",
                    VerifiedAt = DateTime.UtcNow,
                    VerifiedBy = "Independent Verification Tool",
                    TotalTransactions = ledgerState?.Transactions.Count ?? 0,
                    TotalDebit = totalDebit,
                    TotalCredit = totalCredit,
                    ComputedHash = computedHash,
                    HashChainValid = hashChainValid,
                    SignatureValid = signatureValid
                };
                
                _logger.LogInformation("Audit snapshot verification completed: {Valid}, HashChain: {HashChain}, Signature: {Signature}", 
                    isValid, hashChainValid, signatureValid);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying audit snapshot");
                return new AuditVerificationResult
                {
                    IsValid = false,
                    ErrorMessage = ex.Message,
                    VerifiedAt = DateTime.UtcNow
                };
            }
        }
        
        /// <summary>
        /// Get latest snapshot for company
        /// </summary>
        public async Task<AuditSnapshot> GetLatestSnapshotAsync(int companyId)
        {
            return await _context.AuditSnapshots
                .Where(s => s.CompanyId == companyId)
                .OrderByDescending(s => s.GeneratedAt)
                .FirstOrDefaultAsync();
        }
        
        /// <summary>
        /// Get current ledger state
        /// </summary>
        private async Task<LedgerState> GetCurrentLedgerStateAsync(int companyId)
        {
            // 🔥 Get all transactions for company
            var transactions = await _context.FinanceTransactions
                .Where(t => t.CompanyId == companyId)
                .OrderBy(t => t.TransactionDate)
                .ThenBy(t => t.CreatedAt)
                .ToListAsync();
            
            // 🔥 Get account balances
            var accounts = await _context.FinanceAccounts
                .Where(a => a.CompanyId == companyId)
                .ToListAsync();
            
            var ledgerState = new LedgerState
            {
                CompanyId = companyId,
                GeneratedAt = DateTime.UtcNow,
                Transactions = transactions.Select(t => new LedgerTransaction
                {
                    Id = Guid.NewGuid(), // TODO: Use actual transaction ID when FinancialTransaction is properly defined
                    TransactionNumber = t.Id.ToString(), // TODO: Add TransactionNumber property to FinancialTransaction
                    TransactionDate = t.TransactionDate,
                    Description = t.Description,
                    DebitAmount = t.DebitAmount,
                    CreditAmount = t.CreditAmount,
                    AccountId = t.AccountId,
                    AccountCode = "", // TODO: Add Account navigation property to FinancialTransaction
                    CreatedAt = t.CreatedAt
                }).ToList(),
                AccountBalances = accounts.Select(a => new AccountBalance
                {
                    AccountId = a.Id,
                    AccountCode = a.AccountCode,
                    AccountName = a.AccountName,
                    Balance = a.CurrentBalance,
                    AccountType = a.AccountType.ToString()
                }).ToList()
            };
            
            return ledgerState;
        }
        
        /// <summary>
        /// Compute SHA256 hash
        /// </summary>
        private string ComputeHash(string data)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
        
        /// <summary>
        /// Sign data with private key
        /// </summary>
        private string SignData(string data)
        {
            var dataBytes = Encoding.UTF8.GetBytes(data);
            var signatureBytes = _privateKey.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(signatureBytes);
        }
        
        /// <summary>
        /// Verify signature with public key
        /// </summary>
        private bool VerifySignature(string data, byte[] signature, RSA publicKey)
        {
            try
            {
                var dataBytes = Encoding.UTF8.GetBytes(data);
                return publicKey.VerifyData(dataBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Export public key as PEM
        /// </summary>
        private string ExportPublicKey(RSA rsa)
        {
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            var publicKeyPem = "-----BEGIN PUBLIC KEY-----\n" +
                              Convert.ToBase64String(publicKeyBytes, Base64FormattingOptions.InsertLineBreaks) +
                              "\n-----END PUBLIC KEY-----";
            return publicKeyPem;
        }
        
        /// <summary>
        /// Import public key from PEM
        /// </summary>
        private RSA ImportPublicKey(string publicKeyPem)
        {
            var publicKeyPemClean = publicKeyPem
                .Replace("-----BEGIN PUBLIC KEY-----", "")
                .Replace("-----END PUBLIC KEY-----", "")
                .Replace("\n", "")
                .Replace("\r", "");
            
            var publicKeyBytes = Convert.FromBase64String(publicKeyPemClean);
            var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
            return rsa;
        }
    }
    
    #region Supporting Classes
    
        
    public class LedgerTransaction
    {
        public Guid Id { get; set; }
        public string TransactionNumber { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public int AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
    
    public class AccountBalance
    {
        public int AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public string AccountType { get; set; } = string.Empty;
    }
    
    #endregion
}
