namespace Csi500DropRadar.Services.Indices;

/// <summary>以 id（"csi500"、"sox"、"ndx"…）為 key 的指數模組查詢表。</summary>
public class IndexRegistry
{
    private readonly Dictionary<string, IMarketIndexModule> _map;

    public IndexRegistry(IEnumerable<IMarketIndexModule> modules)
    {
        _map = modules.ToDictionary(
            m => m.Descriptor.Id,
            StringComparer.OrdinalIgnoreCase);
    }

    public IMarketIndexModule Get(string id) =>
        _map.TryGetValue(id, out var m)
            ? m
            : throw new ArgumentException($"未知指數代號：{id}");

    public IReadOnlyCollection<IMarketIndexModule> All() => _map.Values;
}
