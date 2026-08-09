namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Mode B (always-on) half of the cross-schema reference-constraint pattern
/// (docs/cross-schema-reference-constraints.md): validates a <c>client_id</c> against the
/// IDP clients table at write time, in code, so a write naming a client that does not exist
/// is refused instead of silently orphaning tenant data. Runs in every deployment regardless
/// of whether Mode A (a physical FK, only possible where the clients table is co-located)
/// is also present -- see "How the modes combine" in the doc.
/// </summary>
public interface IClientReferenceValidator
{
    /// <summary>
    /// True if <paramref name="clientId"/> resolves to a real row in identity.clients.
    /// </summary>
    Task<bool> ClientExistsAsync(int clientId);
}
