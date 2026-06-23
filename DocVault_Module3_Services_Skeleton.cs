// =====================================================
// DocVault Module 3: User & Permission Management
// Services Skeleton - Interfaces & DTOs
// Date: June 21, 2026
// =====================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DocVault.Core.Models;

namespace DocVault.Core.UserManagement
{
    // =====================================================
    // 1. USER PROVISIONING SERVICE
    // =====================================================

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

    public class UserProvisioningService : IUserProvisioningService
    {
        // TODO: Implement in Checkpoint 1
        public async Task<User> ProvisionUserAsync(ProvisionUserRequest request) => throw new NotImplementedException();
        public async Task<bool> SendWelcomeEmailAsync(int userId) => throw new NotImplementedException();
        public async Task<bool> ValidateProvisioningDataAsync(ProvisionUserRequest request) => throw new NotImplementedException();
        public async Task<User> GetUserAsync(int userId) => throw new NotImplementedException();
        public async Task<List<User>> GetDepartmentUsersAsync(int departmentId) => throw new NotImplementedException();
        public async Task<List<User>> SearchUsersAsync(UserSearchRequest request) => throw new NotImplementedException();
        public async Task<bool> UpdateUserAsync(int userId, UpdateUserRequest request) => throw new NotImplementedException();
        public async Task<bool> DeactivateUserAsync(int userId, string reason) => throw new NotImplementedException();
        public async Task<bool> ReactivateUserAsync(int userId) => throw new NotImplementedException();
        public async Task<bool> DeleteUserAsync(int userId, string reason) => throw new NotImplementedException();
    }

    // =====================================================
    // 2. PERMISSION ASSIGNMENT SERVICE
    // =====================================================

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

    public class PermissionAssignmentService : IPermissionAssignmentService
    {
        // TODO: Implement in Checkpoint 1
        public async Task<bool> GrantPermissionAsync(GrantPermissionRequest request) => throw new NotImplementedException();
        public async Task<bool> RevokePermissionAsync(int userId, int permissionId, string reason) => throw new NotImplementedException();
        public async Task<bool> GrantRoleAsync(GrantRoleRequest request) => throw new NotImplementedException();
        public async Task<bool> RevokeRoleAsync(int userId, int roleId, string reason) => throw new NotImplementedException();
        public async Task<List<Permission>> GetUserPermissionsAsync(int userId) => throw new NotImplementedException();
        public async Task<List<Permission>> GetUserPermissionsByDeptAsync(int userId, int deptId) => throw new NotImplementedException();
        public async Task<List<Role>> GetUserRolesAsync(int userId) => throw new NotImplementedException();
        public async Task<bool> ValidatePermissionAsync(int userId, string resource, string action) => throw new NotImplementedException();
        public async Task<bool> DelegatePermissionsAsync(DelegatePermissionsRequest request) => throw new NotImplementedException();
        public async Task<bool> RevokeDelegatedPermissionsAsync(int fromUserId, int toUserId) => throw new NotImplementedException();
    }

    // =====================================================
    // 3. ROLE MANAGEMENT SERVICE
    // =====================================================

    public interface IRoleManagementService
    {
        Task<Role> CreateCustomRoleAsync(CreateCustomRoleRequest request);
        Task<bool> UpdateRoleAsync(int roleId, UpdateRoleRequest request);
        Task<bool> DeleteRoleAsync(int roleId);
        Task<List<Role>> GetRolesAsync();
        Task<List<Role>> GetRolesByDepartmentAsync(int deptId);
        Task<List<User>> GetRoleUsersAsync(int roleId);
        Task<Role> CloneRoleAsync(int sourceRoleId, string newRoleName);
        Task<bool> ValidateRolePermissionsAsync(int roleId);
    }

    public class RoleManagementService : IRoleManagementService
    {
        // TODO: Implement in Checkpoint 1
        public async Task<Role> CreateCustomRoleAsync(CreateCustomRoleRequest request) => throw new NotImplementedException();
        public async Task<bool> UpdateRoleAsync(int roleId, UpdateRoleRequest request) => throw new NotImplementedException();
        public async Task<bool> DeleteRoleAsync(int roleId) => throw new NotImplementedException();
        public async Task<List<Role>> GetRolesAsync() => throw new NotImplementedException();
        public async Task<List<Role>> GetRolesByDepartmentAsync(int deptId) => throw new NotImplementedException();
        public async Task<List<User>> GetRoleUsersAsync(int roleId) => throw new NotImplementedException();
        public async Task<Role> CloneRoleAsync(int sourceRoleId, string newRoleName) => throw new NotImplementedException();
        public async Task<bool> ValidateRolePermissionsAsync(int roleId) => throw new NotImplementedException();
    }

    // =====================================================
    // 4. USER LIFECYCLE SERVICE
    // =====================================================

    public interface IUserLifecycleService
    {
        Task<List<User>> GetExpiredUsersAsync();
        Task<List<User>> GetInactiveUsersAsync(int daysInactive);
        Task<bool> SetUserExpiryAsync(int userId, DateTime expiryDate);
        Task<bool> ExtendUserAccessAsync(int userId, int days);
        Task<bool> ChangeUserDepartmentAsync(int userId, int newDeptId);
        Task<UserAuditTrail> GetUserAuditTrailAsync(int userId, DateTime from, DateTime to);
        Task<bool> ExportUserAuditAsync(int userId, string format);
        Task<List<UserAccessReport>> GetAccessReportAsync();
        Task<bool> DisableUserMFAAsync(int userId, string reason);
        Task<bool> ResetUserPasswordAsync(int userId, string reason);
    }

    public class UserLifecycleService : IUserLifecycleService
    {
        // TODO: Implement in Checkpoint 1
        public async Task<List<User>> GetExpiredUsersAsync() => throw new NotImplementedException();
        public async Task<List<User>> GetInactiveUsersAsync(int daysInactive) => throw new NotImplementedException();
        public async Task<bool> SetUserExpiryAsync(int userId, DateTime expiryDate) => throw new NotImplementedException();
        public async Task<bool> ExtendUserAccessAsync(int userId, int days) => throw new NotImplementedException();
        public async Task<bool> ChangeUserDepartmentAsync(int userId, int newDeptId) => throw new NotImplementedException();
        public async Task<UserAuditTrail> GetUserAuditTrailAsync(int userId, DateTime from, DateTime to) => throw new NotImplementedException();
        public async Task<bool> ExportUserAuditAsync(int userId, string format) => throw new NotImplementedException();
        public async Task<List<UserAccessReport>> GetAccessReportAsync() => throw new NotImplementedException();
        public async Task<bool> DisableUserMFAAsync(int userId, string reason) => throw new NotImplementedException();
        public async Task<bool> ResetUserPasswordAsync(int userId, string reason) => throw new NotImplementedException();
    }

    // =====================================================
    // 5. ACCESS CONTROL SERVICE
    // =====================================================

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

    public class AccessControlService : IAccessControlService
    {
        // TODO: Implement in Checkpoint 1
        public async Task<bool> CanUserAccessResourceAsync(int userId, string resource, string action) => throw new NotImplementedException();
        public async Task<bool> CanUserAccessDepartmentAsync(int userId, int deptId, string action) => throw new NotImplementedException();
        public async Task<AccessLevel> GetUserAccessLevelAsync(int userId) => throw new NotImplementedException();
        public async Task<List<AccessibleDepartment>> GetAccessibleDepartmentsAsync(int userId) => throw new NotImplementedException();
        public async Task<bool> ValidateAccessChainAsync(int userId, int targetDeptId) => throw new NotImplementedException();
        public async Task<AccessReport> GenerateAccessReportAsync(int userId) => throw new NotImplementedException();
        public async Task<bool> EnforceAccessControlAsync(int userId, string resource) => throw new NotImplementedException();
        public async Task<List<AnomalousAccess>> DetectAnomalousAccessAsync() => throw new NotImplementedException();
    }

    // =====================================================
    // DTOs & REQUEST OBJECTS
    // =====================================================

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

    public class UpdateUserRequest
    {
        public string Email { get; set; }
        public string FullName { get; set; }
        public int? DepartmentID { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }

    public class UserSearchRequest
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public int? DepartmentID { get; set; }
        public int? RoleID { get; set; }
        public bool? IsActive { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
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

    public class CreateCustomRoleRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<int> PermissionIDs { get; set; }
        public int? DepartmentID { get; set; }  // Dept-specific role
    }

    public class UpdateRoleRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<int> PermissionIDs { get; set; }
    }

    // =====================================================
    // RESPONSE OBJECTS
    // =====================================================

    public class UserDetailResponse
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public List<UserRoleResponse> Roles { get; set; }
        public List<Permission> Permissions { get; set; }
        public bool IsActive { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UserRoleResponse
    {
        public int DepartmentID { get; set; }
        public string DepartmentName { get; set; }
        public int RoleID { get; set; }
        public string RoleName { get; set; }
        public bool IsPrimary { get; set; }
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

    public class UserAccessReport
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public int DepartmentCount { get; set; }
        public int RoleCount { get; set; }
        public int PermissionCount { get; set; }
        public DateTime? LastLogin { get; set; }
    }

    // =====================================================
    // MODEL CLASSES
    // =====================================================

    // User, Role, Permission are defined in DocVault.Core.Models (DocVault.Models.cs)
    // Import with: using DocVault.Core.Models;

    public class AccessibleDepartment
    {
        public int DepartmentID { get; set; }
        public string DepartmentName { get; set; }
        public string AccessLevel { get; set; }
    }

    public enum AccessLevel
    {
        None = 0,
        View = 1,
        Edit = 2,
        Manage = 3,
        Admin = 4
    }

    public class AccessReport
    {
        public int UserID { get; set; }
        public List<AccessibleDepartment> AccessibleDepts { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    public class AnomalousAccess
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public int DepartmentID { get; set; }
        public string ActionType { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class AuditLogEntry
    {
        public long AuditID { get; set; }
        public int UserID { get; set; }
        public string ActionType { get; set; }
        public string NewValue { get; set; }
        public string Reason { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
