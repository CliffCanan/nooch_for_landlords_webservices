//------------------------------------------------------------------------------
// <auto-generated>
//    This code was generated from a template.
//
//    Manual changes to this file may cause unexpected behavior in your application.
//    Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace LanLordlAPIs.Models.db_Model
{
    using System;
    
    public partial class GetTenantsInGivenPropertyId_Result
    {
        public System.Guid TenantId { get; set; }
        public System.Guid UnitId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string UserPic { get; set; }
        public string UnitRent { get; set; }
        public Nullable<System.DateTime> LastPaymentDate { get; set; }
        public Nullable<bool> IsPaymentDueForThisMonth { get; set; }
        public Nullable<bool> IsPhoneVerfied { get; set; }
        public Nullable<bool> IsEmailVerified { get; set; }
        public Nullable<bool> IsIdDocumentVerified { get; set; }
        public Nullable<System.Guid> BankAccountId { get; set; }
    }
}
