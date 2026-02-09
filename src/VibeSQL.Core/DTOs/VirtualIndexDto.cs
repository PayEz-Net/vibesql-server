using System;

namespace VibeSQL.Core.DTOs;

/// <summary>
/// DTO for VirtualIndex entity.
/// Tracks user-declared virtual indexes on JSONB fields.
/// </summary>
public class VirtualIndexDto
{
    /// <summary>
    /// Primary key
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("virtual_index_id")]
    [Newtonsoft.Json.JsonProperty("virtual_index_id")]
    public int VirtualIndexId { get; set; }

    /// <summary>
    /// IDP client identifier (tenant)
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("client_id")]
    [Newtonsoft.Json.JsonProperty("client_id")]
    public int ClientId { get; set; }

    /// <summary>
    /// Collection name
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("collection")]
    [Newtonsoft.Json.JsonProperty("collection")]
    public string Collection { get; set; } = string.Empty;

    /// <summary>
    /// Table name within collection
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("table_name")]
    [Newtonsoft.Json.JsonProperty("table_name")]
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// Logical index name (user-friendly)
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("index_name")]
    [Newtonsoft.Json.JsonProperty("index_name")]
    public string IndexName { get; set; } = string.Empty;

    /// <summary>
    /// Physical PostgreSQL index name (includes client ID and hash)
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("physical_index_name")]
    [Newtonsoft.Json.JsonProperty("physical_index_name")]
    public string PhysicalIndexName { get; set; } = string.Empty;

    /// <summary>
    /// Index definition as JSON string
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("index_definition")]
    [Newtonsoft.Json.JsonProperty("index_definition")]
    public string IndexDefinition { get; set; } = string.Empty;

    /// <summary>
    /// Partition where physical index exists
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("partition_name")]
    [Newtonsoft.Json.JsonProperty("partition_name")]
    public string PartitionName { get; set; } = string.Empty;

    /// <summary>
    /// When the index was created
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("created_at")]
    [Newtonsoft.Json.JsonProperty("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// User ID who created the index
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("created_by")]
    [Newtonsoft.Json.JsonProperty("created_by")]
    public int? CreatedBy { get; set; }

    /// <summary>
    /// When the index was dropped (soft delete)
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("dropped_at")]
    [Newtonsoft.Json.JsonProperty("dropped_at")]
    public DateTimeOffset? DroppedAt { get; set; }
}
