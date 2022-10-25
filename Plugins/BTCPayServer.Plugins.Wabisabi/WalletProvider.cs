using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Common;
using BTCPayServer.Payments.PayJoin;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBXplorer;
using NBXplorer.DerivationStrategy;
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

    public async Task<IEnumerable<IWallet>> GetWalletsAsync()
    {
        var explorerClient = _explorerClientProvider.GetExplorerClient("BTC");
        var status = await explorerClient.GetStatusAsync();
        if (!status.IsFullySynched)
        {
            return Array.Empty<IWallet>();
        }
       return await _memoryCache.GetOrCreateAsync<IEnumerable<IWallet>>("Wabisabi_WalletProvider", async entry =>
        {
            
            var configuredStores =
                await _storeRepository.GetSettingsAsync<WabisabiStoreSettings>(nameof(WabisabiStoreSettings));

            
            var tasks = await Task.WhenAll(configuredStores.Where(pair => pair.Value?.Settings.Any(settings => settings.Coordinator == CoordinatorName && settings.Enabled) is true).Select(
                async pair =>
                {
                    try
                    {
                        var client = await _btcPayServerClientFactory.Create(null, pair.Key);
                        var pm = await client.GetStoreOnChainPaymentMethod(pair.Key, "BTC");
                        
                        if (!pm.Enabled)
                        {
                            return null;
                        }

                        var derivationStrategy =
                            explorerClient.Network.DerivationStrategyFactory.Parse(pm.DerivationScheme);

                        var masterKey = await explorerClient.GetMetadataAsync<BitcoinExtKey>(derivationStrategy,
                            WellknownMetadataKeys.MasterHDKey);
                        var accountKey = await explorerClient.GetMetadataAsync<BitcoinExtKey>(derivationStrategy,
                            WellknownMetadataKeys.AccountHDKey);

                        var keychain = new BTCPayKeyChain(explorerClient, derivationStrategy, masterKey, accountKey);

                        var destinationProvider = new NBXInternalDestinationProvider(explorerClient, derivationStrategy, client, pair.Key);
                        return new BTCPayWallet(derivationStrategy, explorerClient, keychain, destinationProvider, _btcPayServerClientFactory, pair.Key, configuredStores[pair.Key], CoordinatorName, UtxoLocker, _loggerFactory );
                    }
                    catch (Exception e)
                    {
                        return null;
                    }

                }));
            return tasks.Where(wallet => wallet is not null);
        });

    }

    public async Task UnlockUTXOs()
    {
        var wallets = await GetWalletsAsync();
        foreach (BTCPayWallet wallet in wallets)
        {
            await wallet.UnlockUTXOs();
        }
    }
}
