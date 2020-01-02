using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Controllers.RestApi.Models;
using BTCPayServer.Data;
using BTCPayServer.Rating;
using BTCPayServer.Security;
using BTCPayServer.Services.Rates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Controllers.RestApi
{
    [ApiController]
    [IncludeInOpenApiDocs]
    [Route("api/v1/stores/{storeId}/rates")]
    [Authorize(Policy = Policies.CanModifyStoreSettings.Key, AuthenticationSchemes = AuthenticationSchemes.ApiKey)]
    public class StoreRatesController : ControllerBase
    {
        private readonly BTCPayNetworkProvider _btcPayNetworkProvider;
        private readonly RateFetcher _rateProviderFactory;

        public StoreRatesController(
            BTCPayNetworkProvider btcPayNetworkProvider,
            RateFetcher rateProviderFactory)
        {
            _btcPayNetworkProvider = btcPayNetworkProvider;
            _rateProviderFactory = rateProviderFactory;
        }

        [HttpGet("")]
        public async Task<ActionResult<IEnumerable<StoreRateResult>>> GetStoreRates([FromQuery] string[] currencyPairs)
        {
            var store = HttpContext.GetStoreData();
            var parsedCurrencyPairs = new HashSet<CurrencyPair>();
            if (currencyPairs == null || !currencyPairs.Any() )
            {
                currencyPairs = store.GetStoreBlob().DefaultCurrencyPairs.Select(pair => pair.ToString()).ToArray();
            }

            
            foreach (var pair in currencyPairs)
            {
                if (!CurrencyPair.TryParse(pair, out var currencyPairParsed))
                {
                    ModelState.AddModelError(nameof(currencyPairs),
                        $"Invalid currency pair '{pair}' (it should be formatted like BTC_USD,BTC_CAD)");
                    continue;
                }

                parsedCurrencyPairs.Add(currencyPairParsed);
            }
            
            return Ok(await GetRates(parsedCurrencyPairs));
        }

        
        [HttpGet("{baseCurrency}/{toCurrency}")]
        public async Task<ActionResult<IEnumerable<StoreRateResult>>> GetStoreRatesForCurrency(string baseCurrency,string toCurrency)
        {
            var result = await GetRates(new HashSet<CurrencyPair>()
            {
                CurrencyPair.Parse($"{baseCurrency}_{toCurrency}")
            });
            if (result.Any())
            {
                return Ok(result.First());
            }

            return NotFound();

        }
        
        [HttpGet("{baseCurrency}")]
        public async Task<ActionResult<IEnumerable<StoreRateResult>>> GetStoreRatesForCurrency(string baseCurrency,
            [FromQuery] string[] toCurrency)
        {
            var parsedCurrencyPairs = new HashSet<CurrencyPair>();
            foreach (var i in toCurrency)
            {
                var pair = $"{baseCurrency}_{i}";
                CurrencyPair.TryParse(pair, out var currencyPairParsed);
                parsedCurrencyPairs.Add(currencyPairParsed);
            }

            return Ok(await GetRates(parsedCurrencyPairs));
        }

        private async Task<List<StoreRateResult>> GetRates(HashSet<CurrencyPair> parsedCurrencyPairs)
        {
            var blob = HttpContext.GetStoreData().GetStoreBlob();
            var rules = blob.GetRateRules(_btcPayNetworkProvider);
            var rateTasks = _rateProviderFactory.FetchRates(parsedCurrencyPairs, rules, CancellationToken.None);
            await Task.WhenAll(rateTasks.Values);
            var result = new List<StoreRateResult>();
            foreach (var rateTask in rateTasks)
            {
                var rateTaskResult = rateTask.Value.Result;
                if (rateTaskResult.Errors.Any())
                {
                    continue;
                }

                result.Add(new StoreRateResult()
                {
                    CurrencyPair = rateTask.Key.ToString(),
                    Rate = rateTaskResult.BidAsk.Bid
                });
            }

            return result;
        }
    }


}
