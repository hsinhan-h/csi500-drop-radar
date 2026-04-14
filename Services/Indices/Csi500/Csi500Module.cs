namespace Csi500DropRadar.Services.Indices.Csi500;

public class Csi500Module : IMarketIndexModule
{
    public IndexDescriptor      Descriptor   { get; }
    public IConstituentProvider Constituents { get; }
    public IQuoteProvider       Quotes       { get; }
    public IMarketClock         Clock        { get; }

    public Csi500Module(
        EastmoneyCsi500ConstituentProvider constituents,
        TencentChinaQuoteProvider quotes,
        ChinaMarketClock clock)
    {
        Descriptor = new IndexDescriptor(
            Id:                      "csi500",
            DisplayNameZh:           "中證500",
            DisplayNameEn:           "CSI 500",
            Currency:                "CNY",
            ExpectedConstituentCount: 500,
            EstimatedTimeZh:         "約需 1–3 分鐘",
            EstimatedTimeEn:         "~1–3 min");
        Constituents = constituents;
        Quotes       = quotes;
        Clock        = clock;
    }
}
