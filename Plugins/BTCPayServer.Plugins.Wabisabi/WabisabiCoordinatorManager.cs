using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Payments.PayJoin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.Userfacing;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.WebClients.Wasabi;

namespace BTCPayServer.Plugins.Wabisabi;

public class WabisabiCoordinatorManager : IWabisabiCoordinatorManager
{
    private readonly IUTXOLocker _utxoLocker;
    private readonly ILogger<WabisabiCoordinatorManager> _logger;
    public string CoordinatorDisplayName { get; }
    public string CoordinatorName { get; set; }
    public Uri Coordinator { get; set; }
    public WalletProvider WalletProvider { get; }
    public HttpClientFactory WasabiHttpClientFactory { get; set; }
    public RoundStateUpdater RoundStateUpdater { get; set; }
    public WasabiCoordinatorStatusFetcher WasabiCoordinatorStatusFetcher { get; set; }
    public CoinJoinManager CoinJoinManager { get; set; }

    public WabisabiCoordinatorManager(string coordinatorDisplayName,string coordinatorName, Uri coordinator, ILoggerFactory loggerFactory, IServiceProvider serviceProvider, IUTXOLocker utxoLocker)
    {
        _utxoLocker = utxoLocker;
        var config = serviceProvider.GetService<IConfiguration>();
        var socksEndpoint = config.GetValue<string>("socksendpoint");
        EndPointParser.TryParse(socksEndpoint,9050, out var torEndpoint);
        if (torEndpoint is not null && torEndpoint is DnsEndPoint dnsEndPoint)
        {
            torEndpoint = new IPEndPoint(Dns.GetHostAddresses(dnsEndPoint.Host).First(), dnsEndPoint.Port);
        }
        CoordinatorDisplayName = coordinatorDisplayName;
        CoordinatorName = coordinatorName;
        Coordinator = coordinator;
        WalletProvider = ActivatorUtilities.CreateInstance<WalletProvider>(serviceProvider);
        WalletProvider.UtxoLocker = _utxoLocker;
        WalletProvider.CoordinatorName = CoordinatorName;
        _logger = loggerFactory.CreateLogger<WabisabiCoordinatorManager>();
        WasabiHttpClientFactory = new HttpClientFactory(torEndpoint, () => Coordinator);
        var roundStateUpdaterCircuit = new PersonCircuit();
        var roundStateUpdaterHttpClient =
            WasabiHttpClientFactory.NewHttpClient(Mode.SingleCircuitPerLifetime, roundStateUpdaterCircuit);
        RoundStateUpdater = new RoundStateUpdater(TimeSpan.FromSeconds(5),
            new WabiSabiHttpApiClient(roundStateUpdaterHttpClient));
        WasabiCoordinatorStatusFetcher = new WasabiCoordinatorStatusFetcher(WasabiHttpClientFactory.SharedWasabiClient,
            loggerFactory.CreateLogger<WasabiCoordinatorStatusFetcher>());
        CoinJoinManager = new CoinJoinManager(WalletProvider, RoundStateUpdater, WasabiHttpClientFactory,
            WasabiCoordinatorStatusFetcher, "CoinJoinCoordinatorIdentifier");
        CoinJoinManager.StatusChanged += OnStatusChanged;
        
    }

    private void OnStatusChanged(object sender, StatusChangedEventArgs e)
    {
        switch (e)
        {
            case CoinJoinStatusEventArgs coinJoinStatusEventArgs:
                switch (coinJoinStatusEventArgs.CoinJoinProgressEventArgs)
                {
                    case RoundEnded roundEnded:
                        
                        switch (roundEnded.LastRoundState.EndRoundState)
                        {
                            
                            case EndRoundState.TransactionBroadcasted:
                                Task.Run(async () =>
                                {
                                    var wallets = await CoinJoinManager.WalletProvider.GetWalletsAsync();
                                    foreach (BTCPayWallet wallet in wallets)
                                    {
                                        await wallet.RegisterCoinjoinTransaction(
                                            roundEnded.LastRoundState.Assert<SigningState>().CreateTransaction(), roundEnded.LastRoundState.Id);
                                    }

                                });
                                break;
                            default:
                                _logger.LogInformation("unlocking coins because round failed");
                                _utxoLocker.TryUnlock(
                                    roundEnded.LastRoundState.CoinjoinState.Inputs.Select(coin => coin.Outpoint).ToArray());
                                break;
                        }
                        break;
                }
                _logger.LogInformation(e.GetType() +
                                       coinJoinStatusEventArgs.CoinJoinProgressEventArgs.GetType().ToString() + "   :" +
                                       e.Wallet.Identifier);
                break;
            case CompletedEventArgs completedEventArgs:
                _logger.LogInformation(e.GetType() + completedEventArgs.CompletionStatus.ToString() + "   :" +
                                       e.Wallet.Identifier);
                break;
            case LoadedEventArgs loadedEventArgs:
                _ = CoinJoinManager.StartAsync(loadedEventArgs.Wallet, false, false, CancellationToken.None);
                _logger.LogInformation(e.GetType() + "   :" + e.Wallet.Identifier);
                break;
            case StartErrorEventArgs errorArgs:
                _logger.LogInformation(e.GetType() + errorArgs.Error.ToString() + "   :" + e.Wallet.Identifier);
                break;
            case StoppedEventArgs stoppedEventArgs:
                _logger.LogInformation(e.GetType() + " " + stoppedEventArgs.Reason + "   :" + e.Wallet.Identifier);
                break;
            default:
                _logger.LogInformation(e.GetType() + "   :" + e.Wallet.Identifier);
                break;
        }
    }


    public Task StartAsync(CancellationToken cancellationToken)
    {
        RoundStateUpdater.StartAsync(cancellationToken);
        WasabiCoordinatorStatusFetcher.StartAsync(cancellationToken);
        CoinJoinManager.StartAsync(cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        RoundStateUpdater.StopAsync(cancellationToken);
        WasabiCoordinatorStatusFetcher.StopAsync(cancellationToken);
        CoinJoinManager.StopAsync(cancellationToken);
        return Task.CompletedTask;
    }
}
