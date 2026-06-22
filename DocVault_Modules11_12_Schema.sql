-- ============================================================================
-- DOCVAULT MODULES 11 & 12: MIGRATION & SECURITY SCHEMA
-- ============================================================================

-- ============================================================================
-- MIGRATION LOG TABLE
-- ============================================================================

CREATE TABLE [dbo].[MigrationLogs] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [MigrationVersion] INT NOT NULL,
    [MigrationName] NVARCHAR(500) NOT NULL,

    [ExecutedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [IsSuccessful] BIT NOT NULL DEFAULT 0,
    [ErrorMessage] NVARCHAR(MAX) NULL,

    [ExecutionTimeMs] BIGINT NOT NULL,

    CONSTRAINT UQ_MigrationLogs_Version UNIQUE ([MigrationVersion])
);

CREATE INDEX IDX_MigrationLogs_ExecutedAt ON [dbo].[MigrationLogs]([ExecutedAt] DESC);

-- ============================================================================
-- SCHEMA VERSION TABLE
-- ============================================================================

CREATE TABLE [dbo].[SchemaVersions] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [VersionNumber] INT NOT NULL PRIMARY KEY,
    [Description] NVARCHAR(MAX) NOT NULL,

    [AppliedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [Changes] NVARCHAR(MAX) NOT NULL, -- JSON array

    CONSTRAINT UQ_SchemaVersions_VersionNumber UNIQUE ([VersionNumber])
);

-- ============================================================================
-- ENCRYPTION KEYS TABLE
-- ============================================================================

CREATE TABLE [dbo].[EncryptionKeys] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,

    [KeyName] NVARCHAR(500) NOT NULL,
    [Algorithm] NVARCHAR(100) NOT NULL, -- AES-256, RSA-2048
    [KeySizeBytes] INT NOT NULL,

    [EncryptedKey] VARBINARY(MAX) NOT NULL,
    [KeyFingerprint] NVARCHAR(500) NOT NULL,

    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [RotatedAt] DATETIME2 NULL,
    [ExpiresAt] DATETIME2 NULL,

    [IsActive] BIT NOT NULL DEFAULT 1,

    CONSTRAINT FK_EncryptionKeys_Organization FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organizations]([Id]) ON DELETE CASCADE,
    CONSTRAINT UQ_EncryptionKeys_Fingerprint UNIQUE ([KeyFingerprint])
);

CREATE INDEX IDX_EncryptionKeys_Organization ON [dbo].[EncryptionKeys]([OrganizationId]);
CREATE INDEX IDX_EncryptionKeys_IsActive ON [dbo].[EncryptionKeys]([IsActive]) WHERE [IsActive] = 1;
CREATE INDEX IDX_EncryptionKeys_ExpiresAt ON [dbo].[EncryptionKeys]([ExpiresAt]) WHERE [ExpiresAt] IS NOT NULL;

-- ============================================================================
-- SECURITY ALERTS TABLE
-- ============================================================================

CREATE TABLE [dbo].[SecurityAlerts] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,

    [AlertType] INT NOT NULL, -- 0=UnauthorizedAccess, 1=BruteForce, 2=PermissionChange, 3=DataExport, 4=Malicious, 5=Compliance
    [Severity] INT NOT NULL, -- 0=Low, 1=Medium, 2=High, 3=Critical

    [Title] NVARCHAR(500) NOT NULL,
    [Description] NVARCHAR(MAX) NOT NULL,

    [UserId] UNIQUEIDENTIFIER NULL,
    [IpAddress] NVARCHAR(45) NULL,

    [DetectedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [IsResolved] BIT NOT NULL DEFAULT 0,
    [Resolution] NVARCHAR(MAX) NULL,
    [ResolvedAt] DATETIME2 NULL,

    [SourceSystem] NVARCHAR(100) NULL,

    CONSTRAINT FK_SecurityAlerts_Organization FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organizations]([Id]) ON DELETE CASCADE,
    CONSTRAINT FK_SecurityAlerts_User FOREIGN KEY ([UserId])
        REFERENCES [dbo].[Users]([Id]) ON DELETE SET NULL
);

CREATE INDEX IDX_SecurityAlerts_Organization ON [dbo].[SecurityAlerts]([OrganizationId]);
CREATE INDEX IDX_SecurityAlerts_Severity ON [dbo].[SecurityAlerts]([Severity]);
CREATE INDEX IDX_SecurityAlerts_IsResolved ON [dbo].[SecurityAlerts]([IsResolved]) WHERE [IsResolved] = 0;
CREATE INDEX IDX_SecurityAlerts_DetectedAt ON [dbo].[SecurityAlerts]([DetectedAt] DESC);
CREATE INDEX IDX_SecurityAlerts_AlertType ON [dbo].[SecurityAlerts]([AlertType]);

-- ============================================================================
-- THREAT INDICATORS TABLE
-- ============================================================================

CREATE TABLE [dbo].[ThreatIndicators] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,

    [IndicatorType] NVARCHAR(100) NOT NULL, -- BruteForce, UnusualAccess, DataExfiltration
    [RiskScore] NVARCHAR(50) NOT NULL, -- Low, Medium, High, Critical

    [DetectedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [Evidence] NVARCHAR(MAX) NOT NULL, -- JSON
    [RecommendedActions] NVARCHAR(MAX) NULL, -- JSON array

    [AddressedAt] DATETIME2 NULL,

    CONSTRAINT FK_ThreatIndicators_Organization FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organizations]([Id]) ON DELETE CASCADE
);

CREATE INDEX IDX_ThreatIndicators_Organization ON [dbo].[ThreatIndicators]([OrganizationId]);
CREATE INDEX IDX_ThreatIndicators_RiskScore ON [dbo].[ThreatIndicators]([RiskScore]);
CREATE INDEX IDX_ThreatIndicators_DetectedAt ON [dbo].[ThreatIndicators]([DetectedAt] DESC);

-- ============================================================================
-- FAILED ACCESS ATTEMPTS TABLE
-- ============================================================================

CREATE TABLE [dbo].[FailedAccessAttempts] (
    [Id] BIGINT NOT NULL PRIMARY KEY IDENTITY(1,1),
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,

    [AttemptedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [IpAddress] NVARCHAR(45) NULL,
    [Reason] NVARCHAR(500) NOT NULL,

    [DocumentId] UNIQUEIDENTIFIER NULL,

    CONSTRAINT FK_FailedAccessAttempts_User FOREIGN KEY ([UserId])
        REFERENCES [dbo].[Users]([Id]),
    CONSTRAINT FK_FailedAccessAttempts_Organization FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organizations]([Id]),
    CONSTRAINT FK_FailedAccessAttempts_Document FOREIGN KEY ([DocumentId])
        REFERENCES [dbo].[Documents]([Id]) ON DELETE SET NULL
);

CREATE INDEX IDX_FailedAccessAttempts_User ON [dbo].[FailedAccessAttempts]([UserId]);
CREATE INDEX IDX_FailedAccessAttempts_AttemptedAt ON [dbo].[FailedAccessAttempts]([AttemptedAt] DESC);
CREATE INDEX IDX_FailedAccessAttempts_IpAddress ON [dbo].[FailedAccessAttempts]([IpAddress]);

-- ============================================================================
-- KEY ROTATION HISTORY TABLE
-- ============================================================================

CREATE TABLE [dbo].[KeyRotationHistory] (
    [Id] BIGINT NOT NULL PRIMARY KEY IDENTITY(1,1),
    [KeyId] UNIQUEIDENTIFIER NOT NULL,

    [OldKeyFingerprint] NVARCHAR(500) NOT NULL,
    [NewKeyFingerprint] NVARCHAR(500) NOT NULL,

    [RotatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [Reason] NVARCHAR(500) NULL,
    [RotatedByUserId] UNIQUEIDENTIFIER NULL,

    [Status] NVARCHAR(50) NOT NULL DEFAULT 'Completed', -- Scheduled, InProgress, Completed, Failed

    CONSTRAINT FK_KeyRotationHistory_Key FOREIGN KEY ([KeyId])
        REFERENCES [dbo].[EncryptionKeys]([Id]) ON DELETE CASCADE,
    CONSTRAINT FK_KeyRotationHistory_User FOREIGN KEY ([RotatedByUserId])
        REFERENCES [dbo].[Users]([Id]) ON DELETE SET NULL
);

CREATE INDEX IDX_KeyRotationHistory_KeyId ON [dbo].[KeyRotationHistory]([KeyId]);
CREATE INDEX IDX_KeyRotationHistory_RotatedAt ON [dbo].[KeyRotationHistory]([RotatedAt] DESC);

-- ============================================================================
-- STORED PROCEDURES
-- ============================================================================

CREATE PROCEDURE [dbo].[sp_GetMigrationHistory]
    @Limit INT = 50
AS
BEGIN
    SELECT TOP (@Limit)
        [Id],
        [MigrationVersion],
        [MigrationName],
        [ExecutedAt],
        [IsSuccessful],
        [ErrorMessage],
        [ExecutionTimeMs]
    FROM [dbo].[MigrationLogs]
    ORDER BY [ExecutedAt] DESC;
END;
GO

CREATE PROCEDURE [dbo].[sp_GetSecurityAlertsForOrg]
    @OrganizationId UNIQUEIDENTIFIER,
    @DaysBack INT = 30
AS
BEGIN
    SELECT
        [Id],
        [AlertType],
        [Severity],
        [Title],
        [Description],
        [DetectedAt],
        [IsResolved]
    FROM [dbo].[SecurityAlerts]
    WHERE [OrganizationId] = @OrganizationId
    AND [DetectedAt] >= DATEADD(DAY, -@DaysBack, GETUTCDATE())
    ORDER BY [Severity] DESC, [DetectedAt] DESC;
END;
GO

CREATE PROCEDURE [dbo].[sp_DetectBruteForce]
    @UserId UNIQUEIDENTIFIER,
    @WindowMinutes INT = 15,
    @MaxAttempts INT = 5
AS
BEGIN
    DECLARE @FailureCount INT = (
        SELECT COUNT(*)
        FROM [dbo].[FailedAccessAttempts]
        WHERE [UserId] = @UserId
        AND [AttemptedAt] >= DATEADD(MINUTE, -@WindowMinutes, GETUTCDATE())
    );

    IF @FailureCount >= @MaxAttempts
    BEGIN
        INSERT INTO [dbo].[SecurityAlerts] ([OrganizationId], [AlertType], [Severity], [Title], [Description], [UserId])
        SELECT
            (SELECT [OrganizationId] FROM [dbo].[Users] WHERE [Id] = @UserId),
            1, -- BruteForce
            3, -- Critical
            'Brute Force Attack Detected',
            CONCAT('User ', @UserId, ' attempted ', @FailureCount, ' failed logins in ', @WindowMinutes, ' minutes'),
            @UserId;

        SELECT 1 AS BruteForceDetected;
    END
    ELSE
    BEGIN
        SELECT 0 AS BruteForceDetected;
    END
END;
GO

-- ============================================================================
-- VIEWS FOR SECURITY & COMPLIANCE
-- ============================================================================

CREATE VIEW [dbo].[vw_SecurityAlertSummary]
AS
SELECT
    [OrganizationId],
    [Severity],
    COUNT(*) AS AlertCount,
    SUM(CASE WHEN [IsResolved] = 0 THEN 1 ELSE 0 END) AS UnresolvedCount
FROM [dbo].[SecurityAlerts]
WHERE [DetectedAt] >= DATEADD(DAY, -30, GETUTCDATE())
GROUP BY [OrganizationId], [Severity]
ORDER BY [Severity] DESC;
GO

CREATE VIEW [dbo].[vw_FailedAccessTrends]
AS
SELECT
    CAST([AttemptedAt] AS DATE) AS AttemptDate,
    COUNT(*) AS FailureCount,
    COUNT(DISTINCT [UserId]) AS UniqueUsers,
    COUNT(DISTINCT [IpAddress]) AS UniqueIPs
FROM [dbo].[FailedAccessAttempts]
WHERE [AttemptedAt] >= DATEADD(DAY, -30, GETUTCDATE())
GROUP BY CAST([AttemptedAt] AS DATE)
ORDER BY [AttemptDate] DESC;
GO

CREATE VIEW [dbo].[vw_KeyHealthStatus]
AS
SELECT
    ek.[Id],
    ek.[OrganizationId],
    ek.[KeyName],
    ek.[Algorithm],
    ek.[IsActive],
    ek.[CreatedAt],
    ek.[ExpiresAt],
    CASE
        WHEN ek.[ExpiresAt] IS NOT NULL AND ek.[ExpiresAt] < GETUTCDATE() THEN 'Expired'
        WHEN ek.[ExpiresAt] IS NOT NULL AND DATEDIFF(DAY, GETUTCDATE(), ek.[ExpiresAt]) <= 30 THEN 'Expiring Soon'
        ELSE 'Active'
    END AS Status
FROM [dbo].[EncryptionKeys] ek
WHERE ek.[IsActive] = 1;
GO
