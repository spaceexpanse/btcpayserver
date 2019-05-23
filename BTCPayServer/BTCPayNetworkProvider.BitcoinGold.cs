using NBitcoin;
using NBXplorer;

namespace BTCPayServer
{
    public class BitcoinGoldBTCPayNetworkInitializer: BaseBitcoinlikeBTCPayNetworkProvider
    {

        public override string CryptCode => "BTG";
        
        public BitcoinGoldBTCPayNetworkInitializer(NBXplorerNetworkProvider provider) : base(provider)
        {
        }
        public override BTCPayNetwork Initialize(NBXplorerNetwork network, NetworkType networkType)
        {
            return new BTCPayNetwork()
            {
                CryptoCode = network.CryptoCode,
                DisplayName = "BGold",
                BlockExplorerLink = networkType == NetworkType.Mainnet ? "https://explorer.bitcoingold.org/insight/tx/{0}/" : "https://test-explorer.bitcoingold.org/insight/tx/{0}",
                NBitcoinNetwork = network.NBitcoinNetwork,
                NBXplorerNetwork = network,
                UriScheme = "bitcoingold",
                DefaultRateRules = new[]
                {
                    "BTG_X = BTG_BTC * BTC_X",
                    "BTG_BTC = bitfinex(BTG_BTC)",
                },
                CryptoImagePath = "imlegacy/btg.svg",
                LightningImagePath = "imlegacy/btg-lightning.svg",
                DefaultSettings = BTCPayDefaultSettings.GetDefaultSettings(networkType),
                CoinType = networkType == NetworkType.Mainnet ? new KeyPath("156'") : new KeyPath("1'")
            };
        }
    }
}
