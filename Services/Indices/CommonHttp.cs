using System.Net;
using System.Net.Sockets;
using Polly;
using Polly.Retry;

namespace IndexSwingRadar.Services.Indices;

public static class CommonHttp
{
    /// <summary>
    /// 建立可處理 429 / 5xx / 網路例外的 Polly Retry Pipeline（指數退避 + Jitter）。
    /// </summary>
    public static ResiliencePipeline<HttpResponseMessage> CreateHttpRetryPipeline(int maxAttempts = 4) =>
        new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests
                                    || (int)r.StatusCode >= 500),
                MaxRetryAttempts = maxAttempts - 1,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            })
            .Build();

    /// <summary>
    /// 建立強制走 IPv4 的 HttpClient，避免在 Render 等平台上因 IPv6 出站
    /// 連到只有 IPv4 後端的 API 時收到 502 Bad Gateway。
    /// </summary>
    public static HttpClient CreateIpv4Client(bool useCookies = false)
    {
        var handler = new SocketsHttpHandler
        {
            UseCookies = useCookies,
            ConnectCallback = async (ctx, ct) =>
            {
                var addresses = await Dns.GetHostAddressesAsync(
                    ctx.DnsEndPoint.Host, AddressFamily.InterNetwork, ct);
                var socket = new Socket(
                    AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                { NoDelay = true };
                await socket.ConnectAsync(
                    new IPEndPoint(addresses[0], ctx.DnsEndPoint.Port), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
        };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <summary>最多重試 <paramref name="maxAttempts"/> 次，指數退避，失敗則拋例外。</summary>
    public static async Task<string> RetryGetAsync(
        HttpClient http,
        string url,
        int maxAttempts = 3,
        CancellationToken ct = default)
    {
        var pipeline = new ResiliencePipelineBuilder<string>()
            .AddRetry(new RetryStrategyOptions<string>
            {
                ShouldHandle = new PredicateBuilder<string>().Handle<Exception>(),
                MaxRetryAttempts = maxAttempts - 1,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
            })
            .Build();

        return await pipeline.ExecuteAsync(
            async token => await http.GetStringAsync(url, token), ct);
    }
}
