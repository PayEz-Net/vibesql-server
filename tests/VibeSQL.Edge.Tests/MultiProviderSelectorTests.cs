using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using VibeSQL.Edge;
using VibeSQL.Edge.Authentication;

namespace VibeSQL.Edge.Tests;

public class MultiProviderSelectorTests
{
    private readonly ILogger _logger = NullLogger.Instance;

    private static string CreateTestJwt(string issuer, string audience = "test-api")
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(new byte[32]);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature),
            Subject = new ClaimsIdentity(new[] { new Claim("sub", "test-user") })
        };
        return handler.CreateEncodedJwt(descriptor);
    }

    private static HttpContext CreateHttpContextWithBearer(string token)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = $"Bearer {token}";
        return context;
    }

    [Fact]
    public void SelectScheme_MissingAuthHeader_ReturnsNull()
    {
        var registry = new ProviderRegistry();
        var context = new DefaultHttpContext();

        var result = MultiProviderSelector.SelectScheme(context, registry, _logger);

        result.Should().BeNull();
    }

    [Fact]
    public void SelectScheme_NonBearerAuthHeader_ReturnsNull()
    {
        var registry = new ProviderRegistry();
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Basic dXNlcjpwYXNz";

        var result = MultiProviderSelector.SelectScheme(context, registry, _logger);

        result.Should().BeNull();
    }

    [Fact]
    public void SelectScheme_MalformedToken_ReturnsNull()
    {
        var registry = new ProviderRegistry();
        var context = CreateHttpContextWithBearer("not-a-jwt-token");

        var result = MultiProviderSelector.SelectScheme(context, registry, _logger);

        result.Should().BeNull();
    }

    [Fact]
    public void SelectScheme_OversizedToken_ReturnsNull()
    {
        var registry = new ProviderRegistry();
        var oversized = new string('A', 17 * 1024);
        var context = CreateHttpContextWithBearer(oversized);

        var result = MultiProviderSelector.SelectScheme(context, registry, _logger);

        result.Should().BeNull();
    }

    [Fact]
    public void SelectScheme_UnknownIssuer_ReturnsNull()
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

        var token = CreateTestJwt("https://unknown-issuer.example.com");
        var context = CreateHttpContextWithBearer(token);

        var result = MultiProviderSelector.SelectScheme(context, registry, _logger);

        result.Should().BeNull();
    }

    [Fact]
    public void SelectScheme_DisabledProvider_ReturnsNull()
    {
        var registry = new ProviderRegistry();
        registry.Replace(new List<ProviderRecord>
        {
            new()
            {
                ProviderKey = "disabled-idp",
                Issuer = "https://disabled.example.com",
                SchemeId = "oidc-disabled-idp",
                IsActive = false,
                IsBootstrap = false,
                DisabledAt = DateTimeOffset.UtcNow
            }
        });

        var token = CreateTestJwt("https://disabled.example.com");
        var context = CreateHttpContextWithBearer(token);

        var result = MultiProviderSelector.SelectScheme(context, registry, _logger);

        result.Should().BeNull();
    }

    [Fact]
    public void SelectScheme_ActiveProvider_ReturnsSchemeId()
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

        var token = CreateTestJwt("https://idp.payez.net");
        var context = CreateHttpContextWithBearer(token);

        var result = MultiProviderSelector.SelectScheme(context, registry, _logger);

        result.Should().Be("oidc-payez-idp");
    }

    [Fact]
    public void SelectScheme_ActiveProvider_SetsEdgeProviderKeyOnContext()
    {
        var registry = new ProviderRegistry();
        registry.Replace(new List<ProviderRecord>
        {
            new()
            {
                ProviderKey = "contoso-ad",
                Issuer = "https://login.microsoftonline.com/contoso",
                SchemeId = "oidc-contoso-ad",
                IsActive = true,
                IsBootstrap = false
            }
        });

        var token = CreateTestJwt("https://login.microsoftonline.com/contoso");
        var context = CreateHttpContextWithBearer(token);

        MultiProviderSelector.SelectScheme(context, registry, _logger);

        context.Items[EdgeContextKeys.ProviderKey].Should().Be("contoso-ad");
    }

    [Fact]
    public void SelectScheme_MultipleProviders_RoutesToCorrectScheme()
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
            },
            new()
            {
                ProviderKey = "contoso-ad",
                Issuer = "https://login.microsoftonline.com/contoso",
                SchemeId = "oidc-contoso-ad",
                IsActive = true,
                IsBootstrap = false
            },
            new()
            {
                ProviderKey = "okta-dev",
                Issuer = "https://dev-123.okta.com",
                SchemeId = "oidc-okta-dev",
                IsActive = true,
                IsBootstrap = false
            }
        });

        var payezToken = CreateTestJwt("https://idp.payez.net");
        var contosoToken = CreateTestJwt("https://login.microsoftonline.com/contoso");
        var oktaToken = CreateTestJwt("https://dev-123.okta.com");

        MultiProviderSelector.SelectScheme(CreateHttpContextWithBearer(payezToken), registry, _logger)
            .Should().Be("oidc-payez-idp");
        MultiProviderSelector.SelectScheme(CreateHttpContextWithBearer(contosoToken), registry, _logger)
            .Should().Be("oidc-contoso-ad");
        MultiProviderSelector.SelectScheme(CreateHttpContextWithBearer(oktaToken), registry, _logger)
            .Should().Be("oidc-okta-dev");
    }

    [Fact]
    public void ExtractBearerToken_EmptyHeader_ReturnsNull()
    {
        var context = new DefaultHttpContext();
        MultiProviderSelector.ExtractBearerToken(context).Should().BeNull();
    }

    [Fact]
    public void ExtractBearerToken_BearerWithToken_ReturnsToken()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer abc123";
        MultiProviderSelector.ExtractBearerToken(context).Should().Be("abc123");
    }

    [Fact]
    public void ExtractBearerToken_CaseInsensitive()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "bearer abc123";
        MultiProviderSelector.ExtractBearerToken(context).Should().Be("abc123");
    }

    [Fact]
    public void ReadIssuerFromUnvalidatedJwt_ValidJwt_ReturnsIssuer()
    {
        var token = CreateTestJwt("https://idp.payez.net");
        MultiProviderSelector.ReadIssuerFromUnvalidatedJwt(token).Should().Be("https://idp.payez.net");
    }

    [Fact]
    public void ReadIssuerFromUnvalidatedJwt_GarbageString_ReturnsNull()
    {
        MultiProviderSelector.ReadIssuerFromUnvalidatedJwt("not.a.jwt").Should().BeNull();
    }

    [Fact]
    public void ReadIssuerFromUnvalidatedJwt_EmptyString_ReturnsNull()
    {
        MultiProviderSelector.ReadIssuerFromUnvalidatedJwt("").Should().BeNull();
    }
}
