using System;

namespace VibeSQL.Core.DTOs;

public class VibeDocumentDto
{
    [System.Text.Json.Serialization.JsonPropertyName("document_id")]
    [Newtonsoft.Json.JsonProperty("document_id")]
    public int DocumentId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("client_id")]
    [Newtonsoft.Json.JsonProperty("client_id")]
    public int ClientId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("user_id")]
    [Newtonsoft.Json.JsonProperty("user_id")]
    public int? OwnerUserId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("collection")]
    [Newtonsoft.Json.JsonProperty("collection")]
    public string Collection { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("table_name")]
    [Newtonsoft.Json.JsonProperty("table_name")]
    public string TableName { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("data")]
    [Newtonsoft.Json.JsonProperty("data")]
    public string? Data { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("collection_schema_id")]
    [Newtonsoft.Json.JsonProperty("collection_schema_id")]
    public int? CollectionSchemaId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("created_at")]
    [Newtonsoft.Json.JsonProperty("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("created_by")]
    [Newtonsoft.Json.JsonProperty("created_by")]
    public int? CreatedBy { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("updated_at")]
    [Newtonsoft.Json.JsonProperty("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("updated_by")]
    [Newtonsoft.Json.JsonProperty("updated_by")]
    public int? UpdatedBy { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("deleted_at")]
    [Newtonsoft.Json.JsonProperty("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }
}
