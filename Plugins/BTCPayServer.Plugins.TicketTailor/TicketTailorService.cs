using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using Microsoft.Extensions.Caching.Memory;

namespace BTCPayServer.Plugins.TicketTailor;

public class TicketTailorService
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly IMemoryCache _memoryCache;
    private readonly IStoreRepository _storeRepository;

    public TicketTailorService(ISettingsRepository settingsRepository, IMemoryCache memoryCache,
        IStoreRepository storeRepository)
    {
        _settingsRepository = settingsRepository;
        _memoryCache = memoryCache;
        _storeRepository = storeRepository;
    }


    public async Task<TicketTailorSettings> GetTicketTailorForStore(string storeId)
    {
        var k = $"{nameof(TicketTailorSettings)}_{storeId}";
        return await _memoryCache.GetOrCreateAsync(k, async _ =>
        {
            var res = await _storeRepository.GetSettingAsync<TicketTailorSettings>(storeId,
                nameof(TicketTailorSettings));
            if (res is not null) return res;
            res = await _settingsRepository.GetSettingAsync<TicketTailorSettings>(k);

            if (res is not null)
            {
                await SetTicketTailorForStore(storeId, res);
            }

            await _settingsRepository.UpdateSetting<TicketTailorSettings>(null, k);
            return res;
        });
    }

    public async Task SetTicketTailorForStore(string storeId, TicketTailorSettings TicketTailorSettings)
    {
        await _storeRepository.UpdateSetting(storeId, nameof(TicketTailorSettings), TicketTailorSettings);
    }
}
