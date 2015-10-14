using System;
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
                                           where d.UserName == userNameEncrypted && d.Password == passEncrypted
                                                 && d.IsDeleted == false && c.IsDeleted == false && (c.Status != "Suspended" || c.Status != "Temporarily_Blocked")
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
            Logger.Info("Landlords API -> Users -> GetUserInfo. GetUserInfo requested by [" + User.LandlorId + "]");

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
                        // Reading Landlord's details from DB
                        var landlordObj = (from c in obj.Landlords
                                              where c.LandlordId == landlordguidId
                                              select c).FirstOrDefault();

                        if (landlordObj != null)
                        {
                            res.FirstName = !String.IsNullOrEmpty(landlordObj.FirstName) ? CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(landlordObj.FirstName)) : "";
                            res.LastName = !String.IsNullOrEmpty(landlordObj.LastName) ? CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(landlordObj.LastName)) : "";
                            res.AccountType = !String.IsNullOrEmpty(landlordObj.Type) ? landlordObj.Type : "";
                            res.SubType = !String.IsNullOrEmpty(landlordObj.SubType) ? landlordObj.SubType : "";

                            res.IsPhoneVerified = landlordObj.IsPhoneVerified != null;
                            res.IsEmailVerified = landlordObj.IsEmailVerfieid != null;

                            res.DOB = landlordObj.DateOfBirth != null ? Convert.ToDateTime(landlordObj.DateOfBirth).ToString("d") : "";
                            res.SSN = !String.IsNullOrEmpty(landlordObj.SSN) ? CommonHelper.GetDecryptedData(landlordObj.SSN) : "";
                            res.isIdVerified = landlordObj.IsIdVerified ?? false;

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
                        var lanlorddetails = (from c in obj.Landlords
                                              where c.LandlordId == landlordguidId
                                              select c).FirstOrDefault();

                        if (lanlorddetails != null)
                        {
                            if (User.UserInfo.InfoType == "Personal")
                            {
                                #region Editing Personal Info

                                if (String.IsNullOrEmpty(User.UserInfo.FullName))
                                {
                                    result.ErrorMessage = "Name missing.";
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
                                    lanlorddetails.FirstName = CommonHelper.GetEncryptedData(firstName.Trim());
                                    lanlorddetails.LastName = CommonHelper.GetEncryptedData(lastName.Trim());
                                    if (!String.IsNullOrEmpty(User.UserInfo.DOB))
                                    {
                                        lanlorddetails.DateOfBirth = Convert.ToDateTime(User.UserInfo.DOB);
                                    }
                                    if (!String.IsNullOrEmpty(User.UserInfo.SSN))
                                    {
                                        lanlorddetails.SSN = CommonHelper.GetEncryptedData(User.UserInfo.SSN);
                                    }
                                    lanlorddetails.DateModified = DateTime.Now;

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
                                lanlorddetails.CompanyName = CommonHelper.GetEncryptedData(CommonHelper.UppercaseFirst(User.UserInfo.CompanyName));
                                if (!String.IsNullOrEmpty(User.UserInfo.CompanyEID))
                                {
                                    lanlorddetails.CompanyEIN = CommonHelper.GetEncryptedData(User.UserInfo.CompanyEID);
                                }
                                lanlorddetails.DateModified = DateTime.Now;

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
                                lanlorddetails.DateModified = DateTime.Now;

                                if (lanlorddetails.eMail != userEmailNew)
                                {
                                    lanlorddetails.IsEmailVerfieid = false;
                                    lanlorddetails.eMail = userEmailNew;
                                }

                                if (String.IsNullOrEmpty(User.UserInfo.MobileNumber))
                                {
                                    if (CommonHelper.RemovePhoneNumberFormatting(lanlorddetails.MobileNumber) != CommonHelper.RemovePhoneNumberFormatting(User.UserInfo.MobileNumber))
                                    {
                                        lanlorddetails.MobileNumber = CommonHelper.RemovePhoneNumberFormatting(User.UserInfo.MobileNumber);
                                    }
                                }

                                lanlorddetails.AddressLineOne = CommonHelper.GetEncryptedData(User.UserInfo.AddressLine1);

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
                Logger.Info("Landlords API -> UsersController -> EditUserInfo FAILED - [Outer Exception: " + ex.ToString() + "]");
                result.ErrorMessage = "Server error";
            }

            return result;
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

                        // need to make new entry in member table first
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
                                Type = "Personal",

                                Address = CommonHelper.GetEncryptedData(" "),   // some blanks as default
                                State = CommonHelper.GetEncryptedData(" "),
                                City = CommonHelper.GetEncryptedData(" "),
                                Zipcode = CommonHelper.GetEncryptedData(" "),
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
                                                           llDetails.eMail.Trim() + "]");
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

                                // Finally, make an entry in DB 
                                Landlord l = CommonHelper.AddNewLandlordEntryInDb(llDetails.FirstName,
                                    llDetails.LastName, llDetails.eMail, llDetails.Password, false, false,
                                    member.MemberId);

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
                Logger.Error("UsersController -> RegisterLandlord FAILED - Outer Exception - [Email: " + llDetails.eMail + "], [Exception: " + ex.Message + "]");
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
                Logger.Error("UsersController -> ResetPassword FAILED - [UserName: " + userName + "], [Exception: " + ex.Message + "]");

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
            Logger.Info("UsersController -> GetAccountCompletetionStatsOfGivenLandlord Initiated -[LandlordID: " +
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
                            result.IsIDVerified = landlordObj.IsIdVerified ?? false;
                            result.IsAnyRentReceived = landlordObj.IsAnyRentReceived ?? false;
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
    }
}
