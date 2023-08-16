using CurrencyExchange.Models;

namespace CurrencyExchange.Interfaces
{
    public interface ITradeService
    {
        Task<decimal> PerformTradeAsync(TradeRequestModel trade);
    }
}
