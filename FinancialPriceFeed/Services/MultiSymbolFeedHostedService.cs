using FinancialPriceFeed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class MultiSymbolFeedHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MultiSymbolFeedHostedService> _logger;
    private readonly List<IPriceFeedStrategy> _strategies = new();
    private CancellationTokenSource? _cts;

    public MultiSymbolFeedHostedService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<MultiSymbolFeedHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var mapping = _configuration
            .GetSection("PriceFeed:Symbols")
            .Get<Dictionary<string, string>>() ?? new();

        // Create a BinancePriceFeedStrategy instance for each provider symbol
        foreach (var providerSymbol in mapping.Keys)
        {
            try
            {
                // Use ActivatorUtilities to pass the symbol parameter into the constructor
                var strategy = ActivatorUtilities.CreateInstance<BinancePriceFeedStrategy>(
                    _serviceProvider,
                    providerSymbol
                );

                // Optionally subscribe to the PriceUpdated event
                strategy.PriceUpdated += (symbol, price) =>
                {
                    _logger.LogInformation("Price update for {Symbol}: {Price}", symbol, price);
                };

                _strategies.Add(strategy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating strategy for symbol {Symbol}", providerSymbol);
            }
        }

        // Start all strategies concurrently
        var tasks = _strategies.Select(s => s.StartAsync(_cts.Token));
        await Task.WhenAll(tasks);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts != null)
        {
            _cts.Cancel();
        }

        var tasks = _strategies.Select(s => s.StopAsync(cancellationToken));
        await Task.WhenAll(tasks);
    }
}
