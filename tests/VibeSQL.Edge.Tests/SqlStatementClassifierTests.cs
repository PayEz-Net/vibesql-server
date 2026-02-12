using FluentAssertions;
using VibeSQL.Edge.Authorization;
using VibeSQL.Edge.Data.Entities;

namespace VibeSQL.Edge.Tests;

public class SqlStatementClassifierTests
{
    [Theory]
    [InlineData("SELECT * FROM users", VibePermissionLevel.Read, "SELECT")]
    [InlineData("select id from orders", VibePermissionLevel.Read, "SELECT")]
    [InlineData("SHOW tables", VibePermissionLevel.Read, "SHOW")]
    public void Classify_ReadStatements(string sql, VibePermissionLevel expected, string type)
    {
        var result = SqlStatementClassifier.Classify(sql);
        result.RequiredLevel.Should().Be(expected);
        result.StatementType.Should().Be(type);
        result.IsError.Should().BeFalse();
    }

    [Theory]
    [InlineData("INSERT INTO users VALUES (1)", VibePermissionLevel.Write, "INSERT")]
    [InlineData("UPDATE users SET name='x' WHERE id=1", VibePermissionLevel.Write, "UPDATE")]
    [InlineData("DELETE FROM users WHERE id=1", VibePermissionLevel.Write, "DELETE")]
    [InlineData("UPSERT INTO users VALUES (1)", VibePermissionLevel.Write, "UPSERT")]
    [InlineData("MERGE INTO users USING ...", VibePermissionLevel.Write, "MERGE")]
    [InlineData("COPY users FROM '/tmp/data.csv'", VibePermissionLevel.Write, "COPY")]
    public void Classify_WriteStatements(string sql, VibePermissionLevel expected, string type)
    {
        var result = SqlStatementClassifier.Classify(sql);
        result.RequiredLevel.Should().Be(expected);
        result.StatementType.Should().Be(type);
    }

    [Theory]
    [InlineData("CREATE TABLE users (id int)", VibePermissionLevel.Schema, "CREATE")]
    [InlineData("ALTER TABLE users ADD COLUMN name text", VibePermissionLevel.Schema, "ALTER")]
    [InlineData("DROP TABLE users", VibePermissionLevel.Schema, "DROP")]
    [InlineData("DROP INDEX idx_users_name", VibePermissionLevel.Schema, "DROP")]
    public void Classify_SchemaStatements(string sql, VibePermissionLevel expected, string type)
    {
        var result = SqlStatementClassifier.Classify(sql);
        result.RequiredLevel.Should().Be(expected);
        result.StatementType.Should().Be(type);
    }

    [Theory]
    [InlineData("DROP SCHEMA public CASCADE", VibePermissionLevel.Admin, "DROP SCHEMA")]
    [InlineData("CREATE SCHEMA new_schema", VibePermissionLevel.Admin, "CREATE SCHEMA")]
    public void Classify_SchemaAdminStatements(string sql, VibePermissionLevel expected, string type)
    {
        var result = SqlStatementClassifier.Classify(sql);
        result.RequiredLevel.Should().Be(expected);
        result.StatementType.Should().Be(type);
    }

    [Theory]
    [InlineData("TRUNCATE users", VibePermissionLevel.Admin, "TRUNCATE")]
    [InlineData("GRANT ALL ON users TO admin", VibePermissionLevel.Admin, "GRANT")]
    [InlineData("REVOKE ALL ON users FROM admin", VibePermissionLevel.Admin, "REVOKE")]
    [InlineData("VACUUM users", VibePermissionLevel.Admin, "VACUUM")]
    [InlineData("REINDEX TABLE users", VibePermissionLevel.Admin, "REINDEX")]
    [InlineData("CLUSTER users USING idx_users_id", VibePermissionLevel.Admin, "CLUSTER")]
    public void Classify_AdminStatements(string sql, VibePermissionLevel expected, string type)
    {
        var result = SqlStatementClassifier.Classify(sql);
        result.RequiredLevel.Should().Be(expected);
        result.StatementType.Should().Be(type);
    }

    [Theory]
    [InlineData("WITH cte AS (SELECT * FROM users) SELECT * FROM cte", VibePermissionLevel.Read)]
    [InlineData("WITH x AS (SELECT 1) SELECT * FROM x", VibePermissionLevel.Read)]
    public void Classify_CteSelect_IsRead(string sql, VibePermissionLevel expected)
    {
        var result = SqlStatementClassifier.Classify(sql);
        result.RequiredLevel.Should().Be(expected);
        result.StatementType.Should().StartWith("WITH...");
    }

    [Theory]
    [InlineData("WITH cte AS (SELECT * FROM staging) INSERT INTO users SELECT * FROM cte", VibePermissionLevel.Write)]
    [InlineData("WITH x AS (SELECT 1) DELETE FROM users WHERE id IN (SELECT * FROM x)", VibePermissionLevel.Write)]
    [InlineData("WITH x AS (SELECT 1) UPDATE users SET active=true", VibePermissionLevel.Write)]
    public void Classify_CteWrite_IsWrite(string sql, VibePermissionLevel expected)
    {
        var result = SqlStatementClassifier.Classify(sql);
        result.RequiredLevel.Should().Be(expected);
    }

    [Fact]
    public void Classify_ExplainSelect_IsRead()
    {
        var result = SqlStatementClassifier.Classify("EXPLAIN SELECT * FROM users");
        result.RequiredLevel.Should().Be(VibePermissionLevel.Read);
        result.StatementType.Should().Be("EXPLAIN SELECT");
    }

    [Fact]
    public void Classify_ExplainInsert_IsWrite()
    {
        var result = SqlStatementClassifier.Classify("EXPLAIN INSERT INTO users VALUES (1)");
        result.RequiredLevel.Should().Be(VibePermissionLevel.Write);
        result.StatementType.Should().Be("EXPLAIN INSERT");
    }

    [Fact]
    public void Classify_ExplainAnalyzeSelect_IsRead()
    {
        var result = SqlStatementClassifier.Classify("EXPLAIN ANALYZE SELECT * FROM users");
        result.RequiredLevel.Should().Be(VibePermissionLevel.Read);
    }

    [Fact]
    public void Classify_ExplainAnalyzeDelete_IsWrite()
    {
        var result = SqlStatementClassifier.Classify("EXPLAIN ANALYZE DELETE FROM users WHERE id=1");
        result.RequiredLevel.Should().Be(VibePermissionLevel.Write);
    }

    [Fact]
    public void Classify_ExplainWithOptions_IsRead()
    {
        var result = SqlStatementClassifier.Classify("EXPLAIN (ANALYZE, BUFFERS) SELECT * FROM users");
        result.RequiredLevel.Should().Be(VibePermissionLevel.Read);
    }

    [Fact]
    public void Classify_LeadingLineComments_Stripped()
    {
        var result = SqlStatementClassifier.Classify("-- this is a comment\nSELECT * FROM users");
        result.RequiredLevel.Should().Be(VibePermissionLevel.Read);
        result.StatementType.Should().Be("SELECT");
    }

    [Fact]
    public void Classify_LeadingBlockComments_Stripped()
    {
        var result = SqlStatementClassifier.Classify("/* block comment */ SELECT * FROM users");
        result.RequiredLevel.Should().Be(VibePermissionLevel.Read);
        result.StatementType.Should().Be("SELECT");
    }

    [Fact]
    public void Classify_MultiStatement_RejectsWithError()
    {
        var result = SqlStatementClassifier.Classify("SELECT 1; DROP TABLE users");
        result.IsError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Multi-statement");
    }

    [Fact]
    public void Classify_SemicolonInString_NotMultiStatement()
    {
        var result = SqlStatementClassifier.Classify("SELECT * FROM users WHERE name = 'foo;bar'");
        result.IsError.Should().BeFalse();
        result.RequiredLevel.Should().Be(VibePermissionLevel.Read);
    }

    [Fact]
    public void Classify_TrailingSemicolon_NotMultiStatement()
    {
        var result = SqlStatementClassifier.Classify("SELECT * FROM users;");
        result.IsError.Should().BeFalse();
        result.RequiredLevel.Should().Be(VibePermissionLevel.Read);
    }

    [Fact]
    public void Classify_EmptySql_ReturnsError()
    {
        var result = SqlStatementClassifier.Classify("");
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void Classify_WhitespaceSql_ReturnsError()
    {
        var result = SqlStatementClassifier.Classify("   \n\t  ");
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void Classify_UnrecognizedKeyword_ReturnsError()
    {
        var result = SqlStatementClassifier.Classify("FOOBAR something");
        result.IsError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Unrecognized");
    }

    [Fact]
    public void Classify_OnlyComments_ReturnsError()
    {
        var result = SqlStatementClassifier.Classify("-- only a comment");
        result.IsError.Should().BeTrue();
    }

    [Theory]
    [InlineData("GET", "/v1/collections/users", VibePermissionLevel.Read)]
    [InlineData("POST", "/v1/query", VibePermissionLevel.Read)]
    [InlineData("POST", "/v1/collections/users", VibePermissionLevel.Write)]
    [InlineData("PUT", "/v1/collections/users/1", VibePermissionLevel.Write)]
    [InlineData("DELETE", "/v1/collections/users/1", VibePermissionLevel.Write)]
    [InlineData("GET", "/v1/schemas/my_app", VibePermissionLevel.Schema)]
    [InlineData("POST", "/v1/schemas/my_app/tables", VibePermissionLevel.Schema)]
    [InlineData("DELETE", "/v1/schemas/my_app", VibePermissionLevel.Schema)]
    public void ClassifyHttpRequest_ReturnsCorrectLevel(string method, string path, VibePermissionLevel expected)
    {
        var result = SqlStatementClassifier.ClassifyHttpRequest(method, path);
        result.Should().Be(expected);
    }

    [Fact]
    public void StripLeadingComments_MultipleComments()
    {
        var sql = "-- first\n-- second\n/* block */SELECT 1";
        var result = SqlStatementClassifier.StripLeadingComments(sql);
        result.Should().Be("SELECT 1");
    }

    [Fact]
    public void ContainsUnquotedSemicolon_DoubleQuotedIdentifier_NotDetected()
    {
        var result = SqlStatementClassifier.ContainsUnquotedSemicolon("SELECT * FROM \"table;name\"");
        result.Should().BeFalse();
    }
}
