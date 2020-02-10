using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BTCPayServer.Controllers.RestApi.Models;
using BTCPayServer.Security;
using BTCPayServer.Security.Bitpay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using NBitpayClient.Extensions;

namespace BTCPayServer.Controllers.RestApi
{
    [ApiController]
    [Route("api/v0.1/stores/{storeId}/bitpay-tokens")]
    [Authorize(Policy = Policies.CanModifyStoreSettings.Key)]
    [IncludeInOpenApiDocs]
    public class BitPayTokensController : ControllerBase
    {
        private readonly TokenRepository _tokenRepository;

        public BitPayTokensController(TokenRepository tokenRepository)
        {
            _tokenRepository = tokenRepository;
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

        [HttpPost("pair/sin")]
        public async Task<ActionResult<string>> CreateTokenBySIN(string storeId,
            [FromBody] CreateTokenRequestBySIN request)
        {
            var pairingCode = await _tokenRepository.CreatePairingCodeAsync();
            var pairingCodeEntity = await _tokenRepository.UpdatePairingCode(new PairingCodeEntity
            {
                Id = pairingCode,
                Label = request.Label,
            });
            var pairingResult = await _tokenRepository.PairWithStoreAsync(pairingCode, storeId);

            switch (pairingResult)
            {
                case PairingResult.Complete:
                case PairingResult.Partial:
                    return Ok(pairingCode);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        [HttpPost("pair")]
        public async Task<ActionResult<BitTokenEntity>> CreateToken(string storeId,
            [FromBody] CreateTokenRequest request)
        {
            var pairingCode = await _tokenRepository.CreatePairingCodeAsync();
            await _tokenRepository.PairWithSINAsync(pairingCode, new PubKey(request.PublicKey).GetBitIDSIN());
            var pairingCodeEntity = await _tokenRepository.UpdatePairingCode(new PairingCodeEntity()
            {
                Id = pairingCode,
                Label = request.Label
            });


            return await GetToken(storeId, pairingCodeEntity.TokenValue);
        }
    }
}

