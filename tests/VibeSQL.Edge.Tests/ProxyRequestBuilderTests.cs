using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using VibeSQL.Edge.Proxy;

namespace VibeSQL.Edge.Tests;

public class ProxyRequestBuilderTests
{
    private readonly ProxyRequestBuilder _builder = new();

    private static HttpContext CreateHttpContext(
        string method = "POST",
        string path = "/v1/query",
        string? queryString = null,
        string? body = null,
        string? contentType = null,
        string? clientTier = null,
        string? tierClaims = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;

        if (queryString is not null)
            context.Request.QueryString = new QueryString(queryString);

        if (body is not null)
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(body));
            context.Request.Body = stream;
            context.Request.ContentLength = stream.Length;
        }

        if (contentType is not null)
            context.Request.ContentType = contentType;

        if (clientTier is not null)
            context.Items["ClientTier"] = clientTier;

        if (tierClaims is not null)
            context.Items["TierClaims"] = tierClaims;

        return context;
    }

    [Fact]
    public void Build_SetsHmacHeaders()
    {
        var context = CreateHttpContext();
        var hmac = new HmacSignatureResult("1703520000", "test-signature");

        var message = _builder.Build(context, hmac);

        message.Headers.GetValues("X-Vibe-Timestamp").Should().Contain("1703520000");
        message.Headers.GetValues("X-Vibe-Signature").Should().Contain("test-signature");
        message.Headers.GetValues("X-Vibe-Service").Should().Contain("vibesql-edge");
    }

    [Fact]
    public void Build_SetsCorrectMethodAndPath()
    {
        var context = CreateHttpContext("POST", "/v1/query");
        var hmac = new HmacSignatureResult("123", "sig");

        var message = _builder.Build(context, hmac);

        message.Method.Should().Be(HttpMethod.Post);
        message.RequestUri!.ToString().Should().Be("/v1/query");
    }

    [Fact]
    public void Build_IncludesQueryString()
    {
        var context = CreateHttpContext("GET", "/v1/schemas", queryString: "?format=json");
        var hmac = new HmacSignatureResult("123", "sig");

        var message = _builder.Build(context, hmac);

        message.RequestUri!.ToString().Should().Be("/v1/schemas?format=json");
    }

    [Fact]
    public void Build_ForwardsClientTierHeader()
    {
        var context = CreateHttpContext(clientTier: "premium");
        var hmac = new HmacSignatureResult("123", "sig");

        var message = _builder.Build(context, hmac);

        message.Headers.GetValues("X-Vibe-Client-Tier").Should().Contain("premium");
    }

    [Fact]
    public void Build_NoClientTier_OmitsHeader()
    {
        var context = CreateHttpContext();
        var hmac = new HmacSignatureResult("123", "sig");

        var message = _builder.Build(context, hmac);

        message.Headers.Contains("X-Vibe-Client-Tier").Should().BeFalse();
    }

    [Fact]
    public void Build_ForwardsTierClaimsHeader()
    {
        var context = CreateHttpContext(tierClaims: "sql_read,sql_write");
        var hmac = new HmacSignatureResult("123", "sig");

        var message = _builder.Build(context, hmac);

        message.Headers.GetValues("X-Vibe-Tier-Claims").Should().Contain("sql_read,sql_write");
    }

    [Fact]
    public void Build_WithBody_SetsStreamContent()
    {
        var context = CreateHttpContext(body: "{\"sql\":\"SELECT 1\"}", contentType: "application/json");
        var hmac = new HmacSignatureResult("123", "sig");

        var message = _builder.Build(context, hmac);

        message.Content.Should().NotBeNull();
        message.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public void Build_NoBody_NoContent()
    {
        var context = CreateHttpContext("GET", "/v1/health");
        var hmac = new HmacSignatureResult("123", "sig");

        var message = _builder.Build(context, hmac);

        message.Content.Should().BeNull();
    }

    [Fact]
    public void Build_DoesNotForwardVibeUserHeaders()
    {
        var context = CreateHttpContext();
        context.Request.Headers["X-Vibe-User-Id"] = "123";
        context.Request.Headers["X-Vibe-Client-Id"] = "abc";
        context.Request.Headers["X-Vibe-Permission-Level"] = "admin";
        var hmac = new HmacSignatureResult("123", "sig");

        var message = _builder.Build(context, hmac);

        message.Headers.Contains("X-Vibe-User-Id").Should().BeFalse();
        message.Headers.Contains("X-Vibe-Client-Id").Should().BeFalse();
        message.Headers.Contains("X-Vibe-Permission-Level").Should().BeFalse();
    }
}
