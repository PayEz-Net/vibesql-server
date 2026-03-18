using System.Security.Cryptography;
using System.Text;

namespace VibeSQL.Edge.Proxy;

public interface IHmacSigner
{
    HmacSignatureResult Sign(string method, string path);
}

public record HmacSignatureResult(string Timestamp, string Signature);

public sealed class HmacSigner : IHmacSigner
{
    private readonly byte[] _keyBytes;

    public HmacSigner(string base64Key)
    {
        _keyBytes = Convert.FromBase64String(base64Key);
    }

    public HmacSignatureResult Sign(string method, string path)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = ComputeSignature(timestamp, method.ToUpperInvariant(), path);
        return new HmacSignatureResult(timestamp, signature);
    }

    internal string ComputeSignature(string timestamp, string method, string path)
    {
        var stringToSign = $"{timestamp}|{method}|{path}";
        var dataBytes = Encoding.UTF8.GetBytes(stringToSign);

        using var hmac = new HMACSHA256(_keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        return Convert.ToBase64String(hash);
    }
}
