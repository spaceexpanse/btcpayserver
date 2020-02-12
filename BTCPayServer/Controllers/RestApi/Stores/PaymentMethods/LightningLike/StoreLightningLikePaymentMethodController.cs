using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Configuration;
using BTCPayServer.Controllers.RestApi.Models;
using BTCPayServer.Data;
using BTCPayServer.HostedServices;
using BTCPayServer.Lightning;
using BTCPayServer.Payments;
using BTCPayServer.Payments.Lightning;
using BTCPayServer.Security;
using BTCPayServer.Security.APIKeys;
using BTCPayServer.Services.Stores;
using BTCPayServer.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Controllers.RestApi
{
    [ApiController]
    [IncludeInOpenApiDocs]
    [Route("api/v1/stores/{storeId}/payment-methods/" + nameof(PaymentTypes.LightningLike))]
    [Authorize(Policy = Policies.CanModifyStoreSettings.Key, AuthenticationSchemes = AuthenticationSchemes.ApiKey)]
    public class StoreLightningLikePaymentMethodController : ControllerBase
    {
        private StoreData Store => HttpContext.GetStoreData();
        private readonly StoreRepository _storeRepository;
        private readonly BTCPayServerOptions _btcPayServerOptions;
        private readonly BTCPayNetworkProvider _btcPayNetworkProvider;
        private readonly CssThemeManager _cssThemeManager;

        public StoreLightningLikePaymentMethodController(
            StoreRepository storeRepository,
            BTCPayServerOptions btcPayServerOptions,
            BTCPayNetworkProvider btcPayNetworkProvider,
            CssThemeManager cssThemeManager
            )
        {
            _storeRepository = storeRepository;
            _btcPayServerOptions = btcPayServerOptions;
            _btcPayNetworkProvider = btcPayNetworkProvider;
            _cssThemeManager = cssThemeManager;
        }

        [HttpGet("")]
        [Authorize(Policy = Policies.CanModifyStoreSettings.Key, AuthenticationSchemes = AuthenticationSchemes.ApiKey)]
        public ActionResult<IEnumerable<StoreLightningLikePaymentMethod>> GetLightningLikePaymentMethods(
            [FromQuery] bool enabledOnly = false)
        {
            var blob = Store.GetStoreBlob();
            var excludedPaymentMethods = blob.GetExcludedPaymentMethods();
            var defaultPaymentId = Store.GetDefaultPaymentId(_btcPayNetworkProvider);
            return Ok(Store.GetSupportedPaymentMethods(_btcPayNetworkProvider)
                .Where((method) => method.PaymentId.PaymentType == PaymentTypes.LightningLike)
                .OfType<LightningSupportedPaymentMethod>()
                .Select(strategy =>
                    new StoreLightningLikePaymentMethod(strategy, !excludedPaymentMethods.Match(strategy.PaymentId), defaultPaymentId == strategy.PaymentId))
                .Where((result) => !enabledOnly || result.Enabled)
                .ToList()
            );
        }

        [HttpGet("{cryptoCode}")]
        [Authorize(Policy = Policies.CanModifyStoreSettings.Key, AuthenticationSchemes = AuthenticationSchemes.ApiKey)]
        public ActionResult<StoreLightningLikePaymentMethod> GetLightningLikePaymentMethod(string cryptoCode)
        {
            if (!GetCryptoCodeWallet(cryptoCode, out var network))
            {
                return NotFound();
            }

            return Ok(GetExistingLightningLikePaymentMethod(cryptoCode));
        }

        [HttpPut("{cryptoCode}")]
        [Authorize(Policy = Policies.CanModifyStoreSettings.Key, AuthenticationSchemes = AuthenticationSchemes.ApiKey)]
        public async Task<ActionResult<StoreBtcLikePaymentMethod>> UpdateLightningLikePaymentMethod(string cryptoCode,
            [FromBody] UpdateStoreLightningLikePaymentMethod paymentMethod)
        {
            var id = new PaymentMethodId(cryptoCode, PaymentTypes.LightningLike);

            if (!GetCryptoCodeWallet(cryptoCode, out var network))
            {
                return NotFound();
            }


            var store = Store;
            var storeBlob = store.GetStoreBlob();



            var internalLightning = GetInternalLighningNode(network.CryptoCode);
            // vm.InternalLightningNode = internalLightning?.ToString();


            Payments.Lightning.LightningSupportedPaymentMethod updatedPaymentMethod = null;
            if (!string.IsNullOrEmpty(paymentMethod.ConnectionString))
            {
                if (!LightningConnectionString.TryParse(paymentMethod.ConnectionString, false, out var connectionString,
                    out var error))
                {
                    ModelState.AddModelError(nameof(paymentMethod.ConnectionString), $"Invalid URL ({error})");
                    return BadRequest(ModelState);
                }

                if (connectionString.ConnectionType == LightningConnectionType.LndGRPC)
                {
                    ModelState.AddModelError(nameof(paymentMethod.ConnectionString),
                        $"BTCPay does not support gRPC connections");
                    return BadRequest(ModelState);
                }

                var internalDomain = internalLightning?.BaseUri?.DnsSafeHost;

                bool isInternalNode = connectionString.ConnectionType == LightningConnectionType.CLightning ||
                                      connectionString.BaseUri.DnsSafeHost == internalDomain ||
                                      (internalDomain == "127.0.0.1" || internalDomain == "localhost");

                if (connectionString.BaseUri.Scheme == "http")
                {
                    if (!isInternalNode)
                    {
                        ModelState.AddModelError(nameof(paymentMethod.ConnectionString), "The url must be HTTPS");
                        return BadRequest(ModelState);
                    }
                }

                if (connectionString.MacaroonFilePath != null)
                {
                    if (!CanUseInternalLightning())
                    {
                        ModelState.AddModelError(nameof(paymentMethod.ConnectionString),
                            "You are not authorized to use macaroonfilepath");
                        return BadRequest(ModelState);
                    }

                    if (!System.IO.File.Exists(connectionString.MacaroonFilePath))
                    {
                        ModelState.AddModelError(nameof(paymentMethod.ConnectionString),
                            "The macaroonfilepath file does not exist");
                        return BadRequest(ModelState);
                    }

                    if (!System.IO.Path.IsPathRooted(connectionString.MacaroonFilePath))
                    {
                        ModelState.AddModelError(nameof(paymentMethod.ConnectionString),
                            "The macaroonfilepath should be fully rooted");
                        return BadRequest(ModelState);
                    }
                }

                if (isInternalNode && !CanUseInternalLightning())
                {
                    ModelState.AddModelError(nameof(paymentMethod.ConnectionString), "Unauthorized url");
                    return BadRequest(ModelState);
                }

                updatedPaymentMethod = new Payments.Lightning.LightningSupportedPaymentMethod()
                {
                    CryptoCode = id.CryptoCode
                };
                updatedPaymentMethod.SetLightningUrl(connectionString);
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            store.SetSupportedPaymentMethod(id, updatedPaymentMethod);
            storeBlob.SetExcluded(id, !paymentMethod.Enabled);
            if (paymentMethod.Default)
            {

                store.SetDefaultPaymentId(id);
            }

            store.SetStoreBlob(storeBlob);
            await _storeRepository.UpdateStore(store);
            return Ok(GetExistingLightningLikePaymentMethod(cryptoCode, store));

        }

        private bool GetCryptoCodeWallet(string cryptoCode, out BTCPayNetwork network)
        {
            network = _btcPayNetworkProvider.GetNetwork<BTCPayNetwork>(cryptoCode);
            return network != null;
        }
        private StoreLightningLikePaymentMethod GetExistingLightningLikePaymentMethod(string cryptoCode, StoreData store = null)
        {
            store = store ?? Store;
            var storeBlob = store.GetStoreBlob();
            var defaultPaymentMethod = store.GetDefaultPaymentId(_btcPayNetworkProvider);
            var id = new PaymentMethodId(cryptoCode, PaymentTypes.LightningLike);
            var paymentMethod = store
                .GetSupportedPaymentMethods(_btcPayNetworkProvider)
                .OfType<LightningSupportedPaymentMethod>()
                .FirstOrDefault(method => method.PaymentId == id);

            var excluded = storeBlob.IsExcluded(paymentMethod.PaymentId);
            return paymentMethod == null
                ? new StoreLightningLikePaymentMethod(cryptoCode,  string.Empty,!excluded, defaultPaymentMethod == id)
                : new StoreLightningLikePaymentMethod(paymentMethod, !excluded, defaultPaymentMethod == paymentMethod.PaymentId);
        }
        
        private LightningConnectionString GetInternalLighningNode(string cryptoCode)
        {
            if (_btcPayServerOptions.InternalLightningByCryptoCode.TryGetValue(cryptoCode, out var connectionString))
            {
                return CanUseInternalLightning() ? connectionString : null;
            }
            return null;
        }
        private bool CanUseInternalLightning()
        {
            return _cssThemeManager.AllowLightningInternalNodeForAll ||
                   User.HasPermissions(APIKeyConstants.Permissions.ServerManagement);
        }
    }
}
