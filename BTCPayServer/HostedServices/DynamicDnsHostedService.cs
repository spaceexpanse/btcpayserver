﻿using System;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Events;
using BTCPayServer.Logging;
using BTCPayServer.Services;

namespace BTCPayServer.HostedServices
{
    public class DynamicDnsHostedService : BaseAsyncService
    {
        private readonly EventAggregator _EventAggregator;

        public DynamicDnsHostedService(IHttpClientFactory httpClientFactory, SettingsRepository settingsRepository, EventAggregator eventAggregator)
        {
            _EventAggregator = eventAggregator;
            HttpClientFactory = httpClientFactory;
            SettingsRepository = settingsRepository;
        }

        public IHttpClientFactory HttpClientFactory { get; }
        public SettingsRepository SettingsRepository { get; }

        internal override Task[] InitializeTasks()
        {
            return new[]
            {
                CreateLoopTask(UpdateRecord)
            };
        }

        TimeSpan Period = TimeSpan.FromMinutes(60);
        async Task UpdateRecord()
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation))
            {
                var settings = await SettingsRepository.GetSettingAsync<DynamicDnsSettings>() ?? new DynamicDnsSettings();
                foreach (var service in settings.Services)
                {
                    if (service?.Enabled is true && (service.LastUpdated is null ||
                                             (DateTimeOffset.UtcNow - service.LastUpdated) > Period))
                    {
                        timeout.CancelAfter(TimeSpan.FromSeconds(20.0));
                        try
                        {
                            var errorMessage = await service.SendUpdateRequest(HttpClientFactory.CreateClient());
                            if (errorMessage == null)
                            {
                                Logs.PayServer.LogInformation("Dynamic DNS service successfully refresh the DNS record");
                                service.LastUpdated = DateTimeOffset.UtcNow;
                                await SettingsRepository.UpdateSetting(settings);
                            }
                            else
                            {
                                Logs.PayServer.LogWarning($"Dynamic DNS service is enabled but the request to the provider failed: {errorMessage}");
                            }
                        }
                        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                        {
                        }
                    }
                }
            }
            using (var delayCancel = CancellationTokenSource.CreateLinkedTokenSource(Cancellation))
            {
                var delay = Task.Delay(Period, delayCancel.Token);
                var changed = ListenForRatesSettingChanges(Cancellation);
                await Task.WhenAny(delay, changed);
                delayCancel.Cancel();
            }
        }
        
        async Task ListenForRatesSettingChanges(CancellationToken cancellation)
        {
            await _EventAggregator.WaitNext<SettingsChanged<DynamicDnsSettings>>(cancellation);
        }
    }
}
