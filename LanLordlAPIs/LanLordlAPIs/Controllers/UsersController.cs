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
                            
                            if (!String.IsNullOrEmpty( lanlorddetails.FirstName))
                            {
                                result.FirstName = CommonHelper.GetDecryptedData(lanlorddetails.FirstName);
                            }
                            if (!String.IsNullOrEmpty(lanlorddetails.LastName))
                            {
                                result.FirstName = CommonHelper.GetDecryptedData(lanlorddetails.LastName);
                            }

                            if (!String.IsNullOrEmpty(lanlorddetails.Type))
                            {
                                result.AccountType = lanlorddetails.Type;
                            }

                            result.IsPhoneVerified = lanlorddetails.IsPhoneVerified != null;
                            result.IsEmailVerified = lanlorddetails.IsEmailVerfieid != null;

                            if (lanlorddetails.DateOfBirth!=null)
                            {
                                result.DOB = Convert.ToDateTime(lanlorddetails.DateOfBirth).ToString("YYYY MMMM DD");
                            }

                            if (!String.IsNullOrEmpty(lanlorddetails.SSN))
                            {
                                result.SSN = CommonHelper.GetDecryptedData(lanlorddetails.SSN);
                            }
                            if (!String.IsNullOrEmpty(lanlorddetails.eMail))
                            {
                                result.UserEmail = CommonHelper.GetDecryptedData(lanlorddetails.eMail);
                            }

                            if (!String.IsNullOrEmpty(lanlorddetails.MobileNumber))
                            {
                                result.MobileNumber = CommonHelper.FormatPhoneNumber(lanlorddetails.MobileNumber);
                            }

                            if (!String.IsNullOrEmpty(lanlorddetails.AddressLineOne))
                            {
                                result.Address = CommonHelper.GetDecryptedData(lanlorddetails.AddressLineOne);
                            }
                            if (!String.IsNullOrEmpty(lanlorddetails.AddressLineTwo))
                            {
                                result.Address += " "+CommonHelper.GetDecryptedData(lanlorddetails.AddressLineOne);
                            }
                            if (!String.IsNullOrEmpty(lanlorddetails.City))
                            {
                                result.Address += " " + CommonHelper.GetDecryptedData(lanlorddetails.City);
                            }

                            if (!String.IsNullOrEmpty(lanlorddetails.State))
                            {
                                result.Address += " " + CommonHelper.GetDecryptedData(lanlorddetails.State);
                            }

                            if (!String.IsNullOrEmpty(lanlorddetails.Zip))
                            {
                                result.Address += " " + CommonHelper.GetDecryptedData(lanlorddetails.Zip);
                            }
                            if (!String.IsNullOrEmpty(lanlorddetails.Country))
                            {
                                result.Address += " " + CommonHelper.GetDecryptedData(lanlorddetails.Country);
                            }

                            if (!String.IsNullOrEmpty(lanlorddetails.TwitterHandle))
                            {
                                result.TwitterHandle = CommonHelper.GetDecryptedData(lanlorddetails.TwitterHandle);
                            }
                            if (!String.IsNullOrEmpty(lanlorddetails.FBId))
                            {
                                result.FbUrl = lanlorddetails.FBId;
                            }
                            if (!String.IsNullOrEmpty(lanlorddetails.FBId))
                            {
                                result.FbUrl = lanlorddetails.FBId;
                            }

                            if (!String.IsNullOrEmpty(lanlorddetails.InstagramUrl))
                            {
                                result.InstaUrl = lanlorddetails.InstagramUrl;
                            }

                            if (!String.IsNullOrEmpty(lanlorddetails.CompanyName))
                            {
                                result.CompanyName = CommonHelper.GetDecryptedData(lanlorddetails.CompanyName);
                            }


                            if (!String.IsNullOrEmpty(lanlorddetails.CompanyEIN))
                            {
                                result.CompanyEID =CommonHelper.GetDecryptedData( lanlorddetails.CompanyEIN);
                            }
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
                Logger.Error("Landlords API -> Users -> GetUserInfo. Error while GetUserInfo request from LandlorgId  - [ " + User.LandlorId+ " ] . Exception details [ " + ex + " ]");
                result.IsSuccess = false;
                result.ErrorMessage = "Error while logging on. Retry.";
                return result;

            }


        }


    }
}
