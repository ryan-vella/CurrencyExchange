using CurrencyExchange.Models;

namespace CurrencyExchange.Interfaces
{
    public interface IExchangeRateProviderService {
        Task<decimal> GetLatestExchangeRateAsync(ExchangeRateModel exchangeRateModel);
    }
}
