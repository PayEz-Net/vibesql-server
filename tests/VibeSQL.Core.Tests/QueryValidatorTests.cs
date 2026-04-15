using Xunit;
using FluentAssertions;
using VibeSQL.Core.Query;

namespace VibeSQL.Core.Tests;

/// <summary>
/// Entry 3 upstreaming — QueryValidator DX tests.
///
/// Covers the three additions in the Entry 3 upstreaming program:
///   1. Schema-write detection precision: only INSERT INTO / UPDATE
///      collection_schemas lift the limit from 256KB to 512KB. SELECT,
///      DELETE, and DDL on that table stay on the default 256KB limit.
///   2. Oversized-query error details include the first 80-char preview
///      of the offending SQL (QueryTooLarge).
///   3. Invalid-keyword error details include the same 80-char preview
///      (InvalidSQL).
///
/// Unicode slicing decision — Option 2 (pragmatic code-unit slice + high-
/// surrogate guard). Rationale: the preview is a diagnostic string in an
/// error body, not a security boundary. Full rune enumeration via
/// System.Text.Rune / EnumerateRunes would be defensively correct but
/// costs 5-10 lines for a case nobody hits in practice. Pure code-unit
/// slicing leaves a split surrogate pair at the boundary, which some JSON
/// serializers emit as U+FFFD — a visible polish failure in the error UX.
/// Option 2 slices by code unit and checks whether the last code unit of
/// the slice is a high surrogate; if so, shortens by one so the pair is
/// not split. Three lines, handles the realistic emoji-at-position-79
/// case cleanly, keeps the JSON serializer happy. Test
/// `QueryTooBigWithEmojiAtBoundary_Preview_DropsSurrogatePair` exercises
/// this exact path. A future reviewer should not upgrade this to Option 1
/// without an explicit trade-off conversation — the current choice is
/// intentional.
/// </summary>
public class QueryValidatorTests
{
    private readonly QueryValidator _validator = new();

    private const int OverQuarterMb = (256 * 1024) + 1024; // 257KB — above the default 256KB limit

    // -----------------------------------------------------------------
    // Schema-write detection precision (Entry 3 addition 1 + QAPert S1)
    // -----------------------------------------------------------------

    [Fact]
    public void LargeInsertIntoCollectionSchemas_UnderHalfMb_Passes()
    {
        var body = new string('x', OverQuarterMb);
        var sql = $"INSERT INTO collection_schemas (id, body) VALUES (1, '{body}')";

        var act = () => _validator.Validate(sql);

        act.Should().NotThrow(
            "INSERT INTO collection_schemas must get the 512KB schema-write allowance " +
            "so that schema rows with full JSON definitions can be written");
    }

    [Fact]
    public void LargeUpdateCollectionSchemas_UnderHalfMb_Passes()
    {
        var body = new string('x', OverQuarterMb);
        var sql = $"UPDATE collection_schemas SET body = '{body}' WHERE id = 1";

        var act = () => _validator.Validate(sql);

        act.Should().NotThrow(
            "UPDATE collection_schemas must get the 512KB schema-write allowance " +
            "for the same reason as INSERT INTO");
    }

    [Fact]
    public void LargeSelectFromCollectionSchemas_OverQuarterMb_IsRejected()
    {
        var body = new string('x', OverQuarterMb);
        var sql = $"SELECT * FROM collection_schemas WHERE note = '{body}'";

        var act = () => _validator.Validate(sql);

        act.Should().Throw<VibeQueryError>()
            .Where(e => e.Code == VibeErrorCodes.QueryTooLarge,
                "SELECTs from collection_schemas do not need 512KB — " +
                "this guards against a future drift back to loose Contains-based detection");
    }

    [Fact]
    public void LargeDeleteFromCollectionSchemas_OverQuarterMb_IsRejected()
    {
        var body = new string('x', OverQuarterMb);
        var sql = $"DELETE FROM collection_schemas WHERE note = '{body}'";

        var act = () => _validator.Validate(sql);

        act.Should().Throw<VibeQueryError>()
            .Where(e => e.Code == VibeErrorCodes.QueryTooLarge,
                "DELETE is not a schema write and must get the default 256KB limit " +
                "(QAPert S1): schema-write detection is INSERT INTO / UPDATE only");
    }

    // -----------------------------------------------------------------
    // Oversized-query preview (Entry 3 addition 2)
    // -----------------------------------------------------------------

    [Fact]
    public void QueryTooLarge_Detail_ContainsFirst80CharPreview()
    {
        // Prefix is exactly 80 chars via PadRight so the preview is deterministic.
        var prefix = "SELECT ".PadRight(80, 'a');
        prefix.Length.Should().Be(80, "test setup requires an exactly-80-char prefix");
        var sql = prefix + new string('x', OverQuarterMb);

        var act = () => _validator.Validate(sql);

        act.Should().Throw<VibeQueryError>()
            .Where(e => e.Code == VibeErrorCodes.QueryTooLarge &&
                        e.Detail.Contains($"Starts with: {prefix}..."),
                "oversized-query error detail must echo the first 80 chars of the SQL " +
                "so clients can identify which query tripped the limit");
    }

    // -----------------------------------------------------------------
    // Invalid-keyword preview (Entry 3 addition 3)
    // -----------------------------------------------------------------

    [Fact]
    public void InvalidKeyword_LongQuery_Detail_ContainsFirst80CharPreview()
    {
        var prefix = "NOTAKEYWORD ".PadRight(80, 'z');
        prefix.Length.Should().Be(80, "test setup requires an exactly-80-char prefix");
        var sql = prefix + " and some more padding to push this well past the 80-char preview boundary";

        var act = () => _validator.Validate(sql);

        act.Should().Throw<VibeQueryError>()
            .Where(e => e.Code == VibeErrorCodes.InvalidSQL &&
                        e.Detail.Contains($"Received: {prefix}..."),
                "invalid-keyword error detail must include the 80-char preview so " +
                "clients see what their query started with instead of a generic message");
    }

    [Fact]
    public void InvalidKeyword_ShortQuery_Detail_ShowsFullQueryWithNoEllipsis()
    {
        var sql = "NOTAKEYWORD foo bar";
        sql.Length.Should().BeLessThan(80);

        var act = () => _validator.Validate(sql);

        act.Should().Throw<VibeQueryError>()
            .Where(e => e.Code == VibeErrorCodes.InvalidSQL &&
                        e.Detail.Contains($"Received: {sql}") &&
                        !e.Detail.Contains($"Received: {sql}..."),
                "short queries under the 80-char preview cap must appear in full without " +
                "an ellipsis — the preview equals the original query in this edge case");
    }

    [Fact]
    public void InvalidKeyword_EmojiAtSliceBoundary_PreviewDropsSurrogatePair()
    {
        // Construct: 4-char invalid prefix + 75 'x' padding + grinning face emoji
        // (U+1F600 = high surrogate U+D83D at pos 79, low surrogate U+DE00 at pos 80)
        // + trailing content. Emoji's high surrogate lands exactly at the slice
        // boundary, exercising the Option 2 pragmatic guard path in BuildPreview.
        var prefix = "FOO " + new string('x', 75); // 4 + 75 = 79 chars
        var sql = prefix + "\uD83D\uDE00 trailing content that makes the query exceed 80 chars";

        // Sanity: high surrogate sits at index 79 so the slice boundary bisects the pair.
        char.IsHighSurrogate(sql[79]).Should().BeTrue(
            "test setup requires the high surrogate of the emoji to land at code unit 79");

        var act = () => _validator.Validate(sql);

        act.Should().Throw<VibeQueryError>()
            .Where(e => e.Code == VibeErrorCodes.InvalidSQL &&
                        e.Detail.Contains($"Received: {prefix}...") &&
                        !e.Detail.Contains("\uFFFD") &&
                        !e.Detail.Contains("\uD83D") &&
                        !e.Detail.Contains("\uDE00"),
                "Option 2 pragmatic slicing must drop the full surrogate pair when it lands " +
                "at the boundary; the preview should be a 79-char clean truncation with " +
                "neither a replacement char nor a split surrogate in the error body");
    }
}
