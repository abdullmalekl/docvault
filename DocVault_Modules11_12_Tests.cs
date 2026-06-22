using Xunit;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DocVault.Tests.Modules11_12
{
    public class MigrationServiceTests
    {
        private readonly Mock<IMigrationRepository> _mockRepository;
        private readonly Mock<IMigrationRunner> _mockRunner;
        private readonly MigrationService _service;

        public MigrationServiceTests()
        {
            _mockRepository = new Mock<IMigrationRepository>();
            _mockRunner = new Mock<IMigrationRunner>();
            _mockRunner.Setup(x => x.TargetVersion).Returns(2);
            _mockRunner.Setup(x => x.MigrationName).Returns("Test Migration");

            var migrations = new List<IMigrationRunner> { _mockRunner.Object };
            _service = new MigrationService(_mockRepository.Object, migrations);
        }

        [Fact]
        public async Task ExecuteMigrationAsync_WithValidVersion_MigratesSuccessfully()
        {
            _mockRepository.Setup(x => x.GetCurrentVersionAsync()).ReturnsAsync(1);
            _mockRunner.Setup(x => x.ExecuteAsync()).Returns(Task.CompletedTask);

            var result = await _service.ExecuteMigrationAsync(2);

            Assert.True(result);
            _mockRunner.Verify(x => x.ExecuteAsync(), Times.Once);
        }

        [Fact]
        public async Task GetCurrentSchemaVersionAsync_ReturnsVersion()
        {
            _mockRepository.Setup(x => x.GetCurrentVersionAsync()).ReturnsAsync(3);

            var result = await _service.GetCurrentSchemaVersionAsync();

            Assert.Equal(3, result);
        }

        [Fact]
        public async Task GetMigrationHistoryAsync_ReturnsLogs()
        {
            var logs = new List<MigrationLog>
            {
                new MigrationLog { MigrationVersion = 1, IsSuccessful = true }
            };

            _mockRepository.Setup(x => x.GetMigrationHistoryAsync(50)).ReturnsAsync(logs);

            var result = await _service.GetMigrationHistoryAsync();

            Assert.Single(result);
        }

        [Fact]
        public async Task RollbackMigrationAsync_WithValidVersions_RollsBack()
        {
            _mockRunner.Setup(x => x.RollbackAsync()).ReturnsAsync(true);

            var result = await _service.RollbackMigrationAsync(2, 1);

            Assert.True(result);
        }

        [Fact]
        public async Task ValidateMigrationAsync_WithValidVersion_ReturnsTrue()
        {
            _mockRunner.Setup(x => x.ValidateAsync()).ReturnsAsync(true);

            var result = await _service.ValidateMigrationAsync(2);

            Assert.True(result);
        }
    }

    public class EncryptionServiceTests
    {
        private readonly Mock<IKeyManagementRepository> _mockRepository;
        private readonly EncryptionService _service;

        public EncryptionServiceTests()
        {
            _mockRepository = new Mock<IKeyManagementRepository>();
            _service = new EncryptionService(_mockRepository.Object);
        }

        [Fact]
        public async Task GenerateKeyAsync_WithValidParams_GeneratesKey()
        {
            var orgId = Guid.NewGuid();

            var key = await _service.GenerateKeyAsync(orgId, "AES-256");

            Assert.NotNull(key);
            Assert.Equal(orgId, key.OrganizationId);
            Assert.Equal("AES-256", key.Algorithm);
            Assert.True(key.IsActive);
        }

        [Fact]
        public async Task EncryptAsync_WithValidData_EncryptsSuccessfully()
        {
            var keyId = Guid.NewGuid();
            var plaintext = System.Text.Encoding.UTF8.GetBytes("Hello World");

            var key = new EncryptionKey
            {
                Id = keyId,
                EncryptedKey = new byte[32]
            };

            _mockRepository.Setup(x => x.GetKeyAsync(keyId)).ReturnsAsync(key);

            var ciphertext = await _service.EncryptAsync(plaintext, keyId);

            Assert.NotEmpty(ciphertext);
        }

        [Fact]
        public async Task RotateKeyAsync_WithValidKeyId_RotatesKey()
        {
            var keyId = Guid.NewGuid();
            var orgId = Guid.NewGuid();

            var oldKey = new EncryptionKey
            {
                Id = keyId,
                OrganizationId = orgId,
                Algorithm = "AES-256",
                IsActive = true
            };

            _mockRepository.Setup(x => x.GetKeyAsync(keyId)).ReturnsAsync(oldKey);
            _mockRepository.Setup(x => x.SaveKeyAsync(It.IsAny<EncryptionKey>())).Returns(Task.CompletedTask);
            _mockRepository.Setup(x => x.UpdateKeyAsync(It.IsAny<EncryptionKey>())).Returns(Task.CompletedTask);

            var result = await _service.RotateKeyAsync(keyId);

            Assert.True(result);
            Assert.False(oldKey.IsActive);
        }

        [Fact]
        public async Task GetActiveKeysAsync_ReturnsActiveKeys()
        {
            var orgId = Guid.NewGuid();
            var keys = new List<EncryptionKey>
            {
                new EncryptionKey { IsActive = true }
            };

            _mockRepository.Setup(x => x.GetActiveKeysAsync(orgId)).ReturnsAsync(keys);

            var result = await _service.GetActiveKeysAsync(orgId);

            Assert.Single(result);
        }
    }

    public class ThreatDetectionServiceTests
    {
        private readonly Mock<IThreatDetectionRepository> _mockRepository;
        private readonly Mock<IAccessControlRepository> _mockAccessRepository;
        private readonly ThreatDetectionService _service;

        public ThreatDetectionServiceTests()
        {
            _mockRepository = new Mock<IThreatDetectionRepository>();
            _mockAccessRepository = new Mock<IAccessControlRepository>();
            _service = new ThreatDetectionService(_mockRepository.Object, _mockAccessRepository.Object);
        }

        [Fact]
        public async Task AnalyzeAccessPatternAsync_WithHighActivity_ReturnsMediumRisk()
        {
            var userId = Guid.NewGuid();
            var orgId = Guid.NewGuid();

            var accesses = new List<dynamic>();
            for (int i = 0; i < 110; i++)
            {
                accesses.Add(new { AccessTime = DateTime.UtcNow.AddDays(-1) });
            }

            _mockAccessRepository.Setup(x => x.GetUserAccessHistoryAsync(userId))
                .ReturnsAsync(accesses);

            var indicator = await _service.AnalyzeAccessPatternAsync(userId, orgId);

            Assert.Equal("Medium", indicator.RiskScore);
        }

        [Fact]
        public async Task DetectBruteForceAsync_WithMultipleFailures_ReturnsTrue()
        {
            var userId = Guid.NewGuid();
            var failures = new List<dynamic>
            {
                new { }, new { }, new { }, new { }, new { }, new { }
            };

            _mockRepository.Setup(x => x.GetFailedAccessAttemptsAsync(userId, It.IsAny<TimeSpan>()))
                .ReturnsAsync(failures);

            var result = await _service.DetectBruteForceAsync(userId);

            Assert.True(result);
        }

        [Fact]
        public async Task GetActiveAlertsAsync_ReturnsAlerts()
        {
            var orgId = Guid.NewGuid();
            var alerts = new List<SecurityAlert>
            {
                new SecurityAlert { Severity = AlertSeverity.High, IsResolved = false }
            };

            _mockRepository.Setup(x => x.GetActiveAlertsAsync(orgId)).ReturnsAsync(alerts);

            var result = await _service.GetActiveAlertsAsync(orgId);

            Assert.Single(result);
        }

        [Fact]
        public async Task RaiseIncidentAsync_CreatesAlert()
        {
            var orgId = Guid.NewGuid();

            await _service.RaiseIncidentAsync(orgId, "Suspicious activity detected");

            _mockRepository.Verify(x => x.CreateAlertAsync(It.Is<SecurityAlert>(
                a => a.Severity == AlertSeverity.Critical &&
                a.AlertType == AlertType.MaliciousActivity)), Times.Once);
        }
    }
}
