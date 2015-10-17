﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Web.Services.Protocols;
using LanLordlAPIs.Classes.Utility;
using LanLordlAPIs.Models.db_Model;
using LanLordlAPIs.Models.Input_Models;
using LanLordlAPIs.Models.Output_Models;

namespace LanLordlAPIs.Controllers
{
    public class UsersController : ApiController
    {
        [HttpPost]
        [ActionName("Login")]
        public LoginResult Login(LoginInput User)
        {
            LoginResult result = new LoginResult();

            try
            {
                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    DateTime requestDatetime = DateTime.Now;

                    if (String.IsNullOrEmpty(User.Ip) ||
                        String.IsNullOrEmpty(User.UserName) ||
                        String.IsNullOrEmpty(User.Password))
                    {
                        result.IsSuccess = false;
                        result.ErrorMessage = "Invalid login information provided.";
                        return result;
                    }

                    Logger.Info("Landlords API -> Users -> Login. Login requested by [" + User.UserName + "]");

                    #region All authentication code in this block

                    string passEncrypted = CommonHelper.GetEncryptedData(User.Password);
                    string userNameEncrypted = CommonHelper.GetEncryptedData(User.UserName);

                    // checking if username and password is correct and given user is landlord or not
                    var userCheckResult = (from c in obj.Landlords
                                           join d in obj.Members on c.MemberId equals d.MemberId
                                           where d.UserName == userNameEncrypted &&
                                                 d.Password == passEncrypted &&
                                                 d.IsDeleted == false &&
                                                 c.IsDeleted == false &&
                                                 (c.Status != "Suspended" || c.Status != "Temporarily_Blocked")
                                           select
                                               new
                                               {
                                                   c.LandlordId,
                                                   c.IpAddresses,
                                                   c.MemberId
                                               }
                        ).FirstOrDefault();

                    if (userCheckResult != null)
                    {
                        //updating ip in db
                        var landlordEntity = (from ll in obj.Landlords
                                              where ll.LandlordId == userCheckResult.LandlordId
                                              select ll).FirstOrDefault();

                        CommonHelper.saveLandlordIp(userCheckResult.LandlordId, User.Ip);
                        landlordEntity.DateModified = requestDatetime;
                        landlordEntity.LastSeenOn = requestDatetime;

                        landlordEntity.WebAccessToken = CommonHelper.GenerateAccessToken();

                        obj.SaveChanges();

                        result.IsSuccess = true;
                        result.ErrorMessage = "OK";
                        result.AccessToken = landlordEntity.WebAccessToken;
                        result.MemberId = landlordEntity.MemberId.ToString();
                        result.LandlordId = landlordEntity.LandlordId.ToString();
                    }
                    else
                    {
                        result.IsSuccess = false;
                        result.ErrorMessage = "Invalid Username password or Member not active.";
                        return result;
                    }

                    #endregion

                    return result;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Landlords API -> Users -> Login. Error while login request from username  - [ " + User.UserName + " ] . Exception details [ " + ex + " ]");
                result.IsSuccess = false;
                result.ErrorMessage = "Error while logging on. Retry.";
                return result;
            }
        }


        /// <summary>
        /// To save a landlord user's profile picture.
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [ActionName("UploadLandlordProfileImage")]
        public LoginResult UploadLandlordProfileImage()
        {
            Logger.Error("Landlords API -> UsersController -> UploadProfileImage Initiated");

            LoginResult result = new LoginResult();
            result.IsSuccess = false;

            //GetProfileDataInput User = new GetProfileDataInput();

            try
            {
                var file = HttpContext.Current.Request.Files.Count > 0 ? HttpContext.Current.Request.Files[0] : null;

                if (file != null && file.ContentLength > 0)
                {
                    string[] llId = HttpContext.Current.Request.Form.GetValues("LandlorId");

                    if (llId != null && llId.Length > 0)
                    {
                        using (NOOCHEntities obj = new NOOCHEntities())
                        {
                            Guid landlordguidId = new Guid(llId[0]);

                            var fileExtension = Path.GetExtension(file.FileName);
                            var fileName = landlordguidId.ToString().Replace("-", "_").Trim() + fileExtension;

                            var path = Path.Combine(
                                HttpContext.Current.Server.MapPath(CommonHelper.GetValueFromConfig("UserPhotoPath")),
                                fileName);

                            if (File.Exists(path))
                            {
                                File.Delete(path);
                            }

                            file.SaveAs(path);

                            var llDetails = (from c in obj.Landlords
                                             where c.LandlordId == landlordguidId
                                             select c).FirstOrDefault();

                            if (llDetails != null)
                            {
                                llDetails.UserPic = CommonHelper.GetValueFromConfig("UserPhotoUrl") + fileName;
                                obj.SaveChanges();
                                result.IsSuccess = true;
                                result.ErrorMessage = llDetails.UserPic;
                            }
                            else
                            {
                                result.ErrorMessage = "Invalid landlord Id passed.";
                            }
                        }
                    }
                    else
                    {
                        // No Lanlord ID found
                        result.ErrorMessage = "No landlord ID found.";
                    }
                }
                else
                {
                    // no file selected

                    result.ErrorMessage = "No or invalid file passed.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Landlords API -> UsersController -> UploadProfileImage FAILED. [LandlordID: ], [Exception: " + ex + "]");
                result.ErrorMessage = "Error while uploading image. Retry.";
            }

            return result;
        }


        /// <summary>
        /// To get a Landlord's user details.
        /// </summary>
        /// <param name="User"></param>
        /// <returns>LandlordProfileInfoResult object</returns>
        [HttpPost]
        [ActionName("GetUserInfo")]
        public LandlordProfileInfoResult GetUserInfo(GetProfileDataInput User)
        {
            //Logger.Info("UsersController -> GetUserInfo Initiated [" + User.LandlorId + "]");

            LandlordProfileInfoResult res = new LandlordProfileInfoResult();
            res.IsSuccess = false;

            try
            {
                Guid landlordguidId = new Guid(User.LandlorId);

                res.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordguidId, User.AccessToken);

                if (res.AuthTokenValidation.IsTokenOk)
                {
                    using (NOOCHEntities obj = new NOOCHEntities())
                    {
                        // Reading Landlord's details from Landlords Table in  DB
                        var landlordObj = (from c in obj.Landlords
                                           where c.LandlordId == landlordguidId
                                           select c).FirstOrDefault();

                        if (landlordObj != null)
                        {
                            res.MemberId = landlordObj.MemberId.ToString();

                            res.FirstName = !String.IsNullOrEmpty(landlordObj.FirstName) ? CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(landlordObj.FirstName)) : "";
                            res.LastName = !String.IsNullOrEmpty(landlordObj.LastName) ? CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(landlordObj.LastName)) : "";
                            res.AccountType = !String.IsNullOrEmpty(landlordObj.Type) ? landlordObj.Type : "";
                            res.SubType = !String.IsNullOrEmpty(landlordObj.SubType) ? landlordObj.SubType : "";

                            res.IsPhoneVerified = (landlordObj.IsPhoneVerified == true) ? true : false;
                            res.IsEmailVerified = (landlordObj.IsEmailVerfieid == true) ? true : false;

                            res.DOB = landlordObj.DateOfBirth != null ? Convert.ToDateTime(landlordObj.DateOfBirth).ToString("d") : "";
                            res.SSN = !String.IsNullOrEmpty(landlordObj.SSN) ? CommonHelper.GetDecryptedData(landlordObj.SSN) : "";
                            res.isIdVerified = (landlordObj.IsIdVerified == true) ? true : false;

                            res.UserEmail = !String.IsNullOrEmpty(landlordObj.eMail) ? CommonHelper.GetDecryptedData(landlordObj.eMail) : "";
                            res.MobileNumber = !String.IsNullOrEmpty(landlordObj.MobileNumber) ? CommonHelper.FormatPhoneNumber(landlordObj.MobileNumber) : "";

                            if (!String.IsNullOrEmpty(landlordObj.AddressLineOne))
                            {
                                res.Address = CommonHelper.GetDecryptedData(landlordObj.AddressLineOne);
                                res.AddressLine1 = CommonHelper.GetDecryptedData(landlordObj.AddressLineOne);
                            }
                            else
                            {
                                res.AddressLine1 = "";
                            }

                            if (!String.IsNullOrEmpty(landlordObj.AddressLineTwo))
                            {
                                res.Address += " " + CommonHelper.GetDecryptedData(landlordObj.AddressLineTwo);
                                res.AddressLine2 = CommonHelper.GetDecryptedData(landlordObj.AddressLineTwo);
                            }
                            else
                            {
                                res.AddressLine2 = "";
                            }
                            if (!String.IsNullOrEmpty(landlordObj.City))
                            {
                                res.Address += " " + CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(landlordObj.City));
                                res.City = CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(landlordObj.City));
                            }
                            else
                            {
                                res.City = "";
                            }

                            if (!String.IsNullOrEmpty(landlordObj.State))
                            {
                                res.Address += " " + CommonHelper.GetDecryptedData(landlordObj.State);
                                res.AddState = CommonHelper.GetDecryptedData(landlordObj.State);
                            }
                            else
                            {
                                res.AddState = "";
                            }
                            if (!String.IsNullOrEmpty(landlordObj.Zip))
                            {
                                res.Address += " " + CommonHelper.GetDecryptedData(landlordObj.Zip);
                                res.Zip = CommonHelper.GetDecryptedData(landlordObj.Zip);
                            }
                            else
                            {
                                res.Zip = "";
                            }
                            if (!String.IsNullOrEmpty(landlordObj.Country))
                            {
                                res.Address += " " + CommonHelper.GetDecryptedData(landlordObj.Country);
                                res.Country = CommonHelper.GetDecryptedData(landlordObj.Country);
                            }
                            else
                            {
                                res.Country = "";
                            }

                            res.TwitterHandle = !String.IsNullOrEmpty(landlordObj.TwitterHandle)
                                                    ? landlordObj.TwitterHandle
                                                    : "";
                            res.FbUrl = !String.IsNullOrEmpty(landlordObj.FBId)
                                                    ? landlordObj.FBId : "";
                            res.InstaUrl = !String.IsNullOrEmpty(landlordObj.InstagramUrl)
                                                    ? landlordObj.InstagramUrl
                                                    : "";
                            res.CompanyName = !String.IsNullOrEmpty(landlordObj.CompanyName)
                                                    ? CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(landlordObj.CompanyName))
                                                    : "";
                            res.CompanyEID = !String.IsNullOrEmpty(landlordObj.CompanyEIN)
                                                    ? CommonHelper.GetDecryptedData(landlordObj.CompanyEIN)
                                                    : "";

                            res.UserImageUrl = landlordObj.UserPic;
                            res.TenantsCount = obj.GetTenantsCountForGivenLandlord(User.LandlorId).SingleOrDefault().ToString();
                            res.PropertiesCount = obj.GetPropertiesCountForGivenLandlord(User.LandlorId).SingleOrDefault().ToString();
                            res.UnitsCount = obj.GetUnitsCountForGivenLandlord(User.LandlorId).SingleOrDefault().ToString();
                            res.IsSuccess = true;
                            res.ErrorMessage = "OK";

                            // Now get Landlord's details from MEMBERS Table in  DB
                            var memberObj = (from c in obj.Members
                                             where c.MemberId == landlordObj.MemberId
                                             select c).FirstOrDefault();

                            if (memberObj != null)
                            {
                                if (memberObj.IsVerifiedWithSynapse == true)
                                {
                                    res.isIdVerified = true;
                                }

                                // Now check the values in the Member Table and use them if they are verified
                                res.IsPhoneVerified = (memberObj.IsVerifiedPhone == true) ? true : res.IsPhoneVerified;
                                res.IsEmailVerified = (memberObj.Status == "Active") ? true : false;
                            }
                        }

                        return res;
                    }
                }
                else
                {
                    res.IsSuccess = false;
                    res.ErrorMessage = "Auth token failure";
                    return res;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Landlords API -> Users -> GetUserInfo. Error while GetUserInfo request from LandlorgId - [ " + User.LandlorId + " ] . Exception details [ " + ex + " ]");
                res.IsSuccess = false;
                res.ErrorMessage = "Error while logging on. Retry.";
                return res;
            }
        }


        [HttpPost]
        [ActionName("EditUserInfo")]
        public CreatePropertyResultOutput EditUserInfo(EditPersonalInfoInputClass User)
        {
            CreatePropertyResultOutput result = new CreatePropertyResultOutput();
            result.IsSuccess = false;

            try
            {
                Guid landlordguidId = new Guid(User.DeviceInfo.LandlorId);
                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordguidId, User.DeviceInfo.AccessToken);

                if (result.AuthTokenValidation.IsTokenOk)
                {
                    // valid access token continue with edit

                    using (NOOCHEntities obj = new NOOCHEntities())
                    {
                        //reading details from db
                        var lanlordObj = (from c in obj.Landlords
                                          where c.LandlordId == landlordguidId
                                          select c).FirstOrDefault();

                        if (lanlordObj != null)
                        {
                            if (User.UserInfo.InfoType == "Personal")
                            {
                                #region Editing Personal Info

                                if (String.IsNullOrEmpty(User.UserInfo.FullName))
                                {
                                    result.ErrorMessage = "Name missing";
                                    return result;
                                }

                                string firstName = "", lastName = "";
                                string[] nameAftetSplit = User.UserInfo.FullName.Trim().ToLower().Split(' ');

                                if (nameAftetSplit.Length > 1)
                                {
                                    firstName = CommonHelper.UppercaseFirst(nameAftetSplit[0]);

                                    for (int i = 1; i < nameAftetSplit.Length; i++)
                                    {
                                        lastName += CommonHelper.UppercaseFirst(nameAftetSplit[i]) + " ";
                                    }

                                    // Now store info in DB
                                    lanlordObj.FirstName = CommonHelper.GetEncryptedData(firstName.Trim());
                                    lanlordObj.LastName = CommonHelper.GetEncryptedData(lastName.Trim());
                                    if (!String.IsNullOrEmpty(User.UserInfo.DOB))
                                    {
                                        lanlordObj.DateOfBirth = Convert.ToDateTime(User.UserInfo.DOB);
                                    }
                                    if (!String.IsNullOrEmpty(User.UserInfo.SSN) &&
                                        User.UserInfo.SSN.Length == 4)
                                    {
                                        lanlordObj.SSN = CommonHelper.GetEncryptedData(User.UserInfo.SSN);
                                    }
                                    lanlordObj.DateModified = DateTime.Now;

                                    obj.SaveChanges();

                                    result.IsSuccess = true;
                                    result.ErrorMessage = "OK";
                                }
                                else
                                {
                                    result.ErrorMessage = "Last or First Name missing.";
                                    return result;
                                }

                                #endregion Editing Personal Info
                            }

                            else if (User.UserInfo.InfoType == "Company")
                            {
                                #region Editing Company Info

                                if (String.IsNullOrEmpty(User.UserInfo.CompanyName))
                                {
                                    result.ErrorMessage = "Company name missing.";
                                    return result;
                                }

                                // Now store company info in DB
                                lanlordObj.CompanyName = CommonHelper.GetEncryptedData(CommonHelper.UppercaseFirst(User.UserInfo.CompanyName));
                                if (!String.IsNullOrEmpty(User.UserInfo.CompanyEID))
                                {
                                    lanlordObj.CompanyEIN = CommonHelper.GetEncryptedData(User.UserInfo.CompanyEID);
                                }
                                lanlordObj.DateModified = DateTime.Now;

                                obj.SaveChanges();

                                result.IsSuccess = true;
                                result.ErrorMessage = "OK";

                                #endregion Editing Company Info
                            }

                            else if (User.UserInfo.InfoType == "Contact")
                            {
                                #region Editing Contact Info

                                if (String.IsNullOrEmpty(User.UserInfo.UserEmail))
                                {
                                    result.ErrorMessage = "eMail missing.";
                                    return result;
                                }

                                if (String.IsNullOrEmpty(User.UserInfo.MobileNumber))
                                {
                                    result.ErrorMessage = "Contact number missing.";
                                    return result;
                                }

                                if (String.IsNullOrEmpty(User.UserInfo.AddressLine1))
                                {
                                    result.ErrorMessage = "Address missing.";
                                    return result;
                                }

                                string userEmailNew = CommonHelper.GetEncryptedData(User.UserInfo.UserEmail.ToLower().Trim());

                                // Check if given email is already registered or not
                                var lanlorddetailsbyEmail = (from c in obj.Landlords
                                                             where c.eMail == userEmailNew &&
                                                                   c.LandlordId != landlordguidId
                                                             select c).FirstOrDefault();

                                if (lanlorddetailsbyEmail != null)
                                {
                                    result.ErrorMessage = "User with given email already exists.";
                                    return result;
                                }

                                // Now store all contact info in DB
                                lanlordObj.DateModified = DateTime.Now;

                                if (lanlordObj.eMail != userEmailNew)
                                {
                                    lanlordObj.IsEmailVerfieid = false;
                                    lanlordObj.eMail = userEmailNew;
                                }

                                if (String.IsNullOrEmpty(User.UserInfo.MobileNumber))
                                {
                                    if (CommonHelper.RemovePhoneNumberFormatting(lanlordObj.MobileNumber) != CommonHelper.RemovePhoneNumberFormatting(User.UserInfo.MobileNumber))
                                    {
                                        lanlordObj.MobileNumber = CommonHelper.RemovePhoneNumberFormatting(User.UserInfo.MobileNumber);
                                    }
                                }

                                lanlordObj.AddressLineOne = CommonHelper.GetEncryptedData(User.UserInfo.AddressLine1);

                                obj.SaveChanges();

                                result.IsSuccess = true;
                                result.ErrorMessage = "OK";

                                #endregion Editing Contact Info
                            }

                            else if (User.UserInfo.InfoType == "Social")
                            {
                                #region Editing Social Info

                                // Now store all social info in DB

                                if (!String.IsNullOrEmpty(User.UserInfo.TwitterHandle))
                                {
                                    lanlordObj.TwitterHandle = User.UserInfo.TwitterHandle;
                                }
                                if (!String.IsNullOrEmpty(User.UserInfo.FbUrl))
                                {
                                    lanlordObj.FBId = User.UserInfo.FbUrl;
                                }
                                if (!String.IsNullOrEmpty(User.UserInfo.InstaUrl))
                                {
                                    lanlordObj.InstagramUrl = User.UserInfo.InstaUrl;
                                }

                                obj.SaveChanges();

                                result.IsSuccess = true;
                                result.ErrorMessage = "OK";

                                #endregion Editing Social Info
                            }
                        }
                        else
                        {
                            result.ErrorMessage = "Given landlord ID not found.";
                        }
                    }
                }
                else
                {
                    result.ErrorMessage = result.AuthTokenValidation.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Landlords API -> UsersController -> EditUserInfo FAILED - [Outer Exception: " + ex.ToString() + "]");
                result.ErrorMessage = "Server error";
            }

            return result;
        }


        [HttpPost]
        [ActionName("submitLandlordIdVerWiz")]
        public GenericInternalResponse submitLandlordIdVerWiz(idVerWizardInput landlordsInput)
        {
            Logger.Error("Landlords API -> UsersController -> submitLandlordIdVerWiz Initiated - [LandlordID: " + landlordsInput.DeviceInfo.LandlorId + "]");

            GenericInternalResponse res = new GenericInternalResponse();
            res.success = false;

            try
            {
                Guid landlordguidId = new Guid(landlordsInput.DeviceInfo.LandlorId);
                var checkToken = CommonHelper.AuthTokenValidation(landlordguidId, landlordsInput.DeviceInfo.AccessToken);

                if (checkToken.IsTokenOk)
                {
                    // valid access token continue with edit

                    using (NOOCHEntities obj = new NOOCHEntities())
                    {
                        // Get Landlord details from DB
                        var lanlordObj = (from c in obj.Landlords
                                          where c.LandlordId == landlordguidId
                                          select c).FirstOrDefault();

                        if (lanlordObj != null)
                        {
                            #region Update LANDLORDS Table

                            // Now store info in DB
                            if (!String.IsNullOrEmpty(landlordsInput.dob))
                            {
                                lanlordObj.DateOfBirth = Convert.ToDateTime(landlordsInput.dob);
                            }
                            if (!String.IsNullOrEmpty(landlordsInput.ssn) &&
                                landlordsInput.ssn.Length == 4)
                            {
                                lanlordObj.SSN = CommonHelper.GetEncryptedData(landlordsInput.ssn);
                            }
                            if (!String.IsNullOrEmpty(landlordsInput.staddress))
                            {
                                lanlordObj.AddressLineOne = CommonHelper.GetEncryptedData(landlordsInput.staddress);
                            }
                            if (!String.IsNullOrEmpty(landlordsInput.zip))
                            {
                                lanlordObj.Zip = CommonHelper.GetEncryptedData(landlordsInput.zip);
                            }
                            lanlordObj.IsIdVerified = true;
                            lanlordObj.DateModified = DateTime.Now;

                            #endregion Update LANDLORDS Table


                            #region Update MEMBERS Table

                            // CLIFF (10/15/15): Since all the Synapse methods take the data from the Members Table,
                            //                   we have to also save any of that data for Landlords in the Members Table 
                            //                   ...even though we have most of the same data in the Landlords table.  We shouldn't have duplicated everything :-(

                            var memberObj = (from c in obj.Members
                                             where c.MemberId == lanlordObj.MemberId
                                             select c).FirstOrDefault();

                            if (memberObj != null)
                            {
                                string firstName = "", lastName = "";
                                string[] nameAftetSplit = landlordsInput.fullName.Trim().ToLower().Split(' ');

                                if (nameAftetSplit.Length > 1)
                                {
                                    firstName = CommonHelper.UppercaseFirst(nameAftetSplit[0]);

                                    for (int i = 1; i < nameAftetSplit.Length; i++)
                                    {
                                        lastName += CommonHelper.UppercaseFirst(nameAftetSplit[i]) + " ";
                                    }
                                }
                                memberObj.FirstName = CommonHelper.GetEncryptedData(firstName.Trim());
                                memberObj.LastName = CommonHelper.GetEncryptedData(lastName.Trim());
                                if (!String.IsNullOrEmpty(landlordsInput.dob))
                                {
                                    memberObj.DateOfBirth = Convert.ToDateTime(landlordsInput.dob);
                                }
                                if (!String.IsNullOrEmpty(landlordsInput.ssn) &&
                                    landlordsInput.ssn.Length == 4)
                                {
                                    memberObj.SSN = CommonHelper.GetEncryptedData(landlordsInput.ssn);
                                }
                                if (!String.IsNullOrEmpty(landlordsInput.staddress))
                                {
                                    memberObj.Address = CommonHelper.GetEncryptedData(landlordsInput.staddress);
                                }
                                if (!String.IsNullOrEmpty(landlordsInput.zip))
                                {
                                    memberObj.Zipcode = CommonHelper.GetEncryptedData(landlordsInput.zip);
                                }

                                // Setting IsVerifiedPhone to be 'true' because it might prevent the Landlord from adding a bank
                                memberObj.IsVerifiedPhone = true;

                                memberObj.DateModified = DateTime.Now;

                                obj.SaveChanges();

                                res.success = true;
                                res.msg = "OK";
                            }
                            else
                            {
                                Logger.Error("Landlords API -> UsersController -> submitLandlordIdVerWiz FAILED - Member Not Found");
                            }

                            #endregion Update MEMBERS Table
                        }
                        else
                        {
                            Logger.Error("Landlords API -> UsersController -> submitLandlordIdVerWiz FAILED - Landlord Not Found");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("UsersControllers -> submitLandlordIdVerWiz FAILED - [LandlordID: " +
                             landlordsInput.DeviceInfo.LandlorId + "], [Exception: " + ex + "]");

                res.msg = "Server exception while submitting SSN info - try again later!";
            }

            return res;
        }


        [HttpPost]
        [ActionName("RegisterLandlord")]
        public RegisterLandlordResult RegisterLandlord(RegisterLandlordInput llDetails)
        {
            RegisterLandlordResult result = new RegisterLandlordResult();
            result.IsSuccess = false;

            try
            {
                // Check if Lanlord exists for given account
                CheckAndRegisterLandlordByEmailResult ll = CommonHelper.checkAndRegisterLandlordByemailId(llDetails.eMail);

                if (ll.IsSuccess && ll.ErrorMessage == "No user found.")
                {
                    // Check if regular Nooch member (non-Landlord) exists with given email id
                    CheckAndRegisterMemberByEmailResult mem = CommonHelper.CheckIfMemberExistsWithGivenEmailId(llDetails.eMail);

                    if (mem.IsSuccess && mem.ErrorMessage == "No user found.")
                    {
                        #region Save New Landlord & Member Details In DB

                        // Make new entry in Members Table first
                        var userNameLowerCase = llDetails.eMail.Trim().ToLower();
                        string noochRandomId = CommonHelper.GetRandomNoochId();

                        if (noochRandomId == null)
                        {
                            result.ErrorMessage = "Some duplicate values are being generated at server. Retry later! ";
                            return result;
                        }

                        using (NOOCHEntities obj = new NOOCHEntities())
                        {
                            #region Create User Settings & Save To DB

                            #region Create Member Object

                            string randomPin = CommonHelper.GetRandomPinNumber();
                            var member = new Member
                            {
                                Nooch_ID = noochRandomId,
                                MemberId = Guid.NewGuid(),
                                UserName = CommonHelper.GetEncryptedData(llDetails.eMail),
                                FirstName = CommonHelper.GetEncryptedData(llDetails.FirstName),
                                LastName = CommonHelper.GetEncryptedData(llDetails.LastName),
                                SecondaryEmail = llDetails.eMail,
                                RecoveryEmail = llDetails.eMail,
                                Password = CommonHelper.GetEncryptedData(llDetails.Password),
                                PinNumber = CommonHelper.GetEncryptedData(randomPin),
                                Status = Constants.STATUS_REGISTERED,

                                IsDeleted = false,
                                DateCreated = DateTime.Now,
                                UserNameLowerCase = CommonHelper.GetEncryptedData(userNameLowerCase),
                                FacebookAccountLogin = null,
                                InviteCodeIdUsed = null,
                                Type = "Landlord",
                                IsVerifiedPhone = false,
                                IsVerifiedWithSynapse = false,
                                UDID1 = llDetails.fingerprint,

                                // some blanks as default
                                Address = CommonHelper.GetEncryptedData(" "),
                                State = CommonHelper.GetEncryptedData(" "),
                                City = CommonHelper.GetEncryptedData(" "),
                                Zipcode = CommonHelper.GetEncryptedData(" "),
                                Country = llDetails.country,
                                ContactNumber = CommonHelper.GetEncryptedData(" ")
                            };

                            #endregion Create Member Object

                            obj.Members.Add(member);

                            try
                            {
                                obj.SaveChanges();

                                CommonHelper.setReferralCode(member.MemberId);
                                var tokenId = Guid.NewGuid();

                                #region Send Verification email

                                // Send registration email to member with autogenerated token 
                                var link = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"),
                                           "/Registration/Activation.aspx?tokenId=" + tokenId);
                                var fromAddress = CommonHelper.GetValueFromConfig("welcomeMail");

                                var tokens = new Dictionary<string, string>
                                {
                                    {
                                        Constants.PLACEHOLDER_FIRST_NAME,
                                        CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(member.FirstName))
                                    },
                                    {
                                        Constants.PLACEHOLDER_LAST_NAME,
                                        CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(member.LastName))
                                    },
                                    {Constants.PLACEHOLDER_OTHER_LINK, link}
                                };
                                try
                                {
                                    CommonHelper.SendEmail(Constants.TEMPLATE_REGISTRATION,
                                        fromAddress, llDetails.eMail.Trim(),
                                        "Confirm your email on Nooch",
                                        tokens, null);

                                    Logger.Info("UserController -> RegisterLandlord - Registration email sent to [" + llDetails.eMail.Trim() + "] successfully.");
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error("UserController -> RegisterLandlord - Registration email NOT sent to [" +
                                                           llDetails.eMail.Trim() + "], [Exception: " + ex + "]");
                                }

                                #endregion Send Verification email


                                #region Create Authentication Token

                                var requestId = Guid.Empty;

                                var token = new AuthenticationToken
                                {
                                    TokenId = tokenId,
                                    MemberId = member.MemberId,
                                    IsActivated = false,
                                    DateGenerated = DateTime.Now,
                                    FriendRequestId = requestId
                                };
                                // Now save the token details into Authentication tokens DB table  
                                obj.AuthenticationTokens.Add(token);
                                obj.SaveChanges();

                                #endregion Create Authentication Token


                                #region Notification Settings

                                var memberNotification = new MemberNotification
                                {
                                    NotificationId = Guid.NewGuid(),

                                    MemberId = member.MemberId,
                                    FriendRequest = true,
                                    InviteRequestAccept = true,
                                    TransferSent = true,
                                    TransferReceived = true,
                                    TransferAttemptFailure = true,
                                    NoochToBank = true,
                                    BankToNooch = true,
                                    EmailFriendRequest = true,
                                    EmailInviteRequestAccept = true,
                                    EmailTransferSent = true,
                                    EmailTransferReceived = true,
                                    EmailTransferAttemptFailure = true,
                                    TransferUnclaimed = true,
                                    BankToNoochRequested = true,
                                    BankToNoochCompleted = true,
                                    NoochToBankRequested = true,
                                    NoochToBankCompleted = true,
                                    InviteReminder = true,
                                    LowBalance = true,
                                    ValidationRemainder = true,
                                    ProductUpdates = true,
                                    NewAndUpdate = true,
                                    DateCreated = DateTime.Now
                                };

                                obj.MemberNotifications.Add(memberNotification);

                                #endregion Notification Settings


                                #region Privacy Settings

                                var memberPrivacySettings = new MemberPrivacySetting
                                {
                                    MemberId = member.MemberId,
                                    AllowSharing = true,
                                    ShowInSearch = true,
                                    DateCreated = DateTime.Now
                                };
                                obj.MemberPrivacySettings.Add(memberPrivacySettings);

                                #endregion Privacy Settings

                                // Finally, make an entry in Landlords Table 
                                Landlord l = CommonHelper.AddNewLandlordEntryInDb(llDetails.FirstName,
                                    llDetails.LastName, llDetails.eMail, llDetails.Password, false, false,
                                    llDetails.ip, member.MemberId);

                                if (l != null)
                                {
                                    result.IsSuccess = true;
                                    result.ErrorMessage = "OK";
                                }
                                else
                                {
                                    // exception while creating account
                                    result.ErrorMessage = "Server error. Retry later! ";
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Error("UsersController -> RegisterLandlord FAILED while making account for: [" + llDetails.eMail + "], [Exception: " + ex.Message + "]");
                                result.ErrorMessage = "Some duplicate values are being generated at server. Retry later! ";
                            }
                            #endregion Create User Settings & Save To DB
                        }

                        #endregion Save New Landlord & Member Details In DB
                    }
                    else
                    {
                        // Member with that email already exists
                        #region New Landlord But Existing Member

                        Landlord l = CommonHelper.AddNewLandlordEntryInDb(llDetails.FirstName,
                            llDetails.LastName, llDetails.eMail, llDetails.Password, true, true,
                            llDetails.ip, mem.MemberDetails.MemberId);

                        if (l != null)
                        {
                            result.IsSuccess = true;
                            result.ErrorMessage = "OK";

                            return result;
                        }
                        else
                        {
                            // exception while creating account
                            result.IsSuccess = false;
                            result.ErrorMessage = "Server error. Retry later! ";
                            return result;
                        }

                        #endregion New Landlord But Existing Member
                    }
                }
                else
                {
                    result.ErrorMessage = ll.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("UsersController -> RegisterLandlord FAILED - Outer Exception - [Email: " + llDetails.eMail + "], [Exception: " + ex + "]");
                result.ErrorMessage = "Server Error.";
            }
            return result;
        }


        [HttpPost]
        [ActionName("ResetPassword")]
        public PasswordResetOutputClass ResetPassword(PasswordResetInputClass userName)
        {
            PasswordResetOutputClass res = new PasswordResetOutputClass();
            res.IsSuccess = false;

            var getMember = CommonHelper.getMemberByEmailId(userName.eMail);

            try
            {
                if (getMember != null)
                {
                    bool status = CommonHelper.SendPasswordMail(getMember, userName.eMail);

                    if (status)
                    {
                        res.IsSuccess = true;
                        res.ErrorMessage = "Your reset password link has been sent to your mail successfully.";
                        return res;
                    }
                    else
                    {
                        res.ErrorMessage = "Problem occured while sending mail.";
                    }
                }
                else
                {
                    res.ErrorMessage = "Problem occured while sending mail.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("UsersController -> ResetPassword FAILED - [UserName: " + userName + "], [Exception: " + ex + "]");

                res.ErrorMessage = "Problem occured while sending mail.";
            }

            return res;
        }


        /// <summary>
        /// to get account setting stats
        /// </summary>
        /// <param name="Property"></param>
        [HttpPost]
        [ActionName("GetAccountCompletetionStatsOfGivenLandlord")]
        public GetAccountCompletionStatsResultClass GetAccountCompletetionStatsOfGivenLandlord(GetProfileDataInput Property)
        {
            Logger.Info("UsersController -> GetAccountCompletetionStatsOfGivenLandlord Initiated - [LandlordID: " +
                            Property.LandlorId + "]");

            GetAccountCompletionStatsResultClass result = new GetAccountCompletionStatsResultClass();
            result.IsSuccess = false;

            try
            {
                Guid landlordguidId = new Guid(Property.LandlorId);
                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordguidId, Property.AccessToken);

                if (result.AuthTokenValidation.IsTokenOk)
                {
                    using (NOOCHEntities obj = new NOOCHEntities())
                    {
                        result.AllUnitsCount = obj.GetUnitsCountForGivenLandlord(Property.LandlorId).SingleOrDefault().ToString();
                        result.AllPropertysCount = obj.GetPropertiesCountForGivenLandlord(Property.LandlorId).SingleOrDefault().ToString();
                        result.AllTenantsCount = obj.GetTenantsCountForGivenLandlord(Property.LandlorId).SingleOrDefault().ToString();

                        result.IsAccountAdded = Convert.ToBoolean(obj.IsBankAccountAddedforGivenLandlordOrTenant("Landlord", Property.LandlorId).SingleOrDefault());
                        result.IsEmailVerified = Convert.ToBoolean(obj.IsEmailVerifiedforGivenLandlordOrTenant("Landlord", Property.LandlorId).SingleOrDefault());
                        result.IsPhoneVerified = Convert.ToBoolean(obj.IsPhoneVerifiedforGivenLandlordOrTenant("Landlord", Property.LandlorId).SingleOrDefault());

                        var landlordObj = (from c in obj.Landlords
                                           where c.LandlordId == landlordguidId
                                           select c).FirstOrDefault();

                        if (landlordObj != null)
                        {
                            result.isIdVerified = (landlordObj.IsIdVerified == true) ? true : false;
                            result.IsAnyRentReceived = (landlordObj.IsAnyRentReceived == true) ? true : false;

                            if (result.isIdVerified != true)
                            {
                                // Now check Landlord's details in MEMBERS Table to confirm ID is NOT verified
                                var memberObj = (from c in obj.Members
                                                 where c.MemberId == landlordObj.MemberId
                                                 select c).FirstOrDefault();

                                if (memberObj != null)
                                {
                                    if (memberObj.IsVerifiedWithSynapse == true)
                                    {
                                        result.isIdVerified = true;
                                    }
                                }
                            }
                        }

                        result.IsSuccess = true;
                        result.ErrorMessage = "OK";
                    }
                }
                else
                {
                    result.ErrorMessage = "Invalid Access token.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("UsersControllers -> GetAccountCompletionStatsOfGivenLandlord FAILED - [LandlordID: " +
                             Property.LandlorId + "], [Exception: " + ex.Message + "]");

                result.ErrorMessage = "Error while getting properties list. Retry later!";
            }

            return result;
        }


        /// <summary>
        /// To resend activation email to a Landlord.
        /// </summary>
        /// <param name="property"></param>
        [HttpPost]
        [ActionName("ResendVerificationEmailAndSMS")]
        public LoginResult ResendVerificationEmailAndSMS(ResendVerificationEmailAndSMSInput property)
        {
            LoginResult result = new LoginResult();
            result.IsSuccess = false;

            try
            {
                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    Guid userGUID = new Guid(property.UserId);
                    switch (property.UserType)
                    {
                        case "Landlord":

                            #region Landlord Related Operations

                            var landlordDetails = (from c in obj.Landlords
                                                   where c.LandlordId == userGUID
                                                   select c).FirstOrDefault();

                            if (landlordDetails != null)
                            {
                                switch (property.RequestFor)
                                {
                                    case "Email":
                                        string s = CommonHelper.ResendVerificationLink(CommonHelper.GetDecryptedData(landlordDetails.eMail));
                                        if (s == "Success")
                                        {
                                            result.IsSuccess = true;
                                            result.ErrorMessage = "OK.";
                                        }
                                        else
                                        {
                                            result.ErrorMessage = s;
                                        }
                                        break;
                                    case "SMS":
                                        string s2 = CommonHelper.ResendVerificationSMS(CommonHelper.GetDecryptedData(landlordDetails.eMail));
                                        if (s2 == "Success")
                                        {
                                            result.IsSuccess = true;
                                            result.ErrorMessage = "OK.";
                                        }
                                        else
                                        {
                                            result.ErrorMessage = s2;
                                        }
                                        break;
                                    default:
                                        result.ErrorMessage = "Invalid data.";
                                        break;
                                }
                            }

                            #endregion Landlord Related Operations

                            break;
                        case "Tenant":
                            #region Tenants Related Operations

                            var tenantDetails =
                                                    (from c in obj.Tenants where c.TenantId == userGUID select c).FirstOrDefault();
                            if (tenantDetails != null)
                            {
                                switch (property.RequestFor)
                                {
                                    case "Email":
                                        string s = CommonHelper.ResendVerificationLink(CommonHelper.GetDecryptedData(tenantDetails.eMail));
                                        if (s == "Success")
                                        {
                                            result.IsSuccess = true;
                                            result.ErrorMessage = "OK.";
                                        }
                                        else
                                        {
                                            result.IsSuccess = false;
                                            result.ErrorMessage = s;
                                        }
                                        break;
                                    case "SMS":
                                        string s2 = CommonHelper.ResendVerificationSMS(CommonHelper.GetDecryptedData(tenantDetails.eMail));
                                        if (s2 == "Success")
                                        {
                                            result.IsSuccess = true;
                                            result.ErrorMessage = "OK.";
                                        }
                                        else
                                        {
                                            result.IsSuccess = false;
                                            result.ErrorMessage = s2;
                                        }
                                        break;
                                    default:
                                        result.IsSuccess = false;
                                        result.ErrorMessage = "Invalid data.";
                                        break;
                                }
                            }

                            #endregion Tenants Related Operations

                            break;
                        default:
                            result.IsSuccess = false;
                            result.ErrorMessage = "Invalid data.";
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("UsersControllers -> ResendVerificationEmailAndSMS FAILED - [UserID: " +
                             property.UserId + " ], [Exception: " + ex.Message + "]");

                result.ErrorMessage = "Server error, retry later!";
            }

            return result;
        }


        [HttpPost]
        [ActionName("GetBankAccountDetails")]
        public SynapseAccoutDetailsInput GetBankAccountDetails(GetProfileDataInput input)
        {
            Logger.Info("Landlord APIs -> UsersController -> GetBankAccountDetails Initiated - [LandlordID: " + input.LandlorId + "]");

            SynapseAccoutDetailsInput res = new SynapseAccoutDetailsInput();
            res.success = false;
            res.msg = "Initial";

            try
            {
                Guid landlordguidId = new Guid(input.LandlorId);
                var checkToken = CommonHelper.AuthTokenValidation(landlordguidId, input.AccessToken);

                if (checkToken.IsTokenOk)
                {
                    // valid access token continue with edit

                    using (NOOCHEntities obj = new NOOCHEntities())
                    {
                        var bankAccnt = (from c in obj.SynapseBanksOfMembers
                                         join d in obj.Landlords on c.MemberId equals d.MemberId
                                         where d.LandlordId == landlordguidId &&
                                               d.IsDeleted == false
                                         select
                                         new
                                         {
                                             c.MemberId,
                                             c.bank_name,
                                             c.nickname,
                                             c.account_number_string,
                                             c.allowed,
                                             c.Status,
                                             c.AddedOn
                                         }
                        ).FirstOrDefault();


                        if (bankAccnt != null)
                        {
                            res.msg = "Bank Found!";

                            string appPath = "https://noochme.com/noochweb/";

                            res.BankName = CommonHelper.GetDecryptedData(bankAccnt.bank_name);
                            res.BankNickname = CommonHelper.GetDecryptedData(bankAccnt.nickname);
                            switch (res.BankName)
                            {
                                case "Ally":
                                    {
                                        res.BankImageURL = String.Concat(appPath, "Assets/Images/bankPictures/ally.png");
                                    }
                                    break;
                                case "Bank of America":
                                    {
                                        res.BankImageURL = String.Concat(appPath, "Assets/Images/bankPictures/bankofamerica.png");
                                    }
                                    break;
                                case "Wells Fargo":
                                    {
                                        res.BankImageURL = String.Concat(appPath, "Assets/Images/bankPictures/WellsFargo.png");
                                    }
                                    break;
                                case "Chase":
                                    {
                                        res.BankImageURL = String.Concat(appPath, "Assets/Images/bankPictures/chase.png");
                                    }
                                    break;
                                case "Citibank":
                                    {
                                        res.BankImageURL = String.Concat(appPath, "Assets/Images/bankPictures/citibank.png");
                                    }
                                    break;
                                case "TD Bank":
                                    {
                                        res.BankImageURL = String.Concat(appPath, "Assets/Images/bankPictures/td.png");
                                    }
                                    break;
                                case "Capital One 360":
                                    {
                                        res.BankImageURL = String.Concat(appPath, "Assets/Images/bankPictures/capone360.png");
                                    }
                                    break;
                                case "US Bank":
                                    {
                                        res.BankImageURL = String.Concat(appPath, "Assets/Images/bankPictures/usbank.png");
                                    }
                                    break;
                                case "PNC":
                                    {
                                        res.BankImageURL = String.Concat(appPath, "Assets/Images/bankPictures/pnc.png");
                                    }
                                    break;
                                case "SunTrust":
                                    {
                                        res.BankImageURL = String.Concat(appPath, "Assets/Images/bankPictures/suntrust.png");
                                    }
                                    break;
                                case "USAA":
                                    {
                                        res.BankImageURL = String.Concat(appPath, "Assets/Images/bankPictures/usaa.png");
                                    }
                                    break;

                                case "First Tennessee":
                                    {
                                        res.BankImageURL = String.Concat(appPath, "Assets/Images/bankPictures/firsttennessee.png");
                                    }
                                    break;
                                default:
                                    {
                                        res.BankImageURL = String.Concat(appPath, "Assets/Images/bankPictures/no.png");
                                    }
                                    break;
                            }
                            res.AccountName = CommonHelper.GetDecryptedData(bankAccnt.account_number_string);
                            res.AccountStatus = bankAccnt.Status;
                            res.allowed = bankAccnt.allowed;
                            res.dateCreated = Convert.ToDateTime(bankAccnt.AddedOn).ToString("d");
                            res.MemberId = bankAccnt.MemberId.ToString();
                            res.msg = "Worked like a charm";
                        }
                        else
                        {
                            res.msg = "No banks found!";
                        }

                        res.success = true;
                    }
                }
                else
                {
                    res.msg = "Trouble with the auth token";
                }
            }
            catch (Exception ex)
            {
                res.msg = "Hit exception";
                Logger.Error("UsersController -> GetSynapseBankAccountDetails FAILED - [LandlorID: " + input.LandlorId + "]. Exception: [" + ex + "]");
            }
            return res;
        }


        [HttpPost]
        [ActionName("DeleteSynapseBankAccount")]
        public GenericInternalResponse deleteSynapseBank(basicLandlordPayload input)
        {
            Logger.Info("UsersController -> deleteSynapseBank Initiated - [LandlordID: " + input.LandlordId + "]");

            GenericInternalResponse res = new GenericInternalResponse();
            res.success = false;
            res.msg = "Initial";

            try
            {
                Guid memGuid = new Guid(input.MemberId);
                var checkToken = CommonHelper.AuthTokenValidation(memGuid, input.AccessToken);

                if (checkToken.IsTokenOk)
                {
                    // valid access token continue with edit

                    using (NOOCHEntities obj = new NOOCHEntities())
                    {
                        var synapseBank = (from c in obj.SynapseBanksOfMembers
                                           where c.MemberId == memGuid &&
                                                 c.IsDefault == true
                                           select c).FirstOrDefault();

                        if (synapseBank != null)
                        {
                            synapseBank.IsDefault = false;
                            obj.SaveChanges();

                            res.msg = "ok";
                            res.success = true;
                        }
                        else
                        {
                            res.msg = "No bank found for that MemberID";
                        }
                    }
                }
                else
                {
                    res.msg = "Trouble with the auth token";
                }
            }
            catch (Exception ex)
            {
                res.msg = "Hit exception";
                Logger.Error("UsersController -> deleteSynapseBank FAILED - [LandlorID: " + input.LandlordId + "]. Exception: [" + ex + "]");
            }
            return res;
        }


        /// <summary>
        /// To send custom emails to tenants from a landlord.
        /// </summary>
        /// <param name="User"></param>
        [HttpPost]
        [ActionName("SendEmailsToTenants")]
        public CreatePropertyResultOutput SendEmailsToTenants(SendEmailsToTenantsInputClass User)
        {
            CreatePropertyResultOutput result = new CreatePropertyResultOutput();
            result.IsSuccess = false;

            try
            {
                Guid landlordguidId = new Guid(User.DeviceInfo.LandlorId);
                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordguidId, User.DeviceInfo.AccessToken);

                if (result.AuthTokenValidation.IsTokenOk)
                {
                    // valid access token continue with edit

                    using (NOOCHEntities obj = new NOOCHEntities())
                    {
                        // Reading landlord details from DB
                        var lanlorddetails = (from c in obj.Landlords
                                              where c.LandlordId == landlordguidId
                                              select c).FirstOrDefault();

                        if (lanlorddetails != null)
                        {
                            // Now check if the email is going to just 1 tenant, or all a Property's tenants

                            if (User.EmailInfo.IsForAllOrOne == "One")
                            {
                                #region Sending To A Single Tenant

                                try
                                {
                                    if (!String.IsNullOrEmpty(User.EmailInfo.TenantIdToBeMessaged))
                                    {
                                        Guid tenantguidId = new Guid(User.DeviceInfo.LandlorId);

                                        // Get tenant info

                                        var tenanInfo = (from c in obj.Tenants
                                                         where c.TenantId == tenantguidId
                                                         select c).FirstOrDefault();

                                        if (tenanInfo != null)
                                        {
                                            string emailtobesentto = CommonHelper.GetDecryptedData(tenanInfo.eMail);
                                            string emailtobesentfrom = CommonHelper.GetDecryptedData(lanlorddetails.eMail);

                                            string bodytext = User.EmailInfo.MessageToBeSent;

                                            CommonHelper.SendEmail(null, emailtobesentfrom, emailtobesentto,
                                                "New message from " +
                                                CommonHelper.GetDecryptedData(lanlorddetails.FirstName), null, bodytext);

                                            result.IsSuccess = true;
                                            result.ErrorMessage = "Message sent.";
                                        }
                                        else
                                        {
                                            result.ErrorMessage = "Given tenant not found.";
                                        }
                                    }
                                    else
                                    {
                                        result.ErrorMessage = "Invalid input data.";
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error("UsersControllers -> SendEmailsToTenants (to single tenant) FAILED - [Exception: " + ex.Message + "]");
                                }

                                #endregion Sending To A Single Tenant
                            }

                            else if (User.EmailInfo.IsForAllOrOne == "All")
                            {
                                #region Sending To All Tenants For A Given Property

                                try
                                {
                                    // getting all tenants of given landlord
                                    string propId = User.EmailInfo.PropertyId;

                                    List<GetTenantsInGivenPropertyId_Result1> allTenantsOfLl = obj.GetTenantsInGivenPropertyId(propId).ToList();

                                    if (allTenantsOfLl.Count > 0)
                                    {
                                        string emailtobesentfrom = CommonHelper.GetDecryptedData(lanlorddetails.eMail);

                                        foreach (GetTenantsInGivenPropertyId_Result1 ten in allTenantsOfLl)
                                        {
                                            string emailtobesentto = CommonHelper.GetDecryptedData(ten.TenantEmail);
                                            string bodytext = User.EmailInfo.MessageToBeSent;

                                            CommonHelper.SendEmail(null, emailtobesentfrom, emailtobesentto,
                                                "New message from " +
                                               CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(lanlorddetails.FirstName)) + " " +
                                               CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(lanlorddetails.LastName)), null, bodytext);
                                        }

                                        result.IsSuccess = true;
                                        result.ErrorMessage = "Messages sent.";
                                    }
                                    else
                                    {
                                        result.ErrorMessage = "No tenant found for given landlord and property.";
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error("UsersControllers -> SendEmailsToTenants (to ALL tenants) FAILED - [Exception: " + ex.Message + "]");
                                }

                                #endregion Sending To All Tenants For A Given Property
                            }
                            else
                            {
                                result.ErrorMessage = "Invalid input data.";
                            }
                        }
                        else
                        {
                            result.ErrorMessage = "Given landlord doesn't exists in system.";
                        }
                    }
                }
                else
                {
                    result.ErrorMessage = result.AuthTokenValidation.ErrorMessage;
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.Error("UsersControllers -> SendEmailsToTenants FAILED - [Outer Exception: " + ex.Message + "]");
                result.ErrorMessage = "Server error";
                return result;
            }
        }


        // to change password for given user
        [HttpPost]
        [ActionName("ChangeUserPassword")]
        public CreatePropertyResultOutput ChangeUserPassword(EditPersonalInfoInputClass User)
        {
            CreatePropertyResultOutput result = new CreatePropertyResultOutput();
            result.IsSuccess = false;

            try
            {
                Guid landlordguidId = new Guid(User.DeviceInfo.LandlorId);
                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordguidId, User.DeviceInfo.AccessToken);

                if (result.AuthTokenValidation.IsTokenOk)
                {
                    // valid access token continue with edit

                    using (NOOCHEntities obj = new NOOCHEntities())
                    {
                        //reading details from db
                        var lanlordObj = (from c in obj.Landlords
                                          where c.LandlordId == landlordguidId
                                          select c).FirstOrDefault();

                        if (lanlordObj != null)
                        {
                            if (String.IsNullOrEmpty(User.UserInfo.NewPassword))
                            {
                                result.IsSuccess = false;
                                result.ErrorMessage = "Invalid password.";    
                            }
                            else
                            {
                                //getting member from members table
                                var memberDetails = (from c in obj.Members  where c.MemberId==lanlordObj.MemberId select c).FirstOrDefault();
                                if (memberDetails!=null)
                                {
                                    memberDetails.Password = CommonHelper.GetEncryptedData(User.UserInfo.NewPassword);
                                    memberDetails.DateModified = DateTime.Now;
                                    obj.SaveChanges();
                                    result.IsSuccess = true;
                                    result.ErrorMessage = "Password changed successfully.";    
                                }
                                else
                                {
                                    result.IsSuccess = false;
                                    result.ErrorMessage = "Invalid user id.";    
                                }
                                
                            }
                        }
                        else
                        {
                            result.ErrorMessage = "Given landlord ID not found.";
                        }
                    }
                }
                else
                {
                    result.ErrorMessage = result.AuthTokenValidation.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Landlords API -> UsersController -> EditUserInfo FAILED - [Outer Exception: " + ex.ToString() + "]");
                result.ErrorMessage = "Server error";
            }

            return result;
        }

    }
}
