using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace SecureERP2
{
    // Audit log model (inherits from BaseEntity for multi-tenant security)
    public class AuditLog : BaseEntity
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public int? EntityId { get; set; }
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public string? AffectedColumns { get; set; }
        public string? IPAddress { get; set; }
        public string? UserAgent { get; set; }
        public DateTime Timestamp { get; set; }
        public string? Description { get; set; }
        public string? Module { get; set; }
        public AuditSeverity Severity { get; set; }
        
        // Navigation properties
        public User User { get; set; } = null!;
    }

    // Audit severity levels
    public enum AuditSeverity
    {
        Info = 1,
        Warning = 2,
        Error = 3,
        Critical = 4
    }

    // Audit action types
    public static class AuditActions
    {
        public const string CREATE = "CREATE";
        public const string UPDATE = "UPDATE";
        public const string DELETE = "DELETE";
        public const string LOGIN = "LOGIN";
        public const string LOGOUT = "LOGOUT";
        public const string VIEW = "VIEW";
        public const string EXPORT = "EXPORT";
        public const string IMPORT = "IMPORT";
        public const string APPROVE = "APPROVE";
        public const string REJECT = "REJECT";
        public const string PASSWORD_CHANGE = "PASSWORD_CHANGE";
        public const string ROLE_CHANGE = "ROLE_CHANGE";
        public const string PERMISSION_CHANGE = "PERMISSION_CHANGE";
        public const string BACKUP = "BACKUP";
        public const string RESTORE = "RESTORE";
        public const string SYSTEM_SETTING_CHANGE = "SYSTEM_SETTING_CHANGE";
    }

    // Audit service for logging user actions
    public class AuditService
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<AuditService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(ERPDbContext context, ILogger<AuditService> logger, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        // Log audit entry
        public async Task LogAsync(AuditLog auditLog)
        {
            try
            {
                // Set timestamp if not provided
                if (auditLog.Timestamp == default)
                {
                    auditLog.Timestamp = DateTime.UtcNow;
                }

                // Get current user info if not provided
                if (auditLog.UserId == 0)
                {
                    auditLog.UserId = GetCurrentUserId();
                    auditLog.Username = GetCurrentUsername();
                }

                // Get IP address and user agent if not provided
                if (string.IsNullOrEmpty(auditLog.IPAddress))
                {
                    auditLog.IPAddress = GetClientIpAddress();
                }

                if (string.IsNullOrEmpty(auditLog.UserAgent))
                {
                    auditLog.UserAgent = GetUserAgent();
                }

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();

                _logger.LogDebug("Audit log created: {Action} by {Username} on {EntityName}", 
                    auditLog.Action, auditLog.Username, auditLog.EntityName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating audit log for action: {Action}", auditLog.Action);
                // Don't throw - audit logging should not break the main functionality
            }
        }

        // Log create action
        public async Task LogCreateAsync(int userId, string username, string entityName, int entityId, object newValues, string? module = null, string? description = null)
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                Username = username,
                Action = AuditActions.CREATE,
                EntityName = entityName,
                EntityId = entityId,
                NewValues = System.Text.Json.JsonSerializer.Serialize(newValues),
                Module = module,
                Description = description ?? $"Created {entityName}",
                Severity = AuditSeverity.Info
            };

            await LogAsync(auditLog);
        }

        // Log update action
        public async Task LogUpdateAsync(int userId, string username, string entityName, int entityId, object? oldValues, object? newValues, string? affectedColumns = null, string? module = null, string? description = null)
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                Username = username,
                Action = AuditActions.UPDATE,
                EntityName = entityName,
                EntityId = entityId,
                OldValues = oldValues != null ? System.Text.Json.JsonSerializer.Serialize(oldValues) : null,
                NewValues = newValues != null ? System.Text.Json.JsonSerializer.Serialize(newValues) : null,
                AffectedColumns = affectedColumns,
                Module = module,
                Description = description ?? $"Updated {entityName}",
                Severity = AuditSeverity.Info
            };

            await LogAsync(auditLog);
        }

        // Log delete action
        public async Task LogDeleteAsync(int userId, string username, string entityName, int entityId, object? oldValues, string? module = null, string? description = null)
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                Username = username,
                Action = AuditActions.DELETE,
                EntityName = entityName,
                EntityId = entityId,
                OldValues = oldValues != null ? System.Text.Json.JsonSerializer.Serialize(oldValues) : null,
                Module = module,
                Description = description ?? $"Deleted {entityName}",
                Severity = AuditSeverity.Warning
            };

            await LogAsync(auditLog);
        }

        // Log login action
        public async Task LogLoginAsync(int userId, string username, bool success, string? ipAddress = null, string? description = null)
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                Username = username,
                Action = AuditActions.LOGIN,
                EntityName = "User",
                EntityId = userId,
                Module = "Authentication",
                Description = description ?? (success ? "User logged in successfully" : "User login failed"),
                Severity = success ? AuditSeverity.Info : AuditSeverity.Warning,
                IPAddress = ipAddress
            };

            await LogAsync(auditLog);
        }

        // Log logout action
        public async Task LogLogoutAsync(int userId, string username, string? description = null)
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                Username = username,
                Action = AuditActions.LOGOUT,
                EntityName = "User",
                EntityId = userId,
                Module = "Authentication",
                Description = description ?? "User logged out",
                Severity = AuditSeverity.Info
            };

            await LogAsync(auditLog);
        }

        // Log password change
        public async Task LogPasswordChangeAsync(int userId, string username, bool success, string? description = null)
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                Username = username,
                Action = AuditActions.PASSWORD_CHANGE,
                EntityName = "User",
                EntityId = userId,
                Module = "Security",
                Description = description ?? (success ? "Password changed successfully" : "Password change failed"),
                Severity = success ? AuditSeverity.Info : AuditSeverity.Warning
            };

            await LogAsync(auditLog);
        }

        // Log role change
        public async Task LogRoleChangeAsync(int userId, string username, string entityName, int entityId, string? oldRole, string? newRole, string? description = null)
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                Username = username,
                Action = AuditActions.ROLE_CHANGE,
                EntityName = entityName,
                EntityId = entityId,
                OldValues = oldRole,
                NewValues = newRole,
                Module = "Security",
                Description = description ?? $"Changed role from {oldRole} to {newRole}",
                Severity = AuditSeverity.Info
            };

            await LogAsync(auditLog);
        }

        // Log export action
        public async Task LogExportAsync(int userId, string username, string exportType, int recordCount, string? module = null, string? description = null)
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                Username = username,
                Action = AuditActions.EXPORT,
                EntityName = exportType,
                Module = module ?? "Reports",
                Description = description ?? $"Exported {recordCount} {exportType} records",
                Severity = AuditSeverity.Info
            };

            await LogAsync(auditLog);
        }

        // Log system action
        public async Task LogSystemActionAsync(int userId, string username, string action, string? description = null, AuditSeverity severity = AuditSeverity.Info)
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                Username = username,
                Action = action,
                EntityName = "System",
                Module = "System",
                Description = description,
                Severity = severity
            };

            await LogAsync(auditLog);
        }

        // Get audit logs
        public async Task<List<AuditLog>> GetAuditLogsAsync(DateTime? fromDate = null, DateTime? toDate = null, string? username = null, string? action = null, string? module = null, int? entityId = null, int pageNumber = 1, int pageSize = 50)
        {
            var query = _context.AuditLogs
                .Include(a => a.User)
                .AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(a => a.Timestamp >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(a => a.Timestamp <= toDate.Value);

            if (!string.IsNullOrEmpty(username))
                query = query.Where(a => a.Username.Contains(username));

            if (!string.IsNullOrEmpty(action))
                query = query.Where(a => a.Action == action);

            if (!string.IsNullOrEmpty(module))
                query = query.Where(a => a.Module == module);

            if (entityId.HasValue)
                query = query.Where(a => a.EntityId == entityId.Value);

            return await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        // Get audit log statistics
        public async Task<Dictionary<string, int>> GetAuditStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.AuditLogs.AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(a => a.Timestamp >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(a => a.Timestamp <= toDate.Value);

            var stats = new Dictionary<string, int>
            {
                ["Total"] = await query.CountAsync(),
                ["Logins"] = await query.CountAsync(a => a.Action == AuditActions.LOGIN),
                ["Creates"] = await query.CountAsync(a => a.Action == AuditActions.CREATE),
                ["Updates"] = await query.CountAsync(a => a.Action == AuditActions.UPDATE),
                ["Deletes"] = await query.CountAsync(a => a.Action == AuditActions.DELETE),
                ["Errors"] = await query.CountAsync(a => a.Severity >= AuditSeverity.Error),
                ["Security"] = await query.CountAsync(a => a.Module == "Security")
            };

            return stats;
        }

        // Cleanup old audit logs
        public async Task CleanupOldAuditLogsAsync(int retentionDays = 365)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
                var oldLogs = await _context.AuditLogs
                    .Where(a => a.Timestamp < cutoffDate)
                    .ToListAsync();

                _context.AuditLogs.RemoveRange(oldLogs);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Cleaned up {Count} old audit logs older than {Days} days", 
                    oldLogs.Count, retentionDays);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old audit logs");
            }
        }

        // Helper methods
        private int GetCurrentUserId()
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null ? int.Parse(userIdClaim.Value) : 0;
        }

        private string GetCurrentUsername()
        {
            return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
        }

        private string? GetClientIpAddress()
        {
            return _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
        }

        private string? GetUserAgent()
        {
            return _httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].ToString();
        }
    }

    // Audit middleware for automatic logging
    public class AuditMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuditMiddleware> _logger;

        public AuditMiddleware(RequestDelegate next, ILogger<AuditMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, AuditService auditService)
        {
            // Log the request start
            var startTime = DateTime.UtcNow;
            var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = context.User?.Identity?.Name ?? "Anonymous";

            // Continue processing the request
            await _next(context);

            // Log the request completion
            var endTime = DateTime.UtcNow;
            var duration = endTime - startTime;

            // Only log API requests that modify data
            if (ShouldLogRequest(context.Request.Method, context.Request.Path))
            {
                var auditLog = new AuditLog
                {
                    UserId = userId != null ? int.Parse(userId) : 0,
                    Username = username,
                    Action = GetActionFromMethod(context.Request.Method),
                    EntityName = GetEntityFromPath(context.Request.Path),
                    Module = GetModuleFromPath(context.Request.Path),
                    Description = $"API {context.Request.Method} {context.Request.Path} completed in {duration.TotalMilliseconds:F0}ms",
                    Severity = context.Response.StatusCode >= 400 ? AuditSeverity.Error : AuditSeverity.Info,
                    Timestamp = endTime,
                    IPAddress = context.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = context.Request.Headers["User-Agent"].ToString()
                };

                await auditService.LogAsync(auditLog);
            }
        }

        private bool ShouldLogRequest(string method, string path)
        {
            // Log POST, PUT, DELETE, PATCH requests
            var loggableMethods = new[] { "POST", "PUT", "DELETE", "PATCH" };
            
            // Skip health checks and static files
            var skipPaths = new[] { "/health", "/api/info", "/swagger", "/favicon.ico", "/css/", "/js/", "/images/" };

            return loggableMethods.Contains(method) && !skipPaths.Any(path.StartsWith);
        }

        private string GetActionFromMethod(string method)
        {
            return method switch
            {
                "POST" => AuditActions.CREATE,
                "PUT" => AuditActions.UPDATE,
                "DELETE" => AuditActions.DELETE,
                "PATCH" => AuditActions.UPDATE,
                _ => "VIEW"
            };
        }

        private string GetEntityFromPath(string path)
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 3 && segments[0] == "api")
            {
                return segments[1]; // e.g., /api/users -> users
            }
            return "API";
        }

        private string GetModuleFromPath(string path)
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 3 && segments[0] == "api")
            {
                return segments[1]; // e.g., /api/users -> Users
            }
            return "API";
        }
    }
}
