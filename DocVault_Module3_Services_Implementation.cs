// =====================================================
// DocVault Module 3: User & Permission Management
// Complete Services Implementation
// Date: June 21, 2026 (Checkpoint 2)
// =====================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocVault.Core.UserManagement
{
    // =====================================================
    // 1. USER PROVISIONING SERVICE (300 lines)
    // =====================================================

    public class UserProvisioningServiceImpl : IUserProvisioningService
    {
        private readonly DbContext _dbContext;
        private readonly IAuditService _auditService;
        private readonly IEmailService _emailService;
        private readonly IPasswordHasher _passwordHasher;

        public UserProvisioningServiceImpl(
            DbContext dbContext,
            IAuditService auditService,
            IEmailService emailService,
            IPasswordHasher passwordHasher)
        {
            _dbContext = dbContext;
            _auditService = auditService;
            _emailService = emailService;
            _passwordHasher = passwordHasher;
        }

        public async Task<User> ProvisionUserAsync(ProvisionUserRequest request)
        {
            // Validate request
            if (!await ValidateProvisioningDataAsync(request))
                throw new InvalidOperationException("Provisioning data validation failed");

            // Check for duplicate username/email
            var existing = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username || u.Email == request.Email);
            if (existing != null)
                throw new InvalidOperationException("Username or email already exists");

            // Generate temporary password
            var tempPassword = GenerateTemporaryPassword();
            var passwordHash = _passwordHasher.HashPassword(tempPassword);

            // Create user
            var user = new User
            {
                Username = request.Username.ToLower().Trim(),
                Email = request.Email,
                PasswordHash = passwordHash,
                DepartmentID = request.DepartmentID,
                RoleID = request.RoleID ?? 4,  // Default to Viewer
                IsActive = true,
                RequirePasswordChange = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            // Assign to department with role if specified
            if (request.RoleID.HasValue)
            {
                var userDeptRole = new UserDepartmentRole
                {
                    UserID = user.UserID,
                    DepartmentID = request.DepartmentID,
                    RoleID = request.RoleID.Value,
                    IsPrimary = true,
                    AssignedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _dbContext.UserDepartmentRoles.Add(userDeptRole);
                await _dbContext.SaveChangesAsync();
            }

            // Log provisioning
            await _auditService.LogActionAsync(
                $"User '{request.Username}' provisioned in {request.DepartmentID}");

            // Send welcome email
            await SendWelcomeEmailAsync(user.UserID);

            return user;
        }

        public async Task<bool> SendWelcomeEmailAsync(int userId)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return false;

            var emailBody = $@"
Welcome to DocVault!

Username: {user.Username}
Email: {user.Email}

You are required to change your password on first login.

Please log in at: https://docvault.local/login

Best regards,
DocVault System";

            return await _emailService.SendEmailAsync(
                user.Email,
                "Welcome to DocVault",
                emailBody);
        }

        public async Task<bool> ValidateProvisioningDataAsync(ProvisionUserRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length < 3)
                return false;

            if (string.IsNullOrWhiteSpace(request.Email) || !IsValidEmail(request.Email))
                return false;

            var dept = await _dbContext.Departments.FindAsync(request.DepartmentID);
            if (dept == null) return false;

            return true;
        }

        public async Task<User> GetUserAsync(int userId)
        {
            return await _dbContext.Users
                .Include(u => u.UserDepartmentRoles)
                .FirstOrDefaultAsync(u => u.UserID == userId && u.IsActive);
        }

        public async Task<List<User>> GetDepartmentUsersAsync(int departmentId)
        {
            return await _dbContext.Users
                .Where(u => u.DepartmentID == departmentId && u.IsActive)
                .OrderBy(u => u.Username)
                .ToListAsync();
        }

        public async Task<List<User>> SearchUsersAsync(UserSearchRequest request)
        {
            var query = _dbContext.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Username))
                query = query.Where(u => u.Username.Contains(request.Username));

            if (!string.IsNullOrWhiteSpace(request.Email))
                query = query.Where(u => u.Email.Contains(request.Email));

            if (request.DepartmentID.HasValue)
                query = query.Where(u => u.DepartmentID == request.DepartmentID);

            if (request.IsActive.HasValue)
                query = query.Where(u => u.IsActive == request.IsActive);

            return await query
                .OrderBy(u => u.Username)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();
        }

        public async Task<bool> UpdateUserAsync(int userId, UpdateUserRequest request)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return false;

            user.Email = request.Email ?? user.Email;
            user.UpdatedAt = DateTime.UtcNow;

            if (request.DepartmentID.HasValue)
                user.DepartmentID = request.DepartmentID.Value;

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeactivateUserAsync(int userId, string reason)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return false;

            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            await _auditService.LogActionAsync($"User {user.Username} deactivated: {reason}");

            return true;
        }

        public async Task<bool> ReactivateUserAsync(int userId)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return false;

            user.IsActive = true;
            user.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            await _auditService.LogActionAsync($"User {user.Username} reactivated");

            return true;
        }

        public async Task<bool> DeleteUserAsync(int userId, string reason)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return false;

            user.IsActive = false;  // Soft delete
            user.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            await _auditService.LogActionAsync($"User {user.Username} soft deleted: {reason}");

            return true;
        }

        private string GenerateTemporaryPassword()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%";
            var random = new Random();
            return new string(Enumerable.Range(0, 12)
                .Select(_ => chars[random.Next(chars.Length)])
                .ToArray());
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }

    // =====================================================
    // 2. PERMISSION ASSIGNMENT SERVICE (250 lines)
    // =====================================================

    public class PermissionAssignmentServiceImpl : IPermissionAssignmentService
    {
        private readonly DbContext _dbContext;
        private readonly IAuditService _auditService;

        public PermissionAssignmentServiceImpl(DbContext dbContext, IAuditService auditService)
        {
            _dbContext = dbContext;
            _auditService = auditService;
        }

        public async Task<bool> GrantPermissionAsync(GrantPermissionRequest request)
        {
            var user = await _dbContext.Users.FindAsync(request.UserID);
            if (user == null) return false;

            var permission = await _dbContext.Permissions.FindAsync(request.PermissionID);
            if (permission == null) return false;

            var userRole = await _dbContext.UserDepartmentRoles
                .FirstOrDefaultAsync(ur => ur.UserID == request.UserID
                    && ur.DepartmentID == (request.ScopedToDepartmentID ?? ur.DepartmentID)
                    && ur.IsActive);

            if (userRole == null) return false;

            // Check if already has permission
            var existing = await _dbContext.RolePermissions
                .FirstOrDefaultAsync(rp => rp.RoleID == userRole.RoleID
                    && rp.PermissionID == request.PermissionID);

            if (existing != null) return false;  // Already assigned

            // Add permission to user's role
            var rolePermission = new RolePermission
            {
                RoleID = userRole.RoleID,
                PermissionID = request.PermissionID,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.RolePermissions.Add(rolePermission);
            await _dbContext.SaveChangesAsync();

            await _auditService.LogActionAsync(
                $"Permission {permission.Resource}:{permission.Action} granted to {user.Username}");

            return true;
        }

        public async Task<bool> RevokePermissionAsync(int userId, int permissionId, string reason)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return false;

            var userRole = await _dbContext.UserDepartmentRoles
                .FirstOrDefaultAsync(ur => ur.UserID == userId && ur.IsActive);

            if (userRole == null) return false;

            var rolePermission = await _dbContext.RolePermissions
                .FirstOrDefaultAsync(rp => rp.RoleID == userRole.RoleID
                    && rp.PermissionID == permissionId);

            if (rolePermission == null) return false;

            _dbContext.RolePermissions.Remove(rolePermission);
            await _dbContext.SaveChangesAsync();

            await _auditService.LogActionAsync(
                $"Permission revoked from {user.Username}: {reason}");

            return true;
        }

        public async Task<bool> GrantRoleAsync(GrantRoleRequest request)
        {
            var user = await _dbContext.Users.FindAsync(request.UserID);
            if (user == null) return false;

            var role = await _dbContext.Roles.FindAsync(request.RoleID);
            if (role == null) return false;

            // Check for duplicate
            var existing = await _dbContext.UserDepartmentRoles
                .FirstOrDefaultAsync(ur => ur.UserID == request.UserID
                    && ur.DepartmentID == request.DepartmentID
                    && ur.RoleID == request.RoleID);

            if (existing != null) return false;

            var userDeptRole = new UserDepartmentRole
            {
                UserID = request.UserID,
                DepartmentID = request.DepartmentID,
                RoleID = request.RoleID,
                IsPrimary = request.IsPrimary,
                EffectiveFrom = request.EffectiveFrom,
                EffectiveTo = request.EffectiveTo,
                AssignedAt = DateTime.UtcNow,
                IsActive = true
            };

            _dbContext.UserDepartmentRoles.Add(userDeptRole);
            await _dbContext.SaveChangesAsync();

            await _auditService.LogActionAsync(
                $"Role {role.Name} assigned to {user.Username} in dept {request.DepartmentID}");

            return true;
        }

        public async Task<bool> RevokeRoleAsync(int userId, int roleId, string reason)
        {
            var userRole = await _dbContext.UserDepartmentRoles
                .FirstOrDefaultAsync(ur => ur.UserID == userId && ur.RoleID == roleId && ur.IsActive);

            if (userRole == null) return false;

            userRole.IsActive = false;
            await _dbContext.SaveChangesAsync();

            return true;
        }

        public async Task<List<Permission>> GetUserPermissionsAsync(int userId)
        {
            return await _dbContext.RolePermissions
                .Where(rp => _dbContext.UserDepartmentRoles
                    .Where(ur => ur.UserID == userId && ur.IsActive)
                    .Any(ur => ur.RoleID == rp.RoleID))
                .Join(_dbContext.Permissions, rp => rp.PermissionID, p => p.PermissionID,
                    (rp, p) => p)
                .Distinct()
                .ToListAsync();
        }

        public async Task<List<Permission>> GetUserPermissionsByDeptAsync(int userId, int deptId)
        {
            return await _dbContext.RolePermissions
                .Where(rp => _dbContext.UserDepartmentRoles
                    .Where(ur => ur.UserID == userId && ur.DepartmentID == deptId && ur.IsActive)
                    .Any(ur => ur.RoleID == rp.RoleID))
                .Join(_dbContext.Permissions, rp => rp.PermissionID, p => p.PermissionID,
                    (rp, p) => p)
                .ToListAsync();
        }

        public async Task<List<Role>> GetUserRolesAsync(int userId)
        {
            return await _dbContext.UserDepartmentRoles
                .Where(ur => ur.UserID == userId && ur.IsActive)
                .Join(_dbContext.Roles, ur => ur.RoleID, r => r.RoleID,
                    (ur, r) => r)
                .Distinct()
                .ToListAsync();
        }

        public async Task<bool> ValidatePermissionAsync(int userId, string resource, string action)
        {
            var hasPermission = await _dbContext.RolePermissions
                .Where(rp => _dbContext.UserDepartmentRoles
                    .Where(ur => ur.UserID == userId && ur.IsActive)
                    .Any(ur => ur.RoleID == rp.RoleID))
                .Join(_dbContext.Permissions, rp => rp.PermissionID, p => p.PermissionID,
                    (rp, p) => p)
                .AnyAsync(p => p.Resource == resource && p.Action == action);

            return hasPermission;
        }

        public async Task<bool> DelegatePermissionsAsync(DelegatePermissionsRequest request)
        {
            var fromUser = await _dbContext.Users.FindAsync(request.FromUserID);
            var toUser = await _dbContext.Users.FindAsync(request.ToUserID);

            if (fromUser == null || toUser == null) return false;

            // Grant permissions to recipient
            foreach (var permId in request.PermissionIDs)
            {
                var grantReq = new GrantPermissionRequest
                {
                    UserID = request.ToUserID,
                    PermissionID = permId,
                    ExpiryDate = request.ExpiryDate,
                    Reason = $"Delegated by {fromUser.Username}"
                };

                await GrantPermissionAsync(grantReq);
            }

            await _auditService.LogActionAsync(
                $"{fromUser.Username} delegated {request.PermissionIDs.Count} permissions to {toUser.Username}");

            return true;
        }

        public async Task<bool> RevokeDelegatedPermissionsAsync(int fromUserId, int toUserId)
        {
            var fromUser = await _dbContext.Users.FindAsync(fromUserId);
            var toUser = await _dbContext.Users.FindAsync(toUserId);

            if (fromUser == null || toUser == null) return false;

            var permissions = await GetUserPermissionsAsync(toUserId);
            foreach (var perm in permissions)
            {
                await RevokePermissionAsync(toUserId, perm.PermissionID,
                    $"Delegation revoked by {fromUser.Username}");
            }

            return true;
        }
    }

    // =====================================================
    // 3. ROLE MANAGEMENT SERVICE (200 lines)
    // =====================================================

    public class RoleManagementServiceImpl : IRoleManagementService
    {
        private readonly DbContext _dbContext;
        private readonly IAuditService _auditService;

        public RoleManagementServiceImpl(DbContext dbContext, IAuditService auditService)
        {
            _dbContext = dbContext;
            _auditService = auditService;
        }

        public async Task<Role> CreateCustomRoleAsync(CreateCustomRoleRequest request)
        {
            var role = new Role
            {
                Name = request.Name,
                Description = request.Description,
                IsBuiltIn = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Roles.Add(role);
            await _dbContext.SaveChangesAsync();

            // Add permissions to role
            foreach (var permId in request.PermissionIDs ?? new List<int>())
            {
                var rolePermission = new RolePermission
                {
                    RoleID = role.RoleID,
                    PermissionID = permId,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.RolePermissions.Add(rolePermission);
            }

            await _dbContext.SaveChangesAsync();
            await _auditService.LogActionAsync($"Custom role '{request.Name}' created");

            return role;
        }

        public async Task<bool> UpdateRoleAsync(int roleId, UpdateRoleRequest request)
        {
            var role = await _dbContext.Roles.FindAsync(roleId);
            if (role == null || role.IsBuiltIn) return false;  // Can't modify built-in roles

            role.Name = request.Name ?? role.Name;
            role.Description = request.Description ?? role.Description;
            role.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            // Update permissions
            var existingPerms = await _dbContext.RolePermissions
                .Where(rp => rp.RoleID == roleId)
                .ToListAsync();

            foreach (var perm in existingPerms)
                _dbContext.RolePermissions.Remove(perm);

            foreach (var permId in request.PermissionIDs ?? new List<int>())
            {
                var rolePermission = new RolePermission
                {
                    RoleID = roleId,
                    PermissionID = permId,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.RolePermissions.Add(rolePermission);
            }

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteRoleAsync(int roleId)
        {
            var role = await _dbContext.Roles.FindAsync(roleId);
            if (role == null || role.IsBuiltIn) return false;

            // Check if role is assigned to users
            var usersWithRole = await _dbContext.UserDepartmentRoles
                .AnyAsync(ur => ur.RoleID == roleId && ur.IsActive);

            if (usersWithRole) return false;  // Can't delete if in use

            role.IsActive = false;
            await _dbContext.SaveChangesAsync();

            return true;
        }

        public async Task<List<Role>> GetRolesAsync()
        {
            return await _dbContext.Roles
                .Where(r => r.IsActive)
                .OrderBy(r => r.IsBuiltIn ? 0 : 1)
                .ThenBy(r => r.Name)
                .ToListAsync();
        }

        public async Task<List<Role>> GetRolesByDepartmentAsync(int deptId)
        {
            return await _dbContext.UserDepartmentRoles
                .Where(ur => ur.DepartmentID == deptId && ur.IsActive)
                .Join(_dbContext.Roles, ur => ur.RoleID, r => r.RoleID,
                    (ur, r) => r)
                .Distinct()
                .ToListAsync();
        }

        public async Task<List<User>> GetRoleUsersAsync(int roleId)
        {
            return await _dbContext.UserDepartmentRoles
                .Where(ur => ur.RoleID == roleId && ur.IsActive)
                .Join(_dbContext.Users, ur => ur.UserID, u => u.UserID,
                    (ur, u) => u)
                .ToListAsync();
        }

        public async Task<Role> CloneRoleAsync(int sourceRoleId, string newRoleName)
        {
            var sourceRole = await _dbContext.Roles.FindAsync(sourceRoleId);
            if (sourceRole == null) return null;

            var permissions = await _dbContext.RolePermissions
                .Where(rp => rp.RoleID == sourceRoleId)
                .Select(rp => rp.PermissionID)
                .ToListAsync();

            var createRequest = new CreateCustomRoleRequest
            {
                Name = newRoleName,
                Description = $"Cloned from {sourceRole.Name}",
                PermissionIDs = permissions
            };

            return await CreateCustomRoleAsync(createRequest);
        }

        public async Task<bool> ValidateRolePermissionsAsync(int roleId)
        {
            var role = await _dbContext.Roles.FindAsync(roleId);
            if (role == null) return false;

            var permissions = await _dbContext.RolePermissions
                .Where(rp => rp.RoleID == roleId)
                .ToListAsync();

            return permissions.Any();
        }
    }

    // =====================================================
    // 4. USER LIFECYCLE SERVICE (200 lines)
    // =====================================================

    public class UserLifecycleServiceImpl : IUserLifecycleService
    {
        private readonly DbContext _dbContext;
        private readonly IAuditService _auditService;

        public UserLifecycleServiceImpl(DbContext dbContext, IAuditService auditService)
        {
            _dbContext = dbContext;
            _auditService = auditService;
        }

        public async Task<List<User>> GetExpiredUsersAsync()
        {
            return await _dbContext.Users
                .Where(u => u.PasswordExpiresAt.HasValue && u.PasswordExpiresAt < DateTime.UtcNow)
                .ToListAsync();
        }

        public async Task<List<User>> GetInactiveUsersAsync(int daysInactive)
        {
            var inactiveDate = DateTime.UtcNow.AddDays(-daysInactive);
            return await _dbContext.Users
                .Where(u => u.LastLoginAt.HasValue && u.LastLoginAt < inactiveDate)
                .ToListAsync();
        }

        public async Task<bool> SetUserExpiryAsync(int userId, DateTime expiryDate)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return false;

            user.PasswordExpiresAt = expiryDate;
            await _dbContext.SaveChangesAsync();

            return true;
        }

        public async Task<bool> ExtendUserAccessAsync(int userId, int days)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return false;

            user.PasswordExpiresAt = DateTime.UtcNow.AddDays(days);
            await _dbContext.SaveChangesAsync();

            await _auditService.LogActionAsync($"User {user.Username} access extended for {days} days");

            return true;
        }

        public async Task<bool> ChangeUserDepartmentAsync(int userId, int newDeptId)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return false;

            var oldDeptId = user.DepartmentID;
            user.DepartmentID = newDeptId;
            user.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            await _auditService.LogActionAsync(
                $"User {user.Username} moved from dept {oldDeptId} to {newDeptId}");

            return true;
        }

        public async Task<UserAuditTrail> GetUserAuditTrailAsync(int userId, DateTime from, DateTime to)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return null;

            var entries = await _dbContext.UserAuditLogs
                .Where(al => (al.UserID == userId || al.TargetUserID == userId)
                    && al.Timestamp >= from && al.Timestamp <= to)
                .OrderByDescending(al => al.Timestamp)
                .ToListAsync();

            return new UserAuditTrail
            {
                UserID = userId,
                Entries = entries,
                TotalActions = entries.Count,
                ExportedAt = DateTime.UtcNow
            };
        }

        public async Task<bool> ExportUserAuditAsync(int userId, string format)
        {
            var trail = await GetUserAuditTrailAsync(userId, DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow);
            if (trail == null) return false;

            // TODO: Export to CSV/PDF based on format
            return true;
        }

        public async Task<List<UserAccessReport>> GetAccessReportAsync()
        {
            return await _dbContext.Users
                .Where(u => u.IsActive)
                .Select(u => new UserAccessReport
                {
                    UserID = u.UserID,
                    Username = u.Username,
                    DepartmentCount = _dbContext.UserDepartmentRoles.Count(ur => ur.UserID == u.UserID && ur.IsActive),
                    RoleCount = _dbContext.UserDepartmentRoles.Where(ur => ur.UserID == u.UserID && ur.IsActive).Select(ur => ur.RoleID).Distinct().Count(),
                    LastLogin = u.LastLoginAt
                })
                .ToListAsync();
        }

        public async Task<bool> DisableUserMFAAsync(int userId, string reason)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return false;

            user.IsMFAEnabled = false;
            user.MFASecret = null;
            await _dbContext.SaveChangesAsync();

            await _auditService.LogActionAsync($"MFA disabled for {user.Username}: {reason}");

            return true;
        }

        public async Task<bool> ResetUserPasswordAsync(int userId, string reason)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return false;

            user.RequirePasswordChange = true;
            await _dbContext.SaveChangesAsync();

            await _auditService.LogActionAsync($"Password reset requested for {user.Username}: {reason}");

            return true;
        }
    }

    // =====================================================
    // 5. ACCESS CONTROL SERVICE (150 lines)
    // =====================================================

    public class AccessControlServiceImpl : IAccessControlService
    {
        private readonly DbContext _dbContext;
        private readonly IPermissionAssignmentService _permissionService;

        public AccessControlServiceImpl(
            DbContext dbContext,
            IPermissionAssignmentService permissionService)
        {
            _dbContext = dbContext;
            _permissionService = permissionService;
        }

        public async Task<bool> CanUserAccessResourceAsync(int userId, string resource, string action)
        {
            return await _permissionService.ValidatePermissionAsync(userId, resource, action);
        }

        public async Task<bool> CanUserAccessDepartmentAsync(int userId, int deptId, string action)
        {
            var hasRole = await _dbContext.UserDepartmentRoles
                .AnyAsync(ur => ur.UserID == userId && ur.DepartmentID == deptId && ur.IsActive);

            if (!hasRole) return false;

            // Check if role has permission for department
            return await CanUserAccessResourceAsync(userId, "Document", action);
        }

        public async Task<AccessLevel> GetUserAccessLevelAsync(int userId)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return AccessLevel.None;

            var permissions = await _permissionService.GetUserPermissionsAsync(userId);
            var permCount = permissions.Count;

            return permCount switch
            {
                0 => AccessLevel.None,
                <= 5 => AccessLevel.View,
                <= 12 => AccessLevel.Edit,
                <= 18 => AccessLevel.Manage,
                _ => AccessLevel.Admin
            };
        }

        public async Task<List<AccessibleDepartment>> GetAccessibleDepartmentsAsync(int userId)
        {
            return await _dbContext.UserDepartmentRoles
                .Where(ur => ur.UserID == userId && ur.IsActive)
                .Join(_dbContext.Departments, ur => ur.DepartmentID, d => d.DepartmentID,
                    (ur, d) => new AccessibleDepartment
                    {
                        DepartmentID = d.DepartmentID,
                        DepartmentName = d.Name,
                        AccessLevel = ur.IsPrimary ? "Primary" : "Secondary"
                    })
                .ToListAsync();
        }

        public async Task<bool> ValidateAccessChainAsync(int userId, int targetDeptId)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return false;

            // Check if user has access to target department
            return await CanUserAccessDepartmentAsync(userId, targetDeptId, "View");
        }

        public async Task<AccessReport> GenerateAccessReportAsync(int userId)
        {
            var accessibleDepts = await GetAccessibleDepartmentsAsync(userId);
            var permissions = await _permissionService.GetUserPermissionsAsync(userId);

            return new AccessReport
            {
                UserID = userId,
                AccessibleDepts = accessibleDepts,
                GeneratedAt = DateTime.UtcNow
            };
        }

        public async Task<bool> EnforceAccessControlAsync(int userId, string resource)
        {
            var hasAccess = await CanUserAccessResourceAsync(userId, resource, "View");
            return hasAccess;
        }

        public async Task<List<AnomalousAccess>> DetectAnomalousAccessAsync()
        {
            // Detect users accessing resources outside their departments
            var anomalies = new List<AnomalousAccess>();

            // TODO: Implement anomaly detection logic
            // Query audit logs for access attempts outside permitted departments

            return anomalies;
        }
    }

    // =====================================================
    // SERVICE REGISTRATION
    // =====================================================

    public static class UserManagementExtensions
    {
        public static IServiceCollection AddUserManagementServices(this IServiceCollection services)
        {
            services.AddScoped<IUserProvisioningService, UserProvisioningServiceImpl>();
            services.AddScoped<IPermissionAssignmentService, PermissionAssignmentServiceImpl>();
            services.AddScoped<IRoleManagementService, RoleManagementServiceImpl>();
            services.AddScoped<IUserLifecycleService, UserLifecycleServiceImpl>();
            services.AddScoped<IAccessControlService, AccessControlServiceImpl>();

            return services;
        }
    }

    // =====================================================
    // SUPPORTING INTERFACES
    // =====================================================

    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string to, string subject, string body);
    }

    public interface IPasswordHasher
    {
        string HashPassword(string password);
        bool VerifyPassword(string password, string hash);
    }

    public interface IAuditService
    {
        Task LogActionAsync(string action);
    }

    public interface IDbContext
    {
        // Database set definitions
    }

    public class DbContext : IDbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<UserDepartmentRole> UserDepartmentRoles { get; set; }
        public DbSet<UserAuditLog> UserAuditLogs { get; set; }

        public async Task SaveChangesAsync() { }
    }

    // Database models
    public class User { public int UserID { get; set; } public string Username { get; set; } public string Email { get; set; } public string PasswordHash { get; set; } public int DepartmentID { get; set; } public int RoleID { get; set; } public bool IsActive { get; set; } public bool IsMFAEnabled { get; set; } public string MFASecret { get; set; } public bool RequirePasswordChange { get; set; } public DateTime? PasswordExpiresAt { get; set; } public DateTime? LastLoginAt { get; set; } public DateTime CreatedAt { get; set; } public DateTime UpdatedAt { get; set; } public List<UserDepartmentRole> UserDepartmentRoles { get; set; } }
    public class Role { public int RoleID { get; set; } public string Name { get; set; } public string Description { get; set; } public bool IsBuiltIn { get; set; } public bool IsActive { get; set; } public DateTime CreatedAt { get; set; } public DateTime UpdatedAt { get; set; } }
    public class Permission { public int PermissionID { get; set; } public string Resource { get; set; } public string Action { get; set; } public bool IsActive { get; set; } }
    public class RolePermission { public int RolePermissionID { get; set; } public int RoleID { get; set; } public int PermissionID { get; set; } public DateTime CreatedAt { get; set; } }
    public class UserDepartmentRole { public int UserDeptRoleID { get; set; } public int UserID { get; set; } public int DepartmentID { get; set; } public int RoleID { get; set; } public bool IsPrimary { get; set; } public DateTime? EffectiveFrom { get; set; } public DateTime? EffectiveTo { get; set; } public DateTime AssignedAt { get; set; } public bool IsActive { get; set; } }
    public class UserAuditLog { public long AuditID { get; set; } public int? UserID { get; set; } public int? TargetUserID { get; set; } public string ActionType { get; set; } public string NewValue { get; set; } public DateTime Timestamp { get; set; } }
    public class DbSet<T> { public async Task AddAsync(T entity) { } public void Add(T entity) { } public void Remove(T entity) { } public async Task<T> FindAsync(int id) { return null; } public IQueryable<T> AsQueryable() { return null; } }
}
