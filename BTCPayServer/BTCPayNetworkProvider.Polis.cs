using NBitcoin;
using NBXplorer;

namespace BTCPayServer
{
    public class PolisBTCPayNetworkInitializer: BaseBitcoinlikeBTCPayNetworkProvider
    {
        public override string CryptCode => "POLIS";
        
        public PolisBTCPayNetworkInitializer(NBXplorerNetworkProvider provider) : base(provider)
        {
        }
        public override BTCPayNetwork Initialize(NBXplorerNetwork network, NetworkType networkType)
        {
            
            // Disabled because of https://twitter.com/Cryptopia_NZ/status/1085084168852291586
            return null;
#pragma warning disable 162
            return new BTCPayNetwork()
            {
                CryptoCode = network.CryptoCode,
                DisplayName = "Polis",
                BlockExplorerLink =
                    networkType == NetworkType.Mainnet
                        ? "https://insight.polispay.org/tx/{0}"
                        : "https://insight.polispay.org/tx/{0}",
                NBitcoinNetwork = network.NBitcoinNetwork,
                NBXplorerNetwork = network,
                UriScheme = "polis",
                DefaultRateRules = new[] {"POLIS_X = POLIS_BTC * BTC_X", "POLIS_BTC = cryptopia(POLIS_BTC)"},
                CryptoImagePath = "imlegacy/polis.png",
                DefaultSettings = BTCPayDefaultSettings.GetDefaultSettings(networkType),
                CoinType = networkType == NetworkType.Mainnet ? new KeyPath("1997'") : new KeyPath("1'")
            };
#pragma warning restore 162
        }
    }
}
