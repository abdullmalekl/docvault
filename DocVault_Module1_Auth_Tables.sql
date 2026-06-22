-- =====================================================
-- DocVault Module 1: Authentication & Security
-- SQL Scripts - Users, Roles, Permissions, Audit
-- Date: June 21, 2026
-- =====================================================

-- =====================================================
-- USERS TABLE (with enhanced fields for auth)
-- =====================================================

CREATE TABLE [dbo].[Users] (
    [UserID] INT PRIMARY KEY IDENTITY(1,1),
    [Username] NVARCHAR(100) NOT NULL UNIQUE,
    [Email] NVARCHAR(255) NOT NULL,
    [PasswordHash] NVARCHAR(255) NOT NULL,  -- Bcrypt hash (60 chars)
    [DepartmentID] INT NOT NULL,
    [RoleID] INT NOT NULL,
    [IsActive] BIT DEFAULT 1 NOT NULL,

    -- 2FA/MFA Fields
    [IsMFAEnabled] BIT DEFAULT 0 NOT NULL,
    [MFASecret] NVARCHAR(255),  -- Encrypted Google Authenticator secret

    -- Account Lockout
    [FailedLoginAttempts] INT DEFAULT 0,
    [LockedUntil] DATETIME2,  -- Locked until this timestamp

    -- Login Tracking
    [LastLoginAt] DATETIME2,
    [LastLoginIP] NVARCHAR(45),  -- IPv4 or IPv6

    -- Password Management
    [PasswordExpiresAt] DATETIME2,  -- 90 days from change
    [RequirePasswordChange] BIT DEFAULT 0,

    -- Audit
    [CreatedAt] DATETIME2 DEFAULT GETDATE() NOT NULL,
    [UpdatedAt] DATETIME2 DEFAULT GETDATE() NOT NULL,
    [CreatedByUserID] INT,

    -- Constraints
    CONSTRAINT CK_User_Username CHECK (LEN(TRIM(Username)) >= 3),
    CONSTRAINT CK_User_Email CHECK (Email LIKE '%@%.%'),
    CONSTRAINT FK_User_Dept FOREIGN KEY (DepartmentID)
        REFERENCES [dbo].[Departments](DepartmentID) ON DELETE RESTRICT,
    CONSTRAINT FK_User_Role FOREIGN KEY (RoleID)
        REFERENCES [dbo].[Roles](RoleID) ON DELETE RESTRICT,
    CONSTRAINT FK_User_Creator FOREIGN KEY (CreatedByUserID)
        REFERENCES [dbo].[Users](UserID) ON DELETE SET NULL
);

-- Indexes for performance
CREATE NONCLUSTERED INDEX [IX_User_Username] ON [dbo].[Users]([Username]);
CREATE NONCLUSTERED INDEX [IX_User_Email] ON [dbo].[Users]([Email]);
CREATE NONCLUSTERED INDEX [IX_User_Dept] ON [dbo].[Users]([DepartmentID]);
CREATE NONCLUSTERED INDEX [IX_User_Role] ON [dbo].[Users]([RoleID]);
CREATE NONCLUSTERED INDEX [IX_User_Active] ON [dbo].[Users]([IsActive]);
CREATE NONCLUSTERED INDEX [IX_User_MFA] ON [dbo].[Users]([IsMFAEnabled]);
CREATE NONCLUSTERED INDEX [IX_User_LastLogin] ON [dbo].[Users]([LastLoginAt] DESC);

-- =====================================================
-- ROLES TABLE
-- =====================================================

CREATE TABLE [dbo].[Roles] (
    [RoleID] INT PRIMARY KEY IDENTITY(1,1),
    [Name] NVARCHAR(100) NOT NULL UNIQUE,
    [Description] NVARCHAR(MAX),
    [IsBuiltIn] BIT DEFAULT 0 NOT NULL,  -- System roles
    [CreatedAt] DATETIME2 DEFAULT GETDATE() NOT NULL,
    [UpdatedAt] DATETIME2 DEFAULT GETDATE() NOT NULL,
    [IsActive] BIT DEFAULT 1 NOT NULL,

    CONSTRAINT CK_Role_Name CHECK (LEN(TRIM(Name)) > 0)
);

-- Insert built-in roles
SET IDENTITY_INSERT [dbo].[Roles] ON;
INSERT INTO [dbo].[Roles] ([RoleID], [Name], [Description], [IsBuiltIn], [IsActive])
VALUES
    (1, N'Admin', N'System administrator with full access', 1, 1),
    (2, N'Manager', N'Department manager with supervisory access', 1, 1),
    (3, N'Operator', N'Document operator for scanning and data entry', 1, 1),
    (4, N'Viewer', N'Read-only access to documents', 1, 1);
SET IDENTITY_INSERT [dbo].[Roles] OFF;

-- =====================================================
-- PERMISSIONS TABLE
-- =====================================================

CREATE TABLE [dbo].[Permissions] (
    [PermissionID] INT PRIMARY KEY IDENTITY(1,1),
    [Resource] NVARCHAR(100) NOT NULL,  -- Document, User, System, Department
    [Action] NVARCHAR(100) NOT NULL,    -- View, Create, Edit, Delete, Print, Export
    [Description] NVARCHAR(MAX),
    [CreatedAt] DATETIME2 DEFAULT GETDATE() NOT NULL,
    [IsActive] BIT DEFAULT 1 NOT NULL,

    CONSTRAINT UQ_Permission UNIQUE (Resource, Action),
    CONSTRAINT CK_Perm_Resource CHECK (Resource IN ('Document', 'User', 'System', 'Department', 'Report')),
    CONSTRAINT CK_Perm_Action CHECK (Action IN ('View', 'Create', 'Edit', 'Delete', 'Print', 'Export', 'Manage'))
);

-- Insert permissions
INSERT INTO [dbo].[Permissions] ([Resource], [Action], [Description])
VALUES
    -- Document permissions
    ('Document', 'View', 'View documents'),
    ('Document', 'Create', 'Create new documents'),
    ('Document', 'Edit', 'Edit document metadata'),
    ('Document', 'Delete', 'Delete documents'),
    ('Document', 'Print', 'Print documents'),
    ('Document', 'Export', 'Export documents to PDF/Excel'),

    -- User permissions
    ('User', 'View', 'View users'),
    ('User', 'Create', 'Create new users'),
    ('User', 'Edit', 'Edit user details'),
    ('User', 'Delete', 'Delete users'),
    ('User', 'Manage', 'Manage user roles and permissions'),

    -- System permissions
    ('System', 'View', 'View system configuration'),
    ('System', 'Edit', 'Edit system configuration'),
    ('System', 'Manage', 'Manage system settings'),

    -- Report permissions
    ('Report', 'View', 'View reports'),
    ('Report', 'Create', 'Create custom reports'),
    ('Report', 'Export', 'Export reports');

-- =====================================================
-- ROLE_PERMISSIONS JUNCTION TABLE
-- =====================================================

CREATE TABLE [dbo].[RolePermissions] (
    [RolePermissionID] INT PRIMARY KEY IDENTITY(1,1),
    [RoleID] INT NOT NULL,
    [PermissionID] INT NOT NULL,
    [CreatedAt] DATETIME2 DEFAULT GETDATE() NOT NULL,

    CONSTRAINT FK_RP_Role FOREIGN KEY (RoleID)
        REFERENCES [dbo].[Roles](RoleID) ON DELETE CASCADE,
    CONSTRAINT FK_RP_Permission FOREIGN KEY (PermissionID)
        REFERENCES [dbo].[Permissions](PermissionID) ON DELETE CASCADE,
    CONSTRAINT UQ_RolePermission UNIQUE (RoleID, PermissionID)
);

-- Assign permissions to roles
-- Admin: All permissions
INSERT INTO [dbo].[RolePermissions] ([RoleID], [PermissionID])
SELECT 1, [PermissionID] FROM [dbo].[Permissions];

-- Manager: Document, User (view), Report permissions
INSERT INTO [dbo].[RolePermissions] ([RoleID], [PermissionID])
SELECT 2, [PermissionID] FROM [dbo].[Permissions]
WHERE Resource IN ('Document', 'Report') OR (Resource = 'User' AND Action = 'View');

-- Operator: Document (Create, Edit), Report (View)
INSERT INTO [dbo].[RolePermissions] ([RoleID], [PermissionID])
SELECT 3, [PermissionID] FROM [dbo].[Permissions]
WHERE (Resource = 'Document' AND Action IN ('Create', 'Edit', 'View'))
   OR (Resource = 'Report' AND Action = 'View');

-- Viewer: Document (View only), Report (View)
INSERT INTO [dbo].[RolePermissions] ([RoleID], [PermissionID])
SELECT 4, [PermissionID] FROM [dbo].[Permissions]
WHERE Action = 'View' AND Resource IN ('Document', 'Report');

-- =====================================================
-- AUDIT_LOG TABLE (Enhanced for auth)
-- =====================================================

CREATE TABLE [dbo].[AuditLog] (
    [AuditID] BIGINT PRIMARY KEY IDENTITY(1,1),
    [UserID] INT,
    [ActionType] NVARCHAR(50) NOT NULL,  -- Login, Logout, PasswordChange, 2FAToggle, etc.
    [TableName] NVARCHAR(100),
    [RecordID] BIGINT,
    [OldValue] NVARCHAR(MAX),
    [NewValue] NVARCHAR(MAX),
    [IPAddress] NVARCHAR(45),
    [UserAgent] NVARCHAR(MAX),
    [DeviceName] NVARCHAR(255),
    [IsSuccess] BIT DEFAULT 1 NOT NULL,
    [FailureReason] NVARCHAR(MAX),
    [ExecutionTimeMs] INT,
    [Timestamp] DATETIME2 DEFAULT GETDATE() NOT NULL,

    CONSTRAINT FK_Audit_User FOREIGN KEY (UserID)
        REFERENCES [dbo].[Users](UserID) ON DELETE SET NULL
);

-- Indexes for audit performance
CREATE NONCLUSTERED INDEX [IX_Audit_Action] ON [dbo].[AuditLog]([ActionType]);
CREATE NONCLUSTERED INDEX [IX_Audit_User] ON [dbo].[AuditLog]([UserID]);
CREATE NONCLUSTERED INDEX [IX_Audit_Timestamp] ON [dbo].[AuditLog]([Timestamp] DESC);
CREATE NONCLUSTERED INDEX [IX_Audit_Success] ON [dbo].[AuditLog]([IsSuccess]);

-- =====================================================
-- LOGIN_HISTORY TABLE (Detailed login tracking)
-- =====================================================

CREATE TABLE [dbo].[LoginHistory] (
    [LoginID] BIGINT PRIMARY KEY IDENTITY(1,1),
    [UserID] INT,
    [Username] NVARCHAR(100) NOT NULL,
    [LoginTime] DATETIME2 DEFAULT GETDATE() NOT NULL,
    [LogoutTime] DATETIME2,
    [IPAddress] NVARCHAR(45),
    [DeviceName] NVARCHAR(255),
    [Browser] NVARCHAR(255),
    [OS] NVARCHAR(255),
    [IsSuccess] BIT NOT NULL,
    [FailureReason] NVARCHAR(MAX),
    [SessionDurationSeconds] INT,  -- Calculated at logout

    CONSTRAINT FK_LoginHist_User FOREIGN KEY (UserID)
        REFERENCES [dbo].[Users](UserID) ON DELETE SET NULL
);

CREATE NONCLUSTERED INDEX [IX_LoginHist_User] ON [dbo].[LoginHistory]([UserID]);
CREATE NONCLUSTERED INDEX [IX_LoginHist_Time] ON [dbo].[LoginHistory]([LoginTime] DESC);

-- =====================================================
-- PASSWORD_HISTORY TABLE (Prevent password reuse)
-- =====================================================

CREATE TABLE [dbo].[PasswordHistory] (
    [PasswordHistoryID] INT PRIMARY KEY IDENTITY(1,1),
    [UserID] INT NOT NULL,
    [PasswordHash] NVARCHAR(255) NOT NULL,
    [ChangedAt] DATETIME2 DEFAULT GETDATE() NOT NULL,

    CONSTRAINT FK_PwdHist_User FOREIGN KEY (UserID)
        REFERENCES [dbo].[Users](UserID) ON DELETE CASCADE,
    CONSTRAINT UQ_UserPassword UNIQUE (UserID, PasswordHash)
);

CREATE NONCLUSTERED INDEX [IX_PwdHist_User] ON [dbo].[PasswordHistory]([UserID], [ChangedAt] DESC);

-- =====================================================
-- STORED PROCEDURES FOR AUTHENTICATION
-- =====================================================

-- SP: Check if user has permission
CREATE PROCEDURE [dbo].[sp_CheckUserPermission]
    @UserID INT,
    @Resource NVARCHAR(100),
    @Action NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS HasPermission
    FROM [dbo].[RolePermissions] rp
    INNER JOIN [dbo].[Permissions] p ON rp.PermissionID = p.PermissionID
    INNER JOIN [dbo].[Users] u ON rp.RoleID = u.RoleID
    WHERE u.UserID = @UserID
        AND p.Resource = @Resource
        AND p.Action = @Action
        AND u.IsActive = 1
        AND p.IsActive = 1;
END;

-- SP: Get user permissions
CREATE PROCEDURE [dbo].[sp_GetUserPermissions]
    @UserID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        p.Resource,
        p.Action,
        CONCAT(p.Resource, '_', p.Action) AS PermissionKey
    FROM [dbo].[RolePermissions] rp
    INNER JOIN [dbo].[Permissions] p ON rp.PermissionID = p.PermissionID
    INNER JOIN [dbo].[Users] u ON rp.RoleID = u.RoleID
    WHERE u.UserID = @UserID
        AND u.IsActive = 1
        AND p.IsActive = 1
    ORDER BY p.Resource, p.Action;
END;

-- SP: Log login attempt
CREATE PROCEDURE [dbo].[sp_LogLoginAttempt]
    @UserID INT = NULL,
    @Username NVARCHAR(100),
    @IsSuccess BIT,
    @IPAddress NVARCHAR(45),
    @DeviceName NVARCHAR(255) = NULL,
    @FailureReason NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[AuditLog] (
        [UserID], [ActionType], [IPAddress], [DeviceName],
        [IsSuccess], [FailureReason], [Timestamp]
    )
    VALUES (
        @UserID,
        'Login',
        @IPAddress,
        @DeviceName,
        @IsSuccess,
        @FailureReason,
        GETDATE()
    );

    INSERT INTO [dbo].[LoginHistory] (
        [UserID], [Username], [IPAddress], [DeviceName],
        [IsSuccess], [FailureReason]
    )
    VALUES (
        @UserID,
        @Username,
        @IPAddress,
        @DeviceName,
        @IsSuccess,
        @FailureReason
    );
END;

-- SP: Lock user account
CREATE PROCEDURE [dbo].[sp_LockUserAccount]
    @UserID INT,
    @LockedUntilMinutes INT = 30
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[Users]
    SET [LockedUntil] = DATEADD(MINUTE, @LockedUntilMinutes, GETDATE()),
        [UpdatedAt] = GETDATE()
    WHERE [UserID] = @UserID;
END;

-- SP: Unlock user account
CREATE PROCEDURE [dbo].[sp_UnlockUserAccount]
    @UserID INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[Users]
    SET [LockedUntil] = NULL,
        [FailedLoginAttempts] = 0,
        [UpdatedAt] = GETDATE()
    WHERE [UserID] = @UserID;
END;

-- =====================================================
-- VIEWS FOR AUTHENTICATION
-- =====================================================

-- View: User with role and status
CREATE VIEW [dbo].[vw_UserWithRole] AS
SELECT
    u.[UserID],
    u.[Username],
    u.[Email],
    u.[IsActive],
    u.[IsMFAEnabled],
    u.[LastLoginAt],
    u.[LockedUntil],
    u.[DepartmentID],
    r.[RoleID],
    r.[Name] AS RoleName
FROM [dbo].[Users] u
INNER JOIN [dbo].[Roles] r ON u.RoleID = r.RoleID;

-- View: Recent login attempts
CREATE VIEW [dbo].[vw_RecentLogins] AS
SELECT TOP 1000
    lh.[LoginID],
    lh.[Username],
    lh.[LoginTime],
    lh.[IsSuccess],
    lh.[IPAddress],
    lh.[FailureReason],
    lh.[SessionDurationSeconds]
FROM [dbo].[LoginHistory] lh
ORDER BY lh.[LoginTime] DESC;

-- =====================================================
-- END OF AUTHENTICATION MODULE SCHEMA
-- =====================================================
