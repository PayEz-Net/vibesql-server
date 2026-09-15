using System.Text.Json;
using FluentAssertions;

namespace VibeSQL.Edge.Tests;

/// <summary>
/// Card 394479. Pins the Graylog fail-open remedy in THIS repository so it cannot be quietly
/// undone.
///
/// *** THE DEFECT. *** Both Program.cs files registered a UDP Graylog sink with
/// `HostnameOrAddress = graylogHost ?? "localhost"`, and every appsettings.json supplied
/// `"localhost"` as the configured value. `??` only catches NULL, so the fallback was STRICTLY
/// WEAKER than no check: an EMPTY host reached the sink verbatim. And because the sink is UDP,
/// there is no delivery failure to observe - a sink aimed at a box with no Graylog listener and
/// a healthy sink are indistinguishable from inside the process while every log line is
/// discarded.
///
/// *** THE SCENARIO THE CARD WAS FILED ABOUT IS A DROPPED OR MISTYPED ENV VAR KEY, AND A GATE ON
/// THE VALUE CANNOT SEE IT. *** A gate can refuse a value it is given; it cannot notice that the
/// value came from the wrong place. While appsettings supplied a non-empty default, the
/// "unconfigured" branch was UNREACHABLE - dead code that looked like a safety net. The fix is
/// therefore CONFIG as well as code: the host is `""` in every appsettings file, so a dropped
/// env var lands on the WARNING branch and is LOUD instead of silent.
///
/// *** WHY EMPTY STRING AND NOT A DELETED KEY. *** An absent key reads as an oversight and
/// invites the next person to helpfully put `"localhost"` back, silently restoring this exact
/// defect. A present-and-empty key is a visible decision, and this guard pins it as one.
///
/// *** THE Development.json FILE IS THE LOAD-BEARING ONE HERE, NOT THE BASE FILE. ***
/// `appsettings.Development.json` OUTRANKS `appsettings.json`, so emptying only the base file
/// would have been defeated by it. This repository's Server Development file DID carry
/// `"localhost"` (line 11) - and note that PayEz-Core's vendored copy of this tree has NO Graylog
/// key in its Development files at all. *** THE TWO TREES DIVERGE, so a green run on the vendored
/// copy never had to consider this file. *** That is precisely why this guard lives here.
///
/// *** SCOPE - WHAT A GREEN RUN HERE DOES AND DOES NOT MEAN. *** `docker/Dockerfile` builds
/// VibeSQL.Server ONLY (with VibeSQL.Core as a dependency). NOTHING in the vsql estate builds
/// VibeSQL.Edge - and there is a trap in the name: a directory called `vibesql-edge` builds a
/// DIFFERENT project, Vibe.Edge.csproj -> Vibe.Edge.dll. So the Server assertions below are about
/// a SHIPPING service and the Edge assertions are source hygiene. "The source contains a defect"
/// and "the defect ships" are different claims; they are kept apart here deliberately, per
/// project and not per directory.
///
/// *** FIXTURE DESIGN IS ONE PREDICATE PER FIXTURE, EACH ASSERTED ALONE. *** A single fixture
/// carrying several defects goes red if ANY one of them matches, so a broken clause sitting
/// beside a working one produces a green-looking control run and stays broken. Each file gets its
/// own assertion and its own verdict, and each detector has a control that FAILS FOR ITS OWN
/// REASON - a control that only proves the instrument is ALIVE does not prove it is LOOKING AT
/// THE RIGHT THING.
/// </summary>
public class GraylogHostHasNoFailOpenDefaultTests
{
    // ---------------------------------------------------------------------------------------
    // The two detectors under test. They are used BOTH against the real tree and against
    // synthetic controls, so a control genuinely exercises the same code path as the assertion.
    // ---------------------------------------------------------------------------------------

    /// <summary>Reads Logging:Graylog:HostnameOrAddress out of appsettings JSON text.</summary>
    internal static string? ConfiguredGraylogHost(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("Logging", out var logging)) return null;
        if (!logging.TryGetProperty("Graylog", out var graylog)) return null;
        if (!graylog.TryGetProperty("HostnameOrAddress", out var host)) return null;
        return host.GetString();
    }

    /// <summary>
    /// True when source text still contains the fail-open coalesce AS CODE.
    ///
    /// *** COMMENTS ARE STRIPPED FIRST, AND THAT IS LOAD-BEARING, NOT A CONVENIENCE. *** The
    /// remedy in both Program.cs files is a comment that QUOTES the defect verbatim
    /// (`DO NOT RESTORE graylogHost ?? "localhost"`), because a warning that does not name the
    /// exact thing it is warning about is not a warning. A naive substring scan therefore fired
    /// on the fix itself - measured, this test failed 2/2 on its first run for exactly that
    /// reason. *** A DETECTOR THAT CANNOT TELL CODE FROM PROSE PUNISHES DOCUMENTING THE DEFECT,
    /// which is how a guard trains people to delete the explanation and keep the bug. ***
    ///
    /// The stripping is line-comment only and deliberately crude. It would mis-handle a `//`
    /// inside a string literal; no such line exists in either file, and the control below pins
    /// that stripping has NOT blinded the detector to real code.
    /// </summary>
    internal static bool ContainsFailOpenCoalesce(string source)
        => StripLineComments(source).Contains("graylogHost ?? \"", StringComparison.Ordinal);

    internal static string StripLineComments(string source)
    {
        var kept = source
            .Split('\n')
            .Select(line =>
            {
                var marker = line.IndexOf("//", StringComparison.Ordinal);
                return marker >= 0 ? line[..marker] : line;
            });

        return string.Join("\n", kept);
    }

    // ---------------------------------------------------------------------------------------
    // POSITIVE CONTROLS - one per predicate, asserted alone.
    // Each proves its OWN detector can FAIL, on the exact shape the real defect had.
    // Without these, a detector that returned "clean" for everything would make every
    // assertion below pass while the defect sat untouched in the tree.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Control_ConfigDetector_FlagsALocalhostDefault()
        => ConfiguredGraylogHost(
                """
                { "Logging": { "Graylog": { "HostnameOrAddress": "localhost", "Port": "12201" } } }
                """)
            .Should().Be("localhost",
                "the config detector must be able to SEE a localhost default - if this fixture " +
                "does not come back as 'localhost' the detector is broken and every " +
                "appsettings assertion in this class is vacuously green");

    [Fact]
    public void Control_ConfigDetector_DistinguishesEmptyFromAbsent()
    {
        ConfiguredGraylogHost("""{ "Logging": { "Graylog": { "HostnameOrAddress": "" } } }""")
            .Should().Be("", "an explicitly empty host is the remedy and must read back as empty");

        ConfiguredGraylogHost("""{ "Logging": { "LogLevel": { "Default": "Information" } } }""")
            .Should().BeNull(
                "an ABSENT key must be distinguishable from an empty one - they are the same " +
                "behaviour at runtime but not the same decision, and this guard pins the decision");
    }

    [Fact]
    public void Control_SourceDetector_FlagsTheFailOpenCoalesce()
        => ContainsFailOpenCoalesce("HostnameOrAddress = graylogHost ?? \"localhost\",")
            .Should().BeTrue(
                "the source detector must fire on the exact line this card was filed about - " +
                "otherwise the Program.cs assertions below prove nothing");

    [Fact]
    public void Control_SourceDetector_DoesNotFlagTheRemedy()
        => ContainsFailOpenCoalesce("HostnameOrAddress = graylogHost,")
            .Should().BeFalse(
                "a detector that fires on everything is as useless as one that fires on nothing; " +
                "this pins that the remedy itself is not reported as the defect");

    [Fact]
    public void Control_SourceDetector_DoesNotFlagTheDefectQuotedInAComment()
        => ContainsFailOpenCoalesce("// DO NOT RESTORE graylogHost ?? \"localhost\" - it fails open.")
            .Should().BeFalse(
                "the remedy in both Program.cs files QUOTES the defect in a comment so the next " +
                "reader knows exactly what not to write back. If this fires, the guard punishes " +
                "documenting the defect and the cheapest way to go green is to delete the warning");

    [Fact]
    public void Control_SourceDetector_StillFlagsRealCodeBesideSuchAComment()
        => ContainsFailOpenCoalesce(
                """
                // DO NOT RESTORE graylogHost ?? "localhost" - it fails open.
                HostnameOrAddress = graylogHost ?? "localhost",
                """)
            .Should().BeTrue(
                "*** THIS IS THE ONE THAT MATTERS. *** Stripping comments is how the detector " +
                "stops firing on its own warning - and the obvious way to get that wrong is to " +
                "discard the whole file the moment a comment mentions the pattern, which would " +
                "make this guard green while the live defect sat two lines below. A control that " +
                "only proves the detector is ALIVE would not catch that; this one fails for that " +
                "exact reason");

    // ---------------------------------------------------------------------------------------
    // THE REAL TREE - one file per fact, each asserted alone so the failure message names
    // the exact file an operator has to edit.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ServerBaseAppsettings_HasNoFailOpenHost()
        => AssertNoFailOpenHost("src/VibeSQL.Server/appsettings.json");

    [Fact]
    public void ServerDevelopmentAppsettings_HasNoFailOpenHost()
        => AssertNoFailOpenHost("src/VibeSQL.Server/appsettings.Development.json");

    [Fact]
    public void EdgeBaseAppsettings_HasNoFailOpenHost()
        => AssertNoFailOpenHost("src/VibeSQL.Edge/appsettings.json");

    [Fact]
    public void ServerProgram_DoesNotRestoreTheFailOpenCoalesce()
        => AssertNoFailOpenCoalesce("src/VibeSQL.Server/Program.cs");

    [Fact]
    public void EdgeProgram_DoesNotRestoreTheFailOpenCoalesce()
        => AssertNoFailOpenCoalesce("src/VibeSQL.Edge/Program.cs");

    private static void AssertNoFailOpenHost(string relativePath)
    {
        var path = Path.Combine(RepoRoot(), relativePath);
        File.Exists(path).Should().BeTrue(
            "{0} is the file this assertion is about - if it moved, this guard stopped guarding " +
            "and must be repointed rather than deleted", relativePath);

        var host = ConfiguredGraylogHost(File.ReadAllText(path));

        host.Should().NotBeNull(
            "{0} must keep a PRESENT-and-empty Graylog host. An absent key behaves identically " +
            "today but reads as an oversight, and the next person helpfully restores " +
            "\"localhost\" - which is exactly the defect card 394479 was filed about",
            relativePath);

        host.Should().BeEmpty(
            "{0} must not supply a Graylog host default. A non-empty default here makes the " +
            "\"unconfigured\" WARNING branch in Program.cs UNREACHABLE, so a dropped or mistyped " +
            "Logging__Graylog__HostnameOrAddress env var silently aims a UDP sink at a box with " +
            "no listener and every log line is discarded with nothing to observe. Set it to \"\" " +
            "and configure the real host via the environment variable, which outranks this file",
            relativePath);
    }

    private static void AssertNoFailOpenCoalesce(string relativePath)
    {
        var path = Path.Combine(RepoRoot(), relativePath);
        File.Exists(path).Should().BeTrue(
            "{0} is the file this assertion is about - if it moved, this guard stopped guarding " +
            "and must be repointed rather than deleted", relativePath);

        ContainsFailOpenCoalesce(File.ReadAllText(path)).Should().BeFalse(
            "{0} must not restore `graylogHost ?? \"...\"`. That coalesce only catches NULL, so " +
            "an EMPTY host still reached the sink verbatim - it was strictly weaker than no " +
            "check at all. Guard with string.IsNullOrWhiteSpace and skip registering the sink",
            relativePath);
    }

    /// <summary>
    /// Walks up from the test binary to the directory holding VibeSQL-Server.sln. Anchored on a
    /// file that must exist at the repo root rather than a hop count, so moving the test project
    /// does not silently repoint every assertion at the wrong tree - the scan-root failure that
    /// makes a guard go green by looking at nothing.
    /// </summary>
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "VibeSQL-Server.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate VibeSQL-Server.sln above " + AppContext.BaseDirectory +
            ". This guard asserts on files by repo-relative path; failing loudly here is " +
            "deliberate, because a guard that cannot find the tree would otherwise pass.");
    }
}
