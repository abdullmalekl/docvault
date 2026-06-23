using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// ============================================================================
// DOCVAULT MODULE 6: DOCUMENT WORKFLOW & APPROVALS
// ============================================================================
// Document status workflow, approval routing, notifications, history tracking

namespace DocVault.Core.Workflow
{
    // ========================================================================
    // DATA MODELS
    // ========================================================================

    public class DocumentWorkflow
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public Guid OrganizationId { get; set; }

        public string WorkflowName { get; set; }
        public List<WorkflowStage> Stages { get; set; } = new();

        public int CurrentStageIndex { get; set; } = 0;
        public DocumentWorkflowStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public Guid CreatedByUserId { get; set; }
    }

    public class WorkflowStage
    {
        public Guid Id { get; set; }
        public Guid WorkflowId { get; set; }
        public int StageNumber { get; set; }

        public string StageName { get; set; } // Draft, Review, Approval, Publishing
        public string Description { get; set; }

        public List<Guid> ApproverUserIds { get; set; } = new();
        public List<Guid> ApproverRoleIds { get; set; } = new();

        public ApprovalType ApprovalType { get; set; } // Sequential, Parallel, Optional
        public int RequiredApprovals { get; set; } = 1;

        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public StageStatus Status { get; set; }

        public bool CanRejectToStart { get; set; } = false;
        public bool CanSkip { get; set; } = false;
    }

    public class ApprovalTask
    {
        public Guid Id { get; set; }
        public Guid WorkflowId { get; set; }
        public Guid StageId { get; set; }
        public Guid DocumentId { get; set; }

        public Guid AssignedToUserId { get; set; }
        public ApprovalTaskStatus Status { get; set; } // Pending, Approved, Rejected, Skipped

        public DateTime CreatedAt { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedAt { get; set; }

        public string Comments { get; set; }
        public string RejectionReason { get; set; }

        public int Priority { get; set; } = 0; // 0=Normal, 1=High, 2=Urgent
    }

    public class WorkflowNotification
    {
        public Guid Id { get; set; }
        public Guid WorkflowId { get; set; }
        public Guid RecipientUserId { get; set; }

        public string Subject { get; set; }
        public string Message { get; set; }
        public NotificationType Type { get; set; } // TaskAssigned, Approved, Rejected, Expired

        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
    }

    public enum DocumentWorkflowStatus
    {
        Draft = 0,
        InReview = 1,
        PendingApproval = 2,
        Approved = 3,
        Rejected = 4,
        Published = 5,
        Archived = 6
    }

    public enum StageStatus
    {
        NotStarted = 0,
        InProgress = 1,
        Completed = 2,
        Skipped = 3,
        Rejected = 4
    }

    public enum ApprovalType
    {
        Sequential = 0, // One by one
        Parallel = 1,   // All at once
        Optional = 2    // Optional approval
    }

    public enum ApprovalTaskStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2,
        Skipped = 3
    }

    public enum NotificationType
    {
        TaskAssigned = 0,
        Approved = 1,
        Rejected = 2,
        Expired = 3,
        WorkflowCompleted = 4
    }

    public class SubmitForApprovalRequest
    {
        public Guid DocumentId { get; set; }
        public Guid WorkflowTemplateId { get; set; }
        public string SubmissionComments { get; set; }
    }

    public class ApprovalDecisionRequest
    {
        public Guid ApprovalTaskId { get; set; }
        public ApprovalTaskStatus Decision { get; set; }
        public string Comments { get; set; }
        public string RejectionReason { get; set; }
    }

    // ========================================================================
    // WORKFLOW SERVICE
    // ========================================================================

    public interface IDocumentWorkflowService
    {
        Task<DocumentWorkflow> CreateWorkflowAsync(Guid userId, Guid documentId, Guid workflowTemplateId);
        Task<DocumentWorkflow> GetWorkflowAsync(Guid workflowId);
        Task<List<WorkflowStage>> GetCurrentStageAsync(Guid workflowId);
        Task SubmitForApprovalAsync(Guid userId, SubmitForApprovalRequest request);
        Task ApproveStageAsync(Guid userId, Guid approvalTaskId, string comments);
        Task RejectStageAsync(Guid userId, Guid approvalTaskId, string rejectionReason);
        Task<List<ApprovalTask>> GetPendingTasksAsync(Guid userId);
        Task<List<WorkflowStage>> GetWorkflowHistoryAsync(Guid workflowId);
    }

    public class DocumentWorkflowService
    {
        private readonly IWorkflowRepository _workflowRepository;
        private readonly IApprovalTaskRepository _approvalTaskRepository;
        private readonly IWorkflowNotificationService _notificationService;
        private readonly IDocumentRepository _documentRepository;

        public DocumentWorkflowService(
            IWorkflowRepository workflowRepository,
            IApprovalTaskRepository approvalTaskRepository,
            IWorkflowNotificationService notificationService,
            IDocumentRepository documentRepository)
        {
            _workflowRepository = workflowRepository;
            _approvalTaskRepository = approvalTaskRepository;
            _notificationService = notificationService;
            _documentRepository = documentRepository;
        }

        public async Task<DocumentWorkflow> CreateWorkflowAsync(Guid userId, Guid documentId, Guid workflowTemplateId)
        {
            var document = await _documentRepository.GetByIdAsync(documentId)
                ?? throw new KeyNotFoundException($"Document not found: {documentId}");

            var template = await _workflowRepository.GetTemplateAsync(workflowTemplateId)
                ?? throw new KeyNotFoundException($"Workflow template not found: {workflowTemplateId}");

            var workflow = new DocumentWorkflow
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                OrganizationId = document.CreatedByUserId == userId ? document.OrganizationId : throw new UnauthorizedAccessException(),
                WorkflowName = template.Name,
                Stages = template.Stages.Select((s, i) => new WorkflowStage
                {
                    Id = Guid.NewGuid(),
                    StageNumber = i + 1,
                    StageName = s.StageName,
                    ApprovalType = s.ApprovalType,
                    RequiredApprovals = s.RequiredApprovals,
                    Status = i == 0 ? StageStatus.InProgress : StageStatus.NotStarted,
                    StartedAt = i == 0 ? DateTime.UtcNow : (DateTime?)null
                }).ToList(),
                Status = DocumentWorkflowStatus.InReview,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = userId
            };

            await _workflowRepository.CreateAsync(workflow);

            // Create approval tasks for first stage
            await CreateApprovalTasksForStageAsync(workflow.Id, workflow.Stages[0]);

            // Update document status
            document.Status = DocumentStatus.InReview;
            await _documentRepository.UpdateAsync(document);

            return workflow;
        }

        public async Task<DocumentWorkflow> GetWorkflowAsync(Guid workflowId)
        {
            return await _workflowRepository.GetByIdAsync(workflowId)
                ?? throw new KeyNotFoundException($"Workflow not found: {workflowId}");
        }

        public async Task<List<WorkflowStage>> GetCurrentStageAsync(Guid workflowId)
        {
            var workflow = await GetWorkflowAsync(workflowId);
            var currentStage = workflow.Stages[workflow.CurrentStageIndex];
            return new List<WorkflowStage> { currentStage };
        }

        public async Task SubmitForApprovalAsync(Guid userId, SubmitForApprovalRequest request)
        {
            var workflow = await GetWorkflowAsync(request.WorkflowTemplateId);
            var document = await _documentRepository.GetByIdAsync(request.DocumentId);

            if (document.CreatedByUserId != userId)
                throw new UnauthorizedAccessException("Only document owner can submit for approval");

            workflow.Status = DocumentWorkflowStatus.InReview;
            await _workflowRepository.UpdateAsync(workflow);
        }

        public async Task ApproveStageAsync(Guid userId, Guid approvalTaskId, string comments)
        {
            var task = await _approvalTaskRepository.GetByIdAsync(approvalTaskId)
                ?? throw new KeyNotFoundException("Approval task not found");

            if (task.AssignedToUserId != userId)
                throw new UnauthorizedAccessException("Task not assigned to this user");

            task.Status = ApprovalTaskStatus.Approved;
            task.Comments = comments;
            task.CompletedAt = DateTime.UtcNow;

            await _approvalTaskRepository.UpdateAsync(task);

            // Check if stage is complete
            await CheckStageCompletionAsync(task.WorkflowId, task.StageId);

            // Notify workflow creator
            await _notificationService.NotifyApprovalAsync(task.WorkflowId, userId, NotificationType.Approved, comments);
        }

        public async Task RejectStageAsync(Guid userId, Guid approvalTaskId, string rejectionReason)
        {
            var task = await _approvalTaskRepository.GetByIdAsync(approvalTaskId)
                ?? throw new KeyNotFoundException("Approval task not found");

            if (task.AssignedToUserId != userId)
                throw new UnauthorizedAccessException("Task not assigned to this user");

            task.Status = ApprovalTaskStatus.Rejected;
            task.RejectionReason = rejectionReason;
            task.CompletedAt = DateTime.UtcNow;

            await _approvalTaskRepository.UpdateAsync(task);

            // Move workflow back to draft
            var workflow = await GetWorkflowAsync(task.WorkflowId);
            workflow.Status = DocumentWorkflowStatus.Rejected;
            workflow.CurrentStageIndex = 0;
            await _workflowRepository.UpdateAsync(workflow);

            // Notify
            await _notificationService.NotifyRejectionAsync(task.WorkflowId, userId, rejectionReason);
        }

        public async Task<List<ApprovalTask>> GetPendingTasksAsync(Guid userId)
        {
            return await _approvalTaskRepository.GetPendingTasksForUserAsync(userId);
        }

        public async Task<List<WorkflowStage>> GetWorkflowHistoryAsync(Guid workflowId)
        {
            var workflow = await GetWorkflowAsync(workflowId);
            return workflow.Stages.OrderBy(s => s.StageNumber).ToList();
        }

        private async Task CreateApprovalTasksForStageAsync(Guid workflowId, WorkflowStage stage)
        {
            var approvers = new List<Guid>();
            approvers.AddRange(stage.ApproverUserIds);

            foreach (var approverId in approvers)
            {
                var task = new ApprovalTask
                {
                    Id = Guid.NewGuid(),
                    WorkflowId = workflowId,
                    StageId = stage.Id,
                    AssignedToUserId = approverId,
                    Status = ApprovalTaskStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow.AddDays(3) // 3-day due date
                };

                await _approvalTaskRepository.CreateAsync(task);
                await _notificationService.NotifyTaskAssignedAsync(task.WorkflowId, approverId, stage.StageName);
            }
        }

        private async Task CheckStageCompletionAsync(Guid workflowId, Guid stageId)
        {
            var workflow = await GetWorkflowAsync(workflowId);
            var stage = workflow.Stages.FirstOrDefault(s => s.Id == stageId);

            if (stage == null) return;

            var stageTasks = await _approvalTaskRepository.GetByStageIdAsync(stageId);
            var approvedCount = stageTasks.Count(t => t.Status == ApprovalTaskStatus.Approved);

            if (approvedCount >= stage.RequiredApprovals)
            {
                // Stage complete - move to next
                stage.Status = StageStatus.Completed;
                stage.CompletedAt = DateTime.UtcNow;

                var nextStageIndex = workflow.CurrentStageIndex + 1;
                if (nextStageIndex < workflow.Stages.Count)
                {
                    var nextStage = workflow.Stages[nextStageIndex];
                    nextStage.Status = StageStatus.InProgress;
                    nextStage.StartedAt = DateTime.UtcNow;
                    workflow.CurrentStageIndex = nextStageIndex;

                    await CreateApprovalTasksForStageAsync(workflowId, nextStage);
                }
                else
                {
                    // Workflow complete
                    workflow.Status = DocumentWorkflowStatus.Approved;
                    workflow.CompletedAt = DateTime.UtcNow;

                    var doc = await _documentRepository.GetByIdAsync(workflow.DocumentId);
                    doc.Status = DocumentStatus.Published;
                    await _documentRepository.UpdateAsync(doc);
                }

                await _workflowRepository.UpdateAsync(workflow);
            }
        }
    }

    // ========================================================================
    // WORKFLOW NOTIFICATION SERVICE
    // ========================================================================

    public interface IWorkflowNotificationService
    {
        Task NotifyTaskAssignedAsync(Guid workflowId, Guid userId, string stageName);
        Task NotifyApprovalAsync(Guid workflowId, Guid approverId, NotificationType type, string message);
        Task NotifyRejectionAsync(Guid workflowId, Guid approverId, string reason);
        Task<List<WorkflowNotification>> GetNotificationsAsync(Guid userId);
        Task MarkAsReadAsync(Guid notificationId);
    }

    public class WorkflowNotificationService : IWorkflowNotificationService
    {
        private readonly IWorkflowNotificationRepository _repository;
        private readonly IEmailService _emailService;

        public WorkflowNotificationService(
            IWorkflowNotificationRepository repository,
            IEmailService emailService)
        {
            _repository = repository;
            _emailService = emailService;
        }

        public async Task NotifyTaskAssignedAsync(Guid workflowId, Guid userId, string stageName)
        {
            var notification = new WorkflowNotification
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflowId,
                RecipientUserId = userId,
                Subject = $"Approval Task: {stageName}",
                Message = $"A document requires your approval at stage: {stageName}",
                Type = NotificationType.TaskAssigned,
                CreatedAt = DateTime.UtcNow
            };

            await _repository.CreateAsync(notification);
            await _emailService.SendNotificationAsync(userId, notification.Subject, notification.Message);
        }

        public async Task NotifyApprovalAsync(Guid workflowId, Guid approverId, NotificationType type, string message)
        {
            var notification = new WorkflowNotification
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflowId,
                RecipientUserId = approverId,
                Subject = type switch
                {
                    NotificationType.Approved => "Document Approved",
                    NotificationType.Rejected => "Document Rejected",
                    _ => "Workflow Update"
                },
                Message = message,
                Type = type,
                CreatedAt = DateTime.UtcNow
            };

            await _repository.CreateAsync(notification);
        }

        public async Task NotifyRejectionAsync(Guid workflowId, Guid approverId, string reason)
        {
            await NotifyApprovalAsync(workflowId, approverId, NotificationType.Rejected, $"Rejected: {reason}");
        }

        public async Task<List<WorkflowNotification>> GetNotificationsAsync(Guid userId)
        {
            return await _repository.GetUnreadNotificationsAsync(userId);
        }

        public async Task MarkAsReadAsync(Guid notificationId)
        {
            var notification = await _repository.GetByIdAsync(notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                await _repository.UpdateAsync(notification);
            }
        }
    }

    // ========================================================================
    // REPOSITORY INTERFACES
    // ========================================================================

    public interface IWorkflowRepository
    {
        Task CreateAsync(DocumentWorkflow workflow);
        Task<DocumentWorkflow> GetByIdAsync(Guid id);
        Task<WorkflowStage> GetTemplateAsync(Guid templateId);
        Task UpdateAsync(DocumentWorkflow workflow);
    }

    public interface IApprovalTaskRepository
    {
        Task CreateAsync(ApprovalTask task);
        Task<ApprovalTask> GetByIdAsync(Guid id);
        Task<List<ApprovalTask>> GetByStageIdAsync(Guid stageId);
        Task<List<ApprovalTask>> GetPendingTasksForUserAsync(Guid userId);
        Task UpdateAsync(ApprovalTask task);
    }

    public interface IWorkflowNotificationRepository
    {
        Task CreateAsync(WorkflowNotification notification);
        Task<WorkflowNotification> GetByIdAsync(Guid id);
        Task<List<WorkflowNotification>> GetUnreadNotificationsAsync(Guid userId);
        Task UpdateAsync(WorkflowNotification notification);
    }

    public interface IEmailService
    {
        Task SendNotificationAsync(Guid userId, string subject, string message);
    }
}
