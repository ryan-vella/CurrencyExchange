using CurrencyExchange.DbContexts;
using CurrencyExchange.Interfaces;
using CurrencyExchange.Models;
using CurrencyExchange.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using System.Text;

namespace CurrencyExchange.Services
{
    public class ExchangeRateProviderService : IExchangeRateProviderService
    {
        private readonly HttpClient _httpClient;
        private readonly IDistributedCache _distributedCache;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ExchangeRateProviderService> _logger;
        private readonly CurrencyExchangeDbContext _dbContext;

        public ExchangeRateProviderService(HttpClient httpClient, IDistributedCache distributedCache, IConfiguration configuration, ILogger<ExchangeRateProviderService> logger, CurrencyExchangeDbContext dbContext)
        {
            _httpClient = httpClient;
            _distributedCache = distributedCache;
            _configuration = configuration;
            _logger = logger;
            _dbContext = dbContext;
        }
        public async Task<decimal> GetLatestExchangeRateAsync(ExchangeRateModel exchangeRateModel)
        {
            try
            {
                var cachedData = await _distributedCache.GetStringAsync($"ExchangeRate_{exchangeRateModel.SourceCurrency}_{exchangeRateModel.TargetCurrency}");

                if (!string.IsNullOrEmpty(cachedData) &&
                    JsonConvert.DeserializeObject<CachedExchangeRate>(cachedData)?.Timestamp >= DateTime.UtcNow.AddMinutes(-30))
                {
                    return JsonConvert.DeserializeObject<CachedExchangeRate>(cachedData).Value;
                }

                decimal exchangeRateValue = -1m; // Default value in case no rate is available
                ExchangeRateDTO exchangeRateFromDatabase = null;
                if (_dbContext != null)
                {
                    // Check the database for exchange rate
                    exchangeRateFromDatabase = await _dbContext.ExchangeRates.FirstOrDefaultAsync(
                       er => er.SourceCurrency == exchangeRateModel.SourceCurrency && er.TargetCurrency == exchangeRateModel.TargetCurrency);


                    if (exchangeRateFromDatabase != null)
                        // Cache the exchange rate
                        return exchangeRateFromDatabase.Rate;
                }

                // Fetch and cache a new exchange rate
                if (exchangeRateValue == -1m)
                {
                    // Fetch and cache a new exchange rate
                    exchangeRateValue = await GetAndCacheExchangeRateAsync(exchangeRateModel);
                }

                return exchangeRateValue;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting the latest exchange rate.");
                return -1;
            }
        }
        public async Task<decimal> FetchExchangeRateFromApi(ExchangeRateModel exchangeRateModel)
        {
            try
            {
                var configuration = _configuration.GetSection("ExchangeRatesApi").Get<ExchangeRateApiConfiguration>();

                string apiUrl = $"{configuration.BaseUrl}/latest?access_key={configuration.AccessKey}";
                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    var exchangeRateData = JsonConvert.DeserializeObject<ExchangeRateApiResponse>(responseBody);

                    if (exchangeRateData.Rates.TryGetValue(exchangeRateModel.TargetCurrency, out decimal exchangeRate))
                    {
                        return exchangeRate;
                    }
                }

                throw new Exception("Failed to fetch exchange rate from the API.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch exchange rate from the API.");
                throw new Exception("Failed to fetch exchange rate from the API.");
            }
        }
        public async Task<decimal> GetAndCacheExchangeRateAsync(ExchangeRateModel exchangeRateModel)
        {
            try
            {
                decimal exchangeRate = await FetchExchangeRateFromApi(exchangeRateModel);

                // Store exchange rate and timestamp
                var cacheData = new CachedExchangeRate
                {
                    Name = exchangeRateModel.TargetCurrency,
                    Value = exchangeRate,
                    Timestamp = DateTime.UtcNow
                };

                var cacheEntryOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                };

                var serializedData = JsonConvert.SerializeObject(cacheData);
                await _distributedCache.SetStringAsync($"ExchangeRate_{exchangeRateModel.SourceCurrency}_{exchangeRateModel.TargetCurrency}", serializedData, cacheEntryOptions);

                // Cache in the database
                var exchangeRateEntity = new ExchangeRateDTO
                {
                    SourceCurrency = exchangeRateModel.SourceCurrency,
                    TargetCurrency = exchangeRateModel.TargetCurrency,
                    Rate = exchangeRate,
                    Timestamp = DateTime.UtcNow
                };

                _dbContext.ExchangeRates.Add(exchangeRateEntity);
                await _dbContext.SaveChangesAsync();

                return exchangeRate;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting and caching the exchange rate.");
                throw;
            }
        }
    }
}
