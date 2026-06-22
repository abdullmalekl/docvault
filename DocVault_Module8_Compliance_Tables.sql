-- ============================================================================
-- DOCVAULT MODULE 8: COMPLIANCE & AUDIT LOGGING SCHEMA
-- ============================================================================

-- ============================================================================
-- AUDIT LOG TABLE
-- ============================================================================

CREATE TABLE [dbo].[AuditLog] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
    [UserId] UNIQUEIDENTIFIER NULL,

    [ActionType] INT NOT NULL, -- 0=Create, 1=Read, 2=Update, 3=Delete, 4=Share, 5=ChangePermission, 6=Download, 7=Archive, 8=Restore, 9=Approve, 10=Reject
    [EntityType] NVARCHAR(100) NOT NULL,
    [EntityId] NVARCHAR(36) NULL,

    [Description] NVARCHAR(MAX) NULL,
    [PerformedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    [IpAddress] NVARCHAR(45) NULL,
    [UserAgent] NVARCHAR(MAX) NULL,

    [ChangeDetails] NVARCHAR(MAX) NULL, -- JSON
    [Result] NVARCHAR(50) NOT NULL DEFAULT 'Success', -- Success, Failure, Partial

    CONSTRAINT FK_AuditLog_Organization FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organizations]([Id]) ON DELETE CASCADE,
    CONSTRAINT FK_AuditLog_User FOREIGN KEY ([UserId])
        REFERENCES [dbo].[Users]([Id]) ON DELETE SET NULL
);

CREATE INDEX IDX_AuditLog_Organization ON [dbo].[AuditLog]([OrganizationId]);
CREATE INDEX IDX_AuditLog_User ON [dbo].[AuditLog]([UserId]);
CREATE INDEX IDX_AuditLog_PerformedAt ON [dbo].[AuditLog]([PerformedAt] DESC);
CREATE INDEX IDX_AuditLog_ActionType ON [dbo].[AuditLog]([ActionType]);
CREATE INDEX IDX_AuditLog_EntityType ON [dbo].[AuditLog]([EntityType]);
CREATE INDEX IDX_AuditLog_Result ON [dbo].[AuditLog]([Result]) WHERE [Result] = 'Failure';

-- ============================================================================
-- COMPLIANCE REPORT TABLE
-- ============================================================================

CREATE TABLE [dbo].[ComplianceReports] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,

    [ReportName] NVARCHAR(500) NOT NULL,
    [Framework] INT NOT NULL, -- 0=GDPR, 1=SOC2, 2=ISO27001, 3=HIPAA, 4=PCI_DSS, 5=Custom

    [ReportPeriodStart] DATETIME2 NOT NULL,
    [ReportPeriodEnd] DATETIME2 NOT NULL,
    [GeneratedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    [TotalDocuments] INT NOT NULL DEFAULT 0,
    [ControlsAssessed] INT NOT NULL DEFAULT 0,
    [ComplianceScore] INT NOT NULL DEFAULT 0, -- 0-100

    [Recommendations] NVARCHAR(MAX) NULL, -- JSON array

    CONSTRAINT FK_ComplianceReports_Organization FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organizations]([Id]) ON DELETE CASCADE,
    CONSTRAINT CK_ComplianceReports_Score CHECK ([ComplianceScore] BETWEEN 0 AND 100)
);

CREATE INDEX IDX_ComplianceReports_Organization ON [dbo].[ComplianceReports]([OrganizationId]);
CREATE INDEX IDX_ComplianceReports_Framework ON [dbo].[ComplianceReports]([Framework]);
CREATE INDEX IDX_ComplianceReports_GeneratedAt ON [dbo].[ComplianceReports]([GeneratedAt] DESC);

-- ============================================================================
-- COMPLIANCE FINDINGS TABLE
-- ============================================================================

CREATE TABLE [dbo].[ComplianceFindings] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [ReportId] UNIQUEIDENTIFIER NOT NULL,

    [ControlId] NVARCHAR(100) NOT NULL,
    [Title] NVARCHAR(500) NOT NULL,
    [Description] NVARCHAR(MAX) NOT NULL,

    [Severity] INT NOT NULL, -- 0=Low, 1=Medium, 2=High, 3=Critical
    [Status] INT NOT NULL DEFAULT 0, -- 0=Open, 1=InProgress, 2=Resolved, 3=Accepted, 4=Deferred

    [Remediation] NVARCHAR(MAX) NULL,
    [DueDate] DATETIME2 NULL,
    [ResolvedAt] DATETIME2 NULL,

    [Notes] NVARCHAR(MAX) NULL,

    CONSTRAINT FK_ComplianceFindings_Report FOREIGN KEY ([ReportId])
        REFERENCES [dbo].[ComplianceReports]([Id]) ON DELETE CASCADE
);

CREATE INDEX IDX_ComplianceFindings_Report ON [dbo].[ComplianceFindings]([ReportId]);
CREATE INDEX IDX_ComplianceFindings_Severity ON [dbo].[ComplianceFindings]([Severity]);
CREATE INDEX IDX_ComplianceFindings_Status ON [dbo].[ComplianceFindings]([Status]);
CREATE INDEX IDX_ComplianceFindings_DueDate ON [dbo].[ComplianceFindings]([DueDate]) WHERE [Status] != 2;

-- ============================================================================
-- DATA RETENTION SCHEDULE TABLE
-- ============================================================================

CREATE TABLE [dbo].[DataRetentionSchedules] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,

    [DocumentType] NVARCHAR(500) NOT NULL,
    [RetentionYears] INT NOT NULL,

    [ActionOnExpiry] INT NOT NULL DEFAULT 0, -- 0=Archive, 1=Delete, 2=Notify, 3=ArchiveThenDelete
    [NotifyBeforeExpiry] BIT NOT NULL DEFAULT 1,
    [NotifyDaysBefore] INT NOT NULL DEFAULT 30,

    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [LastModifiedAt] DATETIME2 NULL,

    CONSTRAINT FK_DataRetentionSchedules_Organization FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organizations]([Id]) ON DELETE CASCADE,
    CONSTRAINT UQ_DataRetentionSchedules_Type UNIQUE ([OrganizationId], [DocumentType])
);

CREATE INDEX IDX_DataRetentionSchedules_Organization ON [dbo].[DataRetentionSchedules]([OrganizationId]);
CREATE INDEX IDX_DataRetentionSchedules_RetentionYears ON [dbo].[DataRetentionSchedules]([RetentionYears]);

-- ============================================================================
-- RETENTION AUDIT TRAIL TABLE
-- ============================================================================

CREATE TABLE [dbo].[RetentionAuditTrail] (
    [Id] BIGINT NOT NULL PRIMARY KEY IDENTITY(1,1),
    [DocumentId] UNIQUEIDENTIFIER NOT NULL,
    [ScheduleId] UNIQUEIDENTIFIER NOT NULL,

    [Action] NVARCHAR(50) NOT NULL, -- Archived, Deleted, NotificationSent, Scheduled
    [ExecutedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [ExpiryDate] DATETIME2 NOT NULL,

    [ExecutedByUserId] UNIQUEIDENTIFIER NULL,
    [Details] NVARCHAR(MAX) NULL,

    CONSTRAINT FK_RetentionAuditTrail_Document FOREIGN KEY ([DocumentId])
        REFERENCES [dbo].[Documents]([Id]),
    CONSTRAINT FK_RetentionAuditTrail_Schedule FOREIGN KEY ([ScheduleId])
        REFERENCES [dbo].[DataRetentionSchedules]([Id])
);

CREATE INDEX IDX_RetentionAuditTrail_Document ON [dbo].[RetentionAuditTrail]([DocumentId]);
CREATE INDEX IDX_RetentionAuditTrail_ExecutedAt ON [dbo].[RetentionAuditTrail]([ExecutedAt] DESC);
CREATE INDEX IDX_RetentionAuditTrail_Action ON [dbo].[RetentionAuditTrail]([Action]);

-- ============================================================================
-- DATA CLASSIFICATION POLICY TABLE
-- ============================================================================

CREATE TABLE [dbo].[DataClassificationPolicies] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,

    [PolicyName] NVARCHAR(500) NOT NULL,
    [Description] NVARCHAR(MAX) NULL,

    [ClassificationRules] NVARCHAR(MAX) NOT NULL, -- JSON
    [SensitiveKeywords] NVARCHAR(MAX) NOT NULL, -- JSON array

    [IsActive] BIT NOT NULL DEFAULT 1,
    [LastUpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedByUserId] UNIQUEIDENTIFIER NULL,

    CONSTRAINT FK_DataClassificationPolicies_Organization FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organizations]([Id]) ON DELETE CASCADE,
    CONSTRAINT FK_DataClassificationPolicies_UpdatedBy FOREIGN KEY ([UpdatedByUserId])
        REFERENCES [dbo].[Users]([Id]) ON DELETE SET NULL
);

CREATE INDEX IDX_DataClassificationPolicies_Organization ON [dbo].[DataClassificationPolicies]([OrganizationId]);
CREATE INDEX IDX_DataClassificationPolicies_IsActive ON [dbo].[DataClassificationPolicies]([IsActive]) WHERE [IsActive] = 1;

-- ============================================================================
-- STORED PROCEDURES
-- ============================================================================

CREATE PROCEDURE [dbo].[sp_GetAuditTrail]
    @OrganizationId UNIQUEIDENTIFIER,
    @FromDate DATETIME2,
    @ToDate DATETIME2,
    @Top INT = 1000
AS
BEGIN
    SELECT TOP (@Top)
        [Id],
        [UserId],
        [ActionType],
        [EntityType],
        [EntityId],
        [Description],
        [PerformedAt],
        [Result]
    FROM [dbo].[AuditLog]
    WHERE [OrganizationId] = @OrganizationId
    AND [PerformedAt] BETWEEN @FromDate AND @ToDate
    ORDER BY [PerformedAt] DESC;
END;
GO

CREATE PROCEDURE [dbo].[sp_GenerateComplianceReport]
    @OrganizationId UNIQUEIDENTIFIER,
    @Framework INT,
    @FromDate DATETIME2,
    @ToDate DATETIME2
AS
BEGIN
    DECLARE @ReportId UNIQUEIDENTIFIER = NEWID();

    INSERT INTO [dbo].[ComplianceReports] (
        [Id], [OrganizationId], [ReportName], [Framework],
        [ReportPeriodStart], [ReportPeriodEnd], [GeneratedAt], [ComplianceScore]
    )
    VALUES (
        @ReportId, @OrganizationId,
        CONCAT('Compliance Report - ', FORMAT(GETUTCDATE(), 'yyyy-MM-dd')),
        @Framework,
        @FromDate, @ToDate, GETUTCDATE(), 100
    );

    SELECT @ReportId AS ReportId;
END;
GO

CREATE PROCEDURE [dbo].[sp_IdentifyExpiredDocuments]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SELECT d.[Id], d.[Title], s.[DocumentType], s.[RetentionYears]
    FROM [dbo].[Documents] d
    INNER JOIN [dbo].[DataRetentionSchedules] s
        ON d.[DocumentType] = s.[DocumentType]
    WHERE d.[OrganizationId] = @OrganizationId
    AND d.[IsArchived] = 0
    AND DATEDIFF(YEAR, d.[CreatedAt], GETUTCDATE()) >= s.[RetentionYears];
END;
GO

CREATE PROCEDURE [dbo].[sp_GetUpcomingExpiryDates]
    @OrganizationId UNIQUEIDENTIFIER,
    @DaysAhead INT = 30
AS
BEGIN
    DECLARE @ThresholdDate DATETIME2 = DATEADD(DAY, @DaysAhead, GETUTCDATE());

    SELECT DISTINCT
        DATEADD(YEAR, s.[RetentionYears], d.[CreatedAt]) AS ExpiryDate,
        s.[DocumentType],
        COUNT(d.[Id]) AS DocumentCount
    FROM [dbo].[Documents] d
    INNER JOIN [dbo].[DataRetentionSchedules] s
        ON d.[DocumentType] = s.[DocumentType]
    WHERE d.[OrganizationId] = @OrganizationId
    AND d.[IsArchived] = 0
    AND DATEADD(YEAR, s.[RetentionYears], d.[CreatedAt]) <= @ThresholdDate
    AND DATEADD(YEAR, s.[RetentionYears], d.[CreatedAt]) > GETUTCDATE()
    GROUP BY s.[DocumentType], s.[RetentionYears], d.[CreatedAt]
    ORDER BY ExpiryDate ASC;
END;
GO

-- ============================================================================
-- VIEWS FOR COMPLIANCE & AUDIT
-- ============================================================================

CREATE VIEW [dbo].[vw_ComplianceSummary]
AS
SELECT
    [OrganizationId],
    [Framework],
    MAX([ComplianceScore]) AS LatestScore,
    MAX([GeneratedAt]) AS LastReportDate,
    COUNT(*) AS TotalReports
FROM [dbo].[ComplianceReports]
WHERE [GeneratedAt] >= DATEADD(DAY, -365, GETUTCDATE())
GROUP BY [OrganizationId], [Framework];
GO

CREATE VIEW [dbo].[vw_OpenFindings]
AS
SELECT
    cf.[Id],
    cf.[ControlId],
    cf.[Title],
    cf.[Severity],
    cf.[DueDate],
    cr.[Framework],
    DATEDIFF(DAY, GETUTCDATE(), cf.[DueDate]) AS DaysOverdue
FROM [dbo].[ComplianceFindings] cf
INNER JOIN [dbo].[ComplianceReports] cr ON cf.[ReportId] = cr.[Id]
WHERE cf.[Status] IN (0, 1); -- Open or InProgress
GO

CREATE VIEW [dbo].[vw_AuditSummary]
AS
SELECT
    CAST([PerformedAt] AS DATE) AS AuditDate,
    [ActionType],
    COUNT(*) AS ActionCount,
    SUM(CASE WHEN [Result] = 'Success' THEN 1 ELSE 0 END) AS SuccessCount,
    SUM(CASE WHEN [Result] = 'Failure' THEN 1 ELSE 0 END) AS FailureCount
FROM [dbo].[AuditLog]
WHERE [PerformedAt] >= DATEADD(DAY, -90, GETUTCDATE())
GROUP BY CAST([PerformedAt] AS DATE), [ActionType]
ORDER BY [AuditDate] DESC, [ActionType];
GO

CREATE VIEW [dbo].[vw_UserActivitySummary]
AS
SELECT
    [UserId],
    COUNT(*) AS TotalActions,
    COUNT(DISTINCT [ActionType]) AS UniqueActionTypes,
    MAX([PerformedAt]) AS LastActivity,
    SUM(CASE WHEN [Result] = 'Success' THEN 1 ELSE 0 END) AS SuccessfulActions
FROM [dbo].[AuditLog]
WHERE [UserId] IS NOT NULL
AND [PerformedAt] >= DATEADD(DAY, -30, GETUTCDATE())
GROUP BY [UserId];
GO
