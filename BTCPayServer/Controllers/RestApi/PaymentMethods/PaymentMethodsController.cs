using System.Collections.Generic;
using System.Linq;
using BTCPayServer.Payments;
using BTCPayServer.Security;
using BTCPayServer.Services.Invoices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Controllers.RestApi
{
    [ApiController]
    [IncludeInOpenApiDocs]
    [Route("api/v1/payment-methods")]
    [Authorize(AuthenticationSchemes = AuthenticationSchemes.ApiKey)]
    public class PaymentMethodsController : ControllerBase
    {
        private readonly PaymentMethodHandlerDictionary _paymentMethodHandlerDictionary;

        public PaymentMethodsController(PaymentMethodHandlerDictionary paymentMethodHandlerDictionary)
        {
            _paymentMethodHandlerDictionary = paymentMethodHandlerDictionary;
        }
        
        [HttpGet("")]
        public ActionResult<IEnumerable<string>> GetPaymentMethods()
        {
            return Ok(_paymentMethodHandlerDictionary.SelectMany(handler =>
                handler.GetSupportedPaymentMethods().Select(id => id.PaymentType)));
        }
        
        [HttpGet("{paymentType}")]
        public ActionResult<IEnumerable<PaymentMethodId>> GetPaymentMethodsForType(PaymentType paymentType)
        {
            return Ok(_paymentMethodHandlerDictionary.SelectMany(handler => handler.GetSupportedPaymentMethods())
                .Where(id => id.PaymentType == paymentType));
        }
    }
}
