using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web;
using LanLordlAPIs.Classes.Crypto;
using LanLordlAPIs.Models.db_Model;
using System.Drawing;
using System.Net;
using System.Net.Mail;
using System.Web.Hosting;
using LanLordlAPIs.Models.Output_Models;

namespace LanLordlAPIs.Classes.Utility
{
    public class CommonHelper
    {
        public static string GetEncryptedData(string sourceData)
        {
            try
            {
                var aesAlgorithm = new AES();
                string encryptedData = aesAlgorithm.Encrypt(sourceData, string.Empty);
                return encryptedData.Replace(" ", "+");
            }
            catch (Exception ex)
            {
                Logger.Error("Landlord Common Helper -> GetEncryptdData FAILED - [SourceData: [" + sourceData + ", [Exception: " + ex + "]");
            }
            return string.Empty;
        }


        public static string GetDecryptedData(string sourceData)
        {
            try
            {
                var aesAlgorithm = new AES();
                string decryptedData = aesAlgorithm.Decrypt(sourceData.Replace(" ", "+"), string.Empty);
                return decryptedData;
            }
            catch (Exception ex)
            {
                Logger.Error("Landlord Common Helper -> GetDecryptedData FAILED - [SourceData: [" + sourceData + ", [Exception: " + ex + "]");
            }
            return string.Empty;
        }


        public static string UppercaseFirst(string s)
        {
            // Check for empty string.
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            // Return char and concat substring.
            return char.ToUpper(s[0]) + s.Substring(1);
        }


        public static string GetRandomNoochId()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            int j = 1;
            using (NOOCHEntities obj = new NOOCHEntities())
            {
                for (int i = 0; i <= j; i++)
                {
                    var randomId = new string(
                        Enumerable.Repeat(chars, 8)
                            .Select(s => s[random.Next(s.Length)])
                            .ToArray());

                    var memberEntity = getMemberByNoochId(randomId);

                    if (memberEntity == null)
                    {
                        return randomId;
                    }

                    j += i + 1;
                }
            }
            return null;
        }


        public static bool saveLandlordIp(Guid LandlorId, string IP)
        {
            try
            {
                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    var lanlorddetails = (from c in obj.Landlords
                                          where c.LandlordId == LandlorId
                                          select c).FirstOrDefault();

                    if (lanlorddetails == null) return false;

                    if (!String.IsNullOrEmpty(lanlorddetails.IpAddresses))
                    {
                        string IPsListPrepared = "";
                        //trying to split and see how many old ips we have
                        string[] recenips = lanlorddetails.IpAddresses.Split(',');
                        if (recenips.Length >= 5)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                if (i == 0)
                                {
                                    IPsListPrepared = recenips[i];
                                }
                                else if (i == 4)
                                {
                                    IPsListPrepared = IPsListPrepared + ", " + recenips[i];
                                    break;
                                }
                                else
                                {
                                    IPsListPrepared = IPsListPrepared + ", " + recenips[i];
                                }
                            }
                            IPsListPrepared = IPsListPrepared + ", " + IP;
                        }
                        else
                        {
                            IPsListPrepared = lanlorddetails.IpAddresses + ", " + IP;
                        }

                        // Update IP in DB
                        lanlorddetails.IpAddresses = IPsListPrepared;
                        obj.SaveChanges();

                        return true;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Landlords API -> CommonHelper -> saveLandlordIp. Error while updating IP address - [ " + IP + " ] for Landlor Id [ " + LandlorId + " ], [Exception: " + ex + " ]");
                return false;
            }
        }


        public static string GenerateAccessToken()
        {
            byte[] time = BitConverter.GetBytes(DateTime.UtcNow.ToBinary());
            byte[] key = Guid.NewGuid().ToByteArray();
            string token = Convert.ToBase64String(time.Concat(key).ToArray());
            return CommonHelper.GetEncryptedData(token);
        }


        public static AccessTokenValidationOutput AuthTokenValidation(Guid LandlorId, string accesstoken)
        {
            AccessTokenValidationOutput result = new AccessTokenValidationOutput();
            using (NOOCHEntities obj = new NOOCHEntities())
            {
                var lanlorddetails = (from c in obj.Landlords 
                                      where c.LandlordId == LandlorId && c.WebAccessToken == accesstoken && c.IsDeleted == false
                                      select c).FirstOrDefault();

                if (lanlorddetails != null)
                {
                    //checking if there is need to update existing token or not
                    DateTime currentTime = DateTime.Now;
                    DateTime lastseen = currentTime;
                    TimeSpan t;

                    if (lanlorddetails.LastSeenOn != null)
                    {
                        t = currentTime - Convert.ToDateTime(lanlorddetails.LastSeenOn);
                    }
                    else
                    {
                        t = currentTime - lastseen;
                    }

                    if (t.TotalMinutes > 20)
                    {
                        lanlorddetails.WebAccessToken = GenerateAccessToken();
                        lanlorddetails.LastSeenOn = lastseen;
                        obj.SaveChanges();

                        result.IsTokenOk = true;
                        result.IsTokenUpdated = true;
                        result.AccessToken = lanlorddetails.WebAccessToken;
                        result.ErrorMessage = "OK";
                    }
                    else
                    {
                        result.IsTokenOk = true;
                        result.IsTokenUpdated = false;
                        result.AccessToken = lanlorddetails.WebAccessToken;
                        result.ErrorMessage = "OK";
                        lanlorddetails.LastSeenOn = lastseen;
                        obj.SaveChanges();
                    }
                }
                else
                {
                    result.IsTokenOk = false;
                    result.IsTokenUpdated = false;
                    result.ErrorMessage = "Unauthorised access";
                    result.AccessToken = "";
                }
            }

            return result;
        }


        public static string FormatPhoneNumber(string sourcePhone)
        {
            if (String.IsNullOrEmpty(sourcePhone) || sourcePhone.Length != 10) return sourcePhone;
            sourcePhone = "(" + sourcePhone;
            sourcePhone = sourcePhone.Insert(4, ")");
            sourcePhone = sourcePhone.Insert(5, " ");
            sourcePhone = sourcePhone.Insert(9, "-");
            return sourcePhone;
        }


        public static string RemovePhoneNumberFormatting(string sourceNum)
        {
            if (!String.IsNullOrEmpty(sourceNum))
            {
                // removing extra stuff from phone number
                sourceNum = sourceNum.Replace("(", "");
                sourceNum = sourceNum.Replace(")", "");
                sourceNum = sourceNum.Replace(" ", "");
                sourceNum = sourceNum.Replace("-", "");
                sourceNum = sourceNum.Replace("+", "");
                sourceNum = sourceNum.Replace(".", "");
            }
            return sourceNum;
        }


        public static string GetRandomPinNumber()
        {
            const string chars = "0123456789";
            var random = new Random();
            var randomId = new string(
                Enumerable.Repeat(chars, 4)
                          .Select(s => s[random.Next(s.Length)])
                          .ToArray());

            return randomId;
        }


        public static Guid ConvertToGuid(string value)
        {
            var id = new Guid();
            try
            {
                if (!String.IsNullOrEmpty(value) && value != Guid.Empty.ToString())
                {
                    id = new Guid(value);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Landlord Common Helper -> ConvertTo Guid FAILED - [Exception: " + ex.Message + "]. Unable to format string: [" + value + "]");
                throw;
            }
            return id;
        }


        public static string getReferralCode(String memberId)
        {
            using (var noochConnection = new NOOCHEntities())
            {
                var id = ConvertToGuid(memberId);

                var noochMember = (from c in noochConnection.Members where c.MemberId == id && c.IsDeleted == false select c).FirstOrDefault();

                if (noochMember != null)
                {
                    if (noochMember.InviteCodeId != null)
                    {
                        Guid v = ConvertToGuid(noochMember.InviteCodeId.ToString());

                        var inviteMember = (from c in noochConnection.InviteCodes where c.InviteCodeId == v select c).FirstOrDefault();

                        if (inviteMember != null)
                        {
                            return inviteMember.code;
                        }
                        else
                        {
                            return "";
                        }
                    }
                    else
                    {
                        //No referal code
                        return "";
                    }
                }
                else
                {
                    return "Invalid";
                }
            }
        }


        public static string setReferralCode(Guid memberId)
        {
            using (var noochConnection = new NOOCHEntities())
            {
                try
                {
                    // Get the member's details
                    var noochMember = (from c in noochConnection.Members where c.MemberId == memberId select c).FirstOrDefault();

                    if (noochMember != null)
                    {
                        //Check if the user already has an invite code generted or not
                        string existing = getReferralCode(memberId.ToString());
                        if (existing == "")
                        {
                            //Generate random code
                            Random rng = new Random();
                            int value = rng.Next(1000);
                            string text = value.ToString("000");
                            string userName = GetDecryptedData(noochMember.FirstName);
                            string code = userName.Substring(0, 4).ToUpper() + text;

                            //Insert into invites
                            InviteCode obj = new InviteCode();
                            obj.InviteCodeId = Guid.NewGuid();
                            obj.code = code;
                            obj.totalAllowed = 10;
                            obj.count = 0;

                            noochConnection.InviteCodes.Add(obj);
                            noochConnection.SaveChanges();
                            //update the inviteid into the members table's invitecodeid column
                            noochMember.InviteCodeId = obj.InviteCodeId;
                            noochConnection.SaveChanges();
                            return "Success";
                        }
                        else
                        {
                            return "Invite Code Already Exists";
                        }
                    }
                    else
                    {
                        return "Invalid";
                    }
                }
                catch (Exception ex)
                {
                    return "Error";
                }
            }
        }


        public static string GetValueFromConfig(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }


        public static Image byteArrayToImage(byte[] byteArrayIn)
        {
            MemoryStream ms = new MemoryStream(byteArrayIn);
            Image returnImage = Image.FromStream(ms);
            return returnImage;
        }

        //to save image in directory and get url
        public static string SaveBase64AsImage(string fileNametobeused, string base64String)
        {
            string filnameMade = "";
            byte[] bytes = Convert.FromBase64String(base64String);
            string folderPath = GetValueFromConfig("PhotoPath");

            string PhotoUrl = GetValueFromConfig("PhotoUrl");

            Image image = byteArrayToImage(bytes);
            string fullpathtoSaveimage = "";
            using (MemoryStream ms = new MemoryStream(bytes))
            {
                image = Image.FromStream(ms);
                Bitmap bm = new Bitmap(image);
                fullpathtoSaveimage = Path.Combine(folderPath, fileNametobeused);
                fullpathtoSaveimage = HostingEnvironment.MapPath(fullpathtoSaveimage);
                filnameMade = fileNametobeused;

                // checking if file with same name already exists
                while (File.Exists(Path.Combine(folderPath, fileNametobeused)))
                {
                    filnameMade = fileNametobeused + "1";
                    fullpathtoSaveimage = Path.Combine(folderPath, filnameMade);
                    fullpathtoSaveimage = HostingEnvironment.MapPath(fullpathtoSaveimage);
                }

                bm.Save(fullpathtoSaveimage, System.Drawing.Imaging.ImageFormat.Png);
            }

            fullpathtoSaveimage = PhotoUrl + filnameMade;
            return fullpathtoSaveimage;
        }


        public static string GetMemberIdOfLandlord(Guid landlorID)
        {
            using (NOOCHEntities obj = new NOOCHEntities())
            {
                return (from c in obj.Landlords where c.LandlordId == landlorID select c.MemberId).SingleOrDefault().ToString();
            }
        }


        public static CheckAndRegisterMemberByEmailResult CheckIfMemberExistsWithGivenEmailId(string eMailId)
        {
            CheckAndRegisterMemberByEmailResult result = new CheckAndRegisterMemberByEmailResult();
            try
            {
                if (String.IsNullOrEmpty(eMailId))
                {
                    return null;
                }

                string email = eMailId.Trim().ToLower();
                email = CommonHelper.GetEncryptedData(email);
                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    var existingMemberDetails = (from c in obj.Members where c.UserName == email && c.IsDeleted == false select c).FirstOrDefault();
                    if (existingMemberDetails != null)
                    {
                        // user already exists
                        result.IsSuccess = false;
                        result.ErrorMessage = "OK";
                        result.MemberDetails = existingMemberDetails;
                    }
                    else
                    {
                        result.IsSuccess = true;
                        result.ErrorMessage = "No user found.";
                        return result;
                    }
                }
                return result;
            }
            catch (Exception)
            {
                Logger.Error("CheckIfMemberExistsWithGivenEmailId while checking " + eMailId);
                result.IsSuccess = false;
                result.ErrorMessage = "Server Error.";
                return result;
            }
        }


        public static Member getMemberByEmailId(string eMailID)
        {
            using (NOOCHEntities obj = new NOOCHEntities())
            {
                string email = eMailID.Trim().ToLower();
                email = CommonHelper.GetEncryptedData(email);

                var memDetails = (from c in obj.Members where c.UserName == email && c.IsDeleted == false select c).SingleOrDefault();
                return memDetails;
            }
        }


        public static Member getMemberByNoochId(string NoochId)
        {
            using (NOOCHEntities obj = new NOOCHEntities())
            {
                var memDetails = (from c in obj.Members where c.Nooch_ID == NoochId select c).SingleOrDefault();
                return memDetails;
            }
        }


        public static CheckAndRegisterLandlordByEmailResult checkAndRegisterLandlordByemailId(string eMailID)
        {
            CheckAndRegisterLandlordByEmailResult result = new CheckAndRegisterLandlordByEmailResult();
            try
            {
                string email = eMailID.Trim().ToLower();
                email = CommonHelper.GetEncryptedData(email);
                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    var existingMemberDetails = (from c in obj.Landlords where c.eMail == email && c.IsDeleted == false select c).FirstOrDefault();
                    if (existingMemberDetails != null)
                    {

                        result.IsSuccess = false;
                        result.ErrorMessage = "Landlord already exists with given eMail id.";
                        result.LanlordDetails = existingMemberDetails;
                    }
                    else
                    {
                        // registering new landlord
                        result.IsSuccess = true;
                        result.ErrorMessage = "No user found.";
                        return result;
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error("Landlord Common Helper -> checkAndRegisterLandlordByemailId FAIELD - [Exception: " + ex.Message + "], [Email: " + eMailID + "]");
                result.IsSuccess = false;
                result.ErrorMessage = "Server Error.";
                return result;
            }
        }


        public static bool SendPasswordMail(Member member, string primaryMail)
        {
            try
            {
                var fromAddress = GetValueFromConfig("adminMail");

                var tokens = new Dictionary<string, string>
                {
                    {
                        Constants.PLACEHOLDER_FIRST_NAME,
                        CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(member.FirstName)) + " " +
                        CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(member.LastName))
                    },
                    {Constants.PLACEHOLDER_LAST_NAME, CommonHelper.GetDecryptedData(member.LastName)},
                    {
                        Constants.PLACEHOLDER_PASSWORDLINK,
                        String.Concat(GetValueFromConfig("ApplicationURL"),
                            "/ForgotPassword/ResetPasswordLandlords.aspx?memberId=" + member.MemberId)
                    }
                };

                //code to make entry in db
                using (var noochConnection = new NOOCHEntities())
                {
                    var entity = new PasswordResetRequest
                    {
                        RequestedOn = DateTime.Now,
                        MemberId = member.MemberId
                    };

                    int result = 0;
                    noochConnection.PasswordResetRequests.Add(entity);

                    result = noochConnection.SaveChanges();
                }

                return SendEmail(Constants.TEMPLATE_FORGOT_PASSWORD, fromAddress,
                    primaryMail, "Reset your Nooch password", tokens
                    , null);
            }
            catch (Exception ex)
            {
                return false;
            }
        }


        public static Landlord AddNewLandlordEntryInDb(string fName, string lName, string email, string pw, bool eMailSatusToSet, bool phoneStatusToSet, string ip, Guid memberGuid)
        {
            try
            {
                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    Landlord ll = new Landlord();
                    ll.LandlordId = Guid.NewGuid();
                    ll.FirstName = GetEncryptedData(fName.ToLower().Trim());
                    ll.LastName = GetEncryptedData(lName.ToLower().Trim());
                    ll.eMail = GetEncryptedData(email.ToLower().Trim());
                    ll.IsEmailVerfieid = eMailSatusToSet;
                    ll.IsPhoneVerified = phoneStatusToSet;
                    ll.Status = "Active";
                    ll.Type = "Landlord";
                    ll.SubType = "Basic";
                    ll.MemberId = memberGuid;
                    ll.IsDeleted = false;
                    ll.DateCreated = DateTime.Now;
                    ll.IpAddresses = ip;
                    ll.IsIdVerified = false;

                    obj.Landlords.Add(ll);
                    obj.SaveChanges();

                    return ll;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("CommonHelper -> AddNewLandlordEntryInDb FAILED - [Exception: " + ex + "]");
                return null;
            }
        }


        public static string GetEmailTemplate(string physicalPath)
        {
            using (var sr = new StreamReader(physicalPath))
                return sr.ReadToEnd();
        }


        public static bool SendEmail(string templateName, string fromAddress, string toAddress, string subject, IEnumerable<KeyValuePair<string, string>> replacements, string bodyText)
        {
            try
            {
                MailMessage mailMessage = new MailMessage();
                string template;
                string subjectString = subject;
                string content = string.Empty;
                if (!String.IsNullOrEmpty(templateName))
                {
                    template = GetEmailTemplate(String.Concat(GetValueFromConfig("EmailTemplatesPath"), templateName, ".txt"));
                    content = template;

                    // Replace tokens in the message body and subject line
                    if (replacements != null)
                    {
                        foreach (var token in replacements)
                        {
                            content = content.Replace(token.Key, token.Value);
                            subjectString = subject.Replace(token.Key, token.Value);
                        }
                    }
                    mailMessage.Body = content;
                }
                else
                {
                    mailMessage.Body = bodyText;
                }

                switch (fromAddress)
                {
                    case "receipts@nooch.com":
                        mailMessage.From = new MailAddress(fromAddress, "Nooch Receipts");
                        break;
                    case "support@nooch.com":
                        mailMessage.From = new MailAddress(fromAddress, "Nooch Support");
                        break;
                    default:
                        mailMessage.From = new MailAddress(fromAddress, "Nooch Admin");
                        break;
                }
                mailMessage.IsBodyHtml = true;
                mailMessage.Subject = subjectString;
                mailMessage.To.Add(toAddress);

                SmtpClient smtp = new SmtpClient();

                smtp.Host = "smtp.mandrillapp.com";
                smtp.Port = 25;

                //smtp.Timeout = 6000;
                smtp.UseDefaultCredentials = true;
                smtp.Credentials = new NetworkCredential("cliff@nooch.com", "7UehAJkEBJJas0EpQKWppQ");
                smtp.EnableSsl = false;

                mailMessage.From = new MailAddress(fromAddress, fromAddress);

                mailMessage.Priority = MailPriority.High;
                smtp.Send(mailMessage);
                mailMessage.Dispose();

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Landlord CommonHelper -> SendEmail ERROR -> [Template: " + templateName + "], " +
                                       "[ToAddress: " + toAddress + "],  [Exception: " + ex + "]");

                return false;
            }
        }


        public static string IsDuplicateMember(string userName)
        {
            var userNameLowerCase = CommonHelper.GetEncryptedData(userName.ToLower());

            using (var noochConnection = new NOOCHEntities())
            {
                var noochMember = (from c in noochConnection.Members where c.UserName == userNameLowerCase && c.IsDeleted == false select c).FirstOrDefault();

                if (noochMember != null)
                {
                    return "Username already exists for the primary email you entered. Please try with some other email.";
                }

                return "Not a nooch member.";
            }
        }


        public static string SendSMS(string phoneto, string msg, string memberId)
        {
            try
            {
                string AccountSid = ConfigurationSettings.AppSettings["AccountSid"].ToString();
                string AuthToken = ConfigurationSettings.AppSettings["AuthToken"].ToString();
                string from = ConfigurationSettings.AppSettings["AccountPhone"].ToString();
                string to = "";

                if (!phoneto.Trim().Contains("+"))
                    to = GetValueFromConfig("SMSInternationalCode") + phoneto.Trim();
                else
                    to = phoneto.Trim();

                var client = new Twilio.TwilioRestClient(AccountSid, AuthToken);
                //var sms = client.SendSmsMessage(from, to, msg);
                var sms2 = client.SendMessage(from, to, msg);

                return sms2.Status;
            }
            catch (Exception ex)
            {
                Logger.Error("CommonHelper -> SEND SMS FAILED - [To #: " + phoneto + "], [MemberID: " +
                                       memberId + "], [Exception: " + ex.InnerException + "]");
            }
            return "Failure";
        }


        public static string ResendVerificationSMS(string Username)
        {
            //1. Check if user exists
            //2. Check if phone is already verified

            bool IsValidMember = false;
            string s = IsDuplicateMember(Username);

            if (s != "Not a nooch member.")
            {
                // member exists, now check if Phone is already verified
                IsValidMember = true;
                // checking if phone is already activated
                // getting MemberId
                Username = CommonHelper.GetEncryptedData(Username);

                using (var noochConnection = new NOOCHEntities())
                {
                    var memberEntity = (from c in noochConnection.Members
                                        where c.UserName == Username && c.IsDeleted == false
                                        select c).FirstOrDefault();

                    if (memberEntity != null)
                    {
                        if (memberEntity.Status == "Temporarily_Blocked" || memberEntity.Status == "Suspended")
                        {
                            return memberEntity.Status;
                        }

                        else if (memberEntity.Status == "Active" || memberEntity.Status == "Registered")
                        {
                            string msg = "Reply with 'GO' to this message to confirm your mobile number.";

                            if (memberEntity.ContactNumber != null &&
                                memberEntity.IsVerifiedPhone != true)
                            {
                                string result = SendSMS(memberEntity.ContactNumber, msg, memberEntity.MemberId.ToString());
                                return result;
                            }
                            else if (memberEntity.ContactNumber != null &&
                                     memberEntity.IsVerifiedPhone == true)
                            {
                                return "Already Verified.";
                            }
                            else
                            {
                                return "Failure";
                            }
                        }
                        else
                        {
                            return "Failure";
                        }
                    }
                    else
                    {
                        return "Not a nooch member.";
                    }
                }
            }
            else
            {
                // Member doesn't exists
                return "Not a nooch member.";
            }
        }


        public static string GetMemberNameByUserName(string userName)
        {
            var userNameLowerCase = CommonHelper.GetEncryptedData(userName.ToLower());
            userName = CommonHelper.GetEncryptedData(userName);

            using (var noochConnection = new NOOCHEntities())
            {
                var noochMember = (from c in noochConnection.Members where c.UserName == userName && c.IsDeleted == false select c).FirstOrDefault();

                if (noochMember != null)
                {
                    return CommonHelper.UppercaseFirst((CommonHelper.GetDecryptedData(noochMember.FirstName))) + " "
                           + CommonHelper.UppercaseFirst((CommonHelper.GetDecryptedData(noochMember.LastName)));
                }
            }
            return null;
        }


        public static string ResendVerificationLink(string Username)
        {
            //1. Check if user exists or not
            //2. Check if already verified or not

            string s = IsDuplicateMember(Username);
            if (s != "Not a nooch member.")
            {
                // getting MemberId
                Member mem = getMemberByEmailId(Username);
                string MemberId = mem.MemberId.ToString();

                var userNameLowerCase = CommonHelper.GetEncryptedData(Username.ToLower());
                using (var noochConnection = new NOOCHEntities())
                {
                    // member exists check for already activated or not
                    Guid MemId = mem.MemberId;

                    var noochMember = (from c in noochConnection.AuthenticationTokens where c.IsActivated == false && c.MemberId == MemId select c).FirstOrDefault();

                    if (noochMember != null)
                    {
                        // send registration email to member with autogenerated token 
                        var link = String.Concat(GetValueFromConfig("ApplicationURL"),
                            "/Registration/Activation.aspx?tokenId=" + noochMember.TokenId);
                        string MemberName = GetMemberNameByUserName(Username);

                        var fromAddress = GetValueFromConfig("welcomeMail");
                        // Add any tokens you want to find/replace within your template file
                        var tokens = new Dictionary<string, string>
                        {
                            {Constants.PLACEHOLDER_FIRST_NAME, MemberName},
                            {Constants.PLACEHOLDER_LAST_NAME, ""},
                            {Constants.PLACEHOLDER_OTHER_LINK, link}
                        };
                        try
                        {
                            SendEmail(Constants.TEMPLATE_REGISTRATION,
                                fromAddress, Username,
                                "Confirm Nooch Registration", tokens, null);
                            return "Success";
                        }
                        catch (Exception)
                        {
                            Logger.Error("CommonHelper -> ResendVerificationLink - Member activation email not sent to [" +
                                                   Username + "].");
                            return "Failure";
                        }
                    }
                    else
                    {
                        // already activated send failure
                        return "Already Activated.";
                    }
                }

            }
            else
            {
                // Member doesn't exist
                return "Not a nooch member.";
            }
        }
    }


    //All utility classes goes here--------------------------------XXXXXXXXXXXXXXXXXXXXXXXXX-------------------------

    public class AccessTokenValidationOutput
    {
        public bool IsTokenOk { get; set; }
        public bool IsTokenUpdated { get; set; }
        public string AccessToken { get; set; }
        public string ErrorMessage { get; set; }
    }
    //All utility classes up there----------------------------------XXXXXXXXXXXXXXXXXXXXXXXXX------------------------
}