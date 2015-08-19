using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
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
                    var validationResult = IsPropertyDataInputValid(Property);
                    if (validationResult.IsDataValid)
                    {
                        // all set.... data is ready to be saved in db
                        string propertyImagePath = "no_image";
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

                            if (properTyInDb != null)
                            {
                                properTyInDb.PropStatus = Property.PropertyStatusToSet ? "Published" : "Not Published";
                                obj.SaveChanges();


                                // updating sub units

                                var allUnits =
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

                                AllPropertiesPreparedToDisp.Add(currentProperty);




                            }

                            result.AllProperties = AllPropertiesPreparedToDisp;

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


        private PropertyInputValidationResult IsPropertyDataInputValid(AddNewPropertyClass inputData)
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
            //}
            return res;

        }

        private class PropertyInputValidationResult
        {
            public bool IsDataValid { get; set; }public string ValidationError { get; set; }
        }
    }
}