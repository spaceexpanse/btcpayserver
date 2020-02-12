using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Controllers.RestApi.Models;
using BTCPayServer.Data;
using BTCPayServer.Payments;
using BTCPayServer.Security;
using BTCPayServer.Services.Stores;
using BTCPayServer.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NBXplorer.DerivationStrategy;
using NSwag.Annotations;

namespace BTCPayServer.Controllers.RestApi
{
    [ApiController]
    [IncludeInOpenApiDocs]
    [OpenApiTags("Store Payment Methods", "On-chain Payment Methods")]
    [Route("api/v1/stores/{storeId}/payment-methods/" + nameof(PaymentTypes.BTCLike))]
    [Authorize(Policy = Policies.CanModifyStoreSettings.Key, AuthenticationSchemes = AuthenticationSchemes.ApiKey)]
    public class StoreBtcLikePaymentMethodController : ControllerBase
    {
        private StoreData Store => HttpContext.GetStoreData();
        private readonly StoreRepository _storeRepository;
        private readonly BTCPayNetworkProvider _btcPayNetworkProvider;
        private readonly BTCPayWalletProvider _walletProvider;

        public StoreBtcLikePaymentMethodController(
            StoreRepository storeRepository,
            BTCPayNetworkProvider btcPayNetworkProvider,
            BTCPayWalletProvider walletProvider)
        {
            _storeRepository = storeRepository;
            _btcPayNetworkProvider = btcPayNetworkProvider;
            _walletProvider = walletProvider;
        }

        /// <param name="enabledOnly">Whether to get payment methods that are enabled only </param>
        [HttpGet("")]
        [OpenApiOperation("Get derivation schemes", "Get all configured BTCLike payment methods for this store")]
        [SwaggerResponse(StatusCodes.Status200OK, typeof(StoreBtcLikePaymentMethod[]),
            Description = "All BTCLike payment method configurations on this store")]
        public ActionResult<IEnumerable<StoreBtcLikePaymentMethod>> GetBtcLikePaymentMethods(
            [FromQuery] bool enabledOnly = false)
        {
            var blob = Store.GetStoreBlob();
            var excludedPaymentMethods = blob.GetExcludedPaymentMethods();
            var defaultPaymentId = Store.GetDefaultPaymentId(_btcPayNetworkProvider);
            return Ok(Store.GetSupportedPaymentMethods(_btcPayNetworkProvider)
                .Where((method) => method.PaymentId.PaymentType == PaymentTypes.BTCLike)
                .OfType<DerivationSchemeSettings>()
                .Select(strategy =>
                    new StoreBtcLikePaymentMethod(strategy, !excludedPaymentMethods.Match(strategy.PaymentId), defaultPaymentId == strategy.PaymentId))
                .Where((result) => !enabledOnly || result.Enabled)
                .ToList()
            );
        }

        /// <param name="cryptoCode">Crypto Code</param>
        /// <returns></returns>
        [HttpGet("{cryptoCode}")]
        [OpenApiOperation("Get derivation scheme", "Get configured BTCLike payment method by crypto code for this store")]
        [SwaggerResponse(StatusCodes.Status200OK, typeof(StoreBtcLikePaymentMethod),
            Description = "BTCLike payment method configuration on this store")]
        [SwaggerResponse(StatusCodes.Status404NotFound, null,
            Description = "Crypto code not available")]
        public ActionResult<StoreBtcLikePaymentMethod> GetBtcLikePaymentMethod(string cryptoCode)
        {
            if (!GetCryptoCodeWallet(cryptoCode, out var network, out var wallet))
            {
                return NotFound();
            }

            return Ok(GetExistingBtcLikePaymentMethod(cryptoCode));
        }

        [HttpGet("{cryptoCode}/preview")]
        [OpenApiOperation("Preview addresses of the current derivation scheme", "Preview addresses of the current derivation scheme by crypto code for this store")]
        [SwaggerResponse(StatusCodes.Status200OK, typeof(StoreBtcLikePaymentMethodPreviewResult),
            Description = "List of addresses with key paths")]
        [SwaggerResponse(StatusCodes.Status404NotFound, null,
            Description = "Crypto code not available")] 
        [SwaggerResponse(StatusCodes.Status400BadRequest, null,
            Description = "Not configured")]
        public ActionResult<StoreBtcLikePaymentMethodPreviewResult> GetBtcLikePaymentAddressPreview(string cryptoCode,
            int offset = 0, int amount = 10)
        {
            if (!GetCryptoCodeWallet(cryptoCode, out var network, out var wallet))
            {
                return NotFound();
            }

            var paymentMethod = GetExistingBtcLikePaymentMethod(cryptoCode);
            if (string.IsNullOrEmpty(paymentMethod.DerivationScheme))
            {
                return BadRequest();
            }

            var strategy = DerivationSchemeSettings.Parse(paymentMethod.DerivationScheme, network);
            var deposit = new NBXplorer.KeyPathTemplates(null).GetKeyPathTemplate(DerivationFeature.Deposit);

            var line = strategy.AccountDerivation.GetLineFor(deposit);
            var result = new StoreBtcLikePaymentMethodPreviewResult();
            for (var i = offset; i < amount; i++)
            {
                var address = line.Derive((uint)i);
                result.Addresses.Add(
                    new StoreBtcLikePaymentMethodPreviewResult.StoreBtcLikePaymentMethodPreviewResultAddressItem()
                    {
                        KeyPath =  deposit.GetKeyPath((uint)i).ToString(),
                        Address = address.ScriptPubKey.GetDestinationAddress(strategy.Network.NBitcoinNetwork)
                            .ToString()
                    });
            }

            return Ok(result);
        }

        [HttpPost("{cryptoCode}/preview")]
        [OpenApiOperation("Preview addresses of the proposed derivation scheme", "Preview addresses of the proposed derivation scheme by crypto code for this store")]
        [SwaggerResponse(StatusCodes.Status200OK, typeof(StoreBtcLikePaymentMethodPreviewResult),
            Description = "List of addresses with key paths")]
        [SwaggerResponse(StatusCodes.Status404NotFound, null,
            Description = "Crypto code not available")] 
        [SwaggerResponse(StatusCodes.Status422UnprocessableEntity, typeof(ModelStateDictionary),
            Description = "Validation error")]
        public ActionResult<StoreBtcLikePaymentMethodPreviewResult> GetBtcLikePaymentAddressPreview(string cryptoCode,
            [FromBody] StoreBtcLikePaymentMethod paymentMethod,
            int offset = 0, int amount = 10)
        {
            if (!GetCryptoCodeWallet(cryptoCode, out var network, out var wallet))
            {
                return NotFound();
            }
            try
            {
                var strategy = DerivationSchemeSettings.Parse(paymentMethod.DerivationScheme, network);
                var deposit = new NBXplorer.KeyPathTemplates(null).GetKeyPathTemplate(DerivationFeature.Deposit);
                var line = strategy.AccountDerivation.GetLineFor(deposit);
                var result = new StoreBtcLikePaymentMethodPreviewResult();
                for (var i = offset; i < amount; i++)
                {
                    var derivation = line.Derive((uint)i);
                    result.Addresses.Add(
                        new StoreBtcLikePaymentMethodPreviewResult.StoreBtcLikePaymentMethodPreviewResultAddressItem()
                        {
                            KeyPath = deposit.GetKeyPath((uint)i).ToString(),
                            Address = strategy.Network.NBXplorerNetwork.CreateAddress(strategy.AccountDerivation,
                                line.KeyPathTemplate.GetKeyPath((uint)i),
                                derivation.ScriptPubKey).ToString()
                        });
                }

                return Ok(result);
            }

            catch
            {
                ModelState.AddModelError(nameof(StoreBtcLikePaymentMethod.DerivationScheme),
                    "Invalid Derivation Scheme");
                return UnprocessableEntity(ModelState);
            }
        }

        [HttpPut("{cryptoCode}")]
        [OpenApiOperation("Update derivation scheme", "Update derivation scheme for crypto code for this store")]
        [SwaggerResponse(StatusCodes.Status200OK, typeof(StoreBtcLikePaymentMethod),
            Description = "BTCLike payment method configuration on this store")]
        [SwaggerResponse(StatusCodes.Status404NotFound, null,
            Description = "Crypto code not available")] 
        [SwaggerResponse(StatusCodes.Status422UnprocessableEntity, typeof(ModelStateDictionary),
            Description = "Validation error")]
        public async Task<ActionResult<StoreBtcLikePaymentMethod>> UpdateBtcLikePaymentMethod(string cryptoCode,
            [FromBody] StoreBtcLikePaymentMethod paymentMethod)
        {
            var id = new PaymentMethodId(cryptoCode, PaymentTypes.BTCLike);

            if (!GetCryptoCodeWallet(cryptoCode, out var network, out var wallet))
            {
                return NotFound();
            }

            try
            {
                var store = Store;
                var storeBlob = store.GetStoreBlob();
                var strategy = DerivationSchemeSettings.Parse(paymentMethod.DerivationScheme, network);
                if (strategy != null)
                    await wallet.TrackAsync(strategy.AccountDerivation);
                store.SetSupportedPaymentMethod(id, strategy);
                storeBlob.SetExcluded(id, !paymentMethod.Enabled);
                store.SetStoreBlob(storeBlob);
                await _storeRepository.UpdateStore(store);
                return Ok(GetExistingBtcLikePaymentMethod(cryptoCode, store));
            }
            catch
            {
                ModelState.AddModelError(nameof(StoreBtcLikePaymentMethod.DerivationScheme),
                    "Invalid Derivation Scheme");
                return UnprocessableEntity(ModelState);
            }
        }

        private bool GetCryptoCodeWallet(string cryptoCode, out BTCPayNetwork network, out BTCPayWallet wallet)
        {
            network = _btcPayNetworkProvider.GetNetwork<BTCPayNetwork>(cryptoCode);
            wallet = network != null ? _walletProvider.GetWallet(network) : null;
            return wallet != null;
        }
        private StoreBtcLikePaymentMethod GetExistingBtcLikePaymentMethod(string cryptoCode, StoreData store = null)
        {
            store ??= Store;
            var storeBlob = store.GetStoreBlob();
            var defaultPaymentMethod = store.GetDefaultPaymentId(_btcPayNetworkProvider);
            var id = new PaymentMethodId(cryptoCode, PaymentTypes.BTCLike);
            var paymentMethod = store
                .GetSupportedPaymentMethods(_btcPayNetworkProvider)
                .OfType<DerivationSchemeSettings>()
                .FirstOrDefault(method => method.PaymentId == id);

            var excluded = storeBlob.IsExcluded(paymentMethod.PaymentId);
            return paymentMethod == null
                ? new StoreBtcLikePaymentMethod(cryptoCode,  string.Empty,!excluded, defaultPaymentMethod == new PaymentMethodId(cryptoCode, PaymentTypes.BTCLike))
                : new StoreBtcLikePaymentMethod(paymentMethod, !excluded, defaultPaymentMethod == paymentMethod.PaymentId);
        }
    }
}
