using System;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Client;
using Microsoft.Extensions.Caching.Memory;
using NBitcoin;

namespace BTCPayServer.Plugins.FujiOracle
{
    public class FujiOracleService
    {
        private readonly ISettingsRepository _settingsRepository;
        private readonly IMemoryCache _memoryCache;
        private readonly IStoreRepository _storeRepository;

        public FujiOracleService(ISettingsRepository settingsRepository, IMemoryCache memoryCache, IStoreRepository storeRepository)
        {
            _settingsRepository = settingsRepository;
            _memoryCache = memoryCache;
            _storeRepository = storeRepository;
        }
        
        public async Task<FujiOracleSettings> GetFujiOracleForStore(string storeId)
        {
            var k = $"{nameof(FujiOracleSettings)}_{storeId}";
            return await _memoryCache.GetOrCreateAsync(k, async _ =>
            {
                var res = await _storeRepository.GetSettingAsync<FujiOracleSettings>(storeId,
                    nameof(FujiOracleSettings));
                return res;
            });
        }

        public async Task SetFujiOracleForStore(string storeId, FujiOracleSettings FujiOracleSettings)
        {
            var k = $"{nameof(FujiOracleSettings)}_{storeId}";
            
            await _storeRepository.UpdateSetting(storeId, nameof(FujiOracleSettings), FujiOracleSettings);
            _memoryCache.Set(k, FujiOracleSettings);
        }
    }
}
