using System;
using System.Linq;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.Common;
using BTCPayServer.Payments.PayJoin;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Client;
using LogLevel = WalletWasabi.Logging.LogLevel;

namespace BTCPayServer.Plugins.Wabisabi;

public class WabisabiPlugin : BaseBTCPayServerPlugin
{
    private ILogger _logger;
    public override string Identifier => "BTCPayServer.Plugins.Wabisabi";
    public override string Name => "Wabisabi";


    public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } =
    {
        new() {Identifier = nameof(BTCPayServer), Condition = ">=1.6.3.0"}
    };

    public override string Description =>
        "Allows you to integrate with TicketTailor.com to sell tickets for Bitcoin";


    public override void Execute(IServiceCollection applicationBuilder)
    {

        var utxoLocker = new LocalisedUTXOLocker();
        AddCoordinator(applicationBuilder, "zkSNACKS Coordinator", "zksnacks", provider =>
        {
            var chain = provider.GetService<IExplorerClientProvider>().GetExplorerClient("BTC").Network
                .NBitcoinNetwork.ChainName;
            if (chain == ChainName.Mainnet)
            {
                return new Uri("http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion/");
            }

            if (chain == ChainName.Testnet)
            {
                return new Uri("http://testwnp3fugjln6vh5vpj7mvq3lkqqwjj3c2aafyu7laxz42kgwh2rad.onion/");
            }

            return new Uri("http://localhost:37127");
        },utxoLocker);
        applicationBuilder.AddSingleton<WabisabiService>();
        applicationBuilder.AddSingleton<IUIExtension>(new UIExtension("Wabisabi/StoreIntegrationWabisabiOption",
            "store-integrations-list"));
        applicationBuilder.AddSingleton<IUIExtension>(new UIExtension("Wabisabi/WabisabiNav",
            "store-integrations-nav"));
        Logger.SetMinimumLevel(LogLevel.Debug);
        Logger.SetModes(LogMode.Console, LogMode.Debug);
        base.Execute(applicationBuilder);
    }

    // public override void Execute(IApplicationBuilder applicationBuilder, IServiceProvider applicationBuilderApplicationServices)
    // {
    //     Task.Run(async () =>
    //     {
    //         var walletProvider =
    //             (WalletProvider)applicationBuilderApplicationServices.GetRequiredService<IWalletProvider>();
    //         await walletProvider.UnlockUTXOs();
    //     });
    //     base.Execute(applicationBuilder, applicationBuilderApplicationServices);
    // }

    private void AddCoordinator(IServiceCollection serviceCollection, string displayName, string name, Func<IServiceProvider, Uri> fetcher, IUTXOLocker utxoLocker)
    {
        serviceCollection.AddSingleton<IWabisabiCoordinatorManager>(provider => new WabisabiCoordinatorManager(
            displayName, name, fetcher.Invoke(provider), provider.GetService<ILoggerFactory>(), provider, utxoLocker));

        serviceCollection.AddHostedService(s =>
            s.GetServices<IWabisabiCoordinatorManager>().Single(manager => manager.CoordinatorName == name));
    }
}
