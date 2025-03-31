using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using FinancialPriceFeed.Services;
using System.Collections.Generic;
using System.Linq;

namespace FinancialPriceFeed.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InstrumentsController : ControllerBase
    {
        private readonly PriceCacheService _priceCacheService;
        private readonly Dictionary<string, string> _instrumentMapping;

        public InstrumentsController(IConfiguration configuration, PriceCacheService priceCacheService)
        {
            _priceCacheService = priceCacheService;
            // Read the mapping from configuration.
            // The key is the provider symbol (e.g. "BTCUSDT")
            // The value is your normalized internal symbol (e.g. "BTCUSD")
            _instrumentMapping = configuration.GetSection("PriceFeed:Symbols")
                                    .Get<Dictionary<string, string>>() ?? new Dictionary<string, string>();
        }

        // GET /api/instruments
        [HttpGet]
        public IActionResult GetInstruments()
        {
            // Return the internal symbols (values) or keys if you prefer.
            // Here we're returning distinct internal symbol names.
            var instruments = _instrumentMapping.Values.Distinct().ToArray();
            return Ok(instruments);
        }

        // GET /api/instruments/{symbol}
        [HttpGet("{symbol}")]
        public IActionResult GetPrice(string symbol)
        {
            // Assume the incoming symbol is already normalized (e.g., "BTCUSD")
            var normalizedSymbol = symbol.ToUpper();
            var priceInfo = _priceCacheService.GetPrice(normalizedSymbol);
            if (priceInfo == null)
                return NotFound($"Price not found for symbol {normalizedSymbol}");
            return Ok(priceInfo);
        }
    }
}
