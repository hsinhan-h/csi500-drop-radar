using Csi500DropRadar.Models;

namespace Csi500DropRadar.Services.Indices;

public interface IQuoteProvider
{
    Task<StockRecord?> FetchAsync(
        StockSymbol symbol,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default);
}
