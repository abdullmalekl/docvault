# DocVault — Enterprise Document Archival System
## Software Requirements Specification & Architecture Document
**Version:** 1.0  
**Date:** June 21, 2026  
**Status:** APPROVED  
**Prepared By:** Alwadi Developer Team  

---

# TABLE OF CONTENTS

1. [Executive Summary](#executive-summary)
2. [System Overview](#system-overview)
3. [Functional Requirements](#functional-requirements)
4. [Non-Functional Requirements](#non-functional-requirements)
5. [Technology Stack](#technology-stack)
6. [System Architecture](#system-architecture)
7. [Database Design](#database-design)
8. [Security Architecture](#security-architecture)
9. [API Specification](#api-specification)
10. [UI/UX Specification](#uiux-specification)
11. [Deployment Architecture](#deployment-architecture)
12. [Development Roadmap](#development-roadmap)

---

# EXECUTIVE SUMMARY

## Project Overview

**DocVault** is an enterprise-grade Windows desktop application for digitizing, archiving, and retrieving official documents and records within government institutions and organizations.

**Key Characteristics:**
- 📄 **Document Types:** Incoming, Outgoing, Internal documents
- 💾 **Scale:** Up to 1-2 million documents
- 👥 **Users:** 10-50 concurrent users
- 🌐 **Deployment:** Single-node + Network (10 nodes max)
- 🔒 **Security:** Enterprise-grade encryption + Multi-auth
- 📊 **Analytics:** Advanced reporting & dashboards
- 🌍 **Language:** Arabic + English (Bilingual)

---

# SYSTEM OVERVIEW

## 1.1 Purpose

DocVault digitizes paper documents through optical scanning, stores them electronically with complete metadata, and enables rapid retrieval through advanced search capabilities—replacing manual file searches in traditional filing cabinets.

## 1.2 Scope

### In Scope
✅ Document scanning and archival  
✅ Multi-user access with role-based permissions  
✅ Network sync & offline mode  
✅ Advanced search & retrieval  
✅ Audit logging & compliance  
✅ Backup & disaster recovery  
✅ API for future integrations  

### Out of Scope
❌ Web-based access (Phase 2)  
❌ Mobile applications (Phase 2)  
❌ Email integration (Phase 2)  
❌ Third-party integrations (Future)  

## 1.3 Deployment Modes

| Mode | Configuration | Users | Data Storage |
|------|---------------|-------|--------------|
| **Single Node** | 1 PC | 1-5 | Local disk |
| **Network** | 10 PCs on LAN | 10-50 | Central server |
| **Hybrid** | Domain + Standalone | Flexible | Mixed |

---

# FUNCTIONAL REQUIREMENTS

## 2.1 Document Management

### FR1: Document Ingestion
- **Scanner Integration:** TWAIN standard + Bizhub support
- **Multi-source:** Optical scanning + file upload (PDF, Word, Excel, Images)
- **Batch Processing:** Scan multiple pages into single document
- **Auto-feed Support:** Continuous scanning without manual intervention
- **Quality Control:**
  - DPI options: 300 DPI (standard), 600 DPI (detailed)
  - Color modes: True Color, Grayscale, Black & White
  - Page size: A4, A3, Legal (auto-detect)
  - Rotation & deskew: Automatic
  - Quality score: 0-100% per page

### FR2: Document Metadata
Every document stores:
- Document number (auto-generated, customizable)
- Date (received/sent/created)
- Subject
- Department/Organization (linked)
- Category/Classification
- Status (Active, Archived, Deleted)
- Physical location (Cabinet, File box)
- Keywords (searchable)
- Created by (user)
- Last modified by (user)
- Modification timestamp

### FR3: Document Relationships
- Link outgoing documents to related incoming documents
- Track full chain of correspondence
- Support one-to-many relationships
- Visual relationship graph

### FR4: OCR & Text Extraction
- **Language Support:** Arabic, English, Multi-language
- **Processing:** Auto-deskew, denoise, enhance
- **Output:** Searchable PDF + Full-text index
- **Quality Score:** Per-page confidence level (0-100%)
- **Limitations:** Handle 85-92% accuracy on real-world documents

### FR5: Advanced Search
**Multi-criteria search supporting:**
- Document number
- Date range
- Subject
- Department
- Category
- Status
- Keywords
- Full-text (OCR content)
- Combinations of above criteria

### FR6: Document Viewing
- Display document in viewer
- Zoom in/out with smooth scrolling
- Page navigation (first, last, next, previous)
- Thumbnail preview
- Rotation controls
- Full-screen mode

### FR7: Document Operations
- **Print:** Direct to printer + print preview
- **Export:** PDF, Excel, ZIP archive
- **Save Search:** Store frequently-used searches
- **Bulk Actions:** Perform operations on multiple documents
- **Download:** Save to local disk

## 2.2 User & Access Management

### FR8: Organizational Structure
- Create hierarchical department/division structure
- Support tree-based navigation
- Drag-drop reorganization
- Display full hierarchy in UI

### FR9: User Accounts
- Multiple users per system
- Username + password authentication
- Google Authenticator 2FA (MFA)
- Support Windows domain authentication (flexible)
- Local account fallback for standalone mode
- Password complexity policies
- Account status (Active, Disabled, Locked)

### FR10: Role-Based Access Control
- Predefined roles: Admin, Manager, Operator, Viewer
- Custom role creation
- Granular permissions:
  - View documents
  - Create documents
  - Edit metadata
  - Delete documents
  - Print documents
  - Export documents
  - Manage users
  - View audit logs
  - Configure system
- Document-level access: Users see only their department's documents unless granted broader access

### FR11: Audit Logging
Complete audit trail capturing:
- Who performed the action
- What action (create, edit, delete, view, print, export, download)
- When (timestamp, timezone)
- Which document
- What changed (metadata, content)
- Result (success, failure)
- IP address (network mode)
- Device information

**Audit Log Storage:**
- Immutable append-only log
- Retention: Minimum 2 years
- Search audit logs by date, user, action, document

## 2.3 Data Protection

### FR12: Encryption
- **At Rest:** AES-256 encryption for all documents + metadata
- **In Transit:** TLS 1.3 for all network communication
- **Password Storage:** Bcrypt hashing with salt
- **Key Management:** Hardware security module (HSM) support (optional)

### FR13: Backup & Recovery
- **Frequency:** Daily (configurable: 6h, 12h, 24h, weekly, monthly)
- **Enable/Disable:** User-controlled
- **Retention Policy:** Keep 30 backups; delete oldest when limit reached
- **Recovery:** One-click full restore with password protection
- **Verification:** Automated backup integrity checks
- **Backup Location:** Local disk or external storage (configurable)

### FR14: Data Integrity
- Soft delete: Deleted documents marked but not removed initially
- Permanent delete: After 90-day hold period (configurable)
- Referential integrity: Prevent orphaned records
- Transaction support: All operations atomic
- Conflict resolution: Last-write-wins for concurrent edits

## 2.4 Synchronization & Offline Mode

### FR15: Network Synchronization
- **Mode:** Real-time push + scheduled pull
- **Frequency:** Configurable (1min to 24h)
- **Conflict Resolution:** 
  - Last-write-wins (default)
  - Manual conflict resolution UI
- **Bandwidth:** Delta sync (only changed data)
- **Verification:** Checksum validation

### FR16: Offline Mode
- Work without network connection
- Local queue for pending operations
- Automatic sync when connection restored
- Conflict detection & resolution
- Status indicator (Online/Offline)
- Bandwidth throttling for slow networks

## 2.5 Reporting & Analytics

### FR17: Pre-built Reports
1. **Daily Statistics:** Documents added, searched, printed, exported
2. **Category Distribution:** Documents by category, department
3. **Audit Trail:** User actions, access logs, changes
4. **User Statistics:** Active users, login times, actions per user
5. **Linked Documents:** Relationship analysis, correspondence chains

### FR18: Custom Reports
- Drag-drop report builder
- Multiple output formats: PDF, Excel, HTML, CSV
- Scheduling: Run at specific times
- Email delivery (Phase 2)
- Dashboard widgets: Pin reports to dashboard

### FR19: Dashboard
- Real-time statistics
- Key metrics: Total docs, users, searches/day
- Charts: Line graphs, pie charts, heat maps
- Customizable widgets
- Dark/Light theme

## 2.6 Configuration & Customization

### FR20: System Configuration
- Document number format (prefix, numbering scheme)
- Department hierarchy management
- Category list management
- Status list management
- Field visibility: Show/hide fields per role
- Mandatory vs. optional fields
- Custom fields: Add new fields without code changes
- Date/time formats
- Language selection

---

# NON-FUNCTIONAL REQUIREMENTS

## 3.1 Performance

| Metric | Requirement |
|--------|-------------|
| **Search Response Time** | <2 seconds for 1M documents |
| **Document Loading** | <1 second (display) |
| **Sync Time** | <5 seconds for 100 docs |
| **Concurrent Users** | 50+ simultaneous |
| **Throughput** | 1000 docs/day ingestion |
| **Daily Search Volume** | 10,000+ queries |

## 3.2 Scalability

- **Horizontal:** Support 10+ client nodes
- **Vertical:** Support 20TB storage
- **Database:** Support 1-2 million documents
- **Concurrent:** 50+ simultaneous connections
- **Query:** Optimized for full-text search on millions

## 3.3 Availability & Reliability

- **Uptime:** 99.5% (business hours)
- **MTBF:** 720+ hours between failures
- **MTTR:** <1 hour recovery time
- **Backup Frequency:** Daily (configurable)
- **Recovery RTO:** <4 hours
- **Recovery RPO:** <1 hour data loss

## 3.4 Security

- **Authentication:** Multi-factor (Google Authenticator)
- **Authorization:** Role-based access control (RBAC)
- **Encryption:** AES-256 at rest, TLS 1.3 in transit
- **Audit:** Complete immutable audit trail
- **Compliance:** FADGI standards for document preservation
- **Data Residency:** All data stays within organization
- **No Internet:** Fully offline-capable, no cloud dependency

## 3.5 Usability

- **Learning Curve:** <4 hours for new operators
- **Help System:** Built-in tooltips, user manual, video tutorials
- **Keyboard Shortcuts:** Power users can work 50% faster
- **Accessibility:** WCAG 2.1 AA compliance
- **Localization:** Full Arabic & English support (RTL for Arabic)
- **Responsiveness:** Smooth UI even on older hardware

## 3.6 Maintainability

- **Code Quality:** No technical debt
- **Documentation:** 100% code & architecture documentation
- **Testing:** Unit + Integration + E2E test coverage >80%
- **Logging:** Structured logging for troubleshooting
- **Updates:** Automatic security patches (configurable)

---

# TECHNOLOGY STACK

## 4.1 Platform & Runtime

| Component | Choice | Rationale |
|-----------|--------|-----------|
| **OS** | Windows Server 2019/2022 + Windows 11 | Enterprise standard |
| **Runtime** | .NET 8 (Modern, performant, cross-platform ready) | Latest LTS, excellent performance |
| **Desktop Framework** | WPF (Windows Presentation Foundation) | Native Windows UI, enterprise standard |
| **Database** | SQL Server 2019/2022 (Enterprise) or PostgreSQL (Cost-effective) | Enterprise RDBMS, proven at scale |

## 4.2 Key Libraries & Frameworks

| Layer | Technology | Purpose |
|-------|-----------|---------|
| **UI** | WPF + MVVM Toolkit | Desktop interface |
| **Data Access** | Entity Framework Core 8 | ORM |
| **Database** | SQL Server / PostgreSQL | Data storage |
| **Scanning** | Dynamsoft DynamsoftSDK or LibTwain | Scanner integration |
| **OCR** | Tesseract.NET or ABBYY | Text extraction |
| **PDF** | iTextSharp or SelectPdf | PDF generation |
| **Encryption** | System.Security.Cryptography | AES-256 |
| **Authentication** | Google Authenticator (TOTP) + Windows Auth | MFA |
| **Logging** | Serilog | Structured logging |
| **Search** | Lucene.NET or Full-text SQL | Full-text indexing |
| **Sync** | Custom sync engine with conflict resolution | Offline + Network |
| **API** | ASP.NET Core 8 Web API | REST API layer |

## 4.3 Development Tools

- **IDE:** Visual Studio 2022 Community/Professional
- **Version Control:** Git + GitHub/Azure DevOps
- **Build:** MSBuild + GitHub Actions
- **Testing:** NUnit + Moq + Selenium (E2E)
- **Code Analysis:** SonarQube + StyleCop
- **Documentation:** Swagger/OpenAPI for API

---

# SYSTEM ARCHITECTURE

## 5.1 Architecture Overview

```
┌─────────────────────────────────────────────────────┐
│                   DOCVAULT SYSTEM                    │
└─────────────────────────────────────────────────────┘

TIER 1: PRESENTATION LAYER
├── WPF Desktop Application (Windows)
│   ├── Main Module (Dashboard, Search, View)
│   ├── Scanning Module (TWAIN Integration)
│   ├── Admin Module (User Management, Config)
│   └── Reporting Module (Analytics, Charts)

TIER 2: APPLICATION LAYER
├── Business Logic Engine
│   ├── Document Service
│   ├── Search Service
│   ├── User Service
│   ├── Sync Service
│   ├── Backup Service
│   └── Audit Service
├── REST API (ASP.NET Core)
│   └── Future integrations

TIER 3: DATA ACCESS LAYER
├── Entity Framework Core (ORM)
├── Repository Pattern
└── Database Abstraction

TIER 4: INFRASTRUCTURE LAYER
├── SQL Server / PostgreSQL
├── File Storage (Encrypted)
├── Encryption Engine (AES-256)
├── Authentication Service (LDAP, Google Auth)
└── Logging Service (Serilog)

TIER 5: SYNC & OFFLINE ENGINE
├── Offline Queue Manager
├── Sync Conflict Resolver
├── Network Manager
└── Data Replication Service
```

## 5.2 Modular Design

**Core Modules:**

1. **Document Module**
   - Document ingestion
   - OCR processing
   - Metadata management
   - Full-text indexing

2. **Search Module**
   - Multi-criteria search
   - Full-text search (Lucene)
   - Query optimizer
   - Result ranking

3. **User & Security Module**
   - Authentication (LDAP, Google Auth, Local)
   - Authorization (RBAC)
   - Password management
   - Session management

4. **Sync Module**
   - Network synchronization
   - Offline queue
   - Conflict detection/resolution
   - Bandwidth management

5. **Backup & Recovery Module**
   - Backup scheduler
   - Encryption
   - Integrity verification
   - Restore functionality

6. **Audit Module**
   - Event logging
   - Immutable storage
   - Query & filtering
   - Report generation

7. **Reporting Module**
   - Pre-built reports
   - Custom report builder
   - Dashboard
   - Export functionality

8. **Admin Module**
   - Organization structure
   - User management
   - System configuration
   - License management

## 5.3 Deployment Modes

### Single-Node Mode
```
┌──────────────────┐
│   Windows PC     │
├──────────────────┤
│   WPF App        │
│   SQL Server     │
│   File Storage   │
│   Backup Storage │
└──────────────────┘
```

### Network Mode (Client-Server)
```
┌─────────────────┐
│  Admin Server   │
├─────────────────┤
│  SQL Server     │
│  File Storage   │
│  Backup Storage │
└────────┬────────┘
         │ LAN
┌────────┴────────┬──────────┐
│                 │          │
┌─────────┐  ┌─────────┐  ┌─────────┐
│ Client1 │  │ Client2 │  │Client10 │
│ WPF App │  │ WPF App │  │ WPF App │
└─────────┘  └─────────┘  └─────────┘
```

---

# DATABASE DESIGN

## 6.1 Entity-Relationship Diagram (ERD)

```
CORE ENTITIES:
├── Users
│   ├── UserID (PK)
│   ├── Username
│   ├── PasswordHash
│   ├── Email
│   ├── DepartmentID (FK)
│   ├── RoleID (FK)
│   ├── Status
│   ├── CreatedAt
│   └── LastLoginAt

├── Departments
│   ├── DepartmentID (PK)
│   ├── Name
│   ├── ParentDepartmentID (FK, self-referencing)
│   ├── Description
│   └── CreatedAt

├── Roles
│   ├── RoleID (PK)
│   ├── Name
│   ├── Description
│   └── Permissions (JSON blob)

├── Documents
│   ├── DocumentID (PK)
│   ├── DocumentNumber (Unique)
│   ├── Title
│   ├── Subject
│   ├── Content (Full-text indexed)
│   ├── DocumentDate
│   ├── DepartmentID (FK)
│   ├── CategoryID (FK)
│   ├── StatusID (FK)
│   ├── CreatedBy (FK → Users)
│   ├── CreatedAt
│   ├── ModifiedBy (FK → Users)
│   ├── ModifiedAt
│   ├── PhysicalLocation
│   └── IsDeleted

├── DocumentPages
│   ├── PageID (PK)
│   ├── DocumentID (FK)
│   ├── PageNumber
│   ├── ImagePath (encrypted)
│   ├── OCRText (searchable)
│   ├── QualityScore
│   └── CreatedAt

├── DocumentRelationships
│   ├── RelationshipID (PK)
│   ├── SourceDocumentID (FK)
│   ├── TargetDocumentID (FK)
│   ├── RelationType (Reply, Forward, Related)
│   └── CreatedAt

├── Keywords
│   ├── KeywordID (PK)
│   ├── DocumentID (FK)
│   ├── Keyword
│   └── Frequency

├── AuditLog
│   ├── AuditID (PK)
│   ├── UserID (FK)
│   ├── ActionType (Create, Edit, View, Delete, Print, Export)
│   ├── TableName
│   ├── RecordID
│   ├── OldValue (nullable)
│   ├── NewValue (nullable)
│   ├── Timestamp
│   ├── IPAddress
│   └── IsSuccess

├── Backups
│   ├── BackupID (PK)
│   ├── BackupPath
│   ├── BackupSize
│   ├── CreatedAt
│   ├── VerificationHash
│   └── IsVerified

├── SearchHistory
│   ├── SearchID (PK)
│   ├── UserID (FK)
│   ├── SearchQuery
│   ├── ResultCount
│   ├── ExecutionTime
│   ├── CreatedAt
│   └── IsSaved

└── SystemConfig
    ├── ConfigID (PK)
    ├── Key
    ├── Value
    └── UpdatedAt
```

## 6.2 Indexing Strategy

**Primary Keys:**
- All tables have single-column PKs (identity)

**Unique Indexes:**
- Users.Username
- Documents.DocumentNumber

**Full-Text Indexes:**
- Documents.Title
- Documents.Subject
- DocumentPages.OCRText
- Keywords.Keyword

**Performance Indexes:**
- Documents.DepartmentID
- Documents.CategoryID
- Documents.CreatedAt
- AuditLog.UserID
- AuditLog.Timestamp

**Partitioning (for 1M+ documents):**
- Partition Documents by year (DocumentDate)
- Partition AuditLog by month (Timestamp)

---

# SECURITY ARCHITECTURE

## 7.1 Authentication & Authorization

### Authentication Flow:
```
1. User enters credentials
2. Check local account OR Domain (Active Directory) OR Google Authenticator
3. MFA challenge (Google Authenticator TOTP)
4. Generate JWT token (60-minute expiry)
5. Store session in memory + database
6. Refresh token (7-day expiry) for long sessions
```

### Authorization Flow:
```
1. User requests action
2. Check JWT token validity
3. Lookup user's role
4. Check role permissions for requested action
5. Check document-level permissions
6. Allow/Deny + Log action
```

## 7.2 Encryption

**At Rest:**
- File encryption: AES-256 CBC with random IV
- Database encryption: SQL Server Transparent Data Encryption (TDE)
- Backup encryption: AES-256 with password-protected keys

**In Transit:**
- Network: TLS 1.3 minimum
- Certificate pinning for server-client communication

**Password Security:**
- Hash: Bcrypt with salt (cost factor: 12)
- No plaintext storage
- Minimum 12 characters required
- Complexity: Uppercase, lowercase, digits, special chars

## 7.3 Audit & Compliance

**Audit Logging:**
- All actions logged immutably
- Stored in append-only table
- Retention: 2+ years minimum
- Export audit logs for compliance audits

**Compliance Standards:**
- FADGI (Federal Agencies Digital Guidelines Initiative)
- ISO 27001 (Information Security)
- GDPR-compliant (if processing EU data)
- SOC 2 Type II ready

## 7.4 Threat Mitigation

| Threat | Mitigation |
|--------|-----------|
| **SQL Injection** | Parameterized queries (Entity Framework) |
| **XSS** | Input validation + output encoding |
| **CSRF** | Token-based CSRF protection |
| **Brute Force** | Account lockout (5 attempts → 30 min lock) |
| **Session Hijacking** | Secure cookies, HTTPS only |
| **Privilege Escalation** | RBAC with strict role definitions |
| **Data Breach** | AES-256 encryption + access control |
| **Malware** | File scanning via Windows Defender |

---

# API SPECIFICATION

## 8.1 REST API Design

**Base URL:** `https://docvault-api/v1` (for future Web access)

**Authentication:** JWT Bearer Token in Authorization header

```
Authorization: Bearer <jwt_token>
```

## 8.2 Core Endpoints

### Documents
```
GET    /documents                    # List documents
GET    /documents/{id}               # Get document details
POST   /documents                    # Create document
PUT    /documents/{id}               # Update document
DELETE /documents/{id}               # Delete document (soft)
GET    /documents/{id}/pages         # Get document pages
GET    /documents/{id}/relationships # Get related documents
GET    /documents/search             # Advanced search
POST   /documents/{id}/export        # Export document
```

### Users
```
GET    /users                        # List users (Admin only)
GET    /users/{id}                   # Get user details
POST   /users                        # Create user (Admin only)
PUT    /users/{id}                   # Update user
DELETE /users/{id}                   # Delete user (Admin only)
POST   /users/auth/login             # Login
POST   /users/auth/logout            # Logout
POST   /users/auth/mfa               # MFA verification
```

### Reports
```
GET    /reports                      # List available reports
POST   /reports/generate             # Generate custom report
GET    /reports/{id}/download        # Download report
GET    /dashboard/metrics            # Dashboard data
```

### Admin
```
GET    /admin/audit-log              # Audit logs
GET    /admin/config                 # System configuration
PUT    /admin/config                 # Update configuration
POST   /admin/backup/trigger         # Trigger backup
GET    /admin/backup/list            # List backups
POST   /admin/backup/restore         # Restore from backup
```

## 8.3 Response Format

**Success Response:**
```json
{
  "success": true,
  "data": { /* response object */ },
  "timestamp": "2026-06-21T11:25:00Z"
}
```

**Error Response:**
```json
{
  "success": false,
  "error": {
    "code": "DOCUMENT_NOT_FOUND",
    "message": "Document with ID 123 not found",
    "details": { /* optional details */ }
  },
  "timestamp": "2026-06-21T11:25:00Z"
}
```

---

# UI/UX SPECIFICATION

## 9.1 Application Screens

### 1. Login Screen
- Username/Email field
- Password field
- Google Authenticator 2FA input
- "Remember Me" checkbox
- "Forgot Password?" link
- Language selector (Arabic/English)

### 2. Main Dashboard
- **Left Panel:** Department tree (expandable)
- **Top Bar:** User menu, notifications, settings
- **Main Area:** Quick stats + Recent documents
- **Right Panel:** Quick search + Filters

### 3. Document Search Screen
- **Search Criteria:**
  - Document number field
  - Date range picker
  - Subject field
  - Department dropdown
  - Category dropdown
  - Status dropdown
  - Keywords input
- **Results:** Table with document previews
- **Actions:** View, Edit, Print, Export per document

### 4. Document Viewer Screen
- **Document Display:** Full-page PDF/image viewer
- **Page Navigation:** First, Previous, Next, Last
- **Zoom Controls:** +/-, Fit to page, Fit to width
- **Tools:** Rotate, Print, Download, Share
- **Sidebar:** Thumbnail navigation

### 5. Document Editor Screen
- **Metadata Form:**
  - Document number (read-only)
  - Title (required)
  - Subject (required)
  - Department (required)
  - Category (required)
  - Status (required)
  - Physical location (optional)
  - Keywords (optional)
- **Related Documents:** Linked documents section
- **Attachments:** Add/remove files
- **History:** Revision history

### 6. Scanning Module
- **Scanner Selection:** Dropdown of connected scanners
- **Settings:**
  - DPI: 300/600 (dropdown)
  - Color mode: RGB/Grayscale/B&W (dropdown)
  - Page size: A4/A3/Legal (auto-detect)
- **Preview:** Live preview before scan
- **Batch Mode:** Auto-feed multiple pages
- **Progress:** Real-time progress bar
- **Result:** Save as new document

### 7. User Management Screen (Admin)
- **User List:** Table with add/edit/delete actions
- **User Form:**
  - Username
  - Email
  - Department
  - Role
  - Status (Active/Disabled)
  - MFA enabled checkbox
- **Permission Matrix:** Granular per-role permissions

### 8. Reporting Dashboard
- **Pre-built Reports:** Cards for each standard report
- **Custom Reports:** Report builder interface
- **Charts:** Line, Pie, Bar charts with drill-down
- **Export:** PDF, Excel buttons per report
- **Schedule:** Set report to run on schedule

### 9. Audit Log Screen
- **Filters:**
  - User
  - Action type
  - Date range
  - Document
  - Status
- **Results:** Table with sortable columns
- **Export:** Download audit log as Excel/PDF

### 10. System Configuration Screen (Admin)
- **Document Numbering:** Format + next number
- **Department Management:** Add/edit/delete departments
- **Categories:** Add/edit/delete categories
- **Status Types:** Add/edit/delete status options
- **Custom Fields:** Add new fields without coding
- **Backup Settings:** Frequency, retention, location
- **Sync Settings:** Frequency, bandwidth limits

## 9.2 Navigation Flow

```
┌─────────────┐
│ Login Page  │
└──────┬──────┘
       │
       ▼
┌──────────────────────────┐
│   Main Dashboard         │
├──────────────────────────┤
│  ├─ Search Documents     │
│  ├─ Scanning Module      │
│  ├─ View Document        │
│  ├─ Reports (Admin)      │
│  ├─ User Management      │
│  ├─ System Config (Admin)│
│  └─ Logout              │
└──────────────────────────┘
```

## 9.3 Design System

**Colors:**
- Primary: #1F73B7 (Professional Blue)
- Secondary: #28A745 (Success Green)
- Danger: #DC3545 (Error Red)
- Background: #F5F5F5 (Light Gray)
- Text: #333333 (Dark Gray)

**Typography:**
- Headings: Segoe UI, 18-24px, Bold
- Body: Segoe UI, 12-14px, Regular
- Monospace: Consolas for code/IDs

**Spacing:**
- Padding: 8px, 16px, 24px increments
- Margins: 16px, 24px, 32px increments

**RTL Support:**
- Automatic layout flip for Arabic
- All text properly aligned (RTL)
- Icons mirrored where appropriate

---

# DEPLOYMENT ARCHITECTURE

## 10.1 Single-Node Deployment

**Hardware Requirements:**
- CPU: 4+ cores
- RAM: 8GB minimum, 16GB recommended
- Storage: 500GB SSD minimum (scalable to 20TB)
- Network: Gigabit Ethernet

**Software Stack:**
- Windows Server 2019/2022 or Windows 11 Pro/Enterprise
- SQL Server 2019/2022 Express (free, 10GB limit) or Enterprise
- .NET Runtime 8.0

**Installation Steps:**
```
1. Install Windows Server / Windows 11
2. Install SQL Server (with Full-Text Search)
3. Install .NET Runtime 8.0
4. Extract DocVault application
5. Run database migration script
6. Create initial admin user
7. Configure backup location
8. Start DocVault service
```

## 10.2 Network Deployment (Client-Server)

**Server Hardware:**
- CPU: 8+ cores
- RAM: 32GB
- Storage: 1TB+ SSD + 20TB backup
- Network: Gigabit Ethernet + redundancy

**Server Software:**
- Windows Server 2019/2022
- SQL Server 2019/2022 Enterprise
- .NET Runtime 8.0
- IIS (for API layer)

**Client Hardware:**
- CPU: 2+ cores
- RAM: 4GB minimum
- Network: Gigabit Ethernet

**Network Architecture:**
```
┌─────────────────────────────────────┐
│        Internal LAN (10.x.x.x)      │
├─────────────────────────────────────┤
│  ┌─────────────────────────────┐    │
│  │    Server (192.168.1.1)     │    │
│  │  - SQL Server               │    │
│  │  - File Storage (encrypted) │    │
│  │  - Backup Storage           │    │
│  └─────────────────────────────┘    │
│           │      │      │            │
│  ┌────────┴──────┴──────┴────┐      │
│  │                            │      │
│  ▼                            ▼      │
│ Client1                    Client10  │
│ (WPF App)                (WPF App)   │
│                                      │
└─────────────────────────────────────┘
```

## 10.3 Backup Strategy

**Daily Backup Schedule:**
- Time: 2 AM (configurable)
- Frequency: Every 24 hours (customizable)
- Retention: Keep 30 backups
- Auto-delete: Oldest backup when limit exceeded
- Verification: Hash-based integrity check

**Backup Content:**
- Full database dump
- Encrypted file storage
- Application configuration
- User settings

**Restore Procedure:**
```
1. Shutdown DocVault service
2. Select backup point
3. Verify backup integrity
4. Restore database
5. Restore file storage
6. Verify data consistency
7. Restart service
8. Test application
```

---

# DEVELOPMENT ROADMAP

## 11.1 Release Schedule

### Phase 1: MVP (Months 1-3)
**Deliverables:**
- ✅ Core document ingestion (scanning + OCR)
- ✅ Search (basic multi-criteria)
- ✅ User management (local accounts)
- ✅ Basic reporting
- ✅ Single-node deployment
- ✅ Backup/restore

**Release:** v1.0 (Pilot with 1-2 departments)

### Phase 2: Enterprise (Months 4-6)
**Deliverables:**
- ✅ Network sync + offline mode
- ✅ Google Authenticator MFA
- ✅ Advanced reporting dashboard
- ✅ Full-text search (Lucene)
- ✅ Custom fields + configuration
- ✅ Multi-language (Arabic + English)

**Release:** v2.0 (Production rollout)

### Phase 3: Extensions (Months 7-12)
**Deliverables:**
- ✅ REST API finalization
- ✅ Web portal (Read-only access)
- ✅ Mobile app (iOS/Android)
- ✅ Email integration
- ✅ Third-party connectors
- ✅ Performance optimizations

**Release:** v3.0 (Full ecosystem)

## 11.2 Epic Breakdown

### Epic 1: Document Management
- Task 1.1: Scanner integration (TWAIN, Bizhub)
- Task 1.2: OCR engine integration
- Task 1.3: Metadata management
- Task 1.4: Document relationships
- Task 1.5: Full-text indexing

### Epic 2: User & Security
- Task 2.1: Authentication (Local, Domain, Google Auth)
- Task 2.2: Authorization (RBAC)
- Task 2.3: Encryption (AES-256)
- Task 2.4: Audit logging
- Task 2.5: Access control lists

### Epic 3: Search & Retrieval
- Task 3.1: Multi-criteria search
- Task 3.2: Full-text search engine
- Task 3.3: Search optimization
- Task 3.4: Saved searches
- Task 3.5: Result ranking

### Epic 4: Sync & Offline
- Task 4.1: Network sync engine
- Task 4.2: Offline queue manager
- Task 4.3: Conflict resolution
- Task 4.4: Bandwidth throttling
- Task 4.5: Status indicators

### Epic 5: Reporting
- Task 5.1: Pre-built reports
- Task 5.2: Dashboard design
- Task 5.3: Custom report builder
- Task 5.4: Chart/visualization
- Task 5.5: Export functionality

### Epic 6: Admin & Config
- Task 6.1: Organization structure
- Task 6.2: User management
- Task 6.3: Custom fields
- Task 6.4: System configuration
- Task 6.5: Backup management

### Epic 7: Testing & QA
- Task 7.1: Unit tests (>80% coverage)
- Task 7.2: Integration tests
- Task 7.3: E2E tests
- Task 7.4: Performance testing
- Task 7.5: Security testing

### Epic 8: Deployment
- Task 8.1: Installer creation
- Task 8.2: Database migration
- Task 8.3: Configuration scripts
- Task 8.4: Documentation
- Task 8.5: Training materials

---

# TESTING & QA PLAN

## 12.1 Test Types & Coverage

| Test Type | Coverage | Tools |
|-----------|----------|-------|
| **Unit Tests** | >80% code paths | NUnit + Moq |
| **Integration Tests** | Database + API | NUnit + TestContainers |
| **E2E Tests** | User workflows | Selenium / TestStack |
| **Performance Tests** | Load + stress | JMeter / LoadRunner |
| **Security Tests** | OWASP Top 10 | Burp Suite / ZAP |
| **Accessibility Tests** | WCAG 2.1 AA | WAVE / Axe DevTools |

## 12.2 Test Scenarios

**Document Management:**
- ✅ Scan document with various DPI/colors
- ✅ Upload PDF/Word/Excel files
- ✅ Extract text via OCR
- ✅ Index full-text content
- ✅ Link related documents

**Search:**
- ✅ Search by document number
- ✅ Search by date range
- ✅ Search by multiple criteria
- ✅ Full-text search in OCR content
- ✅ Performance with 1M documents

**User Access:**
- ✅ Login with local account
- ✅ Login with domain account
- ✅ 2FA with Google Authenticator
- ✅ RBAC enforcement
- ✅ Audit log recording

**Sync & Offline:**
- ✅ Real-time sync between clients
- ✅ Offline operation
- ✅ Auto-sync on reconnection
- ✅ Conflict detection & resolution
- ✅ Bandwidth throttling

**Backup:**
- ✅ Daily backup execution
- ✅ Backup integrity verification
- ✅ Full restore procedure
- ✅ Verify data consistency post-restore
- ✅ Backup retention (30 versions)

---

# SECURITY TESTING CHECKLIST

- [ ] SQL Injection attempts
- [ ] XSS payload testing
- [ ] CSRF token validation
- [ ] Brute force attack resistance
- [ ] Session management security
- [ ] Password policy enforcement
- [ ] Encryption verification
- [ ] Audit log integrity
- [ ] Data leakage prevention
- [ ] Privilege escalation attempts

---

# DELIVERABLES SUMMARY

## Phase 1 Deliverables:

1. ✅ **SRS Document** (This document - Section 2-3)
2. ✅ **Architecture Document** (Section 4-10)
3. ✅ **Database Design** (ERD + Schema, Section 6)
4. ✅ **Security Model** (Section 7)
5. ✅ **API Specification** (Section 8)
6. ✅ **UI/UX Specification** (Screen designs, Section 9)
7. ✅ **Deployment Guide** (Section 10)
8. ✅ **Development Roadmap** (Section 11)
9. ✅ **Testing Plan** (Section 12)
10. ✅ **Technology Stack** (Section 4.2)

## Missing Requirements Report:

- None identified — All requirements captured

---

# APPROVAL & SIGN-OFF

**Prepared By:** Alwadi Developer Team (AI Software Engineering Department)  
**Date:** June 21, 2026  
**Status:** READY FOR PHASE 3 - ARCHITECTURE REVIEW  

**Next Steps:**
1. ✅ Review this SRS & Architecture document
2. ✅ Provide feedback/modifications
3. → Proceed to **Phase 4: Database Detailed Design**
4. → Proceed to **Phase 5: UI Mockups & Wireframes**
5. → Proceed to **Phase 6: Development Planning & Epic Breakdown**
6. → Begin **Phase 7: Module-by-Module Development**

---

**END OF DOCVAULT SRS & ARCHITECTURE DOCUMENT v1.0**
