namespace VibeSQL.Core.Exceptions;

/// <summary>
/// Thrown when a write names a <c>client_id</c> that does not resolve to a real row in
/// <c>identity.clients</c>. This is the single typed error for the cross-schema
/// reference-constraint guarantee (docs/cross-schema-reference-constraints.md, review
/// item 3): a Mode A physical FK violation and a Mode B repository-level rejection must
/// present the same contract to callers, so API/CLI code can catch one exception type
/// regardless of which layer caught the bad write.
/// </summary>
public class UnknownClientReferenceException : Exception
{
    public int ClientId { get; }

    public UnknownClientReferenceException(int clientId)
        : base($"client_id {clientId} does not reference an existing client. " +
               "The write was refused rather than orphaning tenant data.")
    {
        ClientId = clientId;
    }
}
