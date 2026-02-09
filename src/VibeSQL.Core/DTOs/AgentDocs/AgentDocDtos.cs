using System.ComponentModel.DataAnnotations;

namespace VibeSQL.Core.DTOs.AgentDocs;

/// <summary>
/// Valid document types for agent documents.
/// </summary>
public static class AgentDocTypes
{
    public const string Spec = "spec";
    public const string Report = "report";
    public const string Review = "review";
    public const string Draft = "draft";
    public const string Archive = "archive";
    public const string Reference = "reference";

    public static readonly string[] All = { Spec, Report, Review, Draft, Archive, Reference };

    public static bool IsValid(string? docType) =>
        !string.IsNullOrEmpty(docType) && All.Contains(docType.ToLowerInvariant());
}

/// <summary>
/// Agent document DTO for API responses.
/// </summary>
public class AgentDocumentDto
{
    public int DocumentId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }  // Omitted in list responses
    public string DocType { get; set; } = AgentDocTypes.Draft;
    public int Version { get; set; } = 1;
    public int? ParentDocumentId { get; set; }
    public long ContentSizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
}

/// <summary>
/// Request to create a new document.
/// </summary>
public class CreateAgentDocRequest
{
    [Required, MaxLength(128)]
    public string AgentName { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(102400)]  // 100KB max
    public string Content { get; set; } = string.Empty;

    [Required]
    public string DocType { get; set; } = AgentDocTypes.Draft;
}

/// <summary>
/// Request to update a document (creates new version).
/// </summary>
public class UpdateAgentDocRequest
{
    [MaxLength(256)]
    public string? Title { get; set; }

    [MaxLength(102400)]  // 100KB max
    public string? Content { get; set; }

    public string? DocType { get; set; }
}

/// <summary>
/// Options for listing documents.
/// </summary>
public class AgentDocListOptions
{
    public string? DocType { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public bool IncludeDeleted { get; set; } = false;
}

/// <summary>
/// Result from uploading a document.
/// </summary>
public class AgentDocUploadResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public AgentDocumentDto? Document { get; set; }
}

/// <summary>
/// Result from listing documents.
/// </summary>
public class AgentDocListResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string Agent { get; set; } = string.Empty;
    public List<AgentDocumentDto> Documents { get; set; } = new();
    public AgentDocPagination Pagination { get; set; } = new();
}

/// <summary>
/// Pagination info for list results.
/// </summary>
public class AgentDocPagination
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

/// <summary>
/// Result from getting a single document.
/// </summary>
public class AgentDocGetResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public AgentDocumentDto? Document { get; set; }
}

/// <summary>
/// Result from deleting a document.
/// </summary>
public class AgentDocDeleteResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int DocumentId { get; set; }
    public bool Deleted { get; set; }
}

/// <summary>
/// Result from getting version history.
/// </summary>
public class AgentDocHistoryResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int DocumentId { get; set; }
    public int CurrentVersion { get; set; }
    public List<AgentDocVersionDto> Versions { get; set; } = new();
}

/// <summary>
/// Version info for history display.
/// </summary>
public class AgentDocVersionDto
{
    public int DocumentId { get; set; }
    public int Version { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
}
