using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.Wabisabi
{
    [Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    [Authorize(Policy = Policies.CanModifyStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    [Route("plugins/{storeId}/Wabisabi")]
    public class WabisabiController : Controller
    {
        private readonly WabisabiService _WabisabiService;
        private readonly WalletProvider _walletProvider;

        public WabisabiController(WabisabiService WabisabiService,WalletProvider walletProvider)
        {
            _WabisabiService = WabisabiService;
            _walletProvider = walletProvider;
        }

        [HttpGet("")]
        public async Task<IActionResult> UpdateWabisabiStoreSettings(string storeId)
        {
            WabisabiStoreSettings Wabisabi = null;
            try
            {
                Wabisabi = await _WabisabiService.GetWabisabiForStore(storeId);
            }
            catch (Exception)
            {
                // ignored
            }

            return View(Wabisabi);
        }


        [HttpPost("")]
        public async Task<IActionResult> UpdateWabisabiStoreSettings(string storeId, WabisabiStoreSettings vm,
            string command)
        {
            var pieces = command.Split(":");
            var actualCommand = pieces[0];
            var commandIndex = pieces.Length > 1 ? pieces[1] : null;
            var coordinator = pieces.Length > 2 ? pieces[2] : null;
            var coord = vm.Settings.SingleOrDefault(settings => settings.Coordinator == coordinator);
            ModelState.Clear();

            switch (actualCommand)
            {
                case "check":
                    await _walletProvider.Check(storeId, CancellationToken.None);
                    TempData["SuccessMessage"] = "Store wallet re-checked";
                    return RedirectToAction(nameof(UpdateWabisabiStoreSettings), new {storeId});
                case "exclude-label-add":
                    vm.InputLabelsExcluded.Add("");
                    return View(vm);

                case "exclude-label-remove":
                    vm.InputLabelsExcluded.Remove(commandIndex);
                    return View(vm);
                case "include-label-add":
                    vm.InputLabelsAllowed.Add("");
                    return View(vm);
                case "include-label-remove":
                    vm.InputLabelsAllowed.Remove(commandIndex);
                    return View(vm);

                case "save":
                    foreach (WabisabiStoreCoordinatorSettings settings in vm.Settings)
                    {
                        vm.InputLabelsAllowed = vm.InputLabelsAllowed.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
                        vm.InputLabelsExcluded = vm.InputLabelsExcluded.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
                    } 
                    await _WabisabiService.SetWabisabiForStore(storeId, vm);
                    TempData["SuccessMessage"] = "Wabisabi settings modified";
                    return RedirectToAction(nameof(UpdateWabisabiStoreSettings), new {storeId});

                default:
                    return View(vm);
            }
        }
    }
}
