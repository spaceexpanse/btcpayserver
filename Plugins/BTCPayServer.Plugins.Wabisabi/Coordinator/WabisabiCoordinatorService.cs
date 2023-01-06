using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Common;
using BTCPayServer.Configuration;
using BTCPayServer.Plugins.Wabisabi;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Cache;
using WalletWasabi.Services;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using WalletWasabi.WabiSabi.Backend.Statistics;

namespace WalletWasabi.Backend.Controllers;

public class WabisabiCoordinatorService:IHostedService
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly IOptions<DataDirectories> _dataDirectories;
    private readonly IExplorerClientProvider _clientProvider;
    private readonly IMemoryCache _memoryCache;
    private readonly WabisabiCoordinatorClientInstanceManager _instanceManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly LinkGenerator _linkGenerator;

    public readonly IdempotencyRequestCache IdempotencyRequestCache;

    private HostedServices HostedServices { get; } = new();
    public WabiSabiCoordinator WabiSabiCoordinator { get; private set; }

    public WabisabiCoordinatorService(ISettingsRepository settingsRepository,
        IOptions<DataDirectories> dataDirectories, IExplorerClientProvider clientProvider, IMemoryCache memoryCache,
        WabisabiCoordinatorClientInstanceManager instanceManager,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        LinkGenerator linkGenerator)
    {
        _settingsRepository = settingsRepository;
        _dataDirectories = dataDirectories;
        _clientProvider = clientProvider;
        _memoryCache = memoryCache;
        _instanceManager = instanceManager;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _linkGenerator = linkGenerator;
        IdempotencyRequestCache = new(memoryCache);
    }



    public async Task<WabisabiCoordinatorSettings> GetSettings()
    {
        return  (await  _settingsRepository.GetSettingAsync<WabisabiCoordinatorSettings>(nameof(WabisabiCoordinatorSettings)))?? new WabisabiCoordinatorSettings();
    }
    public async Task UpdateSettings(WabisabiCoordinatorSettings wabisabiCoordinatorSettings)
    {
        var existing = await GetSettings();
        if (existing.Enabled != wabisabiCoordinatorSettings.Enabled)
        {
            switch (existing.Enabled)
            {
                case true:
                    
                    await StartCoordinator(CancellationToken.None);
                    break;
                case false:
                    
                    await StopAsync(CancellationToken.None);
                    break;
            }
        }
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var explorerClient = _clientProvider.GetExplorerClient("BTC");
        var coordinatorParameters = new CoordinatorParameters(Path.Combine(_dataDirectories.Value.DataDir, "Plugins", "Coinjoin"));
        var coinJoinIdStore = CoinJoinIdStore.Create(Path.Combine(coordinatorParameters.ApplicationDataDir, "CcjCoordinator", $"CoinJoins{explorerClient.Network}.txt"), coordinatorParameters.CoinJoinIdStoreFilePath);
        var coinJoinScriptStore = CoinJoinScriptStore.LoadFromFile(coordinatorParameters.CoinJoinScriptStoreFilePath);
        var rpc = new CachedRpcClient(explorerClient.RPCClient, _memoryCache);
        WabiSabiCoordinator = new WabiSabiCoordinator(coordinatorParameters, rpc, coinJoinIdStore, coinJoinScriptStore, _httpClientFactory);
        HostedServices.Register<WabiSabiCoordinator>(() => WabiSabiCoordinator, "WabiSabi Coordinator");
        var settings = await GetSettings();
        if (settings.Enabled is true)
        {
            _ = StartCoordinator(cancellationToken);
            _instanceManager.AddCoordinator("Local Coordinator", "local", provider =>
            {
                var bindAddress = _configuration.GetValue("bind", IPAddress.Loopback);
                if (Equals(bindAddress, IPAddress.Any))
                {
                    bindAddress = IPAddress.Loopback;
                } 
                if (Equals(bindAddress, IPAddress.IPv6Any))
                {
                    bindAddress = IPAddress.IPv6Loopback;
                }
                int bindPort = _configuration.GetValue<int>("port", 443);
            
                string rootPath = _configuration.GetValue<string>("rootpath", "/");

                return new Uri($"https://{bindAddress}:{bindPort}{rootPath}plugins/wabisabi-coordinator");
            });
        }
    }

    public async Task StartCoordinator(CancellationToken cancellationToken)
    {
        

        await HostedServices.StartAllAsync(cancellationToken);

    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await HostedServices.StopAllAsync(cancellationToken);
    }
}
