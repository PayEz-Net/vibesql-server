using VibeSQL.Edge.Data.Entities;

namespace VibeSQL.Edge.Authorization;

public record SqlClassification(
    VibePermissionLevel RequiredLevel,
    string StatementType,
    string? ErrorMessage = null)
{
    public bool IsError => ErrorMessage is not null;

    public static SqlClassification Error(string message) =>
        new(VibePermissionLevel.Admin, "ERROR", message);
}

public static class SqlStatementClassifier
{
    private static readonly HashSet<string> ReadKeywords = new(StringComparer.OrdinalIgnoreCase)
        { "SELECT", "SHOW" };

    private static readonly HashSet<string> WriteKeywords = new(StringComparer.OrdinalIgnoreCase)
        { "INSERT", "UPDATE", "DELETE", "UPSERT", "MERGE", "COPY" };

    private static readonly HashSet<string> SchemaKeywords = new(StringComparer.OrdinalIgnoreCase)
        { "CREATE", "ALTER", "DROP" };

    private static readonly HashSet<string> AdminKeywords = new(StringComparer.OrdinalIgnoreCase)
        { "TRUNCATE", "GRANT", "REVOKE", "VACUUM", "REINDEX", "CLUSTER" };

    private static readonly HashSet<string> SchemaAdminTargets = new(StringComparer.OrdinalIgnoreCase)
        { "SCHEMA" };

    public static SqlClassification Classify(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return SqlClassification.Error("Empty SQL statement");

        var stripped = StripLeadingComments(sql).TrimStart();
        if (string.IsNullOrWhiteSpace(stripped))
            return SqlClassification.Error("SQL statement contains only comments");

        if (ContainsUnquotedSemicolon(stripped))
            return SqlClassification.Error("Multi-statement batches are not allowed");

        var firstKeyword = GetFirstKeyword(stripped);
        if (firstKeyword is null)
            return SqlClassification.Error("Could not determine SQL statement type");

        var upper = firstKeyword.ToUpperInvariant();

        if (upper == "WITH")
            return ClassifyCte(stripped);

        if (upper == "EXPLAIN")
            return ClassifyExplain(stripped);

        if (ReadKeywords.Contains(upper))
            return new SqlClassification(VibePermissionLevel.Read, upper);

        if (WriteKeywords.Contains(upper))
            return new SqlClassification(VibePermissionLevel.Write, upper);

        if (SchemaKeywords.Contains(upper))
            return ClassifySchemaStatement(stripped, upper);

        if (AdminKeywords.Contains(upper))
            return new SqlClassification(VibePermissionLevel.Admin, upper);

        return SqlClassification.Error($"Unrecognized SQL statement type: {upper}");
    }

    public static VibePermissionLevel ClassifyHttpRequest(string method, string path)
    {
        if (path.StartsWith("/v1/schemas", StringComparison.OrdinalIgnoreCase))
            return VibePermissionLevel.Schema;

        if (method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            return VibePermissionLevel.Read;

        if (path.Contains("/query", StringComparison.OrdinalIgnoreCase)
            && method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            return VibePermissionLevel.Read;

        return VibePermissionLevel.Write;
    }

    internal static string StripLeadingComments(string sql)
    {
        var s = sql.AsSpan();

        while (s.Length > 0)
        {
            s = s.TrimStart();
            if (s.Length == 0) break;

            if (s.Length >= 2 && s[0] == '-' && s[1] == '-')
            {
                var newline = s.IndexOfAny('\n', '\r');
                s = newline < 0 ? ReadOnlySpan<char>.Empty : s[(newline + 1)..];
                continue;
            }

            if (s.Length >= 2 && s[0] == '/' && s[1] == '*')
            {
                var end = s[2..].IndexOf("*/");
                s = end < 0 ? ReadOnlySpan<char>.Empty : s[(end + 4)..];
                continue;
            }

            break;
        }

        return s.ToString();
    }

    internal static bool ContainsUnquotedSemicolon(string sql)
    {
        var inSingle = false;
        var inDouble = false;

        for (int i = 0; i < sql.Length; i++)
        {
            var c = sql[i];

            if (c == '\'' && !inDouble)
            {
                if (i + 1 < sql.Length && sql[i + 1] == '\'')
                    { i++; continue; }
                inSingle = !inSingle;
                continue;
            }

            if (c == '"' && !inSingle)
            {
                inDouble = !inDouble;
                continue;
            }

            if (c == ';' && !inSingle && !inDouble)
            {
                var rest = sql[(i + 1)..].TrimEnd();
                if (rest.Length > 0)
                    return true;
            }
        }

        return false;
    }

    internal static string? GetFirstKeyword(string sql)
    {
        var span = sql.AsSpan().TrimStart();
        if (span.Length == 0) return null;

        var end = 0;
        while (end < span.Length && !char.IsWhiteSpace(span[end]) && span[end] != '(')
            end++;

        return end > 0 ? span[..end].ToString() : null;
    }

    private static SqlClassification ClassifyCte(string stripped)
    {
        var depth = 0;
        var upper = stripped.ToUpperInvariant();
        var i = 4; // skip "WITH"
        var cteBlockEnded = false;
        var peakDepth = 0;

        while (i < upper.Length)
        {
            var c = upper[i];

            if (c == '(') { depth++; if (depth > peakDepth) peakDepth = depth; i++; continue; }
            if (c == ')')
            {
                depth--;
                if (depth == 0 && peakDepth > 0)
                    cteBlockEnded = true;
                i++;
                continue;
            }

            if (depth == 0 && cteBlockEnded && char.IsLetter(c))
            {
                var wordEnd = i;
                while (wordEnd < upper.Length && char.IsLetter(upper[wordEnd]))
                    wordEnd++;

                var word = upper[i..wordEnd];

                if (word == "AS" || word == "WITH")
                {
                    cteBlockEnded = false;
                    peakDepth = 0;
                    i = wordEnd;
                    continue;
                }

                if (WriteKeywords.Contains(word))
                    return new SqlClassification(VibePermissionLevel.Write, $"WITH...{word}");

                if (ReadKeywords.Contains(word))
                    return new SqlClassification(VibePermissionLevel.Read, $"WITH...{word}");

                i = wordEnd;
                continue;
            }

            i++;
        }

        return SqlClassification.Error("Could not determine terminal DML statement in CTE");
    }

    private static SqlClassification ClassifyExplain(string stripped)
    {
        var afterExplain = stripped.AsSpan()[7..].TrimStart();
        if (afterExplain.Length == 0)
            return new SqlClassification(VibePermissionLevel.Read, "EXPLAIN");

        var upper = afterExplain.ToString().ToUpperInvariant();

        if (upper.StartsWith("ANALYZE"))
            upper = upper[7..].TrimStart();

        if (upper.StartsWith("VERBOSE"))
            upper = upper[7..].TrimStart();

        if (upper.StartsWith("("))
        {
            var close = upper.IndexOf(')');
            if (close >= 0)
                upper = upper[(close + 1)..].TrimStart();
        }

        var keyword = GetFirstKeyword(upper);
        if (keyword is null)
            return new SqlClassification(VibePermissionLevel.Read, "EXPLAIN");

        if (WriteKeywords.Contains(keyword))
            return new SqlClassification(VibePermissionLevel.Write, $"EXPLAIN {keyword}");

        if (SchemaKeywords.Contains(keyword))
            return new SqlClassification(VibePermissionLevel.Schema, $"EXPLAIN {keyword}");

        return new SqlClassification(VibePermissionLevel.Read, $"EXPLAIN {keyword}");
    }

    private static SqlClassification ClassifySchemaStatement(string stripped, string keyword)
    {
        var afterKeyword = stripped.AsSpan()[keyword.Length..].TrimStart();
        var target = GetFirstKeyword(afterKeyword.ToString());

        if (target is not null && SchemaAdminTargets.Contains(target))
            return new SqlClassification(VibePermissionLevel.Admin, $"{keyword} SCHEMA");

        return new SqlClassification(VibePermissionLevel.Schema, keyword);
    }
}
