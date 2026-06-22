-- ============================================================================
-- DOCVAULT MODULE 10: REPORTING & ANALYTICS SCHEMA
-- ============================================================================

-- ============================================================================
-- DOCUMENT METRICS TABLE
-- ============================================================================

CREATE TABLE [dbo].[DocumentMetrics] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,

    [TotalDocuments] INT NOT NULL DEFAULT 0,
    [TotalDocumentSize] BIGINT NOT NULL DEFAULT 0,

    [DocumentsCreatedToday] INT NOT NULL DEFAULT 0,
    [DocumentsCreatedThisMonth] INT NOT NULL DEFAULT 0,

    [DocumentsArchivedToday] INT NOT NULL DEFAULT 0,
    [DocumentsDeletedThisMonth] INT NOT NULL DEFAULT 0,

    [DocumentsByStatus] NVARCHAR(MAX) NULL, -- JSON
    [DocumentsByClassification] NVARCHAR(MAX) NULL, -- JSON

    [CalculatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_DocumentMetrics_Organization FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organizations]([Id]) ON DELETE CASCADE
);

CREATE INDEX IDX_DocumentMetrics_Organization ON [dbo].[DocumentMetrics]([OrganizationId]);
CREATE INDEX IDX_DocumentMetrics_CalculatedAt ON [dbo].[DocumentMetrics]([CalculatedAt] DESC);

-- ============================================================================
-- ACCESS METRICS TABLE
-- ============================================================================

CREATE TABLE [dbo].[AccessMetrics] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,

    [TotalAccessRequests] INT NOT NULL DEFAULT 0,
    [GrantedAccessRequests] INT NOT NULL DEFAULT 0,
    [DeniedAccessRequests] INT NOT NULL DEFAULT 0,

    [UniqueUsersWithAccess] INT NOT NULL DEFAULT 0,
    [PermissionsExpiredToday] INT NOT NULL DEFAULT 0,

    [AccessByRole] NVARCHAR(MAX) NULL, -- JSON
    [MostAccessedDocuments] NVARCHAR(MAX) NULL, -- JSON array

    [CalculatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_AccessMetrics_Organization FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organizations]([Id]) ON DELETE CASCADE
);

CREATE INDEX IDX_AccessMetrics_Organization ON [dbo].[AccessMetrics]([OrganizationId]);
CREATE INDEX IDX_AccessMetrics_CalculatedAt ON [dbo].[AccessMetrics]([CalculatedAt] DESC);

-- ============================================================================
-- WORKFLOW METRICS TABLE
-- ============================================================================

CREATE TABLE [dbo].[WorkflowMetrics] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,

    [ActiveWorkflows] INT NOT NULL DEFAULT 0,
    [CompletedWorkflows] INT NOT NULL DEFAULT 0,
    [RejectedWorkflows] INT NOT NULL DEFAULT 0,

    [AvgApprovalTimeHours] FLOAT NOT NULL DEFAULT 0,
    [OverdueApprovals] INT NOT NULL DEFAULT 0,

    [WorkflowsByStatus] NVARCHAR(MAX) NULL, -- JSON
    [ApprovalTimeByStage] NVARCHAR(MAX) NULL, -- JSON

    [CalculatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_WorkflowMetrics_Organization FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organizations]([Id]) ON DELETE CASCADE
);

CREATE INDEX IDX_WorkflowMetrics_Organization ON [dbo].[WorkflowMetrics]([OrganizationId]);
CREATE INDEX IDX_WorkflowMetrics_CalculatedAt ON [dbo].[WorkflowMetrics]([CalculatedAt] DESC);

-- ============================================================================
-- STORAGE METRICS TABLE
-- ============================================================================

CREATE TABLE [dbo].[StorageMetrics] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,

    [TotalStorageUsedBytes] BIGINT NOT NULL DEFAULT 0,
    [StorageQuotaBytes] BIGINT NOT NULL DEFAULT 1099511627776, -- 1TB

    [StorageUtilizationPercent] FLOAT NOT NULL DEFAULT 0,

    [StorageByDocumentType] NVARCHAR(MAX) NULL, -- JSON
    [LargestDocuments] NVARCHAR(MAX) NULL, -- JSON array

    [CalculatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_StorageMetrics_Organization FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organizations]([Id]) ON DELETE CASCADE
);

CREATE INDEX IDX_StorageMetrics_Organization ON [dbo].[StorageMetrics]([OrganizationId]);
CREATE INDEX IDX_StorageMetrics_CalculatedAt ON [dbo].[StorageMetrics]([CalculatedAt] DESC);

-- ============================================================================
-- USER ACTIVITY TABLE
-- ============================================================================

CREATE TABLE [dbo].[UserActivityMetrics] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,

    [DocumentsCreated] INT NOT NULL DEFAULT 0,
    [DocumentsViewed] INT NOT NULL DEFAULT 0,
    [DocumentsApproved] INT NOT NULL DEFAULT 0,
    [DocumentsShared] INT NOT NULL DEFAULT 0,

    [LastActivityAt] DATETIME2 NULL,
    [CalculatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_UserActivityMetrics_User FOREIGN KEY ([UserId])
        REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE,
    CONSTRAINT FK_UserActivityMetrics_Organization FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organizations]([Id]) ON DELETE CASCADE
);

CREATE INDEX IDX_UserActivityMetrics_Organization ON [dbo].[UserActivityMetrics]([OrganizationId]);
CREATE INDEX IDX_UserActivityMetrics_DocumentsCreated ON [dbo].[UserActivityMetrics]([DocumentsCreated] DESC);
CREATE INDEX IDX_UserActivityMetrics_LastActivityAt ON [dbo].[UserActivityMetrics]([LastActivityAt] DESC);

-- ============================================================================
-- REPORTS TABLE
-- ============================================================================

CREATE TABLE [dbo].[Reports] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,

    [ReportName] NVARCHAR(500) NOT NULL,
    [ReportType] INT NOT NULL, -- 0=ExecutiveSummary, 1=DocumentInventory, 2=AccessControl, 3=WorkflowAnalysis, 4=StorageUtilization, 5=UserActivity, 6=Compliance, 7=Custom

    [ReportPeriodStart] DATETIME2 NOT NULL,
    [ReportPeriodEnd] DATETIME2 NOT NULL,
    [GeneratedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    [DocumentMetricsId] UNIQUEIDENTIFIER NULL,
    [AccessMetricsId] UNIQUEIDENTIFIER NULL,
    [WorkflowMetricsId] UNIQUEIDENTIFIER NULL,
    [StorageMetricsId] UNIQUEIDENTIFIER NULL,

    [KeyInsights] NVARCHAR(MAX) NULL, -- JSON array
    [ExportFormat] NVARCHAR(50) NULL, -- PDF, Excel, JSON

    [GeneratedByUserId] UNIQUEIDENTIFIER NULL,

    CONSTRAINT FK_Reports_Organization FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organizations]([Id]) ON DELETE CASCADE,
    CONSTRAINT FK_Reports_DocumentMetrics FOREIGN KEY ([DocumentMetricsId])
        REFERENCES [dbo].[DocumentMetrics]([Id]) ON DELETE SET NULL,
    CONSTRAINT FK_Reports_AccessMetrics FOREIGN KEY ([AccessMetricsId])
        REFERENCES [dbo].[AccessMetrics]([Id]) ON DELETE SET NULL,
    CONSTRAINT FK_Reports_WorkflowMetrics FOREIGN KEY ([WorkflowMetricsId])
        REFERENCES [dbo].[WorkflowMetrics]([Id]) ON DELETE SET NULL,
    CONSTRAINT FK_Reports_StorageMetrics FOREIGN KEY ([StorageMetricsId])
        REFERENCES [dbo].[StorageMetrics]([Id]) ON DELETE SET NULL
);

CREATE INDEX IDX_Reports_Organization ON [dbo].[Reports]([OrganizationId]);
CREATE INDEX IDX_Reports_ReportType ON [dbo].[Reports]([ReportType]);
CREATE INDEX IDX_Reports_GeneratedAt ON [dbo].[Reports]([GeneratedAt] DESC);

-- ============================================================================
-- REPORT SCHEDULE TABLE
-- ============================================================================

CREATE TABLE [dbo].[ReportSchedules] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,

    [ReportType] INT NOT NULL,
    [Schedule] NVARCHAR(50) NOT NULL, -- daily, weekly, monthly

    [IsActive] BIT NOT NULL DEFAULT 1,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [NextRunAt] DATETIME2 NULL,

    [LastRunAt] DATETIME2 NULL,
    [LastSuccessAt] DATETIME2 NULL,
    [FailureCount] INT NOT NULL DEFAULT 0,

    CONSTRAINT FK_ReportSchedules_Organization FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organizations]([Id]) ON DELETE CASCADE
);

CREATE INDEX IDX_ReportSchedules_Organization ON [dbo].[ReportSchedules]([OrganizationId]);
CREATE INDEX IDX_ReportSchedules_IsActive ON [dbo].[ReportSchedules]([IsActive]) WHERE [IsActive] = 1;
CREATE INDEX IDX_ReportSchedules_NextRunAt ON [dbo].[ReportSchedules]([NextRunAt]) WHERE [IsActive] = 1;

-- ============================================================================
-- STORAGE CONFIGURATION TABLE
-- ============================================================================

CREATE TABLE [dbo].[StorageConfiguration] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL UNIQUE,

    [QuotaBytes] BIGINT NOT NULL DEFAULT 1099511627776, -- 1TB
    [WarningThresholdPercent] INT NOT NULL DEFAULT 80,
    [CriticalThresholdPercent] INT NOT NULL DEFAULT 95,

    [AutoArchiveOldDocsYears] INT NULL,
    [AutoDeleteArchivedDocsYears] INT NULL,

    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [LastModifiedAt] DATETIME2 NULL,

    CONSTRAINT FK_StorageConfiguration_Organization FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organizations]([Id]) ON DELETE CASCADE
);

-- ============================================================================
-- STORED PROCEDURES
-- ============================================================================

CREATE PROCEDURE [dbo].[sp_CalculateAllMetrics]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    -- Calculate document metrics
    INSERT INTO [dbo].[DocumentMetrics] ([OrganizationId], [TotalDocuments], [TotalDocumentSize])
    SELECT
        @OrganizationId,
        COUNT(*),
        SUM(ISNULL([Size], 0))
    FROM [dbo].[Documents]
    WHERE [OrganizationId] = @OrganizationId;

    -- Calculate access metrics
    INSERT INTO [dbo].[AccessMetrics] ([OrganizationId], [TotalAccessRequests])
    SELECT
        @OrganizationId,
        COUNT(*)
    FROM [dbo].[DocumentAccessAuditLog]
    WHERE DATEPART(YEAR, [AccessAttemptTime]) = YEAR(GETUTCDATE());

    -- Calculate workflow metrics
    INSERT INTO [dbo].[WorkflowMetrics] ([OrganizationId], [ActiveWorkflows], [CompletedWorkflows])
    SELECT
        @OrganizationId,
        COUNT(CASE WHEN [Status] IN (1, 2) THEN 1 END),
        COUNT(CASE WHEN [Status] = 3 THEN 1 END)
    FROM [dbo].[DocumentWorkflows]
    WHERE [OrganizationId] = @OrganizationId;

    -- Calculate storage metrics
    INSERT INTO [dbo].[StorageMetrics] ([OrganizationId], [TotalStorageUsedBytes])
    SELECT
        @OrganizationId,
        SUM(ISNULL([Size], 0))
    FROM [dbo].[Documents]
    WHERE [OrganizationId] = @OrganizationId;
END;
GO

CREATE PROCEDURE [dbo].[sp_GetTopUsersByActivity]
    @OrganizationId UNIQUEIDENTIFIER,
    @Top INT = 10
AS
BEGIN
    SELECT TOP (@Top)
        [UserId],
        [DocumentsCreated],
        [DocumentsViewed],
        [DocumentsApproved],
        [LastActivityAt]
    FROM [dbo].[UserActivityMetrics]
    WHERE [OrganizationId] = @OrganizationId
    ORDER BY ([DocumentsCreated] + [DocumentsViewed] + [DocumentsApproved]) DESC;
END;
GO

CREATE PROCEDURE [dbo].[sp_GetStorageWarnings]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SELECT
        sm.[Id],
        sm.[TotalStorageUsedBytes],
        sc.[QuotaBytes],
        sm.[StorageUtilizationPercent],
        CASE
            WHEN sm.[StorageUtilizationPercent] >= sc.[CriticalThresholdPercent] THEN 'Critical'
            WHEN sm.[StorageUtilizationPercent] >= sc.[WarningThresholdPercent] THEN 'Warning'
            ELSE 'OK'
        END AS Status
    FROM [dbo].[StorageMetrics] sm
    LEFT JOIN [dbo].[StorageConfiguration] sc ON sm.[OrganizationId] = sc.[OrganizationId]
    WHERE sm.[OrganizationId] = @OrganizationId
    ORDER BY sm.[CalculatedAt] DESC;
END;
GO

-- ============================================================================
-- VIEWS FOR REPORTING
-- ============================================================================

CREATE VIEW [dbo].[vw_OrganizationHealthSummary]
AS
SELECT
    dm.[OrganizationId],
    dm.[TotalDocuments],
    am.[UniqueUsersWithAccess],
    wm.[ActiveWorkflows],
    wm.[OverdueApprovals],
    sm.[StorageUtilizationPercent],
    dm.[CalculatedAt]
FROM [dbo].[DocumentMetrics] dm
LEFT JOIN [dbo].[AccessMetrics] am ON dm.[OrganizationId] = am.[OrganizationId]
LEFT JOIN [dbo].[WorkflowMetrics] wm ON dm.[OrganizationId] = wm.[OrganizationId]
LEFT JOIN [dbo].[StorageMetrics] sm ON dm.[OrganizationId] = sm.[OrganizationId]
WHERE dm.[CalculatedAt] = (SELECT MAX([CalculatedAt]) FROM [dbo].[DocumentMetrics] dm2 WHERE dm2.[OrganizationId] = dm.[OrganizationId]);
GO

CREATE VIEW [dbo].[vw_ReportHistory]
AS
SELECT
    [Id],
    [OrganizationId],
    [ReportName],
    [ReportType],
    [ReportPeriodStart],
    [ReportPeriodEnd],
    [GeneratedAt],
    DATEDIFF(DAY, [ReportPeriodStart], [ReportPeriodEnd]) AS ReportDurationDays,
    DATEDIFF(DAY, [GeneratedAt], GETUTCDATE()) AS DaysSinceGenerated
FROM [dbo].[Reports]
ORDER BY [GeneratedAt] DESC;
GO

CREATE VIEW [dbo].[vw_MetricsTrends]
AS
SELECT
    dm.[OrganizationId],
    dm.[CalculatedAt],
    dm.[TotalDocuments],
    am.[TotalAccessRequests],
    wm.[ActiveWorkflows],
    sm.[StorageUtilizationPercent]
FROM [dbo].[DocumentMetrics] dm
LEFT JOIN [dbo].[AccessMetrics] am ON dm.[OrganizationId] = am.[OrganizationId] AND DATEDIFF(DAY, dm.[CalculatedAt], am.[CalculatedAt]) = 0
LEFT JOIN [dbo].[WorkflowMetrics] wm ON dm.[OrganizationId] = wm.[OrganizationId] AND DATEDIFF(DAY, dm.[CalculatedAt], wm.[CalculatedAt]) = 0
LEFT JOIN [dbo].[StorageMetrics] sm ON dm.[OrganizationId] = sm.[OrganizationId] AND DATEDIFF(DAY, dm.[CalculatedAt], sm.[CalculatedAt]) = 0
ORDER BY dm.[OrganizationId], dm.[CalculatedAt] DESC;
GO
