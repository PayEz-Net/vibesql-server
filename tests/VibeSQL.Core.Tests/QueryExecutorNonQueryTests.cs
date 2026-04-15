// Tier 1 contract regression tests for the Entry 2 non-query execution path.
//
// Scope choice: these tests exercise the DETECTOR (IsNonQueryDml) directly via
// InternalsVisibleTo, not the full ExecuteAsync execution path. NpgsqlCommand
// and NpgsqlConnection are sealed classes that cannot be mocked without
// Testcontainers-Postgres or a real PostgreSQL instance, and the v0.3 WO
// explicitly excludes real Postgres for Tier 1 contract tests (QAPert C3 /
// msg 56). The result-shape guarantees of the non-query branch (empty Rows
// list + RowCount populated from affected rows) are trivially correct by
// construction — 4 lines of deterministic allocation + assignment in
// QueryExecutor.ExecuteAsync — so wrestling with a sealed-class mock for
// shape verification is not worth the 1-day budget.
//
// If a future reviewer wants full-path integration coverage, that is a Tier 2
// follow-up ticket with Testcontainers-Postgres, not a v0.3 blocker.
//
// What these tests DO cover:
//   - Detection accuracy for plain UPDATE/DELETE/INSERT without RETURNING
//   - Regression guards for SELECT and INSERT ... RETURNING (must route through reader)
//   - Comment-stripping guard (-- RETURNING in a comment does not trip detection)
//   - String-literal-stripping guard (RETURNING in a literal routes non-query)
//   - Case-insensitivity guard (lowercase 'returning' still routes through reader)
//   - CTE KNOWN LIMITATION pinned: WITH-prefixed DML does NOT detect as non-query

using Xunit;
using FluentAssertions;
using VibeSQL.Core.Query;

namespace VibeSQL.Core.Tests;

public class QueryExecutorNonQueryTests
{
    [Theory]
    [InlineData("UPDATE t SET x=1 WHERE id=1")]
    [InlineData("DELETE FROM t WHERE id=1")]
    [InlineData("INSERT INTO t(x) VALUES (1)")]
    public void IsNonQueryDml_PlainDmlWithoutReturning_ReturnsTrue(string sql)
    {
        QueryExecutor.IsNonQueryDml(sql).Should().BeTrue(
            "plain DML without RETURNING must route through ExecuteNonQueryAsync so the caller gets the affected row count");
    }

    [Fact]
    public void IsNonQueryDml_Select_ReturnsFalse()
    {
        QueryExecutor.IsNonQueryDml("SELECT * FROM t").Should().BeFalse(
            "SELECT must route through ExecuteReaderAsync — regression guard for the reader path");
    }

    [Fact]
    public void IsNonQueryDml_InsertReturning_ReturnsFalse()
    {
        QueryExecutor.IsNonQueryDml("INSERT INTO t(x) VALUES (1) RETURNING id").Should().BeFalse(
            "INSERT ... RETURNING produces a result set and must route through ExecuteReaderAsync — regression guard");
    }

    [Fact]
    public void IsNonQueryDml_LineCommentContainingReturning_ReturnsTrue()
    {
        // Guard against QAPert C3 comment-edge-case #1: a SQL comment that mentions
        // RETURNING must not false-negative the non-query detection. The private
        // PayEz.VibeSql.Server.Api source false-negatives this; the OSS port fixes it
        // via StripCommentsAndStringLiterals. Reverse-sync candidate.
        var sql = "-- RETURNING\nUPDATE foo SET x=1 WHERE id=1";
        QueryExecutor.IsNonQueryDml(sql).Should().BeTrue(
            "RETURNING inside a line comment must be stripped before detection — the underlying statement is a plain UPDATE that needs the non-query path");
    }

    [Fact]
    public void IsNonQueryDml_StringLiteralContainingReturning_ReturnsTrue()
    {
        // Guard against QAPert C3 comment-edge-case #2: RETURNING inside a string
        // literal must not false-negative the non-query detection. The private
        // source false-negatives this; the OSS port fixes it via StripCommentsAndStringLiterals.
        // Reverse-sync candidate.
        var sql = "UPDATE t SET note = 'tested by RETURNING sentinel' WHERE id = 1";
        QueryExecutor.IsNonQueryDml(sql).Should().BeTrue(
            "RETURNING inside a single-quoted string literal must be stripped before detection — the underlying statement is a plain UPDATE that needs the non-query path");
    }

    [Fact]
    public void IsNonQueryDml_CteWrappedUpdate_ReturnsFalse_KnownLimitation()
    {
        // KNOWN LIMITATION (pinned test — do NOT "fix" this without a scope discussion).
        //
        // CTE-wrapped DML starts with WITH, not with UPDATE/DELETE/INSERT, so the
        // StartsWith check in IsNonQueryDml false-negatives it. The query routes
        // through ExecuteReaderAsync and may silently misbehave (zero rows returned
        // where the caller expected an affected-row-count). This matches the private
        // PayEz.VibeSql.Server.Api implementation exactly — the v0.3 upstreaming
        // program is a mechanical port, not a detection-engine upgrade. Fixing
        // this properly requires parsing past leading CTEs to find the actual
        // root statement kind, which is real SQL parsing work and out of scope.
        //
        // BAPert-Jon's explicit call (v0.3 WO Hours 1-3 section): accept as KNOWN
        // LIMITATION, pin with a test, document in CHANGELOG. A future reverse-sync
        // program or dedicated parser entry can revisit.
        var sql = "WITH x AS (SELECT 1) UPDATE foo SET x=1 WHERE id=1";
        QueryExecutor.IsNonQueryDml(sql).Should().BeFalse(
            "CTE-wrapped DML is not detected as non-query — KNOWN LIMITATION pinned by BAPert-Jon's mechanical-port-not-scope-creep call; do not 'fix' without a scope discussion");
    }

    [Fact]
    public void IsNonQueryDml_LowercaseInsertReturning_ReturnsFalse()
    {
        // Case-insensitivity regression guard. Private uses upperTrimmed.Contains("RETURNING")
        // after ToUpperInvariant — the OSS port must match. A naive sql.Contains("RETURNING")
        // without case normalization would false-negative on lowercase 'returning' and
        // incorrectly route through the non-query path (yielding zero rows where the
        // caller expected the inserted id).
        var sql = "insert into t(x) values (1) returning id";
        QueryExecutor.IsNonQueryDml(sql).Should().BeFalse(
            "lowercase 'returning' must still route through ExecuteReaderAsync after ToUpperInvariant normalization");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n ")]
    public void IsNonQueryDml_NullOrWhitespace_ReturnsFalse(string? sql)
    {
        QueryExecutor.IsNonQueryDml(sql!).Should().BeFalse(
            "null or whitespace-only input is not a DML statement and must not route through the non-query path");
    }
}
