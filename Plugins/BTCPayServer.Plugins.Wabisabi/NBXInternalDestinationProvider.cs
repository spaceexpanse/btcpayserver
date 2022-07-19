using System.Collections.Generic;
using NBitcoin;
using NBXplorer;
using NBXplorer.DerivationStrategy;
using WalletWasabi.WabiSabi.Client;

namespace BTCPayServer.Plugins.Wabisabi;

public class NBXInternalDestinationProvider : IDestinationProvider
{
    private readonly ExplorerClient _explorerClient;
    private readonly DerivationStrategyBase _derivationStrategy;

    public NBXInternalDestinationProvider(ExplorerClient explorerClient, DerivationStrategyBase derivationStrategy)
    {
        _explorerClient = explorerClient;
        _derivationStrategy = derivationStrategy;
    }

    public IEnumerable<IDestination> GetNextDestinations(int count)
    {
        var res = new List<IDestination>();
        for (var i = 0; i < count; i++)
        {
            var kpi = _explorerClient.GetUnused(_derivationStrategy, DerivationFeature.Deposit);
            res.Add(kpi.Address);
        }

        return res;
    }
}