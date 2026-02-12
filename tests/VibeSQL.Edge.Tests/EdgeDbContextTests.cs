using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VibeSQL.Edge.Data;
using VibeSQL.Edge.Data.Entities;

namespace VibeSQL.Edge.Tests;

public class EdgeDbContextTests : IDisposable
{
    private readonly EdgeDbContext _context;

    public EdgeDbContextTests()
    {
        var options = new DbContextOptionsBuilder<EdgeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new EdgeDbContext(options);
    }

    [Fact]
    public async Task CanInsertAndRetrieveProvider()
    {
        var provider = new OidcProvider
        {
            ProviderKey = "test-idp",
            DisplayName = "Test IDP",
            Issuer = "https://test.idp.example.com",
            DiscoveryUrl = "https://test.idp.example.com/.well-known/openid-configuration",
            Audience = "test-api"
        };

        _context.OidcProviders.Add(provider);
        await _context.SaveChangesAsync();

        var retrieved = await _context.OidcProviders.FindAsync("test-idp");
        retrieved.Should().NotBeNull();
        retrieved!.Issuer.Should().Be("https://test.idp.example.com");
    }

    [Fact]
    public async Task CanInsertRoleMappingWithProvider()
    {
        var provider = new OidcProvider
        {
            ProviderKey = "test-idp",
            DisplayName = "Test",
            Issuer = "https://test.example.com",
            DiscoveryUrl = "https://test.example.com/.well-known/openid-configuration",
            Audience = "api"
        };

        var roleMapping = new OidcProviderRoleMapping
        {
            ProviderKey = "test-idp",
            ExternalRole = "admin",
            VibePermission = "admin",
            DeniedStatements = new[] { "DROP" }
        };

        _context.OidcProviders.Add(provider);
        _context.OidcProviderRoleMappings.Add(roleMapping);
        await _context.SaveChangesAsync();

        var mappings = await _context.OidcProviderRoleMappings
            .Where(m => m.ProviderKey == "test-idp")
            .ToListAsync();

        mappings.Should().HaveCount(1);
        mappings[0].ExternalRole.Should().Be("admin");
        mappings[0].DeniedStatements.Should().Contain("DROP");
    }

    [Fact]
    public async Task CanInsertFederatedIdentity()
    {
        var provider = new OidcProvider
        {
            ProviderKey = "azure-ad",
            DisplayName = "Azure AD",
            Issuer = "https://login.microsoftonline.com/tenant",
            DiscoveryUrl = "https://login.microsoftonline.com/tenant/.well-known/openid-configuration",
            Audience = "api"
        };

        var identity = new FederatedIdentity
        {
            ProviderKey = "azure-ad",
            ExternalSubject = "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
            VibeUserId = 42,
            Email = "user@contoso.com"
        };

        _context.OidcProviders.Add(provider);
        _context.FederatedIdentities.Add(identity);
        await _context.SaveChangesAsync();

        var found = await _context.FederatedIdentities
            .FirstOrDefaultAsync(f => f.ProviderKey == "azure-ad" && f.ExternalSubject == "a1b2c3d4-e5f6-7890-abcd-ef1234567890");

        found.Should().NotBeNull();
        found!.VibeUserId.Should().Be(42);
    }

    [Fact]
    public async Task CanInsertClientMapping()
    {
        var provider = new OidcProvider
        {
            ProviderKey = "okta",
            DisplayName = "Okta",
            Issuer = "https://dev-123.okta.com",
            DiscoveryUrl = "https://dev-123.okta.com/.well-known/openid-configuration",
            Audience = "api"
        };

        var clientMapping = new OidcProviderClientMapping
        {
            ProviderKey = "okta",
            VibeClientId = "vibe_client_abc",
            MaxPermission = "write"
        };

        _context.OidcProviders.Add(provider);
        _context.OidcProviderClientMappings.Add(clientMapping);
        await _context.SaveChangesAsync();

        var found = await _context.OidcProviderClientMappings
            .FirstOrDefaultAsync(c => c.ProviderKey == "okta");

        found.Should().NotBeNull();
        found!.GetMaxPermissionLevel().Should().Be(VibePermissionLevel.Write);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
