using Xunit;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocVault.Tests.Module5
{
    // ========================================================================
    // DOCUMENT ACCESS CONTROL TESTS
    // ========================================================================

    public class DocumentAccessControlServiceTests
    {
        private readonly Mock<IDocumentPermissionRepository> _mockPermissionRepo;
        private readonly Mock<IDocumentAccessLogRepository> _mockAuditRepo;
        private readonly Mock<IUserRoleRepository> _mockUserRoleRepo;
        private readonly Mock<IDocumentRepository> _mockDocumentRepo;
        private readonly DocumentAccessControlService _service;

        public DocumentAccessControlServiceTests()
        {
            _mockPermissionRepo = new Mock<IDocumentPermissionRepository>();
            _mockAuditRepo = new Mock<IDocumentAccessLogRepository>();
            _mockUserRoleRepo = new Mock<IUserRoleRepository>();
            _mockDocumentRepo = new Mock<IDocumentRepository>();

            _service = new DocumentAccessControlService(
                _mockPermissionRepo.Object,
                _mockAuditRepo.Object,
                _mockUserRoleRepo.Object,
                _mockDocumentRepo.Object);
        }

        [Fact]
        public async Task CanUserAccessDocumentAsync_DocumentOwner_ReturnsTrue()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var doc = new Document { Id = documentId, CreatedByUserId = userId };

            _mockDocumentRepo.Setup(x => x.GetByIdAsync(documentId)).ReturnsAsync(doc);

            // Act
            var result = await _service.CanUserAccessDocumentAsync(userId, documentId, DocumentAction.Edit);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task CanUserAccessDocumentAsync_WithDirectPermission_ReturnsTrue()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var doc = new Document { Id = documentId, CreatedByUserId = ownerId };

            _mockDocumentRepo.Setup(x => x.GetByIdAsync(documentId)).ReturnsAsync(doc);

            var access = new DocumentAccessEntry
            {
                UserId = userId,
                DocumentId = documentId,
                CanView = true,
                CanDownload = true,
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            };

            _mockPermissionRepo.Setup(x => x.GetByUserIdAsync(userId, documentId)).ReturnsAsync(access);

            // Act
            var result = await _service.CanUserAccessDocumentAsync(userId, documentId, DocumentAction.Download);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task CanUserAccessDocumentAsync_WithExpiredPermission_ReturnsFalse()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var doc = new Document { Id = documentId, CreatedByUserId = ownerId };

            _mockDocumentRepo.Setup(x => x.GetByIdAsync(documentId)).ReturnsAsync(doc);

            var expiredAccess = new DocumentAccessEntry
            {
                UserId = userId,
                DocumentId = documentId,
                CanView = true,
                ExpiresAt = DateTime.UtcNow.AddDays(-1) // Expired
            };

            _mockPermissionRepo.Setup(x => x.GetByUserIdAsync(userId, documentId)).ReturnsAsync(expiredAccess);
            _mockUserRoleRepo.Setup(x => x.GetUserRolesAsync(userId)).ReturnsAsync(new List<Role>());

            // Act
            var result = await _service.CanUserAccessDocumentAsync(userId, documentId, DocumentAction.View);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task CanUserAccessDocumentAsync_WithRolePermission_ReturnsTrue()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var roleId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var documentId = Guid.NewGuid();

            var doc = new Document { Id = documentId, CreatedByUserId = ownerId };
            _mockDocumentRepo.Setup(x => x.GetByIdAsync(documentId)).ReturnsAsync(doc);

            // No direct permission
            _mockPermissionRepo.Setup(x => x.GetByUserIdAsync(userId, documentId)).ReturnsAsync((DocumentAccessEntry)null);

            // Has role
            var role = new Role { Id = roleId, Name = "Reviewer" };
            _mockUserRoleRepo.Setup(x => x.GetUserRolesAsync(userId)).ReturnsAsync(new List<Role> { role });

            // Role has permission
            var roleAccess = new DocumentAccessEntry
            {
                CanView = true,
                ExpiresAt = null
            };
            _mockPermissionRepo.Setup(x => x.GetByRoleIdAsync(roleId, documentId)).ReturnsAsync(roleAccess);

            // Act
            var result = await _service.CanUserAccessDocumentAsync(userId, documentId, DocumentAction.View);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task GrantAccessAsync_WithoutSharePermission_ThrowsException()
        {
            var granterId = Guid.NewGuid();
            var targetUserId = Guid.NewGuid();
            var documentId = Guid.NewGuid();

            _mockDocumentRepo.Setup(x => x.GetByIdAsync(documentId))
                .ReturnsAsync(new Document { Id = documentId, CreatedByUserId = Guid.NewGuid() });
            _mockPermissionRepo.Setup(x => x.GetByUserIdAsync(granterId, documentId)).ReturnsAsync((DocumentAccessEntry)null);
            _mockUserRoleRepo.Setup(x => x.GetUserRolesAsync(granterId)).ReturnsAsync(new List<Role>());

            var access = new DocumentAccessEntry { CanView = true };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.GrantAccessAsync(granterId, documentId, targetUserId, access));
        }

        [Fact]
        public async Task GrantAccessAsync_WithPermission_GrantsAccess()
        {
            // Arrange
            var granterId = Guid.NewGuid();
            var targetUserId = Guid.NewGuid();
            var documentId = Guid.NewGuid();

            var doc = new Document { Id = documentId, CreatedByUserId = granterId };
            _mockDocumentRepo.Setup(x => x.GetByIdAsync(documentId)).ReturnsAsync(doc);

            var accessToGrant = new DocumentAccessEntry
            {
                CanView = true,
                CanDownload = true,
                CanEdit = false,
                CanDelete = false
            };

            // Act
            var result = await _service.GrantAccessAsync(granterId, documentId, targetUserId, accessToGrant);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(targetUserId, result.UserId);
            Assert.True(result.CanView);
            Assert.True(result.CanDownload);
            _mockPermissionRepo.Verify(x => x.CreateAsync(It.IsAny<DocumentAccessEntry>()), Times.Once);
        }

        [Fact]
        public async Task RevokeAccessAsync_RemovesAccess()
        {
            var revokerId = Guid.NewGuid();
            var targetUserId = Guid.NewGuid();
            var documentId = Guid.NewGuid();

            var doc = new Document { Id = documentId, CreatedByUserId = revokerId };
            _mockDocumentRepo.Setup(x => x.GetByIdAsync(documentId)).ReturnsAsync(doc);

            await _service.RevokeAccessAsync(revokerId, documentId, targetUserId);

            _mockPermissionRepo.Verify(x => x.DeleteAsync(documentId, targetUserId), Times.Once);
            _mockAuditRepo.Verify(x => x.LogAsync(It.IsAny<DocumentAccessLog>()), Times.Once);
        }

        [Fact]
        public async Task ShareDocumentAsync_SharesWithMultipleUsers()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var doc = new Document { Id = Guid.NewGuid(), CreatedByUserId = userId };

            _mockDocumentRepo.Setup(x => x.GetByIdAsync(doc.Id)).ReturnsAsync(doc);

            var shareRequest = new DocumentShareRequest
            {
                DocumentId = doc.Id,
                UserIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() },
                CanView = true,
                CanDownload = false
            };

            // Act
            await _service.ShareDocumentAsync(userId, shareRequest);

            // Assert
            _mockPermissionRepo.Verify(x => x.CreateAsync(It.IsAny<DocumentAccessEntry>()), Times.Exactly(2));
        }

        [Fact]
        public async Task GetAccessibleDocumentsAsync_ReturnsCombinedList()
        {
            var userId = Guid.NewGuid();

            var directDocs = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            _mockPermissionRepo.Setup(x => x.GetAccessibleDocumentsForUserAsync(userId)).ReturnsAsync(directDocs);

            var role = new Role { Id = Guid.NewGuid() };
            _mockUserRoleRepo.Setup(x => x.GetUserRolesAsync(userId)).ReturnsAsync(new List<Role> { role });

            var roleDocs = new List<Guid> { Guid.NewGuid(), directDocs[0] }; // One overlap
            _mockPermissionRepo.Setup(x => x.GetAccessibleDocumentsForRoleAsync(role.Id)).ReturnsAsync(roleDocs);

            var result = await _service.GetAccessibleDocumentsAsync(userId);

            Assert.Equal(3, result.Count);
        }
    }

    // ========================================================================
    // DOCUMENT AUDIT SERVICE TESTS
    // ========================================================================

    public class DocumentAuditServiceTests
    {
        private readonly Mock<IDocumentAccessLogRepository> _mockRepository;
        private readonly DocumentAuditService _service;

        public DocumentAuditServiceTests()
        {
            _mockRepository = new Mock<IDocumentAccessLogRepository>();
            _service = new DocumentAuditService(_mockRepository.Object);
        }

        [Fact]
        public async Task LogAccessAttemptAsync_LogsSuccessfulAccess()
        {
            var documentId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            await _service.LogAccessAttemptAsync(documentId, userId, DocumentAction.View, true);

            _mockRepository.Verify(x => x.LogAsync(It.Is<DocumentAccessLog>(
                log => log.DocumentId == documentId &&
                log.UserId == userId &&
                log.Action == DocumentAction.View.ToString() &&
                log.Success == true)), Times.Once);
        }

        [Fact]
        public async Task LogAccessAttemptAsync_LogsFailedAccess()
        {
            var documentId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            await _service.LogAccessAttemptAsync(documentId, userId, DocumentAction.Edit, false, "No permission");

            _mockRepository.Verify(x => x.LogAsync(It.Is<DocumentAccessLog>(
                log => log.Success == false &&
                log.DenyReason == "No permission")), Times.Once);
        }

        [Fact]
        public async Task GetAccessHistoryAsync_ReturnsRecentLogs()
        {
            var documentId = Guid.NewGuid();
            var logs = new List<DocumentAccessLog>
            {
                new DocumentAccessLog { Action = "View", Timestamp = DateTime.UtcNow },
                new DocumentAccessLog { Action = "Download", Timestamp = DateTime.UtcNow.AddHours(-1) }
            };

            _mockRepository.Setup(x => x.GetByDocumentIdAsync(documentId, It.IsAny<DateTime>()))
                .ReturnsAsync(logs);

            var result = await _service.GetAccessHistoryAsync(documentId);

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetUserAccessHistoryAsync_ReturnsUserLogs()
        {
            var userId = Guid.NewGuid();
            var logs = new List<DocumentAccessLog>
            {
                new DocumentAccessLog { UserId = userId, Action = "View" },
                new DocumentAccessLog { UserId = userId, Action = "Download" }
            };

            _mockRepository.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<DateTime>()))
                .ReturnsAsync(logs);

            var result = await _service.GetUserAccessHistoryAsync(userId);

            Assert.Equal(2, result.Count);
        }
    }

    // ========================================================================
    // DOCUMENT RETENTION SERVICE TESTS
    // ========================================================================

    public class DocumentRetentionServiceTests
    {
        private readonly Mock<IDocumentRetentionRepository> _mockPolicyRepo;
        private readonly Mock<IDocumentRepository> _mockDocumentRepo;
        private readonly DocumentRetentionService _service;

        public DocumentRetentionServiceTests()
        {
            _mockPolicyRepo = new Mock<IDocumentRetentionRepository>();
            _mockDocumentRepo = new Mock<IDocumentRepository>();
            _service = new DocumentRetentionService(_mockPolicyRepo.Object, _mockDocumentRepo.Object);
        }

        [Fact]
        public async Task CreateRetentionPolicyAsync_CreatesPolicy()
        {
            var orgId = Guid.NewGuid();
            var policy = new DocumentRetentionPolicy
            {
                OrganizationId = orgId,
                RetentionDays = 365,
                ActionOnExpiry = RetentionAction.Archive
            };

            var result = await _service.CreateRetentionPolicyAsync(orgId, policy);

            Assert.NotNull(result);
            Assert.Equal(orgId, result.OrganizationId);
            Assert.True(result.IsActive);
            _mockPolicyRepo.Verify(x => x.CreateAsync(It.IsAny<DocumentRetentionPolicy>()), Times.Once);
        }

        [Fact]
        public async Task ApplyRetentionPoliciesAsync_ArchivesExpiredDocuments()
        {
            var expiredDocId = Guid.NewGuid();
            var policies = new List<DocumentRetentionPolicy> { new DocumentRetentionPolicy { IsActive = true } };

            _mockPolicyRepo.Setup(x => x.GetActiveAsync()).ReturnsAsync(policies);
            _mockPolicyRepo.Setup(x => x.GetDocumentsExceedingRetentionAsync(RetentionAction.Archive))
                .ReturnsAsync(new List<Guid> { expiredDocId });
            _mockPolicyRepo.Setup(x => x.GetDocumentsExceedingRetentionAsync(RetentionAction.Delete))
                .ReturnsAsync(new List<Guid>());

            var doc = new Document { Id = expiredDocId, IsArchived = false };
            _mockDocumentRepo.Setup(x => x.GetByIdAsync(expiredDocId)).ReturnsAsync(doc);

            await _service.ApplyRetentionPoliciesAsync();

            _mockDocumentRepo.Verify(x => x.UpdateAsync(It.Is<Document>(
                d => d.IsArchived && d.Status == DocumentStatus.Archived)), Times.Once);
        }
    }

    // ========================================================================
    // INTEGRATION TESTS
    // ========================================================================

    public class DocumentAccessIntegrationTests
    {
        [Fact]
        public async Task CompleteAccessWorkflow_GrantRevokeAndAudit()
        {
            // Arrange
            var mockPermRepo = new Mock<IDocumentPermissionRepository>();
            var mockAuditRepo = new Mock<IDocumentAccessLogRepository>();
            var mockRoleRepo = new Mock<IUserRoleRepository>();
            var mockDocRepo = new Mock<IDocumentRepository>();

            var service = new DocumentAccessControlService(
                mockPermRepo.Object,
                mockAuditRepo.Object,
                mockRoleRepo.Object,
                mockDocRepo.Object);

            var ownerId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var documentId = Guid.NewGuid();

            var doc = new Document { Id = documentId, CreatedByUserId = ownerId };
            mockDocRepo.Setup(x => x.GetByIdAsync(documentId)).ReturnsAsync(doc);

            // Act - Grant Access
            var access = new DocumentAccessEntry { CanView = true, CanDownload = true };
            var granted = await service.GrantAccessAsync(ownerId, documentId, userId, access);

            // Assert - Access Granted
            Assert.NotNull(granted);

            // Setup for revoke
            mockPermRepo.Setup(x => x.GetByUserIdAsync(userId, documentId)).ReturnsAsync(granted);

            // Act - Check Access
            mockRoleRepo.Setup(x => x.GetUserRolesAsync(userId)).ReturnsAsync(new List<Role>());
            var canAccess = await service.CanUserAccessDocumentAsync(userId, documentId, DocumentAction.View);

            // Assert - Can Access
            Assert.True(canAccess);

            // Act - Revoke Access
            await service.RevokeAccessAsync(ownerId, documentId, userId);

            // Assert - Revoke Called
            mockPermRepo.Verify(x => x.DeleteAsync(documentId, userId), Times.Once);
        }
    }
}
