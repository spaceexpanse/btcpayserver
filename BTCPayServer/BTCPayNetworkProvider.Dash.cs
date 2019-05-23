using NBitcoin;
using NBXplorer;

namespace BTCPayServer
{
    public class DashBTCPayNetworkInitializer: BaseBitcoinlikeBTCPayNetworkProvider
    {
        public override string CryptCode => "DASH";
        
        public DashBTCPayNetworkInitializer(NBXplorerNetworkProvider provider) : base(provider)
        {
        }
        public override BTCPayNetwork Initialize(NBXplorerNetwork network, NetworkType networkType)
        {
            return new BTCPayNetwork()
            {
                CryptoCode = network.CryptoCode,
                DisplayName = "Dash",
                BlockExplorerLink = networkType == NetworkType.Mainnet
                    ? "https://insight.dash.org/insight/tx/{0}"
                    : "https://testnet-insight.dashevo.org/insight/tx/{0}",
                NBitcoinNetwork = network.NBitcoinNetwork,
                NBXplorerNetwork = network,
                UriScheme = "dash",
                DefaultRateRules = new[]
                    {
                        "DASH_X = DASH_BTC * BTC_X",
                        "DASH_BTC = bittrex(DASH_BTC)"
                    },
                CryptoImagePath = "imlegacy/dash.png",
                DefaultSettings = BTCPayDefaultSettings.GetDefaultSettings(networkType),
                //https://github.com/satoshilabs/slips/blob/master/slip-0044.md
                CoinType = networkType == NetworkType.Mainnet ? new KeyPath("5'")
                    : new KeyPath("1'")
            };
        }
    }
}
