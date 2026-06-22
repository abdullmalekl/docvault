-- ============================================================================
-- DOCVAULT MODULE 5: DOCUMENT ACCESS CONTROL SCHEMA
-- ============================================================================
-- Tables for permission management, audit logging, and retention policies

-- ============================================================================
-- DOCUMENT PERMISSIONS TABLE (Enhanced from Module 4)
-- ============================================================================

ALTER TABLE [dbo].[DocumentPermissions]
ADD CONSTRAINT CK_DocumentPermissions_Permissions CHECK (
    ([CanView] = 1) OR
    ([CanDownload] = 1) OR
    ([CanEdit] = 1) OR
    ([CanDelete] = 1) OR
    ([CanShare] = 1)
);

CREATE INDEX IDX_DocumentPermissions_Expiry ON [dbo].[DocumentPermissions]([ExpiresAt])
WHERE [ExpiresAt] IS NOT NULL;

-- ============================================================================
-- DOCUMENT ACCESS AUDIT LOG TABLE
-- ============================================================================

CREATE TABLE [dbo].[DocumentAccessAuditLog] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [DocumentId] UNIQUEIDENTIFIER NOT NULL,
    [UserId] UNIQUEIDENTIFIER NOT NULL,

    [Action] NVARCHAR(50) NOT NULL, -- View, Download, Edit, Delete, Share, AccessGranted, AccessRevoked
    [Timestamp] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [IpAddress] NVARCHAR(50) NULL,
    [UserAgent] NVARCHAR(500) NULL,

    [Success] BIT NOT NULL DEFAULT 1,
    [DenyReason] NVARCHAR(MAX) NULL,

    CONSTRAINT FK_DocumentAccessAuditLog_Document FOREIGN KEY ([DocumentId])
        REFERENCES [dbo].[Documents]([Id]) ON DELETE CASCADE,
    CONSTRAINT FK_DocumentAccessAuditLog_User FOREIGN KEY ([UserId])
        REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE,
    CONSTRAINT CK_DocumentAccessAuditLog_Action CHECK ([Action] IN (
        'View', 'Download', 'Edit', 'Delete', 'Share',
        'AccessGranted', 'AccessRevoked', 'DocumentShared'
    ))
);

CREATE INDEX IDX_DocumentAccessAuditLog_Document ON [dbo].[DocumentAccessAuditLog]([DocumentId]);
CREATE INDEX IDX_DocumentAccessAuditLog_User ON [dbo].[DocumentAccessAuditLog]([UserId]);
CREATE INDEX IDX_DocumentAccessAuditLog_Timestamp ON [dbo].[DocumentAccessAuditLog]([Timestamp] DESC);
CREATE INDEX IDX_DocumentAccessAuditLog_Action ON [dbo].[DocumentAccessAuditLog]([Action]);
CREATE INDEX IDX_DocumentAccessAuditLog_Success ON [dbo].[DocumentAccessAuditLog]([Success]) WHERE [Success] = 0;

-- ============================================================================
-- DOCUMENT RETENTION POLICIES TABLE
-- ============================================================================

CREATE TABLE [dbo].[DocumentRetentionPolicies] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
    [FolderId] UNIQUEIDENTIFIER NULL,

    [PolicyName] NVARCHAR(500) NOT NULL,
    [Description] NVARCHAR(MAX) NULL,

    [RetentionDays] INT NOT NULL, -- Number of days to retain documents
    [ActionOnExpiry] INT NOT NULL, -- 0=Archive, 1=Delete, 2=Notify
    [NotifyDaysBefore] INT NULL, -- Notify N days before expiry (when Action=2)

    [IsActive] BIT NOT NULL DEFAULT 1,
    [ApplyRecursive] BIT NOT NULL DEFAULT 1, -- Apply to subfolders

    [CreatedByUserId] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [LastAppliedAt] DATETIME2 NULL,

    CONSTRAINT FK_DocumentRetentionPolicies_Organization FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organizations]([Id]),
    CONSTRAINT FK_DocumentRetentionPolicies_Folder FOREIGN KEY ([FolderId])
        REFERENCES [dbo].[DocumentFolders]([Id]) ON DELETE SET NULL,
    CONSTRAINT FK_DocumentRetentionPolicies_CreatedBy FOREIGN KEY ([CreatedByUserId])
        REFERENCES [dbo].[Users]([Id]),
    CONSTRAINT CK_DocumentRetentionPolicies_Action CHECK ([ActionOnExpiry] IN (0, 1, 2)),
    CONSTRAINT CK_DocumentRetentionPolicies_Days CHECK ([RetentionDays] > 0)
);

CREATE INDEX IDX_DocumentRetentionPolicies_Organization ON [dbo].[DocumentRetentionPolicies]([OrganizationId]);
CREATE INDEX IDX_DocumentRetentionPolicies_Folder ON [dbo].[DocumentRetentionPolicies]([FolderId])
    WHERE [FolderId] IS NOT NULL;
CREATE INDEX IDX_DocumentRetentionPolicies_IsActive ON [dbo].[DocumentRetentionPolicies]([IsActive])
    WHERE [IsActive] = 1;

-- ============================================================================
-- DOCUMENT EXPIRY LOG TABLE
-- ============================================================================

CREATE TABLE [dbo].[DocumentExpiryLog] (
    [Id] BIGINT NOT NULL PRIMARY KEY IDENTITY(1,1),
    [DocumentId] UNIQUEIDENTIFIER NOT NULL,
    [PolicyId] UNIQUEIDENTIFIER NOT NULL,

    [ExpiryDate] DATETIME2 NOT NULL,
    [ActionTaken] NVARCHAR(50) NOT NULL, -- Archived, Deleted, Notified
    [ExecutedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    [Result] NVARCHAR(MAX) NULL, -- Status/error details

    CONSTRAINT FK_DocumentExpiryLog_Document FOREIGN KEY ([DocumentId])
        REFERENCES [dbo].[Documents]([Id]) ON DELETE CASCADE,
    CONSTRAINT FK_DocumentExpiryLog_Policy FOREIGN KEY ([PolicyId])
        REFERENCES [dbo].[DocumentRetentionPolicies]([Id]) ON DELETE CASCADE
);

CREATE INDEX IDX_DocumentExpiryLog_Document ON [dbo].[DocumentExpiryLog]([DocumentId]);
CREATE INDEX IDX_DocumentExpiryLog_ExecutedAt ON [dbo].[DocumentExpiryLog]([ExecutedAt] DESC);
CREATE INDEX IDX_DocumentExpiryLog_ActionTaken ON [dbo].[DocumentExpiryLog]([ActionTaken]);

-- ============================================================================
-- ACCESS REVIEW TABLE (For compliance)
-- ============================================================================

CREATE TABLE [dbo].[DocumentAccessReview] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [DocumentId] UNIQUEIDENTIFIER NOT NULL,
    [ReviewedByUserId] UNIQUEIDENTIFIER NOT NULL,

    [ReviewDate] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [Comments] NVARCHAR(MAX) NULL,

    [AccessConfirmedCorrect] BIT NOT NULL,
    [AccessRemovalRecommended] BIT NOT NULL DEFAULT 0,

    CONSTRAINT FK_DocumentAccessReview_Document FOREIGN KEY ([DocumentId])
        REFERENCES [dbo].[Documents]([Id]) ON DELETE CASCADE,
    CONSTRAINT FK_DocumentAccessReview_ReviewedBy FOREIGN KEY ([ReviewedByUserId])
        REFERENCES [dbo].[Users]([Id])
);

CREATE INDEX IDX_DocumentAccessReview_Document ON [dbo].[DocumentAccessReview]([DocumentId]);
CREATE INDEX IDX_DocumentAccessReview_ReviewDate ON [dbo].[DocumentAccessReview]([ReviewDate] DESC);

-- ============================================================================
-- STORED PROCEDURES FOR ACCESS CONTROL
-- ============================================================================

CREATE PROCEDURE [dbo].[sp_CanUserAccessDocument]
    @DocumentId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Action NVARCHAR(50) -- View, Download, Edit, Delete, Share
AS
BEGIN
    DECLARE @OwnerId UNIQUEIDENTIFIER;
    DECLARE @Permission BIT = 0;
    DECLARE @ActionColumn NVARCHAR(50);

    -- Get document owner
    SELECT @OwnerId = [CreatedByUserId]
    FROM [dbo].[Documents]
    WHERE [Id] = @DocumentId;

    -- Owner has all permissions
    IF @UserId = @OwnerId
        SELECT 1;
    ELSE
    BEGIN
        -- Check direct user permission
        SELECT @Permission = CASE
            WHEN @Action = 'View' THEN [CanView]
            WHEN @Action = 'Download' THEN [CanDownload]
            WHEN @Action = 'Edit' THEN [CanEdit]
            WHEN @Action = 'Delete' THEN [CanDelete]
            WHEN @Action = 'Share' THEN [CanShare]
            ELSE 0
        END
        FROM [dbo].[DocumentPermissions]
        WHERE [DocumentId] = @DocumentId
        AND [UserId] = @UserId
        AND ([ExpiresAt] IS NULL OR [ExpiresAt] > GETUTCDATE());

        -- If not direct permission, check role-based permission
        IF @Permission = 0
        BEGIN
            SELECT @Permission = CASE
                WHEN @Action = 'View' THEN MAX(CAST([CanView] AS INT))
                WHEN @Action = 'Download' THEN MAX(CAST([CanDownload] AS INT))
                WHEN @Action = 'Edit' THEN MAX(CAST([CanEdit] AS INT))
                WHEN @Action = 'Delete' THEN MAX(CAST([CanDelete] AS INT))
                WHEN @Action = 'Share' THEN MAX(CAST([CanShare] AS INT))
                ELSE 0
            END
            FROM [dbo].[DocumentPermissions] dp
            WHERE dp.[DocumentId] = @DocumentId
            AND dp.[RoleId] IN (
                SELECT [RoleId] FROM [dbo].[UserRoles]
                WHERE [UserId] = @UserId
            )
            AND (dp.[ExpiresAt] IS NULL OR dp.[ExpiresAt] > GETUTCDATE());
        END

        SELECT @Permission;
    END
END;
GO

CREATE PROCEDURE [dbo].[sp_ApplyRetentionPolicies]
AS
BEGIN
    DECLARE @RetentionDays INT;
    DECLARE @ActionOnExpiry INT;
    DECLARE @ExpiryDate DATETIME2;

    -- Archive documents
    UPDATE [dbo].[Documents]
    SET [IsArchived] = 1, [Status] = 4
    WHERE [Id] IN (
        SELECT d.[Id]
        FROM [dbo].[Documents] d
        INNER JOIN [dbo].[DocumentFolders] f ON d.[FolderId] = f.[Id]
        INNER JOIN [dbo].[DocumentRetentionPolicies] p ON f.[Id] = p.[FolderId]
        WHERE p.[IsActive] = 1
        AND p.[ActionOnExpiry] = 0 -- Archive
        AND d.[CreatedAt] < DATEADD(DAY, -p.[RetentionDays], GETUTCDATE())
        AND d.[IsArchived] = 0
    );

    -- Mark for deletion
    UPDATE [dbo].[Documents]
    SET [IsArchived] = 1, [Status] = 5
    WHERE [Id] IN (
        SELECT d.[Id]
        FROM [dbo].[Documents] d
        INNER JOIN [dbo].[DocumentFolders] f ON d.[FolderId] = f.[Id]
        INNER JOIN [dbo].[DocumentRetentionPolicies] p ON f.[Id] = p.[FolderId]
        WHERE p.[IsActive] = 1
        AND p.[ActionOnExpiry] = 1 -- Delete
        AND d.[CreatedAt] < DATEADD(DAY, -p.[RetentionDays], GETUTCDATE())
        AND d.[IsArchived] = 0
    );

    -- Log execution
    UPDATE [dbo].[DocumentRetentionPolicies]
    SET [LastAppliedAt] = GETUTCDATE()
    WHERE [IsActive] = 1;
END;
GO

CREATE PROCEDURE [dbo].[sp_GetDocumentAccessHistory]
    @DocumentId UNIQUEIDENTIFIER,
    @DaysBack INT = 30
AS
BEGIN
    SELECT
        [Id],
        [DocumentId],
        [UserId],
        [Action],
        [Timestamp],
        [IpAddress],
        [Success],
        [DenyReason]
    FROM [dbo].[DocumentAccessAuditLog]
    WHERE [DocumentId] = @DocumentId
    AND [Timestamp] >= DATEADD(DAY, -@DaysBack, GETUTCDATE())
    ORDER BY [Timestamp] DESC;
END;
GO

CREATE PROCEDURE [dbo].[sp_RevokeExpiredPermissions]
AS
BEGIN
    DELETE FROM [dbo].[DocumentPermissions]
    WHERE [ExpiresAt] IS NOT NULL
    AND [ExpiresAt] < GETUTCDATE();

    -- Log revocation
    INSERT INTO [dbo].[DocumentAccessAuditLog] ([DocumentId], [UserId], [Action], [Success])
    SELECT [DocumentId], [UserId], 'AccessExpired', 1
    FROM [dbo].[DocumentPermissions]
    WHERE [ExpiresAt] < GETUTCDATE();
END;
GO

-- ============================================================================
-- VIEWS FOR ACCESS REPORTING
-- ============================================================================

CREATE VIEW [dbo].[vw_DocumentAccessSummary]
AS
SELECT
    d.[Id],
    d.[Title],
    COUNT(DISTINCT dp.[UserId]) AS DirectAccessCount,
    COUNT(DISTINCT dp.[RoleId]) AS RoleAccessCount,
    MAX(CASE WHEN dp.[ExpiresAt] IS NOT NULL AND dp.[ExpiresAt] < GETUTCDATE()
        THEN 1 ELSE 0 END) AS HasExpiredAccess
FROM [dbo].[Documents] d
LEFT JOIN [dbo].[DocumentPermissions] dp ON d.[Id] = dp.[DocumentId]
GROUP BY d.[Id], d.[Title];
GO

CREATE VIEW [dbo].[vw_UnauthorizedAccessAttempts]
AS
SELECT
    [DocumentId],
    [UserId],
    COUNT(*) AS AttemptCount,
    MAX([Timestamp]) AS LastAttempt
FROM [dbo].[DocumentAccessAuditLog]
WHERE [Success] = 0
AND [Timestamp] >= DATEADD(DAY, -7, GETUTCDATE())
GROUP BY [DocumentId], [UserId]
HAVING COUNT(*) > 3;
GO

CREATE VIEW [dbo].[vw_DocumentsNearExpiry]
AS
SELECT
    d.[Id],
    d.[Title],
    d.[CreatedAt],
    DATEADD(DAY, p.[RetentionDays], d.[CreatedAt]) AS ExpiryDate,
    DATEDIFF(DAY, GETUTCDATE(), DATEADD(DAY, p.[RetentionDays], d.[CreatedAt])) AS DaysUntilExpiry
FROM [dbo].[Documents] d
INNER JOIN [dbo].[DocumentFolders] f ON d.[FolderId] = f.[Id]
INNER JOIN [dbo].[DocumentRetentionPolicies] p ON f.[Id] = p.[FolderId]
WHERE p.[IsActive] = 1
AND d.[IsArchived] = 0
AND DATEDIFF(DAY, GETUTCDATE(), DATEADD(DAY, p.[RetentionDays], d.[CreatedAt])) BETWEEN 0 AND 30;
GO
