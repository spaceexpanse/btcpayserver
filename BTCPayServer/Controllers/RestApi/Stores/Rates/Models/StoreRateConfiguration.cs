using System.ComponentModel.DataAnnotations;
using BTCPayServer.Validation;

namespace BTCPayServer.Controllers.RestApi.Models
{
    public class StoreRateConfiguration
    {
        [Range(0.0, 100.0)] public decimal Spread { get; set; }
        public string PreferredSource { get; set; }

        [MaxLength(2000)]
        [RateScriptValidator]
        public string Script { get; set; }

        public bool UseScript { get; set; }
    }
}
