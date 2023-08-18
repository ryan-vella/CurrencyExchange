using CurrencyExchange.Models;
using CurrencyExchange.Services;
using Moq;
using System.Net;
using Microsoft.Extensions.Configuration;
using CurrencyExchange.DbContexts;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using CurrencyExchange.Models.DTOs;
using Moq.Protected;
using Microsoft.Extensions.Options;
using CurrencyExchange.Tests.Helpers;

namespace CurrencyExchange.Tests
{
    public class ExchangeRateProviderServiceTests
    {
        private readonly Mock<IDistributedCache> _distributedCacheMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<CurrencyExchangeDbContext> _dbContextMock;
        private readonly Mock<ILogger<ExchangeRateProviderService>> _loggerMock;
        private readonly MockDataSetupHelper _mockDataSetupHelper;

        public ExchangeRateProviderServiceTests()
        {
            _distributedCacheMock = new Mock<IDistributedCache>();
            _configurationMock = new Mock<IConfiguration>();
            _dbContextMock = new Mock<CurrencyExchangeDbContext>();
            _loggerMock = new Mock<ILogger<ExchangeRateProviderService>>();
            _mockDataSetupHelper = new MockDataSetupHelper();
        }

        [Fact]
        public async Task GetLatestExchangeRateAsync_CachedRateExpired_DatabaseRateAvailable_ReturnsDatabaseRate()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CurrencyExchangeDbContext>()
                .UseInMemoryDatabase(databaseName: "test_database")
                .Options;

            using var context = new CurrencyExchangeDbContext(options);
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
            context.ExchangeRates.Add(new ExchangeRateDTO { SourceCurrency = "USD", TargetCurrency = "EUR", Rate = 1.2m });
            context.SaveChanges();

            var service = _mockDataSetupHelper.CreateServiceWithMocks(context);

            // Act
            var exchangeRateModel = new ExchangeRateModel { SourceCurrency = "USD", TargetCurrency = "EUR" };
            var result = await service.GetLatestExchangeRateAsync(exchangeRateModel);

            // Assert
            Assert.Equal(1.2m, result); // Should return the rate from the database
        }

        [Fact]
        public async Task GetLatestExchangeRateAsync_CachedRateNotExpired_ReturnsCachedRate()
        {
            // Arrange
            var cachedExchangeRate = new CachedExchangeRate { Timestamp = DateTime.UtcNow.AddMinutes(-15), Value = 1.2m };
            var mockCache = _mockDataSetupHelper.SetupCacheMock(cachedExchangeRate);

            var service = _mockDataSetupHelper.CreateServiceWithMocks(null, mockCache);

            var exchangeRateModel = new ExchangeRateModel { SourceCurrency = "USD", TargetCurrency = "EUR" };

            // Act
            var result = await service.GetLatestExchangeRateAsync(exchangeRateModel);

            // Assert
            Assert.Equal(cachedExchangeRate.Value, result);
        }

        [Fact]
        public async Task GetLatestExchangeRateAsync_CachedRateExpired_DatabaseRateNotAvailable_ReturnsNewRate()
        {
            // Arrange
            var expiredCachedExchangeRate = new CachedExchangeRate { Timestamp = DateTime.UtcNow.AddMinutes(-60), Value = 1.2m };
            var mockCache = _mockDataSetupHelper.SetupCacheMock(expiredCachedExchangeRate);

            var exchangeRateApiConfiguration = new ExchangeRateApiConfiguration
            {
                BaseUrl = "https://baseurl.com/api"
            };

            var mockConfiguration = _mockDataSetupHelper.SetupConfigurationMock(exchangeRateApiConfiguration);

            var options = new DbContextOptionsBuilder<CurrencyExchangeDbContext>()
                .UseInMemoryDatabase(databaseName: "test_database")
                .Options;

            using var context = new CurrencyExchangeDbContext(options);
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
            context.ExchangeRates.Add(new ExchangeRateDTO { SourceCurrency = "GBP", TargetCurrency = "EUR", Rate = 1.3m });
            context.SaveChanges();

            var httpClient = _mockDataSetupHelper.SetupHttpClientMock("{\"rates\": {\"EUR\": 1.4}}");

            var service = _mockDataSetupHelper.CreateServiceWithMocks(context, mockCache, mockConfiguration, null, httpClient);

            // Act
            var exchangeRateModel = new ExchangeRateModel { SourceCurrency = "USD", TargetCurrency = "EUR" };
            var result = await service.GetLatestExchangeRateAsync(exchangeRateModel);

            // Assert
            Assert.Equal(1.4m, result); // Should return the new fetched rate
            mockCache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetLatestExchangeRateAsync_CachedDataIsValid_ReturnsCachedRate()
        {
            // Arrange
            var cachedExchangeRate = new CachedExchangeRate { Timestamp = DateTime.UtcNow.AddMinutes(5), Value = 1.2m };
            var mockCache = _mockDataSetupHelper.SetupCacheMock(cachedExchangeRate);
            var service = _mockDataSetupHelper.CreateServiceWithMocks(null, mockCache);

            var exchangeRateModel = new ExchangeRateModel { SourceCurrency = "USD", TargetCurrency = "EUR" };

            // Act
            var result = await service.GetLatestExchangeRateAsync(exchangeRateModel);

            // Assert
            Assert.Equal(cachedExchangeRate.Value, result);
        }

        [Fact]
        public async Task GetLatestExchangeRateAsync_CachedDataExpired_RetrievesAndCachesNewRate()
        {
            // Arrange
            var expiredCachedExchangeRate = new CachedExchangeRate { Timestamp = DateTime.UtcNow.AddMinutes(-30), Value = 1.2m };
            var mockCache = _mockDataSetupHelper.SetupCacheMock(expiredCachedExchangeRate);
            var httpClient = _mockDataSetupHelper.SetupHttpClientMock("{\"rates\": {\"EUR\": 1.3}}");
            var options = new DbContextOptionsBuilder<CurrencyExchangeDbContext>()
                .UseInMemoryDatabase(databaseName: "test_database")
                .Options;

            using var context = new CurrencyExchangeDbContext(options);
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
            context.ExchangeRates.Add(new ExchangeRateDTO { SourceCurrency = "GBP", TargetCurrency = "USD", Rate = 1.2m });
            context.SaveChanges();

            var exchangeRateApiConfiguration = new ExchangeRateApiConfiguration
            {
                BaseUrl = "https://baseurl.com/api"
            };

            var mockConfiguration = _mockDataSetupHelper.SetupConfigurationMock(exchangeRateApiConfiguration);

            var service = _mockDataSetupHelper.CreateServiceWithMocks(context, mockCache, mockConfiguration, null, httpClient);

            var exchangeRateModel = new ExchangeRateModel { SourceCurrency = "USD", TargetCurrency = "EUR" };

            // Act
            var result = await service.GetLatestExchangeRateAsync(exchangeRateModel);

            // Assert
            Assert.Equal(1.3m, result); // Should return the new fetched rate
            mockCache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetLatestExchangeRateAsync_ExchangeRateInDatabase_ReturnsDatabaseRate()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CurrencyExchangeDbContext>()
                .UseInMemoryDatabase(databaseName: "test_database")
                .Options;

            using var context = new CurrencyExchangeDbContext(options);
            context.ExchangeRates.Add(new ExchangeRateDTO { SourceCurrency = "USD", TargetCurrency = "EUR", Rate = 1.2m });
            context.SaveChanges();

            var service = _mockDataSetupHelper.CreateServiceWithMocks(context);

            var exchangeRateModel = new ExchangeRateModel { SourceCurrency = "USD", TargetCurrency = "EUR" };

            // Act
            var result = await service.GetLatestExchangeRateAsync(exchangeRateModel);

            // Assert
            Assert.Equal(1.2m, result); // Should return the rate from the database
        }

        [Fact]
        public async Task GetLatestExchangeRateAsync_ExchangeRateNotInDatabase_FetchesAndCachesNewRate()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CurrencyExchangeDbContext>()
                .UseInMemoryDatabase(databaseName: "test_database")
                .Options;

            var exchangeRateApiConfiguration = new ExchangeRateApiConfiguration
            {
                BaseUrl = "https://baseurl.com/api"
            };

            var mockConfiguration = _mockDataSetupHelper.SetupConfigurationMock(exchangeRateApiConfiguration);

            var expiredCachedExchangeRate = new CachedExchangeRate { Timestamp = DateTime.UtcNow.AddMinutes(-30), Value = 1.2m };
            var mockCache = _mockDataSetupHelper.SetupCacheMock(expiredCachedExchangeRate);

            var httpClient = _mockDataSetupHelper.SetupHttpClientMock("{\"rates\": {\"EUR\": 1.4}}");

            using var context = new CurrencyExchangeDbContext(options);
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
            context.ExchangeRates.Add(new ExchangeRateDTO { SourceCurrency = "GBP", TargetCurrency = "USD", Rate = 1.2m });
            context.SaveChanges();

            var service = _mockDataSetupHelper.CreateServiceWithMocks(context: context, httpClientMock: httpClient, cacheMock: mockCache, configurationMock: mockConfiguration);

            var exchangeRateModel = new ExchangeRateModel { SourceCurrency = "USD", TargetCurrency = "EUR" };

            // Act
            var result = await service.GetLatestExchangeRateAsync(exchangeRateModel);

            // Assert
            Assert.Equal(1.4m, result); // Should return the new fetched rate
            Assert.True(context.ExchangeRates.Any()); // Should store the rate in the database
            mockCache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetLatestExchangeRateAsync_FetchExchangeRateFromApi_SuccessfulResponse_ReturnsExchangeRate()
        {
            // Arrange
            var httpClient = _mockDataSetupHelper.SetupHttpClientMock("{\"rates\": {\"EUR\": 1.4}}");

            var exchangeRateApiConfiguration = new ExchangeRateApiConfiguration
            {
                BaseUrl = "https://baseurl.com/api"
            };

            var mockConfiguration = _mockDataSetupHelper.SetupConfigurationMock(exchangeRateApiConfiguration);

            var exchangeRateModel = new ExchangeRateModel { SourceCurrency = "USD", TargetCurrency = "EUR" };
            var options = new DbContextOptionsBuilder<CurrencyExchangeDbContext>()
               .UseInMemoryDatabase(databaseName: "test_database")
               .Options;
            using var context = new CurrencyExchangeDbContext(options);
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
            context.ExchangeRates.Add(new ExchangeRateDTO { SourceCurrency = "GBP", TargetCurrency = "USD", Rate = 1.2m });
            context.SaveChanges();

            var service = _mockDataSetupHelper.CreateServiceWithMocks(httpClientMock: httpClient, context: context, configurationMock: mockConfiguration);
            // Act
            var result = await service.GetLatestExchangeRateAsync(exchangeRateModel);

            // Assert
            Assert.Equal(1.4m, result); // Should return the fetched rate
        }

        [Fact]
        public async Task GetLatestExchangeRateAsync_FetchExchangeRateFromApi_UnsuccessfulResponse_ThrowsException()
        {
            // Arrange
            var httpClient = _mockDataSetupHelper.SetupHttpClientMock(null, HttpStatusCode.BadRequest); // Simulate an unsuccessful response

            var exchangeRateApiConfiguration = new ExchangeRateApiConfiguration
            {
                BaseUrl = "https://baseurl.com/api"
            };
            var loggerMock = new Mock<ILogger<ExchangeRateProviderService>>();

            var mockConfiguration = _mockDataSetupHelper.SetupConfigurationMock(exchangeRateApiConfiguration);

            var exchangeRateModel = new ExchangeRateModel { SourceCurrency = "USD", TargetCurrency = "EUR" };
            var options = new DbContextOptionsBuilder<CurrencyExchangeDbContext>()
               .UseInMemoryDatabase(databaseName: "test_database")
               .Options;
            using var context = new CurrencyExchangeDbContext(options);
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
            context.ExchangeRates.Add(new ExchangeRateDTO { SourceCurrency = "GBP", TargetCurrency = "USD", Rate = 1.2m });
            context.SaveChanges();

            var expiredCachedExchangeRate = new CachedExchangeRate { Timestamp = DateTime.UtcNow.AddMinutes(-30), Value = 1.2m };
            var mockCache = _mockDataSetupHelper.SetupCacheMock(expiredCachedExchangeRate);

            var service = _mockDataSetupHelper.CreateServiceWithMocks(loggerMock: loggerMock, cacheMock: mockCache, httpClientMock: httpClient, context: context, configurationMock: mockConfiguration);
            // Act & Assert
            await service.GetLatestExchangeRateAsync(exchangeRateModel);

            loggerMock.Verify(
               x => x.Log(
                   LogLevel.Error,
                   It.IsAny<EventId>(),
                   It.IsAny<It.IsAnyType>(),
                   It.IsAny<Exception>(),
                   (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()
               ),
               Times.Exactly(3)
           );
        }

        [Fact]
        public async Task GetLatestExchangeRateAsync_FetchExchangeRateFromApi_ExceptionThrown_LogsError()
        {
            // Arrange
            var httpClient = _mockDataSetupHelper.SetupHttpClientMockForException(new Exception("Simulated exception"));
            var loggerMock = new Mock<ILogger<ExchangeRateProviderService>>();
            var service = _mockDataSetupHelper.CreateServiceWithMocks(httpClientMock: httpClient, loggerMock: loggerMock);

            var exchangeRateModel = new ExchangeRateModel { SourceCurrency = "USD", TargetCurrency = "EUR" };

            // Act
            await service.GetLatestExchangeRateAsync(exchangeRateModel);

            // Assert
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task GetAndCacheExchangeRateAsync_FetchExchangeRateFromApi_SuccessfulResponse_ReturnsExchangeRateAndCaches()
        {
            // Arrange
            var httpClient = _mockDataSetupHelper.SetupHttpClientMock("{\"rates\": {\"EUR\": 1.4}}");
            var cachedExchangeRate = new CachedExchangeRate { Timestamp = DateTime.UtcNow.AddMinutes(-15), Value = 1.2m };
            var mockCache = _mockDataSetupHelper.SetupCacheMock(cachedExchangeRate);

            var exchangeRateApiConfiguration = new ExchangeRateApiConfiguration
            {
                BaseUrl = "https://baseurl.com/api"
            };
            var loggerMock = new Mock<ILogger<ExchangeRateProviderService>>();

            var mockConfiguration = _mockDataSetupHelper.SetupConfigurationMock(exchangeRateApiConfiguration);

            var options = new DbContextOptionsBuilder<CurrencyExchangeDbContext>()
              .UseInMemoryDatabase(databaseName: "test_database")
              .Options;
            using var context = new CurrencyExchangeDbContext(options);
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
            context.ExchangeRates.Add(new ExchangeRateDTO { SourceCurrency = "GBP", TargetCurrency = "USD", Rate = 1.2m });
            context.SaveChanges();

            var service = _mockDataSetupHelper.CreateServiceWithMocks(context, httpClientMock: httpClient, cacheMock: mockCache, configurationMock: mockConfiguration, loggerMock: loggerMock);

            var exchangeRateModel = new ExchangeRateModel { SourceCurrency = "USD", TargetCurrency = "EUR" };

            // Act
            var result = await service.GetAndCacheExchangeRateAsync(exchangeRateModel);

            // Assert
            Assert.Equal(1.4m, result); // Should return the fetched rate
            mockCache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }


        [Fact]
        public async Task GetAndCacheExchangeRateAsync_FetchExchangeRateFromApi_UnsuccessfulResponse_ThrowsExceptionAndDoesNotCache()
        {
            // Arrange
            var httpClient = _mockDataSetupHelper.SetupHttpClientMock(null, HttpStatusCode.BadRequest); // Simulate an unsuccessful response
            var cachedExchangeRate = new CachedExchangeRate { Timestamp = DateTime.UtcNow.AddMinutes(-15), Value = 1.2m };
            var mockCache = _mockDataSetupHelper.SetupCacheMock(cachedExchangeRate);
            var service = _mockDataSetupHelper.CreateServiceWithMocks(httpClientMock: httpClient, cacheMock: mockCache);

            var exchangeRateModel = new ExchangeRateModel { SourceCurrency = "USD", TargetCurrency = "EUR" };

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => service.GetAndCacheExchangeRateAsync(exchangeRateModel));
            mockCache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetAndCacheExchangeRateAsync_FetchExchangeRateFromApi_ExceptionThrown_LogsError()
        {
            // Arrange
            var httpClient = _mockDataSetupHelper.SetupHttpClientMockForException(new Exception("Simulated exception"));
            var cachedExchangeRate = new CachedExchangeRate { Timestamp = DateTime.UtcNow.AddMinutes(-15), Value = 1.2m };
            var mockCache = _mockDataSetupHelper.SetupCacheMock(cachedExchangeRate);
            var loggerMock = new Mock<ILogger<ExchangeRateProviderService>>();
            var service = _mockDataSetupHelper.CreateServiceWithMocks(httpClientMock: httpClient, cacheMock: mockCache, loggerMock: loggerMock);

            var exchangeRateModel = new ExchangeRateModel { SourceCurrency = "USD", TargetCurrency = "EUR" };

            // Act
            await Assert.ThrowsAsync<Exception>(() => service.GetAndCacheExchangeRateAsync(exchangeRateModel));

            // Assert
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()
                ),
                Times.Exactly(2)
            );
        }

        [Fact]
        public async Task GetAndCacheExchangeRateAsync_CacheExchangeRateInDatabase_SuccessfulCache_ReturnsCachedRate()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CurrencyExchangeDbContext>()
                .UseInMemoryDatabase(databaseName: "test_database")
                .Options;

            var exchangeRateApiConfiguration = new ExchangeRateApiConfiguration
            {
                BaseUrl = "https://baseurl.com/api"
            };

            var mockConfiguration = _mockDataSetupHelper.SetupConfigurationMock(exchangeRateApiConfiguration);


            var exchangeRateModel = new ExchangeRateModel { SourceCurrency = "USD", TargetCurrency = "EUR" };

            using var context = new CurrencyExchangeDbContext(options);
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
            context.ExchangeRates.Add(new ExchangeRateDTO { SourceCurrency = "USD", TargetCurrency = "EUR", Rate = 1.2m });
            context.SaveChanges();

            var httpClient = _mockDataSetupHelper.SetupHttpClientMock("{\"rates\": {\"EUR\": 1.2}}");

            var service = _mockDataSetupHelper.CreateServiceWithMocks(configurationMock: mockConfiguration, context: context, httpClientMock: httpClient);


            // Act
            var result = await service.GetAndCacheExchangeRateAsync(exchangeRateModel);
            var cachedRate = context.ExchangeRates.FirstOrDefault();
            // Assert
            Assert.NotNull(cachedRate); // Should have cached the rate in the database
            Assert.Equal(cachedRate.Rate, result);


        }

        [Fact]
        public async Task GetAndCacheExchangeRateAsync_CacheExchangeRateInDatabase_ExceptionThrown_LogsError()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CurrencyExchangeDbContext>()
                .UseInMemoryDatabase(databaseName: "test_database")
                .Options;

            using var context = new CurrencyExchangeDbContext(options);
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            var exchangeRateApiConfiguration = new ExchangeRateApiConfiguration
            {
                BaseUrl = "https://baseurl.com/api"
            };

            var mockConfiguration = _mockDataSetupHelper.SetupConfigurationMock(exchangeRateApiConfiguration);

            var httpClient = _mockDataSetupHelper.SetupHttpClientMockForException(new Exception("Simulated exception"));
            var cachedExchangeRate = new CachedExchangeRate { Timestamp = DateTime.UtcNow.AddMinutes(-15), Value = 1.2m };
            var mockCache = _mockDataSetupHelper.SetupCacheMock(cachedExchangeRate);
            var loggerMock = new Mock<ILogger<ExchangeRateProviderService>>();
            var service = _mockDataSetupHelper.CreateServiceWithMocks(httpClientMock: httpClient, loggerMock: loggerMock);

            var exchangeRateModel = new ExchangeRateModel { SourceCurrency = "USD", TargetCurrency = "EUR" };

            // Act
            await Assert.ThrowsAsync<Exception>(() => service.GetAndCacheExchangeRateAsync(exchangeRateModel));

            // Assert

            var cachedRate = context.ExchangeRates.FirstOrDefault();
            Assert.Null(cachedRate); // Should not have cached the rate in the database

            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => true), // Match any object type
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()
                ),
                Times.Exactly(2)
            );
        }

        [Fact]
        public async Task FetchExchangeRateFromApi_SuccessfulResponse_ReturnsExchangeRate()
        {
            // Arrange
            var exchangeRateModel = new ExchangeRateModel { SourceCurrency = "USD", TargetCurrency = "EUR" };
            var responseBody = "{\"rates\": {\"EUR\": 1.4}}";
            var httpClient = _mockDataSetupHelper.SetupHttpClientMock(responseBody);

            var exchangeRateApiConfiguration = new ExchangeRateApiConfiguration
            {
                BaseUrl = "https://baseurl.com/api"
            };

            var mockConfiguration = _mockDataSetupHelper.SetupConfigurationMock(exchangeRateApiConfiguration);

            var service = _mockDataSetupHelper.CreateServiceWithMocks(httpClientMock: httpClient, configurationMock: mockConfiguration);


            // Act
            var result = await service.FetchExchangeRateFromApi(exchangeRateModel);

            // Assert
            Assert.Equal(1.4m, result);
        }

        [Fact]
        public async Task FetchExchangeRateFromApi_UnsuccessfulResponse_ThrowsException()
        {
            // Arrange
            var exchangeRateModel = new ExchangeRateModel { SourceCurrency = "USD", TargetCurrency = "EUR" };
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound // Simulating an unsuccessful response
            };
            var httpClient = _mockDataSetupHelper.SetupHttpClientMockForException(new Exception("Exception thrown"));

            var exchangeRateApiConfiguration = new ExchangeRateApiConfiguration
            {
                BaseUrl = "https://baseurl.com/api"
            };

            var mockConfiguration = _mockDataSetupHelper.SetupConfigurationMock(exchangeRateApiConfiguration);

            var service = _mockDataSetupHelper.CreateServiceWithMocks(httpClientMock: httpClient, configurationMock: mockConfiguration);

            // Assert and Act
            await Assert.ThrowsAsync<Exception>(() => service.FetchExchangeRateFromApi(exchangeRateModel));
        }

        

    }
}
