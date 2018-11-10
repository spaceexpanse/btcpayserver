using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Models;
using BTCPayServer.Security;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Stores;
using BTCPayServer.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;

namespace BTCPayServer.Controllers.NewApi
{
    [ApiController]
    [Route("api/v1.0/[controller]")]
    [Authorize()]
    public class StoresController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly StoreRepository _storeRepository;

        public StoresController(UserManager<ApplicationUser> userManager, StoreRepository storeRepository)
        {
            _userManager = userManager;
            _storeRepository = storeRepository;
        }

        [HttpGet("")]
        public async Task<ActionResult<IEnumerable<StoreData>>> GetStores()
        {
            var stores = await _storeRepository.GetStoresByUserId(_userManager.GetUserId(User));
            return Ok(stores);
        }

        [HttpGet("{storeId}")]
        [Authorize(Policy = Policies.CanModifyStoreSettings.Key)]
        public async Task<ActionResult<StoreModel>> GetStore(string storeId)
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

    public class UpdateStoreRequest : StoreModel
    {
    }

    public class StoreModel
    {
        public string Id { get; set; }

        [Required]
        [MaxLength(50)]
        [MinLength(1)]
        public string StoreName { get; set; }

        [MaxLength(500)] [Uri] 
        public string StoreWebsite { get; set; }
        public bool CanDelete { get; set; }
        [Range(1, 60 * 24 * 24)] 
        public int InvoiceExpiration { get; set; }
        public int MonitoringExpiration { get; set; }
        public SpeedPolicy SpeedPolicy { get; set; }
        public bool NetworkFee { get; set; }
        public string LightningDescriptionTemplate { get; set; }
        [Range(0, 100)] public double PaymentTolerance { get; set; }
        public bool AnyoneCanCreateInvoice { get; set; }

        public StoreModel()
        {
        }

        public StoreModel(StoreData storeData, bool canDelete)
        {
            var storeBlob = storeData.GetStoreBlob();
            Id = storeData.Id;
            StoreName = storeData.StoreName;
            StoreWebsite = storeData.StoreWebsite;
            NetworkFee = !storeBlob.NetworkFeeDisabled;
            AnyoneCanCreateInvoice = storeBlob.AnyoneCanInvoice;
            SpeedPolicy = storeData.SpeedPolicy;
            CanDelete = canDelete;

            MonitoringExpiration = storeBlob.MonitoringExpiration;
            InvoiceExpiration = storeBlob.InvoiceExpiration;
            LightningDescriptionTemplate = storeBlob.LightningDescriptionTemplate;
            PaymentTolerance = storeBlob.PaymentTolerance;
        }
    }


    public class CreateStoreRequest
    {
        [Required]
        [MaxLength(50)]
        [MinLength(1)]
        public string StoreName { get; set; }
    }
}
