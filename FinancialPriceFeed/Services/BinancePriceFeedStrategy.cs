using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FinancialPriceFeed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Fetches live price updates from Binance for a single symbol.
/// </summary>
public class BinancePriceFeedStrategy : IPriceFeedStrategy
{
    public event Action<string, decimal>? PriceUpdated;

    private ClientWebSocket? _clientWebSocket;
    private readonly Uri _endpoint;
    private readonly string _providerSymbol;  // e.g., "BTCUSDT"
    private readonly string _internalSymbol;  // e.g., "BTCUSD"
    private readonly ILogger<BinancePriceFeedStrategy> _logger;

    /// <summary>
    /// Creates a price feed strategy for a specific symbol from Binance.
    /// </summary>
    /// <param name="config">The configuration that contains "PriceFeed:Symbols" and "Binance:WsBaseEndpoint".</param>
    /// <param name="logger">Logger for error/info logs.</param>
    /// <param name="symbol">The provider symbol, e.g. "BTCUSDT".</param>
    public BinancePriceFeedStrategy(IConfiguration config, ILogger<BinancePriceFeedStrategy> logger, string symbol)
    {
        _logger = logger;
        _providerSymbol = symbol;

        // Load the mapping from configuration: "PriceFeed:Symbols"
        var mapping = config.GetSection("PriceFeed:Symbols").Get<Dictionary<string, string>>() ?? new();
        _internalSymbol = mapping.TryGetValue(_providerSymbol, out var mapped) ? mapped : _providerSymbol;

        // Build the full WebSocket URL
        var baseEndpoint = config["Binance:WsBaseEndpoint"] ?? "wss://stream.binance.com:443/ws";
        _endpoint = new Uri($"{baseEndpoint}/{_providerSymbol.ToLower()}@aggTrade");
    }

    /// <summary>
    /// Starts listening to the Binance WebSocket for price updates.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _clientWebSocket = new ClientWebSocket();
            await _clientWebSocket.ConnectAsync(_endpoint, cancellationToken);

            var buffer = new byte[4096];

            while (!cancellationToken.IsCancellationRequested &&
                   _clientWebSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await _clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error receiving from Binance WebSocket for symbol {Symbol}.", _providerSymbol);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        // "s" is the symbol, "p" is the price in an aggTrade event
                        if (doc.RootElement.TryGetProperty("s", out var s) &&
                            doc.RootElement.TryGetProperty("p", out var p))
                        {
                            if (decimal.TryParse(p.ToString(), out var price))
                            {
                                // Emit the internal symbol, e.g. "BTCUSD"
                                PriceUpdated?.Invoke(_internalSymbol, price);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error parsing JSON for symbol {Symbol}.", _providerSymbol);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in BinancePriceFeedStrategy for symbol {Symbol}.", _providerSymbol);
        }
        finally
        {
            await StopAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Gracefully closes the WebSocket connection.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_clientWebSocket != null &&
            (_clientWebSocket.State == WebSocketState.Open || _clientWebSocket.State == WebSocketState.CloseReceived))
        {
            try
            {
                await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Stopping", cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing Binance WebSocket for symbol {Symbol}.", _providerSymbol);
            }
        }
    }
}
