using CurrencyExchange.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CurrencyExchange.DbContexts
{
    public class CurrencyExchangeDbContext : DbContext
    {
        public CurrencyExchangeDbContext(DbContextOptions<CurrencyExchangeDbContext> options)
            : base(options)
        {
        }

        public CurrencyExchangeDbContext() { }

        public DbSet<CurrencyExchangeTradeDTO> CurrencyExchangeTrades { get; set; }
        public DbSet<ExchangeRateDTO> ExchangeRates { get; set; }
    }
}