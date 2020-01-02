using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Controllers.RestApi.Models;
using BTCPayServer.Controllers.RestApi.Stores.Models;
using BTCPayServer.Data;
using BTCPayServer.PaymentRequest;
using BTCPayServer.Security;
using BTCPayServer.Security.APIKeys;
using BTCPayServer.Services.PaymentRequests;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Controllers.RestApi.Stores
{
    [ApiController]
    [Route("api/v1/stores/{storeId}/payment-requests")]
    [Authorize(Policy = Policies.CanModifyStoreSettings.Key, AuthenticationSchemes = AuthenticationSchemes.ApiKey)]
    [IncludeInOpenApiDocs]
    public class PaymentRequestsController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly PaymentRequestService _paymentRequestService;
        private readonly PaymentRequestRepository _paymentRequestRepository;

        public PaymentRequestsController(UserManager<ApplicationUser> userManager, PaymentRequestService paymentRequestService, PaymentRequestRepository paymentRequestRepository)
        {
            _userManager = userManager;
            _paymentRequestService = paymentRequestService;
            _paymentRequestRepository = paymentRequestRepository;
        }

        [HttpGet("")]
        public async Task<ActionResult<IEnumerable<StoreModel>>> GetPaymentRequests(string storeId)
        {
            var prs = await _paymentRequestRepository.FindPaymentRequests(
                new PaymentRequestQuery() {StoreId = storeId});
            return Ok(prs.Items.Select(PaymentRequestModel.GetPaymentRequestModel));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<StoreModel>> GetPaymentRequestId(string storeId, string id)
        {
            var pr = await _paymentRequestRepository.FindPaymentRequests(
                new PaymentRequestQuery() {StoreId =storeId, Ids = new []{id}});

            if (pr.Total == 0)
            {
                return NotFound();
            }
            return Ok(PaymentRequestModel.GetPaymentRequestModel(pr.Items.First()));
        }

        [HttpPost("")]
        public async Task<ActionResult<StoreModel>> CreatePaymentRequest(string storeId, [FromBody] UpdatePaymentRequest request)
        {
            var pr = new PaymentRequestData()
            {
                StoreDataId = storeId,
                Status = PaymentRequestData.PaymentRequestStatus.Pending,
                Created = DateTimeOffset.Now
            };
            pr.SetBlob(request);
            pr = await _paymentRequestRepository.CreateOrUpdatePaymentRequest(pr);
            return CreatedAtAction(nameof(GetPaymentRequestId), new {storeId = storeId, id = pr.Id}, PaymentRequestModel.GetPaymentRequestModel(pr));
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = Policies.CanModifyStoreSettings.Key, AuthenticationSchemes = AuthenticationSchemes.ApiKey)]
        public async Task<ActionResult> DeletePaymentRequest(string storeId, string id)
        {
            var pr = await _paymentRequestRepository.FindPaymentRequests(
                new PaymentRequestQuery() {StoreId =storeId, Ids = new []{id}});
            if (pr.Total == 0)
            {
                return NotFound();
            }
            var result = await _paymentRequestRepository.RemovePaymentRequest(id, _userManager.GetUserId(User));
            return result ? (ActionResult) Ok() : BadRequest();
        }

        [HttpPut("{id}")]
        [Authorize(Policy = Policies.CanModifyStoreSettings.Key, AuthenticationSchemes = AuthenticationSchemes.ApiKey)]
        public async Task<ActionResult<StoreModel>> UpdateStore(string storeId,string id,  [FromBody] UpdatePaymentRequest request)
        {
            var pr = await _paymentRequestRepository.FindPaymentRequests(
                new PaymentRequestQuery() {StoreId =storeId, Ids = new []{id}});
            if (pr.Total == 0)
            {
                return NotFound();
            }

            var updatedPr = pr.Items.First();
            updatedPr.SetBlob(request);
            
            return Ok(PaymentRequestModel.GetPaymentRequestModel(await _paymentRequestRepository.CreateOrUpdatePaymentRequest(updatedPr)));
        }
    }
}
