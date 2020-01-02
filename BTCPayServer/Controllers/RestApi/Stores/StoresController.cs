using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Controllers.RestApi.Models;
using BTCPayServer.Data;
using BTCPayServer.Security;
using BTCPayServer.Security.APIKeys;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Controllers.RestApi.Stores
{
    [ApiController]
    [Route("api/v1/stores")]
    [Authorize(AuthenticationSchemes = AuthenticationSchemes.ApiKey)]
    [IncludeInOpenApiDocs]
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
        [Authorize(Policy = Policies.CanListStoreSettings.Key, AuthenticationSchemes = AuthenticationSchemes.ApiKey)]
        public async Task<ActionResult<IEnumerable<StoreModel>>> GetStores()
        {
            var stores = await User.GetStores(_userManager, _storeRepository);
            return Ok(stores.Select(data => StoreModel.GetStoreModel(data)));
        }

        [HttpGet("{storeId}")]
        [Authorize(Policy = Policies.CanModifyStoreSettings.Key, AuthenticationSchemes = AuthenticationSchemes.ApiKey)]
        public ActionResult<StoreModel> GetStore(string storeId)
        {
            return Ok(StoreModel.GetStoreModel(HttpContext.GetStoreData()));
        }

        [HttpPost("")]
        [Authorize(Policy = Policies.CanModifyStoreSettings.Key, AuthenticationSchemes = AuthenticationSchemes.ApiKey)]
        public async Task<ActionResult<StoreModel>> CreateStore([FromBody] CreateStoreRequest request)
        {
            var store = await _storeRepository.CreateStore(_userManager.GetUserId(User), request.StoreName);
            return CreatedAtAction(nameof(GetStore), new {storeId = store.Id}, StoreModel.GetStoreModel(store));
        }

        [HttpDelete("{storeId}")]
        [Authorize(Policy = Policies.CanModifyStoreSettings.Key, AuthenticationSchemes = AuthenticationSchemes.ApiKey)]
        public async Task<ActionResult> DeleteStore(string storeId)
        {
            var result = await _storeRepository.DeleteStore(storeId);
            return result ? (ActionResult)Ok() : BadRequest();
        }

        [HttpPut("{storeId}")]
        [Authorize(Policy = Policies.CanModifyStoreSettings.Key, AuthenticationSchemes = AuthenticationSchemes.ApiKey)]
        public async Task<ActionResult<StoreModel>> UpdateStore(string storeId, [FromBody] StoreModel store)
        {
            var currentStore = HttpContext.GetStoreData();
            store.SetValues(ref currentStore);
            await _storeRepository.UpdateStore(currentStore);
            return Ok(StoreModel.GetStoreModel(currentStore));
        }
    }
}
