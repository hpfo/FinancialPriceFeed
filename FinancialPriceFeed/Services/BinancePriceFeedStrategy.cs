using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FinancialPriceFeed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class BinancePriceFeedStrategy : IPriceFeedStrategy
{
    public event Action<string, decimal>? PriceUpdated;
    private ClientWebSocket? _clientWebSocket;
    private readonly Uri _endpoint;
    private readonly string _providerSymbol;  // e.g. "BTCUSDT"
    private readonly string _internalSymbol;  // e.g. "BTCUSD"
    private readonly ILogger<BinancePriceFeedStrategy> _logger;

    // Now the symbol is passed in as a parameter.
    public BinancePriceFeedStrategy(IConfiguration config, ILogger<BinancePriceFeedStrategy> logger, string symbol)
    {
        _logger = logger;
        _providerSymbol = symbol;
        // Load the mapping from configuration.
        var mapping = config.GetSection("PriceFeed:Symbols").Get<Dictionary<string, string>>() ?? new();
        _internalSymbol = mapping.TryGetValue(_providerSymbol, out var mapped) ? mapped : _providerSymbol;
        var baseEndpoint = config["Binance:WsBaseEndpoint"] ?? "wss://stream.binance.com:443/ws";
        // Binance expects the symbol in lowercase with the stream suffix.
        _endpoint = new Uri($"{baseEndpoint}/{_providerSymbol.ToLower()}@aggTrade");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _clientWebSocket = new ClientWebSocket();
            await _clientWebSocket.ConnectAsync(_endpoint, cancellationToken);
            var buffer = new byte[4096];

            while (!cancellationToken.IsCancellationRequested && _clientWebSocket.State == WebSocketState.Open)
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
                        if (doc.RootElement.TryGetProperty("s", out var s) &&
                            doc.RootElement.TryGetProperty("p", out var p))
                        {
                            if (decimal.TryParse(p.ToString(), out var price))
                            {
                                // Raise event with the internal symbol name.
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
