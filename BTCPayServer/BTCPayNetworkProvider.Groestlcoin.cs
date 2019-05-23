using NBitcoin;
using NBXplorer;

namespace BTCPayServer
{
    public class GroestlcoinBTCPayNetworkInitializer: BaseBitcoinlikeBTCPayNetworkProvider
    {
        public override string CryptCode => "GRS";
        
        public GroestlcoinBTCPayNetworkInitializer(NBXplorerNetworkProvider provider) : base(provider)
        {
        }
        public override BTCPayNetwork Initialize(NBXplorerNetwork network, NetworkType networkType)
        {
            return new BTCPayNetwork()
            {
                CryptoCode = network.CryptoCode,
                DisplayName = "Groestlcoin",
                BlockExplorerLink = networkType == NetworkType.Mainnet
                    ? "https://chainz.cryptoid.info/grs/tx.dws?{0}.htm"
                    : "https://chainz.cryptoid.info/grs-test/tx.dws?{0}.htm",
                NBitcoinNetwork = network.NBitcoinNetwork,
                NBXplorerNetwork = network,
                UriScheme = "groestlcoin",
                DefaultRateRules = new[]
                {
                    "GRS_X = GRS_BTC * BTC_X",
                    "GRS_BTC = bittrex(GRS_BTC)"
                },
                CryptoImagePath = "imlegacy/groestlcoin.png",
                LightningImagePath = "imlegacy/groestlcoin-lightning.svg",
                DefaultSettings = BTCPayDefaultSettings.GetDefaultSettings(networkType),
                CoinType = networkType == NetworkType.Mainnet ? new KeyPath("17'") : new KeyPath("1'")
            };
        }
    }
}
