using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using VibeSQL.Edge.Authentication;
using VibeSQL.Edge.Configuration;
using VibeSQL.Edge.Data;
using VibeSQL.Edge.Data.Entities;

namespace VibeSQL.Edge.Tests;

public class EdgeAuthBackgroundServiceTests
{
    private readonly ProviderRegistry _registry;
    private readonly Mock<IDynamicSchemeRegistrar> _registrarMock;
    private readonly ServiceProvider _serviceProvider;

    public EdgeAuthBackgroundServiceTests()
    {
        _registry = new ProviderRegistry();
        _registrarMock = new Mock<IDynamicSchemeRegistrar>();
        _registrarMock.Setup(r => r.RegisterProviderAsync(It.IsAny<OidcProvider>())).Returns(Task.CompletedTask);
        _registrarMock.Setup(r => r.UnregisterProviderAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddDbContext<EdgeDbContext>(options =>
            options.UseInMemoryDatabase(_dbName));

        _serviceProvider = services.BuildServiceProvider();
    }

    private EdgeAuthBackgroundService CreateService()
    {
        var options = Options.Create(new VibeEdgeOptions { RefreshIntervalMinutes = 30 });
        return new EdgeAuthBackgroundService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _registry,
            _registrarMock.Object,
            options,
            NullLogger<EdgeAuthBackgroundService>.Instance);
    }

    private readonly string _dbName = Guid.NewGuid().ToString();

    private EdgeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EdgeDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        return new EdgeDbContext(options);
    }

    private async Task SeedProvider(string key, string issuer, bool isActive = true, bool isBootstrap = false)
    {
        using var db = CreateDbContext();
        db.OidcProviders.Add(new OidcProvider
        {
            ProviderKey = key,
            DisplayName = $"Test {key}",
            Issuer = issuer,
            DiscoveryUrl = $"{issuer}/.well-known/openid-configuration",
            Audience = "test-api",
            IsActive = isActive,
            IsBootstrap = isBootstrap
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task LoadProvidersAsync_RegistersActiveProviders()
    {
        await SeedProvider("payez-idp", "https://idp.payez.net", isActive: true, isBootstrap: true);
        var service = CreateService();

        await service.LoadProvidersAsync();

        _registrarMock.Verify(r => r.RegisterProviderAsync(It.Is<OidcProvider>(p =>
            p.ProviderKey == "payez-idp")), Times.Once);
    }

    [Fact]
    public async Task LoadProvidersAsync_SkipsInactiveProviders()
    {
        await SeedProvider("disabled-idp", "https://disabled.example.com", isActive: false);
        var service = CreateService();

        await service.LoadProvidersAsync();

        _registrarMock.Verify(r => r.RegisterProviderAsync(It.Is<OidcProvider>(p =>
            p.ProviderKey == "disabled-idp")), Times.Never);
    }

    [Fact]
    public async Task LoadProvidersAsync_UnregistersDisabledProviderIfPreviouslyRegistered()
    {
        _registrarMock.Setup(r => r.IsRegistered("oidc-disabled-idp")).Returns(true);
        await SeedProvider("disabled-idp", "https://disabled.example.com", isActive: false);
        var service = CreateService();

        await service.LoadProvidersAsync();

        _registrarMock.Verify(r => r.UnregisterProviderAsync("oidc-disabled-idp"), Times.Once);
    }

    [Fact]
    public async Task LoadProvidersAsync_UpdatesRegistryWithAllProviders()
    {
        await SeedProvider("idp-a", "https://a.example.com", isActive: true);
        await SeedProvider("idp-b", "https://b.example.com", isActive: false);
        var service = CreateService();

        await service.LoadProvidersAsync();

        _registry.GetAll().Should().HaveCount(2);
        _registry.GetByIssuer("https://a.example.com").Should().NotBeNull();
        _registry.GetByIssuer("https://a.example.com")!.IsActive.Should().BeTrue();
        _registry.GetByIssuer("https://b.example.com").Should().NotBeNull();
        _registry.GetByIssuer("https://b.example.com")!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task LoadProvidersAsync_RemovesOrphanSchemes()
    {
        _registry.Replace(new List<ProviderRecord>
        {
            new()
            {
                ProviderKey = "orphan",
                Issuer = "https://orphan.example.com",
                SchemeId = "oidc-orphan",
                IsActive = true,
                IsBootstrap = false
            }
        });

        var service = CreateService();
        await service.LoadProvidersAsync();

        _registrarMock.Verify(r => r.UnregisterProviderAsync("oidc-orphan"), Times.Once);
    }

    [Fact]
    public async Task LoadProvidersAsync_MultipleActiveProviders_RegistersAll()
    {
        await SeedProvider("payez-idp", "https://idp.payez.net", isActive: true, isBootstrap: true);
        await SeedProvider("contoso-ad", "https://login.microsoftonline.com/contoso", isActive: true);
        await SeedProvider("okta-dev", "https://dev-123.okta.com", isActive: true);
        var service = CreateService();

        await service.LoadProvidersAsync();

        _registrarMock.Verify(r => r.RegisterProviderAsync(It.IsAny<OidcProvider>()), Times.Exactly(3));
        _registry.GetAll().Should().HaveCount(3);
    }

    [Fact]
    public async Task LoadProvidersAsync_DbFailure_KeepsExistingRegistry()
    {
        _registry.Replace(new List<ProviderRecord>
        {
            new()
            {
                ProviderKey = "existing",
                Issuer = "https://existing.example.com",
                SchemeId = "oidc-existing",
                IsActive = true,
                IsBootstrap = false
            }
        });

        var brokenServices = new ServiceCollection();
        brokenServices.AddScoped<EdgeDbContext>(_ => throw new InvalidOperationException("DB down"));
        var brokenSp = brokenServices.BuildServiceProvider();

        var options = Options.Create(new VibeEdgeOptions { RefreshIntervalMinutes = 30 });
        var service = new EdgeAuthBackgroundService(
            brokenSp.GetRequiredService<IServiceScopeFactory>(),
            _registry,
            _registrarMock.Object,
            options,
            NullLogger<EdgeAuthBackgroundService>.Instance);

        await service.LoadProvidersAsync();

        _registry.GetAll().Should().HaveCount(1);
        _registry.GetByIssuer("https://existing.example.com").Should().NotBeNull();
    }
}
