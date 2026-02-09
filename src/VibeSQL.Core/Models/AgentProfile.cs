using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace VibeSQL.Core.Models;

/// <summary>
/// Agent profile data from Vibe SQL agent_profiles collection
/// </summary>
public class AgentProfile
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonPropertyName("team_id")]
    [JsonProperty("team_id")]
    public int TeamId { get; set; }

    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    [JsonProperty("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("username")]
    [JsonProperty("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("role_preset")]
    [JsonProperty("role_preset")]
    public string? RolePreset { get; set; }

    [JsonPropertyName("is_active")]
    [JsonProperty("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("is_coordinator")]
    [JsonProperty("is_coordinator")]
    public bool IsCoordinator { get; set; }

    [JsonPropertyName("project_id")]
    [JsonProperty("project_id")]
    public int? ProjectId { get; set; }

    [JsonPropertyName("status")]
    [JsonProperty("status")]
    public string? Status { get; set; }

    [JsonPropertyName("role")]
    [JsonProperty("role")]
    public string? Role { get; set; }
}
