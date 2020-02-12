using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Controllers.RestApi.Models
{
    public class StoreBtcLikePaymentMethod
    {
        /// <summary>
        /// Whether the payment method is enabled
        /// </summary>
        public bool Enabled { get; set; }
        /// <summary>
        /// Whether the payment method is the default
        /// </summary>
        public bool Default { get; set; }
        /// <summary>
        /// Crypto code of the payment emthod
        /// </summary>
        public string CryptoCode { get; set; }
        /// <summary>
        /// The derivation scheme
        /// </summary>
        [Required] public string DerivationScheme { get; set; }

        public StoreBtcLikePaymentMethod()
        {
        }

        public StoreBtcLikePaymentMethod(DerivationSchemeSettings schemeSettings, bool enabled, bool defaultMethod)
        {
            Enabled = enabled;
            Default = defaultMethod;
            CryptoCode = schemeSettings.PaymentId.CryptoCode;
            DerivationScheme = schemeSettings.AccountDerivation.ToString();
        }
        
        public StoreBtcLikePaymentMethod(string cryptoCode, string  derivationScheme, bool enabled, bool defaultMethod)
        {
            Enabled = enabled;
            Default = defaultMethod;
            CryptoCode = cryptoCode;
            DerivationScheme = derivationScheme;
        }
    }
}
