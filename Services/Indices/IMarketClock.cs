namespace Csi500DropRadar.Services.Indices;

public interface IMarketClock
{
    /// <summary>依據各市場時區與收盤門檻，回傳目前最近一個有效交易日。</summary>
    DateTime ResolveLatestTradingDay();
}
