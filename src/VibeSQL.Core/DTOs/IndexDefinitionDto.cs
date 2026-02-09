using System.Collections.Generic;

namespace VibeSQL.Core.DTOs;

/// <summary>
/// Virtual index definition for JSONB field indexing.
/// Used when creating indexes via schema metadata or direct API calls.
/// </summary>
public class IndexDefinitionDto
{
    /// <summary>
    /// Logical index name (user-friendly). If not provided, auto-generated from fields.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("index_name")]
    [Newtonsoft.Json.JsonProperty("index_name")]
    public string? IndexName { get; set; }

    /// <summary>
    /// Table name within the collection
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("table_name")]
    [Newtonsoft.Json.JsonProperty("table_name")]
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// List of JSONB fields to index
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("fields")]
    [Newtonsoft.Json.JsonProperty("fields")]
    public List<string> Fields { get; set; } = new();

    /// <summary>
    /// Partial index condition (e.g., "data->>'status' = 'active'")
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("partial_condition")]
    [Newtonsoft.Json.JsonProperty("partial_condition")]
    public string? PartialCondition { get; set; }

    /// <summary>
    /// Whether this is a unique index
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("is_unique")]
    [Newtonsoft.Json.JsonProperty("is_unique")]
    public bool IsUnique { get; set; }

    /// <summary>
    /// PostgreSQL index type (btree, gin, gist, etc.)
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("index_type")]
    [Newtonsoft.Json.JsonProperty("index_type")]
    public string IndexType { get; set; } = "btree";
}
