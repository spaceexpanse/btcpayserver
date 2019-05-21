﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Models;
using BTCPayServer.Models.InvoicingModels;
using BTCPayServer.Rating;
using BTCPayServer.Services;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Rates;
using NBitcoin;
using NBitpayClient;
using Newtonsoft.Json;

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

        public override Task PreparePaymentModel(PaymentModel model, InvoiceResponse invoiceResponse)
        {
            var paymentMethodId = new PaymentMethodId(model.CryptoCode, PaymentTypes.BTCLike);
            
            var cryptoInfo = invoiceResponse.CryptoInfo.First(o => o.GetpaymentMethodId() == paymentMethodId);
            var network = _networkProvider.GetNetwork(model.CryptoCode);
            model.IsLightning = false;
            model.PaymentMethodName = GetPaymentMethodName(network);
            model.CryptoImage = GetCryptoImage(network);
            model.PaymentMethodId = ToString(false, paymentMethodId);
            model.InvoiceBitcoinUrl = cryptoInfo.PaymentUrls.BIP21;
            model.InvoiceBitcoinUrlQR = cryptoInfo.PaymentUrls.BIP21;
            return Task.CompletedTask;
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

        public override async Task<string> IsPaymentMethodAllowedBasedOnInvoiceAmount(StoreBlob storeBlob,
            Dictionary<CurrencyPair, Task<RateResult>> rate, Money amount, PaymentMethodId paymentMethodId)
        {
            if (storeBlob.OnChainMinValue == null)
            {
                return null;
            }

            var limitValueRate = await rate[new CurrencyPair(paymentMethodId.CryptoCode, storeBlob.OnChainMinValue.Currency)];
            
            if (limitValueRate.BidAsk != null)
            {
                var limitValueCrypto = Money.Coins(storeBlob.OnChainMinValue.Value / limitValueRate.BidAsk.Bid);

                if (amount > limitValueCrypto)
                {
                    return null;
                }
            }
            return "The amount of the invoice is too low to be paid on chain";
        }

        public override IEnumerable<PaymentMethodId> GetSupportedPaymentMethods()
        {
            return _networkProvider.GetAll()
                .Select(network => new PaymentMethodId(network.CryptoCode, PaymentTypes.BTCLike));
        }

        public override CryptoPaymentData GetCryptoPaymentData(PaymentEntity paymentEntity)
        {
            BitcoinLikePaymentData paymentData;
            if (string.IsNullOrEmpty(paymentEntity.CryptoPaymentDataType))
            {
#pragma warning disable CS0618
                // For invoices created when CryptoPaymentDataType was not existing, we just consider that it is a RBFed payment for safety
                paymentData = new BitcoinLikePaymentData();
                paymentData.Outpoint = paymentEntity.Outpoint;
                paymentData.Output = paymentEntity.Output;
                paymentData.RBF = true;
                paymentData.ConfirmationCount = 0;
                paymentData.Legacy = true;
                return paymentData;
#pragma warning restore CS0618
            }

            paymentData =
                JsonConvert.DeserializeObject<BitcoinLikePaymentData>(paymentEntity.CryptoPaymentData);
#pragma warning disable CS0618
            // legacy
            paymentData.Output = paymentEntity.Output;
            paymentData.Outpoint = paymentEntity.Outpoint;
#pragma warning restore CS0618
            return paymentData;
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

        
        public override string ToString(bool pretty, PaymentMethodId paymentMethodId)
        {
            return !pretty ? paymentMethodId.CryptoCode : $"{paymentMethodId.CryptoCode} ({ToString()})";
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
                minerInfo.SatoshiPerBytes = ((BitcoinLikeOnChainPaymentMethod)info.GetPaymentMethodDetails(new []{this})).FeeRate.GetFee(1).Satoshi;
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
