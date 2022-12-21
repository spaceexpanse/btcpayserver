using System.Collections.Generic;
using System.Linq;

namespace BTCPayServer.Plugins.Wabisabi;

public class WabisabiStoreSettings
{
    public List<WabisabiStoreCoordinatorSettings> Settings { get; set; } = new();


}

public class WabisabiStoreCoordinatorSettings
{
    public string Coordinator { get; set; }
    public bool Enabled { get; set; } = false;
    public bool PlebMode { get; set; } = true;
    
    public List<string> InputLabelsAllowed { get; set; } = new();
    public List<string> InputLabelsExcluded { get; set; } = new();
    public bool ConsolidationMode { get; set; } = false;
    public bool RedCoinIsolation { get; set; } = false;
    public int? AnonScoreTarget { get; set; } = null;

    public bool SelectiveCoinjoin => InputLabelsAllowed.Any() || InputLabelsExcluded.Any();

}
