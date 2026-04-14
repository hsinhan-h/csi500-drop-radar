using Csi500DropRadar.Services.Indices.UsCommon;

namespace Csi500DropRadar.Services.Indices.Sox;

public class SoxModule : IMarketIndexModule
{
    public IndexDescriptor      Descriptor   { get; }
    public IConstituentProvider Constituents { get; }
    public IQuoteProvider       Quotes       { get; }
    public IMarketClock         Clock        { get; }

    public SoxModule(
        IsharesSoxxConstituentProvider constituents,
        YahooQuoteProvider quotes,
        UsMarketClock clock)
    {
        Descriptor = new IndexDescriptor(
            Id:                      "sox",
            DisplayNameZh:           "費半 SOX",
            DisplayNameEn:           "PHLX SOX",
            Currency:                "USD",
            ExpectedConstituentCount: 30,
            EstimatedTimeZh:         "約需 10–30 秒",
            EstimatedTimeEn:         "~10–30 sec");
        Constituents = constituents;
        Quotes       = quotes;
        Clock        = clock;
    }
}
