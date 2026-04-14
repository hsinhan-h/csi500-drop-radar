namespace Csi500DropRadar.Services.Indices;

/// <summary>
/// 代表一個完整的指數模組。新增指數 = 實作此介面並在 Program.cs 以
/// AddSingleton&lt;IMarketIndexModule, YourModule&gt;() 註冊。
/// </summary>
public interface IMarketIndexModule
{
    IndexDescriptor      Descriptor   { get; }
    IConstituentProvider Constituents { get; }
    IQuoteProvider       Quotes       { get; }
    IMarketClock         Clock        { get; }
}
