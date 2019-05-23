using NBitcoin;
using NBXplorer;

namespace BTCPayServer
{
    public class DogecoinBTCPayNetworkInitializer: BaseBitcoinlikeBTCPayNetworkProvider
    {
        public override string CryptCode => "DOGE";
        
        public DogecoinBTCPayNetworkInitializer(NBXplorerNetworkProvider provider) : base(provider)
        {
        }
        public override BTCPayNetwork Initialize(NBXplorerNetwork network, NetworkType networkType)
        {
            return new BTCPayNetwork()
            {
                CryptoCode = network.CryptoCode,
                DisplayName = "Dogecoin",
                BlockExplorerLink = networkType == NetworkType.Mainnet ? "https://dogechain.info/tx/{0}" : "https://dogechain.info/tx/{0}",
                NBitcoinNetwork = network.NBitcoinNetwork,
                NBXplorerNetwork = network,
                UriScheme = "dogecoin",
                DefaultRateRules = new[] 
                {
                                "DOGE_X = DOGE_BTC * BTC_X",
                                "DOGE_BTC = bittrex(DOGE_BTC)"
                },
                CryptoImagePath = "imlegacy/dogecoin.png",
                DefaultSettings = BTCPayDefaultSettings.GetDefaultSettings(networkType),
                CoinType = networkType == NetworkType.Mainnet ? new KeyPath("3'") : new KeyPath("1'")
            };
        }
    }
}
