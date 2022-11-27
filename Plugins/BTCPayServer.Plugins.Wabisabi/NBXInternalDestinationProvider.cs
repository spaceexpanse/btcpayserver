using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Client;
using BTCPayServer.Client.Models;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NBitcoin;
using NBitcoin.Payment;
using NBXplorer;
using NBXplorer.DerivationStrategy;
using Newtonsoft.Json.Linq;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;

namespace BTCPayServer.Plugins.Wabisabi;

public class NBXInternalDestinationProvider : IDestinationProvider
{
    private readonly ExplorerClient _explorerClient;
    private readonly DerivationStrategyBase _derivationStrategy;
    private readonly BTCPayServerClient _client;
    private readonly string _storeId;

    public NBXInternalDestinationProvider(ExplorerClient explorerClient, 
        DerivationStrategyBase derivationStrategy, BTCPayServerClient client, string storeId)
    {
        _explorerClient = explorerClient;
        _derivationStrategy = derivationStrategy;
        _client = client;
        _storeId = storeId;
    }

    public async Task<IEnumerable<IDestination>> GetNextDestinations(int count, bool preferTaproot)
    {
      return  await  Task.WhenAll(Enumerable.Repeat(0, count).Select(_ =>
            _explorerClient.GetUnusedAsync(_derivationStrategy, DerivationFeature.Deposit, 0, true))).ContinueWith(task => task.Result.Select(information => information.Address));
    }
    public async Task<IEnumerable<PendingPayment>> GetPendingPayments( RoundParameters roundParameters, ImmutableArray<AliceClient> registeredAliceClients)
    {
        try
        {
           var payouts = await _client.GetStorePayouts(_storeId, false);
           return payouts.Where(data =>
               data.State == PayoutState.AwaitingPayment &&
               data.PaymentMethod.Equals("BTC", StringComparison.InvariantCultureIgnoreCase)).Select(data =>
           {
               IDestination destination = null;
               try
               {

                   var bip21 = new BitcoinUrlBuilder(data.Destination, _explorerClient.Network.NBitcoinNetwork);
                   destination = bip21.Address;
               }
               catch (Exception e)
               {
                   destination = BitcoinAddress.Create(data.Destination, _explorerClient.Network.NBitcoinNetwork);
               }

               return new PendingPayment()
               {
                   Destination = destination,
                   Value = new Money((decimal)data.PaymentMethodAmount, MoneyUnit.BTC),
                   PaymentStarted = PaymentStarted(_storeId, data.Id),
                   PaymentFailed = PaymentFailed(_storeId, data.Id),
                   PaymentSucceeded = PaymentSucceeded(_storeId, data.Id,
                       _explorerClient.Network.NBitcoinNetwork.ChainName == ChainName.Mainnet),
               };
           }).ToArray();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return Array.Empty<PendingPayment>();
        }
    }

    private Action<(uint256 roundId, uint256 transactionId, int outputIndex)> PaymentSucceeded(string storeId, string payoutId, bool mainnet)
    {
        return tuple =>
            _client.MarkPayout(storeId, payoutId, new MarkPayoutRequest()
            {
                State = PayoutState.Completed,
                PaymentProof = JObject.FromObject(new WabisabiPaymentProof()
                {
                    Id = tuple.transactionId.ToString(),
                    OutputIndex = tuple.outputIndex,
                    Link = mainnet? $"https://mempool.space/tx/{tuple.outputIndex}:{tuple.transactionId}#flow" : 
                        $"https://mempool.space/testnet/tx/{tuple.outputIndex}:{tuple.transactionId}#flow"
                })
            });

    }


    private Action PaymentFailed(string storeId, string payoutId)
    {
        return () =>
            _client.MarkPayout(storeId, payoutId, new MarkPayoutRequest() {State = PayoutState.AwaitingPayment});
    }

    private Action PaymentStarted(string storeId, string payoutId)
    {
        return () =>
            _client.MarkPayout(storeId, payoutId, new MarkPayoutRequest() {State = PayoutState.InProgress, PaymentProof = JObject.FromObject(new WabisabiPaymentProof()
            {
                
            })});
    }

    public class WabisabiPaymentProof
    {
        public string Type { get; set; } = "Wabisabi";
        public string Id { get; set; }
        public string Link { get; set; }
        public int OutputIndex { get; set; }
    }
}
