using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
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

                                    for (int i = 1; i < nameAftetSplit.Length; i++)
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
                                    lanlorddetails.LastName = CommonHelper.GetEncryptedData(lastName.Trim());
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

                                if (String.IsNullOrEmpty(User.UserInfo.MobileNumber))
                                {
                                    if (lanlorddetails.MobileNumber.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", "") != User.UserInfo.MobileNumber.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", ""))
                                    {
                                        lanlorddetails.MobileNumber = User.UserInfo.MobileNumber.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", "");

                                    }

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
                result.IsSuccess = false;
                result.ErrorMessage = "Server error";
                return result;

            }
        }



        [HttpPost]
        [ActionName("RegisterLandlord")]
        public RegisterLandlordResult RegisterLandlord(RegisterLandlordInput llDetails)
        {
            RegisterLandlordResult result = new RegisterLandlordResult();

            try
            {
                // checking if lanlord exists for given account
                CheckAndRegisterLandlordByEmailResult ll = CommonHelper.checkAndRegisterLandlordByemailId(llDetails.eMail);

                if (ll.IsSuccess && ll.ErrorMessage == "No user found.")
                {
                    // checking if member exists with given email id
                    CheckAndRegisterMemberByEmailResult mem =
                        CommonHelper.CheckIfMemberExistsWithGivenEmailId(llDetails.eMail);

                    if (mem.IsSuccess && mem.ErrorMessage == "No user found.")
                    {
                        #region New Member details and landlord details being saved in db here.
                        // need to make new entry in member table first
                        var userNameLowerCase = llDetails.eMail.Trim().ToLower();
                        string noochRandomId = CommonHelper.GetRandomNoochId();
                        if (noochRandomId == null)
                        {
                            result.IsSuccess = false;
                            result.ErrorMessage = "Some duplicate values are being generated at server. Retry later! ";
                            return result;

                        }

                        #region region
                        using (NOOCHEntities obj = new NOOCHEntities())
                        {
                            #region Member object
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
                                Type = "Personal",

                                Address = CommonHelper.GetEncryptedData(" "),   // some blanks as default
                                State = CommonHelper.GetEncryptedData(" "),
                                City = CommonHelper.GetEncryptedData(" "),
                                Zipcode = CommonHelper.GetEncryptedData(" "),
                                ContactNumber = CommonHelper.GetEncryptedData(" ")

                            };
                            #endregion

                            obj.Members.Add(member);
                            try
                            {

                                obj.SaveChanges();

                                CommonHelper.setReferralCode(member.MemberId);
                                var tokenId = Guid.NewGuid();
                                #region Verification email

                                //send registration email to member with autogenerated token 
                                var link = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"),
                                    "/Registration/Activation.aspx?tokenId=" + tokenId);
                                var fromAddress = CommonHelper.GetValueFromConfig("welcomeMail");
                                // Add any tokens you want to find/replace within your template file
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

                                    Logger.Info("MemberDataAccess - Registration mail sent to [" + llDetails.eMail.Trim() +
                                                           "].");
                                }
                                catch (Exception)
                                {
                                    // to revert the member record when mail is not sent successfully.
                                    Logger.Error("MemberDataAccess - Member activation mail NOT sent to [" +
                                                           llDetails.eMail.Trim() + "].");
                                }

                                #endregion


                                #region PinNumber email

                                // emailing temp pin number
                                var tokens2 = new Dictionary<string, string>
                            {
                                {
                                    Constants.PLACEHOLDER_FIRST_NAME,
                                    CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(member.FirstName))
                                },
                                {Constants.PLACEHOLDER_PINNUMBER, randomPin}
                            };
                                try
                                {
                                    //CommonHelper.SendEmail("pinSetForNewUser", MailPriority.High,
                                    //    fromAddress, NewUserEmail, null,
                                    //    "Your temporary Nooch PIN", null,
                                    //    tokens2, null, null, null);


                                    CommonHelper.SendEmail("pinSetForNewUser",
                                        fromAddress, llDetails.eMail.Trim(),
                                        "Your temporary Nooch PIN",
                                        tokens2, null);


                                }
                                catch (Exception)
                                {
                                    Logger.Error("MemberDataAccess - Member temp pin mail not sent to [" +
                                                           userNameLowerCase + "].");
                                }

                                #endregion


                                #region AuthenticationToken

                                var requestId = Guid.Empty;
                                // save the token details into authentication tokens table  
                                var token = new AuthenticationToken
                                {
                                    TokenId = tokenId,
                                    MemberId = member.MemberId,
                                    IsActivated = false,
                                    DateGenerated = DateTime.Now,
                                    FriendRequestId = requestId
                                };
                                obj.AuthenticationTokens.Add(token);
                                obj.SaveChanges();


                                #endregion


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

                                #endregion

                                #region Privacy Settings


                                var memberPrivacySettings = new MemberPrivacySetting
                                {
                                    MemberId = member.MemberId,

                                    AllowSharing = true,
                                    ShowInSearch = true,
                                    DateCreated = DateTime.Now
                                };
                                obj.MemberPrivacySettings.Add(memberPrivacySettings);

                                #endregion


                                // finally making an entry in 
                                Landlord l = CommonHelper.AddNewLandlordEntryInDb(llDetails.FirstName,
                                    llDetails.LastName, llDetails.eMail, llDetails.Password, false, false,
                                    member.MemberId);

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

                            }
                            catch (Exception)
                            {

                                result.IsSuccess = false;
                                result.ErrorMessage = "Some duplicate values are being generated at server. Retry later! ";
                                return result;
                            }


                        }
                        #endregion 
                        #endregion
                        

                    }
                    else
                    {


                        

                        


                        // mem already exists
                        #region new landlord but existing member
                        // finally making an entry in 
                        Landlord l = CommonHelper.AddNewLandlordEntryInDb(llDetails.FirstName,
                            llDetails.LastName, llDetails.eMail, llDetails.Password, true, true,
                            mem.MemberDetails.MemberId);

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
                        #endregion

                    }


                }
                else
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = ll.ErrorMessage;
                    return result;
                }
            }
            catch (Exception ec)
            {
                Logger.Error("RegisterLandlord error while making account for " + llDetails.eMail);
                result.IsSuccess = false;
                result.ErrorMessage = "Server Error.";
                return result;

            }
        }

    }
}
