// =====================================================
// DocVault.Models.cs
// UNIFIED MODEL DEFINITIONS
// Single source of truth for all entity, DTO, and interface definitions
// =====================================================

using System;
using System.Collections.Generic;

namespace DocVault.Core.Document
{
    // =====================================================
    // CORE ENTITIES (Organization Structure)
    // =====================================================

    public class Organization
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public string Industry { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class User
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public string PasswordHash { get; set; }
        public bool IsActive { get; set; }
        public bool IsSuperAdmin { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
    }

    public class Document
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid CreatedByUserId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string DocumentType { get; set; }
        public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
        public DocumentClassification Classification { get; set; }
        public string FileUrl { get; set; }
        public long FileSizeBytes { get; set; }
        public bool IsArchived { get; set; }
        public List<string> Tags { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PublishedAt { get; set; }
    }

    public class Department
    {
        public int DepartmentID { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public int BranchID { get; set; }
        public Branch Branch { get; set; }
        public int? ManagerUserID { get; set; }
        public User Manager { get; set; }
        public bool IsActive { get; set; }
        public List<Unit> Units { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class Branch
    {
        public int BranchID { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public int? HeadUserID { get; set; }
        public User Head { get; set; }
        public int? ParentBranchID { get; set; }
        public bool IsHeadquarters { get; set; }
        public bool IsActive { get; set; }
        public List<Department> Departments { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class Unit
    {
        public int UnitID { get; set; }
        public string Name { get; set; }
        public int DepartmentID { get; set; }
        public Department Department { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // =====================================================
    // ACCESS CONTROL ENTITIES
    // =====================================================

    public class DocumentPermission
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid DocumentId { get; set; }
        public string AccessLevel { get; set; }  // "Read", "Write", "Admin"
        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
        public DateTime? RevokedAt { get; set; }
    }

    public class DocumentRole
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public string Name { get; set; }
        public List<string> Permissions { get; set; } = new();
        public bool IsSystem { get; set; }
    }

    public class Role
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<Permission> Permissions { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class Permission
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Resource { get; set; }
        public string Action { get; set; }
    }

    // =====================================================
    // WORKFLOW & APPROVAL ENTITIES
    // =====================================================

    public class DocumentWorkflow
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid DocumentId { get; set; }
        public string Name { get; set; }
        public WorkflowStatus Status { get; set; }
        public List<WorkflowStep> Steps { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class WorkflowStep
    {
        public Guid Id { get; set; }
        public int StepNumber { get; set; }
        public string Name { get; set; }
        public string Action { get; set; }
        public Guid AssignedToUserId { get; set; }
        public StepStatus Status { get; set; }
    }

    public class ApprovalTask
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public Guid AssignedToUserId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }

    // =====================================================
    // AUDIT & COMPLIANCE ENTITIES
    // =====================================================

    public class AuditLog
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid UserId { get; set; }
        public string Action { get; set; }
        public string ResourceType { get; set; }
        public Guid ResourceId { get; set; }
        public string Details { get; set; }
        public string IpAddress { get; set; }
        public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
    }

    public class SecurityAlert
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public string AlertType { get; set; }
        public string Severity { get; set; }
        public string Message { get; set; }
        public DateTime AlertedAt { get; set; }
        public bool IsResolved { get; set; }
    }

    // =====================================================
    // NOTIFICATION ENTITIES
    // =====================================================

    public class Notification
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Type { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReadAt { get; set; }
    }

    public class EmailNotification
    {
        public Guid Id { get; set; }
        public string RecipientEmail { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public EmailStatus Status { get; set; } = EmailStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SentAt { get; set; }
    }

    public class WebhookSubscription
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public string EventType { get; set; }
        public string EndpointUrl { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class WebhookEvent
    {
        public Guid Id { get; set; }
        public string EventType { get; set; }
        public string EndpointUrl { get; set; }
        public DateTime OccurredAt { get; set; }
    }

    // =====================================================
    // SEARCH & ANALYTICS ENTITIES
    // =====================================================

    public class SearchAnalytic
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid UserId { get; set; }
        public string QueryText { get; set; }
        public int ResultCount { get; set; }
        public DateTime PerformedAt { get; set; }
    }

    public class Report
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public ReportType Type { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    // =====================================================
    // INTEGRATION ENTITIES
    // =====================================================

    public class ExternalIntegration
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public string IntegrationType { get; set; }
        public string Credentials { get; set; }  // Encrypted
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class MigrationLog
    {
        public Guid Id { get; set; }
        public string MigrationName { get; set; }
        public DateTime ExecutedAt { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class EncryptionKey
    {
        public Guid Id { get; set; }
        public string KeyName { get; set; }
        public string Algorithm { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
    }

    // =====================================================
    // ENUMS
    // =====================================================

    public enum DocumentStatus
    {
        Draft,
        InReview,
        Approved,
        Published,
        Archived
    }

    public enum DocumentClassification
    {
        Public,
        Internal,
        Confidential,
        Secret
    }

    public enum WorkflowStatus
    {
        Draft,
        Active,
        Completed,
        Archived
    }

    public enum StepStatus
    {
        NotStarted,
        InProgress,
        Completed,
        Skipped,
        Rejected
    }

    public enum ApprovalStatus
    {
        Pending,
        Approved,
        Rejected,
        Cancelled
    }

    public enum EmailStatus
    {
        Pending,
        Sent,
        Failed,
        Bounced
    }

    public enum ReportType
    {
        ActivitySummary,
        DocumentMetrics,
        AccessAnalysis,
        ComplianceStatus,
        SecurityAudit
    }

    // =====================================================
    // INTERFACES (Repositories)
    // =====================================================

    public interface IUserRepository
    {
        Task<User> GetByIdAsync(Guid id);
        Task<User> GetByEmailAsync(string email);
        Task<List<User>> GetByOrganizationAsync(Guid orgId);
        Task CreateAsync(User user);
        Task UpdateAsync(User user);
        Task DeleteAsync(Guid id);
    }

    public interface IDocumentRepository
    {
        Task<Document> GetByIdAsync(Guid id);
        Task<List<Document>> GetByOrganizationAsync(Guid orgId);
        Task<List<Document>> FindByTypeAndDateAsync(Guid orgId, string type, DateTime? from, DateTime? to);
        Task CreateAsync(Document doc);
        Task UpdateAsync(Document doc);
        Task DeleteAsync(Guid id);
    }

    public interface IPermissionRepository
    {
        Task<List<DocumentPermission>> GetByDocumentAsync(Guid docId);
        Task<bool> CheckAccessAsync(Guid userId, Guid documentId, string action);
        Task GrantAccessAsync(Guid userId, Guid documentId, string level);
        Task RevokeAccessAsync(Guid userId, Guid documentId);
    }

    public interface IWorkflowRepository
    {
        Task<DocumentWorkflow> GetByIdAsync(Guid id);
        Task<List<DocumentWorkflow>> GetByOrganizationAsync(Guid orgId);
        Task CreateAsync(DocumentWorkflow workflow);
        Task UpdateAsync(DocumentWorkflow workflow);
        Task<DocumentWorkflow> GetTemplateAsync(Guid templateId);
    }

    public interface IAuditRepository
    {
        Task CreateAsync(AuditLog log);
        Task<List<AuditLog>> GetByOrganizationAsync(Guid orgId, DateTime from, DateTime to, int take);
        Task<List<AuditLog>> GetByUserAsync(Guid userId, DateTime from, DateTime to);
    }

    public interface INotificationRepository
    {
        Task CreateAsync(Notification notification);
        Task<Notification> GetByIdAsync(Guid id);
        Task<List<Notification>> GetByUserAsync(Guid userId);
        Task<List<Notification>> GetUnreadAsync(Guid userId);
        Task UpdateAsync(Notification notification);
        Task DeleteAsync(Guid id);
        Task MarkAllAsReadAsync(Guid userId);
    }

    public interface IEmailRepository
    {
        Task CreateAsync(EmailNotification notification);
        Task<List<EmailNotification>> GetSentEmailsAsync(Guid organizationId, DateTime from);
        Task<int> GetFailedCountAsync(Guid organizationId);
        Task<List<EmailNotification>> GetFailedEmailsAsync(Guid organizationId);
    }

    public interface ISearchRepository
    {
        Task<List<SearchResult>> SearchAsync(SearchQuery query);
        Task<int> CountByClassificationAsync(DocumentClassification classification, SearchQuery baseQuery);
        Task<int> CountByStatusAsync(DocumentStatus status, SearchQuery baseQuery);
        Task<Dictionary<string, int>> GetPopularTagsAsync(SearchQuery baseQuery, int topN);
        Task<List<string>> GetAutocompleteAsync(string prefix, Guid organizationId);
    }

    public interface IReportRepository
    {
        Task SaveAsync(Report report);
        Task<Report> GetByIdAsync(Guid id);
        Task<List<Report>> GetReportsAsync(Guid organizationId, DateTime from);
        Task SaveScheduleAsync(Guid organizationId, ReportType type, string schedule);
    }

    public interface IAccessControlRepository
    {
        Task<List<DocumentPermission>> GetDocumentPermissionsAsync(Guid documentId);
        Task GrantPermissionAsync(Guid userId, Guid documentId, string level);
        Task RevokePermissionAsync(Guid userId, Guid documentId);
    }

        Task<bool> SendAsync(string recipientEmail, string subject, string body, string htmlBody);
        Task<bool> SendWithAttachmentsAsync(string recipientEmail, string subject, string body, List<EmailAttachment> attachments);
    }

    public interface IWebhookDeliveryProvider
    {
        Task DeliverAsync(WebhookEvent webhookEvent, string authToken);
    }

    public interface IPasswordHasher
    {
        string HashPassword(string password);
        bool VerifyPassword(string password, string hash);
    }

    public interface IDbContext
    {
        Task SaveChangesAsync();
    }

    // =====================================================
    // DTOs (Data Transfer Objects)
    // =====================================================

    public class CreateDocumentRequest
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string DocumentType { get; set; }
        public DocumentClassification Classification { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    public class SearchQuery
    {
        public string QueryText { get; set; }
        public List<DocumentClassification>? Classifications { get; set; }
        public List<DocumentStatus>? Statuses { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class SearchResult
    {
        public Guid DocumentId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DocumentStatus Status { get; set; }
        public DocumentClassification Classification { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AuthenticationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Token { get; set; }
        public User User { get; set; }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new();

        public void AddError(string error)
        {
            IsValid = false;
            Errors.Add(error);
        }
    }

    public class EmailAttachment
    {
        public string FileName { get; set; }
        public byte[] FileContent { get; set; }
    }

    public class ApprovalDecisionRequest
    {
        public Guid ApprovalTaskId { get; set; }
        public bool IsApproved { get; set; }
        public string Comments { get; set; }
    }

    public class CreateUserRequest
    {
        public string Email { get; set; }
        public string FullName { get; set; }
        public string Password { get; set; }
    }

    public class CreateBranchRequest
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public bool IsHeadquarters { get; set; }
    }

    public class CreateDepartmentRequest
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public int BranchID { get; set; }
    }

    public class CreateUnitRequest
    {
        public string Name { get; set; }
        public int DepartmentID { get; set; }
    }

    public class BranchResponse
    {
        public int BranchID { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public string Location { get; set; }
        public bool IsHeadquarters { get; set; }
    }

    public class AccessControlResponse
    {
        public Guid DocumentId { get; set; }
        public List<UserAccessGrant> UserGrants { get; set; }
    }

    public class UserAccessGrant
    {
        public Guid UserId { get; set; }
        public string UserEmail { get; set; }
        public string AccessLevel { get; set; }
    }

    public class AccessibleDepartment
    {
        public int DepartmentID { get; set; }
        public string Name { get; set; }
    }

    public class AccessReport
    {
        public DateTime ReportDate { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public List<AccessMetrics> Metrics { get; set; }
    }

    public class AccessMetrics
    {
        public string MetricName { get; set; }
        public int Value { get; set; }
    }

    public class AnomalousAccess
    {
        public Guid UserId { get; set; }
        public string UserEmail { get; set; }
        public string AnomalyType { get; set; }
        public string Details { get; set; }
        public DateTime DetectedAt { get; set; }
    }

    public class ComplianceReport
    {
        public DateTime GeneratedAt { get; set; }
        public string Status { get; set; }
        public List<ComplianceFinding> Findings { get; set; }
    }

    public class ComplianceFinding
    {
        public string FindingType { get; set; }
        public string Severity { get; set; }
        public string Description { get; set; }
        public string Remediation { get; set; }
    }

    public class DataRetentionPolicy
    {
        public Guid Id { get; set; }
        public string PolicyName { get; set; }
        public int RetentionDays { get; set; }
        public bool IsActive { get; set; }
    }

    public class DataClassificationPolicy
    {
        public Guid Id { get; set; }
        public DocumentClassification Classification { get; set; }
        public string RetentionDays { get; set; }
    }

    public class DelegatePermissionsRequest
    {
        public Guid UserIdToDelegate { get; set; }
        public Guid DocumentId { get; set; }
        public string AccessLevel { get; set; }
    }

    public class CustomRole
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public List<string> Permissions { get; set; }
    }

    public class CreateCustomRoleRequest
    {
        public string Name { get; set; }
        public List<string> Permissions { get; set; }
    }

    public class LoginHistory
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public DateTime LoginTime { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
    }
}
