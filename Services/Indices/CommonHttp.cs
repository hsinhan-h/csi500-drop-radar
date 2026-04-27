using System.Net;
using System.Net.Sockets;

namespace IndexSwingRadar.Services.Indices;

/// <summary>供各 Provider 共用的 HTTP 工具方法。</summary>
public static class CommonHttp
{
    /// <summary>
    /// 建立強制走 IPv4 的 HttpClient，避免在 Render 等平台上因 IPv6 出站
    /// 連到只有 IPv4 後端的中國 API 時收到 502 Bad Gateway。
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
        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                return await http.GetStringAsync(url, ct);
            }
            catch when (i < maxAttempts - 1)
            {
                await Task.Delay(2000 * (i + 1), ct);
            }
        }
        throw new Exception($"重試 {maxAttempts} 次失敗：{url}");
    }
}
