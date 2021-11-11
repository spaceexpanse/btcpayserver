﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BTCPayServer.TransferProcessors.Lightning;

public class LightningAutomatedTransferSenderFactory : ITransferProcessorFactory
{
    private readonly BTCPayNetworkProvider _btcPayNetworkProvider;
    private readonly IServiceProvider _serviceProvider;
    private readonly LinkGenerator _linkGenerator;

    public LightningAutomatedTransferSenderFactory(BTCPayNetworkProvider btcPayNetworkProvider, IServiceProvider serviceProvider, LinkGenerator linkGenerator)
    {
        _btcPayNetworkProvider = btcPayNetworkProvider;
        _serviceProvider = serviceProvider;
        _linkGenerator = linkGenerator;
    }

    public string FriendlyName { get; } = "Automated Lightning Sender";

    public string ConfigureLink(string storeId, PaymentMethodId paymentMethodId, HttpRequest request)
    {
        return _linkGenerator.GetUriByAction("Configure", 
            "UILightningAutomatedTransferProcessors",new
            {
                storeId,
                cryptoCode = paymentMethodId.CryptoCode
            }, request.Scheme, request.Host, request.PathBase);
    }
    public string Processor => ProcessorName;
    public static string ProcessorName => nameof(LightningAutomatedTransferSenderFactory);
    public IEnumerable<PaymentMethodId> GetSupportedPaymentMethods()
    {
        return  _btcPayNetworkProvider.GetAll().OfType<BTCPayNetwork>()
            .Where(network => network.SupportLightning)
            .Select(network =>
                new PaymentMethodId(network.CryptoCode, LightningPaymentType.Instance));
    }

    public async Task<IHostedService> ConstructProcessor(TransferProcessorData settings)
    {
        if (settings.Processor != Processor)
        {
            throw new NotSupportedException("This processor cannot handle the provided requirements");
        }

        return ActivatorUtilities.CreateInstance<LightningTransferSender>(_serviceProvider, settings);

    }
}
