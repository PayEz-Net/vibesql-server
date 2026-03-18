using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using VibeSQL.Edge.Proxy;

namespace VibeSQL.Edge.Tests;

public class HmacSignerTests
{
    private const string TestKey = "dGVzdC1rZXktZm9yLWhtYWMtc2lnbmluZy0xMjM0NTY3OA==";

    [Fact]
    public void ComputeSignature_MatchesHmacAuthMiddlewareFormat()
    {
        var signer = new HmacSigner(TestKey);
        var timestamp = "1703520000";
        var method = "POST";
        var path = "/v1/query";

        var signature = signer.ComputeSignature(timestamp, method, path);

        var keyBytes = Convert.FromBase64String(TestKey);
        var stringToSign = $"{timestamp}|{method}|{path}";
        var dataBytes = Encoding.UTF8.GetBytes(stringToSign);
        using var hmac = new HMACSHA256(keyBytes);
        var expected = Convert.ToBase64String(hmac.ComputeHash(dataBytes));

        signature.Should().Be(expected);
    }

    [Fact]
    public void Sign_ReturnsTimestampAndSignature()
    {
        var signer = new HmacSigner(TestKey);

        var result = signer.Sign("GET", "/v1/query");

        result.Timestamp.Should().NotBeNullOrEmpty();
        result.Signature.Should().NotBeNullOrEmpty();
        long.TryParse(result.Timestamp, out _).Should().BeTrue();
    }

    [Fact]
    public void Sign_UsesUppercaseMethod()
    {
        var signer = new HmacSigner(TestKey);

        var lower = signer.Sign("post", "/v1/query");
        var upper = signer.Sign("POST", "/v1/query");

        lower.Signature.Should().Be(upper.Signature);
    }

    [Fact]
    public void ComputeSignature_DifferentPathsProduceDifferentSignatures()
    {
        var signer = new HmacSigner(TestKey);
        var timestamp = "1703520000";

        var sig1 = signer.ComputeSignature(timestamp, "POST", "/v1/query");
        var sig2 = signer.ComputeSignature(timestamp, "POST", "/v1/schemas");

        sig1.Should().NotBe(sig2);
    }

    [Fact]
    public void ComputeSignature_DifferentMethodsProduceDifferentSignatures()
    {
        var signer = new HmacSigner(TestKey);
        var timestamp = "1703520000";

        var sig1 = signer.ComputeSignature(timestamp, "GET", "/v1/query");
        var sig2 = signer.ComputeSignature(timestamp, "POST", "/v1/query");

        sig1.Should().NotBe(sig2);
    }

    [Fact]
    public void ComputeSignature_DifferentTimestampsProduceDifferentSignatures()
    {
        var signer = new HmacSigner(TestKey);

        var sig1 = signer.ComputeSignature("1703520000", "POST", "/v1/query");
        var sig2 = signer.ComputeSignature("1703520001", "POST", "/v1/query");

        sig1.Should().NotBe(sig2);
    }

    [Fact]
    public void ComputeSignature_OutputIsBase64()
    {
        var signer = new HmacSigner(TestKey);

        var signature = signer.ComputeSignature("1703520000", "POST", "/v1/query");

        var act = () => Convert.FromBase64String(signature);
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_InvalidBase64Key_Throws()
    {
        var act = () => new HmacSigner("not-valid-base64!!!");
        act.Should().Throw<FormatException>();
    }
}
