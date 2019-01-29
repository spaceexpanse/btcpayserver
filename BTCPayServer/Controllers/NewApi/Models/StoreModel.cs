using System.ComponentModel.DataAnnotations;
using BTCPayServer.Data;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Validation;

namespace BTCPayServer.Controllers.NewApi.Models
{
    public class StoreModel
    {
        public string Id { get; set; }

        [Required]
        [MaxLength(50)]
        [MinLength(1)]
        public string StoreName { get; set; }

        [MaxLength(500)] [Uri] 
        public string StoreWebsite { get; set; }
        public bool CanDelete { get; set; }
        [Range(1, 60 * 24 * 24)] 
        public int InvoiceExpiration { get; set; }
        public int MonitoringExpiration { get; set; }
        public SpeedPolicy SpeedPolicy { get; set; }
        public bool NetworkFee { get; set; }
        public string LightningDescriptionTemplate { get; set; }
        [Range(0, 100)] public double PaymentTolerance { get; set; }
        public bool AnyoneCanCreateInvoice { get; set; }

        public StoreModel()
        {
        }

        public StoreModel(StoreData storeData, bool canDelete)
        {
            var storeBlob = storeData.GetStoreBlob();
            Id = storeData.Id;
            StoreName = storeData.StoreName;
            StoreWebsite = storeData.StoreWebsite;
            NetworkFee = storeBlob.NetworkFeeDisabled?? false;
            AnyoneCanCreateInvoice = storeBlob.AnyoneCanInvoice;
            SpeedPolicy = storeData.SpeedPolicy;
            CanDelete = canDelete;

            MonitoringExpiration = storeBlob.MonitoringExpiration;
            InvoiceExpiration = storeBlob.InvoiceExpiration;
            LightningDescriptionTemplate = storeBlob.LightningDescriptionTemplate;
            PaymentTolerance = storeBlob.PaymentTolerance;
        }
    }
}
