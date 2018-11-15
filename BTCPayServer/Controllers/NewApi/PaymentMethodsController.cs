using System.Collections.Generic;
using BTCPayServer.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Controllers.NewApi
{
    [ApiController]
    [IncludeInOpenApiDocs]
    [Route("api/v0.1/PaymentMethods")]
    [Authorize()]
    public class PaymentMethodsController : ControllerBase
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

    public class BtcLikePaymentMethod
    {
        public string CryptoCode { get; set; }
    }
}
