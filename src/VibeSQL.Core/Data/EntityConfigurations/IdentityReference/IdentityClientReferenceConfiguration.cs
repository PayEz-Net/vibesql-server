using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeSQL.Core.Entities.IdentityReference;

namespace VibeSQL.Core.Data.EntityConfigurations.IdentityReference;

/// <summary>
/// Maps <see cref="IdentityClientReference"/> onto <c>identity.clients</c> -- a table this
/// service does not own. Read-only by convention: nothing in VibeSQL.Core ever calls
/// Add/Update/Remove against this DbSet, only queries against its primary key. See
/// docs/cross-schema-reference-constraints.md, Mode B.
///
/// SCHEMA NAME: "identity" is the name verified live in
/// PayEz-Core/database/migrations/V006__vss_schema_cleanup_and_constraints.sql, which
/// already declares a physical FK from vibe.collection_schemas.client_id to
/// identity.clients(client_id) in this same deployment/database -- i.e. co-location for
/// Mode A is confirmed in the environment that migration was applied to. This mapping does
/// not repeat the "identity.clients vs core_identity.idp_clients" naming ambiguity the doc
/// flags as an open item elsewhere; it uses the name proven live by that migration.
/// </summary>
public class IdentityClientReferenceConfiguration : IEntityTypeConfiguration<IdentityClientReference>
{
    public void Configure(EntityTypeBuilder<IdentityClientReference> builder)
    {
        builder.ToTable("clients", "identity", t => t.ExcludeFromMigrations());

        builder.HasKey(c => c.ClientId);

        builder.Property(c => c.ClientId)
            .HasColumnName("client_id")
            .HasColumnType("integer")
            .ValueGeneratedNever();
    }
}
