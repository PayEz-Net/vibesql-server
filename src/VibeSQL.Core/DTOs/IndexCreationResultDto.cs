namespace VibeSQL.Core.DTOs;

/// <summary>
/// Result of a virtual index creation operation
/// </summary>
public class IndexCreationResultDto
{
    /// <summary>
    /// Whether the index was created successfully
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("success")]
    [Newtonsoft.Json.JsonProperty("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Error message if creation failed
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("error_message")]
    [Newtonsoft.Json.JsonProperty("error_message")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Physical PostgreSQL index name (includes client ID and hash)
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("physical_index_name")]
    [Newtonsoft.Json.JsonProperty("physical_index_name")]
    public string? PhysicalIndexName { get; set; }

    /// <summary>
    /// Virtual index ID in vibe.virtual_indexes table
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("virtual_index_id")]
    [Newtonsoft.Json.JsonProperty("virtual_index_id")]
    public int? VirtualIndexId { get; set; }
}
