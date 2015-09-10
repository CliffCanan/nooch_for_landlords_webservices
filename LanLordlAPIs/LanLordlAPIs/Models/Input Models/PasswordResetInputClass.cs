using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LanLordlAPIs.Models.Input_Models
{
    public class PasswordResetInputClass
    {
        public string eMail { get; set; }
    }


    public class PasswordResetOutputClass
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
    }
}