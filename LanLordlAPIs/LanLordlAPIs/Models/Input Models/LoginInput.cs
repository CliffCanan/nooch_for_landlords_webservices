using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using LanLordlAPIs.Models.Output_Models;

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

    public class ResendVerificationEmailAndSMSInput
    {
        public string UserId { get; set; }
        public string AccessToken { get; set; }
        public string UserType { get; set; }
        public string RequestFor { get; set; }
    }

    public class EditPersonalInfoInputClass
    {
        public GetProfileDataInput DeviceInfo { get; set; }
        public UserProfileInfoInputClass UserInfo { get; set; }
    }

    public class SendEmailsToTenantsInputClass
    {
        public GetProfileDataInput DeviceInfo { get; set; }
        public SendEmailsToTenantsInternalClass EmailInfo { get; set; }
    }

    public class SendEmailsToTenantsInternalClass
    {
        public string IsForAllOrOne { get; set; }
        public string MessageToBeSent { get; set; }
        public string TenantIdToBeMessaged { get; set; }
        public string PropertyId { get; set; }   
    }

    public class RegisterLandlordInput
    {
        public string FirstName { get; set; }public string LastName { get; set; }
        public string eMail  { get; set; }public string Password { get; set; }
    }
    
    public class SetPropertyStatusClass
    {
        public string PropertyId { get; set; }
        public bool PropertyStatusToSet { get; set; }
        public GetProfileDataInput User { get; set; }
    }

    public class AddNewPropertyClass
    {
        public string PropertyId { get; set; }
        public string PropertyName { get; set; }
        public string PropertyImage { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string Zip { get; set; }
        public string State { get; set; }
        public string ContactNumber { get; set; }

        public string UnitsCount { get; set; }
        public string Rent { get; set; }

        public GetProfileDataInput User { get; set; }

        public AddNewUnitClass[] Unit { get; set; }

        public bool IsMultipleUnitsAdded { get; set; }
        public bool IsPropertyImageAdded { get; set; }
    }

    public class AddNewUnitInputOuterClass
    {
        public string PropertyId { get; set; }
        public GetProfileDataInput User { get; set; }
        public AddNewUnitInnerClass Unit { get; set; }
    }

    public class AddNewUnitInnerClass
    {
        public string UnitNum { get; set; }
        public string UnitNickName { get; set; }
        public string Rent { get; set; }
        public string DueDate { get; set; }

        public bool IsTenantAdded { get; set; }
        public string TenantId { get; set; }

        public string RentStartDate { get; set; }
        public string AgreementDuration { get; set; }
    }

    public class AddNewUnitClass
    {
        public string UnitNum { get; set; }
        public string Rent { get; set; }
        public bool IsAddedWithProperty { get; set; }
    }

}