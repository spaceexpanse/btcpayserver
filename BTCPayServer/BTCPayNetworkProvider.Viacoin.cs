using NBitcoin;
using NBXplorer;

namespace BTCPayServer
{
    public class ViacoinBTCPayNetworkInitializer: BaseBitcoinlikeBTCPayNetworkProvider
    {
        public override string CryptCode => "VIA";
        
        public ViacoinBTCPayNetworkInitializer(NBXplorerNetworkProvider provider) : base(provider)
        {
        }
        public override BTCPayNetwork Initialize(NBXplorerNetwork network, NetworkType networkType)
        {
            return new BTCPayNetwork()
            {
                CryptoCode = network.CryptoCode,
                DisplayName = "Viacoin",
                BlockExplorerLink = networkType == NetworkType.Mainnet ? "https://explorer.viacoin.org/tx/{0}" : "https://explorer.viacoin.org/tx/{0}",
                NBitcoinNetwork = network.NBitcoinNetwork,
                NBXplorerNetwork = network,
                UriScheme = "viacoin",
                DefaultRateRules = new[]
                {
                                "VIA_X = VIA_BTC * BTC_X",
                                "VIA_BTC = bittrex(VIA_BTC)"
                },
                CryptoImagePath = "imlegacy/viacoin.png",
                DefaultSettings = BTCPayDefaultSettings.GetDefaultSettings(networkType),
                CoinType = networkType == NetworkType.Mainnet ? new KeyPath("14'") : new KeyPath("1'")
            };
        }
    }
}
