namespace CurrencyExchange.Models
{
    public class ExchangeRateApiResponse
    {
        public string? Base { get; set; }
        public DateTime Date { get; set; }
        public Dictionary<string, decimal>? Rates { get; set; }
    }
}
