namespace BTCPayServer.Controllers.NewApi.Models
{
    public class StoreRatePreviewResult : StoreRateResult
    {
        public string CurrencyPair { get; set; }
        public decimal? Rate { get; set; }
        public IEnumerable<string> Errors { get; set; }
    }
}