using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Models;
using BTCPayServer.Models.InvoicingModels;
using BTCPayServer.Services.Invoices;
using NBitpayClient;

namespace BTCPayServer.Payments
{
    /// <summary>
    /// This class customize invoice creation by the creation of payment details for the PaymentMethod during invoice creation
    /// </summary>
    public interface IPaymentMethodHandler
    {
        /// <summary>
        /// Create needed to track payments of this invoice
        /// </summary>
        /// <param name="supportedPaymentMethod"></param>
        /// <param name="paymentMethod"></param>
        /// <param name="store"></param>
        /// <param name="network"></param>
        /// <returns></returns>
        Task<IPaymentMethodDetails> CreatePaymentMethodDetails(ISupportedPaymentMethod supportedPaymentMethod, PaymentMethod paymentMethod, StoreData store, BTCPayNetwork network, object preparePaymentObject);

        /// <summary>
        /// This method called before the rate have been fetched
        /// </summary>
        /// <param name="supportedPaymentMethod"></param>
        /// <param name="store"></param>
        /// <param name="network"></param>
        /// <returns></returns>
        object PreparePayment(ISupportedPaymentMethod supportedPaymentMethod, StoreData store, BTCPayNetwork network);
        
        bool CanHandle(PaymentMethodId paymentMethodId);
        Task<string> ToString(bool pretty, PaymentMethodId paymentMethodId);

        Task PrepareInvoiceDto(InvoiceResponse invoiceResponse, InvoiceEntity invoiceEntity,
            InvoiceCryptoInfo invoiceCryptoInfo,
            PaymentMethodAccounting accounting, PaymentMethod info);

        Task PreparePaymentModel(PaymentModel model, InvoiceResponse invoiceResponse);
        string GetCryptoImage(PaymentMethodId paymentMethodId);
        string GetPaymentMethodName(PaymentMethodId paymentMethodId);

        IEnumerable<PaymentMethodId> GetSupportedPaymentMethods();
    }

    public interface IPaymentMethodHandler<T> : IPaymentMethodHandler where T : ISupportedPaymentMethod
    {
        Task<IPaymentMethodDetails> CreatePaymentMethodDetails(T supportedPaymentMethod, PaymentMethod paymentMethod, StoreData store, BTCPayNetwork network, object preparePaymentObject);
    }

    public abstract class PaymentMethodHandlerBase<T> : IPaymentMethodHandler<T> where T : ISupportedPaymentMethod
    {
        
        public abstract Task<IPaymentMethodDetails> CreatePaymentMethodDetails(T supportedPaymentMethod, PaymentMethod paymentMethod, StoreData store, BTCPayNetwork network, object preparePaymentObject);
        public abstract bool CanHandle(PaymentMethodId paymentMethodId);

        public abstract Task<string> ToString(bool pretty, PaymentMethodId paymentMethodId);
        
        public abstract Task PrepareInvoiceDto(InvoiceResponse invoiceResponse, InvoiceEntity invoiceEntity,
            InvoiceCryptoInfo invoiceCryptoInfo, PaymentMethodAccounting accounting, PaymentMethod info);

        public abstract Task PreparePaymentModel(PaymentModel model, InvoiceResponse invoiceResponse);
        public abstract string GetCryptoImage(PaymentMethodId paymentMethodId);
        public abstract string GetPaymentMethodName(PaymentMethodId paymentMethodId);
        public abstract IEnumerable<PaymentMethodId> GetSupportedPaymentMethods();


        public virtual object PreparePayment(T supportedPaymentMethod, StoreData store, BTCPayNetwork network)
        {
            return null;
        }

        object IPaymentMethodHandler.PreparePayment(ISupportedPaymentMethod supportedPaymentMethod, StoreData store, BTCPayNetwork network)
        {
            if (supportedPaymentMethod is T method)
            {
                return PreparePayment(method, store, network);
            }
            throw new NotSupportedException("Invalid supportedPaymentMethod");
        }

        Task<IPaymentMethodDetails> IPaymentMethodHandler.CreatePaymentMethodDetails(ISupportedPaymentMethod supportedPaymentMethod, PaymentMethod paymentMethod, StoreData store, BTCPayNetwork network, object preparePaymentObject)
        {
            if (supportedPaymentMethod is T method)
            {
                return CreatePaymentMethodDetails(method, paymentMethod, store, network, preparePaymentObject);
            }
            throw new NotSupportedException("Invalid supportedPaymentMethod");
        }
    }
}
