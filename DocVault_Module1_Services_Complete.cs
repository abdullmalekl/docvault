// =====================================================
// DocVault Module 1: Authentication & Security
// COMPLETE SERVICES IMPLEMENTATION
// Date: June 21, 2026 (Resumed)
// =====================================================

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.IdentityModel.Tokens;
using OtpNet;

namespace DocVault.Core.Authentication
{
    // =====================================================
    // 1. TOKEN SERVICE (JWT Implementation)
    // =====================================================

    public interface ITokenService
    {
        string GenerateToken(User user);
        string GeneratePartialToken(int userId);
        string RefreshToken(string expiredToken);
        ClaimsPrincipal ValidateToken(string token);
        bool IsTokenRevoked(string token);
        void RevokeToken(string token);
    }

    public class TokenService : ITokenService
    {
        private readonly string _secretKey;
        private readonly int _expirationMinutes = 60;  // 1 hour
        private readonly int _refreshExpirationDays = 7;
        private readonly HashSet<string> _revokedTokens = new HashSet<string>();

        public TokenService(string secretKey)
        {
            if (string.IsNullOrEmpty(secretKey) || secretKey.Length < 32)
                throw new ArgumentException("Secret key must be at least 32 characters");
            _secretKey = secretKey;
        }

        public string GenerateToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_secretKey);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("DepartmentID", user.DepartmentID.ToString()),
                new Claim("RoleID", user.RoleID.ToString()),
                new Claim("IsActive", user.IsActive.ToString())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_expirationMinutes),
                Issuer = "DocVault",
                Audience = "DocVaultUsers",
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public string GeneratePartialToken(int userId)
        {
            // Token for 2FA verification (5 minute expiry)
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_secretKey);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("IsMFAPartial", "true")
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(5),
                Issuer = "DocVault",
                Audience = "DocVaultMFA",
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public string RefreshToken(string expiredToken)
        {
            var principal = ValidateToken(expiredToken);
            if (principal == null) return null;

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return null;

            // In real implementation, would fetch user from DB
            // For now, return new token
            return GenerateToken(new User { UserID = int.Parse(userIdClaim.Value) });
        }

        public ClaimsPrincipal ValidateToken(string token)
        {
            if (IsTokenRevoked(token)) return null;

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_secretKey);

                var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = "DocVault",
                    ValidateAudience = true,
                    ValidAudiences = new[] { "DocVaultUsers", "DocVaultMFA" },
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                });

                return principal;
            }
            catch
            {
                return null;
            }
        }

        public bool IsTokenRevoked(string token)
        {
            return _revokedTokens.Contains(token);
        }

        public void RevokeToken(string token)
        {
            _revokedTokens.Add(token);
        }
    }

    // =====================================================
    // 2. TWO-FACTOR SERVICE (Google Authenticator)
    // =====================================================

    public interface ITwoFactorService
    {
        string GenerateSecret();
        string GenerateQrCode(string username, string email, string secret);
        bool VerifyCode(string secret, string code);
        List<string> GenerateBackupCodes();
        bool VerifyBackupCode(int userId, string code, IEnumerable<string> backupCodes);
    }

    public class TwoFactorService : ITwoFactorService
    {
        public string GenerateSecret()
        {
            var key = KeyGeneration.GenerateRandomKey(32);  // 256 bits
            return Base32Encoding.ToString(key);
        }

        public string GenerateQrCode(string username, string email, string secret)
        {
            var accountIdentifier = $"{email} (DocVault)";
            var issuerInformation = "DocVault";

            try
            {
                var auth = new Totp(Base32Encoding.ToBytes(secret));
                var qrCodeUri = $"otpauth://totp/{accountIdentifier}?secret={secret}&issuer={issuerInformation}";

                // In real implementation, generate QR code image
                // For now, return URI (can be encoded by frontend)
                return qrCodeUri;
            }
            catch
            {
                return null;
            }
        }

        public bool VerifyCode(string secret, string code)
        {
            if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(code))
                return false;

            if (code.Length != 6 || !code.All(char.IsDigit))
                return false;

            try
            {
                var totp = new Totp(Base32Encoding.ToBytes(secret));

                // Check current and ±1 window for time skew
                var result = totp.VerifyTotp(code, out long timeStepMatched, VerificationWindow.RfcSpecified);

                return result;
            }
            catch
            {
                return false;
            }
        }

        public List<string> GenerateBackupCodes()
        {
            var codes = new List<string>();
            var random = new Random();

            for (int i = 0; i < 10; i++)
            {
                var code = random.Next(10000000, 99999999).ToString();  // 8-digit codes
                codes.Add(code);
            }

            return codes;
        }

        public bool VerifyBackupCode(int userId, string code, IEnumerable<string> backupCodes)
        {
            if (string.IsNullOrEmpty(code)) return false;

            // Check if code exists in backup codes
            return backupCodes.Any(c => c == code);
        }
    }

    // =====================================================
    // 3. AUDIT SERVICE (Logging)
    // =====================================================

    public interface IAuditService
    {
        Task LogLoginSuccessAsync(int userId, string ipAddress);
        Task LogLoginFailureAsync(string username, string reason, string ipAddress);
        Task LogUserCreatedAsync(int userId, string username);
        Task LogPasswordChangedAsync(int userId);
        Task LogPasswordResetAsync(int userId);
        Task LogMfaToggleAsync(int userId, bool enabled);
        Task LogDocumentAccessAsync(int userId, long documentId, string action);
        Task LogPermissionChangeAsync(int userId, string resource, string permission, bool granted);
    }

    public class AuditService 
    {
        private readonly DbContext _dbContext;

        public AuditService(DbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task LogLoginSuccessAsync(int userId, string ipAddress)
        {
            var audit = new AuditLogEntry
            {
                UserID = userId,
                ActionType = "Login",
                IsSuccess = true,
                IPAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            };

            await LogAsync(audit);
        }

        public async Task LogLoginFailureAsync(string username, string reason, string ipAddress)
        {
            var audit = new AuditLogEntry
            {
                ActionType = "Login",
                IsSuccess = false,
                FailureReason = reason,
                IPAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            };

            await LogAsync(audit);
        }

        public async Task LogUserCreatedAsync(int userId, string username)
        {
            var audit = new AuditLogEntry
            {
                UserID = userId,
                ActionType = "UserCreated",
                NewValue = username,
                IsSuccess = true,
                Timestamp = DateTime.UtcNow
            };

            await LogAsync(audit);
        }

        public async Task LogPasswordChangedAsync(int userId)
        {
            var audit = new AuditLogEntry
            {
                UserID = userId,
                ActionType = "PasswordChanged",
                IsSuccess = true,
                Timestamp = DateTime.UtcNow
            };

            await LogAsync(audit);
        }

        public async Task LogPasswordResetAsync(int userId)
        {
            var audit = new AuditLogEntry
            {
                UserID = userId,
                ActionType = "PasswordReset",
                IsSuccess = true,
                Timestamp = DateTime.UtcNow
            };

            await LogAsync(audit);
        }

        public async Task LogMfaToggleAsync(int userId, bool enabled)
        {
            var audit = new AuditLogEntry
            {
                UserID = userId,
                ActionType = "MFAToggle",
                NewValue = enabled.ToString(),
                IsSuccess = true,
                Timestamp = DateTime.UtcNow
            };

            await LogAsync(audit);
        }

        public async Task LogDocumentAccessAsync(int userId, long documentId, string action)
        {
            var audit = new AuditLogEntry
            {
                UserID = userId,
                ActionType = $"Document_{action}",
                RecordID = documentId,
                IsSuccess = true,
                Timestamp = DateTime.UtcNow
            };

            await LogAsync(audit);
        }

        public async Task LogPermissionChangeAsync(int userId, string resource, string permission, bool granted)
        {
            var audit = new AuditLogEntry
            {
                UserID = userId,
                ActionType = granted ? "PermissionGranted" : "PermissionRevoked",
                NewValue = $"{resource}:{permission}",
                IsSuccess = true,
                Timestamp = DateTime.UtcNow
            };

            await LogAsync(audit);
        }

        private async Task LogAsync(AuditLogEntry entry)
        {
            try
            {
                _dbContext.AuditLogs.Add(entry);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log error but don't throw (audit failure shouldn't break operations)
                System.Diagnostics.Debug.WriteLine($"Audit logging failed: {ex.Message}");
            }
        }
    }

    // =====================================================
    // 4. ROLE SERVICE (RBAC)
    // =====================================================

    public interface IRoleService
    {
        Task<Role> CreateRoleAsync(string name, List<int> permissionIds);
        Task<Role> GetRoleAsync(int roleId);
        Task<bool> AssignRoleToUserAsync(int userId, int roleId);
        Task<List<Permission>> GetRolePermissionsAsync(int roleId);
        Task<bool> AddPermissionToRoleAsync(int roleId, int permissionId);
        Task<bool> RemovePermissionFromRoleAsync(int roleId, int permissionId);
    }

    public class RoleService : IRoleService
    {
        private readonly DbContext _dbContext;
        private readonly IAuditService _auditService;

        public RoleService(DbContext dbContext, IAuditService auditService)
        {
            _dbContext = dbContext;
            _auditService = auditService;
        }

        public async Task<Role> CreateRoleAsync(string name, List<int> permissionIds)
        {
            var role = new Role
            {
                Name = name,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _dbContext.Roles.Add(role);
            await _dbContext.SaveChangesAsync();

            // Add permissions
            foreach (var permissionId in permissionIds)
            {
                var rolePermission = new RolePermission
                {
                    RoleID = role.RoleID,
                    PermissionID = permissionId,
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.RolePermissions.Add(rolePermission);
            }

            await _dbContext.SaveChangesAsync();
            return role;
        }

        public async Task<Role> GetRoleAsync(int roleId)
        {
            return await _dbContext.Roles.FindAsync(roleId);
        }

        public async Task<bool> AssignRoleToUserAsync(int userId, int roleId)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return false;

            user.RoleID = roleId;
            user.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            return true;
        }

        public async Task<List<Permission>> GetRolePermissionsAsync(int roleId)
        {
            return await _dbContext.RolePermissions
                .Where(rp => rp.RoleID == roleId)
                .Join(_dbContext.Permissions, rp => rp.PermissionID, p => p.PermissionID,
                    (rp, p) => p)
                .ToListAsync();
        }

        public async Task<bool> AddPermissionToRoleAsync(int roleId, int permissionId)
        {
            var rolePermission = new RolePermission
            {
                RoleID = roleId,
                PermissionID = permissionId,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.RolePermissions.Add(rolePermission);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemovePermissionFromRoleAsync(int roleId, int permissionId)
        {
            var rolePermission = await _dbContext.RolePermissions
                .FirstOrDefaultAsync(rp => rp.RoleID == roleId && rp.PermissionID == permissionId);

            if (rolePermission == null) return false;

            _dbContext.RolePermissions.Remove(rolePermission);
            await _dbContext.SaveChangesAsync();
            return true;
        }
    }

    // =====================================================
    // 5. PERMISSION SERVICE (Fine-grained Access)
    // =====================================================

    public interface IPermissionService
    {
        Task<bool> CanUserAsync(int userId, string resource, string action);
        Task<List<string>> GetUserActionsAsync(int userId, string resource);
        Task<bool> GrantPermissionAsync(int roleId, string resource, string action);
        Task<bool> RevokePermissionAsync(int roleId, string resource, string action);
    }

    public class PermissionService : IPermissionService
    {
        private readonly DbContext _dbContext;

        public PermissionService(DbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<bool> CanUserAsync(int userId, string resource, string action)
        {
            var hasPermission = await _dbContext.RolePermissions
                .Where(rp => _dbContext.Users
                    .Where(u => u.UserID == userId)
                    .Any(u => u.RoleID == rp.RoleID))
                .Join(_dbContext.Permissions,
                    rp => rp.PermissionID,
                    p => p.PermissionID,
                    (rp, p) => p)
                .AnyAsync(p => p.Resource == resource && p.Action == action);

            return hasPermission;
        }

        public async Task<List<string>> GetUserActionsAsync(int userId, string resource)
        {
            return await _dbContext.RolePermissions
                .Where(rp => _dbContext.Users
                    .Where(u => u.UserID == userId)
                    .Any(u => u.RoleID == rp.RoleID))
                .Join(_dbContext.Permissions,
                    rp => rp.PermissionID,
                    p => p.PermissionID,
                    (rp, p) => p)
                .Where(p => p.Resource == resource)
                .Select(p => p.Action)
                .Distinct()
                .ToListAsync();
        }

        public async Task<bool> GrantPermissionAsync(int roleId, string resource, string action)
        {
            var permission = await _dbContext.Permissions
                .FirstOrDefaultAsync(p => p.Resource == resource && p.Action == action);

            if (permission == null)
                throw new InvalidOperationException($"Permission {resource}:{action} not found");

            var rolePermission = new RolePermission
            {
                RoleID = roleId,
                PermissionID = permission.PermissionID,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.RolePermissions.Add(rolePermission);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RevokePermissionAsync(int roleId, string resource, string action)
        {
            var permission = await _dbContext.Permissions
                .FirstOrDefaultAsync(p => p.Resource == resource && p.Action == action);

            if (permission == null) return false;

            var rolePermission = await _dbContext.RolePermissions
                .FirstOrDefaultAsync(rp => rp.RoleID == roleId && rp.PermissionID == permission.PermissionID);

            if (rolePermission == null) return false;

            _dbContext.RolePermissions.Remove(rolePermission);
            await _dbContext.SaveChangesAsync();
            return true;
        }
    }

    // =====================================================
    // 6. SESSION SERVICE (Management)
    // =====================================================

    public interface ISessionService
    {
        Task<bool> CreateSessionAsync(int userId);
        Task UpdateLastActivityAsync(int userId);
        Task<bool> IsSessionActiveAsync(int userId);
        Task<bool> InvalidateSessionAsync(int userId);
        Task<List<LoginHistory>> GetUserLoginHistoryAsync(int userId, int take = 10);
    }

    public class SessionService : ISessionService
    {
        private readonly DbContext _dbContext;
        private readonly Dictionary<int, DateTime> _sessionCache = new Dictionary<int, DateTime>();

        public async Task<bool> CreateSessionAsync(int userId)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return false;

            user.LastLoginAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            _sessionCache[userId] = DateTime.UtcNow;
            return true;
        }

        public async Task UpdateLastActivityAsync(int userId)
        {
            _sessionCache[userId] = DateTime.UtcNow;

            // Update DB every 5 minutes
            if (!_sessionCache.ContainsKey(userId) ||
                DateTime.UtcNow.Subtract(_sessionCache[userId]).TotalMinutes >= 5)
            {
                var user = await _dbContext.Users.FindAsync(userId);
                if (user != null)
                {
                    user.UpdatedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                }
            }
        }

        public async Task<bool> IsSessionActiveAsync(int userId)
        {
            // Check cache first
            if (_sessionCache.TryGetValue(userId, out var lastActivity))
            {
                var idleTime = DateTime.UtcNow.Subtract(lastActivity).TotalMinutes;
                if (idleTime > 60) // 60-minute idle timeout
                {
                    _sessionCache.Remove(userId);
                    return false;
                }
                return true;
            }

            // Check database
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return false;

            if (user.LastLoginAt.HasValue)
            {
                var idleTime = DateTime.UtcNow.Subtract(user.LastLoginAt.Value).TotalMinutes;
                return idleTime <= 60;
            }

            return false;
        }

        public async Task<bool> InvalidateSessionAsync(int userId)
        {
            _sessionCache.Remove(userId);

            var user = await _dbContext.Users.FindAsync(userId);
            if (user != null)
            {
                user.LastLoginAt = null;
                await _dbContext.SaveChangesAsync();
            }

            return true;
        }

        public async Task<List<LoginHistory>> GetUserLoginHistoryAsync(int userId, int take = 10)
        {
            return await _dbContext.LoginHistory
                .Where(lh => lh.UserID == userId)
                .OrderByDescending(lh => lh.LoginTime)
                .Take(take)
                .ToListAsync();
        }
    }

    // =====================================================
    // 7. DEPENDENCY INJECTION EXTENSIONS
    // =====================================================

    public static class AuthServiceExtensions
    {
        public static IServiceCollection AddAuthenticationServices(
            this IServiceCollection services,
            string jwtSecretKey)
        {
            services.AddScoped<IPasswordHasher, PasswordHasher>();
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddScoped<ITokenService>(sp => new TokenService(jwtSecretKey));
            services.AddScoped<ITwoFactorService, TwoFactorService>();
            services.AddScoped<IAuditService, AuditService>();
            services.AddScoped<IRoleService, RoleService>();
            services.AddScoped<IPermissionService, PermissionService>();
            services.AddScoped<ISessionService, SessionService>();

            return services;
        }
    }
}

// =====================================================
// MODEL ENTITIES (DbContext additions)
// =====================================================

/*
In DbContext:

public DbSet<User> Users { get; set; }
public DbSet<Role> Roles { get; set; }
public DbSet<Permission> Permissions { get; set; }
public DbSet<RolePermission> RolePermissions { get; set; }
public DbSet<AuditLogEntry> AuditLogs { get; set; }
public DbSet<LoginHistory> LoginHistory { get; set; }
public DbSet<PasswordHistory> PasswordHistory { get; set; }
*/
