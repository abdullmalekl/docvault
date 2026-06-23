using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// ============================================================================
// DOCVAULT MODULE 8: COMPLIANCE & AUDIT LOGGING
// ============================================================================
// Compliance tracking, audit trails, regulatory reporting

namespace DocVault.Core.Compliance
{
    // ========================================================================
    // DATA MODELS
    // ========================================================================

    public class AuditLog
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid? DocumentId { get; set; }

        public AuditActionType ActionType { get; set; }
        public string EntityType { get; set; }
        public string EntityId { get; set; }

        public string Description { get; set; }
        public DateTime PerformedAt { get; set; }

        public string IpAddress { get; set; }
        public string UserAgent { get; set; }

        public Dictionary<string, object> ChangeDetails { get; set; } = new();
        public string Result { get; set; } // Success, Failure, Partial
    }

    public class ComplianceReport
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }

        public string ReportName { get; set; }
        public ComplianceFramework Framework { get; set; }

        public DateTime ReportPeriodStart { get; set; }
        public DateTime ReportPeriodEnd { get; set; }
        public DateTime GeneratedAt { get; set; }

        public int TotalDocuments { get; set; }
        public int ControlsAssessed { get; set; }
        public int ComplianceScore { get; set; }

        public List<ComplianceFinding> Findings { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public class ComplianceFinding
    {
        public Guid Id { get; set; }
        public string ControlId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }

        public FindingSeverity Severity { get; set; }
        public FindingStatus Status { get; set; }

        public string Remediation { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }

    public class DocumentRetentionSchedule
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }

        public string DocumentType { get; set; }
        public int RetentionYears { get; set; }

        public RetentionAction ActionOnExpiry { get; set; }
        public bool NotifyBeforeExpiry { get; set; }
        public int NotifyDaysBefore { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class DataClassificationPolicy
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }

        public string PolicyName { get; set; }
        public string Description { get; set; }

        public Dictionary<string, List<string>> ClassificationRules { get; set; } = new();
        public List<string> SensitiveKeywords { get; set; } = new();

        public bool IsActive { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }

    public enum AuditActionType
    {
        Create = 0,
        Read = 1,
        Update = 2,
        Delete = 3,
        Share = 4,
        ChangePermission = 5,
        DownloadExport = 6,
        Archive = 7,
        Restore = 8,
        Approve = 9,
        Reject = 10
    }

    public enum ComplianceFramework
    {
        GDPR = 0,
        SOC2 = 1,
        ISO27001 = 2,
        HIPAA = 3,
        PCI_DSS = 4,
        Custom = 5
    }

    public enum FindingSeverity
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }

    public enum FindingStatus
    {
        Open = 0,
        InProgress = 1,
        Resolved = 2,
        Accepted = 3,
        Deferred = 4
    }

    public enum RetentionAction
    {
        Archive = 0,
        Delete = 1,
        Notify = 2,
        ArchiveThenDelete = 3
    }

    // ========================================================================
    // AUDIT SERVICE
    // ========================================================================

    public interface IAuditService
    {
        Task LogActionAsync(Guid userId, AuditActionType action, string entityType, Guid? entityId, Dictionary<string, object> details = null);
        Task<List<AuditLog>> GetAuditTrailAsync(Guid organizationId, DateTime? from = null, DateTime? to = null, int take = 1000);
        Task<List<AuditLog>> GetUserActivityAsync(Guid userId, DateTime? from = null, DateTime? to = null);
        Task<Dictionary<AuditActionType, int>> GetActionSummaryAsync(Guid organizationId, DateTime? from = null, DateTime? to = null);
        Task<bool> IsAccessAuthorizedAsync(Guid userId, Guid documentId, AuditActionType action);
    }

    public class AuditService 
    {
        private readonly IAuditRepository _repository;
        private readonly IAccessControlRepository _accessRepository;
        private readonly ILogger _logger;

        public AuditService(
            IAuditRepository repository,
            IAccessControlRepository accessRepository,
            ILogger logger)
        {
            _repository = repository;
            _accessRepository = accessRepository;
            _logger = logger;
        }

        public async Task LogActionAsync(Guid userId, AuditActionType action, string entityType, Guid? entityId, Dictionary<string, object> details = null)
        {
            try
            {
                var log = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ActionType = action,
                    EntityType = entityType,
                    EntityId = entityId?.ToString(),
                    PerformedAt = DateTime.UtcNow,
                    ChangeDetails = details ?? new(),
                    Result = "Success"
                };

                await _repository.CreateAsync(log);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to log audit action: {ex.Message}");
            }
        }

        public async Task<List<AuditLog>> GetAuditTrailAsync(Guid organizationId, DateTime? from = null, DateTime? to = null, int take = 1000)
        {
            var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
            var toDate = to ?? DateTime.UtcNow;

            return await _repository.GetByOrganizationAsync(organizationId, fromDate, toDate, take);
        }

        public async Task<List<AuditLog>> GetUserActivityAsync(Guid userId, DateTime? from = null, DateTime? to = null)
        {
            var fromDate = from ?? DateTime.UtcNow.AddDays(-90);
            var toDate = to ?? DateTime.UtcNow;

            return await _repository.GetByUserAsync(userId, fromDate, toDate);
        }

        public async Task<Dictionary<AuditActionType, int>> GetActionSummaryAsync(Guid organizationId, DateTime? from = null, DateTime? to = null)
        {
            var logs = await GetAuditTrailAsync(organizationId, from, to);
            return logs
                .GroupBy(l => l.ActionType)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public async Task<bool> IsAccessAuthorizedAsync(Guid userId, Guid documentId, AuditActionType action)
        {
            var authorized = await _accessRepository.CheckAccessAsync(userId, documentId, action.ToString());
            return authorized;
        }
    }

    // ========================================================================
    // COMPLIANCE SERVICE
    // ========================================================================

    public interface IComplianceService
    {
        Task<ComplianceReport> GenerateReportAsync(Guid organizationId, ComplianceFramework framework, DateTime from, DateTime to);
        Task<int> CalculateComplianceScoreAsync(Guid organizationId, ComplianceFramework framework);
        Task<List<ComplianceFinding>> AssessControlsAsync(Guid organizationId, ComplianceFramework framework);
        Task<bool> ValidateDocumentClassificationAsync(Guid documentId);
        Task UpdateFindingStatusAsync(Guid findingId, FindingStatus status, string notes = null);
    }

    public class ComplianceService : IComplianceService
    {
        private readonly IComplianceRepository _repository;
        private readonly IAuditRepository _auditRepository;
        private readonly IDocumentRepository _documentRepository;

        public ComplianceService(
            IComplianceRepository repository,
            IAuditRepository auditRepository,
            IDocumentRepository documentRepository)
        {
            _repository = repository;
            _auditRepository = auditRepository;
            _documentRepository = documentRepository;
        }

        public async Task<ComplianceReport> GenerateReportAsync(Guid organizationId, ComplianceFramework framework, DateTime from, DateTime to)
        {
            var findings = await AssessControlsAsync(organizationId, framework);
            var score = CalculateScore(findings);

            var report = new ComplianceReport
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ReportName = $"{framework} Compliance Report",
                Framework = framework,
                ReportPeriodStart = from,
                ReportPeriodEnd = to,
                GeneratedAt = DateTime.UtcNow,
                ComplianceScore = score,
                Findings = findings,
                Recommendations = GenerateRecommendations(findings)
            };

            await _repository.SaveReportAsync(report);
            return report;
        }

        public async Task<int> CalculateComplianceScoreAsync(Guid organizationId, ComplianceFramework framework)
        {
            var findings = await AssessControlsAsync(organizationId, framework);
            return CalculateScore(findings);
        }

        public async Task<List<ComplianceFinding>> AssessControlsAsync(Guid organizationId, ComplianceFramework framework)
        {
            var controls = GetFrameworkControls(framework);
            var findings = new List<ComplianceFinding>();

            foreach (var control in controls)
            {
                var finding = await EvaluateControlAsync(organizationId, control);
                if (finding != null) findings.Add(finding);
            }

            return findings;
        }

        public async Task<bool> ValidateDocumentClassificationAsync(Guid documentId)
        {
            var doc = await _documentRepository.GetByIdAsync(documentId);
            if (doc == null) return false;

            var policy = await _repository.GetActiveClassificationPolicyAsync(doc.OrganizationId);
            if (policy == null) return true;

            // Check if document contains sensitive keywords
            var content = doc.Description + " " + string.Join(" ", doc.Tags ?? new List<string>());
            var hasSensitiveKeywords = policy.SensitiveKeywords.Any(k => content.Contains(k, StringComparison.OrdinalIgnoreCase));

            if (hasSensitiveKeywords && doc.Classification == DocumentClassification.Public)
                return false;

            return true;
        }

        public async Task UpdateFindingStatusAsync(Guid findingId, FindingStatus status, string notes = null)
        {
            await _repository.UpdateFindingStatusAsync(findingId, status, notes);
        }

        private int CalculateScore(List<ComplianceFinding> findings)
        {
            if (!findings.Any()) return 100;

            var criticalCount = findings.Count(f => f.Severity == FindingSeverity.Critical);
            var highCount = findings.Count(f => f.Severity == FindingSeverity.High);
            var mediumCount = findings.Count(f => f.Severity == FindingSeverity.Medium);

            var deduction = (criticalCount * 30) + (highCount * 15) + (mediumCount * 5);
            return Math.Max(0, 100 - deduction);
        }

        private List<string> GenerateRecommendations(List<ComplianceFinding> findings)
        {
            var recommendations = new List<string>();

            foreach (var finding in findings.Where(f => f.Severity == FindingSeverity.Critical || f.Severity == FindingSeverity.High))
            {
                recommendations.Add($"Address {finding.Title}: {finding.Remediation}");
            }

            return recommendations;
        }

        private List<string> GetFrameworkControls(ComplianceFramework framework)
        {
            return framework switch
            {
                ComplianceFramework.GDPR => new List<string> { "DataMinimization", "Consent", "RightToBeDeleted", "DataPortability" },
                ComplianceFramework.SOC2 => new List<string> { "AccessControl", "ChangeManagement", "Monitoring", "Encryption" },
                ComplianceFramework.ISO27001 => new List<string> { "IncidentResponse", "RiskAssessment", "AssetManagement", "UserAccess" },
                _ => new List<string>()
            };
        }

        private async Task<ComplianceFinding> EvaluateControlAsync(Guid organizationId, string control)
        {
            // Simplified control evaluation
            return new ComplianceFinding
            {
                Id = Guid.NewGuid(),
                ControlId = control,
                Title = control,
                Description = $"Assessment of {control} control",
                Severity = FindingSeverity.Medium,
                Status = FindingStatus.Open
            };
        }
    }

    // ========================================================================
    // DATA RETENTION SERVICE
    // ========================================================================

    public interface IDataRetentionService
    {
        Task<DocumentRetentionSchedule> CreateScheduleAsync(Guid organizationId, string documentType, int retentionYears);
        Task<List<Guid>> IdentifyExpiredDocumentsAsync(Guid organizationId);
        Task ExecuteRetentionActionAsync(Guid documentId, RetentionAction action);
        Task<List<DateTime>> GetUpcomingExpiryAsync(Guid organizationId, int daysAhead = 30);
    }

    public class DataRetentionService : IDataRetentionService
    {
        private readonly IRetentionRepository _repository;
        private readonly IDocumentRepository _documentRepository;

        public DataRetentionService(
            IRetentionRepository repository,
            IDocumentRepository documentRepository)
        {
            _repository = repository;
            _documentRepository = documentRepository;
        }

        public async Task<DocumentRetentionSchedule> CreateScheduleAsync(Guid organizationId, string documentType, int retentionYears)
        {
            var schedule = new DocumentRetentionSchedule
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                DocumentType = documentType,
                RetentionYears = retentionYears,
                ActionOnExpiry = RetentionAction.Archive,
                NotifyBeforeExpiry = true,
                NotifyDaysBefore = 30,
                CreatedAt = DateTime.UtcNow
            };

            await _repository.CreateAsync(schedule);
            return schedule;
        }

        public async Task<List<Guid>> IdentifyExpiredDocumentsAsync(Guid organizationId)
        {
            var schedules = await _repository.GetSchedulesAsync(organizationId);
            var expiredIds = new List<Guid>();

            foreach (var schedule in schedules)
            {
                var expiryDate = DateTime.UtcNow.AddYears(-schedule.RetentionYears);
                var expired = await _documentRepository.FindByTypeAndDateAsync(
                    organizationId,
                    schedule.DocumentType,
                    null,
                    expiryDate);

                expiredIds.AddRange(expired.Select(d => d.Id));
            }

            return expiredIds;
        }

        public async Task ExecuteRetentionActionAsync(Guid documentId, RetentionAction action)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document == null) return;

            switch (action)
            {
                case RetentionAction.Archive:
                    document.IsArchived = true;
                    await _documentRepository.UpdateAsync(document);
                    break;

                case RetentionAction.Delete:
                    await _documentRepository.DeleteAsync(documentId);
                    break;

                case RetentionAction.ArchiveThenDelete:
                    document.IsArchived = true;
                    document.ScheduledDeletionDate = DateTime.UtcNow.AddDays(30);
                    await _documentRepository.UpdateAsync(document);
                    break;
            }
        }

        public async Task<List<DateTime>> GetUpcomingExpiryAsync(Guid organizationId, int daysAhead = 30)
        {
            var schedules = await _repository.GetSchedulesAsync(organizationId);
            var upcomingDates = new List<DateTime>();

            foreach (var schedule in schedules)
            {
                var threshold = DateTime.UtcNow.AddDays(daysAhead);
                var expiryDate = DateTime.UtcNow.AddYears(-schedule.RetentionYears);

                if (expiryDate <= threshold && expiryDate > DateTime.UtcNow)
                    upcomingDates.Add(expiryDate);
            }

            return upcomingDates.OrderBy(d => d).ToList();
        }
    }

    // ========================================================================
    // REPOSITORY INTERFACES
    // ========================================================================

    public interface IAuditRepository
    {
        Task CreateAsync(AuditLog log);
        Task<List<AuditLog>> GetByOrganizationAsync(Guid orgId, DateTime from, DateTime to, int take);
        Task<List<AuditLog>> GetByUserAsync(Guid userId, DateTime from, DateTime to);
    }

    public interface IComplianceRepository
    {
        Task SaveReportAsync(ComplianceReport report);
        Task UpdateFindingStatusAsync(Guid findingId, FindingStatus status, string notes);
        Task<DataClassificationPolicy> GetActiveClassificationPolicyAsync(Guid organizationId);
    }

    public interface IRetentionRepository
    {
        Task CreateAsync(DocumentRetentionSchedule schedule);
        Task<List<DocumentRetentionSchedule>> GetSchedulesAsync(Guid organizationId);
    }

    public interface IAccessControlRepository
    {
        Task<bool> CheckAccessAsync(Guid userId, Guid documentId, string action);
    }

    public interface ILogger
    {
        void LogError(string message);
        void LogInfo(string message);
    }
}
