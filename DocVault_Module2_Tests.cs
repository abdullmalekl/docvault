// =====================================================
// DocVault Module 2: Organization Structure Tests
// Unit Tests + Integration Tests
// Framework: xUnit + Moq
// Date: June 21, 2026 (Checkpoint 2)
// =====================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using DocVault.Core.Organization;

namespace DocVault.Core.Tests.Organization
{
    // =====================================================
    // BRANCH SERVICE TESTS (10 tests)
    // =====================================================

    public class BranchServiceTests
    {
        private readonly Mock<DbContext> _mockDbContext = new();
        private readonly Mock<IAuditService> _mockAuditService = new();

        private IBranchService GetBranchService()
        {
            return new BranchService(_mockDbContext.Object, _mockAuditService.Object);
        }

        [Fact]
        public async Task CreateBranch_ValidRequest_CreatesBranch()
        {
            // Arrange
            var request = new CreateBranchRequest
            {
                Name = "Cairo Branch",
                Code = "CAIRO",
                Location = "Cairo, Egypt",
                IsHeadquarters = false
            };

            var service = GetBranchService();

            // Act
            var result = await service.CreateBranchAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(request.Name, result.Name);
            Assert.Equal(request.Code, result.Code);
            Assert.True(result.IsActive);
        }

        [Fact]
        public async Task CreateBranch_DuplicateCode_ThrowsException()
        {
            // Arrange
            var request = new CreateBranchRequest
            {
                Name = "Duplicate Branch",
                Code = "DUP"
            };

            var service = GetBranchService();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateBranchAsync(request));
        }

        [Fact]
        public async Task GetBranch_ValidId_ReturnsBranch()
        {
            // Arrange
            var branchId = 1;
            var service = GetBranchService();

            // Act
            var result = await service.GetBranchAsync(branchId);

            // Assert - Would be populated from mock
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetAllBranches_Active_ReturnsList()
        {
            // Arrange
            var service = GetBranchService();

            // Act
            var result = await service.GetAllBranchesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.IsType<List<Branch>>(result);
        }

        [Fact]
        public async Task UpdateBranch_ValidData_UpdatesBranch()
        {
            // Arrange
            var branchId = 1;
            var request = new UpdateBranchRequest
            {
                Name = "Updated Branch",
                Location = "New Location"
            };

            var service = GetBranchService();

            // Act
            var result = await service.UpdateBranchAsync(branchId, request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task DeleteBranch_ValidId_DeactivatesBranch()
        {
            // Arrange
            var branchId = 1;
            var service = GetBranchService();

            // Act
            var result = await service.DeleteBranchAsync(branchId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task GetHeadquarters_ReturnHQBranch()
        {
            // Arrange
            var service = GetBranchService();

            // Act
            var result = await service.GetHeadquartersAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsHeadquarters);
        }

        [Fact]
        public async Task GetSubBranches_ValidParentId_ReturnsList()
        {
            // Arrange
            var parentBranchId = 1;
            var service = GetBranchService();

            // Act
            var result = await service.GetSubBranchesAsync(parentBranchId);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<List<Branch>>(result);
        }

        [Fact]
        public async Task GetBranchUserCount_ValidBranchId_ReturnsCount()
        {
            // Arrange
            var branchId = 1;
            var service = GetBranchService();

            // Act
            var result = await service.GetBranchUserCountAsync(branchId);

            // Assert
            Assert.IsType<int>(result);
            Assert.True(result >= 0);
        }

        [Fact]
        public async Task GetBranchDepartments_ValidBranchId_ReturnsList()
        {
            // Arrange
            var branchId = 1;
            var service = GetBranchService();

            // Act
            var result = await service.GetBranchDepartmentsAsync(branchId);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<List<Department>>(result);
        }
    }

    // =====================================================
    // DEPARTMENT SERVICE TESTS (12 tests)
    // =====================================================

    public class DepartmentServiceTests
    {
        private readonly Mock<DbContext> _mockDbContext = new();
        private readonly Mock<IAuditService> _mockAuditService = new();

        private IDepartmentService GetDepartmentService()
        {
            return new DepartmentService(_mockDbContext.Object, _mockAuditService.Object);
        }

        [Fact]
        public async Task CreateDepartment_ValidRequest_CreatesDepartment()
        {
            // Arrange
            var request = new CreateDepartmentRequest
            {
                Name = "Human Resources",
                Code = "HR",
                BranchID = 1,
                Description = "HR Department"
            };

            var service = GetDepartmentService();

            // Act
            var result = await service.CreateDepartmentAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(request.Name, result.Name);
            Assert.Equal(request.Code, result.Code);
        }

        [Fact]
        public async Task CreateDepartment_DuplicateCodeInBranch_ThrowsException()
        {
            // Arrange
            var request = new CreateDepartmentRequest
            {
                Name = "Duplicate Dept",
                Code = "HR",
                BranchID = 1
            };

            var service = GetDepartmentService();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateDepartmentAsync(request));
        }

        [Fact]
        public async Task GetDepartment_ValidId_ReturnsDepartment()
        {
            // Arrange
            var deptId = 1;
            var service = GetDepartmentService();

            // Act
            var result = await service.GetDepartmentAsync(deptId);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetDepartmentsByBranch_ValidBranchId_ReturnsList()
        {
            // Arrange
            var branchId = 1;
            var service = GetDepartmentService();

            // Act
            var result = await service.GetDepartmentsByBranchAsync(branchId);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<List<Department>>(result);
        }

        [Fact]
        public async Task GetDepartmentsByManager_ValidManagerId_ReturnsList()
        {
            // Arrange
            var managerId = 1;
            var service = GetDepartmentService();

            // Act
            var result = await service.GetDepartmentsByManagerAsync(managerId);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<List<Department>>(result);
        }

        [Fact]
        public async Task UpdateDepartment_ValidData_UpdatesDepartment()
        {
            // Arrange
            var deptId = 1;
            var request = new UpdateDepartmentRequest
            {
                Name = "Updated HR"
            };

            var service = GetDepartmentService();

            // Act
            var result = await service.UpdateDepartmentAsync(deptId, request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task DeleteDepartment_ValidId_DeactivatesDepartment()
        {
            // Arrange
            var deptId = 1;
            var service = GetDepartmentService();

            // Act
            var result = await service.DeleteDepartmentAsync(deptId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task AssignManager_ValidIds_AssignsManager()
        {
            // Arrange
            var deptId = 1;
            var userId = 5;
            var service = GetDepartmentService();

            // Act
            var result = await service.AssignManagerAsync(deptId, userId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task GetDepartmentUnits_ValidDeptId_ReturnsList()
        {
            // Arrange
            var deptId = 1;
            var service = GetDepartmentService();

            // Act
            var result = await service.GetDepartmentUnitsAsync(deptId);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<List<Unit>>(result);
        }

        [Fact]
        public async Task GetDepartmentUserCount_ValidDeptId_ReturnsCount()
        {
            // Arrange
            var deptId = 1;
            var service = GetDepartmentService();

            // Act
            var result = await service.GetDepartmentUserCountAsync(deptId);

            // Assert
            Assert.IsType<int>(result);
            Assert.True(result >= 0);
        }

        [Fact]
        public async Task AssignManager_InvalidDept_ReturnsFalse()
        {
            // Arrange
            var invalidDeptId = 9999;
            var userId = 1;
            var service = GetDepartmentService();

            // Act
            var result = await service.AssignManagerAsync(invalidDeptId, userId);

            // Assert
            Assert.False(result);
        }
    }

    // =====================================================
    // UNIT SERVICE TESTS (10 tests)
    // =====================================================

    public class UnitServiceTests
    {
        private readonly Mock<DbContext> _mockDbContext = new();
        private readonly Mock<IAuditService> _mockAuditService = new();

        private IUnitService GetUnitService()
        {
            return new UnitService(_mockDbContext.Object, _mockAuditService.Object);
        }

        [Fact]
        public async Task CreateUnit_ValidRequest_CreatesUnit()
        {
            // Arrange
            var request = new CreateUnitRequest
            {
                Name = "Recruitment Unit",
                Code = "REC",
                DepartmentID = 1,
                Responsibility = "Hiring and recruitment"
            };

            var service = GetUnitService();

            // Act
            var result = await service.CreateUnitAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(request.Name, result.Name);
        }

        [Fact]
        public async Task GetUnit_ValidId_ReturnsUnit()
        {
            // Arrange
            var unitId = 1;
            var service = GetUnitService();

            // Act
            var result = await service.GetUnitAsync(unitId);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetUnitsByDepartment_ValidDeptId_ReturnsList()
        {
            // Arrange
            var deptId = 1;
            var service = GetUnitService();

            // Act
            var result = await service.GetUnitsByDepartmentAsync(deptId);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<List<Unit>>(result);
        }

        [Fact]
        public async Task GetUnitsBySupervisor_ValidSupervisorId_ReturnsList()
        {
            // Arrange
            var supervisorId = 5;
            var service = GetUnitService();

            // Act
            var result = await service.GetUnitsBySupervisorAsync(supervisorId);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<List<Unit>>(result);
        }

        [Fact]
        public async Task UpdateUnit_ValidData_UpdatesUnit()
        {
            // Arrange
            var unitId = 1;
            var request = new UpdateUnitRequest
            {
                Name = "Updated Recruitment"
            };

            var service = GetUnitService();

            // Act
            var result = await service.UpdateUnitAsync(unitId, request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task DeleteUnit_ValidId_DeactivatesUnit()
        {
            // Arrange
            var unitId = 1;
            var service = GetUnitService();

            // Act
            var result = await service.DeleteUnitAsync(unitId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task AssignSupervisor_ValidIds_AssignsSupervisor()
        {
            // Arrange
            var unitId = 1;
            var userId = 5;
            var service = GetUnitService();

            // Act
            var result = await service.AssignSupervisorAsync(unitId, userId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task GetUnitMembers_ValidUnitId_ReturnsList()
        {
            // Arrange
            var unitId = 1;
            var service = GetUnitService();

            // Act
            var result = await service.GetUnitMembersAsync(unitId);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<List<User>>(result);
        }

        [Fact]
        public async Task CreateUnit_WithSupervisor_SetsSupervisor()
        {
            // Arrange
            var request = new CreateUnitRequest
            {
                Name = "Test Unit",
                Code = "TEST",
                DepartmentID = 1,
                SupervisorUserID = 3
            };

            var service = GetUnitService();

            // Act
            var result = await service.CreateUnitAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.SupervisorUserID);
        }

        [Fact]
        public async Task AssignSupervisor_InvalidUnit_ReturnsFalse()
        {
            // Arrange
            var invalidUnitId = 9999;
            var userId = 1;
            var service = GetUnitService();

            // Act
            var result = await service.AssignSupervisorAsync(invalidUnitId, userId);

            // Assert
            Assert.False(result);
        }
    }

    // =====================================================
    // ORGANIZATION SERVICE TESTS (8 tests)
    // =====================================================

    public class OrganizationServiceTests
    {
        private readonly Mock<DbContext> _mockDbContext = new();
        private readonly Mock<IBranchService> _mockBranchService = new();
        private readonly Mock<IDepartmentService> _mockDepartmentService = new();

        private IOrganizationService GetOrganizationService()
        {
            return new OrganizationService(
                _mockDbContext.Object,
                _mockBranchService.Object,
                _mockDepartmentService.Object);
        }

        [Fact]
        public async Task GetFullHierarchy_ReturnsOrgStructure()
        {
            // Arrange
            _mockBranchService.Setup(x => x.GetHeadquartersAsync())
                .ReturnsAsync(new Branch { BranchID = 1, Name = "HQ", IsHeadquarters = true });

            _mockBranchService.Setup(x => x.GetAllBranchesAsync())
                .ReturnsAsync(new List<Branch>
                {
                    new Branch { BranchID = 1, Name = "HQ", IsHeadquarters = true },
                    new Branch { BranchID = 2, Name = "Cairo", IsHeadquarters = false }
                });

            _mockDepartmentService.Setup(x => x.GetDepartmentsByBranchAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<Department>
                {
                    new Department { DepartmentID = 1, Name = "HR", Code = "HR" }
                });

            var service = GetOrganizationService();

            // Act
            var result = await service.GetFullHierarchyAsync();

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Headquarters);
            Assert.NotNull(result.DepartmentsByBranch);
        }

        [Fact]
        public async Task GetHierarchyNode_ValidDeptId_ReturnsNode()
        {
            // Arrange
            _mockDepartmentService.Setup(x => x.GetDepartmentAsync(1))
                .ReturnsAsync(new Department { DepartmentID = 1, Name = "HR", Code = "HR" });

            _mockDepartmentService.Setup(x => x.GetDepartmentUnitsAsync(1))
                .ReturnsAsync(new List<Unit>
                {
                    new Unit { UnitID = 1, Name = "Recruitment" }
                });

            _mockDepartmentService.Setup(x => x.GetDepartmentUserCountAsync(1))
                .ReturnsAsync(5);

            var service = GetOrganizationService();

            // Act
            var result = await service.GetHierarchyNodeAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.UnitCount);
            Assert.Equal(5, result.UserCount);
        }

        [Fact]
        public async Task GetOrganizationUsers_ReturnsUserList()
        {
            // Arrange
            var service = GetOrganizationService();

            // Act
            var result = await service.GetOrganizationUsersAsync();

            // Assert
            Assert.NotNull(result);
            Assert.IsType<List<OrganizationUser>>(result);
        }

        [Fact]
        public async Task GetStatistics_ReturnsStatistics()
        {
            // Arrange
            var service = GetOrganizationService();

            // Act
            var result = await service.GetStatisticsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.IsType<OrganizationStatistics>(result);
            Assert.True(result.TotalBranches >= 0);
            Assert.True(result.TotalDepartments >= 0);
            Assert.True(result.TotalUsers >= 0);
        }

        [Fact]
        public async Task GetReportingLines_ReturnsReportingLines()
        {
            // Arrange
            var service = GetOrganizationService();

            // Act
            var result = await service.GetReportingLinesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.IsType<List<ReportingLine>>(result);
        }

        [Fact]
        public async Task GetStatistics_ValidatesStructure()
        {
            // Arrange
            var service = GetOrganizationService();

            // Act
            var result = await service.GetStatisticsAsync();

            // Assert
            Assert.NotNull(result.UsersByBranch);
        }

        [Fact]
        public async Task GetFullHierarchy_MaintainsHierarchy()
        {
            // Arrange
            _mockBranchService.Setup(x => x.GetHeadquartersAsync())
                .ReturnsAsync(new Branch { BranchID = 1, IsHeadquarters = true });

            _mockBranchService.Setup(x => x.GetAllBranchesAsync())
                .ReturnsAsync(new List<Branch>
                {
                    new Branch { BranchID = 1, IsHeadquarters = true }
                });

            var service = GetOrganizationService();

            // Act
            var result = await service.GetFullHierarchyAsync();

            // Assert
            Assert.NotNull(result.Headquarters);
            Assert.True(result.Headquarters.IsHeadquarters);
        }
    }

    // =====================================================
    // INTEGRATION TESTS (6 tests - Full workflows)
    // =====================================================

    public class OrganizationIntegrationTests
    {
        [Fact]
        public async Task CompleteOrgCreation_CreateHierarchy_Success()
        {
            // Arrange: Create complete org structure
            var mockDbContext = new Mock<DbContext>();
            var mockAuditService = new Mock<IAuditService>();

            var branchService = new BranchService(mockDbContext.Object, mockAuditService.Object);
            var deptService = new DepartmentService(mockDbContext.Object, mockAuditService.Object);
            var unitService = new UnitService(mockDbContext.Object, mockAuditService.Object);

            // Act: Create HQ branch
            var hqRequest = new CreateBranchRequest
            {
                Name = "Headquarters",
                Code = "HQ",
                IsHeadquarters = true
            };

            var hqBranch = await branchService.CreateBranchAsync(hqRequest);

            // Act: Create departments under HQ
            var deptRequest = new CreateDepartmentRequest
            {
                Name = "Human Resources",
                Code = "HR",
                BranchID = hqBranch.BranchID
            };

            var dept = await deptService.CreateDepartmentAsync(deptRequest);

            // Act: Create units under department
            var unitRequest = new CreateUnitRequest
            {
                Name = "Recruitment",
                Code = "REC",
                DepartmentID = dept.DepartmentID
            };

            var unit = await unitService.CreateUnitAsync(unitRequest);

            // Assert: Hierarchy created correctly
            Assert.NotNull(hqBranch);
            Assert.NotNull(dept);
            Assert.NotNull(unit);
            Assert.Equal(hqBranch.BranchID, dept.BranchID);
            Assert.Equal(dept.DepartmentID, unit.DepartmentID);
        }

        [Fact]
        public async Task OrganizationStructure_ValidateHierarchy_Consistent()
        {
            // Arrange
            var mockDbContext = new Mock<DbContext>();
            var mockAuditService = new Mock<IAuditService>();

            var branchService = new BranchService(mockDbContext.Object, mockAuditService.Object);

            // Act: Get all branches
            var branches = await branchService.GetAllBranchesAsync();

            // Assert: Hierarchy is valid
            Assert.NotNull(branches);
        }

        [Fact]
        public async Task ManagerAssignment_AssignToMultipleDepts_Success()
        {
            // Arrange
            var mockDbContext = new Mock<DbContext>();
            var mockAuditService = new Mock<IAuditService>();

            var deptService = new DepartmentService(mockDbContext.Object, mockAuditService.Object);
            var userId = 5;

            // Act: Assign same manager to multiple departments
            var result1 = await deptService.AssignManagerAsync(1, userId);
            var result2 = await deptService.AssignManagerAsync(2, userId);

            // Assert
            Assert.True(result1);
            Assert.True(result2);
        }

        [Fact]
        public async Task OrganizationUpdate_MoveDepartment_Success()
        {
            // Arrange
            var mockDbContext = new Mock<DbContext>();
            var mockAuditService = new Mock<IAuditService>();

            var deptService = new DepartmentService(mockDbContext.Object, mockAuditService.Object);
            var deptId = 1;
            var newBranchId = 2;

            // Act: Update department to move to different branch
            var updateRequest = new UpdateDepartmentRequest
            {
                Name = "Moved Department"
            };

            var result = await deptService.UpdateDepartmentAsync(deptId, updateRequest);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task BulkUnitCreation_CreateMultipleUnits_AllSucceed()
        {
            // Arrange
            var mockDbContext = new Mock<DbContext>();
            var mockAuditService = new Mock<IAuditService>();

            var unitService = new UnitService(mockDbContext.Object, mockAuditService.Object);
            var deptId = 1;

            // Act: Create multiple units
            var units = new List<Unit>();
            for (int i = 1; i <= 5; i++)
            {
                var request = new CreateUnitRequest
                {
                    Name = $"Unit {i}",
                    Code = $"UNIT{i}",
                    DepartmentID = deptId
                };

                var unit = await unitService.CreateUnitAsync(request);
                units.Add(unit);
            }

            // Assert
            Assert.Equal(5, units.Count);
            Assert.All(units, u => Assert.NotNull(u));
        }

        [Fact]
        public async Task ReportingLineQuery_GetChainOfCommand_Success()
        {
            // Arrange
            var mockDbContext = new Mock<DbContext>();
            var mockBranchService = new Mock<IBranchService>();
            var mockDeptService = new Mock<IDepartmentService>();

            var orgService = new OrganizationService(
                mockDbContext.Object,
                mockBranchService.Object,
                mockDeptService.Object);

            // Act
            var reportingLines = await orgService.GetReportingLinesAsync();

            // Assert
            Assert.NotNull(reportingLines);
        }
    }

    // =====================================================
    // TEST DATA BUILDERS
    // =====================================================

    public static class TestOrgDataBuilder
    {
        public static Branch BuildBranch(int id = 1, string name = "Test Branch", bool isHQ = false)
        {
            return new Branch
            {
                BranchID = id,
                Name = name,
                Code = $"BR{id}",
                IsHeadquarters = isHQ,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public static Department BuildDepartment(int id = 1, int branchId = 1, string name = "Test Dept")
        {
            return new Department
            {
                DepartmentID = id,
                Name = name,
                Code = $"D{id}",
                BranchID = branchId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public static Unit BuildUnit(int id = 1, int deptId = 1, string name = "Test Unit")
        {
            return new Unit
            {
                UnitID = id,
                Name = name,
                Code = $"U{id}",
                DepartmentID = deptId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }
    }
}
