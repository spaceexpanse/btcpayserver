using System.ComponentModel.DataAnnotations;
using BTCPayServer.Payments.Lightning;

namespace BTCPayServer.Controllers.RestApi.Models
{
    public class UpdateStoreLightningLikePaymentMethod : StoreLightningLikePaymentMethod
    {
        public bool UseInternalLightningNode { get; set; }
    }
    
    public class StoreLightningLikePaymentMethod
    {
        public bool Enabled { get; set; }
        public bool Default { get; set; }
        public string CryptoCode { get; set; }
        [Required] public string ConnectionString { get; set; }

        public StoreLightningLikePaymentMethod()
        {
        }

        public StoreLightningLikePaymentMethod(LightningSupportedPaymentMethod schemeSettings, bool enabled, bool defaultMethod)
        {
            Enabled = enabled;
            Default = defaultMethod;
            CryptoCode = schemeSettings.PaymentId.CryptoCode;
            ConnectionString = schemeSettings.GetLightningUrl().ToString();
        }
        
        public StoreLightningLikePaymentMethod(string cryptoCode, string  connectionString, bool enabled, bool defaultMethod)
        {
            Enabled = enabled;
            Default = defaultMethod;
            CryptoCode = cryptoCode;
            ConnectionString = connectionString;
        }
    }
}
