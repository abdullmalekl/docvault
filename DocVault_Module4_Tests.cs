using Xunit;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocVault.Tests.Module4
{
    // ========================================================================
    // DOCUMENT SERVICE TESTS
    // ========================================================================

    public class DocumentServiceTests
    {
        private readonly Mock<IDocumentRepository> _mockRepository;
        private readonly Mock<IDocumentVersionService> _mockVersionService;
        private readonly Mock<IStorageService> _mockStorage;
        private readonly Mock<IAuditService> _mockAudit;
        private readonly DocumentService _service;

        public DocumentServiceTests()
        {
            _mockRepository = new Mock<IDocumentRepository>();
            _mockVersionService = new Mock<IDocumentVersionService>();
            _mockStorage = new Mock<IStorageService>();
            _mockAudit = new Mock<IAuditService>();

            _service = new DocumentService(
                _mockRepository.Object,
                _mockVersionService.Object,
                _mockStorage.Object,
                _mockAudit.Object);
        }

        [Fact]
        public async Task CreateDocumentAsync_WithValidRequest_CreatesDocument()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var request = new CreateDocumentRequest
            {
                Title = "Test Document",
                Description = "Test Description",
                FolderId = Guid.NewGuid(),
                Classification = DocumentClassification.Internal,
                FileContent = new byte[] { 1, 2, 3, 4, 5 },
                FileName = "test.pdf",
                Tags = new List<string> { "tag1", "tag2" }
            };

            _mockRepository.Setup(x => x.GetByContentHashAsync(It.IsAny<string>())).ReturnsAsync((Document)null);
            _mockStorage.Setup(x => x.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<DocumentClassification>()))
                .ReturnsAsync("/storage/test.pdf");

            var mockVersion = new DocumentVersion { Id = Guid.NewGuid(), VersionNumber = 1 };
            _mockVersionService.Setup(x => x.CreateVersionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>()))
                .ReturnsAsync(mockVersion);

            // Act
            var result = await _service.CreateDocumentAsync(userId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Document", result.Title);
            Assert.Equal(DocumentStatus.Draft, result.Status);
            Assert.Equal(1, result.VersionCount);
            _mockRepository.Verify(x => x.CreateAsync(It.IsAny<Document>()), Times.Once);
            _mockAudit.Verify(x => x.LogDocumentCreatedAsync(userId, It.IsAny<Guid>(), "Test Document"), Times.Once);
        }

        [Fact]
        public async Task CreateDocumentAsync_WithNullTitle_ThrowsException()
        {
            var request = new CreateDocumentRequest
            {
                Title = null,
                FileContent = new byte[] { 1, 2, 3 }
            };

            await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateDocumentAsync(Guid.NewGuid(), request));
        }

        [Fact]
        public async Task CreateDocumentAsync_WithEmptyFileContent_ThrowsException()
        {
            var request = new CreateDocumentRequest
            {
                Title = "Test",
                FileContent = null
            };

            await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateDocumentAsync(Guid.NewGuid(), request));
        }

        [Fact]
        public async Task CreateDocumentAsync_WithDuplicateContent_ThrowsException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var fileContent = new byte[] { 1, 2, 3 };
            var existingDoc = new Document { Id = Guid.NewGuid(), Title = "Existing" };

            var request = new CreateDocumentRequest
            {
                Title = "New Doc",
                FileContent = fileContent,
                FileName = "test.pdf"
            };

            _mockRepository.Setup(x => x.GetByContentHashAsync(It.IsAny<string>()))
                .ReturnsAsync(existingDoc);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateDocumentAsync(userId, request));
        }

        [Fact]
        public async Task GetDocumentAsync_WithValidId_ReturnsDocument()
        {
            // Arrange
            var documentId = Guid.NewGuid();
            var doc = new Document { Id = documentId, Title = "Test" };
            _mockRepository.Setup(x => x.GetByIdAsync(documentId)).ReturnsAsync(doc);

            // Act
            var result = await _service.GetDocumentAsync(documentId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(documentId, result.Id);
            Assert.Equal("Test", result.Title);
        }

        [Fact]
        public async Task GetDocumentAsync_WithInvalidId_ThrowsException()
        {
            _mockRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Document)null);

            await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.GetDocumentAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task UpdateDocumentAsync_WithValidRequest_UpdatesDocument()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var doc = new Document { Id = documentId, Title = "Old Title", IsLocked = false };

            _mockRepository.Setup(x => x.GetByIdAsync(documentId)).ReturnsAsync(doc);

            var request = new UpdateDocumentRequest
            {
                DocumentId = documentId,
                Title = "New Title",
                Description = "New Description"
            };

            // Act
            var result = await _service.UpdateDocumentAsync(userId, request);

            // Assert
            Assert.Equal("New Title", result.Title);
            Assert.Equal("New Description", result.Description);
            _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<Document>()), Times.Once);
        }

        [Fact]
        public async Task UpdateDocumentAsync_WhenLockedByOtherUser_ThrowsException()
        {
            var userId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var doc = new Document { Id = documentId, IsLocked = true, LockedByUserId = otherUserId };

            _mockRepository.Setup(x => x.GetByIdAsync(documentId)).ReturnsAsync(doc);

            var request = new UpdateDocumentRequest { DocumentId = documentId, Title = "New Title" };

            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UpdateDocumentAsync(userId, request));
        }

        [Fact]
        public async Task LockDocumentAsync_WithValidId_LocksDocument()
        {
            var userId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var doc = new Document { Id = documentId, IsLocked = false };

            _mockRepository.Setup(x => x.GetByIdAsync(documentId)).ReturnsAsync(doc);

            var result = await _service.LockDocumentAsync(userId, documentId);

            Assert.True(result.IsLocked);
            Assert.Equal(userId, result.LockedByUserId);
            _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<Document>()), Times.Once);
        }

        [Fact]
        public async Task UnlockDocumentAsync_WhenLockedByDifferentUser_ThrowsException()
        {
            var userId = Guid.NewGuid();
            var lockingUserId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var doc = new Document { Id = documentId, IsLocked = true, LockedByUserId = lockingUserId };

            _mockRepository.Setup(x => x.GetByIdAsync(documentId)).ReturnsAsync(doc);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.UnlockDocumentAsync(userId, documentId));
        }

        [Fact]
        public async Task SearchDocumentsAsync_WithQuery_ReturnsResults()
        {
            var query = "test";
            var orgId = Guid.NewGuid();
            var docs = new List<Document>
            {
                new Document { Id = Guid.NewGuid(), Title = "Test Document" },
                new Document { Id = Guid.NewGuid(), Title = "Another Test" }
            };

            _mockRepository.Setup(x => x.SearchAsync(query, orgId)).ReturnsAsync(docs);

            var results = await _service.SearchDocumentsAsync(query, orgId);

            Assert.Equal(2, results.Count);
        }
    }

    // ========================================================================
    // DOCUMENT VERSION SERVICE TESTS
    // ========================================================================

    public class DocumentVersionServiceTests
    {
        private readonly Mock<IDocumentVersionRepository> _mockRepository;
        private readonly Mock<IStorageService> _mockStorage;
        private readonly Mock<IAuditService> _mockAudit;
        private readonly DocumentVersionService _service;

        public DocumentVersionServiceTests()
        {
            _mockRepository = new Mock<IDocumentVersionRepository>();
            _mockStorage = new Mock<IStorageService>();
            _mockAudit = new Mock<IAuditService>();

            _service = new DocumentVersionService(
                _mockRepository.Object,
                _mockStorage.Object,
                _mockAudit.Object);
        }

        [Fact]
        public async Task CreateVersionAsync_WithNoHistory_CreatesVersion1()
        {
            var userId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var content = new byte[] { 1, 2, 3, 4, 5 };

            _mockRepository.Setup(x => x.GetByDocumentIdAsync(documentId)).ReturnsAsync(new List<DocumentVersion>());
            _mockStorage.Setup(x => x.UploadVersionAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<string>()))
                .ReturnsAsync("/storage/v1");

            var result = await _service.CreateVersionAsync(userId, documentId, "file.pdf", content, "Initial");

            Assert.Equal(1, result.VersionNumber);
            Assert.Equal(VersionStatus.Current, result.Status);
        }

        [Fact]
        public async Task CreateVersionAsync_WithHistory_IncrementsVersion()
        {
            var userId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var content = new byte[] { 1, 2, 3 };

            var history = new List<DocumentVersion>
            {
                new DocumentVersion { VersionNumber = 1, Status = VersionStatus.Current },
                new DocumentVersion { VersionNumber = 2, Status = VersionStatus.Current }
            };

            _mockRepository.Setup(x => x.GetByDocumentIdAsync(documentId)).ReturnsAsync(history);
            _mockStorage.Setup(x => x.UploadVersionAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<string>()))
                .ReturnsAsync("/storage/v3");

            var result = await _service.CreateVersionAsync(userId, documentId, "file.pdf", content, "Update");

            Assert.Equal(3, result.VersionNumber);
        }

        [Fact]
        public async Task GetVersionHistoryAsync_ReturnsAllVersions()
        {
            var documentId = Guid.NewGuid();
            var versions = new List<DocumentVersion>
            {
                new DocumentVersion { VersionNumber = 1 },
                new DocumentVersion { VersionNumber = 2 },
                new DocumentVersion { VersionNumber = 3 }
            };

            _mockRepository.Setup(x => x.GetByDocumentIdAsync(documentId)).ReturnsAsync(versions);

            var result = await _service.GetVersionHistoryAsync(documentId);

            Assert.Equal(3, result.Count);
        }

        [Fact]
        public async Task RestoreVersionAsync_WithValidVersion_RestoresContent()
        {
            var userId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var versionToRestore = 2;
            var content = new byte[] { 1, 2, 3 };

            var versions = new List<DocumentVersion>
            {
                new DocumentVersion { VersionNumber = 1, FileName = "v1.pdf", StoragePath = "/storage/v1" },
                new DocumentVersion { VersionNumber = 2, FileName = "v2.pdf", StoragePath = "/storage/v2" }
            };

            _mockRepository.Setup(x => x.GetByDocumentIdAsync(documentId)).ReturnsAsync(versions);
            _mockStorage.Setup(x => x.DownloadAsync("/storage/v2")).ReturnsAsync(content);
            _mockStorage.Setup(x => x.UploadVersionAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<string>()))
                .ReturnsAsync("/storage/v3");

            var result = await _service.RestoreVersionAsync(userId, documentId, versionToRestore);

            Assert.NotNull(result);
            Assert.True(result.ChangeNotes.Contains("Restored from version"));
        }
    }

    // ========================================================================
    // DOCUMENT METADATA SERVICE TESTS
    // ========================================================================

    public class DocumentMetadataServiceTests
    {
        private readonly Mock<IDocumentMetadataRepository> _mockRepository;
        private readonly DocumentMetadataService _service;

        public DocumentMetadataServiceTests()
        {
            _mockRepository = new Mock<IDocumentMetadataRepository>();
            _service = new DocumentMetadataService(_mockRepository.Object);
        }

        [Fact]
        public async Task ExtractMetadataAsync_WithContent_ExtractsMetadata()
        {
            var documentId = Guid.NewGuid();
            var content = new byte[10000]; // Simulates ~2 page PDF

            var result = await _service.ExtractMetadataAsync(documentId, content);

            Assert.NotNull(result);
            Assert.Equal(documentId, result.DocumentId);
            Assert.Equal(2, result.PageCount);
            _mockRepository.Verify(x => x.CreateAsync(It.IsAny<DocumentMetadata>()), Times.Once);
        }

        [Fact]
        public async Task GetMetadataAsync_WithValidId_ReturnsMetadata()
        {
            var documentId = Guid.NewGuid();
            var metadata = new DocumentMetadata { DocumentId = documentId, PageCount = 5 };

            _mockRepository.Setup(x => x.GetByDocumentIdAsync(documentId)).ReturnsAsync(metadata);

            var result = await _service.GetMetadataAsync(documentId);

            Assert.NotNull(result);
            Assert.Equal(5, result.PageCount);
        }

        [Fact]
        public async Task UpdateMetadataAsync_UpdatesMetadata()
        {
            var documentId = Guid.NewGuid();
            var metadata = new DocumentMetadata { DocumentId = documentId, Author = "John Doe" };

            var result = await _service.UpdateMetadataAsync(documentId, metadata);

            Assert.Equal(documentId, result.DocumentId);
            _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<DocumentMetadata>()), Times.Once);
        }
    }

    // ========================================================================
    // INTEGRATION TESTS
    // ========================================================================

    public class DocumentManagementIntegrationTests
    {
        [Fact]
        public async Task CreateDocumentAndVersion_CreatesCompleteDocument()
        {
            // Arrange
            var mockRepository = new Mock<IDocumentRepository>();
            var mockVersionRepository = new Mock<IDocumentVersionRepository>();
            var mockVersionService = new Mock<IDocumentVersionService>();
            var mockStorage = new Mock<IStorageService>();
            var mockAudit = new Mock<IAuditService>();

            var documentService = new DocumentService(
                mockRepository.Object,
                mockVersionService.Object,
                mockStorage.Object,
                mockAudit.Object);

            var userId = Guid.NewGuid();
            var fileContent = new byte[] { 1, 2, 3, 4, 5 };

            mockRepository.Setup(x => x.GetByContentHashAsync(It.IsAny<string>())).ReturnsAsync((Document)null);
            mockStorage.Setup(x => x.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<DocumentClassification>()))
                .ReturnsAsync("/storage/test.pdf");

            var version = new DocumentVersion { Id = Guid.NewGuid(), VersionNumber = 1, Status = VersionStatus.Current };
            mockVersionService.Setup(x => x.CreateVersionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>()))
                .ReturnsAsync(version);

            // Act
            var request = new CreateDocumentRequest
            {
                Title = "Integration Test Document",
                FileName = "test.pdf",
                FileContent = fileContent,
                Classification = DocumentClassification.Confidential
            };

            var document = await documentService.CreateDocumentAsync(userId, request);

            // Assert
            Assert.NotNull(document);
            Assert.Equal("Integration Test Document", document.Title);
            Assert.Equal(1, document.VersionCount);
            Assert.Equal(DocumentStatus.Draft, document.Status);
            Assert.Equal(DocumentClassification.Confidential, document.Classification);
        }

        [Fact]
        public async Task DocumentLockingWorkflow_LocksAndUnlocks()
        {
            var mockRepository = new Mock<IDocumentRepository>();
            var mockVersionService = new Mock<IDocumentVersionService>();
            var mockStorage = new Mock<IStorageService>();
            var mockAudit = new Mock<IAuditService>();

            var service = new DocumentService(mockRepository.Object, mockVersionService.Object, mockStorage.Object, mockAudit.Object);

            var userId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var doc = new Document { Id = documentId, IsLocked = false };

            mockRepository.Setup(x => x.GetByIdAsync(documentId)).ReturnsAsync(doc);

            // Lock
            var locked = await service.LockDocumentAsync(userId, documentId);
            Assert.True(locked.IsLocked);

            // Unlock
            var unlocked = await service.UnlockDocumentAsync(userId, documentId);
            Assert.False(unlocked.IsLocked);
        }
    }
}
