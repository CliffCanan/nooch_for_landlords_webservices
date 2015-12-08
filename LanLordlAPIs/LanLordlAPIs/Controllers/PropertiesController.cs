using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Web.UI.WebControls;
using LanLordlAPIs.Classes.Utility;
using LanLordlAPIs.Models.db_Model;
using LanLordlAPIs.Models.Input_Models;
using LanLordlAPIs.Models.Output_Models;

namespace LanLordlAPIs.Controllers
{
    public class PropertiesController : ApiController
    {
        [HttpPost]
        [ActionName("AddNewProperty")]
        public CreatePropertyResultOutput AddNewProperty(AddNewPropertyClass Property)
        {
            Logger.Info("Landlords API -> Properties -> AddNewProperty - Requested by [" + Property.User.LandlordId + "]");

            CreatePropertyResultOutput result = new CreatePropertyResultOutput();
            result.IsSuccess = false;

            try
            {
                Guid landlordguidId = new Guid(Property.User.LandlordId);

                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordguidId, Property.User.AccessToken);

                if (result.AuthTokenValidation.IsTokenOk)
                {
                    var validationResult = IsPropertyDataInputValid(Property, true);

                    if (validationResult.IsDataValid)
                    {
                        // all set... Data is ready to be saved in DB
                        string propertyImagePath = CommonHelper.GetValueFromConfig("PhotoUrl") + "propertyDefault.png";

                        if (Property.IsPropertyImageAdded)
                        {
                            // Get image URL from Base64 string
                            string fileName = Property.PropertyName.Trim().Replace("-", "_").Replace(" ", "_").Replace("'", "") + ".png";
                            propertyImagePath = CommonHelper.SaveBase64AsImage(landlordguidId.ToString().Replace("-", "_") +
                                                "_property_" + fileName, Property.PropertyImage);
                        }

                        using (NOOCHEntities obj = new NOOCHEntities())
                        {
                            Property prop = new Property
                            {
                                PropertyId = Guid.NewGuid(),
                                PropType = Property.IsMultipleUnitsAdded ? "Multi Unit" : "Single Unit",
                                PropStatus = "Not Published",
                                PropName = Property.PropertyName.Trim(),
                                AddressLineOne = Property.Address.Trim(),
                                City = Property.City.Trim(),
                                Zip = Property.Zip.Trim(),
                                DateAdded = DateTime.Now,
                                LandlordId = landlordguidId,
                                MemberId = new Guid(Property.User.MemberId),
                                PropertyImage = propertyImagePath,
                                IsSingleUnit = !Property.IsMultipleUnitsAdded,
                                IsDeleted = false,
                                DefaultDueDate = "1st of Month"
                            };

                            obj.Properties.Add(prop);
                            obj.SaveChanges();

                            // Set type to single unit if only one item passed in property units
                            if (Property.Unit.Length < 2)
                            {
                                prop.IsSingleUnit = true;
                                prop.PropType = "Single Unit";
                                obj.SaveChanges();
                            }

                            // Saving Units (if any)
                            foreach (AddNewUnitClass unitItem in Property.Unit)
                            {
                                PropertyUnit pu = new PropertyUnit();
                                pu.UnitId = Guid.NewGuid();
                                pu.PropertyId = prop.PropertyId;
                                pu.LandlordId = landlordguidId;
                                pu.DateAdded = DateTime.Now;
                                pu.IsDeleted = false;
                                pu.Status = "Not Published";

                                if (prop.IsSingleUnit != true)
                                {
                                    pu.UnitNumber = unitItem.UnitNum;
                                }
                                else
                                {
                                    pu.UnitNumber = "1";
                                }
                                pu.UnitRent = unitItem.Rent;
                                pu.IsHidden = true;
                                pu.IsOccupied = false;
                                pu.MemberId = new Guid(CommonHelper.GetLandlordsMemberIdFromLandlordId(landlordguidId));

                                obj.PropertyUnits.Add(pu);
                                obj.SaveChanges();
                            }
                            result.PropertyIdGenerated = prop.PropertyId.ToString();
                            result.IsSuccess = true;
                            result.ErrorMessage = "OK";
                        }
                    }
                    else
                    {
                        result.ErrorMessage = validationResult.ValidationError;
                    }
                }
                else
                {
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Landlords API -> Properties -> AddNewProperty FAILED - [LandlordID: " + Property.User.LandlordId + "], [Exception: [ " + ex + " ]");
                result.ErrorMessage = "Error while creating property. Retry later!";
            }

            return result;
        }


        /// <summary>
        /// To save a new unit for a given property.
        /// </summary>
        /// <param name="unitInput"></param>
        /// <returns>CreatePropertyResultOutput</returns>
        [HttpPost]
        [ActionName("AddNewUnitInProperty")]
        public CreatePropertyResultOutput AddNewUnitInProperty(AddOrEditUnitInput unitInput)
        {
            CreatePropertyResultOutput result = new CreatePropertyResultOutput();
            result.IsSuccess = false;

            try
            {
                Logger.Info("Properties Cntrlr -> AddNewUnitInProperty - Requested by [" + unitInput.User.LandlordId + "]");

                Guid landlordguidId = new Guid(unitInput.User.LandlordId);
                Guid propertyguidId = new Guid(unitInput.PropertyId);

                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordguidId, unitInput.User.AccessToken);

                if (result.AuthTokenValidation.IsTokenOk)
                {
                    using (NOOCHEntities obj = new NOOCHEntities())
                    {
                        // First, check if this is a valid property
                        var propDetails = (from c in obj.Properties
                                           where c.PropertyId == propertyguidId && c.IsDeleted == false
                                           select c).FirstOrDefault();

                        if (propDetails != null)
                        {
                            // Now add the new unit
                            PropertyUnit pu = new PropertyUnit();
                            pu.UnitId = Guid.NewGuid();
                            pu.PropertyId = propertyguidId;
                            pu.DateAdded = DateTime.Now;
                            pu.LandlordId = landlordguidId;
                            pu.UnitRent = unitInput.Unit.Rent;
                            pu.DueDate = unitInput.Unit.DueDate;
                            pu.LeaseLength = unitInput.Unit.LeaseLength;
                            pu.IsDeleted = false;
                            pu.IsHidden = false;
                            pu.UnitNumber = !String.IsNullOrEmpty(unitInput.Unit.UnitNum) ? unitInput.Unit.UnitNum : null;
                            pu.UnitNickName = !String.IsNullOrEmpty(unitInput.Unit.UnitNickName) ? unitInput.Unit.UnitNickName : null;
                            DateTime date = DateTime.Now.AddMonths(1);
                            DateTime newDate = new DateTime(date.Year, date.Month, 1, 0, 0, 0, date.Kind);
                            pu.RentStartDate = !String.IsNullOrEmpty(unitInput.Unit.RentStartDate) ? Convert.ToDateTime(unitInput.Unit.RentStartDate.Trim()) : newDate;


                            Logger.Info("Properties Cntrlr -> AddNewUnitInProperty - IsTenantAdded: [" + unitInput.Unit.IsTenantAdded + "], " +
                                        "Email: [" + unitInput.Unit.TenantEmail + "], " +
                                        "TenantID: [" + unitInput.Unit.TenantId + "]");

                            if (unitInput.Unit.IsTenantAdded == "true")
                            {
                                pu.IsOccupied = true;

                                if (!String.IsNullOrEmpty(unitInput.Unit.TenantId) && unitInput.Unit.TenantId.Length > 10)
                                {
                                    pu.Status = "Occupied";

                                    // Code to save tenant for given unit - if input only includes a tenant email, then we must invite that person and create a new tenant record
                                    #region Create New 'UnitsOccupiedByTenant' Record

                                    Guid tenantguid = CommonHelper.ConvertToGuid(unitInput.Unit.TenantId);

                                    UnitsOccupiedByTenant uobt = new UnitsOccupiedByTenant();
                                    uobt.TenantId = tenantguid; // NOTE: 'TenantId' = 'MemberId'
                                    uobt.UnitId = pu.UnitId;
                                    uobt.IsDeleted = false;

                                    if (!String.IsNullOrEmpty(unitInput.Unit.LeaseLength))
                                    {
                                        pu.LeaseLength = unitInput.Unit.LeaseLength;
                                    }
                                    if (!String.IsNullOrEmpty(unitInput.Unit.RentStartDate))
                                    {
                                        pu.RentStartDate = Convert.ToDateTime(unitInput.Unit.RentStartDate);
                                    }

                                    obj.UnitsOccupiedByTenants.Add(uobt);

                                    #endregion Create New 'UnitsOccupiedByTenant' Record
                                }
                                else if (!String.IsNullOrEmpty(unitInput.Unit.TenantEmail) && unitInput.Unit.TenantEmail.Length > 3)
                                {
                                    pu.Status = "Pending Invite";
                                }
                            }
                            else
                            {
                                pu.Status = "Vacant";
                                pu.IsOccupied = false;
                            }

                            obj.PropertyUnits.Add(pu);
                            int saveToDB = obj.SaveChanges();

                            if (saveToDB > 0)
                            {
                                if (!String.IsNullOrEmpty(unitInput.Unit.TenantEmail) && unitInput.Unit.TenantEmail.Length > 3)
                                {
                                    Logger.Info("Properties Cntrlr -> AddNewUnitInProperty - Tenant ID was not provided, so should invite a new tenant - [Email: " + unitInput.Unit.TenantEmail + "]");

                                    #region Invite New Tenant For This Unit

                                    TenantInfo ti = new TenantInfo
                                    {
                                        email = unitInput.Unit.TenantEmail,
                                        firstName = "",
                                        lastName = ""
                                    };

                                    basicLandlordPayload authInfo = new basicLandlordPayload
                                    {
                                        AccessToken = unitInput.User.AccessToken,
                                        LandlordId = unitInput.User.LandlordId,
                                        MemberId = unitInput.User.MemberId
                                    };

                                    AddNewTenantInput inviteTenantInputs = new AddNewTenantInput
                                    {
                                        authData = authInfo,
                                        propertyId = unitInput.PropertyId,
                                        unitId = pu.UnitId.ToString(),
                                        rent = unitInput.Unit.Rent,
                                        startDate = unitInput.Unit.RentStartDate,
                                        leaseLength = unitInput.Unit.LeaseLength,
                                        tenant = ti
                                    };

                                    InviteTenant(inviteTenantInputs);

                                    #endregion Invite New Tenant For This Unit
                                }

                                result.IsSuccess = true;
                                result.ErrorMessage = "OK.";
                                result.PropertyIdGenerated = pu.UnitId.ToString();
                            }
                            else
                            {
                                Logger.Error("Properties Cntrlr -> AddNewUnitInProperty - FAILED TO SAVE TO DB");
                            }
                        }
                        else
                        {
                            result.ErrorMessage = "Invalid property Id passed.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Properties Cntrlr -> AddNewUnitInProperty - [LandlordID: " + unitInput.User.LandlordId + "], [Exception: [" + ex + "]");
                result.ErrorMessage = "Error while creating property. Retry later!";
            }

            return result;
        }


        /// <summary>
        /// This method is for EDITING an existing unit in a property.
        /// </summary>
        /// <param name="unitInput"></param>
        /// <returns></returns>
        [HttpPost]
        [ActionName("EditPropertyUnit")]
        public CreatePropertyResultOutput EditPropertyUnit(AddOrEditUnitInput unitInput)
        {
            Logger.Info("PropertiesController -> EditPropertyUnit Initiated - [LandlordID: " + unitInput.User.LandlordId +
                        "], UnitID: [" + unitInput.Unit.UnitId +
                        "], Unit #: [" + unitInput.Unit.UnitNum +
                        "], isNewUnit: [" + unitInput.Unit.isNewUnit +
                        "], Rent Amount: [" + unitInput.Unit.Rent +
                        "], RentStartDate: [" + unitInput.Unit.RentStartDate +
                        "], IsTenantAdded: [" + unitInput.Unit.IsTenantAdded +
                        "], TenantId: [" + unitInput.Unit.TenantId + "]");

            CreatePropertyResultOutput result = new CreatePropertyResultOutput();
            result.IsSuccess = false;

            try
            {
                Guid landlordGuid = new Guid(unitInput.User.LandlordId);
                Guid unitGuid = new Guid(unitInput.Unit.UnitId);
                Guid tenantId = (unitInput.Unit.IsTenantAdded == "true" && !String.IsNullOrEmpty(unitInput.Unit.TenantId))
                                ? new Guid(unitInput.Unit.TenantId)
                                : new Guid();


                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordGuid, unitInput.User.AccessToken);

                if (result.AuthTokenValidation.IsTokenOk)
                {
                    using (NOOCHEntities obj = new NOOCHEntities())
                    {
                        // Get UNIT from DB
                        PropertyUnit unitObj = (from u in obj.PropertyUnits
                                                where u.UnitId == unitGuid &&
                                                      u.IsDeleted == false
                                                select u).SingleOrDefault();

                        if (unitObj != null)
                        {
                            if (String.IsNullOrEmpty(unitInput.Unit.UnitNum) &&
                                String.IsNullOrEmpty(unitInput.Unit.UnitNickName))
                            {
                                result.ErrorMessage = "Either unit number or nickname required.";
                            }

                            unitObj.UnitNumber = !String.IsNullOrEmpty(unitInput.Unit.UnitNum) ? unitInput.Unit.UnitNum : null;
                            unitObj.UnitNickName = !String.IsNullOrEmpty(unitInput.Unit.UnitNickName) ? unitInput.Unit.UnitNickName : null;
                            unitObj.UnitRent = unitInput.Unit.Rent;
                            unitObj.DueDate = unitInput.Unit.DueDate;
                            unitObj.ModifiedOn = DateTime.Now;
                            unitObj.LeaseLength = unitInput.Unit.LeaseLength;
                            DateTime date = DateTime.Now.AddMonths(1);
                            DateTime newDate = new DateTime(date.Year, date.Month, 1, 0, 0, 0, date.Kind);
                            unitObj.RentStartDate = !String.IsNullOrEmpty(unitInput.Unit.RentStartDate)
                                                    ? Convert.ToDateTime(unitInput.Unit.RentStartDate.Trim())
                                                    : newDate;


                            #region Delete Any Existing Tenants For This Unit

                            var sameOrNewTenant = 0;

                            try
                            {
                                // Check for existing tenants in this unit's UOBT
                                var existingTenantsInUnit = (from c in obj.UnitsOccupiedByTenants
                                                             where c.UnitId == unitObj.UnitId &&
                                                                   c.IsDeleted != true
                                                             select c).ToList();

                                if (existingTenantsInUnit.Count > 0)
                                {
                                    // Tenant found... checking if same tenant or different
                                    foreach (UnitsOccupiedByTenant n in existingTenantsInUnit)
                                    {
                                        if (n.TenantId != tenantId)
                                        {
                                            Logger.Info("PropertiesController -> EditPropertyUnit - Deleting existing tenant - Old TenantID: " + n.TenantId +
                                                        "], New TenantID: [" + tenantId + "], UnitId: [" + unitObj.UnitId + "]");

                                            // Cliff (10/15/15): Let's keep them un-deleted for now... just do nothing to them (Maybe notify them by email... but not right now)
                                            // UPDATE (10/25/15): This is actually unlikely to ever happen for a while.  Landlords will usually be adding NEW users... they wouldn't be selecting an existing
                                            //                    user that has a Tenant ID yet (until it gets created ealier inside this method).  We might add more functionality where Landlords
                                            //                    can select existing Nooch users, but all the early Landlords will be inviting New users every time.

                                            try
                                            {
                                                Member memberObj = (from c in obj.Members
                                                                    where c.MemberId == n.TenantId &&
                                                                          c.IsDeleted != true
                                                                    select c).FirstOrDefault();

                                                memberObj.Status = "Unlinked";
                                                memberObj.DateModified = DateTime.Now;
                                            }
                                            catch (Exception ex)
                                            {
                                                Logger.Error("PropertiesController -> EditPropertyUnit - Deleting Existing tenant - FAILED to update Member record " +
                                                             "of Existing Tenant - Existing Tenant ID: [" + n.TenantId + "], Exception: [" + ex.Message + "]");
                                            }
                                            n.IsDeleted = true;
                                            n.ModifiedOn = DateTime.Now;
                                            obj.SaveChanges();
                                        }
                                        else
                                        {
                                            sameOrNewTenant += 1;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Error("PropertiesController -> EditPropertyUnit EXCEPTION on trying to delete existing tenant in this unit - [Exception: " + ex.Message + "]");
                            }

                            #endregion Delete Any Existing Tenants For This Unit


                            if (unitInput.Unit.IsTenantAdded == "true")
                            {
                                unitObj.IsOccupied = true;

                                if (!String.IsNullOrEmpty(unitInput.Unit.TenantId))
                                {
                                    unitObj.Status = "Occupied";

                                    #region Create New 'UnitsOccupiedByTenant' Record

                                    if (sameOrNewTenant == 0)
                                    {
                                        UnitsOccupiedByTenant uobt = new UnitsOccupiedByTenant();
                                        uobt.TenantId = tenantId;
                                        uobt.UnitId = unitGuid;
                                        uobt.IsDeleted = false;

                                        obj.UnitsOccupiedByTenants.Add(uobt);
                                    }

                                    #endregion Create New 'UnitsOccupiedByTenant' Record
                                }
                                else if (!String.IsNullOrEmpty(unitInput.Unit.TenantEmail) &&
                                         unitInput.Unit.TenantEmail.Length > 3)
                                {
                                    #region Invite New Tenant For This Unit

                                    unitObj.Status = "Pending Invite";

                                    TenantInfo ti = new TenantInfo
                                    {
                                        email = unitInput.Unit.TenantEmail,
                                    };

                                    basicLandlordPayload authInfo = new basicLandlordPayload
                                    {
                                        AccessToken = unitInput.User.AccessToken,
                                        LandlordId = unitInput.User.LandlordId,
                                        MemberId = unitInput.User.MemberId
                                    };

                                    AddNewTenantInput inviteTenantInputs = new AddNewTenantInput
                                    {
                                        authData = authInfo,
                                        propertyId = unitObj.PropertyId.ToString(),
                                        unitId = unitObj.UnitId.ToString(),
                                        rent = unitInput.Unit.Rent,
                                        startDate = unitInput.Unit.RentStartDate,
                                        leaseLength = unitInput.Unit.LeaseLength,
                                        tenant = ti,
                                    };

                                    InviteTenant(inviteTenantInputs);

                                    #endregion Invite New Tenant For This Unit
                                }
                            }
                            else
                            {
                                unitObj.Status = "Published";
                                unitObj.IsOccupied = false;
                            }

                            obj.SaveChanges();

                            result.IsSuccess = true;
                            result.ErrorMessage = "ok";
                        }
                        else
                        {
                            result.ErrorMessage = "Invalid unit Id passed.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("PropertiesController -> EditPropertyUnit FAILED - [LandlordID: " + unitInput.User.LandlordId + " ], " +
                             "[UnitID: " + unitInput.Unit.UnitId + "], [Exception: " + ex + "]");
                result.ErrorMessage = "Error while creating property. Retry later!";
            }

            return result;
        }


        /// <summary>
        /// To edit an existing property's details.
        /// </summary>
        /// <param name="Property"></param>
        /// <returns>CreatePropertyResultOutput</returns>
        [HttpPost]
        [ActionName("EditProperty")]
        public CreatePropertyResultOutput EditProperty(AddNewPropertyClass Property)
        {
            Logger.Info("PropertiesController -> EditProperty Initiated - [LandlordID: " +
                        Property.User.LandlordId + "], PropertyID: [" + Property.PropertyId + "]");

            CreatePropertyResultOutput result = new CreatePropertyResultOutput();
            result.IsSuccess = false;

            try
            {
                Guid landlordguidId = new Guid(Property.User.LandlordId);
                Guid propertyguidId = new Guid(Property.PropertyId);

                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordguidId, Property.User.AccessToken);

                if (result.AuthTokenValidation.IsTokenOk)
                {
                    var validationResult = IsPropertyDataInputValid(Property, false);

                    if (validationResult.IsDataValid)
                    {
                        using (NOOCHEntities obj = new NOOCHEntities())
                        {
                            var existingProp = (from c in obj.Properties
                                                where c.PropertyId == propertyguidId
                                                select c).FirstOrDefault();

                            if (existingProp != null)
                            {
                                existingProp.PropName = Property.PropertyName.Trim();
                                existingProp.AddressLineOne = Property.Address.Trim();
                                existingProp.City = Property.City.Trim();
                                existingProp.Zip = Property.Zip.Trim();
                                existingProp.State = Property.State.Trim();
                                existingProp.ContactNumber = Property.ContactNumber.Trim();

                                obj.SaveChanges();

                                result.IsSuccess = true;
                                result.ErrorMessage = "OK";
                            }
                            else
                            {
                                result.ErrorMessage = "Invalid property id passed.";
                            }
                        }
                    }
                    else
                    {
                        result.ErrorMessage = validationResult.ValidationError;
                    }

                    return result;
                }
                else
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Landlords API -> Properties -> AddNewProperty FAILED - [LandlordID: " + Property.User.LandlordId +
                             "], [Exception: " + ex + "]");
                result.ErrorMessage = "Error while creating property. Retry later!";
                return result;
            }
        }


        /// <summary>
        /// To mark given property and sub units as active/inactive.
        /// </summary>
        /// <param name="Property"></param>
        /// <returns></returns>
        [HttpPost]
        [ActionName("SetPropertyStatus")]
        public CreatePropertyResultOutput SetPropertyStatus(SetPropertyStatusClass Property)
        {
            Logger.Info("Landlords API -> Properties - SetPropertyStatus - [LandlordID: " +
                            Property.User.LandlorId + "], [Property ID: " + Property.PropertyId + "]");

            CreatePropertyResultOutput result = new CreatePropertyResultOutput();
            result.IsSuccess = false;

            try
            {
                Guid landlordguidId = new Guid(Property.User.LandlorId);
                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordguidId, Property.User.AccessToken);

                if (result.AuthTokenValidation.IsTokenOk)
                {
                    if (!String.IsNullOrEmpty(Property.PropertyId))
                    {
                        Guid propId = new Guid(Property.PropertyId);
                        using (NOOCHEntities obj = new NOOCHEntities())
                        {
                            var properTyInDb = (from c in obj.Properties
                                                where c.PropertyId == propId
                                                select c).FirstOrDefault();

                            // checking units inside property

                            var allUnits = (from d in obj.PropertyUnits
                                            where d.PropertyId == propId
                                            select d).ToList();

                            if (Property.PropertyStatusToSet == false)
                            {
                                // query is to set hide property

                                bool IsAnyocupiedUnitFound = false;
                                if (allUnits.Count > 0)
                                {
                                    foreach (PropertyUnit pu in allUnits)
                                    {
                                        if (pu.IsOccupied == true && (pu.IsDeleted == false || pu.IsDeleted == null) &&
                                            (pu.IsHidden == false || pu.IsDeleted == null))
                                        {
                                            IsAnyocupiedUnitFound = true;
                                        }
                                    }

                                    if (IsAnyocupiedUnitFound)
                                    {
                                        result.ErrorMessage = "Property can't be set to hidden as one or more units are occupied by Tenants.";
                                        return result;
                                    }
                                }
                            }

                            if (properTyInDb != null)
                            {
                                properTyInDb.PropStatus = Property.PropertyStatusToSet ? "Published" : "Not Published";
                                obj.SaveChanges();

                                // updating sub units

                                allUnits = (from d in obj.PropertyUnits
                                            where d.PropertyId == propId
                                            select d).ToList();

                                if (allUnits.Count > 0)
                                {
                                    foreach (PropertyUnit pu in allUnits)
                                    {
                                        if (Property.PropertyStatusToSet)
                                        {
                                            pu.Status = "Published";
                                            pu.IsHidden = false;

                                        }
                                        else
                                        {
                                            pu.Status = "Not Published";
                                            pu.IsHidden = true;
                                        }
                                        obj.SaveChanges();
                                    }
                                }

                                result.IsSuccess = true;
                                result.ErrorMessage = "OK";
                            }
                            else
                            {
                                // invalid property id or no data found
                                result.ErrorMessage = "No property found for given Id.";
                            }
                        }
                    }
                    else
                    {
                        // invalid data sent error
                        result.ErrorMessage = "No property Id passed. Retyr!";
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error("Landlords API -> Properties -> SetPropertyStatus. SetPropertyStatus requested by- [ " +
                             Property.User.LandlorId + " ] . Exception details [ " + ex + " ]");
                result.ErrorMessage = "Error while updating property. Retry later!";
                return result;
            }
        }


        // to delete property  -- TBD with Cliff... what will happen if any unit is occupied.
        // CLIFF (10/3/15): If a unit is occupied and it's deleted, we should probably do nothing to the tenant...
        //                  No reason to "delete" the tenant, and they won't be able to pay rent because the unit will
        //                  be deleted.  And any automatic reminders we send to Tenants to pay their rent each month will
        //                  also stop because this unit will be deleted.  So the tenants can just remain as they were.s
        [HttpPost]
        [ActionName("DeleteProperty")]
        public CreatePropertyResultOutput DeleteProperty(SetPropertyStatusClass Property)
        {
            Logger.Info("Landlords API -> DeleteProperty - [LandlordID: " + Property.User.LandlorId +
                        "], [Property ID: " + Property.PropertyId + "]");

            CreatePropertyResultOutput result = new CreatePropertyResultOutput();
            result.IsSuccess = false;

            try
            {
                Guid landlordguidId = new Guid(Property.User.LandlorId);
                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordguidId, Property.User.AccessToken);

                if (result.AuthTokenValidation.IsTokenOk)
                {
                    if (!String.IsNullOrEmpty(Property.PropertyId))
                    {
                        Guid propId = new Guid(Property.PropertyId);
                        using (NOOCHEntities obj = new NOOCHEntities())
                        {
                            var properTyInDb = (from c in obj.Properties
                                                where c.PropertyId == propId
                                                select c).FirstOrDefault();

                            if (properTyInDb != null)
                            {
                                // Check for units inside this property
                                var allUnits = (from d in obj.PropertyUnits
                                                where d.PropertyId == propId
                                                select d).ToList();

                                bool anyOccupiedUnitFound = false;

                                if (allUnits.Count > 0)
                                {
                                    foreach (PropertyUnit pu in allUnits)
                                    {
                                        if (pu.IsOccupied == true &&
                                            (pu.IsDeleted == false || pu.IsDeleted == null) &&
                                            (pu.IsHidden == false || pu.IsDeleted == null))
                                        {
                                            anyOccupiedUnitFound = true;
                                        }
                                    }

                                    if (!anyOccupiedUnitFound)
                                    {
                                        foreach (PropertyUnit pu in allUnits)
                                        {
                                            pu.IsDeleted = true;
                                            pu.IsHidden = true;
                                            obj.SaveChanges();
                                        }

                                        // code to mark property as deleted
                                        properTyInDb.IsDeleted = true;
                                        obj.SaveChanges();
                                        result.IsSuccess = true;
                                        result.ErrorMessage = "OK";
                                    }
                                    else
                                    {
                                        result.ErrorMessage = "Property can't be deleted as one or more units are occupied by Tenants.";
                                    }
                                }
                                else
                                {
                                    // Mark property as deleted
                                    properTyInDb.IsDeleted = true;
                                    obj.SaveChanges();
                                    result.IsSuccess = true;
                                    result.ErrorMessage = "OK";
                                }
                            }
                            else
                            {
                                // Invalid property ID or no data found
                                result.ErrorMessage = "No property found for given Id.";
                            }
                        }
                    }
                    else
                    {
                        // Invalid data sent
                        result.ErrorMessage = "No property Id passed. Retyr!";
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Landlords API -> Properties -> DeleteProperty - [LandlordID: " +
                             Property.User.LandlorId + "], [Exception: " + ex + "]");
                result.IsSuccess = false;
                result.ErrorMessage = "Error while deleting property. Retry later!";
            }

            return result;
        }


        /// <summary>
        /// To get all properties added by given user
        /// </summary>
        /// <param name="Property"></param>
        /// <returns></returns>
        [HttpPost]
        [ActionName("LoadProperties")]
        public GetAllPropertiesResult LoadProperties(GetProfileDataInput Property)
        {

            GetAllPropertiesResult result = new GetAllPropertiesResult();
            try
            {
                //Logger.Info("PropertiesController -> LoadProperties - [LandlordID: " + Property.LandlorId + "]");

                Guid landlordguidId = new Guid(Property.LandlorId);
                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordguidId, Property.AccessToken);

                if (result.AuthTokenValidation.IsTokenOk)
                {
                    Guid propId = new Guid(Property.LandlorId);

                    using (NOOCHEntities obj = new NOOCHEntities())
                    {
                        var propertyInDb = (from c in obj.Properties
                                            where c.LandlordId == propId &&
                                                 (c.IsDeleted == false || c.IsDeleted == null)
                                            select c).ToList();

                        if (propertyInDb.Count > 0)
                        {
                            List<PropertyClassWithUnits> AllPropertiesPreparedToDisp = new List<PropertyClassWithUnits>();
                            foreach (Property prop in propertyInDb)
                            {
                                PropertyClassWithUnits currentProperty = new PropertyClassWithUnits();

                                currentProperty.PropertyId = prop.PropertyId.ToString();
                                currentProperty.PropStatus = prop.PropStatus ?? "";
                                currentProperty.PropType = prop.PropType ?? "";
                                currentProperty.PropName = prop.PropName ?? "";
                                currentProperty.AddressLineOne = prop.AddressLineOne ?? "";
                                currentProperty.AddressLineTwo = prop.AddressLineTwo ?? "";

                                currentProperty.City = prop.City ?? "";
                                currentProperty.Zip = prop.Zip ?? "";
                                currentProperty.State = prop.State ?? "";
                                currentProperty.ContactNumber = prop.ContactNumber ?? "";

                                currentProperty.DefaultDueDate = prop.DefaultDueDate ?? "";
                                currentProperty.DateAdded = prop.DateAdded != null ? Convert.ToDateTime(prop.DateAdded).ToShortDateString() : "";
                                currentProperty.DateModified = prop.DateModified != null ? Convert.ToDateTime(prop.DateModified).ToShortDateString() : "";

                                currentProperty.LandlordId = prop.LandlordId.ToString();
                                currentProperty.MemberId = prop.MemberId != null ? prop.MemberId.ToString() : "";
                                currentProperty.PropertyImage = prop.PropertyImage ?? "";
                                currentProperty.IsSingleUnit = prop.IsSingleUnit;
                                currentProperty.IsDeleted = prop.IsDeleted;
                                currentProperty.DefaulBank = prop.DefaulBank != null ? prop.DefaulBank.ToString() : "";


                                // Now get all units within this property
                                var AllSubUnits = (from d in obj.PropertyUnits
                                                   where d.PropertyId == prop.PropertyId &&
                                                        (d.IsDeleted == false || d.IsDeleted == null)
                                                   select d).ToList();

                                List<PropertyUnitClass> AllUnitsListPrepared = new List<PropertyUnitClass>();

                                foreach (PropertyUnit pUnit in AllSubUnits)
                                {
                                    PropertyUnitClass currentPUnit = new PropertyUnitClass();

                                    currentPUnit.UnitId = pUnit.UnitId.ToString();
                                    currentPUnit.PropertyId = pUnit.PropertyId.ToString();
                                    currentPUnit.UnitNumber = pUnit.UnitNumber ?? "";

                                    currentPUnit.UnitRent = pUnit.UnitRent ?? "";
                                    currentPUnit.BankAccountId = pUnit.BankAccountId != null ? pUnit.BankAccountId.ToString() : "";
                                    currentPUnit.DateAdded = pUnit.DateAdded != null ? Convert.ToDateTime(pUnit.DateAdded).ToShortDateString() : "";
                                    currentPUnit.ModifiedOn = pUnit.ModifiedOn != null ? Convert.ToDateTime(pUnit.ModifiedOn).ToShortDateString() : "";

                                    currentPUnit.LandlordId = pUnit.LandlordId != null ? pUnit.LandlordId.ToString() : "";
                                    currentPUnit.MemberId = pUnit.MemberId != null ? pUnit.MemberId.ToString() : "";

                                    currentPUnit.UnitImage = pUnit.UnitImage ?? "";
                                    currentPUnit.IsDeleted = pUnit.IsDeleted;
                                    currentPUnit.IsHidden = pUnit.IsHidden;
                                    currentPUnit.IsOccupied = pUnit.IsOccupied;
                                    currentPUnit.Status = pUnit.Status ?? "";
                                    currentPUnit.DueDate = pUnit.DueDate ?? "";

                                    AllUnitsListPrepared.Add(currentPUnit);
                                }

                                currentProperty.AllUnits = AllUnitsListPrepared;
                                currentProperty.UnitsCount = AllUnitsListPrepared.Count.ToString();
                                currentProperty.TenantsCount = obj.GetTenantsCountInGivenPropertyId(currentProperty.PropertyId).FirstOrDefault().ToString();

                                AllPropertiesPreparedToDisp.Add(currentProperty);
                            }

                            result.AllProperties = AllPropertiesPreparedToDisp;

                            result.AllUnitsCount = obj.GetUnitsCountForGivenLandlord(Property.LandlorId).SingleOrDefault().ToString();
                            result.AllPropertysCount = obj.GetPropertiesCountForGivenLandlord(Property.LandlorId).SingleOrDefault().ToString();
                            result.AllTenantsCount = obj.GetTenantsCountForGivenLandlord(Property.LandlorId).SingleOrDefault().ToString();

                            result.IsAccountAdded = Convert.ToBoolean(obj.IsBankAccountAddedforGivenLandlordOrTenant("Landlord", Property.LandlorId).SingleOrDefault());
                            result.IsEmailVerified = Convert.ToBoolean(obj.IsEmailVerifiedforGivenLandlordOrTenant("Landlord", Property.LandlorId).SingleOrDefault());
                            result.IsPhoneVerified = Convert.ToBoolean(obj.IsPhoneVerifiedforGivenLandlordOrTenant("Landlord", Property.LandlorId).SingleOrDefault());

                            result.IsSuccess = true;
                            result.ErrorMessage = "OK";
                        }
                        else
                        {
                            // Invalid property ID or no data found
                            result.IsSuccess = false;
                            result.ErrorMessage = "No properties found for given Landlord.";
                        }
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error("Landlords API -> Properties -> LoadProperties. LoadProperties requested by- [ " +
                             Property.LandlorId + " ] . Exception details [ " + ex + " ]");
                result.IsSuccess = false;
                result.ErrorMessage = "Error while getting properties list. Retry later!";
                return result;
            }
        }





        [HttpPost]
        [ActionName("InviteTenant")]
        public GenericInternalResponse InviteTenant(AddNewTenantInput input)
        {
            Logger.Info("PropertiesController -> InviteTenant Initiated - " +
                        "LandlordID: [" + input.authData.LandlordId +
                        "], Tenant: [" + input.tenant.email +
                        "], Unit #: [" + input.unitId + "]");

            GenericInternalResponse result = new GenericInternalResponse();
            result.success = false;

            try
            {
                Guid landlordGuid = new Guid(input.authData.LandlordId);

                AccessTokenValidationOutput landlordTokenCheck = CommonHelper.AuthTokenValidation(landlordGuid, input.authData.AccessToken);

                if (landlordTokenCheck.IsTokenOk)
                {
                    Logger.Info("PropertiesController -> InviteTenant - Checkpoing #1 - Access Token is OK!");

                    Guid memberId = Guid.NewGuid();
                    Guid tenantGuid = Guid.NewGuid();
                    Guid unitGuid = new Guid(input.unitId);
                    Guid propGuid = new Guid(input.propertyId);

                    string firstName = input.tenant.firstName;
                    string lastName = input.tenant.lastName;
                    string email = input.tenant.email;

                    // Get Landlord's MemberId
                    Guid landlordMemberId;

                    Logger.Info("PropertiesController -> InviteTenant - Checkpoing #2!");

                    using (NOOCHEntities obj = new NOOCHEntities())
                    {
                        var landlordObj = (from c in obj.Landlords
                                           where c.LandlordId == landlordGuid
                                           select c).FirstOrDefault();

                        if (landlordObj != null)
                        {
                            landlordMemberId = new Guid(landlordObj.MemberId.ToString());

                            // Get Unit Object from DB
                            PropertyUnit unitObj = (from c in obj.PropertyUnits
                                                    where c.UnitId == unitGuid
                                                    select c).FirstOrDefault();

                            if (unitObj != null)
                            {
                                Logger.Info("PropertiesController -> InviteTenant - Checkpoing #3 - Found a Unit!");

                                // Check if regular Nooch member (non-Landlord) exists with given email id
                                CheckAndRegisterMemberByEmailResult mem = CommonHelper.CheckIfMemberExistsWithGivenEmailId(input.tenant.email);

                                CheckIfTenantExistsResult ten = CommonHelper.CheckIfTenantExistsWithGivenEmailId(input.tenant.email);

                                Logger.Info("PropertiesController -> InviteTenant - Checkpoing #4");

                                #region Create New Member & Tenant Records

                                if (mem.IsSuccess && // if true, that means the user does NOT exist already
                                    mem.ErrorMessage == "No user found." &&
                                    ten.ErrorMessage == "No tenant found")
                                {
                                    // Create New Member Record
                                    Logger.Info("PropertiesController -> InviteTenant - About to create a New MEMBER Record - [MemberID (just created): " + memberId.ToString() + "]");
                                    CommonHelper.AddNewMemberRecordInDB(memberId, firstName, lastName, email);

                                    // Create New Tenant Record
                                    Logger.Info("PropertiesController -> InviteTenant - About to create a New TENANT Record - [TenantID (just created): " + tenantGuid.ToString() + "]");
                                    CommonHelper.AddNewTenantRecordInDB(tenantGuid, firstName, lastName, email, false, null, null, null, null, null, null, null, false, memberId);
                                }
                                else if (mem.ErrorMessage == "OK" && // Member with that email already exists
                                         ten.ErrorMessage == "No tenant found")
                                {
                                    Logger.Info("PropertiesController -> InviteTenant - Member already exists, so just creating a new TENANT Record - " +
                                                "MemberID: [" + mem.MemberDetails.MemberId.ToString() + "], " +
                                                "TenantID (Just created): " + tenantGuid.ToString() + "]");

                                    firstName = CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(mem.MemberDetails.FirstName));
                                    lastName = CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(mem.MemberDetails.LastName));
                                    email = CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(mem.MemberDetails.UserName));
                                    bool isEmVer = (mem.MemberDetails.Status == "Active" || mem.MemberDetails.Status == "NonRegistered") ? true : false;
                                    DateTime? dob = mem.MemberDetails.DateOfBirth;
                                    string ssn = !String.IsNullOrEmpty(mem.MemberDetails.SSN) ? CommonHelper.GetDecryptedData(mem.MemberDetails.SSN) : null;
                                    string address = !String.IsNullOrEmpty(mem.MemberDetails.Address) ? CommonHelper.GetDecryptedData(mem.MemberDetails.Address) : null;
                                    string city = !String.IsNullOrEmpty(mem.MemberDetails.City) ? CommonHelper.GetDecryptedData(mem.MemberDetails.City) : null;
                                    string state = !String.IsNullOrEmpty(mem.MemberDetails.Status) ? CommonHelper.GetDecryptedData(mem.MemberDetails.Status) : null;
                                    string zip = !String.IsNullOrEmpty(mem.MemberDetails.Zipcode) ? CommonHelper.GetDecryptedData(mem.MemberDetails.Zipcode) : null;
                                    string phone = mem.MemberDetails.ContactNumber;
                                    bool isPhVer = mem.MemberDetails.IsVerifiedPhone == true ? true : false;

                                    memberId = mem.MemberDetails.MemberId; // Use existing MemberID

                                    // Create New Tenant Record
                                    CommonHelper.AddNewTenantRecordInDB(tenantGuid, firstName, lastName, email, isEmVer, dob, ssn, address, city, state, zip, phone, isPhVer, mem.MemberDetails.MemberId);
                                }
                                else if (ten.IsSuccess == false &&
                                         ten.TenantDetails.TenantId != null)
                                {
                                    // Unlikely to ever get here. Only would if a Landlord tries to invite a Tenant that somehow
                                    // does NOT have a Member record, but does have a Tenant record, which shouldn't be possible.
                                    memberId = mem.MemberDetails.MemberId; // Use existing MemberID
                                    tenantGuid = ten.TenantDetails.TenantId;
                                    
                                    Logger.Info("PropertiesController -> InviteTenant - Tenant already exists - " +
                                                "Email: [" + input.tenant.email + "], " +
                                                "MemberID: [" + ten.TenantDetails.MemberId + "], " +
                                                "TenantID: [" + ten.TenantDetails.TenantId.ToString() + "]");
                                }

                                #endregion Create New Member & Tenant Records


                                #region Create New 'UnitsOccupiedByTenant' Record

                                UnitsOccupiedByTenant uobt = new UnitsOccupiedByTenant();
                                uobt.TenantId = tenantGuid;
                                uobt.UnitId = unitGuid;
                                uobt.IsDeleted = false;

                                obj.UnitsOccupiedByTenants.Add(uobt);

                                #endregion Create New 'UnitsOccupiedByTenant' Record


                                #region Update Unit Record in PropertyUnits Table

                                try
                                {
                                    Logger.Info("PropertiesController -> InviteTenant - About to update Property UNITS table - [UnitID: " + unitGuid + "]");

                                    // NOTE: The "PropertyUnits" table and "UnitsOccupiedByTenant" table aren't organized in the best way...
                                    //       UOBT table should have a Property ID (aleady has UnitID)
                                    unitObj.IsOccupied = true;
                                    unitObj.MemberId = tenantGuid;
                                    unitObj.IsHidden = false;
                                    unitObj.ModifiedOn = DateTime.Now;
                                    unitObj.Status = "Pending Invite";
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error("PropertiesController -> InviteTenant - EXCEPTION when attempting to udpate Property UNITS table - [Exception: " + ex + "]");
                                }

                                #endregion Update Unit Record in PropertyUnits Table

                                int saveToDB = 0;
                                saveToDB = obj.SaveChanges();

                                if (saveToDB > 0)
                                {
                                    Logger.Info("PropertiesController -> InviteTenant - All DB Tables SAVED SUCCESSFULLY");
                                    result.success = true;
                                }
                                else
                                {
                                    Logger.Error("PropertiesController -> InviteTenant - FAILED to save All DB Tables");
                                    result.msg = "Failed to save new tenant in UOBT table!";
                                }


                                try
                                {
                                    #region Making Transaction Object

                                    Logger.Info("PropertiesController -> InviteTenant - About to Create New Transaction Object - [PropID: " + propGuid.ToString() + "]");

                                    // SenderId - this would be MemberId of new user who was just created in Members table.
                                    // RecepientID - this would be landlord's MemberId 
                                    Property prop = CommonHelper.GetPropertyByPropId(propGuid);

                                    string propName = (!String.IsNullOrEmpty(prop.PropName)) ? CommonHelper.UppercaseFirst(prop.PropName) : "";

                                    string unitNameToUse = "";

                                    if (!String.IsNullOrEmpty(unitObj.UnitNumber))
                                    {
                                        unitNameToUse = unitObj.UnitNumber;
                                    }
                                    else if (!String.IsNullOrEmpty(unitObj.UnitNickName))
                                    {
                                        unitNameToUse = CommonHelper.UppercaseFirst(unitObj.UnitNickName);
                                    }

                                    Transaction trans = new Transaction();

                                    #region Making entry in GeoLocations table

                                    // Geolocations record is required for transactions table, but not needed for Landlords... so just setting some filler values here
                                    GeoLocation gl = new GeoLocation()
                                    {
                                        LocationId = Guid.NewGuid(),
                                        Latitude = 23.23,
                                        Longitude = 23.23,
                                        Altitude = 23.23,
                                        AddressLine1 = "",
                                        AddressLine2 = "",
                                        City = "",
                                        State = "",
                                        Country = "",
                                        ZipCode = "",
                                        DateCreated = DateTime.Now
                                    };

                                    obj.GeoLocations.Add(gl);
                                    obj.SaveChanges();

                                    #endregion


                                    // Making Transactions object
                                    #region Add Entry To Transactions Table

                                    Guid newTransId = Guid.NewGuid();
                                    Logger.Info("PropertiesController -> InviteTenant - About to Add New Transaction Object to DB - [TransactionID: " + newTransId + "]");

                                    trans.TransactionId = newTransId;
                                    trans.SenderId = memberId;
                                    trans.RecipientId = landlordObj.MemberId;
                                    trans.TransactionDate = DateTime.Now;
                                    trans.Amount = Convert.ToDecimal(input.rent);
                                    trans.TransactionType = CommonHelper.GetEncryptedData("Rent");
                                    trans.TransactionStatus = "Pending";

                                    trans.GeoLocation = gl;
                                    trans.TransactionTrackingId = CommonHelper.GetRandomTransactionTrackingId();
                                    trans.TransactionFee = 0;

                                    trans.IsPhoneInvitation = false;
                                    trans.InvitationSentTo = !String.IsNullOrEmpty(email) ? CommonHelper.GetEncryptedData(email) : null;

                                    trans.DeviceId = null;
                                    trans.DisputeStatus = null;
                                    trans.Memo = DateTime.Now.ToString("MMM") + " Rent - " + unitNameToUse + " " + propName;
                                    trans.Picture = null;

                                    obj.Transactions.Add(trans);
                                    obj.SaveChanges();

                                    #endregion Add Entry To Transactions Table


                                    #endregion Making Transaction Object

                                    Logger.Info("PropertiesController -> InviteTenant - New Transaction Added To DB - Now sending email to tenant");

                                    #region Send Email to New TENANT

                                    #region Setup Email Variables

                                    string LandlordFirstName = CommonHelper.UppercaseFirst((CommonHelper.GetDecryptedData(landlordObj.FirstName)));
                                    string LandlordLastName = CommonHelper.UppercaseFirst((CommonHelper.GetDecryptedData(landlordObj.LastName)));
                                    string LandlordFullName = LandlordFirstName + " " + LandlordLastName;
                                    string landlordEmail = CommonHelper.GetDecryptedData(landlordObj.eMail);

                                    //string CancelRequestLinkForLandlord = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"), "trans/CancelRequest.aspx?TransactionId=" + trans.TransactionId + "&MemberId=" + landlordDetailsInMembersTable.MemberId + "&UserType=U6De3haw2r4mSgweNpdgXQ==");

                                    string rentAmount = trans.Amount.ToString("n2");
                                    string[] rentAmountArray = rentAmount.Split('.');

                                    string memo = trans.Memo;

                                    // Send email to Request Receiver - Send 'UserType', 'LinkSource', 'TransType' as encrypted
                                    // In this case UserType would = 'New'
                                    // TransType would = 'Request'
                                    // and link source would = 'Email'

                                    // added new parameter to identify If user is invited by Landlord IsRentTrans

                                    string rejectRequestLinkForTenant = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"), "trans/rejectMoney.aspx?TransactionId=" + trans.TransactionId + "&UserType=U6De3haw2r4mSgweNpdgXQ==&LinkSource=75U7bZRpVVxLNbQuoMQEGQ==&TransType=T3EMY1WWZ9IscHIj3dbcNw==&IsRentTrans=true");
                                    string paylink = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"), "trans/payRequest.aspx?TransactionId=" + trans.TransactionId + "&IsRentTrans=true");

                                    var tokens2 = new Dictionary<string, string>
												 {
													{Constants.PLACEHOLDER_FIRST_NAME, LandlordFirstName},
													{Constants.PLACEHOLDER_NEWUSER, email},
													{Constants.PLACEHOLDER_TRANSFER_AMOUNT, rentAmountArray[0].ToString()},
													{Constants.PLACEHLODER_CENTS, rentAmountArray[1].ToString()},
													{Constants.PLACEHOLDER_REJECT_LINK, rejectRequestLinkForTenant},
													{Constants.PLACEHOLDER_SENDER_FULL_NAME, LandlordFirstName + " " + LandlordLastName},
													{Constants.MEMO, memo},
													{Constants.PLACEHOLDER_PAY_LINK, paylink}
												 };

                                    #endregion Setup Email Variables

                                    try
                                    {
                                        CommonHelper.SendEmail("requestReceivedToNewUser", landlordEmail, LandlordFullName, email,
                                            "Rent Payment request from " + LandlordFirstName + " " + LandlordLastName,
                                             tokens2, null, null);

                                        Logger.Info("PropertiesController -> requestReceivedToNewUser email sent to - [" + email + "] successfully.");

                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Error("PropertiesController -> Invite Tenant - EXCEPTION on trying to send requestReceivedToNewUser email NOT sent to: [" + email + "]" + ", [Exception: " + ex + "]");
                                        result.msg = "Exception on trying to send email to new tenant";
                                    }

                                    #endregion Send Email to New TENANT


                                    //string rentAmount = input.rent;
                                    //string landlordName = "";
                                    //string propertyName = "";
                                    //string unitNum = "";

                                    //CommonHelper.SendEmail(Constants.TEMPLATE_REGISTRATION, CommonHelper.GetValueFromConfig("welcomeMail"),
                                    //                            email, "NEW Tenant Created :-) $" + input.rent, null, null);

                                    result.success = true;
                                    result.msg = "Request made successfully.";
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error("PropertiesController -> InviteTenant EXCEPTION in block for sending invite to new tenant - [New Tenant Email: " +
                                                 input.tenant.email + "], [Exception: " + ex + "]");
                                    result.msg = "Invite Tenant Exception [1205]";
                                }
                            }
                            else
                            {
                                result.msg = "Unit not found!";
                                Logger.Error("Properties Ctrlr -> InviteTenant ERROR - Unit was not found in PropertyUnits table - [UnitID: " + input.unitId + "]");
                            }
                        }
                    }
                }
                else
                {
                    Logger.Error("Properties Ctrlr -> InviteTenant ERROR - Problem with auth token! - [PropertyID: " + input.propertyId + "], [UnitID: " + input.unitId + "]");
                    result.msg = "Problem with auth token!";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("PropertiesController -> InviteTenant FAILED - [LandlordID: " + input.authData.LandlordId + " ], " +
                             "[UnitID: " + input.unitId + "], [Exception: " + ex + "]");
                result.msg = "Error while creating property. Retry later!";
            }

            return result;
        }





        /// <summary>
        /// To get the details of a sepcific property
        /// </summary>
        /// <param name="Property"></param>
        [HttpPost]
        [ActionName("GetPropertyDetailsPageData")]
        public GetPropertyDetailsPageDataResult GetPropertyDetailsPageData(SetPropertyStatusClass Property)
        {
            GetPropertyDetailsPageDataResult result = new GetPropertyDetailsPageDataResult();
            result.IsSuccess = false;

            try
            {
                Logger.Info("Properties Cntrlr -> GetPropertyDetailsPageData Initiated - [LandlordID: " + Property.User.LandlorId + "]");

                Guid landlordguidId = new Guid(Property.User.LandlorId);
                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordguidId, Property.User.AccessToken);

                if (result.AuthTokenValidation.IsTokenOk)
                {
                    Guid PropertyGuId = new Guid(Property.PropertyId);
                    Guid LandlordGuidId = new Guid(Property.User.LandlorId);

                    using (NOOCHEntities obj = new NOOCHEntities())
                    {
                        var propertyInDb = (from c in obj.Properties
                                            where c.PropertyId == PropertyGuId &&
                                                 (c.IsDeleted == false || c.IsDeleted == null) &&
                                                  c.LandlordId == LandlordGuidId
                                            select c).FirstOrDefault();

                        if (propertyInDb != null)
                        {
                            PropertyClassWithUnits currentProperty = new PropertyClassWithUnits();

                            currentProperty.PropertyId = propertyInDb.PropertyId.ToString();
                            currentProperty.PropStatus = propertyInDb.PropStatus ?? "";
                            currentProperty.PropType = propertyInDb.PropType ?? "";
                            currentProperty.PropName = propertyInDb.PropName ?? "";
                            currentProperty.AddressLineOne = propertyInDb.AddressLineOne ?? "";
                            currentProperty.AddressLineTwo = propertyInDb.AddressLineTwo ?? "";
                            currentProperty.City = propertyInDb.City ?? "";
                            currentProperty.Zip = propertyInDb.Zip ?? "";
                            currentProperty.State = propertyInDb.State ?? "";
                            currentProperty.ContactNumber = propertyInDb.ContactNumber ?? "";
                            currentProperty.DefaultDueDate = propertyInDb.DefaultDueDate ?? "";
                            currentProperty.DateAdded = propertyInDb.DateAdded != null ? Convert.ToDateTime(propertyInDb.DateAdded).ToShortDateString() : "";
                            currentProperty.DateModified = propertyInDb.DateModified != null ? Convert.ToDateTime(propertyInDb.DateModified).ToShortDateString() : "";
                            currentProperty.LandlordId = propertyInDb.LandlordId.ToString();
                            currentProperty.MemberId = propertyInDb.MemberId != null ? propertyInDb.MemberId.ToString() : "";
                            currentProperty.PropertyImage = propertyInDb.PropertyImage ?? "";
                            currentProperty.IsSingleUnit = propertyInDb.IsSingleUnit;
                            currentProperty.IsDeleted = propertyInDb.IsDeleted;


                            #region Get Bank Details For This Property

                            //Logger.Info("Properties Cntrlr -> GetPropertyDetailsPageData - About to attempt to get Synapse Bank info - [PropertyID: " + Property.PropertyId + "]");

                            BankDetailsResult bdetails = new BankDetailsResult();
                            if (propertyInDb.MemberId != null)
                            {
                                Guid memGuid = new Guid(propertyInDb.MemberId.ToString());

                                // Get bank details
                                var bankDetails = (from c in obj.SynapseBanksOfMembers
                                                   where c.MemberId == memGuid && c.IsDefault == true
                                                   select c).FirstOrDefault();

                                if (bankDetails != null)
                                {
                                    result.IsBankAccountAdded = true;

                                    bdetails.BankAccountID = bankDetails.Id.ToString();
                                    bdetails.BankName = bankDetails.bank_name != null
                                                        ? CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(bankDetails.bank_name))
                                                        : "";
                                    bdetails.BankAccountNick = bankDetails.nickname != null
                                                               ? CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(bankDetails.nickname))
                                                               : "";
                                    bdetails.BankAccountNumString = bankDetails.account_number_string != null
                                                                    ? CommonHelper.GetDecryptedData(bankDetails.account_number_string)
                                                                    : "";

                                    string bankNameToMatch = bankDetails.bank_name != null
                                                             ? CommonHelper.GetDecryptedData(bankDetails.bank_name).ToUpper()
                                                             : "";
                                    if (bankNameToMatch.Length > 0)
                                    {
                                        switch (bankNameToMatch)
                                        {
                                            case "ALLY":
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/ally.png";
                                                break;
                                            case "BANK OF AMERICA":
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/bankofamerica.png";
                                                break;
                                            case "BB&T BANK":
                                                bdetails.BankName = "BB&T Bank";
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/bbandt.png";
                                                break;
                                            case "CHASE":
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/chase.png";
                                                break;
                                            case "CITIBANK":
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/citibank.png";
                                                break;
                                            case "CHARLES SCHWAB":
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/schwab.png";
                                                break;
                                            case "CAPITAL ONE 360":
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/capone360.png";
                                                break;
                                            case "FIDELITY"://need logo
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/no.png";
                                                break;
                                            case "FIRST TENNESSEE":
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/firsttennessee.png";
                                                break;
                                            case "US BANK":
                                                bdetails.BankName = "US Bank";
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/usbank.png";
                                                break;
                                            case "USAA":
                                                bdetails.BankName = "USAA";
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/usaa.png";
                                                break;
                                            case "WELLS FARGO":
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/WellsFargo.png";
                                                break;
                                            case "PNC":
                                                bdetails.BankName = "PNC";
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/pnc.png";
                                                break;
                                            case "REGIONS":
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/regions.png";
                                                break;
                                            case "SUNTRUST":
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/suntrust.png";
                                                break;
                                            case "TD BANK":
                                                bdetails.BankName = "TD Bank";
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/td.png";
                                                break;
                                            default:
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/bank.png";
                                                break;
                                        }
                                    }
                                    else
                                    {
                                        bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/bank.png";
                                    }
                                }
                            }
                            else
                            {
                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/bank.png";

                                result.IsBankAccountAdded = false;
                            }
                            result.BankAccountDetails = bdetails;

                            #endregion Get Bank Details For This Property


                            // Get all units of this property
                            #region Get All Units For This Property

                            Logger.Info("Properties Cntrlr -> GetPropertyDetailsPageData - About to attempt to get All Units - [PropertyID: " + Property.PropertyId + "]");

                            var allUnits = (from d in obj.PropertyUnits
                                            where d.PropertyId == propertyInDb.PropertyId &&
                                                 (d.IsDeleted == false || d.IsDeleted == null)
                                            select d).ToList();

                            List<PropertyUnitClass> AllUnitsListPrepared = new List<PropertyUnitClass>();

                            foreach (PropertyUnit unitX in allUnits)
                            {
                                PropertyUnitClass currentUnit = new PropertyUnitClass();

                                currentUnit.UnitId = unitX.UnitId.ToString();
                                currentUnit.PropertyId = unitX.PropertyId.ToString();
                                if (!String.IsNullOrEmpty(unitX.UnitNumber))
                                {
                                    currentUnit.UnitNumber = unitX.UnitNumber;
                                }
                                else if (!String.IsNullOrEmpty(unitX.UnitNickName))
                                {
                                    // When the Landlord entered a Nickname but no actual Unit #, then use the Nickname as the Unit Number
                                    currentUnit.UnitNumber = unitX.UnitNickName;
                                }
                                currentUnit.UnitNickname = unitX.UnitNickName ?? "";

                                currentUnit.UnitRent = unitX.UnitRent ?? "";
                                currentUnit.RentStartDate = unitX.RentStartDate != null
                                                            ? Convert.ToDateTime(unitX.RentStartDate).ToString("MM/dd/yyyyy")
                                                            : null;
                                currentUnit.LeaseLength = unitX.LeaseLength;
                                currentUnit.BankAccountId = unitX.BankAccountId != null ? unitX.BankAccountId.ToString() : "";
                                currentUnit.DateAdded = unitX.DateAdded != null ? Convert.ToDateTime(unitX.DateAdded).ToShortDateString() : "";
                                currentUnit.ModifiedOn = unitX.ModifiedOn != null ? Convert.ToDateTime(unitX.ModifiedOn).ToShortDateString() : "";

                                currentUnit.LandlordId = unitX.LandlordId != null ? unitX.LandlordId.ToString() : "";
                                currentUnit.MemberId = unitX.MemberId != null ? unitX.MemberId.ToString() : "";
                                currentUnit.UnitImage = unitX.UnitImage ?? "";
                                currentUnit.IsDeleted = unitX.IsDeleted;
                                currentUnit.IsHidden = unitX.IsHidden;
                                currentUnit.IsOccupied = unitX.IsOccupied;
                                currentUnit.LeaseDocPath = unitX.LeaseDocumentPath ?? "";


                                if (currentUnit.IsOccupied == true)
                                {
                                    string s = obj.GetTenantNameForGivenUnitId(currentUnit.UnitId).FirstOrDefault();
                                    string tEmail = obj.GetTenantEmailForGivenUnitId(currentUnit.UnitId).FirstOrDefault();
                                    string timgurl = obj.GetTenantImageForGivenUnitId(currentUnit.UnitId).FirstOrDefault();

                                    bool isRentPaid = Convert.ToBoolean(obj.IsRentPaidByTenantForGivenUnitId(currentUnit.UnitId).FirstOrDefault());
                                    bool isemail = Convert.ToBoolean(obj.IsEmailIdVerifiedOfTenantInGivenUnitId(currentUnit.UnitId).FirstOrDefault());
                                    bool isphone = Convert.ToBoolean(obj.IsPhoneVerifiedOfTenantInGivenUnitId(currentUnit.UnitId).FirstOrDefault());

                                    // CLIFF (10/23/15): THIS PROCEDURE IS CAUSING A PROBLEM WHEN THERE IS MORE THAN ONE RESULT
                                    //                   i.e. more than 1 record with the given Tenant/MemberID in the UnitsOccupiedByTenants Table.
                                    //                   I think it's possibly b/c when we Invite a Tenant to a unit, we are not deleting any existing record in UOBT table...
                                    //bool isaccount = Convert.ToBoolean(obj.IsBankAccountAddedOfTenantInGivenUnitId(currentUnit.UnitId).FirstOrDefault());

                                    //DateTime? d = obj.GetLastRentPaymentDateForGivenUnitId(currentUnit.UnitId).FirstOrDefault();
                                    //string lastPayDate = "";

                                    //if (d != null)
                                    //{
                                    //    lastPayDate = Convert.ToDateTime(d).ToShortDateString();
                                    //}

                                    //currentUnit.LastRentPaidOn = lastPayDate;
                                    currentUnit.IsRentPaidForThisMonth = isRentPaid;
                                    currentUnit.IsEmailVerified = isemail;
                                    currentUnit.IsPhoneVerified = isphone;
                                    //currentUnit.IsBankAccountAdded = isaccount;

                                    if (!String.IsNullOrEmpty(s))
                                    {
                                        string[] namesSplit = s.Split(' ');
                                        if (namesSplit.Length > 0)
                                        {
                                            currentUnit.TenantName = CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(namesSplit[0])) + " " +
                                            CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(namesSplit[1]));
                                        }
                                        else
                                        {
                                            currentUnit.TenantName = "";
                                        }
                                    }
                                    else
                                    {
                                        currentUnit.TenantName = "";
                                    }

                                    currentUnit.TenantEmail = !String.IsNullOrEmpty(tEmail) ? CommonHelper.GetDecryptedData(tEmail) : "";
                                    currentUnit.ImageUrl = timgurl ?? "https://www.noochme.com/noochweb/Assets/Images/userpic-default.png";
                                }
                                else
                                {
                                    // Set other tenants-related data to blank
                                    currentUnit.TenantName = "";
                                    currentUnit.TenantEmail = "";
                                    currentUnit.ImageUrl = "";
                                }

                                currentUnit.Status = unitX.Status ?? "";
                                currentUnit.DueDate = unitX.DueDate ?? "";

                                AllUnitsListPrepared.Add(currentUnit);
                            }

                            currentProperty.AllUnits = AllUnitsListPrepared;
                            currentProperty.UnitsCount = AllUnitsListPrepared.Count.ToString();

                            #endregion Get All Units For This Property

                            currentProperty.TenantsCount = obj.GetTenantsCountInGivenPropertyId(currentProperty.PropertyId).FirstOrDefault().ToString();

                            // Get list of all tenants for this property
                            #region Get All Tenants For This Property

                            var AllTenantsInGivenProperty = obj.GetTenantsInGivenPropertyId(currentProperty.PropertyId).ToList();

                            List<TenantDetailsResult> TenantsListForThisPropertyPrepared = new List<TenantDetailsResult>();

                            //Logger.Info("Properties Cntrlr -> GetPropertyDetailsPageData -> AllTenantsInGivenProperty.Count: [" + AllTenantsInGivenProperty.Count + "]");

                            if (AllTenantsInGivenProperty.Count > 0)
                            {
                                foreach (var v in AllTenantsInGivenProperty)
                                {
                                    TenantDetailsResult trc = new TenantDetailsResult();

                                    trc.TenantId = v.TenantId.ToString() ?? "";
                                    trc.UnitId = v.UnitId.ToString() ?? "";

                                    if (!String.IsNullOrEmpty(v.FirstName))
                                    {
                                        trc.Name = CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(v.FirstName));
                                    }
                                    if (!String.IsNullOrEmpty(v.LastName))
                                    {
                                        trc.Name = trc.Name + " " + CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(v.LastName));
                                    }

                                    trc.UnitNumber = v.UnitNumber;
                                    trc.TenantEmail = CommonHelper.GetDecryptedData(v.TenantEmail);
                                    trc.ImageUrl = v.Photo ?? "https://www.noochme.com/noochweb/Assets/Images/userpic-default.png";
                                    trc.UnitRent = v.UnitRent ?? "";
                                    trc.LastRentPaidOn = Convert.ToDateTime(v.LastPaymentDate).ToString("MMM d, yyyy") ?? "";
                                    trc.IsRentPaidForThisMonth = v.IsPaymentDueForThisMonth ?? false;
                                    trc.IsPhoneVerified = v.IsVerifiedPhone ?? false;
                                    //trc.IsEmailVerified = v.IsVerifiedWithSynapse ?? false;
                                    //trc.IsDocumentsVerified = v.IsVerifiedWithSynapse?? false;

                                    if (v.IsVerifiedWithSynapse ?? false)
                                    {
                                        trc.IsBankAccountAdded = true;
                                    }
                                    else
                                    {
                                        trc.IsBankAccountAdded = false;
                                    }

                                    TenantsListForThisPropertyPrepared.Add(trc);
                                }
                            }

                            result.TenantsListForThisProperty = TenantsListForThisPropertyPrepared;

                            #endregion Get All Tenants For This Property

                            result.AllTenantsWithPassedDueDateCount = "0";

                            result.PropertyDetails = currentProperty;

                            result.IsSuccess = true;
                            result.ErrorMessage = "OK";
                        }
                        else
                        {
                            // Invalid property ID or no data found
                            result.ErrorMessage = "No properties found for given Landlord.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Properties Cntrlr -> GetPropertyDetailsPageData FAILED - Outer Exception - [PropertyID: " + Property.User.LandlorId + " ], [Exception: " + ex + "]");
                result.ErrorMessage = "Error while getting properties list. Retry later!";
            }

            return result;
        }


        private PropertyInputValidationResult IsPropertyDataInputValid(AddNewPropertyClass inputData, bool IsUnitsCheckRequired)
        {
            PropertyInputValidationResult res = new PropertyInputValidationResult();
            res.IsDataValid = false;
            res.ValidationError = "OK";

            // Check property data sent
            if (String.IsNullOrEmpty(inputData.PropertyName))
            {
                res.ValidationError = "Property name missing.";
                return res;
            }
            if (String.IsNullOrEmpty(inputData.Address))
            {
                res.ValidationError = "Property Address missing.";
                return res;
            }
            if (String.IsNullOrEmpty(inputData.City))
            {
                res.ValidationError = "Property City missing.";
                return res;
            }
            if (String.IsNullOrEmpty(inputData.Zip))
            {
                res.ValidationError = "Property Zip missing.";
                return res;
            }

            if (IsUnitsCheckRequired)
            {
                if (inputData.IsMultipleUnitsAdded)
                {
                    if (inputData.Unit.Any(unitItem => String.IsNullOrEmpty(unitItem.UnitNum)))
                    {
                        res.ValidationError = "One or more unit(s) number missing in data provided.";
                        return res;
                    }
                    if (inputData.Unit.Any(unitItem => String.IsNullOrEmpty(unitItem.Rent)))
                    {
                        res.ValidationError = "One or more unit(s) missing Rent in data provided.";
                        return res;
                    }
                }
                else if (inputData.Unit.Any(unitItem => String.IsNullOrEmpty(unitItem.Rent)))
                {
                    res.ValidationError = "One or more unit(s) missing Rent in data provided.";
                    return res;
                }
            }

            res.IsDataValid = true;
            return res;
        }


        private class PropertyInputValidationResult
        {
            public bool IsDataValid { get; set; }
            public string ValidationError { get; set; }
        }


        /// <summary>
        /// For uploading a picture for a property.
        /// </summary>
        /// <returns>LoginResult</returns>
        [HttpPost]
        [ActionName("UploadPropertyImage")]
        public LoginResult UploadPropertyImage()
        {
            LoginResult result = new LoginResult();
            result.IsSuccess = false;

            try
            {
                var file = HttpContext.Current.Request.Files.Count > 0 ? HttpContext.Current.Request.Files[0] : null;

                if (file != null && file.ContentLength > 0)
                {
                    string[] propId = HttpContext.Current.Request.Form.GetValues("PropertyId");

                    Logger.Info("Properties Cntrlr -> UploadPropertyImage -> [PropID: " + propId + "]");

                    if (propId != null && propId.Length > 0)
                    {
                        using (NOOCHEntities obj = new NOOCHEntities())
                        {
                            Guid landlordGuidId = new Guid(propId[0]);

                            var fileExtension = Path.GetExtension(file.FileName);
                            var fileName = landlordGuidId.ToString().Replace("-", "_").Replace("'", "").Trim() + fileExtension;

                            Logger.Info("Properties Cntrlr -> UploadPropertyImage -> fileName: [" + fileName.ToString() + "]");

                            var path = Path.Combine(
                                        HttpContext.Current.Server.MapPath(CommonHelper.GetValueFromConfig("PhotoPath")),
                                        fileName);

                            if (File.Exists(path))
                            {
                                File.Delete(path);
                            }

                            file.SaveAs(path);

                            var propDetails = (from c in obj.Properties
                                               where c.PropertyId == landlordGuidId
                                               select c).FirstOrDefault();

                            if (propDetails != null)
                            {
                                propDetails.PropertyImage = CommonHelper.GetValueFromConfig("PhotoUrl") + fileName;
                                obj.SaveChanges();

                                result.IsSuccess = true;
                                result.ErrorMessage = propDetails.PropertyImage;
                            }
                            else
                            {
                                result.ErrorMessage = "Invalid property Id passed.";
                            }
                        }
                    }
                    else
                    {
                        // Prop ID was invalid
                        result.ErrorMessage = "No or invalid property id passed.";
                    }
                }
                else
                {
                    // No file selected
                    result.ErrorMessage = "No or invalid file passed.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Properties Cntrlr -> UploadPropertyImage FAILED - [Exception: " + ex.Message + " ]");
                result.ErrorMessage = "Error while uploading image. Retry.";
            }

            return result;
        }


        /// <summary>
        /// To remove/delete a unit from a property.
        /// </summary>
        /// <param name="unitInput"></param>
        /// <returns>CreatePropertyResultOutput</returns>
        [HttpPost]
        [ActionName("DeletePropertyUnit")]
        public CreatePropertyResultOutput DeletePropertyUnit(SetPropertyStatusClass unitInput)
        {
            Logger.Info("Properties Cntrlr -> DeletePropertyUnit Initiated - [Landlord ID: " +
                        unitInput.User.LandlorId + "], [Unit Id: " + unitInput.PropertyId + "]");

            CreatePropertyResultOutput result = new CreatePropertyResultOutput();
            result.IsSuccess = false;

            try
            {
                Guid landlordGuidId = new Guid(unitInput.User.LandlorId);
                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordGuidId, unitInput.User.AccessToken);

                if (result.AuthTokenValidation.IsTokenOk)
                {
                    if (!String.IsNullOrEmpty(unitInput.PropertyId))
                    {
                        Guid unitId = new Guid(unitInput.PropertyId); // This is actually UnitID NOT PropertyID...

                        using (NOOCHEntities obj = new NOOCHEntities())
                        {
                            var unitInDb = (from c in obj.PropertyUnits
                                            where c.UnitId == unitId && c.IsDeleted == false
                                            select c).FirstOrDefault();

                            if (unitInDb != null)
                            {
                                // Mark this unit as Deleted
                                unitInDb.IsDeleted = true;
                                unitInDb.ModifiedOn = DateTime.Now;

                                obj.SaveChanges();

                                #region Delete UnitOccupiedByTenant Record

                                try
                                {
                                    bool IsAnyocupiedUnitFound = unitInDb.IsOccupied == true;

                                    if (unitInDb.IsOccupied == true)
                                    {
                                        var uobtObj = (from c in obj.UnitsOccupiedByTenants
                                                       where c.UnitId == unitId && c.IsDeleted == false
                                                       select c).ToList();

                                        foreach (UnitsOccupiedByTenant u in uobtObj)
                                        {
                                            u.IsDeleted = true;
                                        }

                                        obj.SaveChanges();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error("Properties Cntrlr -> DeletePropertyUnit EXCEPTION on trying to delete existing UnitsOccupiedByTenant record - " +
                                                 "UnitID: [" + unitInput.PropertyId + "], Exception: [" + ex + "]");
                                }

                                #endregion Delete UnitOccupiedByTenant Record

                                result.IsSuccess = true;
                                result.ErrorMessage = "OK";
                            }
                            else
                            {
                                // Invalid property id or no data found
                                result.ErrorMessage = "No property unit found for given Id.";
                            }
                        }
                    }
                    else
                    {
                        // Invalid data sent error
                        result.ErrorMessage = "No property Id passed. Retyr!";
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error("Landlords API -> Properties -> DeletePropertyUnit - [LandlordID: " +
                             unitInput.User.LandlorId + " ], [Exception: " + ex + "]");
                result.IsSuccess = false;
                result.ErrorMessage = "Error while deleting property. Retry later!";
                return result;
            }
        }


        // service to store lease document of given unit id
        [HttpPost]
        [ActionName("UploadPropertyUnitLeasePDF")]
        public LoginResult UploadPropertyUnitLeasePDF()
        {
            Logger.Info("Properties Cntrlr -> UploadPropertyUnitLeasePDF Inititated");

            LoginResult result = new LoginResult();
            result.IsSuccess = false;

            try
            {
                if (!Request.Content.IsMimeMultipartContent())
                {
                    result.ErrorMessage = "File not passed to server.";
                    return result;
                }

                var file = HttpContext.Current.Request.Files.Count > 0 ? HttpContext.Current.Request.Files[0] : null;

                if (file != null && file.ContentLength > 0)
                {
                    string[] unitId = HttpContext.Current.Request.Form.GetValues("UnitId");

                    if (unitId != null && unitId.Length > 0)
                    {
                        if (!String.IsNullOrEmpty(unitId[0]))
                        {
                            Logger.Info("Properties Cntrlr -> UploadPropertyUnitLeasePDF -> [UnitID: " + unitId[0] + "]");

                            Guid propUnitId = CommonHelper.ConvertToGuid(unitId[0]);

                            using (NOOCHEntities obj = new NOOCHEntities())
                            {
                                var fileExtension = Path.GetExtension(file.FileName);
                                var fileName = propUnitId.ToString().Replace("-", "_").Replace("'", "").Trim() + fileExtension;

                                Logger.Info("Properties Cntrlr -> UploadPropertyUnitLeasePDF -> [File Name: " + fileName + "]");

                                var path = Path.Combine(
                                            HttpContext.Current.Server.MapPath(CommonHelper.GetValueFromConfig("LeaseDocumentsPath")),
                                            fileName);

                                if (File.Exists(path))
                                {
                                    File.Delete(path);
                                }

                                file.SaveAs(path);

                                var propDetails = (from c in obj.PropertyUnits
                                                   where c.UnitId == propUnitId
                                                   select c).FirstOrDefault();

                                if (propDetails != null)
                                {
                                    propDetails.LeaseDocumentPath = CommonHelper.GetValueFromConfig("LeaseDocumentsUrl") + fileName;
                                    obj.SaveChanges();

                                    result.IsSuccess = true;
                                    result.ErrorMessage = propDetails.LeaseDocumentPath;
                                }
                                else
                                {
                                    result.ErrorMessage = "Invalid property unit Id passed.";
                                }
                            }
                        }
                        else
                        {
                            // Prop ID was invalid
                            result.ErrorMessage = "No or invalid property unit id passed.";
                        }
                    }
                }
                else
                {
                    // No file selected
                    result.ErrorMessage = "No or invalid file passed.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Properties Cntrlr -> UploadPropertyUnitLeasePDF FAILED - Exception: [" + ex.Message + " ]");
                result.ErrorMessage = "Error while uploading PDF!";
            }

            return result;
        }
    }
}