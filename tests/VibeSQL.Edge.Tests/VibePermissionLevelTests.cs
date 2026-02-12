using Xunit;
using FluentAssertions;
using VibeSQL.Edge.Data.Entities;

namespace VibeSQL.Edge.Tests;

public class VibePermissionLevelTests
{
    [Theory]
    [InlineData(VibePermissionLevel.None, VibePermissionLevel.Read)]
    [InlineData(VibePermissionLevel.Read, VibePermissionLevel.Write)]
    [InlineData(VibePermissionLevel.Write, VibePermissionLevel.Schema)]
    [InlineData(VibePermissionLevel.Schema, VibePermissionLevel.Admin)]
    public void PermissionLevels_AreOrdered_Correctly(VibePermissionLevel lower, VibePermissionLevel higher)
    {
        ((int)lower).Should().BeLessThan((int)higher);
    }

    [Theory]
    [InlineData("none", VibePermissionLevel.None)]
    [InlineData("read", VibePermissionLevel.Read)]
    [InlineData("write", VibePermissionLevel.Write)]
    [InlineData("schema", VibePermissionLevel.Schema)]
    [InlineData("admin", VibePermissionLevel.Admin)]
    [InlineData("ADMIN", VibePermissionLevel.Admin)]
    [InlineData("Read", VibePermissionLevel.Read)]
    public void Parse_ReturnsCorrectLevel(string input, VibePermissionLevel expected)
    {
        VibePermissionLevelExtensions.Parse(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("superadmin")]
    public void Parse_UnknownValue_ReturnsNone(string input)
    {
        VibePermissionLevelExtensions.Parse(input).Should().Be(VibePermissionLevel.None);
    }

    [Theory]
    [InlineData(VibePermissionLevel.None, "none")]
    [InlineData(VibePermissionLevel.Read, "read")]
    [InlineData(VibePermissionLevel.Write, "write")]
    [InlineData(VibePermissionLevel.Schema, "schema")]
    [InlineData(VibePermissionLevel.Admin, "admin")]
    public void ToDbString_ReturnsCorrectString(VibePermissionLevel level, string expected)
    {
        level.ToDbString().Should().Be(expected);
    }

    [Fact]
    public void Parse_And_ToDbString_RoundTrip()
    {
        foreach (var level in Enum.GetValues<VibePermissionLevel>())
        {
            var dbString = level.ToDbString();
            VibePermissionLevelExtensions.Parse(dbString).Should().Be(level);
        }
    }

    [Fact]
    public void PermissionComparison_HighestWins()
    {
        var levels = new[] { VibePermissionLevel.Read, VibePermissionLevel.Admin, VibePermissionLevel.Write };
        var max = levels.Max();
        max.Should().Be(VibePermissionLevel.Admin);
    }

    [Fact]
    public void PermissionComparison_MinCaps()
    {
        var roleLevel = VibePermissionLevel.Admin;
        var clientMax = VibePermissionLevel.Write;
        var effective = (VibePermissionLevel)Math.Min((int)roleLevel, (int)clientMax);
        effective.Should().Be(VibePermissionLevel.Write);
    }
}
