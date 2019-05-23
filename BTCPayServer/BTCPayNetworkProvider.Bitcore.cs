using NBitcoin;
using NBXplorer;

namespace BTCPayServer
{
    public class BitcoreBTCPayNetworkInitializer: BaseBitcoinlikeBTCPayNetworkProvider
    {
        public override string CryptCode => "BTX";
        
        public BitcoreBTCPayNetworkInitializer(NBXplorerNetworkProvider provider) : base(provider)
        {
        }
        public override BTCPayNetwork Initialize(NBXplorerNetwork network, NetworkType networkType)
        {
            return new BTCPayNetwork()
            {
                CryptoCode = network.CryptoCode,
                DisplayName = "Bitcore",
                BlockExplorerLink = networkType == NetworkType.Mainnet ? "https://insight.bitcore.cc/tx/{0}" : "https://insight.bitcore.cc/tx/{0}",
                NBitcoinNetwork = network.NBitcoinNetwork,
                NBXplorerNetwork = network,
                UriScheme = "bitcore",
                DefaultRateRules = new[]
                {
                                "BTX_X = BTX_BTC * BTC_X",
                                "BTX_BTC = hitbtc(BTX_BTC)"
                },
                CryptoImagePath = "imlegacy/bitcore.svg",
                LightningImagePath = "imlegacy/bitcore-lightning.svg",
                DefaultSettings = BTCPayDefaultSettings.GetDefaultSettings(networkType),
                CoinType = networkType == NetworkType.Mainnet ? new KeyPath("160'") : new KeyPath("1'")
            };
        }
    }
}
