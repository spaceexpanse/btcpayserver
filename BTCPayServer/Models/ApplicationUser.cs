using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using BTCPayServer.Data;
using U2F.Demo.Models;

namespace BTCPayServer.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        public List<UserStore> UserStores
        {
            get;
            set;
        }

        public bool RequiresEmailConfirmation
        {
            get; set;
        }
        
        public virtual List<Device> DeviceRegistrations { get; set; }

        public virtual List<AuthenticationRequest> AuthenticationRequest { get; set; }
    }
}
