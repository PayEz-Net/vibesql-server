namespace VibeSQL.Core.Entities;

/// <summary>
/// Stores an email address in the allow or block list for a client.
/// Used for beta access control and email filtering.
/// </summary>
public class EmailAccessListEntry
{
    /// <summary>
    /// Primary key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The IDP client identifier
    /// </summary>
    public int ClientId { get; set; }

    /// <summary>
    /// List type: "allow" or "block"
    /// </summary>
    public string ListType { get; set; } = "allow";

    /// <summary>
    /// The email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Optional reason for adding to list
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Optional expiration date for temporary entries
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// When the entry was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// User ID who created the entry
    /// </summary>
    public int? CreatedBy { get; set; }
}
