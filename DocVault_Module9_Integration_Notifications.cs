using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// ============================================================================
// DOCVAULT MODULE 9: INTEGRATION & NOTIFICATIONS
// ============================================================================
// Email, webhook, real-time notifications, third-party integrations

namespace DocVault.Core.Integration
{
    // ========================================================================
    // DATA MODELS
    // ========================================================================

    public class Notification
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid OrganizationId { get; set; }

        public string Title { get; set; }
        public string Message { get; set; }
        public string Type { get; set; } // DocumentApproved, AccessGranted, PermissionExpired, RetentionReminder

        public Guid? RelatedDocumentId { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();

        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
    }

    public class EmailNotification
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }

        public string RecipientEmail { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public string HtmlBody { get; set; }

        public List<EmailAttachment> Attachments { get; set; } = new();

        public DateTime CreatedAt { get; set; }
        public DateTime? SentAt { get; set; }
        public EmailStatus Status { get; set; } // Pending, Sent, Failed, Bounced

        public string FailureReason { get; set; }
    }

    public class EmailAttachment
    {
        public string FileName { get; set; }
        public byte[] Content { get; set; }
        public string MimeType { get; set; }
    }

    public class WebhookEvent
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }

        public string EventType { get; set; } // DocumentCreated, DocumentApproved, PermissionChanged
        public string EndpointUrl { get; set; }

        public Guid? RelatedDocumentId { get; set; }
        public string Payload { get; set; } // JSON

        public DateTime TriggeredAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public WebhookStatus Status { get; set; } // Pending, Delivered, Failed, Retrying

        public int RetryCount { get; set; }
        public int MaxRetries { get; set; } = 5;

        public string ResponseMessage { get; set; }
    }

    public class WebhookSubscription
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }

        public string EndpointUrl { get; set; }
        public List<string> EventTypes { get; set; } = new();

        public string AuthToken { get; set; }
        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? DisabledAt { get; set; }
    }

    public class ExternalIntegration
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }

        public string IntegrationType { get; set; } // Slack, Teams, Zapier, CustomAPI
        public string IntegrationName { get; set; }

        public Dictionary<string, string> Credentials { get; set; } = new();
        public Dictionary<string, object> Configuration { get; set; } = new();

        public bool IsActive { get; set; }
        public DateTime ConnectedAt { get; set; }
        public DateTime? LastSyncAt { get; set; }

        public int SyncIntervalMinutes { get; set; } = 60;
    }

    public enum EmailStatus
    {
        Pending = 0,
        Sent = 1,
        Failed = 2,
        Bounced = 3,
        Opened = 4,
        Clicked = 5
    }

    public enum WebhookStatus
    {
        Pending = 0,
        Delivered = 1,
        Failed = 2,
        Retrying = 3,
        MaxRetriesExceeded = 4
    }

    // ========================================================================
    // NOTIFICATION SERVICE
    // ========================================================================

    public interface INotificationService
    {
        Task<Guid> CreateNotificationAsync(Guid userId, string title, string message, string type);
        Task<List<Notification>> GetUserNotificationsAsync(Guid userId, bool unreadOnly = false);
        Task MarkAsReadAsync(Guid notificationId);
        Task MarkAllAsReadAsync(Guid userId);
        Task DeleteNotificationAsync(Guid notificationId);
    }

    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _repository;

        public NotificationService(INotificationRepository repository)
        {
            _repository = repository;
        }

        public async Task<Guid> CreateNotificationAsync(Guid userId, string title, string message, string type)
        {
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            await _repository.CreateAsync(notification);
            return notification.Id;
        }

        public async Task<List<Notification>> GetUserNotificationsAsync(Guid userId, bool unreadOnly = false)
        {
            if (unreadOnly)
                return await _repository.GetUnreadAsync(userId);

            return await _repository.GetByUserAsync(userId);
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

        public async Task MarkAllAsReadAsync(Guid userId)
        {
            await _repository.MarkAllAsReadAsync(userId);
        }

        public async Task DeleteNotificationAsync(Guid notificationId)
        {
            await _repository.DeleteAsync(notificationId);
        }
    }

    // ========================================================================
    // EMAIL SERVICE
    // ========================================================================

    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string recipientEmail, string subject, string body, string htmlBody = null);
        Task<bool> SendEmailWithAttachmentsAsync(string recipientEmail, string subject, string body, List<EmailAttachment> attachments);
        Task<List<EmailNotification>> GetSentEmailsAsync(Guid organizationId, int days = 30);
        Task<int> GetFailedEmailCountAsync(Guid organizationId);
        Task RetryFailedEmailsAsync(Guid organizationId);
    }

    public class EmailService : IEmailService
    {
        private readonly IEmailRepository _repository;
        private readonly IEmailProvider _provider;

        public EmailService(IEmailRepository repository, IEmailProvider provider)
        {
            _repository = repository;
            _provider = provider;
        }

        public async Task<bool> SendEmailAsync(string recipientEmail, string subject, string body, string htmlBody = null)
        {
            try
            {
                var result = await _provider.SendAsync(recipientEmail, subject, body, htmlBody);

                var notification = new EmailNotification
                {
                    Id = Guid.NewGuid(),
                    RecipientEmail = recipientEmail,
                    Subject = subject,
                    Body = body,
                    HtmlBody = htmlBody,
                    CreatedAt = DateTime.UtcNow,
                    SentAt = DateTime.UtcNow,
                    Status = result ? EmailStatus.Sent : EmailStatus.Failed
                };

                await _repository.CreateAsync(notification);
                return result;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> SendEmailWithAttachmentsAsync(string recipientEmail, string subject, string body, List<EmailAttachment> attachments)
        {
            try
            {
                var result = await _provider.SendWithAttachmentsAsync(recipientEmail, subject, body, attachments);

                var notification = new EmailNotification
                {
                    Id = Guid.NewGuid(),
                    RecipientEmail = recipientEmail,
                    Subject = subject,
                    Body = body,
                    Attachments = attachments,
                    CreatedAt = DateTime.UtcNow,
                    SentAt = DateTime.UtcNow,
                    Status = result ? EmailStatus.Sent : EmailStatus.Failed
                };

                await _repository.CreateAsync(notification);
                return result;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<List<EmailNotification>> GetSentEmailsAsync(Guid organizationId, int days = 30)
        {
            var from = DateTime.UtcNow.AddDays(-days);
            return await _repository.GetSentEmailsAsync(organizationId, from);
        }

        public async Task<int> GetFailedEmailCountAsync(Guid organizationId)
        {
            return await _repository.GetFailedCountAsync(organizationId);
        }

        public async Task RetryFailedEmailsAsync(Guid organizationId)
        {
            var failed = await _repository.GetFailedEmailsAsync(organizationId);

            foreach (var email in failed)
            {
                await SendEmailAsync(email.RecipientEmail, email.Subject, email.Body, email.HtmlBody);
            }
        }
    }

    // ========================================================================
    // WEBHOOK SERVICE
    // ========================================================================

    public interface IWebhookService
    {
        Task<Guid> SubscribeAsync(Guid organizationId, string endpointUrl, List<string> eventTypes);
        Task<bool> UnsubscribeAsync(Guid subscriptionId);
        Task TriggerEventAsync(Guid organizationId, string eventType, Guid? documentId, object payload);
        Task<List<WebhookEvent>> GetEventHistoryAsync(Guid organizationId, int days = 30);
        Task RetryFailedEventsAsync();
    }

    public class WebhookService : IWebhookService
    {
        private readonly IWebhookRepository _repository;
        private readonly IWebhookDeliveryProvider _deliveryProvider;

        public WebhookService(
            IWebhookRepository repository,
            IWebhookDeliveryProvider deliveryProvider)
        {
            _repository = repository;
            _deliveryProvider = deliveryProvider;
        }

        public async Task<Guid> SubscribeAsync(Guid organizationId, string endpointUrl, List<string> eventTypes)
        {
            var subscription = new WebhookSubscription
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                EndpointUrl = endpointUrl,
                EventTypes = eventTypes,
                AuthToken = Guid.NewGuid().ToString("N"),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _repository.CreateSubscriptionAsync(subscription);
            return subscription.Id;
        }

        public async Task<bool> UnsubscribeAsync(Guid subscriptionId)
        {
            var subscription = await _repository.GetSubscriptionAsync(subscriptionId);
            if (subscription == null) return false;

            subscription.IsActive = false;
            subscription.DisabledAt = DateTime.UtcNow;
            await _repository.UpdateSubscriptionAsync(subscription);

            return true;
        }

        public async Task TriggerEventAsync(Guid organizationId, string eventType, Guid? documentId, object payload)
        {
            var subscriptions = await _repository.GetSubscriptionsAsync(organizationId, eventType);

            foreach (var subscription in subscriptions)
            {
                var webhookEvent = new WebhookEvent
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    EventType = eventType,
                    EndpointUrl = subscription.EndpointUrl,
                    RelatedDocumentId = documentId,
                    Payload = System.Text.Json.JsonSerializer.Serialize(payload),
                    TriggeredAt = DateTime.UtcNow,
                    Status = WebhookStatus.Pending,
                    MaxRetries = 5
                };

                await _repository.CreateEventAsync(webhookEvent);
                await _deliveryProvider.DeliverAsync(webhookEvent, subscription.AuthToken);
            }
        }

        public async Task<List<WebhookEvent>> GetEventHistoryAsync(Guid organizationId, int days = 30)
        {
            var from = DateTime.UtcNow.AddDays(-days);
            return await _repository.GetEventsAsync(organizationId, from);
        }

        public async Task RetryFailedEventsAsync()
        {
            var failedEvents = await _repository.GetFailedEventsAsync();

            foreach (var evt in failedEvents)
            {
                if (evt.RetryCount < evt.MaxRetries)
                {
                    evt.RetryCount++;
                    await _repository.UpdateEventAsync(evt);
                    await _deliveryProvider.DeliverAsync(evt, "");
                }
            }
        }
    }

    // ========================================================================
    // INTEGRATION SERVICE
    // ========================================================================

    public interface IIntegrationService
    {
        Task<Guid> ConnectIntegrationAsync(Guid organizationId, string type, Dictionary<string, string> credentials);
        Task<bool> DisconnectIntegrationAsync(Guid integrationId);
        Task<List<ExternalIntegration>> GetActiveIntegrationsAsync(Guid organizationId);
        Task SyncIntegrationsAsync(Guid organizationId);
    }

    public class IntegrationService : IIntegrationService
    {
        private readonly IIntegrationRepository _repository;
        private readonly Dictionary<string, IIntegrationProvider> _providers;

        public IntegrationService(
            IIntegrationRepository repository,
            Dictionary<string, IIntegrationProvider> providers)
        {
            _repository = repository;
            _providers = providers;
        }

        public async Task<Guid> ConnectIntegrationAsync(Guid organizationId, string type, Dictionary<string, string> credentials)
        {
            var integration = new ExternalIntegration
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                IntegrationType = type,
                IntegrationName = $"{type} Integration",
                Credentials = credentials,
                IsActive = true,
                ConnectedAt = DateTime.UtcNow
            };

            if (_providers.TryGetValue(type, out var provider))
            {
                await provider.AuthenticateAsync(credentials);
            }

            await _repository.CreateAsync(integration);
            return integration.Id;
        }

        public async Task<bool> DisconnectIntegrationAsync(Guid integrationId)
        {
            var integration = await _repository.GetByIdAsync(integrationId);
            if (integration == null) return false;

            integration.IsActive = false;
            await _repository.UpdateAsync(integration);

            return true;
        }

        public async Task<List<ExternalIntegration>> GetActiveIntegrationsAsync(Guid organizationId)
        {
            return await _repository.GetActiveAsync(organizationId);
        }

        public async Task SyncIntegrationsAsync(Guid organizationId)
        {
            var integrations = await GetActiveIntegrationsAsync(organizationId);

            foreach (var integration in integrations)
            {
                if (_providers.TryGetValue(integration.IntegrationType, out var provider))
                {
                    await provider.SyncAsync(integration);
                    integration.LastSyncAt = DateTime.UtcNow;
                    await _repository.UpdateAsync(integration);
                }
            }
        }
    }

    // ========================================================================
    // REPOSITORY & PROVIDER INTERFACES
    // ========================================================================

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

    public interface IIntegrationRepository
    {
        Task CreateAsync(ExternalIntegration integration);
        Task UpdateAsync(ExternalIntegration integration);
        Task<ExternalIntegration> GetByIdAsync(Guid id);
        Task<List<ExternalIntegration>> GetActiveAsync(Guid organizationId);
    }

    public interface IEmailProvider
    {
        Task<bool> SendAsync(string recipientEmail, string subject, string body, string htmlBody);
        Task<bool> SendWithAttachmentsAsync(string recipientEmail, string subject, string body, List<EmailAttachment> attachments);
    }

    public interface IWebhookDeliveryProvider
    {
        Task DeliverAsync(WebhookEvent webhookEvent, string authToken);
    }

    public interface IIntegrationProvider
    {
        Task AuthenticateAsync(Dictionary<string, string> credentials);
        Task SyncAsync(ExternalIntegration integration);
    }
}
