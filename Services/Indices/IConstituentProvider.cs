namespace Csi500DropRadar.Services.Indices;

public interface IConstituentProvider
{
    Task<IReadOnlyList<StockSymbol>> FetchAsync(CancellationToken ct = default);
}

public record StockSymbol(string Code, string Name);
