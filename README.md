Currency Exchange Services
==========================

This repository contains services for managing currency exchange rates and performing trades using ASP.NET Core and Entity Framework Core.

ExchangeRateProviderService
---------------------------

The `ExchangeRateProviderService` is responsible for fetching and caching exchange rates from an external API. It utilizes Redis caching to optimize rate retrieval and provides the latest exchange rate between two currencies.

### Usage

1.  **Setup**
    *   Ensure you have the required dependencies: .NET Core, Entity Framework Core, Redis Server.
    *   Configure your Redis connection in the `appsettings.json` file. The file is currently named appsettings-dummy.json, remove -dummy from the file name and replace database connection string, Redis connection and API Key to retrieve currencies.
2.  **Exchange Rate Retrieval**
    *   The `GetLatestExchangeRateAsync` method fetches the latest exchange rate between two currencies.
    *   Exchange rates are cached in Redis for improved performance.

TradeService
------------

The `TradeService` enables clients to perform currency exchange trades while adhering to trade limits.

### Usage

1.  **Setup**
    *   Make sure you've set up the dependencies for the Exchange Rate service and the Redis cache.
    *   Configure your database connection in the `appsettings.json` file.
2.  **Perform Trade**
    *   The `PerformTradeAsync` method allows clients to perform currency exchange trades.
    *   It fetches the exchange rate using the `ExchangeRateRetrieverService`.
    *   Trades are stored in the database, and the trade count for each client is incremented in the Redis cache.
    *   Trade limits of 10 trades per hour per client are enforced.

How to Use
----------

1.  Clone the repository: `git clone https://github.com/ryan-vella/CurrencyExchange.git`
2.  Navigate to the solution directory: `cd CurrencyExchangeSolution`
3.  Configure your database connection and Redis cache settings in the `appsettings.json` file.
4.  Run migrations: `dotnet ef database update`
5.  Run the application: `dotnet run`
