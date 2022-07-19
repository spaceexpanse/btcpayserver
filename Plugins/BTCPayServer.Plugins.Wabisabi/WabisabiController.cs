using System;
using System.Linq;
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

        public WabisabiController(WabisabiService WabisabiService)
        {
            _WabisabiService = WabisabiService;
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
            var coordinator = pieces.Length > 1 ? pieces[1] : null;
            var commandIndex = pieces.Length > 2 ? pieces[2] : null;
            var coord = vm.Settings.SingleOrDefault(settings => settings.Coordinator == coordinator);
            ModelState.Clear();

            switch (actualCommand)
            {
                case "coinjoin-label-add":
                    coord.LabelsToAddToCoinjoin.Add("");
                    return View(vm);
                case "coinjoin-label-remove":
                    coord.LabelsToAddToCoinjoin.Remove(commandIndex);
                    return View(vm);
                case "exclude-label-add":
                    coord.InputLabelsExcluded.Add("");
                    return View(vm);

                case "exclude-label-remove":
                    coord.InputLabelsExcluded.Remove(commandIndex);
                    return View(vm);
                case "include-label-add":
                    coord.InputLabelsAllowed.Add("");
                    return View(vm);
                case "include-label-remove":
                    coord.InputLabelsAllowed.Remove(commandIndex);
                    return View(vm);

                case "save":
                    foreach (WabisabiStoreCoordinatorSettings settings in vm.Settings)
                    {
                        settings.InputLabelsAllowed = settings.InputLabelsAllowed.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
                        settings.InputLabelsExcluded = settings.InputLabelsExcluded.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
                        settings.LabelsToAddToCoinjoin = settings.LabelsToAddToCoinjoin.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
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
