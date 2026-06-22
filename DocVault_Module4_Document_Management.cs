using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Security.Cryptography;

// ============================================================================
// DOCVAULT MODULE 4: DOCUMENT MANAGEMENT SERVICES
// ============================================================================
// Core Document CRUD, Versioning, Metadata Management
// Enterprise-grade implementation with audit logging and retention policies

namespace DocVault.Core.DocumentManagement
{
    // ========================================================================
    // DATA MODELS
    // ========================================================================

    public class Document
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid CreatedByUserId { get; set; }
        public Guid FolderId { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }
        public string ContentType { get; set; }
        public long FileSizeBytes { get; set; }

        public string StoragePath { get; set; }
        public string ContentHash { get; set; } // SHA-256 for dedup

        public DocumentStatus Status { get; set; }
        public DocumentClassification Classification { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public Guid CurrentVersionId { get; set; }
        public int VersionCount { get; set; }

        public bool IsLocked { get; set; }
        public Guid LockedByUserId { get; set; }
        public DateTime LockedAt { get; set; }

        public DateTime? ExpiresAt { get; set; }
        public bool IsArchived { get; set; }

        public List<string> Tags { get; set; } = new();
        public Dictionary<string, string> CustomMetadata { get; set; } = new();
    }

    public class DocumentVersion
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public int VersionNumber { get; set; }

        public string FileName { get; set; }
        public long FileSizeBytes { get; set; }
        public string ContentHash { get; set; }

        public string ChangeNotes { get; set; }
        public Guid CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }

        public string StoragePath { get; set; }
        public VersionStatus Status { get; set; }
    }

    public class DocumentMetadata
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }

        public string Author { get; set; }
        public string Subject { get; set; }
        public string Keywords { get; set; }

        public DateTime? DocumentDate { get; set; }
        public string Language { get; set; }

        public int PageCount { get; set; }
        public Dictionary<string, string> CustomProperties { get; set; } = new();

        public DateTime ExtractedAt { get; set; }
    }

    public enum DocumentStatus
    {
        Draft = 0,
        InReview = 1,
        Approved = 2,
        Published = 3,
        Archived = 4,
        Deleted = 5
    }

    public enum DocumentClassification
    {
        Public = 0,
        Internal = 1,
        Confidential = 2,
        Secret = 3
    }

    public enum VersionStatus
    {
        Draft = 0,
        Current = 1,
        Superseded = 2,
        Archived = 3
    }

    public class CreateDocumentRequest
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public Guid FolderId { get; set; }
        public DocumentClassification Classification { get; set; }
        public byte[] FileContent { get; set; }
        public string FileName { get; set; }
        public List<string> Tags { get; set; } = new();
        public Dictionary<string, string> CustomMetadata { get; set; } = new();
    }

    public class UpdateDocumentRequest
    {
        public Guid DocumentId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DocumentClassification? Classification { get; set; }
        public List<string> Tags { get; set; }
        public Dictionary<string, string> CustomMetadata { get; set; }
    }

    // ========================================================================
    // DOCUMENT SERVICE
    // ========================================================================

    public interface IDocumentService
    {
        Task<Document> CreateDocumentAsync(Guid userId, CreateDocumentRequest request);
        Task<Document> GetDocumentAsync(Guid documentId);
        Task<List<Document>> GetDocumentsByFolderAsync(Guid folderId, int skip = 0, int take = 50);
        Task<Document> UpdateDocumentAsync(Guid userId, UpdateDocumentRequest request);
        Task DeleteDocumentAsync(Guid userId, Guid documentId, string reason);
        Task<Document> LockDocumentAsync(Guid userId, Guid documentId);
        Task<Document> UnlockDocumentAsync(Guid userId, Guid documentId);
        Task<List<Document>> SearchDocumentsAsync(string query, Guid organizationId);
    }

    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _repository;
        private readonly IDocumentVersionService _versionService;
        private readonly IStorageService _storage;
        private readonly IAuditService _audit;

        public DocumentService(
            IDocumentRepository repository,
            IDocumentVersionService versionService,
            IStorageService storage,
            IAuditService audit)
        {
            _repository = repository;
            _versionService = versionService;
            _storage = storage;
            _audit = audit;
        }

        public async Task<Document> CreateDocumentAsync(Guid userId, CreateDocumentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                throw new ArgumentException("Document title required");

            if (request.FileContent == null || request.FileContent.Length == 0)
                throw new ArgumentException("File content required");

            // Calculate content hash for deduplication
            string contentHash = CalculateSHA256(request.FileContent);

            // Check if content already exists
            var existing = await _repository.GetByContentHashAsync(contentHash);
            if (existing != null)
                throw new InvalidOperationException($"Document with identical content already exists: {existing.Id}");

            // Store file
            var storagePath = await _storage.UploadAsync(
                request.FileContent,
                request.FileName,
                request.Classification);

            var document = new Document
            {
                Id = Guid.NewGuid(),
                CreatedByUserId = userId,
                Title = request.Title,
                Description = request.Description,
                FolderId = request.FolderId,
                Classification = request.Classification,
                ContentType = GetContentType(request.FileName),
                FileSizeBytes = request.FileContent.Length,
                StoragePath = storagePath,
                ContentHash = contentHash,
                Status = DocumentStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                Tags = request.Tags,
                CustomMetadata = request.CustomMetadata
            };

            // Create initial version
            var version = await _versionService.CreateVersionAsync(
                userId,
                document.Id,
                request.FileName,
                request.FileContent,
                "Initial version");

            document.CurrentVersionId = version.Id;
            document.VersionCount = 1;

            // Persist
            await _repository.CreateAsync(document);

            // Audit
            await _audit.LogDocumentCreatedAsync(userId, document.Id, document.Title);

            return document;
        }

        public async Task<Document> GetDocumentAsync(Guid documentId)
        {
            var document = await _repository.GetByIdAsync(documentId)
                ?? throw new KeyNotFoundException($"Document not found: {documentId}");

            return document;
        }

        public async Task<List<Document>> GetDocumentsByFolderAsync(Guid folderId, int skip = 0, int take = 50)
        {
            return await _repository.GetByFolderAsync(folderId, skip, take);
        }

        public async Task<Document> UpdateDocumentAsync(Guid userId, UpdateDocumentRequest request)
        {
            var document = await GetDocumentAsync(request.DocumentId);

            if (document.IsLocked && document.LockedByUserId != userId)
                throw new InvalidOperationException("Document is locked by another user");

            document.Title = request.Title ?? document.Title;
            document.Description = request.Description ?? document.Description;
            document.Classification = request.Classification ?? document.Classification;
            document.Tags = request.Tags ?? document.Tags;
            document.CustomMetadata = request.CustomMetadata ?? document.CustomMetadata;
            document.ModifiedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(document);
            await _audit.LogDocumentModifiedAsync(userId, document.Id);

            return document;
        }

        public async Task DeleteDocumentAsync(Guid userId, Guid documentId, string reason)
        {
            var document = await GetDocumentAsync(documentId);

            document.Status = DocumentStatus.Deleted;
            document.IsArchived = true;
            document.ModifiedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(document);
            await _audit.LogDocumentDeletedAsync(userId, documentId, reason);
        }

        public async Task<Document> LockDocumentAsync(Guid userId, Guid documentId)
        {
            var document = await GetDocumentAsync(documentId);

            if (document.IsLocked && document.LockedByUserId != userId)
                throw new InvalidOperationException($"Document already locked by {document.LockedByUserId}");

            document.IsLocked = true;
            document.LockedByUserId = userId;
            document.LockedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(document);
            return document;
        }

        public async Task<Document> UnlockDocumentAsync(Guid userId, Guid documentId)
        {
            var document = await GetDocumentAsync(documentId);

            if (document.LockedByUserId != userId)
                throw new UnauthorizedAccessException("Only lock holder can unlock");

            document.IsLocked = false;
            document.LockedByUserId = Guid.Empty;
            document.LockedAt = DateTime.MinValue;

            await _repository.UpdateAsync(document);
            return document;
        }

        public async Task<List<Document>> SearchDocumentsAsync(string query, Guid organizationId)
        {
            return await _repository.SearchAsync(query, organizationId);
        }

        private string CalculateSHA256(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(data);
                return Convert.ToBase64String(hash);
            }
        }

        private string GetContentType(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLower();
            return ext switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".txt" => "text/plain",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => "application/octet-stream"
            };
        }
    }

    // ========================================================================
    // DOCUMENT VERSION SERVICE
    // ========================================================================

    public interface IDocumentVersionService
    {
        Task<DocumentVersion> CreateVersionAsync(Guid userId, Guid documentId, string fileName, byte[] content, string changeNotes);
        Task<DocumentVersion> GetVersionAsync(Guid versionId);
        Task<List<DocumentVersion>> GetVersionHistoryAsync(Guid documentId);
        Task<DocumentVersion> RestoreVersionAsync(Guid userId, Guid documentId, int versionNumber);
    }

    public class DocumentVersionService : IDocumentVersionService
    {
        private readonly IDocumentVersionRepository _repository;
        private readonly IStorageService _storage;
        private readonly IAuditService _audit;

        public DocumentVersionService(
            IDocumentVersionRepository repository,
            IStorageService storage,
            IAuditService audit)
        {
            _repository = repository;
            _storage = storage;
            _audit = audit;
        }

        public async Task<DocumentVersion> CreateVersionAsync(Guid userId, Guid documentId, string fileName, byte[] content, string changeNotes)
        {
            var history = await GetVersionHistoryAsync(documentId);
            int nextVersion = (history.Count > 0 ? history.Max(v => v.VersionNumber) : 0) + 1;

            // Mark previous version as superseded
            var currentVersion = history.FirstOrDefault(v => v.Status == VersionStatus.Current);
            if (currentVersion != null)
            {
                currentVersion.Status = VersionStatus.Superseded;
                await _repository.UpdateAsync(currentVersion);
            }

            // Store new version
            string contentHash = CalculateSHA256(content);
            var storagePath = await _storage.UploadVersionAsync(documentId, nextVersion, content, fileName);

            var version = new DocumentVersion
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                VersionNumber = nextVersion,
                FileName = fileName,
                FileSizeBytes = content.Length,
                ContentHash = contentHash,
                ChangeNotes = changeNotes,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                StoragePath = storagePath,
                Status = VersionStatus.Current
            };

            await _repository.CreateAsync(version);
            await _audit.LogVersionCreatedAsync(userId, documentId, nextVersion);

            return version;
        }

        public async Task<DocumentVersion> GetVersionAsync(Guid versionId)
        {
            return await _repository.GetByIdAsync(versionId)
                ?? throw new KeyNotFoundException($"Version not found: {versionId}");
        }

        public async Task<List<DocumentVersion>> GetVersionHistoryAsync(Guid documentId)
        {
            return await _repository.GetByDocumentIdAsync(documentId);
        }

        public async Task<DocumentVersion> RestoreVersionAsync(Guid userId, Guid documentId, int versionNumber)
        {
            var history = await GetVersionHistoryAsync(documentId);
            var targetVersion = history.FirstOrDefault(v => v.VersionNumber == versionNumber)
                ?? throw new KeyNotFoundException($"Version {versionNumber} not found");

            // Download content from storage
            var content = await _storage.DownloadAsync(targetVersion.StoragePath);

            // Create new version from restored content
            return await CreateVersionAsync(userId, documentId, targetVersion.FileName, content, $"Restored from version {versionNumber}");
        }

        private string CalculateSHA256(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(data);
                return Convert.ToBase64String(hash);
            }
        }
    }

    // ========================================================================
    // DOCUMENT METADATA SERVICE
    // ========================================================================

    public interface IDocumentMetadataService
    {
        Task<DocumentMetadata> ExtractMetadataAsync(Guid documentId, byte[] content);
        Task<DocumentMetadata> GetMetadataAsync(Guid documentId);
        Task<DocumentMetadata> UpdateMetadataAsync(Guid documentId, DocumentMetadata metadata);
    }

    public class DocumentMetadataService : IDocumentMetadataService
    {
        private readonly IDocumentMetadataRepository _repository;

        public DocumentMetadataService(IDocumentMetadataRepository repository)
        {
            _repository = repository;
        }

        public async Task<DocumentMetadata> ExtractMetadataAsync(Guid documentId, byte[] content)
        {
            var metadata = new DocumentMetadata
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                ExtractedAt = DateTime.UtcNow
            };

            // Extract basic file properties
            metadata.PageCount = EstimatePageCount(content);

            // Save
            await _repository.CreateAsync(metadata);
            return metadata;
        }

        public async Task<DocumentMetadata> GetMetadataAsync(Guid documentId)
        {
            return await _repository.GetByDocumentIdAsync(documentId)
                ?? throw new KeyNotFoundException($"Metadata not found for document: {documentId}");
        }

        public async Task<DocumentMetadata> UpdateMetadataAsync(Guid documentId, DocumentMetadata metadata)
        {
            metadata.DocumentId = documentId;
            await _repository.UpdateAsync(metadata);
            return metadata;
        }

        private int EstimatePageCount(byte[] content)
        {
            // Rough estimation based on file size and format
            // For PDF: ~5KB per page average
            if (content.Length < 5000) return 1;
            return Math.Max(1, content.Length / 5000);
        }
    }

    // ========================================================================
    // REPOSITORY INTERFACES (Data Access)
    // ========================================================================

    public interface IDocumentRepository
    {
        Task CreateAsync(Document document);
        Task<Document> GetByIdAsync(Guid id);
        Task<Document> GetByContentHashAsync(string contentHash);
        Task<List<Document>> GetByFolderAsync(Guid folderId, int skip, int take);
        Task<List<Document>> SearchAsync(string query, Guid organizationId);
        Task UpdateAsync(Document document);
        Task DeleteAsync(Guid id);
    }

    public interface IDocumentVersionRepository
    {
        Task CreateAsync(DocumentVersion version);
        Task<DocumentVersion> GetByIdAsync(Guid id);
        Task<List<DocumentVersion>> GetByDocumentIdAsync(Guid documentId);
        Task UpdateAsync(DocumentVersion version);
    }

    public interface IDocumentMetadataRepository
    {
        Task CreateAsync(DocumentMetadata metadata);
        Task<DocumentMetadata> GetByDocumentIdAsync(Guid documentId);
        Task UpdateAsync(DocumentMetadata metadata);
    }

    // ========================================================================
    // STORAGE SERVICE
    // ========================================================================

    public interface IStorageService
    {
        Task<string> UploadAsync(byte[] content, string fileName, DocumentClassification classification);
        Task<string> UploadVersionAsync(Guid documentId, int versionNumber, byte[] content, string fileName);
        Task<byte[]> DownloadAsync(string storagePath);
        Task DeleteAsync(string storagePath);
    }

    // ========================================================================
    // AUDIT SERVICE
    // ========================================================================

    public interface IAuditService
    {
        Task LogDocumentCreatedAsync(Guid userId, Guid documentId, string title);
        Task LogDocumentModifiedAsync(Guid userId, Guid documentId);
        Task LogDocumentDeletedAsync(Guid userId, Guid documentId, string reason);
        Task LogVersionCreatedAsync(Guid userId, Guid documentId, int versionNumber);
    }
}
