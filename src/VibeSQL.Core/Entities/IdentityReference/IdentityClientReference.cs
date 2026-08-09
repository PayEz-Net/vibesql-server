namespace VibeSQL.Core.Entities.IdentityReference;

/// <summary>
/// Read-only shadow mapping of the IDP <c>identity.clients</c> table -- the "reference
/// entity" half of the Mode B virtual-FK pattern documented in
/// <c>docs/cross-schema-reference-constraints.md</c>.
///
/// We do not own this table and never write to it from VibeSQL. It exists solely so a
/// relationship to its primary key (<see cref="ClientId"/>) can be expressed and validated
/// in code, the same way <c>PayEz-Core/.../EntityConfigurations/IdentityReference/AspNetUserConfiguration.cs</c>
/// shadow-maps <c>core_identity.asp_net_users</c> for the analogous <c>user_id</c> case.
/// </summary>
public class IdentityClientReference
{
    /// <summary>
    /// The IDP client identifier (tenant). Primary key of <c>identity.clients</c>.
    /// </summary>
    public int ClientId { get; set; }
}
