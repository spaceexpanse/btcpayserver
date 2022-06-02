dotnet publish -c Altcoins-Release -o bin/publish/BTCPayServer.Plugins.Transmuter
dotnet run -p ../../BTCPayServer.PluginPacker bin/publish/BTCPayServer.Plugins.Transmuter BTCPayServer.Plugins.Transmuter ../packed
