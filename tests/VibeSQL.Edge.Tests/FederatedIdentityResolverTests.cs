using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VibeSQL.Edge.Data;
using VibeSQL.Edge.Data.Entities;
using VibeSQL.Edge.Identity;

namespace VibeSQL.Edge.Tests;

public class FederatedIdentityResolverTests : IDisposable
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly Mock<IUserProvisioningService> _provisionerMock = new();

    private EdgeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EdgeDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        return new EdgeDbContext(options);
    }

    private FederatedIdentityResolver CreateResolver(EdgeDbContext db) =>
        new(db, _provisionerMock.Object, NullLogger<FederatedIdentityResolver>.Instance);

    private static OidcProvider CreateProvider(bool autoProvision = false) => new()
    {
        ProviderKey = "test-provider",
        DisplayName = "Test",
        Issuer = "https://test.example.com",
        DiscoveryUrl = "https://test.example.com/.well-known/openid-configuration",
        Audience = "test-api",
        AutoProvision = autoProvision
    };

    private static ExtractedClaims CreateClaims(
        string subject = "ext-user-1",
        string? email = "user@example.com") =>
        new(subject, new List<string> { "role-a" }, email);

    [Fact]
    public async Task ResolveAsync_ExistingActiveIdentity_ReturnsVibeUserId()
    {
        using var db = CreateDbContext();
        db.FederatedIdentities.Add(new FederatedIdentity
        {
            ProviderKey = "test-provider",
            ExternalSubject = "ext-user-1",
            VibeUserId = 42,
            IsActive = true,
            Email = "old@example.com"
        });
        await db.SaveChangesAsync();

        var resolver = CreateResolver(db);
        var result = await resolver.ResolveAsync("test-provider", CreateClaims(), CreateProvider());

        result.Should().NotBeNull();
        result!.VibeUserId.Should().Be(42);
        result.ProviderKey.Should().Be("test-provider");
        result.ExternalSubject.Should().Be("ext-user-1");
        result.WasProvisioned.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveAsync_ExistingActiveIdentity_UpdatesLastSeenAndEmail()
    {
        using var db = CreateDbContext();
        var before = DateTimeOffset.UtcNow.AddDays(-1);
        db.FederatedIdentities.Add(new FederatedIdentity
        {
            ProviderKey = "test-provider",
            ExternalSubject = "ext-user-1",
            VibeUserId = 42,
            IsActive = true,
            Email = "old@example.com",
            LastSeenAt = before
        });
        await db.SaveChangesAsync();

        var resolver = CreateResolver(db);
        await resolver.ResolveAsync("test-provider", CreateClaims(email: "new@example.com"), CreateProvider());

        var updated = await db.FederatedIdentities.FirstAsync();
        updated.LastSeenAt.Should().BeAfter(before);
        updated.Email.Should().Be("new@example.com");
    }

    [Fact]
    public async Task ResolveAsync_InactiveIdentity_ReturnsNull()
    {
        using var db = CreateDbContext();
        db.FederatedIdentities.Add(new FederatedIdentity
        {
            ProviderKey = "test-provider",
            ExternalSubject = "ext-user-1",
            VibeUserId = 42,
            IsActive = false
        });
        await db.SaveChangesAsync();

        var resolver = CreateResolver(db);
        var result = await resolver.ResolveAsync("test-provider", CreateClaims(), CreateProvider());

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_UnknownIdentity_AutoProvisionFalse_ReturnsNull()
    {
        using var db = CreateDbContext();
        var resolver = CreateResolver(db);

        var result = await resolver.ResolveAsync("test-provider", CreateClaims(), CreateProvider(autoProvision: false));

        result.Should().BeNull();
        _provisionerMock.Verify(p => p.ProvisionAsync(
            It.IsAny<string>(), It.IsAny<ExtractedClaims>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_UnknownIdentity_AutoProvisionTrue_CallsProvisioner()
    {
        _provisionerMock
            .Setup(p => p.ProvisionAsync("test-provider", It.IsAny<ExtractedClaims>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUser(10001, 1));

        using var db = CreateDbContext();
        var resolver = CreateResolver(db);

        var result = await resolver.ResolveAsync("test-provider", CreateClaims(), CreateProvider(autoProvision: true));

        result.Should().NotBeNull();
        result!.VibeUserId.Should().Be(10001);
        result.WasProvisioned.Should().BeTrue();
        _provisionerMock.Verify(p => p.ProvisionAsync(
            "test-provider", It.IsAny<ExtractedClaims>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_DifferentProvider_DoesNotMatch()
    {
        using var db = CreateDbContext();
        db.FederatedIdentities.Add(new FederatedIdentity
        {
            ProviderKey = "other-provider",
            ExternalSubject = "ext-user-1",
            VibeUserId = 99,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var resolver = CreateResolver(db);
        var result = await resolver.ResolveAsync("test-provider", CreateClaims(), CreateProvider(autoProvision: false));

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_DifferentSubject_DoesNotMatch()
    {
        using var db = CreateDbContext();
        db.FederatedIdentities.Add(new FederatedIdentity
        {
            ProviderKey = "test-provider",
            ExternalSubject = "different-user",
            VibeUserId = 99,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var resolver = CreateResolver(db);
        var result = await resolver.ResolveAsync("test-provider", CreateClaims(subject: "ext-user-1"), CreateProvider(autoProvision: false));

        result.Should().BeNull();
    }

    public void Dispose()
    {
    }
}
