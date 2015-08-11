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

    public class AddNewPropertyClass
    {
        public string PropertyName { get; set; }
        public string PropertyImage { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string Zip { get; set; }

        public string UnitsCount { get; set; }
        public string Rent { get; set; }

        public GetProfileDataInput User { get; set; }

        public AddNewUnitClass[] Unit { get; set; }

        public bool IsMultipleUnitsAdded { get; set; }
        public bool IsPropertyImageAdded { get; set; }



    }

    public class AddNewUnitClass
    {
        public string UnitNum { get; set; }
        public string Rent { get; set; }
        public bool IsAddedWithProperty { get; set; }
        


    }

}