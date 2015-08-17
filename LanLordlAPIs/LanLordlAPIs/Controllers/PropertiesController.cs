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
                            string fileName = Property.PropertyName.Trim().Replace("-", "_").Replace(" ","_") + ".png";
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