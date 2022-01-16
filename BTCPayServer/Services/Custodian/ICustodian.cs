using System.Collections.Generic;

namespace BTCPayServer.Services.Custodian;

public interface ICustodian
{
    /**
     * Get the unique code that identifies this custodian.
     */
    public string getCode();
    
    public string getName();

    /**
     * Get a list of assets (cryptoCode or currencyCode) the custodian can store. Always in uppercase.
     * Example: ["BTC","EUR", "USD", "ETH",...]
     */
    public string[] getSupportedAssets();

    /**
     * A list of tradable asset pairs, or NULL if the custodian cannot trade/convert assets. if thr asset pair contains fiat, fiat is always put last. If both assets are a cyrptocode or both are fiat, the pair is written alphabetically. Always in uppercase. Example: ["BTC/EUR","BTC/USD", "EUR/USD", "BTC/ETH",...]
     */
    public string[]? getTradableAssetPairs();

    /**
     * Get a list of assets and their qty in custody.
     */
    public Dictionary<string, decimal> getAssetBalances();
}
