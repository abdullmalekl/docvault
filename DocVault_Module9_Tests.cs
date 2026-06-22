using Xunit;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DocVault.Tests.Module9
{
    public class NotificationServiceTests
    {
        private readonly Mock<INotificationRepository> _mockRepository;
        private readonly NotificationService _service;

        public NotificationServiceTests()
        {
            _mockRepository = new Mock<INotificationRepository>();
            _service = new NotificationService(_mockRepository.Object);
        }

        [Fact]
        public async Task CreateNotificationAsync_WithValidData_CreatesNotification()
        {
            var userId = Guid.NewGuid();

            var id = await _service.CreateNotificationAsync(userId, "Test", "Message", "DocumentApproved");

            Assert.NotEqual(Guid.Empty, id);
            _mockRepository.Verify(x => x.CreateAsync(It.Is<Notification>(
                n => n.UserId == userId && n.Type == "DocumentApproved")), Times.Once);
        }

        [Fact]
        public async Task GetUserNotificationsAsync_ReturnsNotifications()
        {
            var userId = Guid.NewGuid();
            var notifications = new List<Notification>
            {
                new Notification { Id = Guid.NewGuid(), UserId = userId }
            };

            _mockRepository.Setup(x => x.GetByUserAsync(userId)).ReturnsAsync(notifications);

            var result = await _service.GetUserNotificationsAsync(userId);

            Assert.Single(result);
        }

        [Fact]
        public async Task MarkAsReadAsync_UpdatesNotification()
        {
            var notificationId = Guid.NewGuid();
            var notification = new Notification { Id = notificationId, IsRead = false };

            _mockRepository.Setup(x => x.GetByIdAsync(notificationId)).ReturnsAsync(notification);

            await _service.MarkAsReadAsync(notificationId);

            Assert.True(notification.IsRead);
            Assert.NotNull(notification.ReadAt);
            _mockRepository.Verify(x => x.UpdateAsync(notification), Times.Once);
        }

        [Fact]
        public async Task MarkAllAsReadAsync_MarksAll()
        {
            var userId = Guid.NewGuid();

            await _service.MarkAllAsReadAsync(userId);

            _mockRepository.Verify(x => x.MarkAllAsReadAsync(userId), Times.Once);
        }

        [Fact]
        public async Task DeleteNotificationAsync_RemovesNotification()
        {
            var notificationId = Guid.NewGuid();

            await _service.DeleteNotificationAsync(notificationId);

            _mockRepository.Verify(x => x.DeleteAsync(notificationId), Times.Once);
        }
    }

    public class EmailServiceTests
    {
        private readonly Mock<IEmailRepository> _mockRepository;
        private readonly Mock<IEmailProvider> _mockProvider;
        private readonly EmailService _service;

        public EmailServiceTests()
        {
            _mockRepository = new Mock<IEmailRepository>();
            _mockProvider = new Mock<IEmailProvider>();
            _service = new EmailService(_mockRepository.Object, _mockProvider.Object);
        }

        [Fact]
        public async Task SendEmailAsync_WithValidData_SendsEmail()
        {
            var email = "user@example.com";
            var subject = "Test";
            var body = "Body";

            _mockProvider.Setup(x => x.SendAsync(email, subject, body, null)).ReturnsAsync(true);

            var result = await _service.SendEmailAsync(email, subject, body);

            Assert.True(result);
            _mockRepository.Verify(x => x.CreateAsync(It.Is<EmailNotification>(
                e => e.RecipientEmail == email && e.Status == EmailStatus.Sent)), Times.Once);
        }

        [Fact]
        public async Task SendEmailAsync_ProviderFails_ReturnsFalse()
        {
            _mockProvider.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), null))
                .ReturnsAsync(false);

            var result = await _service.SendEmailAsync("user@example.com", "Test", "Body");

            Assert.False(result);
        }

        [Fact]
        public async Task SendEmailWithAttachmentsAsync_SendsWithAttachments()
        {
            var attachments = new List<EmailAttachment>
            {
                new EmailAttachment { FileName = "test.pdf", Content = new byte[] { 1, 2, 3 } }
            };

            _mockProvider.Setup(x => x.SendWithAttachmentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), attachments))
                .ReturnsAsync(true);

            var result = await _service.SendEmailWithAttachmentsAsync("user@example.com", "Test", "Body", attachments);

            Assert.True(result);
        }

        [Fact]
        public async Task GetFailedEmailCountAsync_ReturnsCount()
        {
            var orgId = Guid.NewGuid();

            _mockRepository.Setup(x => x.GetFailedCountAsync(orgId)).ReturnsAsync(3);

            var result = await _service.GetFailedEmailCountAsync(orgId);

            Assert.Equal(3, result);
        }

        [Fact]
        public async Task RetryFailedEmailsAsync_RetriesFailed()
        {
            var orgId = Guid.NewGuid();
            var failed = new List<EmailNotification>
            {
                new EmailNotification { RecipientEmail = "user@example.com", Subject = "Test", Body = "Body" }
            };

            _mockRepository.Setup(x => x.GetFailedEmailsAsync(orgId)).ReturnsAsync(failed);
            _mockProvider.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), null))
                .ReturnsAsync(true);

            await _service.RetryFailedEmailsAsync(orgId);

            _mockProvider.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), null), Times.Once);
        }
    }

    public class WebhookServiceTests
    {
        private readonly Mock<IWebhookRepository> _mockRepository;
        private readonly Mock<IWebhookDeliveryProvider> _mockDeliveryProvider;
        private readonly WebhookService _service;

        public WebhookServiceTests()
        {
            _mockRepository = new Mock<IWebhookRepository>();
            _mockDeliveryProvider = new Mock<IWebhookDeliveryProvider>();
            _service = new WebhookService(_mockRepository.Object, _mockDeliveryProvider.Object);
        }

        [Fact]
        public async Task SubscribeAsync_WithValidData_CreatesSubscription()
        {
            var orgId = Guid.NewGuid();
            var events = new List<string> { "DocumentCreated" };

            var result = await _service.SubscribeAsync(orgId, "https://example.com/webhook", events);

            Assert.NotEqual(Guid.Empty, result);
            _mockRepository.Verify(x => x.CreateSubscriptionAsync(It.Is<WebhookSubscription>(
                s => s.OrganizationId == orgId && s.IsActive)), Times.Once);
        }

        [Fact]
        public async Task UnsubscribeAsync_DisablesSubscription()
        {
            var subscriptionId = Guid.NewGuid();
            var subscription = new WebhookSubscription { Id = subscriptionId, IsActive = true };

            _mockRepository.Setup(x => x.GetSubscriptionAsync(subscriptionId)).ReturnsAsync(subscription);

            var result = await _service.UnsubscribeAsync(subscriptionId);

            Assert.True(result);
            Assert.False(subscription.IsActive);
            _mockRepository.Verify(x => x.UpdateSubscriptionAsync(subscription), Times.Once);
        }

        [Fact]
        public async Task TriggerEventAsync_WithValidEvent_DeliversPayload()
        {
            var orgId = Guid.NewGuid();
            var subscriptions = new List<WebhookSubscription>
            {
                new WebhookSubscription { EndpointUrl = "https://example.com", AuthToken = "token" }
            };

            _mockRepository.Setup(x => x.GetSubscriptionsAsync(orgId, "DocumentCreated")).ReturnsAsync(subscriptions);

            await _service.TriggerEventAsync(orgId, "DocumentCreated", null, new { DocumentId = Guid.NewGuid() });

            _mockRepository.Verify(x => x.CreateEventAsync(It.IsAny<WebhookEvent>()), Times.Once);
            _mockDeliveryProvider.Verify(x => x.DeliverAsync(It.IsAny<WebhookEvent>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task RetryFailedEventsAsync_RetriesFailedEvents()
        {
            var failedEvent = new WebhookEvent
            {
                Id = Guid.NewGuid(),
                Status = WebhookStatus.Failed,
                RetryCount = 0,
                MaxRetries = 5
            };

            _mockRepository.Setup(x => x.GetFailedEventsAsync()).ReturnsAsync(new List<WebhookEvent> { failedEvent });

            await _service.RetryFailedEventsAsync();

            Assert.Equal(1, failedEvent.RetryCount);
            _mockDeliveryProvider.Verify(x => x.DeliverAsync(failedEvent, ""), Times.Once);
        }
    }

    public class IntegrationServiceTests
    {
        private readonly Mock<IIntegrationRepository> _mockRepository;
        private readonly Mock<IIntegrationProvider> _mockProvider;
        private readonly IntegrationService _service;

        public IntegrationServiceTests()
        {
            _mockRepository = new Mock<IIntegrationRepository>();
            _mockProvider = new Mock<IIntegrationProvider>();

            var providers = new Dictionary<string, IIntegrationProvider>
            {
                { "Slack", _mockProvider.Object }
            };

            _service = new IntegrationService(_mockRepository.Object, providers);
        }

        [Fact]
        public async Task ConnectIntegrationAsync_WithValidCredentials_CreatesIntegration()
        {
            var orgId = Guid.NewGuid();
            var credentials = new Dictionary<string, string> { { "token", "abc123" } };

            _mockProvider.Setup(x => x.AuthenticateAsync(credentials)).Returns(Task.CompletedTask);

            var result = await _service.ConnectIntegrationAsync(orgId, "Slack", credentials);

            Assert.NotEqual(Guid.Empty, result);
            _mockRepository.Verify(x => x.CreateAsync(It.Is<ExternalIntegration>(
                i => i.OrganizationId == orgId && i.IntegrationType == "Slack")), Times.Once);
        }

        [Fact]
        public async Task DisconnectIntegrationAsync_DisablesIntegration()
        {
            var integrationId = Guid.NewGuid();
            var integration = new ExternalIntegration { Id = integrationId, IsActive = true };

            _mockRepository.Setup(x => x.GetByIdAsync(integrationId)).ReturnsAsync(integration);

            var result = await _service.DisconnectIntegrationAsync(integrationId);

            Assert.True(result);
            Assert.False(integration.IsActive);
            _mockRepository.Verify(x => x.UpdateAsync(integration), Times.Once);
        }

        [Fact]
        public async Task GetActiveIntegrationsAsync_ReturnsActive()
        {
            var orgId = Guid.NewGuid();
            var integrations = new List<ExternalIntegration>
            {
                new ExternalIntegration { IsActive = true }
            };

            _mockRepository.Setup(x => x.GetActiveAsync(orgId)).ReturnsAsync(integrations);

            var result = await _service.GetActiveIntegrationsAsync(orgId);

            Assert.Single(result);
        }

        [Fact]
        public async Task SyncIntegrationsAsync_SyncsAll()
        {
            var orgId = Guid.NewGuid();
            var integration = new ExternalIntegration
            {
                Id = Guid.NewGuid(),
                IntegrationType = "Slack",
                IsActive = true
            };

            _mockRepository.Setup(x => x.GetActiveAsync(orgId)).ReturnsAsync(new List<ExternalIntegration> { integration });
            _mockProvider.Setup(x => x.SyncAsync(integration)).Returns(Task.CompletedTask);

            await _service.SyncIntegrationsAsync(orgId);

            Assert.NotNull(integration.LastSyncAt);
            _mockRepository.Verify(x => x.UpdateAsync(integration), Times.Once);
        }
    }
}
