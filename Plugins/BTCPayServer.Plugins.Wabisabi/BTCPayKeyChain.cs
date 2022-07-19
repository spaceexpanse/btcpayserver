using System.Collections.Generic;
using NBitcoin;
using NBXplorer;
using NBXplorer.DerivationStrategy;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;

namespace BTCPayServer.Plugins.Wabisabi;

public class BTCPayKeyChain: BaseKeyChain
{
    private readonly ExplorerClient _explorerClient;
    private readonly DerivationStrategyBase _derivationStrategy;
    private readonly ExtKey _masterKey;
    private readonly ExtKey _accountKey;

    public BTCPayKeyChain(ExplorerClient explorerClient, DerivationStrategyBase derivationStrategy, ExtKey masterKey, ExtKey accountKey) : base(new Kitchen())
    {
        _explorerClient = explorerClient;
        _derivationStrategy = derivationStrategy;
        _masterKey = masterKey;
        _accountKey = accountKey;
    }

    protected override Key GetMasterKey()
    {
        return _masterKey.PrivateKey;
    }

    public override void TrySetScriptStates(KeyState state, IEnumerable<Script> scripts)
    {
    }

    protected override BitcoinSecret GetBitcoinSecret(Script scriptPubKey)
    {
        var keyPath = _explorerClient.GetKeyInformation(_derivationStrategy, scriptPubKey).KeyPath;
        return _accountKey.Derive(keyPath).PrivateKey.GetBitcoinSecret(_explorerClient.Network.NBitcoinNetwork);
    }
}
