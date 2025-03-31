using FinancialPriceFeed.Hubs;
using FinancialPriceFeed.Services;
using FinancialPriceFeed;
using Microsoft.AspNetCore.SignalR;

public class FeedHostedService : BackgroundService
{
    private readonly IPriceFeedStrategy _strategy;
    private readonly PriceCacheService _priceCache;
    private readonly IHubContext<PriceHub> _hubContext;

    public FeedHostedService(IPriceFeedStrategy strategy, PriceCacheService priceCache, IHubContext<PriceHub> hubContext)
    {
        _strategy = strategy;
        _priceCache = priceCache;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _strategy.PriceUpdated += async (symbol, price) =>
        {
            _priceCache.UpdatePrice(symbol, price);
            await _hubContext.Clients.Group(symbol).SendAsync("ReceivePrice", symbol, price, stoppingToken);
        };

        await _strategy.StartAsync(stoppingToken);

        // Keep running until the service is stopped
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _strategy.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
