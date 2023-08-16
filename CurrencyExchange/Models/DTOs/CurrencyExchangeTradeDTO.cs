using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.ComponentModel.DataAnnotations.Schema;

namespace CurrencyExchange.Models.DTOs
{
    public class CurrencyExchangeTradeDTO
    {
        public int Id { get; set; }
        public string? ClientId { get; set; }
        [Column(TypeName = "decimal(18,6)")]
        public decimal Amount { get; set; }
        public string? SourceCurrency { get; set; }
        public string? TargetCurrency { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
