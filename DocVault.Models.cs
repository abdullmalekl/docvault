// =====================================================
// DocVault.Models.cs
// MINIMAL CORE MODEL DEFINITIONS
// Only contains types that are genuinely missing from module files
// =====================================================

using System;
using System.Collections.Generic;

namespace DocVault.Core.Models
{
    // =====================================================
    // CORE ENTITY: Organization (MISSING from all modules)
    // =====================================================
    // This entity is referenced throughout the codebase but never defined.
    // Added here as the authoritative definition.

    public class Organization
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public string Industry { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    // =====================================================
    // CORE ENTITY: LoginHistory (MISSING from all modules)
    // =====================================================
    // Referenced in authentication flows but never defined.

    public class LoginHistory
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public DateTime LoginTime { get; set; } = DateTime.UtcNow;
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public bool Success { get; set; } = true;
    }

    // =====================================================
    // ENUMS (Core classifications)
    // =====================================================
    // These are referenced but defined inconsistently across modules.

    public enum DocumentStatus
    {
        Draft,
        InReview,
        Approved,
        Published,
        Archived
    }

    public enum DocumentClassification
    {
        Public,
        Internal,
        Confidential,
        Secret
    }

    public enum ApprovalStatus
    {
        Pending,
        Approved,
        Rejected,
        Cancelled
    }

    public enum EmailStatus
    {
        Pending,
        Sent,
        Failed,
        Bounced
    }

    public enum WorkflowStatus
    {
        Draft,
        Active,
        Completed,
        Archived
    }

    // =====================================================
    // DTOs: Minimal request/response types
    // =====================================================
    // Only include if genuinely missing from modules

    public class CreateBranchRequest
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public bool IsHeadquarters { get; set; }
    }

    public class CreateDepartmentRequest
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public int BranchID { get; set; }
    }

    public class CreateUnitRequest
    {
        public string Name { get; set; }
        public int DepartmentID { get; set; }
    }

    public class CreateDocumentRequest
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string DocumentType { get; set; }
        public DocumentClassification Classification { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new();

        public void AddError(string error)
        {
            IsValid = false;
            Errors.Add(error);
        }
    }

    public class EmailAttachment
    {
        public string FileName { get; set; }
        public byte[] FileContent { get; set; }
    }

    public class ApprovalDecisionRequest
    {
        public Guid ApprovalTaskId { get; set; }
        public bool IsApproved { get; set; }
        public string Comments { get; set; }
    }

    public class BranchResponse
    {
        public int BranchID { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public string Location { get; set; }
        public bool IsHeadquarters { get; set; }
    }

    public class SearchQuery
    {
        public string QueryText { get; set; }
        public List<DocumentClassification> Classifications { get; set; } = new();
        public List<DocumentStatus> Statuses { get; set; } = new();
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class SearchResult
    {
        public Guid DocumentId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DocumentStatus Status { get; set; }
        public DocumentClassification Classification { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AuthenticationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Token { get; set; }
    }
}
