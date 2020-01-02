using System.Collections.Generic;

namespace BTCPayServer.Controllers.RestApi.Models
{
    public class StoreBtcLikePaymentMethodPreviewResult
    {
        public IList<StoreBtcLikePaymentMethodPreviewResultAddressItem> Addresses { get; set; } =
            new List<StoreBtcLikePaymentMethodPreviewResultAddressItem>();

        public class StoreBtcLikePaymentMethodPreviewResultAddressItem
        {
            public string KeyPath { get; set; }
            public string Address { get; set; }
        }
    }
}