using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Common;
using BTCPayServer.Configuration;
using BTCPayServer.Plugins.Wabisabi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NBitcoin;
using NBitcoin.RPC;
using NBXplorer;
using Newtonsoft.Json.Linq;
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
    private readonly IServiceProvider _serviceProvider;

    public readonly IdempotencyRequestCache IdempotencyRequestCache;

    private HostedServices HostedServices { get; } = new();
    public WabiSabiCoordinator WabiSabiCoordinator { get; private set; }

    public WabisabiCoordinatorService(ISettingsRepository settingsRepository,
        IOptions<DataDirectories> dataDirectories, IExplorerClientProvider clientProvider, IMemoryCache memoryCache,
        WabisabiCoordinatorClientInstanceManager instanceManager,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration, IServiceProvider serviceProvider)
    {
        _settingsRepository = settingsRepository;
        _dataDirectories = dataDirectories;
        _clientProvider = clientProvider;
        _memoryCache = memoryCache;
        _instanceManager = instanceManager;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
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
    
    public class BtcPayRpcClient: CachedRpcClient
    {
        private readonly ExplorerClient _explorerClient;

        public BtcPayRpcClient(RPCClient rpc, IMemoryCache cache, ExplorerClient explorerClient) : base(rpc, cache)
        {
            _explorerClient = explorerClient;
        }

        public override async Task<Transaction> GetRawTransactionAsync(uint256 txid, bool throwIfNotFound = true, CancellationToken cancellationToken = default)
        {
            var result = (await _explorerClient.GetTransactionAsync(txid, cancellationToken))?.Transaction;
            if (result is null && throwIfNotFound)
            {
                throw new RPCException(RPCErrorCode.RPC_MISC_ERROR, "tx not found", new RPCResponse(new JObject()));
            }

            return result;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var explorerClient = _clientProvider.GetExplorerClient("BTC");
        var coordinatorParameters = new CoordinatorParameters(Path.Combine(_dataDirectories.Value.DataDir, "Plugins", "Coinjoin"));
        var coinJoinIdStore = CoinJoinIdStore.Create(Path.Combine(coordinatorParameters.ApplicationDataDir, "CcjCoordinator", $"CoinJoins{explorerClient.Network}.txt"), coordinatorParameters.CoinJoinIdStoreFilePath);
        var coinJoinScriptStore = CoinJoinScriptStore.LoadFromFile(coordinatorParameters.CoinJoinScriptStoreFilePath);
        var rpc = new BtcPayRpcClient(explorerClient.RPCClient, _memoryCache, explorerClient);
        
        WabiSabiCoordinator = new WabiSabiCoordinator(coordinatorParameters,rpc, coinJoinIdStore, coinJoinScriptStore, _httpClientFactory);
        HostedServices.Register<WabiSabiCoordinator>(() => WabiSabiCoordinator, "WabiSabi Coordinator");
        var settings = await GetSettings();
        if (settings.Enabled is true)
        {
            _ = StartCoordinator(cancellationToken).ContinueWith(async task =>
            {
                Console.Error.WriteLine("BIND:" + _configuration.GetValue("bind", "no value"));
                Console.Error.WriteLine("PORT:" + _configuration.GetValue("port", "no value"));
                var host = await _serviceProvider.GetService<Task<IWebHost>>();
                Console.Error.WriteLine("ADDRESSES:" +  host.ServerFeatures.Get<IServerAddressesFeature>().Addresses.FirstOrDefault());
                
                
                string rootPath = _configuration.GetValue<string>("rootpath", "/");


                var serverAddress = host.ServerFeatures.Get<IServerAddressesFeature>().Addresses.FirstOrDefault();
                _instanceManager.AddCoordinator("Local Coordinator", "local", provider =>
            {
                
                if(!string.IsNullOrEmpty(serverAddress))
                {

                    var serverAddressUri = new Uri(serverAddress);
                    if (new[] {UriHostNameType.IPv4, UriHostNameType.IPv6}.Contains(serverAddressUri.HostNameType))
                    {
                        var ipEndpoint = IPEndPoint.Parse(serverAddressUri.Host);
                        if (Equals(ipEndpoint.Address, IPAddress.Any))
                        {
                            ipEndpoint.Address = IPAddress.Loopback;
                        }

                        if (Equals(ipEndpoint.Address, IPAddress.IPv6Any))
                        {
                            ipEndpoint.Address = IPAddress.Loopback;
                        }

                        UriBuilder builder = new(serverAddressUri);
                        builder.Host = ipEndpoint.Address.ToString();
                        builder.Path =  $"{rootPath}plugins/wabisabi-coordinator";
                        
                        Console.Error.WriteLine($"COORD URL-1: {builder.Uri}");
                        return builder.Uri;
                    }
                }
                
               
                Uri result;
                    var rawBind = _configuration.GetValue("bind", IPAddress.Loopback.ToString()).Split(":", StringSplitOptions.RemoveEmptyEntries);

                    var bindAddress = IPAddress.Parse(rawBind.First());
                    if (Equals(bindAddress, IPAddress.Any))
                    {
                        bindAddress = IPAddress.Loopback;
                    } 
                    if (Equals(bindAddress, IPAddress.IPv6Any))
                    {
                        bindAddress = IPAddress.IPv6Loopback;
                    }

                    int bindPort = rawBind.Length > 2 ? int.Parse(rawBind[1]) : _configuration.GetValue("port", 443);
            

                    result =  new Uri($"https://{bindAddress}:{bindPort}{rootPath}plugins/wabisabi-coordinator");
                    Console.Error.WriteLine($"COORD URL: {result}");
                   
                
                return result;
            });
            }, cancellationToken);
            
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
