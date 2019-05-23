using NBitcoin;
using NBXplorer;

namespace BTCPayServer
{
    public class BitcoinPlusBTCPayNetworkInitializer : BaseBitcoinlikeBTCPayNetworkProvider
    {
        public override string CryptCode => "XBC";

        public BitcoinPlusBTCPayNetworkInitializer(NBXplorerNetworkProvider provider) : base(provider)
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
                DisplayName = "Bitcoinplus",
                BlockExplorerLink =
                    networkType == NetworkType.Mainnet
                        ? "https://chainz.cryptoid.info/xbc/tx.dws?{0}"
                        : "https://chainz.cryptoid.info/xbc/tx.dws?{0}",
                NBitcoinNetwork = network.NBitcoinNetwork,
                NBXplorerNetwork = network,
                UriScheme = "bitcoinplus",
                DefaultRateRules = new[] {"XBC_X = XBC_BTC * BTC_X", "XBC_BTC = cryptopia(XBC_BTC)"},
                CryptoImagePath = "imlegacy/bitcoinplus.png",
                DefaultSettings = BTCPayDefaultSettings.GetDefaultSettings(networkType),
                CoinType = networkType == NetworkType.Mainnet ? new KeyPath("65'") : new KeyPath("1'")
            };

#pragma warning restore 162
        }
    }
}
