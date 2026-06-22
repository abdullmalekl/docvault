-- ============================================================================
-- DOCVAULT MODULE 9: INTEGRATION & NOTIFICATIONS SCHEMA
-- ============================================================================

-- ============================================================================
-- NOTIFICATIONS TABLE
-- ============================================================================

CREATE TABLE [dbo].[Notifications] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,

    [Title] NVARCHAR(500) NOT NULL,
    [Message] NVARCHAR(MAX) NOT NULL,
    [Type] NVARCHAR(50) NOT NULL, -- DocumentApproved, AccessGranted, PermissionExpired, RetentionReminder

    [RelatedDocumentId] UNIQUEIDENTIFIER NULL,
    [Metadata] NVARCHAR(MAX) NULL, -- JSON

    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [IsRead] BIT NOT NULL DEFAULT 0,
    [ReadAt] DATETIME2 NULL,

    CONSTRAINT FK_Notifications_User FOREIGN KEY ([UserId])
        REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE,
    CONSTRAINT FK_Notifications_Organization FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organizations]([Id]),
    CONSTRAINT FK_Notifications_Document FOREIGN KEY ([RelatedDocumentId])
        REFERENCES [dbo].[Documents]([Id]) ON DELETE SET NULL
);

CREATE INDEX IDX_Notifications_User ON [dbo].[Notifications]([UserId]);
CREATE INDEX IDX_Notifications_IsRead ON [dbo].[Notifications]([IsRead]) WHERE [IsRead] = 0;
CREATE INDEX IDX_Notifications_CreatedAt ON [dbo].[Notifications]([CreatedAt] DESC);
CREATE INDEX IDX_Notifications_Type ON [dbo].[Notifications]([Type]);

-- ============================================================================
-- EMAIL NOTIFICATIONS TABLE
-- ============================================================================

CREATE TABLE [dbo].[EmailNotifications] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [UserId] UNIQUEIDENTIFIER NOT NULL,

    [RecipientEmail] NVARCHAR(255) NOT NULL,
    [Subject] NVARCHAR(500) NOT NULL,
    [Body] NVARCHAR(MAX) NOT NULL,
    [HtmlBody] NVARCHAR(MAX) NULL,

    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [SentAt] DATETIME2 NULL,
    [Status] INT NOT NULL DEFAULT 0, -- 0=Pending, 1=Sent, 2=Failed, 3=Bounced, 4=Opened, 5=Clicked

    [FailureReason] NVARCHAR(MAX) NULL,
    [RetryCount] INT NOT NULL DEFAULT 0,

    CONSTRAINT FK_EmailNotifications_User FOREIGN KEY ([UserId])
        REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE
);

CREATE INDEX IDX_EmailNotifications_Status ON [dbo].[EmailNotifications]([Status]);
CREATE INDEX IDX_EmailNotifications_SentAt ON [dbo].[EmailNotifications]([SentAt] DESC);
CREATE INDEX IDX_EmailNotifications_RecipientEmail ON [dbo].[EmailNotifications]([RecipientEmail]);
CREATE INDEX IDX_EmailNotifications_CreatedAt ON [dbo].[EmailNotifications]([CreatedAt] DESC);

-- ============================================================================
-- EMAIL ATTACHMENTS TABLE
-- ============================================================================

CREATE TABLE [dbo].[EmailAttachments] (
    [Id] BIGINT NOT NULL PRIMARY KEY IDENTITY(1,1),
    [EmailNotificationId] UNIQUEIDENTIFIER NOT NULL,

    [FileName] NVARCHAR(500) NOT NULL,
    [MimeType] NVARCHAR(100) NOT NULL,
    [Content] VARBINARY(MAX) NOT NULL,
    [FileSize] BIGINT NOT NULL,

    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_EmailAttachments_EmailNotification FOREIGN KEY ([EmailNotificationId])
        REFERENCES [dbo].[EmailNotifications]([Id]) ON DELETE CASCADE
);

CREATE INDEX IDX_EmailAttachments_EmailNotification ON [dbo].[EmailAttachments]([EmailNotificationId]);

-- ============================================================================
-- WEBHOOK SUBSCRIPTIONS TABLE
-- ============================================================================

CREATE TABLE [dbo].[WebhookSubscriptions] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,

    [EndpointUrl] NVARCHAR(MAX) NOT NULL,
    [EventTypes] NVARCHAR(MAX) NOT NULL, -- JSON array

    [AuthToken] NVARCHAR(500) NOT NULL,
    [IsActive] BIT NOT NULL DEFAULT 1,

    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [DisabledAt] DATETIME2 NULL,

    [SigningSecret] NVARCHAR(500) NULL,
    [LastTestAt] DATETIME2 NULL,
    [FailureCount] INT NOT NULL DEFAULT 0,

    CONSTRAINT FK_WebhookSubscriptions_Organization FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organizations]([Id]) ON DELETE CASCADE
);

CREATE INDEX IDX_WebhookSubscriptions_Organization ON [dbo].[WebhookSubscriptions]([OrganizationId]);
CREATE INDEX IDX_WebhookSubscriptions_IsActive ON [dbo].[WebhookSubscriptions]([IsActive]) WHERE [IsActive] = 1;

-- ============================================================================
-- WEBHOOK EVENTS TABLE
-- ============================================================================

CREATE TABLE [dbo].[WebhookEvents] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
    [SubscriptionId] UNIQUEIDENTIFIER NULL,

    [EventType] NVARCHAR(100) NOT NULL,
    [EndpointUrl] NVARCHAR(MAX) NOT NULL,

    [RelatedDocumentId] UNIQUEIDENTIFIER NULL,
    [Payload] NVARCHAR(MAX) NOT NULL, -- JSON

    [TriggeredAt] DATETIME2 NOT NULL,
    [DeliveredAt] DATETIME2 NULL,
    [Status] INT NOT NULL DEFAULT 0, -- 0=Pending, 1=Delivered, 2=Failed, 3=Retrying, 4=MaxRetriesExceeded

    [RetryCount] INT NOT NULL DEFAULT 0,
    [MaxRetries] INT NOT NULL DEFAULT 5,

    [ResponseCode] INT NULL,
    [ResponseMessage] NVARCHAR(MAX) NULL,

    [ScheduledRetryAt] DATETIME2 NULL,

    CONSTRAINT FK_WebhookEvents_Organization FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organizations]([Id]) ON DELETE CASCADE,
    CONSTRAINT FK_WebhookEvents_Subscription FOREIGN KEY ([SubscriptionId])
        REFERENCES [dbo].[WebhookSubscriptions]([Id]) ON DELETE SET NULL,
    CONSTRAINT FK_WebhookEvents_Document FOREIGN KEY ([RelatedDocumentId])
        REFERENCES [dbo].[Documents]([Id]) ON DELETE SET NULL
);

CREATE INDEX IDX_WebhookEvents_Organization ON [dbo].[WebhookEvents]([OrganizationId]);
CREATE INDEX IDX_WebhookEvents_Status ON [dbo].[WebhookEvents]([Status]);
CREATE INDEX IDX_WebhookEvents_TriggeredAt ON [dbo].[WebhookEvents]([TriggeredAt] DESC);
CREATE INDEX IDX_WebhookEvents_ScheduledRetryAt ON [dbo].[WebhookEvents]([ScheduledRetryAt]) WHERE [Status] = 3;
CREATE INDEX IDX_WebhookEvents_EventType ON [dbo].[WebhookEvents]([EventType]);

-- ============================================================================
-- EXTERNAL INTEGRATIONS TABLE
-- ============================================================================

CREATE TABLE [dbo].[ExternalIntegrations] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,

    [IntegrationType] NVARCHAR(100) NOT NULL, -- Slack, Teams, Zapier, CustomAPI
    [IntegrationName] NVARCHAR(500) NOT NULL,

    [Credentials] NVARCHAR(MAX) NOT NULL, -- JSON (encrypted ideally)
    [Configuration] NVARCHAR(MAX) NULL, -- JSON

    [IsActive] BIT NOT NULL DEFAULT 1,
    [ConnectedAt] DATETIME2 NOT NULL,
    [LastSyncAt] DATETIME2 NULL,

    [SyncIntervalMinutes] INT NOT NULL DEFAULT 60,
    [NextSyncAt] DATETIME2 NULL,

    [LastErrorMessage] NVARCHAR(MAX) NULL,
    [SyncCount] INT NOT NULL DEFAULT 0,
    [FailureCount] INT NOT NULL DEFAULT 0,

    CONSTRAINT FK_ExternalIntegrations_Organization FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organizations]([Id]) ON DELETE CASCADE
);

CREATE INDEX IDX_ExternalIntegrations_Organization ON [dbo].[ExternalIntegrations]([OrganizationId]);
CREATE INDEX IDX_ExternalIntegrations_IsActive ON [dbo].[ExternalIntegrations]([IsActive]) WHERE [IsActive] = 1;
CREATE INDEX IDX_ExternalIntegrations_NextSyncAt ON [dbo].[ExternalIntegrations]([NextSyncAt]) WHERE [IsActive] = 1;
CREATE INDEX IDX_ExternalIntegrations_IntegrationType ON [dbo].[ExternalIntegrations]([IntegrationType]);

-- ============================================================================
-- STORED PROCEDURES
-- ============================================================================

CREATE PROCEDURE [dbo].[sp_GetUserNotifications]
    @UserId UNIQUEIDENTIFIER,
    @UnreadOnly BIT = 0
AS
BEGIN
    IF @UnreadOnly = 1
    BEGIN
        SELECT
            [Id],
            [Title],
            [Message],
            [Type],
            [CreatedAt],
            [IsRead]
        FROM [dbo].[Notifications]
        WHERE [UserId] = @UserId
        AND [IsRead] = 0
        ORDER BY [CreatedAt] DESC;
    END
    ELSE
    BEGIN
        SELECT
            [Id],
            [Title],
            [Message],
            [Type],
            [CreatedAt],
            [IsRead]
        FROM [dbo].[Notifications]
        WHERE [UserId] = @UserId
        ORDER BY [CreatedAt] DESC;
    END
END;
GO

CREATE PROCEDURE [dbo].[sp_GetFailedEmails]
    @OrganizationId UNIQUEIDENTIFIER,
    @Days INT = 7
AS
BEGIN
    SELECT
        e.[Id],
        e.[RecipientEmail],
        e.[Subject],
        e.[Status],
        e.[FailureReason],
        e.[RetryCount],
        e.[CreatedAt]
    FROM [dbo].[EmailNotifications] e
    WHERE e.[Status] IN (2, 3) -- Failed, Bounced
    AND e.[CreatedAt] >= DATEADD(DAY, -@Days, GETUTCDATE())
    ORDER BY e.[CreatedAt] DESC;
END;
GO

CREATE PROCEDURE [dbo].[sp_GetPendingWebhookEvents]
    @MaxRetries INT = 5
AS
BEGIN
    SELECT
        [Id],
        [OrganizationId],
        [EventType],
        [EndpointUrl],
        [Payload],
        [RetryCount],
        [ScheduledRetryAt]
    FROM [dbo].[WebhookEvents]
    WHERE [Status] IN (0, 3) -- Pending or Retrying
    AND [RetryCount] < @MaxRetries
    AND ([ScheduledRetryAt] IS NULL OR [ScheduledRetryAt] <= GETUTCDATE())
    ORDER BY [TriggeredAt] ASC;
END;
GO

CREATE PROCEDURE [dbo].[sp_GetDueIntegrationSyncs]
AS
BEGIN
    SELECT
        [Id],
        [OrganizationId],
        [IntegrationType],
        [LastSyncAt],
        [SyncIntervalMinutes]
    FROM [dbo].[ExternalIntegrations]
    WHERE [IsActive] = 1
    AND ([LastSyncAt] IS NULL OR [NextSyncAt] <= GETUTCDATE())
    ORDER BY [NextSyncAt] ASC;
END;
GO

-- ============================================================================
-- VIEWS FOR NOTIFICATIONS & INTEGRATIONS
-- ============================================================================

CREATE VIEW [dbo].[vw_UnreadNotifications]
AS
SELECT
    n.[Id],
    n.[UserId],
    n.[Title],
    n.[Type],
    n.[CreatedAt],
    DATEDIFF(HOUR, n.[CreatedAt], GETUTCDATE()) AS HoursAgo
FROM [dbo].[Notifications] n
WHERE n.[IsRead] = 0
ORDER BY n.[CreatedAt] DESC;
GO

CREATE VIEW [dbo].[vw_EmailDeliveryStats]
AS
SELECT
    CAST([SentAt] AS DATE) AS DeliveryDate,
    [Status],
    COUNT(*) AS EmailCount,
    AVG(DATEDIFF(SECOND, [CreatedAt], [SentAt])) AS AvgDeliverySeconds
FROM [dbo].[EmailNotifications]
WHERE [SentAt] IS NOT NULL
GROUP BY CAST([SentAt] AS DATE), [Status]
ORDER BY [DeliveryDate] DESC;
GO

CREATE VIEW [dbo].[vw_WebhookHealthStatus]
AS
SELECT
    ws.[Id],
    ws.[EndpointUrl],
    COUNT(we.[Id]) AS TotalEvents,
    SUM(CASE WHEN we.[Status] = 1 THEN 1 ELSE 0 END) AS DeliveredCount,
    SUM(CASE WHEN we.[Status] IN (2, 4) THEN 1 ELSE 0 END) AS FailedCount,
    MAX(we.[DeliveredAt]) AS LastDelivery,
    ws.[FailureCount]
FROM [dbo].[WebhookSubscriptions] ws
LEFT JOIN [dbo].[WebhookEvents] we ON ws.[Id] = we.[SubscriptionId]
WHERE ws.[IsActive] = 1
GROUP BY ws.[Id], ws.[EndpointUrl], ws.[FailureCount]
ORDER BY ws.[FailureCount] DESC;
GO

CREATE VIEW [dbo].[vw_IntegrationSyncStatus]
AS
SELECT
    [Id],
    [OrganizationId],
    [IntegrationType],
    [IntegrationName],
    [IsActive],
    [LastSyncAt],
    [NextSyncAt],
    [FailureCount],
    CASE
        WHEN [LastSyncAt] IS NULL THEN 'Never'
        WHEN DATEDIFF(HOUR, [LastSyncAt], GETUTCDATE()) < 1 THEN 'Recently'
        WHEN DATEDIFF(HOUR, [LastSyncAt], GETUTCDATE()) < 24 THEN 'Today'
        ELSE 'Outdated'
    END AS SyncStatus
FROM [dbo].[ExternalIntegrations]
ORDER BY [LastSyncAt] DESC;
GO
