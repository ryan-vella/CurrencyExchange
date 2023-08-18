using CurrencyExchange.DbContexts;
using CurrencyExchange.Models;
using CurrencyExchange.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq.Protected;
using Moq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using CurrencyExchange.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using CurrencyExchange.Interfaces;

namespace CurrencyExchange.Tests.Helpers
{
    public class MockDataSetupHelper
    {

        private readonly Mock<IDistributedCache> _distributedCacheMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<CurrencyExchangeDbContext> _dbContextMock;
        private readonly Mock<ILogger<ExchangeRateProviderService>> _loggerMock;
        private readonly Mock<ILogger<TradeService>> _loggerMockTrade;
        private readonly Mock<IExchangeRateProviderService> _exchangeProviderMock;

        public MockDataSetupHelper()
        {
            _distributedCacheMock = new Mock<IDistributedCache>();
            _configurationMock = new Mock<IConfiguration>();
            _dbContextMock = new Mock<CurrencyExchangeDbContext>();
            _loggerMock = new Mock<ILogger<ExchangeRateProviderService>>();
            _exchangeProviderMock = new Mock<IExchangeRateProviderService>();
        }
        public ExchangeRateProviderService CreateExchangeRateProviderServiceWithMocks(CurrencyExchangeDbContext context = null, Mock<IDistributedCache> cacheMock = null, Mock<IConfiguration> configurationMock = null, Mock<ILogger<ExchangeRateProviderService>> loggerMock = null, Mock<HttpClient> httpClientMock = null)
        {
            return new ExchangeRateProviderService(
                httpClientMock?.Object ?? new HttpClient(),
                cacheMock?.Object ?? _distributedCacheMock.Object,
                configurationMock?.Object ?? _configurationMock.Object,
                loggerMock?.Object ?? _loggerMock.Object,
                context ?? _dbContextMock.Object
            );
        }

        public TradeService CreateTradeProviderServiceWithMocks(CurrencyExchangeDbContext context = null, Mock<IDistributedCache> cacheMock = null, Mock<ILogger<TradeService>> loggerMock = null, Mock<IExchangeRateProviderService> exchangeServiceMock = null)
        {
            return new TradeService(
                context ?? _dbContextMock.Object,
                cacheMock?.Object ?? _distributedCacheMock.Object,
                loggerMock?.Object ?? _loggerMockTrade.Object,
                exchangeServiceMock?.Object ?? _exchangeProviderMock.Object
            );
        }

        public Mock<IDistributedCache> SetupCacheMock<T>(T cachedValue)
        {
            var mockCache = new Mock<IDistributedCache>();
            mockCache.Setup(c => c.Remove(It.IsAny<string>())).Verifiable();
            mockCache.Setup(c => c.GetAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>())
            ).ReturnsAsync(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(cachedValue)));
            return mockCache;
        }

        public Mock<IConfiguration> SetupConfigurationMock(ExchangeRateApiConfiguration exchangeRateApiConfiguration)
        {
            var mockConfiguration = new Mock<IConfiguration>();
            var configValues = new Dictionary<string, string>
                {
                    { "ExchangeRatesApi:BaseUrl", exchangeRateApiConfiguration.BaseUrl }
                };
            var configSection = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build()
                .GetSection("ExchangeRatesApi");
            mockConfiguration.Setup(x => x.GetSection("ExchangeRatesApi"))
                  .Returns(configSection);
            return mockConfiguration;
        }

        public Mock<HttpClient> SetupHttpClientMock(string responseContent, HttpStatusCode httpStatusCode = HttpStatusCode.OK)
        {
            var handlerMock = new Mock<HttpMessageHandler>();

            if (responseContent != null)
            {
                var response = new HttpResponseMessage
                {
                    StatusCode = httpStatusCode,
                    Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
                };

                handlerMock.Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>()
                    )
                    .ReturnsAsync(response);
            }
            else
            {
                handlerMock.Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>()
                    )
                    .ThrowsAsync(new Exception("Simulated exception"));
            }

            var httpClientMock = new Mock<HttpClient>(handlerMock.Object);
            return httpClientMock;
        }

        public Mock<HttpClient> SetupHttpClientMockForException(Exception exception)
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                       .Setup<Task<HttpResponseMessage>>(
                            "SendAsync",
                            ItExpr.IsAny<HttpRequestMessage>(),
                            ItExpr.IsAny<CancellationToken>()
                        )
                       .ThrowsAsync(exception);

            var httpClientMock = new Mock<HttpClient>(handlerMock.Object);

            return httpClientMock;
        }

        public CurrencyExchangeDbContext SetupDatabaseWithInitialData(CurrencyExchangeDbContext context, ExchangeRateDTO dto = null)
        {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            if(dto != null )
            {
                context.ExchangeRates.Add(dto);
                context.SaveChanges();
            }
            
            return context;

        }

        public DbContextOptions<CurrencyExchangeDbContext> GetInMemoryDbContextOptions()
        {
            return new DbContextOptionsBuilder<CurrencyExchangeDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDatabase")
                .Options;
        }
    }
}
