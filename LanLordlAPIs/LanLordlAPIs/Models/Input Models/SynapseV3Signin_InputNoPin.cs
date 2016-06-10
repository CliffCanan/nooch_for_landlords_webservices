using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LanLordlAPIs.Models.Input_Models
{
    public class SynapseV3Signin_InputNoPin
    {
        public createUser_client client { get; set; }
        public createUser_login2 login { get; set; }
        public SynapseV3Signin_Input_UserNoPin user { get; set; }
    }

    public class SynapseV3Signin_Input_UserNoPin
    {
        public synapseSearchUserResponse_Id1 _id { get; set; }
        public string fingerprint { get; set; }
        public string ip { get; set; }
        public string phone_number { get; set; }
    }

    public class SynapseV3Signin_InputWithPin
    {
        public createUser_client client { get; set; }
        public createUser_login2 login { get; set; }
        public SynapseV3Signin_Input_UserWithPin user { get; set; }
    }

    public class SynapseV3Signin_Input_UserWithPin
    {
        public synapseSearchUserResponse_Id1 _id { get; set; }
        public string fingerprint { get; set; }
        public string ip { get; set; }
        public string phone_number { get; set; }
        public string validation_pin { get; set; }
    }
}