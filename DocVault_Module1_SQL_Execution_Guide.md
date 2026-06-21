# DocVault Module 1 — SQL Execution Guide
**Date:** June 21, 2026 (Checkpoint 2)  
**Database:** SQL Server 2019/2022  
**Scope:** Authentication & Security Module Schema

---

## 📋 PRE-EXECUTION CHECKLIST

- [ ] SQL Server 2019 or 2022 installed
- [ ] Target database created (e.g., `DocVault_Dev`, `DocVault_Test`)
- [ ] Admin credentials available
- [ ] Backup created before schema changes
- [ ] Test environment (NOT production)

---

## 🔧 EXECUTION STEPS

### Step 1: Connect to SQL Server

**Using SQL Server Management Studio (SSMS):**
```
Server: localhost (or your server)
Database: DocVault_Dev
Authentication: Windows or SQL Server
```

**Using sqlcmd:**
```bash
sqlcmd -S localhost -d DocVault_Dev -U sa -P <password>
```

---

### Step 2: Execute Authentication Schema

**Option A: SSMS GUI**
1. Open `DocVault_Module1_Auth_Tables.sql`
2. Select all (Ctrl+A)
3. Execute (F5)
4. Review execution results

**Option B: Command Line**
```bash
sqlcmd -S localhost -d DocVault_Dev -i DocVault_Module1_Auth_Tables.sql -o execution.log
```

**Option C: PowerShell**
```powershell
$query = Get-Content -Path "DocVault_Module1_Auth_Tables.sql" -Raw
Invoke-SqlCmd -ServerInstance "localhost" -Database "DocVault_Dev" -Query $query
```

---

## 📝 EXPECTED EXECUTION OUTPUT

### Tables Created (8)
```
✓ Users (UserID, Username, Email, PasswordHash, ...)
✓ Roles (RoleID, Name, IsBuiltIn, ...)
✓ Permissions (PermissionID, Resource, Action, ...)
✓ RolePermissions (RolePermissionID, RoleID, PermissionID)
✓ AuditLog (AuditID, UserID, ActionType, ...)
✓ LoginHistory (LoginID, UserID, Username, ...)
✓ PasswordHistory (PasswordHistoryID, UserID, ...)
✓ (Implicit: Departments table referenced by FK)
```

### Indexes Created (15)
```
✓ IX_User_Username
✓ IX_User_Email
✓ IX_User_Dept
✓ IX_User_Role
✓ IX_User_Active
✓ IX_User_MFA
✓ IX_User_LastLogin
✓ IX_Audit_Action
✓ IX_Audit_User
✓ IX_Audit_Timestamp
✓ IX_Audit_Success
✓ IX_LoginHist_User
✓ IX_LoginHist_Time
✓ IX_PwdHist_User
```

### Built-in Roles Inserted (4)
```
✓ Admin (RoleID: 1)
✓ Manager (RoleID: 2)
✓ Operator (RoleID: 3)
✓ Viewer (RoleID: 4)
```

### Permissions Inserted (18)
```
✓ Document: View, Create, Edit, Delete, Print, Export
✓ User: View, Create, Edit, Delete, Manage
✓ System: View, Edit, Manage
✓ Report: View, Create, Export
```

### Role-Permission Assignments
```
✓ Admin → All 18 permissions
✓ Manager → Document, Report, User (View)
✓ Operator → Document (Create, Edit, View), Report (View)
✓ Viewer → View only (Document, Report)
```

### Stored Procedures (4)
```
✓ sp_CheckUserPermission
✓ sp_GetUserPermissions
✓ sp_LogLoginAttempt
✓ sp_LockUserAccount
✓ sp_UnlockUserAccount
```

### Views (2)
```
✓ vw_UserWithRole
✓ vw_RecentLogins
```

---

## 🧪 VERIFICATION QUERIES

### After execution, run these to verify setup:

**1. Count tables:**
```sql
SELECT COUNT(*) as TableCount 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_SCHEMA = 'dbo' 
  AND TABLE_NAME IN ('Users', 'Roles', 'Permissions', 'RolePermissions', 
                      'AuditLog', 'LoginHistory', 'PasswordHistory');
-- Expected: 7 (or 8 if Departments already exists)
```

**2. Verify built-in roles:**
```sql
SELECT * FROM [dbo].[Roles] WHERE [IsBuiltIn] = 1;
-- Expected: 4 rows (Admin, Manager, Operator, Viewer)
```

**3. Count permissions:**
```sql
SELECT COUNT(*) as PermissionCount FROM [dbo].[Permissions];
-- Expected: 18
```

**4. Check Admin role permissions:**
```sql
SELECT p.Resource, p.Action 
FROM [dbo].[RolePermissions] rp
INNER JOIN [dbo].[Permissions] p ON rp.PermissionID = p.PermissionID
WHERE rp.RoleID = 1  -- Admin
ORDER BY p.Resource, p.Action;
-- Expected: 18 rows (all permissions)
```

**5. Check stored procedures:**
```sql
SELECT name FROM sys.objects 
WHERE type = 'P' AND schema_id = SCHEMA_ID('dbo')
  AND name LIKE 'sp_%';
-- Expected: 5 procedures
```

**6. Check views:**
```sql
SELECT name FROM sys.objects 
WHERE type = 'V' AND schema_id = SCHEMA_ID('dbo')
  AND name LIKE 'vw_%';
-- Expected: 2 views
```

---

## ⚠️ COMMON ISSUES & FIXES

### Issue 1: "Invalid column name 'DepartmentID'"
**Cause:** Departments table doesn't exist  
**Fix:** Create Departments table first or comment out FK constraint
```sql
-- Temporarily modify FK constraint:
-- CONSTRAINT FK_User_Dept FOREIGN KEY (DepartmentID)
--    REFERENCES [dbo].[Departments](DepartmentID) ON DELETE RESTRICT,
```

### Issue 2: "There is already an object named 'Users'"
**Cause:** Table already exists  
**Fix:** Drop table first (if safe):
```sql
DROP TABLE IF EXISTS [dbo].[Users];
```

### Issue 3: "Login failed for user"
**Cause:** Authentication error  
**Fix:** Check SQL Server credentials and database permissions

### Issue 4: Stored procedure creation fails
**Cause:** Syntax error in procedure  
**Fix:** Run procedures individually to identify issue:
```sql
CREATE PROCEDURE [dbo].[sp_CheckUserPermission] ...
GO
```

---

## 🔄 ROLLBACK PROCEDURE

If you need to undo the changes:

```sql
-- Drop views first (dependencies)
DROP VIEW IF EXISTS [dbo].[vw_RecentLogins];
DROP VIEW IF EXISTS [dbo].[vw_UserWithRole];

-- Drop procedures
DROP PROCEDURE IF EXISTS [dbo].[sp_UnlockUserAccount];
DROP PROCEDURE IF EXISTS [dbo].[sp_LockUserAccount];
DROP PROCEDURE IF EXISTS [dbo].[sp_LogLoginAttempt];
DROP PROCEDURE IF EXISTS [dbo].[sp_GetUserPermissions];
DROP PROCEDURE IF EXISTS [dbo].[sp_CheckUserPermission];

-- Drop tables (respects foreign keys)
DROP TABLE IF EXISTS [dbo].[PasswordHistory];
DROP TABLE IF EXISTS [dbo].[LoginHistory];
DROP TABLE IF EXISTS [dbo].[AuditLog];
DROP TABLE IF EXISTS [dbo].[RolePermissions];
DROP TABLE IF EXISTS [dbo].[Permissions];
DROP TABLE IF EXISTS [dbo].[Users];
DROP TABLE IF EXISTS [dbo].[Roles];
```

---

## 📊 SCHEMA SUMMARY

**Total Database Objects:**
- 8 Tables (Users, Roles, Permissions, RolePermissions, AuditLog, LoginHistory, PasswordHistory, + implicit Departments)
- 15 Indexes (performance optimization)
- 5 Stored Procedures (business logic)
- 2 Views (simplified queries)
- 18 Permissions (fine-grained access control)
- 4 Built-in Roles (role-based access control)

**Data Volume Recommendations:**
- Users: Start with 50-100 test users
- Roles: 4 built-in + custom as needed
- Permissions: 18 standard (can extend)
- AuditLog: Expect ~10-50 entries per login
- LoginHistory: Similar to audit volume

---

## ✅ POST-EXECUTION VERIFICATION

After SQL scripts execute successfully:

1. **Connect to database and run verification queries** (see above)
2. **Test stored procedures:**
   ```sql
   EXEC [dbo].[sp_CheckUserPermission] @UserID=1, @Resource='Document', @Action='View';
   ```
3. **Insert test data** (see next section)
4. **Verify all services compile** against schema
5. **Run unit tests** (xUnit test suite)

---

## 🌱 INSERT TEST DATA

After tables are created, populate with test data:

```sql
-- Create test user
INSERT INTO [dbo].[Users] (
    [Username], [Email], [PasswordHash], [DepartmentID], [RoleID],
    [IsActive], [IsMFAEnabled], [CreatedAt], [UpdatedAt]
) VALUES (
    'testuser', 'test@docvault.local', '$2a$12$...bcrypt_hash...', 1, 3,
    1, 0, GETDATE(), GETDATE()
);

-- Create test department (if needed)
INSERT INTO [dbo].[Departments] ([Name]) VALUES ('Engineering');
```

---

## 📝 NOTES FOR .NET APPLICATION

After SQL schema is ready, your .NET code can:

1. **Use Entity Framework Core** with DbContext mapping to tables
2. **Call stored procedures** via `context.Database.SqlInterpolated()`
3. **Query views** with LINQ-to-EF
4. **Execute services** that use these tables (PasswordHasher, TokenService, etc.)

---

## 🎯 NEXT STEPS

1. ✅ Execute SQL script (DocVault_Module1_Auth_Tables.sql)
2. ✅ Verify all tables, procedures, views created
3. ✅ Insert test data
4. ✅ Compile .NET services against schema
5. ✅ Run 67 unit tests
6. ✅ Run 6 integration tests
7. ✅ Run 12 security tests
8. 🔄 Module 1 sign-off and release v1.0

---

**Status:** Ready for execution ✅  
**Estimated Execution Time:** 10-30 seconds  
**Database Impact:** Additive only (creates new objects)  
**Rollback Difficulty:** Easy (see rollback section)

