namespace Csi500DropRadar.Services;

/// <summary>
/// 中國 A 股交易日曆。
/// 每年國務院公告假日後，請在 <see cref="Holidays"/> 補充下一年度的週間休市日。
/// </summary>
public static class TradingCalendar
{
    // 僅列出「週間（非週六日）的休市日」；週末由 IsTradingDay 另行判斷
    private static readonly HashSet<DateTime> Holidays =
    [
        // 2025 ──────────────────────────────────────────────────────────────
        new(2025,  1,  1),                                          // 元旦
        new(2025,  1, 28), new(2025,  1, 29), new(2025,  1, 30),
        new(2025,  1, 31), new(2025,  2,  3), new(2025,  2,  4),   // 春節
        new(2025,  4,  4),                                          // 清明
        new(2025,  5,  1), new(2025,  5,  2), new(2025,  5,  5),   // 勞動節
        new(2025,  6,  2),                                          // 端午
        new(2025, 10,  1), new(2025, 10,  2), new(2025, 10,  3),
        new(2025, 10,  6), new(2025, 10,  7), new(2025, 10,  8),   // 國慶＋中秋

        // 2026 ──────────────────────────────────────────────────────────────
        new(2026,  1,  1),                                          // 元旦
        new(2026,  1, 28), new(2026,  1, 29), new(2026,  1, 30),
        new(2026,  2,  2), new(2026,  2,  3), new(2026,  2,  4),   // 春節（正月初一 1/29）
        new(2026,  4,  6),                                          // 清明（補休）
        new(2026,  5,  1), new(2026,  5,  4), new(2026,  5,  5),   // 勞動節
        new(2026,  6, 19),                                          // 端午
        new(2026, 10,  1), new(2026, 10,  2), new(2026, 10,  5),
        new(2026, 10,  6), new(2026, 10,  7), new(2026, 10,  8),   // 國慶＋中秋
    ];

    /// <summary>判斷指定日期是否為交易日（非週末且非國定假日）。</summary>
    public static bool IsTradingDay(DateTime date) =>
        date.DayOfWeek != DayOfWeek.Saturday &&
        date.DayOfWeek != DayOfWeek.Sunday &&
        !Holidays.Contains(date.Date);

    /// <summary>從指定日期往前回推，找到最近一個交易日。</summary>
    public static DateTime GetLatestTradingDay(DateTime from)
    {
        var date = from.Date;
        while (!IsTradingDay(date))
            date = date.AddDays(-1);
        return date;
    }
}
