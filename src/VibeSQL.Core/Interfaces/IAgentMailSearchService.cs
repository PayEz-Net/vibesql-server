// File: VibeSQL.Core/Interfaces/IAgentMailSearchService.cs
// Interface for Agent Mail Search Service

using VibeSQL.Core.DTOs.AgentMail;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for full-text search across agent mail messages.
/// </summary>
public interface IAgentMailSearchService
{
    /// <summary>
    /// Search messages by keyword with optional filters.
    /// </summary>
    /// <param name="clientId">Client/tenant ID</param>
    /// <param name="agentId">Agent performing the search</param>
    /// <param name="query">Search query (min 2 chars)</param>
    /// <param name="from">Filter by sender agent name</param>
    /// <param name="to">Filter by recipient agent name</param>
    /// <param name="after">Messages after this date</param>
    /// <param name="before">Messages before this date</param>
    /// <param name="threadId">Filter by thread ID</param>
    /// <param name="unreadOnly">Only unread messages</param>
    /// <param name="limit">Results per page (max 100)</param>
    /// <param name="offset">Pagination offset</param>
    /// <param name="sort">Sort order: relevance, date_desc, date_asc</param>
    /// <param name="highlight">Include highlighted snippets</param>
    /// <returns>Search results with pagination info</returns>
    Task<SearchResponse> SearchAsync(
        int clientId,
        int agentId,
        string query,
        string? from = null,
        string? to = null,
        DateTime? after = null,
        DateTime? before = null,
        string? threadId = null,
        bool? unreadOnly = null,
        int limit = 20,
        int offset = 0,
        string sort = "relevance",
        bool highlight = true);
    
    /// <summary>
    /// Advanced search with structured query and filters.
    /// </summary>
    /// <param name="clientId">Client/tenant ID</param>
    /// <param name="agentId">Agent performing the search</param>
    /// <param name="request">Advanced search request</param>
    /// <returns>Search results</returns>
    Task<SearchResponse> AdvancedSearchAsync(int clientId, int agentId, AdvancedSearchRequest request);
    
    /// <summary>
    /// Get search suggestions based on history and common terms.
    /// </summary>
    /// <param name="clientId">Client/tenant ID</param>
    /// <param name="agentId">Agent ID</param>
    /// <param name="partialQuery">Partial query for autocomplete (optional)</param>
    /// <param name="limit">Max suggestions</param>
    /// <returns>Recent searches and suggestions</returns>
    Task<SuggestionsResponse> GetSuggestionsAsync(int clientId, int agentId, string? partialQuery = null, int limit = 10);
    
    /// <summary>
    /// Clear search history for an agent.
    /// </summary>
    /// <param name="clientId">Client/tenant ID</param>
    /// <param name="agentId">Agent ID</param>
    /// <returns>Number of entries deleted</returns>
    Task<ClearHistoryResponse> ClearSearchHistoryAsync(int clientId, int agentId);
}
