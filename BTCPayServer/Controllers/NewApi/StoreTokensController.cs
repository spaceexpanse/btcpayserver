using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Authentication;
using BTCPayServer.Models;
using BTCPayServer.Models.StoreViewModels;
using BTCPayServer.Security;
using BTCPayServer.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using NBitpayClient.Extensions;

namespace BTCPayServer.Controllers.NewApi
{
    [ApiController]
    [Route("api/v0.1/stores/{storeId}/tokens")]
    [Authorize(Policy = Policies.CanModifyStoreSettings.Key)]
    [Authorize]
    public class StoreTokensController : ControllerBase
    {
        private readonly TokenRepository _tokenRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AccessTokenController _accessTokenController;

        public StoreTokensController(TokenRepository tokenRepository, 
            UserManager<ApplicationUser> userManager, AccessTokenController accessTokenController)
        {
            _tokenRepository = tokenRepository;
            _userManager = userManager;
            _accessTokenController = accessTokenController;
        }

        [HttpGet("")]
        public async Task<ActionResult<IEnumerable<BitTokenEntity>>> GetTokens(string storeId)
        {
            return Ok(await _tokenRepository.GetTokensByStoreIdAsync(storeId));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BitTokenEntity>> GetToken(string storeId, string id)
        {
            var result = await _tokenRepository.GetToken(id);
            if (result == null || result.StoreId != storeId)
            {
                return NotFound();
            }

            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> RevokeToken(string storeId, string id)
        {
            var token = await _tokenRepository.GetToken(id);
            if (token == null || token.StoreId != storeId)
            {
                return NotFound();
            }

            var result = await _tokenRepository.DeleteToken(id);
            if (!result)
            {
                BadRequest();
            }

            return Ok();
        }
        
        [HttpPost("")]
        public async Task<ActionResult<BitTokenEntity>> CreateToken(string storeId, [FromBody]CreateTokenRequest request)
        {
            var tokenRequest = new TokenRequest
            {
                Facade = request.Facade,
                Label = request.Label,
                Id = request.PublicKey == null
                    ? null
                    : BitIdExtensions.GetBitIDSIN(new PubKey(request.PublicKey))
            };
            
            string pairingCode = null;
            if (request.PublicKey == null)
            {
                tokenRequest.PairingCode = await _tokenRepository.CreatePairingCodeAsync();
                await _tokenRepository.UpdatePairingCode(new PairingCodeEntity
                {
                    Id = tokenRequest.PairingCode,
                    Facade = request.Facade,
                    Label = request.Label
                });
                var result = await _tokenRepository.PairWithStoreAsync(tokenRequest.PairingCode, storeId);
                if (result == PairingResult.ReusedKey)
                {
                    ModelState.AddModelError(nameof(CreateTokenRequest.PublicKey), "Key was reused.");
                    return BadRequest(ModelState);
                }

                return await GetToken(storeId, tokenRequest.PairingCode);
            }

            pairingCode = (await _accessTokenController.Tokens(tokenRequest)).Data.First().PairingCode;
            
            return await GetToken(storeId, pairingCode);
            
        }
    }

    public class CreateTokenRequest
    {
        [PubKeyValidator]
        public string PublicKey
        {
            get; set;
        }

        public string Label
        {
            get; set;
        }

        [Required]
        public string Facade
        {
            get; set;
        }
    }
}
