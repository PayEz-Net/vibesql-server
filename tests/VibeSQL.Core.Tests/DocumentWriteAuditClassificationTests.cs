using FluentAssertions;
using VibeSQL.Core.Query;

namespace VibeSQL.Core.Tests;

/// <summary>
/// 189589 — the no-confident-garbage guarantee: only statements whose target is
/// cleanly identifiable as vibe.documents may classify as auditable writes.
/// Everything else — reads, other tables, near-miss names — classifies null.
/// </summary>
public class DocumentWriteAuditClassificationTests
{
    [Theory]
    [InlineData("INSERT INTO vibe.documents (client_id, collection, table_name, data) VALUES (1,'c','t','{}')", "document.insert")]
    [InlineData("insert into vibe.documents (client_id) values (1)", "document.insert")]
    [InlineData("  INSERT INTO documents (client_id) VALUES (1)", "document.insert")]
    [InlineData("INSERT INTO vibe.\"documents\" (client_id) VALUES (1) RETURNING document_id", "document.insert")]
    [InlineData("UPDATE vibe.documents SET data = '{}' WHERE document_id = 5", "document.update")]
    [InlineData("update documents set data='{}' where client_id=1", "document.update")]
    [InlineData("DELETE FROM vibe.documents WHERE document_id = 5", "document.delete")]
    [InlineData("delete from documents where client_id = 1", "document.delete")]
    public void DocumentWrites_Classify(string sql, string expectedAction)
    {
        DocumentWriteAudit.ClassifyDocumentWrite(sql).Should().Be(expectedAction);
    }

    [Theory]
    // Reads: 465 of 519 production requests - must NEVER emit audit rows.
    [InlineData("SELECT * FROM vibe.documents WHERE client_id = 1")]
    [InlineData("SELECT count(*) FROM documents")]
    // Other tables: not the document store.
    [InlineData("UPDATE vibe.audit_logs SET description = 'x' WHERE audit_log_id = 1")]
    [InlineData("INSERT INTO vibe.tier_limits (tier_id, tier_name) VALUES (9, 'x')")]
    [InlineData("UPDATE vibe_shop.purchases SET tier_granted = 'pro'")]
    // Near-miss identifiers must not match (\b guard): documents_archive is not documents.
    [InlineData("UPDATE vibe.documents_archive SET data = '{}'")]
    [InlineData("INSERT INTO vibe.documents_backup (client_id) VALUES (1)")]
    // Qualified with another schema.
    [InlineData("UPDATE other_schema.documents SET data = '{}'")]
    // DDL is not a document write.
    [InlineData("CREATE TABLE vibe.x (id int)")]
    [InlineData("DROP TABLE vibe.documents")]
    public void NonDocumentWrites_AndReads_ClassifyNull(string sql)
    {
        DocumentWriteAudit.ClassifyDocumentWrite(sql).Should().BeNull(
            "a statement we cannot identify with certainty as a vibe.documents write " +
            "must produce NO audit row - never a fabricated one");
    }
}
