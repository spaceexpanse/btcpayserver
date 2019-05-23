using NBitcoin;
using NBXplorer;

namespace BTCPayServer
{
    public class MonacoinBTCPayNetworkInitializer: BaseBitcoinlikeBTCPayNetworkProvider
    {
        public override string CryptCode => "MONA";
        
        public MonacoinBTCPayNetworkInitializer(NBXplorerNetworkProvider provider) : base(provider)
        {
        }
        public override BTCPayNetwork Initialize(NBXplorerNetwork network, NetworkType networkType)
        {
            return new BTCPayNetwork()
            {
                CryptoCode = network.CryptoCode,
                DisplayName = "Monacoin",
                BlockExplorerLink = networkType == NetworkType.Mainnet ? "https://mona.insight.monaco-ex.org/insight/tx/{0}" : "https://testnet-mona.insight.monaco-ex.org/insight/tx/{0}",
                NBitcoinNetwork = network.NBitcoinNetwork,
                NBXplorerNetwork = network,
                UriScheme = "monacoin",
                DefaultRateRules = new[] 
                {
                                "MONA_X = MONA_BTC * BTC_X",
                                "MONA_BTC = bittrex(MONA_BTC)"
                },
                CryptoImagePath = "imlegacy/monacoin.png",
                LightningImagePath = "imlegacy/mona-lightning.svg",
                DefaultSettings = BTCPayDefaultSettings.GetDefaultSettings(networkType),
                CoinType = networkType == NetworkType.Mainnet ? new KeyPath("22'") : new KeyPath("1'")
            };
        }
    }
}
