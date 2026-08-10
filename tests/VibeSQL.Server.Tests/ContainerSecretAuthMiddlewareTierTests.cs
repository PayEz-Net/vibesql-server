using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Models;
using VibeSQL.Server.Middleware;

namespace VibeSQL.Server.Tests;

/// <summary>
/// Card 209102 / 186214 lane 1, REWORKED per QAPert reject 26967 (done-leg b):
/// the DB read is ALWAYS live once ClientId resolves (tier_configurations reads
/// on the request path - the lane's purpose), but the STAMP over
/// Items["ClientTier"] is gated behind VibeSQL:TierResolution:DbAuthoritative
/// (default false) - reads-live, behavior-inert BY CONSTRUCTION, not by an
/// unmeasured assumption about the live default row's tier_key. Enabling the
/// flag is the Jon-gated behavior flip, after 195316/195919.
///
/// Exercised through the Secret auth path (the JWT path shares the same
/// PassTierHeadersAsync call).
/// </summary>
public class ContainerSecretAuthMiddlewareTierTests
{
    private const string Secret = "test-container-secret";

    private static (ContainerSecretAuthMiddleware middleware, HttpContext context, Mock<IVibeUsageRepository> usage)
        Setup(string? tierHeader, string? clientIdHeader)
    {
        var middleware = new ContainerSecretAuthMiddleware(
            next: ctx => Task.CompletedTask,
            NullLogger<ContainerSecretAuthMiddleware>.Instance,
            new VibeContainerSecretConfig { Secret = Secret });

        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/query";
        context.Request.Headers["Authorization"] = $"Secret {Secret}";
        if (tierHeader != null) context.Request.Headers["X-Vibe-Client-Tier"] = tierHeader;
        if (clientIdHeader != null) context.Request.Headers["X-Vibe-Resolved-Client-Id"] = clientIdHeader;

        var usage = new Mock<IVibeUsageRepository>();
        return (middleware, context, usage);
    }

    private static Task Invoke(
        (ContainerSecretAuthMiddleware middleware, HttpContext context, Mock<IVibeUsageRepository> usage) s,
        bool dbAuthoritative)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VibeSQL:TierResolution:DbAuthoritative"] = dbAuthoritative ? "true" : "false"
            })
            .Build();
        return s.middleware.InvokeAsync(s.context, jwksCache: null!, s.usage.Object, configuration);
    }

    [Fact]
    public async Task ResolvedClient_FlagOff_ReadsLive_ButHeaderStands()
    {
        // THE REWORK PIN: with the gate off (the shipping default), the DB read
        // still happens on the request path but the asserted header is untouched -
        // behavior identical to pre-lane regardless of what the live default
        // row's tier_key is.
        var s = Setup(tierHeader: "enterprise", clientIdHeader: "42");
        s.usage.Setup(u => u.GetClientTierAsync(42))
            .ReturnsAsync(new TierConfiguration { TierKey = "free" });

        await Invoke(s, dbAuthoritative: false);

        s.context.Items["ClientId"].Should().Be(42);
        s.context.Items["ClientTier"].Should().Be("enterprise",
            "gate off -> the stamp never happens, whatever the DB row says");
        s.usage.Verify(u => u.GetClientTierAsync(42), Times.Once,
            "the read IS live on the request path even with the gate off");
    }

    [Fact]
    public async Task ResolvedClient_FlagOn_DbTierWins_OverAssertedHeader()
    {
        // The Jon-gated flip, exercised in its enabled state: once the flag is
        // set (post-195316/195919 + Jon's call), the DB is authoritative.
        var s = Setup(tierHeader: "enterprise", clientIdHeader: "42");
        s.usage.Setup(u => u.GetClientTierAsync(42))
            .ReturnsAsync(new TierConfiguration { TierKey = "free" });

        await Invoke(s, dbAuthoritative: true);

        s.context.Items["ClientTier"].Should().Be("free",
            "gate on -> the DB stamps over the asserted header");
    }

    [Fact]
    public async Task ResolvedClient_NullTierRow_HeaderValueStands()
    {
        var s = Setup(tierHeader: "enterprise", clientIdHeader: "42");
        s.usage.Setup(u => u.GetClientTierAsync(42)).ReturnsAsync((TierConfiguration?)null);

        await Invoke(s, dbAuthoritative: true);

        s.context.Items["ClientTier"].Should().Be("enterprise",
            "no tier row -> nothing to stamp, header stands");
    }

    [Fact]
    public async Task ResolvedClient_TierLookupThrows_HeaderValueStands()
    {
        var s = Setup(tierHeader: "enterprise", clientIdHeader: "42");
        s.usage.Setup(u => u.GetClientTierAsync(42))
            .ThrowsAsync(new InvalidOperationException("forced tier lookup failure"));

        await Invoke(s, dbAuthoritative: true);

        s.context.Items["ClientTier"].Should().Be("enterprise",
            "tier resolution must never break an authenticated request");
    }

    [Fact]
    public async Task UnresolvedClient_NoDbLookup_HeaderValueStands()
    {
        var s = Setup(tierHeader: "enterprise", clientIdHeader: null);

        await Invoke(s, dbAuthoritative: true);

        s.context.Items["ClientTier"].Should().Be("enterprise");
        s.usage.Verify(u => u.GetClientTierAsync(It.IsAny<int>()), Times.Never,
            "no ClientId -> no attributable tier lookup");
    }
}
