using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using LanLordlAPIs.Classes.Utility;

namespace LanLordlAPIs.Models.Output_Models
{
    public class LoginResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }

        public string MemberId { get; set; }
        public string AccessToken { get; set; }
    }

    public class UserProfileInfoResult
    {

        //auth token and validation result
        public AccessTokenValidationOutput AuthTokenValidation { get; set; }



        // if any exception
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }


        //Account info
        public string AccountType { get; set; }
        public string VerificationType { get; set; }


        //Personel info
        public string DOB { get; set; }
        public string SSN { get; set; }

        public string UserEmail { get; set; }

        public string FirstName { get; set; }
        public string LastName { get; set; }

        public string MobileNumber { get; set; }
        public string Address { get; set; }
        public string TwitterHandle { get; set; }
        public string FbUrl { get; set; }
        public string InstaUrl { get; set; }

        public string CompanyName { get; set; }
        public string CompanyEID { get; set; }

        public bool IsPhoneVerified { get; set; }
        public bool IsEmailVerified { get; set; }
    }
}