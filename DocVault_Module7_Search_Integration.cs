using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// ============================================================================
// DOCVAULT MODULE 7: SEARCH & FULL-TEXT INTEGRATION
// ============================================================================
// Full-text search, advanced filtering, result ranking, search analytics

namespace DocVault.Core.Search
{
    // ========================================================================
    // DATA MODELS
    // ========================================================================

    public class SearchQuery
    {
        public string QueryText { get; set; }
        public Guid? OrganizationId { get; set; }
        public Guid? FolderId { get; set; }

        public List<DocumentClassification> Classifications { get; set; } = new();
        public List<DocumentStatus> Statuses { get; set; } = new();
        public List<string> Tags { get; set; } = new();

        public DateTime? CreatedAfter { get; set; }
        public DateTime? CreatedBefore { get; set; }

        public int Skip { get; set; } = 0;
        public int Take { get; set; } = 50;
        public SearchSortOrder SortBy { get; set; } = SearchSortOrder.Relevance;
    }

    public class SearchResult
    {
        public Guid DocumentId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Preview { get; set; }

        public double RelevanceScore { get; set; }
        public DateTime CreatedAt { get; set; }
        public DocumentStatus Status { get; set; }
        public DocumentClassification Classification { get; set; }

        public List<string> MatchedTags { get; set; } = new();
        public Dictionary<string, string> HighlightedMatches { get; set; } = new();
    }

    public class SearchFacet
    {
        public string FacetName { get; set; }
        public int Count { get; set; }
        public string Value { get; set; }
    }

    public class SearchAnalytic
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }

        public string SearchQuery { get; set; }
        public int ResultCount { get; set; }
        public int ClickCount { get; set; }

        public DateTime SearchedAt { get; set; }
        public TimeSpan? ExecutionTime { get; set; }

        public bool IsSuccessful { get; set; }
        public string ErrorMessage { get; set; }
    }

    public enum SearchSortOrder
    {
        Relevance = 0,
        RecencyDesc = 1,
        RecencyAsc = 2,
        TitleAsc = 3,
        TitleDesc = 4,
        SizeAsc = 5,
        SizeDesc = 6
    }

    // ========================================================================
    // SEARCH SERVICE
    // ========================================================================

    public interface IDocumentSearchService
    {
        Task<List<SearchResult>> SearchAsync(Guid userId, SearchQuery query);
        Task<List<SearchFacet>> GetFacetsAsync(Guid userId, SearchQuery baseQuery);
        Task<SearchResult> GetPreviewAsync(Guid documentId);
        Task<List<string>> GetSuggestionsAsync(string prefix, Guid organizationId);
        Task<bool> IndexDocumentAsync(Guid documentId);
        Task<bool> DeleteFromIndexAsync(Guid documentId);
    }

    public class DocumentSearchService : IDocumentSearchService
    {
        private readonly ISearchRepository _searchRepository;
        private readonly ISearchAnalyticsRepository _analyticsRepository;
        private readonly IDocumentRepository _documentRepository;
        private readonly ISearchIndexService _indexService;

        public DocumentSearchService(
            ISearchRepository searchRepository,
            ISearchAnalyticsRepository analyticsRepository,
            IDocumentRepository documentRepository,
            ISearchIndexService indexService)
        {
            _searchRepository = searchRepository;
            _analyticsRepository = analyticsRepository;
            _documentRepository = documentRepository;
            _indexService = indexService;
        }

        public async Task<List<SearchResult>> SearchAsync(Guid userId, SearchQuery query)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                if (string.IsNullOrWhiteSpace(query.QueryText))
                    return new List<SearchResult>();

                // Execute search
                var results = await _searchRepository.SearchAsync(query);

                // Rank results
                var ranked = RankResults(results, query.QueryText);

                // Sort
                var sorted = ApplySorting(ranked, query.SortBy);

                // Paginate
                var paginated = sorted.Skip(query.Skip).Take(query.Take).ToList();

                // Log analytics
                await LogSearchAsync(userId, query.QueryText, paginated.Count, startTime);

                return paginated;
            }
            catch (Exception ex)
            {
                await LogSearchErrorAsync(userId, query.QueryText, ex.Message);
                throw;
            }
        }

        public async Task<List<SearchFacet>> GetFacetsAsync(Guid userId, SearchQuery baseQuery)
        {
            var facets = new List<SearchFacet>();

            // Classification facets
            foreach (var classification in Enum.GetValues(typeof(DocumentClassification)).Cast<DocumentClassification>())
            {
                var count = await _searchRepository.CountByClassificationAsync(classification, baseQuery);
                if (count > 0)
                    facets.Add(new SearchFacet { FacetName = "Classification", Value = classification.ToString(), Count = count });
            }

            // Status facets
            foreach (var status in Enum.GetValues(typeof(DocumentStatus)).Cast<DocumentStatus>())
            {
                var count = await _searchRepository.CountByStatusAsync(status, baseQuery);
                if (count > 0)
                    facets.Add(new SearchFacet { FacetName = "Status", Value = status.ToString(), Count = count });
            }

            // Tag facets
            var tags = await _searchRepository.GetPopularTagsAsync(baseQuery, 10);
            foreach (var tag in tags)
                facets.Add(new SearchFacet { FacetName = "Tag", Value = tag.Key, Count = tag.Value });

            return facets;
        }

        public async Task<SearchResult> GetPreviewAsync(Guid documentId)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document == null) return null;

            return new SearchResult
            {
                DocumentId = document.Id,
                Title = document.Title,
                Description = document.Description,
                Preview = GeneratePreview(document.Description, 200),
                CreatedAt = document.CreatedAt,
                Status = document.Status,
                Classification = document.Classification
            };
        }

        public async Task<List<string>> GetSuggestionsAsync(string prefix, Guid organizationId)
        {
            if (string.IsNullOrWhiteSpace(prefix) || prefix.Length < 2)
                return new List<string>();

            return await _searchRepository.GetAutocompleteAsync(prefix, organizationId);
        }

        public async Task<bool> IndexDocumentAsync(Guid documentId)
        {
            try
            {
                var document = await _documentRepository.GetByIdAsync(documentId);
                if (document == null) return false;

                await _indexService.IndexAsync(document);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteFromIndexAsync(Guid documentId)
        {
            try
            {
                await _indexService.DeleteAsync(documentId);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private List<SearchResult> RankResults(List<SearchResult> results, string queryText)
        {
            var query = queryText.ToLower();
            return results.OrderByDescending(r =>
            {
                double score = 0;

                // Title match (highest weight)
                if (r.Title?.ToLower().Contains(query) == true) score += 10;
                if (r.Title?.ToLower().StartsWith(query) == true) score += 5;

                // Description match
                if (r.Description?.ToLower().Contains(query) == true) score += 5;

                // Tag match
                if (r.MatchedTags?.Any(t => t.ToLower().Contains(query)) == true) score += 3;

                // Recency bonus
                var daysSinceCreated = (DateTime.UtcNow - r.CreatedAt).TotalDays;
                score += Math.Max(0, 2 - (daysSinceCreated / 30));

                r.RelevanceScore = score;
                return score;
            }).ToList();
        }

        private List<SearchResult> ApplySorting(List<SearchResult> results, SearchSortOrder sortOrder)
        {
            return sortOrder switch
            {
                SearchSortOrder.RecencyDesc => results.OrderByDescending(r => r.CreatedAt).ToList(),
                SearchSortOrder.RecencyAsc => results.OrderBy(r => r.CreatedAt).ToList(),
                SearchSortOrder.TitleAsc => results.OrderBy(r => r.Title).ToList(),
                SearchSortOrder.TitleDesc => results.OrderByDescending(r => r.Title).ToList(),
                SearchSortOrder.SizeAsc => results.OrderBy(r => r.DocumentId).ToList(), // Placeholder
                SearchSortOrder.SizeDesc => results.OrderByDescending(r => r.DocumentId).ToList(), // Placeholder
                _ => results.OrderByDescending(r => r.RelevanceScore).ToList()
            };
        }

        private string GeneratePreview(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Length > maxLength ? text.Substring(0, maxLength) + "..." : text;
        }

        private async Task LogSearchAsync(Guid userId, string query, int resultCount, DateTime startTime)
        {
            var analytic = new SearchAnalytic
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SearchQuery = query,
                ResultCount = resultCount,
                SearchedAt = DateTime.UtcNow,
                ExecutionTime = DateTime.UtcNow - startTime,
                IsSuccessful = true
            };

            await _analyticsRepository.LogAsync(analytic);
        }

        private async Task LogSearchErrorAsync(Guid userId, string query, string error)
        {
            var analytic = new SearchAnalytic
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SearchQuery = query,
                SearchedAt = DateTime.UtcNow,
                IsSuccessful = false,
                ErrorMessage = error
            };

            await _analyticsRepository.LogAsync(analytic);
        }
    }

    // ========================================================================
    // SEARCH INDEX SERVICE
    // ========================================================================

    public interface ISearchIndexService
    {
        Task IndexAsync(Document document);
        Task DeleteAsync(Guid documentId);
        Task RebuildIndexAsync(Guid organizationId);
    }

    public class SearchIndexService : ISearchIndexService
    {
        private readonly ISearchIndexRepository _repository;

        public SearchIndexService(ISearchIndexRepository repository)
        {
            _repository = repository;
        }

        public async Task IndexAsync(Document document)
        {
            var indexEntry = new
            {
                document.Id,
                document.Title,
                document.Description,
                document.Tags,
                document.Status,
                document.Classification,
                document.CreatedAt,
                document.OrganizationId
            };

            await _repository.IndexAsync(indexEntry);
        }

        public async Task DeleteAsync(Guid documentId)
        {
            await _repository.DeleteAsync(documentId);
        }

        public async Task RebuildIndexAsync(Guid organizationId)
        {
            await _repository.RebuildAsync(organizationId);
        }
    }

    // ========================================================================
    // REPOSITORY INTERFACES
    // ========================================================================

    public interface ISearchRepository
    {
        Task<List<SearchResult>> SearchAsync(SearchQuery query);
        Task<int> CountByClassificationAsync(DocumentClassification classification, SearchQuery baseQuery);
        Task<int> CountByStatusAsync(DocumentStatus status, SearchQuery baseQuery);
        Task<Dictionary<string, int>> GetPopularTagsAsync(SearchQuery baseQuery, int topN);
        Task<List<string>> GetAutocompleteAsync(string prefix, Guid organizationId);
    }

    public interface ISearchAnalyticsRepository
    {
        Task LogAsync(SearchAnalytic analytic);
        Task<List<SearchAnalytic>> GetSearchHistoryAsync(Guid userId, int days = 30);
    }

    public interface ISearchIndexRepository
    {
        Task IndexAsync(dynamic entry);
        Task DeleteAsync(Guid documentId);
        Task RebuildAsync(Guid organizationId);
    }
}
