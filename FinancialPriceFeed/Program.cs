var builder = WebApplication.CreateBuilder(args);

// Logging & Configuration
builder.Services.AddLogging();
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

// Register the MultiSymbolFeedHostedService as a hosted service
builder.Services.AddHostedService<MultiSymbolFeedHostedService>();

// We do NOT register BinancePriceFeedStrategy directly as an IPriceFeedStrategy singleton
// because we need multiple instances, each with a different string parameter.
// Instead, the MultiSymbolFeedHostedService calls ActivatorUtilities.CreateInstance<BinancePriceFeedStrategy>().

var app = builder.Build();
app.Run();
