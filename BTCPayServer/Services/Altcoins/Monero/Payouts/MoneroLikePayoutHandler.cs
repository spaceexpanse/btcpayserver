using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Client.Models;
using BTCPayServer.Data;
using BTCPayServer.HostedServices;
using BTCPayServer.Payments;
using BTCPayServer.Services.Altcoins.Monero.Payments;
using BTCPayServer.Services.Altcoins.Monero.RPC;
using BTCPayServer.Services.Altcoins.Monero.Services;
using BTCPayServer.Services.Altcoins.Monero.Utils;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PayoutData = BTCPayServer.Data.PayoutData;
using StoreData = BTCPayServer.Data.StoreData;

namespace BTCPayServer.Services.Altcoins.Monero.Payouts;

public class MoneroClaimDestination: IClaimDestination
{
    public string PaymentRequest { get; set; }
    public string Address { get; set; }
    
    public string Id => Address;
    public decimal? Amount { get; set; }
    public override string ToString()
    {
        return PaymentRequest ?? Address;
    }
}
public class MoneroLikePayoutHandler : IPayoutHandler
{
    private readonly BTCPayNetworkProvider _btcPayNetworkProvider;
    private readonly MoneroRPCProvider _moneroRpcProvider;

    public MoneroLikePayoutHandler(BTCPayNetworkProvider btcPayNetworkProvider,
        MoneroRPCProvider moneroRpcProvider)
    {
        _btcPayNetworkProvider = btcPayNetworkProvider;
        _moneroRpcProvider = moneroRpcProvider;
    }


    public bool CanHandle(PaymentMethodId paymentMethod)
    {
        return paymentMethod?.PaymentType == MoneroPaymentType.Instance &&
               _btcPayNetworkProvider.GetNetwork<MoneroLikeSpecificBtcPayNetwork>(paymentMethod.CryptoCode) is not null;
    }
    
    public class ParseUriRequest
    {
        [JsonProperty("uri ")] public string Uri { get; set; }
    }
    public class ParseUriResponse
    {
        [JsonProperty("amount")] public ulong Amount { get; set; }
        [JsonProperty("address")] public string Address { get; set; }
    }
    public class ValidateAddressRequest
    {
        [JsonProperty("address")] public string Address { get; set; }
    }
    public class ValidateAddressResponse
    {
        [JsonProperty("valid")] public bool Valid { get; set; }
    }

    public Task TrackClaim(ClaimRequest claimRequest, PayoutData payoutData)
    {
        return Task.CompletedTask;
    }

    public  async Task<(IClaimDestination destination, string error)> ParseClaimDestination(PaymentMethodId paymentMethodId, string destination, CancellationToken cancellationToken)
    {
        var network = _btcPayNetworkProvider.GetNetwork<MoneroLikeSpecificBtcPayNetwork>(paymentMethodId.CryptoCode);
        if (!_moneroRpcProvider.IsAvailable(paymentMethodId.CryptoCode))
        {
            return (null, "the node is not available");
        }
        destination = destination.Trim();
        try
        {

            var rpc = _moneroRpcProvider.WalletRpcClients[paymentMethodId.CryptoCode];
            

            if (destination.StartsWith($"{network.UriScheme}:", StringComparison.OrdinalIgnoreCase))
            {

                var response = await rpc.SendCommandAsync<ParseUriRequest, ParseUriResponse>("parse_uri",
                    new ParseUriRequest() {Uri = destination}, cancellationToken);

                if (!string.IsNullOrEmpty(response.Address))
                {
                    return (
                        new MoneroClaimDestination()
                        {
                            Address = response.Address,
                            Amount = response.Amount > 0 ? MoneroMoney.Convert(response.Amount) : null,
                            PaymentRequest = destination
                        }, null);
                }
            }
            else
            {
                var response = await rpc.SendCommandAsync<ValidateAddressRequest, ValidateAddressResponse>("validate_address",
                    new ValidateAddressRequest() {Address = destination}, cancellationToken);

                if (response.Valid)
                {
                    return (
                        new MoneroClaimDestination()
                        {
                            Address = destination,
                            Amount = null
                        }, null);
                }
            }

        }
        catch (JsonRpcClient.JsonRpcApiException jsonRpcApiException)
        {
            
            return (null, jsonRpcApiException.Message);
        }
        catch
        {
            // ignored
        }

        return (null,  "A valid address was not provided");
    }

    public (bool valid, string error) ValidateClaimDestination(IClaimDestination claimDestination, PullPaymentBlob pullPaymentBlob)
    {
        return (true, null);
    }

    public IPayoutProof ParseProof(PayoutData payout)
    {
        if (payout?.Proof is null)
            return null;
        var paymentMethodId = payout.GetPaymentMethodId();
        if (paymentMethodId is null)
        {
            return null;
        }

        BitcoinLikePayoutHandler.ParseProofType(payout.Proof, out var raw, out var proofType);
        return raw.ToObject<ManualPayoutProof>();
    }


    public void StartBackgroundCheck(Action<Type[]> subscribe)
    {
    }

    public async Task BackgroundCheck(object o)
    {
    }

    public Task<decimal> GetMinimumPayoutAmount(PaymentMethodId paymentMethodId, IClaimDestination claimDestination)
    {
        return Task.FromResult(0m);
    }


    public Dictionary<PayoutState, List<(string Action, string Text)>> GetPayoutSpecificActions()
    {
        return new();
    }

    public Task<StatusMessageModel> DoSpecificAction(string action, string[] payoutIds, string storeId)
    {
        return Task.FromResult<StatusMessageModel>(null);
    }

    public Task<IEnumerable<PaymentMethodId>> GetSupportedPaymentMethods(StoreData storeData)
    {
        return Task.FromResult(storeData.GetEnabledPaymentIds(_btcPayNetworkProvider)
            .Where(id => id.PaymentType == MoneroPaymentType.Instance));
    }

    public async Task<IActionResult> InitiatePayment(PaymentMethodId paymentMethodId, string[] payoutIds)
    {
        return new NotFoundResult();
    }
}
