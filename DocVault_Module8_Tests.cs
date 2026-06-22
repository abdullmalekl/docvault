using Xunit;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocVault.Tests.Module8
{
    public class AuditServiceTests
    {
        private readonly Mock<IAuditRepository> _mockRepository;
        private readonly Mock<IAccessControlRepository> _mockAccessRepository;
        private readonly Mock<ILogger> _mockLogger;
        private readonly AuditService _service;

        public AuditServiceTests()
        {
            _mockRepository = new Mock<IAuditRepository>();
            _mockAccessRepository = new Mock<IAccessControlRepository>();
            _mockLogger = new Mock<ILogger>();

            _service = new AuditService(
                _mockRepository.Object,
                _mockAccessRepository.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task LogActionAsync_WithValidAction_CreatesAuditLog()
        {
            var userId = Guid.NewGuid();
            var entityId = Guid.NewGuid();
            var details = new Dictionary<string, object> { { "field", "value" } };

            await _service.LogActionAsync(userId, AuditActionType.Create, "Document", entityId, details);

            _mockRepository.Verify(x => x.CreateAsync(It.Is<AuditLog>(
                l => l.UserId == userId &&
                l.ActionType == AuditActionType.Create &&
                l.EntityType == "Document")), Times.Once);
        }

        [Fact]
        public async Task GetAuditTrailAsync_WithValidOrgId_ReturnsList()
        {
            var orgId = Guid.NewGuid();
            var logs = new List<AuditLog>
            {
                new AuditLog { Id = Guid.NewGuid(), ActionType = AuditActionType.Create },
                new AuditLog { Id = Guid.NewGuid(), ActionType = AuditActionType.Update }
            };

            _mockRepository.Setup(x => x.GetByOrganizationAsync(orgId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>()))
                .ReturnsAsync(logs);

            var result = await _service.GetAuditTrailAsync(orgId);

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetUserActivityAsync_WithValidUserId_ReturnsUserLogs()
        {
            var userId = Guid.NewGuid();
            var logs = new List<AuditLog>
            {
                new AuditLog { UserId = userId, ActionType = AuditActionType.Read }
            };

            _mockRepository.Setup(x => x.GetByUserAsync(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(logs);

            var result = await _service.GetUserActivityAsync(userId);

            Assert.Single(result);
            Assert.Equal(userId, result[0].UserId);
        }

        [Fact]
        public async Task GetActionSummaryAsync_ReturnsActionCounts()
        {
            var orgId = Guid.NewGuid();
            var logs = new List<AuditLog>
            {
                new AuditLog { ActionType = AuditActionType.Create },
                new AuditLog { ActionType = AuditActionType.Create },
                new AuditLog { ActionType = AuditActionType.Update }
            };

            _mockRepository.Setup(x => x.GetByOrganizationAsync(orgId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>()))
                .ReturnsAsync(logs);

            var result = await _service.GetActionSummaryAsync(orgId);

            Assert.Equal(2, result[AuditActionType.Create]);
            Assert.Equal(1, result[AuditActionType.Update]);
        }

        [Fact]
        public async Task IsAccessAuthorizedAsync_WithAuthorizedUser_ReturnsTrue()
        {
            var userId = Guid.NewGuid();
            var documentId = Guid.NewGuid();

            _mockAccessRepository.Setup(x => x.CheckAccessAsync(userId, documentId, It.IsAny<string>()))
                .ReturnsAsync(true);

            var result = await _service.IsAccessAuthorizedAsync(userId, documentId, AuditActionType.Read);

            Assert.True(result);
        }
    }

    public class ComplianceServiceTests
    {
        private readonly Mock<IComplianceRepository> _mockRepository;
        private readonly Mock<IAuditRepository> _mockAuditRepository;
        private readonly Mock<IDocumentRepository> _mockDocumentRepository;
        private readonly ComplianceService _service;

        public ComplianceServiceTests()
        {
            _mockRepository = new Mock<IComplianceRepository>();
            _mockAuditRepository = new Mock<IAuditRepository>();
            _mockDocumentRepository = new Mock<IDocumentRepository>();

            _service = new ComplianceService(
                _mockRepository.Object,
                _mockAuditRepository.Object,
                _mockDocumentRepository.Object);
        }

        [Fact]
        public async Task GenerateReportAsync_WithValidParams_CreatesReport()
        {
            var orgId = Guid.NewGuid();
            var from = DateTime.UtcNow.AddDays(-30);
            var to = DateTime.UtcNow;

            var result = await _service.GenerateReportAsync(orgId, ComplianceFramework.GDPR, from, to);

            Assert.NotNull(result);
            Assert.Equal(orgId, result.OrganizationId);
            Assert.Equal(ComplianceFramework.GDPR, result.Framework);
            Assert.True(result.ComplianceScore >= 0 && result.ComplianceScore <= 100);
        }

        [Fact]
        public async Task CalculateComplianceScoreAsync_WithNoFindings_Returns100()
        {
            var orgId = Guid.NewGuid();

            var score = await _service.CalculateComplianceScoreAsync(orgId, ComplianceFramework.SOC2);

            Assert.Equal(100, score);
        }

        [Fact]
        public async Task ValidateDocumentClassificationAsync_WithSensitiveContent_RequiresConfidential()
        {
            var docId = Guid.NewGuid();
            var doc = new Document
            {
                Id = docId,
                Classification = DocumentClassification.Public,
                Tags = new List<string> { "password", "secret" }
            };

            _mockDocumentRepository.Setup(x => x.GetByIdAsync(docId)).ReturnsAsync(doc);

            var policy = new DataClassificationPolicy
            {
                SensitiveKeywords = new List<string> { "password", "credit card" }
            };

            _mockRepository.Setup(x => x.GetActiveClassificationPolicyAsync(doc.OrganizationId))
                .ReturnsAsync(policy);

            var result = await _service.ValidateDocumentClassificationAsync(docId);

            Assert.False(result);
        }

        [Fact]
        public async Task UpdateFindingStatusAsync_UpdatesStatus()
        {
            var findingId = Guid.NewGuid();

            await _service.UpdateFindingStatusAsync(findingId, FindingStatus.Resolved, "Fixed");

            _mockRepository.Verify(x => x.UpdateFindingStatusAsync(findingId, FindingStatus.Resolved, "Fixed"), Times.Once);
        }

        [Fact]
        public async Task AssessControlsAsync_WithFramework_ReturnsFindings()
        {
            var orgId = Guid.NewGuid();

            var findings = await _service.AssessControlsAsync(orgId, ComplianceFramework.GDPR);

            Assert.NotEmpty(findings);
            Assert.NotNull(findings[0].ControlId);
        }
    }

    public class DataRetentionServiceTests
    {
        private readonly Mock<IRetentionRepository> _mockRepository;
        private readonly Mock<IDocumentRepository> _mockDocumentRepository;
        private readonly DataRetentionService _service;

        public DataRetentionServiceTests()
        {
            _mockRepository = new Mock<IRetentionRepository>();
            _mockDocumentRepository = new Mock<IDocumentRepository>();

            _service = new DataRetentionService(
                _mockRepository.Object,
                _mockDocumentRepository.Object);
        }

        [Fact]
        public async Task CreateScheduleAsync_WithValidParams_CreatesSchedule()
        {
            var orgId = Guid.NewGuid();

            var result = await _service.CreateScheduleAsync(orgId, "Invoice", 7);

            Assert.NotNull(result);
            Assert.Equal(orgId, result.OrganizationId);
            Assert.Equal("Invoice", result.DocumentType);
            Assert.Equal(7, result.RetentionYears);
        }

        [Fact]
        public async Task IdentifyExpiredDocumentsAsync_FindsExpiredDocs()
        {
            var orgId = Guid.NewGuid();
            var schedule = new DocumentRetentionSchedule
            {
                OrganizationId = orgId,
                DocumentType = "Temp",
                RetentionYears = 1
            };

            _mockRepository.Setup(x => x.GetSchedulesAsync(orgId))
                .ReturnsAsync(new List<DocumentRetentionSchedule> { schedule });

            var expiredDocs = new List<Document>
            {
                new Document { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow.AddYears(-2) }
            };

            _mockDocumentRepository.Setup(x => x.FindByTypeAndDateAsync(orgId, "Temp", null, It.IsAny<DateTime>()))
                .ReturnsAsync(expiredDocs);

            var result = await _service.IdentifyExpiredDocumentsAsync(orgId);

            Assert.Single(result);
        }

        [Fact]
        public async Task ExecuteRetentionActionAsync_ArchivesDocument()
        {
            var docId = Guid.NewGuid();
            var doc = new Document { Id = docId, IsArchived = false };

            _mockDocumentRepository.Setup(x => x.GetByIdAsync(docId)).ReturnsAsync(doc);

            await _service.ExecuteRetentionActionAsync(docId, RetentionAction.Archive);

            Assert.True(doc.IsArchived);
            _mockDocumentRepository.Verify(x => x.UpdateAsync(doc), Times.Once);
        }

        [Fact]
        public async Task GetUpcomingExpiryAsync_ReturnsUpcomingDates()
        {
            var orgId = Guid.NewGuid();
            var schedule = new DocumentRetentionSchedule
            {
                RetentionYears = 1,
                DocumentType = "Report"
            };

            _mockRepository.Setup(x => x.GetSchedulesAsync(orgId))
                .ReturnsAsync(new List<DocumentRetentionSchedule> { schedule });

            var result = await _service.GetUpcomingExpiryAsync(orgId, 365);

            Assert.NotEmpty(result);
        }
    }

    public class ComplianceIntegrationTests
    {
        [Fact]
        public async Task CompleteComplianceWorkflow_AssessAndReport()
        {
            var orgId = Guid.NewGuid();

            var mockRepository = new Mock<IComplianceRepository>();
            var mockAuditRepository = new Mock<IAuditRepository>();
            var mockDocumentRepository = new Mock<IDocumentRepository>();

            var service = new ComplianceService(
                mockRepository.Object,
                mockAuditRepository.Object,
                mockDocumentRepository.Object);

            var report = await service.GenerateReportAsync(
                orgId,
                ComplianceFramework.ISO27001,
                DateTime.UtcNow.AddDays(-30),
                DateTime.UtcNow);

            Assert.NotNull(report);
            Assert.True(report.ComplianceScore >= 0);
            Assert.NotEmpty(report.Findings);
        }
    }
}
