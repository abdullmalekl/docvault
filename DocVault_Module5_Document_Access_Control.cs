using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// ============================================================================
// DOCVAULT MODULE 5: DOCUMENT ACCESS CONTROL
// ============================================================================
// Permission validation, role-based access, audit logging, retention policies

namespace DocVault.Core.DocumentAccess
{
    // ========================================================================
    // DATA MODELS
    // ========================================================================

    public class DocumentAccessEntry
    {
        public Guid UserId { get; set; }
        public Guid DocumentId { get; set; }

        public bool CanView { get; set; }
        public bool CanDownload { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public bool CanShare { get; set; }

        public DateTime GrantedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public Guid GrantedByUserId { get; set; }
    }

    public class DocumentAccessLog
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public Guid UserId { get; set; }

        public string Action { get; set; } // View, Download, Edit, Delete, Share
        public DateTime Timestamp { get; set; }
        public string IpAddress { get; set; }
        public bool Success { get; set; }
        public string DenyReason { get; set; }
    }

    public class DocumentRetentionPolicy
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid FolderId { get; set; }

        public int RetentionDays { get; set; }
        public RetentionAction ActionOnExpiry { get; set; } // Archive, Delete, Notify
        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
        public Guid CreatedByUserId { get; set; }
    }

    public enum RetentionAction
    {
        Archive = 0,
        Delete = 1,
        Notify = 2
    }

    public class DocumentShareRequest
    {
        public Guid DocumentId { get; set; }
        public List<Guid> UserIds { get; set; } = new();
        public List<Guid> RoleIds { get; set; } = new();

        public bool CanView { get; set; } = true;
        public bool CanDownload { get; set; } = false;
        public bool CanEdit { get; set; } = false;
        public bool CanDelete { get; set; } = false;

        public DateTime? ExpiresAt { get; set; }
        public string ShareMessage { get; set; }
    }

    // ========================================================================
    // DOCUMENT ACCESS CONTROL SERVICE
    // ========================================================================

    public interface IDocumentAccessControlService
    {
        Task<bool> CanUserAccessDocumentAsync(Guid userId, Guid documentId, DocumentAction action);
        Task<DocumentAccessEntry> GrantAccessAsync(Guid grantedByUserId, Guid documentId, Guid targetUserId, DocumentAccessEntry access);
        Task RevokeAccessAsync(Guid revokedByUserId, Guid documentId, Guid targetUserId);
        Task<List<DocumentAccessEntry>> GetDocumentAccessListAsync(Guid documentId);
        Task<List<Guid>> GetAccessibleDocumentsAsync(Guid userId);
        Task ShareDocumentAsync(Guid userId, DocumentShareRequest request);
        Task<bool> IsAccessExpiredAsync(Guid documentId, Guid userId);
    }

    public enum DocumentAction
    {
        View = 0,
        Download = 1,
        Edit = 2,
        Delete = 3,
        Share = 4
    }

    public class DocumentAccessControlService : IDocumentAccessControlService
    {
        private readonly IDocumentPermissionRepository _permissionRepository;
        private readonly IDocumentAccessLogRepository _auditRepository;
        private readonly IUserRoleRepository _userRoleRepository;
        private readonly IDocumentRepository _documentRepository;

        public DocumentAccessControlService(
            IDocumentPermissionRepository permissionRepository,
            IDocumentAccessLogRepository auditRepository,
            IUserRoleRepository userRoleRepository,
            IDocumentRepository documentRepository)
        {
            _permissionRepository = permissionRepository;
            _auditRepository = auditRepository;
            _userRoleRepository = userRoleRepository;
            _documentRepository = documentRepository;
        }

        public async Task<bool> CanUserAccessDocumentAsync(Guid userId, Guid documentId, DocumentAction action)
        {
            // Check if user owns the document
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document == null)
                return false;

            if (document.CreatedByUserId == userId)
                return true; // Owner has all permissions

            // Get user's direct permissions
            var directPermissions = await _permissionRepository.GetByUserIdAsync(userId, documentId);
            if (directPermissions != null && !IsAccessExpired(directPermissions))
            {
                if (HasPermission(directPermissions, action))
                    return true;
            }

            // Check role-based permissions
            var userRoles = await _userRoleRepository.GetUserRolesAsync(userId);
            foreach (var role in userRoles)
            {
                var rolePermissions = await _permissionRepository.GetByRoleIdAsync(role.Id, documentId);
                if (rolePermissions != null && !IsAccessExpired(rolePermissions))
                {
                    if (HasPermission(rolePermissions, action))
                        return true;
                }
            }

            return false;
        }

        public async Task<DocumentAccessEntry> GrantAccessAsync(Guid grantedByUserId, Guid documentId, Guid targetUserId, DocumentAccessEntry access)
        {
            // Verify granter has permission to grant access
            if (!await CanUserAccessDocumentAsync(grantedByUserId, documentId, DocumentAction.Share))
                throw new UnauthorizedAccessException("You don't have permission to share this document");

            // Create access entry
            var entry = new DocumentAccessEntry
            {
                UserId = targetUserId,
                DocumentId = documentId,
                CanView = access.CanView,
                CanDownload = access.CanDownload,
                CanEdit = access.CanEdit,
                CanDelete = access.CanDelete,
                CanShare = access.CanShare,
                GrantedAt = DateTime.UtcNow,
                ExpiresAt = access.ExpiresAt,
                GrantedByUserId = grantedByUserId
            };

            // Persist
            await _permissionRepository.CreateAsync(entry);

            // Log
            await _auditRepository.LogAsync(new DocumentAccessLog
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                UserId = targetUserId,
                Action = "AccessGranted",
                Timestamp = DateTime.UtcNow,
                Success = true
            });

            return entry;
        }

        public async Task RevokeAccessAsync(Guid revokedByUserId, Guid documentId, Guid targetUserId)
        {
            // Verify revoker has permission
            if (!await CanUserAccessDocumentAsync(revokedByUserId, documentId, DocumentAction.Share))
                throw new UnauthorizedAccessException("You don't have permission to revoke access");

            // Revoke
            await _permissionRepository.DeleteAsync(documentId, targetUserId);

            // Log
            await _auditRepository.LogAsync(new DocumentAccessLog
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                UserId = targetUserId,
                Action = "AccessRevoked",
                Timestamp = DateTime.UtcNow,
                Success = true
            });
        }

        public async Task<List<DocumentAccessEntry>> GetDocumentAccessListAsync(Guid documentId)
        {
            return await _permissionRepository.GetAllByDocumentIdAsync(documentId);
        }

        public async Task<List<Guid>> GetAccessibleDocumentsAsync(Guid userId)
        {
            var directAccessDocs = await _permissionRepository.GetAccessibleDocumentsForUserAsync(userId);
            var userRoles = await _userRoleRepository.GetUserRolesAsync(userId);
            var roleBasedDocs = new List<Guid>();

            foreach (var role in userRoles)
            {
                var roleDocs = await _permissionRepository.GetAccessibleDocumentsForRoleAsync(role.Id);
                roleBasedDocs.AddRange(roleDocs);
            }

            // Combine and deduplicate
            return directAccessDocs
                .Union(roleBasedDocs)
                .Distinct()
                .ToList();
        }

        public async Task ShareDocumentAsync(Guid userId, DocumentShareRequest request)
        {
            // Verify permission
            if (!await CanUserAccessDocumentAsync(userId, request.DocumentId, DocumentAction.Share))
                throw new UnauthorizedAccessException("You cannot share this document");

            // Grant to users
            foreach (var targetUserId in request.UserIds)
            {
                await GrantAccessAsync(userId, request.DocumentId, targetUserId, new DocumentAccessEntry
                {
                    CanView = request.CanView,
                    CanDownload = request.CanDownload,
                    CanEdit = request.CanEdit,
                    CanDelete = request.CanDelete,
                    ExpiresAt = request.ExpiresAt
                });
            }

            // Grant to roles
            foreach (var roleId in request.RoleIds)
            {
                await _permissionRepository.GrantToRoleAsync(request.DocumentId, roleId,
                    request.CanView, request.CanDownload, request.CanEdit, request.CanDelete, request.ExpiresAt);
            }

            // Log share action
            await _auditRepository.LogAsync(new DocumentAccessLog
            {
                Id = Guid.NewGuid(),
                DocumentId = request.DocumentId,
                UserId = userId,
                Action = "DocumentShared",
                Timestamp = DateTime.UtcNow,
                Success = true
            });
        }

        public async Task<bool> IsAccessExpiredAsync(Guid documentId, Guid userId)
        {
            var access = await _permissionRepository.GetByUserIdAsync(userId, documentId);
            return access != null && IsAccessExpired(access);
        }

        private bool HasPermission(DocumentAccessEntry entry, DocumentAction action)
        {
            return action switch
            {
                DocumentAction.View => entry.CanView,
                DocumentAction.Download => entry.CanDownload,
                DocumentAction.Edit => entry.CanEdit,
                DocumentAction.Delete => entry.CanDelete,
                DocumentAction.Share => entry.CanShare,
                _ => false
            };
        }

        private bool IsAccessExpired(DocumentAccessEntry entry)
        {
            return entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTime.UtcNow;
        }
    }

    // ========================================================================
    // DOCUMENT AUDIT SERVICE
    // ========================================================================

    public interface IDocumentAuditService
    {
        Task LogAccessAttemptAsync(Guid documentId, Guid userId, DocumentAction action, bool success, string reason = null);
        Task<List<DocumentAccessLog>> GetAccessHistoryAsync(Guid documentId, int days = 30);
        Task<List<DocumentAccessLog>> GetUserAccessHistoryAsync(Guid userId, int days = 30);
        Task GenerateAccessReportAsync(Guid documentId);
    }

    public class DocumentAuditService : IDocumentAuditService
    {
        private readonly IDocumentAccessLogRepository _repository;

        public DocumentAuditService(IDocumentAccessLogRepository repository)
        {
            _repository = repository;
        }

        public async Task LogAccessAttemptAsync(Guid documentId, Guid userId, DocumentAction action, bool success, string reason = null)
        {
            var log = new DocumentAccessLog
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                UserId = userId,
                Action = action.ToString(),
                Timestamp = DateTime.UtcNow,
                Success = success,
                DenyReason = reason
            };

            await _repository.LogAsync(log);
        }

        public async Task<List<DocumentAccessLog>> GetAccessHistoryAsync(Guid documentId, int days = 30)
        {
            var since = DateTime.UtcNow.AddDays(-days);
            return await _repository.GetByDocumentIdAsync(documentId, since);
        }

        public async Task<List<DocumentAccessLog>> GetUserAccessHistoryAsync(Guid userId, int days = 30)
        {
            var since = DateTime.UtcNow.AddDays(-days);
            return await _repository.GetByUserIdAsync(userId, since);
        }

        public async Task GenerateAccessReportAsync(Guid documentId)
        {
            var logs = await GetAccessHistoryAsync(documentId);
            // TODO: Generate report (PDF, Excel, etc.)
        }
    }

    // ========================================================================
    // DOCUMENT RETENTION SERVICE
    // ========================================================================

    public interface IDocumentRetentionService
    {
        Task<DocumentRetentionPolicy> CreateRetentionPolicyAsync(Guid organizationId, DocumentRetentionPolicy policy);
        Task ApplyRetentionPoliciesAsync();
        Task<List<Guid>> GetDocumentsToArchiveAsync();
        Task<List<Guid>> GetDocumentsToDeleteAsync();
    }

    public class DocumentRetentionService : IDocumentRetentionService
    {
        private readonly IDocumentRetentionRepository _policyRepository;
        private readonly IDocumentRepository _documentRepository;

        public DocumentRetentionService(
            IDocumentRetentionRepository policyRepository,
            IDocumentRepository documentRepository)
        {
            _policyRepository = policyRepository;
            _documentRepository = documentRepository;
        }

        public async Task<DocumentRetentionPolicy> CreateRetentionPolicyAsync(Guid organizationId, DocumentRetentionPolicy policy)
        {
            policy.Id = Guid.NewGuid();
            policy.OrganizationId = organizationId;
            policy.CreatedAt = DateTime.UtcNow;
            policy.IsActive = true;

            await _policyRepository.CreateAsync(policy);
            return policy;
        }

        public async Task ApplyRetentionPoliciesAsync()
        {
            var policies = await _policyRepository.GetActiveAsync();

            foreach (var policy in policies)
            {
                var docsToArchive = await GetDocumentsToArchiveAsync();
                var docsToDelete = await GetDocumentsToDeleteAsync();

                // Archive
                foreach (var docId in docsToArchive)
                {
                    var doc = await _documentRepository.GetByIdAsync(docId);
                    if (doc != null)
                    {
                        doc.IsArchived = true;
                        doc.Status = DocumentStatus.Archived;
                        await _documentRepository.UpdateAsync(doc);
                    }
                }

                // Delete (soft delete)
                foreach (var docId in docsToDelete)
                {
                    var doc = await _documentRepository.GetByIdAsync(docId);
                    if (doc != null)
                    {
                        doc.IsArchived = true;
                        doc.Status = DocumentStatus.Deleted;
                        await _documentRepository.UpdateAsync(doc);
                    }
                }
            }
        }

        public async Task<List<Guid>> GetDocumentsToArchiveAsync()
        {
            return await _policyRepository.GetDocumentsExceedingRetentionAsync(RetentionAction.Archive);
        }

        public async Task<List<Guid>> GetDocumentsToDeleteAsync()
        {
            return await _policyRepository.GetDocumentsExceedingRetentionAsync(RetentionAction.Delete);
        }
    }

    // ========================================================================
    // REPOSITORY INTERFACES
    // ========================================================================

    public interface IDocumentPermissionRepository
    {
        Task CreateAsync(DocumentAccessEntry entry);
        Task<DocumentAccessEntry> GetByUserIdAsync(Guid userId, Guid documentId);
        Task<DocumentAccessEntry> GetByRoleIdAsync(Guid roleId, Guid documentId);
        Task<List<DocumentAccessEntry>> GetAllByDocumentIdAsync(Guid documentId);
        Task<List<Guid>> GetAccessibleDocumentsForUserAsync(Guid userId);
        Task<List<Guid>> GetAccessibleDocumentsForRoleAsync(Guid roleId);
        Task GrantToRoleAsync(Guid documentId, Guid roleId, bool view, bool download, bool edit, bool delete, DateTime? expires);
        Task DeleteAsync(Guid documentId, Guid userId);
    }

    public interface IDocumentAccessLogRepository
    {
        Task LogAsync(DocumentAccessLog log);
        Task<List<DocumentAccessLog>> GetByDocumentIdAsync(Guid documentId, DateTime since);
        Task<List<DocumentAccessLog>> GetByUserIdAsync(Guid userId, DateTime since);
    }

    public interface IDocumentRetentionRepository
    {
        Task CreateAsync(DocumentRetentionPolicy policy);
        Task<List<DocumentRetentionPolicy>> GetActiveAsync();
        Task<List<Guid>> GetDocumentsExceedingRetentionAsync(RetentionAction action);
    }

    public interface IUserRoleRepository
    {
        Task<List<Role>> GetUserRolesAsync(Guid userId);
    }

    public class Role
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }
}
