using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VibeSQL.Edge;
using VibeSQL.Edge.Authentication;
using VibeSQL.Edge.Data;
using VibeSQL.Edge.Data.Entities;
using VibeSQL.Edge.Identity;

namespace VibeSQL.Edge.Tests;

public class IdentityResolutionMiddlewareTests
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    private EdgeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EdgeDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        return new EdgeDbContext(options);
    }

    private void SeedRegistry(IProviderRegistry registry, string key = "test-provider", bool autoProvision = false)
    {
        registry.Replace(new[]
        {
            new ProviderRecord
            {
                ProviderKey = key,
                Issuer = "https://test.example.com",
                SchemeId = $"oidc-{key}",
                IsActive = true,
                IsBootstrap = false,
                AutoProvision = autoProvision,
                SubjectClaimPath = "sub",
                RoleClaimPath = "roles",
                EmailClaimPath = "email"
            }
        });
    }

    private async Task SeedProvider(string key = "test-provider", bool autoProvision = false)
    {
        using var db = CreateDbContext();
        db.OidcProviders.Add(new OidcProvider
        {
            ProviderKey = key,
            DisplayName = "Test",
            Issuer = "https://test.example.com",
            DiscoveryUrl = "https://test.example.com/.well-known/openid-configuration",
            Audience = "test-api",
            AutoProvision = autoProvision
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedFederatedIdentity(string providerKey, string externalSubject, int vibeUserId, bool isActive = true)
    {
        using var db = CreateDbContext();
        db.FederatedIdentities.Add(new FederatedIdentity
        {
            ProviderKey = providerKey,
            ExternalSubject = externalSubject,
            VibeUserId = vibeUserId,
            IsActive = isActive
        });
        await db.SaveChangesAsync();
    }

    private (HttpContext context, bool nextCalled) CreateAuthenticatedContext(
        string providerKey,
        IProviderRegistry? registry = null,
        params Claim[] claims)
    {
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity(claims, "Bearer");
        context.User = new ClaimsPrincipal(identity);
        context.Items[EdgeContextKeys.ProviderKey] = providerKey;
        context.Response.Body = new MemoryStream();

        var services = new ServiceCollection();
        services.AddDbContext<EdgeDbContext>(options =>
            options.UseInMemoryDatabase(_dbName));
        services.AddScoped<IClaimExtractor, ClaimExtractor>();
        services.AddScoped<IUserProvisioningService, UserProvisioningService>();
        services.AddScoped<IFederatedIdentityResolver, FederatedIdentityResolver>();
        services.AddSingleton<IProviderRegistry>(registry ?? new ProviderRegistry());
        services.AddLogging();
        context.RequestServices = services.BuildServiceProvider();

        var nextCalled = false;

        return (context, nextCalled);
    }

    private IdentityResolutionMiddleware CreateMiddleware(Action? onNext = null)
    {
        RequestDelegate next = ctx =>
        {
            onNext?.Invoke();
            return Task.CompletedTask;
        };
        return new IdentityResolutionMiddleware(next, NullLogger<IdentityResolutionMiddleware>.Instance);
    }

    [Fact]
    public async Task InvokeAsync_UnauthenticatedRequest_PassesThrough()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(onNext: () => nextCalled = true);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedNoProviderKey_PassesThrough()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(onNext: () => nextCalled = true);
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "user-1") }, "Bearer"));

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_ProviderNotInRegistry_Returns500()
    {
        var registry = new ProviderRegistry();
        var (context, _) = CreateAuthenticatedContext("nonexistent-provider", registry, new Claim("sub", "user-1"));
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task InvokeAsync_ClaimExtractionFails_Returns401()
    {
        var registry = new ProviderRegistry();
        SeedRegistry(registry, "test-provider");
        var (context, _) = CreateAuthenticatedContext("test-provider", registry);
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task InvokeAsync_IdentityNotResolved_Returns403()
    {
        var registry = new ProviderRegistry();
        SeedRegistry(registry, "test-provider", autoProvision: false);
        var (context, _) = CreateAuthenticatedContext("test-provider", registry, new Claim("sub", "unknown-user"));
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task InvokeAsync_IdentityResolved_SetsContextItems()
    {
        var registry = new ProviderRegistry();
        SeedRegistry(registry, "test-provider");
        await SeedFederatedIdentity("test-provider", "ext-user-1", 42);

        var nextCalled = false;
        var middleware = CreateMiddleware(onNext: () => nextCalled = true);
        var (context, _) = CreateAuthenticatedContext("test-provider", registry,
            new Claim("sub", "ext-user-1"),
            new Claim("email", "user@test.com"));

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Items[EdgeContextKeys.UserId].Should().Be(42);
        context.Items[EdgeContextKeys.ProviderKey].Should().Be("test-provider");
        context.Items[EdgeContextKeys.ExternalSubject].Should().Be("ext-user-1");
        context.Items[EdgeContextKeys.ExtractedRoles].Should().BeAssignableTo<IReadOnlyList<string>>();
    }

    [Fact]
    public async Task InvokeAsync_AutoProvision_CreatesAndResolvesIdentity()
    {
        UserProvisioningService.ResetForTesting();
        var registry = new ProviderRegistry();
        SeedRegistry(registry, "test-provider", autoProvision: true);

        var nextCalled = false;
        var middleware = CreateMiddleware(onNext: () => nextCalled = true);
        var (context, _) = CreateAuthenticatedContext("test-provider", registry,
            new Claim("sub", "new-user"),
            new Claim("email", "new@example.com"));

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Items[EdgeContextKeys.UserId].Should().NotBeNull();
        ((int)context.Items[EdgeContextKeys.UserId]!).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task InvokeAsync_DeactivatedIdentity_Returns403()
    {
        var registry = new ProviderRegistry();
        SeedRegistry(registry, "test-provider");
        await SeedFederatedIdentity("test-provider", "ext-user-1", 42, isActive: false);

        var middleware = CreateMiddleware();
        var (context, _) = CreateAuthenticatedContext("test-provider", registry, new Claim("sub", "ext-user-1"));

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task InvokeAsync_SetsClientIdFromAudClaim()
    {
        var registry = new ProviderRegistry();
        SeedRegistry(registry, "test-provider");
        await SeedFederatedIdentity("test-provider", "ext-user-1", 42);

        var nextCalled = false;
        var middleware = CreateMiddleware(onNext: () => nextCalled = true);
        var (context, _) = CreateAuthenticatedContext("test-provider", registry,
            new Claim("sub", "ext-user-1"),
            new Claim("aud", "my-client-app"));

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Items[EdgeContextKeys.ClientId].Should().Be("my-client-app");
    }
}
