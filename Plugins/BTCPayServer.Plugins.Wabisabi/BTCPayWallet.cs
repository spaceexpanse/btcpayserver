using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Client;
using BTCPayServer.Client.Models;
using BTCPayServer.Payments.PayJoin;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBXplorer;
using NBXplorer.DerivationStrategy;
using NBXplorer.Models;
using WalletWasabi.Blockchain.Analysis;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;

namespace BTCPayServer.Plugins.Wabisabi;


public class Smartifier
{
    private readonly ExplorerClient _explorerClient;
    private readonly DerivationStrategyBase _derivationStrategyBase;
    private readonly IBTCPayServerClientFactory _btcPayServerClientFactory;
    private readonly string _storeId;

    public Smartifier(ExplorerClient explorerClient, DerivationStrategyBase derivationStrategyBase,
        IBTCPayServerClientFactory btcPayServerClientFactory, string storeId)
    {
        _explorerClient = explorerClient;
        _derivationStrategyBase = derivationStrategyBase;
        _btcPayServerClientFactory = btcPayServerClientFactory;
        _storeId = storeId;
        _accountKeyPath = _explorerClient.GetMetadataAsync<RootedKeyPath>(_derivationStrategyBase,
            WellknownMetadataKeys.AccountKeyPath);
        
    }

    private ConcurrentDictionary<uint256, Task<TransactionInformation>> cached = new();
    public readonly ConcurrentDictionary<uint256, Task<SmartTransaction>> Transactions = new();
    public readonly  ConcurrentDictionary<OutPoint, Task<SmartCoin>> Coins = new();
    private readonly Task<RootedKeyPath> _accountKeyPath;

    public async Task LoadCoins(List<OnChainWalletUTXOData> coins, int current = 1)
    {
        coins = coins.Where(data => data is not null).ToList();
        if (current > 3)
        {
            return;
        }
        var txs = coins.Select(data => data.Outpoint.Hash).Distinct();
        foreach (uint256 tx in txs)
        {
            cached.TryAdd(tx, _explorerClient.GetTransactionAsync(_derivationStrategyBase, tx));
        }

        foreach (OnChainWalletUTXOData coin in coins)
        {
            var client = await _btcPayServerClientFactory.Create(null, _storeId);
            var tx = await Transactions.GetOrAdd(coin.Outpoint.Hash, async uint256 =>
            {
                var unsmartTx = await cached[coin.Outpoint.Hash];
                if (unsmartTx is null)
                {
                    return null;
                }
                var smartTx = new SmartTransaction(unsmartTx.Transaction,
                    unsmartTx.Height is null ? Height.Mempool : new Height((uint)unsmartTx.Height.Value),
                    unsmartTx.BlockHash, firstSeen: unsmartTx.Timestamp);
                //var indexesOfOurSpentInputs = unsmartTx.Inputs.Select(output => (uint)output.Inputndex).ToArray();
                // var ourSpentUtxos = unsmartTx.Transaction.Inputs.AsIndexedInputs()
                //     .Where(@in => indexesOfOurSpentInputs.Contains(@in.Index)).ToDictionary(@in=> @in.Index,@in => @in);


                var ourSpentUtxos = new Dictionary<MatchedOutput, IndexedTxIn>();
                var potentialMatches = new Dictionary<MatchedOutput, IndexedTxIn[]>();
                foreach (MatchedOutput matchedInput in unsmartTx.Inputs)
                {
                    var potentialMatchesForInput = unsmartTx.Transaction.Inputs
                        .AsIndexedInputs()
                        .Where(txIn => txIn.PrevOut.N == matchedInput.Index);
                    potentialMatches.TryAdd(matchedInput, potentialMatchesForInput.ToArray());
                    foreach (IndexedTxIn potentialMatchForInput in potentialMatchesForInput)
                    {
                        var ti = await cached.GetOrAdd(potentialMatchForInput.PrevOut.Hash,
                            _explorerClient.GetTransactionAsync(_derivationStrategyBase,
                                potentialMatchForInput.PrevOut.Hash));

                        if (ti is not null)
                        {
                            MatchedOutput found = ti.Outputs.Find(output =>
                                matchedInput.Index == output.Index &&
                                matchedInput.Value == output.Value &&
                                matchedInput.KeyPath == output.KeyPath &&
                                matchedInput.ScriptPubKey == output.ScriptPubKey
                            );
                            if (found is not null)
                            {
                                ourSpentUtxos.Add(matchedInput, potentialMatchForInput);
                                break;
                            }
                        }
                    }
                }      
                var utxoObjects = await client.GetOnChainWalletObjects(_storeId, "BTC",
                    new GetWalletObjectsRequest()
                    {
                        Ids = ourSpentUtxos.Select(point => point.Value.PrevOut.ToString()).ToArray(),
                        Type = "utxo",
                        IncludeNeighbourData = true
                    });
                var labelsOfOurSpentUtxos =utxoObjects.ToDictionary(data => data.Id,
                    data => data.Links.Where(link => link.Type == "label"));
                
                
                await LoadCoins(unsmartTx.Inputs.Select(output =>
                {
                    if (!ourSpentUtxos.TryGetValue(output, out var outputtxin))
                    {
                        return null;
                    }
                    var outpoint = outputtxin.PrevOut;
                    var labels = labelsOfOurSpentUtxos
                        .GetValueOrDefault(outpoint.ToString(),
                            new List<OnChainWalletObjectData.OnChainWalletObjectLink>())
                        .ToDictionary(link => link.Id, link => new LabelData());
                    return new OnChainWalletUTXOData()
                    {
                        Timestamp = DateTimeOffset.Now,
                        Address =
                            output.Address?.ToString() ?? _explorerClient.Network
                                .CreateAddress(_derivationStrategyBase, output.KeyPath, output.ScriptPubKey)
                                .ToString(),
                        KeyPath = output.KeyPath,
                        Amount = ((Money)output.Value).ToDecimal(MoneyUnit.BTC),
                        Outpoint = outpoint,
                        Labels = labels,
                        Confirmations = unsmartTx.Confirmations
                    };
                }).ToList(),current+1);
                foreach (MatchedOutput input in unsmartTx.Inputs)
                {
                    if (!ourSpentUtxos.TryGetValue(input, out var outputtxin))
                    {
                        continue;
                    }
                    if (Coins.TryGetValue(outputtxin.PrevOut, out var coinTask))
                    {
                        var c = await coinTask;
                        c.SpenderTransaction = smartTx;
                        smartTx.TryAddWalletInput(c);
                        
                    }
                }
                return smartTx;
            });

            var smartCoin = await Coins.GetOrAdd(coin.Outpoint, async point =>
            {

                var unsmartTx = await cached[coin.Outpoint.Hash];
                var pubKey = _derivationStrategyBase.GetChild(coin.KeyPath).GetExtPubKeys().First().PubKey;
                var kp = (await _accountKeyPath).Derive(coin.KeyPath).KeyPath;
                var hdPubKey = new HdPubKey(pubKey, kp, new SmartLabel(coin.Labels.Keys.ToList()),
                    current == 1 ? KeyState.Clean : KeyState.Used);
                var anonsetLabel = coin.Labels.Keys.FirstOrDefault(s => s.StartsWith("anonset-"))
                    ?.Split("-", StringSplitOptions.RemoveEmptyEntries)?.ElementAtOrDefault(1) ?? "1";
                hdPubKey.SetAnonymitySet(double.Parse(anonsetLabel));

                return new SmartCoin(tx, coin.Outpoint.N, hdPubKey);
            });
            tx.TryAddWalletOutput(smartCoin);
            
        }
    }

}


public class BTCPayWallet : IWallet
{
    private readonly DerivationStrategyBase _derivationScheme;
    private readonly ExplorerClient _explorerClient;
    private readonly IBTCPayServerClientFactory _btcPayServerClientFactory;
    private readonly WabisabiStoreSettings _wabisabiStoreSettings;
    public string CoordinatorName { get; }
    private readonly IUTXOLocker _utxoLocker;
    private readonly ILoggerFactory _loggerFactory;
    private readonly WabisabiStoreCoordinatorSettings _settings;
    private readonly ILogger _logger;
    private static BlockchainAnalyzer BlockchainAnalyzer = new();

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
        _loggerFactory = loggerFactory;
        _settings = wabisabiStoreSettings.Settings.SingleOrDefault(settings =>
            settings.Coordinator.Equals(CoordinatorName));
        _logger = loggerFactory.CreateLogger($"BTCPayWallet_{storeId}");
        _smartifier = new Smartifier(explorerClient, _derivationScheme, btcPayServerClientFactory, storeId);
    }

    public string StoreId { get; set; }

    public string WalletName => StoreId;
    public bool IsUnderPlebStop =>  !_settings.Enabled;
    public bool IsMixable => true;
    public IKeyChain KeyChain { get; }
    public IDestinationProvider DestinationProvider { get; }

    public int AnonScoreTarget => _settings.PlebMode? 2:  _settings.AnonScoreTarget ?? 2;
    public bool ConsolidationMode => !_settings.PlebMode && _settings.ConsolidationMode;
    public TimeSpan FeeRateMedianTimeFrame { get; } = TimeSpan.FromHours(KeyManager.DefaultFeeRateMedianTimeFrameHours);
    public bool RedCoinIsolation => !_settings.PlebMode &&_settings.RedCoinIsolation;

    public async Task<bool> IsWalletPrivateAsync()
    {
        return false;
    }

    private IRoundCoinSelector _coinSelector;
    private readonly Smartifier _smartifier;

    public IRoundCoinSelector GetCoinSelector()
    {
        _coinSelector??= new BTCPayCoinjoinCoinSelector(this,  _logger );
        return _coinSelector;
    }

    public async Task<IEnumerable<SmartCoin>> GetCoinjoinCoinCandidatesAsync()
    {
        try
        {
            if (IsUnderPlebStop)
            {
                return Array.Empty<SmartCoin>();
            }

            var client = await _btcPayServerClientFactory.Create(null, StoreId);
            var utxos = await client.GetOnChainWalletUTXOs(StoreId, "BTC");
            
            if (!_settings.PlebMode)
            {
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
            }

            var locks = await _utxoLocker.FindLocks(utxos.Select(data => data.Outpoint).ToArray());
            utxos = utxos.Where(data => !locks.Contains(data.Outpoint)).Where(data => data.Confirmations > 0);

            var kp = await _explorerClient.GetMetadataAsync<RootedKeyPath>(_derivationScheme,
                WellknownMetadataKeys.AccountKeyPath);
            await _smartifier.LoadCoins(utxos.ToList());
            var resultX =  await Task.WhenAll(_smartifier.Coins.Where(pair =>  utxos.Any(data => data.Outpoint == pair.Key))
                .Select(pair => pair.Value));
            foreach (var coin in resultX)
            {
                
                coin.PropertyChanged += CoinOnPropertyChanged;
            }

            return resultX;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not compute coin candidate");
            return Array.Empty<SmartCoin>();
        }
    }

    private SmartCoin ConstructCoin(KeyPath coinKeyPath, string[] labels, Money amount, uint outputIndex, RootedKeyPath kp, SmartTransaction smartTx)
    {
        SmartCoin coin;
        var derivation = _derivationScheme.GetChild(coinKeyPath).GetExtPubKeys().First().PubKey;
        var hdPubKey = new HdPubKey(derivation, kp.Derive(coinKeyPath).KeyPath, SmartLabel.Empty,
            KeyState.Clean);
        var anonsetLabel = labels.FirstOrDefault(s => s.StartsWith("anonset-"))
            ?.Split("-", StringSplitOptions.RemoveEmptyEntries)?.ElementAtOrDefault(1)?? "1";
        hdPubKey.SetAnonymitySet(double.Parse(anonsetLabel));
       
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
            if (hdPubKey.AnonymitySet == HdPubKey.DefaultHighAnonymitySet)
            {
                hdPubKey.SetAnonymitySet(1);
            }

            TxIn txin = null;
            try
            {
                txin = tx.Transaction.Inputs.Find(txIn =>
                    txIn.PrevOut.N == input.Index && txIn.GetSigner().ScriptPubKey == input.ScriptPubKey);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"need input {input.Index} and & index count is{tx.Transaction.Inputs.Count()} \n {tx.TransactionId} {(Money)input.Value}, {input.ScriptPubKey.GetDestinationAddress(_explorerClient.Network.NBitcoinNetwork) }");
            };
            // _explorerClient.GetTransactionAsync(_derivationScheme, tx.Transaction.Inputs[input.Index].PrevOut.Hash)
            var sc = new SmartCoin(txin, (Money)input.Value, input.ScriptPubKey , hdPubKey);
            sc.SpenderTransaction = result;
            result.TryAddWalletInput(sc);
        }

        foreach (var output in tx.Outputs)
        {
            var coin = ConstructCoin(output.KeyPath, labels ?? Array.Empty<string>(), (Money)output.Value, (uint) output.Index,
                accountKeyPath, result);
            result.TryAddWalletOutput(coin);
        }

        return result;
    }

    public async Task<IEnumerable<SmartTransaction>> GetTransactionsAsync()
    {
        return Array.Empty<SmartTransaction>();
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



    public async Task RegisterCoinjoinTransaction(CoinJoinResult result)
    {
        try
        {
            var client = await _btcPayServerClientFactory.Create(null, StoreId);
            var kp = await _explorerClient.GetMetadataAsync<RootedKeyPath>(_derivationScheme,
                WellknownMetadataKeys.AccountKeyPath);
            
            await MarkTransactionAsCoinjoin(result, client);

            List<(IndexedTxOut txout, Task<KeyPathInformation>)> scriptInfos = new();
            await client.AddOrUpdateOnChainWalletObject(StoreId, "BTC", new AddOnChainWalletObjectRequest("label", "coinjoin-std-denom"));
            await client.AddOrUpdateOnChainWalletObject(StoreId, "BTC", new AddOnChainWalletObjectRequest("label", "coinjoin-change"));
            await client.AddOrUpdateOnChainWalletObject(StoreId, "BTC", new AddOnChainWalletObjectRequest("label", "coinjoin-payment"));

            foreach (var script in result.RegisteredOutputs)
            {
                var txout = result.Transaction.Outputs.AsIndexedOutputs()
                    .Single(@out => @out.TxOut.ScriptPubKey == script);

                
                //create the utxo object 
                var newutxo = txout.ToCoin().Outpoint.ToString();
                var utxoObject = new AddOnChainWalletObjectRequest()
                {
                    Id = newutxo,
                    Type = "utxo"
                };
                await client.AddOrUpdateOnChainWalletObject(StoreId, "BTC", utxoObject);
                
                //this was not a mix to self, but rather a payment
                if (result.HandledPayments.Any(pair => pair.Key == txout.TxOut))
                {
                    var payment = result.HandledPayments.Single(pair => pair.Key.ScriptPubKey == script);
                    await client.AddOrUpdateOnChainWalletLink(StoreId, "BTC", utxoObject, new AddOnChainWalletObjectLinkRequest("label", "coinjoin-payment"));
                    continue;
                }

                scriptInfos.Add((txout, _explorerClient.GetKeyInformationAsync(_derivationScheme, script)));
                
            }

            await Task.WhenAll(scriptInfos.Select(t => t.Item2));
                scriptInfos = scriptInfos.Where(tuple => tuple.Item2.Result is not null).ToList();
                var smartTx = new SmartTransaction(result.Transaction, new Height(HeightType.Unknown));
                result.RegisteredCoins.ForEach(coin =>
                {
                    coin.HdPubKey.SetKeyState(KeyState.Used);
                    coin.SpenderTransaction = smartTx;
                    smartTx.TryAddWalletInput(coin);
                });
                
                
                scriptInfos.ForEach(information =>
                {
                    var derivation = _derivationScheme.GetChild(information.Item2.Result.KeyPath).GetExtPubKeys().First().PubKey;
                    var hdPubKey = new HdPubKey(derivation, kp.Derive(information.Item2.Result.KeyPath).KeyPath, SmartLabel.Empty,
                        KeyState.Used);
                    
                    var coin =new SmartCoin(smartTx, information.txout.N, hdPubKey);
                    smartTx.TryAddWalletOutput(coin);
                });
                try
                {

                    BlockchainAnalyzer.Analyze(smartTx);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                foreach (SmartCoin smartTxWalletOutput in smartTx.WalletOutputs)
                {
                    var utxoObject = new AddOnChainWalletObjectRequest()
                    {
                        Id = smartTxWalletOutput.Outpoint.ToString(),
                        Type = "utxo"
                    };
                    await client.AddOrUpdateOnChainWalletObject(StoreId, "BTC", new AddOnChainWalletObjectRequest( "utxo", smartTxWalletOutput.Outpoint.ToString()));
                    await client.AddOrUpdateOnChainWalletObject(StoreId, "BTC", new AddOnChainWalletObjectRequest( "label", $"anonset-{smartTxWalletOutput.AnonymitySet}"));

                    if (smartTxWalletOutput.AnonymitySet != 1)
                    {
                        await client.AddOrUpdateOnChainWalletLink(StoreId, "BTC", utxoObject, 
                            new AddOnChainWalletObjectLinkRequest() {Id =  $"anonset-{smartTxWalletOutput.AnonymitySet}", Type = "label"}, CancellationToken.None);

                    }
                   
                    if (BlockchainAnalyzer.StdDenoms.Contains(smartTxWalletOutput.TxOut.Value.Satoshi))
                    {
                    
                        await client.AddOrUpdateOnChainWalletLink(StoreId, "BTC", utxoObject,
                            new AddOnChainWalletObjectLinkRequest() {Id = "coinjoin-std-denom", Type = "label"},
                            CancellationToken.None);
                    }
                    else
                    {
                   
                        await client.AddOrUpdateOnChainWalletLink(StoreId, "BTC", utxoObject,
                            new AddOnChainWalletObjectLinkRequest() {Id = "coinjoin-change", Type = "label"},
                            CancellationToken.None);
                    }
                }
            
        }
        catch (Exception e)
        {
            // ignored
        }
    }

    private async Task MarkTransactionAsCoinjoin(CoinJoinResult result, BTCPayServerClient client)
    {
        //mark the tx as a coinjoin at a specific coordinator
        var txObject = new AddOnChainWalletObjectRequest() {Id = result.Transaction.GetHash().ToString(), Type = "tx"};
        var labels = new[]
        {
            new AddOnChainWalletObjectRequest() {Id = "coinjoin", Type = "label"},
            new AddOnChainWalletObjectRequest() {Id = CoordinatorName, Type = "label"},
        };

        await client.AddOrUpdateOnChainWalletObject(StoreId, "BTC", txObject);
        foreach (var label in labels)
        {
            await client.AddOrUpdateOnChainWalletObject(StoreId, "BTC", label);
            await client.AddOrUpdateOnChainWalletLink(StoreId, "BTC", txObject, new AddOnChainWalletObjectLinkRequest()
            {
                Id = label.Id,
                Type = label.Type
            }, CancellationToken.None);
        }
    }

    private void CoinOnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (sender is SmartCoin smartCoin && e.PropertyName == nameof(SmartCoin.CoinJoinInProgress))
        {
            
            _logger.LogInformation($"{smartCoin.Outpoint}.CoinJoinInProgress = {smartCoin.CoinJoinInProgress}");
            _ = (smartCoin.CoinJoinInProgress
                ? _utxoLocker.TryLock(smartCoin.Outpoint)
                : _utxoLocker.TryUnlock(smartCoin.Outpoint)).ContinueWith(task =>
                {
                    _logger.LogInformation(
                        $"{(task.Result ? "Success" : "Fail")}: {(smartCoin.CoinJoinInProgress ? "" : "un")}locking coin for coinjoin: {smartCoin.Outpoint} ");
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
