-- =====================================================
-- DocVault Module 3: User & Permission Management
-- Database Schema - UserDepartmentRoles, Permissions
-- Date: June 21, 2026
-- =====================================================

-- =====================================================
-- USER_DEPARTMENT_ROLES TABLE (Multi-role assignments)
-- =====================================================

CREATE TABLE [dbo].[UserDepartmentRoles] (
    [UserDeptRoleID] INT PRIMARY KEY IDENTITY(1,1),
    [UserID] INT NOT NULL,
    [DepartmentID] INT NOT NULL,
    [RoleID] INT NOT NULL,
    [AssignedByUserID] INT,
    [AssignedAt] DATETIME2 DEFAULT GETDATE(),
    [EffectiveFrom] DATETIME2,
    [EffectiveTo] DATETIME2,
    [IsPrimary] BIT DEFAULT 0,  -- Primary department/role
    [Notes] NVARCHAR(MAX),
    [IsActive] BIT DEFAULT 1 NOT NULL,

    CONSTRAINT FK_UDR_User FOREIGN KEY (UserID)
        REFERENCES [dbo].[Users](UserID) ON DELETE CASCADE,
    CONSTRAINT FK_UDR_Dept FOREIGN KEY (DepartmentID)
        REFERENCES [dbo].[Departments](DepartmentID),
    CONSTRAINT FK_UDR_Role FOREIGN KEY (RoleID)
        REFERENCES [dbo].[Roles](RoleID),
    CONSTRAINT FK_UDR_AssignedBy FOREIGN KEY (AssignedByUserID)
        REFERENCES [dbo].[Users](UserID) ON DELETE SET NULL,
    CONSTRAINT UQ_UserDeptRole UNIQUE (UserID, DepartmentID, RoleID)
);

CREATE NONCLUSTERED INDEX [IX_UDR_User] ON [dbo].[UserDepartmentRoles]([UserID]);
CREATE NONCLUSTERED INDEX [IX_UDR_Dept] ON [dbo].[UserDepartmentRoles]([DepartmentID]);
CREATE NONCLUSTERED INDEX [IX_UDR_Role] ON [dbo].[UserDepartmentRoles]([RoleID]);
CREATE NONCLUSTERED INDEX [IX_UDR_Active] ON [dbo].[UserDepartmentRoles]([IsActive]);
CREATE NONCLUSTERED INDEX [IX_UDR_Primary] ON [dbo].[UserDepartmentRoles]([UserID], [IsPrimary]);

-- =====================================================
-- DEPARTMENT_PERMISSIONS TABLE (Dept-specific perms)
-- =====================================================

CREATE TABLE [dbo].[DepartmentPermissions] (
    [DeptPermID] INT PRIMARY KEY IDENTITY(1,1),
    [DepartmentID] INT NOT NULL,
    [PermissionID] INT NOT NULL,
    [IsRequiredPermission] BIT DEFAULT 0,
    [Notes] NVARCHAR(MAX),
    [CreatedAt] DATETIME2 DEFAULT GETDATE(),

    CONSTRAINT FK_DP_Dept FOREIGN KEY (DepartmentID)
        REFERENCES [dbo].[Departments](DepartmentID) ON DELETE CASCADE,
    CONSTRAINT FK_DP_Perm FOREIGN KEY (PermissionID)
        REFERENCES [dbo].[Permissions](PermissionID),
    CONSTRAINT UQ_DeptPerm UNIQUE (DepartmentID, PermissionID)
);

CREATE NONCLUSTERED INDEX [IX_DP_Dept] ON [dbo].[DepartmentPermissions]([DepartmentID]);
CREATE NONCLUSTERED INDEX [IX_DP_Perm] ON [dbo].[DepartmentPermissions]([PermissionID]);

-- =====================================================
-- USER_AUDIT_LOG TABLE (Extended audit)
-- =====================================================

CREATE TABLE [dbo].[UserAuditLog] (
    [AuditID] BIGINT PRIMARY KEY IDENTITY(1,1),
    [UserID] INT,
    [TargetUserID] INT,  -- User being modified
    [ActionType] NVARCHAR(50),  -- UserCreated, RoleAssigned, PermissionGranted, etc.
    [ResourceType] NVARCHAR(50),  -- User, Role, Permission, Department
    [ResourceID] INT,
    [OldValue] NVARCHAR(MAX),
    [NewValue] NVARCHAR(MAX),
    [Reason] NVARCHAR(MAX),
    [IPAddress] NVARCHAR(45),
    [IsSuccess] BIT DEFAULT 1,
    [ErrorMessage] NVARCHAR(MAX),
    [Timestamp] DATETIME2 DEFAULT GETDATE(),

    CONSTRAINT FK_UserAudit_User FOREIGN KEY (UserID)
        REFERENCES [dbo].[Users](UserID) ON DELETE SET NULL,
    CONSTRAINT FK_UserAudit_Target FOREIGN KEY (TargetUserID)
        REFERENCES [dbo].[Users](UserID) ON DELETE SET NULL
);

CREATE NONCLUSTERED INDEX [IX_UserAudit_Action] ON [dbo].[UserAuditLog]([ActionType]);
CREATE NONCLUSTERED INDEX [IX_UserAudit_Target] ON [dbo].[UserAuditLog]([TargetUserID]);
CREATE NONCLUSTERED INDEX [IX_UserAudit_Timestamp] ON [dbo].[UserAuditLog]([Timestamp] DESC);
CREATE NONCLUSTERED INDEX [IX_UserAudit_User] ON [dbo].[UserAuditLog]([UserID]);

-- =====================================================
-- STORED PROCEDURES FOR USER MANAGEMENT
-- =====================================================

-- SP: Get user with all roles and permissions
CREATE PROCEDURE [dbo].[sp_GetUserDetails]
    @UserID INT
AS
BEGIN
    SET NOCOUNT ON;

    -- User basic info
    SELECT
        u.[UserID],
        u.[Username],
        u.[Email],
        u.[IsActive],
        u.[LastLoginAt],
        u.[IsMFAEnabled],
        u.[FailedLoginAttempts],
        u.[LockedUntil]
    FROM [dbo].[Users] u
    WHERE u.[UserID] = @UserID;

    -- User roles in each department
    SELECT DISTINCT
        udr.[DepartmentID],
        d.[Name] AS [DepartmentName],
        r.[RoleID],
        r.[Name] AS [RoleName],
        udr.[IsPrimary],
        udr.[EffectiveFrom],
        udr.[EffectiveTo]
    FROM [dbo].[UserDepartmentRoles] udr
    INNER JOIN [dbo].[Departments] d ON udr.[DepartmentID] = d.[DepartmentID]
    INNER JOIN [dbo].[Roles] r ON udr.[RoleID] = r.[RoleID]
    WHERE udr.[UserID] = @UserID
      AND udr.[IsActive] = 1
    ORDER BY udr.[IsPrimary] DESC, d.[Name];

    -- User permissions
    SELECT DISTINCT
        p.[PermissionID],
        p.[Resource],
        p.[Action],
        CONCAT(p.[Resource], '_', p.[Action]) AS [PermissionKey]
    FROM [dbo].[RolePermissions] rp
    INNER JOIN [dbo].[Permissions] p ON rp.[PermissionID] = p.[PermissionID]
    INNER JOIN [dbo].[Roles] r ON rp.[RoleID] = r.[RoleID]
    INNER JOIN [dbo].[UserDepartmentRoles] udr ON r.[RoleID] = udr.[RoleID]
    WHERE udr.[UserID] = @UserID
      AND udr.[IsActive] = 1
      AND p.[IsActive] = 1
    ORDER BY p.[Resource], p.[Action];
END;

-- SP: Get users by department
CREATE PROCEDURE [dbo].[sp_GetDepartmentUsers]
    @DepartmentID INT,
    @OnlyActive BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.[UserID],
        u.[Username],
        u.[Email],
        udr.[RoleID],
        r.[Name] AS [RoleName],
        u.[IsActive],
        u.[LastLoginAt],
        udr.[IsPrimary]
    FROM [dbo].[UserDepartmentRoles] udr
    INNER JOIN [dbo].[Users] u ON udr.[UserID] = u.[UserID]
    INNER JOIN [dbo].[Roles] r ON udr.[RoleID] = r.[RoleID]
    WHERE udr.[DepartmentID] = @DepartmentID
      AND (udr.[IsActive] = 1)
      AND (u.[IsActive] = @OnlyActive OR @OnlyActive = 0)
    ORDER BY udr.[IsPrimary] DESC, u.[Username];
END;

-- SP: Validate user access to resource
CREATE PROCEDURE [dbo].[sp_ValidateUserAccess]
    @UserID INT,
    @Resource NVARCHAR(100),
    @Action NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CanAccess INT = 0;

    SELECT @CanAccess = COUNT(*)
    FROM [dbo].[RolePermissions] rp
    INNER JOIN [dbo].[Permissions] p ON rp.[PermissionID] = p.[PermissionID]
    INNER JOIN [dbo].[Roles] r ON rp.[RoleID] = r.[RoleID]
    INNER JOIN [dbo].[UserDepartmentRoles] udr ON r.[RoleID] = udr.[RoleID]
    INNER JOIN [dbo].[Users] u ON udr.[UserID] = u.[UserID]
    WHERE u.[UserID] = @UserID
      AND p.[Resource] = @Resource
      AND p.[Action] = @Action
      AND u.[IsActive] = 1
      AND udr.[IsActive] = 1
      AND (udr.[EffectiveFrom] IS NULL OR udr.[EffectiveFrom] <= GETDATE())
      AND (udr.[EffectiveTo] IS NULL OR udr.[EffectiveTo] > GETDATE());

    SELECT @CanAccess AS [CanAccess];
END;

-- SP: Get user audit trail
CREATE PROCEDURE [dbo].[sp_GetUserAuditTrail]
    @TargetUserID INT,
    @FromDate DATETIME2 = NULL,
    @ToDate DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ActualFromDate DATETIME2 = ISNULL(@FromDate, DATEADD(MONTH, -1, GETDATE()));
    DECLARE @ActualToDate DATETIME2 = ISNULL(@ToDate, GETDATE());

    SELECT
        [AuditID],
        [UserID],
        [ActionType],
        [ResourceType],
        [OldValue],
        [NewValue],
        [Reason],
        [IsSuccess],
        [Timestamp]
    FROM [dbo].[UserAuditLog]
    WHERE [TargetUserID] = @TargetUserID
      AND [Timestamp] >= @ActualFromDate
      AND [Timestamp] <= @ActualToDate
    ORDER BY [Timestamp] DESC;
END;

-- =====================================================
-- VIEWS FOR USER MANAGEMENT
-- =====================================================

-- View: User with primary department and role
CREATE VIEW [dbo].[vw_UserPrimaryRole] AS
SELECT
    u.[UserID],
    u.[Username],
    u.[Email],
    u.[IsActive],
    d.[DepartmentID],
    d.[Name] AS [DepartmentName],
    r.[RoleID],
    r.[Name] AS [RoleName],
    u.[LastLoginAt],
    u.[FailedLoginAttempts]
FROM [dbo].[Users] u
LEFT JOIN [dbo].[UserDepartmentRoles] udr ON u.[UserID] = udr.[UserID]
    AND udr.[IsPrimary] = 1
    AND udr.[IsActive] = 1
LEFT JOIN [dbo].[Departments] d ON udr.[DepartmentID] = d.[DepartmentID]
LEFT JOIN [dbo].[Roles] r ON udr.[RoleID] = r.[RoleID]
WHERE u.[IsActive] = 1;

-- View: User access report
CREATE VIEW [dbo].[vw_UserAccessReport] AS
SELECT
    u.[UserID],
    u.[Username],
    u.[Email],
    COUNT(DISTINCT udr.[DepartmentID]) AS [DepartmentCount],
    COUNT(DISTINCT udr.[RoleID]) AS [RoleCount],
    COUNT(DISTINCT p.[PermissionID]) AS [PermissionCount],
    MAX(u.[LastLoginAt]) AS [LastLogin],
    u.[IsActive]
FROM [dbo].[Users] u
LEFT JOIN [dbo].[UserDepartmentRoles] udr ON u.[UserID] = udr.[UserID]
    AND udr.[IsActive] = 1
LEFT JOIN [dbo].[RolePermissions] rp ON udr.[RoleID] = rp.[RoleID]
LEFT JOIN [dbo].[Permissions] p ON rp.[PermissionID] = p.[PermissionID]
WHERE u.[IsActive] = 1
GROUP BY u.[UserID], u.[Username], u.[Email], u.[IsActive];

-- =====================================================
-- END OF USER PERMISSION MANAGEMENT SCHEMA
-- =====================================================
