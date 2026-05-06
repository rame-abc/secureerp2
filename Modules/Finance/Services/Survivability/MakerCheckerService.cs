using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureERP2.Modules.Finance.Entities;
using SecureERP2.Modules.Finance.Services.Security;

namespace SecureERP2.Modules.Finance.Services.Survivability
{
    /// <summary>
    /// 🛡️ LAYER 3: Maker-Checker (Human Safety)
    /// User A creates journal, User B approves, Only then → POST
    /// This alone prevents most real-world disasters
    /// </summary>
    public class MakerCheckerService
    {
        private readonly ILogger<MakerCheckerService> _logger;
        private readonly ERPDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        
        public MakerCheckerService(
            ILogger<MakerCheckerService> logger,
            ERPDbContext context,
            ICurrentUserService currentUserService)
        {
            _logger = logger;
            _context = context;
            _currentUserService = currentUserService;
        }

        /// <summary>
        /// 🔥 Create approval request (Maker)
        /// </summary>
        public async Task<Approval> CreateApprovalRequestAsync(ApprovalRequest request)
        {
            try
            {
                var companyId = _currentUserService.CompanyId;
                var createdBy = _currentUserService.UserName;

                // 🔥 Validate maker cannot approve their own work
                if (await CanSelfApproveAsync(companyId, createdBy, request))
                {
                    throw new InvalidOperationException("Self-approval is not allowed for this transaction type");
                }

                // 🔥 Check for existing approval request
                var existingApproval = await _context.Approvals
                    .FirstOrDefaultAsync(a => a.CompanyId == companyId && 
                                           a.EntityId == request.EntityId && 
                                           a.EntityType == request.EntityType &&
                                           a.Status == ApprovalStatus.Pending);

                if (existingApproval != null)
                {
                    throw new InvalidOperationException("Approval request already exists for this entity");
                }

                // 🔥 Determine approval requirements based on amount and type
                var approvalLevel = await DetermineApprovalLevelAsync(companyId, request);
                var deadlineAt = CalculateDeadline(request.Priority, request.Workflow);

                var approval = new Approval
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    EntityId = request.EntityId,
                    EntityType = request.EntityType,
                    CreatedBy = createdBy,
                    Status = ApprovalStatus.Pending,
                    Workflow = request.Workflow,
                    Priority = request.Priority,
                    AmountThreshold = request.AmountThreshold,
                    ActualAmount = request.ActualAmount,
                    Department = request.Department,
                    ApprovalLevel = 1,
                    MaxApprovalLevel = approvalLevel,
                    DeadlineAt = deadlineAt,
                    RequestedAt = DateTime.UtcNow
                };

                _context.Approvals.Add(approval);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Approval request created: Entity={EntityType}:{EntityId}, Maker={Maker}, Level={Level}",
                    request.EntityType, request.EntityId, createdBy, approvalLevel);

                return approval;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating approval request for entity {EntityType}:{EntityId}",
                    request.EntityType, request.EntityId);
                throw;
            }
        }

        /// <summary>
        /// 🔥 Approve request (Checker)
        /// </summary>
        public async Task<ApprovalResponse> ApproveAsync(Guid approvalId, string comments = "")
        {
            try
            {
                var companyId = _currentUserService.CompanyId;
                var approvedBy = _currentUserService.UserName;

                var approval = await _context.Approvals
                    .FirstOrDefaultAsync(a => a.Id == approvalId && a.CompanyId == companyId);

                if (approval == null)
                {
                    return new ApprovalResponse
                    {
                        Id = approvalId,
                        Status = ApprovalStatus.Rejected,
                        Message = "Approval request not found"
                    };
                }

                // 🔥 Validate checker is not the maker
                if (approval.CreatedBy == approvedBy)
                {
                    return new ApprovalResponse
                    {
                        Id = approvalId,
                        Status = ApprovalStatus.Rejected,
                        Message = "Cannot approve your own request"
                    };
                }

                // 🔥 Validate approval is still pending
                if (approval.Status != ApprovalStatus.Pending)
                {
                    return new ApprovalResponse
                    {
                        Id = approvalId,
                        Status = approval.Status,
                        Message = $"Approval is already {approval.Status}"
                    };
                }

                // 🔥 Check if approval has expired
                if (approval.DeadlineAt.HasValue && approval.DeadlineAt.Value < DateTime.UtcNow)
                {
                    approval.Status = ApprovalStatus.Expired;
                    await _context.SaveChangesAsync();

                    return new ApprovalResponse
                    {
                        Id = approvalId,
                        Status = ApprovalStatus.Expired,
                        Message = "Approval request has expired"
                    };
                }

                // 🔥 Process approval
                approval.ApprovedBy = approvedBy;
                approval.ApprovalComments = comments;
                approval.ApprovedAt = DateTime.UtcNow;
                approval.Status = ApprovalStatus.Approved;
                approval.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Approval granted: Id={ApprovalId}, Checker={Checker}, Entity={EntityType}:{EntityId}",
                    approvalId, approvedBy, approval.EntityType, approval.EntityId);

                return new ApprovalResponse
                {
                    Id = approvalId,
                    Status = ApprovalStatus.Approved,
                    ApprovedBy = approvedBy,
                    ApprovalComments = comments,
                    ApprovedAt = approval.ApprovedAt.Value,
                    CanPost = true,
                    Message = "Approval granted successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving request {ApprovalId}", approvalId);
                throw;
            }
        }

        /// <summary>
        /// 🔥 Reject request (Checker)
        /// </summary>
        public async Task<ApprovalResponse> RejectAsync(Guid approvalId, string reason)
        {
            try
            {
                var companyId = _currentUserService.CompanyId;
                var approvedBy = _currentUserService.UserName;

                var approval = await _context.Approvals
                    .FirstOrDefaultAsync(a => a.Id == approvalId && a.CompanyId == companyId);

                if (approval == null)
                {
                    return new ApprovalResponse
                    {
                        Id = approvalId,
                        Status = ApprovalStatus.Rejected,
                        Message = "Approval request not found"
                    };
                }

                // 🔥 Validate checker is not the maker
                if (approval.CreatedBy == approvedBy)
                {
                    return new ApprovalResponse
                    {
                        Id = approvalId,
                        Status = ApprovalStatus.Rejected,
                        Message = "Cannot reject your own request"
                    };
                }

                // 🔥 Process rejection
                approval.ApprovedBy = approvedBy;
                approval.RejectionReason = reason;
                approval.ApprovedAt = DateTime.UtcNow;
                approval.Status = ApprovalStatus.Rejected;
                approval.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Approval rejected: Id={ApprovalId}, Checker={Checker}, Reason={Reason}",
                    approvalId, approvedBy, reason);

                return new ApprovalResponse
                {
                    Id = approvalId,
                    Status = ApprovalStatus.Rejected,
                    ApprovedBy = approvedBy,
                    RejectionReason = reason,
                    ApprovedAt = approval.ApprovedAt.Value,
                    CanPost = false,
                    Message = "Approval rejected"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting request {ApprovalId}", approvalId);
                throw;
            }
        }

        /// <summary>
        /// 🔥 Check if entity can be posted
        /// </summary>
        public async Task<bool> CanPostAsync(Guid entityId, string entityType)
        {
            try
            {
                var companyId = _currentUserService.CompanyId;

                var approval = await _context.Approvals
                    .FirstOrDefaultAsync(a => a.CompanyId == companyId && 
                                           a.EntityId == entityId && 
                                           a.EntityType == entityType);

                if (approval == null)
                {
                    // 🔥 No approval required for some entities
                    return await IsApprovalRequiredAsync(companyId, entityId, entityType) == false;
                }

                return approval.Status == ApprovalStatus.Approved;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if entity can be posted {EntityType}:{EntityId}",
                    entityType, entityId);
                return false;
            }
        }

        /// <summary>
        /// 🔥 Mark as posted after successful posting
        /// </summary>
        public async Task MarkAsPostedAsync(Guid entityId, string entityType)
        {
            try
            {
                var companyId = _currentUserService.CompanyId;

                var approval = await _context.Approvals
                    .FirstOrDefaultAsync(a => a.CompanyId == companyId && 
                                           a.EntityId == entityId && 
                                           a.EntityType == entityType);

                if (approval != null && approval.Status == ApprovalStatus.Approved)
                {
                    approval.Status = ApprovalStatus.Posted;
                    approval.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Entity marked as posted: {EntityType}:{EntityId}",
                        entityType, entityId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking entity as posted {EntityType}:{EntityId}",
                    entityType, entityId);
                throw;
            }
        }

        /// <summary>
        /// 🔥 Get pending approvals for current user
        /// </summary>
        public async Task<List<Approval>> GetPendingApprovalsAsync()
        {
            try
            {
                var companyId = _currentUserService.CompanyId;
                var currentUser = _currentUserService.UserName;

                var approvals = await _context.Approvals
                    .Where(a => a.CompanyId == companyId && 
                               a.Status == ApprovalStatus.Pending &&
                               a.CreatedBy != currentUser) // Don't show own requests
                    .OrderBy(a => a.Priority)
                    .ThenBy(a => a.RequestedAt)
                    .ToListAsync();

                return approvals;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending approvals");
                return new List<Approval>();
            }
        }

        /// <summary>
        /// 🔥 Get approval statistics
        /// </summary>
        public async Task<ApprovalStatistics> GetApprovalStatisticsAsync()
        {
            try
            {
                var companyId = _currentUserService.CompanyId;

                var approvals = await _context.Approvals
                    .Where(a => a.CompanyId == companyId)
                    .ToListAsync();

                var today = DateTime.Today;
                var stats = new ApprovalStatistics
                {
                    TotalApprovals = approvals.Count,
                    PendingApprovals = approvals.Count(a => a.Status == ApprovalStatus.Pending),
                    ApprovedToday = approvals.Count(a => a.Status == ApprovalStatus.Approved && a.ApprovedAt.HasValue && a.ApprovedAt.Value.Date == today),
                    RejectedToday = approvals.Count(a => a.Status == ApprovalStatus.Rejected && a.ApprovedAt.HasValue && a.ApprovedAt.Value.Date == today),
                    OverdueApprovals = approvals.Count(a => a.Status == ApprovalStatus.Pending && a.DeadlineAt.HasValue && a.DeadlineAt.Value < DateTime.UtcNow)
                };

                // 🔥 Calculate average approval time
                var completedApprovals = approvals.Where(a => a.ApprovedAt.HasValue).ToList();
                if (completedApprovals.Any())
                {
                    var avgHours = completedApprovals
                        .Average(a => (a.ApprovedAt!.Value - a.RequestedAt).TotalHours);
                    stats.AverageApprovalTimeHours = (decimal)avgHours;
                }

                // 🔥 Status breakdown
                stats.StatusBreakdown = approvals
                    .GroupBy(a => a.Status)
                    .ToDictionary(g => g.Key, g => g.Count());

                // 🔥 Department breakdown
                stats.DepartmentBreakdown = approvals
                    .Where(a => !string.IsNullOrEmpty(a.Department))
                    .GroupBy(a => a.Department)
                    .ToDictionary(g => g.Key, g => g.Count());

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting approval statistics");
                return new ApprovalStatistics();
            }
        }

        /// <summary>
        /// 🔥 Determine approval level based on amount and type
        /// </summary>
        private async Task<int> DetermineApprovalLevelAsync(int companyId, ApprovalRequest request)
        {
            // 🔥 High-value transactions require multiple levels
            if (request.ActualAmount.HasValue && request.AmountThreshold.HasValue)
            {
                if (request.ActualAmount >= request.AmountThreshold * 10)
                    return 3; // Executive level
                if (request.ActualAmount >= request.AmountThreshold * 5)
                    return 2; // Manager level
            }

            // 🔥 Special workflows require higher levels
            if (request.Workflow == ApprovalWorkflow.HighValue)
                return 2;
            if (request.Workflow == ApprovalWorkflow.Regulatory)
                return 3;

            return 1; // Standard level
        }

        /// <summary>
        /// 🔥 Calculate approval deadline
        /// </summary>
        private DateTime? CalculateDeadline(ApprovalPriority priority, ApprovalWorkflow workflow)
        {
            var hours = priority switch
            {
                ApprovalPriority.Emergency => 1,
                ApprovalPriority.Critical => 4,
                ApprovalPriority.High => 8,
                ApprovalPriority.Normal => 24,
                ApprovalPriority.Low => 72,
                _ => 24
            };

            // 🔥 Regulatory workflows have longer deadlines
            if (workflow == ApprovalWorkflow.Regulatory)
                hours *= 2;

            return DateTime.UtcNow.AddHours(hours);
        }

        /// <summary>
        /// 🔥 Check if self-approval is allowed
        /// </summary>
        private async Task<bool> CanSelfApproveAsync(int companyId, string userName, ApprovalRequest request)
        {
            // 🔥 Emergency workflows allow self-approval
            if (request.Workflow == ApprovalWorkflow.Emergency)
                return true;

            // 🔥 Low amounts might allow self-approval
            if (request.ActualAmount.HasValue && request.ActualAmount < 100)
                return true;

            // 🔥 Check user permissions
            return await HasSelfApprovalPermissionAsync(companyId, userName, request.EntityType);
        }

        /// <summary>
        /// 🔥 Check if approval is required for entity
        /// </summary>
        private async Task<bool> IsApprovalRequiredAsync(int companyId, Guid entityId, string entityType)
        {
            // 🔥 Journal entries always require approval
            if (entityType == "Journal")
                return true;

            // 🔥 High-value invoices require approval
            if (entityType == "Invoice")
            {
                var invoice = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.Id.ToString() == entityId.ToString() && i.CompanyId == companyId);
                return invoice?.TotalAmount >= 1000;
            }

            return false;
        }

        /// <summary>
        /// 🔥 Check if user has self-approval permission
        /// </summary>
        private async Task<bool> HasSelfApprovalPermissionAsync(int companyId, string userName, string entityType)
        {
            // 🔥 Implement role-based permission check
            // For now, return false (no self-approval)
            return false;
        }
    }
}
