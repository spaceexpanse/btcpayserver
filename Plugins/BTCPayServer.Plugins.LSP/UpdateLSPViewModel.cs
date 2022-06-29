using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BTCPayServer.Plugins.LSP;

public class LSPSettings
{
    public bool Enabled { get; set; }
    public uint Minimum { get; set; }
    public uint Maximum { get; set; }
    public decimal FeePerSat { get; set; }
    public uint BaseFee { get; set; }
    public string CustomCSS { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
}

public class LSPViewModel
{
    public LSPSettings Settings { get; set; }
}
