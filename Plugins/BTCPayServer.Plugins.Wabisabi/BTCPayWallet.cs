using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Client.Models;
using BTCPayServer.Payments.PayJoin;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBXplorer;
using NBXplorer.DerivationStrategy;
using NBXplorer.Models;
using WalletWasabi.Blockchain.Analysis;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;

namespace BTCPayServer.Plugins.Wabisabi;

public class BTCPayWallet : IWallet
{
    private readonly DerivationStrategyBase _derivationScheme;
    private readonly ExplorerClient _explorerClient;
    private readonly IBTCPayServerClientFactory _btcPayServerClientFactory;
    private readonly WabisabiStoreSettings _wabisabiStoreSettings;
    public string CoordinatorName { get; }
    private readonly IUTXOLocker _utxoLocker;
    private readonly WabisabiStoreCoordinatorSettings _settings;
    private readonly ILogger _logger;
    private static BlockchainAnalyzer BlockchainAnalyzer = new BlockchainAnalyzer();

    public BTCPayWallet(DerivationStrategyBase derivationScheme, ExplorerClient explorerClient, BTCPayKeyChain keyChain,
        IDestinationProvider destinationProvider, IBTCPayServerClientFactory btcPayServerClientFactory, string storeId,
        WabisabiStoreSettings wabisabiStoreSettings, string coordinatorName, IUTXOLocker utxoLocker,
        ILoggerFactory loggerFactory)
    {
        KeyChain = keyChain;
        DestinationProvider = destinationProvider;
        _derivationScheme = derivationScheme;
        _explorerClient = explorerClient;
        _btcPayServerClientFactory = btcPayServerClientFactory;
        StoreId = storeId;
        _wabisabiStoreSettings = wabisabiStoreSettings;
        CoordinatorName = coordinatorName;
        _utxoLocker = utxoLocker;
        _settings = wabisabiStoreSettings.Settings.SingleOrDefault(settings =>
            settings.Coordinator.Equals(CoordinatorName));
        _logger = loggerFactory.CreateLogger($"BTCPayWallet_{derivationScheme}");
    }

    public string StoreId { get; set; }

    public string Identifier => _derivationScheme.ToString();
    public bool IsUnderPlebStop =>  !_settings.Enabled;
    public bool IsMixable => true;
    public IKeyChain KeyChain { get; }
    public IDestinationProvider DestinationProvider { get; }

    public int AnonScoreTarget =>
        string.IsNullOrEmpty(_settings.AnonScoreTarget) ? 2 : int.Parse(_settings.AnonScoreTarget);
    public bool ConsolidationMode => _settings.ConsolidationMode;
    public TimeSpan FeeRateMedianTimeFrame { get; } = TimeSpan.FromHours(KeyManager.DefaultFeeRateMedianTimeFrameHours);
    public bool RedCoinIsolation => _settings.RedCoinIsolation;

    public async Task<bool> IsWalletPrivateAsync()
    {
        return false;
    }

    public async Task<IEnumerable<SmartCoin>> GetCoinjoinCoinCandidatesAsync(int bestHeight)
    {
        try
        {
            if (IsUnderPlebStop)
            {
                return Array.Empty<SmartCoin>();
            }

            var client = await _btcPayServerClientFactory.Create(null, StoreId);
            var utxos = await client.GetOnChainWalletUTXOs(StoreId, "BTC");

            if (_settings.InputLabelsAllowed?.Any() is true)
            {
                utxos = utxos.Where(data =>
                    !_settings.InputLabelsAllowed.Any(s => data.Labels.ContainsKey(s)));
            }

            if (_settings.InputLabelsExcluded?.Any() is true)
            {
                utxos = utxos.Where(data =>
                    _settings.InputLabelsExcluded.All(s => !data.Labels.ContainsKey(s)));
            }

            var locks = await _utxoLocker.FindLocks(utxos.Select(data => data.Outpoint).ToArray());
            utxos = utxos.Where(data => !locks.Contains(data.Outpoint));

            // var utxos = await _explorerClient.GetUTXOsAsync(_derivationScheme);
            var kp = await _explorerClient.GetMetadataAsync<RootedKeyPath>(_derivationScheme,
                WellknownMetadataKeys.AccountKeyPath);
            var result = (await Task.WhenAll(utxos.Where(data => data.Confirmations > 0).Select(async utxo =>
            {
                var tx = await _explorerClient.GetTransactionAsync(_derivationScheme, utxo.Outpoint.Hash);
                
                var smartTx = ConstructSmartTx(tx, utxo.Labels?.Keys?.ToArray(), kp);
                if (smartTx is null)
                {
                    return null;
                }

                return smartTx.WalletOutputs.SingleOrDefault(smartCoin => smartCoin.OutPoint == utxo.Outpoint);
                           
            }))).Where(coin => coin != null);
            return result;
        }
        catch (Exception e)
        {
            Logger.LogError(e);
            return Array.Empty<SmartCoin>();
        }
    }

    private SmartCoin ConstructCoin(KeyPath coinKeyPath, string[] labels, Money amount, uint outputIndex, RootedKeyPath kp, SmartTransaction smartTx)
    {
        SmartCoin coin;
        var derivation = _derivationScheme.GetChild(coinKeyPath).GetExtPubKeys().First().PubKey;
        var hdPubKey = new HdPubKey(derivation, kp.Derive(coinKeyPath).KeyPath, SmartLabel.Empty,
            KeyState.Clean);
        if (string.IsNullOrEmpty(_settings.AnonScoreTarget))
        {
            var anonset = labels.Contains("coinjoin") &&
                          BlockchainAnalyzer.StdDenoms.Contains(amount.Satoshi)
                ? 2
                : 1;
            hdPubKey.SetAnonymitySet(anonset);
        }
        
       
        hdPubKey.SetLabel(new SmartLabel(labels));

        coin = new SmartCoin(smartTx, outputIndex, hdPubKey);
        coin.PropertyChanged += CoinOnPropertyChanged;
        return coin;
    }

    private SmartTransaction ConstructSmartTx(TransactionInformation tx, string[] labels, RootedKeyPath accountKeyPath)
    {
        if (tx.Transaction is null)
        {
            return null;
        }

        var result = new SmartTransaction(tx.Transaction,
            tx.Height is null ? Height.Mempool : new Height((uint)tx.Height.Value), tx.BlockHash);


        foreach (var input in tx.Inputs)
        {
            var derivation = _derivationScheme.GetChild(input.KeyPath).GetExtPubKeys().First().PubKey;
            var hdPubKey = new HdPubKey(derivation, accountKeyPath.Derive(input.KeyPath).KeyPath, SmartLabel.Empty,
                KeyState.Used);
            result.TryAddWalletInput(new SmartCoin(result, (uint)input.Index, hdPubKey));
        }

        foreach (var output in tx.Outputs)
        {
            var coin = ConstructCoin(output.KeyPath, labels ?? Array.Empty<string>(), (Money)output.Value, (uint) output.Index,
                accountKeyPath, result);
            result.TryAddWalletOutput(coin);
        }

        if (!string.IsNullOrEmpty(_settings.AnonScoreTarget))
        {
            BlockchainAnalyzer.Analyze(result);
        }

        return result;
    }

    public async Task<IEnumerable<SmartTransaction>> GetTransactionsAsync()
    {
        var kp = await _explorerClient.GetMetadataAsync<RootedKeyPath>(_derivationScheme,
            WellknownMetadataKeys.AccountKeyPath);
        var client = await _btcPayServerClientFactory.Create(null, StoreId);
        var txs = await client.ShowOnChainWalletTransactions(StoreId, "BTC");
        return (await Task.WhenAll(txs.Select(async data =>
        {
            var tx = await _explorerClient.GetTransactionAsync(_derivationScheme, data.TransactionHash);
            return ConstructSmartTx(tx, data.Labels.Keys.ToArray(), kp);
        }))).Where(transaction => transaction != null);
    }

    public async Task RegisterCoinjoinTransaction(Transaction tx, uint256 round)
    {
        try
        {
            if (_settings.LabelsToAddToCoinjoin?.Any() is true)
            {
                var client = await _btcPayServerClientFactory.Create(null, StoreId);

                await client.PatchOnChainWalletTransaction(StoreId, "BTC", tx.GetHash().ToString(),
                    new PatchOnChainTransactionRequest()
                    {
                        Labels = _settings.LabelsToAddToCoinjoin
                            .Select(s =>
                                s.Replace("{Coordinator}", CoordinatorName).Replace("{RoundId}", round.ToString()))
                            .ToList()
                    },
                    true);
            }
        }
        catch (Exception e)
        {
            // ignored
        }
    }

    private void CoinOnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (sender is SmartCoin smartCoin && e.PropertyName == nameof(SmartCoin.CoinJoinInProgress))
        {
            _ = smartCoin.CoinJoinInProgress
                ? _utxoLocker.TryLock(smartCoin.OutPoint)
                : _utxoLocker.TryUnlock(smartCoin.OutPoint).ContinueWith(task =>
                {
                    if (smartCoin.CoinJoinInProgress)
                    {
                        _logger.LogInformation(
                            $"{(task.Result ? "Success" : "Fail")}: {(smartCoin.CoinJoinInProgress ? "" : "un")}locking coin for coinjoin: {smartCoin.OutPoint} ");
                    }
                });
        }
    }

    public async Task UnlockUTXOs()
    {
        var client = await _btcPayServerClientFactory.Create(null, StoreId);
        var utxos = await client.GetOnChainWalletUTXOs(StoreId, "BTC");
        var unlocked = new List<string>();
        foreach (OnChainWalletUTXOData utxo in utxos)
        {

            if (await _utxoLocker.TryUnlock(utxo.Outpoint))
            {
                unlocked.Add(utxo.Outpoint.ToString());
            }
        }

        _logger.LogInformation($"unlocked utxos: {string.Join(',', unlocked)}");
    }
}
