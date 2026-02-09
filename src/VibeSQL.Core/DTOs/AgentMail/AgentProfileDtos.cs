using System.Text.Json;
using System.Text.Json.Serialization;

namespace VibeSQL.Core.DTOs.AgentMail;

// Request DTOs

public class CreateProfileRequest
{
    [JsonPropertyName("identity_md")]
    public string? IdentityMd { get; set; }

    [JsonPropertyName("role_md")]
    public string? RoleMd { get; set; }

    [JsonPropertyName("expertise_json")]
    public JsonElement? ExpertiseJson { get; set; }

    [JsonPropertyName("philosophy_md")]
    public string? PhilosophyMd { get; set; }

    [JsonPropertyName("response_pattern_md")]
    public string? ResponsePatternMd { get; set; }

    [JsonPropertyName("communication_md")]
    public string? CommunicationMd { get; set; }
}

public class UpdateAgentProfileRequest : CreateProfileRequest
{
}

public class CreateSkillRequest
{
    [JsonPropertyName("skill_name")]
    public string SkillName { get; set; } = "";

    [JsonPropertyName("skill_content_md")]
    public string SkillContentMd { get; set; } = "";

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 100;
}

public class CreateRepoRequest
{
    [JsonPropertyName("repo_path")]
    public string RepoPath { get; set; } = "";

    [JsonPropertyName("repo_name")]
    public string? RepoName { get; set; }

    [JsonPropertyName("access_level")]
    public string AccessLevel { get; set; } = "write";

    [JsonPropertyName("is_primary")]
    public bool IsPrimary { get; set; } = false;
}

// Response/Data DTOs

public class ProfileDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("identity_md")]
    public string? IdentityMd { get; set; }

    [JsonPropertyName("role_md")]
    public string? RoleMd { get; set; }

    [JsonPropertyName("expertise_json")]
    public JsonElement? ExpertiseJson { get; set; }

    [JsonPropertyName("philosophy_md")]
    public string? PhilosophyMd { get; set; }

    [JsonPropertyName("response_pattern_md")]
    public string? ResponsePatternMd { get; set; }

    [JsonPropertyName("communication_md")]
    public string? CommunicationMd { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class SkillDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("skill_name")]
    public string SkillName { get; set; } = "";

    [JsonPropertyName("skill_content_md")]
    public string? SkillContentMd { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }
}

public class RepoDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("repo_path")]
    public string RepoPath { get; set; } = "";

    [JsonPropertyName("repo_name")]
    public string? RepoName { get; set; }

    [JsonPropertyName("access_level")]
    public string AccessLevel { get; set; } = "write";

    [JsonPropertyName("is_primary")]
    public bool IsPrimary { get; set; }
}

// Template DTOs for personal agent templates at client_id=0

/// <summary>
/// Request to create a personal agent template.
/// Templates are stored at client_id=0 with owner_user_id set.
/// </summary>
public class CreateAgentTemplateRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; } = "agent";

    [JsonPropertyName("identity_md")]
    public string? IdentityMd { get; set; }

    [JsonPropertyName("role_md")]
    public string? RoleMd { get; set; }

    [JsonPropertyName("expertise_json")]
    public JsonElement? ExpertiseJson { get; set; }

    [JsonPropertyName("philosophy_md")]
    public string? PhilosophyMd { get; set; }

    [JsonPropertyName("response_pattern_md")]
    public string? ResponsePatternMd { get; set; }

    [JsonPropertyName("communication_md")]
    public string? CommunicationMd { get; set; }
}

/// <summary>
/// Agent template response DTO.
/// </summary>
public class AgentTemplateDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("owner_user_id")]
    public int? OwnerUserId { get; set; }

    [JsonPropertyName("is_system")]
    public bool IsSystem { get; set; }

    [JsonPropertyName("identity_md")]
    public string? IdentityMd { get; set; }

    [JsonPropertyName("role_md")]
    public string? RoleMd { get; set; }

    [JsonPropertyName("expertise_json")]
    public JsonElement? ExpertiseJson { get; set; }

    [JsonPropertyName("philosophy_md")]
    public string? PhilosophyMd { get; set; }

    [JsonPropertyName("response_pattern_md")]
    public string? ResponsePatternMd { get; set; }

    [JsonPropertyName("communication_md")]
    public string? CommunicationMd { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>
/// Data model for agent template documents in vibe_documents.
/// </summary>
public class AgentTemplateDataModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("owner_user_id")]
    public int? OwnerUserId { get; set; }

    [JsonPropertyName("is_template")]
    public bool IsTemplate { get; set; } = true;

    [JsonPropertyName("identity_md")]
    public string? IdentityMd { get; set; }

    [JsonPropertyName("role_md")]
    public string? RoleMd { get; set; }

    [JsonPropertyName("expertise_json")]
    public JsonElement? ExpertiseJson { get; set; }

    [JsonPropertyName("philosophy_md")]
    public string? PhilosophyMd { get; set; }

    [JsonPropertyName("response_pattern_md")]
    public string? ResponsePatternMd { get; set; }

    [JsonPropertyName("communication_md")]
    public string? CommunicationMd { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }
}

// JSON Data Models (for parsing Vibe documents)

public class ProfileDataModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("identity_md")]
    public string? IdentityMd { get; set; }

    [JsonPropertyName("role_md")]
    public string? RoleMd { get; set; }

    [JsonPropertyName("expertise_json")]
    public JsonElement? ExpertiseJson { get; set; }

    [JsonPropertyName("philosophy_md")]
    public string? PhilosophyMd { get; set; }

    [JsonPropertyName("response_pattern_md")]
    public string? ResponsePatternMd { get; set; }

    [JsonPropertyName("communication_md")]
    public string? CommunicationMd { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }
}

public class SkillDataModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("skill_name")]
    public string? SkillName { get; set; }

    [JsonPropertyName("skill_content_md")]
    public string? SkillContentMd { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 100;

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }
}

public class RepoDataModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("repo_path")]
    public string? RepoPath { get; set; }

    [JsonPropertyName("repo_name")]
    public string? RepoName { get; set; }

    [JsonPropertyName("access_level")]
    public string AccessLevel { get; set; } = "write";

    [JsonPropertyName("is_primary")]
    public bool IsPrimary { get; set; } = false;
}
