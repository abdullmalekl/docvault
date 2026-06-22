// =====================================================
// DocVault Module 1: Authentication & Security
// Core Implementation
// Date: June 21, 2026
// =====================================================

using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace DocVault.Core.Authentication
{
    // =====================================================
    // 1. PASSWORD HASHER (BCrypt Implementation)
    // =====================================================

    public interface IPasswordHasher
    {
        string HashPassword(string password);
        bool VerifyPassword(string password, string hash);
        ValidationResult ValidatePasswordPolicy(string password);
    }

    public class PasswordHasher : IPasswordHasher
    {
        private const int BcryptCost = 12;  // 2^12 = 4096 iterations (~0.5 seconds)

        // Password policy constants
        private const int MinimumLength = 12;
        private const int MaxPasswordAge = 90;  // Days

        public string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be empty", nameof(password));

            // BCrypt automatically generates salt and includes it in hash
            return BCrypt.Net.BCrypt.HashPassword(password, BcryptCost);
        }

        public bool VerifyPassword(string password, string hash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
                return false;

            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch
            {
                return false;
            }
        }

        public ValidationResult ValidatePasswordPolicy(string password)
        {
            var result = new ValidationResult();

            // Check length
            if (string.IsNullOrWhiteSpace(password))
            {
                result.AddError("Password cannot be empty");
                return result;
            }

            if (password.Length < MinimumLength)
            {
                result.AddError($"Password must be at least {MinimumLength} characters");
            }

            // Check character types
            bool hasUppercase = password.Any(char.IsUpper);
            bool hasLowercase = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);
            bool hasSpecial = password.Any(c => !char.IsLetterOrDigit(c));

            if (!hasUppercase)
                result.AddError("Password must contain at least one uppercase letter");
            if (!hasLowercase)
                result.AddError("Password must contain at least one lowercase letter");
            if (!hasDigit)
                result.AddError("Password must contain at least one digit");
            if (!hasSpecial)
                result.AddError("Password must contain at least one special character (!@#$%^&*)");

            // Check common patterns (basic)
            if (HasCommonPattern(password))
                result.AddError("Password contains common patterns (sequential, repeating)");

            result.IsValid = result.Errors.Count == 0;
            return result;
        }

        private bool HasCommonPattern(string password)
        {
            // Check for sequential characters (abc, 123, etc)
            for (int i = 0; i < password.Length - 2; i++)
            {
                if (password[i + 1] == password[i] + 1 &&
                    password[i + 2] == password[i + 1] + 1)
                {
                    return true;
                }
            }

            // Check for repeating characters (aaa, 111, etc)
            for (int i = 0; i < password.Length - 2; i++)
            {
                if (password[i] == password[i + 1] && password[i + 1] == password[i + 2])
                {
                    return true;
                }
            }

            return false;
        }
    }

    // =====================================================
    // 2. AUTHENTICATION SERVICE (Local & LDAP Support)
    // =====================================================

    public interface IAuthenticationService
    {
        Task<AuthenticationResult> LoginAsync(LoginRequest request);
        Task<AuthenticationResult> LoginWithLdapAsync(string username, string password);
        Task<bool> CreateUserAsync(CreateUserRequest request);
        Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword);
        Task<bool> ResetPasswordAsync(int userId, string newPassword);
        Task<bool> UnlockAccountAsync(int userId);
    }

    public class AuthenticationService : IAuthenticationService
    {
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenService _tokenService;
        private readonly IAuditService _auditService;
        private readonly DbContext _dbContext;

        public AuthenticationService(
            IPasswordHasher passwordHasher,
            ITokenService tokenService,
            IAuditService auditService,
            DbContext dbContext)
        {
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
            _auditService = auditService;
            _dbContext = dbContext;
        }

        public async Task<AuthenticationResult> LoginAsync(LoginRequest request)
        {
            var result = new AuthenticationResult();

            // Validate input
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                result.Success = false;
                result.Message = "Username and password are required";
                return result;
            }

            // Lookup user (case-insensitive)
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.ToLower());

            if (user == null)
            {
                result.Success = false;
                result.Message = "Invalid username or password";
                await _auditService.LogLoginFailureAsync(request.Username, "User not found", request.IpAddress);
                return result;
            }

            // Check if account is locked
            if (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow)
            {
                result.Success = false;
                result.Message = $"Account locked until {user.LockedUntil:g}";
                await _auditService.LogLoginFailureAsync(request.Username, "Account locked", request.IpAddress);
                return result;
            }

            // Check if account is active
            if (!user.IsActive)
            {
                result.Success = false;
                result.Message = "Account is disabled";
                await _auditService.LogLoginFailureAsync(request.Username, "Account disabled", request.IpAddress);
                return result;
            }

            // Verify password
            if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
            {
                user.FailedLoginAttempts++;

                // Lock account after 5 failed attempts
                if (user.FailedLoginAttempts >= 5)
                {
                    user.LockedUntil = DateTime.UtcNow.AddMinutes(30);
                }

                await _dbContext.SaveChangesAsync();
                result.Success = false;
                result.Message = "Invalid username or password";
                await _auditService.LogLoginFailureAsync(
                    request.Username,
                    $"Invalid password (attempt {user.FailedLoginAttempts})",
                    request.IpAddress);
                return result;
            }

            // Password correct - check if 2FA is enabled
            if (user.IsMFAEnabled)
            {
                // Return partial token for 2FA challenge
                result.Success = true;
                result.Message = "2FA required";
                result.RequiresMFA = true;
                result.PartialToken = _tokenService.GeneratePartialToken(user.UserID);
                return result;
            }

            // Generate full token
            result.Success = true;
            result.Message = "Login successful";
            result.Token = _tokenService.GenerateToken(user);
            result.User = new UserResponse
            {
                UserID = user.UserID,
                Username = user.Username,
                Email = user.Email,
                DepartmentID = user.DepartmentID
            };

            // Reset failed attempts and update last login
            user.FailedLoginAttempts = 0;
            user.LockedUntil = null;
            user.LastLoginAt = DateTime.UtcNow;
            user.LastLoginIP = request.IpAddress;
            await _dbContext.SaveChangesAsync();

            // Log successful login
            await _auditService.LogLoginSuccessAsync(user.UserID, request.IpAddress);

            return result;
        }

        public async Task<AuthenticationResult> LoginWithLdapAsync(string username, string password)
        {
            var result = new AuthenticationResult();

            // TODO: Implement LDAP authentication
            // 1. Query AD/LDAP server
            // 2. If successful, lookup/create user in DB
            // 3. Return token

            result.Success = false;
            result.Message = "LDAP authentication not yet implemented";
            return result;
        }

        public async Task<bool> CreateUserAsync(CreateUserRequest request)
        {
            try
            {
                // Validate password policy
                var passwordValidation = _passwordHasher.ValidatePasswordPolicy(request.Password);
                if (!passwordValidation.IsValid)
                {
                    throw new InvalidOperationException(
                        $"Password policy violation: {string.Join(", ", passwordValidation.Errors)}");
                }

                var user = new User
                {
                    Username = request.Username.ToLower().Trim(),
                    Email = request.Email,
                    PasswordHash = _passwordHasher.HashPassword(request.Password),
                    DepartmentID = request.DepartmentID,
                    RoleID = request.RoleID,
                    IsActive = true,
                    IsMFAEnabled = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _dbContext.Users.Add(user);
                await _dbContext.SaveChangesAsync();

                await _auditService.LogUserCreatedAsync(user.UserID, request.Username);
                return true;
            }
            catch (Exception ex)
            {
                // Log error
                return false;
            }
        }

        public async Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
        {
            try
            {
                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null) return false;

                // Verify old password
                if (!_passwordHasher.VerifyPassword(oldPassword, user.PasswordHash))
                {
                    return false;
                }

                // Validate new password policy
                var validation = _passwordHasher.ValidatePasswordPolicy(newPassword);
                if (!validation.IsValid)
                {
                    throw new InvalidOperationException(
                        $"New password does not meet policy: {string.Join(", ", validation.Errors)}");
                }

                // Update password
                user.PasswordHash = _passwordHasher.HashPassword(newPassword);
                user.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                await _auditService.LogPasswordChangedAsync(userId);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ResetPasswordAsync(int userId, string newPassword)
        {
            try
            {
                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null) return false;

                user.PasswordHash = _passwordHasher.HashPassword(newPassword);
                user.UpdatedAt = DateTime.UtcNow;
                user.FailedLoginAttempts = 0;
                user.LockedUntil = null;
                await _dbContext.SaveChangesAsync();

                await _auditService.LogPasswordResetAsync(userId);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UnlockAccountAsync(int userId)
        {
            try
            {
                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null) return false;

                user.LockedUntil = null;
                user.FailedLoginAttempts = 0;
                await _dbContext.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    // =====================================================
    // 3. PLACEHOLDER INTERFACES (To be implemented)
    // =====================================================

    public interface ITokenService
    {
        string GenerateToken(User user);
        string GeneratePartialToken(int userId);
    }

    public interface IAuditService
    {
        Task LogLoginSuccessAsync(int userId, string ipAddress);
        Task LogLoginFailureAsync(string username, string reason, string ipAddress);
        Task LogUserCreatedAsync(int userId, string username);
        Task LogPasswordChangedAsync(int userId);
        Task LogPasswordResetAsync(int userId);
    }

    // =====================================================
    // 4. DTOs & MODELS
    // =====================================================

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string IpAddress { get; set; }
        public string DeviceName { get; set; }
    }

    public class AuthenticationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Token { get; set; }
        public string PartialToken { get; set; }
        public bool RequiresMFA { get; set; }
        public UserResponse User { get; set; }
    }

    public class UserResponse
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public int DepartmentID { get; set; }
    }

    public class CreateUserRequest
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public int DepartmentID { get; set; }
        public int RoleID { get; set; }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();

        public void AddError(string error)
        {
            Errors.Add(error);
        }
    }

    // =====================================================
    // 5. DEPENDENCY INJECTION SETUP
    // =====================================================

    public static class AuthenticationServiceExtensions
    {
        public static IServiceCollection AddAuthenticationServices(this IServiceCollection services)
        {
            services.AddScoped<IPasswordHasher, PasswordHasher>();
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            // Add other services: ITokenService, IAuditService, etc.
            return services;
        }
    }
}

// =====================================================
// USAGE IN CONTROLLER
// =====================================================

/*
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;

    public AuthController(IAuthenticationService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthenticationResult>> Login([FromBody] LoginRequest request)
    {
        request.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        request.DeviceName = HttpContext.Request.Headers["User-Agent"];

        var result = await _authService.LoginAsync(request);

        if (!result.Success)
            return Unauthorized(result);

        return Ok(result);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<bool>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        var success = await _authService.ChangePasswordAsync(
            int.Parse(userId.Value),
            request.OldPassword,
            request.NewPassword);

        if (!success)
            return BadRequest("Failed to change password");

        return Ok(true);
    }
}
*/
