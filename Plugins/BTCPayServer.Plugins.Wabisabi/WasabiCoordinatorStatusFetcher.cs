using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Bases;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WebClients.Wasabi;

namespace BTCPayServer.Plugins.Wabisabi;

public class WasabiCoordinatorStatusFetcher : PeriodicRunner, IWasabiBackendStatusProvider
{
    private readonly WasabiClient _wasabiClient;
    private readonly ILogger<WasabiCoordinatorStatusFetcher> _logger;
    public SynchronizeResponse? LastResponse { get; set; }
    public event EventHandler<SynchronizeResponse?> OnResponse;
    public WasabiCoordinatorStatusFetcher(WasabiClient wasabiClient, ILogger<WasabiCoordinatorStatusFetcher> logger) :
        base(TimeSpan.FromSeconds(30))
    {
        _wasabiClient = wasabiClient;
        _logger = logger;
    }

    protected override async Task ActionAsync(CancellationToken cancel)
    {
        try
        {
            LastResponse = await _wasabiClient.GetSynchronizeAsync(uint256.Zero, 0, null, cancel);
            OnResponse?.Invoke(this, LastResponse );
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not connect to the coordinator ");
        }
    }
}
