# DocVault Module 2 — SQL Execution Guide
**Date:** June 21, 2026 (Checkpoint 2)  
**Database:** SQL Server 2019/2022  
**Scope:** Organization Structure (Branches, Departments, Units)

---

## 📋 PRE-EXECUTION CHECKLIST

- [ ] Module 1 schema executed successfully
- [ ] SQL Server 2019 or 2022 installed
- [ ] Target database exists (e.g., `DocVault_Dev`)
- [ ] Admin credentials available
- [ ] Backup created before schema changes
- [ ] Test environment (NOT production)

---

## 🔧 EXECUTION STEPS

### Step 1: Connect to SQL Server

**Using SQL Server Management Studio (SSMS):**
```
Server: localhost
Database: DocVault_Dev
Authentication: Windows or SQL Server
```

**Using sqlcmd:**
```bash
sqlcmd -S localhost -d DocVault_Dev -U sa -P <password>
```

### Step 2: Execute Organization Schema

**Option A: SSMS GUI**
1. Open `DocVault_Module2_Organization_Tables.sql`
2. Select all (Ctrl+A)
3. Execute (F5)
4. Review execution results

**Option B: Command Line**
```bash
sqlcmd -S localhost -d DocVault_Dev -i DocVault_Module2_Organization_Tables.sql -o execution.log
```

**Option C: PowerShell**
```powershell
$query = Get-Content -Path "DocVault_Module2_Organization_Tables.sql" -Raw
Invoke-SqlCmd -ServerInstance "localhost" -Database "DocVault_Dev" -Query $query
```

---

## 📝 EXPECTED EXECUTION OUTPUT

### Tables Created (4)
```
✓ Branches (BranchID, Name, Code, IsHeadquarters, ...)
✓ Departments (DepartmentID, Name, Code, BranchID, ...)
✓ Units (UnitID, Name, Code, DepartmentID, ...)
✓ DepartmentHierarchy (HierarchyID, ChildDepartmentID, Path, ...)
```

### Indexes Created (12)
```
✓ IX_Branch_Head
✓ IX_Branch_Parent
✓ IX_Branch_Active
✓ IX_Branch_HQ
✓ IX_Dept_Branch
✓ IX_Dept_Manager
✓ IX_Dept_Active
✓ IX_Dept_Name
✓ IX_Unit_Dept
✓ IX_Unit_Supervisor
✓ IX_Unit_Active
✓ IX_Hier_Child
✓ IX_Hier_Parent
✓ IX_Hier_Depth
```

### Stored Procedures (4)
```
✓ sp_GetOrganizationHierarchy
✓ sp_GetDepartmentPath
✓ sp_GetOrganizationStatistics
✓ sp_GetDepartmentUserCount
```

### Views (2)
```
✓ vw_OrganizationStructure (branches + departments + units)
✓ vw_ReportingLines (user → department → manager hierarchy)
```

### Audit Triggers (2)
```
✓ trg_Branch_Audit (log branch changes)
✓ trg_Department_Audit (log department changes)
```

### Default Data (1)
```
✓ Headquarters branch inserted (IsHeadquarters = 1)
```

---

## 🧪 VERIFICATION QUERIES

### After execution, run these to verify setup:

**1. Count tables:**
```sql
SELECT COUNT(*) as TableCount 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_SCHEMA = 'dbo' 
  AND TABLE_NAME IN ('Branches', 'Departments', 'Units', 'DepartmentHierarchy');
-- Expected: 4
```

**2. Verify HQ branch exists:**
```sql
SELECT * FROM [dbo].[Branches] WHERE [IsHeadquarters] = 1;
-- Expected: 1 row (Headquarters, HQ, True)
```

**3. Verify indexes:**
```sql
SELECT COUNT(*) as IndexCount
FROM sys.indexes
WHERE object_id = OBJECT_ID('[dbo].[Branches]')
   OR object_id = OBJECT_ID('[dbo].[Departments]')
   OR object_id = OBJECT_ID('[dbo].[Units]');
-- Expected: 12+
```

**4. Verify stored procedures:**
```sql
SELECT name FROM sys.objects 
WHERE type = 'P' AND schema_id = SCHEMA_ID('dbo')
  AND name IN ('sp_GetOrganizationHierarchy', 
               'sp_GetDepartmentPath',
               'sp_GetOrganizationStatistics',
               'sp_GetDepartmentUserCount');
-- Expected: 4 rows
```

**5. Verify views:**
```sql
SELECT name FROM sys.objects 
WHERE type = 'V' AND schema_id = SCHEMA_ID('dbo')
  AND name IN ('vw_OrganizationStructure', 'vw_ReportingLines');
-- Expected: 2 rows
```

**6. Test HQ branch query:**
```sql
SELECT [BranchID], [Name], [Code], [IsHeadquarters]
FROM [dbo].[Branches]
WHERE [IsHeadquarters] = 1;
-- Expected: 1 row
```

**7. Test stored procedure:**
```sql
EXEC [dbo].[sp_GetOrganizationStatistics];
-- Expected: Statistics table returned
```

---

## ⚠️ COMMON ISSUES & FIXES

### Issue 1: "Invalid column name 'IsActive'"
**Cause:** Column name mismatch  
**Fix:** Check schema definition, ensure column names match

### Issue 2: "There is already an object named 'Branches'"
**Cause:** Table already exists  
**Fix:** Drop table first (if safe in dev):
```sql
DROP TABLE IF EXISTS [dbo].[Branches];
```

### Issue 3: "Foreign key constraint conflict"
**Cause:** Module 1 Users table doesn't have correct structure  
**Fix:** Ensure Module 1 is executed first and Users table has UserID PK

### Issue 4: "Cannot create index, table doesn't exist"
**Cause:** Table creation failed silently  
**Fix:** Check for errors in table creation, run table DDL separately

### Issue 5: Trigger creation fails
**Cause:** AuditLog table might not exist yet  
**Fix:** Comment out triggers temporarily, run tables/procs first:
```sql
-- CREATE TRIGGER [dbo].[trg_Branch_Audit] ... (commented out initially)
```

---

## 🔄 ROLLBACK PROCEDURE

If you need to undo the changes:

```sql
-- Drop audit triggers first
DROP TRIGGER IF EXISTS [dbo].[trg_Department_Audit];
DROP TRIGGER IF EXISTS [dbo].[trg_Branch_Audit];

-- Drop views (dependencies)
DROP VIEW IF EXISTS [dbo].[vw_ReportingLines];
DROP VIEW IF EXISTS [dbo].[vw_OrganizationStructure];

-- Drop stored procedures
DROP PROCEDURE IF EXISTS [dbo].[sp_GetDepartmentUserCount];
DROP PROCEDURE IF EXISTS [dbo].[sp_GetOrganizationStatistics];
DROP PROCEDURE IF EXISTS [dbo].[sp_GetDepartmentPath];
DROP PROCEDURE IF EXISTS [dbo].[sp_GetOrganizationHierarchy];

-- Drop tables (respects foreign keys)
DROP TABLE IF EXISTS [dbo].[DepartmentHierarchy];
DROP TABLE IF EXISTS [dbo].[Units];
DROP TABLE IF EXISTS [dbo].[Departments];
DROP TABLE IF EXISTS [dbo].[Branches];
```

---

## 📊 SCHEMA SUMMARY

**Total Database Objects:**
- 4 Tables (Branches, Departments, Units, DepartmentHierarchy)
- 12 Indexes (performance optimization)
- 4 Stored Procedures (business logic)
- 2 Views (simplified queries)
- 2 Audit Triggers (change tracking)

**Data Volume Recommendations:**
- Branches: 1 HQ + 3-5 regional
- Departments: 2-3 per branch (6-15 total)
- Units: 2-3 per department (12-45 total)
- Users: 1-10 per unit/department (30-100 total)

---

## ✅ POST-EXECUTION VERIFICATION

After SQL scripts execute successfully:

1. **Run verification queries** (see above)
2. **Test stored procedures:**
   ```sql
   EXEC [dbo].[sp_GetOrganizationStatistics];
   EXEC [dbo].[sp_GetDepartmentUserCount] @DepartmentID=1;
   ```
3. **Test views:**
   ```sql
   SELECT TOP 10 * FROM [dbo].[vw_OrganizationStructure];
   SELECT TOP 10 * FROM [dbo].[vw_ReportingLines];
   ```
4. **Insert test data** (see next section)
5. **Verify services compile** against schema
6. **Run test suite** (46 tests)

---

## 🌱 INSERT TEST DATA

After tables are created, populate with test data:

```sql
-- Create test branches
INSERT INTO [dbo].[Branches] (
    [Name], [Code], [Location], [IsActive]
) VALUES
    (N'Cairo Branch', N'CAIRO', N'Cairo, Egypt', 1),
    (N'Alexandria Branch', N'ALEX', N'Alexandria, Egypt', 1);

-- Create test departments under HQ
DECLARE @HQ_ID INT = (SELECT [BranchID] FROM [dbo].[Branches] WHERE [IsHeadquarters] = 1);

INSERT INTO [dbo].[Departments] (
    [Name], [Code], [BranchID], [IsActive]
) VALUES
    (N'Human Resources', N'HR', @HQ_ID, 1),
    (N'Finance', N'FIN', @HQ_ID, 1),
    (N'Operations', N'OPS', @HQ_ID, 1);

-- Create test units
INSERT INTO [dbo].[Units] (
    [Name], [Code], [DepartmentID], [IsActive]
) VALUES
    (N'Recruitment', N'REC', 1, 1),
    (N'Payroll', N'PAY', 1, 1),
    (N'Accounting', N'ACC', 2, 1),
    (N'Infrastructure', N'INF', 3, 1);

-- Verify
SELECT * FROM [dbo].[Branches] WHERE [IsActive] = 1;
SELECT * FROM [dbo].[Departments] WHERE [IsActive] = 1;
SELECT * FROM [dbo].[Units] WHERE [IsActive] = 1;
```

---

## 📝 NOTES FOR .NET APPLICATION

After SQL schema is ready, your .NET code can:

1. **Use Entity Framework Core** with DbContext mapping to tables
2. **Call stored procedures** via `context.Database.SqlInterpolated()`
3. **Query views** with LINQ-to-EF
4. **Execute services** that use these tables (BranchService, DepartmentService, etc.)

Example EF Core DbContext:
```csharp
public class DocVaultContext : DbContext
{
    public DbSet<Branch> Branches { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<Unit> Units { get; set; }
    public DbSet<DepartmentHierarchy> DepartmentHierarchy { get; set; }
}
```

---

## 🎯 NEXT STEPS

1. ✅ Execute SQL script (DocVault_Module2_Organization_Tables.sql)
2. ✅ Verify all tables, procedures, views created
3. ✅ Insert test data
4. ✅ Compile .NET services against schema
5. ✅ Run 40 unit tests
6. ✅ Run 6 integration tests
7. 🔄 Module 2 sign-off and release v1.0

---

**Status:** Ready for execution ✅  
**Estimated Execution Time:** 5-10 seconds  
**Database Impact:** Additive only (creates new objects)  
**Rollback Difficulty:** Easy (see rollback section)

