using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using LanLordlAPIs.Models.db_Model;

namespace LanLordlAPIs.Models.Output_Models
{
    public class CheckAndRegisterLandlordByEmailResult
    {
        public string ErrorMessage { get; set; }
        public bool IsSuccess { get; set; }
        public Landlord LanlordDetails { get; set; }
    }

    public class CheckAndRegisterMemberByEmailResult
    {
        public string ErrorMessage { get; set; }
        public bool IsSuccess { get; set; }
        public Member MemberDetails { get; set; }
    }

    public class CheckIfTenantExistsResult
    {
        public string ErrorMessage { get; set; }
        public bool IsSuccess { get; set; }
        public Tenant TenantDetails { get; set; }
    }
}