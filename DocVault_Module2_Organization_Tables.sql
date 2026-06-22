-- =====================================================
-- DocVault Module 2: Organization Structure
-- Database Schema - Branches, Departments, Units
-- Date: June 21, 2026
-- =====================================================

-- =====================================================
-- BRANCHES TABLE (Regional/HQ branches)
-- =====================================================

CREATE TABLE [dbo].[Branches] (
    [BranchID] INT PRIMARY KEY IDENTITY(1,1),
    [Name] NVARCHAR(255) NOT NULL UNIQUE,
    [Code] NVARCHAR(50) NOT NULL UNIQUE,  -- HQ, CAIRO, ALEX, etc.
    [Description] NVARCHAR(MAX),
    [Location] NVARCHAR(255),              -- City/Region
    [HeadUserID] INT,                      -- Branch director
    [PhoneNumber] NVARCHAR(20),
    [Email] NVARCHAR(255),
    [ParentBranchID] INT,                  -- For sub-branches
    [IsHeadquarters] BIT DEFAULT 0,        -- HQ branch marker
    [IsActive] BIT DEFAULT 1 NOT NULL,
    [CreatedAt] DATETIME2 DEFAULT GETDATE(),
    [UpdatedAt] DATETIME2 DEFAULT GETDATE(),

    CONSTRAINT FK_Branch_Head FOREIGN KEY (HeadUserID)
        REFERENCES [dbo].[Users](UserID) ON DELETE SET NULL,
    CONSTRAINT FK_Branch_Parent FOREIGN KEY (ParentBranchID)
        REFERENCES [dbo].[Branches](BranchID) ON DELETE SET NULL,
    CONSTRAINT CK_Branch_Code CHECK (LEN(TRIM([Code])) > 0)
);

CREATE NONCLUSTERED INDEX [IX_Branch_Head] ON [dbo].[Branches]([HeadUserID]);
CREATE NONCLUSTERED INDEX [IX_Branch_Parent] ON [dbo].[Branches]([ParentBranchID]);
CREATE NONCLUSTERED INDEX [IX_Branch_Active] ON [dbo].[Branches]([IsActive]);
CREATE NONCLUSTERED INDEX [IX_Branch_HQ] ON [dbo].[Branches]([IsHeadquarters]);

-- =====================================================
-- DEPARTMENTS TABLE (Departments within branches)
-- =====================================================

CREATE TABLE [dbo].[Departments] (
    [DepartmentID] INT PRIMARY KEY IDENTITY(1,1),
    [Name] NVARCHAR(255) NOT NULL,
    [Code] NVARCHAR(50) NOT NULL,         -- HR, FIN, OPS, etc.
    [Description] NVARCHAR(MAX),
    [BranchID] INT NOT NULL,              -- Parent branch
    [ManagerUserID] INT,                  -- Department head
    [Location] NVARCHAR(255),             -- Physical location
    [PhoneNumber] NVARCHAR(20),
    [Email] NVARCHAR(255),
    [BudgetCode] NVARCHAR(50),            -- Financial tracking
    [HeadquartersSync] BIT DEFAULT 0,     -- Sync with HQ
    [IsActive] BIT DEFAULT 1 NOT NULL,
    [CreatedAt] DATETIME2 DEFAULT GETDATE(),
    [UpdatedAt] DATETIME2 DEFAULT GETDATE(),

    CONSTRAINT FK_Dept_Branch FOREIGN KEY (BranchID)
        REFERENCES [dbo].[Branches](BranchID),
    CONSTRAINT FK_Dept_Manager FOREIGN KEY (ManagerUserID)
        REFERENCES [dbo].[Users](UserID) ON DELETE SET NULL,
    CONSTRAINT UQ_Dept_Code UNIQUE (BranchID, Code),
    CONSTRAINT CK_Dept_Code CHECK (LEN(TRIM([Code])) > 0)
);

CREATE NONCLUSTERED INDEX [IX_Dept_Branch] ON [dbo].[Departments]([BranchID]);
CREATE NONCLUSTERED INDEX [IX_Dept_Manager] ON [dbo].[Departments]([ManagerUserID]);
CREATE NONCLUSTERED INDEX [IX_Dept_Active] ON [dbo].[Departments]([IsActive]);
CREATE NONCLUSTERED INDEX [IX_Dept_Name] ON [dbo].[Departments]([Name]);

-- =====================================================
-- UNITS TABLE (Sub-units within departments)
-- =====================================================

CREATE TABLE [dbo].[Units] (
    [UnitID] INT PRIMARY KEY IDENTITY(1,1),
    [Name] NVARCHAR(255) NOT NULL,
    [Code] NVARCHAR(50) NOT NULL,
    [Description] NVARCHAR(MAX),
    [DepartmentID] INT NOT NULL,          -- Parent department
    [SupervisorUserID] INT,               -- Unit supervisor
    [Location] NVARCHAR(255),
    [PhoneNumber] NVARCHAR(20),
    [Email] NVARCHAR(255),
    [Responsibility] NVARCHAR(MAX),       -- Core responsibility
    [IsActive] BIT DEFAULT 1 NOT NULL,
    [CreatedAt] DATETIME2 DEFAULT GETDATE(),
    [UpdatedAt] DATETIME2 DEFAULT GETDATE(),

    CONSTRAINT FK_Unit_Dept FOREIGN KEY (DepartmentID)
        REFERENCES [dbo].[Departments](DepartmentID) ON DELETE CASCADE,
    CONSTRAINT FK_Unit_Supervisor FOREIGN KEY (SupervisorUserID)
        REFERENCES [dbo].[Users](UserID) ON DELETE SET NULL,
    CONSTRAINT UQ_Unit_Code UNIQUE (DepartmentID, Code),
    CONSTRAINT CK_Unit_Code CHECK (LEN(TRIM([Code])) > 0)
);

CREATE NONCLUSTERED INDEX [IX_Unit_Dept] ON [dbo].[Units]([DepartmentID]);
CREATE NONCLUSTERED INDEX [IX_Unit_Supervisor] ON [dbo].[Units]([SupervisorUserID]);
CREATE NONCLUSTERED INDEX [IX_Unit_Active] ON [dbo].[Units]([IsActive]);

-- =====================================================
-- DEPARTMENT_HIERARCHY TABLE (Materialized path)
-- =====================================================

CREATE TABLE [dbo].[DepartmentHierarchy] (
    [HierarchyID] INT PRIMARY KEY IDENTITY(1,1),
    [ChildDepartmentID] INT NOT NULL,
    [ParentDepartmentID] INT NOT NULL,
    [Depth] INT NOT NULL,                 -- Levels deep
    [Path] NVARCHAR(MAX) NOT NULL,        -- Path like "1/5/12/42"
    [CreatedAt] DATETIME2 DEFAULT GETDATE(),

    CONSTRAINT FK_Hier_Child FOREIGN KEY (ChildDepartmentID)
        REFERENCES [dbo].[Departments](DepartmentID) ON DELETE CASCADE,
    CONSTRAINT FK_Hier_Parent FOREIGN KEY (ParentDepartmentID)
        REFERENCES [dbo].[Departments](DepartmentID) ON DELETE CASCADE,
    CONSTRAINT UQ_Hierarchy UNIQUE (ChildDepartmentID, ParentDepartmentID)
);

CREATE NONCLUSTERED INDEX [IX_Hier_Child] ON [dbo].[DepartmentHierarchy]([ChildDepartmentID]);
CREATE NONCLUSTERED INDEX [IX_Hier_Parent] ON [dbo].[DepartmentHierarchy]([ParentDepartmentID]);
CREATE NONCLUSTERED INDEX [IX_Hier_Depth] ON [dbo].[DepartmentHierarchy]([Depth]);

-- =====================================================
-- STORED PROCEDURES FOR ORGANIZATION
-- =====================================================

-- SP: Get organization hierarchy (full tree)
CREATE PROCEDURE [dbo].[sp_GetOrganizationHierarchy]
AS
BEGIN
    SET NOCOUNT ON;

    -- Get HQ branch
    SELECT TOP 1
        [BranchID],
        [Name],
        [Code],
        [Location],
        [IsHeadquarters]
    FROM [dbo].[Branches]
    WHERE [IsHeadquarters] = 1
    ORDER BY [CreatedAt];

    -- Get all branches
    SELECT
        [BranchID],
        [Name],
        [Code],
        [Location],
        [ParentBranchID],
        [HeadUserID]
    FROM [dbo].[Branches]
    WHERE [IsActive] = 1
    ORDER BY [ParentBranchID], [Name];

    -- Get all departments
    SELECT
        [DepartmentID],
        [Name],
        [Code],
        [BranchID],
        [ManagerUserID]
    FROM [dbo].[Departments]
    WHERE [IsActive] = 1
    ORDER BY [BranchID], [Name];
END;

-- SP: Get department path (ancestors to root)
CREATE PROCEDURE [dbo].[sp_GetDepartmentPath]
    @DepartmentID INT
AS
BEGIN
    SET NOCOUNT ON;

    WITH DeptPath AS (
        SELECT
            [DepartmentID],
            [Name],
            [Code],
            0 AS [Level]
        FROM [dbo].[Departments]
        WHERE [DepartmentID] = @DepartmentID

        UNION ALL

        SELECT
            d.[DepartmentID],
            d.[Name],
            d.[Code],
            dp.[Level] + 1
        FROM [dbo].[Departments] d
        INNER JOIN DeptPath dp ON d.[DepartmentID] = dp.[DepartmentID]
        -- Join would go up hierarchy (requires parent tracking)
    )
    SELECT * FROM DeptPath
    ORDER BY [Level];
END;

-- SP: Get organization statistics
CREATE PROCEDURE [dbo].[sp_GetOrganizationStatistics]
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TotalBranches INT;
    DECLARE @TotalDepartments INT;
    DECLARE @TotalUnits INT;
    DECLARE @TotalUsers INT;

    SELECT @TotalBranches = COUNT(*) FROM [dbo].[Branches] WHERE [IsActive] = 1;
    SELECT @TotalDepartments = COUNT(*) FROM [dbo].[Departments] WHERE [IsActive] = 1;
    SELECT @TotalUnits = COUNT(*) FROM [dbo].[Units] WHERE [IsActive] = 1;
    SELECT @TotalUsers = COUNT(*) FROM [dbo].[Users] WHERE [IsActive] = 1;

    SELECT
        @TotalBranches AS [TotalBranches],
        @TotalDepartments AS [TotalDepartments],
        @TotalUnits AS [TotalUnits],
        @TotalUsers AS [TotalUsers];

    -- Users by branch
    SELECT
        b.[BranchID],
        b.[Name] AS [BranchName],
        COUNT(u.[UserID]) AS [UserCount]
    FROM [dbo].[Branches] b
    LEFT JOIN [dbo].[Departments] d ON b.[BranchID] = d.[BranchID]
    LEFT JOIN [dbo].[Users] u ON d.[DepartmentID] = u.[DepartmentID]
    WHERE b.[IsActive] = 1
    GROUP BY b.[BranchID], b.[Name]
    ORDER BY [UserCount] DESC;
END;

-- SP: Get department users count
CREATE PROCEDURE [dbo].[sp_GetDepartmentUserCount]
    @DepartmentID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(DISTINCT [UserID]) AS [UserCount]
    FROM [dbo].[Users]
    WHERE [DepartmentID] = @DepartmentID
      AND [IsActive] = 1;
END;

-- =====================================================
-- VIEWS FOR ORGANIZATION
-- =====================================================

-- View: Organization structure (branches, departments, units)
CREATE VIEW [dbo].[vw_OrganizationStructure] AS
SELECT
    b.[BranchID],
    b.[Name] AS [BranchName],
    d.[DepartmentID],
    d.[Name] AS [DepartmentName],
    u.[UnitID],
    u.[Name] AS [UnitName],
    u.[SupervisorUserID],
    CONCAT(us.[Username], ' (', us.[Email], ')') AS [Supervisor]
FROM [dbo].[Branches] b
LEFT JOIN [dbo].[Departments] d ON b.[BranchID] = d.[BranchID]
LEFT JOIN [dbo].[Units] u ON d.[DepartmentID] = u.[DepartmentID]
LEFT JOIN [dbo].[Users] us ON u.[SupervisorUserID] = us.[UserID]
WHERE b.[IsActive] = 1
  AND (d.[IsActive] = 1 OR d.[DepartmentID] IS NULL)
  AND (u.[IsActive] = 1 OR u.[UnitID] IS NULL);

-- View: Reporting lines (who reports to whom)
CREATE VIEW [dbo].[vw_ReportingLines] AS
SELECT
    u.[UserID],
    u.[Username],
    u.[Email],
    d.[DepartmentID],
    d.[Name] AS [DepartmentName],
    dm.[UserID] AS [ManagerID],
    dm.[Username] AS [ManagerName],
    b.[BranchID],
    b.[Name] AS [BranchName]
FROM [dbo].[Users] u
LEFT JOIN [dbo].[Departments] d ON u.[DepartmentID] = d.[DepartmentID]
LEFT JOIN [dbo].[Users] dm ON d.[ManagerUserID] = dm.[UserID]
LEFT JOIN [dbo].[Branches] b ON d.[BranchID] = b.[BranchID]
WHERE u.[IsActive] = 1;

-- =====================================================
-- INSERT DEFAULT DATA
-- =====================================================

-- Insert HQ Branch
INSERT INTO [dbo].[Branches] (
    [Name], [Code], [Description], [Location],
    [IsHeadquarters], [IsActive]
) VALUES (
    N'Headquarters',
    N'HQ',
    N'Headquarters - Central Office',
    N'Capital City',
    1,
    1
);

-- Insert Regional Branches (for structure)
-- Note: In production, these would be added by users

-- =====================================================
-- AUDIT TRIGGERS (Optional)
-- =====================================================

-- Trigger: Log branch changes
CREATE TRIGGER [dbo].[trg_Branch_Audit]
ON [dbo].[Branches]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM inserted)
    BEGIN
        INSERT INTO [dbo].[AuditLog] (
            [ActionType], [TableName], [RecordID],
            [OldValue], [NewValue], [IsSuccess], [Timestamp]
        )
        SELECT
            CASE WHEN EXISTS (SELECT 1 FROM deleted) THEN 'Update' ELSE 'Insert' END,
            'Branches',
            i.[BranchID],
            CONVERT(NVARCHAR(MAX), d.[Name]),
            CONVERT(NVARCHAR(MAX), i.[Name]),
            1,
            GETDATE()
        FROM inserted i
        LEFT JOIN deleted d ON i.[BranchID] = d.[BranchID];
    END;

    IF EXISTS (SELECT 1 FROM deleted) AND NOT EXISTS (SELECT 1 FROM inserted)
    BEGIN
        INSERT INTO [dbo].[AuditLog] (
            [ActionType], [TableName], [RecordID],
            [OldValue], [IsSuccess], [Timestamp]
        )
        SELECT
            'Delete',
            'Branches',
            d.[BranchID],
            CONVERT(NVARCHAR(MAX), d.[Name]),
            1,
            GETDATE()
        FROM deleted d;
    END;
END;

-- Similar trigger for Departments
CREATE TRIGGER [dbo].[trg_Department_Audit]
ON [dbo].[Departments]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM inserted)
    BEGIN
        INSERT INTO [dbo].[AuditLog] (
            [ActionType], [TableName], [RecordID],
            [OldValue], [NewValue], [IsSuccess], [Timestamp]
        )
        SELECT
            CASE WHEN EXISTS (SELECT 1 FROM deleted) THEN 'Update' ELSE 'Insert' END,
            'Departments',
            i.[DepartmentID],
            CONVERT(NVARCHAR(MAX), d.[Name]),
            CONVERT(NVARCHAR(MAX), i.[Name]),
            1,
            GETDATE()
        FROM inserted i
        LEFT JOIN deleted d ON i.[DepartmentID] = d.[DepartmentID];
    END;
END;

-- =====================================================
-- END OF ORGANIZATION MODULE SCHEMA
-- =====================================================
