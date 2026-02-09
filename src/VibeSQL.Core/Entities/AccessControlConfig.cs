namespace VibeSQL.Core.Entities;

/// <summary>
/// Stores the email access control mode for a client.
/// Mode can be: off (no filtering), allow_list (whitelist), block_list (blacklist)
/// </summary>
public class AccessControlConfig
{
    /// <summary>
    /// The IDP client identifier (primary key)
    /// </summary>
    public int ClientId { get; set; }

    /// <summary>
    /// Access control mode: "off", "allow_list", or "block_list"
    /// </summary>
    public string Mode { get; set; } = "off";

    /// <summary>
    /// When the configuration was last updated
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// User ID who last updated the configuration
    /// </summary>
    public int? UpdatedBy { get; set; }
}
