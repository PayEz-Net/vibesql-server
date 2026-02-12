using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VibeSQL.Edge.Data;
using VibeSQL.Edge.Identity;

namespace VibeSQL.Edge.Tests;

public class UserProvisioningServiceTests
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    private EdgeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EdgeDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        return new EdgeDbContext(options);
    }

    [Fact]
    public async Task ProvisionAsync_CreatesIdentityRecord()
    {
        using var db = CreateDbContext();
        var service = new UserProvisioningService(db, NullLogger<UserProvisioningService>.Instance);

        var claims = new ExtractedClaims("ext-sub-1", new List<string> { "role-a" }, "user@example.com");
        var result = await service.ProvisionAsync("test-provider", claims);

        result.VibeUserId.Should().BeGreaterThan(0);
        result.FederatedIdentityId.Should().BeGreaterThan(0);

        var identity = await db.FederatedIdentities.FirstAsync();
        identity.ProviderKey.Should().Be("test-provider");
        identity.ExternalSubject.Should().Be("ext-sub-1");
        identity.VibeUserId.Should().Be(result.VibeUserId);
        identity.Email.Should().Be("user@example.com");
        identity.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ProvisionAsync_NullEmail_SetsNullDisplayName()
    {
        using var db = CreateDbContext();
        var service = new UserProvisioningService(db, NullLogger<UserProvisioningService>.Instance);

        var claims = new ExtractedClaims("ext-sub-2", new List<string>(), null);
        var result = await service.ProvisionAsync("test-provider", claims);

        var identity = await db.FederatedIdentities.FirstAsync();
        identity.Email.Should().BeNull();
        identity.DisplayName.Should().BeNull();
    }

    [Fact]
    public async Task ProvisionAsync_MultipleUsers_GetUniqueVibeUserIds()
    {
        using var db = CreateDbContext();
        var service = new UserProvisioningService(db, NullLogger<UserProvisioningService>.Instance);

        var result1 = await service.ProvisionAsync("p1", new ExtractedClaims("sub-1", new List<string>(), null));
        var result2 = await service.ProvisionAsync("p1", new ExtractedClaims("sub-2", new List<string>(), null));
        var result3 = await service.ProvisionAsync("p2", new ExtractedClaims("sub-3", new List<string>(), null));

        var ids = new[] { result1.VibeUserId, result2.VibeUserId, result3.VibeUserId };
        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ProvisionAsync_SetsTimestamps()
    {
        var before = DateTimeOffset.UtcNow;

        using var db = CreateDbContext();
        var service = new UserProvisioningService(db, NullLogger<UserProvisioningService>.Instance);

        await service.ProvisionAsync("test-provider", new ExtractedClaims("sub-1", new List<string>(), null));

        var after = DateTimeOffset.UtcNow;
        var identity = await db.FederatedIdentities.FirstAsync();
        identity.FirstSeenAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        identity.LastSeenAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}
