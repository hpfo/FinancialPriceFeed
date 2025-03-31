using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FinancialPriceFeed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class TiingoPriceFeedStrategy : IPriceFeedStrategy
{
    public event Action<string, decimal>? PriceUpdated;
    private ClientWebSocket? _clientWebSocket;
    private readonly Uri _endpoint;
    private readonly string _apiToken;
    private readonly Dictionary<string, string> _symbolMapping;
    private readonly string _providerSymbol;  // The symbol provided (e.g., "BTCUSDT")
    private readonly string _internalSymbol;  // The mapped internal symbol (e.g., "BTCUSD")
    private readonly ILogger<TiingoPriceFeedStrategy> _logger;

    // Again, the symbol is passed as a parameter.
    public TiingoPriceFeedStrategy(IConfiguration config, ILogger<TiingoPriceFeedStrategy> logger, string symbol)
    {
        _logger = logger;
        _providerSymbol = symbol;
        _apiToken = config["Tiingo:ApiToken"] ?? "";
        _symbolMapping = config.GetSection("PriceFeed:Symbols").Get<Dictionary<string, string>>() ?? new();
        _internalSymbol = _symbolMapping.TryGetValue(_providerSymbol, out var mapped) ? mapped : _providerSymbol;
        var endpointStr = config["Tiingo:WsEndpoint"] ?? "wss://api.tiingo.com/iex";
        _endpoint = new Uri(endpointStr);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _clientWebSocket = new ClientWebSocket();
            await _clientWebSocket.ConnectAsync(_endpoint, cancellationToken);

            var subscribeMessage = new
            {
                eventName = "subscribe",
                authorization = _apiToken,
                eventData = new { tickers = new[] { _providerSymbol } }
            };
            var jsonSub = JsonSerializer.Serialize(subscribeMessage);
            await _clientWebSocket.SendAsync(Encoding.UTF8.GetBytes(jsonSub), WebSocketMessageType.Text, true, cancellationToken);
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
                    _logger.LogError(ex, "Error receiving from Tiingo WebSocket.");
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        // Ignore heartbeat messages
                        if (doc.RootElement.TryGetProperty("messageType", out var mt) && mt.GetString() == "H")
                            continue;
                        // Look for ticker ("ticker") and price ("last")
                        if (doc.RootElement.TryGetProperty("ticker", out var tickerElem) &&
                            doc.RootElement.TryGetProperty("last", out var priceElem))
                        {
                            var receivedSymbol = tickerElem.GetString() ?? "";
                            if (decimal.TryParse(priceElem.ToString(), out var price))
                            {
                                PriceUpdated?.Invoke(_internalSymbol, price);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error parsing Tiingo JSON.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TiingoPriceFeedStrategy.");
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
                _logger.LogError(ex, "Error closing Tiingo WebSocket.");
            }
        }
    }
}
