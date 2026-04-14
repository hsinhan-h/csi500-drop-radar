namespace Csi500DropRadar.Services.Indices.Csi500;

/// <summary>以北京時間（15:00 收盤）為基準，回傳最近一個中國 A 股交易日。</summary>
public class ChinaMarketClock : IMarketClock
{
    private readonly ChinaTradingCalendar _calendar;

    public ChinaMarketClock(ChinaTradingCalendar calendar)
        => _calendar = calendar;

    public DateTime ResolveLatestTradingDay()
    {
        var tz = GetTimeZone();
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        // 15:00（收盤）後才算當日資料完整
        var baseDate = now.TimeOfDay < TimeSpan.FromHours(15)
            ? now.Date.AddDays(-1)
            : now.Date;
        return _calendar.GetLatestTradingDay(baseDate);
    }

    // Windows："China Standard Time"；Linux/Docker："Asia/Shanghai"
    private static TimeZoneInfo GetTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"); }
    }
}
