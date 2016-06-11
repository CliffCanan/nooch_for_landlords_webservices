using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LanLordlAPIs.Models.Output_Models
{
    public class synapseCreateUserV3Result_int
    {
        public createUserV3Result_oauth oauth { get; set; }
        public synapseV3Result_user user { get; set; }
        public bool success { get; set; }
        public string user_id { get; set; }
        public string error_code { get; set; }
        public string http_code { get; set; }
        public string errorMsg { get; set; }

        public string memberIdGenerated { get; set; } // Used when creating a new Member from a landing page
        public string ssn_verify_status { get; set; }
        public string reason { get; set; }
    }

    public class createUserV3Result_oauth
    {
        public string expires_at { get; set; }
        public string expires_in { get; set; }
        public string oauth_key { get; set; }
        public string refresh_token { get; set; }
    }

    public class synapseV3Result_user  // RESULT CLASS
    {
        public synapseV3Result_user_id _id { get; set; }
        public synapseV3Result_user_client client { get; set; }
        public synapseV3Result_user_docStatus doc_status { get; set; }
        public synapseV3Result_user_extra extra { get; set; }

        //public string[] documents { get; set; }

        public string[] legal_names { get; set; }
        public synapseV3Result_user_logins[] logins { get; set; }
        public bool is_hidden { get; set; }

        public string permission { get; set; }
        public string[] photos { get; set; }
        public string[] phone_numbers { get; set; }
    }

    public class synapseV3Result_user_id
    {
        [JsonProperty(PropertyName = "$oid")]
        public string id { get; set; }
    }

    public class synapseV3Result_user_client
    {
        public string id { get; set; } // This is an integer ID
        public string name { get; set; }
    }
    public class synapseV3Result_user_docStatus
    {
        public string physical_doc { get; set; }
        public string virtual_doc { get; set; }
    }

    public class synapseV3Result_user_logins
    {
        public string email { get; set; }
        public string scope { get; set; }
        public bool read_only { get; set; }
    }

    public class synapseV3Result_user_extra
    {
        public synapseV3Result_user_extra_dateJoined date_joined { get; set; }
        public bool is_business { get; set; }
        public bool extra_security { get; set; }
        public string supp_id { get; set; }
        public string cip_tag { get; set; }
    }

    public class synapseV3Result_user_extra_dateJoined
    {
        public DateTime date { get; set; }
    }

    public class SynapseDetailsClass_UserDetails
    {
        public string access_token { get; set; }
        public string MemberId { get; set; }
        public string user_id { get; set; }
        public string permission { get; set; }
        public string user_fingerprints { get; set; }
    }
}