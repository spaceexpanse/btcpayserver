using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Controllers.NewApi.Models
{
    public class StoreBtcLikePaymentMethodItem
    {
        public bool Enabled { get; set; }
        [Required] public string DerivationScheme { get; set; }
    }
}