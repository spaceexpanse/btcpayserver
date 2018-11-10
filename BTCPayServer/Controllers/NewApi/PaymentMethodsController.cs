using System.Collections.Generic;
using BTCPayServer.Payments;
using BTCPayServer.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Controllers.NewApi
{
    [ApiController]
    [Route("api/v0.1/stores/{storeId}/[controller]")]
    [Authorize(Policy = Policies.CanModifyStoreSettings.Key)]
    [Authorize()]
    public class PaymentMethodsController : ControllerBase
    {

        [HttpGet("")]
        public ActionResult<IEnumerable<string>> GetPaymentMethods()
        {
            return Ok(new List<string>()
            {
                nameof(PaymentTypes.BTCLike),
                nameof(PaymentTypes.LightningLike),
            });
        }
    }
}
