namespace Csi500DropRadar.Services.Localization;

/// <summary>
/// 極簡靜態 i18n 工具。鍵值表與前端 wwwroot/i18n/*.json 語義對應。
/// 供後端 Excel 輸出使用。
/// </summary>
public static class LocalizationHelper
{
    private static readonly Dictionary<string, Dictionary<string, string>> _t = new()
    {
        ["zh-TW"] = new()
        {
            ["col.rank"]        = "#",
            ["col.code"]        = "代碼",
            ["col.name"]        = "名稱",
            ["col.start_close"] = "期初收盤價",
            ["col.end_close"]   = "最新收盤價",
            ["col.pct_change"]  = "漲跌幅(%)",
            ["col.start_date"]  = "期初日期",
            ["col.end_date"]    = "最新日期",
            ["period.week"]     = "近一週",
            ["period.month"]    = "近一個月",
            ["sheet.top"]       = "跌幅前",
            ["sheet.all"]       = "全部成分股",
        },
        ["en"] = new()
        {
            ["col.rank"]        = "#",
            ["col.code"]        = "Code",
            ["col.name"]        = "Name",
            ["col.start_close"] = "Start Close",
            ["col.end_close"]   = "End Close",
            ["col.pct_change"]  = "Change(%)",
            ["col.start_date"]  = "Start Date",
            ["col.end_date"]    = "End Date",
            ["period.week"]     = "Past Week",
            ["period.month"]    = "Past Month",
            ["sheet.top"]       = "Top Losers",
            ["sheet.all"]       = "All Components",
        },
    };

    /// <summary>
    /// 取得翻譯字串。<paramref name="locale"/> 以 "zh" 開頭視為繁中，否則英文；
    /// 找不到 key 時直接回傳 key。
    /// </summary>
    public static string Get(string key, string? locale = null)
    {
        var lang = locale?.StartsWith("zh", StringComparison.OrdinalIgnoreCase) == true
            ? "zh-TW" : "en";

        return _t.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out var val)
            ? val
            : key;
    }

    public static string PeriodLabel(string period, string? locale = null) =>
        Get($"period.{period}", locale);
}
