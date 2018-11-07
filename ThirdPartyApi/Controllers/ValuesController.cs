using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ThirdPartyApi.Controllers
{
    [Route("")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        [Authorize()]
        [HttpGet]
        public IActionResult Private()
        {
            var identity = User.Identity as ClaimsIdentity;
            if (identity == null)
            {
                return BadRequest();
            }

            return Content($"You have authorized access to resources belonging to {identity.Name} on ResourceServer01.");
        }

        [HttpGet]
        public IActionResult Public()
        {
            return Content("This is a public endpoint that is at ResourceServer01; it does not require authorization.");
        }
    }
}