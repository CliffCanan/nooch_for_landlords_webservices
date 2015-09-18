using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
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
        public CreatePropertyResultOutput GetUserInfo(AddNewPropertyClass Property)
        {

            CreatePropertyResultOutput result = new CreatePropertyResultOutput();
            try
            {
                Logger.Info("Landlords API -> Properties -> AddNewProperty. AddNewProperty requested by [" +
                            Property.User.LandlorId + "]");
                Guid landlordguidId = new Guid(Property.User.LandlorId);
                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordguidId, Property.User.AccessToken);

                if (result.AuthTokenValidation.IsTokenOk)
                {
                    var validationResult = IsPropertyDataInputValid(Property, true);
                    if (validationResult.IsDataValid)
                    {
                        // all set.... data is ready to be saved in db
                        string propertyImagePath = CommonHelper.GetValueFromConfig("PhotoUrl") + "propertyDefault.png";
                        if (Property.IsPropertyImageAdded)
                        {
                            //getting image url from base64 string
                            string fileName = Property.PropertyName.Trim().Replace("-", "_").Replace(" ", "_") + ".png";
                            propertyImagePath =
                                CommonHelper.SaveBase64AsImage(landlordguidId.ToString().Replace("-", "_") + "_property_" + fileName,
                                    Property.PropertyImage);
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
                                PropertyImage = propertyImagePath,
                                IsSingleUnit = !Property.IsMultipleUnitsAdded,
                                IsDeleted = false


                            };


                            obj.Properties.Add(prop);
                            obj.SaveChanges();

                            // setting type to single unit if only one item passed in property units
                            if (Property.Unit.Length < 2)
                            {
                                prop.IsSingleUnit = true;
                                prop.PropType = "Single Unit";
                                obj.SaveChanges();
                            }


                            // saving units... if any
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
                                pu.MemberId = new Guid(CommonHelper.GetMemberIdOfLandlord(landlordguidId));


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
                        result.IsSuccess = false;
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
                Logger.Error("Landlords API -> Properties -> AddNewProperty. AddNewProperty requested by- [ " + Property.User.LandlorId + " ] . Exception details [ " + ex + " ]");
                result.IsSuccess = false;
                result.ErrorMessage = "Error while creating property. Retry later!";
                return result;

            }
        }



        [HttpPost]
        [ActionName("EditProperty")]
        public CreatePropertyResultOutput EditProperty(AddNewPropertyClass Property)
        {

            CreatePropertyResultOutput result = new CreatePropertyResultOutput();
            try
            {
                Logger.Info("Landlords API -> Properties -> AddNewProperty. AddNewProperty requested by [" +
                            Property.User.LandlorId + "]");
                Guid landlordguidId = new Guid(Property.User.LandlorId);
                Guid propertyguidId = new Guid(Property.PropertyId);

                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordguidId, Property.User.AccessToken);

                if (result.AuthTokenValidation.IsTokenOk)
                {
                    var validationResult = IsPropertyDataInputValid(Property, false);
                    if (validationResult.IsDataValid)
                    {

                        using (NOOCHEntities obj = new NOOCHEntities())
                        {

                            var existingProp = (from c in obj.Properties where c.PropertyId == propertyguidId select c).FirstOrDefault();


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
                                result.IsSuccess = false;
                                result.ErrorMessage = "Invalid property id passed.";
                            }


                        }


                    }
                    else
                    {
                        result.IsSuccess = false;
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
                Logger.Error("Landlords API -> Properties -> AddNewProperty. AddNewProperty requested by- [ " + Property.User.LandlorId + " ] . Exception details [ " + ex + " ]");
                result.IsSuccess = false;
                result.ErrorMessage = "Error while creating property. Retry later!";
                return result;

            }
        }

        // To mark given property and sub units as active/inactive
        [HttpPost]
        [ActionName("SetPropertyStatus")]
        public CreatePropertyResultOutput SetPropertyStatus(SetPropertyStatusClass Property)
        {

            CreatePropertyResultOutput result = new CreatePropertyResultOutput();
            try
            {
                Logger.Info("Landlords API -> Properties -> SetPropertyStatus. SetPropertyStatus requested by [" +
                            Property.User.LandlorId + "]");
                Guid landlordguidId = new Guid(Property.User.LandlorId);
                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordguidId, Property.User.AccessToken);

                if (result.AuthTokenValidation.IsTokenOk)
                {

                    if (!String.IsNullOrEmpty(Property.PropertyId))
                    {
                        Guid propId = new Guid(Property.PropertyId);
                        using (NOOCHEntities obj = new NOOCHEntities())
                        {
                            var properTyInDb =
                                (from c in obj.Properties where c.PropertyId == propId select c).FirstOrDefault();


                            // checking units inside property

                            var allUnits =
                                (from d in obj.PropertyUnits where d.PropertyId == propId select d).ToList();



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
                                        result.IsSuccess = false;
                                        result.ErrorMessage =
                                            "Property can't be set to hidden as one or more units are occupied by Tenants.";
                                        return result;
                                    }

                                }



                            }





                            if (properTyInDb != null)
                            {
                                properTyInDb.PropStatus = Property.PropertyStatusToSet ? "Published" : "Not Published";
                                obj.SaveChanges();


                                // updating sub units

                                allUnits =
                                   (from d in obj.PropertyUnits where d.PropertyId == propId select d).ToList();

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
                                result.IsSuccess = false;
                                result.ErrorMessage = "No property found for given Id.";

                            }
                        }

                    }
                    else
                    {
                        // invalid data sent error
                        result.IsSuccess = false;
                        result.ErrorMessage = "No property Id passed. Retyr!";
                    }


                }
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error("Landlords API -> Properties -> SetPropertyStatus. SetPropertyStatus requested by- [ " +
                             Property.User.LandlorId + " ] . Exception details [ " + ex + " ]");
                result.IsSuccess = false;
                result.ErrorMessage = "Error while updating property. Retry later!";
                return result;

            }
        }



        // to delete property  -- TBD with Cliff... what will happen if any unit is occupied .
        [HttpPost]
        [ActionName("DeleteProperty")]
        public CreatePropertyResultOutput DeleteProperty(SetPropertyStatusClass Property)
        {

            CreatePropertyResultOutput result = new CreatePropertyResultOutput();
            try
            {
                Logger.Info("Landlords API -> DeleteProperty -> DeleteProperty. SetPropertyStatus requested by [" +
                            Property.User.LandlorId + "] and Property Id - [ " + Property.PropertyId + " ]");
                Guid landlordguidId = new Guid(Property.User.LandlorId);
                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordguidId, Property.User.AccessToken);

                if (result.AuthTokenValidation.IsTokenOk)
                {

                    if (!String.IsNullOrEmpty(Property.PropertyId))
                    {
                        Guid propId = new Guid(Property.PropertyId);
                        using (NOOCHEntities obj = new NOOCHEntities())
                        {
                            var properTyInDb =
                                (from c in obj.Properties where c.PropertyId == propId select c).FirstOrDefault();

                            if (properTyInDb != null)
                            {

                                // checking units inside property

                                var allUnits =
                                    (from d in obj.PropertyUnits where d.PropertyId == propId select d).ToList();

                                bool IsAnyocupiedUnitFound = false;
                                if (allUnits.Count > 0)
                                {
                                    foreach (PropertyUnit pu in allUnits)
                                    {
                                        if (pu.IsOccupied == true && (pu.IsDeleted == false || pu.IsDeleted == null) && (pu.IsHidden == false || pu.IsDeleted == null))
                                        {
                                            IsAnyocupiedUnitFound = true;
                                        }
                                    }

                                    if (!IsAnyocupiedUnitFound)
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
                                        result.IsSuccess = false;
                                        result.ErrorMessage = "Property can't be deleted as one or more units are occupied by Tenants.";
                                    }
                                }
                                else
                                {
                                    // code to mark property as deleted
                                    properTyInDb.IsDeleted = true;
                                    obj.SaveChanges();
                                    result.IsSuccess = true;
                                    result.ErrorMessage = "OK";

                                }



                            }
                            else
                            {
                                // invalid property id or no data found
                                result.IsSuccess = false;
                                result.ErrorMessage = "No property found for given Id.";

                            }
                        }

                    }
                    else
                    {
                        // invalid data sent error
                        result.IsSuccess = false;
                        result.ErrorMessage = "No property Id passed. Retyr!";
                    }


                }
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error("Landlords API -> Properties -> DeleteProperty. DeleteProperty requested by- [ " +
                             Property.User.LandlorId + " ] . Exception details [ " + ex + " ]");
                result.IsSuccess = false;
                result.ErrorMessage = "Error while deleting property. Retry later!";
                return result;

            }
        }


        // to get all properties added by given user
        [HttpPost]
        [ActionName("LoadProperties")]
        public GetAllPropertysResultClass LoadProperties(GetProfileDataInput Property)
        {

            GetAllPropertysResultClass result = new GetAllPropertysResultClass();
            try
            {
                Logger.Info("Landlords API -> Properties -> LoadProperties. LoadProperties requested by [" +
                            Property.LandlorId + "]");
                Guid landlordguidId = new Guid(Property.LandlorId);
                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordguidId, Property.AccessToken);

                if (result.AuthTokenValidation.IsTokenOk)
                {


                    Guid propId = new Guid(Property.LandlorId);
                    using (NOOCHEntities obj = new NOOCHEntities())
                    {
                        var properTyInDb =
                            (from c in obj.Properties
                             where c.LandlordId == propId &&
                                 (c.IsDeleted == false || c.IsDeleted == null)
                             select c).ToList();

                        if (properTyInDb.Count > 0)
                        {

                            List<PropertyClassWithUnits> AllPropertiesPreparedToDisp = new List<PropertyClassWithUnits>();
                            foreach (Property prop in properTyInDb)
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


                                // raeding all units of this property
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

                                    currentPUnit.DueDate = pUnit.DueDate != null ? Convert.ToDateTime(pUnit.DueDate).ToShortDateString() : "";

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

                            result.IsAccountAdded = Convert.ToBoolean( obj.IsBankAccountAddedforGivenLandlordOrTenant("Landlord",Property.LandlorId).SingleOrDefault());
                            result.IsEmailVerified = Convert.ToBoolean(obj.IsEmailVerifiedforGivenLandlordOrTenant("Landlord", Property.LandlorId).SingleOrDefault());
                            result.IsPhoneVerified = Convert.ToBoolean(obj.IsPhoneVerifiedforGivenLandlordOrTenant("Landlord", Property.LandlorId).SingleOrDefault());

                            result.IsSuccess = true;
                            result.ErrorMessage = "OK";


                        }
                        else
                        {
                            // invalid property id or no data found
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


        // to get all properties added by given user
        [HttpPost]
        [ActionName("GetPropertyDetailsPageData")]
        public GetPropertyDetailsPageDataResultClass GetPropertyDetailsPageData(SetPropertyStatusClass Property)
        {

            GetPropertyDetailsPageDataResultClass result = new GetPropertyDetailsPageDataResultClass();
            try
            {
                Logger.Info("Landlords API -> Properties -> LoadProperties. LoadProperties requested by [" +
                            Property.User.LandlorId + "]");
                Guid landlordguidId = new Guid(Property.User.LandlorId);
                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordguidId, Property.User.AccessToken);

                if (result.AuthTokenValidation.IsTokenOk)
                {


                    Guid PropertyGuId = new Guid(Property.PropertyId);
                    Guid LandlordGuidId = new Guid(Property.User.LandlorId);
                    using (NOOCHEntities obj = new NOOCHEntities())
                    {
                        var properTyInDb =
                            (from c in obj.Properties
                             where c.PropertyId == PropertyGuId &&
                                 (c.IsDeleted == false || c.IsDeleted == null) && c.LandlordId == LandlordGuidId
                             select c).FirstOrDefault();

                        if (properTyInDb != null)
                        {


                            PropertyClassWithUnits currentProperty = new PropertyClassWithUnits();

                            currentProperty.PropertyId = properTyInDb.PropertyId.ToString();
                            currentProperty.PropStatus = properTyInDb.PropStatus ?? "";
                            currentProperty.PropType = properTyInDb.PropType ?? "";
                            currentProperty.PropName = properTyInDb.PropName ?? "";
                            currentProperty.AddressLineOne = properTyInDb.AddressLineOne ?? "";
                            currentProperty.AddressLineTwo = properTyInDb.AddressLineTwo ?? "";

                            currentProperty.City = properTyInDb.City ?? "";
                            currentProperty.Zip = properTyInDb.Zip ?? "";
                            currentProperty.State = properTyInDb.State ?? "";
                            currentProperty.ContactNumber = properTyInDb.ContactNumber ?? "";

                            currentProperty.DefaultDueDate = properTyInDb.DefaultDueDate ?? "";
                            currentProperty.DateAdded = properTyInDb.DateAdded != null ? Convert.ToDateTime(properTyInDb.DateAdded).ToShortDateString() : "";
                            currentProperty.DateModified = properTyInDb.DateModified != null ? Convert.ToDateTime(properTyInDb.DateModified).ToShortDateString() : "";

                            currentProperty.LandlordId = properTyInDb.LandlordId.ToString();
                            currentProperty.MemberId = properTyInDb.MemberId != null ? properTyInDb.MemberId.ToString() : "";

                            currentProperty.PropertyImage = properTyInDb.PropertyImage ?? "";

                            currentProperty.IsSingleUnit = properTyInDb.IsSingleUnit;
                            currentProperty.IsDeleted = properTyInDb.IsDeleted;

                            //currentProperty.DefaulBank = properTyInDb.DefaulBank != null ? properTyInDb.DefaulBank.ToString() : "."; // hard coded it for testing purpose
                            //if (currentProperty.DefaulBank != "")
                            //{
                            //    result.IsBankAccountAdded = true;

                            //    // code here to add bank details

                            //    BankDetailsReusltClass bdetails = new BankDetailsReusltClass();
                            //    bdetails.BankAccountID = "XXXX-11111-22222-1231232";
                            //    bdetails.BankName = "Bank of America";

                            //    bdetails.BankIcon = "http://54.201.43.89/landlords/db/UploadedImages/PropertyImages/21d4e1ee_865c_40dd_b841_15807546daae_property_test_bASDASD.png";
                            //    bdetails.BankAccountNick = "Checking account";
                            //    bdetails.BankAccountNumString = "1234";

                            //    result.BankAccountDetails = bdetails;


                            //}
                            //else
                            //{
                            //    result.IsBankAccountAdded = false;
                            //}

                            BankDetailsReusltClass bdetails = new BankDetailsReusltClass();
                            if (properTyInDb.DefaulBank != null)
                            {

                                int bnkId = Convert.ToInt16(properTyInDb.DefaulBank);
                                // gettting bank details
                                var allBankDetails =
                                    (from c in obj.SynapseBanksOfMembers
                                     where c.Id == bnkId && c.IsDefault == true
                                     select c).FirstOrDefault();
                                if (allBankDetails != null)
                                {
                                    result.IsBankAccountAdded = true;

                                    bdetails.BankAccountID = allBankDetails.Id.ToString();
                                    bdetails.BankName = allBankDetails.bank_name != null ? CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(allBankDetails.bank_name)) : "";
                                    bdetails.BankAccountNick = allBankDetails.nickname != null ? CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(allBankDetails.nickname)) : "";
                                    bdetails.BankAccountNumString = allBankDetails.account_number_string != null ? CommonHelper.GetDecryptedData(allBankDetails.account_number_string) : "";

                                    string bankNameToMatch = allBankDetails.bank_name != null
                                        ? CommonHelper.GetDecryptedData(allBankDetails.bank_name).ToUpper()
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
                                            case "BB&T BANK"://need logo
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/no.png";
                                                break;
                                            case "CHASE":
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/chase.png";
                                                break;
                                            case "CITIBANK":
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/citibank.png";
                                                break;
                                            case "CHARLES SCHWAB"://need logo
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/no.png";
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
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/usbank.png";
                                                break;
                                            case "USAA":
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/usaa.png";
                                                break;
                                            case "WELLS FARGO":
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/WellsFargo.png";
                                                break;
                                            case "PNC":
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/pnc.png";
                                                break;
                                            case "REGIONS":
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/regions.png";
                                                break;
                                            case "SUNTRUST":
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/suntrust.png";
                                                break;
                                            case "TD BANK":
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/td.png";
                                                break;
                                            default:
                                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/no.png";
                                                break;
                                        }
                                    }
                                    else
                                    {

                                        bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/no.png";
                                    }






                                }





                            }
                            else
                            {

                                bdetails.BankIcon = "https://www.noochme.com/noochweb/Assets/Images/bankPictures/no.png";

                                result.IsBankAccountAdded = false;
                            }
                            result.BankAccountDetails = bdetails;
                            result.PropertyDetails = currentProperty;

                            // raeding all units of this property
                            var AllSubUnits = (from d in obj.PropertyUnits
                                               where d.PropertyId == properTyInDb.PropertyId &&
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

                                currentPUnit.DueDate = pUnit.DueDate != null ? Convert.ToDateTime(pUnit.DueDate).ToShortDateString() : "";

                                AllUnitsListPrepared.Add(currentPUnit);

                            }


                            //var alLUnitIds =
                            //    from c in obj.PropertyUnits
                            //        where c.PropertyId == properTyInDb.PropertyId
                            //        select c.UnitId;
                            //var alltenantsCount =
                            //    (from dc in obj.UnitsOccupiedByTenants where alLUnitIds.Contains(dc.UnitId) select dc);


                            currentProperty.AllUnits = AllUnitsListPrepared;
                            currentProperty.UnitsCount = AllUnitsListPrepared.Count.ToString();
                            currentProperty.TenantsCount = obj.GetTenantsCountInGivenPropertyId(currentProperty.PropertyId).FirstOrDefault().ToString();


                            // getting tenants list for this property
                            var AllTenantsOccupiedGivenPropertyUnits =
                                obj.GetTenantsInGivenPropertyId(currentProperty.PropertyId).ToList();

                            List<TenantDetailsResultClass> TenantsListForThisPropertyPrepared = new List<TenantDetailsResultClass>();

                            if (AllTenantsOccupiedGivenPropertyUnits.Count > 0)
                            {
                                foreach (var v in AllTenantsOccupiedGivenPropertyUnits)
                                {
                                    TenantDetailsResultClass trc = new TenantDetailsResultClass();
                                    trc.TenantId = v.TenantId.ToString() ?? "";
                                    trc.UnitId = v.UnitId.ToString() ?? "";
                                    if (!String.IsNullOrEmpty(v.FirstName))
                                    {
                                        trc.Name = CommonHelper.GetDecryptedData(v.FirstName);
                                    }
                                    if (!String.IsNullOrEmpty(v.LastName))
                                    {
                                        trc.Name = trc.Name + " " + CommonHelper.GetDecryptedData(v.LastName);
                                    }

                                    trc.ImageUrl = v.UserPic ?? "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAHgAAAB4CAMAAAAOusbgAAAAPFBMVEX///+4uLjr6+u1tbWysrL39/f7+/vg4OC7u7vm5ubMzMzt7e3BwcH09PT5+fm9vb3KysrW1tbR0dHZ2dlNTySgAAAC4ElEQVRoge2a25arIAyGRRDE8+H933XLjNNWu5WcqGvW+N/16luJSUh/yLJbt27d+ivq6jz3Ps/r4oNQm49DU6ovlc0wevsJauErZYzWapXWy6/Bpw68dY1+QtWT3s9tQqwN2Dfqim7mZBlvD7Eruk6CtfMZ9gttxgRBF1OMG9CV+Je2QxwbyI1wedvBQLgLeRAlF1BuiFkw23YC5XklV3IV5hDchTxLcQuFAisj1M/Agn4JuelEwA6HVVLJLrABS1W2Rwe8kB2f2zXogBdwzx8jOYErEvIEnlkb8MAGl6SIleaOr5oU8DJEPBM8U8EjE4w5Hl7F/cjocfkQcyMoeiJXlbzh1ZZUsMpZ4JoO5pV1Teb+WvBVqWYUF2//uaydGAOEuXfRTkWBc9FddUi0Vx2LWU9cBLjc61Yf2uzS7EzTGkrkT8xVC33WXfUXhhCySMCLKmRh60bIE7C4pV5rMRfEoywBqUQHzRjzZRK09yx8fulKxof4EZRsBM2mlQyzFMW5mZ3jFab1JJvnb3kVSbfWLo1XbquzoLWS9U838tXhulsO/JPwWIU78oB049LFW0+nW3Y/JbmTWII1keIyRj7s4vjiZ5Nw6Yz7AXpMLLUtV2TdaRu9o4WmtXWxwbGXURKDpIXcN+2D1hN7F2gp5q3AulfTuIHMamrczrMlc7wIR+cGMnn38viy2pCpf6ByolX9JJckd4/NJZKJfbQjE7qqEuCG5Q/LpZouexlkaTM8zL1Qg8TKJDoIt2lTXIBDIbqZdK13JIwfMgpV1rcM+FLXckb0u7SCfmWpVvoRtKVEv3AQ9Cvj3kCAyKCQBXv4AQb1ck2+DThWDxlf8pmG5TpBpmG5LojO+DkY8FiB9hghSo6vItQr8nMB7kbIF02nAlwV8Fe8/4LLGLdLwl3IsalJfX4RU/SNl08Fju0h0kfiAxybXWm6CbCGyG49L+BYI18GbvNESvn8+tatW7c+o3+CASXGSkLOCwAAAABJRU5ErkJggg==";  // will modify it after testing
                                    trc.UnitRent = v.UnitRent ?? "";
                                    trc.LastRentPaidOn = Convert.ToDateTime(v.LastPaymentDate).ToString(" MMM d, yyyy") ?? "";

                                    trc.IsRentPaidForThisMonth = v.IsPaymentDueForThisMonth ?? false;
                                    trc.IsPhoneVerified = v.IsPhoneVerfied ?? false;

                                    trc.IsEmailVerified = v.IsEmailVerified ?? false;
                                    trc.IsDocumentsVerified = v.IsIdDocumentVerified ?? false;

                                    if (!String.IsNullOrEmpty(v.BankAccountId.ToString()))
                                    {
                                        trc.IsBankAccountAdded = true;
                                    }
                                    else
                                    {
                                        trc.IsBankAccountAdded = false;
                                    }

                                    trc.UnitNumber = v.UnitNumber;
                                    trc.TenantEmail = CommonHelper.GetDecryptedData(v.TenantEmail);

                                    TenantsListForThisPropertyPrepared.Add(trc);
                                }
                            }

                            result.TenantsListForThisProperty = TenantsListForThisPropertyPrepared;

                            result.AllTenantsWithPassedDueDateCount = "0";

                            result.IsSuccess = true;
                            result.ErrorMessage = "OK";


                        }
                        else
                        {
                            // invalid property id or no data found
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
                             Property.User.LandlorId + " ] . Exception details [ " + ex + " ]");
                result.IsSuccess = false;
                result.ErrorMessage = "Error while getting properties list. Retry later!";
                return result;

            }
        }


        private PropertyInputValidationResult IsPropertyDataInputValid(AddNewPropertyClass inputData, bool IsUnitsCheckRequired)
        {
            PropertyInputValidationResult res = new PropertyInputValidationResult();
            res.IsDataValid = true;
            res.ValidationError = "OK";
            // checking property details
            if (String.IsNullOrEmpty(inputData.PropertyName))
            {
                res.IsDataValid = false;
                res.ValidationError = "Property name missing.";
                return res;
            }

            if (String.IsNullOrEmpty(inputData.Address))
            {
                res.IsDataValid = false;
                res.ValidationError = "Property Address missing.";
                return res;
            }

            if (String.IsNullOrEmpty(inputData.City))
            {
                res.IsDataValid = false;
                res.ValidationError = "Property City missing.";
                return res;
            }
            if (String.IsNullOrEmpty(inputData.Zip))
            {
                res.IsDataValid = false;
                res.ValidationError = "Property Zip missing.";
                return res;
            }

            //if (String.IsNullOrEmpty(inputData.Rent))
            //{
            //    res.IsDataValid = false;
            //    res.ValidationError = "Property Rent missing.";
            //    return res;
            //}

            //if (inputData.IsMultipleUnitsAdded)
            //{


            if (IsUnitsCheckRequired)
            {



                if (inputData.IsMultipleUnitsAdded)
                {


                    if (inputData.Unit.Any(unitItem => String.IsNullOrEmpty(unitItem.UnitNum)))
                    {
                        res.IsDataValid = false;
                        res.ValidationError = "One or more unit(s) number missing in data provided.";
                        return res;
                    }
                    if (inputData.Unit.Any(unitItem => String.IsNullOrEmpty(unitItem.Rent)))
                    {
                        res.IsDataValid = false;
                        res.ValidationError = "One or more unit(s) missing Rent in data provided.";
                        return res;
                    }
                }
                else
                {
                    if (inputData.Unit.Any(unitItem => String.IsNullOrEmpty(unitItem.Rent)))
                    {
                        res.IsDataValid = false;
                        res.ValidationError = "One or more unit(s) missing Rent in data provided.";
                        return res;
                    }
                }

            }
            //}
            return res;

        }

        private class PropertyInputValidationResult
        {
            public bool IsDataValid { get; set; }public string ValidationError { get; set; }
        }
    }
}