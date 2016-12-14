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
using System.Text;
using System.Web.Hosting;
using LanLordlAPIs.Models.Input_Models;
using LanLordlAPIs.Models.Output_Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LanLordlAPIs.Classes.Utility
{
    public class CommonHelper
    {
        private static NOOCHEntities _dbContext = null;

        static CommonHelper()
        {
            _dbContext = new NOOCHEntities();
        }
        public static SynapseDetailsClass GetSynapseBankAndUserDetailsforGivenMemberId(string memberId)
        {
            SynapseDetailsClass res = new SynapseDetailsClass();

            try
            {
                var id = ConvertToGuid(memberId);

                using (NOOCHEntities noochConnection = new NOOCHEntities())
                {
                    // checking user details for given id
                    // Full Member Table Details
                    Member memberObject = GetMemberDetails(memberId);


                    var createSynapseUserObj = (from c in noochConnection.SynapseCreateUserResults
                                                where c.MemberId == id &&
                                                      c.IsDeleted == false &&
                                                      c.success != null
                                                select c).FirstOrDefault();

                    if (createSynapseUserObj != null)
                    {
                        // This MemberId was found in the SynapseCreateUserResults DB
                        res.wereUserDetailsFound = true;
                        Logger.Info("Common Helper -> GetSynapseBankAndUserDetailsforGivenMemberId - Checkpoint #1 - " +
                                "SynapseCreateUserResults Record Found! - Now about to check if Synapse OAuth Key is expired or still valid.");

                        #region Check If OAuth Key Still Valid
                        // CLIFF (10/3/15): ADDING CALL TO NEW METHOD TO CHECK USER'S STATUS WITH SYNAPSE, AND REFRESHING OAUTH KEY IF NECESSARY

                        synapseV3checkUsersOauthKey checkTokenResult = refreshSynapseV3OautKey(createSynapseUserObj.access_token);

                        if (checkTokenResult != null)
                        {
                            if (checkTokenResult.success == true)
                            {
                                res.UserDetails = new SynapseDetailsClass_UserDetails();
                                res.UserDetails.MemberId = memberId;
                                res.UserDetails.access_token = (checkTokenResult.oauth_consumer_key);  // Note: Giving in encrypted format
                                res.UserDetails.user_id = checkTokenResult.user_oid;
                                res.UserDetails.user_fingerprints = memberObject.UDID1;
                                res.UserDetails.permission = createSynapseUserObj.permission;
                                res.UserDetailsErrMessage = "OK";
                            }
                            else
                            {
                                Logger.Error("Common Helper -> GetSynapseBankAndUserDetailsforGivenMemberId FAILED on Checking User's Synapse OAuth Token - " +
                                             "CheckTokenResult.msg: [" + checkTokenResult.msg + "], MemberID: [" + memberId + "]");

                                res.UserDetailsErrMessage = checkTokenResult.msg;
                                return res;
                            }
                        }
                        else
                        {
                            Logger.Error("Common Helper -> GetSynapseBankAndUserDetailsforGivenMemberId FAILED on Checking User's Synapse OAuth Token - " +
                                         "CheckTokenResult was NULL, MemberID: [" + memberId + "]");

                            res.UserDetailsErrMessage = "Unable to check user's Oauth Token";
                        }

                        #endregion Check If OAuth Key Still Valid
                    }
                    else
                    {
                        res.wereUserDetailsFound = false;
                        res.UserDetails = null;
                        res.UserDetailsErrMessage = "User synapse details not found.";
                    }

                    // Now get the user's bank account details
                    var defaultBank = (from c in noochConnection.SynapseBanksOfMembers
                                       where c.MemberId == id && c.IsDefault == true
                                       select c).FirstOrDefault();

                    if (defaultBank != null)
                    {
                        // Found a Synapse bank account for this user
                        res.wereBankDetailsFound = true;
                        res.BankDetails = defaultBank;
                        res.AccountDetailsErrMessage = "OK";
                    }
                    else
                    {
                        res.wereBankDetailsFound = false;
                        res.BankDetails = null;
                        res.AccountDetailsErrMessage = "User synapse bank not found.";
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Landlords WEB API -> GetSynapseBankAndUserDetailsforGivenMemberId FAILED - [MemberID: " + memberId + "], [Exception: " + ex + "]");
            }

            return res;
        }

        // oAuth token needs to be in encrypted format
        public static synapseV3checkUsersOauthKey refreshSynapseV3OautKey(string oauthKey)
        {
            Logger.Info("Common Helper -> refreshSynapseV3OautKey Initiated - User's Original OAuth Key (enc): [" + oauthKey + "]");

            synapseV3checkUsersOauthKey res = new synapseV3checkUsersOauthKey();
            res.success = false;

            try
            {
                SynapseCreateUserResult synCreateUserObject = _dbContext.SynapseCreateUserResults.FirstOrDefault(m => m.access_token == oauthKey && m.IsDeleted == false);

                // Will be calling login/refresh access token service to confirm if saved oAtuh token matches with token coming in response, if not then will update the token.
                if (synCreateUserObject != null)
                {
                    _dbContext.Entry(synCreateUserObject).Reload();

                    var noochMemberObject = GetMemberDetails(synCreateUserObject.MemberId.ToString());

                    #region Found Refresh Token

                    //Logger.Info("Common Helper -> refreshSynapseV3OautKey - Found Member By Original OAuth Key");

                    SynapseV3RefreshOauthKeyAndSign_Input input = new SynapseV3RefreshOauthKeyAndSign_Input();

                    List<string> clientIds = getClientSecretId(noochMemberObject.MemberId.ToString());

                    string SynapseClientId = clientIds[0];
                    string SynapseClientSecret = clientIds[1];

                    input.login = new createUser_login2()
                    {
                        email = GetDecryptedData(noochMemberObject.UserName),
                        refresh_token = GetDecryptedData(synCreateUserObject.refresh_token)
                    };

                    input.client = new createUser_client()
                    {
                        client_id = SynapseClientId,
                        client_secret = SynapseClientSecret
                    };

                    SynapseV3RefreshOAuthToken_User_Input user = new SynapseV3RefreshOAuthToken_User_Input();

                    user._id = new synapseSearchUserResponse_Id1()
                    {
                        oid = synCreateUserObject.user_id
                    };
                    user.fingerprint = noochMemberObject.UDID1;

                    user.ip = GetRecentOrDefaultIPOfMember(noochMemberObject.MemberId);

                    input.user = user;

                    string UrlToHit = Convert.ToBoolean(GetValueFromConfig("IsRunningOnSandBox")) ? "https://sandbox.synapsepay.com/api/v3/user/signin" : "https://synapsepay.com/api/v3/user/signin";

                    Logger.Info("Common Helper -> refreshSynapseV3OautKey - Payload to send to Synapse /v3/user/signin: [" + JsonConvert.SerializeObject(input) + "]");

                    var http = (HttpWebRequest)WebRequest.Create(new Uri(UrlToHit));
                    http.Accept = "application/json";
                    http.ContentType = "application/json";
                    http.Method = "POST";

                    string parsedContent = JsonConvert.SerializeObject(input);
                    ASCIIEncoding encoding = new ASCIIEncoding();
                    Byte[] bytes = encoding.GetBytes(parsedContent);

                    Stream newStream = http.GetRequestStream();
                    newStream.Write(bytes, 0, bytes.Length);
                    newStream.Close();

                    try
                    {
                        var response = http.GetResponse();
                        var stream = response.GetResponseStream();
                        var sr = new StreamReader(stream);
                        var content = sr.ReadToEnd();

                        synapseCreateUserV3Result_int refreshResultFromSyn = new synapseCreateUserV3Result_int();
                        refreshResultFromSyn = JsonConvert.DeserializeObject<synapseCreateUserV3Result_int>(content);

                        JObject refreshResponse = JObject.Parse(content);

                        //Logger.Info("Common Helper -> synapseV3checkUsersOauthKey - Just Parsed Synapse Response: [" + refreshResponse + "]");

                        #region Signed Into Synapse Successfully

                        if ((refreshResponse["success"] != null && Convert.ToBoolean(refreshResponse["success"])) ||
                             refreshResultFromSyn.success == true)
                        {
                            //Logger.Info("Common Helper -> synapseV3checkUsersOauthKey - Signed User In With Synapse Successfully!");

                            // Check if Token from Synapse /user/signin is same as the one we already have saved in DB for this suer
                            if (synCreateUserObject.access_token == GetEncryptedData(refreshResultFromSyn.oauth.oauth_key))
                            {
                                res.success = true;
                                Logger.Info("Common Helper -> refreshSynapseV3OautKey - Access_Token from Synapse MATCHES what we already had in DB.");
                            }
                            else // New Access Token...
                            {
                                Logger.Info("Common Helper -> refreshSynapseV3OautKey - Access_Token from Synapse MATCHES what we already had in DB.");
                            }

                            // Update all values no matter what, even if access_token hasn't changed - possible one of the other values did
                            synCreateUserObject.access_token = GetEncryptedData(refreshResultFromSyn.oauth.oauth_key);
                            synCreateUserObject.refresh_token = GetEncryptedData(refreshResultFromSyn.oauth.refresh_token);
                            synCreateUserObject.expires_in = refreshResultFromSyn.oauth.expires_in;
                            synCreateUserObject.expires_at = refreshResultFromSyn.oauth.expires_at;
                            synCreateUserObject.physical_doc = refreshResultFromSyn.user.doc_status != null ? refreshResultFromSyn.user.doc_status.physical_doc : null;
                            synCreateUserObject.virtual_doc = refreshResultFromSyn.user.doc_status != null ? refreshResultFromSyn.user.doc_status.virtual_doc : null;
                            synCreateUserObject.extra_security = refreshResultFromSyn.user.extra != null ? refreshResultFromSyn.user.extra.extra_security.ToString() : null;

                            if (!String.IsNullOrEmpty(refreshResultFromSyn.user.permission))
                            {
                                synCreateUserObject.permission = refreshResultFromSyn.user.permission;
                            }

                            int save = _dbContext.SaveChanges();
                            _dbContext.Entry(synCreateUserObject).Reload();

                            if (save > 0)
                            {
                                Logger.Info("Common Helper -> refreshSynapseV3OautKey - SUCCESS From Synapse and Successfully saved updates to Nooch DB.");

                                res.success = true;
                                res.oauth_consumer_key = synCreateUserObject.access_token;
                                res.oauth_refresh_token = synCreateUserObject.refresh_token;
                                res.user_oid = synCreateUserObject.user_id;
                                res.msg = "Oauth key refreshed successfully";
                            }
                            else
                            {
                                Logger.Error("Common Helper -> refreshSynapseV3OautKey FAILED - Error saving new key in Nooch DB - " +
                                             "Orig. Oauth Key: [" + oauthKey + "], " +
                                             "Refreshed OAuth Key: [" + synCreateUserObject.access_token + "]");
                                res.msg = "Failed to save new OAuth key in Nooch DB.";
                            }
                        }

                        #endregion Signed Into Synapse Successfully

                        else if (refreshResponse["success"] != null && Convert.ToBoolean(refreshResponse["success"]) == false)
                        {
                            // Error returned from Synapse, but not a 400 HTTP Code error (probably will be a 202 Code). Example:
                            /* {"error": {
                                  "en": "Fingerprint not verified. Please verify fingerprint via 2FA."
                                },
                                "error_code": "10",
                                "http_code": "202",
                                "phone_numbers": [
                                  "3133339465"
                                ],
                                "success": false
                              }*/
                            if (refreshResponse["error"] != null && refreshResponse["error"]["en"] != null)
                            {
                                res.msg = refreshResponse["error"]["en"].ToString();

                                Logger.Error("Common Helper -> refreshSynapseV3OautKey FAILED - Synapse Error Msg: [" + res.msg + "]");

                                if (res.msg.ToLower().IndexOf("fingerprint not verified") > -1)
                                {
                                    // USER'S FINGERPRINT MUST HAVE CHANGED SINCE THE USER WAS ORIGINALLY CREATED (WHICH IS BAD AND UNLIKELY, BUT STILL POSSIBLE)
                                    // NEED TO CALL THE NEW SERVICE FOR HANDLING Synapse 2FA and generating a new Fingerprint (NOT BUILT YET)

                                    // Make sure the Phone # given by Synapse matches what we have for this user in the DB
                                    if (refreshResponse["phone_numbers"][0] != null)
                                    {
                                        var synapsePhone = RemovePhoneNumberFormatting(refreshResponse["phone_numbers"][0].ToString());
                                        var usersPhoneinDB = RemovePhoneNumberFormatting(noochMemberObject.ContactNumber);

                                        if (synapsePhone == usersPhoneinDB)
                                        {
                                            // Good, phone #'s matched - proceed with 2FA process
                                            Logger.Info("Common Helper -> refreshSynapseV3OautKey - About to attempt 2FA process by querying SynapseV3SignIn()");

                                            // Return response from 2nd Signin attempt w/ phone number (should trigger Synapse to send a PIN to the user)
                                            return SynapseV3SignIn(oauthKey, noochMemberObject, null);
                                        }
                                        else
                                        {
                                            // Bad - Synapse has a different phone # than we do for this user,
                                            // which means it probably changed since we created the user with Synapse...
                                            res.msg = "Phone number from Synapse doesn't match Nooch phone number";
                                            Logger.Error("Common Helper -> refreshSynapseV3OautKey FAILED - Phone # Array returned from Synapse - " +
                                                         "But didn't match user's ContactNumber in DB - Can't attempt 2FA flow - ABORTING");
                                        }
                                    }
                                    else
                                    {
                                        res.msg = "Phone number not found from synapse";
                                        Logger.Error("Common Helper -> refreshSynapseV3OautKey FAILED - No Phone # Array returned from Synapse - " +
                                                     "Can't attempt 2FA flow - ABORTING");
                                    }
                                }
                                else
                                {
                                    res.msg = "Error from Synapse, but didn't includ 'fingerprint not verified'";
                                    Logger.Error("Common Helper -> refreshSynapseV3OautKey FAILED - Synapse Returned Error other than - " +
                                                 "'fingerprint not verified' - Can't attempt 2FA flow - ABORTING");
                                }
                            }
                        }
                        else
                        {
                            Logger.Error("Common Helper -> refreshSynapseV3OautKey FAILED - Attempted to Sign user into Synapse, but got " +
                                         "error from Synapse service, no 'success' key found - Orig. Oauth Key: [" + oauthKey + "]");
                            res.msg = "Service error.";
                        }
                    }
                    catch (WebException we)
                    {
                        #region Synapse V3 Signin Exception

                        var httpStatusCode = ((HttpWebResponse)we.Response).StatusCode;
                        string http_code = httpStatusCode.ToString();

                        var response = new StreamReader(we.Response.GetResponseStream()).ReadToEnd();
                        JObject jsonFromSynapse = JObject.Parse(response);

                        var error_code = jsonFromSynapse["error_code"].ToString();
                        res.msg = jsonFromSynapse["error"]["en"].ToString();

                        if (!String.IsNullOrEmpty(error_code))
                        {
                            Logger.Error("Common Helper -> refreshSynapseV3OautKey FAILED (Exception)- [Synapse Error Code: " + error_code +
                                         "], [Error Msg: " + res.msg + "]");
                        }

                        if (!String.IsNullOrEmpty(res.msg))
                        {
                            Logger.Error("Common Helper -> synapseV3checkUsersOauthKey FAILED (Exception) - HTTP Code: [" + http_code +
                                         "], Error Msg: [" + res.msg + "]");
                        }
                        else
                        {
                            Logger.Error("Common Helper -> synapseV3checkUsersOauthKey FAILED (Exception) - Synapse Error msg was null or not found - [Original Oauth Key (enc): " +
                                         oauthKey + "], [Exception: " + we.Message + "]");
                        }

                        #endregion Synapse V3 Signin Exception
                    }

                    #endregion
                }
                else
                {
                    // no record found for given oAuth token in synapse createuser results table
                    Logger.Error("Common Helper -> refreshSynapseV3OautKey FAILED - no record found for given oAuth key found - " +
                                 "Orig. Oauth Key: (enc) [" + oauthKey + "]");
                    res.msg = "Service error - no record found for give OAuth Key.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Common Helper -> refreshSynapseV3OautKey FAILED: Outer Catch Error - Orig. OAuth Key (enc): [" + oauthKey +
                             "], [Exception: " + ex + "]");

                res.msg = "Nooch Server Error: Outer Exception #2326.";
            }

            return res;
        }

        // Method to change user fingerprint
        // this required user's member id and new fingerprint
        // from member id, we will get synapse id and password if any given
        // UPDATE (Cliff - 5/31/16): This will be almost exactly the same as the above refreshSynapseOautKey()
        //                           This will ONLY be called from that method when the 1st attempt at signing in fails.
        //                           When that happens, Synapse returns an array of phone numbers for that user (should only ever be 1 in our case),
        //                           then the user is supposed to "pick" which # to verify. But we'll skip that and assume it's the only 1 in the array.
        //                           So then we query the /user/signin API again, this time with the phone number included.  Synapse then sends a code to the user's phone via SMS.
        //                           Then the user must enter that code (I'll make the interface) and we submit it to Synapse using the same API: /user/signin.
        // oAuth token needs to be in encrypted format
        public static synapseV3checkUsersOauthKey SynapseV3SignIn(string oauthKey, Member memberObj, string validationPin)
        {
            bool isPinIncluded = false;
            if (String.IsNullOrEmpty(validationPin))
            {
                Logger.Info("Common Helper -> SynapseV3SignIn Initiated - Oauth Key (enc): [" + oauthKey + "] - No ValidationPIN Passed.");
            }
            else
            {
                isPinIncluded = true;
                Logger.Info("Common Helper -> SynapseV3SignIn Initiated - Submitting Validation PIN: [" + validationPin + "], Oauth Key (enc): [" + oauthKey + "]");
            }

            synapseV3checkUsersOauthKey res = new synapseV3checkUsersOauthKey();
            res.success = false;
            res.is2FA = false;

            #region Initial Data Checks

            if (String.IsNullOrEmpty(oauthKey))
            {
                Logger.Error("Common Helper -> SynapseV3SignIn FAILED - Missing Oauth Key - Oauth Key: [" + oauthKey + "]");
                res.msg = "Missing Oauth Key";
                return res;
            }
            if (memberObj == null)
            {
                Logger.Error("Common Helper -> SynapseV3SignIn FAILED - Missing MemberObj - Oauth Key: [" + oauthKey + "]");
                res.msg = "Missing Member to Signin";
                return res;
            }
            else if (String.IsNullOrEmpty(memberObj.ContactNumber))
            {
                Logger.Error("Common Helper -> SynapseV3SignIn FAILED - No Phone Number Found for this User - MemberID: [" + memberObj.MemberId.ToString() + "]");
                res.msg = "User is Missing a Phone Number";
                return res;
            }

            #endregion Initial Data Checks

            try
            {
                SynapseCreateUserResult synCreateUserObject = _dbContext.SynapseCreateUserResults.FirstOrDefault(m => m.access_token == oauthKey && m.IsDeleted == false);

                if (synCreateUserObject != null)
                {
                    #region Found Synapse User In DB

                    _dbContext.Entry(synCreateUserObject).Reload();

                    Logger.Info("Common Helper -> SynapseV3SignIn - Found Member By Original OAuth Key");

                    List<string> clientIds = CommonHelper.getClientSecretId(memberObj.MemberId.ToString());

                    string SynapseClientId = clientIds[0];
                    string SynapseClientSecret = clientIds[1];

                    var client = new createUser_client()
                    {
                        client_id = SynapseClientId,
                        client_secret = SynapseClientSecret
                    };

                    var login = new createUser_login2()
                    {
                        email = GetDecryptedData(memberObj.UserName),
                        refresh_token = GetDecryptedData(synCreateUserObject.refresh_token)
                    };

                    // Cliff (5/31/16): Have to do it this way because using 1 class causes a problem with Synapse because
                    //                  it doesn't like a NULL value for Validation_PIN if it's not there.  Maybe I'm doing it wrong though...
                    var inputNoPin = new SynapseV3Signin_InputNoPin();
                    var inputWithPin = new SynapseV3Signin_InputWithPin();

                    if (isPinIncluded)
                    {
                        SynapseV3Signin_Input_UserWithPin user = new SynapseV3Signin_Input_UserWithPin();

                        user._id = new synapseSearchUserResponse_Id1()
                        {
                            oid = synCreateUserObject.user_id
                        };
                        user.fingerprint = memberObj.UDID1; // This would be the "new" fingerprint for the user - it's already been saved in the DB for this user
                        user.ip = GetRecentOrDefaultIPOfMember(memberObj.MemberId);
                        user.phone_number = RemovePhoneNumberFormatting(memberObj.ContactNumber); // Inluding the user's Phone #
                        user.validation_pin = validationPin;

                        inputWithPin.client = client;
                        inputWithPin.login = login;
                        inputWithPin.user = user;
                    }
                    else
                    {
                        SynapseV3Signin_Input_UserNoPin user = new SynapseV3Signin_Input_UserNoPin();

                        user._id = new synapseSearchUserResponse_Id1()
                        {
                            oid = synCreateUserObject.user_id
                        };
                        user.fingerprint = memberObj.UDID1; // This would be the "new" fingerprint for the user - it's already been saved in the DB for this user
                        user.ip = GetRecentOrDefaultIPOfMember(memberObj.MemberId);
                        user.phone_number = RemovePhoneNumberFormatting(memberObj.ContactNumber); // Inluding the user's Phone #

                        inputNoPin.user = user;
                        inputNoPin.client = client;
                        inputNoPin.login = login;
                    }

                    string UrlToHit = Convert.ToBoolean(GetValueFromConfig("IsRunningOnSandBox")) ? "https://sandbox.synapsepay.com/api/v3/user/signin" : "https://synapsepay.com/api/v3/user/signin";
                    string parsedContent = isPinIncluded ? JsonConvert.SerializeObject(inputWithPin) : JsonConvert.SerializeObject(inputNoPin);

                    Logger.Info("Common Helper -> SynapseV3SignIn - isPinIncluded: [" + isPinIncluded + "] - Payload to send to Synapse /v3/user/signin: [" + parsedContent + "]");

                    var http = (HttpWebRequest)WebRequest.Create(new Uri(UrlToHit));
                    http.Accept = "application/json";
                    http.ContentType = "application/json";
                    http.Method = "POST";

                    ASCIIEncoding encoding = new ASCIIEncoding();
                    Byte[] bytes = encoding.GetBytes(parsedContent);

                    Stream newStream = http.GetRequestStream();
                    newStream.Write(bytes, 0, bytes.Length);
                    newStream.Close();

                    try
                    {
                        var response = http.GetResponse();
                        var stream = response.GetResponseStream();
                        var sr = new StreamReader(stream);
                        var content = sr.ReadToEnd();

                        synapseCreateUserV3Result_int refreshResultFromSyn = new synapseCreateUserV3Result_int();
                        refreshResultFromSyn = JsonConvert.DeserializeObject<synapseCreateUserV3Result_int>(content);

                        JObject refreshResponse = JObject.Parse(content);

                        Logger.Info("Common Helper -> SynapseV3SignIn - Synapse Response: HTTP_CODE: [" + refreshResponse["http_code"] +
                                    "], Success: [" + refreshResponse["success"] + "]");

                        if ((refreshResponse["success"] != null && Convert.ToBoolean(refreshResponse["success"])) ||
                             refreshResultFromSyn.success.ToString() == "true")
                        {
                            Logger.Info("Common Helper -> SynapseV3SignIn - Signed User In With Synapse Successfully - Oauth Key: [" +
                                        oauthKey + "] - Checking Synapse Message...");

                            #region Response That PIN Was Sent To User's Phone

                            if (refreshResponse["message"] != null && refreshResponse["message"]["en"] != null)
                            {
                                res.msg = refreshResponse["message"]["en"].ToString();
                                res.is2FA = true;

                                Logger.Info("Common Helper -> SynapseV3SignIn - Synapse Message: [" + res.msg + "]");

                                if (res.msg.ToLower().IndexOf("validation pin sent") > -1)
                                {
                                    res.msg = "Validation PIN sent to: " + FormatPhoneNumber(memberObj.ContactNumber);
                                    res.success = true;
                                }

                                return res;
                            }

                            #endregion Response That PIN Was Sent To User's Phone


                            #region Response With Full Signin Information

                            // Check if Token from Synapse /user/signin is same as the one we already have saved in DB for this suer
                            if (synCreateUserObject.access_token == GetEncryptedData(refreshResultFromSyn.oauth.oauth_key))
                            {
                                res.success = true;
                                Logger.Info("Common Helper -> SynapseV3SignIn - Access_Token from Synapse MATCHES what we already had in DB.");
                            }
                            else // New Access Token...
                            {
                                Logger.Info("Common Helper -> SynapseV3SignIn - Access_Token from Synapse DOES NOT MATCH what we already had in DB, updating with New value.");
                            }

                            // Update all values no matter what, even if access_token hasn't changed - possible one of the other values did
                            synCreateUserObject.access_token = GetEncryptedData(refreshResultFromSyn.oauth.oauth_key);
                            synCreateUserObject.refresh_token = GetEncryptedData(refreshResultFromSyn.oauth.refresh_token);
                            synCreateUserObject.expires_in = refreshResultFromSyn.oauth.expires_in;
                            synCreateUserObject.expires_at = refreshResultFromSyn.oauth.expires_at;
                            synCreateUserObject.physical_doc = refreshResultFromSyn.user.doc_status != null ? refreshResultFromSyn.user.doc_status.physical_doc : null;
                            synCreateUserObject.virtual_doc = refreshResultFromSyn.user.doc_status != null ? refreshResultFromSyn.user.doc_status.virtual_doc : null;
                            synCreateUserObject.extra_security = refreshResultFromSyn.user.extra != null ? refreshResultFromSyn.user.extra.extra_security.ToString() : null;

                            if (!String.IsNullOrEmpty(refreshResultFromSyn.user.permission))
                            {
                                synCreateUserObject.permission = refreshResultFromSyn.user.permission;
                            }

                            int save = _dbContext.SaveChanges();
                            _dbContext.Entry(synCreateUserObject).Reload();

                            if (save > 0)
                            {
                                Logger.Info("Common Helper -> SynapseV3SignIn - SUCCESS From Synapse and Successfully saved updates to Nooch DB.");

                                res.success = true;
                                res.oauth_consumer_key = synCreateUserObject.access_token;
                                res.oauth_refresh_token = synCreateUserObject.refresh_token;
                                res.user_oid = synCreateUserObject.user_id;
                                res.msg = "Oauth key refreshed successfully";
                            }
                            else
                            {
                                Logger.Error("Common Helper -> SynapseV3SignIn FAILED - Error saving new key in Nooch DB - " +
                                             "Orig. Oauth Key: [" + oauthKey + "], " +
                                             "Refreshed OAuth Key: [" + synCreateUserObject.access_token + "]");
                                res.msg = "Failed to save new OAuth key in Nooch DB.";
                            }

                            #endregion Response With Full Signin Information
                        }
                        else if (refreshResponse["success"] != null && Convert.ToBoolean(refreshResponse["success"]) == false)
                        {
                            if (refreshResponse["error"] != null && refreshResponse["error"]["en"] != null)
                            {
                                res.msg = refreshResponse["error"]["en"].ToString();

                                Logger.Error("Common Helper -> SynapseV3SignIn FAILED - Synapse Error Msg: [" + res.msg + "]");

                                if (res.msg.ToLower().IndexOf("fingerprint not verified") > -1)
                                {
                                    // USER'S FINGERPRINT MUST HAVE CHANGED SINCE THE USER WAS ORIGINALLY CREATED (WHICH IS BAD AND UNLIKELY, BUT STILL POSSIBLE)
                                    // NEED TO CALL THE NEW SERVICE FOR HANDLING Synapse 2FA and generating a new Fingerprint (NOT BUILT YET)
                                }
                            }
                        }
                        else
                        {
                            Logger.Error("Common Helper -> SynapseV3SignIn FAILED - Attempted to Sign user into Synapse, but got " +
                                         "error from Synapse service, no 'success' key found - Orig. Oauth Key: [" + oauthKey + "]");
                            res.msg = "Service error.";
                        }
                    }
                    catch (WebException we)
                    {
                        #region Synapse V3 Signin Exception

                        var httpStatusCode = ((HttpWebResponse)we.Response).StatusCode;
                        string http_code = httpStatusCode.ToString();

                        var response = new StreamReader(we.Response.GetResponseStream()).ReadToEnd();
                        JObject jsonFromSynapse = JObject.Parse(response);

                        var error_code = jsonFromSynapse["error_code"].ToString();
                        res.msg = jsonFromSynapse["error"]["en"].ToString();

                        if (!String.IsNullOrEmpty(error_code))
                        {
                            Logger.Error("Common Helper -> SynapseV3SignIn FAILED (Exception)- [Synapse Error Code: " + error_code +
                                         "], [Error Msg: " + res.msg + "]");
                        }

                        if (!String.IsNullOrEmpty(res.msg))
                        {
                            Logger.Error("Common Helper -> SynapseV3SignIn FAILED (Exception) - HTTP Code: [" + http_code +
                                         "], Error Msg: [" + res.msg + "]");
                        }
                        else
                        {
                            Logger.Error("Common Helper -> SynapseV3SignIn FAILED (Exception) - Synapse Error msg was null or not found - [Original Oauth Key (enc): " +
                                         oauthKey + "], [Exception: " + we.Message + "]");
                        }

                        #endregion Synapse V3 Signin Exception
                    }

                    #endregion Found Synapse User In DB
                }
                else
                {
                    // No record found for given oAuth token in SynapseCreateUserResults table
                    Logger.Error("Common Helper -> SynapseV3SignIn FAILED - no record found for given oAuth key found - Orig. Oauth Key: (enc) [" + oauthKey + "]");
                    res.msg = "Service error - no record found for give OAuth Key.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Common Helper -> SynapseV3SignIn FAILED: Outer Catch Error - Orig. OAuth Key (enc): [" + oauthKey + "], [Exception: " + ex + "]");
                res.msg = "Nooch Server Error: Outer Exception #3330.";
            }

            return res;
        }



        public static Member GetMemberDetails(string memberId)
        {
            try
            {
                var id = ConvertToGuid(memberId);

                var noochMember = _dbContext.Members.FirstOrDefault(m => m.MemberId == id && m.IsDeleted == false);

                if (noochMember != null)
                {
                    _dbContext.Entry(noochMember).Reload();
                    return noochMember;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Common Helper -> GetMemberDetails FAILED - Member ID: [" + memberId + "], [Exception: " + ex + "]");
            }
            return new Member();
        }


        public static bool isOverTransactionLimit(decimal amount, string senderMemId, string recipMemId)
        {
            var maxTransferLimitPerPayment = GetValueFromConfig("MaximumTransferLimitPerTransaction");

            if (amount > Convert.ToDecimal(maxTransferLimitPerPayment))
            {
                if (senderMemId.ToLower() == "00bd3972-d900-429d-8a0d-28a5ac4a75d7" || // TEAM NOOCH
                    recipMemId.ToLower() == "00bd3972-d900-429d-8a0d-28a5ac4a75d7")
                {
                    Logger.Info("*****  Landlords WEB API -> isOverTransactionLimit - Transaction for TEAM NOOCH, so allowing transaction - [Amount: $" + amount + "]  ****");
                    return false;
                }
                else if (senderMemId.ToLower() == "8b4b4983-f022-4289-ba6e-48d5affb5484" || // Josh Detweiler (AppJaxx)
                         recipMemId.ToLower() == "8b4b4983-f022-4289-ba6e-48d5affb5484")
                {
                    Logger.Info("*****  Landlords WEB API -> isOverTransactionLimit - Transaction is for APPJAXX, so allowing transaction - [Amount: $" + amount + "]  ****");
                    return false;
                }

                return true;
            }

            return false;
        }


        public static string GetRandomTransactionTrackingId()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            int j = 1;

            using (NOOCHEntities obj = new NOOCHEntities())
            {
                for (int i = 0; i <= j; i++)
                {
                    var randomId = new string(Enumerable.Repeat(chars, 9)
                                             .Select(s => s[random.Next(s.Length)])
                                             .ToArray());

                    var memberEntity = getTransactionByTrackingId(randomId);

                    if (memberEntity == null)
                        return randomId;

                    j += i + 1;
                }
            }

            return null;
        }


        public static Transaction getTransactionByTrackingId(string transTrackId)
        {
            using (NOOCHEntities obj = new NOOCHEntities())
            {
                var memDetails = (from c in obj.Transactions
                                  where c.TransactionTrackingId == transTrackId
                                  select c).SingleOrDefault();
                return memDetails;
            }
        }


        public static List<string> getClientSecretId(string memId)
        {
            List<string> clientIds = new List<string>();
            try
            {
                Member member = GetMemberDetails(memId);

                string SynapseClientId = GetValueFromConfig("SynapseClientId");
                string SynapseClientSecret = GetValueFromConfig("SynapseClientSecret");
                clientIds.Add(SynapseClientId);
                clientIds.Add(SynapseClientSecret);
            }
            catch (Exception ex)
            {
                Logger.Error("Common Helper -> getClientSecretId FAILED - MemberID: [" + memId + "], Exception: [" + ex + "]");
            }

            return clientIds;
        }


        public static string GetEncryptedData(string sourceData)
        {
            try
            {
                var aesAlgorithm = new AES();
                var encryptedData = aesAlgorithm.Encrypt(sourceData, string.Empty);
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
                var decryptedData = aesAlgorithm.Decrypt(sourceData.Replace(" ", "+"), string.Empty);
                return decryptedData;
            }
            catch (Exception ex)
            {
                Logger.Error("Landlord Common Helper -> GetDecryptedData FAILED - [SourceData: " + sourceData + ", [Exception: " + ex + "]");
            }
            return string.Empty;
        }


        public static string UppercaseFirst(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            // Return char and concat substring.
            return char.ToUpper(s[0]) + s.Substring(1);
        }


        public static string GetEmailTemplate(string physicalPath)
        {
            using (var sr = new StreamReader(physicalPath))
                return sr.ReadToEnd();
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
                        return randomId;

                    j += i + 1;
                }
            }

            return null;
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


        public static string GetValueFromConfig(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }


        public static Member GetMemberByEmailId(string eMailID)
        {
            Member memberObj = new Member();

            try
            {
                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    var email = eMailID.Trim().ToLower();
                    email = CommonHelper.GetEncryptedData(email);

                    memberObj = (from c in obj.Members
                                 where (c.UserName == email || c.UserNameLowerCase == email) &&
                                        c.IsDeleted == false
                                 select c).SingleOrDefault();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("CommonHelper -> getMemberByEmailId FAILED - Exception: [" + ex + "]");
            }

            return memberObj;
        }


        public static Member GetMemberByMemberId(Guid memberId)
        {
            Member memberObj = new Member();

            try
            {
                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    memberObj = (from c in obj.Members
                                 where c.MemberId == memberId &&
                                        c.IsDeleted == false
                                 select c).SingleOrDefault();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("CommonHelper -> GetMemberByMemberId FAILED - Exception: [" + ex + "]");
            }

            return memberObj;
        }


        public static Member getMemberByNoochId(string NoochId)
        {
            Member memberObj = new Member();

            try
            {
                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    memberObj = (from c in obj.Members
                                 where c.Nooch_ID == NoochId
                                 select c).SingleOrDefault();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("CommonHelper -> getMemberByNoochId FAILED - Exception: [" + ex + "]");
            }

            return memberObj;
        }


        public static Landlord GetLandlordByLandlordId(Guid landlordId)
        {
            Landlord landlordObj = new Landlord();

            try
            {
                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    landlordObj = (from c in obj.Landlords
                                   where c.LandlordId == landlordId &&
                                          c.IsDeleted == false
                                   select c).SingleOrDefault();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("CommonHelper -> GetLandlordByLandlordId FAILED - Exception: [" + ex + "]");
            }

            return landlordObj;
        }


        public static Tenant GetTenantByTenantId(Guid tenantId)
        {
            Tenant landlordObj = new Tenant();

            try
            {
                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    landlordObj = (from c in obj.Tenants
                                   where c.TenantId == tenantId &&
                                          c.IsDeleted == false
                                   select c).SingleOrDefault();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("CommonHelper -> GetLandlordByLandlordId FAILED - Exception: [" + ex + "]");
            }

            return landlordObj;
        }


        public static string GetLandlordsMemberIdFromLandlordId(Guid landlorID)
        {
            var result = string.Empty;

            try
            {
                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    result = (from c in obj.Landlords
                              where c.LandlordId == landlorID
                              select c.MemberId).SingleOrDefault().ToString();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("CommonHelper -> GetLandlordsMemberIdFromLandlordId EXCEPTION - [" + ex + "]");
            }

            return result;
        }

        
        public static string GetTenantsMemberIdFromTenantId(string tenantId)
        {
            var result = string.Empty;

            try
            {
                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    result = (from c in obj.Tenants
                              where c.TenantId == new Guid(tenantId)
                              select c.MemberId).SingleOrDefault().ToString();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("CommonHelper -> GetTenantsMemberIdFromTenantId FAILED - [TenantID: " + tenantId + "], [Exception: " + ex + "]");
            }

            return result;
        }


        public static string GetMemberNameByUserName(string userName)
        {
            try
            {
                var userNameLowerCase = CommonHelper.GetEncryptedData(userName.ToLower());
                userName = CommonHelper.GetEncryptedData(userName);

                using (var obj = new NOOCHEntities())
                {
                    var memberObj = (from c in obj.Members
                                     where c.UserName == userName && c.IsDeleted == false
                                     select c).FirstOrDefault();

                    if (memberObj != null)
                    {
                        return CommonHelper.UppercaseFirst((CommonHelper.GetDecryptedData(memberObj.FirstName))) + " " +
                               CommonHelper.UppercaseFirst((CommonHelper.GetDecryptedData(memberObj.LastName)));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("CommonHelper -> GetMemberNameByUserName EXCEPTION - [UserName: " + userName + "], [Exception: " + ex + "]");
            }

            return null;
        }

 
        public static Property GetPropertyByPropId(Guid propId)
        {
            Property prop = new Property();

            try
            {
                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    prop = (from p in obj.Properties
                            where p.PropertyId == propId && p.IsDeleted == false
                            select p).SingleOrDefault();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("CommonHelper -> GetPropertyByPropId EXCEPTION - [" + ex + "]");
            }

            return prop;
        }


        public static PropertyUnit GetUnitByUnitId(string unitId)
        {
            Guid unitGuid = new Guid(unitId);

            PropertyUnit unit = new PropertyUnit();

            try
            {
                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    unit = (from u in obj.PropertyUnits
                            where u.UnitId == unitGuid && u.IsDeleted == false
                            select u).SingleOrDefault();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("CommonHelper -> GetPropertyByPropId EXCEPTION - [" + ex + "]");
            }

            return unit;
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
                            return inviteMember.code;
                        else
                            return "";
                    }
                    else //No referal code
                        return "";
                }
                else
                    return "Invalid";
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
                        var existing = getReferralCode(memberId.ToString());
                        if (existing == "")
                        {
                            //Generate random code
                            Random rng = new Random();
                            int value = rng.Next(1000);
                            var text = value.ToString("000");
                            var fName = GetDecryptedData(noochMember.FirstName);

                            // Make sure First name is at least 4 letters
                            if (fName.Length < 4)
                            {
                                var lname = CommonHelper.GetDecryptedData(noochMember.LastName);
                                fName = fName + lname.Substring(0, 4 - fName.Length).ToUpper();
                            }
                            var code = fName.Substring(0, 4).ToUpper() + text;

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
                            return "Invite Code Already Exists";
                    }
                    else
                        return "Invalid";
                }
                catch (Exception ex)
                {
                    Logger.Error("CommonHelper -> setReferralCode FAILED - MemberID: [" + memberId + "], Exception: [" + ex.Message + "]");
                    return "Error";
                }
            }
        }


        public static Landlord AddNewLandlordEntryInDb(string fName, string lName, string email, string pw, bool eMailSatusToSet, bool phoneStatusToSet, string ip, bool isBiz, Guid memberGuid)
        {
            try
            {
                Logger.Info("CommonHelper -> AddNewLandlordEntryInDb Fired - Name: [" + fName + " " + lName + "], Email: [" + email + "]");

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
                    ll.SubType = isBiz ? "Business" : "Basic";
                    ll.MemberId = memberGuid;
                    ll.IsDeleted = false;
                    ll.DateCreated = DateTime.Now;
                    ll.IpAddresses = ip;
                    ll.IsIdVerified = false;
                    ll.UserPic = "http://noochme.com/noochweb/Assets/Images/userpic-default.png";

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


        public static Tenant AddNewTenantRecordInDB(Guid guid, string fName, string lName, string email, bool isEmVer, DateTime? dob, string ssn, string address1, string city, string state, string zip, string phone, bool isPhVer, Guid memberId)
        {
            try
            {
                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    Tenant tenant = new Tenant
                    {
                        TenantId = guid,
                        MemberId = memberId,
                        FirstName = GetEncryptedData(fName.Trim().ToLower()),
                        LastName = GetEncryptedData(lName.Trim().ToLower()),
                        eMail = GetEncryptedData(email.Trim().ToLower()),
                        DateOfBirth = dob,
                        SSN = !String.IsNullOrEmpty(ssn) ? GetEncryptedData(ssn) : null,
                        AddressLineOne = !String.IsNullOrEmpty(address1) ? GetEncryptedData(ssn) : null,
                        City = !String.IsNullOrEmpty(city) ? GetEncryptedData(city) : null,
                        Zip = !String.IsNullOrEmpty(zip) ? GetEncryptedData(zip) : null,
                        State = !String.IsNullOrEmpty(state) ? GetEncryptedData(state) : null,
                        PhoneNumber = phone,
                        IsEmailVerified = isEmVer,
                        IsPhoneVerfied = isPhVer,
                        IsDeleted = false,
                        DateAdded = DateTime.Now,
                        IsAnyRentPaid = false,
                    };

                    obj.Tenants.Add(tenant);
                    obj.SaveChanges();

                    return tenant;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("CommonHelper -> AddNewTenantRecordInDB FAILED - [Exception: " + ex + "]");
                return null;
            }
        }


        public static Member AddNewMemberRecordInDB(Guid guid, string fName, string lName, string email)
        {
            try
            {
                string noochRandomId = GetRandomNoochId();

                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    var member = new Member
                    {
                        Nooch_ID = noochRandomId,
                        MemberId = guid,
                        FirstName = GetEncryptedData(fName.Trim()),
                        LastName = GetEncryptedData(lName.Trim()),
                        UserName = GetEncryptedData(email.Trim()),
                        UserNameLowerCase = GetEncryptedData(email.Trim().ToLower()),
                        SecondaryEmail = GetEncryptedData(email.Trim()),
                        RecoveryEmail = GetEncryptedData(email.Trim()),

                        Password = "",
                        PinNumber = GetRandomPinNumber(),
                        Status = "Invited",

                        IsDeleted = false,
                        DateCreated = DateTime.Now,
                        FacebookAccountLogin = null,
                        InviteCodeIdUsed = null,
                        Type = "Tenant",
                        IsVerifiedPhone = false,
                        IsVerifiedWithSynapse = false,
                    };

                    obj.Members.Add(member);
                    int saveNewMemberInDB = obj.SaveChanges();

                    #region Create Authentication Token

                    try
                    {
                        var tokenId = Guid.NewGuid();
                        var requestId = Guid.Empty;

                        var token = new AuthenticationToken
                        {
                            TokenId = tokenId,
                            MemberId = member.MemberId,
                            IsActivated = false,
                            DateGenerated = DateTime.Now,
                            FriendRequestId = requestId
                        };
                        // Save token details in Authentication Tokens DB table  
                        obj.AuthenticationTokens.Add(token);
                        obj.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("CommonHelper -> AddNewMemberRecordInDB -> Create Auth Token FAILED - [Exception: " + ex + "]");
                    }

                    #endregion Create Authentication Token


                    #region Notification Settings

                    try
                    {
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
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("CommonHelper -> AddNewMemberRecordInDB -> Create Notification Settings FAILED - [Exception: " + ex + "]");
                    }

                    #endregion Notification Settings


                    #region Privacy Settings

                    try
                    {
                        var memberPrivacySettings = new MemberPrivacySetting
                        {
                            MemberId = member.MemberId,
                            AllowSharing = true,
                            ShowInSearch = true,
                            DateCreated = DateTime.Now
                        };
                        obj.MemberPrivacySettings.Add(memberPrivacySettings);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("CommonHelper -> AddNewMemberRecordInDB -> Create Privacy Settings FAILED - [Exception: " + ex + "]");
                    }

                    #endregion Privacy Settings

                    if (saveNewMemberInDB > 0)
                    {
                        Logger.Info("CommonHelper -> AddNewMemberRecordInDB - New Member Record Created SUCCESSFULLY - [MemberID: " + guid.ToString() + "]");
                    }
                    else
                    {
                        Logger.Error("CommonHelper -> AddNewMemberRecordInDB - New Member Record Creation FAILED - [MemberID: " + guid.ToString() + "]");
                    }

                    return member;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("CommonHelper -> AddNewMemberRecordInDB FAILED - [Exception: " + ex + "]");
                return null;
            }
        }



        public static bool saveLandlordIp(Guid LandlordId, string IP)
        {
            try
            {
                //Logger.Info("CommonHelper -> saveLandlordIP Initiated - LandlordID: [" + LandlordId + "], IP: [" + IP + "]");

                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    var landlordObj = (from c in obj.Landlords
                                       where c.LandlordId == LandlordId
                                       select c).FirstOrDefault();

                    if (landlordObj == null) return false;

                    if (!String.IsNullOrEmpty(IP))
                    {
                        string IPsListPrepared = "";
                        //trying to split and see how many old ips we have
                        string[] existingIps = landlordObj.IpAddresses.Split(',');

                        if (existingIps.Length >= 5)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                if (i == 0)
                                {
                                    IPsListPrepared = existingIps[i];
                                }
                                else if (i == 4)
                                {
                                    IPsListPrepared = IPsListPrepared + ", " + existingIps[i];
                                    break;
                                }
                                else
                                {
                                    IPsListPrepared = IPsListPrepared + ", " + existingIps[i];
                                }
                            }
                            IPsListPrepared = IPsListPrepared + ", " + IP;
                        }
                        else
                        {
                            IPsListPrepared = landlordObj.IpAddresses + ", " + IP;
                        }

                        landlordObj.IpAddresses = IPsListPrepared;

                        landlordObj.DateModified = DateTime.Now;
                        obj.SaveChanges();

                        #region Update MembersIPAddress Table

                        try
                        {
                            var ipAddressesFound = (from c in obj.MembersIPAddresses
                                                    where c.MemberId == landlordObj.MemberId
                                                    select c).ToList();

                            if (ipAddressesFound.Count > 5)
                            {
                                // If there are already 5 entries, update the one added first (the oldest)
                                var lastIpFound = (from c in ipAddressesFound select c)
                                                  .OrderBy(m => m.ModifiedOn)
                                                  .Take(1)
                                                  .SingleOrDefault();

                                lastIpFound.ModifiedOn = DateTime.Now;
                                lastIpFound.Ip = IP;
                            }
                            else
                            {
                                // Otherwise, make a new entry

                                MembersIPAddress mip = new MembersIPAddress();
                                mip.MemberId = landlordObj.MemberId;
                                mip.ModifiedOn = DateTime.Now;
                                mip.Ip = IP;
                                obj.MembersIPAddresses.Add(mip);
                            }

                            int saveIpInDB = obj.SaveChanges();

                            if (saveIpInDB > 0)
                            {
                                Logger.Info("CommonHelper -> saveLandlordIp - Landlord's IP Address Updated in MembersIPAddress Table - LandlordID: [" + LandlordId +
                                            "], IP: [" + IP + "]");
                            }
                            else
                            {
                                Logger.Info("CommonHelper -> saveLandlordIp - FAILED Trying To Saving IP Address in MembersIPAddress - LandlordID: [" + LandlordId +
                                            "], IP: [" + IP + "]");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("CommonHelper -> saveLandlordIp FAILED For Saving IP Address - [Exception: " + ex + "]");
                        }

                        #endregion Update MembersIPAddress Table

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("CommonHelper -> saveLandlordIp - Error while updating IP address - [" + IP +
                             "] for LandlordID: [" + LandlordId + "], [Exception: " + ex + " ]");
            }

            return false;
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


        public static IsPhoneAlreadyRegistered IsPhoneNumberAlreadyRegistered(string PhoneNumberToSearch)
        {
            IsPhoneAlreadyRegistered res = new IsPhoneAlreadyRegistered();

            if (!String.IsNullOrEmpty(PhoneNumberToSearch))
            {
                string NumAltOne = "+" + PhoneNumberToSearch;
                string NumAltTwo = "+1" + PhoneNumberToSearch;
                string BlankNumCase = CommonHelper.GetEncryptedData(" ");

                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    var memberObj = (from c in obj.Members
                                     where (c.ContactNumber == PhoneNumberToSearch || c.ContactNumber == NumAltOne || c.ContactNumber == NumAltTwo) &&
                                            c.IsDeleted == false &&
                                            c.ContactNumber != BlankNumCase
                                     select c).FirstOrDefault();

                    if (memberObj == null)
                    {
                        res.isAlreadyRegistered = false;
                    }
                    else
                    {
                        res.memberMatched = memberObj;
                        res.isAlreadyRegistered = true;
                    }
                }
            }
            else
            {
                res.isAlreadyRegistered = false;
            }

            return res;
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



        public static CheckAndRegisterMemberByEmailResult CheckIfMemberExistsWithGivenEmailId(string email)
        {
            CheckAndRegisterMemberByEmailResult result = new CheckAndRegisterMemberByEmailResult();
            result.IsSuccess = false;

            try
            {
                if (String.IsNullOrEmpty(email))
                {
                    result.ErrorMessage = "Missing email to check!";
                    return result;
                }

                email = email.Trim().ToLower();
                email = CommonHelper.GetEncryptedData(email);

                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    var existingMemberDetails = (from c in obj.Members
                                                 where (c.UserName == email || c.UserNameLowerCase == email) && c.IsDeleted == false
                                                 select c).FirstOrDefault();

                    if (existingMemberDetails != null)
                    {
                        // user already exists
                        result.ErrorMessage = "OK";
                        result.MemberDetails = existingMemberDetails;
                    }
                    else
                    {
                        result.IsSuccess = true;
                        result.ErrorMessage = "No user found.";
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error("CommonHelper -> CheckIfMemberExistsWithGivenEmailId FAILED - [Email: " + email + "], [Exception: " + ex.Message + "]");
                result.ErrorMessage = "Server Error.";
                return result;
            }
        }


        public static CheckIfTenantExistsResult CheckIfTenantExistsWithGivenEmailId(string email)
        {
            CheckIfTenantExistsResult result = new CheckIfTenantExistsResult();
            result.IsSuccess = false;

            try
            {
                if (String.IsNullOrEmpty(email))
                {
                    result.ErrorMessage = "Missing email to check!";
                    return result;
                }

                email = email.Trim().ToLower();
                email = CommonHelper.GetEncryptedData(email);

                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    var existingMemberDetails = (from c in obj.Tenants
                                                 where c.eMail == email && c.IsDeleted == false
                                                 select c).FirstOrDefault();

                    if (existingMemberDetails != null)
                    {
                        // user already exists
                        result.ErrorMessage = "Tenant already exists";
                        result.TenantDetails = existingMemberDetails;
                    }
                    else
                    {
                        result.IsSuccess = true;
                        result.ErrorMessage = "No tenant found";
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error("CommonHelper -> CheckIfTenantExistsWithGivenEmailId FAILED - [Email: " + email + "], [Exception: " + ex.Message + "]");
                result.ErrorMessage = "Server Error.";
                return result;
            }
        }


        public static CheckAndRegisterLandlordByEmailResult checkIfLandlordExistsForGivenEmail(string email)
        {
            CheckAndRegisterLandlordByEmailResult result = new CheckAndRegisterLandlordByEmailResult();
            result.IsSuccess = false;

            try
            {
                email = email.Trim().ToLower();
                email = CommonHelper.GetEncryptedData(email);

                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    var existingMemberDetails = (from c in obj.Landlords
                                                 where c.eMail == email && c.IsDeleted == false
                                                 select c).FirstOrDefault();

                    if (existingMemberDetails != null)
                    {
                        result.ErrorMessage = "Landlord already exists with given eMail id.";
                        result.LanlordDetails = existingMemberDetails;
                    }
                    else
                    {
                        result.IsSuccess = true;
                        result.ErrorMessage = "No user found.";
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Landlord Common Helper -> checkAndRegisterLandlordByemailId FAIELD - [Exception: " + ex.Message + "], [Email: " + email + "]");
                result.ErrorMessage = "Server Error.";
            }

            return result;
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
                            "Nooch/ResetPassword?memberId=" + member.MemberId)
                            //"/ForgotPassword/ResetPasswordLandlords.aspx?memberId=" + member.MemberId) 
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

                return SendEmail(Constants.TEMPLATE_FORGOT_PASSWORD, fromAddress, null, primaryMail, "Reset your Nooch password", tokens, null, null);
            }
            catch (Exception ex)
            {
                Logger.Error("CommonHelper -> SendPasswordMail FAILED - [Exception: " + ex + "]");
                return false;
            }
        }


        public static bool SendEmail(string templateName, string fromAddress, string fromName, string toAddress, string subject, IEnumerable<KeyValuePair<string, string>> replacements, string bodyText, string bccEmail)
        {
            try
            {
                MailMessage mailMessage = new MailMessage();

                var subjectString = subject;
                var template = string.Empty; 
                var content = string.Empty;

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
                    mailMessage.Body = bodyText;

                if (!String.IsNullOrEmpty(fromAddress))
                {
                    switch (fromAddress)
                    {
                        case "receipts@nooch.com":
                            mailMessage.From = new MailAddress(fromAddress, "Nooch Receipts");
                            break;
                        case "support@nooch.com":
                            mailMessage.From = new MailAddress(fromAddress, "Nooch Support");
                            break;
                        default:
                            mailMessage.From = !String.IsNullOrEmpty(fromName)
                                               ? new MailAddress(fromAddress, fromName)
                                               : new MailAddress(fromAddress, "Team Nooch");
                            break;
                    }
                }
                else
                    mailMessage.From = new MailAddress("team@nooch.com", "Team Nooch");

                Logger.Info("CommonHelper -> SendEmail - DisplayName: [" + mailMessage.From.DisplayName.ToString() + "], Address: [" + mailMessage.From.Address.ToString() + "]");
                mailMessage.IsBodyHtml = true;
                mailMessage.Subject = subjectString;
                mailMessage.To.Add(toAddress);

                if (!String.IsNullOrEmpty(bccEmail))
                    mailMessage.Bcc.Add(bccEmail);

                SmtpClient smtp = new SmtpClient();

                smtp.Host = "smtp.mandrillapp.com";
                smtp.UseDefaultCredentials = false;
                smtp.Port = 587;// 25;

                smtp.Credentials = new NetworkCredential("cliff@nooch.com", "dxcLRQMoNNKoON8q8I1nqw");// "7UehAJkEBJJas0EpQKWppQ");
                smtp.EnableSsl = false;

                //mailMessage.From = new MailAddress(fromAddress, fromAddress);

                mailMessage.Priority = MailPriority.Normal;
                smtp.Send(mailMessage);
                mailMessage.Dispose();

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("CommonHelper -> SendEmail ERROR -> Template: [" + templateName + "], To: [" + toAddress + "], Exception: [" + ex + "]");

                return false;
            }
        }


        public static string IsDuplicateMember(string userName)
        {
            var userNameLowerCase = CommonHelper.GetEncryptedData(userName.ToLower());

            using (var noochConnection = new NOOCHEntities())
            {
                var noochMember = (from c in noochConnection.Members
                                   where c.UserName == userNameLowerCase && c.IsDeleted == false
                                   select c).FirstOrDefault();

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
                var sms2 = client.SendMessage(from, to, msg);

                return sms2.Status;
            }
            catch (Exception ex)
            {
                Logger.Error("CommonHelper -> SEND SMS FAILED - [To #: " + phoneto +
                             "], MemberID: [" + memberId +
                             "], [Exception: " + ex.InnerException + "]");
            }
            return "Failure";
        }


        public static string ResendVerificationSMS(string Username)
        {
            string s = IsDuplicateMember(Username);

            if (s != "Not a nooch member.")
            {
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


        public static string ResendVerificationLink(string Username)
        {
            string s = IsDuplicateMember(Username);

            if (s != "Not a nooch member.")
            {
                Member mem = GetMemberByEmailId(Username);
                string MemberId = mem.MemberId.ToString();

                var userNameLowerCase = CommonHelper.GetEncryptedData(Username.ToLower());

                using (var noochConnection = new NOOCHEntities())
                {
                    // Member exists, now check if already activated or not
                    Guid MemId = mem.MemberId;

                    var noochMember = (from c in noochConnection.AuthenticationTokens
                                       where c.IsActivated == false && c.MemberId == MemId
                                       select c).FirstOrDefault();

                    if (noochMember != null)
                    {
                        string fromAddress = GetValueFromConfig("welcomeMail");
                        string MemberName = GetMemberNameByUserName(Username);

                        // send registration email to member with autogenerated token 
                        var link = String.Concat(GetValueFromConfig("ApplicationURL"),
                            "Nooch/Activation?tokenId=" + noochMember.TokenId + "&type=ll&llem=" + Username);
                        //"Registration/Activation.aspx?tokenId=" + noochMember.TokenId + "&type=ll&llem=" + Username);


                        var tokens = new Dictionary<string, string>
                        {
                            {Constants.PLACEHOLDER_FIRST_NAME, MemberName},
                            {Constants.PLACEHOLDER_LAST_NAME, ""},
                            {Constants.PLACEHOLDER_OTHER_LINK, link}
                        };
                        try
                        {
                            SendEmail(Constants.TEMPLATE_REGISTRATION, fromAddress, null, Username,
                                      "Confirm Your Email on Nooch", tokens, null, null);

                            return "Success";
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("CommonHelper -> ResendVerificationLink - Member activation email not sent to [" +
                                          Username + "], [Exception: " + ex.Message + "]");
                            return "Failure";
                        }
                    }
                    else
                    {
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



        public static string GetRecentOrDefaultIPOfMember(Guid MemberIdPassed)
        {
            string RecentIpOfUser = "";
            using (var noochConnection = new NOOCHEntities())
            {


                var defaultIp =
                    (from c in noochConnection.MembersIPAddresses where c.MemberId == MemberIdPassed select c)
                        .OrderByDescending(m => m.ModifiedOn).FirstOrDefault();

                if (defaultIp != null)
                    RecentIpOfUser = defaultIp.Ip;
                else
                    RecentIpOfUser = "54.201.43.89";

            }
            return RecentIpOfUser;
        }

        public static void ResetSearchData()
        {
            SEARCHUSER_CURRENT_PAGE = 1;
            SEARCHUSER_TOTAL_PAGES_COUNT = 0;
            SEARCHED_USERS.Clear();
        }

        //Added these flags to keep track of pagination result being sent by synapse after hitting search url.
        static int SEARCHUSER_CURRENT_PAGE = 1;
        static int SEARCHUSER_TOTAL_PAGES_COUNT = 0;
        static List<synapseSearchUserResponse_User> SEARCHED_USERS = new List<synapseSearchUserResponse_User>();

        public static synapseSearchUserResponse getUserPermissionsForSynapseV3(string userEmail)
        {
            Logger.Info("CommonHelper -> getUserPermissionsForSynapseV3 Initiated - [Email: " + userEmail + "]");

            synapseSearchUserResponse res = new synapseSearchUserResponse();
            res.success = false;

            try
            {

                synapseSearchUserInputClass input = new synapseSearchUserInputClass();

                synapseSearchUser_Client client = new synapseSearchUser_Client();
                client.client_id = GetValueFromConfig("SynapseClientId");
                client.client_secret = GetValueFromConfig("SynapseClientSecret");

                synapseSearchUser_Filter filter = new synapseSearchUser_Filter();
                filter.page = SEARCHUSER_CURRENT_PAGE;
                filter.exact_match = true; // we might want to set this to false to prevent error due to capitalization mis-match... (or make sure we only send all lowercase email when creating a Synapse user)
                filter.query = userEmail;

                input.client = client;
                input.filter = filter;

                string UrlToHit = GetValueFromConfig("Synapse_Api_User_Search");

                Logger.Info("CommonHelper -> getUserPermissionsForSynapseV3 - About to query Synapse's /user/search API - UrlToHit: [" + UrlToHit + "]");

                var http = (HttpWebRequest)WebRequest.Create(new Uri(UrlToHit));
                http.Accept = "application/json";
                http.ContentType = "application/json";
                http.Method = "POST";

                string parsedContent = JsonConvert.SerializeObject(input);
                ASCIIEncoding encoding = new ASCIIEncoding();
                Byte[] bytes = encoding.GetBytes(parsedContent);

                Stream newStream = http.GetRequestStream();
                newStream.Write(bytes, 0, bytes.Length);
                newStream.Close();

                try
                {
                    var response = http.GetResponse();
                    var stream = response.GetResponseStream();
                    var sr = new StreamReader(stream);
                    var content = sr.ReadToEnd();

                    JObject checkPermissionResponse = JObject.Parse(content);

                    if (checkPermissionResponse["success"] != null &&
                        Convert.ToBoolean(checkPermissionResponse["success"]) == true)
                    {
                        //Logger.Info("CommonHelper -> getUserPermissionsForSynapseV3 - JSON Result from Synapse: [" + checkPermissionResponse + "]");
                        res = JsonConvert.DeserializeObject<synapseSearchUserResponse>(content);

                        if (res.page != res.page_count || res.page == res.page_count)
                        {
                            if (SEARCHUSER_CURRENT_PAGE == 1)
                            {
                                SEARCHED_USERS = res.users.ToList<synapseSearchUserResponse_User>();
                            }
                            else
                            {
                                List<synapseSearchUserResponse_User> temp = res.users.ToList<synapseSearchUserResponse_User>();
                                SEARCHED_USERS.AddRange(temp);
                            }

                            // Cliff (5/17/16): In theory SEACHUSER_CURRENT_PAGE and res.page should always be the same...
                            SEARCHUSER_CURRENT_PAGE = res.page + 1;
                            SEARCHUSER_TOTAL_PAGES_COUNT = res.page_count;

                            // If there are more pages left, loop back into this same method (I assume this is safe to do?)
                            if (res.page < res.page_count)
                                getUserPermissionsForSynapseV3(userEmail);
                        }
                    }
                    else
                    {
                        Logger.Error("Common Helper -> getUserPermissionsForSynapseV3 FAILED - Got response from Synapse /user/search, but 'success' was null or not 'true'");
                        res.error_code = "Service error.";
                    }
                }
                catch (WebException we)
                {
                    #region Synapse V3 Get User Permissions Exception

                    var httpStatusCode = ((HttpWebResponse)we.Response).StatusCode;
                    res.http_code = httpStatusCode.ToString();

                    var response = new StreamReader(we.Response.GetResponseStream()).ReadToEnd();
                    JObject errorJsonFromSynapse = JObject.Parse(response);

                    // CLIFF (10/10/15): Synapse lists all possible V3 error codes in the docs -> Introduction -> Errors
                    //                   We might have to do different things depending on which error is returned... for now just pass
                    //                   back the error number & msg to the function that called this method.
                    res.error_code = errorJsonFromSynapse["error_code"].ToString();
                    res.errorMsg = errorJsonFromSynapse["error"]["en"].ToString();

                    if (!String.IsNullOrEmpty(res.error_code))
                    {
                        Logger.Error("Common Helper -> getUserPermissionsForSynapseV3 FAILED - [Synapse Error Code: " + res.error_code +
                                     "], [Error Msg: " + res.errorMsg + "], [User Email: " + userEmail + "]");
                    }
                    else
                    {
                        Logger.Error("Common Helper -> getUserPermissionsForSynapseV3 FAILED: Synapse Error, but *error_code* was null for [User Email: " +
                                     userEmail + "], [Exception: " + we.InnerException + "]");
                    }

                    #endregion Synapse V3 Get User Permissions Exception
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Common Helper -> getUserPermissionsForSynapseV3 FAILED: Outer Catch Error - [User Email: " + userEmail +
                             "], [Exception: " + ex.InnerException + "]");

                res.error_code = "Nooch Server Error: Outer Exception.";
            }

            res.users = SEARCHED_USERS.ToArray();

            return res;
        }



        public static NodePermissionCheckResult IsNodeActiveInGivenSetOfNodes(synapseSearchUserResponse_Node[] allNodes, string nodeToMatch)
        {
            NodePermissionCheckResult res = new NodePermissionCheckResult();

            res.IsPermissionfound = false;

            foreach (synapseSearchUserResponse_Node node in allNodes)
            {
                if (node._id != null && node._id.oid == nodeToMatch || node._id.oid == CommonHelper.GetDecryptedData(nodeToMatch))
                {
                    if (!String.IsNullOrEmpty(node.allowed))
                    {
                        res.IsPermissionfound = true;
                        res.PermissionType = node.allowed;
                        break;
                    }
                }
            }

            return res;
        }


        public static SynapseV3AddTrans_ReusableClass AddTransSynapseV3Reusable(string sender_oauth, string sender_fingerPrint,
           string sender_bank_node_id, string amount, string fee, string receiver_oauth, string receiver_fingerprint,
           string receiver_bank_node_id, string suppID_or_transID, string senderUserName, string receiverUserName, string iPForTransaction, string senderLastName, string recepientLastName)
        {
            Logger.Info("Common Helper-> SynapseV3AddTrans_ReusableClass Initiated - [Sender Username: " + senderUserName + "], " +
                                   "[Recipient Username: " + receiverUserName + "], [Amount: " + amount + "]");

            SynapseV3AddTrans_ReusableClass res = new SynapseV3AddTrans_ReusableClass();
            res.success = false;

            try
            {
                bool SenderSynapsePermissionOK = false;
                bool RecipientSynapsePermissionOK = false;

                #region Check Sender Synapse Permissions

                // 1. Check USER permissions for SENDER
                synapseSearchUserResponse senderPermissions = getUserPermissionsForSynapseV3(senderUserName);

                if (senderPermissions == null || !senderPermissions.success)
                {
                    Logger.Error("Landlords API -> Common Helper -> SynapseV3AddTrans_ReusableClass - SENDER's Synapse Permissions were NULL or not successful :-(");

                    res.ErrorMessage = "Problem with senders synapse user permission.";
                    return res;
                }

                // 2. Check BANK/NODE permission for SENDER
                if (senderPermissions.users != null && senderPermissions.users.Length > 0)
                {
                    foreach (synapseSearchUserResponse_User senderUser in senderPermissions.users)
                    {
                        // iterating each node inside
                        if (senderUser.nodes != null && senderUser.nodes.Length > 0)
                        {
                            NodePermissionCheckResult nodePermCheckRes = IsNodeActiveInGivenSetOfNodes(senderUser.nodes, sender_bank_node_id);

                            if (nodePermCheckRes.IsPermissionfound == true)
                            {
                                if (nodePermCheckRes.PermissionType == "CREDIT-AND-DEBIT" || nodePermCheckRes.PermissionType == "DEBIT")
                                {
                                    SenderSynapsePermissionOK = true;
                                }
                                // iterate through all users
                                //else
                                //{
                                //    res.success = false;
                                //    res.ErrorMessage = "Sender doesn't have permission to send money from account.";
                                //    return res;
                                //}
                            }
                            // iterate through all users
                            //else
                            //{
                            //    res.success = false;
                            //    res.ErrorMessage = "Sender doesn't have permission to send money from account.";
                            //    return res;
                            //}
                        }
                    }
                }
                #endregion Check Sender Synapse Permissions

                #region Check Recipient Synapse Permissions
                ResetSearchData();
                // 3. Check USER permissions for RECIPIENT
                synapseSearchUserResponse recepientPermissions = getUserPermissionsForSynapseV3(receiverUserName);

                if (recepientPermissions == null || !recepientPermissions.success)
                {
                    Logger.Error("Landlords API -> Common Helper -> SynapseV3AddTrans_ReusableClass - RECIPIENT's Synapse Permissions were NULL or not successful :-(");

                    res.ErrorMessage = "Problem with recepient bank account permission.";
                    return res;
                }

                // 4. Check BANK/NODE permission for RECIPIENT
                if (recepientPermissions.users != null && recepientPermissions.users.Length > 0)
                {
                    foreach (synapseSearchUserResponse_User recUser in recepientPermissions.users)
                    {
                        // iterating each node inside
                        if (recUser.nodes != null && recUser.nodes.Length > 0)
                        {
                            NodePermissionCheckResult nodePermCheckRes = IsNodeActiveInGivenSetOfNodes(recUser.nodes, receiver_bank_node_id);

                            if (nodePermCheckRes.IsPermissionfound == true)
                            {
                                if (nodePermCheckRes.PermissionType == "CREDIT-AND-DEBIT" || nodePermCheckRes.PermissionType == "DEBIT")
                                {
                                    RecipientSynapsePermissionOK = true;
                                }
                                // iterate through all users
                                //else
                                //{
                                //    res.success = false;
                                //    res.ErrorMessage = "Sender doesn't have permission to send money from account.";
                                //    return res;
                                //}
                            }
                            // iterate through all users
                            //else
                            //{
                            //    res.success = false;
                            //    res.ErrorMessage = "Sender doesn't have permission to send money from account.";
                            //    return res;
                            //}
                        }
                    }
                }
                #endregion Check Recipient Synapse Permissions

                if (!SenderSynapsePermissionOK)
                {
                    res.ErrorMessage = "Sender bank permission problem.";
                    return res;
                }
                if (!RecipientSynapsePermissionOK)
                {
                    res.ErrorMessage = "Recipient bank permission problem.";
                    return res;
                }

                // all set...time to move money between accounts
                try
                {
                    #region Setup Synapse V3 Order Details

                    SynapseV3AddTransInput transParamsForSynapse = new SynapseV3AddTransInput();

                    SynapseV3Input_login login = new SynapseV3Input_login() { oauth_key = sender_oauth };
                    SynapseV3Input_user user = new SynapseV3Input_user() { fingerprint = sender_fingerPrint };
                    transParamsForSynapse.login = login;
                    transParamsForSynapse.user = user;

                    SynapseV3AddTransInput_trans transMain = new SynapseV3AddTransInput_trans();

                    SynapseV3AddTransInput_trans_from from = new SynapseV3AddTransInput_trans_from()
                    {
                        id = CommonHelper.GetDecryptedData(sender_bank_node_id),
                        type = "ACH-US"
                    };
                    SynapseV3AddTransInput_trans_to to = new SynapseV3AddTransInput_trans_to()
                    {
                        id = CommonHelper.GetDecryptedData(receiver_bank_node_id),
                        type = "ACH-US"
                    };
                    transMain.to = to;
                    transMain.from = from;

                    SynapseV3AddTransInput_trans_amount amountMain = new SynapseV3AddTransInput_trans_amount()
                    {
                        amount = Convert.ToDouble(amount),
                        currency = "USD"
                    };
                    transMain.amount = amountMain;
                    string webhooklink = GetValueFromConfig("NoochWebHookURL") + suppID_or_transID;

                    SynapseV3AddTransInput_trans_extra extraMain = new SynapseV3AddTransInput_trans_extra()
                    {
                        supp_id = suppID_or_transID,
                        // This is where we put the ACH memo (customized for Landlords, but just the same template for regular P2P transfers: "Nooch Payment {LNAME SENDER} / {LNAME RECIPIENT})
                        // maybe we should set this in whichever function calls this function because we don't have the names here...
                        // yes modifying this method to add 3 new parameters....sender IP, sender last name, recepient last name... this would be helpfull in keeping this method clean.
                        note = "NOOCH PAYMENT // " + senderLastName + " / " + recepientLastName, // + moneySenderLastName + " / " + requestMakerLastName, 
                        webhook = webhooklink,
                        process_on = 0, // this should be greater then 0 I guess... CLIFF: I don't think so, it's an optional parameter, but we always want it to process immediately, so I guess it should always be 0
                        ip = iPForTransaction // CLIFF:  This is actually required.  It should be the most recent IP address of the SENDER, or if none found, then '54.148.37.21'
                    };
                    transMain.extra = extraMain;

                    SynapseV3AddTransInput_trans_fees feeMain = new SynapseV3AddTransInput_trans_fees();

                    if (Convert.ToDouble(amount) > 10)
                    {
                        feeMain.fee = "0.20"; // to offset the Synapse fee so the user doesn't pay it
                    }
                    else if (Convert.ToDouble(amount) <= 10)
                    {
                        feeMain.fee = "0.10"; // to offset the Synapse fee so the user doesn't pay it
                    }
                    feeMain.note = "Negative Nooch Fee";

                    SynapseV3AddTransInput_trans_fees_to tomain = new SynapseV3AddTransInput_trans_fees_to()
                    {
                        id = "5618028c86c27347a1b3aa0f" // Temporary: ID of Nooch's SYNAPSE account (not bank account)... using temp Sandbox account until we get Production credentials
                    };

                    feeMain.to = tomain;
                    transMain.fees = new SynapseV3AddTransInput_trans_fees[1];
                    transMain.fees[0] = feeMain;

                    transParamsForSynapse.trans = transMain;

                    #endregion Setup Synapse V3 Order Details

                    #region Calling Synapse V3 TRANSACTION ADD

                    string UrlToHitV3 = GetValueFromConfig("Synapse_Api_Order_Add_V3");


                    try
                    {
                        // Calling Add Trans API

                        var http = (HttpWebRequest)WebRequest.Create(new Uri(UrlToHitV3));
                        http.Accept = "application/json";
                        http.ContentType = "application/json";
                        http.Method = "POST";

                        string parsedContent = JsonConvert.SerializeObject(transParamsForSynapse);
                        ASCIIEncoding encoding = new ASCIIEncoding();
                        Byte[] bytes = encoding.GetBytes(parsedContent);

                        Stream newStream = http.GetRequestStream();
                        newStream.Write(bytes, 0, bytes.Length);
                        newStream.Close();

                        var response = http.GetResponse();
                        var stream = response.GetResponseStream();
                        var sr = new StreamReader(stream);
                        var content = sr.ReadToEnd();

                        var synapseResponse = JsonConvert.DeserializeObject<SynapseV3AddTrans_Resp>(content);

                        if (synapseResponse.success == true ||
                            synapseResponse.success.ToString().ToLower() == "true")
                        {
                            res.success = true;
                            res.ErrorMessage = "OK";
                            try
                            {
                                // save changes into synapseTransactionResult table in db
                                SynapseAddTransactionResult satr = new SynapseAddTransactionResult();
                                satr.TransactionId = ConvertToGuid(suppID_or_transID);
                                satr.OidFromSynapse = synapseResponse.trans._id.oid.ToString();
                                satr.Status_DateTimeStamp = synapseResponse.trans.recent_status.date.date.ToString();
                                satr.Status_Id = synapseResponse.trans.recent_status.status_id;
                                satr.Status_Note = synapseResponse.trans.recent_status.note;
                                satr.Status_Text = synapseResponse.trans.recent_status.status;

                                _dbContext.SynapseAddTransactionResults.Add(satr);
                                _dbContext.SaveChanges();
                            }
                            catch (Exception ex)
                            {
                                Logger.Error("Landlords API -> Common Helper -> AddTransSynapseV3Reusable FAILED to save add transaction response into SynapseAddTransactionResult table. [Exception: " + ex.ToString() + "]");

                            }
                        }
                        else
                        {
                            res.success = false;
                            res.ErrorMessage = "Check synapse error.";
                        }
                        res.responseFromSynapse = synapseResponse;

                    }
                    catch (WebException ex)
                    {
                        var resp = new StreamReader(ex.Response.GetResponseStream()).ReadToEnd();
                        JObject jsonFromSynapse = JObject.Parse(resp);

                        Logger.Error("Landlords API -> Common Helper -> AddTransSynapseV3Reusable FAILED. [Exception: " + jsonFromSynapse.ToString() + "]");

                        JToken token = jsonFromSynapse["error"]["en"];

                        if (token != null)
                        {
                            res.ErrorMessage = token.ToString();
                        }
                        else
                        {
                            res.ErrorMessage = "Error occured in call money transfer service.";
                        }
                    }

                    #endregion Calling Synapse V3 TRANSACTION ADD
                }
                catch (Exception ex)
                {
                    Logger.Error("Landlords API -> Common Helper -> AddTransSynapseV3Reusable FAILED - Inner Exception: [Exception: " + ex + "]");
                    res.ErrorMessage = "Server Error - TDA Inner Exception";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Landlords API -> Common Helper -> AddTransSynapseV3Reusable FAILED - Outer Exception: [Exception: " + ex + "]");
                res.ErrorMessage = "TDA Outer Exception";
            }

            return res;
        }


        public static MemberNotification GetMemberNotificationSettings(string memberId)
        {
            Logger.Info("Landlords' API -> CommonHelper -> GetMemberNotificationSettings - [MemberId: " + memberId + "]");

            using (var noochConnection = new NOOCHEntities())
            {
                Guid memId = ConvertToGuid(memberId);

                var notifSettings = (from c in noochConnection.MemberNotifications
                                     where c.MemberId == memId
                                     select c)
                                     .FirstOrDefault();

                return notifSettings;
            }
        }

    }


    /*****************************/
    /****   Utility Classes   ****/
    /*****************************/
    public class AccessTokenValidationOutput
    {
        public bool IsTokenOk { get; set; }
        public bool IsTokenUpdated { get; set; }
        public string AccessToken { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class IsPhoneAlreadyRegistered
    {
        public bool isAlreadyRegistered { get; set; }
        public Member memberMatched { get; set; }
    }
}