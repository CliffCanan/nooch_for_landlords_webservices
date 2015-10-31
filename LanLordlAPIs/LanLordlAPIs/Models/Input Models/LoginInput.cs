using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using LanLordlAPIs.Models.Output_Models;

namespace LanLordlAPIs.Models.Input_Models
{
    public class basicLandlordPayload
    {
        public string LandlordId { get; set; }
        public string MemberId { get; set; }
        public string AccessToken { get; set; }
    }

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
        public string MemberId { get; set; }
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
        public LandlordProfileInfoInputClass UserInfo { get; set; }
    }

    public class UpdatePasswordInput
    {
        public GetProfileDataInput AuthInfo { get; set; }
        public string currentPw { get; set; }
        public string newPw { get; set; }
    }

    public class idVerWizardInput
    {
        public string ssn { get; set; }
        public string fullName { get; set; }
        public string dob { get; set; }
        public string staddress { get; set; }
        public string zip { get; set; }
        public string phone { get; set; }
        public GetProfileDataInput DeviceInfo { get; set; }
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
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string eMail { get; set; }
        public string Password { get; set; }
        public string fingerprint { get; set; }
        public string ip { get; set; }
        public string country { get; set; }
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

        public basicLandlordPayload User { get; set; }

        public AddNewUnitClass[] Unit { get; set; }

        public bool IsMultipleUnitsAdded { get; set; }
        public bool IsPropertyImageAdded { get; set; }
    }

    public class AddOrEditUnitInput
    {
        public string PropertyId { get; set; }
        public basicLandlordPayload User { get; set; }
        public AddOrEditUnit_Unit Unit { get; set; }
    }

    public class AddOrEditUnit_Unit
    {
        public bool isNewUnit { get; set; }
        public string UnitNum { get; set; }
        public string UnitNickName { get; set; }
        public string Rent { get; set; }
        public string DueDate { get; set; }

        public string IsTenantAdded { get; set; }
        public string TenantId { get; set; }
        public string TenantEmail { get; set; }
        public string UnitId { get; set; }

        public string RentStartDate { get; set; }
        public string LeaseLength { get; set; }
    }

    public class AddNewUnitClass
    {
        public string UnitNum { get; set; }
        public string Rent { get; set; }
        public bool IsAddedWithProperty { get; set; }
    }

    public class AddNewTenantInput
    {
        public string propertyId { get; set; }
        public string unitId { get; set; }
        public string rent { get; set; }
        public string startDate { get; set; }
        public string leaseLength { get; set; }
        public basicLandlordPayload authData { get; set; }
        public TenantInfo tenant { get; set; }
    }

    public class TenantInfo
    {
        public string email { get; set; }
        public string firstName { get; set; }
        public string lastName { get; set; }
    }
}