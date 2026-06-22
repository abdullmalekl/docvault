-- ============================================================================
-- DOCVAULT MODULE 7: SEARCH & FULL-TEXT INTEGRATION SCHEMA
-- ============================================================================

-- ============================================================================
-- FULL-TEXT SEARCH CATALOG & INDEX
-- ============================================================================

CREATE FULLTEXT CATALOG FT_DocumentsCatalog AS DEFAULT;

-- Create full-text index on Documents
CREATE FULLTEXT INDEX ON [dbo].[Documents]
(
    [Title] LANGUAGE 1033,
    [Description] LANGUAGE 1033,
    [Tags] LANGUAGE 1033
)
KEY INDEX PK__Documents__3214EC27
WITH STOPLIST = SYSTEM;

-- ============================================================================
-- SEARCH ANALYTICS TABLE
-- ============================================================================

CREATE TABLE [dbo].[SearchAnalytics] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [UserId] UNIQUEIDENTIFIER NOT NULL,

    [SearchQuery] NVARCHAR(MAX) NOT NULL,
    [ResultCount] INT NOT NULL,
    [ClickCount] INT NOT NULL DEFAULT 0,

    [SearchedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [ExecutionTime] INT NULL, -- Milliseconds

    [IsSuccessful] BIT NOT NULL DEFAULT 1,
    [ErrorMessage] NVARCHAR(MAX) NULL,

    CONSTRAINT FK_SearchAnalytics_User FOREIGN KEY ([UserId])
        REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE
);

CREATE INDEX IDX_SearchAnalytics_User ON [dbo].[SearchAnalytics]([UserId]);
CREATE INDEX IDX_SearchAnalytics_SearchedAt ON [dbo].[SearchAnalytics]([SearchedAt] DESC);
CREATE INDEX IDX_SearchAnalytics_IsSuccessful ON [dbo].[SearchAnalytics]([IsSuccessful]);

-- ============================================================================
-- SEARCH CLICK TRACKING (User interactions with search results)
-- ============================================================================

CREATE TABLE [dbo].[SearchClickTracking] (
    [Id] BIGINT NOT NULL PRIMARY KEY IDENTITY(1,1),
    [SearchAnalyticId] UNIQUEIDENTIFIER NOT NULL,
    [DocumentId] UNIQUEIDENTIFIER NOT NULL,

    [ClickedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [ResultPosition] INT NOT NULL, -- 1-based ranking

    CONSTRAINT FK_SearchClickTracking_SearchAnalytic FOREIGN KEY ([SearchAnalyticId])
        REFERENCES [dbo].[SearchAnalytics]([Id]) ON DELETE CASCADE,
    CONSTRAINT FK_SearchClickTracking_Document FOREIGN KEY ([DocumentId])
        REFERENCES [dbo].[Documents]([Id])
);

CREATE INDEX IDX_SearchClickTracking_SearchAnalytic ON [dbo].[SearchClickTracking]([SearchAnalyticId]);
CREATE INDEX IDX_SearchClickTracking_DocumentId ON [dbo].[SearchClickTracking]([DocumentId]);

-- ============================================================================
-- SEARCH SUGGESTIONS & AUTOCOMPLETE
-- ============================================================================

CREATE TABLE [dbo].[SearchSuggestions] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,

    [SuggestionText] NVARCHAR(500) NOT NULL,
    [Frequency] INT NOT NULL DEFAULT 1, -- How often searched
    [IsPopular] BIT NOT NULL DEFAULT 0,

    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [LastUsedAt] DATETIME2 NULL,

    CONSTRAINT FK_SearchSuggestions_Organization FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organizations]([Id]),
    CONSTRAINT UQ_SearchSuggestions_Text UNIQUE ([OrganizationId], [SuggestionText])
);

CREATE INDEX IDX_SearchSuggestions_Organization ON [dbo].[SearchSuggestions]([OrganizationId]);
CREATE INDEX IDX_SearchSuggestions_Frequency ON [dbo].[SearchSuggestions]([Frequency] DESC);
CREATE INDEX IDX_SearchSuggestions_IsPopular ON [dbo].[SearchSuggestions]([IsPopular]) WHERE [IsPopular] = 1;

-- ============================================================================
-- SEARCH INDEX METADATA
-- ============================================================================

CREATE TABLE [dbo].[SearchIndexMetadata] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,

    [IndexedDocumentsCount] INT NOT NULL DEFAULT 0,
    [LastIndexedAt] DATETIME2 NULL,
    [LastRebuildAt] DATETIME2 NULL,

    [IndexHealthStatus] NVARCHAR(50) NOT NULL DEFAULT 'Healthy', -- Healthy, Degraded, Needs_Rebuild
    [IndexSize] BIGINT NULL, -- Bytes

    CONSTRAINT FK_SearchIndexMetadata_Organization FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organizations]([Id]) ON DELETE CASCADE,
    CONSTRAINT UQ_SearchIndexMetadata_Organization UNIQUE ([OrganizationId])
);

CREATE INDEX IDX_SearchIndexMetadata_Organization ON [dbo].[SearchIndexMetadata]([OrganizationId]);

-- ============================================================================
-- SAVED SEARCHES (Bookmarks)
-- ============================================================================

CREATE TABLE [dbo].[SavedSearches] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,

    [SearchName] NVARCHAR(500) NOT NULL,
    [QueryJson] NVARCHAR(MAX) NOT NULL, -- Serialized SearchQuery

    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [LastExecutedAt] DATETIME2 NULL,
    [ExecutionCount] INT NOT NULL DEFAULT 0,

    CONSTRAINT FK_SavedSearches_User FOREIGN KEY ([UserId])
        REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE,
    CONSTRAINT FK_SavedSearches_Organization FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organizations]([Id])
);

CREATE INDEX IDX_SavedSearches_User ON [dbo].[SavedSearches]([UserId]);
CREATE INDEX IDX_SavedSearches_Organization ON [dbo].[SavedSearches]([OrganizationId]);

-- ============================================================================
-- STORED PROCEDURES
-- ============================================================================

CREATE PROCEDURE [dbo].[sp_SearchDocuments]
    @QueryText NVARCHAR(MAX),
    @OrganizationId UNIQUEIDENTIFIER,
    @Skip INT = 0,
    @Take INT = 50
AS
BEGIN
    SELECT
        d.[Id],
        d.[Title],
        d.[Description],
        d.[Status],
        d.[Classification],
        d.[CreatedAt],
        d.[Tags]
    FROM [dbo].[Documents] d
    WHERE d.[OrganizationId] = @OrganizationId
    AND d.[IsArchived] = 0
    AND (
        CONTAINS(d.[Title], @QueryText)
        OR CONTAINS(d.[Description], @QueryText)
        OR d.[Title] LIKE '%' + @QueryText + '%'
    )
    ORDER BY d.[CreatedAt] DESC
    OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
END;
GO

CREATE PROCEDURE [dbo].[sp_GetSearchFacets]
    @QueryText NVARCHAR(MAX),
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    -- Classification facets
    SELECT 'Classification' AS FacetName, CAST([Classification] AS NVARCHAR(50)) AS Value, COUNT(*) AS Count
    FROM [dbo].[Documents]
    WHERE [OrganizationId] = @OrganizationId
    AND [IsArchived] = 0
    AND (CONTAINS([Title], @QueryText) OR [Title] LIKE '%' + @QueryText + '%')
    GROUP BY [Classification]

    UNION ALL

    -- Status facets
    SELECT 'Status', CAST([Status] AS NVARCHAR(50)), COUNT(*)
    FROM [dbo].[Documents]
    WHERE [OrganizationId] = @OrganizationId
    AND [IsArchived] = 0
    AND (CONTAINS([Title], @QueryText) OR [Title] LIKE '%' + @QueryText + '%')
    GROUP BY [Status];
END;
GO

CREATE PROCEDURE [dbo].[sp_GetPopularSearches]
    @OrganizationId UNIQUEIDENTIFIER,
    @Days INT = 7
AS
BEGIN
    SELECT TOP 10
        s.[SuggestionText],
        s.[Frequency],
        s.[LastUsedAt]
    FROM [dbo].[SearchSuggestions] s
    WHERE s.[OrganizationId] = @OrganizationId
    AND s.[IsPopular] = 1
    ORDER BY s.[Frequency] DESC;
END;
GO

CREATE PROCEDURE [dbo].[sp_LogSearchAnalytic]
    @UserId UNIQUEIDENTIFIER,
    @QueryText NVARCHAR(MAX),
    @ResultCount INT,
    @ExecutionTime INT,
    @IsSuccessful BIT
AS
BEGIN
    INSERT INTO [dbo].[SearchAnalytics] ([UserId], [SearchQuery], [ResultCount], [ExecutionTime], [IsSuccessful])
    VALUES (@UserId, @QueryText, @ResultCount, @ExecutionTime, @IsSuccessful);

    -- Update suggestions
    IF NOT EXISTS (SELECT 1 FROM [dbo].[SearchSuggestions] WHERE [SuggestionText] = @QueryText)
    BEGIN
        INSERT INTO [dbo].[SearchSuggestions] ([OrganizationId], [SuggestionText])
        SELECT (SELECT [OrganizationId] FROM [dbo].[Users] WHERE [Id] = @UserId), @QueryText;
    END
    ELSE
    BEGIN
        UPDATE [dbo].[SearchSuggestions]
        SET [Frequency] = [Frequency] + 1,
            [LastUsedAt] = GETUTCDATE()
        WHERE [SuggestionText] = @QueryText;
    END
END;
GO

-- ============================================================================
-- VIEWS FOR ANALYTICS
-- ============================================================================

CREATE VIEW [dbo].[vw_SearchTrends]
AS
SELECT
    CAST([SearchedAt] AS DATE) AS SearchDate,
    COUNT(*) AS TotalSearches,
    SUM(CASE WHEN [IsSuccessful] = 1 THEN 1 ELSE 0 END) AS SuccessfulSearches,
    AVG(CAST([ExecutionTime] AS FLOAT)) AS AvgExecutionTime
FROM [dbo].[SearchAnalytics]
WHERE [SearchedAt] >= DATEADD(DAY, -30, GETUTCDATE())
GROUP BY CAST([SearchedAt] AS DATE);
GO

CREATE VIEW [dbo].[vw_PopularSearchTerms]
AS
SELECT TOP 50
    [SuggestionText],
    [Frequency],
    RANK() OVER (ORDER BY [Frequency] DESC) AS PopularityRank
FROM [dbo].[SearchSuggestions]
WHERE [LastUsedAt] >= DATEADD(DAY, -30, GETUTCDATE())
ORDER BY [Frequency] DESC;
GO

CREATE VIEW [dbo].[vw_UserSearchBehavior]
AS
SELECT
    sa.[UserId],
    COUNT(*) AS TotalSearches,
    COUNT(DISTINCT sa.[SearchQuery]) AS UniqueQueries,
    AVG(sa.[ExecutionTime]) AS AvgExecutionTime,
    SUM(CASE WHEN sa.[IsSuccessful] = 1 THEN 1 ELSE 0 END) AS SuccessfulSearches
FROM [dbo].[SearchAnalytics] sa
WHERE sa.[SearchedAt] >= DATEADD(DAY, -30, GETUTCDATE())
GROUP BY sa.[UserId];
GO
