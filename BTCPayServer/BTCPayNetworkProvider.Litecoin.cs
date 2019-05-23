using System.Collections.Generic;
using NBitcoin;
using NBXplorer;

namespace BTCPayServer
{
    public class LitecoinBTCPayNetworkInitializer: BaseBitcoinlikeBTCPayNetworkProvider
    {
        public override string CryptCode => "LTC";
        
        public LitecoinBTCPayNetworkInitializer(NBXplorerNetworkProvider provider) : base(provider)
        {
        }
        public override BTCPayNetwork Initialize(NBXplorerNetwork network, NetworkType networkType)
        {
            return new BTCPayNetwork()
            {
                CryptoCode = network.CryptoCode,
                DisplayName = "Litecoin",
                BlockExplorerLink = networkType == NetworkType.Mainnet
                    ? "https://live.blockcypher.com/ltc/tx/{0}/"
                    : "http://explorer.litecointools.com/tx/{0}",
                NBitcoinNetwork = network.NBitcoinNetwork,
                NBXplorerNetwork = network,
                UriScheme = "litecoin",
                CryptoImagePath = "imlegacy/litecoin.svg",
                LightningImagePath = "imlegacy/litecoin-lightning.svg",
                DefaultSettings = BTCPayDefaultSettings.GetDefaultSettings(networkType),
                CoinType = networkType == NetworkType.Mainnet ? new KeyPath("2'") : new KeyPath("1'"),
                //https://github.com/pooler/electrum-ltc/blob/0d6989a9d2fb2edbea421c116e49d1015c7c5a91/electrum_ltc/constants.py
                ElectrumMapping = networkType == NetworkType.Mainnet
                    ? new Dictionary<uint, DerivationType>()
                    {
                        {0x0488b21eU, DerivationType.Legacy },
                        {0x049d7cb2U, DerivationType.SegwitP2SH },
                        {0x04b24746U, DerivationType.Segwit },
                    }
                    : new Dictionary<uint, DerivationType>()
                    {
                        {0x043587cfU, DerivationType.Legacy },
                        {0x044a5262U, DerivationType.SegwitP2SH },
                        {0x045f1cf6U, DerivationType.Segwit }
                    }
            };
        }
    }
}
