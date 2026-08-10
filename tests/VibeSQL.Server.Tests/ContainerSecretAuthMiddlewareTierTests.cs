using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Models;
using VibeSQL.Server.Middleware;

namespace VibeSQL.Server.Tests;

/// <summary>
/// Card 209102 / 186214 lane 1: once ClientId resolves, the client tier is
/// stamped from the database and the caller-asserted X-Vibe-Client-Tier header
/// stops being authoritative. Until 195316/195919 land, GetClientTierAsync
/// returns the default tier for everyone - reads live, no per-client divergence
/// (the behavior flip is Jon-gated, out of scope).
///
/// ACCEPTANCE PIN: header-no-longer-authoritative. Exercised through the Secret
/// auth path (the JWT path shares the same PassTierHeadersAsync call).
/// </summary>
public class ContainerSecretAuthMiddlewareTierTests
{
    private const string Secret = "test-container-secret";

    private static (ContainerSecretAuthMiddleware middleware, HttpContext context, Mock<IVibeUsageRepository> usage)
        Setup(string? tierHeader, string? clientIdHeader)
    {
        IDictionary<object, object?> captured = new Dictionary<object, object?>();
        var middleware = new ContainerSecretAuthMiddleware(
            next: ctx => { captured = ctx.Items; return Task.CompletedTask; },
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
        (ContainerSecretAuthMiddleware middleware, HttpContext context, Mock<IVibeUsageRepository> usage) s) =>
        s.middleware.InvokeAsync(s.context, jwksCache: null!, s.usage.Object);

    [Fact]
    public async Task ResolvedClient_DbTierWins_OverAssertedHeader()
    {
        var s = Setup(tierHeader: "enterprise", clientIdHeader: "42");
        s.usage.Setup(u => u.GetClientTierAsync(42))
            .ReturnsAsync(new TierConfiguration { TierKey = "free" });

        await Invoke(s);

        s.context.Items["ClientId"].Should().Be(42);
        s.context.Items["ClientTier"].Should().Be("free",
            "the DB is authoritative once ClientId resolves - the asserted header loses");
    }

    [Fact]
    public async Task ResolvedClient_NullTierRow_HeaderValueStands()
    {
        var s = Setup(tierHeader: "enterprise", clientIdHeader: "42");
        s.usage.Setup(u => u.GetClientTierAsync(42)).ReturnsAsync((TierConfiguration?)null);

        await Invoke(s);

        s.context.Items["ClientTier"].Should().Be("enterprise",
            "no default tier row configured -> behavior exactly as before this lane");
    }

    [Fact]
    public async Task ResolvedClient_TierLookupThrows_HeaderValueStands()
    {
        var s = Setup(tierHeader: "enterprise", clientIdHeader: "42");
        s.usage.Setup(u => u.GetClientTierAsync(42))
            .ThrowsAsync(new InvalidOperationException("forced tier lookup failure"));

        await Invoke(s);

        s.context.Items["ClientTier"].Should().Be("enterprise",
            "tier resolution must never break an authenticated request");
    }

    [Fact]
    public async Task UnresolvedClient_NoDbLookup_HeaderValueStands()
    {
        var s = Setup(tierHeader: "enterprise", clientIdHeader: null);

        await Invoke(s);

        s.context.Items["ClientTier"].Should().Be("enterprise");
        s.usage.Verify(u => u.GetClientTierAsync(It.IsAny<int>()), Times.Never,
            "no ClientId -> no attributable tier lookup");
    }
}
