using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VibeSQL.Edge.Authentication;
using VibeSQL.Edge.Data;
using VibeSQL.Edge.Data.Entities;
using VibeSQL.Edge.Identity;

namespace VibeSQL.Edge.Tests.Integration;

public class EdgeIntegrationTests : IClassFixture<EdgeWebApplicationFactory>, IAsyncLifetime
{
    private readonly EdgeWebApplicationFactory _factory;

    public EdgeIntegrationTests(EdgeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.SeedProviderAsync();
        UserProvisioningService.ResetForTesting();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task UnknownIssuer_Returns401()
    {
        var client = _factory.CreateClient();
        var token = TestJwtGenerator.GenerateToken("https://unknown-idp.com", "unknown-aud", "user-1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync("/v1/query",
            new StringContent("{\"sql\":\"SELECT 1\"}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NoToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/v1/query",
            new StringContent("{\"sql\":\"SELECT 1\"}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ValidToken_NoRoleMappings_Returns403()
    {
        await _factory.SeedFederatedIdentityAsync("user-no-roles", 5001);
        var client = _factory.CreateAuthenticatedClient("user-no-roles", roles: new[] { "unmapped-role" });

        var response = await client.PostAsync("/v1/query",
            new StringContent("{\"sql\":\"SELECT 1\"}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SelectWithReadPermission_Allowed()
    {
        await _factory.SeedFederatedIdentityAsync("reader-user", 5002);
        await _factory.SeedRoleMappingAsync("reader", "read");
        var client = _factory.CreateAuthenticatedClient("reader-user", roles: new[] { "reader" });

        var response = await client.PostAsync("/v1/query",
            new StringContent("{\"sql\":\"SELECT * FROM users\"}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InsertWithReadPermission_Returns403()
    {
        await _factory.SeedFederatedIdentityAsync("reader-insert", 5003);
        await _factory.SeedRoleMappingAsync("read-only", "read");
        var client = _factory.CreateAuthenticatedClient("reader-insert", roles: new[] { "read-only" });

        var response = await client.PostAsync("/v1/query",
            new StringContent("{\"sql\":\"INSERT INTO users VALUES (1)\"}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateWithWritePermission_Returns403()
    {
        await _factory.SeedFederatedIdentityAsync("writer-schema", 5004);
        await _factory.SeedRoleMappingAsync("writer", "write");
        var client = _factory.CreateAuthenticatedClient("writer-schema", roles: new[] { "writer" });

        var response = await client.PostAsync("/v1/query",
            new StringContent("{\"sql\":\"CREATE TABLE test (id int)\"}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeniedStatements_BlocksSpecificStatement()
    {
        await _factory.SeedFederatedIdentityAsync("writer-no-delete", 5005);
        await _factory.SeedRoleMappingAsync("writer-no-del", "write", deniedStatements: new[] { "DELETE" });
        var client = _factory.CreateAuthenticatedClient("writer-no-delete", roles: new[] { "writer-no-del" });

        var selectResponse = await client.PostAsync("/v1/query",
            new StringContent("{\"sql\":\"SELECT 1\"}", Encoding.UTF8, "application/json"));
        selectResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleteResponse = await client.PostAsync("/v1/query",
            new StringContent("{\"sql\":\"DELETE FROM users WHERE id=1\"}", Encoding.UTF8, "application/json"));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ClientMaxPermission_CapsRolePermission()
    {
        await _factory.SeedFederatedIdentityAsync("capped-user", 5006);
        await _factory.SeedRoleMappingAsync("admin-role", "admin");
        await _factory.SeedClientMappingAsync(EdgeWebApplicationFactory.TestAudience, "write");
        var client = _factory.CreateAuthenticatedClient("capped-user", roles: new[] { "admin-role" });

        var writeResponse = await client.PostAsync("/v1/query",
            new StringContent("{\"sql\":\"INSERT INTO t VALUES (1)\"}", Encoding.UTF8, "application/json"));
        writeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var schemaResponse = await client.PostAsync("/v1/query",
            new StringContent("{\"sql\":\"CREATE TABLE test2 (id int)\"}", Encoding.UTF8, "application/json"));
        schemaResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AutoProvision_CreatesNewFederatedIdentity()
    {
        await _factory.SeedRoleMappingAsync("auto-role", "read");
        var client = _factory.CreateAuthenticatedClient("brand-new-user", roles: new[] { "auto-role" }, email: "new@test.com");

        var response = await client.PostAsync("/v1/query",
            new StringContent("{\"sql\":\"SELECT 1\"}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EdgeDbContext>();
        var identity = await db.FederatedIdentities
            .FirstOrDefaultAsync(f => f.ExternalSubject == "brand-new-user" && f.ProviderKey == EdgeWebApplicationFactory.TestProviderKey);
        identity.Should().NotBeNull();
        identity!.VibeUserId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task HmacProxy_ForwardsToServer_WithCorrectHeaders()
    {
        _factory.MockServer.CallCount = 0;
        await _factory.SeedFederatedIdentityAsync("proxy-user", 5007);
        await _factory.SeedRoleMappingAsync("proxy-role", "read");
        var client = _factory.CreateAuthenticatedClient("proxy-user", roles: new[] { "proxy-role" });

        var response = await client.PostAsync("/v1/query",
            new StringContent("{\"sql\":\"SELECT 1\"}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.MockServer.CallCount.Should().BeGreaterThan(0);

        var lastReq = _factory.MockServer.LastRequest;
        lastReq.Should().NotBeNull();
        lastReq!.Headers.Contains("X-Vibe-Timestamp").Should().BeTrue();
        lastReq.Headers.Contains("X-Vibe-Signature").Should().BeTrue();
        lastReq.Headers.Contains("X-Vibe-Service").Should().BeTrue();
        lastReq.Headers.GetValues("X-Vibe-Service").First().Should().Be("vibesql-edge");
    }

    [Fact]
    public async Task HealthProviders_ReturnsStatus()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/providers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("providers");
    }

    [Fact]
    public async Task MultiStatementSql_Returns400()
    {
        await _factory.SeedFederatedIdentityAsync("multi-stmt", 5008);
        await _factory.SeedRoleMappingAsync("admin-ms", "admin");
        var client = _factory.CreateAuthenticatedClient("multi-stmt", roles: new[] { "admin-ms" });

        var response = await client.PostAsync("/v1/query",
            new StringContent("{\"sql\":\"SELECT 1; DROP TABLE users\"}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeactivatedIdentity_Returns403()
    {
        await _factory.SeedFederatedIdentityAsync("deactivated-user", 5009, isActive: false);
        await _factory.SeedRoleMappingAsync("deact-role", "admin");
        var client = _factory.CreateAuthenticatedClient("deactivated-user", roles: new[] { "deact-role" });

        var response = await client.PostAsync("/v1/query",
            new StringContent("{\"sql\":\"SELECT 1\"}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
