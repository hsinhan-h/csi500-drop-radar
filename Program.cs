using IndexSwingRadar.Services;
using IndexSwingRadar.Services.Indices;
using IndexSwingRadar.Services.Indices.Csi500;
using IndexSwingRadar.Services.Indices.Ndx;
using IndexSwingRadar.Services.Indices.Sox;
using IndexSwingRadar.Services.Indices.Szse100;
using IndexSwingRadar.Services.Indices.UsCommon;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

// ── 各市場交易日曆 & 時鐘 ──────────────────────────────────────────────
builder.Services.AddSingleton<ChinaTradingCalendar>();
builder.Services.AddSingleton<ChinaMarketClock>();
builder.Services.AddSingleton<UsTradingCalendar>();
builder.Services.AddSingleton<UsMarketClock>();

// ── 各 Provider（均為 singleton，自行管理 HttpClient 生命週期）──────────
builder.Services.AddSingleton<EastmoneyCsi500ConstituentProvider>();
builder.Services.AddSingleton<EastmoneySzse100ConstituentProvider>();
builder.Services.AddSingleton<TencentChinaQuoteProvider>();
builder.Services.AddSingleton<MoneyDjSoxxConstituentProvider>();
builder.Services.AddSingleton<NasdaqNdxConstituentProvider>();
builder.Services.AddSingleton<YahooQuoteProvider>();

// ── 指數模組（新增第四個指數：加一個 AddSingleton<IMarketIndexModule, ...>）
builder.Services.AddSingleton<IMarketIndexModule, Csi500Module>();
builder.Services.AddSingleton<IMarketIndexModule, Szse100Module>();
builder.Services.AddSingleton<IMarketIndexModule, SoxModule>();
builder.Services.AddSingleton<IMarketIndexModule, NdxModule>();

// ── 核心服務 ────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IndexRegistry>();
builder.Services.AddSingleton<TaskManagerService>();
builder.Services.AddSingleton<DropAnalysisService>();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter(
                System.Text.Json.JsonNamingPolicy.CamelCase)));

var app = builder.Build();

app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
