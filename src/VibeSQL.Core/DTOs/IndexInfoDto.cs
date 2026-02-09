using System;
using System.Collections.Generic;

namespace VibeSQL.Core.DTOs;

/// <summary>
/// Information about an existing virtual index
/// </summary>
public class IndexInfoDto
{
    /// <summary>
    /// Virtual index ID in vibe.virtual_indexes table
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("virtual_index_id")]
    [Newtonsoft.Json.JsonProperty("virtual_index_id")]
    public int VirtualIndexId { get; set; }

    /// <summary>
    /// Logical index name (user-facing)
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("index_name")]
    [Newtonsoft.Json.JsonProperty("index_name")]
    public string IndexName { get; set; } = string.Empty;

    /// <summary>
    /// Table name within collection
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("table_name")]
    [Newtonsoft.Json.JsonProperty("table_name")]
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// Physical PostgreSQL index name
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("physical_index_name")]
    [Newtonsoft.Json.JsonProperty("physical_index_name")]
    public string PhysicalIndexName { get; set; } = string.Empty;

    /// <summary>
    /// Partition where index exists
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("partition_name")]
    [Newtonsoft.Json.JsonProperty("partition_name")]
    public string PartitionName { get; set; } = string.Empty;

    /// <summary>
    /// List of indexed fields
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("fields")]
    [Newtonsoft.Json.JsonProperty("fields")]
    public List<string> Fields { get; set; } = new();

    /// <summary>
    /// When the index was created
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("created_at")]
    [Newtonsoft.Json.JsonProperty("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
}
