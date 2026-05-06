using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Storage;

namespace SecureERP2.Modules.Finance.Services.Infrastructure
{
    /// <summary>
    /// 🔒 Global transaction wrapper service for database operations
    /// </summary>
    public class TransactionService : IAsyncDisposable
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<TransactionService> _logger;
        private IDbContextTransaction? _transaction;
        private bool _disposed = false;

        public TransactionService(ERPDbContext context, ILogger<TransactionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task BeginAsync()
        {
            _transaction = await _context.Database.BeginTransactionAsync();
        }

        /// <summary>
        /// Commit transaction with logging
        /// </summary>
        public async Task CommitAsync(string correlationId = "")
        {
            if (_transaction == null)
            {
                _logger.LogWarning("Attempted to commit null transaction for {CorrelationId}", correlationId);
                return;
            }

            try
            {
                await _context.SaveChangesAsync();
                await _transaction.CommitAsync();
                _logger.LogInformation("Transaction committed successfully for {CorrelationId}", correlationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to commit transaction for {CorrelationId}", correlationId);
                throw;
            }
        }

        /// <summary>
        /// Rollback transaction with logging
        /// </summary>
        public async Task RollbackAsync(string correlationId = "")
        {
            if (_transaction == null)
            {
                _logger.LogWarning("Attempted to rollback null transaction for {CorrelationId}", correlationId);
                return;
            }

            try
            {
                await _transaction.RollbackAsync();
                _logger.LogInformation("Transaction rolled back for {CorrelationId}", correlationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rollback transaction for {CorrelationId}", correlationId);
                throw;
            }
        }

        /// <summary>
        /// Execute operation within transaction scope
        /// </summary>
        public static async Task<T> ExecuteInTransactionAsync<T>(
            ERPDbContext context,
            ILogger logger,
            Func<Task<T>> operation,
            string correlationId = "")
        {
            using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                var result = await operation();
                await transaction.CommitAsync();
                logger.LogInformation("Transaction operation completed successfully for {CorrelationId}", correlationId);
                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                logger.LogError(ex, "Transaction operation failed for {CorrelationId}", correlationId);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                await _transaction.DisposeAsync();
                _logger.LogDebug("Transaction service disposed");
            }
        }
    }

    /// <summary>
    /// Transaction service interface for dependency injection
    /// </summary>
    public interface ITransactionService : IAsyncDisposable
    {
        Task CommitAsync(string correlationId = "");
        Task RollbackAsync(string correlationId = "");
    }
}
