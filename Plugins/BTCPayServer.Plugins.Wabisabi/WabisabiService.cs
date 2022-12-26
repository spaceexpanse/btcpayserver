using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Client;
using Microsoft.Extensions.Caching.Memory;

namespace BTCPayServer.Plugins.Wabisabi
{
    public class WabisabiService
    {
        private readonly IStoreRepository _storeRepository;
        private readonly IMemoryCache _memoryCache;
        private readonly IEnumerable<IWabisabiCoordinatorManager> _wabisabiCoordinatorManagers;
        private readonly string[] _ids;

        public WabisabiService( IStoreRepository storeRepository, IMemoryCache memoryCache, IEnumerable<IWabisabiCoordinatorManager> wabisabiCoordinatorManagers)
        {
            _storeRepository = storeRepository;
            _memoryCache = memoryCache;
            _wabisabiCoordinatorManagers = wabisabiCoordinatorManagers;
            _ids = wabisabiCoordinatorManagers.Select(manager => manager.CoordinatorName).ToArray();
        }
        
        public async Task<WabisabiStoreSettings> GetWabisabiForStore(string storeId)
        {
            
            var res = await  _storeRepository.GetSettingAsync<WabisabiStoreSettings>(storeId, nameof(WabisabiStoreSettings));
            res ??= new WabisabiStoreSettings();
            res.Settings = res.Settings.Where(settings => _ids.Contains(settings.Coordinator)).ToList();
            foreach (var wabisabiCoordinatorManager in _wabisabiCoordinatorManagers)
            {
                if (res.Settings.All(settings => settings.Coordinator != wabisabiCoordinatorManager.CoordinatorName))
                {
                    res.Settings.Add(new WabisabiStoreCoordinatorSettings()
                    {
                        Coordinator = wabisabiCoordinatorManager.CoordinatorName,
                    });
                }
            }

            return res;
        }

        public async Task SetWabisabiForStore(string storeId, WabisabiStoreSettings wabisabiSettings)
        {
            
            await _storeRepository.UpdateSetting(storeId, nameof(WabisabiStoreSettings), wabisabiSettings);
            _memoryCache.Remove($"Wabisabi_WalletProvider_{storeId}");
            _memoryCache.Remove($"Wabisabi_Smartifier_{storeId}");
        }
    }
}
