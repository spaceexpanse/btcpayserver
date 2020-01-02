using System.ComponentModel.DataAnnotations;
using BTCPayServer.Data;
using BTCPayServer.Validation;

namespace BTCPayServer.Controllers.RestApi.Models
{
    public class StoreModel
    {
        public string Id { get; set; }

        [Required]
        [MaxLength(50)]
        [MinLength(1)]
        public string StoreName { get; set; }

        [MaxLength(500)] [Uri] public string StoreWebsite { get; set; }
        [Range(1, 60 * 24 * 24)] public int InvoiceExpiration { get; set; }
        public int MonitoringExpiration { get; set; }
        public SpeedPolicy SpeedPolicy { get; set; }
        public string LightningDescriptionTemplate { get; set; }
        [Range(0, 100)] public double PaymentTolerance { get; set; }
        public bool AnyoneCanCreateInvoice { get; set; }

        public bool ShowRecommendedFee { get; set; }

        public int RecommendedFeeBlockTarget { get; set; }

        public string DefaultLang { get; set; }

        public CurrencyValue OnChainMinValue { get; set; }

        public CurrencyValue LightningMaxValue { get; set; }

        public bool LightningAmountInSatoshi { get; set; }

        public string CustomLogo { get; set; }

        public string CustomCSS { get; set; }

        public string HtmlTitle { get; set; }

        public bool AnyoneCanInvoice { get; set; }

        public bool RedirectAutomatically { get; set; }

        public bool RequiresRefundEmail { get; set; }

        public NetworkFeeMode NetworkFeeMode { get; set; }

        public StoreModel()
        {
        }

        public void SetValues(ref StoreData storeData)
        {
            var blob = storeData.GetStoreBlob();

            storeData.Id = Id;
            storeData.StoreName = StoreName;
            storeData.StoreWebsite = StoreWebsite;
            storeData.SpeedPolicy = SpeedPolicy;
            //we do not include the default payment method in this model and instead opt to set it in the stores/storeid/payment-methods endpoints
            //blob
            //we do not include DefaultCurrencyPairs;Spread; PreferredExchange; RateScripting; RateScript  in this model and instead opt to set it in stores/storeid/rates endpoints
            //we do not include ChangellySettings in this model and instead opt to set it in stores/storeid/changelly endpoints
            //we do not include CoinSwitchSettings in this model and instead opt to set it in stores/storeid/coinswitch endpoints
            //we do not include ExcludedPaymentMethods in this model and instead opt to set it in stores/storeid/payment-methods endpoints
            //we do not include EmailSettings in this model and instead opt to set it in stores/storeid/email endpoints
            blob.NetworkFeeMode = NetworkFeeMode;
            blob.RequiresRefundEmail = RequiresRefundEmail;
            blob.ShowRecommendedFee = ShowRecommendedFee;
            blob.RecommendedFeeBlockTarget = RecommendedFeeBlockTarget;
            blob.DefaultLang = DefaultLang;
            blob.MonitoringExpiration = MonitoringExpiration;
            blob.InvoiceExpiration = InvoiceExpiration;
            blob.OnChainMinValue = OnChainMinValue;
            blob.LightningMaxValue = LightningMaxValue;
            blob.LightningAmountInSatoshi = LightningAmountInSatoshi;
            blob.CustomLogo = CustomLogo;
            blob.CustomCSS = CustomCSS;
            blob.HtmlTitle = HtmlTitle;
            blob.AnyoneCanInvoice = AnyoneCanInvoice;
            blob.LightningDescriptionTemplate = LightningDescriptionTemplate;
            blob.PaymentTolerance = PaymentTolerance;
            blob.RedirectAutomatically = RedirectAutomatically;

            storeData.SetStoreBlob(blob);
        }

        public static StoreModel GetStoreModel(StoreData storeData)
        {
            var storeBlob = storeData.GetStoreBlob();

            return new StoreModel()
            {
                Id = storeData.Id,
                StoreName = storeData.StoreName,
                StoreWebsite = storeData.StoreWebsite,
                SpeedPolicy = storeData.SpeedPolicy,
                //we do not include the default payment method in this model and instead opt to set it in the stores/storeid/payment-methods endpoints
                //blob
                //we do not include DefaultCurrencyPairs,Spread, PreferredExchange, RateScripting, RateScript  in this model and instead opt to set it in stores/storeid/rates endpoints
                //we do not include ChangellySettings in this model and instead opt to set it in stores/storeid/changelly endpoints
                //we do not include CoinSwitchSettings in this model and instead opt to set it in stores/storeid/coinswitch endpoints
                //we do not include ExcludedPaymentMethods in this model and instead opt to set it in stores/storeid/payment-methods endpoints
                //we do not include EmailSettings in this model and instead opt to set it in stores/storeid/email endpoints
                NetworkFeeMode = storeBlob.NetworkFeeMode,
                RequiresRefundEmail = storeBlob.RequiresRefundEmail,
                ShowRecommendedFee = storeBlob.ShowRecommendedFee,
                RecommendedFeeBlockTarget = storeBlob.RecommendedFeeBlockTarget,
                DefaultLang = storeBlob.DefaultLang,
                MonitoringExpiration = storeBlob.MonitoringExpiration,
                InvoiceExpiration = storeBlob.InvoiceExpiration,
                OnChainMinValue = storeBlob.OnChainMinValue,
                LightningMaxValue = storeBlob.LightningMaxValue,
                LightningAmountInSatoshi = storeBlob.LightningAmountInSatoshi,
                CustomLogo = storeBlob.CustomLogo,
                CustomCSS = storeBlob.CustomCSS,
                HtmlTitle = storeBlob.HtmlTitle,
                AnyoneCanInvoice = storeBlob.AnyoneCanInvoice,
                LightningDescriptionTemplate = storeBlob.LightningDescriptionTemplate,
                PaymentTolerance = storeBlob.PaymentTolerance,
                RedirectAutomatically = storeBlob.RedirectAutomatically
            };
        }
    }
}
