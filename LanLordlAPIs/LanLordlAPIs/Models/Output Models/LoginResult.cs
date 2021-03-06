﻿using System;
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
        public string LandlordId { get; set; }
        public string AccessToken { get; set; }
    }


    public class CreatePropertyResultOutput
    {
        //auth token and validation result
        public AccessTokenValidationOutput AuthTokenValidation { get; set; }

        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public string PropertyIdGenerated { get; set; }
    }


    public class LandlordProfileInfoInputClass
    {
        //Account info
        public string AccountType { get; set; }
        public string SubType { get; set; }
        public string VerificationType { get; set; }

        public string DOB { get; set; }
        public string SSN { get; set; }

        public string UserEmail { get; set; }

        public string FullName { get; set; }
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

        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }

        public string City { get; set; }
        public string AddState { get; set; }
        public string Country { get; set; }
        public string Zip { get; set; }

        public string InfoType { get; set; }
        public string NewPassword { get; set; }
    }


    public class LandlordProfileInfoResult
    {
        //auth token and validation result
        public AccessTokenValidationOutput AuthTokenValidation { get; set; }
        public string MemberId { get; set; }
        public string memberStatus { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }

        //Account info
        public string AccountType { get; set; }
        public string SubType { get; set; }
        public string VerificationType { get; set; }

        public string DOB { get; set; }
        public string SSN { get; set; }
        public bool? isIdVerified { get; set; }

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

        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }

        public string City { get; set; }
        public string AddState { get; set; }
        public string Country { get; set; }
        public string Zip { get; set; }

        public string UserImageUrl { get; set; }
        public string PropertiesCount { get; set; }
        public string TenantsCount { get; set; }
        public string UnitsCount { get; set; }
    }


    public class GetAccountCompletionStatsResultClass
    {
        public AccessTokenValidationOutput AuthTokenValidation { get; set; }

        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }

        public string AllPropertysCount { get; set; }
        public string AllUnitsCount { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool IsPhoneVerified { get; set; }
        public bool IsAccountAdded { get; set; }
        public bool isIdVerified { get; set; }
        public bool IsAnyRentReceived { get; set; }
        public string AllTenantsCount { get; set; }
    }


    public class GetAllPropertiesResult
    {
        public AccessTokenValidationOutput AuthTokenValidation { get; set; }

        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }

        public List<PropertyClassWithUnits> AllProperties { get; set; }
        public string AllPropertysCount { get; set; }
        public string AllUnitsCount { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool IsPhoneVerified { get; set; }
        public bool IsAccountAdded { get; set; }
        public bool IsIDVerified { get; set; }
        public bool IsAnyRentReceived { get; set; }
        public string AllTenantsCount { get; set; }
    }


    public class GetPropertyDetailsPageDataResult
    {
        public AccessTokenValidationOutput AuthTokenValidation { get; set; }

        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }

        public PropertyClassWithUnits PropertyDetails { get; set; }

        public BankDetailsResult BankAccountDetails { get; set; }
        public bool IsBankAccountAdded { get; set; }

        public string AllUnitsCount { get; set; }
        public string AllTenantsCount { get; set; }

        public string AllTenantsWithPassedDueDateCount { get; set; }

        public List<TenantDetailsResult> TenantsListForThisProperty { get; set; }
        public List<TenantDetailsResult> DefaulterTenants { get; set; }
    }


    public class TenantDetailsResult
    {
        public string TenantId { get; set; }
        public string UnitId { get; set; }
        public string UnitNumber { get; set; }
        public string TenantEmail { get; set; }
        public string UnitRent { get; set; }
        public string Name { get; set; }
        public string ImageUrl { get; set; }
        public string RentAmount { get; set; }
        public string LastRentPaidOn { get; set; }
        public bool IsRentPaidForThisMonth { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool IsDocumentsVerified { get; set; }
        public bool IsPhoneVerified { get; set; }
        public bool IsBankAccountAdded { get; set; }
    }


    public class SynapseAccoutDetailsInput
    {
        public bool success { get; set; }
        public string msg { get; set; }
        public string MemberId { get; set; }
        public string BankName { get; set; }
        public string BankNickname { get; set; }
        public string AccountName { get; set; }
        public string BankImageURL { get; set; }
        public string AccountStatus { get; set; }
        public string allowed { get; set; }
        public string dateCreated { get; set; }
    }

    public class BankDetailsResult
    {
        public string BankName { get; set; }
        public string BankIcon { get; set; }
        public string BankAccountID { get; set; }
        public string BankAccountNick { get; set; }
        public string BankAccountNumString { get; set; }
    }


    public class PropertyClassWithUnits
    {
        public string PropertyId { get; set; }
        public string PropStatus { get; set; }
        public string PropType { get; set; }
        public string PropName { get; set; }
        public string AddressLineOne { get; set; }
        public string AddressLineTwo { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }
        public string ContactNumber { get; set; }
        public string DefaultDueDate { get; set; }
        public string DateAdded { get; set; }
        public string DateModified { get; set; }
        public string LandlordId { get; set; }
        public string MemberId { get; set; }
        public string PropertyImage { get; set; }
        public Nullable<bool> IsSingleUnit { get; set; }
        public Nullable<bool> IsDeleted { get; set; }
        public string DefaulBank { get; set; }


        public List<PropertyUnitClass> AllUnits { get; set; }

        public string UnitsCount { get; set; }
        public string TenantsCount { get; set; }
    }


    public class PropertyClass
    {
        public string PropertyId { get; set; }
        public string PropStatus { get; set; }
        public string PropType { get; set; }
        public string PropName { get; set; }
        public string AddressLineOne { get; set; }
        public string AddressLineTwo { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }
        public string ContactNumber { get; set; }
        public string DefaultDueDate { get; set; }
        public string DateAdded { get; set; }
        public string DateModified { get; set; }
        public string LandlordId { get; set; }
        public string MemberId { get; set; }
        public string PropertyImage { get; set; }
        public Nullable<bool> IsSingleUnit { get; set; }
        public Nullable<bool> IsDeleted { get; set; }
        public string DefaulBank { get; set; }
    }


    public class PropertyUnitClass
    {
        public string UnitId { get; set; }
        public string PropertyId { get; set; }
        public string UnitNumber { get; set; }
        public string UnitNickname { get; set; }
        public string UnitRent { get; set; }
        public string BankAccountId { get; set; }
        public string DateAdded { get; set; }
        public string ModifiedOn { get; set; }
        public string LandlordId { get; set; }
        public string MemberId { get; set; }
        public string TenantName { get; set; }
        public string TenantEmail { get; set; }
        public string RentStartDate { get; set; }
        public string LeaseLength { get; set; }

        // rent
        public string LastRentPaidOn { get; set; }
        public bool IsRentPaidForThisMonth { get; set; }

        // tenant bank email and phone related
        public bool IsEmailVerified { get; set; }
        public bool IsPhoneVerified { get; set; }
        public bool IsBankAccountAdded { get; set; }

        public string ImageUrl { get; set; }  // for tenant image url
        public string UnitImage { get; set; }
        public Nullable<bool> IsDeleted { get; set; }
        public Nullable<bool> IsHidden { get; set; }
        public Nullable<bool> IsOccupied { get; set; }
        public string Status { get; set; }
        public string DueDate { get; set; }
        public string LeaseDocPath { get; set; }
    }


    public class RegisterLandlordResult
    {
        //auth token and validation result
        public AccessTokenValidationOutput AuthTokenValidation { get; set; }

        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
    }


    public class GenericInternalResponse
    {
        public bool success { get; set; }
        public string msg { get; set; }
        public AccessTokenValidationOutput AuthTokenValidation { get; set; }
    }
}