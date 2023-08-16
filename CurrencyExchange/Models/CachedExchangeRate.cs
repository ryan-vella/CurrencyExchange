namespace CurrencyExchange.Models
{
    public class CachedExchangeRate
    {
        public string? Name { get; set; }
        public decimal Value { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
