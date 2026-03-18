using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VibeSQL.Edge.Configuration;

namespace VibeSQL.Edge.Proxy;

[ApiController]
[Authorize]
public class ProxyController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHmacSigner _hmacSigner;
    private readonly IProxyRequestBuilder _requestBuilder;
    private readonly ILogger<ProxyController> _logger;

    public ProxyController(
        IHttpClientFactory httpClientFactory,
        IHmacSigner hmacSigner,
        IProxyRequestBuilder requestBuilder,
        ILogger<ProxyController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _hmacSigner = hmacSigner;
        _requestBuilder = requestBuilder;
        _logger = logger;
    }

    [Route("v1/{**catchAll}")]
    [AcceptVerbs("GET", "POST", "PUT", "PATCH", "DELETE")]
    public async Task<IActionResult> ProxyToServer(string? catchAll)
    {
        var path = HttpContext.Request.Path.Value ?? "/";
        var method = HttpContext.Request.Method;

        Request.EnableBuffering();

        var hmac = _hmacSigner.Sign(method, path);
        var proxyRequest = _requestBuilder.Build(HttpContext, hmac);

        var client = _httpClientFactory.CreateClient("VibeServer");

        try
        {
            var response = await client.SendAsync(proxyRequest, HttpContext.RequestAborted);

            HttpContext.Response.StatusCode = (int)response.StatusCode;

            foreach (var header in response.Headers)
            {
                if (ShouldForwardResponseHeader(header.Key))
                    HttpContext.Response.Headers[header.Key] = header.Value.ToArray();
            }

            if (response.Content.Headers.ContentType is not null)
                HttpContext.Response.ContentType = response.Content.Headers.ContentType.ToString();

            await response.Content.CopyToAsync(HttpContext.Response.Body, HttpContext.RequestAborted);
            return new EmptyResult();
        }
        catch (TaskCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            return StatusCode(499);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "EDGE_PROXY: Failed to forward request to VibeSQL Server. Path={Path}", path);
            return StatusCode(502, new { success = false, error = new { code = "PROXY_ERROR", message = "Failed to reach VibeSQL Server" } });
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "EDGE_PROXY: Request to VibeSQL Server timed out. Path={Path}", path);
            return StatusCode(504, new { success = false, error = new { code = "PROXY_TIMEOUT", message = "VibeSQL Server request timed out" } });
        }
    }

    private static bool ShouldForwardResponseHeader(string headerName)
    {
        return !headerName.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)
            && !headerName.Equals("Connection", StringComparison.OrdinalIgnoreCase);
    }
}
