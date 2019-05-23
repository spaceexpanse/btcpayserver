using System.Collections.Generic;
using NBitcoin;
using NBXplorer;

namespace BTCPayServer
{
    public class BitcoinBTCPayNetworkInitializer: BaseBitcoinlikeBTCPayNetworkProvider
    {
        public override string CryptCode => "BTC";
        
        public BitcoinBTCPayNetworkInitializer(NBXplorerNetworkProvider provider) : base(provider)
        {
        }
        public override BTCPayNetwork Initialize(NBXplorerNetwork network, NetworkType networkType)
        {
            return new BTCPayNetwork()
            {
                CryptoCode = network.CryptoCode,
                DisplayName = "Bitcoin",
                BlockExplorerLink =
                    networkType == NetworkType.Mainnet
                        ? "https://www.smartbit.com.au/tx/{0}"
                        : "https://testnet.smartbit.com.au/tx/{0}",
                NBitcoinNetwork = network.NBitcoinNetwork,
                NBXplorerNetwork = network,
                UriScheme = "bitcoin",
                CryptoImagePath = "imlegacy/bitcoin.svg",
                LightningImagePath = "imlegacy/bitcoin-lightning.svg",
                DefaultSettings = BTCPayDefaultSettings.GetDefaultSettings(networkType),
                CoinType = networkType == NetworkType.Mainnet ? new KeyPath("0'") : new KeyPath("1'"),
                SupportRBF = true,
                //https://github.com/spesmilo/electrum/blob/11733d6bc271646a00b69ff07657119598874da4/electrum/constants.py
                ElectrumMapping = networkType == NetworkType.Mainnet
                    ? new Dictionary<uint, DerivationType>()
                    {
                        {0x0488b21eU, DerivationType.Legacy}, // xpub
                        {0x049d7cb2U, DerivationType.SegwitP2SH}, // ypub
                        {0x4b24746U, DerivationType.Segwit}, //zpub
                    }
                    : new Dictionary<uint, DerivationType>()
                    {
                        {0x043587cfU, DerivationType.Legacy},
                        {0x044a5262U, DerivationType.SegwitP2SH},
                        {0x045f1cf6U, DerivationType.Segwit}
                    }
            };
        }
    }
}
