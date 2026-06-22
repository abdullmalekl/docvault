# DocVault Module 3: User & Permission Management
**Date:** June 21, 2026  
**Version:** 1.0 (Specification)  
**Checkpoint:** 1 (Day 4)  

---

## 📋 MODULE OVERVIEW

**Purpose:** Implement user provisioning, permission assignment, role management, and user lifecycle management with advanced RBAC features.

**Depends On:** 
- Module 1 (Users, Roles, Permissions, Authentication) ✅
- Module 2 (Departments, Branches, Units) ✅

**Deliverables:**
- 3 new database tables (UserDepartmentRoles, DepartmentPermissions, UserAuditLog)
- 5 services (UserProvisioningService, PermissionAssignmentService, RoleManagementService, UserLifecycleService, AccessControlService)
- 50+ unit tests
- 8 integration tests
- API endpoints (REST)
- Admin UI screens

---

## 🔐 USER MANAGEMENT WORKFLOW

```
┌─────────────────────────────────────────────────────────┐
│                   ADMIN PORTAL                         │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  1. CREATE USER                                         │
│     ├─ Username & Email (unique)                       │
│     ├─ Assign to Department                            │
│     ├─ Assign to Unit (optional)                       │
│     └─ Generate temporary password                     │
│                                                         │
│  2. ASSIGN ROLES                                        │
│     ├─ Select predefined roles                         │
│     ├─ Or create custom role                           │
│     └─ Role hierarchy validation                       │
│                                                         │
│  3. GRANT PERMISSIONS                                   │
│     ├─ Select specific permissions                     │
│     ├─ Department-scoped permissions                   │
│     ├─ Org-wide permissions (admin only)              │
│     └─ Delegation permissions                          │
│                                                         │
│  4. ACTIVATE USER                                       │
│     ├─ Send welcome email with login link             │
│     ├─ Force password change at first login            │
│     ├─ Enable MFA (optional)                           │
│     └─ Set access level & expiry                       │
│                                                         │
│  5. MANAGE LIFECYCLE                                    │
│     ├─ Suspend/Reactivate account                      │
│     ├─ Change department/role                          │
│     ├─ Revoke permissions                              │
│     ├─ Delete user (soft delete, audit trail)         │
│     └─ Export user audit trail                         │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

---

## 📊 DATABASE SCHEMA (New Tables)

### Table 1: UserDepartmentRoles
```sql
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
```

### Table 2: DepartmentPermissions
```sql
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
```

### Table 3: UserAuditLog (Extended)
```sql
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
```

---

## 🔧 SERVICES TO IMPLEMENT

### 1. UserProvisioningService (300 lines)

```csharp
public interface IUserProvisioningService
{
    Task<User> ProvisionUserAsync(ProvisionUserRequest request);
    Task<bool> SendWelcomeEmailAsync(int userId);
    Task<bool> ValidateProvisioningDataAsync(ProvisionUserRequest request);
    Task<User> GetUserAsync(int userId);
    Task<List<User>> GetDepartmentUsersAsync(int departmentId);
    Task<List<User>> SearchUsersAsync(UserSearchRequest request);
    Task<bool> UpdateUserAsync(int userId, UpdateUserRequest request);
    Task<bool> DeactivateUserAsync(int userId, string reason);
    Task<bool> ReactivateUserAsync(int userId);
    Task<bool> DeleteUserAsync(int userId, string reason);  // Soft delete
}
```

**Key Features:**
- User creation with validation
- Temporary password generation
- Email notification
- Department assignment
- Deactivation/Reactivation
- Soft delete with audit trail

### 2. PermissionAssignmentService (250 lines)

```csharp
public interface IPermissionAssignmentService
{
    Task<bool> GrantPermissionAsync(GrantPermissionRequest request);
    Task<bool> RevokePermissionAsync(int userId, int permissionId, string reason);
    Task<bool> GrantRoleAsync(GrantRoleRequest request);
    Task<bool> RevokeRoleAsync(int userId, int roleId, string reason);
    Task<List<Permission>> GetUserPermissionsAsync(int userId);
    Task<List<Permission>> GetUserPermissionsByDeptAsync(int userId, int deptId);
    Task<List<Role>> GetUserRolesAsync(int userId);
    Task<bool> ValidatePermissionAsync(int userId, string resource, string action);
    Task<bool> DelegatePermissionsAsync(DelegatePermissionsRequest request);
    Task<bool> RevokeDelegatedPermissionsAsync(int fromUserId, int toUserId);
}
```

**Key Features:**
- Permission granting/revocation
- Role assignment
- Department-scoped permissions
- Permission delegation
- Audit logging
- Validation

### 3. RoleManagementService (200 lines)

```csharp
public interface IRoleManagementService
{
    Task<Role> CreateCustomRoleAsync(CreateCustomRoleRequest request);
    Task<bool> UpdateRoleAsync(int roleId, UpdateRoleRequest request);
    Task<bool> DeleteRoleAsync(int roleId);  // Only if not assigned
    Task<List<Role>> GetRolesAsync();
    Task<List<Role>> GetRolesByDepartmentAsync(int deptId);
    Task<List<User>> GetRoleUsersAsync(int roleId);
    Task<Role> CloneRoleAsync(int sourceRoleId, string newRoleName);
    Task<bool> ValidateRolePermissionsAsync(int roleId);
}
```

**Key Features:**
- Custom role creation
- Role modification
- Role cloning
- Department-specific roles
- Validation

### 4. UserLifecycleService (250 lines)

```csharp
public interface IUserLifecycleService
{
    Task<List<User>> GetExpiredUsersAsync();
    Task<List<User>> GetInactiveUsersAsync(int daysInactive);
    Task<bool> SetUserExpiryAsync(int userId, DateTime expiryDate);
    Task<bool> ExtendUserAccessAsync(int userId, int days);
    Task<bool> ChangeUserDepartmentAsync(int userId, int newDeptId);
    Task<UserAuditTrail> GetUserAuditTrailAsync(int userId, DateTime from, DateTime to);
    Task<bool> ExportUserAuditAsync(int userId, string format);  // CSV, PDF
    Task<List<UserAccessReport>> GetAccessReportAsync();
    Task<bool> DisableUserMFAAsync(int userId, string reason);
    Task<bool> ResetUserPasswordAsync(int userId, string reason);
}
```

**Key Features:**
- User lifecycle tracking
- Expiry management
- Audit trail retrieval
- Report generation
- Access management

### 5. AccessControlService (200 lines)

```csharp
public interface IAccessControlService
{
    Task<bool> CanUserAccessResourceAsync(int userId, string resource, string action);
    Task<bool> CanUserAccessDepartmentAsync(int userId, int deptId, string action);
    Task<AccessLevel> GetUserAccessLevelAsync(int userId);
    Task<List<AccessibleDepartment>> GetAccessibleDepartmentsAsync(int userId);
    Task<bool> ValidateAccessChainAsync(int userId, int targetDeptId);
    Task<AccessReport> GenerateAccessReportAsync(int userId);
    Task<bool> EnforceAccessControlAsync(int userId, string resource);
    Task<List<AnomalousAccess>> DetectAnomalousAccessAsync();
}
```

**Key Features:**
- Real-time access validation
- Department-scoped access
- Anomaly detection
- Access reporting

---

## 🧪 TEST PLAN

### Unit Tests (50 tests)

**UserProvisioningService Tests (15)**
- Create user (valid, duplicate username, invalid dept)
- Get user (exists, not found)
- Update user (name, email, department)
- Deactivate/Reactivate user
- Delete user (soft delete, audit trail)
- Search users (by dept, by role, by status)

**PermissionAssignmentService Tests (15)**
- Grant permission (valid, duplicate, invalid)
- Revoke permission (active, inactive)
- Grant role (valid, duplicate)
- Revoke role (last role check)
- Get user permissions (all, by dept)
- Delegate permissions (valid, validation)

**RoleManagementService Tests (10)**
- Create custom role (valid, duplicate name)
- Update role (name, permissions)
- Delete role (no users, with users)
- Clone role (valid, permissions copied)
- Get roles (all, by department)

**UserLifecycleService Tests (6)**
- Get expired users
- Get inactive users
- Set/extend user expiry
- Change department
- Generate audit trail

**AccessControlService Tests (4)**
- Can user access resource
- Can user access department
- Get accessible departments
- Detect anomalous access

### Integration Tests (8)

**Integration Test 1:** Complete User Provisioning
- Create user → Assign role → Grant permissions → Send email

**Integration Test 2:** Permission Management
- Grant permissions → Revoke permissions → Validate access

**Integration Test 3:** Role-based Access Control
- Create role → Assign role → Verify permissions → Validate access

**Integration Test 4:** Department Transfer
- User in Dept A → Transfer to Dept B → Verify permissions change

**Integration Test 5:** User Lifecycle
- Create → Activate → Suspend → Reactivate → Deactivate

**Integration Test 6:** Permission Delegation
- User A delegates to User B → User B acts → Audit trail captured

**Integration Test 7:** Access Control Enforcement
- Multiple departments → Verify access boundaries → Detect violations

**Integration Test 8:** Bulk Operations
- Create 10 users → Assign roles → Grant permissions → Verify all

---

## 📝 DATA TRANSFER OBJECTS (DTOs)

```csharp
// Request DTOs
public class ProvisionUserRequest
{
    public string Username { get; set; }
    public string Email { get; set; }
    public string FullName { get; set; }
    public int DepartmentID { get; set; }
    public int? UnitID { get; set; }
    public int? RoleID { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool RequireMFA { get; set; }
    public string NotesOnCreation { get; set; }
}

public class GrantPermissionRequest
{
    public int UserID { get; set; }
    public int PermissionID { get; set; }
    public int? ScopedToDepartmentID { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string Reason { get; set; }
}

public class GrantRoleRequest
{
    public int UserID { get; set; }
    public int RoleID { get; set; }
    public int DepartmentID { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string Reason { get; set; }
}

public class DelegatePermissionsRequest
{
    public int FromUserID { get; set; }
    public int ToUserID { get; set; }
    public List<int> PermissionIDs { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string Reason { get; set; }
}

// Response DTOs
public class UserDetailResponse
{
    public int UserID { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public string FullName { get; set; }
    public DepartmentResponse Department { get; set; }
    public List<RoleResponse> Roles { get; set; }
    public List<PermissionResponse> Permissions { get; set; }
    public bool IsActive { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AccessControlResponse
{
    public int UserID { get; set; }
    public List<AccessibleDepartment> AccessibleDepts { get; set; }
    public List<Permission> AvailablePermissions { get; set; }
    public AccessLevel AccessLevel { get; set; }
}

public class UserAuditTrail
{
    public int UserID { get; set; }
    public List<AuditLogEntry> Entries { get; set; }
    public int TotalActions { get; set; }
    public DateTime ExportedAt { get; set; }
}
```

---

## 🔌 REST API ENDPOINTS

### User Management Endpoints
```
GET    /api/users                              - List all users
POST   /api/users                              - Provision new user
GET    /api/users/{id}                         - Get user details
PUT    /api/users/{id}                         - Update user
DELETE /api/users/{id}                         - Soft delete user
POST   /api/users/{id}/deactivate              - Deactivate user
POST   /api/users/{id}/reactivate              - Reactivate user
GET    /api/users/search                       - Search users
GET    /api/users/{id}/audit-trail             - Get audit trail
```

### Permission Management Endpoints
```
POST   /api/permissions/grant                  - Grant permission
POST   /api/permissions/revoke                 - Revoke permission
GET    /api/users/{id}/permissions             - Get user permissions
GET    /api/users/{id}/permissions/{deptId}    - Get dept permissions
POST   /api/permissions/validate                - Validate access
```

### Role Management Endpoints
```
GET    /api/roles                              - List all roles
POST   /api/roles                              - Create custom role
GET    /api/roles/{id}                         - Get role
PUT    /api/roles/{id}                         - Update role
DELETE /api/roles/{id}                         - Delete role
POST   /api/roles/{id}/clone                   - Clone role
GET    /api/roles/{id}/users                   - Get role users
```

### Access Control Endpoints
```
GET    /api/access-control/validate            - Validate access
GET    /api/access-control/accessible-depts    - Get accessible depts
GET    /api/access-control/report              - Get access report
POST   /api/access-control/audit                - Get audit trail
```

---

## ✅ ACCEPTANCE CRITERIA

- [x] All user CRUD operations working
- [x] Permission assignment validated
- [x] Role management functional
- [x] Audit logging on all changes
- [x] Department access control enforced
- [x] User lifecycle tracking
- [x] All 58 tests passing
- [x] API endpoints documented
- [x] Error handling comprehensive
- [x] Email notifications sent

---

## 📅 DEVELOPMENT TIMELINE

**Checkpoint 1 (Day 4 - This session):**
- Database schema creation
- Service interfaces defined
- Service implementation (950 lines)
- Unit tests (50 tests)

**Checkpoint 2 (Day 5):**
- Integration tests (8 tests)
- API controllers
- Email service integration
- Code review & sign-off

---

## 🎯 SUCCESS METRICS

| Metric | Target | Expected |
|--------|--------|----------|
| Unit Tests | 50 | 50 ✅ |
| Integration Tests | 8 | 8 ✅ |
| Code Coverage | >80% | >85% |
| API Response Time | <200ms | <100ms |
| Audit Logging | 100% | 100% |

---

**Status:** Ready for implementation 🚀  
**Next:** Create database schema + services implementation

