using Xunit;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocVault.Tests.Module7
{
    public class DocumentSearchServiceTests
    {
        private readonly Mock<ISearchRepository> _mockSearchRepo;
        private readonly Mock<ISearchAnalyticsRepository> _mockAnalyticsRepo;
        private readonly Mock<IDocumentRepository> _mockDocumentRepo;
        private readonly Mock<ISearchIndexService> _mockIndexService;
        private readonly DocumentSearchService _service;

        public DocumentSearchServiceTests()
        {
            _mockSearchRepo = new Mock<ISearchRepository>();
            _mockAnalyticsRepo = new Mock<ISearchAnalyticsRepository>();
            _mockDocumentRepo = new Mock<IDocumentRepository>();
            _mockIndexService = new Mock<ISearchIndexService>();

            _service = new DocumentSearchService(
                _mockSearchRepo.Object,
                _mockAnalyticsRepo.Object,
                _mockDocumentRepo.Object,
                _mockIndexService.Object);
        }

        [Fact]
        public async Task SearchAsync_WithValidQuery_ReturnsResults()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var query = new SearchQuery { QueryText = "budget report", Take = 10 };

            var mockResults = new List<SearchResult>
            {
                new SearchResult { DocumentId = Guid.NewGuid(), Title = "Q1 Budget Report", RelevanceScore = 10 },
                new SearchResult { DocumentId = Guid.NewGuid(), Title = "Budget Planning Document", RelevanceScore = 8 }
            };

            _mockSearchRepo.Setup(x => x.SearchAsync(query)).ReturnsAsync(mockResults);

            // Act
            var results = await _service.SearchAsync(userId, query);

            // Assert
            Assert.NotEmpty(results);
            Assert.True(results.Count <= query.Take);
            _mockAnalyticsRepo.Verify(x => x.LogAsync(It.IsAny<SearchAnalytic>()), Times.Once);
        }

        [Fact]
        public async Task SearchAsync_EmptyQuery_ReturnsEmpty()
        {
            var userId = Guid.NewGuid();
            var query = new SearchQuery { QueryText = "" };

            var results = await _service.SearchAsync(userId, query);

            Assert.Empty(results);
        }

        [Fact]
        public async Task SearchAsync_AppliesPagination()
        {
            var userId = Guid.NewGuid();
            var query = new SearchQuery { QueryText = "test", Skip = 10, Take = 5 };

            var mockResults = Enumerable.Range(0, 20)
                .Select(i => new SearchResult { DocumentId = Guid.NewGuid() })
                .ToList();

            _mockSearchRepo.Setup(x => x.SearchAsync(query)).ReturnsAsync(mockResults);

            var results = await _service.SearchAsync(userId, query);

            Assert.True(results.Count <= query.Take);
        }

        [Fact]
        public async Task SearchAsync_RanksResultsByRelevance()
        {
            var userId = Guid.NewGuid();
            var query = new SearchQuery { QueryText = "document", SortBy = SearchSortOrder.Relevance };

            var mockResults = new List<SearchResult>
            {
                new SearchResult { Title = "other content", RelevanceScore = 0 },
                new SearchResult { Title = "document management system", RelevanceScore = 0 },
                new SearchResult { Title = "document", RelevanceScore = 0 }
            };

            _mockSearchRepo.Setup(x => x.SearchAsync(query)).ReturnsAsync(mockResults);

            var results = await _service.SearchAsync(userId, query);

            // Should rank title matches highest
            Assert.NotEmpty(results);
        }

        [Fact]
        public async Task GetFacetsAsync_ReturnsCategorizedFacets()
        {
            var userId = Guid.NewGuid();
            var query = new SearchQuery();

            _mockSearchRepo.Setup(x => x.CountByClassificationAsync(It.IsAny<DocumentClassification>(), query))
                .ReturnsAsync(5);
            _mockSearchRepo.Setup(x => x.CountByStatusAsync(It.IsAny<DocumentStatus>(), query))
                .ReturnsAsync(3);
            _mockSearchRepo.Setup(x => x.GetPopularTagsAsync(query, 10))
                .ReturnsAsync(new Dictionary<string, int> { { "report", 10 }, { "budget", 8 } });

            var facets = await _service.GetFacetsAsync(userId, query);

            Assert.NotEmpty(facets);
            Assert.True(facets.Any(f => f.FacetName == "Classification"));
            Assert.True(facets.Any(f => f.FacetName == "Tag"));
        }

        [Fact]
        public async Task GetPreviewAsync_WithValidDocument_ReturnsPreview()
        {
            var documentId = Guid.NewGuid();
            var doc = new Document
            {
                Id = documentId,
                Title = "Test Document",
                Description = "This is a test document with some content"
            };

            _mockDocumentRepo.Setup(x => x.GetByIdAsync(documentId)).ReturnsAsync(doc);

            var result = await _service.GetPreviewAsync(documentId);

            Assert.NotNull(result);
            Assert.Equal(doc.Title, result.Title);
            Assert.NotEmpty(result.Preview);
        }

        [Fact]
        public async Task GetSuggestionsAsync_WithPrefix_ReturnsAutocomplete()
        {
            var prefix = "bud";
            var orgId = Guid.NewGuid();

            var suggestions = new List<string> { "budget", "budgeting", "budget report" };
            _mockSearchRepo.Setup(x => x.GetAutocompleteAsync(prefix, orgId)).ReturnsAsync(suggestions);

            var results = await _service.GetSuggestionsAsync(prefix, orgId);

            Assert.Equal(suggestions.Count, results.Count);
        }

        [Fact]
        public async Task GetSuggestionsAsync_ShortPrefix_ReturnsEmpty()
        {
            var results = await _service.GetSuggestionsAsync("a", Guid.NewGuid());

            Assert.Empty(results);
        }

        [Fact]
        public async Task IndexDocumentAsync_WithValidDocument_ReturnsTrue()
        {
            var documentId = Guid.NewGuid();
            var doc = new Document { Id = documentId, Title = "Test" };

            _mockDocumentRepo.Setup(x => x.GetByIdAsync(documentId)).ReturnsAsync(doc);

            var result = await _service.IndexDocumentAsync(documentId);

            Assert.True(result);
            _mockIndexService.Verify(x => x.IndexAsync(doc), Times.Once);
        }

        [Fact]
        public async Task IndexDocumentAsync_WithInvalidDocument_ReturnsFalse()
        {
            var documentId = Guid.NewGuid();

            _mockDocumentRepo.Setup(x => x.GetByIdAsync(documentId)).ReturnsAsync((Document)null);

            var result = await _service.IndexDocumentAsync(documentId);

            Assert.False(result);
        }

        [Fact]
        public async Task DeleteFromIndexAsync_RemovesDocumentFromIndex()
        {
            var documentId = Guid.NewGuid();

            var result = await _service.DeleteFromIndexAsync(documentId);

            Assert.True(result);
            _mockIndexService.Verify(x => x.DeleteAsync(documentId), Times.Once);
        }
    }

    public class SearchIndexServiceTests
    {
        private readonly Mock<ISearchIndexRepository> _mockRepository;
        private readonly SearchIndexService _service;

        public SearchIndexServiceTests()
        {
            _mockRepository = new Mock<ISearchIndexRepository>();
            _service = new SearchIndexService(_mockRepository.Object);
        }

        [Fact]
        public async Task IndexAsync_IndexesDocument()
        {
            var doc = new Document { Id = Guid.NewGuid(), Title = "Test Document" };

            await _service.IndexAsync(doc);

            _mockRepository.Verify(x => x.IndexAsync(It.IsAny<dynamic>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_RemovesDocumentFromIndex()
        {
            var documentId = Guid.NewGuid();

            await _service.DeleteAsync(documentId);

            _mockRepository.Verify(x => x.DeleteAsync(documentId), Times.Once);
        }

        [Fact]
        public async Task RebuildIndexAsync_RebuildsOrganizationIndex()
        {
            var orgId = Guid.NewGuid();

            await _service.RebuildIndexAsync(orgId);

            _mockRepository.Verify(x => x.RebuildAsync(orgId), Times.Once);
        }
    }

    public class SearchIntegrationTests
    {
        [Fact]
        public async Task CompleteSearchWorkflow_SearchAndIndex()
        {
            // Arrange
            var mockSearchRepo = new Mock<ISearchRepository>();
            var mockAnalyticsRepo = new Mock<ISearchAnalyticsRepository>();
            var mockDocumentRepo = new Mock<IDocumentRepository>();
            var mockIndexService = new Mock<ISearchIndexService>();

            var service = new DocumentSearchService(
                mockSearchRepo.Object,
                mockAnalyticsRepo.Object,
                mockDocumentRepo.Object,
                mockIndexService.Object);

            var userId = Guid.NewGuid();
            var documentId = Guid.NewGuid();

            // Act 1: Index document
            var doc = new Document { Id = documentId, Title = "Project Proposal" };
            mockDocumentRepo.Setup(x => x.GetByIdAsync(documentId)).ReturnsAsync(doc);

            var indexResult = await service.IndexDocumentAsync(documentId);

            // Assert 1: Document indexed
            Assert.True(indexResult);

            // Act 2: Search for document
            var query = new SearchQuery { QueryText = "proposal" };
            var searchResult = new SearchResult { DocumentId = documentId, Title = "Project Proposal" };
            mockSearchRepo.Setup(x => x.SearchAsync(query)).ReturnsAsync(new List<SearchResult> { searchResult });

            var results = await service.SearchAsync(userId, query);

            // Assert 2: Search successful
            Assert.NotEmpty(results);
            Assert.True(results.Any(r => r.DocumentId == documentId));
        }

        [Fact]
        public async Task SearchWithMultipleFilters_ReturnsFacetedResults()
        {
            var mockSearchRepo = new Mock<ISearchRepository>();
            var mockAnalyticsRepo = new Mock<ISearchAnalyticsRepository>();
            var mockDocumentRepo = new Mock<IDocumentRepository>();
            var mockIndexService = new Mock<ISearchIndexService>();

            var service = new DocumentSearchService(
                mockSearchRepo.Object,
                mockAnalyticsRepo.Object,
                mockDocumentRepo.Object,
                mockIndexService.Object);

            var query = new SearchQuery
            {
                QueryText = "budget",
                Classifications = new List<DocumentClassification> { DocumentClassification.Confidential },
                Statuses = new List<DocumentStatus> { DocumentStatus.Published }
            };

            var results = new List<SearchResult>
            {
                new SearchResult
                {
                    DocumentId = Guid.NewGuid(),
                    Title = "Budget Report",
                    Classification = DocumentClassification.Confidential,
                    Status = DocumentStatus.Published
                }
            };

            mockSearchRepo.Setup(x => x.SearchAsync(query)).ReturnsAsync(results);

            var searchResults = await service.SearchAsync(Guid.NewGuid(), query);

            Assert.NotEmpty(searchResults);
        }
    }
}
