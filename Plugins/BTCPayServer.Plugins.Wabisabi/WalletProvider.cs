using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Client.Models;
using BTCPayServer.Common;
using BTCPayServer.Payments.PayJoin;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBXplorer;
using NBXplorer.DerivationStrategy;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;

namespace BTCPayServer.Plugins.Wabisabi;

public class WalletProvider: IWalletProvider
{
    public string CoordinatorName { get; set; }
    private readonly IStoreRepository _storeRepository;
    private readonly IBTCPayServerClientFactory _btcPayServerClientFactory;
    private readonly IExplorerClientProvider _explorerClientProvider;
    private readonly IMemoryCache _memoryCache;
    public IUTXOLocker UtxoLocker { get; set; }
    private readonly ILoggerFactory _loggerFactory;

    public WalletProvider(IStoreRepository storeRepository, IBTCPayServerClientFactory btcPayServerClientFactory, IExplorerClientProvider explorerClientProvider, IMemoryCache memoryCache, ILoggerFactory loggerFactory)
    {
        _storeRepository = storeRepository;
        _btcPayServerClientFactory = btcPayServerClientFactory;
        _explorerClientProvider = explorerClientProvider;
        _memoryCache = memoryCache;
        _loggerFactory = loggerFactory;
    }

    public async Task<IWallet> GetWalletAsync(string name, WabisabiStoreSettings wabisabiStoreSettings = null)
    {
        wabisabiStoreSettings ??= await
            _storeRepository.GetSettingAsync<WabisabiStoreSettings>(name, nameof(WabisabiStoreSettings));
        return await _memoryCache.GetOrCreateAsync<IWallet>($"Wabisabi_WalletProvider_{name}",
            async cache =>
            {
                cache.AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(5);
                try
                {
                    var client = await _btcPayServerClientFactory.Create(null, name);
                    var pm = await client.GetStoreOnChainPaymentMethod(name, "BTC");

                    if (!pm.Enabled)
                    {
                        return null;
                    }

                    var explorerClient = _explorerClientProvider.GetExplorerClient("BTC");
                    var derivationStrategy =
                        explorerClient.Network.DerivationStrategyFactory.Parse(pm.DerivationScheme);

                    var masterKey = await explorerClient.GetMetadataAsync<BitcoinExtKey>(derivationStrategy,
                        WellknownMetadataKeys.MasterHDKey);
                    var accountKey = await explorerClient.GetMetadataAsync<BitcoinExtKey>(derivationStrategy,
                        WellknownMetadataKeys.AccountHDKey);

                    var keychain = new BTCPayKeyChain(explorerClient, derivationStrategy, masterKey, accountKey);

                    var destinationProvider =
                        new NBXInternalDestinationProvider(explorerClient, derivationStrategy, client, name);

                    async Task<Smartifier> CreateSmartifier()
                    {
                        return await _memoryCache.GetOrCreateAsync($"Wabisabi_Smartifier_{name}",
                            async entry => new Smartifier(explorerClient, derivationStrategy,
                                _btcPayServerClientFactory, name, CoinOnPropertyChanged));
                    }

                    var smartifier = await CreateSmartifier();
                    if (smartifier.DerivationScheme != derivationStrategy)
                    {
                        _memoryCache.Remove($"Wabisabi_Smartifier_{name}");
                        smartifier = await CreateSmartifier();
                    }

                    return new BTCPayWallet(derivationStrategy, explorerClient, keychain, destinationProvider,
                        _btcPayServerClientFactory, name, wabisabiStoreSettings, CoordinatorName, UtxoLocker,
                        _loggerFactory, smartifier);
                }
                catch (Exception e)
                {
                    return null;
                }
            });
    }

    public async Task<IEnumerable<IWallet>> GetWalletsAsync()
    {
        var explorerClient = _explorerClientProvider.GetExplorerClient("BTC");
        var status = await explorerClient.GetStatusAsync();
        if (!status.IsFullySynched)
        {
            return Array.Empty<IWallet>();
        }
        
        var configuredStores =
            await _storeRepository.GetSettingsAsync<WabisabiStoreSettings>(nameof(WabisabiStoreSettings));
        return (await Task.WhenAll(configuredStores.Where(pair => pair.Value?.Settings
                .Any(settings => settings.Coordinator == CoordinatorName && settings.Enabled) is true)
            .Select(pair => GetWalletAsync(pair.Key, pair.Value)))).Where(wallet => wallet is not null);
    }

    public async Task UnlockUTXOs()
    {
        var wallets = await GetWalletsAsync();
        foreach (BTCPayWallet wallet in wallets)
        {
            await wallet.UnlockUTXOs();
        }
    }
    
    
    private void CoinOnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (sender is SmartCoin smartCoin && e.PropertyName == nameof(SmartCoin.CoinJoinInProgress))
        {
            var _logger = _loggerFactory.CreateLogger("");
            // _logger.LogInformation($"{smartCoin.Outpoint}.CoinJoinInProgress = {smartCoin.CoinJoinInProgress}");
            _ = (smartCoin.CoinJoinInProgress
                ? UtxoLocker.TryLock(smartCoin.Outpoint)
                : UtxoLocker.TryUnlock(smartCoin.Outpoint)).ContinueWith(task =>
            {
                // _logger.LogInformation(
                //     $"{(task.Result ? "Success" : "Fail")}: {(smartCoin.CoinJoinInProgress ? "" : "un")}locking coin for coinjoin: {smartCoin.Outpoint} ");
            });
        }
    }

    public async Task ResetWabisabiStuckPayouts()
    {
        var wallets = await GetWalletsAsync();
        foreach (BTCPayWallet wallet in wallets)
        {
           var client = await  _btcPayServerClientFactory.Create(null, wallet.StoreId);
           var payouts = await client.GetStorePayouts(wallet.StoreId);
           var inProgressPayouts = payouts.Where(data =>
               data.State == PayoutState.InProgress && data.PaymentMethod == "BTC" &&
               data.PaymentProof?.Value<string>("proofType") == "Wabisabi");
           foreach (PayoutData payout in inProgressPayouts)
           {
               try
               {
                   var paymentproof =
                       payout.PaymentProof.ToObject<NBXInternalDestinationProvider.WabisabiPaymentProof>();
                   if(paymentproof.Candidates?.Any() is not true)
                    await client.MarkPayout(wallet.StoreId, payout.Id,
                       new MarkPayoutRequest() {State = PayoutState.AwaitingPayment});

               }
               catch (Exception e)
               {
               }
           }
        }
    }
}
