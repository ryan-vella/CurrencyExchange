using Microsoft.AspNetCore.Mvc;
using static System.Net.Mime.MediaTypeNames;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.ComponentModel.DataAnnotations.Schema;

namespace CurrencyExchange.Models.DTOs
{
    public class ExchangeRateDTO
    {
        public int Id { get; set; }
        public string? SourceCurrency { get; set; }
        public string? TargetCurrency { get; set; }
        [Column(TypeName = "decimal(18,6)")]
        public decimal Rate { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
