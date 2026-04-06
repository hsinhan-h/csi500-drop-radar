using Csi500DropRadar.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<TaskManagerService>();
builder.Services.AddHttpClient<StockFetchService>();
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
