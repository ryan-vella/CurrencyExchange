using CurrencyExchange.Models;

namespace CurrencyExchange.Helpers
{
    public class ModelHelper
    {
        public static ExchangeRateModel ToExchangeRateModel(TradeRequestModel tradeRequestModel)
        {
            return new ExchangeRateModel
            {
                SourceCurrency = tradeRequestModel.SourceCurrency,
                TargetCurrency = tradeRequestModel.TargetCurrency
            };
        }
    }
}
