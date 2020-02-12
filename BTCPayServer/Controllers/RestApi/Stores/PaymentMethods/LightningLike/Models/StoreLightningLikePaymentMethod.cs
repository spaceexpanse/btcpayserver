using System.ComponentModel.DataAnnotations;
using BTCPayServer.Payments.Lightning;

namespace BTCPayServer.Controllers.RestApi.Models
{
    public class UpdateStoreLightningLikePaymentMethod : StoreLightningLikePaymentMethod
    {
        /// <summary>
        /// If set to true, will set to use  the deployed lightning node (if there is one) alongside BTCPay Server.
        /// </summary>
        public bool UseInternalLightningNode { get; set; }
    }
    
    public class StoreLightningLikePaymentMethod
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
        /// The lightning node connection string
        /// </summary>
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
