using System;
using Microsoft.Extensions.Hosting;

namespace BTCPayServer.Plugins.Wabisabi;

public interface IWabisabiCoordinatorManager:IHostedService
{
    string CoordinatorDisplayName { get; }
    string CoordinatorName { get; set; }
    Uri Coordinator { get; set; }
}
