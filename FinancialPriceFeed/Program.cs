using FinancialPriceFeed;

var builder = WebApplication.CreateBuilder(args);

// Register configuration and logging services.
builder.Services.AddSingleton<IPriceFeedStrategy, BinancePriceFeedStrategy>(); // Not used directly.
builder.Services.AddHostedService<MultiSymbolFeedHostedService>();

// Add logging, configuration, etc.
builder.Services.AddLogging();
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

var app = builder.Build();
app.Run();
