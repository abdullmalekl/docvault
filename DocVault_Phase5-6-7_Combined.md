# DocVault — Phase 5 + 6 + 7
## UI/UX Design + Sprint Planning + Search Engine Architecture
**Version:** 1.0  
**Date:** June 21, 2026  

---

# PHASE 5: UI/UX DESIGN

## Main Screen (من الملف)

```
┌─────────────────────────────────────────────────────────────┐
│                    DOCVAULT DASHBOARD                       │
├─────────────────────────────────────────────────────────────┤
│  [☰] DocVault  [🔍 Search]  [📊 Reports]  [👤 Admin]  [👤] │
├──────────────┬───────────────────────────────────────────────┤
│              │                                               │
│ DEPARTMENTS  │              MAIN CONTENT AREA                │
│              │                                               │
│ ├─ HR        │  📊 Quick Stats:                              │
│ ├─ Finance   │  ├─ Documents: 125,432                        │
│ ├─ Ops       │  ├─ Today: 47 new                             │
│ └─ Audit     │  ├─ Users: 32 active                          │
│              │  └─ Searches: 1,205                           │
│ [+] New Dept │                                               │
│              │  📄 Recent Documents:                          │
│              │  ┌────────────────────────────────────────────┐ │
│              │  │ DOC-2026-0145 │ HR Policy 2026 │ 2026-06-20│ │
│              │  │ DOC-2026-0144 │ Financial.. │ 2026-06-20│ │
│              │  │ DOC-2026-0143 │ Operational │ 2026-06-19│ │
│              │  └────────────────────────────────────────────┘ │
│              │                                               │
│              │  🔎 Quick Search:                             │
│              │  ┌──────────────────────────────────────────┐ │
│              │  │ [Search documents...]        [🔍 Search]│ │
│              │  └──────────────────────────────────────────┘ │
│              │                                               │
├──────────────┴───────────────────────────────────────────────┤
│ Status: Online | Sync: OK | Last Backup: 2026-06-21 02:00    │
└─────────────────────────────────────────────────────────────┘
```

## Screen Layout Design System

### 1. Color Palette
```
Primary Blue:     #1F73B7
Success Green:    #28A745
Danger Red:       #DC3545
Warning Yellow:   #FFC107
Background:       #F5F5F5
Text Dark:        #333333
Text Light:       #666666
Border:           #DDDDDD
```

### 2. Typography
```
Heading 1:  Segoe UI, 24px, Bold      (Page titles)
Heading 2:  Segoe UI, 18px, Bold      (Section titles)
Body:       Segoe UI, 13px, Regular   (Text content)
Small:      Segoe UI, 11px, Regular   (Help text)
Mono:       Consolas, 11px, Regular   (IDs, codes)
```

### 3. Spacing Grid
```
xs: 4px   | sm: 8px   | md: 16px  | lg: 24px  | xl: 32px
```

### 4. Component Library

#### Button Styles
```
Primary Button:
  ┌──────────────┐
  │  Save       │  Background: #1F73B7, Color: White
  └──────────────┘  Hover: #155A8A

Secondary Button:
  ┌──────────────┐
  │  Cancel     │  Background: #F5F5F5, Color: #333
  └──────────────┘  Hover: #E0E0E0

Danger Button:
  ┌──────────────┐
  │  Delete     │  Background: #DC3545, Color: White
  └──────────────┘  Hover: #BB2D3B
```

#### Input Fields
```
Text Input:
  ┌─────────────────────────────────┐
  │ [Label]                         │
  │ ┌─────────────────────────────┐ │
  │ │ Enter text...              │ │
  │ └─────────────────────────────┘ │
  └─────────────────────────────────┘

Dropdown:
  ┌─────────────────────────────────┐
  │ [Select Category...]    [▼]     │
  ├─────────────────────────────────┤
  │ ✓ Incoming                      │
  │   Outgoing                      │
  │   Internal                      │
  └─────────────────────────────────┘
```

#### Tables
```
┌─────────────┬──────────────┬──────────┬────────────┐
│ Document #  │ Title        │ Category │ Date       │
├─────────────┼──────────────┼──────────┼────────────┤
│ DOC-2026-01 │ HR Policy    │ Incoming │ 2026-06-20 │
│ DOC-2026-02 │ Budget 2026  │ Finance  │ 2026-06-19 │
│ DOC-2026-03 │ Operations   │ Internal │ 2026-06-18 │
└─────────────┴──────────────┴──────────┴────────────┘
```

## Screen Specifications (10 Screens)

### Screen 1: Login
- Username field (required, min 3 chars)
- Password field (required, min 12 chars)
- Google Authenticator 2FA input
- "Remember Me" checkbox
- "Forgot Password?" link
- Language selector (Arabic/English)
- Status: Online/Offline indicator

### Screen 2: Main Dashboard (shown above)
- Department tree (left sidebar)
- Quick stats (top right)
- Recent documents (center)
- Quick search (bottom)

### Screen 3: Advanced Search
- Multi-criteria form:
  - Document number
  - Date range (from/to)
  - Subject field
  - Department dropdown
  - Category dropdown
  - Status dropdown
  - Keywords input
- Results table (sortable)
- Save search option
- Export results (PDF/Excel)

### Screen 4: Document Viewer
- Full-page display
- Zoom controls (+/-, fit page, fit width)
- Page navigation (First, Prev, Next, Last)
- Thumbnail sidebar (scrollable)
- Rotate control
- Print button
- Download button
- Share button

### Screen 5: Document Editor
- Metadata form:
  - Document number (read-only, auto-generated)
  - Title (required)
  - Subject (required)
  - Department (dropdown, required)
  - Category (dropdown, required)
  - Status (dropdown, required)
  - Physical location (optional)
  - Keywords (comma-separated, optional)
- Related documents section
- Attachments upload
- Revision history
- Save/Cancel buttons

### Screen 6: Scanning Module
- Scanner selection (dropdown)
- Settings panel:
  - DPI: 300/600
  - Color mode: RGB/Gray/B&W
  - Page size: A4/A3/Legal (auto)
- Live preview
- Batch mode toggle
- Start/Stop scanning buttons
- Progress bar
- Save as document button

### Screen 7: User Management (Admin)
- User table:
  - Username | Email | Department | Role | Status | Actions
- Add user button
- Edit/Delete user dialogs
- Bulk actions (Enable/Disable/Delete)
- Filter by role/department
- Search users

### Screen 8: Reporting Dashboard
- Pre-built report cards:
  - Daily Statistics
  - Category Distribution
  - Audit Trail
  - User Statistics
  - Linked Documents
- Custom report builder
- Chart types (Line, Pie, Bar)
- Export buttons (PDF/Excel)
- Schedule report

### Screen 9: Audit Log Viewer
- Filter section:
  - User dropdown
  - Action type (Create/Edit/View/Delete/Print)
  - Date range
  - Document ID
  - Status (Success/Failure)
- Results table:
  - User | Action | Table | Record | Timestamp | Status
- Export audit log
- Search audit logs

### Screen 10: System Configuration (Admin)
- Tabs:
  - Document Numbering (Format + Next #)
  - Departments (Tree manager)
  - Categories (Add/Edit/Delete)
  - Status Types (Add/Edit/Delete)
  - Custom Fields (Add/Remove)
  - Backup Settings (Frequency, Retention, Location)
  - Sync Settings (Frequency, Bandwidth limits)
  - Security (Encryption, Password policy)

## Navigation Flow Diagram

```
┌─────────────┐
│ Login Page  │
└──────┬──────┘
       │ (Success)
       ▼
┌──────────────────────────────────────────┐
│  Main Dashboard                          │
│  ├─ [🔍 Search Documents]   ────────────┐│
│  ├─ [📱 Scanning Module]     ───────────┐││
│  ├─ [📄 View/Edit Document]  ──────────┐│││
│  ├─ [📊 Reports] (All users) ──────────┐││││
│  ├─ [👥 User Management]    ┐(Admin)   ┐│││││
│  ├─ [⚙️ System Config]      ├──────────┐││││││
│  ├─ [📋 Audit Log]          │          ┐│││││││
│  └─ [Logout]                │          ┐│││││││
└──────────────────────────────┼──────────┘│││││││
                               │           │││││││
                 ┌─────────────┴───────────┘││││││
                 │                         │││││
              ┌──▼─────┐ ┌────┬────┬────┬──▼──┐
              │ Search  │ │Scan│View│Edit│List │
              └─────────┘ └────┴────┴────┴─────┘
```

---

# PHASE 6: SPRINT PLANNING & DEVELOPMENT ROADMAP

## Development Timeline

### Sprint 1-2 (Weeks 1-2): Foundation & Setup
- [ ] Project structure (WPF MVVM)
- [ ] Database setup & migration
- [ ] Authentication system (Local + Domain)
- [ ] Basic UI framework
- [ ] Login screen implementation

**Deliverable:** Login working, database ready

### Sprint 3-4 (Weeks 3-4): Core Document Management
- [ ] Document ingestion (scanner TWAIN)
- [ ] OCR integration
- [ ] Database operations (CRUD)
- [ ] Document viewer screen
- [ ] Page navigation

**Deliverable:** Scan documents, view, basic search

### Sprint 5-6 (Weeks 5-6): Search & Advanced Features
- [ ] Full-text search (Lucene.NET)
- [ ] Multi-criteria search
- [ ] Document relationships
- [ ] Keywords extraction
- [ ] Search history

**Deliverable:** Advanced search working, relationships

### Sprint 7-8 (Weeks 7-8): User Management & Security
- [ ] User management screen
- [ ] Role-based access control (RBAC)
- [ ] Google Authenticator MFA
- [ ] Encryption (AES-256)
- [ ] Audit logging

**Deliverable:** Multi-user secure system

### Sprint 9-10 (Weeks 9-10): Sync & Offline Mode
- [ ] Network sync engine
- [ ] Offline queue manager
- [ ] Conflict resolution
- [ ] Sync status indicators
- [ ] Bandwidth throttling

**Deliverable:** Network deployment ready

### Sprint 11-12 (Weeks 11-12): Reporting & Admin
- [ ] Pre-built reports
- [ ] Dashboard design
- [ ] Custom reports
- [ ] Admin configuration screens
- [ ] System settings

**Deliverable:** Full reporting & admin features

### Sprint 13-14 (Weeks 13-14): Backup & Monitoring
- [ ] Backup scheduler
- [ ] Automated backups (daily)
- [ ] Restore functionality
- [ ] System monitoring
- [ ] Health checks

**Deliverable:** Backup system production-ready

### Sprint 15 (Week 15): QA & Polish
- [ ] End-to-end testing
- [ ] Security audit
- [ ] Performance testing
- [ ] UI/UX refinement
- [ ] Documentation

**Deliverable:** v1.0 ready for pilot

---

## Epic Breakdown (8 Epics)

### Epic 1: Document Ingestion & OCR
**Story Points:** 34

- Task 1.1: Scanner TWAIN integration (13 pts)
  - Support multi-page scanning
  - DPI/color configuration
  - Auto-feed support
  - Progress indicators
  
- Task 1.2: OCR engine (13 pts)
  - Tesseract integration
  - Arabic + English support
  - Text extraction
  - Quality scoring
  
- Task 1.3: File uploads (8 pts)
  - PDF/Word/Excel support
  - Image support
  - Batch upload
  - Validation

### Epic 2: Document Management
**Story Points:** 34

- Task 2.1: CRUD operations (13 pts)
  - Create document
  - Edit metadata
  - View document
  - Soft delete
  
- Task 2.2: Document relationships (13 pts)
  - Link documents
  - Reply/Forward chains
  - Relationship visualization
  
- Task 2.3: Versioning (8 pts)
  - Version history
  - Rollback
  - Change tracking

### Epic 3: Search & Retrieval
**Story Points:** 40

- Task 3.1: Full-text search (21 pts)
  - Lucene.NET integration
  - Content indexing
  - Real-time search
  - Ranking algorithm
  
- Task 3.2: Multi-criteria search (13 pts)
  - Date range
  - Category filter
  - Department filter
  - Status filter
  
- Task 3.3: Search history (6 pts)
  - Save searches
  - Recent searches

### Epic 4: User & Security
**Story Points:** 48

- Task 4.1: Authentication (16 pts)
  - Local accounts
  - Domain (LDAP)
  - Google Authenticator
  - Password reset
  
- Task 4.2: Authorization (16 pts)
  - RBAC system
  - Document-level permissions
  - Role management
  
- Task 4.3: Encryption & Audit (16 pts)
  - AES-256 at rest
  - TLS in transit
  - Audit logging
  - Compliance reports

### Epic 5: Sync & Offline
**Story Points:** 35

- Task 5.1: Sync engine (16 pts)
  - Network sync
  - Delta sync
  - Conflict resolution
  
- Task 5.2: Offline mode (16 pts)
  - Offline queue
  - Auto-sync on reconnect
  - Bandwidth management
  
- Task 5.3: Status & monitoring (3 pts)
  - Online/offline indicators

### Epic 6: Reporting & Analytics
**Story Points:** 32

- Task 6.1: Pre-built reports (16 pts)
  - Daily statistics
  - Category distribution
  - User activity
  - Audit summary
  
- Task 6.2: Dashboard (13 pts)
  - Widgets
  - Charts (Line, Pie, Bar)
  - Real-time metrics
  
- Task 6.3: Export (3 pts)
  - PDF export
  - Excel export

### Epic 7: Admin & Configuration
**Story Points:** 24

- Task 7.1: Organization structure (8 pts)
  - Department management
  - Tree hierarchy
  
- Task 7.2: User management (8 pts)
  - CRUD operations
  - Bulk actions
  
- Task 7.3: System settings (8 pts)
  - Custom fields
  - Document numbering
  - Backup configuration

### Epic 8: Testing & Deployment
**Story Points:** 40

- Task 8.1: Unit tests (13 pts)
  - >80% coverage
  - NUnit
  
- Task 8.2: Integration tests (13 pts)
  - Database tests
  - API tests
  
- Task 8.3: E2E tests (8 pts)
  - User workflows
  - Selenium
  
- Task 8.4: Deployment (6 pts)
  - Installer
  - Database migration
  - Documentation

---

## Complexity Estimation

**T-Shirt Sizes:**
```
S  = 1-3 days
M  = 3-5 days
L  = 1 week
XL = 2+ weeks
```

**Dependencies:**
```
Phase 1 (Auth) → Phase 2 (Docs) → Phase 3 (Search)
Phase 2 (Docs) → Phase 4 (Sync)
Phase 3 (Search) → Phase 5 (Reports)
All → Phase 6 (Testing) → Phase 7 (Release)
```

---

# PHASE 7: SEARCH ENGINE ARCHITECTURE

## Full-Text Search Design

### Technology Stack
```
Search Library:    Lucene.NET 4.8.0
Indexing:         Async background process
Query Parser:     Standard query parser
Analyzer:         Multilingual (Arabic + English)
Storage:          File-based index on disk
```

### Search Index Structure

```
Document Index:
├─ Field: DocumentNumber (Stored, Indexed, Not Analyzed)
├─ Field: Title (Stored, Indexed, Analyzed)
├─ Field: Subject (Stored, Indexed, Analyzed)
├─ Field: Content (Stored, Indexed, Analyzed, TermVector)
├─ Field: Keywords (Stored, Indexed, Analyzed)
├─ Field: DocumentDate (Stored, Indexed, Not Analyzed)
├─ Field: DepartmentID (Stored, Indexed, Not Analyzed)
├─ Field: CategoryID (Stored, Indexed, Not Analyzed)
├─ Field: StatusID (Stored, Indexed, Not Analyzed)
└─ Field: CreatedAt (Stored, Indexed, Not Analyzed)

Analyzer Configuration:
├─ Arabic Analyzer: Diacritics removal, stemming
├─ English Analyzer: Porter stemming, stop words
└─ Combined: Support both languages in single index
```

### Search Query Architecture

```
User Query Input
    ↓
Query Parser (Lucene)
    ├─ Single term: "policy"
    ├─ Phrase: "HR policy"
    ├─ Boolean: "HR AND policy NOT 2025"
    ├─ Range: date:[2026-01-01 TO 2026-12-31]
    └─ Wildcard: "polic*"
    ↓
Query Executor
    ├─ Convert to Lucene Query
    ├─ Apply filters (department, category, date)
    ├─ Score results (TF-IDF)
    ├─ Rank by relevance
    └─ Paginate results (10/page)
    ↓
Results Formatter
    ├─ Highlight matches in context
    ├─ Display top 3 matched snippets
    ├─ Show relevance score
    └─ Return to UI
```

### Indexing Strategy

**Indexing Modes:**
```
1. Initial Indexing (on-demand):
   - When document created
   - Scan all pages
   - Extract OCR text
   - Build full-text index
   - Time: ~30 seconds per document

2. Incremental Indexing (background):
   - New documents added to index queue
   - Process queue every 5 minutes
   - Batch update index
   - Lock-free concurrent updates

3. Re-indexing (scheduled):
   - Weekly full re-index (2 AM)
   - Rebuild entire search index
   - Verify index integrity
   - Backup index
```

### Query Performance Optimization

**Indexes:**
```
Primary Index: Content (full-text)
Supporting Indexes:
  - Title (phrase queries)
  - Keywords (tag-based search)
  - DocumentDate (range queries)
  - DocumentNumber (exact match)
```

**Caching Strategy:**
```
Query Result Cache:
  - Cache top 1000 queries
  - TTL: 1 hour
  - Invalidate on: Index update
  - Hit rate: ~70% typical

Page Cache:
  - Cache pages 0-3 (first 30 results)
  - TTL: 30 minutes
  - Size: ~5MB per query
```

**Expected Performance:**
```
Document Count:    1,000,000
Index Size:        ~5-10 GB
Average Query Time: <2 seconds
Concurrent Users:  50+
Throughput:        10,000 searches/day
```

### Search API Endpoints

```
POST /api/search
{
  "query": "HR policy",
  "criteria": {
    "departmentId": 1,
    "categoryId": 2,
    "startDate": "2026-01-01",
    "endDate": "2026-12-31",
    "status": "Active"
  },
  "page": 1,
  "pageSize": 10,
  "sort": "relevance"  // relevance, date, title
}

Response:
{
  "success": true,
  "data": {
    "totalHits": 1234,
    "currentPage": 1,
    "pageSize": 10,
    "results": [
      {
        "documentId": 1001,
        "documentNumber": "DOC-2026-0145",
        "title": "HR Policy 2026",
        "relevanceScore": 0.95,
        "snippet": "...<b>HR policy</b> effective from January 2026...",
        "documentDate": "2026-01-15",
        "department": "Human Resources"
      }
    ],
    "facets": {
      "departments": [{"name": "HR", "count": 234}],
      "categories": [{"name": "Incoming", "count": 567}],
      "dateRange": {"min": "2026-01-01", "max": "2026-12-31"}
    }
  }
}
```

### Advanced Search Features

```
1. Faceted Search:
   - Department facet
   - Category facet
   - Status facet
   - Date range slider
   - Drill-down navigation

2. Did You Mean?
   - Fuzzy matching
   - Spell checking
   - Common misspellings

3. Search Suggestions:
   - Auto-complete
   - Popular searches
   - Recent searches (per user)

4. Saved Searches:
   - Save complex queries
   - Name & description
   - Scheduled reports
   - Export results
```

### Search Quality Metrics

```
Metrics to Track:
  - Query success rate (>99%)
  - Average query time (<2s)
  - Index freshness (<5 min)
  - Zero-result queries (should be <5%)
  - Click-through rate (engagement)

Tools:
  - Log all searches
  - Track user interactions
  - A/B test ranking algorithms
  - Monitor performance
```

---

## Summary

**Phase 5:** UI Design system + 10 screens + navigation flow
**Phase 6:** Sprint planning (15 weeks) + 8 epics + story points
**Phase 7:** Full-text search architecture + Lucene.NET integration + Performance optimization

**Total Development Time:** 15 weeks (3.5 months)
**Team Size:** 5-8 developers
**QA Time:** 2 weeks
**Pilot Deployment:** Week 15

---

**READY FOR:** Module-by-Module Development (Phase 7.1: Document Ingestion Module)
