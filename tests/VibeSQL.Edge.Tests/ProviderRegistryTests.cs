using FluentAssertions;
using VibeSQL.Edge.Authentication;

namespace VibeSQL.Edge.Tests;

public class ProviderRegistryTests
{
    [Fact]
    public void GetByIssuer_EmptyRegistry_ReturnsNull()
    {
        var registry = new ProviderRegistry();
        registry.GetByIssuer("https://idp.payez.net").Should().BeNull();
    }

    [Fact]
    public void GetByIssuer_AfterReplace_FindsProvider()
    {
        var registry = new ProviderRegistry();
        registry.Replace(new List<ProviderRecord>
        {
            new()
            {
                ProviderKey = "payez-idp",
                Issuer = "https://idp.payez.net",
                SchemeId = "oidc-payez-idp",
                IsActive = true,
                IsBootstrap = true
            }
        });

        var found = registry.GetByIssuer("https://idp.payez.net");
        found.Should().NotBeNull();
        found!.ProviderKey.Should().Be("payez-idp");
    }

    [Fact]
    public void GetByIssuer_IsCaseInsensitive()
    {
        var registry = new ProviderRegistry();
        registry.Replace(new List<ProviderRecord>
        {
            new()
            {
                ProviderKey = "test",
                Issuer = "https://IDP.PayEz.Net",
                SchemeId = "oidc-test",
                IsActive = true,
                IsBootstrap = false
            }
        });

        registry.GetByIssuer("https://idp.payez.net").Should().NotBeNull();
    }

    [Fact]
    public void Replace_OverwritesPreviousProviders()
    {
        var registry = new ProviderRegistry();

        registry.Replace(new List<ProviderRecord>
        {
            new()
            {
                ProviderKey = "old",
                Issuer = "https://old.example.com",
                SchemeId = "oidc-old",
                IsActive = true,
                IsBootstrap = false
            }
        });

        registry.Replace(new List<ProviderRecord>
        {
            new()
            {
                ProviderKey = "new",
                Issuer = "https://new.example.com",
                SchemeId = "oidc-new",
                IsActive = true,
                IsBootstrap = false
            }
        });

        registry.GetByIssuer("https://old.example.com").Should().BeNull();
        registry.GetByIssuer("https://new.example.com").Should().NotBeNull();
        registry.GetAll().Should().HaveCount(1);
    }

    [Fact]
    public void GetAll_ReturnsAllProviders()
    {
        var registry = new ProviderRegistry();
        registry.Replace(new List<ProviderRecord>
        {
            new() { ProviderKey = "a", Issuer = "https://a.com", SchemeId = "oidc-a", IsActive = true, IsBootstrap = false },
            new() { ProviderKey = "b", Issuer = "https://b.com", SchemeId = "oidc-b", IsActive = true, IsBootstrap = false },
            new() { ProviderKey = "c", Issuer = "https://c.com", SchemeId = "oidc-c", IsActive = false, IsBootstrap = false }
        });

        registry.GetAll().Should().HaveCount(3);
    }

    [Fact]
    public void GetByKey_ReturnsProviderByKey()
    {
        var registry = new ProviderRegistry();
        registry.Replace(new List<ProviderRecord>
        {
            new() { ProviderKey = "payez-idp", Issuer = "https://idp.payez.net", SchemeId = "oidc-payez-idp", IsActive = true, IsBootstrap = true }
        });

        var found = registry.GetByKey("payez-idp");
        found.Should().NotBeNull();
        found!.Issuer.Should().Be("https://idp.payez.net");
    }

    [Fact]
    public void GetByKey_UnknownKey_ReturnsNull()
    {
        var registry = new ProviderRegistry();
        registry.GetByKey("nonexistent").Should().BeNull();
    }

    [Fact]
    public void ToSchemeId_FormatsCorrectly()
    {
        DynamicSchemeRegistrar.ToSchemeId("payez-idp").Should().Be("oidc-payez-idp");
        DynamicSchemeRegistrar.ToSchemeId("contoso-azure-ad").Should().Be("oidc-contoso-azure-ad");
    }
}
