using Csi500DropRadar.Services;
using Csi500DropRadar.Services.Indices;
using Csi500DropRadar.Services.Indices.Csi500;
using Csi500DropRadar.Services.Indices.Ndx;
using Csi500DropRadar.Services.Indices.Sox;
using Csi500DropRadar.Services.Indices.UsCommon;

var builder = WebApplication.CreateBuilder(args);

// ── 各市場交易日曆 & 時鐘 ──────────────────────────────────────────────
builder.Services.AddSingleton<ChinaTradingCalendar>();
builder.Services.AddSingleton<ChinaMarketClock>();
builder.Services.AddSingleton<UsTradingCalendar>();
builder.Services.AddSingleton<UsMarketClock>();

// ── 各 Provider（均為 singleton，自行管理 HttpClient 生命週期）──────────
builder.Services.AddSingleton<EastmoneyCsi500ConstituentProvider>();
builder.Services.AddSingleton<TencentChinaQuoteProvider>();
builder.Services.AddSingleton<IsharesSoxxConstituentProvider>();
builder.Services.AddSingleton<NasdaqNdxConstituentProvider>();
builder.Services.AddSingleton<YahooQuoteProvider>();

// ── 指數模組（新增第四個指數：加一個 AddSingleton<IMarketIndexModule, ...>）
builder.Services.AddSingleton<IMarketIndexModule, Csi500Module>();
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

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

app.Run();
