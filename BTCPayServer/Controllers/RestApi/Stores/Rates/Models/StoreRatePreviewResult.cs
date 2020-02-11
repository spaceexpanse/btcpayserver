using System.Collections.Generic;

namespace BTCPayServer.Controllers.RestApi.Models
{
    public class StoreRatePreviewResult
    {
        public string CurrencyPair { get; set; }
        public decimal? Rate { get; set; }
        public IEnumerable<string> Errors { get; set; }
    }
}
