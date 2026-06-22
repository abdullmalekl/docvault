// =====================================================
// DocVault Module 2: Organization Structure
// Services Implementation (Departments, Branches, Units)
// Date: June 21, 2026
// =====================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace DocVault.Core.Organization
{
    // =====================================================
    // BRANCH SERVICE
    // =====================================================

    public interface IBranchService
    {
        Task<Branch> CreateBranchAsync(CreateBranchRequest request);
        Task<Branch> GetBranchAsync(int branchId);
        Task<List<Branch>> GetAllBranchesAsync();
        Task<List<Department>> GetBranchDepartmentsAsync(int branchId);
        Task<bool> UpdateBranchAsync(int branchId, UpdateBranchRequest request);
        Task<bool> DeleteBranchAsync(int branchId);
        Task<Branch> GetHeadquartersAsync();
        Task<List<Branch>> GetSubBranchesAsync(int parentBranchId);
        Task<int> GetBranchUserCountAsync(int branchId);
    }

    public class BranchService : IBranchService
    {
        private readonly DbContext _dbContext;
        private readonly IAuditService _auditService;

        public BranchService(DbContext dbContext, IAuditService auditService)
        {
            _dbContext = dbContext;
            _auditService = auditService;
        }

        public async Task<Branch> CreateBranchAsync(CreateBranchRequest request)
        {
            // Validate unique code
            var existing = await _dbContext.Branches
                .FirstOrDefaultAsync(b => b.Code == request.Code);
            if (existing != null)
                throw new InvalidOperationException($"Branch code '{request.Code}' already exists");

            var branch = new Branch
            {
                Name = request.Name,
                Code = request.Code,
                Description = request.Description,
                Location = request.Location,
                HeadUserID = request.HeadUserID,
                PhoneNumber = request.PhoneNumber,
                Email = request.Email,
                ParentBranchID = request.ParentBranchID,
                IsHeadquarters = request.IsHeadquarters,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();

            await _auditService.LogActionAsync("BranchCreated", $"Branch '{request.Name}' created");

            return branch;
        }

        public async Task<Branch> GetBranchAsync(int branchId)
        {
            return await _dbContext.Branches
                .Include(b => b.Head)
                .FirstOrDefaultAsync(b => b.BranchID == branchId && b.IsActive);
        }

        public async Task<List<Branch>> GetAllBranchesAsync()
        {
            return await _dbContext.Branches
                .Where(b => b.IsActive)
                .Include(b => b.Head)
                .OrderBy(b => b.IsHeadquarters ? 0 : 1)
                .ThenBy(b => b.Name)
                .ToListAsync();
        }

        public async Task<List<Department>> GetBranchDepartmentsAsync(int branchId)
        {
            return await _dbContext.Departments
                .Where(d => d.BranchID == branchId && d.IsActive)
                .Include(d => d.Manager)
                .OrderBy(d => d.Name)
                .ToListAsync();
        }

        public async Task<bool> UpdateBranchAsync(int branchId, UpdateBranchRequest request)
        {
            var branch = await _dbContext.Branches.FindAsync(branchId);
            if (branch == null) return false;

            var oldValues = new { branch.Name, branch.Location };

            branch.Name = request.Name ?? branch.Name;
            branch.Location = request.Location ?? branch.Location;
            branch.PhoneNumber = request.PhoneNumber ?? branch.PhoneNumber;
            branch.Email = request.Email ?? branch.Email;
            if (request.HeadUserID.HasValue)
                branch.HeadUserID = request.HeadUserID;
            if (request.IsActive.HasValue)
                branch.IsActive = request.IsActive.Value;

            branch.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            await _auditService.LogActionAsync("BranchUpdated", $"Branch '{branch.Name}' updated");

            return true;
        }

        public async Task<bool> DeleteBranchAsync(int branchId)
        {
            var branch = await _dbContext.Branches.FindAsync(branchId);
            if (branch == null) return false;

            // Don't actually delete, just mark inactive
            branch.IsActive = false;
            branch.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            await _auditService.LogActionAsync("BranchDeleted", $"Branch '{branch.Name}' deactivated");

            return true;
        }

        public async Task<Branch> GetHeadquartersAsync()
        {
            return await _dbContext.Branches
                .FirstOrDefaultAsync(b => b.IsHeadquarters && b.IsActive);
        }

        public async Task<List<Branch>> GetSubBranchesAsync(int parentBranchId)
        {
            return await _dbContext.Branches
                .Where(b => b.ParentBranchID == parentBranchId && b.IsActive)
                .OrderBy(b => b.Name)
                .ToListAsync();
        }

        public async Task<int> GetBranchUserCountAsync(int branchId)
        {
            return await _dbContext.Users
                .Where(u => u.Department.BranchID == branchId && u.IsActive)
                .CountAsync();
        }
    }

    // =====================================================
    // DEPARTMENT SERVICE
    // =====================================================

    public interface IDepartmentService
    {
        Task<Department> CreateDepartmentAsync(CreateDepartmentRequest request);
        Task<Department> GetDepartmentAsync(int departmentId);
        Task<List<Department>> GetDepartmentsByBranchAsync(int branchId);
        Task<List<Department>> GetDepartmentsByManagerAsync(int userId);
        Task<bool> UpdateDepartmentAsync(int departmentId, UpdateDepartmentRequest request);
        Task<bool> DeleteDepartmentAsync(int departmentId);
        Task<List<Unit>> GetDepartmentUnitsAsync(int departmentId);
        Task<int> GetDepartmentUserCountAsync(int departmentId);
        Task<bool> AssignManagerAsync(int departmentId, int userId);
    }

    public class DepartmentService : IDepartmentService
    {
        private readonly DbContext _dbContext;
        private readonly IAuditService _auditService;

        public DepartmentService(DbContext dbContext, IAuditService auditService)
        {
            _dbContext = dbContext;
            _auditService = auditService;
        }

        public async Task<Department> CreateDepartmentAsync(CreateDepartmentRequest request)
        {
            // Validate unique code per branch
            var existing = await _dbContext.Departments
                .FirstOrDefaultAsync(d => d.BranchID == request.BranchID && d.Code == request.Code);
            if (existing != null)
                throw new InvalidOperationException($"Code '{request.Code}' already exists in this branch");

            var department = new Department
            {
                Name = request.Name,
                Code = request.Code,
                Description = request.Description,
                BranchID = request.BranchID,
                ManagerUserID = request.ManagerUserID,
                Location = request.Location,
                PhoneNumber = request.PhoneNumber,
                Email = request.Email,
                BudgetCode = request.BudgetCode,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Departments.Add(department);
            await _dbContext.SaveChangesAsync();

            await _auditService.LogActionAsync("DepartmentCreated", $"Department '{request.Name}' created");

            return department;
        }

        public async Task<Department> GetDepartmentAsync(int departmentId)
        {
            return await _dbContext.Departments
                .Include(d => d.Branch)
                .Include(d => d.Manager)
                .FirstOrDefaultAsync(d => d.DepartmentID == departmentId && d.IsActive);
        }

        public async Task<List<Department>> GetDepartmentsByBranchAsync(int branchId)
        {
            return await _dbContext.Departments
                .Where(d => d.BranchID == branchId && d.IsActive)
                .Include(d => d.Manager)
                .OrderBy(d => d.Name)
                .ToListAsync();
        }

        public async Task<List<Department>> GetDepartmentsByManagerAsync(int userId)
        {
            return await _dbContext.Departments
                .Where(d => d.ManagerUserID == userId && d.IsActive)
                .Include(d => d.Branch)
                .OrderBy(d => d.Name)
                .ToListAsync();
        }

        public async Task<bool> UpdateDepartmentAsync(int departmentId, UpdateDepartmentRequest request)
        {
            var department = await _dbContext.Departments.FindAsync(departmentId);
            if (department == null) return false;

            department.Name = request.Name ?? department.Name;
            department.Description = request.Description ?? department.Description;
            department.Location = request.Location ?? department.Location;
            if (request.ManagerUserID.HasValue)
                department.ManagerUserID = request.ManagerUserID;
            if (request.IsActive.HasValue)
                department.IsActive = request.IsActive.Value;

            department.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            await _auditService.LogActionAsync("DepartmentUpdated", $"Department '{department.Name}' updated");

            return true;
        }

        public async Task<bool> DeleteDepartmentAsync(int departmentId)
        {
            var department = await _dbContext.Departments.FindAsync(departmentId);
            if (department == null) return false;

            department.IsActive = false;
            department.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            await _auditService.LogActionAsync("DepartmentDeleted", $"Department '{department.Name}' deactivated");

            return true;
        }

        public async Task<List<Unit>> GetDepartmentUnitsAsync(int departmentId)
        {
            return await _dbContext.Units
                .Where(u => u.DepartmentID == departmentId && u.IsActive)
                .OrderBy(u => u.Name)
                .ToListAsync();
        }

        public async Task<int> GetDepartmentUserCountAsync(int departmentId)
        {
            return await _dbContext.Users
                .Where(u => u.DepartmentID == departmentId && u.IsActive)
                .CountAsync();
        }

        public async Task<bool> AssignManagerAsync(int departmentId, int userId)
        {
            var department = await _dbContext.Departments.FindAsync(departmentId);
            if (department == null) return false;

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return false;

            department.ManagerUserID = userId;
            department.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            await _auditService.LogActionAsync("ManagerAssigned", $"User '{user.Username}' assigned as manager");

            return true;
        }
    }

    // =====================================================
    // UNIT SERVICE
    // =====================================================

    public interface IUnitService
    {
        Task<Unit> CreateUnitAsync(CreateUnitRequest request);
        Task<Unit> GetUnitAsync(int unitId);
        Task<List<Unit>> GetUnitsByDepartmentAsync(int departmentId);
        Task<List<Unit>> GetUnitsBySupervisorAsync(int userId);
        Task<bool> UpdateUnitAsync(int unitId, UpdateUnitRequest request);
        Task<bool> DeleteUnitAsync(int unitId);
        Task<List<User>> GetUnitMembersAsync(int unitId);
        Task<bool> AssignSupervisorAsync(int unitId, int userId);
    }

    public class UnitService : IUnitService
    {
        private readonly DbContext _dbContext;
        private readonly IAuditService _auditService;

        public UnitService(DbContext dbContext, IAuditService auditService)
        {
            _dbContext = dbContext;
            _auditService = auditService;
        }

        public async Task<Unit> CreateUnitAsync(CreateUnitRequest request)
        {
            var unit = new Unit
            {
                Name = request.Name,
                Code = request.Code,
                Description = request.Description,
                DepartmentID = request.DepartmentID,
                SupervisorUserID = request.SupervisorUserID,
                Location = request.Location,
                PhoneNumber = request.PhoneNumber,
                Email = request.Email,
                Responsibility = request.Responsibility,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Units.Add(unit);
            await _dbContext.SaveChangesAsync();

            await _auditService.LogActionAsync("UnitCreated", $"Unit '{request.Name}' created");

            return unit;
        }

        public async Task<Unit> GetUnitAsync(int unitId)
        {
            return await _dbContext.Units
                .Include(u => u.Department)
                .Include(u => u.Supervisor)
                .FirstOrDefaultAsync(u => u.UnitID == unitId && u.IsActive);
        }

        public async Task<List<Unit>> GetUnitsByDepartmentAsync(int departmentId)
        {
            return await _dbContext.Units
                .Where(u => u.DepartmentID == departmentId && u.IsActive)
                .Include(u => u.Supervisor)
                .OrderBy(u => u.Name)
                .ToListAsync();
        }

        public async Task<List<Unit>> GetUnitsBySupervisorAsync(int userId)
        {
            return await _dbContext.Units
                .Where(u => u.SupervisorUserID == userId && u.IsActive)
                .Include(u => u.Department)
                .OrderBy(u => u.Name)
                .ToListAsync();
        }

        public async Task<bool> UpdateUnitAsync(int unitId, UpdateUnitRequest request)
        {
            var unit = await _dbContext.Units.FindAsync(unitId);
            if (unit == null) return false;

            unit.Name = request.Name ?? unit.Name;
            unit.Description = request.Description ?? unit.Description;
            unit.Location = request.Location ?? unit.Location;
            if (request.SupervisorUserID.HasValue)
                unit.SupervisorUserID = request.SupervisorUserID;
            if (request.IsActive.HasValue)
                unit.IsActive = request.IsActive.Value;

            unit.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            await _auditService.LogActionAsync("UnitUpdated", $"Unit '{unit.Name}' updated");

            return true;
        }

        public async Task<bool> DeleteUnitAsync(int unitId)
        {
            var unit = await _dbContext.Units.FindAsync(unitId);
            if (unit == null) return false;

            unit.IsActive = false;
            unit.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            await _auditService.LogActionAsync("UnitDeleted", $"Unit '{unit.Name}' deactivated");

            return true;
        }

        public async Task<List<User>> GetUnitMembersAsync(int unitId)
        {
            // Assuming users have UnitID or we track through department
            // This is simplified - in production would need unit membership table
            var unit = await _dbContext.Units.FindAsync(unitId);
            if (unit == null) return new List<User>();

            return await _dbContext.Users
                .Where(u => u.DepartmentID == unit.DepartmentID && u.IsActive)
                .OrderBy(u => u.Username)
                .ToListAsync();
        }

        public async Task<bool> AssignSupervisorAsync(int unitId, int userId)
        {
            var unit = await _dbContext.Units.FindAsync(unitId);
            if (unit == null) return false;

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return false;

            unit.SupervisorUserID = userId;
            unit.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            await _auditService.LogActionAsync("SupervisorAssigned", $"User '{user.Username}' assigned as supervisor");

            return true;
        }
    }

    // =====================================================
    // ORGANIZATION SERVICE (High-level operations)
    // =====================================================

    public interface IOrganizationService
    {
        Task<OrganizationStructure> GetFullHierarchyAsync();
        Task<OrganizationNode> GetHierarchyNodeAsync(int departmentId);
        Task<List<OrganizationUser>> GetOrganizationUsersAsync();
        Task<OrganizationStatistics> GetStatisticsAsync();
        Task<List<ReportingLine>> GetReportingLinesAsync();
    }

    public class OrganizationService : IOrganizationService
    {
        private readonly DbContext _dbContext;
        private readonly IBranchService _branchService;
        private readonly IDepartmentService _departmentService;

        public OrganizationService(
            DbContext dbContext,
            IBranchService branchService,
            IDepartmentService departmentService)
        {
            _dbContext = dbContext;
            _branchService = branchService;
            _departmentService = departmentService;
        }

        public async Task<OrganizationStructure> GetFullHierarchyAsync()
        {
            var hq = await _branchService.GetHeadquartersAsync();
            var allBranches = await _branchService.GetAllBranchesAsync();

            var structure = new OrganizationStructure
            {
                Headquarters = BranchToResponse(hq),
                RegionalBranches = allBranches
                    .Where(b => !b.IsHeadquarters && b.ParentBranchID == null)
                    .Select(BranchToResponse)
                    .ToList(),
                DepartmentsByBranch = new Dictionary<int, List<DepartmentResponse>>()
            };

            // Load departments for each branch
            foreach (var branch in allBranches)
            {
                var depts = await _departmentService.GetDepartmentsByBranchAsync(branch.BranchID);
                structure.DepartmentsByBranch[branch.BranchID] = depts
                    .Select(d => new DepartmentResponse
                    {
                        DepartmentID = d.DepartmentID,
                        Name = d.Name,
                        Code = d.Code,
                        ManagerName = d.Manager?.Username
                    })
                    .ToList();
            }

            return structure;
        }

        public async Task<OrganizationNode> GetHierarchyNodeAsync(int departmentId)
        {
            var dept = await _departmentService.GetDepartmentAsync(departmentId);
            if (dept == null) return null;

            var units = await _departmentService.GetDepartmentUnitsAsync(departmentId);
            var userCount = await _departmentService.GetDepartmentUserCountAsync(departmentId);

            return new OrganizationNode
            {
                DepartmentID = dept.DepartmentID,
                Name = dept.Name,
                Code = dept.Code,
                ManagerName = dept.Manager?.Username,
                UnitCount = units.Count,
                UserCount = userCount,
                Units = units.Select(u => new OrganizationNode
                {
                    UnitID = u.UnitID,
                    Name = u.Name,
                    Code = u.Code
                }).ToList()
            };
        }

        public async Task<List<OrganizationUser>> GetOrganizationUsersAsync()
        {
            return await _dbContext.Users
                .Where(u => u.IsActive)
                .Include(u => u.Department)
                .Include(u => u.Department.Branch)
                .Select(u => new OrganizationUser
                {
                    UserID = u.UserID,
                    Username = u.Username,
                    Email = u.Email,
                    DepartmentID = u.DepartmentID,
                    DepartmentName = u.Department.Name,
                    BranchName = u.Department.Branch.Name,
                    Role = "User"
                })
                .OrderBy(u => u.BranchName)
                .ThenBy(u => u.DepartmentName)
                .ThenBy(u => u.Username)
                .ToListAsync();
        }

        public async Task<OrganizationStatistics> GetStatisticsAsync()
        {
            var stats = new OrganizationStatistics();

            stats.TotalBranches = await _dbContext.Branches.CountAsync(b => b.IsActive);
            stats.TotalDepartments = await _dbContext.Departments.CountAsync(d => d.IsActive);
            stats.TotalUnits = await _dbContext.Units.CountAsync(u => u.IsActive);
            stats.TotalUsers = await _dbContext.Users.CountAsync(u => u.IsActive);

            // Users by branch
            stats.UsersByBranch = await _dbContext.Branches
                .Where(b => b.IsActive)
                .Select(b => new { b.BranchID, Count = b.Departments.Count(d => d.IsActive) })
                .ToDictionaryAsync(x => x.BranchID, x => x.Count);

            return stats;
        }

        public async Task<List<ReportingLine>> GetReportingLinesAsync()
        {
            return await _dbContext.Users
                .Where(u => u.IsActive && u.Department.IsActive)
                .Include(u => u.Department)
                .Include(u => u.Department.Manager)
                .Select(u => new ReportingLine
                {
                    UserID = u.UserID,
                    Username = u.Username,
                    DepartmentName = u.Department.Name,
                    ManagerName = u.Department.Manager.Username,
                    BranchName = u.Department.Branch.Name
                })
                .OrderBy(r => r.BranchName)
                .ThenBy(r => r.DepartmentName)
                .ThenBy(r => r.Username)
                .ToListAsync();
        }

        private static BranchResponse BranchToResponse(Branch branch)
        {
            return new BranchResponse
            {
                BranchID = branch.BranchID,
                Name = branch.Name,
                Code = branch.Code,
                Location = branch.Location,
                IsHeadquarters = branch.IsHeadquarters
            };
        }
    }

    // =====================================================
    // DTOs & MODELS
    // =====================================================

    public class CreateBranchRequest
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public int? HeadUserID { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public int? ParentBranchID { get; set; }
        public bool IsHeadquarters { get; set; }
    }

    public class UpdateBranchRequest
    {
        public string Name { get; set; }
        public string Location { get; set; }
        public int? HeadUserID { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public bool? IsActive { get; set; }
    }

    public class CreateDepartmentRequest
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public string Description { get; set; }
        public int BranchID { get; set; }
        public int? ManagerUserID { get; set; }
        public string Location { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public string BudgetCode { get; set; }
    }

    public class UpdateDepartmentRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public int? ManagerUserID { get; set; }
        public string Location { get; set; }
        public bool? IsActive { get; set; }
    }

    public class CreateUnitRequest
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public string Description { get; set; }
        public int DepartmentID { get; set; }
        public int? SupervisorUserID { get; set; }
        public string Location { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public string Responsibility { get; set; }
    }

    public class UpdateUnitRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public int? SupervisorUserID { get; set; }
        public string Location { get; set; }
        public bool? IsActive { get; set; }
    }

    public class BranchResponse { public int BranchID { get; set; } public string Name { get; set; } public string Code { get; set; } public string Location { get; set; } public bool IsHeadquarters { get; set; } }
    public class DepartmentResponse { public int DepartmentID { get; set; } public string Name { get; set; } public string Code { get; set; } public string ManagerName { get; set; } }
    public class OrganizationStructure { public BranchResponse Headquarters { get; set; } public List<BranchResponse> RegionalBranches { get; set; } public Dictionary<int, List<DepartmentResponse>> DepartmentsByBranch { get; set; } }
    public class OrganizationNode { public int DepartmentID { get; set; } public int? UnitID { get; set; } public string Name { get; set; } public string Code { get; set; } public string ManagerName { get; set; } public int UnitCount { get; set; } public int UserCount { get; set; } public List<OrganizationNode> Units { get; set; } }
    public class OrganizationUser { public int UserID { get; set; } public string Username { get; set; } public string Email { get; set; } public int DepartmentID { get; set; } public string DepartmentName { get; set; } public string BranchName { get; set; } public string Role { get; set; } }
    public class OrganizationStatistics { public int TotalBranches { get; set; } public int TotalDepartments { get; set; } public int TotalUnits { get; set; } public int TotalUsers { get; set; } public Dictionary<int, int> UsersByBranch { get; set; } }
    public class ReportingLine { public int UserID { get; set; } public string Username { get; set; } public string DepartmentName { get; set; } public string ManagerName { get; set; } public string BranchName { get; set; } }

    // Database models
    public class Branch { public int BranchID { get; set; } public string Name { get; set; } public string Code { get; set; } public int? HeadUserID { get; set; } public User Head { get; set; } public int? ParentBranchID { get; set; } public bool IsHeadquarters { get; set; } public bool IsActive { get; set; } public List<Department> Departments { get; set; } = new(); public DateTime CreatedAt { get; set; } public DateTime UpdatedAt { get; set; } }
    public class Department { public int DepartmentID { get; set; } public string Name { get; set; } public string Code { get; set; } public int BranchID { get; set; } public Branch Branch { get; set; } public int? ManagerUserID { get; set; } public User Manager { get; set; } public bool IsActive { get; set; } public List<Unit> Units { get; set; } = new(); public DateTime CreatedAt { get; set; } public DateTime UpdatedAt { get; set; } }
    public class Unit { public int UnitID { get; set; } public string Name { get; set; } public string Code { get; set; } public int DepartmentID { get; set; } public Department Department { get; set; } public int? SupervisorUserID { get; set; } public User Supervisor { get; set; } public bool IsActive { get; set; } public DateTime CreatedAt { get; set; } public DateTime UpdatedAt { get; set; } }
    public class User { public int UserID { get; set; } public string Username { get; set; } public string Email { get; set; } public int? DepartmentID { get; set; } public Department Department { get; set; } public bool IsActive { get; set; } }
}
