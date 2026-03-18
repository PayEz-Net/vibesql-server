using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using VibeSQL.Edge.Authentication;
using VibeSQL.Edge.Configuration;
using VibeSQL.Edge.Data;
using VibeSQL.Edge.Data.Entities;
using VibeSQL.Edge.Proxy;

namespace VibeSQL.Edge.Tests.Integration;

public class EdgeWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestIssuer = "https://test-idp.example.com";
    public const string TestAudience = "test-api";
    public const string TestProviderKey = "test-idp";
    public const string HmacSecret = "dGVzdC1rZXktZm9yLWhtYWMtc2lnbmluZy0xMjM0NTY3OA==";
    private static readonly string TestSchemeId = DynamicSchemeRegistrar.ToSchemeId(TestProviderKey);

    private readonly string _dbName = Guid.NewGuid().ToString();
    public MockVibeServerHandler MockServer { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            var toRemove = services
                .Where(d =>
                {
                    var st = d.ServiceType;
                    var it = d.ImplementationType;
                    return st == typeof(DbContextOptions<EdgeDbContext>)
                        || st == typeof(DbContextOptions)
                        || st == typeof(EdgeDbContext)
                        || (st.IsGenericType && st.GetGenericTypeDefinition().Name.Contains("IDbContextOptionsConfiguration"))
                        || st.FullName?.Contains("Devart") == true
                        || it?.FullName?.Contains("Devart") == true;
                })
                .ToList();
            foreach (var d in toRemove)
                services.Remove(d);

            services.AddDbContext<EdgeDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            services.RemoveAll<IHmacSigner>();
            services.AddSingleton<IHmacSigner>(new HmacSigner(HmacSecret));

            services.RemoveAll<IHostedService>();

            services.AddHttpClient("VibeServer")
                .ConfigurePrimaryHttpMessageHandler(() => MockServer);

            services.AddAuthentication()
                .AddJwtBearer(TestSchemeId, options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = TestIssuer,
                        ValidateAudience = true,
                        ValidAudience = TestAudience,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(1),
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = TestJwtGenerator.SecurityKey
                    };
                    options.Configuration = new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration();
                    options.Configuration.SigningKeys.Add(TestJwtGenerator.SecurityKey);
                });
        });

        builder.ConfigureServices(services =>
        {
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var registry = scope.ServiceProvider.GetRequiredService<IProviderRegistry>();

            registry.Replace(new[]
            {
                new ProviderRecord
                {
                    ProviderKey = TestProviderKey,
                    Issuer = TestIssuer,
                    SchemeId = TestSchemeId,
                    IsActive = true,
                    IsBootstrap = true,
                    AutoProvision = true,
                    SubjectClaimPath = "sub",
                    RoleClaimPath = "roles",
                    EmailClaimPath = "email",
                    ProvisionDefaultRole = "viewer"
                }
            });
        });
    }

    public async Task SeedProviderAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EdgeDbContext>();
        if (!await db.OidcProviders.AnyAsync(p => p.ProviderKey == TestProviderKey))
        {
            db.OidcProviders.Add(new OidcProvider
            {
                ProviderKey = TestProviderKey,
                DisplayName = "Test IDP",
                Issuer = TestIssuer,
                DiscoveryUrl = $"{TestIssuer}/.well-known/openid-configuration",
                Audience = TestAudience,
                IsActive = true,
                IsBootstrap = true,
                AutoProvision = true,
                ProvisionDefaultRole = "viewer"
            });
            await db.SaveChangesAsync();
        }
    }

    public async Task SeedRoleMappingAsync(string externalRole, string vibePermission, string[]? deniedStatements = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EdgeDbContext>();
        db.OidcProviderRoleMappings.Add(new OidcProviderRoleMapping
        {
            ProviderKey = TestProviderKey,
            ExternalRole = externalRole,
            VibePermission = vibePermission,
            DeniedStatements = deniedStatements
        });
        await db.SaveChangesAsync();
    }

    public async Task SeedClientMappingAsync(string vibeClientId, string maxPermission)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EdgeDbContext>();
        db.OidcProviderClientMappings.Add(new OidcProviderClientMapping
        {
            ProviderKey = TestProviderKey,
            VibeClientId = vibeClientId,
            MaxPermission = maxPermission,
            IsActive = true
        });
        await db.SaveChangesAsync();
    }

    public async Task SeedFederatedIdentityAsync(string subject, int vibeUserId, bool isActive = true)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EdgeDbContext>();
        db.FederatedIdentities.Add(new FederatedIdentity
        {
            ProviderKey = TestProviderKey,
            ExternalSubject = subject,
            VibeUserId = vibeUserId,
            IsActive = isActive
        });
        await db.SaveChangesAsync();
    }

    public string GenerateToken(string subject = "test-user", string[]? roles = null, string? email = null)
    {
        return TestJwtGenerator.GenerateToken(TestIssuer, TestAudience, subject, roles, email);
    }

    public HttpClient CreateAuthenticatedClient(string subject = "test-user", string[]? roles = null, string? email = null)
    {
        var client = CreateClient();
        var token = GenerateToken(subject, roles, email);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

public class MockVibeServerHandler : DelegatingHandler
{
    public int CallCount;
    public HttpRequestMessage? LastRequest;
    public Func<HttpRequestMessage, HttpResponseMessage>? ResponseFactory;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Interlocked.Increment(ref CallCount);
        LastRequest = request;

        if (ResponseFactory is not null)
            return Task.FromResult(ResponseFactory(request));

        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { success = true, data = new { rows = new object[0] } }),
                System.Text.Encoding.UTF8,
                "application/json")
        };
        return Task.FromResult(response);
    }

    protected override void Dispose(bool disposing)
    {
    }
}
