using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Controllers.NewApi.Models;
using BTCPayServer.Models;
using BTCPayServer.Security;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Controllers.NewApi
{
    [ApiController]
    [Route("api/v0.1/stores")]
    [Authorize()]
    [IncludeInOpenApiDocs]
    public class StoresApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly StoreRepository _storeRepository;

        public StoresApiController(UserManager<ApplicationUser> userManager, StoreRepository storeRepository)
        {
            _userManager = userManager;
            _storeRepository = storeRepository;
        }

        [HttpGet("")]
        public async Task<ActionResult<IEnumerable<StoreModel>>> GetStores()
        {
            var stores = await _storeRepository.GetStoresByUserId(_userManager.GetUserId(User));
            return Ok(stores.Select(data => new StoreModel(data, _storeRepository.CanDeleteStores())));
        }

        [HttpGet("{storeId}")]
        [Authorize(Policy = Policies.CanModifyStoreSettings.Key)]
        public ActionResult<StoreModel> GetStore(string storeId)
        {
            var store = HttpContext.GetStoreData();
            return Ok(new StoreModel(store, _storeRepository.CanDeleteStores()));
        }

        [HttpPost("")]
        public async Task<ActionResult<StoreModel>> CreateStore([FromBody] CreateStoreRequest request)
        {
            var store = await _storeRepository.CreateStore(_userManager.GetUserId(User), request.StoreName);
            return CreatedAtAction(nameof(GetStore), new {storeId = store.Id},
                new StoreModel(store, _storeRepository.CanDeleteStores()));
        }

        [HttpDelete("{storeId}")]
        [Authorize(Policy = Policies.CanModifyStoreSettings.Key)]
        public async Task<ActionResult> DeleteStore(string storeId)
        {
            var result = await _storeRepository.DeleteStore(storeId);
            if (!result)
            {
                return BadRequest();
            }

            return Ok();
        }

        [HttpPut("{storeId}")]
        [Authorize(Policy = Policies.CanModifyStoreSettings.Key)]
        public async Task<ActionResult<StoreModel>> UpdateStore(string storeId, [FromBody] UpdateStoreRequest store)
        {
            bool needUpdate = false;
            var currentStore = HttpContext.GetStoreData();
            if (currentStore.SpeedPolicy != store.SpeedPolicy)
            {
                needUpdate = true;
                currentStore.SpeedPolicy = store.SpeedPolicy;
            }

            if (currentStore.StoreName != store.StoreName)
            {
                needUpdate = true;
                currentStore.StoreName = store.StoreName;
            }

            if (currentStore.StoreWebsite != store.StoreWebsite)
            {
                needUpdate = true;
                currentStore.StoreWebsite = store.StoreWebsite;
            }

            var blob = currentStore.GetStoreBlob();
            blob.AnyoneCanInvoice = store.AnyoneCanCreateInvoice;
            blob.NetworkFeeDisabled = !store.NetworkFee;
            blob.MonitoringExpiration = store.MonitoringExpiration;
            blob.InvoiceExpiration = store.InvoiceExpiration;
            blob.LightningDescriptionTemplate = store.LightningDescriptionTemplate ?? string.Empty;
            blob.PaymentTolerance = store.PaymentTolerance;

            if (currentStore.SetStoreBlob(blob))
            {
                needUpdate = true;
            }

            if (needUpdate)
            {
                await _storeRepository.UpdateStore(currentStore);
            }

            return Ok(new StoreModel(currentStore, _storeRepository.CanDeleteStores()));
        }
    }
}
