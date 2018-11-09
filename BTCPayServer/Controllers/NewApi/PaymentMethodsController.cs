using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Models;
using BTCPayServer.Payments;
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
    [Route("api/v1.0/stores/{storeId}/[controller]")]
    [Authorize(Policy = Policies.CanModifyStoreSettings.Key)]
    [Authorize()]
    public class PaymentMethodsController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly StoreRepository _storeRepository;

        public PaymentMethodsController(UserManager<ApplicationUser> userManager, StoreRepository storeRepository)
        {
            _userManager = userManager;
            _storeRepository = storeRepository;
        }

        [HttpGet("")]
        public ActionResult<IEnumerable<string>> GetPaymentMethods()
        {
            return Ok(new List<string>()
            {
                nameof(PaymentTypes.BTCLike),
                nameof(PaymentTypes.LightningLike),
            });
        }
        [HttpGet(nameof(PaymentTypes.BTCLike) + "/{cryptoCode}")]
        public ActionResult<IEnumerable<string>> GetBtcLikePaymentMethod(string storeId, string cryptoCode)
        {
            return Ok(new List<string>()
            {
                nameof(PaymentTypes.BTCLike),
                nameof(PaymentTypes.LightningLike),
            });
        }

        

    }
}
