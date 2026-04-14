namespace Csi500DropRadar.Services.Indices.UsCommon;

/// <summary>
/// NYSE / NASDAQ 交易日曆。
/// 僅列出「週間（非週六日）的休市日」；週末由 IsTradingDay 另行判斷。
/// </summary>
public class UsTradingCalendar : ITradingCalendar
{
    private static readonly HashSet<DateTime> Holidays =
    [
        // 2025 ──────────────────────────────────────────────────────────────
        new(2025,  1,  1),   // New Year's Day
        new(2025,  1, 20),   // Martin Luther King Jr. Day
        new(2025,  2, 17),   // Presidents' Day
        new(2025,  4, 18),   // Good Friday
        new(2025,  5, 26),   // Memorial Day
        new(2025,  6, 19),   // Juneteenth
        new(2025,  7,  4),   // Independence Day
        new(2025,  9,  1),   // Labor Day
        new(2025, 11, 27),   // Thanksgiving
        new(2025, 12, 25),   // Christmas

        // 2026 ──────────────────────────────────────────────────────────────
        new(2026,  1,  1),   // New Year's Day
        new(2026,  1, 19),   // Martin Luther King Jr. Day
        new(2026,  2, 16),   // Presidents' Day
        new(2026,  4,  3),   // Good Friday
        new(2026,  5, 25),   // Memorial Day
        new(2026,  6, 19),   // Juneteenth
        new(2026,  7,  3),   // Independence Day (observed; Jul 4 is Saturday)
        new(2026,  9,  7),   // Labor Day
        new(2026, 11, 26),   // Thanksgiving
        new(2026, 12, 25),   // Christmas
    ];

    public bool IsTradingDay(DateTime date) =>
        date.DayOfWeek != DayOfWeek.Saturday &&
        date.DayOfWeek != DayOfWeek.Sunday &&
        !Holidays.Contains(date.Date);

    public DateTime GetLatestTradingDay(DateTime from)
    {
        var date = from.Date;
        while (!IsTradingDay(date))
            date = date.AddDays(-1);
        return date;
    }
}
