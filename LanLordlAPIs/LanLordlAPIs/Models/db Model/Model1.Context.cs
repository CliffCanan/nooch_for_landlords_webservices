﻿//------------------------------------------------------------------------------
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
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    using System.Data.Objects;
    using System.Data.Objects.DataClasses;
    using System.Linq;
    
    public partial class NOOCHEntities : DbContext
    {
        public NOOCHEntities()
            : base("name=NOOCHEntities")
        {
        }
    
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
        public DbSet<GeoLocation> GeoLocations { get; set; }
        public DbSet<Landlord> Landlords { get; set; }
        public DbSet<MemberNotification> MemberNotifications { get; set; }
        public DbSet<MemberPrivacySetting> MemberPrivacySettings { get; set; }
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<TenantsIdDocument> TenantsIdDocuments { get; set; }
        public DbSet<InviteCode> InviteCodes { get; set; }
        public DbSet<AuthenticationToken> AuthenticationTokens { get; set; }
        public DbSet<PasswordResetRequest> PasswordResetRequests { get; set; }
        public DbSet<Property> Properties { get; set; }
        public DbSet<Member> Members { get; set; }
        public DbSet<SynapseBanksOfMember> SynapseBanksOfMembers { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<PropertyUnit> PropertyUnits { get; set; }
        public DbSet<UnitsOccupiedByTenant> UnitsOccupiedByTenants { get; set; }
    
        public virtual ObjectResult<Nullable<int>> GetTenantsCountInGivenPropertyId(string vPropertyId)
        {
            var vPropertyIdParameter = vPropertyId != null ?
                new ObjectParameter("vPropertyId", vPropertyId) :
                new ObjectParameter("vPropertyId", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<Nullable<int>>("GetTenantsCountInGivenPropertyId", vPropertyIdParameter);
        }
    
        public virtual ObjectResult<Nullable<int>> GetPropertiesCountForGivenLandlord(string vLandlordId)
        {
            var vLandlordIdParameter = vLandlordId != null ?
                new ObjectParameter("vLandlordId", vLandlordId) :
                new ObjectParameter("vLandlordId", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<Nullable<int>>("GetPropertiesCountForGivenLandlord", vLandlordIdParameter);
        }
    
        public virtual ObjectResult<Nullable<int>> GetTenantsCountForGivenLandlord(string vLandlordId)
        {
            var vLandlordIdParameter = vLandlordId != null ?
                new ObjectParameter("vLandlordId", vLandlordId) :
                new ObjectParameter("vLandlordId", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<Nullable<int>>("GetTenantsCountForGivenLandlord", vLandlordIdParameter);
        }
    
        public virtual ObjectResult<Nullable<int>> GetUnitsCountForGivenLandlord(string vLandlordId)
        {
            var vLandlordIdParameter = vLandlordId != null ?
                new ObjectParameter("vLandlordId", vLandlordId) :
                new ObjectParameter("vLandlordId", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<Nullable<int>>("GetUnitsCountForGivenLandlord", vLandlordIdParameter);
        }
    
        public virtual ObjectResult<Nullable<bool>> IsBankAccountAddedforGivenLandlordOrTenant(string vUserType, string vUserId)
        {
            var vUserTypeParameter = vUserType != null ?
                new ObjectParameter("vUserType", vUserType) :
                new ObjectParameter("vUserType", typeof(string));
    
            var vUserIdParameter = vUserId != null ?
                new ObjectParameter("vUserId", vUserId) :
                new ObjectParameter("vUserId", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<Nullable<bool>>("IsBankAccountAddedforGivenLandlordOrTenant", vUserTypeParameter, vUserIdParameter);
        }
    
        public virtual ObjectResult<Nullable<bool>> IsEmailVerifiedforGivenLandlordOrTenant(string vUserType, string vUserId)
        {
            var vUserTypeParameter = vUserType != null ?
                new ObjectParameter("vUserType", vUserType) :
                new ObjectParameter("vUserType", typeof(string));
    
            var vUserIdParameter = vUserId != null ?
                new ObjectParameter("vUserId", vUserId) :
                new ObjectParameter("vUserId", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<Nullable<bool>>("IsEmailVerifiedforGivenLandlordOrTenant", vUserTypeParameter, vUserIdParameter);
        }
    
        public virtual ObjectResult<Nullable<bool>> IsPhoneVerifiedforGivenLandlordOrTenant(string vUserType, string vUserId)
        {
            var vUserTypeParameter = vUserType != null ?
                new ObjectParameter("vUserType", vUserType) :
                new ObjectParameter("vUserType", typeof(string));
    
            var vUserIdParameter = vUserId != null ?
                new ObjectParameter("vUserId", vUserId) :
                new ObjectParameter("vUserId", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<Nullable<bool>>("IsPhoneVerifiedforGivenLandlordOrTenant", vUserTypeParameter, vUserIdParameter);
        }
    
        public virtual ObjectResult<string> GetTenantNameForGivenUnitId(string vUnitId)
        {
            var vUnitIdParameter = vUnitId != null ?
                new ObjectParameter("vUnitId", vUnitId) :
                new ObjectParameter("vUnitId", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<string>("GetTenantNameForGivenUnitId", vUnitIdParameter);
        }
    
        public virtual ObjectResult<string> GetTenantEmailForGivenUnitId(string vUnitId)
        {
            var vUnitIdParameter = vUnitId != null ?
                new ObjectParameter("vUnitId", vUnitId) :
                new ObjectParameter("vUnitId", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<string>("GetTenantEmailForGivenUnitId", vUnitIdParameter);
        }
    
        public virtual ObjectResult<string> GetTenantImageForGivenUnitId(string vUnitId)
        {
            var vUnitIdParameter = vUnitId != null ?
                new ObjectParameter("vUnitId", vUnitId) :
                new ObjectParameter("vUnitId", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<string>("GetTenantImageForGivenUnitId", vUnitIdParameter);
        }
    
        public virtual ObjectResult<Nullable<System.DateTime>> GetLastRentPaymentDateForGivenUnitId(string vUnitId)
        {
            var vUnitIdParameter = vUnitId != null ?
                new ObjectParameter("vUnitId", vUnitId) :
                new ObjectParameter("vUnitId", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<Nullable<System.DateTime>>("GetLastRentPaymentDateForGivenUnitId", vUnitIdParameter);
        }
    
        public virtual ObjectResult<Nullable<int>> IsBankAccountAddedOfTenantInGivenUnitId(string vUnitId)
        {
            var vUnitIdParameter = vUnitId != null ?
                new ObjectParameter("vUnitId", vUnitId) :
                new ObjectParameter("vUnitId", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<Nullable<int>>("IsBankAccountAddedOfTenantInGivenUnitId", vUnitIdParameter);
        }
    
        public virtual ObjectResult<Nullable<bool>> IsEmailIdVerifiedOfTenantInGivenUnitId(string vUnitId)
        {
            var vUnitIdParameter = vUnitId != null ?
                new ObjectParameter("vUnitId", vUnitId) :
                new ObjectParameter("vUnitId", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<Nullable<bool>>("IsEmailIdVerifiedOfTenantInGivenUnitId", vUnitIdParameter);
        }
    
        public virtual ObjectResult<Nullable<bool>> IsPhoneVerifiedOfTenantInGivenUnitId(string vUnitId)
        {
            var vUnitIdParameter = vUnitId != null ?
                new ObjectParameter("vUnitId", vUnitId) :
                new ObjectParameter("vUnitId", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<Nullable<bool>>("IsPhoneVerifiedOfTenantInGivenUnitId", vUnitIdParameter);
        }
    
        public virtual ObjectResult<Nullable<bool>> IsRentPaidByTenantForGivenUnitId(string vUnitId)
        {
            var vUnitIdParameter = vUnitId != null ?
                new ObjectParameter("vUnitId", vUnitId) :
                new ObjectParameter("vUnitId", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<Nullable<bool>>("IsRentPaidByTenantForGivenUnitId", vUnitIdParameter);
        }
    
        public virtual ObjectResult<GetTenantsInGivenPropertyId_Result2> GetTenantsInGivenPropertyId(string vPropertyId)
        {
            var vPropertyIdParameter = vPropertyId != null ?
                new ObjectParameter("vPropertyId", vPropertyId) :
                new ObjectParameter("vPropertyId", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<GetTenantsInGivenPropertyId_Result2>("GetTenantsInGivenPropertyId", vPropertyIdParameter);
        }
    }
}
