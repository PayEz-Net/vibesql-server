using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VibeSQL.Edge.Authorization;
using VibeSQL.Edge.Data;
using VibeSQL.Edge.Data.Entities;

namespace VibeSQL.Edge.Tests;

public class PermissionResolverTests
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    private EdgeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EdgeDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        return new EdgeDbContext(options);
    }

    private PermissionResolver CreateResolver(EdgeDbContext db) =>
        new(db, NullLogger<PermissionResolver>.Instance);

    private async Task SeedProvider(string key = "test-provider")
    {
        using var db = CreateDbContext();
        db.OidcProviders.Add(new OidcProvider
        {
            ProviderKey = key,
            DisplayName = "Test",
            Issuer = $"https://{key}.example.com",
            DiscoveryUrl = $"https://{key}.example.com/.well-known/openid-configuration",
            Audience = "test-api"
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedRoleMapping(string providerKey, string externalRole, string vibePermission, string[]? deniedStatements = null)
    {
        using var db = CreateDbContext();
        db.OidcProviderRoleMappings.Add(new OidcProviderRoleMapping
        {
            ProviderKey = providerKey,
            ExternalRole = externalRole,
            VibePermission = vibePermission,
            DeniedStatements = deniedStatements
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedClientMapping(string providerKey, string clientId, string maxPermission, bool isActive = true)
    {
        using var db = CreateDbContext();
        db.OidcProviderClientMappings.Add(new OidcProviderClientMapping
        {
            ProviderKey = providerKey,
            VibeClientId = clientId,
            MaxPermission = maxPermission,
            IsActive = isActive
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ResolveAsync_NoTokenRoles_ReturnsNone()
    {
        await SeedProvider();
        using var db = CreateDbContext();
        var resolver = CreateResolver(db);

        var result = await resolver.ResolveAsync("test-provider", Array.Empty<string>());

        result.EffectiveLevel.Should().Be(VibePermissionLevel.None);
        result.DeniedStatements.Should().BeEmpty();
        result.MatchedRoles.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_NoMappedRoles_ReturnsNone()
    {
        await SeedProvider();
        using var db = CreateDbContext();
        var resolver = CreateResolver(db);

        var result = await resolver.ResolveAsync("test-provider", new[] { "unmapped-role" });

        result.EffectiveLevel.Should().Be(VibePermissionLevel.None);
    }

    [Fact]
    public async Task ResolveAsync_SingleRole_ReturnsItsPermission()
    {
        await SeedProvider();
        await SeedRoleMapping("test-provider", "developer", "write");
        using var db = CreateDbContext();
        var resolver = CreateResolver(db);

        var result = await resolver.ResolveAsync("test-provider", new[] { "developer" });

        result.EffectiveLevel.Should().Be(VibePermissionLevel.Write);
        result.MatchedRoles.Should().Contain("developer");
    }

    [Fact]
    public async Task ResolveAsync_MultipleRoles_HighestWins()
    {
        await SeedProvider();
        await SeedRoleMapping("test-provider", "viewer", "read");
        await SeedRoleMapping("test-provider", "editor", "write");
        await SeedRoleMapping("test-provider", "dba", "admin");
        using var db = CreateDbContext();
        var resolver = CreateResolver(db);

        var result = await resolver.ResolveAsync("test-provider", new[] { "viewer", "editor", "dba" });

        result.EffectiveLevel.Should().Be(VibePermissionLevel.Admin);
        result.MatchedRoles.Should().HaveCount(3);
    }

    [Fact]
    public async Task ResolveAsync_DeniedStatements_UnionAcrossRoles()
    {
        await SeedProvider();
        await SeedRoleMapping("test-provider", "developer", "write", new[] { "DELETE" });
        await SeedRoleMapping("test-provider", "auditor", "read", new[] { "TRUNCATE" });
        using var db = CreateDbContext();
        var resolver = CreateResolver(db);

        var result = await resolver.ResolveAsync("test-provider", new[] { "developer", "auditor" });

        result.EffectiveLevel.Should().Be(VibePermissionLevel.Write);
        result.DeniedStatements.Should().Contain("DELETE");
        result.DeniedStatements.Should().Contain("TRUNCATE");
    }

    [Fact]
    public async Task ResolveAsync_DeniedWildcard_ContainsStar()
    {
        await SeedProvider();
        await SeedRoleMapping("test-provider", "readonly", "write", new[] { "*" });
        using var db = CreateDbContext();
        var resolver = CreateResolver(db);

        var result = await resolver.ResolveAsync("test-provider", new[] { "readonly" });

        result.DeniedStatements.Should().Contain("*");
    }

    [Fact]
    public async Task ResolveAsync_AdminWithDeniedDrop_AdminButDropDenied()
    {
        await SeedProvider();
        await SeedRoleMapping("test-provider", "admin-no-drop", "admin", new[] { "DROP" });
        using var db = CreateDbContext();
        var resolver = CreateResolver(db);

        var result = await resolver.ResolveAsync("test-provider", new[] { "admin-no-drop" });

        result.EffectiveLevel.Should().Be(VibePermissionLevel.Admin);
        result.DeniedStatements.Should().Contain("DROP");
    }

    [Fact]
    public async Task ResolveAsync_ClientMaxPermissionCaps()
    {
        await SeedProvider();
        await SeedRoleMapping("test-provider", "admin-role", "admin");
        await SeedClientMapping("test-provider", "client-1", "write");
        using var db = CreateDbContext();
        var resolver = CreateResolver(db);

        var result = await resolver.ResolveAsync("test-provider", new[] { "admin-role" }, vibeClientId: "client-1");

        result.EffectiveLevel.Should().Be(VibePermissionLevel.Write);
    }

    [Fact]
    public async Task ResolveAsync_InactiveClientMapping_DoesNotCap()
    {
        await SeedProvider();
        await SeedRoleMapping("test-provider", "admin-role", "admin");
        await SeedClientMapping("test-provider", "client-1", "read", isActive: false);
        using var db = CreateDbContext();
        var resolver = CreateResolver(db);

        var result = await resolver.ResolveAsync("test-provider", new[] { "admin-role" });

        result.EffectiveLevel.Should().Be(VibePermissionLevel.Admin);
    }

    [Fact]
    public async Task ResolveAsync_NoDeniedStatements_EmptySet()
    {
        await SeedProvider();
        await SeedRoleMapping("test-provider", "writer", "write");
        using var db = CreateDbContext();
        var resolver = CreateResolver(db);

        var result = await resolver.ResolveAsync("test-provider", new[] { "writer" });

        result.DeniedStatements.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_MixOfMappedAndUnmapped_OnlyMappedCount()
    {
        await SeedProvider();
        await SeedRoleMapping("test-provider", "dev", "write");
        using var db = CreateDbContext();
        var resolver = CreateResolver(db);

        var result = await resolver.ResolveAsync("test-provider", new[] { "dev", "unknown-role", "another-unknown" });

        result.EffectiveLevel.Should().Be(VibePermissionLevel.Write);
        result.MatchedRoles.Should().HaveCount(1);
        result.MatchedRoles.Should().Contain("dev");
    }

    [Fact]
    public async Task ResolveAsync_NullClientId_WithClientMappings_FailsClosed()
    {
        await SeedProvider();
        await SeedRoleMapping("test-provider", "admin-role", "admin");
        await SeedClientMapping("test-provider", "client-1", "admin");
        using var db = CreateDbContext();
        var resolver = CreateResolver(db);

        var result = await resolver.ResolveAsync("test-provider", new[] { "admin-role" }, vibeClientId: null);

        result.EffectiveLevel.Should().Be(VibePermissionLevel.None);
    }

    [Fact]
    public async Task ResolveAsync_UnmatchedClientId_FailsClosed()
    {
        await SeedProvider();
        await SeedRoleMapping("test-provider", "admin-role", "admin");
        await SeedClientMapping("test-provider", "client-1", "admin");
        using var db = CreateDbContext();
        var resolver = CreateResolver(db);

        var result = await resolver.ResolveAsync("test-provider", new[] { "admin-role" }, vibeClientId: "wrong-client");

        result.EffectiveLevel.Should().Be(VibePermissionLevel.None);
    }

    [Fact]
    public async Task ResolveAsync_MatchedClientId_CapsCorrectly()
    {
        await SeedProvider();
        await SeedRoleMapping("test-provider", "admin-role", "admin");
        await SeedClientMapping("test-provider", "client-1", "write");
        using var db = CreateDbContext();
        var resolver = CreateResolver(db);

        var result = await resolver.ResolveAsync("test-provider", new[] { "admin-role" }, vibeClientId: "client-1");

        result.EffectiveLevel.Should().Be(VibePermissionLevel.Write);
    }

    [Fact]
    public async Task ResolveAsync_NoClientMappings_NoClientIdRequired()
    {
        await SeedProvider();
        await SeedRoleMapping("test-provider", "admin-role", "admin");
        using var db = CreateDbContext();
        var resolver = CreateResolver(db);

        var result = await resolver.ResolveAsync("test-provider", new[] { "admin-role" }, vibeClientId: null);

        result.EffectiveLevel.Should().Be(VibePermissionLevel.Admin);
    }
}
