using Xunit;
using FluentAssertions;
using VibeSQL.Core.Query;

namespace VibeSQL.Core.Tests;

public class VibeQueryErrorSmokeTests
{
    [Fact]
    public void VibeQueryError_UnsafeQueryCode_MapsTo400()
    {
        var error = new VibeQueryError(
            VibeErrorCodes.UnsafeQuery,
            "UPDATE without WHERE",
            "UPDATE queries must have a WHERE clause for safety");

        error.Code.Should().Be("UNSAFE_QUERY");
        error.HttpStatusCode.Should().Be(400);
    }
}
