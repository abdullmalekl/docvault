using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

// ============================================================================
// DOCVAULT MODULE 12: SECURITY & ENCRYPTION
// ============================================================================
// End-to-end encryption, key management, threat detection

namespace DocVault.Core.Security
{
    public class EncryptionKey
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }

        public string KeyName { get; set; }
        public string Algorithm { get; set; } // AES-256, RSA-2048
        public int KeySizeBytes { get; set; }

        public byte[] EncryptedKey { get; set; }
        public string KeyFingerprint { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? RotatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }

        public bool IsActive { get; set; }
    }

    public class SecurityAlert
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }

        public AlertType AlertType { get; set; }
        public AlertSeverity Severity { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }

        public Guid? UserId { get; set; }
        public string IpAddress { get; set; }

        public DateTime DetectedAt { get; set; }
        public bool IsResolved { get; set; }
        public string Resolution { get; set; }
    }

    public class ThreatIndicator
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }

        public string IndicatorType { get; set; } // BruteForce, UnusualAccess, DataExfiltration
        public string RiskScore { get; set; } // Low, Medium, High, Critical

        public DateTime DetectedAt { get; set; }
        public Dictionary<string, object> Evidence { get; set; } = new();
        public List<string> RecommendedActions { get; set; } = new();
    }

    public enum AlertType
    {
        UnauthorizedAccess = 0,
        BruteForceAttempt = 1,
        PermissionChange = 2,
        DataExport = 3,
        MaliciousActivity = 4,
        ComplianceViolation = 5
    }

    public enum AlertSeverity
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }

    public interface IEncryptionService
    {
        Task<byte[]> EncryptAsync(byte[] plaintext, Guid keyId);
        Task<byte[]> DecryptAsync(byte[] ciphertext, Guid keyId);
        Task<EncryptionKey> GenerateKeyAsync(Guid organizationId, string algorithm);
        Task<bool> RotateKeyAsync(Guid keyId);
        Task<List<EncryptionKey>> GetActiveKeysAsync(Guid organizationId);
    }

    public class EncryptionService : IEncryptionService
    {
        private readonly IKeyManagementRepository _repository;

        public EncryptionService(IKeyManagementRepository repository)
        {
            _repository = repository;
        }

        public async Task<byte[]> EncryptAsync(byte[] plaintext, Guid keyId)
        {
            var key = await _repository.GetKeyAsync(keyId);
            if (key == null) throw new InvalidOperationException("Key not found");

            using (var aes = Aes.Create())
            {
                aes.Key = key.EncryptedKey;
                aes.GenerateIV();

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                {
                    var buffer = new byte[aes.IV.Length + plaintext.Length + encryptor.OutputBlockSize];
                    Buffer.BlockCopy(aes.IV, 0, buffer, 0, aes.IV.Length);

                    int offset = aes.IV.Length;
                    offset += encryptor.TransformBlock(plaintext, 0, plaintext.Length, buffer, offset);
                    encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);

                    return buffer;
                }
            }
        }

        public async Task<byte[]> DecryptAsync(byte[] ciphertext, Guid keyId)
        {
            var key = await _repository.GetKeyAsync(keyId);
            if (key == null) throw new InvalidOperationException("Key not found");

            using (var aes = Aes.Create())
            {
                aes.Key = key.EncryptedKey;
                byte[] iv = new byte[aes.IV.Length];
                Buffer.BlockCopy(ciphertext, 0, iv, 0, iv.Length);
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                {
                    return decryptor.TransformFinalBlock(ciphertext, iv.Length, ciphertext.Length - iv.Length);
                }
            }
        }

        public async Task<EncryptionKey> GenerateKeyAsync(Guid organizationId, string algorithm)
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.GenerateKey();

                var key = new EncryptionKey
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    KeyName = $"{algorithm}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                    Algorithm = algorithm,
                    KeySizeBytes = 256,
                    EncryptedKey = aes.Key,
                    KeyFingerprint = ComputeFingerprint(aes.Key),
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                await _repository.SaveKeyAsync(key);
                return key;
            }
        }

        public async Task<bool> RotateKeyAsync(Guid keyId)
        {
            var oldKey = await _repository.GetKeyAsync(keyId);
            if (oldKey == null) return false;

            var newKey = await GenerateKeyAsync(oldKey.OrganizationId, oldKey.Algorithm);
            oldKey.IsActive = false;
            await _repository.UpdateKeyAsync(oldKey);

            return true;
        }

        public async Task<List<EncryptionKey>> GetActiveKeysAsync(Guid organizationId)
        {
            return await _repository.GetActiveKeysAsync(organizationId);
        }

        private string ComputeFingerprint(byte[] key)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(key);
                return Convert.ToBase64String(hash);
            }
        }
    }

    public interface IThreatDetectionService
    {
        Task<ThreatIndicator> AnalyzeAccessPatternAsync(Guid userId, Guid organizationId);
        Task<bool> DetectBruteForceAsync(Guid userId, int maxAttempts = 5);
        Task<List<SecurityAlert>> GetActiveAlertsAsync(Guid organizationId);
        Task CreateSecurityAlertAsync(SecurityAlert alert);
        Task RaiseIncidentAsync(Guid organizationId, string description);
    }

    public class ThreatDetectionService : IThreatDetectionService
    {
        private readonly IThreatDetectionRepository _repository;
        private readonly IAccessControlRepository _accessRepository;

        public ThreatDetectionService(
            IThreatDetectionRepository repository,
            IAccessControlRepository accessRepository)
        {
            _repository = repository;
            _accessRepository = accessRepository;
        }

        public async Task<ThreatIndicator> AnalyzeAccessPatternAsync(Guid userId, Guid organizationId)
        {
            var accessHistory = await _accessRepository.GetUserAccessHistoryAsync(userId);
            var baselineTime = DateTime.UtcNow.AddDays(-30);
            var recentAccesses = accessHistory.FindAll(a => a.AccessTime > baselineTime);

            var indicator = new ThreatIndicator
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                IndicatorType = "UnusualAccess",
                DetectedAt = DateTime.UtcNow,
                Evidence = new Dictionary<string, object>
                {
                    { "RecentAccessCount", recentAccesses.Count },
                    { "AnalysisWindow", "30 days" }
                }
            };

            if (recentAccesses.Count > 100)
            {
                indicator.RiskScore = "Medium";
                indicator.RecommendedActions.Add("Monitor user activity");
            }

            return indicator;
        }

        public async Task<bool> DetectBruteForceAsync(Guid userId, int maxAttempts = 5)
        {
            var failedAttempts = await _repository.GetFailedAccessAttemptsAsync(userId, TimeSpan.FromMinutes(15));
            return failedAttempts.Count >= maxAttempts;
        }

        public async Task<List<SecurityAlert>> GetActiveAlertsAsync(Guid organizationId)
        {
            return await _repository.GetActiveAlertsAsync(organizationId);
        }

        public async Task CreateSecurityAlertAsync(SecurityAlert alert)
        {
            alert.DetectedAt = DateTime.UtcNow;
            await _repository.CreateAlertAsync(alert);
        }

        public async Task RaiseIncidentAsync(Guid organizationId, string description)
        {
            var alert = new SecurityAlert
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                AlertType = AlertType.MaliciousActivity,
                Severity = AlertSeverity.Critical,
                Title = "Security Incident",
                Description = description,
                DetectedAt = DateTime.UtcNow,
                IsResolved = false
            };

            await CreateSecurityAlertAsync(alert);
        }
    }

    public interface IKeyManagementRepository
    {
        Task<EncryptionKey> GetKeyAsync(Guid keyId);
        Task SaveKeyAsync(EncryptionKey key);
        Task UpdateKeyAsync(EncryptionKey key);
        Task<List<EncryptionKey>> GetActiveKeysAsync(Guid organizationId);
    }

    public interface IThreatDetectionRepository
    {
        Task<List<dynamic>> GetFailedAccessAttemptsAsync(Guid userId, TimeSpan timeWindow);
        Task<List<SecurityAlert>> GetActiveAlertsAsync(Guid organizationId);
        Task CreateAlertAsync(SecurityAlert alert);
    }

    public interface IAccessControlRepository
    {
        Task<List<dynamic>> GetUserAccessHistoryAsync(Guid userId);
    }
}
