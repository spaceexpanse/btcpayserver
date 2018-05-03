using System;
using BTCPayServer.Services.Invoices;
using NBitcoin;
using Newtonsoft.Json;

namespace BTCPayServer.Payments.Manual
{

    public class ManualPaymentData : CryptoPaymentData
    {
        public Guid Id { get; set; }

        public string GetPaymentId()
        {
            if (Guid.Empty.Equals(Id))
            {
                Id = Guid.NewGuid();
            }
            return Id.ToString();
        }

        public string[] GetSearchTerms()
        {
            throw new System.NotImplementedException();
        }

        public decimal GetValue()
        {
            throw new System.NotImplementedException();
        }

        public bool PaymentCompleted(PaymentEntity entity, BTCPayNetwork network)
        {
            return false;
        }

        public bool PaymentConfirmed(PaymentEntity entity, SpeedPolicy speedPolicy, BTCPayNetwork network)
        {
            return false;
        }

        public PaymentTypes GetPaymentType() => PaymentTypes.Manual;
    }
}
