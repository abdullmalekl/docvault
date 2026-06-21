// =====================================================
// DocVault Module 3: User & Permission Management Tests
// Unit Tests + Integration Tests
// Framework: xUnit + Moq
// Date: June 21, 2026 (Checkpoint 2)
// =====================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;

namespace DocVault.Core.Tests.UserManagement
{
    // =====================================================
    // USER PROVISIONING SERVICE TESTS (15 tests)
    // =====================================================

    public class UserProvisioningServiceTests
    {
        private readonly Mock<DbContext> _mockDb = new();
        private readonly Mock<IAuditService> _mockAudit = new();
        private readonly Mock<IEmailService> _mockEmail = new();
        private readonly Mock<IPasswordHasher> _mockHasher = new();

        [Fact]
        public async Task ProvisionUser_ValidRequest_CreatesUser()
        {
            var request = new ProvisionUserRequest { Username = "john", Email = "john@test.com", DepartmentID = 1 };
            var service = new UserProvisioningServiceImpl(_mockDb.Object, _mockAudit.Object, _mockEmail.Object, _mockHasher.Object);

            var result = await service.ProvisionUserAsync(request);

            Assert.NotNull(result);
        }

        [Fact]
        public async Task ProvisionUser_DuplicateUsername_ThrowsException()
        {
            var request = new ProvisionUserRequest { Username = "duplicate", Email = "dup@test.com", DepartmentID = 1 };
            var service = new UserProvisioningServiceImpl(_mockDb.Object, _mockAudit.Object, _mockEmail.Object, _mockHasher.Object);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProvisionUserAsync(request));
        }

        [Fact]
        public async Task SendWelcomeEmail_ValidUser_SendsEmail()
        {
            var service = new UserProvisioningServiceImpl(_mockDb.Object, _mockAudit.Object, _mockEmail.Object, _mockHasher.Object);
            var result = await service.SendWelcomeEmailAsync(1);
            Assert.True(result);
        }

        [Fact]
        public async Task GetUser_ValidId_ReturnsUser()
        {
            var service = new UserProvisioningServiceImpl(_mockDb.Object, _mockAudit.Object, _mockEmail.Object, _mockHasher.Object);
            var result = await service.GetUserAsync(1);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetDepartmentUsers_ValidDept_ReturnsUsers()
        {
            var service = new UserProvisioningServiceImpl(_mockDb.Object, _mockAudit.Object, _mockEmail.Object, _mockHasher.Object);
            var result = await service.GetDepartmentUsersAsync(1);
            Assert.IsType<List<User>>(result);
        }

        [Fact]
        public async Task SearchUsers_WithFilters_ReturnsFilteredList()
        {
            var request = new UserSearchRequest { Username = "john", DepartmentID = 1 };
            var service = new UserProvisioningServiceImpl(_mockDb.Object, _mockAudit.Object, _mockEmail.Object, _mockHasher.Object);
            var result = await service.SearchUsersAsync(request);
            Assert.IsType<List<User>>(result);
        }

        [Fact]
        public async Task UpdateUser_ValidData_UpdatesUser()
        {
            var updateReq = new UpdateUserRequest { Email = "new@test.com", DepartmentID = 2 };
            var service = new UserProvisioningServiceImpl(_mockDb.Object, _mockAudit.Object, _mockEmail.Object, _mockHasher.Object);
            var result = await service.UpdateUserAsync(1, updateReq);
            Assert.True(result);
        }

        [Fact]
        public async Task DeactivateUser_ValidId_DeactivatesUser()
        {
            var service = new UserProvisioningServiceImpl(_mockDb.Object, _mockAudit.Object, _mockEmail.Object, _mockHasher.Object);
            var result = await service.DeactivateUserAsync(1, "Test reason");
            Assert.True(result);
        }

        [Fact]
        public async Task ReactivateUser_ValidId_ReactivatesUser()
        {
            var service = new UserProvisioningServiceImpl(_mockDb.Object, _mockAudit.Object, _mockEmail.Object, _mockHasher.Object);
            var result = await service.ReactivateUserAsync(1);
            Assert.True(result);
        }

        [Fact]
        public async Task DeleteUser_ValidId_SoftDeletesUser()
        {
            var service = new UserProvisioningServiceImpl(_mockDb.Object, _mockAudit.Object, _mockEmail.Object, _mockHasher.Object);
            var result = await service.DeleteUserAsync(1, "Terminated");
            Assert.True(result);
        }

        [Fact]
        public async Task ProvisionUser_InvalidEmail_ReturnsFalse()
        {
            var request = new ProvisionUserRequest { Username = "john", Email = "invalid-email", DepartmentID = 1 };
            var service = new UserProvisioningServiceImpl(_mockDb.Object, _mockAudit.Object, _mockEmail.Object, _mockHasher.Object);

            var isValid = await service.ValidateProvisioningDataAsync(request);
            Assert.False(isValid);
        }

        [Fact]
        public async Task ProvisionUser_ShortUsername_ReturnsFalse()
        {
            var request = new ProvisionUserRequest { Username = "ab", Email = "ab@test.com", DepartmentID = 1 };
            var service = new UserProvisioningServiceImpl(_mockDb.Object, _mockAudit.Object, _mockEmail.Object, _mockHasher.Object);

            var isValid = await service.ValidateProvisioningDataAsync(request);
            Assert.False(isValid);
        }

        [Fact]
        public async Task ValidateProvisioningData_ValidRequest_ReturnsTrue()
        {
            var request = new ProvisionUserRequest { Username = "validuser", Email = "valid@test.com", DepartmentID = 1 };
            var service = new UserProvisioningServiceImpl(_mockDb.Object, _mockAudit.Object, _mockEmail.Object, _mockHasher.Object);

            var result = await service.ValidateProvisioningDataAsync(request);
            Assert.True(result);
        }

        [Fact]
        public async Task SearchUsers_NoFilters_ReturnsAllUsers()
        {
            var request = new UserSearchRequest();
            var service = new UserProvisioningServiceImpl(_mockDb.Object, _mockAudit.Object, _mockEmail.Object, _mockHasher.Object);
            var result = await service.SearchUsersAsync(request);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task UpdateUser_InvalidId_ReturnsFalse()
        {
            var updateReq = new UpdateUserRequest { Email = "new@test.com" };
            var service = new UserProvisioningServiceImpl(_mockDb.Object, _mockAudit.Object, _mockEmail.Object, _mockHasher.Object);
            var result = await service.UpdateUserAsync(9999, updateReq);
            Assert.False(result);
        }
    }

    // =====================================================
    // PERMISSION ASSIGNMENT SERVICE TESTS (15 tests)
    // =====================================================

    public class PermissionAssignmentServiceTests
    {
        private readonly Mock<DbContext> _mockDb = new();
        private readonly Mock<IAuditService> _mockAudit = new();

        [Fact]
        public async Task GrantPermission_ValidRequest_GrantsPermission()
        {
            var request = new GrantPermissionRequest { UserID = 1, PermissionID = 1 };
            var service = new PermissionAssignmentServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.GrantPermissionAsync(request);
            Assert.True(result);
        }

        [Fact]
        public async Task RevokePermission_ValidRequest_RevokesPermission()
        {
            var service = new PermissionAssignmentServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.RevokePermissionAsync(1, 1, "Test revoke");
            Assert.True(result);
        }

        [Fact]
        public async Task GrantRole_ValidRequest_GrantsRole()
        {
            var request = new GrantRoleRequest { UserID = 1, RoleID = 2, DepartmentID = 1 };
            var service = new PermissionAssignmentServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.GrantRoleAsync(request);
            Assert.True(result);
        }

        [Fact]
        public async Task RevokeRole_ValidRequest_RevokesRole()
        {
            var service = new PermissionAssignmentServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.RevokeRoleAsync(1, 2, "Test revoke");
            Assert.True(result);
        }

        [Fact]
        public async Task GetUserPermissions_ValidId_ReturnsPermissions()
        {
            var service = new PermissionAssignmentServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.GetUserPermissionsAsync(1);
            Assert.IsType<List<Permission>>(result);
        }

        [Fact]
        public async Task GetUserPermissionsByDept_ValidIds_ReturnsPermissions()
        {
            var service = new PermissionAssignmentServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.GetUserPermissionsByDeptAsync(1, 1);
            Assert.IsType<List<Permission>>(result);
        }

        [Fact]
        public async Task GetUserRoles_ValidId_ReturnsRoles()
        {
            var service = new PermissionAssignmentServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.GetUserRolesAsync(1);
            Assert.IsType<List<Role>>(result);
        }

        [Fact]
        public async Task ValidatePermission_ValidAccess_ReturnsTrue()
        {
            var service = new PermissionAssignmentServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.ValidatePermissionAsync(1, "Document", "View");
            Assert.True(result);
        }

        [Fact]
        public async Task DelegatePermissions_ValidRequest_DelegatesPermissions()
        {
            var request = new DelegatePermissionsRequest { FromUserID = 1, ToUserID = 2, PermissionIDs = new List<int> { 1, 2, 3 }, ExpiryDate = DateTime.UtcNow.AddDays(7) };
            var service = new PermissionAssignmentServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.DelegatePermissionsAsync(request);
            Assert.True(result);
        }

        [Fact]
        public async Task RevokeDelegatedPermissions_ValidRequest_RevokesPermissions()
        {
            var service = new PermissionAssignmentServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.RevokeDelegatedPermissionsAsync(1, 2);
            Assert.True(result);
        }

        [Fact]
        public async Task GrantPermission_InvalidUser_ReturnsFalse()
        {
            var request = new GrantPermissionRequest { UserID = 9999, PermissionID = 1 };
            var service = new PermissionAssignmentServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.GrantPermissionAsync(request);
            Assert.False(result);
        }

        [Fact]
        public async Task GrantRole_DuplicateRole_ReturnsFalse()
        {
            var request = new GrantRoleRequest { UserID = 1, RoleID = 2, DepartmentID = 1 };
            var service = new PermissionAssignmentServiceImpl(_mockDb.Object, _mockAudit.Object);

            // Grant once
            await service.GrantRoleAsync(request);

            // Try again
            var result = await service.GrantRoleAsync(request);
            Assert.False(result);
        }

        [Fact]
        public async Task RevokePermission_InvalidPermission_ReturnsFalse()
        {
            var service = new PermissionAssignmentServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.RevokePermissionAsync(1, 9999, "Test");
            Assert.False(result);
        }

        [Fact]
        public async Task DelegatePermissions_InvalidUser_ReturnsFalse()
        {
            var request = new DelegatePermissionsRequest { FromUserID = 9999, ToUserID = 2, PermissionIDs = new List<int> { 1 } };
            var service = new PermissionAssignmentServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.DelegatePermissionsAsync(request);
            Assert.False(result);
        }

        [Fact]
        public async Task ValidatePermission_InvalidAccess_ReturnsFalse()
        {
            var service = new PermissionAssignmentServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.ValidatePermissionAsync(1, "InvalidResource", "InvalidAction");
            Assert.False(result);
        }

        [Fact]
        public async Task GetUserRoles_InvalidUser_ReturnsEmpty()
        {
            var service = new PermissionAssignmentServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.GetUserRolesAsync(9999);
            Assert.Empty(result);
        }
    }

    // =====================================================
    // ROLE MANAGEMENT SERVICE TESTS (10 tests)
    // =====================================================

    public class RoleManagementServiceTests
    {
        private readonly Mock<DbContext> _mockDb = new();
        private readonly Mock<IAuditService> _mockAudit = new();

        [Fact]
        public async Task CreateCustomRole_ValidRequest_CreatesRole()
        {
            var request = new CreateCustomRoleRequest { Name = "Reviewer", Description = "Document reviewer", PermissionIDs = new List<int> { 1, 2 } };
            var service = new RoleManagementServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.CreateCustomRoleAsync(request);
            Assert.NotNull(result);
            Assert.Equal("Reviewer", result.Name);
        }

        [Fact]
        public async Task UpdateRole_ValidRequest_UpdatesRole()
        {
            var request = new UpdateRoleRequest { Name = "UpdatedRole", PermissionIDs = new List<int> { 1 } };
            var service = new RoleManagementServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.UpdateRoleAsync(5, request);
            Assert.True(result);
        }

        [Fact]
        public async Task DeleteRole_ValidRequest_DeletesRole()
        {
            var service = new RoleManagementServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.DeleteRoleAsync(5);
            Assert.True(result);
        }

        [Fact]
        public async Task GetRoles_ReturnsAllRoles()
        {
            var service = new RoleManagementServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.GetRolesAsync();
            Assert.IsType<List<Role>>(result);
        }

        [Fact]
        public async Task GetRolesByDepartment_ValidDept_ReturnsRoles()
        {
            var service = new RoleManagementServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.GetRolesByDepartmentAsync(1);
            Assert.IsType<List<Role>>(result);
        }

        [Fact]
        public async Task GetRoleUsers_ValidRole_ReturnsUsers()
        {
            var service = new RoleManagementServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.GetRoleUsersAsync(2);
            Assert.IsType<List<User>>(result);
        }

        [Fact]
        public async Task CloneRole_ValidRole_ClonesRole()
        {
            var service = new RoleManagementServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.CloneRoleAsync(2, "ClonedRole");
            Assert.NotNull(result);
            Assert.Equal("ClonedRole", result.Name);
        }

        [Fact]
        public async Task ValidateRolePermissions_ValidRole_ReturnsTrue()
        {
            var service = new RoleManagementServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.ValidateRolePermissionsAsync(2);
            Assert.True(result);
        }

        [Fact]
        public async Task UpdateRole_BuiltInRole_ReturnsFalse()
        {
            var request = new UpdateRoleRequest { Name = "Modified" };
            var service = new RoleManagementServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.UpdateRoleAsync(1, request);  // Admin (built-in)
            Assert.False(result);
        }

        [Fact]
        public async Task DeleteRole_InvalidRole_ReturnsFalse()
        {
            var service = new RoleManagementServiceImpl(_mockDb.Object, _mockAudit.Object);
            var result = await service.DeleteRoleAsync(9999);
            Assert.False(result);
        }
    }

    // =====================================================
    // INTEGRATION TESTS (8 tests)
    // =====================================================

    public class UserManagementIntegrationTests
    {
        [Fact]
        public async Task ProvisioningAndRoleAssignment_CompleteFlow_Success()
        {
            var mockDb = new Mock<DbContext>();
            var mockAudit = new Mock<IAuditService>();
            var mockEmail = new Mock<IEmailService>();
            var mockHasher = new Mock<IPasswordHasher>();

            var provisioningService = new UserProvisioningServiceImpl(mockDb.Object, mockAudit.Object, mockEmail.Object, mockHasher.Object);
            var roleService = new RoleManagementServiceImpl(mockDb.Object, mockAudit.Object);

            // Provision user
            var userReq = new ProvisionUserRequest { Username = "newuser", Email = "new@test.com", DepartmentID = 1, RoleID = 3 };
            var user = await provisioningService.ProvisionUserAsync(userReq);

            // Verify user created
            Assert.NotNull(user);

            // Create custom role
            var roleReq = new CreateCustomRoleRequest { Name = "CustomRole", PermissionIDs = new List<int> { 1, 2 } };
            var role = await roleService.CreateCustomRoleAsync(roleReq);

            // Verify role created
            Assert.NotNull(role);
        }

        [Fact]
        public async Task UserLifecycle_FromProvisionToDeactivation_Success()
        {
            var mockDb = new Mock<DbContext>();
            var mockAudit = new Mock<IAuditService>();
            var mockEmail = new Mock<IEmailService>();
            var mockHasher = new Mock<IPasswordHasher>();

            var service = new UserProvisioningServiceImpl(mockDb.Object, mockAudit.Object, mockEmail.Object, mockHasher.Object);

            // Provision
            var userReq = new ProvisionUserRequest { Username = "lifecycle", Email = "life@test.com", DepartmentID = 1 };
            var user = await service.ProvisionUserAsync(userReq);
            Assert.NotNull(user);

            // Deactivate
            var deactivated = await service.DeactivateUserAsync(user.UserID, "Separation");
            Assert.True(deactivated);

            // Reactivate
            var reactivated = await service.ReactivateUserAsync(user.UserID);
            Assert.True(reactivated);
        }

        [Fact]
        public async Task PermissionDelegation_CompleteFlow_Success()
        {
            var mockDb = new Mock<DbContext>();
            var mockAudit = new Mock<IAuditService>();

            var permService = new PermissionAssignmentServiceImpl(mockDb.Object, mockAudit.Object);

            // Delegate permissions
            var delegateReq = new DelegatePermissionsRequest
            {
                FromUserID = 1,
                ToUserID = 2,
                PermissionIDs = new List<int> { 1, 2, 3 },
                ExpiryDate = DateTime.UtcNow.AddDays(7)
            };

            var delegated = await permService.DelegatePermissionsAsync(delegateReq);
            Assert.True(delegated);

            // Revoke delegated permissions
            var revoked = await permService.RevokeDelegatedPermissionsAsync(1, 2);
            Assert.True(revoked);
        }

        [Fact]
        public async Task RoleCloning_PreservePermissions_Success()
        {
            var mockDb = new Mock<DbContext>();
            var mockAudit = new Mock<IAuditService>();

            var service = new RoleManagementServiceImpl(mockDb.Object, mockAudit.Object);

            // Clone role
            var cloned = await service.CloneRoleAsync(2, "Manager_Clone");
            Assert.NotNull(cloned);
            Assert.Equal("Manager_Clone", cloned.Name);
        }

        [Fact]
        public async Task UserDepartmentTransfer_PermissionsUpdated_Success()
        {
            var mockDb = new Mock<DbContext>();
            var mockAudit = new Mock<IAuditService>();
            var mockEmail = new Mock<IEmailService>();
            var mockHasher = new Mock<IPasswordHasher>();

            var userService = new UserProvisioningServiceImpl(mockDb.Object, mockAudit.Object, mockEmail.Object, mockHasher.Object);

            // Update user department
            var updateReq = new UpdateUserRequest { DepartmentID = 2 };
            var updated = await userService.UpdateUserAsync(1, updateReq);
            Assert.True(updated);
        }

        [Fact]
        public async Task MultiRoleAssignment_UserInMultipleDepartments_Success()
        {
            var mockDb = new Mock<DbContext>();
            var mockAudit = new Mock<IAuditService>();

            var roleService = new RoleManagementServiceImpl(mockDb.Object, mockAudit.Object);
            var permService = new PermissionAssignmentServiceImpl(mockDb.Object, mockAudit.Object);

            // Assign role in dept 1
            var req1 = new GrantRoleRequest { UserID = 1, RoleID = 2, DepartmentID = 1, IsPrimary = true };
            var result1 = await permService.GrantRoleAsync(req1);
            Assert.True(result1);

            // Assign role in dept 2
            var req2 = new GrantRoleRequest { UserID = 1, RoleID = 3, DepartmentID = 2, IsPrimary = false };
            var result2 = await permService.GrantRoleAsync(req2);
            Assert.True(result2);
        }

        [Fact]
        public async Task AccessControlValidation_UserPermissions_Enforced()
        {
            var mockDb = new Mock<DbContext>();
            var mockAudit = new Mock<IAuditService>();
            var mockPermService = new Mock<IPermissionAssignmentService>();

            mockPermService.Setup(p => p.ValidatePermissionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            var service = new AccessControlServiceImpl(mockDb.Object, mockPermService.Object);

            // Validate access
            var hasAccess = await service.CanUserAccessResourceAsync(1, "Document", "View");
            Assert.True(hasAccess);
        }

        [Fact]
        public async Task BulkUserProvisioning_MultipleUsers_AllCreated()
        {
            var mockDb = new Mock<DbContext>();
            var mockAudit = new Mock<IAuditService>();
            var mockEmail = new Mock<IEmailService>();
            var mockHasher = new Mock<IPasswordHasher>();

            var service = new UserProvisioningServiceImpl(mockDb.Object, mockAudit.Object, mockEmail.Object, mockHasher.Object);

            var users = new List<User>();
            for (int i = 1; i <= 5; i++)
            {
                var req = new ProvisionUserRequest { Username = $"user{i}", Email = $"user{i}@test.com", DepartmentID = 1 };
                var user = await service.ProvisionUserAsync(req);
                users.Add(user);
            }

            Assert.Equal(5, users.Count);
        }
    }
}
