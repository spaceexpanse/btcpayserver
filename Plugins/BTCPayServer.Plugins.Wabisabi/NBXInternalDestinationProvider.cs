using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
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


    public async Task<IEnumerable<(IDestination, Money, string)>> GetPayments(ImmutableArray<AliceClient> registeredAliceClients)
    {
        return Array.Empty<(IDestination, Money, string)>();
    }

    public async Task<IEnumerable<IDestination>> GetNextDestinations(int count)
    {
      return  await  Task.WhenAll(Enumerable.Repeat(0, count).Select(_ =>
            _explorerClient.GetUnusedAsync(_derivationStrategy, DerivationFeature.Deposit, 0, true))).ContinueWith(task => task.Result.Select(information => information.Address));
    }
}
