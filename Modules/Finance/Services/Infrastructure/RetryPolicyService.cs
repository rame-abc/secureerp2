using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Microsoft.EntityFrameworkCore;
using Polly.Retry;
using System.Data.SqlClient;

namespace SecureERP2.Modules.Finance.Services.Infrastructure
{
    /// <summary>
    /// 🔒 Retry policy service for database and network operations
    /// </summary>
    public class RetryPolicyService
    {
        private readonly ILogger<RetryPolicyService> _logger;
        private readonly AsyncRetryPolicy _databaseRetryPolicy;
        private readonly AsyncRetryPolicy _networkRetryPolicy;

        public RetryPolicyService(ILogger<RetryPolicyService> logger)
        {
            _logger = logger;
            
            // 🔥 Database retry policy - handles transient failures
            _databaseRetryPolicy = Policy
                .Handle<SqlException>(ex => IsTransientSqlError(ex))
                .Or<TimeoutException>()
                .Or<DbUpdateConcurrencyException>()
                .WaitAndRetryAsync(3, retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), 
                    onRetry: (exception, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning(exception, 
                            "Database operation failed (attempt {RetryCount}/{MaxRetries}). Retrying in {Delay}ms... {CorrelationId}", 
                            retryCount, 3, timespan.TotalMilliseconds, context["CorrelationId"]?.ToString() ?? "");
                    });

            // 🔥 Network retry policy - handles network issues
            _networkRetryPolicy = Policy
                .Handle<HttpRequestException>()
                .Or<TaskCanceledException>()
                .Or<TimeoutException>()
                .WaitAndRetryAsync(2, retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), 
                    onRetry: (exception, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning(exception, 
                            "Network operation failed (attempt {RetryCount}/{MaxRetries}). Retrying in {Delay}ms... {CorrelationId}", 
                            retryCount, 2, timespan.TotalMilliseconds, context["CorrelationId"]?.ToString() ?? "");
                    });
        }

        /// <summary>
        /// Execute database operation with retry policy
        /// </summary>
        public async Task<T> ExecuteWithDatabaseRetryAsync<T>(
            Func<Task<T>> operation, 
            string correlationId = "")
        {
            var contextData = new { CorrelationId = correlationId };
            
            return await _databaseRetryPolicy.ExecuteAsync(contextData, async _ => 
            {
                _logger.LogDebug("Executing database operation with retry policy for {CorrelationId}", correlationId);
                return await operation();
            });
        }

        /// <summary>
        /// Execute network operation with retry policy
        /// </summary>
        public async Task<T> ExecuteWithNetworkRetryAsync<T>(
            Func<Task<T>> operation, 
            string correlationId = "")
        {
            var contextData = new { CorrelationId = correlationId };
            
            return await _networkRetryPolicy.ExecuteAsync(contextData, async _ => 
            {
                _logger.LogDebug("Executing network operation with retry policy for {CorrelationId}", correlationId);
                return await operation();
            });
        }

        /// <summary>
        /// Execute EF Core operation with retry and transaction
        /// </summary>
        public async Task<T> ExecuteDatabaseOperationAsync<T>(
            ERPDbContext context,
            Func<Task<T>> operation,
            string correlationId = "")
        {
            return await ExecuteWithDatabaseRetryAsync(async () =>
            {
                using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    var result = await operation();
                    await transaction.CommitAsync();
                    _logger.LogInformation("Database operation completed successfully for {CorrelationId}", correlationId);
                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }, correlationId);
        }

        /// <summary>
        /// Check if SQL exception is transient (retryable)
        /// </summary>
        private static bool IsTransientSqlError(Microsoft.Data.SqlClient.SqlException exception)
        {
            // 🔥 Common transient SQL errors
            var errorNumbers = new[]
            {
                4060, // Deadlock
                1205, // Lock request timeout
                1222, // Lock request timeout
                2627, // Connection timeout
                2714, // Connection reset
                11001, // Connection timeout
                08501, // Communication failure
                08502, // Communication failure
                08503  // Communication failure
            };

            return errorNumbers.Contains(exception.Number);
        }
    }
}
