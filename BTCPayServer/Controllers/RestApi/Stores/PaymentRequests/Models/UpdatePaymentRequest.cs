using System;
using BTCPayServer.Data;

namespace BTCPayServer.Controllers.RestApi.Stores.Models
{
    public class UpdatePaymentRequest: PaymentRequestData.PaymentRequestBlob
    {
        
    }

    public class PaymentRequestModel: PaymentRequestData.PaymentRequestBlob
    {
        public PaymentRequestData.PaymentRequestStatus Status { get; set; }
        public DateTimeOffset Created { get; set; }
        public string Id { get; set; }

        public static PaymentRequestModel GetPaymentRequestModel(PaymentRequestData data)
        {
            var blob = data.GetBlob();

            return new PaymentRequestModel()
            {
                Created = data.Created,
                Id = data.Id,
                Status = data.Status,
                Amount = blob.Amount,
                Currency = blob.Currency,
                Description = blob.Description,
                Title = blob.Title,
                ExpiryDate = blob.ExpiryDate,
                Email = blob.Email,
                AllowCustomPaymentAmounts = blob.AllowCustomPaymentAmounts,
                EmbeddedCSS = blob.EmbeddedCSS,
                CustomCSSLink = blob.CustomCSSLink
            };
        }
    }
}
