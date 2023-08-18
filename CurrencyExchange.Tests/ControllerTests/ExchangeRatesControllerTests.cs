using CurrencyExchange.Controllers;
using CurrencyExchange.Interfaces;
using CurrencyExchange.Models;
using CurrencyExchange.Models.DTOs;
using CurrencyExchange.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using Moq;
using System.Net;
using Xunit;


namespace CurrencyExchange.Tests.ControllerTests
{
    public class ExchangeRatesControllerTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly MockDataSetupHelper _mockDataSetupHelper;
        private Mock<IExchangeRateProviderService> _exchangeRateProviderService { get; }
        ExchangeRatesController _controller { get; }

        public ExchangeRatesControllerTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _mockDataSetupHelper = new MockDataSetupHelper();
            _exchangeRateProviderService = new Mock<IExchangeRateProviderService>();
            _controller = new ExchangeRatesController(_exchangeRateProviderService.Object);
        }

        [Fact]
        public async Task GetLatestExchangeRate_ReturnsOkResult()
        {
            // Arrange
            var exchangeRateModel = new ExchangeRateModel { SourceCurrency = "EUR", TargetCurrency = "USD" };
            var result = 1.4m;

            _exchangeRateProviderService.Setup(service => service.GetLatestExchangeRateAsync(exchangeRateModel)).ReturnsAsync(result);

            // Act
            var actionResult = await _controller.GetLatestExchangeRate(exchangeRateModel);

            // Assert
            actionResult.Should().BeOfType<ActionResult<decimal>>();
            actionResult.Result.Should().BeOfType<OkObjectResult>();

            var okObjectResult = actionResult.Result as OkObjectResult;
            okObjectResult.Should().NotBeNull();
            okObjectResult.StatusCode.Should().Be((int)HttpStatusCode.OK);
            okObjectResult.Value.Should().Be(result);
        }

        [Fact]
        public async Task GetLatestExchangeRate_InvalidRequest_ReturnsBadRequest()
        {
            // Arrange
            var exchangeRateModel = new ExchangeRateModel { SourceCurrency = "EUR", TargetCurrency = "" }; // Invalid model
            _exchangeRateProviderService.Setup(service => service.GetLatestExchangeRateAsync(exchangeRateModel))
                .ThrowsAsync(new ArgumentException("Invalid currency.")); // Simulating invalid input

            // Act
            var actionResult = await _controller.GetLatestExchangeRate(exchangeRateModel);

            // Assert
            actionResult.Should().BeOfType<ActionResult<decimal>>();
            actionResult.Result.Should().BeOfType<BadRequestObjectResult>();

            var badRequestObjectResult = actionResult.Result as BadRequestObjectResult;
            badRequestObjectResult.Should().NotBeNull();
            badRequestObjectResult.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
            badRequestObjectResult.Value.Should().Be("Invalid currency.");
        }

        // Other test methods...
    }
}
