using CurrencyExchange.Interfaces;
using CurrencyExchange.Models;
using Microsoft.AspNetCore.Mvc;

namespace CurrencyExchange.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExchangeRatesController : ControllerBase
    {
        private readonly IExchangeRateProviderService _exchangeRateService;

        public ExchangeRatesController(IExchangeRateProviderService exchangeRateService)
        {
            _exchangeRateService = exchangeRateService;
        }

        [HttpGet("latest")]
        public async Task<ActionResult<decimal>> GetLatestExchangeRate(ExchangeRateModel exchangeRateModel)
        {
            try
            {

                decimal exchangeRate = await _exchangeRateService.GetLatestExchangeRateAsync(exchangeRateModel);
                return Ok(exchangeRate);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
