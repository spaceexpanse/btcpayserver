using System.ComponentModel.DataAnnotations;
using BTCPayServer.Validation;

namespace BTCPayServer.Controllers.NewApi.Models
{
    public class CreateTokenRequestBySIN
    {
        public string Label { get; set; }

        [StringRange(AllowableValues = new[] {"merchant", "pos"})]
        [Required]
        public string Facade { get; set; }
    }
}