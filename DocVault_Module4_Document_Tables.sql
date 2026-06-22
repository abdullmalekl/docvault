-- ============================================================================
-- DOCVAULT MODULE 4: DOCUMENT MANAGEMENT SCHEMA
-- ============================================================================
-- Tables for document storage, versioning, and metadata
-- Supports full document lifecycle management

-- ============================================================================
-- CORE DOCUMENT TABLE
-- ============================================================================

CREATE TABLE [dbo].[Documents] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
    [CreatedByUserId] UNIQUEIDENTIFIER NOT NULL,
    [FolderId] UNIQUEIDENTIFIER NULL,

    [Title] NVARCHAR(500) NOT NULL,
    [Description] NVARCHAR(MAX) NULL,
    [ContentType] NVARCHAR(100) NOT NULL,
    [FileSizeBytes] BIGINT NOT NULL,

    [StoragePath] NVARCHAR(1000) NOT NULL UNIQUE,
    [ContentHash] VARCHAR(88) NOT NULL UNIQUE, -- SHA-256 Base64

    [Status] INT NOT NULL DEFAULT 0, -- Draft=0, InReview=1, Approved=2, Published=3, Archived=4, Deleted=5
    [Classification] INT NOT NULL DEFAULT 1, -- Public=0, Internal=1, Confidential=2, Secret=3

    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [ModifiedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [CurrentVersionId] UNIQUEIDENTIFIER NULL,
    [VersionCount] INT NOT NULL DEFAULT 1,

    [IsLocked] BIT NOT NULL DEFAULT 0,
    [LockedByUserId] UNIQUEIDENTIFIER NULL,
    [LockedAt] DATETIME2 NULL,

    [ExpiresAt] DATETIME2 NULL,
    [IsArchived] BIT NOT NULL DEFAULT 0,

    [Tags] NVARCHAR(MAX) NULL, -- JSON array: ["tag1", "tag2"]
    [CustomMetadata] NVARCHAR(MAX) NULL, -- JSON object

    CONSTRAINT FK_Documents_Organization FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organizations]([Id]),
    CONSTRAINT FK_Documents_CreatedBy FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[Users]([Id]),
    CONSTRAINT CK_Documents_Status CHECK ([Status] BETWEEN 0 AND 5),
    CONSTRAINT CK_Documents_Classification CHECK ([Classification] BETWEEN 0 AND 3)
);

CREATE INDEX IDX_Documents_Organization ON [dbo].[Documents]([OrganizationId]);
CREATE INDEX IDX_Documents_Folder ON [dbo].[Documents]([FolderId]) WHERE [FolderId] IS NOT NULL;
CREATE INDEX IDX_Documents_Status ON [dbo].[Documents]([Status]);
CREATE INDEX IDX_Documents_Classification ON [dbo].[Documents]([Classification]);
CREATE INDEX IDX_Documents_CreatedAt ON [dbo].[Documents]([CreatedAt]);
CREATE INDEX IDX_Documents_ContentHash ON [dbo].[Documents]([ContentHash]); -- Deduplication
CREATE INDEX IDX_Documents_IsArchived ON [dbo].[Documents]([IsArchived]);

-- ============================================================================
-- DOCUMENT VERSIONS TABLE
-- ============================================================================

CREATE TABLE [dbo].[DocumentVersions] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [DocumentId] UNIQUEIDENTIFIER NOT NULL,
    [VersionNumber] INT NOT NULL,

    [FileName] NVARCHAR(500) NOT NULL,
    [FileSizeBytes] BIGINT NOT NULL,
    [ContentHash] VARCHAR(88) NOT NULL, -- SHA-256 Base64

    [ChangeNotes] NVARCHAR(MAX) NULL,
    [CreatedByUserId] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    [StoragePath] NVARCHAR(1000) NOT NULL UNIQUE,
    [Status] INT NOT NULL DEFAULT 0, -- Draft=0, Current=1, Superseded=2, Archived=3

    CONSTRAINT FK_DocumentVersions_Document FOREIGN KEY ([DocumentId]) REFERENCES [dbo].[Documents]([Id]) ON DELETE CASCADE,
    CONSTRAINT FK_DocumentVersions_CreatedBy FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[Users]([Id]),
    CONSTRAINT UQ_DocumentVersions_Version UNIQUE ([DocumentId], [VersionNumber]),
    CONSTRAINT CK_DocumentVersions_Status CHECK ([Status] BETWEEN 0 AND 3)
);

CREATE INDEX IDX_DocumentVersions_Document ON [dbo].[DocumentVersions]([DocumentId]);
CREATE INDEX IDX_DocumentVersions_CreatedAt ON [dbo].[DocumentVersions]([CreatedAt]);
CREATE INDEX IDX_DocumentVersions_Status ON [dbo].[DocumentVersions]([Status]);
CREATE INDEX IDX_DocumentVersions_VersionNumber ON [dbo].[DocumentVersions]([DocumentId], [VersionNumber] DESC);

-- ============================================================================
-- DOCUMENT METADATA TABLE
-- ============================================================================

CREATE TABLE [dbo].[DocumentMetadata] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [DocumentId] UNIQUEIDENTIFIER NOT NULL UNIQUE,

    [Author] NVARCHAR(500) NULL,
    [Subject] NVARCHAR(500) NULL,
    [Keywords] NVARCHAR(MAX) NULL,

    [DocumentDate] DATETIME2 NULL,
    [Language] NVARCHAR(50) NULL,

    [PageCount] INT NULL,
    [CustomProperties] NVARCHAR(MAX) NULL, -- JSON object

    [ExtractedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_DocumentMetadata_Document FOREIGN KEY ([DocumentId]) REFERENCES [dbo].[Documents]([Id]) ON DELETE CASCADE
);

CREATE INDEX IDX_DocumentMetadata_Document ON [dbo].[DocumentMetadata]([DocumentId]);

-- ============================================================================
-- DOCUMENT FOLDERS TABLE (Hierarchical)
-- ============================================================================

CREATE TABLE [dbo].[DocumentFolders] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
    [ParentFolderId] UNIQUEIDENTIFIER NULL,

    [Name] NVARCHAR(500) NOT NULL,
    [Description] NVARCHAR(MAX) NULL,

    [CreatedByUserId] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [ModifiedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    [RetentionDays] INT NULL, -- Days to retain documents
    [IsArchived] BIT NOT NULL DEFAULT 0,

    CONSTRAINT FK_DocumentFolders_Organization FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organizations]([Id]),
    CONSTRAINT FK_DocumentFolders_Parent FOREIGN KEY ([ParentFolderId]) REFERENCES [dbo].[DocumentFolders]([Id]),
    CONSTRAINT FK_DocumentFolders_CreatedBy FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[Users]([Id])
);

CREATE INDEX IDX_DocumentFolders_Organization ON [dbo].[DocumentFolders]([OrganizationId]);
CREATE INDEX IDX_DocumentFolders_Parent ON [dbo].[DocumentFolders]([ParentFolderId]) WHERE [ParentFolderId] IS NOT NULL;

-- ============================================================================
-- DOCUMENT AUDIT LOG
-- ============================================================================

CREATE TABLE [dbo].[DocumentAuditLog] (
    [Id] BIGINT NOT NULL PRIMARY KEY IDENTITY(1,1),
    [DocumentId] UNIQUEIDENTIFIER NOT NULL,
    [UserId] UNIQUEIDENTIFIER NOT NULL,

    [Action] NVARCHAR(50) NOT NULL, -- Created, Modified, Deleted, Locked, Unlocked, VersionCreated
    [Details] NVARCHAR(MAX) NULL,

    [Timestamp] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [IpAddress] NVARCHAR(50) NULL,

    CONSTRAINT FK_DocumentAuditLog_Document FOREIGN KEY ([DocumentId]) REFERENCES [dbo].[Documents]([Id]) ON DELETE CASCADE,
    CONSTRAINT FK_DocumentAuditLog_User FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id])
);

CREATE INDEX IDX_DocumentAuditLog_Document ON [dbo].[DocumentAuditLog]([DocumentId]);
CREATE INDEX IDX_DocumentAuditLog_User ON [dbo].[DocumentAuditLog]([UserId]);
CREATE INDEX IDX_DocumentAuditLog_Timestamp ON [dbo].[DocumentAuditLog]([Timestamp] DESC);

-- ============================================================================
-- DOCUMENT PERMISSIONS
-- ============================================================================

CREATE TABLE [dbo].[DocumentPermissions] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [DocumentId] UNIQUEIDENTIFIER NOT NULL,
    [UserId] UNIQUEIDENTIFIER NULL,
    [RoleId] UNIQUEIDENTIFIER NULL,

    [CanView] BIT NOT NULL DEFAULT 0,
    [CanDownload] BIT NOT NULL DEFAULT 0,
    [CanEdit] BIT NOT NULL DEFAULT 0,
    [CanDelete] BIT NOT NULL DEFAULT 0,
    [CanShare] BIT NOT NULL DEFAULT 0,

    [GrantedByUserId] UNIQUEIDENTIFIER NOT NULL,
    [GrantedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [ExpiresAt] DATETIME2 NULL,

    CONSTRAINT FK_DocumentPermissions_Document FOREIGN KEY ([DocumentId]) REFERENCES [dbo].[Documents]([Id]) ON DELETE CASCADE,
    CONSTRAINT FK_DocumentPermissions_User FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE SET NULL,
    CONSTRAINT FK_DocumentPermissions_Role FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles]([Id]) ON DELETE SET NULL,
    CONSTRAINT FK_DocumentPermissions_GrantedBy FOREIGN KEY ([GrantedByUserId]) REFERENCES [dbo].[Users]([Id]),
    CONSTRAINT CK_DocumentPermissions_UserOrRole CHECK (([UserId] IS NOT NULL OR [RoleId] IS NOT NULL))
);

CREATE INDEX IDX_DocumentPermissions_Document ON [dbo].[DocumentPermissions]([DocumentId]);
CREATE INDEX IDX_DocumentPermissions_User ON [dbo].[DocumentPermissions]([UserId]) WHERE [UserId] IS NOT NULL;
CREATE INDEX IDX_DocumentPermissions_Role ON [dbo].[DocumentPermissions]([RoleId]) WHERE [RoleId] IS NOT NULL;

-- ============================================================================
-- DOCUMENT SEARCH INDEX (Full-text)
-- ============================================================================

CREATE FULLTEXT CATALOG FT_DocumentCatalog AS DEFAULT;

CREATE FULLTEXT INDEX ON [dbo].[Documents]
(
    [Title] LANGUAGE 1033,
    [Description] LANGUAGE 1033
)
KEY INDEX PK__Documents__3214EC27
WITH STOPLIST = SYSTEM;

-- ============================================================================
-- STORED PROCEDURES
-- ============================================================================

CREATE PROCEDURE [dbo].[sp_GetDocumentsByFolder]
    @FolderId UNIQUEIDENTIFIER,
    @Skip INT = 0,
    @Take INT = 50
AS
BEGIN
    SELECT *
    FROM [dbo].[Documents]
    WHERE [FolderId] = @FolderId
    AND [IsArchived] = 0
    ORDER BY [CreatedAt] DESC
    OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
END;
GO

CREATE PROCEDURE [dbo].[sp_SearchDocuments]
    @Query NVARCHAR(MAX),
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SELECT *
    FROM [dbo].[Documents]
    WHERE [OrganizationId] = @OrganizationId
    AND [IsArchived] = 0
    AND (
        CONTAINS([Title], @Query)
        OR CONTAINS([Description], @Query)
        OR [Title] LIKE '%' + @Query + '%'
    )
    ORDER BY [CreatedAt] DESC;
END;
GO

CREATE PROCEDURE [dbo].[sp_CleanupExpiredDocuments]
AS
BEGIN
    UPDATE [dbo].[Documents]
    SET [IsArchived] = 1, [Status] = 4
    WHERE [ExpiresAt] IS NOT NULL
    AND [ExpiresAt] < GETUTCDATE()
    AND [IsArchived] = 0;
END;
GO

CREATE PROCEDURE [dbo].[sp_GetDocumentPermissions]
    @DocumentId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SELECT *
    FROM [dbo].[DocumentPermissions]
    WHERE [DocumentId] = @DocumentId
    AND (
        [UserId] = @UserId
        OR [RoleId] IN (SELECT [RoleId] FROM [dbo].[UserRoles] WHERE [UserId] = @UserId)
    )
    AND ([ExpiresAt] IS NULL OR [ExpiresAt] > GETUTCDATE());
END;
GO

-- ============================================================================
-- VIEWS
-- ============================================================================

CREATE VIEW [dbo].[vw_DocumentsByStatus]
AS
SELECT
    [Status],
    COUNT(*) AS DocumentCount,
    SUM([FileSizeBytes]) AS TotalSizeBytes,
    AVG([FileSizeBytes]) AS AvgSizeBytes
FROM [dbo].[Documents]
WHERE [IsArchived] = 0
GROUP BY [Status];
GO

CREATE VIEW [dbo].[vw_DocumentVersionHistory]
AS
SELECT
    d.[Id],
    d.[Title],
    dv.[VersionNumber],
    dv.[FileName],
    dv.[Status],
    dv.[CreatedAt],
    dv.[CreatedByUserId]
FROM [dbo].[Documents] d
INNER JOIN [dbo].[DocumentVersions] dv ON d.[Id] = dv.[DocumentId]
WHERE d.[IsArchived] = 0;
GO
