namespace Csi500DropRadar.Services.Indices.UsCommon;

/// <summary>以美東時間（16:00 收盤）為基準，回傳最近一個美股交易日。</summary>
public class UsMarketClock : IMarketClock
{
    private readonly UsTradingCalendar _calendar;

    public UsMarketClock(UsTradingCalendar calendar)
        => _calendar = calendar;

    public DateTime ResolveLatestTradingDay()
    {
        var tz = GetTimeZone();
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        // 16:00（收盤）後才算當日資料完整
        var baseDate = now.TimeOfDay < TimeSpan.FromHours(16)
            ? now.Date.AddDays(-1)
            : now.Date;
        return _calendar.GetLatestTradingDay(baseDate);
    }

    // Windows："Eastern Standard Time"；Linux/Docker："America/New_York"
    private static TimeZoneInfo GetTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
    }
}
