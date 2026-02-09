using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace VibeSQL.Core.Models;

/// <summary>
/// Agent team data from Vibe SQL agent_teams collection
/// </summary>
public class AgentTeam
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonPropertyName("owner_user_id")]
    [JsonProperty("owner_user_id")]
    public int OwnerUserId { get; set; }

    [JsonPropertyName("tenant_id")]
    [JsonProperty("tenant_id")]
    public string? TenantId { get; set; }

    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonPropertyName("team_type")]
    [JsonProperty("team_type")]
    public string? TeamType { get; set; }

    [JsonPropertyName("is_active")]
    [JsonProperty("is_active")]
    public bool IsActive { get; set; }
}
