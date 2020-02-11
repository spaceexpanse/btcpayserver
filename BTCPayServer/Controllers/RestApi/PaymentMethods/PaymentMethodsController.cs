using System;
using System.Collections.Generic;
using System.Linq;
using BTCPayServer.Payments;
using BTCPayServer.Security;
using BTCPayServer.Services.Invoices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

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
        [OpenApiOperation("Get all supported payment types", "Get all supported payment types available in this BTCPay deployment.")]
        [SwaggerResponse(StatusCodes.Status200OK, typeof(PaymentType[]),
            Description = "All available payment methods of a specific payment type")]
        public ActionResult<IEnumerable<PaymentType>> GetPaymentMethods()
        {
            return Ok(_paymentMethodHandlerDictionary.SelectMany(handler =>
                handler.GetSupportedPaymentMethods().Select(id => id.PaymentType)));
        }


        /// <param name="paymentType">The payment type</param>
        [HttpGet("{paymentType}")]
        [OpenApiOperation("Get Payment Methods of a payment type", "Get all available payment methods of a specific payment type")]
        [SwaggerResponse(StatusCodes.Status200OK, typeof(string[]), Description = "All available payment methods of a specific payment type")]
        public ActionResult<IEnumerable<PaymentMethodId>> GetPaymentMethodsForType(string paymentType)
        {
            return Ok(_paymentMethodHandlerDictionary.SelectMany(handler => handler.GetSupportedPaymentMethods())
                .Where(id => id.PaymentType.ToString().Equals(paymentType, StringComparison.InvariantCultureIgnoreCase)));
        }
    }
}
