using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Client;
using BTCPayServer.Filters;
using BTCPayServer.Payments;
using BTCPayServer.Payments.Lightning;
using BTCPayServer.Services.Invoices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace BTCPayServer.Controllers
{
    public partial class UIInvoiceController
    {
      
        [HttpGet("i/{invoiceId}/unified-address", Order = 0)]
        public async Task<IActionResult>  GetUnifiedAddressCheckout(string invoiceId)
        {
            var invoice = await _InvoiceRepository.GetInvoice(invoiceId);
            if (invoice?.Status == InvoiceStatusLegacy.New)
            {
                return View(new
                {
                    
                    invoice.Price,
                    invoice.Currency
                });

            }
            if (invoice is not null)
            {
                
                return RedirectToAction("Checkout", new
                {
                    invoiceId,
                });
            }

            return NotFound();
        }
        
        [HttpGet]
        [Route("~/.well-known/unified-address/{invoiceId}")]
        [CheatModeRoute]
        public async Task<IActionResult> GetUnifiedAddressOptions(string invoiceId)
        {
            var invoice = await _InvoiceRepository.GetInvoice(invoiceId);
            if (invoice?.Status == InvoiceStatusLegacy.New)
            {
                List<string> result = new List<string>();
                var paymentMethods = invoice.GetSupportedPaymentMethod();
                foreach (var paymentMethod in paymentMethods)
                {
                    switch (paymentMethod)
                    {
                        case DerivationSchemeSettings derivationSchemeSettings:
                            result.Add(derivationSchemeSettings.PaymentId.ToString());
                            break;
                        case LNURLPaySupportedPaymentMethod lnurlPaySupportedPaymentMethod:
                            
                            result.Add("lnurlp");
                            break;
                        
                        case LightningSupportedPaymentMethod lightningSupportedPaymentMethod:
                            result.Add("bolt11");
                            break;
                    }
                }

                return Ok(result);
            }

            return NotFound();
        }

        [HttpGet("~/.well-known/btc/{invoiceId}")]
        public async Task<IActionResult> GetUnifiedAddressOptionBTC(string invoiceId,
            [FromServices] BTCPayServerClient btcPayServerClient)
        {
            return await GetUnifiedAddressOption("BTC", invoiceId, btcPayServerClient);
        }

        [HttpGet("~/.well-known/bolt11/{invoiceId}")]
        public async Task<IActionResult> GetUnifiedAddressOptionBOLT11(string invoiceId,
            [FromServices] BTCPayServerClient btcPayServerClient)
        {
            return await GetUnifiedAddressOption(new PaymentMethodId("BTC", LightningPaymentType.Instance).ToString(),
                invoiceId, btcPayServerClient);
        }
        
        
        [NonAction]
        private async Task<IActionResult> GetUnifiedAddressOption(string paymentMethod, string invoiceId, BTCPayServerClient btcPayServerClient )
        {
            var invoice = await _InvoiceRepository.GetInvoice(invoiceId);
             if (invoice.Status == InvoiceStatusLegacy.New)
            {
                
                var pms = await btcPayServerClient.GetInvoicePaymentMethods(invoice.StoreId, invoiceId);
                var pm = pms.FirstOrDefault(model => model.PaymentMethod == paymentMethod);
                if (pm is not null)
                {
                    if (pm.Activated is false)
                    {
                        await btcPayServerClient.ActivateInvoicePaymentMethod(invoice.StoreId, invoiceId, pm.PaymentMethod);
                        pms = await btcPayServerClient.GetInvoicePaymentMethods(invoice.StoreId, invoiceId);
                        pm = pms.First(model => model.PaymentMethod == paymentMethod);

                    }

                    return Ok(new {pm.PaymentLink});
                }
            }

            return NotFound();
        }

    }
}
