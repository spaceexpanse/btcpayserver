using System;
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
using Newtonsoft.Json.Linq;
using WalletWasabi.Blockchain.Analysis;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
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
    private readonly ILoggerFactory _loggerFactory;
    private readonly WabisabiStoreCoordinatorSettings _settings;
    private readonly ILogger _logger;
    private static BlockchainAnalyzer BlockchainAnalyzer = new();

    public BTCPayWallet(DerivationStrategyBase derivationScheme, ExplorerClient explorerClient, BTCPayKeyChain keyChain,
        IDestinationProvider destinationProvider, IBTCPayServerClientFactory btcPayServerClientFactory, string storeId,
        WabisabiStoreSettings wabisabiStoreSettings, string coordinatorName, IUTXOLocker utxoLocker,
        ILoggerFactory loggerFactory, Smartifier smartifier)
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
        _smartifier = smartifier;
        _settings = wabisabiStoreSettings.Settings.SingleOrDefault(settings =>
            settings.Coordinator.Equals(CoordinatorName));
        _logger = loggerFactory.CreateLogger($"BTCPayWallet_{storeId}");
    }

    public string StoreId { get; set; }

    public string WalletName => StoreId;
    public bool IsUnderPlebStop =>  !_settings.Enabled;
    public bool IsMixable => ((BTCPayKeyChain)KeyChain).KeysAvailable;
    public IKeyChain KeyChain { get; }
    public IDestinationProvider DestinationProvider { get; }

    public int AnonymitySetTarget => _settings.PlebMode? 2:  _settings.AnonymitySetTarget;
    public bool ConsolidationMode => !_settings.PlebMode && _settings.ConsolidationMode;
    public TimeSpan FeeRateMedianTimeFrame { get; } = TimeSpan.FromHours(KeyManager.DefaultFeeRateMedianTimeFrameHours);
    public bool RedCoinIsolation => !_settings.PlebMode &&_settings.RedCoinIsolation;
    public bool BatchPayments => _settings.BatchPayments;

    public async Task<bool> IsWalletPrivateAsync()
    {
      return await GetPrivacyPercentageAsync()>= 1;
    }

    public async Task<double> GetPrivacyPercentageAsync()
    {
        return GetPrivacyPercentage(await GetAllCoins(), AnonymitySetTarget);
    }

    public async Task<CoinsView> GetAllCoins()
    {
        var client = await _btcPayServerClientFactory.Create(null, StoreId);
        var utxos = await client.GetOnChainWalletUTXOs(StoreId, "BTC");
        await _smartifier.LoadCoins(utxos.ToList());
        var coins = await Task.WhenAll(_smartifier.Coins.Where(pair => utxos.Any(data => data.Outpoint == pair.Key))
            .Select(pair => pair.Value));

        return new CoinsView(coins);
    }

    public double GetPrivacyPercentage(CoinsView coins, int privateThreshold)
    {
        var privateAmount = coins.FilterBy(x => x.HdPubKey.AnonymitySet >= privateThreshold).TotalAmount();
        var normalAmount = coins.FilterBy(x => x.HdPubKey.AnonymitySet < privateThreshold).TotalAmount();

        var privateDecimalAmount = privateAmount.ToDecimal(MoneyUnit.BTC);
        var normalDecimalAmount = normalAmount.ToDecimal(MoneyUnit.BTC);
        var totalDecimalAmount = privateDecimalAmount + normalDecimalAmount;

        var pcPrivate = totalDecimalAmount == 0M ? 1d : (double)(privateDecimalAmount / totalDecimalAmount);
        return pcPrivate;
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

            await _smartifier.LoadCoins(utxos.Where(data => data.Confirmations>0).ToList());
            
            var resultX =  await Task.WhenAll(_smartifier.Coins.Where(pair =>  utxos.Any(data => data.Outpoint == pair.Key))
                .Select(pair => pair.Value));

            foreach (SmartCoin c in resultX)
            {
                var utxo = utxos.Single(coin => coin.Outpoint == c.Outpoint);
                c.Height = new Height((uint) utxo.Confirmations);
            }
            
            return resultX;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not compute coin candidate");
            return Array.Empty<SmartCoin>();
        }
    }


    public async Task<IEnumerable<SmartTransaction>> GetTransactionsAsync()
    {
        return Array.Empty<SmartTransaction>();

    }


    public class CoinjoinData
    {
        public class CoinjoinDataCoin
        {
            public string Outpoint { get; set; }
            public decimal Amount { get; set; }
            public double AnonymitySet { get; set; }
            public string? PayoutId { get; set; }
        }
        public string Round { get; set; }
        public string CoordinatorName { get; set; }
        public string Transaction { get; set; }
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
        public CoinjoinDataCoin[] CoinsIn { get; set; } = Array.Empty<CoinjoinDataCoin>();
        public CoinjoinDataCoin[] CoinsOut { get; set; }= Array.Empty<CoinjoinDataCoin>();
    }


    public async Task RegisterCoinjoinTransaction(CoinJoinResult result)
    {
        try
        {
            
            var client = await _btcPayServerClientFactory.Create(null, StoreId);
            var kp = await _explorerClient.GetMetadataAsync<RootedKeyPath>(_derivationScheme,
                WellknownMetadataKeys.AccountKeyPath);
            
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
                   
                        // await client.AddOrUpdateOnChainWalletLink(StoreId, "BTC", utxoObject,
                        //     new AddOnChainWalletObjectLinkRequest() {Id = "coinjoin-change", Type = "label"},
                        //     CancellationToken.None);
                    }
                }
                await client.AddOrUpdateOnChainWalletObject(StoreId, "BTC",
                    new AddOnChainWalletObjectRequest()
                    {
                        Id = result.RoundId.ToString(),
                        Type = "coinjoin",
                        Data = JObject.FromObject(
                            new CoinjoinData()
                            {
                                Round = result.RoundId.ToString(),
                                CoordinatorName = CoordinatorName,
                                Transaction = result.Transaction.GetHash().ToString(),
                                CoinsIn =   smartTx.WalletInputs.Select(coin => new CoinjoinData.CoinjoinDataCoin()
                                {
                                    AnonymitySet = coin.AnonymitySet,
                                    PayoutId =  null,
                                    Amount = coin.Amount.ToDecimal(MoneyUnit.BTC),
                                    Outpoint = coin.Outpoint.ToString()
                                }).ToArray(),
                                CoinsOut =   smartTx.WalletOutputs.Select(coin => new CoinjoinData.CoinjoinDataCoin()
                                {
                                    AnonymitySet = coin.AnonymitySet,
                                    PayoutId =  null,
                                    Amount = coin.Amount.ToDecimal(MoneyUnit.BTC),
                                    Outpoint = coin.Outpoint.ToString()
                                }).ToArray()
                            })
                    });
                
                await client.AddOrUpdateOnChainWalletLink(StoreId, "BTC", txObject,
                    new AddOnChainWalletObjectLinkRequest() {Id = result.RoundId.ToString(), Type = "coinjoin"},
                    CancellationToken.None);

        }
        catch (Exception e)
        {
            // ignored
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
