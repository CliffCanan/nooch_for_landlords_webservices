using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LanLordlAPIs.Models.Input_Models
{
    public class LoginInput
    {
        public string UserName { get; set; }
        public string Password { get; set; }

        public string Ip { get; set; }

    }

    public class GetProfileDataInput
    {
        public string LandlorId { get; set; }
        public string AccessToken { get; set; }

        
    }
}