using System;
using System.Collections.Generic;

namespace DocVault.Data
{
    // ============================================================================
    // IN-MEMORY MOCK DATABASE (للاختبار السريع بدون SQL Server)
    // ============================================================================

    public static class MockDatabase
    {
        public static List<Organization> Organizations { get; set; } = new();
        public static List<User> Users { get; set; } = new();
        public static List<Document> Documents { get; set; } = new();
        public static List<DocumentRole> DocumentRoles { get; set; } = new();
        public static List<DocumentPermission> DocumentPermissions { get; set; } = new();
        public static List<DocumentWorkflow> DocumentWorkflows { get; set; } = new();
        public static List<ApprovalTask> ApprovalTasks { get; set; } = new();
        public static List<SearchAnalytic> SearchAnalytics { get; set; } = new();
        public static List<AuditLog> AuditLogs { get; set; } = new();
        public static List<Notification> Notifications { get; set; } = new();
        public static List<EmailNotification> EmailNotifications { get; set; } = new();
        public static List<WebhookSubscription> WebhookSubscriptions { get; set; } = new();
        public static List<ExternalIntegration> ExternalIntegrations { get; set; } = new();
        public static List<Report> Reports { get; set; } = new();
        public static List<EncryptionKey> EncryptionKeys { get; set; } = new();
        public static List<SecurityAlert> SecurityAlerts { get; set; } = new();
        public static List<MigrationLog> MigrationLogs { get; set; } = new();

        public static void Clear()
        {
            Organizations.Clear();
            Users.Clear();
            Documents.Clear();
            DocumentRoles.Clear();
            DocumentPermissions.Clear();
            DocumentWorkflows.Clear();
            ApprovalTasks.Clear();
            SearchAnalytics.Clear();
            AuditLogs.Clear();
            Notifications.Clear();
            EmailNotifications.Clear();
            WebhookSubscriptions.Clear();
            ExternalIntegrations.Clear();
            Reports.Clear();
            EncryptionKeys.Clear();
            SecurityAlerts.Clear();
            MigrationLogs.Clear();
        }
    }

    // ============================================================================
    // REPOSITORY IMPLEMENTATIONS (IN-MEMORY)
    // ============================================================================

    public class UserRepository : IUserRepository
    {
        public async Task<User> GetByIdAsync(Guid id)
        {
            return MockDatabase.Users.Find(u => u.Id == id);
        }

        public async Task<User> GetByEmailAsync(string email)
        {
            return MockDatabase.Users.Find(u => u.Email == email);
        }

        public async Task<List<User>> GetByOrganizationAsync(Guid orgId)
        {
            return MockDatabase.Users.FindAll(u => u.OrganizationId == orgId);
        }

        public async Task CreateAsync(User user)
        {
            MockDatabase.Users.Add(user);
        }

        public async Task UpdateAsync(User user)
        {
            var existing = MockDatabase.Users.Find(u => u.Id == user.Id);
            if (existing != null)
            {
                MockDatabase.Users.Remove(existing);
                MockDatabase.Users.Add(user);
            }
        }

        public async Task DeleteAsync(Guid id)
        {
            MockDatabase.Users.RemoveAll(u => u.Id == id);
        }
    }

    public class DocumentRepository : IDocumentRepository
    {
        public async Task<Document> GetByIdAsync(Guid id)
        {
            return MockDatabase.Documents.Find(d => d.Id == id);
        }

        public async Task<List<Document>> GetByOrganizationAsync(Guid orgId)
        {
            return MockDatabase.Documents.FindAll(d => d.OrganizationId == orgId && !d.IsArchived);
        }

        public async Task<List<Document>> FindByTypeAndDateAsync(Guid orgId, string type, DateTime? from, DateTime? to)
        {
            return MockDatabase.Documents.FindAll(d =>
                d.OrganizationId == orgId &&
                d.DocumentType == type &&
                d.CreatedAt >= (from ?? DateTime.MinValue) &&
                d.CreatedAt <= (to ?? DateTime.MaxValue));
        }

        public async Task CreateAsync(Document doc)
        {
            MockDatabase.Documents.Add(doc);
        }

        public async Task UpdateAsync(Document doc)
        {
            var existing = MockDatabase.Documents.Find(d => d.Id == doc.Id);
            if (existing != null)
            {
                MockDatabase.Documents.Remove(existing);
                MockDatabase.Documents.Add(doc);
            }
        }

        public async Task DeleteAsync(Guid id)
        {
            var doc = MockDatabase.Documents.Find(d => d.Id == id);
            if (doc != null) doc.IsArchived = true;
        }
    }

    public class PermissionRepository : IPermissionRepository
    {
        public async Task<List<DocumentPermission>> GetByDocumentAsync(Guid docId)
        {
            return MockDatabase.DocumentPermissions.FindAll(p => p.DocumentId == docId);
        }

        public async Task<bool> CheckAccessAsync(Guid userId, Guid documentId, string action)
        {
            return MockDatabase.DocumentPermissions.Any(p =>
                p.DocumentId == documentId &&
                p.UserId == userId &&
                p.AccessLevel >= action);
        }

        public async Task GrantAccessAsync(Guid userId, Guid documentId, string level)
        {
            var perm = new DocumentPermission
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DocumentId = documentId,
                AccessLevel = level,
                GrantedAt = DateTime.UtcNow
            };

            MockDatabase.DocumentPermissions.Add(perm);
        }

        public async Task RevokeAccessAsync(Guid userId, Guid documentId)
        {
            MockDatabase.DocumentPermissions.RemoveAll(p =>
                p.UserId == userId && p.DocumentId == documentId);
        }
    }

    public class WorkflowRepository : IWorkflowRepository
    {
        public async Task<DocumentWorkflow> GetByIdAsync(Guid id)
        {
            return MockDatabase.DocumentWorkflows.Find(w => w.Id == id);
        }

        public async Task<List<DocumentWorkflow>> GetByOrganizationAsync(Guid orgId)
        {
            return MockDatabase.DocumentWorkflows.FindAll(w => w.OrganizationId == orgId);
        }

        public async Task CreateAsync(DocumentWorkflow workflow)
        {
            MockDatabase.DocumentWorkflows.Add(workflow);
        }

        public async Task UpdateAsync(DocumentWorkflow workflow)
        {
            var existing = MockDatabase.DocumentWorkflows.Find(w => w.Id == workflow.Id);
            if (existing != null)
            {
                MockDatabase.DocumentWorkflows.Remove(existing);
                MockDatabase.DocumentWorkflows.Add(workflow);
            }
        }

        public async Task<DocumentWorkflow> GetTemplateAsync(Guid templateId)
        {
            return await GetByIdAsync(templateId);
        }
    }

    public class AuditRepository : IAuditRepository
    {
        public async Task CreateAsync(AuditLog log)
        {
            MockDatabase.AuditLogs.Add(log);
        }

        public async Task<List<AuditLog>> GetByOrganizationAsync(Guid orgId, DateTime from, DateTime to, int take)
        {
            return MockDatabase.AuditLogs
                .FindAll(l => l.OrganizationId == orgId && l.PerformedAt >= from && l.PerformedAt <= to)
                .OrderByDescending(l => l.PerformedAt)
                .Take(take)
                .ToList();
        }

        public async Task<List<AuditLog>> GetByUserAsync(Guid userId, DateTime from, DateTime to)
        {
            return MockDatabase.AuditLogs
                .FindAll(l => l.UserId == userId && l.PerformedAt >= from && l.PerformedAt <= to)
                .OrderByDescending(l => l.PerformedAt)
                .ToList();
        }
    }

    public class NotificationRepository : INotificationRepository
    {
        public async Task CreateAsync(Notification notification)
        {
            MockDatabase.Notifications.Add(notification);
        }

        public async Task<Notification> GetByIdAsync(Guid id)
        {
            return MockDatabase.Notifications.Find(n => n.Id == id);
        }

        public async Task<List<Notification>> GetByUserAsync(Guid userId)
        {
            return MockDatabase.Notifications.FindAll(n => n.UserId == userId);
        }

        public async Task<List<Notification>> GetUnreadAsync(Guid userId)
        {
            return MockDatabase.Notifications.FindAll(n => n.UserId == userId && !n.IsRead);
        }

        public async Task UpdateAsync(Notification notification)
        {
            var existing = MockDatabase.Notifications.Find(n => n.Id == notification.Id);
            if (existing != null)
            {
                MockDatabase.Notifications.Remove(existing);
                MockDatabase.Notifications.Add(notification);
            }
        }

        public async Task DeleteAsync(Guid id)
        {
            MockDatabase.Notifications.RemoveAll(n => n.Id == id);
        }

        public async Task MarkAllAsReadAsync(Guid userId)
        {
            foreach (var notif in MockDatabase.Notifications.Where(n => n.UserId == userId))
            {
                notif.IsRead = true;
                notif.ReadAt = DateTime.UtcNow;
            }
        }
    }

    public class EmailRepository : IEmailRepository
    {
        public async Task CreateAsync(EmailNotification notification)
        {
            MockDatabase.EmailNotifications.Add(notification);
        }

        public async Task<List<EmailNotification>> GetSentEmailsAsync(Guid organizationId, DateTime from)
        {
            return MockDatabase.EmailNotifications.FindAll(e =>
                e.CreatedAt >= from && e.Status == EmailStatus.Sent);
        }

        public async Task<int> GetFailedCountAsync(Guid organizationId)
        {
            return MockDatabase.EmailNotifications.Count(e => e.Status == EmailStatus.Failed);
        }

        public async Task<List<EmailNotification>> GetFailedEmailsAsync(Guid organizationId)
        {
            return MockDatabase.EmailNotifications.FindAll(e => e.Status == EmailStatus.Failed);
        }
    }

    public class SearchRepository : ISearchRepository
    {
        public async Task<List<SearchResult>> SearchAsync(SearchQuery query)
        {
            var results = new List<SearchResult>();

            foreach (var doc in MockDatabase.Documents)
            {
                if (query.QueryText != null &&
                    (doc.Title.Contains(query.QueryText, StringComparison.OrdinalIgnoreCase) ||
                     doc.Description.Contains(query.QueryText, StringComparison.OrdinalIgnoreCase)))
                {
                    results.Add(new SearchResult
                    {
                        DocumentId = doc.Id,
                        Title = doc.Title,
                        Description = doc.Description,
                        Status = doc.Status,
                        Classification = doc.Classification,
                        CreatedAt = doc.CreatedAt
                    });
                }
            }

            return results;
        }

        public async Task<int> CountByClassificationAsync(DocumentClassification classification, SearchQuery baseQuery)
        {
            return MockDatabase.Documents.Count(d => d.Classification == classification);
        }

        public async Task<int> CountByStatusAsync(DocumentStatus status, SearchQuery baseQuery)
        {
            return MockDatabase.Documents.Count(d => d.Status == status);
        }

        public async Task<Dictionary<string, int>> GetPopularTagsAsync(SearchQuery baseQuery, int topN)
        {
            var tags = new Dictionary<string, int>();
            foreach (var doc in MockDatabase.Documents)
            {
                if (doc.Tags != null)
                {
                    foreach (var tag in doc.Tags)
                    {
                        if (tags.ContainsKey(tag))
                            tags[tag]++;
                        else
                            tags[tag] = 1;
                    }
                }
            }

            return tags.OrderByDescending(x => x.Value).Take(topN).ToDictionary(x => x.Key, x => x.Value);
        }

        public async Task<List<string>> GetAutocompleteAsync(string prefix, Guid organizationId)
        {
            return MockDatabase.Documents
                .Where(d => d.OrganizationId == organizationId && d.Title.StartsWith(prefix))
                .Select(d => d.Title)
                .Distinct()
                .ToList();
        }
    }

    public class ReportRepository : IReportRepository
    {
        public async Task SaveAsync(Report report)
        {
            MockDatabase.Reports.Add(report);
        }

        public async Task<Report> GetByIdAsync(Guid id)
        {
            return MockDatabase.Reports.Find(r => r.Id == id);
        }

        public async Task<List<Report>> GetReportsAsync(Guid organizationId, DateTime from)
        {
            return MockDatabase.Reports.FindAll(r => r.OrganizationId == organizationId && r.GeneratedAt >= from);
        }

        public async Task SaveScheduleAsync(Guid organizationId, ReportType type, string schedule)
        {
            // Store schedule in database
        }
    }

    // ============================================================================
    // MOCK EMAIL & WEBHOOK PROVIDERS
    // ============================================================================

    public class MockEmailProvider : IEmailProvider
    {
        public async Task<bool> SendAsync(string recipientEmail, string subject, string body, string htmlBody)
        {
            Console.WriteLine($"📧 [Email Sent] To: {recipientEmail}");
            Console.WriteLine($"   Subject: {subject}");
            return true;
        }

        public async Task<bool> SendWithAttachmentsAsync(string recipientEmail, string subject, string body, List<EmailAttachment> attachments)
        {
            Console.WriteLine($"📧 [Email Sent] To: {recipientEmail} (with {attachments.Count} attachments)");
            return true;
        }
    }

    public class MockWebhookDeliveryProvider : IWebhookDeliveryProvider
    {
        public async Task DeliverAsync(WebhookEvent webhookEvent, string authToken)
        {
            Console.WriteLine($"🔗 [Webhook Delivered] Event: {webhookEvent.EventType} -> {webhookEvent.EndpointUrl}");
        }
    }
}
