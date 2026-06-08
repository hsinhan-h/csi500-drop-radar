using IndexSwingRadar.Services.Indices.Csi500;

namespace IndexSwingRadar.Services.Indices.Szse100;

public class Szse100Module : IMarketIndexModule
{
    public IndexDescriptor      Descriptor   { get; }
    public IConstituentProvider Constituents { get; }
    public IQuoteProvider       Quotes       { get; }
    public IMarketClock         Clock        { get; }

    public Szse100Module(
        EastmoneySzse100ConstituentProvider constituents,
        TencentChinaQuoteProvider quotes,
        ChinaMarketClock clock)
    {
        Descriptor = new IndexDescriptor(
            Id:                      "szse100",
            DisplayNameZh:           "深證100",
            DisplayNameEn:           "SZSE 100",
            Currency:                "CNY",
            ExpectedConstituentCount: 100,
            EstimatedTimeZh:         "約需 20–60 秒",
            EstimatedTimeEn:         "~20–60 sec");
        Constituents = constituents;
        Quotes       = quotes;
        Clock        = clock;
    }
}
