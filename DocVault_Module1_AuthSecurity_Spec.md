# DocVault Module 1: Authentication & Security
## Complete Implementation Specification
**Version:** 1.0  
**Date:** June 21, 2026  
**Status:** DEVELOPMENT STARTED  

---

# MODULE OVERVIEW

**Module Name:** Authentication & Security  
**Priority:** CRITICAL (Blocking dependency for all other modules)  
**Complexity:** High  
**Story Points:** 48  
**Estimated Duration:** 2 weeks (10 business days)  
**Dependencies:** Database (Module 0)  
**Blocks:** All other modules  

---

# FEATURES BREAKDOWN

## 1. User Authentication (21 points)

### 1.1 Local Account Authentication
**Task:** Implement local username/password login

**Requirements:**
- Username: 3-100 characters, alphanumeric + underscore
- Password: Minimum 12 characters
  - Uppercase (A-Z)
  - Lowercase (a-z)
  - Digits (0-9)
  - Special characters (!@#$%^&*)
- Password hashing: Bcrypt with cost factor 12
- Account lockout: 5 failed attempts → 30 min lock
- Session timeout: 60 minutes idle, 8 hours max

**Database:**
- Store in Users table
- PasswordHash (never plaintext)
- FailedLoginAttempts counter
- LockedUntil timestamp

**Implementation:**
```csharp
public class AuthenticationService
{
    public async Task<LoginResult> LoginAsync(string username, string password);
    public async Task<bool> ValidatePasswordAsync(string username, string password);
    public async Task<bool> CreateUserAsync(User user, string password);
    public async Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword);
    public async Task<bool> ResetPasswordAsync(int userId, string newPassword);
}
```

### 1.2 Domain Authentication (LDAP)
**Task:** Support Windows Domain authentication

**Requirements:**
- Auto-detect if machine is domain-joined
- Fallback to local auth if not domain-joined
- LDAP queries to Active Directory
- Support both domain\username and UPN formats
- Caching to avoid constant AD queries
- Offline fallback (cache last successful login)

**Implementation:**
```csharp
public class LdapAuthenticationService
{
    public async Task<LdapUser> AuthenticateAsync(string username, string password);
    public async Task<bool> IsUserInGroupAsync(string username, string groupName);
    public void CacheCredentials(LdapUser user);
    public LdapUser GetCachedUser(string username);
}
```

### 1.3 Google Authenticator (2FA/MFA)
**Task:** Implement TOTP (Time-based One-Time Password)

**Requirements:**
- Google Authenticator compatible
- QR code generation on setup
- Backup codes (10 codes for account recovery)
- Soft 2FA: Can be bypassed with backup code
- Enforcement: Can be mandatory per role
- Time sync tolerance: ±30 seconds
- Window: 6-digit TOTP

**Implementation:**
```csharp
public class TwoFactorService
{
    public string GenerateSecret(); // Random 32-char base32
    public string GenerateQrCode(string username, string secret); // QR PNG
    public bool VerifyCode(string secret, string code); // TOTP validation
    public List<string> GenerateBackupCodes(); // 10 x 8-char codes
    public bool VerifyBackupCode(int userId, string code); // One-time use
}
```

---

## 2. Authorization & Roles (16 points)

### 2.1 Role-Based Access Control (RBAC)
**Task:** Implement 4 predefined roles + custom roles

**Predefined Roles:**
```
Admin:
  - Full system access
  - User management
  - System configuration
  - Audit logs
  - All document operations

Manager:
  - Department documents only
  - User management (within dept)
  - Reporting
  - Configuration (limited)
  - All document operations

Operator:
  - Scanning
  - Document entry
  - Basic search
  - View own documents + dept
  - Print & export

Viewer:
  - Read-only access
  - Search documents
  - View documents
  - No create/edit/delete
```

**Implementation:**
```csharp
public class RoleService
{
    public async Task<Role> CreateRoleAsync(string name, List<Permission> permissions);
    public async Task<Role> GetRoleAsync(int roleId);
    public async Task<bool> AssignRoleToUserAsync(int userId, int roleId);
    public async Task<List<Permission>> GetUserPermissionsAsync(int userId);
    public bool HasPermission(User user, string permission);
}
```

### 2.2 Permission System (Granular)
**Task:** Implement fine-grained permissions

**Permission List:**
```
Document:
  - View
  - Create
  - Edit
  - Delete
  - Print
  - Export
  - Share

User:
  - View
  - Create
  - Edit
  - Delete
  - AssignRole

System:
  - ViewConfig
  - EditConfig
  - ViewAudit
  - ManageBackup

Department:
  - View (own only)
  - View (all)
  - Create
  - Edit
```

**Implementation:**
```csharp
public class PermissionService
{
    public async Task<bool> CanUserAsync(User user, string resource, string action);
    public async Task<List<string>> GetUserActionsAsync(int userId, string resource);
    public async Task<bool> GrantPermissionAsync(int roleId, string permission);
    public async Task<bool> RevokePermissionAsync(int roleId, string permission);
}
```

### 2.3 Document-Level Access Control
**Task:** Users see only their department documents (unless granted broader access)

**Rules:**
- By default: User sees only own dept documents
- Manager: Can grant broader access to specific users
- Admin: Can access all documents
- Shared documents: Explicitly shared docs visible to recipient

**Implementation:**
```csharp
public class DocumentAccessService
{
    public async Task<List<Document>> GetVisibleDocumentsAsync(int userId);
    public async Task<bool> CanAccessDocumentAsync(int userId, long documentId);
    public async Task<bool> ShareDocumentAsync(long documentId, int userId);
}
```

---

## 3. Encryption & Security (11 points)

### 3.1 Password Storage (Bcrypt)
**Task:** Secure password hashing

**Algorithm:** Bcrypt
- Cost factor: 12 (0.5 seconds per hash)
- Salt: Auto-generated per password
- Rounds: 2^12 = 4,096 iterations

**Implementation:**
```csharp
public class PasswordHasher
{
    public string HashPassword(string password);
    public bool VerifyPassword(string password, string hash);
}
```

### 3.2 Encryption at Rest (AES-256)
**Task:** Encrypt sensitive fields

**Fields to encrypt:**
- MFASecret (Google Authenticator)
- Backup codes
- Sensitive user metadata

**Algorithm:** AES-256 CBC with HMAC
- Key management: File-based or HSM (optional)
- IV: Random 16 bytes per encryption
- MAC: HMAC-SHA256 for integrity

**Implementation:**
```csharp
public class EncryptionService
{
    public string EncryptAes256(string plaintext);
    public string DecryptAes256(string ciphertext);
    public void GenerateEncryptionKey();
    public void RotateEncryptionKey();
}
```

### 3.3 TLS/SSL for Network Communication
**Task:** Secure network traffic (for network deployment)

**Requirements:**
- TLS 1.3 minimum
- Certificate pinning (optional)
- Cipher suites: AES-256-GCM
- HSTS: Force HTTPS for API

**Implementation:**
```csharp
// Configure in Startup.cs
services.AddHttpsRedirection(options =>
{
    options.HttpsPort = 443;
});

services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});
```

---

## 4. Session Management (7 points)

### 4.1 JWT Tokens
**Task:** Stateless authentication with JWT

**Token Structure:**
- Header: Algorithm (HS256), Token type (JWT)
- Payload:
  - sub (subject): UserId
  - username: Username
  - roles: User roles
  - exp: Expiration (1 hour)
  - iat: Issued at
  - jti: JWT ID (unique)
- Signature: HMAC-SHA256

**Implementation:**
```csharp
public class TokenService
{
    public string GenerateToken(User user);
    public ClaimsPrincipal ValidateToken(string token);
    public string RefreshToken(string expiredToken);
    public bool IsTokenRevoked(string token);
    public void RevokeToken(string token); // Token blacklist
}
```

### 4.2 Session Timeout
**Task:** Automatic logout on inactivity

**Rules:**
- Idle timeout: 60 minutes
- Absolute timeout: 8 hours max
- Warning: 5 minutes before timeout
- Graceful logout: Save unsaved data

**Implementation:**
```csharp
public class SessionService
{
    public void UpdateLastActivityAsync(int userId);
    public bool IsSessionActiveAsync(int userId);
    public void InvalidateSessionAsync(int userId);
}
```

### 4.3 Token Blacklist
**Task:** Revoke tokens immediately on logout

**Storage:** In-memory cache + database fallback
- Expiration: Match token expiry time
- Cleanup: Remove expired entries

---

## 5. Audit Logging (5 points)

### 5.1 Login/Logout Audit
**Task:** Log all authentication events

**Events to log:**
- Login attempt (success/failure)
- Logout
- Password change
- Password reset
- MFA enabled/disabled
- Account locked
- Account unlocked

**Data to capture:**
- User ID
- Username
- IP address
- Timestamp
- Result (success/failure)
- Reason (if failure)
- Device name
- Browser/OS

**Implementation:**
```csharp
public class AuditService
{
    public async Task LogLoginAsync(string username, bool success, string reason = null);
    public async Task LogLogoutAsync(int userId);
    public async Task LogPasswordChangeAsync(int userId);
    public async Task LogMfaToggleAsync(int userId, bool enabled);
}
```

---

## 6. Password Policy (3 points)

### 6.1 Password Requirements
**Task:** Enforce strong passwords

**Rules:**
- Minimum length: 12 characters
- Character types: Upper, Lower, Digit, Special
- No common patterns: No sequential, no repeating
- No reuse: Last 5 passwords
- Expiration: 90 days (configurable)
- Change on first login: Required
- History: Keep last 5 passwords

**Implementation:**
```csharp
public class PasswordPolicy
{
    public ValidationResult ValidatePassword(string password);
    public bool IsPasswordExpired(DateTime lastChangeDate);
    public bool WasPasswordUsedBefore(int userId, string password);
}
```

---

# TECHNICAL ARCHITECTURE

## Class Diagram

```
┌─────────────────────────────┐
│   AuthenticationController  │
└──────────────┬──────────────┘
               │
        ┌──────┴──────┬──────────────┬─────────────┐
        │             │              │             │
┌───────▼────┐ ┌─────▼─────┐ ┌─────▼──────┐ ┌──▼──────────┐
│   Local     │ │   LDAP    │ │   Google   │ │   Session   │
│   Auth      │ │   Auth    │ │   Auth     │ │   Manager   │
└────────────┘ └───────────┘ └────────────┘ └─────────────┘
               │
        ┌──────▼──────┬──────────────┬─────────────┐
        │             │              │             │
┌───────▼────┐ ┌─────▼─────┐ ┌─────▼──────┐ ┌──▼──────────┐
│   RBAC     │ │  Permission│ │ Encryption │ │   Audit     │
│   Service  │ │  Service   │ │  Service   │ │   Service   │
└────────────┘ └───────────┘ └────────────┘ └─────────────┘
               │
        ┌──────▼──────────────────┐
        │                         │
┌───────▼────┐            ┌──────▼──────┐
│  Database  │            │  Cache      │
│   (Users)  │            │  (Session)  │
└────────────┘            └─────────────┘
```

## Data Flow Diagram

```
User Input (Username/Password)
    ↓
┌─────────────────────────┐
│ Validate Input Format   │
└────────────┬────────────┘
             ↓
    ┌────────────────────┐
    │ Local Auth?        │
    └┬─────────────────┬─┘
     │ Yes              │ No (Domain)
     ↓                  ↓
┌─────────────┐  ┌──────────────┐
│ Lookup User │  │ Query LDAP   │
│ in DB       │  │ Directory    │
└──────┬──────┘  └──────┬───────┘
       ↓                ↓
    ┌──────────────────┐
    │ Hash Password &  │
    │ Compare (Bcrypt) │
    └─────────┬────────┘
              ↓
    ┌─────────────────────┐
    │ Password Correct?   │
    └┬────────────────┬───┘
     │ Yes             │ No
     ↓                 ↓
┌──────────────┐   ┌────────────────┐
│ Check if MFA │   │ Increment      │
│ Enabled?     │   │ Failed Attempts│
└┬─────────────┘   │ Lock if >= 5   │
 │                 │ Log audit      │
 │ Yes             │ Return failure │
 ↓                 └────────────────┘
┌──────────────────┐
│ Request 2FA Code │
│ (Google Auth)    │
└──────────┬───────┘
           ↓
    ┌────────────────┐
    │ Validate TOTP  │
    └┬───────────────┘
     │ Valid
     ↓
┌──────────────────┐
│ Generate JWT     │
│ Create Session   │
│ Log login event  │
│ Reset failed     │
│ attempts counter │
└────────┬─────────┘
         ↓
    ┌──────────────┐
    │ Return Token │
    │ + User Info  │
    └──────────────┘
```

---

# IMPLEMENTATION STACK

| Component | Technology | Purpose |
|-----------|-----------|---------|
| **Authentication** | ASP.NET Core Identity | User management |
| **Password Hashing** | BCrypt.Net-Next | Bcrypt implementation |
| **JWT** | System.IdentityModel.Tokens.Jwt | Token generation |
| **LDAP** | System.DirectoryServices | AD integration |
| **2FA/TOTP** | OtpNet | Google Authenticator |
| **Encryption** | System.Security.Cryptography | AES-256 encryption |
| **Caching** | Microsoft.Extensions.Caching.Memory | Session cache |
| **Logging** | Serilog | Audit logging |

---

# TESTING STRATEGY

## Unit Tests (>80% coverage)
```
Test Classes:
├─ PasswordHasherTests (10 tests)
├─ TokenServiceTests (15 tests)
├─ RoleServiceTests (12 tests)
├─ PermissionServiceTests (12 tests)
├─ EncryptionServiceTests (8 tests)
└─ AuditServiceTests (10 tests)

Total: 67 tests
Coverage: >85%
```

## Security Tests
```
✓ SQL Injection attempts (parameterized queries)
✓ Brute force attacks (lockout mechanism)
✓ Password storage (Bcrypt verification)
✓ JWT token validation
✓ Session hijacking prevention
✓ LDAP injection prevention
✓ XSS prevention (input validation)
✓ CSRF protection
```

## Integration Tests
```
✓ Local authentication flow
✓ Domain authentication flow
✓ 2FA workflow
✓ Session timeout
✓ Token refresh
✓ Audit logging
```

---

# DELIVERABLES

## Code Deliverables
- [ ] AuthenticationService.cs (300 lines)
- [ ] LdapAuthenticationService.cs (200 lines)
- [ ] TwoFactorService.cs (250 lines)
- [ ] RoleService.cs (180 lines)
- [ ] PermissionService.cs (200 lines)
- [ ] EncryptionService.cs (150 lines)
- [ ] SessionService.cs (100 lines)
- [ ] AuditService.cs (120 lines)
- [ ] PasswordHasher.cs (80 lines)
- [ ] TokenService.cs (150 lines)

**Total: ~1,700 lines of production code**

## Database Deliverables
- [ ] Users table (with constraints)
- [ ] Roles table
- [ ] Permissions table
- [ ] RolePermissions junction table
- [ ] AuditLog table
- [ ] SessionCache table
- [ ] Indexes for performance
- [ ] Stored procedures for auth

## Test Deliverables
- [ ] 67 unit tests
- [ ] 12 security tests
- [ ] 6 integration tests
- [ ] Test data fixtures

## Documentation
- [ ] API documentation (Swagger)
- [ ] Implementation guide
- [ ] Security best practices
- [ ] Deployment checklist

---

# TIMELINE & MILESTONES

**Week 1:**
- Day 1-2: Authentication services (Local + LDAP + 2FA)
- Day 3-4: Database & token service
- Day 5: Unit tests + security tests

**Week 2:**
- Day 1-2: Integration tests + bug fixes
- Day 3: Performance testing & optimization
- Day 4: Documentation & code review
- Day 5: UAT & sign-off

---

# BLOCKING ISSUES & RISKS

**Risk: LDAP Configuration**
- Mitigation: Provide flexible LDAP settings
- Fallback: Local auth if LDAP unavailable

**Risk: Performance of Auth Checks**
- Mitigation: Cache role/permission lookups
- TTL: 15 minutes

**Risk: Token Expiration Complexity**
- Mitigation: Refresh token mechanism
- Auto-renew: When 80% expired

---

# READY FOR IMPLEMENTATION

**Status:** ✅ SPECIFICATION COMPLETE  
**Next Step:** Code implementation + Unit tests  
**Estimated Effort:** 40 hours (1 developer, 1 week)

---

