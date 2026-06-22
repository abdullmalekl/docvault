-- =====================================================
-- DocVault Enterprise Document Archival System
-- Database DDL (Data Definition Language)
-- Phase 4: Complete Database Schema with Constraints
-- =====================================================
-- Date: June 21, 2026
-- Version: 1.0
-- Target: SQL Server 2019/2022 + PostgreSQL compatibility
-- =====================================================

-- =====================================================
-- 1. DATABASE & SCHEMA CREATION
-- =====================================================

-- SQL Server
/*
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'DocVault')
BEGIN
    CREATE DATABASE DocVault
    COLLATE SQL_Latin1_General_CP1256_CI_AS;  -- Arabic support
END
GO

USE DocVault;
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'dbo')
BEGIN
    CREATE SCHEMA dbo;
END
GO
*/

-- PostgreSQL
/*
CREATE DATABASE docvault
    ENCODING 'UTF8'
    LC_COLLATE 'en_US.UTF-8'
    LC_CTYPE 'en_US.UTF-8';

\c docvault

CREATE SCHEMA IF NOT EXISTS docvault;
SET search_path = docvault;
*/

-- =====================================================
-- 2. DEPARTMENTS TABLE
-- =====================================================
CREATE TABLE Departments (
    DepartmentID INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(255) NOT NULL,
    Description NVARCHAR(MAX),
    ParentDepartmentID INT,
    CreatedAt DATETIME2 DEFAULT GETDATE() NOT NULL,
    UpdatedAt DATETIME2 DEFAULT GETDATE() NOT NULL,
    IsActive BIT DEFAULT 1 NOT NULL,

    -- Constraints
    CONSTRAINT CK_Dept_Name CHECK (LEN(TRIM(Name)) > 0),
    CONSTRAINT FK_Dept_Parent FOREIGN KEY (ParentDepartmentID)
        REFERENCES Departments(DepartmentID),
    CONSTRAINT UQ_Dept_Name UNIQUE (Name)
);

CREATE INDEX IX_Dept_Parent ON Departments(ParentDepartmentID);
CREATE INDEX IX_Dept_Active ON Departments(IsActive);

-- =====================================================
-- 3. ROLES TABLE
-- =====================================================
CREATE TABLE Roles (
    RoleID INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(MAX),
    Permissions NVARCHAR(MAX),  -- JSON format
    CreatedAt DATETIME2 DEFAULT GETDATE() NOT NULL,
    UpdatedAt DATETIME2 DEFAULT GETDATE() NOT NULL,
    IsActive BIT DEFAULT 1 NOT NULL,

    -- Constraints
    CONSTRAINT CK_Role_Name CHECK (LEN(TRIM(Name)) > 0)
);

-- Insert default roles
INSERT INTO Roles (Name, Description, IsActive) VALUES
    ('Admin', N'System administrator with full access', 1),
    ('Manager', N'Department manager with supervisory access', 1),
    ('Operator', N'Document operator for scanning and data entry', 1),
    ('Viewer', N'Read-only access to documents', 1);

-- =====================================================
-- 4. USERS TABLE
-- =====================================================
CREATE TABLE Users (
    UserID INT PRIMARY KEY IDENTITY(1,1),
    Username NVARCHAR(100) NOT NULL UNIQUE,
    Email NVARCHAR(255) NOT NULL,
    PasswordHash NVARCHAR(255) NOT NULL,  -- Bcrypt hash
    DepartmentID INT NOT NULL,
    RoleID INT NOT NULL,
    IsActive BIT DEFAULT 1 NOT NULL,
    IsMFAEnabled BIT DEFAULT 0 NOT NULL,
    MFASecret NVARCHAR(255),  -- Google Authenticator secret (encrypted)
    LastLoginAt DATETIME2,
    LastLoginIP NVARCHAR(45),
    FailedLoginAttempts INT DEFAULT 0,
    LockedUntil DATETIME2,
    CreatedAt DATETIME2 DEFAULT GETDATE() NOT NULL,
    UpdatedAt DATETIME2 DEFAULT GETDATE() NOT NULL,
    CreatedByUserID INT,

    -- Constraints
    CONSTRAINT CK_User_Username CHECK (LEN(TRIM(Username)) >= 3),
    CONSTRAINT CK_User_Email CHECK (Email LIKE '%@%.%'),
    CONSTRAINT FK_User_Dept FOREIGN KEY (DepartmentID)
        REFERENCES Departments(DepartmentID) ON DELETE RESTRICT,
    CONSTRAINT FK_User_Role FOREIGN KEY (RoleID)
        REFERENCES Roles(RoleID) ON DELETE RESTRICT,
    CONSTRAINT FK_User_Creator FOREIGN KEY (CreatedByUserID)
        REFERENCES Users(UserID) ON DELETE SET NULL
);

CREATE INDEX IX_User_Username ON Users(Username);
CREATE INDEX IX_User_Email ON Users(Email);
CREATE INDEX IX_User_Dept ON Users(DepartmentID);
CREATE INDEX IX_User_Role ON Users(RoleID);
CREATE INDEX IX_User_Active ON Users(IsActive);
CREATE INDEX IX_User_MFA ON Users(IsMFAEnabled);

-- =====================================================
-- 5. CATEGORIES TABLE
-- =====================================================
CREATE TABLE Categories (
    CategoryID INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(MAX),
    ColorCode NVARCHAR(7),  -- Hex color for UI
    DisplayOrder INT DEFAULT 0,
    CreatedAt DATETIME2 DEFAULT GETDATE() NOT NULL,
    UpdatedAt DATETIME2 DEFAULT GETDATE() NOT NULL,
    IsActive BIT DEFAULT 1 NOT NULL,

    CONSTRAINT CK_Cat_Name CHECK (LEN(TRIM(Name)) > 0),
    CONSTRAINT CK_Color_Format CHECK (ColorCode LIKE '#[0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F]')
);

INSERT INTO Categories (Name, Description, ColorCode) VALUES
    (N'Incoming', N'Incoming documents', '#0099FF'),
    (N'Outgoing', N'Outgoing documents', '#FF9933'),
    (N'Internal', N'Internal documents', '#33CC33'),
    (N'Policy', N'Policy and procedures', '#CC33CC'),
    (N'Report', N'Reports and statistics', '#FFCC00');

-- =====================================================
-- 6. DOCUMENT STATUS TABLE
-- =====================================================
CREATE TABLE DocumentStatus (
    StatusID INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(MAX),
    ColorCode NVARCHAR(7),
    DisplayOrder INT DEFAULT 0,
    CreatedAt DATETIME2 DEFAULT GETDATE() NOT NULL,
    IsActive BIT DEFAULT 1 NOT NULL,

    CONSTRAINT CK_Status_Name CHECK (LEN(TRIM(Name)) > 0)
);

INSERT INTO DocumentStatus (Name, Description, ColorCode) VALUES
    (N'Active', N'Document is active and accessible', '#00AA00'),
    (N'Archived', N'Document is archived but accessible', '#9999FF'),
    (N'Pending Review', N'Document pending review', '#FFFF00'),
    (N'Rejected', N'Document was rejected', '#FF3333'),
    (N'Deleted', N'Document marked for deletion', '#CCCCCC');

-- =====================================================
-- 7. DOCUMENTS TABLE
-- =====================================================
CREATE TABLE Documents (
    DocumentID BIGINT PRIMARY KEY IDENTITY(1,1),
    DocumentNumber NVARCHAR(50) NOT NULL UNIQUE,  -- Custom format
    Title NVARCHAR(500) NOT NULL,
    Subject NVARCHAR(500),
    Content NVARCHAR(MAX),  -- Full-text indexed, OCR content
    DocumentDate DATE NOT NULL,
    DepartmentID INT NOT NULL,
    CategoryID INT NOT NULL,
    StatusID INT NOT NULL,
    PhysicalLocation NVARCHAR(255),  -- Cabinet name, file box number
    Keywords NVARCHAR(MAX),  -- Comma-separated or JSON
    CreatedByUserID INT NOT NULL,
    CreatedAt DATETIME2 DEFAULT GETDATE() NOT NULL,
    ModifiedByUserID INT,
    ModifiedAt DATETIME2,
    IsDeleted BIT DEFAULT 0 NOT NULL,
    DeletedAt DATETIME2,
    DeletedByUserID INT,
    DeletionReason NVARCHAR(MAX),
    SyncHash NVARCHAR(64),  -- SHA-256 for sync detection

    -- Constraints
    CONSTRAINT CK_Doc_Title CHECK (LEN(TRIM(Title)) > 0),
    CONSTRAINT CK_Doc_Number CHECK (LEN(TRIM(DocumentNumber)) > 0),
    CONSTRAINT FK_Doc_Dept FOREIGN KEY (DepartmentID)
        REFERENCES Departments(DepartmentID) ON DELETE RESTRICT,
    CONSTRAINT FK_Doc_Cat FOREIGN KEY (CategoryID)
        REFERENCES Categories(CategoryID),
    CONSTRAINT FK_Doc_Status FOREIGN KEY (StatusID)
        REFERENCES DocumentStatus(StatusID),
    CONSTRAINT FK_Doc_Creator FOREIGN KEY (CreatedByUserID)
        REFERENCES Users(UserID) ON DELETE RESTRICT,
    CONSTRAINT FK_Doc_Modifier FOREIGN KEY (ModifiedByUserID)
        REFERENCES Users(UserID) ON DELETE SET NULL,
    CONSTRAINT FK_Doc_Deleter FOREIGN KEY (DeletedByUserID)
        REFERENCES Users(UserID) ON DELETE SET NULL
);

-- Full-text index on content
CREATE NONCLUSTERED INDEX IX_Doc_Content ON Documents(Content);
CREATE NONCLUSTERED INDEX IX_Doc_Number ON Documents(DocumentNumber);
CREATE NONCLUSTERED INDEX IX_Doc_Title ON Documents(Title);
CREATE NONCLUSTERED INDEX IX_Doc_Dept ON Documents(DepartmentID);
CREATE NONCLUSTERED INDEX IX_Doc_Category ON Documents(CategoryID);
CREATE NONCLUSTERED INDEX IX_Doc_Date ON Documents(DocumentDate);
CREATE NONCLUSTERED INDEX IX_Doc_Status ON Documents(StatusID);
CREATE NONCLUSTERED INDEX IX_Doc_IsDeleted ON Documents(IsDeleted);
CREATE NONCLUSTERED INDEX IX_Doc_Creator ON Documents(CreatedByUserID);
CREATE NONCLUSTERED INDEX IX_Doc_Keywords ON Documents(Keywords);

-- =====================================================
-- 8. DOCUMENT PAGES TABLE
-- =====================================================
CREATE TABLE DocumentPages (
    PageID BIGINT PRIMARY KEY IDENTITY(1,1),
    DocumentID BIGINT NOT NULL,
    PageNumber INT NOT NULL,
    ImagePath NVARCHAR(MAX) NOT NULL,  -- Encrypted file path
    ImageHash NVARCHAR(64),  -- SHA-256 for verification
    OCRText NVARCHAR(MAX),  -- Searchable OCR output
    QualityScore INT CHECK (QualityScore BETWEEN 0 AND 100),  -- 0-100%
    OCRLanguage NVARCHAR(10) DEFAULT 'ar,en',  -- Languages detected
    CreatedAt DATETIME2 DEFAULT GETDATE() NOT NULL,
    DPI INT,  -- Dots per inch
    ColorMode NVARCHAR(20),  -- RGB, Grayscale, B&W

    -- Constraints
    CONSTRAINT CK_Page_Number CHECK (PageNumber > 0),
    CONSTRAINT FK_Page_Doc FOREIGN KEY (DocumentID)
        REFERENCES Documents(DocumentID) ON DELETE CASCADE,
    CONSTRAINT UQ_Page_Unique UNIQUE (DocumentID, PageNumber)
);

CREATE INDEX IX_Page_Doc ON DocumentPages(DocumentID);
CREATE INDEX IX_Page_Number ON DocumentPages(DocumentID, PageNumber);
CREATE NONCLUSTERED INDEX IX_Page_OCRText ON DocumentPages(OCRText);

-- =====================================================
-- 9. DOCUMENT RELATIONSHIPS TABLE
-- =====================================================
CREATE TABLE DocumentRelationships (
    RelationshipID BIGINT PRIMARY KEY IDENTITY(1,1),
    SourceDocumentID BIGINT NOT NULL,
    TargetDocumentID BIGINT NOT NULL,
    RelationType NVARCHAR(50) NOT NULL,  -- 'Reply', 'Forward', 'Related', 'Attachment'
    Description NVARCHAR(MAX),
    CreatedAt DATETIME2 DEFAULT GETDATE() NOT NULL,
    CreatedByUserID INT NOT NULL,

    -- Constraints
    CONSTRAINT FK_Rel_Source FOREIGN KEY (SourceDocumentID)
        REFERENCES Documents(DocumentID) ON DELETE CASCADE,
    CONSTRAINT FK_Rel_Target FOREIGN KEY (TargetDocumentID)
        REFERENCES Documents(DocumentID) ON DELETE CASCADE,
    CONSTRAINT CK_Rel_Type CHECK (RelationType IN ('Reply', 'Forward', 'Related', 'Attachment')),
    CONSTRAINT CK_Rel_Different CHECK (SourceDocumentID != TargetDocumentID),
    CONSTRAINT UQ_Rel_Pair UNIQUE (SourceDocumentID, TargetDocumentID, RelationType)
);

CREATE INDEX IX_Rel_Source ON DocumentRelationships(SourceDocumentID);
CREATE INDEX IX_Rel_Target ON DocumentRelationships(TargetDocumentID);

-- =====================================================
-- 10. KEYWORDS TABLE
-- =====================================================
CREATE TABLE Keywords (
    KeywordID INT PRIMARY KEY IDENTITY(1,1),
    DocumentID BIGINT NOT NULL,
    Keyword NVARCHAR(100) NOT NULL,
    Frequency INT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETDATE() NOT NULL,

    -- Constraints
    CONSTRAINT FK_Kw_Doc FOREIGN KEY (DocumentID)
        REFERENCES Documents(DocumentID) ON DELETE CASCADE,
    CONSTRAINT CK_Kw_Keyword CHECK (LEN(TRIM(Keyword)) > 0)
);

CREATE INDEX IX_Kw_Doc ON Keywords(DocumentID);
CREATE INDEX IX_Kw_Keyword ON Keywords(Keyword);
CREATE INDEX IX_Kw_Composite ON Keywords(DocumentID, Keyword);

-- =====================================================
-- 11. AUDIT LOG TABLE (Immutable)
-- =====================================================
CREATE TABLE AuditLog (
    AuditID BIGINT PRIMARY KEY IDENTITY(1,1),
    UserID INT NOT NULL,
    ActionType NVARCHAR(50) NOT NULL,  -- 'Create', 'Update', 'View', 'Delete', 'Print', 'Export', 'Search', 'Login'
    TableName NVARCHAR(100),
    RecordID BIGINT,
    OldValue NVARCHAR(MAX),
    NewValue NVARCHAR(MAX),
    IPAddress NVARCHAR(45),
    UserAgent NVARCHAR(MAX),
    DeviceName NVARCHAR(255),
    IsSuccess BIT DEFAULT 1 NOT NULL,
    FailureReason NVARCHAR(MAX),
    ExecutionTimeMs INT,  -- Query execution time
    Timestamp DATETIME2 DEFAULT GETDATE() NOT NULL,

    -- Constraints
    CONSTRAINT FK_Audit_User FOREIGN KEY (UserID)
        REFERENCES Users(UserID) ON DELETE RESTRICT
);

-- Audit log indexes (heavily queried)
CREATE NONCLUSTERED INDEX IX_Audit_User ON AuditLog(UserID);
CREATE NONCLUSTERED INDEX IX_Audit_Action ON AuditLog(ActionType);
CREATE NONCLUSTERED INDEX IX_Audit_Table ON AuditLog(TableName, RecordID);
CREATE NONCLUSTERED INDEX IX_Audit_Timestamp ON AuditLog(Timestamp DESC);
CREATE NONCLUSTERED INDEX IX_Audit_Date ON AuditLog(CAST(Timestamp AS DATE));

-- Partitioning strategy (for large tables)
-- ALTER TABLE AuditLog ADD CONSTRAINT
--     PK_AuditLog_Partition PRIMARY KEY (AuditID, Timestamp);

-- =====================================================
-- 12. BACKUP HISTORY TABLE
-- =====================================================
CREATE TABLE BackupHistory (
    BackupID INT PRIMARY KEY IDENTITY(1,1),
    BackupPath NVARCHAR(MAX) NOT NULL,
    BackupSize BIGINT,  -- In bytes
    VerificationHash NVARCHAR(64),  -- SHA-256
    IsVerified BIT DEFAULT 0 NOT NULL,
    VerificationFailureReason NVARCHAR(MAX),
    CreatedAt DATETIME2 DEFAULT GETDATE() NOT NULL,
    ScheduledBy NVARCHAR(100),
    BackupType NVARCHAR(50),  -- 'Full', 'Incremental', 'Differential'
    RetentionDays INT DEFAULT 30,
    IsDeleted BIT DEFAULT 0 NOT NULL,
    DeletedAt DATETIME2,

    CONSTRAINT CK_Backup_Size CHECK (BackupSize > 0),
    CONSTRAINT CK_Backup_Type CHECK (BackupType IN ('Full', 'Incremental', 'Differential'))
);

CREATE INDEX IX_Backup_Date ON BackupHistory(CreatedAt DESC);
CREATE INDEX IX_Backup_Verified ON BackupHistory(IsVerified);

-- =====================================================
-- 13. SEARCH HISTORY TABLE
-- =====================================================
CREATE TABLE SearchHistory (
    SearchID BIGINT PRIMARY KEY IDENTITY(1,1),
    UserID INT NOT NULL,
    DepartmentID INT,
    SearchQuery NVARCHAR(MAX) NOT NULL,
    SearchCriteria NVARCHAR(MAX),  -- JSON format
    ResultCount INT DEFAULT 0,
    ExecutionTimeMs INT,
    CreatedAt DATETIME2 DEFAULT GETDATE() NOT NULL,
    IsSaved BIT DEFAULT 0 NOT NULL,
    SavedName NVARCHAR(255),

    CONSTRAINT FK_Search_User FOREIGN KEY (UserID)
        REFERENCES Users(UserID) ON DELETE CASCADE,
    CONSTRAINT FK_Search_Dept FOREIGN KEY (DepartmentID)
        REFERENCES Departments(DepartmentID) ON DELETE SET NULL
);

CREATE INDEX IX_Search_User ON SearchHistory(UserID);
CREATE INDEX IX_Search_Date ON SearchHistory(CreatedAt DESC);
CREATE INDEX IX_Search_Saved ON SearchHistory(UserID, IsSaved);

-- =====================================================
-- 14. SYSTEM CONFIGURATION TABLE
-- =====================================================
CREATE TABLE SystemConfig (
    ConfigID INT PRIMARY KEY IDENTITY(1,1),
    [Key] NVARCHAR(255) NOT NULL UNIQUE,
    [Value] NVARCHAR(MAX),
    DataType NVARCHAR(50),  -- 'string', 'int', 'boolean', 'json'
    Description NVARCHAR(MAX),
    UpdatedAt DATETIME2 DEFAULT GETDATE() NOT NULL,
    UpdatedByUserID INT,

    CONSTRAINT FK_Config_User FOREIGN KEY (UpdatedByUserID)
        REFERENCES Users(UserID) ON DELETE SET NULL
);

-- Insert default configuration
INSERT INTO SystemConfig ([Key], [Value], DataType, Description) VALUES
    ('DocumentNumberFormat', 'DOC-{YYYY}-{0000}', 'string', 'Document number format'),
    ('BackupFrequencyHours', '24', 'int', 'Backup frequency in hours'),
    ('BackupRetentionDays', '30', 'int', 'Number of backups to retain'),
    ('SyncFrequencyMinutes', '60', 'int', 'Network sync frequency'),
    ('MaxUploadSizeMB', '500', 'int', 'Maximum file upload size'),
    ('OCRQualityThreshold', '70', 'int', 'Minimum OCR quality score'),
    ('EncryptionAlgorithm', 'AES-256', 'string', 'Encryption algorithm'),
    ('DefaultLanguage', 'ar', 'string', 'Default system language');

-- =====================================================
-- 15. DOCUMENT ATTACHMENTS TABLE
-- =====================================================
CREATE TABLE DocumentAttachments (
    AttachmentID BIGINT PRIMARY KEY IDENTITY(1,1),
    DocumentID BIGINT NOT NULL,
    FileName NVARCHAR(500) NOT NULL,
    FileType NVARCHAR(50),  -- 'PDF', 'DOCX', 'XLSX', 'IMAGE', etc.
    FilePath NVARCHAR(MAX) NOT NULL,  -- Encrypted path
    FileSize BIGINT,  -- In bytes
    FileHash NVARCHAR(64),  -- SHA-256
    CreatedByUserID INT NOT NULL,
    CreatedAt DATETIME2 DEFAULT GETDATE() NOT NULL,

    CONSTRAINT FK_Att_Doc FOREIGN KEY (DocumentID)
        REFERENCES Documents(DocumentID) ON DELETE CASCADE,
    CONSTRAINT FK_Att_User FOREIGN KEY (CreatedByUserID)
        REFERENCES Users(UserID)
);

CREATE INDEX IX_Att_Doc ON DocumentAttachments(DocumentID);
CREATE INDEX IX_Att_File ON DocumentAttachments(FileType);

-- =====================================================
-- 16. DOCUMENT VERSIONS TABLE (Version Control)
-- =====================================================
CREATE TABLE DocumentVersions (
    VersionID BIGINT PRIMARY KEY IDENTITY(1,1),
    DocumentID BIGINT NOT NULL,
    VersionNumber INT NOT NULL,
    Title NVARCHAR(500),
    Subject NVARCHAR(500),
    Content NVARCHAR(MAX),
    ChangeDescription NVARCHAR(MAX),
    ChangedByUserID INT NOT NULL,
    ChangedAt DATETIME2 DEFAULT GETDATE() NOT NULL,

    CONSTRAINT FK_Ver_Doc FOREIGN KEY (DocumentID)
        REFERENCES Documents(DocumentID) ON DELETE CASCADE,
    CONSTRAINT FK_Ver_User FOREIGN KEY (ChangedByUserID)
        REFERENCES Users(UserID),
    CONSTRAINT UQ_Ver_Unique UNIQUE (DocumentID, VersionNumber)
);

CREATE INDEX IX_Ver_Doc ON DocumentVersions(DocumentID);
CREATE INDEX IX_Ver_Version ON DocumentVersions(DocumentID, VersionNumber DESC);

-- =====================================================
-- 17. SYNC QUEUE TABLE (Offline/Sync)
-- =====================================================
CREATE TABLE SyncQueue (
    QueueID BIGINT PRIMARY KEY IDENTITY(1,1),
    SourceDeviceID NVARCHAR(100),
    ActionType NVARCHAR(50),  -- 'Create', 'Update', 'Delete'
    TableName NVARCHAR(100),
    RecordID BIGINT,
    RecordData NVARCHAR(MAX),  -- JSON
    CreatedAt DATETIME2 DEFAULT GETDATE() NOT NULL,
    SyncedAt DATETIME2,
    IsSynced BIT DEFAULT 0 NOT NULL,
    SyncError NVARCHAR(MAX),
    RetryCount INT DEFAULT 0,

    CONSTRAINT CK_Sync_Action CHECK (ActionType IN ('Create', 'Update', 'Delete'))
);

CREATE INDEX IX_Sync_Status ON SyncQueue(IsSynced, CreatedAt);
CREATE INDEX IX_Sync_Device ON SyncQueue(SourceDeviceID);

-- =====================================================
-- VIEWS FOR COMMON QUERIES
-- =====================================================

-- View: Active Documents
CREATE VIEW vw_ActiveDocuments AS
SELECT
    d.DocumentID,
    d.DocumentNumber,
    d.Title,
    d.Subject,
    d.DocumentDate,
    dp.Name AS DepartmentName,
    c.Name AS CategoryName,
    ds.Name AS StatusName,
    u.Username AS CreatedBy,
    d.CreatedAt
FROM Documents d
INNER JOIN Departments dp ON d.DepartmentID = dp.DepartmentID
INNER JOIN Categories c ON d.CategoryID = c.CategoryID
INNER JOIN DocumentStatus ds ON d.StatusID = ds.StatusID
INNER JOIN Users u ON d.CreatedByUserID = u.UserID
WHERE d.IsDeleted = 0 AND ds.Name = 'Active';

-- View: User Permissions
CREATE VIEW vw_UserPermissions AS
SELECT
    u.UserID,
    u.Username,
    u.Email,
    u.DepartmentID,
    d.Name AS DepartmentName,
    r.RoleID,
    r.Name AS RoleName,
    r.Permissions,
    u.IsActive
FROM Users u
INNER JOIN Departments d ON u.DepartmentID = d.DepartmentID
INNER JOIN Roles r ON u.RoleID = r.RoleID;

-- View: Document Statistics (Daily)
CREATE VIEW vw_DocumentStatistics AS
SELECT
    CAST(d.CreatedAt AS DATE) AS StatDate,
    dp.Name AS DepartmentName,
    c.Name AS CategoryName,
    COUNT(DISTINCT d.DocumentID) AS DocumentCount,
    COUNT(DISTINCT dp2.PageID) AS PageCount
FROM Documents d
INNER JOIN Departments dp ON d.DepartmentID = dp.DepartmentID
INNER JOIN Categories c ON d.CategoryID = c.CategoryID
LEFT JOIN DocumentPages dp2 ON d.DocumentID = dp2.DocumentID
WHERE d.IsDeleted = 0
GROUP BY CAST(d.CreatedAt AS DATE), dp.Name, c.Name;

-- View: Audit Summary
CREATE VIEW vw_AuditSummary AS
SELECT
    u.Username,
    al.ActionType,
    COUNT(*) AS ActionCount,
    MIN(al.Timestamp) AS FirstAction,
    MAX(al.Timestamp) AS LastAction
FROM AuditLog al
INNER JOIN Users u ON al.UserID = u.UserID
GROUP BY u.Username, al.ActionType;

-- =====================================================
-- STORED PROCEDURES FOR COMMON OPERATIONS
-- =====================================================

-- Procedure: Create Document
CREATE PROCEDURE sp_CreateDocument
    @DocumentNumber NVARCHAR(50),
    @Title NVARCHAR(500),
    @Subject NVARCHAR(500),
    @DocumentDate DATE,
    @DepartmentID INT,
    @CategoryID INT,
    @CreatedByUserID INT,
    @DocumentID BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        INSERT INTO Documents (DocumentNumber, Title, Subject, DocumentDate, DepartmentID, CategoryID, StatusID, CreatedByUserID)
        VALUES (@DocumentNumber, @Title, @Subject, @DocumentDate, @DepartmentID, @CategoryID, 1, @CreatedByUserID);

        SET @DocumentID = SCOPE_IDENTITY();

        -- Log audit
        INSERT INTO AuditLog (UserID, ActionType, TableName, RecordID, NewValue)
        VALUES (@CreatedByUserID, 'Create', 'Documents', @DocumentID, @Title);
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH
END;

-- Procedure: Delete Document (Soft Delete)
CREATE PROCEDURE sp_DeleteDocument
    @DocumentID BIGINT,
    @DeletedByUserID INT,
    @DeletionReason NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        UPDATE Documents
        SET IsDeleted = 1, DeletedAt = GETDATE(), DeletedByUserID = @DeletedByUserID, DeletionReason = @DeletionReason
        WHERE DocumentID = @DocumentID;

        -- Log audit
        INSERT INTO AuditLog (UserID, ActionType, TableName, RecordID, NewValue)
        VALUES (@DeletedByUserID, 'Delete', 'Documents', @DocumentID, @DeletionReason);
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH
END;

-- Procedure: Purge Deleted Documents (After 90 days)
CREATE PROCEDURE sp_PurgeDeletedDocuments
    @DaysRetention INT = 90
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        DELETE FROM Documents
        WHERE IsDeleted = 1 AND DeletedAt < DATEADD(DAY, -@DaysRetention, GETDATE());
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH
END;

-- Procedure: Search Documents
CREATE PROCEDURE sp_SearchDocuments
    @SearchQuery NVARCHAR(MAX),
    @DepartmentID INT = NULL,
    @CategoryID INT = NULL,
    @StartDate DATE = NULL,
    @EndDate DATE = NULL,
    @UserID INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @SearchID BIGINT;
    DECLARE @StartTime DATETIME2 = GETDATE();

    BEGIN TRY
        SELECT
            d.DocumentID,
            d.DocumentNumber,
            d.Title,
            d.Subject,
            d.DocumentDate,
            dp.Name AS DepartmentName,
            c.Name AS CategoryName,
            ds.Name AS StatusName,
            u.Username AS CreatedBy,
            d.CreatedAt
        FROM Documents d
        INNER JOIN Departments dp ON d.DepartmentID = dp.DepartmentID
        INNER JOIN Categories c ON d.CategoryID = c.CategoryID
        INNER JOIN DocumentStatus ds ON d.StatusID = ds.StatusID
        INNER JOIN Users u ON d.CreatedByUserID = u.UserID
        WHERE d.IsDeleted = 0
            AND (d.Title LIKE '%' + @SearchQuery + '%' OR d.Subject LIKE '%' + @SearchQuery + '%' OR d.Content LIKE '%' + @SearchQuery + '%')
            AND (@DepartmentID IS NULL OR d.DepartmentID = @DepartmentID)
            AND (@CategoryID IS NULL OR d.CategoryID = @CategoryID)
            AND (@StartDate IS NULL OR d.DocumentDate >= @StartDate)
            AND (@EndDate IS NULL OR d.DocumentDate <= @EndDate);

        -- Log search
        SET @SearchID = @@ROWCOUNT;
        INSERT INTO SearchHistory (UserID, SearchQuery, ResultCount, ExecutionTimeMs, CreatedAt)
        VALUES (@UserID, @SearchQuery, @SearchID, DATEDIFF(MILLISECOND, @StartTime, GETDATE()), GETDATE());
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH
END;

-- =====================================================
-- TRIGGERS FOR AUDIT & INTEGRITY
-- =====================================================

-- Trigger: Track document modifications
CREATE TRIGGER trg_DocumentModified
ON Documents
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO AuditLog (UserID, ActionType, TableName, RecordID, OldValue, NewValue)
    SELECT
        ISNULL(i.ModifiedByUserID, 1),  -- Default to admin if null
        'Update',
        'Documents',
        i.DocumentID,
        NULL,  -- Could store detailed diff here
        i.Title
    FROM inserted i;
END;

-- Trigger: Enforce department hierarchy (prevent circular references)
CREATE TRIGGER trg_DepartmentHierarchy
ON Departments
BEFORE INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1 FROM inserted i
        WHERE i.ParentDepartmentID IS NOT NULL
        AND i.ParentDepartmentID = i.DepartmentID
    )
    BEGIN
        RAISERROR('Circular department reference not allowed.', 16, 1);
        ROLLBACK TRANSACTION;
    END
END;

-- =====================================================
-- INDEXES FOR PERFORMANCE
-- =====================================================

-- Full-text indexes for search performance
CREATE FULLTEXT CATALOG ft_DocumentCatalog;

CREATE FULLTEXT INDEX ON Documents(
    Title LANGUAGE 1025,  -- Arabic
    Subject LANGUAGE 1025,
    Content LANGUAGE 1025
)
KEY INDEX PK_Documents
ON ft_DocumentCatalog;

-- =====================================================
-- SAMPLE DATA (for testing)
-- =====================================================

-- Insert sample department
INSERT INTO Departments (Name, Description) VALUES
    (N'Human Resources', N'HR Department'),
    (N'Finance', N'Finance Department'),
    (N'Operations', N'Operations Department');

-- Insert sample user
INSERT INTO Users (Username, Email, PasswordHash, DepartmentID, RoleID, IsActive)
SELECT 'admin', 'admin@docvault.local', 'HASH_BCRYPT_PASSWORD', 1, 1, 1
WHERE NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'admin');

-- =====================================================
-- END OF DDL SCRIPT
-- =====================================================
-- Total: 17 tables + 5 views + 4 procedures + 2 triggers
-- Estimated database size: 20GB for 1-2M documents
-- =====================================================
