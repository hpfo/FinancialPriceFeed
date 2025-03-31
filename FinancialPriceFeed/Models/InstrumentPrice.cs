namespace FinancialPriceFeed.Models
{
    public class InstrumentPrice
    {
        public string Symbol { get; set; } = default!;
        public decimal Price { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public enum InstrumentSymbol
    {
        BTCUSD,
        EURUSD,
        USDJPY
        // Add more symbols as needed.
    }
}
