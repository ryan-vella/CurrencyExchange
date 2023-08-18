using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Moq;
using CurrencyExchange.Helpers;
using CurrencyExchange.Services;
using CurrencyExchange.DbContexts;
using CurrencyExchange.Interfaces;
using CurrencyExchange.Models;
using CurrencyExchange.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using CurrencyExchange.Tests.Helpers;

namespace CurrencyExchange.Tests
{
    public class TradeServiceTests
    {
        private readonly MockDataSetupHelper _mockDataSetupHelper;

        public TradeServiceTests()
        {
            _mockDataSetupHelper = new MockDataSetupHelper();
        }

        [Fact]
        public async Task PerformTradeAsync_ValidTrade_Success()
        {
            // Arrange
            var tradeRequestModel = new TradeRequestModel
            {
                ClientId = "client123",
                Amount = 100,
                SourceCurrency = "USD",
                TargetCurrency = "EUR"
            };

            var exchangeRate = 1.4m;

            var exchangeRateProviderServiceMock = new Mock<IExchangeRateProviderService>();
            exchangeRateProviderServiceMock.Setup(service =>
                service.GetLatestExchangeRateAsync(It.IsAny<ExchangeRateModel>()))
                .ReturnsAsync(exchangeRate);

            var options = _mockDataSetupHelper.GetInMemoryDbContextOptions();

            using var context = new CurrencyExchangeDbContext(options);
            _mockDataSetupHelper.SetupDatabaseWithInitialData(context, new ExchangeRateDTO { SourceCurrency = "EUR", TargetCurrency = "USD", Rate = 1.4m});

            var cachedExchangeRate = new CachedExchangeRate { Timestamp = DateTime.UtcNow.AddMinutes(-15), Value = 1.4m };
            var mockCache = _mockDataSetupHelper.SetupCacheMock(cachedExchangeRate);
            var loggerMock = new Mock<ILogger<TradeService>>();

            var tradeService = _mockDataSetupHelper.CreateTradeProviderServiceWithMocks(context, mockCache, loggerMock, exchangeRateProviderServiceMock);

            // Act
            var result = await tradeService.PerformTradeAsync(tradeRequestModel);

            // Assert
            Assert.Equal(140m, result); // The result should be 100 * 1.4
        }
    }
}
