using System.Collections.Generic;
using BTCPayServer.Payments;
using BTCPayServer.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Controllers.NewApi
{
    [ApiController]
    [IncludeInOpenApiDocs]
    [Route("api/v0.1/stores/{storeId}/PaymentMethods")]
    [Authorize(Policy = Policies.CanModifyStoreSettings.Key)]
    [Authorize()]
    public class StorePaymentMethodsController : ControllerBase
    {
        [HttpGet("")]
        public ActionResult<IEnumerable<string>> GetPaymentMethods()
        {
            return Ok(new List<string>()
            {
                nameof(PaymentTypes.BTCLike)
            });
        }

    }
}
