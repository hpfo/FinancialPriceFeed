using System.Collections.Concurrent;
using FinancialPriceFeed.Models;

namespace FinancialPriceFeed.Services
{
    public class PriceCacheService
    {
        private readonly ConcurrentDictionary<string, InstrumentPrice> _prices
            = new ConcurrentDictionary<string, InstrumentPrice>();

        public void UpdatePrice(string symbol, decimal price)
        {
            _prices[symbol] = new InstrumentPrice
            {
                Symbol = symbol,
                Price = price,
                Timestamp = DateTime.UtcNow
            };
        }

        public InstrumentPrice? GetPrice(string symbol)
        {
            _prices.TryGetValue(symbol, out var price);
            return price;
        }
    }
}
