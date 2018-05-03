using System;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Services;
using BTCPayServer.Services.Invoices;

namespace BTCPayServer.Payments.Manual
{
    public class ManualPaymentHandler : IPaymentMethodHandler
    {
        public Task<IPaymentMethodDetails> CreatePaymentMethodDetails(ISupportedPaymentMethod supportedPaymentMethod, PaymentMethod paymentMethod,
            StoreData store, BTCPayNetwork network)
        {
            throw new NotImplementedException();
        }
    }
}
