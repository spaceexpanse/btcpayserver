using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer;
using BTCPayServer.Controllers;
using BTCPayServer.Data;
using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Logging;
using BTCPayServer.Models.WalletViewModels;
using BTCPayServer.Payments;
using BTCPayServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Payment;
using NBitcoin.RPC;
using NBXplorer.Models;
using Newtonsoft.Json;
using NewBlockEvent = BTCPayServer.Events.NewBlockEvent;

public class BitcoinLikePayoutHandler : IPayoutHandler
{
    private readonly BTCPayNetworkProvider _btcPayNetworkProvider;
    private readonly ExplorerClientProvider _explorerClientProvider;
    private readonly BTCPayNetworkJsonSerializerSettings _jsonSerializerSettings;
    private readonly ApplicationDbContextFactory _dbContextFactory;
    private readonly EventAggregator _eventAggregator;

    public BitcoinLikePayoutHandler(BTCPayNetworkProvider btcPayNetworkProvider,
        ExplorerClientProvider explorerClientProvider, BTCPayNetworkJsonSerializerSettings jsonSerializerSettings,
        ApplicationDbContextFactory dbContextFactory, EventAggregator eventAggregator)
    {
        _btcPayNetworkProvider = btcPayNetworkProvider;
        _explorerClientProvider = explorerClientProvider;
        _jsonSerializerSettings = jsonSerializerSettings;
        _dbContextFactory = dbContextFactory;
        _eventAggregator = eventAggregator;
    }

    public bool CanHandle(PaymentMethodId paymentMethod)
    {
        return paymentMethod.PaymentType == BitcoinPaymentType.Instance &&
               _btcPayNetworkProvider.GetNetwork<BTCPayNetwork>(paymentMethod.CryptoCode)?.ReadonlyWallet is false;
    }

    public async Task TrackClaim(PaymentMethodId paymentMethodId, IClaimDestination claimDestination)
    {
        if (!CanHandle(paymentMethodId))
        {
            return;
        }

        var network = _btcPayNetworkProvider.GetNetwork<BTCPayNetwork>(paymentMethodId.CryptoCode);
        var explorerClient = _explorerClientProvider.GetExplorerClient(network);
        if (claimDestination is IBitcoinLikeClaimDestination bitcoinLikeClaimDestination)
            await explorerClient.TrackAsync(TrackedSource.Create(bitcoinLikeClaimDestination.Address));
    }

    public async Task<IClaimDestination> ParseClaimDestination(PaymentMethodId paymentMethodId, string destination)
    {
        if (!CanHandle(paymentMethodId))
        {
            return null;
        }

        var network = _btcPayNetworkProvider.GetNetwork<BTCPayNetwork>(paymentMethodId.CryptoCode);
        destination = destination.Trim();
        try
        {
            if (destination.StartsWith($"{network.UriScheme}:", StringComparison.OrdinalIgnoreCase))
            {
                return new UriClaimDestination(new BitcoinUrlBuilder(destination, network.NBitcoinNetwork));
            }

            return new AddressClaimDestination(BitcoinAddress.Create(destination, network.NBitcoinNetwork));
        }
        catch
        {
            return null;
        }
    }

    public IPayoutProof ParseProof(PayoutData payout)
    {
        if (payout?.Proof is null)
            return null;
        var paymentMethodId = payout.GetPaymentMethodId();
        var res =  JsonConvert.DeserializeObject<PayoutTransactionOnChainBlob>(Encoding.UTF8.GetString(payout.Proof), _jsonSerializerSettings.GetSerializer(paymentMethodId.CryptoCode));
        var network = _btcPayNetworkProvider.GetNetwork<BTCPayNetwork>(paymentMethodId.CryptoCode);
        res.LinkTemplate = network.BlockExplorerLink;
        return res;
    }

    public void StartBackgroundCheck(Action<Type[]> subscribe)
    {
        subscribe(new[] {typeof(NewOnChainTransactionEvent), typeof(NewBlockEvent)});
    }

    public async Task BackgroundCheck(object o)
    {
        if (o is NewOnChainTransactionEvent newTransaction)
        {
            await UpdatePayoutsAwaitingForPayment(newTransaction);
        }

        if (o is NewBlockEvent || o is NewOnChainTransactionEvent)
        {
            await UpdatePayoutsInProgress();
        }
    }

    public Task<decimal> GetMinimumPayoutAmount(PaymentMethodId paymentMethodId, IClaimDestination claimDestination)
    {
        if (!CanHandle(paymentMethodId))
        {
            return Task.FromResult(0m);
        }

        if (_btcPayNetworkProvider.GetNetwork<BTCPayNetwork>(paymentMethodId.CryptoCode)?
                .NBitcoinNetwork?
                .Consensus?
                .ConsensusFactory?
                .CreateTxOut() is TxOut txout &&
            claimDestination is IBitcoinLikeClaimDestination bitcoinLikeClaimDestination)
        {
            txout.ScriptPubKey = bitcoinLikeClaimDestination.Address.ScriptPubKey;
            return Task.FromResult(txout.GetDustThreshold(new FeeRate(1.0m)).ToDecimal(MoneyUnit.BTC));
        }

        return Task.FromResult(0m);
    }

    public async Task<IActionResult> CreatePayout(Controller controllerContext, IEnumerable<PayoutData> payouts)
    {
        if(!payouts.Any() || !payouts.All(data => data.GetPaymentMethodId() == payouts.First().GetPaymentMethodId()))
        {
            return null;
        }

        var destandValueList = payouts.Select(data =>
        {
            var y = data.GetBlob(_jsonSerializerSettings);
            return (y.Destination, y.CryptoAmount);
        }).ToArray();
        var walletId = new WalletId(payouts.First().PullPaymentData.StoreId,payouts.First().GetPaymentMethodId().CryptoCode);
        return controllerContext.RedirectToAction(nameof(WalletsController.WalletSend), "Wallets", new
        {
            walletId,
            defaultDestinations = destandValueList.Select(t=>t.Destination).ToArray(),
            defaultAmounts = destandValueList.Select(t=>t.CryptoAmount).ToArray()
        });

    }


    private async Task UpdatePayoutsInProgress()
    {
        try
        {
            using var ctx = _dbContextFactory.CreateContext();
            var payouts = await ctx.Payouts
                .Include(p => p.PullPaymentData)
                .Where(p => p.State == PayoutState.InProgress)
                .ToListAsync();

            foreach (var payout in payouts)
            {
                var proof = ParseProof(payout) as PayoutTransactionOnChainBlob;
                var payoutBlob = payout.GetBlob(this._jsonSerializerSettings);
                if (proof is null)
                {
                    continue;
                }
                foreach (var txid in proof.Candidates.ToList())
                {
                    var explorer = _explorerClientProvider.GetExplorerClient(payout.GetPaymentMethodId().CryptoCode);
                    var tx = await explorer.GetTransactionAsync(txid);
                    if (tx is null)
                    {
                        proof.Candidates.Remove(txid);
                    }
                    else if (tx.Confirmations >= payoutBlob.MinimumConfirmation)
                    {
                        payout.State = PayoutState.Completed;
                        proof.TransactionId = tx.TransactionHash;
                        payout.Destination = null;
                        break;
                    }
                    else
                    {
                        var rebroadcasted = await explorer.BroadcastAsync(tx.Transaction);
                        if (rebroadcasted.RPCCode == RPCErrorCode.RPC_TRANSACTION_ERROR ||
                            rebroadcasted.RPCCode == RPCErrorCode.RPC_TRANSACTION_REJECTED)
                        {
                            proof.Candidates.Remove(txid);
                        }
                        else
                        {
                            payout.State = PayoutState.InProgress;
                            proof.TransactionId = tx.TransactionHash;
                            continue;
                        }
                    }
                }

                if (proof.TransactionId is null && !proof.Candidates.Contains(proof.TransactionId))
                {
                    proof.TransactionId = null;
                }

                if (proof.Candidates.Count == 0)
                {
                    payout.State = PayoutState.AwaitingPayment;
                }
                else if (proof.TransactionId is null)
                {
                    proof.TransactionId = proof.Candidates.First();
                }

                if (payout.State == PayoutState.Completed)
                    proof.Candidates = null;
                SetProofBlob(payout, proof, this._jsonSerializerSettings);
            }

            await ctx.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Logs.PayServer.LogWarning(ex, "Error while processing an update in the pull payment hosted service");
        }
    }

    private async Task UpdatePayoutsAwaitingForPayment(NewOnChainTransactionEvent newTransaction)
    {
        try
        {
            var network = _btcPayNetworkProvider.GetNetwork<BTCPayNetwork>(newTransaction.CryptoCode);
            Dictionary<string, decimal> destinations;
            if (newTransaction.NewTransactionEvent.TrackedSource is AddressTrackedSource addressTrackedSource)
            {
                destinations = new Dictionary<string, decimal>()
                {
                    {
                        addressTrackedSource.Address.ToString(),
                        newTransaction.NewTransactionEvent.Outputs.Sum(output => output.Value.GetValue(network))
                    }
                };
            }
            else
            {
                destinations = newTransaction.NewTransactionEvent.TransactionData.Transaction.Outputs
                    .GroupBy(txout => txout.ScriptPubKey)
                    .ToDictionary(
                        txoutSet => txoutSet.Key.GetDestinationAddress(network.NBitcoinNetwork).ToString(),
                        txoutSet => txoutSet.Sum(txout => txout.Value.ToDecimal(MoneyUnit.BTC)));
            }

            var paymentMethodId = new PaymentMethodId(newTransaction.CryptoCode, BitcoinPaymentType.Instance);

            using var ctx = _dbContextFactory.CreateContext();
            var payouts = await ctx.Payouts
                .Include(o => o.PullPaymentData)
                .Where(p => p.State == PayoutState.AwaitingPayment)
                .Where(p => p.PaymentMethodId == paymentMethodId.ToString())
                .Where(p => destinations.Keys.Contains(p.Destination))
                .ToListAsync();
            var payoutByDestination = payouts.ToDictionary(p => p.Destination);
            foreach (var destination in destinations)
            {
                if (!payoutByDestination.TryGetValue(destination.Key, out var payout))
                    continue;
                var payoutBlob = payout.GetBlob(_jsonSerializerSettings);
                //this is dangerous, a merchant may send an incorrect amount and this would just completely ignore it 
                if (destination.Value != payoutBlob.CryptoAmount)
                    continue;
                var proof = ParseProof(payout) as PayoutTransactionOnChainBlob;
                if (proof is null)
                {
                    proof = new PayoutTransactionOnChainBlob();
                }
                var txId = newTransaction.NewTransactionEvent.TransactionData.TransactionHash;
                if (proof.Candidates.Add(txId))
                {
                    payout.State = PayoutState.InProgress;
                    if (proof.TransactionId is null)
                        proof.TransactionId = txId;
                    SetProofBlob(payout, proof, _jsonSerializerSettings);
                    _eventAggregator.Publish(new UpdateTransactionLabel(
                        new WalletId(payout.PullPaymentData.StoreId, newTransaction.CryptoCode),
                        newTransaction.NewTransactionEvent.TransactionData.TransactionHash,
                        ("#3F88AF", "Payout")));
                }
            }

            await ctx.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Logs.PayServer.LogWarning(ex, "Error while processing a transaction in the pull payment hosted service");
        }
    }
    
    public static void SetProofBlob(PayoutData data, PayoutTransactionOnChainBlob blob, BTCPayNetworkJsonSerializerSettings serializers)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(blob, serializers.GetSerializer(data.GetPaymentMethodId().CryptoCode)));
        // We only update the property if the bytes actually changed, this prevent from hammering the DB too much
        if (data.Proof is null || bytes.Length != data.Proof.Length || !bytes.SequenceEqual(data.Proof))
        {
            data.Proof = bytes;
        }
    }
}
