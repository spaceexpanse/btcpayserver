using NBitcoin;
using NBXplorer;

namespace BTCPayServer
{
    public class UfoBTCPayNetworkInitializer: BaseBitcoinlikeBTCPayNetworkProvider
    {
        public override string CryptCode => "UFO";
        
        public UfoBTCPayNetworkInitializer(NBXplorerNetworkProvider provider) : base(provider)
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
                DisplayName = "Ufo",
                BlockExplorerLink = networkType == NetworkType.Mainnet ? "https://chainz.cryptoid.info/ufo/tx.dws?{0}" : "https://chainz.cryptoid.info/ufo/tx.dws?{0}",
                NBitcoinNetwork = network.NBitcoinNetwork,
                NBXplorerNetwork = network,
                UriScheme = "ufo",
                DefaultRateRules = new[] 
                {
                                "UFO_X = UFO_BTC * BTC_X",
                                "UFO_BTC = coinexchange(UFO_BTC)"
                },
                CryptoImagePath = "imlegacy/ufo.png",
                DefaultSettings = BTCPayDefaultSettings.GetDefaultSettings(networkType),
                CoinType = networkType == NetworkType.Mainnet ? new KeyPath("202'") : new KeyPath("1'")
            };
#pragma warning restore 162
        }
    }
}
