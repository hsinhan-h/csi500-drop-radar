namespace Csi500DropRadar.Services.Indices;

public interface ITradingCalendar
{
    bool IsTradingDay(DateTime date);
    DateTime GetLatestTradingDay(DateTime from);
}
