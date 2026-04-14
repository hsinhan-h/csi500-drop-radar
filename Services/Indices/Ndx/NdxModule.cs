using Csi500DropRadar.Services.Indices.UsCommon;

namespace Csi500DropRadar.Services.Indices.Ndx;

public class NdxModule : IMarketIndexModule
{
    public IndexDescriptor      Descriptor   { get; }
    public IConstituentProvider Constituents { get; }
    public IQuoteProvider       Quotes       { get; }
    public IMarketClock         Clock        { get; }

    public NdxModule(
        NasdaqNdxConstituentProvider constituents,
        YahooQuoteProvider quotes,
        UsMarketClock clock)
    {
        Descriptor = new IndexDescriptor(
            Id:                      "ndx",
            DisplayNameZh:           "那斯達克100",
            DisplayNameEn:           "Nasdaq NDX",
            Currency:                "USD",
            ExpectedConstituentCount: 100,
            EstimatedTimeZh:         "約需 20–60 秒",
            EstimatedTimeEn:         "~20–60 sec");
        Constituents = constituents;
        Quotes       = quotes;
        Clock        = clock;
    }
}
