using Microsoft.Extensions.DependencyInjection;
using DocVault.Core.Auth;
using DocVault.Core.Document;
using DocVault.Core.Access;
using DocVault.Core.Workflow;
using DocVault.Core.Search;
using DocVault.Core.Compliance;
using DocVault.Core.Integration;
using DocVault.Core.Analytics;
using DocVault.Core.Migration;
using DocVault.Core.Security;
using DocVault.Data;
using System;

namespace DocVault.Api
{
    public static class ServiceConfiguration
    {
        public static IServiceCollection AddDocVaultServices(this IServiceCollection services)
        {
            // ====================================================================
            // MODULE 1: AUTHENTICATION & AUTHORIZATION
            // ====================================================================
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IOrgRepository, OrgRepository>();

            // ====================================================================
            // MODULE 2: DOCUMENT MANAGEMENT
            // ====================================================================
            services.AddScoped<IDocumentService, DocumentService>();
            services.AddScoped<IDocumentRepository, DocumentRepository>();

            // ====================================================================
            // MODULE 3: RBAC & PERMISSIONS
            // ====================================================================
            services.AddScoped<IAccessControlService, AccessControlService>();
            services.AddScoped<IPermissionRepository, PermissionRepository>();
            services.AddScoped<IRoleRepository, RoleRepository>();

            // ====================================================================
            // MODULE 4-5: VERSIONING & ACCESS CONTROL
            // ====================================================================
            services.AddScoped<IDocumentAccessControlService, DocumentAccessControlService>();
            services.AddScoped<IDataRetentionService, DataRetentionService>();

            // ====================================================================
            // MODULE 6: WORKFLOWS
            // ====================================================================
            services.AddScoped<IDocumentWorkflowService, DocumentWorkflowService>();
            services.AddScoped<IWorkflowRepository, WorkflowRepository>();

            // ====================================================================
            // MODULE 7: SEARCH & ANALYTICS
            // ====================================================================
            services.AddScoped<IDocumentSearchService, DocumentSearchService>();
            services.AddScoped<ISearchRepository, SearchRepository>();
            services.AddScoped<ISearchIndexService, SearchIndexService>();

            // ====================================================================
            // MODULE 8: COMPLIANCE & AUDIT
            // ====================================================================
            services.AddScoped<IAuditService, AuditService>();
            services.AddScoped<IComplianceService, ComplianceService>();
            services.AddScoped<IAuditRepository, AuditRepository>();

            // ====================================================================
            // MODULE 9: INTEGRATIONS & NOTIFICATIONS
            // ====================================================================
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IWebhookService, WebhookService>();
            services.AddScoped<IIntegrationService, IntegrationService>();

            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddScoped<IEmailRepository, EmailRepository>();
            services.AddScoped<IEmailProvider, MockEmailProvider>();
            services.AddScoped<IWebhookDeliveryProvider, MockWebhookDeliveryProvider>();

            // ====================================================================
            // MODULE 10: REPORTING & ANALYTICS
            // ====================================================================
            services.AddScoped<IAnalyticsService, AnalyticsService>();
            services.AddScoped<IReportingService, ReportingService>();
            services.AddScoped<IReportRepository, ReportRepository>();

            // ====================================================================
            // MODULE 12: SECURITY & ENCRYPTION
            // ====================================================================
            services.AddScoped<IEncryptionService, EncryptionService>();
            services.AddScoped<IThreatDetectionService, ThreatDetectionService>();

            return services;
        }
    }

    // ============================================================================
    // MOCK REPOSITORIES (يمكن استبدالها بـ real SQL repositories)
    // ============================================================================

    public class OrgRepository : IOrgRepository
    {
        public async Task CreateAsync(Organization org)
        {
            if (MockDatabase.Organizations.Find(o => o.Id == org.Id) == null)
            {
                MockDatabase.Organizations.Add(org);
            }
        }

        public async Task<Organization> GetByIdAsync(Guid id)
        {
            return MockDatabase.Organizations.Find(o => o.Id == id);
        }

        public async Task<List<Organization>> GetAllAsync()
        {
            return MockDatabase.Organizations;
        }
    }

    public class RoleRepository : IRoleRepository
    {
        public async Task<List<DocumentRole>> GetByOrganizationAsync(Guid orgId)
        {
            return MockDatabase.DocumentRoles.FindAll(r => r.OrganizationId == orgId);
        }

        public async Task<DocumentRole> GetByNameAsync(Guid orgId, string name)
        {
            return MockDatabase.DocumentRoles.Find(r => r.OrganizationId == orgId && r.RoleName == name);
        }

        public async Task CreateAsync(DocumentRole role)
        {
            MockDatabase.DocumentRoles.Add(role);
        }
    }

    // ============================================================================
    // MISSING INTERFACE IMPLEMENTATIONS
    // ============================================================================

    public interface IOrgRepository
    {
        Task CreateAsync(Organization org);
        Task<Organization> GetByIdAsync(Guid id);
        Task<List<Organization>> GetAllAsync();
    }

    public interface IRoleRepository
    {
        Task<List<DocumentRole>> GetByOrganizationAsync(Guid orgId);
        Task<DocumentRole> GetByNameAsync(Guid orgId, string name);
        Task CreateAsync(DocumentRole role);
    }

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

    public interface IAnalyticsRepository
    {
        Task SaveMetricsAsync(object metrics);
        Task<dynamic> GetStorageConfigAsync(Guid organizationId);
        Task<List<UserActivity>> GetTopUsersAsync(Guid organizationId, int top);
    }

    public class AnalyticsRepository : IAnalyticsRepository
    {
        public async Task SaveMetricsAsync(object metrics) { }
        public async Task<dynamic> GetStorageConfigAsync(Guid organizationId) => new { QuotaBytes = 1000000000000L };
        public async Task<List<UserActivity>> GetTopUsersAsync(Guid organizationId, int top) => new List<UserActivity>();
    }

    public interface IRetentionRepository
    {
        Task CreateAsync(DocumentRetentionSchedule schedule);
        Task<List<DocumentRetentionSchedule>> GetSchedulesAsync(Guid organizationId);
    }

    public class RetentionRepository : IRetentionRepository
    {
        public async Task CreateAsync(DocumentRetentionSchedule schedule) { }
        public async Task<List<DocumentRetentionSchedule>> GetSchedulesAsync(Guid organizationId) => new List<DocumentRetentionSchedule>();
    }

    public interface IAccessControlRepository
    {
        Task<List<dynamic>> GetAccessHistoryAsync(Guid organizationId);
        Task<List<dynamic>> GetUserAccessHistoryAsync(Guid userId);
    }

    public class AccessControlRepository : IAccessControlRepository
    {
        public async Task<List<dynamic>> GetAccessHistoryAsync(Guid organizationId) => new List<dynamic>();
        public async Task<List<dynamic>> GetUserAccessHistoryAsync(Guid userId) => new List<dynamic>();
    }

    public interface IWebhookRepository
    {
        Task CreateSubscriptionAsync(WebhookSubscription subscription);
        Task UpdateSubscriptionAsync(WebhookSubscription subscription);
        Task<WebhookSubscription> GetSubscriptionAsync(Guid id);
        Task<List<WebhookSubscription>> GetSubscriptionsAsync(Guid organizationId, string eventType);
        Task CreateEventAsync(WebhookEvent webhookEvent);
        Task UpdateEventAsync(WebhookEvent webhookEvent);
        Task<List<WebhookEvent>> GetEventsAsync(Guid organizationId, DateTime from);
        Task<List<WebhookEvent>> GetFailedEventsAsync();
    }

    public class WebhookRepository : IWebhookRepository
    {
        public async Task CreateSubscriptionAsync(WebhookSubscription subscription) { }
        public async Task UpdateSubscriptionAsync(WebhookSubscription subscription) { }
        public async Task<WebhookSubscription> GetSubscriptionAsync(Guid id) => null;
        public async Task<List<WebhookSubscription>> GetSubscriptionsAsync(Guid organizationId, string eventType) => new List<WebhookSubscription>();
        public async Task CreateEventAsync(WebhookEvent webhookEvent) { }
        public async Task UpdateEventAsync(WebhookEvent webhookEvent) { }
        public async Task<List<WebhookEvent>> GetEventsAsync(Guid organizationId, DateTime from) => new List<WebhookEvent>();
        public async Task<List<WebhookEvent>> GetFailedEventsAsync() => new List<WebhookEvent>();
    }

    public interface IIntegrationRepository
    {
        Task CreateAsync(ExternalIntegration integration);
        Task UpdateAsync(ExternalIntegration integration);
        Task<ExternalIntegration> GetByIdAsync(Guid id);
        Task<List<ExternalIntegration>> GetActiveAsync(Guid organizationId);
    }

    public class IntegrationRepository : IIntegrationRepository
    {
        public async Task CreateAsync(ExternalIntegration integration) { }
        public async Task UpdateAsync(ExternalIntegration integration) { }
        public async Task<ExternalIntegration> GetByIdAsync(Guid id) => null;
        public async Task<List<ExternalIntegration>> GetActiveAsync(Guid organizationId) => new List<ExternalIntegration>();
    }

    public interface IComplianceRepository
    {
        Task SaveReportAsync(ComplianceReport report);
        Task UpdateFindingStatusAsync(Guid findingId, FindingStatus status, string notes);
        Task<DataClassificationPolicy> GetActiveClassificationPolicyAsync(Guid organizationId);
    }

    public class ComplianceRepository : IComplianceRepository
    {
        public async Task SaveReportAsync(ComplianceReport report) { }
        public async Task UpdateFindingStatusAsync(Guid findingId, FindingStatus status, string notes) { }
        public async Task<DataClassificationPolicy> GetActiveClassificationPolicyAsync(Guid organizationId) => null;
    }

    public interface IThreatDetectionRepository
    {
        Task<List<dynamic>> GetFailedAccessAttemptsAsync(Guid userId, TimeSpan timeWindow);
        Task<List<SecurityAlert>> GetActiveAlertsAsync(Guid organizationId);
        Task CreateAlertAsync(SecurityAlert alert);
    }

    public class ThreatDetectionRepository : IThreatDetectionRepository
    {
        public async Task<List<dynamic>> GetFailedAccessAttemptsAsync(Guid userId, TimeSpan timeWindow) => new List<dynamic>();
        public async Task<List<SecurityAlert>> GetActiveAlertsAsync(Guid organizationId) => new List<SecurityAlert>();
        public async Task CreateAlertAsync(SecurityAlert alert) { }
    }

    public interface IKeyManagementRepository
    {
        Task<EncryptionKey> GetKeyAsync(Guid keyId);
        Task SaveKeyAsync(EncryptionKey key);
        Task UpdateKeyAsync(EncryptionKey key);
        Task<List<EncryptionKey>> GetActiveKeysAsync(Guid organizationId);
    }

    public class KeyManagementRepository : IKeyManagementRepository
    {
        public async Task<EncryptionKey> GetKeyAsync(Guid keyId) => null;
        public async Task SaveKeyAsync(EncryptionKey key) { }
        public async Task UpdateKeyAsync(EncryptionKey key) { }
        public async Task<List<EncryptionKey>> GetActiveKeysAsync(Guid organizationId) => new List<EncryptionKey>();
    }

    public interface IMigrationRepository
    {
        Task<int> GetCurrentVersionAsync();
        Task<List<MigrationLog>> GetMigrationHistoryAsync(int limit);
        Task LogMigrationAsync(MigrationLog log);
        Task RecordVersionAsync(SchemaVersion version);
    }

    public class MigrationRepository : IMigrationRepository
    {
        public async Task<int> GetCurrentVersionAsync() => 1;
        public async Task<List<MigrationLog>> GetMigrationHistoryAsync(int limit) => new List<MigrationLog>();
        public async Task LogMigrationAsync(MigrationLog log) { }
        public async Task RecordVersionAsync(SchemaVersion version) { }
    }

    public interface IReportExportProvider
    {
        Task<string> ExportAsync(Report report, string format);
    }

    public class ReportExportProvider : IReportExportProvider
    {
        public async Task<string> ExportAsync(Report report, string format) => "Report exported";
    }
}
