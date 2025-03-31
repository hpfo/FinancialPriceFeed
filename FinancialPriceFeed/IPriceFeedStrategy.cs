namespace FinancialPriceFeed
{
    public interface IPriceFeedStrategy
    {
        /// <summary>
        /// Fires whenever a price update is received.
        /// </summary>
        event Action<string, decimal>? PriceUpdated;

        /// <summary>
        /// Connects to the feed and starts receiving data.
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Closes the connection gracefully if needed.
        /// </summary>
        Task StopAsync(CancellationToken cancellationToken);
    }


}
