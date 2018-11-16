using System.ComponentModel.DataAnnotations;
using BTCPayServer.Validation;

namespace BTCPayServer.Controllers.NewApi.Models
{
    public class CreateTokenRequest: CreateTokenRequestBySIN
    {
        [PubKeyValidator] [Required] public string PublicKey { get; set; }
    }
}