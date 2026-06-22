using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// ============================================================================
// DOCVAULT MODULE 10: REPORTING & ANALYTICS
// ============================================================================
// Document statistics, usage metrics, compliance dashboards

namespace DocVault.Core.Analytics
{
    // ========================================================================
    // DATA MODELS
    // ========================================================================

    public class DocumentMetrics
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }

        public int TotalDocuments { get; set; }
        public int TotalDocumentSize { get; set; }

        public int DocumentsCreatedToday { get; set; }
        public int DocumentsCreatedThisMonth { get; set; }

        public int DocumentsArchivedToday { get; set; }
        public int DocumentsDeletedThisMonth { get; set; }

        public Dictionary<string, int> DocumentsByStatus { get; set; } = new();
        public Dictionary<string, int> DocumentsByClassification { get; set; } = new();

        public DateTime CalculatedAt { get; set; }
    }

    public class AccessMetrics
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }

        public int TotalAccessRequests { get; set; }
        public int GrantedAccessRequests { get; set; }
        public int DeniedAccessRequests { get; set; }

        public int UniqueUsersWithAccess { get; set; }
        public int PermissionsExpiredToday { get; set; }

        public Dictionary<string, int> AccessByRole { get; set; } = new();
        public List<string> MostAccessedDocuments { get; set; } = new();

        public DateTime CalculatedAt { get; set; }
    }

    public class WorkflowMetrics
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }

        public int ActiveWorkflows { get; set; }
        public int CompletedWorkflows { get; set; }
        public int RejectedWorkflows { get; set; }

        public double AvgApprovalTime { get; set; }
        public int OverdueApprovals { get; set; }

        public Dictionary<string, int> WorkflowsByStatus { get; set; } = new();
        public Dictionary<string, double> ApprovalTimeByStage { get; set; } = new();

        public DateTime CalculatedAt { get; set; }
    }

    public class StorageMetrics
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }

        public long TotalStorageUsed { get; set; } // Bytes
        public long StorageQuota { get; set; } // Bytes
        public double StorageUtilizationPercent { get; set; }

        public Dictionary<string, long> StorageByDocumentType { get; set; } = new();
        public List<string> LargestDocuments { get; set; } = new();

        public DateTime CalculatedAt { get; set; }
    }

    public class UserActivity
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid OrganizationId { get; set; }

        public int DocumentsCreated { get; set; }
        public int DocumentsViewed { get; set; }
        public int DocumentsApproved { get; set; }
        public int DocumentsShared { get; set; }

        public DateTime LastActivityAt { get; set; }
        public DateTime CalculatedAt { get; set; }
    }

    public class Report
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }

        public string ReportName { get; set; }
        public ReportType ReportType { get; set; }

        public DateTime ReportPeriodStart { get; set; }
        public DateTime ReportPeriodEnd { get; set; }
        public DateTime GeneratedAt { get; set; }

        public DocumentMetrics DocumentMetrics { get; set; }
        public AccessMetrics AccessMetrics { get; set; }
        public WorkflowMetrics WorkflowMetrics { get; set; }
        public StorageMetrics StorageMetrics { get; set; }

        public List<string> KeyInsights { get; set; } = new();
        public string ExportFormat { get; set; } // PDF, Excel, JSON
    }

    public enum ReportType
    {
        ExecutiveSummary = 0,
        DocumentInventory = 1,
        AccessControl = 2,
        WorkflowAnalysis = 3,
        StorageUtilization = 4,
        UserActivity = 5,
        Compliance = 6,
        Custom = 7
    }

    // ========================================================================
    // ANALYTICS SERVICE
    // ========================================================================

    public interface IAnalyticsService
    {
        Task<DocumentMetrics> CalculateDocumentMetricsAsync(Guid organizationId);
        Task<AccessMetrics> CalculateAccessMetricsAsync(Guid organizationId);
        Task<WorkflowMetrics> CalculateWorkflowMetricsAsync(Guid organizationId);
        Task<StorageMetrics> CalculateStorageMetricsAsync(Guid organizationId);
        Task<UserActivity> CalculateUserActivityAsync(Guid userId);
        Task<List<UserActivity>> GetTopUsersAsync(Guid organizationId, int top = 10);
    }

    public class AnalyticsService : IAnalyticsService
    {
        private readonly IAnalyticsRepository _repository;
        private readonly IDocumentRepository _documentRepository;
        private readonly IAccessRepository _accessRepository;
        private readonly IWorkflowRepository _workflowRepository;

        public AnalyticsService(
            IAnalyticsRepository repository,
            IDocumentRepository documentRepository,
            IAccessRepository accessRepository,
            IWorkflowRepository workflowRepository)
        {
            _repository = repository;
            _documentRepository = documentRepository;
            _accessRepository = accessRepository;
            _workflowRepository = workflowRepository;
        }

        public async Task<DocumentMetrics> CalculateDocumentMetricsAsync(Guid organizationId)
        {
            var documents = await _documentRepository.GetByOrganizationAsync(organizationId);
            var today = DateTime.UtcNow.Date;

            var metrics = new DocumentMetrics
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                TotalDocuments = documents.Count,
                TotalDocumentSize = documents.Sum(d => d.Size ?? 0),
                DocumentsCreatedToday = documents.Count(d => d.CreatedAt.Date == today),
                DocumentsCreatedThisMonth = documents.Count(d => d.CreatedAt.Month == today.Month && d.CreatedAt.Year == today.Year),
                DocumentsArchivedToday = documents.Count(d => d.IsArchived && d.ModifiedAt?.Date == today),
                DocumentsByStatus = documents.GroupBy(d => d.Status).ToDictionary(g => g.Key.ToString(), g => g.Count()),
                DocumentsByClassification = documents.GroupBy(d => d.Classification).ToDictionary(g => g.Key.ToString(), g => g.Count()),
                CalculatedAt = DateTime.UtcNow
            };

            await _repository.SaveMetricsAsync(metrics);
            return metrics;
        }

        public async Task<AccessMetrics> CalculateAccessMetricsAsync(Guid organizationId)
        {
            var accesses = await _accessRepository.GetAccessHistoryAsync(organizationId);
            var today = DateTime.UtcNow.Date;

            var metrics = new AccessMetrics
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                TotalAccessRequests = accesses.Count,
                GrantedAccessRequests = accesses.Count(a => a.Status == "Granted"),
                DeniedAccessRequests = accesses.Count(a => a.Status == "Denied"),
                UniqueUsersWithAccess = accesses.Select(a => a.UserId).Distinct().Count(),
                PermissionsExpiredToday = accesses.Count(a => a.ExpiryDate?.Date == today),
                AccessByRole = accesses.GroupBy(a => a.Role).ToDictionary(g => g.Key, g => g.Count()),
                CalculatedAt = DateTime.UtcNow
            };

            await _repository.SaveMetricsAsync(metrics);
            return metrics;
        }

        public async Task<WorkflowMetrics> CalculateWorkflowMetricsAsync(Guid organizationId)
        {
            var workflows = await _workflowRepository.GetByOrganizationAsync(organizationId);

            var metrics = new WorkflowMetrics
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ActiveWorkflows = workflows.Count(w => w.Status == "InReview" || w.Status == "PendingApproval"),
                CompletedWorkflows = workflows.Count(w => w.Status == "Approved"),
                RejectedWorkflows = workflows.Count(w => w.Status == "Rejected"),
                AvgApprovalTime = CalculateAverageApprovalTime(workflows),
                WorkflowsByStatus = workflows.GroupBy(w => w.Status).ToDictionary(g => g.Key, g => g.Count()),
                CalculatedAt = DateTime.UtcNow
            };

            await _repository.SaveMetricsAsync(metrics);
            return metrics;
        }

        public async Task<StorageMetrics> CalculateStorageMetricsAsync(Guid organizationId)
        {
            var documents = await _documentRepository.GetByOrganizationAsync(organizationId);
            var config = await _repository.GetStorageConfigAsync(organizationId);

            var totalUsed = documents.Sum(d => d.Size ?? 0);
            var quota = config?.QuotaBytes ?? 1000000000000; // 1TB default

            var metrics = new StorageMetrics
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                TotalStorageUsed = totalUsed,
                StorageQuota = quota,
                StorageUtilizationPercent = (double)totalUsed / quota * 100,
                StorageByDocumentType = documents.GroupBy(d => d.DocumentType).ToDictionary(g => g.Key, g => g.Sum(d => d.Size ?? 0)),
                CalculatedAt = DateTime.UtcNow
            };

            await _repository.SaveMetricsAsync(metrics);
            return metrics;
        }

        public async Task<UserActivity> CalculateUserActivityAsync(Guid userId)
        {
            var activity = new UserActivity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CalculatedAt = DateTime.UtcNow
            };

            // Populate from repositories
            return activity;
        }

        public async Task<List<UserActivity>> GetTopUsersAsync(Guid organizationId, int top = 10)
        {
            return await _repository.GetTopUsersAsync(organizationId, top);
        }

        private double CalculateAverageApprovalTime(List<dynamic> workflows)
        {
            if (!workflows.Any()) return 0;

            var times = workflows
                .Where(w => w.CompletedAt != null)
                .Select(w => (w.CompletedAt - w.CreatedAt).TotalHours)
                .ToList();

            return times.Any() ? times.Average() : 0;
        }
    }

    // ========================================================================
    // REPORTING SERVICE
    // ========================================================================

    public interface IReportingService
    {
        Task<Report> GenerateReportAsync(Guid organizationId, ReportType type, DateTime from, DateTime to);
        Task<List<Report>> GetGeneratedReportsAsync(Guid organizationId, int days = 30);
        Task<string> ExportReportAsync(Guid reportId, string format); // PDF, Excel, JSON
        Task ScheduleReportAsync(Guid organizationId, ReportType type, string schedule); // "daily", "weekly", "monthly"
    }

    public class ReportingService : IReportingService
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly IReportRepository _repository;
        private readonly IReportExportProvider _exportProvider;

        public ReportingService(
            IAnalyticsService analyticsService,
            IReportRepository repository,
            IReportExportProvider exportProvider)
        {
            _analyticsService = analyticsService;
            _repository = repository;
            _exportProvider = exportProvider;
        }

        public async Task<Report> GenerateReportAsync(Guid organizationId, ReportType type, DateTime from, DateTime to)
        {
            var documentMetrics = await _analyticsService.CalculateDocumentMetricsAsync(organizationId);
            var accessMetrics = await _analyticsService.CalculateAccessMetricsAsync(organizationId);
            var workflowMetrics = await _analyticsService.CalculateWorkflowMetricsAsync(organizationId);
            var storageMetrics = await _analyticsService.CalculateStorageMetricsAsync(organizationId);

            var report = new Report
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ReportName = $"{type} Report - {DateTime.UtcNow:yyyy-MM-dd}",
                ReportType = type,
                ReportPeriodStart = from,
                ReportPeriodEnd = to,
                GeneratedAt = DateTime.UtcNow,
                DocumentMetrics = documentMetrics,
                AccessMetrics = accessMetrics,
                WorkflowMetrics = workflowMetrics,
                StorageMetrics = storageMetrics,
                KeyInsights = GenerateInsights(documentMetrics, accessMetrics, workflowMetrics, storageMetrics)
            };

            await _repository.SaveAsync(report);
            return report;
        }

        public async Task<List<Report>> GetGeneratedReportsAsync(Guid organizationId, int days = 30)
        {
            var from = DateTime.UtcNow.AddDays(-days);
            return await _repository.GetReportsAsync(organizationId, from);
        }

        public async Task<string> ExportReportAsync(Guid reportId, string format)
        {
            var report = await _repository.GetByIdAsync(reportId);
            if (report == null) return null;

            return await _exportProvider.ExportAsync(report, format);
        }

        public async Task ScheduleReportAsync(Guid organizationId, ReportType type, string schedule)
        {
            // Store schedule in database for processing
            await _repository.SaveScheduleAsync(organizationId, type, schedule);
        }

        private List<string> GenerateInsights(DocumentMetrics docs, AccessMetrics access, WorkflowMetrics workflows, StorageMetrics storage)
        {
            var insights = new List<string>();

            if (docs.DocumentsCreatedThisMonth > 100)
                insights.Add($"High document creation activity: {docs.DocumentsCreatedThisMonth} new documents this month");

            if (storage.StorageUtilizationPercent > 80)
                insights.Add($"Storage quota approaching: {storage.StorageUtilizationPercent:F1}% utilized");

            if (workflows.OverdueApprovals > 0)
                insights.Add($"Attention needed: {workflows.OverdueApprovals} overdue approvals");

            if (access.UniqueUsersWithAccess > 0 && access.DeniedAccessRequests > access.TotalAccessRequests * 0.1)
                insights.Add("Note: High percentage of access denials - review permission policies");

            return insights;
        }
    }

    // ========================================================================
    // REPOSITORY & PROVIDER INTERFACES
    // ========================================================================

    public interface IAnalyticsRepository
    {
        Task SaveMetricsAsync(object metrics);
        Task<dynamic> GetStorageConfigAsync(Guid organizationId);
        Task<List<UserActivity>> GetTopUsersAsync(Guid organizationId, int top);
    }

    public interface IReportRepository
    {
        Task SaveAsync(Report report);
        Task<Report> GetByIdAsync(Guid id);
        Task<List<Report>> GetReportsAsync(Guid organizationId, DateTime from);
        Task SaveScheduleAsync(Guid organizationId, ReportType type, string schedule);
    }

    public interface IReportExportProvider
    {
        Task<string> ExportAsync(Report report, string format);
    }

    public interface IAccessRepository
    {
        Task<List<dynamic>> GetAccessHistoryAsync(Guid organizationId);
    }

    public interface IWorkflowRepository
    {
        Task<List<dynamic>> GetByOrganizationAsync(Guid organizationId);
    }
}
