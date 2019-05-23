using NBitcoin;
using NBXplorer;

namespace BTCPayServer
{
    public class FeathercoinBTCPayNetworkInitializer: BaseBitcoinlikeBTCPayNetworkProvider
    {
        public override string CryptCode => "FTC";
        
        public FeathercoinBTCPayNetworkInitializer(NBXplorerNetworkProvider provider) : base(provider)
        {
        }
        public override BTCPayNetwork Initialize(NBXplorerNetwork network, NetworkType networkType)
        {
            return new BTCPayNetwork()
            {
                CryptoCode = network.CryptoCode,
                DisplayName = "Feathercoin",
                BlockExplorerLink = networkType == NetworkType.Mainnet ? "https://explorer.feathercoin.com/tx/{0}" : "https://explorer.feathercoin.com/tx/{0}",
                NBitcoinNetwork = network.NBitcoinNetwork,
                NBXplorerNetwork = network,
                UriScheme = "feathercoin",
                DefaultRateRules = new[] 
                {
                                "FTC_X = FTC_BTC * BTC_X",
                                "FTC_BTC = bittrex(FTC_BTC)"
                },
                CryptoImagePath = "imlegacy/feathercoin.png",
                DefaultSettings = BTCPayDefaultSettings.GetDefaultSettings(networkType),
                CoinType = networkType == NetworkType.Mainnet ? new KeyPath("8'") : new KeyPath("1'")
            };
        }
    }
}
