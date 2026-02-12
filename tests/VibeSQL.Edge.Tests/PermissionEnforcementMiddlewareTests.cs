using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VibeSQL.Edge;
using VibeSQL.Edge.Authorization;
using VibeSQL.Edge.Data;
using VibeSQL.Edge.Data.Entities;

namespace VibeSQL.Edge.Tests;

public class PermissionEnforcementMiddlewareTests
{
    private static PermissionEnforcementMiddleware CreateMiddleware(Action? onNext = null)
    {
        RequestDelegate next = ctx =>
        {
            onNext?.Invoke();
            return Task.CompletedTask;
        };
        return new PermissionEnforcementMiddleware(next, NullLogger<PermissionEnforcementMiddleware>.Instance);
    }

    private static HttpContext CreateContext(
        string method,
        string path,
        VibePermissionLevel effectiveLevel,
        HashSet<string>? deniedStatements = null,
        string? sqlBody = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        var identity = new ClaimsIdentity(new[] { new Claim("sub", "user-1") }, "Bearer");
        context.User = new ClaimsPrincipal(identity);

        context.Items[EdgeContextKeys.ProviderKey] = "test-provider";
        context.Items[EdgeContextKeys.ExtractedRoles] = (IReadOnlyList<string>)new List<string> { "role-a" };

        var resolverMock = new Mock<IPermissionResolver>();
        resolverMock
            .Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResolvedPermission(
                effectiveLevel,
                deniedStatements ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new[] { "role-a" }));

        var services = new ServiceCollection();
        services.AddSingleton(resolverMock.Object);
        context.RequestServices = services.BuildServiceProvider();

        if (sqlBody is not null)
        {
            var json = JsonSerializer.Serialize(new { sql = sqlBody });
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            context.Request.Body = stream;
            context.Request.ContentType = "application/json";
            context.Request.ContentLength = stream.Length;
        }

        return context;
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
    public async Task InvokeAsync_NoProviderKey_PassesThrough()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(onNext: () => nextCalled = true);
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "u") }, "Bearer"));

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_QueryWithSufficientPermission_PassesThrough()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(onNext: () => nextCalled = true);
        var context = CreateContext("POST", "/v1/query", VibePermissionLevel.Write, sqlBody: "INSERT INTO users VALUES (1)");

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_QueryWithInsufficientPermission_Returns403()
    {
        var middleware = CreateMiddleware();
        var context = CreateContext("POST", "/v1/query", VibePermissionLevel.Read, sqlBody: "INSERT INTO users VALUES (1)");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task InvokeAsync_SelectWithReadPermission_PassesThrough()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(onNext: () => nextCalled = true);
        var context = CreateContext("POST", "/v1/query", VibePermissionLevel.Read, sqlBody: "SELECT * FROM users");

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_SchemaOperationWithWritePermission_Returns403()
    {
        var middleware = CreateMiddleware();
        var context = CreateContext("POST", "/v1/query", VibePermissionLevel.Write, sqlBody: "CREATE TABLE users (id int)");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task InvokeAsync_AdminOperationWithSchemaPermission_Returns403()
    {
        var middleware = CreateMiddleware();
        var context = CreateContext("POST", "/v1/query", VibePermissionLevel.Schema, sqlBody: "TRUNCATE users");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task InvokeAsync_DeniedStatement_Returns403()
    {
        var denied = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "DELETE" };
        var middleware = CreateMiddleware();
        var context = CreateContext("POST", "/v1/query", VibePermissionLevel.Write, denied, "DELETE FROM users WHERE id=1");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task InvokeAsync_DeniedWildcard_DeniesEverything()
    {
        var denied = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "*" };
        var middleware = CreateMiddleware();
        var context = CreateContext("POST", "/v1/query", VibePermissionLevel.Admin, denied, "SELECT 1");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task InvokeAsync_NotDeniedStatement_PassesThrough()
    {
        var denied = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "DELETE" };
        var nextCalled = false;
        var middleware = CreateMiddleware(onNext: () => nextCalled = true);
        var context = CreateContext("POST", "/v1/query", VibePermissionLevel.Write, denied, "INSERT INTO users VALUES (1)");

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AdminEndpoint_RequiresAdmin()
    {
        var middleware = CreateMiddleware();
        var context = CreateContext("GET", "/v1/admin/providers", VibePermissionLevel.Write);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task InvokeAsync_AdminEndpoint_WithAdminPermission_PassesThrough()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(onNext: () => nextCalled = true);
        var context = CreateContext("GET", "/v1/admin/providers", VibePermissionLevel.Admin);

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_MultiStatement_Returns400()
    {
        var middleware = CreateMiddleware();
        var context = CreateContext("POST", "/v1/query", VibePermissionLevel.Admin, sqlBody: "SELECT 1; DROP TABLE users");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task InvokeAsync_NonV1Path_PassesThrough()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(onNext: () => nextCalled = true);
        var context = CreateContext("GET", "/health", VibePermissionLevel.None);

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_CteInsert_RequiresWrite()
    {
        var middleware = CreateMiddleware();
        var context = CreateContext("POST", "/v1/query", VibePermissionLevel.Read,
            sqlBody: "WITH cte AS (SELECT * FROM staging) INSERT INTO users SELECT * FROM cte");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task InvokeAsync_DeniedInsert_ViaCteDenial()
    {
        var denied = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "INSERT" };
        var middleware = CreateMiddleware();
        var context = CreateContext("POST", "/v1/query", VibePermissionLevel.Write, denied,
            "WITH cte AS (SELECT 1) INSERT INTO users SELECT * FROM cte");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public void IsStatementDenied_WithPrefix_MatchesBase()
    {
        var denied = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "DELETE" };
        PermissionEnforcementMiddleware.IsStatementDenied("WITH...DELETE", denied).Should().BeTrue();
        PermissionEnforcementMiddleware.IsStatementDenied("EXPLAIN DELETE", denied).Should().BeTrue();
    }

    [Fact]
    public void IsStatementDenied_NoMatch_ReturnsFalse()
    {
        var denied = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "DELETE" };
        PermissionEnforcementMiddleware.IsStatementDenied("SELECT", denied).Should().BeFalse();
        PermissionEnforcementMiddleware.IsStatementDenied("INSERT", denied).Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_OversizedBody_Returns400()
    {
        var middleware = CreateMiddleware();
        var context = CreateContext("POST", "/v1/query", VibePermissionLevel.Admin);
        var largeBody = new string('x', 65 * 1024);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(largeBody));
        context.Request.Body = stream;
        context.Request.ContentLength = stream.Length;
        context.Request.ContentType = "application/json";

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task InvokeAsync_MalformedJsonBody_Returns400()
    {
        var middleware = CreateMiddleware();
        var context = CreateContext("POST", "/v1/query", VibePermissionLevel.Admin);
        var malformed = "{not valid json!!!}";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(malformed));
        context.Request.Body = stream;
        context.Request.ContentLength = stream.Length;
        context.Request.ContentType = "application/json";

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(400);
    }
}
