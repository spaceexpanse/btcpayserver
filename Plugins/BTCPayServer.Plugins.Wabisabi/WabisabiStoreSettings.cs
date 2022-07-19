using System.Collections.Generic;

namespace BTCPayServer.Plugins.Wabisabi;

public class WabisabiStoreSettings
{
    public List<WabisabiStoreCoordinatorSettings> Settings { get; set; } = new();


}

public class WabisabiStoreCoordinatorSettings
{
    public string Coordinator { get; set; }
    public bool Enabled { get; set; } = false;
    public List<string> InputLabelsAllowed { get; set; } = new();
    public List<string> InputLabelsExcluded { get; set; } = new();
    public List<string> LabelsToAddToCoinjoin { get; set; } = new();
    public bool ConsolidationMode { get; set; } = false;
    public bool RedCoinIsolation { get; set; } = false;
    public string AnonScoreTarget { get; set; } = "";
}
