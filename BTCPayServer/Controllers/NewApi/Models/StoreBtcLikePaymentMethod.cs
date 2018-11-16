using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Controllers.NewApi.Models
{
    public class StoreBtcLikePaymentMethod
    {
        public bool Enabled { get; set; }
        public string CryptoCode { get; set; }
        [Required] public string DerivationScheme { get; set; }

        public StoreBtcLikePaymentMethod()
        {
        }

        public StoreBtcLikePaymentMethod(string cryptoCode, bool enabled)
        {
            CryptoCode = cryptoCode;
            Enabled = enabled;
        }

        public StoreBtcLikePaymentMethod(DerivationStrategy derivationStrategy, bool enabled)
        {
            Enabled = enabled;
            CryptoCode = derivationStrategy.PaymentId.CryptoCode;
            DerivationScheme = derivationStrategy.DerivationStrategyBase.ToString();
        }
    }
}