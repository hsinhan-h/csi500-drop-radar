namespace Csi500DropRadar.Services.Indices;

/// <summary>供各 Provider 共用的 HTTP 工具方法。</summary>
public static class CommonHttp
{
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
