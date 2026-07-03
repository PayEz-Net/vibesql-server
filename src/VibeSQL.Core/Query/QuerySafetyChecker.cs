using System.Text;
using System.Text.RegularExpressions;

namespace VibeSQL.Core.Query;

/// <summary>
/// Enforces safety rules on SQL queries.
/// </summary>
public class QuerySafetyChecker : IQuerySafetyChecker
{
    private static readonly Regex WhereClausePattern = new(
        "\\bWHERE\\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public void CheckSafety(string? sql)
    {
        var trimmed = sql?.Trim() ?? string.Empty;
        var upperSql = trimmed.ToUpperInvariant();

        if (upperSql.StartsWith("UPDATE") && !HasWhereClause(trimmed))
        {
            throw new VibeQueryError(
                VibeErrorCodes.UnsafeQuery,
                "Unsafe query: UPDATE without WHERE clause",
                "UPDATE queries must include a WHERE clause. Use 'WHERE 1=1' to update all rows explicitly");
        }

        if (upperSql.StartsWith("DELETE") && !HasWhereClause(trimmed))
        {
            throw new VibeQueryError(
                VibeErrorCodes.UnsafeQuery,
                "Unsafe query: DELETE without WHERE clause",
                "DELETE queries must include a WHERE clause. Use 'WHERE 1=1' to delete all rows explicitly");
        }
    }

    /// <summary>
    /// Checks if a SQL query contains a WHERE clause, ignoring WHERE that appears inside string
    /// literals or comments.
    ///
    /// Uses a SINGLE-PASS lexical scanner, not two independent regex passes. The old approach stripped
    /// comments first, then strings — so a comment marker (-- or /*) INSIDE a string literal (e.g. an
    /// UPDATE whose value contains '--' or a markdown '---' divider) was mis-read as a SQL comment that
    /// swallowed everything to end-of-line, including the trailing WHERE — producing a false-positive
    /// "UPDATE/DELETE without WHERE" rejection of a legitimate query. The scanner tracks lexical state so
    /// the two cannot be confused: a string swallows any -- or /* inside it; a comment swallows any ' inside it.
    /// </summary>
    private static bool HasWhereClause(string sql)
    {
        return WhereClausePattern.IsMatch(StripStringsAndComments(sql));
    }

    /// <summary>
    /// Single-pass strip of SQL string literals and comments. String literals collapse to '' (a
    /// harmless empty literal — their content cannot affect WHERE detection); comments are removed.
    /// Handles PostgreSQL '' escaped quotes inside string literals.
    /// </summary>
    private static string StripStringsAndComments(string sql)
    {
        var sb = new StringBuilder(sql.Length);
        var i = 0;
        var length = sql.Length;

        while (i < length)
        {
            var c = sql[i];

            switch (c)
            {
                case '\'':
                    i++;
                    while (i < length)
                    {
                        if (sql[i] == '\'')
                        {
                            if (i + 1 >= length || sql[i + 1] != '\'')
                            {
                                i++;
                                break;
                            }
                            i += 2;
                        }
                        else
                        {
                            i++;
                        }
                    }
                    sb.Append("''");
                    continue;

                case '-':
                    if (i + 1 < length && sql[i + 1] == '-')
                    {
                        for (i += 2; i < length && sql[i] != '\n'; i++) { }
                        continue;
                    }
                    break;
            }

            if (c == '/' && i + 1 < length && sql[i + 1] == '*')
            {
                for (i += 2; i + 1 < length && (sql[i] != '*' || sql[i + 1] != '/'); i++) { }
                i = Math.Min(i + 2, length);
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }

        return sb.ToString();
    }
}
