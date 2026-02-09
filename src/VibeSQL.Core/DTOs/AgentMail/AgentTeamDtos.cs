using System.Text.Json.Serialization;

namespace VibeSQL.Core.DTOs.AgentMail;

// Request DTOs

public class CreateTeamRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("team_type")]
    public string TeamType { get; set; } = "full-stack";

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class UpdateTeamRequest
{
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("team_type")]
    public string? TeamType { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("is_active")]
    public bool? IsActive { get; set; }
}

public class AddTeamMemberRequest
{
    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("role_in_team")]
    public string? RoleInTeam { get; set; }
}

// Response/Data DTOs

public class TeamDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("team_type")]
    public string? TeamType { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("created_by")]
    public int? CreatedBy { get; set; }

    [JsonPropertyName("member_count")]
    public int MemberCount { get; set; }
}

public class TeamMemberDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("team_id")]
    public int TeamId { get; set; }

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("agent_name")]
    public string? AgentName { get; set; }

    [JsonPropertyName("role_in_team")]
    public string? RoleInTeam { get; set; }

    [JsonPropertyName("joined_at")]
    public DateTimeOffset? JoinedAt { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }
}

// JSON Data Models (for parsing Vibe documents)

public class TeamDataModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("team_type")]
    public string? TeamType { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("created_by")]
    public int? CreatedBy { get; set; }
}

public class TeamMemberDataModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("team_id")]
    public int TeamId { get; set; }

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("role_in_team")]
    public string? RoleInTeam { get; set; }

    [JsonPropertyName("joined_at")]
    public string? JoinedAt { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;
}
