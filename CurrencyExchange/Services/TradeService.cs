using CurrencyExchange.DbContexts;
using CurrencyExchange.Helpers;
using CurrencyExchange.Interfaces;
using CurrencyExchange.Models;
using CurrencyExchange.Models.DTOs;
using Microsoft.Extensions.Caching.Distributed;

namespace CurrencyExchange.Services
{
    public class TradeService : ITradeService
    {
        private readonly CurrencyExchangeDbContext _dbContext;
        private readonly IDistributedCache _distributedCache;
        private readonly ILogger<TradeService> _logger;
        private readonly IExchangeRateProviderService _exchangeRateRetriever;

        public TradeService(CurrencyExchangeDbContext dbContext, IDistributedCache distributedCache, ILogger<TradeService> logger, IExchangeRateProviderService exchangeRateRetriever)
        {
            _dbContext = dbContext;
            _distributedCache = distributedCache;
            _logger = logger;
            _exchangeRateRetriever = exchangeRateRetriever;
        }

        public async Task<decimal> PerformTradeAsync(TradeRequestModel tradeRequestModel)
        {
            try
            {
                // Check trade count for the client
                if (await IsTradeLimitExceeded(tradeRequestModel.ClientId))
                {
                    throw new Exception("Trade limit exceeded for the client.");
                }

                decimal exchangeRate = await _exchangeRateRetriever.GetLatestExchangeRateAsync(ModelHelper.ToExchangeRateModel(tradeRequestModel));

                var trade = new CurrencyExchangeTradeDTO
                {
                    ClientId = tradeRequestModel.ClientId,
                    Amount = tradeRequestModel.Amount,
                    SourceCurrency = tradeRequestModel.SourceCurrency,
                    TargetCurrency = tradeRequestModel.TargetCurrency,
                    Timestamp = DateTime.UtcNow
                };

                _dbContext.CurrencyExchangeTrades.Add(trade);
                await _dbContext.SaveChangesAsync();

                await IncrementTradeCountAsync(tradeRequestModel.ClientId);

                return trade.Amount * exchangeRate;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while performing the trade.");
                throw;
            }
        }

        private async Task IncrementTradeCountAsync(string clientId)
        {
            try
            {
                string cacheKey = $"TradeCount_{clientId}";
                string cachedValue = await _distributedCache.GetStringAsync(cacheKey);

                int tradeCount = 0;
                if (!string.IsNullOrEmpty(cachedValue) && int.TryParse(cachedValue, out int cachedTradeCount))
                {
                    tradeCount = cachedTradeCount + 1;
                }

                var cacheEntryOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                };

                await _distributedCache.SetStringAsync(cacheKey, tradeCount.ToString(), cacheEntryOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while incrementing the trade count.");
                throw;
            }
        }
        private async Task<bool> IsTradeLimitExceeded(string clientId)
        {
            try
            {
                string cacheKey = $"TradeCount_{clientId}";
                string cachedValue = await _distributedCache.GetStringAsync(cacheKey);

                int tradeCount = 1;
                if (!string.IsNullOrEmpty(cachedValue) && int.TryParse(cachedValue, out int cachedTradeCount))
                {
                    tradeCount = cachedTradeCount;
                }

                // Check if the trade count exceeds the limit
                return tradeCount >= 10;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while checking the trade count.");
                throw;
            }
        }
    }

}
