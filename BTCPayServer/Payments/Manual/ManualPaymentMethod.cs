using System;
using NBitcoin;
using Newtonsoft.Json;

namespace BTCPayServer.Payments.Manual
{
    public class ManualPaymentMethod : IPaymentMethodDetails
    {
        public string GetPaymentDestination()
        {
            return "Manual Payment";
        }

        public PaymentTypes GetPaymentType()
        {
            return PaymentTypes.Manual;
        }

        public decimal GetTxFee()
        {
            return 0;
        }

        public void SetNoTxFee()
        {
        }

        public void SetPaymentDestination(string newPaymentDestination)
        {
        }
    }
}
