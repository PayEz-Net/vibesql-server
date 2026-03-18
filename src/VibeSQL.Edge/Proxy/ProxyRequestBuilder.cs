namespace VibeSQL.Edge.Proxy;

public interface IProxyRequestBuilder
{
    HttpRequestMessage Build(HttpContext incomingContext, HmacSignatureResult hmac);
}

public sealed class ProxyRequestBuilder : IProxyRequestBuilder
{
    private const string ServiceName = "vibesql-edge";
    private const string TimestampHeader = "X-Vibe-Timestamp";
    private const string SignatureHeader = "X-Vibe-Signature";
    private const string ServiceHeader = "X-Vibe-Service";
    private const string ClientTierHeader = "X-Vibe-Client-Tier";
    private const string TierClaimsHeader = "X-Vibe-Tier-Claims";

    public HttpRequestMessage Build(HttpContext incomingContext, HmacSignatureResult hmac)
    {
        var request = incomingContext.Request;
        var path = request.Path.Value ?? "/";
        var query = request.QueryString.Value ?? "";
        var method = new HttpMethod(request.Method);

        var message = new HttpRequestMessage(method, path + query);

        message.Headers.TryAddWithoutValidation(TimestampHeader, hmac.Timestamp);
        message.Headers.TryAddWithoutValidation(SignatureHeader, hmac.Signature);
        message.Headers.TryAddWithoutValidation(ServiceHeader, ServiceName);

        var clientTier = incomingContext.Items["ClientTier"] as string;
        if (!string.IsNullOrEmpty(clientTier))
            message.Headers.TryAddWithoutValidation(ClientTierHeader, clientTier);

        var tierClaims = incomingContext.Items["TierClaims"] as string;
        if (!string.IsNullOrEmpty(tierClaims))
            message.Headers.TryAddWithoutValidation(TierClaimsHeader, tierClaims);

        if (request.ContentLength > 0 || request.ContentType is not null)
        {
            if (request.Body.CanSeek)
                request.Body.Position = 0;

            message.Content = new StreamContent(request.Body);

            if (request.ContentType is not null)
                message.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(request.ContentType);

            if (request.ContentLength.HasValue)
                message.Content.Headers.ContentLength = request.ContentLength;
        }

        return message;
    }
}
