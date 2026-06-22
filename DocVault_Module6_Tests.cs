using Xunit;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocVault.Tests.Module6
{
    public class DocumentWorkflowServiceTests
    {
        private readonly Mock<IWorkflowRepository> _mockWorkflowRepo;
        private readonly Mock<IApprovalTaskRepository> _mockTaskRepo;
        private readonly Mock<IWorkflowNotificationService> _mockNotificationService;
        private readonly Mock<IDocumentRepository> _mockDocumentRepo;
        private readonly DocumentWorkflowService _service;

        public DocumentWorkflowServiceTests()
        {
            _mockWorkflowRepo = new Mock<IWorkflowRepository>();
            _mockTaskRepo = new Mock<IApprovalTaskRepository>();
            _mockNotificationService = new Mock<IWorkflowNotificationService>();
            _mockDocumentRepo = new Mock<IDocumentRepository>();

            _service = new DocumentWorkflowService(
                _mockWorkflowRepo.Object,
                _mockTaskRepo.Object,
                _mockNotificationService.Object,
                _mockDocumentRepo.Object);
        }

        [Fact]
        public async Task CreateWorkflowAsync_WithValidRequest_CreatesWorkflow()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var templateId = Guid.NewGuid();

            var doc = new Document { Id = documentId, CreatedByUserId = userId, OrganizationId = Guid.NewGuid() };
            _mockDocumentRepo.Setup(x => x.GetByIdAsync(documentId)).ReturnsAsync(doc);

            var template = new DocumentWorkflow
            {
                Id = templateId,
                WorkflowName = "Standard Review",
                Stages = new List<WorkflowStage>
                {
                    new WorkflowStage { StageName = "Review", ApprovalType = ApprovalType.Parallel },
                    new WorkflowStage { StageName = "Approval", ApprovalType = ApprovalType.Sequential }
                }
            };

            _mockWorkflowRepo.Setup(x => x.GetTemplateAsync(templateId)).ReturnsAsync(template);

            // Act
            var result = await _service.CreateWorkflowAsync(userId, documentId, templateId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(documentId, result.DocumentId);
            Assert.Equal(DocumentWorkflowStatus.InReview, result.Status);
            Assert.Equal(2, result.Stages.Count);
            Assert.Equal(StageStatus.InProgress, result.Stages[0].Status);
            Assert.Equal(StageStatus.NotStarted, result.Stages[1].Status);
        }

        [Fact]
        public async Task CreateWorkflowAsync_NonDocumentOwner_ThrowsException()
        {
            var userId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            var documentId = Guid.NewGuid();

            var doc = new Document { Id = documentId, CreatedByUserId = otherId };
            _mockDocumentRepo.Setup(x => x.GetByIdAsync(documentId)).ReturnsAsync(doc);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.CreateWorkflowAsync(userId, documentId, Guid.NewGuid()));
        }

        [Fact]
        public async Task GetWorkflowAsync_WithValidId_ReturnsWorkflow()
        {
            var workflowId = Guid.NewGuid();
            var workflow = new DocumentWorkflow { Id = workflowId, WorkflowName = "Test" };

            _mockWorkflowRepo.Setup(x => x.GetByIdAsync(workflowId)).ReturnsAsync(workflow);

            var result = await _service.GetWorkflowAsync(workflowId);

            Assert.NotNull(result);
            Assert.Equal(workflowId, result.Id);
        }

        [Fact]
        public async Task ApproveStageAsync_WithValidTask_ApprovesStage()
        {
            var userId = Guid.NewGuid();
            var taskId = Guid.NewGuid();
            var stageId = Guid.NewGuid();
            var workflowId = Guid.NewGuid();

            var task = new ApprovalTask
            {
                Id = taskId,
                WorkflowId = workflowId,
                StageId = stageId,
                AssignedToUserId = userId,
                Status = ApprovalTaskStatus.Pending
            };

            _mockTaskRepo.Setup(x => x.GetByIdAsync(taskId)).ReturnsAsync(task);

            var workflow = new DocumentWorkflow
            {
                Id = workflowId,
                DocumentId = Guid.NewGuid(),
                Stages = new List<WorkflowStage> { new WorkflowStage { Id = stageId, RequiredApprovals = 1 } }
            };

            _mockWorkflowRepo.Setup(x => x.GetByIdAsync(workflowId)).ReturnsAsync(workflow);
            _mockTaskRepo.Setup(x => x.GetByStageIdAsync(stageId))
                .ReturnsAsync(new List<ApprovalTask> { task });

            // Act
            await _service.ApproveStageAsync(userId, taskId, "Looks good");

            // Assert
            Assert.Equal(ApprovalTaskStatus.Approved, task.Status);
            Assert.Equal("Looks good", task.Comments);
            _mockTaskRepo.Verify(x => x.UpdateAsync(It.IsAny<ApprovalTask>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task ApproveStageAsync_WrongUser_ThrowsException()
        {
            var userId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            var taskId = Guid.NewGuid();

            var task = new ApprovalTask { AssignedToUserId = otherId };
            _mockTaskRepo.Setup(x => x.GetByIdAsync(taskId)).ReturnsAsync(task);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.ApproveStageAsync(userId, taskId, "Approved"));
        }

        [Fact]
        public async Task RejectStageAsync_WithValidTask_RejectsStage()
        {
            var userId = Guid.NewGuid();
            var taskId = Guid.NewGuid();
            var workflowId = Guid.NewGuid();

            var task = new ApprovalTask
            {
                Id = taskId,
                WorkflowId = workflowId,
                AssignedToUserId = userId,
                Status = ApprovalTaskStatus.Pending
            };

            _mockTaskRepo.Setup(x => x.GetByIdAsync(taskId)).ReturnsAsync(task);

            var workflow = new DocumentWorkflow { Id = workflowId, CurrentStageIndex = 1 };
            _mockWorkflowRepo.Setup(x => x.GetByIdAsync(workflowId)).ReturnsAsync(workflow);

            // Act
            await _service.RejectStageAsync(userId, taskId, "Needs revision");

            // Assert
            Assert.Equal(ApprovalTaskStatus.Rejected, task.Status);
            Assert.Equal("Needs revision", task.RejectionReason);
            Assert.Equal(DocumentWorkflowStatus.Rejected, workflow.Status);
            Assert.Equal(0, workflow.CurrentStageIndex); // Reset to draft
        }

        [Fact]
        public async Task GetPendingTasksAsync_ReturnsPendingTasks()
        {
            var userId = Guid.NewGuid();
            var tasks = new List<ApprovalTask>
            {
                new ApprovalTask { Id = Guid.NewGuid(), Status = ApprovalTaskStatus.Pending },
                new ApprovalTask { Id = Guid.NewGuid(), Status = ApprovalTaskStatus.Pending }
            };

            _mockTaskRepo.Setup(x => x.GetPendingTasksForUserAsync(userId)).ReturnsAsync(tasks);

            var result = await _service.GetPendingTasksAsync(userId);

            Assert.Equal(2, result.Count);
            Assert.All(result, t => Assert.Equal(ApprovalTaskStatus.Pending, t.Status));
        }

        [Fact]
        public async Task GetWorkflowHistoryAsync_ReturnsOrderedStages()
        {
            var workflowId = Guid.NewGuid();
            var workflow = new DocumentWorkflow
            {
                Id = workflowId,
                Stages = new List<WorkflowStage>
                {
                    new WorkflowStage { StageNumber = 2, StageName = "Approval" },
                    new WorkflowStage { StageNumber = 1, StageName = "Review" },
                    new WorkflowStage { StageNumber = 3, StageName = "Publishing" }
                }
            };

            _mockWorkflowRepo.Setup(x => x.GetByIdAsync(workflowId)).ReturnsAsync(workflow);

            var result = await _service.GetWorkflowHistoryAsync(workflowId);

            Assert.Equal(3, result.Count);
            Assert.Equal("Review", result[0].StageName);
            Assert.Equal("Approval", result[1].StageName);
            Assert.Equal("Publishing", result[2].StageName);
        }
    }

    public class WorkflowNotificationServiceTests
    {
        private readonly Mock<IWorkflowNotificationRepository> _mockRepository;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly WorkflowNotificationService _service;

        public WorkflowNotificationServiceTests()
        {
            _mockRepository = new Mock<IWorkflowNotificationRepository>();
            _mockEmailService = new Mock<IEmailService>();
            _service = new WorkflowNotificationService(_mockRepository.Object, _mockEmailService.Object);
        }

        [Fact]
        public async Task NotifyTaskAssignedAsync_CreatesNotificationAndSendsEmail()
        {
            var workflowId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            await _service.NotifyTaskAssignedAsync(workflowId, userId, "Review");

            _mockRepository.Verify(x => x.CreateAsync(It.Is<WorkflowNotification>(
                n => n.Type == NotificationType.TaskAssigned &&
                n.RecipientUserId == userId)), Times.Once);

            _mockEmailService.Verify(x => x.SendNotificationAsync(userId, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task NotifyApprovalAsync_CreatesApprovalNotification()
        {
            var workflowId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            await _service.NotifyApprovalAsync(workflowId, userId, NotificationType.Approved, "Document approved");

            _mockRepository.Verify(x => x.CreateAsync(It.Is<WorkflowNotification>(
                n => n.Type == NotificationType.Approved)), Times.Once);
        }

        [Fact]
        public async Task GetNotificationsAsync_ReturnsUnreadNotifications()
        {
            var userId = Guid.NewGuid();
            var notifications = new List<WorkflowNotification>
            {
                new WorkflowNotification { IsRead = false },
                new WorkflowNotification { IsRead = false }
            };

            _mockRepository.Setup(x => x.GetUnreadNotificationsAsync(userId)).ReturnsAsync(notifications);

            var result = await _service.GetNotificationsAsync(userId);

            Assert.Equal(2, result.Count);
            Assert.All(result, n => Assert.False(n.IsRead));
        }

        [Fact]
        public async Task MarkAsReadAsync_UpdatesNotification()
        {
            var notificationId = Guid.NewGuid();
            var notification = new WorkflowNotification { Id = notificationId, IsRead = false };

            _mockRepository.Setup(x => x.GetByIdAsync(notificationId)).ReturnsAsync(notification);

            await _service.MarkAsReadAsync(notificationId);

            Assert.True(notification.IsRead);
            Assert.NotNull(notification.ReadAt);
            _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<WorkflowNotification>()), Times.Once);
        }
    }

    public class WorkflowIntegrationTests
    {
        [Fact]
        public async Task CompleteWorkflowProcess_FromSubmitToApproval()
        {
            // Arrange - Setup mocks
            var mockWorkflowRepo = new Mock<IWorkflowRepository>();
            var mockTaskRepo = new Mock<IApprovalTaskRepository>();
            var mockNotificationService = new Mock<IWorkflowNotificationService>();
            var mockDocumentRepo = new Mock<IDocumentRepository>();

            var service = new DocumentWorkflowService(
                mockWorkflowRepo.Object,
                mockTaskRepo.Object,
                mockNotificationService.Object,
                mockDocumentRepo.Object);

            var userId = Guid.NewGuid();
            var approverId = Guid.NewGuid();
            var documentId = Guid.NewGuid();

            // Act 1: Create workflow
            var doc = new Document { Id = documentId, CreatedByUserId = userId, OrganizationId = Guid.NewGuid() };
            mockDocumentRepo.Setup(x => x.GetByIdAsync(documentId)).ReturnsAsync(doc);

            var template = new DocumentWorkflow
            {
                Id = Guid.NewGuid(),
                WorkflowName = "Test Workflow",
                Stages = new List<WorkflowStage>
                {
                    new WorkflowStage
                    {
                        Id = Guid.NewGuid(),
                        StageName = "Review",
                        ApprovalType = ApprovalType.Sequential,
                        RequiredApprovals = 1,
                        ApproverUserIds = new List<Guid> { approverId }
                    }
                }
            };

            mockWorkflowRepo.Setup(x => x.GetTemplateAsync(It.IsAny<Guid>())).ReturnsAsync(template);

            var workflow = await service.CreateWorkflowAsync(userId, documentId, template.Id);

            // Assert 1: Workflow created
            Assert.NotNull(workflow);
            Assert.Equal(DocumentWorkflowStatus.InReview, workflow.Status);

            // Act 2: Approve stage
            var approvalTask = new ApprovalTask
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflow.Id,
                AssignedToUserId = approverId,
                Status = ApprovalTaskStatus.Pending
            };

            mockTaskRepo.Setup(x => x.GetByIdAsync(approvalTask.Id)).ReturnsAsync(approvalTask);
            mockTaskRepo.Setup(x => x.GetByStageIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new List<ApprovalTask> { approvalTask });

            workflow.Stages[0].RequiredApprovals = 1;
            mockWorkflowRepo.Setup(x => x.GetByIdAsync(workflow.Id)).ReturnsAsync(workflow);

            await service.ApproveStageAsync(approverId, approvalTask.Id, "Approved");

            // Assert 2: Task approved
            Assert.Equal(ApprovalTaskStatus.Approved, approvalTask.Status);
            mockTaskRepo.Verify(x => x.UpdateAsync(It.IsAny<ApprovalTask>()), Times.AtLeastOnce);
        }
    }
}
