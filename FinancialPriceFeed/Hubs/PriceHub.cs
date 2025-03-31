using Microsoft.AspNetCore.SignalR;

namespace FinancialPriceFeed.Hubs
{
    public class PriceHub : Hub
    {
        public async Task Subscribe(string symbol)
        {
            symbol = symbol.ToUpper();
            await Groups.AddToGroupAsync(Context.ConnectionId, symbol);
            Console.WriteLine($"{Context.ConnectionId} subscribed to {symbol}");
        }

        public async Task Unsubscribe(string symbol)
        {
            symbol = symbol.ToUpper();
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, symbol);
            Console.WriteLine($"{Context.ConnectionId} unsubscribed from {symbol}");
        }
    }
}
