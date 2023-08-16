using CurrencyExchange.Interfaces;
using CurrencyExchange.Models;
using Microsoft.AspNetCore.Mvc;

namespace CurrencyExchange.Controllers
{
    [ApiController]
    [Route("api/trades")]
    public class TradeController : ControllerBase
    {
        private readonly ITradeService _tradeService;

        public TradeController(ITradeService tradeService)
        {
            _tradeService = tradeService;
        }

        [HttpPost]
        public async Task<IActionResult> PerformTrade([FromBody] TradeRequestModel request)
        {
            try
            {
                decimal convertedAmount = await _tradeService.PerformTradeAsync(request);

                return Ok(new { ConvertedAmount = convertedAmount });
            }
            catch (Exception ex)
            {
                // Handle and log exceptions
                string errorMessage = "An error occurred while performing the trade.";
                return StatusCode(StatusCodes.Status500InternalServerError, $"{errorMessage} Details: {ex.Message}");
            }
        }
    }
}
