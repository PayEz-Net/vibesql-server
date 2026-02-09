using System.Text.RegularExpressions;

namespace VibeSQL.Core.Query;

/// <summary>
/// Enforces safety rules on SQL queries
/// </summary>
public interface IQuerySafetyChecker
{
    void CheckSafety(string sql);
}

public class QuerySafetyChecker : IQuerySafetyChecker
{
    private static readonly Regex WhereClausePattern = new(@"\bWHERE\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SingleLineCommentPattern = new(@"--[^\n]*", RegexOptions.Compiled);
    private static readonly Regex MultiLineCommentPattern = new(@"/\*[\s\S]*?\*/", RegexOptions.Compiled);
    private static readonly Regex StringLiteralPattern = new(@"'(?:[^']|'')*'", RegexOptions.Compiled);

    public void CheckSafety(string sql)
    {
        var trimmed = sql?.Trim() ?? string.Empty;
        var upperSql = trimmed.ToUpperInvariant();

        if (upperSql.StartsWith("UPDATE"))
        {
            if (!HasWhereClause(trimmed))
            {
                throw new VibeQueryError(
                    VibeErrorCodes.UnsafeQuery,
                    "Unsafe query: UPDATE without WHERE clause",
                    "UPDATE queries must include a WHERE clause. Use 'WHERE 1=1' to update all rows explicitly");
            }
        }

        if (upperSql.StartsWith("DELETE"))
        {
            if (!HasWhereClause(trimmed))
            {
                throw new VibeQueryError(
                    VibeErrorCodes.UnsafeQuery,
                    "Unsafe query: DELETE without WHERE clause",
                    "DELETE queries must include a WHERE clause. Use 'WHERE 1=1' to delete all rows explicitly");
            }
        }
    }

    private static bool HasWhereClause(string sql)
    {
        sql = RemoveComments(sql);
        sql = RemoveStringLiterals(sql);
        return WhereClausePattern.IsMatch(sql);
    }

    private static string RemoveComments(string sql)
    {
        sql = SingleLineCommentPattern.Replace(sql, "");
        sql = MultiLineCommentPattern.Replace(sql, "");
        return sql;
    }

    private static string RemoveStringLiterals(string sql)
    {
        return StringLiteralPattern.Replace(sql, "''");
    }
}
