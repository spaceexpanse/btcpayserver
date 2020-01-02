using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Controllers.RestApi.Models
{
    public class StoreBtcLikePaymentMethod
    {
        public bool Enabled { get; set; }
        public bool Default { get; set; }
        public string CryptoCode { get; set; }
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
