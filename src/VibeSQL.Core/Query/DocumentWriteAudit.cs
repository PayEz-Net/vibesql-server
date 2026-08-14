using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;

namespace VibeSQL.Core.Query;

/// <summary>
/// Request-scoped context for audit attribution on the raw-SQL lane.
/// QueryExecutor has no HttpContext; QueryController passes this in.
/// </summary>
public sealed record QueryAuditContext(
    string? RequestPath,
    string? HttpMethod,
    string? IpAddress,
    string? UserAgent);

/// <summary>
/// 189589: audit producer for the raw-SQL lane at the QueryExecutor choke point.
///
/// 100% of production document writes arrive as raw SQL at POST /v1/query; the
/// controller-level producer (186222) sits on a path with zero traffic. This
/// writer covers INSERT/UPDATE/DELETE against vibe.documents at the single point
/// every one of those writes passes through.
///
/// Binding constraints from the pass-2 verdict:
/// - NEVER fabricate: only statements whose target parses cleanly as vibe.documents
///   are audited; collection/table/document_id are NOT derivable from arbitrary
///   SQL and are left NULL rather than guessed (a false audit record is worse
///   than none). The row count and a SQL fingerprint are recorded instead.
/// - Reads never reach this writer: it is invoked only from the DML branches.
/// - FAIL CLOSED: attribution requires the tenant's type='system' user; if the
///   INSERT yields zero rows the write throws and the transaction rolls back -
///   no audit row, no document write. (Parity with the controller producer.)
/// - Hot path: the parse is an anchored regex on the statement head; the audit
///   row is ONE extra statement on the same connection and transaction.
/// </summary>
public static class DocumentWriteAudit
{
    private static readonly Regex InsertPattern = new(
        @"^\s*INSERT\s+INTO\s+(?:vibe\s*\.\s*)?(?:""documents""|documents)(?=[\s(])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UpdatePattern = new(
        @"^\s*UPDATE\s+(?:vibe\s*\.\s*)?(?:""documents""|documents)(?=[\s(])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DeletePattern = new(
        @"^\s*DELETE\s+FROM\s+(?:vibe\s*\.\s*)?(?:""documents""|documents)(?=[\s(])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Returns the audit action name ("document.insert" / "document.update" /
    /// "document.delete") when <paramref name="sql"/> is a write whose target is
    /// cleanly identifiable as vibe.documents; otherwise null. A null return is
    /// the no-garbage guarantee: anything we cannot parse with certainty is not
    /// audited, never audited with invented fields.
    /// </summary>
    public static string? ClassifyDocumentWrite(string sql)
    {
        if (InsertPattern.IsMatch(sql)) return "document.insert";
        if (UpdatePattern.IsMatch(sql)) return "document.update";
        if (DeletePattern.IsMatch(sql)) return "document.delete";
        return null;
    }

    /// <summary>
    /// Writes the audit row inside the SAME transaction as the document write, so
    /// the two commit or roll back together. Throws when attribution is impossible
    /// (tenant has no type='system' user) - zero rows means the SELECT found no
    /// system user and the INSERT silently wrote nothing: an audit gap reporting
    /// success. Throwing fails the whole statement instead.
    /// </summary>
    public static async Task WriteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int clientId,
        string action,
        int rowsAffected,
        string sql,
        QueryAuditContext? context,
        CancellationToken cancellationToken)
    {
        const string auditSql = @"
            INSERT INTO vibe.audit_logs
                (client_id, admin_user_id, admin_email, category, action,
                 target_type, target_id, description, is_success,
                 ip_address, user_agent, request_path, http_method, created_at)
            SELECT @client_id,
                   sys.uid,
                   sys.email,
                   'data', @action,
                   'document', NULL, @description, true,
                   @ip_address, @user_agent, @request_path, @http_method, now()
            FROM (SELECT (data->>'user_id')::int AS uid, data->>'email' AS email
                    FROM vibe.documents
                   WHERE client_id = @client_id
                     AND collection = 'vibe_app' AND table_name = 'users'
                     AND deleted_at IS NULL
                     AND data->>'type' = 'system'
                   LIMIT 1) sys";

        var description =
            $"{action} on vibe.documents via raw-SQL lane: {rowsAffected} row(s) affected; " +
            $"sql_sha256:{Fingerprint(sql)}; " +
            "collection/table/document_id intentionally NULL - not derivable from arbitrary SQL (189589: no confident garbage)";

        await using var cmd = new NpgsqlCommand(auditSql, connection, transaction);
        cmd.Parameters.Add(new NpgsqlParameter("client_id", clientId));
        cmd.Parameters.Add(new NpgsqlParameter("action", action));
        cmd.Parameters.Add(new NpgsqlParameter("description", description));
        cmd.Parameters.Add(new NpgsqlParameter("ip_address",
            context?.IpAddress is { Length: > 0 } ip ? ip : "unknown"));
        cmd.Parameters.Add(new NpgsqlParameter("user_agent",
            context?.UserAgent is { Length: > 0 } ua ? ua : "unknown"));
        cmd.Parameters.Add(new NpgsqlParameter("request_path",
            context?.RequestPath is { Length: > 0 } path ? path : "/v1/query"));
        cmd.Parameters.Add(new NpgsqlParameter("http_method",
            context?.HttpMethod is { Length: > 0 } method ? method : "POST"));

        var written = await cmd.ExecuteNonQueryAsync(cancellationToken);
        if (written == 0)
        {
            throw new InvalidOperationException(
                $"Audit write produced no row for client {clientId}: the tenant has no " +
                "type='system' user to attribute a document write to. Seed a " +
                "vibe_app/users document with data->>'type' = 'system' for this client " +
                "before accepting writes. Failing closed: the document write rolls back " +
                "with this exception (189589 fail-closed parity).");
        }
    }

    private static string Fingerprint(string sql)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sql));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
