using System;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BTCPayServer.Plugins.Transmuter
{
    public class TransmuterPlugin : BaseBTCPayServerPlugin
    {
        public override string Identifier => "BTCPayServer.Plugins.Transmuter";
        public override string Name => "Transmuter";


        public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } =
        {
            new() { Identifier = nameof(BTCPayServer), Condition = ">=1.5.4.0" }
        };

        public override string Description =>
            "Allows you to automate workflows";

        public override void Execute(IServiceCollection applicationBuilder)
        {
            base.Execute(applicationBuilder);
        }
    }


    public class TransmuterScript
    {
        public string Script { get; set; }
        public DateTimeOffset LastRun { get; set; }
        public ScriptStatus Status { get; set; }
        public string StoreId { get; set; }
        public enum ScriptStatus    
        {
            Stopped,
            Running
        }
    }
}
