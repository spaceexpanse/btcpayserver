using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Client.Models;
using BTCPayServer.Data;
using BTCPayServer.Security;
using BTCPayServer.Security.Greenfield;
using BTCPayServer.Services.Custodian;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json.Linq;
using CustodianAccountData = BTCPayServer.Data.CustodianAccountData;

namespace BTCPayServer.Controllers.Greenfield
{
    [ApiController]
    [Authorize(AuthenticationSchemes = AuthenticationSchemes.GreenfieldAPIKeys)]
    [EnableCors(CorsPolicies.All)]
    public class GreenfieldCustodianAccountController : ControllerBase
    {
        private readonly CustodianRegistry _custodianRegistry;
        private readonly CustodianAccountRepository _custodianAccountRepository;

        public GreenfieldCustodianAccountController(CustodianAccountRepository custodianAccountRepository,
            CustodianRegistry custodianRegistry)
        {
            _custodianAccountRepository = custodianAccountRepository;
            _custodianRegistry = custodianRegistry;
        }

        [HttpGet("~/api/v1/store/{storeId}/custodian-account")]
        [Authorize(Policy = Policies.Unrestricted, AuthenticationSchemes = AuthenticationSchemes.Greenfield)]
        public async Task<IActionResult> ListCustodianAccount(string storeId)
        {
            var store = HttpContext.GetStoreData();
            if (store == null)
            {
                return this.CreateAPIError(404, "store-not-found", "The store was not found");
            }

            var custodianAccounts =_custodianAccountRepository.FindByStoreId(storeId);
            
            // TODO add field "assetBalances" and fill with data from the API. 
            
            return Ok(custodianAccounts);
        }

        [HttpPost("~/api/v1/store/{storeId}/custodian-account")]
        [Authorize(Policy = Policies.Unrestricted, AuthenticationSchemes = AuthenticationSchemes.Greenfield)]
        public async Task<IActionResult> CreateCustodianAccount(string storeId, CreateCustodianAccountRequest request)
        {
            request ??= new CreateCustodianAccountRequest();

            // TODO this may throw an exception if custodian is not found. How do I make this better?
            var custodian = _custodianRegistry.getAll()[request.CustodianCode];
            
            // TODO If storeId is not valid, we get a foreign key SQL error. Is this okay or do we want to check the storeId first?
            
            var custodianAccount = new CustodianAccountData()
            {
                CustodianCode = custodian.getCode(),
                StoreId = storeId,
                
            };
            var newBlob = new CustodianAccountData.CustodianAccountBlob();
            newBlob.config = request.Config;
            custodianAccount.SetBlob(newBlob);
            
            await _custodianAccountRepository.CreateOrUpdate(custodianAccount);
            return Ok(custodianAccount);
        }

         
        [HttpDelete("~/api/v1/custodian-account/{id}", Order = 1)]
        [Authorize(Policy = Policies.Unrestricted, AuthenticationSchemes = AuthenticationSchemes.Greenfield)]
        public async Task<IActionResult> DeleteCustodianAccount(string id)
        {
            //TODO implement
            return BadRequest();
            // if (!string.IsNullOrEmpty(id) && await _custodianAccountRepository.Remove(id, _userManager.GetUserId(User)))
            // {
            //     return Ok();
            // }
            // return this.CreateAPIError("custodian-account-not-found", "This custodian account does not exist");
        }

        // private static CustodianAccountData FromModel(CustodianAccountData data)
        // {
        //     return new CustodianAccountData()
        //     {
        //         Permissions = Permission.ToPermissions(data.GetBlob().Permissions).ToArray(),
        //         ApiKey = data.Id,
        //         Label = data.Label ?? string.Empty
        //     };
        // }

        [HttpGet("~/api/v1/store/{storeId}/custodian-account/{accountId}/{paymentMethod}/address")]
        [Authorize(Policy = Policies.Unrestricted, AuthenticationSchemes = AuthenticationSchemes.Greenfield)]
        public async Task<IActionResult> GetDepositAddress(string storeId, string accountId, CreateCustodianAccountRequest request)
        {
            var custodianAccount = _custodianAccountRepository.FindById(accountId);
            var custodian = _custodianRegistry.getAll()[custodianAccount.Result.CustodianCode];

            if (custodian is ICanDeposit)
            {
                var result = custodian.GetDepositAddress();
                return OK(result);
            }
        }
    }
    
    
}
