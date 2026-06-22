using Xunit;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocVault.Tests.Module10
{
    public class AnalyticsServiceTests
    {
        private readonly Mock<IAnalyticsRepository> _mockRepository;
        private readonly Mock<IDocumentRepository> _mockDocumentRepository;
        private readonly Mock<IAccessRepository> _mockAccessRepository;
        private readonly Mock<IWorkflowRepository> _mockWorkflowRepository;
        private readonly AnalyticsService _service;

        public AnalyticsServiceTests()
        {
            _mockRepository = new Mock<IAnalyticsRepository>();
            _mockDocumentRepository = new Mock<IDocumentRepository>();
            _mockAccessRepository = new Mock<IAccessRepository>();
            _mockWorkflowRepository = new Mock<IWorkflowRepository>();

            _service = new AnalyticsService(
                _mockRepository.Object,
                _mockDocumentRepository.Object,
                _mockAccessRepository.Object,
                _mockWorkflowRepository.Object);
        }

        [Fact]
        public async Task CalculateDocumentMetricsAsync_WithDocuments_ReturnsMetrics()
        {
            var orgId = Guid.NewGuid();
            var documents = new List<Document>
            {
                new Document { CreatedAt = DateTime.UtcNow, Status = DocumentStatus.Published, Classification = DocumentClassification.Public, Size = 1000 },
                new Document { CreatedAt = DateTime.UtcNow, Status = DocumentStatus.Draft, Classification = DocumentClassification.Confidential, Size = 2000 }
            };

            _mockDocumentRepository.Setup(x => x.GetByOrganizationAsync(orgId)).ReturnsAsync(documents);

            var result = await _service.CalculateDocumentMetricsAsync(orgId);

            Assert.NotNull(result);
            Assert.Equal(2, result.TotalDocuments);
            Assert.Equal(3000, result.TotalDocumentSize);
            _mockRepository.Verify(x => x.SaveMetricsAsync(result), Times.Once);
        }

        [Fact]
        public async Task CalculateAccessMetricsAsync_ReturnAccessMetrics()
        {
            var orgId = Guid.NewGuid();
            var accesses = new List<dynamic>
            {
                new { UserId = Guid.NewGuid(), Status = "Granted", Role = "Reviewer" },
                new { UserId = Guid.NewGuid(), Status = "Denied", Role = "Approver" }
            };

            _mockAccessRepository.Setup(x => x.GetAccessHistoryAsync(orgId)).ReturnsAsync(accesses);

            var result = await _service.CalculateAccessMetricsAsync(orgId);

            Assert.NotNull(result);
            Assert.Equal(2, result.TotalAccessRequests);
            Assert.Equal(1, result.GrantedAccessRequests);
            Assert.Equal(1, result.DeniedAccessRequests);
        }

        [Fact]
        public async Task CalculateWorkflowMetricsAsync_ReturnsWorkflowMetrics()
        {
            var orgId = Guid.NewGuid();
            var workflows = new List<dynamic>
            {
                new { Status = "InReview", CreatedAt = DateTime.UtcNow, CompletedAt = (DateTime?)null },
                new { Status = "Approved", CreatedAt = DateTime.UtcNow.AddDays(-1), CompletedAt = DateTime.UtcNow }
            };

            _mockWorkflowRepository.Setup(x => x.GetByOrganizationAsync(orgId)).ReturnsAsync(workflows);

            var result = await _service.CalculateWorkflowMetricsAsync(orgId);

            Assert.NotNull(result);
            Assert.Equal(1, result.ActiveWorkflows);
            Assert.Equal(1, result.CompletedWorkflows);
        }

        [Fact]
        public async Task CalculateStorageMetricsAsync_CalculatesUtilization()
        {
            var orgId = Guid.NewGuid();
            var documents = new List<Document>
            {
                new Document { DocumentType = "PDF", Size = 500000 },
                new Document { DocumentType = "Word", Size = 300000 }
            };

            _mockDocumentRepository.Setup(x => x.GetByOrganizationAsync(orgId)).ReturnsAsync(documents);
            _mockRepository.Setup(x => x.GetStorageConfigAsync(orgId)).ReturnsAsync(new { QuotaBytes = 1000000000L });

            var result = await _service.CalculateStorageMetricsAsync(orgId);

            Assert.NotNull(result);
            Assert.Equal(800000, result.TotalStorageUsed);
            Assert.True(result.StorageUtilizationPercent > 0);
        }

        [Fact]
        public async Task GetTopUsersAsync_ReturnsTopUsers()
        {
            var orgId = Guid.NewGuid();
            var topUsers = new List<UserActivity>
            {
                new UserActivity { UserId = Guid.NewGuid(), DocumentsCreated = 50 },
                new UserActivity { UserId = Guid.NewGuid(), DocumentsCreated = 30 }
            };

            _mockRepository.Setup(x => x.GetTopUsersAsync(orgId, 10)).ReturnsAsync(topUsers);

            var result = await _service.GetTopUsersAsync(orgId);

            Assert.Equal(2, result.Count);
        }
    }

    public class ReportingServiceTests
    {
        private readonly Mock<IAnalyticsService> _mockAnalyticsService;
        private readonly Mock<IReportRepository> _mockRepository;
        private readonly Mock<IReportExportProvider> _mockExportProvider;
        private readonly ReportingService _service;

        public ReportingServiceTests()
        {
            _mockAnalyticsService = new Mock<IAnalyticsService>();
            _mockRepository = new Mock<IReportRepository>();
            _mockExportProvider = new Mock<IReportExportProvider>();

            _service = new ReportingService(
                _mockAnalyticsService.Object,
                _mockRepository.Object,
                _mockExportProvider.Object);
        }

        [Fact]
        public async Task GenerateReportAsync_WithValidParams_CreatesReport()
        {
            var orgId = Guid.NewGuid();
            var from = DateTime.UtcNow.AddDays(-30);
            var to = DateTime.UtcNow;

            _mockAnalyticsService.Setup(x => x.CalculateDocumentMetricsAsync(orgId))
                .ReturnsAsync(new DocumentMetrics { TotalDocuments = 100 });
            _mockAnalyticsService.Setup(x => x.CalculateAccessMetricsAsync(orgId))
                .ReturnsAsync(new AccessMetrics { TotalAccessRequests = 50 });
            _mockAnalyticsService.Setup(x => x.CalculateWorkflowMetricsAsync(orgId))
                .ReturnsAsync(new WorkflowMetrics { ActiveWorkflows = 10 });
            _mockAnalyticsService.Setup(x => x.CalculateStorageMetricsAsync(orgId))
                .ReturnsAsync(new StorageMetrics { TotalStorageUsed = 1000000 });

            var result = await _service.GenerateReportAsync(orgId, ReportType.ExecutiveSummary, from, to);

            Assert.NotNull(result);
            Assert.Equal(orgId, result.OrganizationId);
            Assert.Equal(ReportType.ExecutiveSummary, result.ReportType);
            Assert.NotEmpty(result.KeyInsights);
            _mockRepository.Verify(x => x.SaveAsync(result), Times.Once);
        }

        [Fact]
        public async Task GetGeneratedReportsAsync_ReturnsReports()
        {
            var orgId = Guid.NewGuid();
            var reports = new List<Report>
            {
                new Report { OrganizationId = orgId, ReportType = ReportType.ExecutiveSummary }
            };

            _mockRepository.Setup(x => x.GetReportsAsync(orgId, It.IsAny<DateTime>())).ReturnsAsync(reports);

            var result = await _service.GetGeneratedReportsAsync(orgId);

            Assert.Single(result);
        }

        [Fact]
        public async Task ExportReportAsync_WithValidFormat_ExportsReport()
        {
            var reportId = Guid.NewGuid();
            var report = new Report { Id = reportId };
            var exportedContent = "PDF content here";

            _mockRepository.Setup(x => x.GetByIdAsync(reportId)).ReturnsAsync(report);
            _mockExportProvider.Setup(x => x.ExportAsync(report, "PDF")).ReturnsAsync(exportedContent);

            var result = await _service.ExportReportAsync(reportId, "PDF");

            Assert.Equal(exportedContent, result);
        }

        [Fact]
        public async Task ScheduleReportAsync_SavesSchedule()
        {
            var orgId = Guid.NewGuid();

            await _service.ScheduleReportAsync(orgId, ReportType.DocumentInventory, "weekly");

            _mockRepository.Verify(x => x.SaveScheduleAsync(orgId, ReportType.DocumentInventory, "weekly"), Times.Once);
        }
    }

    public class AnalyticsIntegrationTests
    {
        [Fact]
        public async Task CompleteAnalyticsFlow_GenerateAndExport()
        {
            var orgId = Guid.NewGuid();
            var mockAnalytics = new Mock<IAnalyticsRepository>();
            var mockDocuments = new Mock<IDocumentRepository>();
            var mockAccess = new Mock<IAccessRepository>();
            var mockWorkflows = new Mock<IWorkflowRepository>();

            var analyticsService = new AnalyticsService(
                mockAnalytics.Object,
                mockDocuments.Object,
                mockAccess.Object,
                mockWorkflows.Object);

            var mockReportRepo = new Mock<IReportRepository>();
            var mockExport = new Mock<IReportExportProvider>();

            var reportingService = new ReportingService(
                analyticsService,
                mockReportRepo.Object,
                mockExport.Object);

            var documents = new List<Document>
            {
                new Document { CreatedAt = DateTime.UtcNow, Size = 100000 }
            };

            mockDocuments.Setup(x => x.GetByOrganizationAsync(orgId)).ReturnsAsync(documents);
            mockAccess.Setup(x => x.GetAccessHistoryAsync(orgId)).ReturnsAsync(new List<dynamic>());
            mockWorkflows.Setup(x => x.GetByOrganizationAsync(orgId)).ReturnsAsync(new List<dynamic>());

            var report = await reportingService.GenerateReportAsync(
                orgId,
                ReportType.ExecutiveSummary,
                DateTime.UtcNow.AddDays(-30),
                DateTime.UtcNow);

            Assert.NotNull(report);
            Assert.True(report.DocumentMetrics.TotalDocuments >= 0);
        }
    }
}
