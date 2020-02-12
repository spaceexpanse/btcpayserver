using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Controllers.RestApi.Models;
using BTCPayServer.Data;
using BTCPayServer.Rating;
using BTCPayServer.Security;
using BTCPayServer.Services.Rates;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Controllers.RestApi
{
    [ApiController]
    [IncludeInOpenApiDocs]
    [Route("api/v0.1/stores/{storeId}/rates/configuration")]
    [Authorize(Policy = Policies.CanModifyStoreSettings.Key, AuthenticationSchemes = AuthenticationSchemes.ApiKey)]
    public class StoreRateConfigurationController : ControllerBase
    {
        private readonly RateFetcher _rateProviderFactory;
        private readonly BTCPayNetworkProvider _btcPayNetworkProvider;
        private readonly StoreRepository _storeRepository;

        public StoreRateConfigurationController(
            RateFetcher rateProviderFactory,
            BTCPayNetworkProvider btcPayNetworkProvider,
            StoreRepository storeRepository)
        {
            _rateProviderFactory = rateProviderFactory;
            _btcPayNetworkProvider = btcPayNetworkProvider;
            _storeRepository = storeRepository;
        }

        [HttpGet("")]
        [Authorize(Policy = Policies.CanModifyStoreSettings.Key, AuthenticationSchemes = AuthenticationSchemes.ApiKey)]
        public ActionResult<StoreRateConfiguration> GetStoreRateConfiguration()
        {
            var data = HttpContext.GetStoreData();
            var blob = data.GetStoreBlob();

            return Ok(new StoreRateConfiguration()
            {
                Script = blob.RateScript,
                Spread = blob.Spread,
                UseScript = blob.RateScripting,
                PreferredSource = blob.PreferredExchange
            });
        }

        [HttpGet("sources")]
        [Authorize(Policy = Policies.CanModifyStoreSettings.Key, AuthenticationSchemes = AuthenticationSchemes.ApiKey)]
        public ActionResult<Dictionary<string, AvailableRateProvider>> GetAvailableSources()
        {
            return Ok(_rateProviderFactory.RateProviderFactory.GetSupportedExchanges());
        }

        [HttpPut("")]
        [Authorize(Policy = Policies.CanModifyStoreSettings.Key, AuthenticationSchemes = AuthenticationSchemes.ApiKey)]
        public async Task<ActionResult<StoreRateConfiguration>> UpdateStoreRateConfiguration(
            StoreRateConfiguration configuration)
        {
            var storeData = HttpContext.GetStoreData();
            var blob = storeData.GetStoreBlob();
            if (!ValidateAndSanitizeConfiguration(configuration, blob))
            {
                return BadRequest(ModelState);
            }

            PopulateBlob(configuration, blob);

            storeData.SetStoreBlob(blob);

            await _storeRepository.UpdateStore(storeData);


            return GetStoreRateConfiguration();
        }

        [HttpPost("preview")]
        [Authorize(Policy = Policies.CanModifyStoreSettings.Key, AuthenticationSchemes = AuthenticationSchemes.ApiKey)]
        public async Task<ActionResult<IEnumerable<StoreRatePreviewResult>>> PreviewUpdateStoreRateConfiguration(
            StoreRateConfiguration configuration, [FromQuery] string[] currencyPair)
        {
            var data = HttpContext.GetStoreData();
            var blob = data.GetStoreBlob();
            var parsedCurrencyPairs = new HashSet<CurrencyPair>();
            
            
            foreach (var pair in currencyPair)
            {
                if (!CurrencyPair.TryParse(pair, out var currencyPairParsed))
                {
                    ModelState.AddModelError(nameof(currencyPair),
                        $"Invalid currency pair '{pair}' (it should be formatted like BTC_USD,BTC_CAD)");
                    continue;
                }

                parsedCurrencyPairs.Add(currencyPairParsed);
            }

            if (!ValidateAndSanitizeConfiguration(configuration, blob))
            {
                return BadRequest(ModelState);
            }

            PopulateBlob(configuration, blob);

            var rules = blob.GetRateRules(_btcPayNetworkProvider);


            var rateTasks = _rateProviderFactory.FetchRates(parsedCurrencyPairs, rules, CancellationToken.None);
            await Task.WhenAll(rateTasks.Values);
            var result = new List<StoreRatePreviewResult>();
            foreach (var rateTask in rateTasks)
            {
                var rateTaskResult = rateTask.Value.Result;

                result.Add(new StoreRatePreviewResult()
                {
                    CurrencyPair = rateTask.Key.ToString(),
                    Errors = rateTaskResult.Errors.Select(errors => errors.ToString()),
                    Rate = rateTaskResult.Errors.Any() ? (decimal?)null : rateTaskResult.BidAsk.Bid
                });
            }

            return Ok(result);
        }

        private bool ValidateAndSanitizeConfiguration(StoreRateConfiguration configuration, StoreBlob storeBlob)
        {
            if (configuration.UseScript && string.IsNullOrEmpty(configuration.Script))
            {
                configuration.Script = storeBlob.GetDefaultRateRules(_btcPayNetworkProvider).ToString();
            }

            if (!string.IsNullOrEmpty(configuration.PreferredSource) &&
                !_rateProviderFactory
                    .RateProviderFactory
                    .GetSupportedExchanges()
                    .Any(s =>
                        s.Id.Equals(configuration.PreferredSource,
                            StringComparison.InvariantCultureIgnoreCase)))
            {
                ModelState.AddModelError(nameof(configuration.PreferredSource),
                    $"Unsupported source ({configuration.PreferredSource})");
            }

            if (RateRules.TryParse(configuration.Script, out var rules, out _))
            {
                configuration.Script = rules.ToString();
            }


            return ModelState.ErrorCount == 0;
        }

        private static void PopulateBlob(StoreRateConfiguration configuration, StoreBlob storeBlob)
        {
            storeBlob.PreferredExchange = configuration.PreferredSource;
            storeBlob.Spread = configuration.Spread;
            storeBlob.RateScripting = configuration.UseScript;
            storeBlob.RateScript = configuration.Script;
        }
    }
}
