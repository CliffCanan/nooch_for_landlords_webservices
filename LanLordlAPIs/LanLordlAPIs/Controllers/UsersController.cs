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


                    if (String.IsNullOrEmpty(User.Ip) || String.IsNullOrEmpty(User.UserName) ||
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
                                           where d.UserName == userNameEncrypted && d.Password == passEncrypted
                                                 && d.IsDeleted == false && c.IsDeleted == false && c.Status == "Active"
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
                        var landlordEntity =
                            (from ll in obj.Landlords where ll.LandlordId == userCheckResult.LandlordId select ll)
                                .FirstOrDefault();

                        CommonHelper.saveLandlordIp(userCheckResult.LandlordId, User.Ip);
                        landlordEntity.DateModified = requestDatetime;
                        landlordEntity.LastSeenOn = requestDatetime;

                        landlordEntity.WebAccessToken = CommonHelper.GenerateAccessToken();

                        obj.SaveChanges();


                        result.IsSuccess = true;
                        result.ErrorMessage = "OK";
                        result.AccessToken = landlordEntity.WebAccessToken;
                        result.MemberId = landlordEntity.LandlordId.ToString();

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

        // to get user homepage information

        [HttpPost]
        [ActionName("GetUserInfo")]
        public UserProfileInfoResult GetUserInfo(GetProfileDataInput User)
        {

            UserProfileInfoResult result = new UserProfileInfoResult();
            try
            {
                Logger.Info("Landlords API -> Users -> GetUserInfo. GetUserInfo requested by [" + User.LandlorId + "]");
                Guid landlordguidId = new Guid(User.LandlorId);
                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordguidId, User.AccessToken);



                if (result.AuthTokenValidation.IsTokenOk)
                {


                    using (NOOCHEntities obj = new NOOCHEntities())
                    {
                        //reading details from db
                        var lanlorddetails =
                       (from c in obj.Landlords

                        where c.LandlordId == landlordguidId
                        select
                            c
                    ).FirstOrDefault();

                        if (lanlorddetails != null)
                        {

                            result.FirstName = !String.IsNullOrEmpty(lanlorddetails.FirstName) ? CommonHelper.GetDecryptedData(lanlorddetails.FirstName) : "";
                            result.LastName = !String.IsNullOrEmpty(lanlorddetails.LastName) ? CommonHelper.GetDecryptedData(lanlorddetails.LastName) : "";
                            result.AccountType = !String.IsNullOrEmpty(lanlorddetails.Type) ? lanlorddetails.Type : "";
                            result.SubType = !String.IsNullOrEmpty(lanlorddetails.SubType) ? lanlorddetails.SubType : "";

                            result.IsPhoneVerified = lanlorddetails.IsPhoneVerified != null;
                            result.IsEmailVerified = lanlorddetails.IsEmailVerfieid != null;

                            result.DOB = lanlorddetails.DateOfBirth != null ? Convert.ToDateTime(lanlorddetails.DateOfBirth).ToString("d") : "";


                            result.SSN = !String.IsNullOrEmpty(lanlorddetails.SSN) ? CommonHelper.GetDecryptedData(lanlorddetails.SSN) : "";
                            result.UserEmail = !String.IsNullOrEmpty(lanlorddetails.eMail) ? CommonHelper.GetDecryptedData(lanlorddetails.eMail) : "";

                            result.MobileNumber = !String.IsNullOrEmpty(lanlorddetails.MobileNumber) ? CommonHelper.FormatPhoneNumber(lanlorddetails.MobileNumber) : "";

                            if (!String.IsNullOrEmpty(lanlorddetails.AddressLineOne))
                            {
                                result.Address = CommonHelper.GetDecryptedData(lanlorddetails.AddressLineOne);
                                result.AddressLine1 = CommonHelper.GetDecryptedData(lanlorddetails.AddressLineOne);
                            }
                            else
                            {
                                result.AddressLine1 = "";
                            }

                            if (!String.IsNullOrEmpty(lanlorddetails.AddressLineTwo))
                            {
                                result.Address += " " + CommonHelper.GetDecryptedData(lanlorddetails.AddressLineTwo);
                                result.AddressLine2 = CommonHelper.GetDecryptedData(lanlorddetails.AddressLineTwo);
                            }
                            else
                            {
                                result.AddressLine2 = "";
                            }
                            if (!String.IsNullOrEmpty(lanlorddetails.City))
                            {
                                result.Address += " " + CommonHelper.GetDecryptedData(lanlorddetails.City);
                                result.City = CommonHelper.GetDecryptedData(lanlorddetails.City);
                            }
                            else
                            {
                                result.City = "";
                            }

                            if (!String.IsNullOrEmpty(lanlorddetails.State))
                            {
                                result.Address += " " + CommonHelper.GetDecryptedData(lanlorddetails.State);
                                result.AddState = CommonHelper.GetDecryptedData(lanlorddetails.State);
                            }
                            else
                            {
                                result.AddState = "";
                            }
                            if (!String.IsNullOrEmpty(lanlorddetails.Zip))
                            {
                                result.Address += " " + CommonHelper.GetDecryptedData(lanlorddetails.Zip);
                                result.Zip = CommonHelper.GetDecryptedData(lanlorddetails.Zip);
                            }
                            else
                            {
                                result.Zip = "";
                            }
                            if (!String.IsNullOrEmpty(lanlorddetails.Country))
                            {
                                result.Address += " " + CommonHelper.GetDecryptedData(lanlorddetails.Country);
                                result.Country = CommonHelper.GetDecryptedData(lanlorddetails.Country);
                            }
                            else
                            {
                                result.Country = "";
                            }
                            result.TwitterHandle = !String.IsNullOrEmpty(lanlorddetails.TwitterHandle) ? lanlorddetails.TwitterHandle : "";
                            result.FbUrl = !String.IsNullOrEmpty(lanlorddetails.FBId) ? lanlorddetails.FBId : "";


                            result.InstaUrl = !String.IsNullOrEmpty(lanlorddetails.InstagramUrl) ? lanlorddetails.InstagramUrl : "";

                            result.CompanyName = !String.IsNullOrEmpty(lanlorddetails.CompanyName) ? CommonHelper.GetDecryptedData(lanlorddetails.CompanyName) : "";


                            result.CompanyEID = !String.IsNullOrEmpty(lanlorddetails.CompanyEIN) ? CommonHelper.GetDecryptedData(lanlorddetails.CompanyEIN) : "";
                            result.IsSuccess = true;
                            result.ErrorMessage = "OK";
                        }







                        return result;
                    }
                }
                else
                {
                    result.IsSuccess = true;
                    result.ErrorMessage = "OK";
                    return result;
                }
            }

            catch (Exception ex)
            {
                Logger.Error("Landlords API -> Users -> GetUserInfo. Error while GetUserInfo request from LandlorgId  - [ " + User.LandlorId + " ] . Exception details [ " + ex + " ]");
                result.IsSuccess = false;
                result.ErrorMessage = "Error while logging on. Retry.";
                return result;

            }


        }

        [HttpPost]
        [ActionName("EditUserInfo")]
        public CreatePropertyResultOutput EditUserInfo(EditPersonalInfoInputClass User)
        {
            CreatePropertyResultOutput result = new CreatePropertyResultOutput();
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
                        var lanlorddetails =
                       (from c in obj.Landlords

                        where c.LandlordId == landlordguidId
                        select
                            c
                    ).FirstOrDefault();

                        if (lanlorddetails != null)
                        {
                            if (User.UserInfo.InfoType == "Personal")
                            {
                                #region Editing Personal Info
                                if (String.IsNullOrEmpty(User.UserInfo.FullName))
                                {
                                    result.IsSuccess = false;
                                    result.ErrorMessage = "Name missing.";
                                    return result;
                                }

                                string firstName = "", lastName = "";
                                string[] nameAftetSplit = User.UserInfo.FullName.Trim().ToLower().Split(' ');

                                if (nameAftetSplit.Length > 1)
                                {
                                    firstName = nameAftetSplit[0];

                                    for (int i = 1; i < nameAftetSplit.Length - 1; i++)
                                    {
                                        lastName += nameAftetSplit[i] + " ";
                                    }

                                    //if (String.IsNullOrEmpty(User.UserInfo.MobileNumber))
                                    //{
                                    //    result.IsSuccess = false;
                                    //    result.ErrorMessage = "Contact number missing.";
                                    //    return result;
                                    //}

                                    if (String.IsNullOrEmpty(User.UserInfo.DOB))
                                    {
                                        result.IsSuccess = false;
                                        result.ErrorMessage = "Date of birth missing.";
                                        return result;
                                    }

                                    if (String.IsNullOrEmpty(User.UserInfo.SSN))
                                    {
                                        result.IsSuccess = false;
                                        result.ErrorMessage = "SSN number missing.";
                                        return result;
                                    }

                                    // have everything now.....going to store in db
                                    lanlorddetails.FirstName = CommonHelper.GetEncryptedData(firstName);
                                    lanlorddetails.LastName = CommonHelper.GetEncryptedData(lastName);
                                    lanlorddetails.DateOfBirth = Convert.ToDateTime(User.UserInfo.DOB);
                                    lanlorddetails.DateModified = DateTime.Now;
                                    lanlorddetails.SSN = CommonHelper.GetEncryptedData(User.UserInfo.SSN);

                                    obj.SaveChanges();


                                    result.IsSuccess = true;
                                    result.ErrorMessage = "OK";


                                }
                                else
                                {
                                    result.IsSuccess = false;
                                    result.ErrorMessage = "Last or First Name missing.";
                                    return result;
                                }
                                #endregion
                            }
                            if (User.UserInfo.InfoType == "Company")
                            {
                                #region Editing Personal Info
                                if (String.IsNullOrEmpty(User.UserInfo.CompanyName))
                                {
                                    result.IsSuccess = false;
                                    result.ErrorMessage = "Company name missing.";
                                    return result;
                                }
                                if (String.IsNullOrEmpty(User.UserInfo.CompanyEID))
                                {
                                    result.IsSuccess = false;
                                    result.ErrorMessage = "Company EIN missing.";
                                    return result;
                                }

                                // have everything now.....going to store in db
                                lanlorddetails.CompanyName = CommonHelper.GetEncryptedData(User.UserInfo.CompanyName);
                                lanlorddetails.CompanyEIN = CommonHelper.GetEncryptedData(User.UserInfo.CompanyEID);
                                lanlorddetails.DateModified = DateTime.Now;


                                obj.SaveChanges();


                                result.IsSuccess = true;
                                result.ErrorMessage = "OK";


                                #endregion
                            }
                            if (User.UserInfo.InfoType == "Contact")
                            {
                                #region Editing Personal Info
                                if (String.IsNullOrEmpty(User.UserInfo.UserEmail))
                                {
                                    result.IsSuccess = false;
                                    result.ErrorMessage = "eMail missing.";
                                    return result;
                                }

                                if (String.IsNullOrEmpty(User.UserInfo.MobileNumber))
                                {
                                    result.IsSuccess = false;
                                    result.ErrorMessage = "Contact number missing.";
                                    return result;
                                }

                                if (String.IsNullOrEmpty(User.UserInfo.AddressLine1))
                                {
                                    result.IsSuccess = false;
                                    result.ErrorMessage = "Address missing.";
                                    return result;
                                }



                                string userEmailNew =
                                    CommonHelper.GetEncryptedData(User.UserInfo.UserEmail.ToLower().Trim());
                                // checking if given email is already registered or not
                                                var lanlorddetailsbyEmail =
                                     (from c in obj.Landlords

                                      where c.eMail == userEmailNew && c.LandlordId != landlordguidId
                                      select
                                          c
                                  ).FirstOrDefault();

                                if (lanlorddetailsbyEmail != null)
                                {
                                    result.IsSuccess = false;
                                    result.ErrorMessage = "User with given email already exists.";
                                    return result;
                                }


                                // have everything now.....going to store in db
                                lanlorddetails.DateModified = DateTime.Now;

                                if (lanlorddetails.eMail != userEmailNew)
                                {
                                    lanlorddetails.IsEmailVerfieid = false;
                                    lanlorddetails.eMail = userEmailNew;
                                }

                                if (!String.IsNullOrEmpty(User.UserInfo.TwitterHandle))
                                {
                                    lanlorddetails.TwitterHandle = User.UserInfo.TwitterHandle;
                                }

                                lanlorddetails.AddressLineOne = CommonHelper.GetEncryptedData(User.UserInfo.AddressLine1);


                                obj.SaveChanges();


                                result.IsSuccess = true;
                                result.ErrorMessage = "OK";


                                #endregion
                            }

                            if (User.UserInfo.InfoType == "Social")
                            {
                                #region Editing Personal Info
                               

                                // have everything now.....going to store in db
                                lanlorddetails.DateModified = DateTime.Now;

                                

                                if (!String.IsNullOrEmpty(User.UserInfo.TwitterHandle))
                                {
                                    lanlorddetails.TwitterHandle = User.UserInfo.TwitterHandle;
                                }
                                if (!String.IsNullOrEmpty(User.UserInfo.FbUrl))
                                {
                                    lanlorddetails.FBId = User.UserInfo.FbUrl;
                                }
                                if (!String.IsNullOrEmpty(User.UserInfo.InstaUrl))
                                {
                                    lanlorddetails.InstagramUrl = User.UserInfo.InstaUrl;
                                }

                                obj.SaveChanges();


                                result.IsSuccess = true;
                                result.ErrorMessage = "OK";


                                #endregion
                            }
                        }
                        else
                        {
                            result.IsSuccess = false;
                            result.ErrorMessage = "Given landlor doesn't exists in system.";
                        }
                    }
                }
                else
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = result.AuthTokenValidation.ErrorMessage;
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.Info("Landlords API -> Users -> EditUserInfo. EditUserInfo exception[" + ex.ToString() + "]");
                return result;

            }
        }
    }
}
