using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
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
        [ActionName("RegisterLandlord")]
        public RegisterLandlordResult RegisterLandlord(RegisterLandlordInput llDetails)
        {
            Logger.Info("UserController -> RegisterLandlord - ** NEW LANDLORD ** - [Name: " + llDetails.FirstName + " " + llDetails.LastName + "], " +
                        "[Email: " + llDetails.eMail + "], [Fingerprint: " + llDetails.fingerprint + "], [IP: " + llDetails.ip + "], [Country: " + llDetails.country + "]");

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
                                UDID1 = !String.IsNullOrEmpty(llDetails.fingerprint) ? llDetails.fingerprint : null,
                                Country = !String.IsNullOrEmpty(llDetails.country) ? llDetails.country : "US",

                                // some blanks as default
                                Address = CommonHelper.GetEncryptedData(""),
                                State = CommonHelper.GetEncryptedData(""),
                                City = CommonHelper.GetEncryptedData(""),
                                Zipcode = CommonHelper.GetEncryptedData(""),
                            };

                            #endregion Create Member Object

                            obj.Members.Add(member);

                            Logger.Info("UserController -> RegisterLandlord - ** NEW LANDLORD ** - MEMBER Created, about to save to DB - [MemberID: " + member.MemberId + "]");
                            try
                            {
                                obj.SaveChanges();

                                CommonHelper.setReferralCode(member.MemberId);
                                var tokenId = Guid.NewGuid();

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
                                int authTokenAddedToDB = obj.SaveChanges();

                                Logger.Info("UserController -> RegisterLandlord - ** NEW LANDLORD ** - AUTH TOKEN Created and saved to DB - [MemberID: " + member.MemberId + "]");

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

                                Logger.Info("UserController -> RegisterLandlord - ** NEW LANDLORD ** - NOTIFICATIONS & PRIVACY SETTINGS Created and saved to DB - [MemberID: " + member.MemberId + "]");

                                #endregion Privacy Settings

                                Logger.Info("UserController -> RegisterLandlord - ** NEW LANDLORD ** - About to created new LANLDORD record - [MemberID: " + member.MemberId + "]");

                                // Finally, make an entry in Landlords Table 
                                Landlord l = CommonHelper.AddNewLandlordEntryInDb(llDetails.FirstName,
                                    llDetails.LastName, llDetails.eMail, llDetails.Password, false, false,
                                    llDetails.ip, member.MemberId);

                                if (l != null && authTokenAddedToDB > 0)
                                {
                                    #region Send Verification email

                                    // Send registration email to member with autogenerated token 
                                    var fromAddress = CommonHelper.GetValueFromConfig("welcomeMail");
                                    var link = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"),
                                               "Registration/Activation.aspx?tokenId=" + tokenId + "&type=ll&llem=" + userNameLowerCase);

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
                                        CommonHelper.SendEmail(Constants.TEMPLATE_REGISTRATION, fromAddress, null,
                                                llDetails.eMail.Trim(), "Confirm your email on Nooch", tokens, null, "NewLandlord@nooch.com");

                                        Logger.Info("UserController -> RegisterLandlord - Registration email sent to [" + llDetails.eMail.Trim() + "] successfully.");
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Error("UserController -> RegisterLandlord - Registration email NOT sent to [" +
                                                               llDetails.eMail.Trim() + "], [Exception: " + ex + "]");
                                    }

                                    #endregion Send Verification email

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
        [ActionName("Login")]
        public LoginResult Login(LoginInput User)
        {
            LoginResult result = new LoginResult();
            result.IsSuccess = false;

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

                    #region All authentication code in this block

                    string passEncrypted = CommonHelper.GetEncryptedData(User.Password);
                    string userNameLowerCaseEncrypted = CommonHelper.GetEncryptedData(User.UserName.ToLower());

                    // checking if username and password is correct and given user is landlord or not
                    var userCheckResult = (from c in obj.Landlords
                                           join d in obj.Members on c.MemberId equals d.MemberId
                                           where d.UserNameLowerCase == userNameLowerCaseEncrypted &&
                                                 d.Password == passEncrypted &&
                                                 d.IsDeleted == false &&
                                                 c.IsDeleted == false
                                           // && (c.Status != "Suspended" || c.Status != "Temporarily_Blocked")
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

                        Logger.Info("Users Cntrlr -> Login requested by [" + User.UserName + "]");
                    }
                    else
                    {
                        Logger.Error("Users Cntrlr -> Login FAILED - [Username: " + User.UserName + "]");

                        result.ErrorMessage = "Invalid Username password or Member not active.";
                        return result;
                    }

                    #endregion

                    return result;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Users Cntrlr -> Login EXCEPTION - [Username: " + User.UserName + "], [Exception: " + ex + "]");

                result.ErrorMessage = "Error while logging on. Retry.";
                return result;
            }
        }


        [HttpPost]
        [ActionName("LoginWithFB")]
        public LoginResult LoginWithFB(LoginwithFBInput User)
        {
            LoginResult result = new LoginResult();
            result.IsSuccess = false;

            try
            {
                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    DateTime requestDatetime = DateTime.Now;


                    // validating fb login data
                    if (String.IsNullOrEmpty(User.eMail) ||
                        String.IsNullOrEmpty(User.FirstName) ||
                        String.IsNullOrEmpty(User.LastName))
                    {
                        result.IsSuccess = false;
                        result.ErrorMessage = "Invalid login information provided.";
                        return result;
                    }


                    // checking if user exists with given email id
                    string userNameLowerCaseEncrypted = CommonHelper.GetEncryptedData(User.eMail.ToLower().Trim());
                    bool is_User_Already_Registerd_With_Nooch = false;
                    bool is_User_Already_Registerd_With_Nooch_as_landlord = false;

                    var memberTableData = (from c in obj.Members
                                           where
                                               c.UserName == userNameLowerCaseEncrypted && c.FacebookUserId == User.FacebookUserId &&
                                               c.IsDeleted == false && c.Status == "Active"
                                           select c).FirstOrDefault();
                    if (memberTableData != null)
                    {
                        is_User_Already_Registerd_With_Nooch = true;
                        // checking user in landlords table

                        var landlordTableDetails = (from c in obj.Landlords
                                                    where c.FacebookUserId == User.FacebookUserId && c.IsDeleted == false && c.Status == "Active"
                                                        && c.eMail == userNameLowerCaseEncrypted
                                                    select c).FirstOrDefault();

                        if (landlordTableDetails != null)
                        {
                            is_User_Already_Registerd_With_Nooch_as_landlord = true;

                            CommonHelper.saveLandlordIp(landlordTableDetails.LandlordId, User.Ip);
                            landlordTableDetails.DateModified = requestDatetime;
                            landlordTableDetails.LastSeenOn = requestDatetime;

                            landlordTableDetails.WebAccessToken = CommonHelper.GenerateAccessToken();

                            obj.SaveChanges();

                            result.IsSuccess = true;
                            result.ErrorMessage = "OK";
                            result.AccessToken = landlordTableDetails.WebAccessToken;
                            result.MemberId = landlordTableDetails.MemberId.ToString();
                            result.LandlordId = landlordTableDetails.LandlordId.ToString();

                            Logger.Info("Users Cntrlr -> Login requested by [" + User.eMail + "] with Facebook");

                            return result;



                        }
                        else
                        {
                            // send email here for landlord welcome.... if sending in register landlord method
                            // Member with that email already exists
                            #region New Landlord But Existing Member

                            Landlord l = CommonHelper.AddNewLandlordEntryInDb(User.FirstName,
                                User.LastName, User.eMail, "", true, true,
                                User.Ip, memberTableData.MemberId);

                            if (l != null)
                            {
                                result.IsSuccess = true;
                                result.ErrorMessage = "OK";


                                CommonHelper.saveLandlordIp(l.LandlordId, User.Ip);
                                l.DateModified = requestDatetime;
                                l.LastSeenOn = requestDatetime;

                                l.WebAccessToken = CommonHelper.GenerateAccessToken();
                                obj.SaveChanges();

                                result.IsSuccess = true;
                                result.ErrorMessage = "OK";
                                result.AccessToken = l.WebAccessToken;
                                result.MemberId = l.MemberId.ToString();
                                result.LandlordId = l.LandlordId.ToString();

                                Logger.Info("Users Cntrlr -> Login requested by [" + User.eMail + "]");

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
                        // new entry in members table and landlords table
                        #region Save New Landlord & Member Details In DB

                        // Make new entry in Members Table first
                        var userNameLowerCase = User.eMail.Trim().ToLower();
                        string noochRandomId = CommonHelper.GetRandomNoochId();

                        if (noochRandomId == null)
                        {
                            result.ErrorMessage = "Some duplicate values are being generated at server. Retry later! ";
                            result.IsSuccess = false;
                            return result;
                        }


                        #region Create User Settings & Save To DB

                        #region Create Member Object

                        string randomPin = CommonHelper.GetRandomPinNumber();

                        var member = new Member
                        {
                            Nooch_ID = noochRandomId,
                            MemberId = Guid.NewGuid(),
                            UserName = CommonHelper.GetEncryptedData(User.eMail),
                            FirstName = CommonHelper.GetEncryptedData(User.FirstName),
                            LastName = CommonHelper.GetEncryptedData(User.LastName),
                            SecondaryEmail = User.eMail,
                            RecoveryEmail = User.eMail,
                            Password = CommonHelper.GetEncryptedData(" "),
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
                            UDID1 = !String.IsNullOrEmpty(User.UserFingerPrints) ? User.UserFingerPrints : null,
                            Country = "US",

                            // some blanks as default
                            Address = CommonHelper.GetEncryptedData(""),
                            State = CommonHelper.GetEncryptedData(""),
                            City = CommonHelper.GetEncryptedData(""),
                            Zipcode = CommonHelper.GetEncryptedData(""),
                        };

                        #endregion Create Member Object

                        obj.Members.Add(member);

                        Logger.Info("UserController -> LoginWithFB - ** NEW LANDLORD ** - MEMBER Created, about to save to DB - [MemberID: " + member.MemberId + "]");
                        try
                        {
                            obj.SaveChanges();

                            CommonHelper.setReferralCode(member.MemberId);
                            var tokenId = Guid.NewGuid();

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
                            int authTokenAddedToDB = obj.SaveChanges();

                            Logger.Info("UserController -> LoginWithFB - ** NEW LANDLORD ** - AUTH TOKEN Created and saved to DB - [MemberID: " + member.MemberId + "]");

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

                            Logger.Info("UserController -> LoginWithFB - ** NEW LANDLORD ** - NOTIFICATIONS & PRIVACY SETTINGS Created and saved to DB - [MemberID: " + member.MemberId + "]");

                            #endregion Privacy Settings

                            Logger.Info("UserController -> LoginWithFB - ** NEW LANDLORD ** - About to created new LANLDORD record - [MemberID: " + member.MemberId + "]");

                            // Finally, make an entry in Landlords Table 
                            Landlord l = CommonHelper.AddNewLandlordEntryInDb(User.FirstName,
                                User.LastName, User.eMail, " ", false, false,
                                User.Ip, member.MemberId);

                            if (l != null && authTokenAddedToDB > 0)
                            {
                                #region Send Verification email

                                // Send registration email to member with autogenerated token 
                                var fromAddress = CommonHelper.GetValueFromConfig("welcomeMail");
                                var link = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"),
                                           "Registration/Activation.aspx?tokenId=" + tokenId + "&type=ll&llem=" + userNameLowerCase);

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
                                    CommonHelper.SendEmail(Constants.TEMPLATE_REGISTRATION, fromAddress, null,
                                            User.eMail.Trim(), "Confirm your email on Nooch", tokens, null, "NewLandlord@nooch.com");

                                    Logger.Info("UserController -> LoginWithFB - Registration email sent to [" + User.eMail.Trim() + "] successfully.");
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error("UserController -> LoginWithFB - Registration email NOT sent to [" +
                                                           User.eMail.Trim() + "], [Exception: " + ex + "]");
                                }

                                #endregion Send Verification email

                                CommonHelper.saveLandlordIp(l.LandlordId, User.Ip);
                                l.DateModified = requestDatetime;
                                l.LastSeenOn = requestDatetime;

                                l.WebAccessToken = CommonHelper.GenerateAccessToken();

                                obj.SaveChanges();

                                result.IsSuccess = true;
                                result.ErrorMessage = "OK";
                                result.AccessToken = l.WebAccessToken;
                                result.MemberId = l.MemberId.ToString();
                                result.LandlordId = l.LandlordId.ToString();


                                return result;




                            }
                            else
                            {
                                // exception while creating account
                                result.ErrorMessage = "Server error. Retry later! ";
                                result.IsSuccess = false;
                                return result;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("UsersController -> RegisterLandlord FAILED while making account for: [" + User.eMail + "], [Exception: " + ex.Message + "]");
                            result.ErrorMessage = "Some duplicate values are being generated at server. Retry later! ";
                            result.IsSuccess = false;
                            return result;
                        }
                        #endregion Create User Settings & Save To DB


                        #endregion Save New Landlord & Member Details In DB

                    }




                }
            }
            catch (Exception ex)
            {
                Logger.Error("Users Cntrlr -> Login EXCEPTION - [Username: " + User.eMail + "], [Exception: " + ex + "]");

                result.ErrorMessage = "Error while logging on. Retry.";
                result.IsSuccess = false;
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

                            string timestamp = DateTime.Now.ToString().Trim();
                            timestamp = digitsOnly.Replace(timestamp, "");

                            string picWithTimestamp = landlordObj.UserPic + "#" + timestamp; // Add timestamp

                            res.UserImageUrl = picWithTimestamp;
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
                                // NOTE:  In general, let's try to use the Member Table info b/c all the existing services for Synapse, email/phone verification, etc. use that table.
                                res.MobileNumber = !String.IsNullOrEmpty(memberObj.ContactNumber) ? CommonHelper.FormatPhoneNumber(memberObj.ContactNumber) : "";
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
            Logger.Info("UsersController -> EditUserInfo Initiated - [LandlordID: " + User.DeviceInfo.LandlorId + "]");

            CreatePropertyResultOutput result = new CreatePropertyResultOutput();
            result.IsSuccess = false;

            try
            {
                Guid landlordguidId = new Guid(User.DeviceInfo.LandlorId);
                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordguidId, User.DeviceInfo.AccessToken);
                result.IsSuccess = false;

                if (result.AuthTokenValidation.IsTokenOk)
                {
                    // valid access token continue with edit

                    using (NOOCHEntities obj = new NOOCHEntities())
                    {
                        // Get details from DB
                        var landlordObj = (from c in obj.Landlords
                                           where c.LandlordId == landlordguidId
                                           select c).FirstOrDefault();

                        if (landlordObj != null)
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
                                    landlordObj.FirstName = CommonHelper.GetEncryptedData(firstName.Trim());
                                    landlordObj.LastName = CommonHelper.GetEncryptedData(lastName.Trim());
                                    if (!String.IsNullOrEmpty(User.UserInfo.DOB))
                                    {
                                        landlordObj.DateOfBirth = Convert.ToDateTime(User.UserInfo.DOB);
                                    }
                                    if (!String.IsNullOrEmpty(User.UserInfo.SSN) &&
                                        User.UserInfo.SSN.Length == 4)
                                    {
                                        landlordObj.SSN = CommonHelper.GetEncryptedData(User.UserInfo.SSN);
                                    }
                                    landlordObj.DateModified = DateTime.Now;

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

                                if (!String.IsNullOrEmpty(User.UserInfo.CompanyName))
                                {
                                    landlordObj.CompanyName = CommonHelper.GetEncryptedData(CommonHelper.UppercaseFirst(User.UserInfo.CompanyName));
                                }

                                if (!String.IsNullOrEmpty(User.UserInfo.CompanyEID))
                                {
                                    landlordObj.CompanyEIN = CommonHelper.GetEncryptedData(User.UserInfo.CompanyEID);
                                }
                                landlordObj.DateModified = DateTime.Now;

                                result.IsSuccess = true;
                                result.ErrorMessage = "OK";

                                #endregion Editing Company Info
                            }

                            else if (User.UserInfo.InfoType == "Contact")
                            {
                                #region Editing Contact Info

                                if (String.IsNullOrEmpty(User.UserInfo.UserEmail))
                                {
                                    result.ErrorMessage = "Email missing!";
                                    return result;
                                }

                                string userEmailNew = CommonHelper.GetEncryptedData(User.UserInfo.UserEmail.ToLower().Trim());

                                // Check if given email is already registered or not
                                var lanlordDetailsByEmail = (from c in obj.Landlords
                                                             where c.eMail == userEmailNew &&
                                                                   c.LandlordId != landlordguidId
                                                             select c).FirstOrDefault();

                                if (lanlordDetailsByEmail != null)
                                {
                                    result.ErrorMessage = "User with given email already exists.";
                                    return result;
                                }

                                if (landlordObj.eMail != userEmailNew)
                                {
                                    landlordObj.IsEmailVerfieid = false;
                                    landlordObj.eMail = userEmailNew;
                                }

                                if (!String.IsNullOrEmpty(User.UserInfo.MobileNumber))
                                {
                                    if (CommonHelper.RemovePhoneNumberFormatting(landlordObj.MobileNumber) != CommonHelper.RemovePhoneNumberFormatting(User.UserInfo.MobileNumber))
                                    {
                                        landlordObj.MobileNumber = CommonHelper.RemovePhoneNumberFormatting(User.UserInfo.MobileNumber);
                                    }
                                }

                                landlordObj.AddressLineOne = CommonHelper.GetEncryptedData(User.UserInfo.AddressLine1);
                                landlordObj.DateModified = DateTime.Now;

                                result.IsSuccess = true;
                                result.ErrorMessage = "OK";

                                #endregion Editing Contact Info
                            }

                            else if (User.UserInfo.InfoType == "Social")
                            {
                                #region Editing Social Info
                                landlordObj.DateModified = DateTime.Now;
                                landlordObj.DateModified = DateTime.Now;

                                // Now store all social info in DB

                                if (!String.IsNullOrEmpty(User.UserInfo.TwitterHandle))
                                {
                                    landlordObj.TwitterHandle = User.UserInfo.TwitterHandle;
                                }
                                if (!String.IsNullOrEmpty(User.UserInfo.FbUrl))
                                {
                                    landlordObj.FBId = User.UserInfo.FbUrl;
                                }
                                if (!String.IsNullOrEmpty(User.UserInfo.InstaUrl))
                                {
                                    landlordObj.InstagramUrl = User.UserInfo.InstaUrl;
                                }

                                landlordObj.DateModified = DateTime.Now;

                                result.IsSuccess = true;
                                result.ErrorMessage = "OK";

                                #endregion Editing Social Info
                            }

                            obj.SaveChanges();
                        }
                        else
                        {
                            Logger.Error("UsersController -> EditUserInfo FAILED - Landlord ID Not Found");
                            result.ErrorMessage = "Given landlord ID not found.";
                        }

                        #region Update MEMBERS Table

                        // CLIFF (10/15/15): Since all the Synapse methods take the data from the Members Table,
                        //                   we have to also save any of that data for Landlords in the Members Table 
                        //                   ...even though we have most of the same data in the Landlords table.  We shouldn't have duplicated everything :-(

                        Guid memGuidId = new Guid(User.DeviceInfo.MemberId);

                        var memberObj = (from c in obj.Members
                                         where c.MemberId == memGuidId && c.IsDeleted == false
                                         select c).FirstOrDefault();

                        if (memberObj != null)
                        {
                            if (!String.IsNullOrEmpty(User.UserInfo.FullName))
                            {
                                string firstName = "", lastName = "";
                                string[] nameAftetSplit = User.UserInfo.FullName.Trim().ToLower().Split(' ');

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
                            }

                            if (!String.IsNullOrEmpty(User.UserInfo.DOB))
                            {
                                memberObj.DateOfBirth = Convert.ToDateTime(User.UserInfo.DOB);
                            }
                            if (!String.IsNullOrEmpty(User.UserInfo.SSN) &&
                                User.UserInfo.SSN.Length == 4)
                            {
                                memberObj.SSN = CommonHelper.GetEncryptedData(User.UserInfo.SSN);
                            }
                            if (!String.IsNullOrEmpty(User.UserInfo.AddressLine1))
                            {
                                memberObj.Address = CommonHelper.GetEncryptedData(User.UserInfo.AddressLine1);
                            }
                            if (!String.IsNullOrEmpty(User.UserInfo.Zip))
                            {
                                memberObj.Zipcode = CommonHelper.GetEncryptedData(User.UserInfo.Zip);
                            }

                            if (!String.IsNullOrEmpty(User.UserInfo.MobileNumber))
                            {
                                string newPhoneClean = CommonHelper.RemovePhoneNumberFormatting(User.UserInfo.MobileNumber);

                                if (CommonHelper.RemovePhoneNumberFormatting(memberObj.ContactNumber) != newPhoneClean)
                                {
                                    memberObj.ContactNumber = newPhoneClean;

                                    //if (!CommonHelper.IsPhoneNumberAlreadyRegistered(newPhoneClean).isAlreadyRegistered)
                                    if (memberObj.IsVerifiedPhone != true)
                                    {
                                        memberObj.IsVerifiedPhone = false;

                                        #region SendingSMSVerificaion

                                        try
                                        {
                                            string MessageBody = "Reply with 'GO' to this message to confirm your phone number on Nooch.";
                                            string SMSresult = CommonHelper.SendSMS(newPhoneClean, MessageBody, memberObj.MemberId.ToString());

                                            Logger.Info("UsersController -> EditUserInfo -> SMS Verification sent to [" + User.UserInfo.MobileNumber + "] successfully.");
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.Error("UsersController -> EditUserInfo -> SMS Verification NOT sent to [" +
                                                User.UserInfo.MobileNumber + "], [Exception: " + ex + "]");
                                        }

                                        #endregion SendingSMSVerificaion
                                    }
                                    //else
                                    //{
                                    //    result.ErrorMessage = "Phone Number already registered with Nooch";
                                    //    return result;
                                    //}
                                }
                            }

                            memberObj.DateModified = DateTime.Now;

                            obj.SaveChanges();

                            result.IsSuccess = true;
                            result.ErrorMessage = "OK";
                        }
                        else
                        {
                            Logger.Error("UsersController -> EditUserInfo FAILED - Member ID Not Found");
                        }

                        #endregion Update MEMBERS Table
                    }
                }
                else
                {
                    result.ErrorMessage = result.AuthTokenValidation.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("UsersController -> EditUserInfo FAILED - [Outer Exception: " + ex.ToString() + "]");
                result.ErrorMessage = "Server error";
            }

            return result;
        }


        [HttpPost]
        [ActionName("submitLandlordIdVerWiz")]
        public GenericInternalResponse submitLandlordIdVerWiz(idVerWizardInput landlordsInput)
        {
            Logger.Info("UsersController -> submitLandlordIdVerWiz Initiated - [LandlordID: " + landlordsInput.DeviceInfo.LandlorId + "]");

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
                            if (!String.IsNullOrEmpty(landlordsInput.phone))
                            {
                                lanlordObj.MobileNumber = CommonHelper.RemovePhoneNumberFormatting(landlordsInput.phone);
                            }
                            else
                            {
                                res.msg = "PHONE WAS NULL OR EMPTY";
                                return res;
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
                                if (!String.IsNullOrEmpty(landlordsInput.phone))
                                {
                                    string newPhoneClean = CommonHelper.RemovePhoneNumberFormatting(landlordsInput.phone);

                                    if (CommonHelper.RemovePhoneNumberFormatting(memberObj.ContactNumber) != newPhoneClean)
                                    {
                                        var IsPhoneAlreadyRegistered = CommonHelper.IsPhoneNumberAlreadyRegistered(newPhoneClean);

                                        //if (!IsPhoneAlreadyRegistered.isAlreadyRegistered)
                                        if (memberObj.IsVerifiedPhone != true)
                                        {
                                            memberObj.ContactNumber = newPhoneClean;
                                            memberObj.IsVerifiedPhone = false;

                                            #region SendingSMSVerificaion

                                            try
                                            {
                                                string MessageBody = "Reply with 'GO' to this message to confirm your phone number on Nooch.";
                                                string SMSresult = CommonHelper.SendSMS(newPhoneClean, MessageBody, memberObj.MemberId.ToString());

                                                Logger.Info("UsersController -> submitLandlordIdVerWiz -> SMS Verification sent to [" + landlordsInput.phone + "] successfully.");
                                            }
                                            catch (Exception ex)
                                            {
                                                Logger.Error("UsersController -> submitLandlordIdVerWiz -> SMS Verification NOT sent to [" +
                                                    landlordsInput.phone + "], [Exception: " + ex + "]");
                                            }

                                            #endregion
                                        }
                                        else
                                        {
                                            Logger.Info("UsersController -> submitLandlordIdVerWiz - Phone Number Already Verified - [Phone: " + newPhoneClean + "]");

                                            //if (IsPhoneAlreadyRegistered.memberMatched != null)
                                            //{
                                            //    string memberWithThatPhone_userName = CommonHelper.GetDecryptedData(IsPhoneAlreadyRegistered.memberMatched.UserName);
                                            //    Logger.Info("UsersController -> submitLandlordIdVerWiz - Phone Number Already Registered by [" + memberWithThatPhone_userName + "], " +
                                            //                "[isPhoneVerified (for the user who already registered this number): " + IsPhoneAlreadyRegistered.memberMatched.IsVerifiedPhone + "]");
                                            //}
                                            // CLIFF (10/23/15): Not sure how we should handle this for Landlords. Probably should enforce unique phone numbers, but for now just 
                                            //                   to avoid any issues with early Landlord users, let's just save the phone and allow.
                                            //res.msg = "Phone Number already registered with Nooch";
                                            //return res;
                                        }
                                    }
                                }

                                memberObj.DateModified = DateTime.Now;

                                obj.SaveChanges();

                                res.success = true;
                                res.msg = "OK";
                            }
                            else
                            {
                                Logger.Error("UsersController -> submitLandlordIdVerWiz FAILED - Member Not Found");
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
        [ActionName("ResetPassword")]
        public PasswordResetOutputClass ResetPassword(PasswordResetInputClass userName)
        {
            PasswordResetOutputClass res = new PasswordResetOutputClass();
            res.IsSuccess = false;

            var getMember = CommonHelper.GetMemberByEmailId(userName.eMail);

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
            //Logger.Info("UsersController -> GetAccountCompletetionStatsOfGivenLandlord Initiated - [LandlordID: " + Property.LandlorId + "]");

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

                        // CLIFF (10/26/15): These values are only checking Landlords table, and not Members Table, which is where we update the user's "Status" on the Email Verification landing page
                        //                   The Angular controller is instead saving and using the value from GetUserInfo(), which does check the Members Table... so these 2 lines should be unneccessary
                        //result.IsEmailVerified = Convert.ToBoolean(obj.IsEmailVerifiedforGivenLandlordOrTenant("Landlord", Property.LandlorId).SingleOrDefault());
                        //result.IsPhoneVerified = Convert.ToBoolean(obj.IsPhoneVerifiedforGivenLandlordOrTenant("Landlord", Property.LandlorId).SingleOrDefault());

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
                Logger.Error("UsersController -> GetAccountCompletionStatsOfGivenLandlord FAILED - [LandlordID: " +
                             Property.LandlorId + "], [Exception: " + ex.Message + "]");

                result.ErrorMessage = "Error while getting properties list. Retry later!";
            }

            return result;
        }


        /// <summary>
        /// To resend activation email to a Landlord.
        /// </summary>
        /// <param name="input"></param>
        [HttpPost]
        [ActionName("ResendVerificationEmailAndSMS")]
        public LoginResult ResendVerificationEmailAndSMS(ResendVerificationEmailAndSMSInput input)
        {
            Logger.Info("UsersController -> ResendVerificationEmailAndSMS Initiated - [LandlordID: " + input.UserId + "], [Type: " + input.RequestFor + "]");

            LoginResult result = new LoginResult();
            result.IsSuccess = false;
            result.ErrorMessage = "Initial";

            try
            {
                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    Guid userGUID = new Guid(input.UserId);

                    switch (input.UserType)
                    {
                        case "Landlord":

                            #region Landlord Related Operations

                            var landlordDetails = (from c in obj.Landlords
                                                   where c.LandlordId == userGUID
                                                   select c).FirstOrDefault();

                            if (landlordDetails != null)
                            {
                                switch (input.RequestFor)
                                {
                                    case "Email":
                                        string s = CommonHelper.ResendVerificationLink(CommonHelper.GetDecryptedData(landlordDetails.eMail));
                                        if (s == "Success")
                                        {
                                            result.IsSuccess = true;
                                            result.ErrorMessage = "OK";
                                        }
                                        else
                                        {
                                            result.ErrorMessage = s;
                                        }
                                        break;
                                    case "SMS":

                                        #region SendingSMSVerificaion

                                        try
                                        {
                                            string MessageBody = "Reply with 'GO' to this message to confirm your phone number on Nooch.";
                                            string SMSresult = CommonHelper.SendSMS(landlordDetails.MobileNumber, MessageBody, landlordDetails.MemberId.ToString());

                                            result.ErrorMessage = SMSresult;
                                            result.IsSuccess = true;

                                            Logger.Info("UsersController -> ResendVerificationEmailAndSMS -> SMS Verification sent to [" + landlordDetails.MobileNumber + "] successfully.");
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.Error("UsersController -> ResendVerificationEmailAndSMS -> SMS Verification NOT sent to [" +
                                                landlordDetails.MobileNumber + "], [Exception: " + ex + "]");
                                            result.ErrorMessage = "Exception on sending SMS";
                                        }

                                        #endregion

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

                            var tenantDetails = (from c in obj.Tenants
                                                 where c.TenantId == userGUID
                                                 select c).FirstOrDefault();

                            if (tenantDetails != null)
                            {
                                switch (input.RequestFor)
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
                             input.UserId + " ], [Exception: " + ex.Message + "]");

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
                                               d.IsDeleted == false &&
                                               c.IsDefault == true
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
                            #region Set Bank Logo
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
                            #endregion Set Bank Logo
                            res.AccountName = CommonHelper.GetDecryptedData(bankAccnt.account_number_string);
                            res.AccountStatus = bankAccnt.Status;
                            res.allowed = bankAccnt.allowed;
                            res.dateCreated = Convert.ToDateTime(bankAccnt.AddedOn).ToString("MMM d, yyyy");
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
                var landlordguidId = new Guid(input.LandlordId);
                var checkToken = CommonHelper.AuthTokenValidation(landlordguidId, input.AccessToken);

                if (checkToken.IsTokenOk)
                {
                    // valid access token continue with edit
                    Guid MemGuidId = new Guid(input.MemberId);

                    using (NOOCHEntities obj = new NOOCHEntities())
                    {
                        var synapseBank = (from c in obj.SynapseBanksOfMembers
                                           where c.MemberId == MemGuidId &&
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
                Logger.Error("UsersController -> deleteSynapseBank FAILED - [LandlorID: " + input.LandlordId + "]. Exception: [" + ex + "]");
                res.msg = "Hit exception";
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
            // CLIFF (11/5/15): STUFF TO ADD FOR THIS METHOD:
            // • Let landlord write an optional subject line (add 1 string value to the SendEmailsToTenantsInputClass)
            // • Let Landlord BCC themsef (add checkmark to the interface, boolean to SendEmailsToTenantsInputClass)

            Logger.Info("UsersController -> SendEmailsToTenants Initiated - [LandlordID: " + User.DeviceInfo.LandlorId + "], [TenantID To Send To: " + User.EmailInfo.TenantIdToBeMessaged + "]");

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
                        var landlordObj = (from c in obj.Landlords
                                           where c.LandlordId == landlordguidId
                                           select c).FirstOrDefault();

                        if (landlordObj != null)
                        {
                            string fromAddress = CommonHelper.GetDecryptedData(landlordObj.eMail);
                            string landlordFullName = CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(landlordObj.FirstName)) + " " +
                                                      CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(landlordObj.LastName));

                            string bccEmail = User.EmailInfo.shouldBccLandlord == true ? fromAddress : null;

                            // Now check if the email is going to just ONE tenant, or ALL of a Property's tenants

                            if (User.EmailInfo.IsForAllOrOne == "One")
                            {
                                #region Sending To A Single Tenant

                                try
                                {
                                    if (!String.IsNullOrEmpty(User.EmailInfo.TenantIdToBeMessaged))
                                    {
                                        Guid tenantguidId = new Guid(User.EmailInfo.TenantIdToBeMessaged);

                                        // Get Tenant's info
                                        var tenantObj = (from c in obj.Tenants
                                                         where c.TenantId == tenantguidId
                                                         select c).FirstOrDefault();

                                        if (tenantObj != null)
                                        {
                                            string toAddress = CommonHelper.GetDecryptedData(tenantObj.eMail);

                                            string bodytext = User.EmailInfo.MessageToBeSent;

                                            CommonHelper.SendEmail(null, fromAddress, landlordFullName, toAddress,
                                                "New message from " + landlordFullName, null, bodytext, bccEmail);

                                            result.IsSuccess = true;
                                            result.ErrorMessage = "Message sent successfully";
                                        }
                                        else
                                        {
                                            result.ErrorMessage = "Given tenant ID not found.";
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
                                    // Get all Tenants of given Landlord for given Property
                                    string propId = User.EmailInfo.PropertyId;

                                    List<GetTenantsInGivenPropertyId_Result2> allTenantsOfLl = obj.GetTenantsInGivenPropertyId(propId).ToList();

                                    if (allTenantsOfLl.Count > 0)
                                    {
                                        foreach (GetTenantsInGivenPropertyId_Result2 ten in allTenantsOfLl)
                                        {
                                            string emailtobesentto = CommonHelper.GetDecryptedData(ten.TenantEmail);
                                            string bodytext = User.EmailInfo.MessageToBeSent;

                                            CommonHelper.SendEmail(null, fromAddress, landlordFullName, emailtobesentto,
                                                "New message from " + landlordFullName, null, bodytext, bccEmail);
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


        /// <summary>
        /// To change password for given user
        /// </summary>
        /// <param name="input"></param>
        /// <returns>GenericInternalResponse object</returns>
        [HttpPost]
        [ActionName("ChangeUserPassword")]
        public GenericInternalResponse ChangeUserPassword(UpdatePasswordInput input)
        {
            GenericInternalResponse result = new GenericInternalResponse();
            result.success = false;

            try
            {
                if (String.IsNullOrEmpty(input.newPw))
                {
                    result.msg = "No new password sent!";
                }
                else
                {
                    Guid landlordguidId = new Guid(input.AuthInfo.LandlorId);
                    result.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordguidId, input.AuthInfo.AccessToken);

                    if (result.AuthTokenValidation.IsTokenOk)
                    {
                        using (NOOCHEntities obj = new NOOCHEntities())
                        {
                            var lanlordObj = (from c in obj.Landlords
                                              where c.LandlordId == landlordguidId
                                              select c).FirstOrDefault();

                            if (lanlordObj != null)
                            {

                                // Now get Member from Members Table
                                var memberObj = (from c in obj.Members
                                                 where c.MemberId == lanlordObj.MemberId
                                                 select c).FirstOrDefault();

                                if (memberObj != null)
                                {
                                    string currentPwEnc = CommonHelper.GetEncryptedData(input.currentPw);

                                    if (currentPwEnc == memberObj.Password)
                                    {
                                        memberObj.Password = CommonHelper.GetEncryptedData(input.newPw);
                                        memberObj.DateModified = DateTime.Now;
                                        obj.SaveChanges();

                                        result.success = true;
                                        result.msg = "Password changed successfully.";
                                    }
                                    else
                                    {
                                        result.msg = "Current password [" + currentPwEnc + "] was incorrect.";
                                    }
                                }
                                else
                                {
                                    result.msg = "Invalid user id.";
                                }
                            }
                            else
                            {
                                result.msg = "Given landlord ID not found.";
                            }
                        }
                    }
                    else
                    {
                        result.msg = result.AuthTokenValidation.ErrorMessage;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("UsersController -> EditUserInfo FAILED - [Outer Exception: " + ex.ToString() + "]");
                result.msg = "Server error";
            }

            return result;
        }

        private static Regex digitsOnly = new Regex(@"[^\d]");
    }
}
