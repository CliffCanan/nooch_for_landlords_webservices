using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LanLordlAPIs.Models.Input_Models
{
    public class SynapseV3RefreshOauthKeyAndSign_Input
    {
        public createUser_client client { get; set; }
        public createUser_login2 login { get; set; }
        public SynapseV3RefreshOAuthToken_User_Input user { get; set; }
    }
    public class createUser_client
    {
        public string client_id { get; set; }
        public string client_secret { get; set; }
    }
    public class createUser_login2
    {
        public string email { get; set; }
        //public string password { get; set; }
        public string refresh_token { get; set; }
    }
    public class SynapseV3RefreshOAuthToken_User_Input
    {
        public synapseSearchUserResponse_Id1 _id { get; set; }
        public string fingerprint { get; set; }
        public string ip { get; set; }
    }
}