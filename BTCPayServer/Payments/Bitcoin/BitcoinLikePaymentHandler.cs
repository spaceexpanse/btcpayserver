using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Models;
using BTCPayServer.Models.InvoicingModels;
using BTCPayServer.Services;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Rates;
using NBitcoin;
using NBitpayClient;

namespace BTCPayServer.Payments.Bitcoin
{
    public class BitcoinLikePaymentHandler : PaymentMethodHandlerBase<DerivationSchemeSettings>
    {
        ExplorerClientProvider _ExplorerProvider;
        private readonly BTCPayNetworkProvider _networkProvider;
        private IFeeProviderFactory _FeeRateProviderFactory;
        private readonly BTCPayServerEnvironment _environment;
        private Services.Wallets.BTCPayWalletProvider _WalletProvider;

        public BitcoinLikePaymentHandler(ExplorerClientProvider provider,
                                        BTCPayNetworkProvider networkProvider,
                                         IFeeProviderFactory feeRateProviderFactory,
                                        BTCPayServerEnvironment environment,
                                         Services.Wallets.BTCPayWalletProvider walletProvider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            _ExplorerProvider = provider;
            _networkProvider = networkProvider;
            this._FeeRateProviderFactory = feeRateProviderFactory;
            _environment = environment;
            _WalletProvider = walletProvider;
        }

        class Prepare
        {
            public Task<FeeRate> GetFeeRate;
            public Task<BitcoinAddress> ReserveAddress;
        }

        public override async Task PreparePaymentModel(PaymentModel model, InvoiceResponse invoiceResponse)
        {
            var paymentMethodId = new PaymentMethodId(model.CryptoCode, PaymentTypes.BTCLike);
            
            var cryptoInfo = invoiceResponse.CryptoInfo.First(o => o.GetpaymentMethodId() == paymentMethodId);
            var network = _networkProvider.GetNetwork(model.CryptoCode);
            model.IsLightning = false;
            model.PaymentMethodName = GetPaymentMethodName(network);
            model.CryptoImage = GetCryptoImage(network);
            model.PaymentMethodId = await ToString(false, paymentMethodId);
            model.InvoiceBitcoinUrl = cryptoInfo.PaymentUrls.BIP21;
            model.InvoiceBitcoinUrlQR = cryptoInfo.PaymentUrls.BIP21;
        }

        public override string GetCryptoImage(PaymentMethodId paymentMethodId)
        {
            var network = _networkProvider.GetNetwork(paymentMethodId.CryptoCode);
            return GetCryptoImage(network);
        }
        
        private string GetCryptoImage(BTCPayNetwork network)
        {
            return _environment.Context.Request.GetRelativePathOrAbsolute(network.CryptoImagePath);
        }
        public override string GetPaymentMethodName(PaymentMethodId paymentMethodId)
        {
            var network = _networkProvider.GetNetwork(paymentMethodId.CryptoCode);
            return GetPaymentMethodName(network);
        }

        public override IEnumerable<PaymentMethodId> GetSupportedPaymentMethods()
        {
            return _networkProvider.GetAll().Select(network => new PaymentMethodId(network.CryptoCode, PaymentTypes.BTCLike));
        }

        private string GetPaymentMethodName(BTCPayNetwork network)
        {
            return network.DisplayName;
        }
        

        public override object PreparePayment(DerivationSchemeSettings supportedPaymentMethod, StoreData store, BTCPayNetwork network)
        {
            return new Prepare()
            {
                GetFeeRate = _FeeRateProviderFactory.CreateFeeProvider(network).GetFeeRateAsync(),
                ReserveAddress = _WalletProvider.GetWallet(network).ReserveAddressAsync(supportedPaymentMethod.AccountDerivation)
            };
        }

        public override async Task<IPaymentMethodDetails> CreatePaymentMethodDetails(DerivationSchemeSettings supportedPaymentMethod, PaymentMethod paymentMethod, StoreData store, BTCPayNetwork network, object preparePaymentObject)
        {
            if (!_ExplorerProvider.IsAvailable(network))
                throw new PaymentMethodUnavailableException($"Full node not available");
            var prepare = (Prepare)preparePaymentObject;
            Payments.Bitcoin.BitcoinLikeOnChainPaymentMethod onchainMethod = new Payments.Bitcoin.BitcoinLikeOnChainPaymentMethod();
            onchainMethod.NetworkFeeMode = store.GetStoreBlob().NetworkFeeMode;
            onchainMethod.FeeRate = await prepare.GetFeeRate;
            switch (onchainMethod.NetworkFeeMode)
            {
                case NetworkFeeMode.Always:
                    onchainMethod.NextNetworkFee = onchainMethod.FeeRate.GetFee(100); // assume price for 100 bytes
                    break;
                case NetworkFeeMode.Never:
                case NetworkFeeMode.MultiplePaymentsOnly:
                    onchainMethod.NextNetworkFee = Money.Zero;
                    break;
            }
            onchainMethod.DepositAddress = (await prepare.ReserveAddress).ToString();
            return onchainMethod;
        }

        public override bool CanHandle(PaymentMethodId paymentMethodId)
        {
            return paymentMethodId.PaymentType == PaymentTypes.BTCLike;
        }

        
        public override Task<string> ToString(bool pretty, PaymentMethodId paymentMethodId)
        {
            return Task.FromResult(!pretty ? paymentMethodId.CryptoCode : $"{paymentMethodId.CryptoCode} ({ToString()})");
        }
        
        public override string ToString()
        {
            return "On-Chain";
        }
        
        public override Task PrepareInvoiceDto(InvoiceResponse invoiceResponse, InvoiceEntity invoiceEntity,
            InvoiceCryptoInfo invoiceCryptoInfo,
            PaymentMethodAccounting accounting, PaymentMethod info)
        {
            
                
            var scheme = info.Network.UriScheme;
            
                var minerInfo = new MinerFeeInfo();
                minerInfo.TotalFee = accounting.NetworkFee.Satoshi;
                minerInfo.SatoshiPerBytes = ((BitcoinLikeOnChainPaymentMethod)info.GetPaymentMethodDetails()).FeeRate.GetFee(1).Satoshi;
                invoiceResponse.MinerFees.TryAdd(invoiceCryptoInfo.CryptoCode, minerInfo);
                invoiceCryptoInfo.PaymentUrls = new NBitpayClient.InvoicePaymentUrls()
                {
                    BIP21 = $"{scheme}:{invoiceCryptoInfo.Address}?amount={invoiceCryptoInfo.Due}",
                };

#pragma warning disable 618
                if (info.CryptoCode == "BTC")

                {
                    invoiceResponse.BTCPrice = invoiceCryptoInfo.Price;
                    invoiceResponse.Rate = invoiceCryptoInfo.Rate;
                    invoiceResponse.ExRates = invoiceCryptoInfo.ExRates;
                    invoiceResponse.BitcoinAddress = invoiceCryptoInfo.Address;
                    invoiceResponse.BTCPaid = invoiceCryptoInfo.Paid;
                    invoiceResponse.BTCDue = invoiceCryptoInfo.Due;
                    invoiceResponse.PaymentUrls = invoiceCryptoInfo.PaymentUrls;
                }
#pragma warning restore 618
                return Task.CompletedTask;
        }
        
    }
}
