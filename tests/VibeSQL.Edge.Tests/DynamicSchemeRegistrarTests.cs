using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using VibeSQL.Edge.Authentication;
using VibeSQL.Edge.Data.Entities;

namespace VibeSQL.Edge.Tests;

public class DynamicSchemeRegistrarTests : IDisposable
{
    private readonly AuthenticationSchemeProvider _schemeProvider;
    private readonly Mock<IOptionsMonitorCache<JwtBearerOptions>> _optionsCacheMock;
    private readonly DynamicSchemeRegistrar _registrar;

    public DynamicSchemeRegistrarTests()
    {
        var authOptions = Options.Create(new AuthenticationOptions());
        _schemeProvider = new AuthenticationSchemeProvider(authOptions);
        _optionsCacheMock = new Mock<IOptionsMonitorCache<JwtBearerOptions>>();

        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Development");

        _registrar = new DynamicSchemeRegistrar(
            _schemeProvider,
            _optionsCacheMock.Object,
            NullLogger<DynamicSchemeRegistrar>.Instance,
            env.Object);
    }

    [Fact]
    public async Task RegisterProviderAsync_AddsScheme()
    {
        var provider = CreateTestProvider("test-idp", "https://test.example.com");

        await _registrar.RegisterProviderAsync(provider);

        var scheme = await _schemeProvider.GetSchemeAsync("oidc-test-idp");
        scheme.Should().NotBeNull();
        scheme!.HandlerType.Should().Be(typeof(JwtBearerHandler));
    }

    [Fact]
    public async Task RegisterProviderAsync_SetsDisplayNameOnScheme()
    {
        var provider = CreateTestProvider("test-idp", "https://test.example.com");
        provider.DisplayName = "My Test IDP";

        await _registrar.RegisterProviderAsync(provider);

        var scheme = await _schemeProvider.GetSchemeAsync("oidc-test-idp");
        scheme!.DisplayName.Should().Be("My Test IDP");
    }

    [Fact]
    public async Task RegisterProviderAsync_DoubleRegister_UpdatesWithoutError()
    {
        var provider = CreateTestProvider("test-idp", "https://test.example.com");

        await _registrar.RegisterProviderAsync(provider);
        await _registrar.RegisterProviderAsync(provider);

        var scheme = await _schemeProvider.GetSchemeAsync("oidc-test-idp");
        scheme.Should().NotBeNull();
        _registrar.IsRegistered("oidc-test-idp").Should().BeTrue();
    }

    [Fact]
    public async Task UnregisterProviderAsync_RemovesScheme()
    {
        var provider = CreateTestProvider("test-idp", "https://test.example.com");
        await _registrar.RegisterProviderAsync(provider);

        await _registrar.UnregisterProviderAsync("oidc-test-idp");

        var scheme = await _schemeProvider.GetSchemeAsync("oidc-test-idp");
        scheme.Should().BeNull();
        _registrar.IsRegistered("oidc-test-idp").Should().BeFalse();
    }

    [Fact]
    public async Task UnregisterProviderAsync_NonExistentScheme_DoesNotThrow()
    {
        var act = () => _registrar.UnregisterProviderAsync("oidc-nonexistent");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task IsRegistered_ReturnsTrueAfterRegister()
    {
        var provider = CreateTestProvider("test-idp", "https://test.example.com");
        await _registrar.RegisterProviderAsync(provider);

        _registrar.IsRegistered("oidc-test-idp").Should().BeTrue();
    }

    [Fact]
    public void IsRegistered_ReturnsFalseForUnknown()
    {
        _registrar.IsRegistered("oidc-unknown").Should().BeFalse();
    }

    [Fact]
    public async Task RegisterMultipleProviders_AllAccessible()
    {
        await _registrar.RegisterProviderAsync(CreateTestProvider("idp-a", "https://a.example.com"));
        await _registrar.RegisterProviderAsync(CreateTestProvider("idp-b", "https://b.example.com"));
        await _registrar.RegisterProviderAsync(CreateTestProvider("idp-c", "https://c.example.com"));

        (await _schemeProvider.GetSchemeAsync("oidc-idp-a")).Should().NotBeNull();
        (await _schemeProvider.GetSchemeAsync("oidc-idp-b")).Should().NotBeNull();
        (await _schemeProvider.GetSchemeAsync("oidc-idp-c")).Should().NotBeNull();
    }

    [Fact]
    public void ToSchemeId_ProducesConsistentFormat()
    {
        DynamicSchemeRegistrar.ToSchemeId("payez-idp").Should().Be("oidc-payez-idp");
        DynamicSchemeRegistrar.ToSchemeId("azure-ad").Should().Be("oidc-azure-ad");
    }

    public void Dispose()
    {
        _registrar.Dispose();
    }

    private static OidcProvider CreateTestProvider(string key, string issuer) => new()
    {
        ProviderKey = key,
        DisplayName = $"Test {key}",
        Issuer = issuer,
        DiscoveryUrl = $"{issuer}/.well-known/openid-configuration",
        Audience = "test-api",
        IsActive = true,
        ClockSkewSeconds = 60
    };
}
