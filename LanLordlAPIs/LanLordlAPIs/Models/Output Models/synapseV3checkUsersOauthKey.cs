using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LanLordlAPIs.Models.Output_Models
{
    public class synapseV3checkUsersOauthKey
    {
        public bool success { get; set; }
        public string oauth_consumer_key { get; set; }
        public string oauth_refresh_token { get; set; }
        public string user_oid { get; set; }
        public string msg { get; set; }
        public bool is2FA { get; set; }
    }
}