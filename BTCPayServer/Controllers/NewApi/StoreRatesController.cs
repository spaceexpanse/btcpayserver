using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Rating;
using BTCPayServer.Security;
using BTCPayServer.Services.Rates;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Controllers.NewApi
{
    [ApiController]
    [IncludeInOpenApiDocs]
    [Route("api/v0.1/stores/{storeId}/rates")]
    [Authorize(Policy = Policies.CanModifyStoreSettings.Key)]
    [Authorize()]
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
        public async Task<ActionResult<IEnumerable<StoreRateResult>>> GetStoreRates(string[] currencyPair)
        {
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

            var blob = HttpContext.GetStoreData().GetStoreBlob();
            var rules = blob.GetRateRules(_btcPayNetworkProvider);
            var rateTasks = _rateProviderFactory.FetchRates(parsedCurrencyPairs, rules);
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

            return Ok(result);
        }

        [HttpGet("{baseCurrency}/{toCurrency?}")]
        public async Task<ActionResult<IEnumerable<StoreRateResult>>> GetStoreRatesForCurrency(string baseCurrency,
            string[] toCurrency)
        {
            var parsedCurrencyPairs = new HashSet<CurrencyPair>();
            foreach (var i in toCurrency)
            {
                var pair = $"{baseCurrency}_{i}";
                CurrencyPair.TryParse(pair, out var currencyPairParsed);
                parsedCurrencyPairs.Add(currencyPairParsed);
            }

            var blob = HttpContext.GetStoreData().GetStoreBlob();
            var rules = blob.GetRateRules(_btcPayNetworkProvider);
            var rateTasks = _rateProviderFactory.FetchRates(parsedCurrencyPairs, rules);
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

            return Ok(result);
        }
    }

    [ApiController]
    [IncludeInOpenApiDocs]
    [Route("api/v0.1/stores/{storeId}/rates/configuration")]
    [Authorize(Policy = Policies.CanModifyStoreSettings.Key)]
    [Authorize()]
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
        public ActionResult<Dictionary<string, CoinAverageExchange>> GetAvailableSources()
        {
            return Ok(_rateProviderFactory.RateProviderFactory.GetSupportedExchanges());
        }

        [HttpPut("")]
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


            var rateTasks = _rateProviderFactory.FetchRates(parsedCurrencyPairs, rules);
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
                    .Keys
                    .Any(s =>
                        s.Equals(configuration.PreferredSource,
                            StringComparison.InvariantCultureIgnoreCase)))
            {
                ModelState.AddModelError(nameof(configuration.PreferredSource),
                    $"Unsupported source ({configuration.PreferredSource})");
            }

            if (RateRules.TryParse(configuration.Script, out var rules, out _))
            {
                configuration.Script = rules.ToString();
            }


            return ModelState.ErrorCount > 0;
        }

        private static void PopulateBlob(StoreRateConfiguration configuration, StoreBlob storeBlob)
        {
            storeBlob.PreferredExchange = configuration.PreferredSource;
            storeBlob.Spread = configuration.Spread;
            storeBlob.RateScripting = configuration.UseScript;
            storeBlob.RateScript = configuration.Script;
        }
    }

    public class StoreRateResult
    {
        public string CurrencyPair { get; set; }
        public decimal Rate { get; set; }
    }

    public class StoreRatePreviewResult : StoreRateResult
    {
        public string CurrencyPair { get; set; }
        public decimal? Rate { get; set; }
        public IEnumerable<string> Errors { get; set; }
    }

    public class StoreRateConfiguration
    {
        [Range(0.0, 100.0)] public decimal Spread { get; set; }
        public string PreferredSource { get; set; }

        [MaxLength(2000)]
        [RateScriptValidator]
        public string Script { get; set; }

        public bool UseScript { get; set; }
    }

    public class RateScriptValidator : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null)
            {
                return ValidationResult.Success;
            }

            if (RateRules.TryParse(value.ToString(), out _, out var errors)) return ValidationResult.Success;
            errors = errors ?? new List<RateRulesErrors>();
            var errorString = string.Join(", ", errors.ToArray());
            return new ValidationResult($"Parsing error ({errorString})");
        }
    }
}
