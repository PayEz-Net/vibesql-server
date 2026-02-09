// File: VibeSQL.Core.DTOs/Vibe/AgentMail/SearchDtos.cs
// Search DTOs for Agent Mail

using System.Text.Json.Serialization;

namespace VibeSQL.Core.DTOs.AgentMail;

/// <summary>
/// Summary of an agent for search results.
/// </summary>
public record AgentSummary
{
    [JsonPropertyName("id")]
    public int Id { get; init; }
    
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";
}

/// <summary>
/// Single search result.
/// </summary>
public record SearchResult
{
    [JsonPropertyName("message_id")]
    public long MessageId { get; init; }
    
    [JsonPropertyName("subject")]
    public string Subject { get; init; } = "";
    
    [JsonPropertyName("subject_highlighted")]
    public string SubjectHighlighted { get; init; } = "";
    
    [JsonPropertyName("body_snippet")]
    public string BodySnippet { get; init; } = "";
    
    [JsonPropertyName("from_agent")]
    public AgentSummary FromAgent { get; init; } = new();
    
    [JsonPropertyName("to_agents")]
    public List<AgentSummary> ToAgents { get; init; } = new();
    
    [JsonPropertyName("sent_at")]
    public DateTime SentAt { get; init; }
    
    [JsonPropertyName("thread_id")]
    public string ThreadId { get; init; } = "";
    
    [JsonPropertyName("read")]
    public bool Read { get; init; }
    
    [JsonPropertyName("relevance_score")]
    public double RelevanceScore { get; init; }
}

/// <summary>
/// Response for GET /v1/agentmail/search
/// </summary>
public record SearchResponse
{
    [JsonPropertyName("query")]
    public string Query { get; init; } = "";
    
    [JsonPropertyName("filters")]
    public Dictionary<string, string?> Filters { get; init; } = new();
    
    [JsonPropertyName("results")]
    public List<SearchResult> Results { get; init; } = new();
    
    [JsonPropertyName("total_count")]
    public int TotalCount { get; init; }
    
    [JsonPropertyName("returned_count")]
    public int ReturnedCount { get; init; }
    
    [JsonPropertyName("has_more")]
    public bool HasMore { get; init; }
    
    [JsonPropertyName("search_time_ms")]
    public int SearchTimeMs { get; init; }
}

/// <summary>
/// Search suggestion item.
/// </summary>
public record SearchSuggestion
{
    [JsonPropertyName("query")]
    public string Query { get; init; } = "";
    
    [JsonPropertyName("searched_at")]
    public DateTime? SearchedAt { get; init; }
    
    [JsonPropertyName("match_count")]
    public int? MatchCount { get; init; }
}

/// <summary>
/// Response for GET /v1/agentmail/search/suggestions
/// </summary>
public record SuggestionsResponse
{
    [JsonPropertyName("recent_searches")]
    public List<SearchSuggestion> RecentSearches { get; init; } = new();
    
    [JsonPropertyName("suggestions")]
    public List<SearchSuggestion> Suggestions { get; init; } = new();
}

/// <summary>
/// Response for DELETE /v1/agentmail/search/history
/// </summary>
public record ClearHistoryResponse
{
    [JsonPropertyName("deleted_count")]
    public int DeletedCount { get; init; }
}

/// <summary>
/// Request for POST /v1/agentmail/search/advanced
/// </summary>
public record AdvancedSearchRequest
{
    [JsonPropertyName("query")]
    public required SearchQuerySpec Query { get; init; }
    
    [JsonPropertyName("filters")]
    public SearchFilters? Filters { get; init; }
    
    [JsonPropertyName("pagination")]
    public PaginationSpec? Pagination { get; init; }
    
    [JsonPropertyName("sort")]
    public SortSpec? Sort { get; init; }
    
    [JsonPropertyName("options")]
    public SearchOptions? Options { get; init; }
}

/// <summary>
/// Query specification for advanced search.
/// </summary>
public record SearchQuerySpec
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }
    
    [JsonPropertyName("fields")]
    public List<string>? Fields { get; init; }
}

/// <summary>
/// Filters for advanced search.
/// </summary>
public record SearchFilters
{
    [JsonPropertyName("from_agents")]
    public List<int>? FromAgents { get; init; }
    
    [JsonPropertyName("to_agents")]
    public List<int>? ToAgents { get; init; }
    
    [JsonPropertyName("date_range")]
    public DateRangeFilter? DateRange { get; init; }
    
    [JsonPropertyName("threads")]
    public List<string>? Threads { get; init; }
    
    [JsonPropertyName("importance")]
    public List<string>? Importance { get; init; }
    
    [JsonPropertyName("has_mentions")]
    public bool? HasMentions { get; init; }
    
    [JsonPropertyName("unread_only")]
    public bool? UnreadOnly { get; init; }
}

/// <summary>
/// Date range filter for search.
/// </summary>
public record DateRangeFilter
{
    [JsonPropertyName("after")]
    public DateTime? After { get; init; }
    
    [JsonPropertyName("before")]
    public DateTime? Before { get; init; }
}

/// <summary>
/// Pagination specification.
/// </summary>
public record PaginationSpec
{
    [JsonPropertyName("limit")]
    public int Limit { get; init; } = 20;
    
    [JsonPropertyName("offset")]
    public int Offset { get; init; } = 0;
}

/// <summary>
/// Sort specification.
/// </summary>
public record SortSpec
{
    [JsonPropertyName("field")]
    public string Field { get; init; } = "relevance";
    
    [JsonPropertyName("direction")]
    public string Direction { get; init; } = "desc";
}

/// <summary>
/// Search options.
/// </summary>
public record SearchOptions
{
    [JsonPropertyName("highlight")]
    public bool Highlight { get; init; } = true;
    
    [JsonPropertyName("snippet_length")]
    public int SnippetLength { get; init; } = 150;
}

/// <summary>
/// Parsed query from search query parser.
/// </summary>
public class ParsedQuery
{
    public List<string> Terms { get; set; } = new();
    public List<string> Phrases { get; set; } = new();
    public Dictionary<string, string> FieldTerms { get; set; } = new();
    public List<string> Exclusions { get; set; } = new();
    public bool HasOrOperator { get; set; }
}
