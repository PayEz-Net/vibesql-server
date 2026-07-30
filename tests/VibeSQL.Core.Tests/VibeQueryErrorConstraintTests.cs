using Xunit;
using FluentAssertions;
using VibeSQL.Core.Query;

namespace VibeSQL.Core.Tests;

/// <summary>
/// Entry 1 reference-pattern test backfill: covers the constraint-observability
/// surface (SQLSTATE class 23) that was added in commit fb4d1e7.
/// Closes v0.3 §5 point 5 (test coverage decided up-front) for the reference pattern.
/// </summary>
public class VibeQueryErrorConstraintTests
{
    [Theory]
    [InlineData("23000")] // integrity_constraint_violation (generic)
    [InlineData("23001")] // restrict_violation
    [InlineData("23502")] // not_null_violation
    [InlineData("23503")] // foreign_key_violation
    [InlineData("23505")] // unique_violation
    [InlineData("23514")] // check_violation
    [InlineData("23P01")] // exclusion_violation
    public void IsConstraintViolation_Class23Sqlstate_ReturnsTrue(string sqlState)
    {
        SqlStateMapper.IsConstraintViolation(sqlState).Should().BeTrue(
            $"SQLSTATE {sqlState} is in class 23 and must route through the CONSTRAINT_VIOLATION observability path");
    }

    [Theory]
    [InlineData("42P01")] // undefined_table (class 42 — syntax/access)
    [InlineData("22P02")] // invalid_text_representation (class 22 — data exception)
    [InlineData("57014")] // query_canceled (class 57 — operator intervention)
    [InlineData("08006")] // connection_failure (class 08 — connection exception)
    [InlineData("")]
    [InlineData(null)]
    public void IsConstraintViolation_NonClass23OrNullOrEmpty_ReturnsFalse(string? sqlState)
    {
        SqlStateMapper.IsConstraintViolation(sqlState).Should().BeFalse(
            $"SQLSTATE '{sqlState ?? "<null>"}' is not class 23 and must not trigger constraint observability");
    }

    [Theory]
    [InlineData(VibeErrorCodes.UniqueViolation)]
    [InlineData(VibeErrorCodes.ForeignKeyViolation)]
    [InlineData(VibeErrorCodes.ExclusionViolation)]
    [InlineData(VibeErrorCodes.ConstraintViolation)]
    public void VibeQueryError_ConflictConstraintCodes_MapTo409(string code)
    {
        var error = new VibeQueryError(code, "constraint violated", "detail");

        error.Code.Should().Be(code);
        error.HttpStatusCode.Should().Be(409,
            $"{code} represents a conflict with existing data and must surface as HTTP 409");
    }

    [Theory]
    [InlineData(VibeErrorCodes.NotNullViolation)]
    [InlineData(VibeErrorCodes.CheckViolation)]
    public void VibeQueryError_BadRequestConstraintCodes_MapTo400(string code)
    {
        var error = new VibeQueryError(code, "constraint violated", "detail");

        error.Code.Should().Be(code);
        error.HttpStatusCode.Should().Be(400,
            $"{code} represents a client-side input problem and must surface as HTTP 400");
    }
}
