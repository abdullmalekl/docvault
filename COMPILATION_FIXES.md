# DocVault Compilation Fix Strategy

## Status
- ✅ Added DocVault.Models.cs with missing core entities (Organization, LoginHistory)
- ⏳ Interface/Implementation mismatches still require fixes

## Critical Issues (CS0535/CS0738)

### Service Classes with Wrong Signatures
The following services declare interfaces but implementations don't match:

1. **EncryptionService** — Missing Encrypt/Decrypt methods
   - Interface: IEncryptionService
   - Missing: async Task<string> EncryptAsync(string data, Guid keyId)
   - Missing: async Task<string> DecryptAsync(string encryptedData, Guid keyId)

2. **DocumentWorkflowService** — Wrong return types
   - Method: ApproveStageAsync returns Task but should return ApprovalTask or void
   - Method: RejectStageAsync returns Task but should return ApprovalTask or void

3. **AuditService** — Missing methods
   - Missing: LogAccessAsync(Guid userId, Guid resourceId, string action)
   - Missing: LogModificationAsync(Guid userId, Guid resourceId, string changes)
   - Missing: GetAuditTrailAsync(Guid resourceId)

4. **NotificationService** — Missing methods  
   - Missing: SendEmailAsync(string to, string subject, string body)
   - Missing: SendSlackAsync(string channel, string message)
   - Missing: SendWebhookAsync(string endpoint, object payload)

5. **EmailService** — Missing implementation
   - Missing: SendAsync(EmailMessage message)

6. **AnalyticsService** — Missing methods
   - Missing: GetDocumentMetricsAsync(Guid organizationId)
   - Missing: GetOrganizationMetricsAsync(Guid organizationId)
   - Missing: TrackSearchQueryAsync(Guid userId, string query, int resultCount)

7. **DocumentSearchService** — Missing methods
   - Missing: SearchAsync(SearchQuery query)
   - Missing: FacetedSearchAsync(SearchQuery query, string[] facets)
   - Missing: IndexDocumentAsync(Document document)

## Fix Options

### Option A (Recommended for Railway deployment)
- Remove `: IServiceInterface` declarations from classes that don't fully implement them
- Keep the implementations as concrete classes
- Deploy and get build working
- Fix interfaces and method signatures in follow-up commits

### Option B (Complete but slower)  
- Implement all missing methods in each service class
- Align return types and signatures with interface declarations
- Add proper implementations instead of stubs

### Option C (Hybrid)
- Keep interface declarations but make them optional implementations
- Use abstract methods and extension methods where needed

## Next Steps
1. Run `dotnet build` after committing DocVault.Models.cs
2. Review actual compiler output for exact mismatches
3. Apply targeted fixes based on actual vs. expected signatures
4. Test with Railway deployment
