using CurrencyExchange.Controllers;
using CurrencyExchange.Interfaces;
using CurrencyExchange.Models;
using CurrencyExchange.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CurrencyExchange.Tests.ControllerTests
{
    public class TradeControllerTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly MockDataSetupHelper _mockDataSetupHelper;
        private Mock<ITradeService> _tradeServiceMock;
        TradeController _controller;

        public TradeControllerTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _mockDataSetupHelper = new MockDataSetupHelper();
            _tradeServiceMock = new Mock<ITradeService>();
            _controller = new TradeController(_tradeServiceMock.Object);
        }

        [Fact]
        public async Task PerformTrade_SuccessfulTrade_ReturnsOkResult()
        {
            // Arrange
            var tradeRequestModel = new TradeRequestModel { ClientId = "Client1", SourceCurrency = "EUR", TargetCurrency = "USD", Amount = 100m };
            decimal expectedConvertedAmount = 140m;

            _tradeServiceMock.Setup(service => service.PerformTradeAsync(tradeRequestModel)).ReturnsAsync(expectedConvertedAmount);

            // Act
            var actionResult = await _controller.PerformTrade(tradeRequestModel);

            // Assert
            actionResult.Should().BeOfType<OkObjectResult>();

            var okObjectResult = actionResult as OkObjectResult;
            okObjectResult.Should().NotBeNull();
            okObjectResult.StatusCode.Should().Be((int)HttpStatusCode.OK);

            var resultValue = okObjectResult?.Value;

            if (resultValue is IDictionary<string, object> dictionary)
            {
                // Assuming the JSON object has a "ConvertedAmount" property
                if (dictionary.TryGetValue("ConvertedAmount", out var convertedAmountObj) &&
                    convertedAmountObj is decimal convertedAmount)
                {
                    var tuple = Tuple.Create("ConvertedAmount", convertedAmount);
                    var expectedTuple = Tuple.Create("ConvertedAmount", 140m); // Adjust the expected value accordingly

                    var actualTuple = Tuple.Create("ConvertedAmount", convertedAmount);

                    Assert.Equal(expectedTuple, actualTuple);
                }

            }
          
        }

        [Fact]
        public async Task PerformTrade_TradeError_ReturnsInternalServerError()
        {
            // Arrange
            var tradeRequestModel = new TradeRequestModel { };
            string expectedErrorMessage = "Trade error message";

            _tradeServiceMock.Setup(service => service.PerformTradeAsync(tradeRequestModel))
                .ThrowsAsync(new Exception(expectedErrorMessage));

            // Act
            var actionResult = await _controller.PerformTrade(tradeRequestModel);

            // Assert
            actionResult.Should().BeOfType<ObjectResult>();

            var objectResult = actionResult as ObjectResult;
            objectResult.Should().NotBeNull();
            objectResult.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        }
    }
}
