namespace CurrencyExchange.Models
{
    public class TradeRequestModel
    {
        public string? ClientId { get; set; }
        public decimal Amount { get; set; }
        public string? SourceCurrency { get; set; }
        public string? TargetCurrency { get; set; }
    }
}
