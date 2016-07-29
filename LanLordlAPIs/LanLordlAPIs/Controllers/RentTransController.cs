using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Web.UI.WebControls;
using LanLordlAPIs.Classes.PushNotification;
using LanLordlAPIs.Classes.Utility;
using LanLordlAPIs.Models.db_Model;
using LanLordlAPIs.Models.Input_Models;
using LanLordlAPIs.Models.Output_Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LanLordlAPIs.Controllers
{
    public class RentTransController : ApiController
    {

        // Method for sending payment reminders
        public CreatePropertyResultOutput SendRentRemindersToTenants(ReminderMailInputClass input)
        {
            Logger.Info("Landlords API -> SendRentRemindersToTenants Initiated. TenantId: [" + input.Trans.TenantId +
                        "]. TransactionId: [" + input.Trans.TransactionId + "]. ReminderType: [" +
                        input.Trans.ReminderType + "]");
            CreatePropertyResultOutput result = new CreatePropertyResultOutput();
            result.IsSuccess = false;
            try
            {
                NOOCHEntities noochConnection = new NOOCHEntities();

                return result;

            }
            catch (Exception ex)
            {
                Logger.Error("Landlords API -> SendRentRemindersToTenants FAILED - Outer Exception - [" + ex + "]");
                result.ErrorMessage = "Error";
                return result;
            }
        }


        // Method for paying back to tenants
        [HttpPost]
        [ActionName("PayToTenants")]
        public CreatePropertyResultOutput PayToTenants(ChargeTenantInputClass input)
        {
            NOOCHEntities noochConn = new NOOCHEntities();
            DateTime TransDateTime = DateTime.Now;
            Logger.Info("Landlords API -> RentTrans -> ChargeTenant - Requested by [" + input.User.LandlordId + "]");

            CreatePropertyResultOutput result = new CreatePropertyResultOutput();
            result.IsSuccess = false;

            try
            {
                Guid Landlord_GUID = CommonHelper.ConvertToGuid(input.User.LandlordId);
                Guid Tenant_GUID = CommonHelper.ConvertToGuid(input.TransRequest.TenantId);
                Guid landlordsMemID = new Guid(CommonHelper.GetLandlordsMemberIdFromLandlordId(Landlord_GUID));
                Guid TenantMemID = new Guid(CommonHelper.GetTenantsMemberIdFromTenantId(Tenant_GUID.ToString()));

                string transactionId = "";

                #region All Checks Before Execution

                // Check uniqueness of requesting and sending user
                if (Landlord_GUID == Tenant_GUID)
                {
                    result.ErrorMessage = "Not allowed to send money to yourself.";
                    return result;
                }

                // Check if request Amount is over per-transaction limit
                decimal transactionAmount = Convert.ToDecimal(input.TransRequest.Amount);

                if (CommonHelper.isOverTransactionLimit(transactionAmount, input.User.LandlordId,
                    input.TransRequest.TenantId))
                {
                    result.ErrorMessage = "To keep Nooch safe, the maximum amount you can send is $" +
                                          Convert.ToDecimal(
                                              CommonHelper.GetValueFromConfig("MaximumTransferLimitPerTransaction"))
                                              .ToString("F2");
                    return result;
                }

                // Get sender and recepient Members table info
                var sender = CommonHelper.GetMemberByMemberId(landlordsMemID);
                var senderLandlordObj = CommonHelper.GetLandlordByLandlordId(Landlord_GUID);
                // Only need this for the Photo for the email template... Members table doesn't have it.
                //var moneyRecipient = CommonHelper.GetMemberByMemberId(Tenant_GUID);
                var moneyRecipient = CommonHelper.GetMemberByMemberId(TenantMemID);


                if (sender == null)
                {
                    Logger.Error(
                        "Landlords API -> RentTrans -> PayToTenants FAILED - Sender Member Not Found - [MemberID: " +
                        Landlord_GUID + "]");
                    result.ErrorMessage = "Sender Member Not Found";

                    return result;
                }
                if (moneyRecipient == null)
                {
                    Logger.Error(
                        "Landlords API -> RentTrans -> PayToTenants FAILED - moneyRecipient (who would receive the money) Member Not Found - [MemberID: " +
                        Tenant_GUID + "]");
                    result.ErrorMessage = "Money Recipient Member Not Found";

                    return result;
                }

                #region Get Money Sender's Synapse Account Details


                var senderSynInfo = CommonHelper.GetSynapseBankAndUserDetailsforGivenMemberId(sender.MemberId.ToString());

                if (senderSynInfo.wereBankDetailsFound != true)
                {
                    Logger.Error(
                        "Landlords API -> RentTrans -> PayToTenants FAILED -> Trans ABORTED: Sender's Synapse bank account NOT FOUND - Trans Creator MemberId is: [" +
                        landlordsMemID + "]");
                    result.ErrorMessage = "Requester does not have any bank added";

                    return result;
                }

                // Check Requestor's Synapse Bank Account status
                if (senderSynInfo.BankDetails != null &&
                    senderSynInfo.BankDetails.Status != "Verified" &&
                    sender.IsVerifiedWithSynapse != true)
                {
                    Logger.Error(
                        "Landlords API -> RentTrans -> PayToTenants FAILED -> Trans ABORTED: Sender's Synapse bank account exists but is not Verified and " +
                        "isVerifiedWithSynapse != true - Trans Creator memberId is: [" + landlordsMemID + "]");
                    result.ErrorMessage = "Sender does not have any verified bank account.";

                    return result;
                }

                #endregion Get Sender's Synapse Account Details


                #region Get Receiver's Synapse Account Details

                var moneyRecipientSynInfo =
                    CommonHelper.GetSynapseBankAndUserDetailsforGivenMemberId(moneyRecipient.MemberId.ToString());

                if (moneyRecipientSynInfo.wereBankDetailsFound != true)
                {
                    Logger.Error(
                        "Landlords API -> RentTrans -> PayToTenants FAILED -> Trans ABORTED: Money Recipient's Synapse bank account NOT FOUND - Money Recipient MemberID: [" +
                        input.TransRequest.TenantId + "]");
                    result.ErrorMessage = "Money recipient does not have any bank added";

                    return result;
                }

                // Check Request recepient's Synapse Bank Account status
                if (moneyRecipientSynInfo.BankDetails != null &&
                    moneyRecipientSynInfo.BankDetails.Status != "Verified" &&
                    moneyRecipient.IsVerifiedWithSynapse != true)
                {
                    Logger.Error(
                        "Landlords API -> RentTrans -> ChargeTenant FAILED -> Trans ABORTED: Money Recipient's Synapse bank account exists but is not Verified and " +
                        "isVerifiedWithSynapse != true - Money Recipient MemberID is: [" + input.TransRequest.TenantId +
                        "]");

                    result.ErrorMessage = "Money recipient does not have any verified bank account.";
                    return result;
                }

                #endregion Get Receiver's Synapse Account Details

                #endregion All Checks Before Execution


                Transaction tr = new Transaction();

                #region Create new transaction in transactions table

                using (NOOCHEntities obj = new NOOCHEntities())
                {

                    tr.TransactionId = Guid.NewGuid();
                    tr.SenderId = landlordsMemID;
                    tr.RecipientId = TenantMemID;
                    tr.Amount = Convert.ToDecimal(input.TransRequest.Amount);
                    tr.TransactionDate = DateTime.Now;
                    tr.Memo = input.TransRequest.Memo;
                    tr.DisputeStatus = null;
                    tr.TransactionStatus = "Pending";
                    tr.TransactionType = CommonHelper.GetEncryptedData("Transfer");
                    tr.DeviceId = null;
                    tr.TransactionTrackingId = CommonHelper.GetRandomTransactionTrackingId();
                    tr.TransactionFee = 0;
                    tr.IsPhoneInvitation = false;

                    GeoLocation gl = new GeoLocation();
                    gl.LocationId = Guid.NewGuid();
                    gl.Latitude = null;
                    gl.Longitude = null;
                    gl.Altitude = null;
                    gl.AddressLine1 = null;
                    gl.AddressLine2 = null;
                    gl.City = null;
                    gl.State = null;
                    gl.Country = null;
                    gl.ZipCode = null;
                    gl.DateCreated = DateTime.Now;
                    //obj.GeoLocations.Add(gl);
                    //obj.SaveChanges();

                    tr.GeoLocation = gl;
                    tr.LocationId = gl.LocationId;

                    try
                    {
                        obj.Transactions.Add(tr);
                        obj.SaveChanges();
                        transactionId = tr.TransactionId.ToString();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(
                            "Landlords API -> RentTrans -> PayToTenants FAILED - Unable to save Transaction in DB - [Sender MemberID:" +
                            input.User.LandlordId + "], [Exception: [ " + ex.InnerException + " ]");
                        result.IsSuccess = false;
                        result.ErrorMessage = "Transaction failed.";
                        return result;
                    }
                }

                #endregion


                #region Define Variables From Transaction for Notifications

                var fromAddress = CommonHelper.GetValueFromConfig("transfersMail");
                string senderUserName = CommonHelper.GetDecryptedData(sender.UserName);
                string senderFirstName = CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(sender.FirstName));
                string senderLastName = CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(sender.LastName));
                string recipientFirstName = CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(moneyRecipient.FirstName));
                string recipientLastName = CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(moneyRecipient.LastName));
                string receiverUserName = CommonHelper.GetDecryptedData(moneyRecipient.UserName);

                string wholeAmount = Convert.ToDecimal(input.TransRequest.Amount).ToString("n2");
                string[] s3 = wholeAmount.Split('.');
                string ce = "";
                string dl = "";
                if (s3.Length <= 1)
                {
                    dl = s3[0].ToString();
                    ce = "00";
                }
                else
                {
                    ce = s3[1].ToString();
                    dl = s3[0].ToString();
                }

                string memo = "";
                if (!String.IsNullOrEmpty(input.TransRequest.Memo))
                {
                    if (input.TransRequest.Memo.Length > 3)
                    {
                        string firstThreeChars = input.TransRequest.Memo.Substring(0, 3).ToLower();
                        bool startsWithFor = firstThreeChars.Equals("for");

                        if (startsWithFor)
                        {
                            memo = input.TransRequest.Memo.ToString();
                        }
                        else
                        {
                            memo = "For: " + input.TransRequest.Memo.ToString();
                        }
                    }
                    else
                    {
                        memo = "For: " + input.TransRequest.Memo.ToString();
                    }
                }

                string senderPic = "https://www.noochme.com/noochweb/Assets/Images/userpic-default.png";
                string recipientPic;

                #endregion Define Variables From Transaction for Notifications



                // Make call to SYNAPSE Order API service
                try
                {
                    short shouldSendFailureNotifications = 0;
                    // Synapse V3 order API code is here

                    #region synapse V3 add trans code.

                    //MemberDataAccess mda = new MemberDataAccess();
                    string sender_oauth = CommonHelper.GetDecryptedData(senderSynInfo.UserDetails.access_token);
                    string sender_fingerPrint = sender.UDID1;
                    string sender_bank_node_id = senderSynInfo.BankDetails.oid.ToString();
                    //string sender_bank_node_id = senderSynInfo.BankDetails.bankid.ToString();
                    string amount = input.TransRequest.Amount.ToString();
                    string fee = "0";
                    if (transactionAmount > 10)
                    {
                        fee = "0.20"; //to offset the Synapse fee so the user doesn't pay it
                    }
                    else if (transactionAmount < 10)
                    {
                        fee = "0.10"; //to offset the Synapse fee so the user doesn't pay it
                    }
                    string receiver_oauth = CommonHelper.GetDecryptedData(moneyRecipientSynInfo.UserDetails.access_token);
                    string receiver_fingerprint = moneyRecipient.UDID1;
                    string receiver_bank_node_id = moneyRecipientSynInfo.BankDetails.oid.ToString();
                    //string receiver_bank_node_id = moneyRecipientSynInfo.BankDetails.bankid.ToString();
                    string suppID_or_transID = transactionId.ToString();
                    //string senderUserName = CommonHelper.GetDecryptedData(sender.UserName).ToLower();
                    //string receiverUserName = CommonHelper.GetDecryptedData(requester.UserName).ToLower();
                    string iPForTransaction = CommonHelper.GetRecentOrDefaultIPOfMember(Landlord_GUID);
                    //string senderLastName = CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(sender.LastName));
                    //string recepientLastName = CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(requester.LastName));


                    SynapseV3AddTrans_ReusableClass transactionResultFromSynapseAPI =
                        CommonHelper.AddTransSynapseV3Reusable(sender_oauth, sender_fingerPrint, sender_bank_node_id,
                            amount, fee, receiver_oauth, receiver_fingerprint, receiver_bank_node_id, suppID_or_transID,
                            senderUserName, receiverUserName, iPForTransaction, senderLastName, recipientLastName);


                    if (transactionResultFromSynapseAPI.success == true)
                    {
                        #region Synapse Response Was Successful

                        Logger.Info("RentTrans -> PayToTenants - SUCCESS Response From AddTransSynapseV3Reusable - " +
                                                "Synapse TransID: [" + transactionResultFromSynapseAPI.responseFromSynapse.trans._id.oid + "]");

                        try
                        {
                            using (NOOCHEntities obj = new NOOCHEntities())
                            {
                                Transaction trn = obj.Transactions.Where(t => t.TransactionId == tr.TransactionId).FirstOrDefault();
                                // updating the transaction status to success.
                                trn.TransactionStatus = "Success";
                                trn.DateAccepted = DateTime.Now;
                                int save = obj.SaveChanges();
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(
                                       "Landlords API -> RentTrans -> PayToTenant -> Updating Transaction status to success failed: TransactionID [" +
                                       tr.TransactionId.ToString() + "], [Exception: " + ex + "]");
                        }
                        #region Send Email to Sender on transfer success

                        var sendersNotificationSets =
                            CommonHelper.GetMemberNotificationSettings(sender.MemberId.ToString());

                        if (sendersNotificationSets != null && (sendersNotificationSets.EmailTransferSent ?? false))
                        {
                            if (!String.IsNullOrEmpty(moneyRecipient.Photo) && moneyRecipient.Photo.Length > 20)
                            {
                                recipientPic = moneyRecipient.Photo.ToString();
                            }

                            var tokens = new Dictionary<string, string>
                            {
                                {Constants.PLACEHOLDER_FIRST_NAME, senderFirstName},
                                {Constants.PLACEHOLDER_FRIEND_FIRST_NAME, recipientFirstName + " " + recipientLastName},
                                {Constants.PLACEHOLDER_TRANSFER_AMOUNT, dl},
                                {Constants.PLACEHLODER_CENTS, ce},
                                {Constants.MEMO, memo}
                            };

                            var toAddress = CommonHelper.GetDecryptedData(sender.UserName);

                            try
                            {
                                CommonHelper.SendEmail("TransferSent", fromAddress, fromAddress, toAddress,
                                    "Your $" + wholeAmount + " payment to " + recipientFirstName + " on Nooch",
                                    tokens, null, null);

                                Logger.Info("Landlords API -> RentTrans -> PayToTenant - TransferSent email sent to [" +
                                            toAddress + "] successfully");
                            }
                            catch (Exception ex)
                            {
                                Logger.Error(
                                    "Landlords API -> RentTrans -> PayToTenant -> EMAIL TO RECIPIENT FAILED: TransferReceived Email NOT sent to [" +
                                    toAddress + "], [Exception: " + ex + "]");
                            }
                        }

                        #endregion Send Email to Sender on transfer success

                        // Now notify the recipient...

                        #region Send Notifications to Recipient on transfer success

                        var recipNotificationSets =
                            CommonHelper.GetMemberNotificationSettings(moneyRecipient.MemberId.ToString());

                        if (recipNotificationSets != null)
                        {
                            // First, send push notification

                            #region Push notification to Recipient

                            if ((recipNotificationSets.TransferReceived == null)
                                ? false
                                : recipNotificationSets.TransferReceived.Value)
                            {
                                string recipDeviceId = recipNotificationSets != null ? moneyRecipient.DeviceToken : null;

                                string pushBodyText = "You received $" + wholeAmount + " from " + senderFirstName +
                                                      " " + senderLastName + "! Spend it wisely :-)";
                                try
                                {
                                    if (recipNotificationSets != null &&
                                        !String.IsNullOrEmpty(recipDeviceId) &&
                                        (recipNotificationSets.TransferReceived ?? false))
                                    {
                                        ApplePushNotification.SendNotificationMessage(pushBodyText, 1,
                                            null, recipDeviceId,
                                            CommonHelper.GetValueFromConfig("AppKey"),
                                            CommonHelper.GetValueFromConfig("MasterSecret"));

                                        Logger.Info(
                                            "Landlords API -> RentTrans -> PayToTenant -> SUCCESS - Push notification sent to Recipient [" +
                                            recipientFirstName + " " + recipientLastName + "] successfully.");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error(
                                        "Landlords API -> RentTrans -> PayToTenant -> Success - BUT Push notification FAILURE - Push to Recipient NOT sent [" +
                                        recipientFirstName + " " + recipientLastName + "], Exception: [" + ex + "]");
                                }
                            }

                            #endregion Push notification to Recipient

                            // Now send email notification

                            #region Email notification to Recipient

                            if (recipNotificationSets != null && (recipNotificationSets.EmailTransferReceived ?? false))
                            {
                                if (!String.IsNullOrEmpty(sender.Photo) && sender.Photo.Length > 20)
                                {
                                    senderPic = sender.Photo.ToString();
                                }

                                var tokensR = new Dictionary<string, string>
                                {
                                    {Constants.PLACEHOLDER_FIRST_NAME, recipientFirstName},
                                    {Constants.PLACEHOLDER_FRIEND_FIRST_NAME, senderFirstName + " " + senderLastName},
                                    {"$UserPicture$", senderPic},
                                    {Constants.PLACEHOLDER_TRANSFER_AMOUNT, wholeAmount},
                                    {
                                        Constants.PLACEHOLDER_TRANSACTION_DATE,
                                        Convert.ToDateTime(TransDateTime).ToString("MMM dd")
                                    },
                                    {Constants.MEMO, memo}
                                };

                                var toAddress2 = CommonHelper.GetDecryptedData(moneyRecipient.UserName);

                                try
                                {
                                    CommonHelper.SendEmail("TransferReceived", fromAddress, fromAddress, toAddress2,
                                        senderFirstName + " sent you $" + wholeAmount + " with Nooch", tokensR, null,
                                        null);

                                    Logger.Info(
                                        "Landlords API -> RentTrans -> PayToTenant -> TransferReceived Email sent to [" +
                                        toAddress2 + "] successfully");
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error(
                                        "Landlords API -> RentTrans -> PayToTenant -> EMAIL TO RECIPIENT FAILED: TransferReceived Email NOT sent to [" +
                                        toAddress2 + "], [Exception: " + ex + "]");
                                }
                            }

                            #endregion Email notification to Recipient
                        }

                        #endregion Send Notifications to Recipient on transfer success

                        result.ErrorMessage = "Your cash was sent successfully";
                        result.IsSuccess = true;
                        return result;

                        #endregion Synapse Response Was Successful
                    }
                    #region Failure Sections

                    else
                    {
                        // Synapse Order API returned failure in response

                        Logger.Error(
                            "Landlords API -> RentTrans -> PayToTenants - Synapse returned failure. For Transaction ID -> " +
                            transactionId);

                        shouldSendFailureNotifications = 2;
                    }

                    // Check if there was a failure above and we need to send the failure Email/SMS notifications to the sender.
                    if (shouldSendFailureNotifications > 0)
                    {
                        Logger.Error(
                            "Landlords API -> RentTrans -> PayToTenants  - THERE WAS A FAILURE - Sending Failure Notifications to both Users");

                        #region Notify Sender about failure

                        var senderNotificationSettings =
                            CommonHelper.GetMemberNotificationSettings(sender.MemberId.ToString());

                        if (senderNotificationSettings != null)
                        {
                            #region Push Notification to Sender about failure

                            if (senderNotificationSettings.TransferAttemptFailure == true)
                            {
                                string senderDeviceId = senderNotificationSettings != null ? sender.DeviceToken : null;

                                string mailBodyText = "Your attempt to send $" + tr.Amount.ToString("n2") +
                                                      " to " + recipientFirstName + " " + recipientLastName +
                                                      " failed ;-(  Contact Nooch support for more info.";

                                if (!String.IsNullOrEmpty(senderDeviceId))
                                {
                                    try
                                    {
                                        ApplePushNotification.SendNotificationMessage(mailBodyText, 0, null,
                                            senderDeviceId,
                                            CommonHelper.GetValueFromConfig("AppKey"),
                                            CommonHelper.GetValueFromConfig("MasterSecret"));

                                        Logger.Info(
                                            "Landlords API -> RentTrans -> PayToTenants  FAILED - Push notif sent to Sender: [" +
                                            senderFirstName + " " + senderLastName + "] successfully.");
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Error(
                                            "Landlords API -> RentTrans -> PayToTenants  FAILED - Push notif FAILED also, SMS NOT sent to [" +
                                            senderFirstName + " " + senderLastName + "],  [Exception: " + ex + "]");
                                    }
                                }
                            }

                            #endregion Push Notification to Sender about failure

                            #region Email notification to Sender about failure

                            if (senderNotificationSettings.EmailTransferAttemptFailure ?? false)
                            {
                                var tokens = new Dictionary<string, string>
                                {
                                    {Constants.PLACEHOLDER_FIRST_NAME, senderFirstName + " " + senderLastName},
                                    {
                                        Constants.PLACEHOLDER_FRIEND_FIRST_NAME,
                                        recipientFirstName + " " + recipientLastName
                                    },
                                    {Constants.PLACEHOLDER_TRANSFER_AMOUNT, dl},
                                    {Constants.PLACEHLODER_CENTS, ce},
                                };

                                var toAddress = CommonHelper.GetDecryptedData(sender.UserName);

                                try
                                {
                                    CommonHelper.SendEmail("transferFailure",
                                        fromAddress, toAddress, toAddress, "Nooch transfer failure :-(",
                                        tokens, null, null);

                                    Logger.Info(
                                        "Landlords API -> RentTrans -> PayToTenants  FAILED - Email sent to Sender: [" +
                                        toAddress + "] successfully.");
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error(
                                        "Landlords API -> RentTrans -> PayToTenants  --> Error: TransferAttemptFailure mail " +
                                        "NOT sent to [" + toAddress + "],  [Exception: " + ex + "]");
                                }
                            }

                            #endregion Email notification to Sender about failure
                        }

                        #endregion Notify Sender about failure

                        if (shouldSendFailureNotifications == 1)
                        {

                            result.ErrorMessage = "There was a problem updating Nooch DB tables.";
                            return result;
                        }
                        else if (shouldSendFailureNotifications == 2)
                        {
                            result.ErrorMessage = "There was a problem with Synapse.";
                            return result;
                        }
                        else
                        {
                            result.ErrorMessage = "Unknown Failure";
                            return result;
                        }
                    }

                    #endregion Failure Sections

                    #endregion

                }
                catch (WebException ex)
                {
                    var resp = new StreamReader(ex.Response.GetResponseStream()).ReadToEnd();
                    JObject jsonFromSynapse = JObject.Parse(resp);

                    Logger.Info("Landlords API -> RentTrans -> PayToTenants  FAILED. [Exception: " + jsonFromSynapse + "]");

                    JToken token = jsonFromSynapse["reason"];

                    if (token != null)
                    {
                        result.ErrorMessage = "Sorry There Was A Problem - PayToTenants(1): " + token.ToString();
                        return result;
                    }
                    else
                    {
                        // bad request or some other error
                        result.ErrorMessage = "Sorry There Was A Problem PayToTenants(2): " + ex.ToString();
                        return result;
                    }
                }

                result.ErrorMessage = "Uh oh - Unknown Failure";
                // This should never be reached b/c code should hit the failure section

                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                Logger.Error("Landlords API -> RentTrans -> ChargeTenant FAILED - [LandlordID: " + input.User.LandlordId +
                             "], [Exception: [ " + ex + " ]");
                result.ErrorMessage = "Error while chargin tenant. Retry later!";
            }
            return result;
        }


        [HttpPost]
        [ActionName("ChargeTenant")]
        public CreatePropertyResultOutput chargeTenant(ChargeTenantInputClass input)
        {
            Logger.Info("Landlords API -> RentTrans -> ChargeTenant - Requested by [" + input.User.LandlordId + "]");

            CreatePropertyResultOutput result = new CreatePropertyResultOutput();
            result.IsSuccess = false;

            try
            {
                Guid Landlord_GUID = CommonHelper.ConvertToGuid(input.User.LandlordId);
                Guid Tenant_GUID = CommonHelper.ConvertToGuid(input.TransRequest.TenantId);
                Guid landlordsMemID = new Guid(CommonHelper.GetLandlordsMemberIdFromLandlordId(Landlord_GUID));

                string requestId = "";

                #region All Checks Before Execution

                // Check uniqueness of requesting and sending user
                if (Landlord_GUID == Tenant_GUID)
                {
                    result.ErrorMessage = "Not allowed to request money from yourself.";
                    return result;
                }

                // Check if request Amount is over per-transaction limit
                decimal transactionAmount = Convert.ToDecimal(input.TransRequest.Amount);

                if (CommonHelper.isOverTransactionLimit(transactionAmount, input.TransRequest.TenantId,
                    input.User.LandlordId))
                {
                    result.ErrorMessage = "To keep Nooch safe, the maximum amount you can request is $" +
                                          Convert.ToDecimal(
                                              CommonHelper.GetValueFromConfig("MaximumTransferLimitPerTransaction"))
                                              .ToString("F2");
                    return result;
                }

                // Get requester and request recepient Members table info
                var requester = CommonHelper.GetMemberByMemberId(landlordsMemID);
                var requesterLandlordObj = CommonHelper.GetLandlordByLandlordId(Landlord_GUID);
                // Only need this for the Photo for the email template... Members table doesn't have it.
                var requestRecipient = CommonHelper.GetMemberByMemberId(Tenant_GUID);


                if (requester == null)
                {
                    Logger.Error(
                        "Landlords API -> RentTrans -> ChargeTenant FAILED - Requester Member Not Found - [MemberID: " +
                        Landlord_GUID + "]");
                    result.ErrorMessage = "Requester Member Not Found";

                    return result;
                }
                if (requestRecipient == null)
                {
                    Logger.Error(
                        "Landlords API -> RentTrans -> ChargeTenant FAILED - requestRecipient (who would pay the request) Member Not Found - [MemberID: " +
                        Landlord_GUID + "]");
                    result.ErrorMessage = "Request Recipient Member Not Found";

                    return result;
                }

                #region Get Request Sender's Synapse Account Details


                var requestorSynInfo =
                    CommonHelper.GetSynapseBankAndUserDetailsforGivenMemberId(landlordsMemID.ToString());

                if (requestorSynInfo.wereBankDetailsFound != true)
                {
                    Logger.Error(
                        "Landlords API -> RentTrans -> ChargeTenant FAILED -> Request ABORTED: Requester's Synapse bank account NOT FOUND - Request Creator MemberId is: [" +
                        requester.MemberId + "]");
                    result.ErrorMessage = "Requester does not have any bank added";

                    return result;
                }

                // Check Requestor's Synapse Bank Account status
                if (requestorSynInfo.BankDetails != null &&
                    requestorSynInfo.BankDetails.Status != "Verified" &&
                    requester.IsVerifiedWithSynapse != true)
                {
                    Logger.Error(
                        "Landlords API -> RentTrans -> ChargeTenant FAILED -> Request ABORTED: Requester's Synapse bank account exists but is not Verified and " +
                        "isVerifiedWithSynapse != true - Request Creator memberId is: [" + requester.MemberId + "]");
                    result.ErrorMessage = "Requester does not have any verified bank account.";

                    return result;
                }

                #endregion Get Sender's Synapse Account Details

                // @Cliff.. feel free to comment this section if you don't want to check Tenant synapse details

                #region Get Request Sender's Synapse Account Details

                /*var requestRecipientSynInfo = CommonHelper.GetSynapseBankAndUserDetailsforGivenMemberId(input.TransRequest.TenantId);

                if (requestRecipientSynInfo.wereBankDetailsFound != true)
                {
                    Logger.Error("Landlords API -> RentTrans -> ChargeTenant FAILED -> Request ABORTED: Request Recipient's Synapse bank account NOT FOUND - Request Recipient MemberID: [" + input.TransRequest.TenantId + "]");
                    result.ErrorMessage = "Request recipient does not have any bank added";

                    return result;
                }

                // Check Request recepient's Synapse Bank Account status
                if (requestRecipientSynInfo.BankDetails != null &&
                    requestRecipientSynInfo.BankDetails.Status != "Verified" &&
                    requestRecipient.IsVerifiedWithSynapse != true)
                {
                    Logger.Error("Landlords API -> RentTrans -> ChargeTenant FAILED -> Request ABORTED: Request Recipient's Synapse bank account exists but is not Verified and " +
                        "isVerifiedWithSynapse != true - Request Recipient MemberID is: [" + input.TransRequest.TenantId + "]");

                    result.ErrorMessage = "Request recipient does not have any verified bank account.";
                    return result;
                }*/

                #endregion Get Sender's Synapse Account Details

                #endregion All Checks Before Execution


                #region Create new transaction in transactions table

                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    Transaction tr = new Transaction();
                    tr.TransactionId = Guid.NewGuid();
                    tr.SenderId = Tenant_GUID;
                    tr.RecipientId = landlordsMemID;
                    tr.Amount = Convert.ToDecimal(input.TransRequest.Amount);
                    tr.TransactionDate = DateTime.Now;
                    tr.Memo = input.TransRequest.Memo; // this would be the reason why we are charging tenant 
                    tr.DisputeStatus = null;
                    tr.TransactionStatus = "Pending";
                    tr.TransactionType = CommonHelper.GetEncryptedData("Request");
                    tr.DeviceId = null;
                    tr.TransactionTrackingId = CommonHelper.GetRandomTransactionTrackingId();
                    tr.TransactionFee = 0;
                    tr.IsPhoneInvitation = false;

                    // we can take advantage of having tenants email id here.
                    //tr.InvitationSentTo = !String.IsNullOrEmpty(requestDto.MoneySenderEmailId) ? CommonHelper.GetEncryptedData(requestDto.MoneySenderEmailId) : null,

                    GeoLocation gl = new GeoLocation();
                    gl.LocationId = Guid.NewGuid();
                    gl.Latitude = null;
                    gl.Longitude = null;
                    gl.Altitude = null;
                    gl.AddressLine1 = null;
                    gl.AddressLine2 = null;
                    gl.City = null;
                    gl.State = null;
                    gl.Country = null;
                    gl.ZipCode = null;
                    gl.DateCreated = DateTime.Now;
                    //obj.GeoLocations.Add(gl);
                    //obj.SaveChanges();

                    tr.GeoLocation = gl;
                    tr.LocationId = gl.LocationId;

                    try
                    {
                        obj.Transactions.Add(tr);
                        obj.SaveChanges();
                        requestId = tr.TransactionId.ToString();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(
                            "Landlords API -> RentTrans -> ChargeTenant FAILED - Unable to save Transaction in DB - [Requester MemberID:" +
                            input.User.LandlordId + "], [Exception: [ " + ex.InnerException + " ]");
                        result.IsSuccess = false;
                        result.ErrorMessage = "Request failed.";
                        return result;
                    }
                }

                #endregion

                #region Send Notifications

                #region Set Up Variables

                string fromAddress = CommonHelper.GetValueFromConfig("transfersMail");

                string RequesterFirstName = !String.IsNullOrEmpty(requester.FirstName)
                    ? CommonHelper.UppercaseFirst((CommonHelper.GetDecryptedData(requester.FirstName)))
                    : "";
                string RequesterLastName = !String.IsNullOrEmpty(requester.LastName)
                    ? CommonHelper.UppercaseFirst((CommonHelper.GetDecryptedData(requester.LastName)))
                    : "";
                string RequesterEmail = CommonHelper.GetDecryptedData(requester.UserName);

                string RequestReceiverFirstName = !String.IsNullOrEmpty(requestRecipient.FirstName)
                    ? CommonHelper.UppercaseFirst((CommonHelper.GetDecryptedData(requestRecipient.FirstName)))
                    : "";
                string RequestReceiverLastName = !String.IsNullOrEmpty(requestRecipient.LastName)
                    ? CommonHelper.UppercaseFirst((CommonHelper.GetDecryptedData(requestRecipient.LastName)))
                    : "";
                string RequestReceiverFullName = (RequestReceiverFirstName.Length > 2 &&
                                                  RequestReceiverLastName.Length > 2)
                    ? RequestReceiverFirstName + " " + RequestReceiverLastName
                    : CommonHelper.GetDecryptedData(requestRecipient.UserName);

                Logger.Info("RequesterFirstName: [" + RequesterFirstName + "], RequesterLastName: [" + RequesterLastName +
                            "], RequestReceiverFirstName: [" + RequestReceiverFirstName +
                            "], RequestReceiverLastName: [" + RequestReceiverLastName + "], RequestReceiverFullName: [" +
                            RequestReceiverFullName + "]");

                string requesterPic = "https://www.noochme.com/noochweb/Assets/Images/userpic-default.png";
                if (!String.IsNullOrEmpty(requesterLandlordObj.UserPic) && requesterLandlordObj.UserPic.Length > 20)
                {
                    requesterPic = requesterLandlordObj.UserPic;
                }
                else if (!String.IsNullOrEmpty(requester.Photo) && requester.Photo.Length > 20)
                {
                    requesterPic = requester.Photo;
                }

                //"trans/CancelRequest.aspx?TransactionId=" + requestId + "&MemberId=" + input.User.LandlordId +
                //"&UserType=6KX3VJv3YvoyK+cemdsvMA==");

                string cancelLink = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"),
                     "Nooch/CancelRequest?TransactionId=" + requestId + "&MemberId=" + input.User.LandlordId +
                    "&UserType=6KX3VJv3YvoyK+cemdsvMA==");

                string wholeAmount = Convert.ToDecimal(input.TransRequest.Amount).ToString("n2");
                string[] amountArray = wholeAmount.Split('.');

                string memo = "";
                if (!string.IsNullOrEmpty(input.TransRequest.Memo))
                {
                    if (input.TransRequest.Memo.Length > 3)
                    {
                        string firstThreeChars = input.TransRequest.Memo.Substring(0, 3).ToLower();
                        bool startWithFor = firstThreeChars.Equals("for");

                        if (startWithFor)
                        {
                            memo = input.TransRequest.Memo.ToString();
                        }
                        else
                        {
                            memo = "for " + input.TransRequest.Memo.ToString();
                        }
                    }
                    else
                    {
                        memo = "for " + input.TransRequest.Memo.ToString();
                    }
                }

                #endregion Set Up Variables


                // Send email to REQUESTER (person who sent this request)

                #region Email To Requester

                try
                {
                    var tokens = new Dictionary<string, string>
                    {
                        {Constants.PLACEHOLDER_FIRST_NAME, RequesterFirstName},
                        {Constants.PLACEHOLDER_NEWUSER, RequestReceiverFullName},
                        {Constants.PLACEHOLDER_TRANSFER_AMOUNT, amountArray[0]},
                        {Constants.PLACEHLODER_CENTS, amountArray[1]},
                        {Constants.PLACEHOLDER_OTHER_LINK, cancelLink},
                        {Constants.MEMO, memo}
                    };

                    Logger.Info("RentTrans Ctrlr -> ChargeTenant - Memo: [" + memo + "], wholeAmount: [" + wholeAmount +
                                "], cancelLink: [" + cancelLink +
                                "], toAddress: [" + RequesterEmail + "], fromAddress: [" + fromAddress + "]");

                    CommonHelper.SendEmail("requestSent", fromAddress, null, RequesterEmail,
                        "Your payment request to " + RequestReceiverFullName +
                        " is pending", tokens, null, null);

                    Logger.Info("Landlords API -> RentTrans -> ChargeTenant -> RequestSent email sent to [" +
                                RequesterEmail + "] successfully.");
                }
                catch (Exception ex)
                {
                    Logger.Error("Landlords API -> RentTrans -> ChargeTenant -> RequestSent email NOT sent to [" +
                                 RequesterEmail +
                                 "], [Exception: " + ex + "]");
                }

                #endregion Email To Requester


                #region Email To Request Recipient

                // Send email to REQUEST RECIPIENT (person who will pay/reject this request)
                // Include 'UserType', 'LinkSource', and 'TransType' as encrypted along with request
                // In this case UserType would = 'Nonregistered'  ->  6KX3VJv3YvoyK+cemdsvMA==
                //              TransType would = 'Request'
                //              LinkSource would = 'Email'


                //string rejectLink = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"),
                //    "trans/rejectMoney.aspx?TransactionId=" + requestId +
                //    "&UserType=6KX3VJv3YvoyK+cemdsvMA==&LinkSource=75U7bZRpVVxLNbQuoMQEGQ==&TransType=T3EMY1WWZ9IscHIj3dbcNw==");
                //string paylink = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"),
                //    "trans/payRequest.aspx?TransactionId=" + requestId + "&UserType=6KX3VJv3YvoyK+cemdsvMA==");

                string rejectLink = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"),
                 "Nooch/RejectMoney?TransactionId=" + requestId +
                 "&UserType=6KX3VJv3YvoyK+cemdsvMA==&LinkSource=75U7bZRpVVxLNbQuoMQEGQ==&TransType=T3EMY1WWZ9IscHIj3dbcNw==");
                string paylink = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"),
                    "Nooch/PayRequest?TransactionId=" + requestId + "&UserType=6KX3VJv3YvoyK+cemdsvMA==");

                var tokens2 = new Dictionary<string, string>
                {
                    {Constants.PLACEHOLDER_FIRST_NAME, RequestReceiverFirstName},
                    {Constants.PLACEHOLDER_USER_PICTURE, requesterPic},
                    {Constants.PLACEHOLDER_SENDER_FULL_NAME, RequesterFirstName + " " + RequesterLastName},
                    {Constants.PLACEHOLDER_TRANSFER_AMOUNT, amountArray[0]},
                    {Constants.PLACEHLODER_CENTS, amountArray[1]},
                    {Constants.MEMO, memo},
                    {Constants.PLACEHOLDER_REJECT_LINK, rejectLink},
                    {Constants.PLACEHOLDER_PAY_LINK, paylink},
                    {Constants.PLACEHOLDER_FRIEND_FIRST_NAME, RequesterFirstName}
                };

                string toAddress = CommonHelper.GetDecryptedData(requestRecipient.UserName);

                try
                {
                    CommonHelper.SendEmail("requestReceivedToExistingNonRegUser", fromAddress,
                        RequesterFirstName + " " + RequesterLastName, toAddress,
                        RequesterFirstName + " " + RequesterLastName + " requested " + "$" + wholeAmount.ToString() +
                        " with Nooch", tokens2, null, null);

                    Logger.Info("RentTrans -> ChargeTenant ->  requestReceivedToNewUser email sent to [" + toAddress +
                                "] successfully");
                }
                catch (Exception ex)
                {
                    Logger.Error("RentTrans -> ChargeTenant -> requestReceivedToNewUser email NOT sent to  [" +
                                 toAddress + "], [Exception: " + ex + "]");
                }

                #endregion Email To Request Recipient


                // Send SMS to REQUEST RECIPIENT (person who will pay/reject this request)
                // CLIFF (10/20/15) This block works (tested successfully) but commenting out b/c the Deposit-Money landing page
                //                  needs to be improved for Mobile screen sizes... not a great experience as it currently is.
                // Malkit Block code fixed to work with landlords api

                #region Send SMS To Non-Nooch Transfer Recipient

                /*  string googleUrlAPIKey = CommonHelper.GetValueFromConfig("GoogleURLAPI");

                // shortening URLs from Google
                string RejectShortLink = rejectLink;
                string AcceptShortLink = paylink;

                #region Call Google URL Shortener API

                try
                {
                    var cli = new WebClient();
                    cli.Headers[HttpRequestHeader.ContentType] = "application/json";
                    string response = cli.UploadString("https://www.googleapis.com/urlshortener/v1/url?key=" + googleUrlAPIKey, "{longUrl:\"" + rejectLink + "\"}");
                    googleURLShortnerResponseClass googlerejectshortlinkresult = JsonConvert.DeserializeObject<googleURLShortnerResponseClass>(response);

                    if (googlerejectshortlinkresult != null)
                    {
                        RejectShortLink = googlerejectshortlinkresult.id;
                    }
                    else
                    {
                        // Google short URL API broke...
                        Logger.Error("Landlords API -> RentTrans -> ChargeTenant -> - GoogleAPI FAILED for Reject Short Link.");
                    }
                    cli.Dispose();

                    // Now shorten Accept link

                    var cli2 = new WebClient();
                    cli2.Headers[HttpRequestHeader.ContentType] = "application/json";
                    string response2 = cli2.UploadString("https://www.googleapis.com/urlshortener/v1/url?key=" + googleUrlAPIKey, "{longUrl:\"" + paylink + "\"}");
                    googleURLShortnerResponseClass googlerejectshortlinkresult2 = JsonConvert.DeserializeObject<googleURLShortnerResponseClass>(response2);

                    if (googlerejectshortlinkresult2 != null)
                    {
                        AcceptShortLink = googlerejectshortlinkresult2.id;
                    }
                    else
                    {
                        // Google short URL API broke...
                        Logger.Error("Landlords API -> RentTrans -> ChargeTenant -> - GoogleAPI FAILED for Accept Short Link.");
                    }
                    cli2.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Error("Landlords API -> RentTrans -> ChargeTenant -> - GoogleAPI FAILED. [Exception: " + ex + "]");
                }

                #endregion Call Google URL Shortener API

                string toPhoneNumber = requestRecipient.ContactNumber;

                try
                {
                    // Example SMS string: "Cliff Canan charged you $10 using Nooch, a free app for {Memo}. Click here to pay: {LINK}. Or here to reject: {LINK}"

                    // Make sure URL is short version (if Google API failued, use long version of Pay link and exclude the Reject link to save space)
                    string SMSContent;

                    if (AcceptShortLink.Length < 30) // Google Short links should be ~ 21 characters
                    {
                        string memoTxtForSms = memo;
                        if (memoTxtForSms.Length < 2)
                        {
                            memoTxtForSms = "from you";
                        }

                        SMSContent = RequesterFirstName + " " + RequesterLastName + " charged you $" +
                                              amountArray[0] + "." + amountArray[1] + " " +
                                              memoTxtForSms +
                                              " using Nooch for " + input.TransRequest.Memo + ". Tap here to pay: " + AcceptShortLink +
                                              ". Or reject: " + RejectShortLink;
                    }
                    else // Google Short link API broke, use long version of Pay link
                    {
                        SMSContent = RequesterFirstName + " " + RequesterLastName + " charged you $" +
                                              amountArray[0] + "." + amountArray[1] +
                                              " using Nooch for " + input.TransRequest.Memo + ". Tap here to pay: " + AcceptShortLink;
                    }

                    string result2 = CommonHelper.SendSMS(toPhoneNumber, SMSContent, "");

                    Logger.Info("Landlords API -> RentTrans -> ChargeTenant -> SMS sent to recipient [" + toPhoneNumber + "] successfully. [Msg: " + result + "]");
                }
                catch (Exception ex)
                {
                    Logger.Error("Landlords API -> RentTrans -> ChargeTenant -> SMS NOT sent to recipient [" + toPhoneNumber +
                                           "], [Exception:" + ex + "]");
                }*/

                #endregion Send SMS To Non-Nooch Transfer Recipient


                #endregion Send Notifications

                result.IsSuccess = true;
                result.ErrorMessage = "Ok";
            }
            catch (Exception ex)
            {
                Logger.Error("Landlords API -> RentTrans -> ChargeTenant FAILED - [LandlordID: " + input.User.LandlordId +
                             "], [Exception: [ " + ex + " ]");
                result.ErrorMessage = "Error while chargin tenant. Retry later!";
            }

            return result;
        }


        /// <summary>
        /// For cancelling a REQUEST sent to a TENANT.
        /// Called from the Landlord Web App - History Page.
        /// </summary>
        /// <param name="TransactionId"></param>
        /// <param name="MemberId"></param>
        [HttpPost]
        [ActionName("CancelTrans")]
        public GenericInternalResponse CancelTransaction(CancelTransInput input)
        {
            GenericInternalResponse result = new GenericInternalResponse();
            result.success = false;
            result.msg = "Initial";

            try
            {
                Logger.Info("RentTrans Cntrlr -> CancelTransaction Initiated - " +
                            "TransactionID: [" + input.TransId + "], LandlordID: [" + input.User.LandlordId + "]");

                Guid transGuid = CommonHelper.ConvertToGuid(input.TransId);

                Guid LandlordGuid = CommonHelper.ConvertToGuid(input.User.LandlordId);
                Landlord landlordObj = CommonHelper.GetLandlordByLandlordId(LandlordGuid);

                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(LandlordGuid, input.User.AccessToken);

                if (result.AuthTokenValidation.IsTokenOk)
                {
                    using (NOOCHEntities obj = new NOOCHEntities())
                    {
                        // Reading Landlord's details from Landlords Table in  DB
                        var transObj = (from c in obj.Transactions
                                        where c.TransactionId == transGuid &&
                                              c.TransactionStatus == "Pending" &&
                                              (c.SenderId == landlordObj.MemberId ||
                                               c.RecipientId == landlordObj.MemberId)
                                        select c).FirstOrDefault();


                        if (transObj != null)
                        {
                            transObj.TransactionStatus = "Cancelled";

                            int i = obj.SaveChanges();

                            if (i > 0)
                            {
                                Logger.Info(
                                    "RentTrans Cntrlr -> CancelTransaction - Transaction Cancelled SUCCESSFULLY - " +
                                    "TransactionID: [" + input.TransId + "], LandlordID: [" + input.User.LandlordId +
                                    "]");

                                #region Send Notifications

                                try
                                {
                                    #region Setup Email Variables

                                    string memo = "";
                                    if (transObj.Memo != null && transObj.Memo != "")
                                    {
                                        memo = "For " + transObj.Memo.ToString();
                                    }

                                    string transDate =
                                        Convert.ToDateTime(transObj.TransactionDate).ToString("MMM dd yyyy");
                                    string requesterFirstName =
                                        CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(landlordObj.FirstName));
                                    string requesterLastName =
                                        CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(landlordObj.LastName));

                                    string amount = transObj.Amount.ToString("n2");

                                    var fromAddress = CommonHelper.GetValueFromConfig("transfersMail");

                                    string recipientUserPhoneOrEmail = "";
                                    string phoneNumStripped = "";

                                    if (transObj.IsPhoneInvitation == true &&
                                        transObj.PhoneNumberInvited != null)
                                    {
                                        phoneNumStripped = CommonHelper.GetDecryptedData(transObj.PhoneNumberInvited);
                                        recipientUserPhoneOrEmail = CommonHelper.FormatPhoneNumber(phoneNumStripped);
                                    }
                                    else
                                    {
                                        recipientUserPhoneOrEmail =
                                            CommonHelper.GetDecryptedData(transObj.InvitationSentTo);
                                    }

                                    #endregion Setup Email Variables

                                    var toAddress = CommonHelper.GetDecryptedData(landlordObj.eMail);

                                    try
                                    {
                                        var tokens = new Dictionary<string, string>
                                        {
                                            {Constants.PLACEHOLDER_FIRST_NAME, requesterFirstName},
                                            {"$Recipient$", recipientUserPhoneOrEmail},
                                            {Constants.PLACEHOLDER_TRANSFER_AMOUNT, amount},
                                            {"$Date$", transDate},
                                            {Constants.MEMO, memo}
                                        };

                                        CommonHelper.SendEmail("requestCancelledToSender", fromAddress, null, toAddress,
                                            "Your payment request was cancelled", tokens, null, null);

                                        Logger.Info(
                                            "RentTrans Cntrlr -> CancelTransaction - requestCancelledToSender email sent " +
                                            "to Requester: [" + toAddress + "] successfully.");
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Error(
                                            "RentTrans Cntrlr -> CancelTransaction - requestCancelledToSender email NOT sent " +
                                            "to Requester: [" + toAddress + "], Exception: [" + ex + "]");
                                    }


                                    var toAddress2 = recipientUserPhoneOrEmail;

                                    try
                                    {
                                        var tokens2 = new Dictionary<string, string>
                                        {
                                            {Constants.PLACEHOLDER_FIRST_NAME, recipientUserPhoneOrEmail},
                                            {
                                                Constants.PLACEHOLDER_LAST_NAME,
                                                requesterFirstName + " " + requesterLastName
                                            },
                                            {Constants.PLACEHOLDER_TRANSFER_AMOUNT, amount},
                                            {Constants.MEMO, memo}
                                        };

                                        string subject = requesterFirstName + " " + requesterLastName +
                                                         " cancelled a payment request to you";

                                        CommonHelper.SendEmail("requestCancelledToRecipient", fromAddress, null,
                                            toAddress, subject, tokens2, null, null);

                                        Logger.Info(
                                            "RentTrans Cntrlr -> CancelTransaction - requestCancelledToRecipient email sent to [" +
                                            toAddress + "] successfully.");
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Error(
                                            "RentTrans Cntrlr -> CancelTransaction - requestCancelledToRecipient email NOT sent to [" +
                                            toAddress + "], Exception: [" + ex + "]");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error(
                                        "RentTrans Cntrlr -> CancelTransaction EXCEPTION - Failure while sending notifications - [" +
                                        "TransactionID: [" + input.TransId + "], Exception: [" + ex + "]");
                                }

                                #endregion Send Notifications

                                result.success = true;
                                result.msg = "Transaction Cancelled Successfully";
                            }
                            else
                            {
                                Logger.Error(
                                    "RentTrans Cntrlr -> CancelTransaction FAILED - Failed to save updates to DB - " +
                                    "TransactionID: [" + input.TransId + "]");

                                result.msg = "Unable to save changes to DB.";
                            }
                        }
                        else
                        {
                            Logger.Error("RentTrans Cntrlr -> CancelTransaction FAILED - Transaction Not Found - " +
                                         "TransactionID: [" + input.TransId + "]");

                            result.msg = "Could not find that transaction.";
                        }
                    }
                }
                else
                {
                    Logger.Error("RentTrans Cntrlr -> Cancel Transaction FAILURE - Invalid Authentication Token - " +
                                 "LandlordID: [" + input.User.LandlordId + "], AccessToken: [" + input.User.LandlordId +
                                 "]" +
                                 "TransactionID: [" + input.TransId + "]");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("RentTrans Cntrlr -> CancelTransaction FAILED - Outer Exception - TransactionID: [" +
                             input.TransId + "], Exception: [" + ex + "]");

                result.msg = "Outer Exception -> Message: [" + ex.Message.ToString() + "]";
            }

            return result;
        }


        [HttpPost]
        [ActionName("GetLandlordsPaymentHistory")]
        public LandlordsPaymentHistoryClass GetLandlordsPaymentHistory(basicLandlordPayload input)
        {
            Logger.Info("Rent Trans Cntrlr -> GetLandlordsPaymentHistory Initiated - [LandlordID: " + input.LandlordId +
                        "], [MemberID: " + input.MemberId + "]");

            LandlordsPaymentHistoryClass res = new LandlordsPaymentHistoryClass();
            res.IsSuccess = false;

            if (!String.IsNullOrEmpty(input.LandlordId))
            {
                try
                {
                    Guid landlordGuidId = new Guid(input.LandlordId);

                    res.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordGuidId, input.AccessToken);

                    if (res.AuthTokenValidation.IsTokenOk)
                    {
                        using (NOOCHEntities obj = new NOOCHEntities())
                        {
                            // Get Landlord's details from Landlords Table in DB
                            var landlordObj = (from c in obj.Landlords
                                               where c.LandlordId == landlordGuidId &&
                                                     c.IsDeleted == false
                                               select c).FirstOrDefault();

                            if (landlordObj != null)
                            {
                                List<PaymentHistoryClass> TransactionsListToRet = new List<PaymentHistoryClass>();

                                // Get all PROPERTIES for given Landlord
                                var allProps = (from prop in obj.Properties
                                                where prop.LandlordId == landlordGuidId
                                                select prop).ToList();

                                Logger.Info("GET HISTORY 1.) -> Properties Count in DB: [" + allProps.Count + "]");

                                foreach (Property p in allProps)
                                {
                                    Logger.Info("BEGIN PROPERTY...");
                                    // Get all property UNITS in each property
                                    var allUnitsInProp = (from c in obj.PropertyUnits
                                                          where c.PropertyId == p.PropertyId &&
                                                                c.IsDeleted == false &&
                                                                c.IsOccupied == true
                                                          select c).ToList();

                                    Logger.Info("GET HISTORY 2a.) - UNITS Count [" + allUnitsInProp.Count +
                                                "] in this Property: [" + p.PropertyId + "]");

                                    // Iterating through each occupied unit
                                    short n = 1;
                                    short x = 1;
                                    foreach (PropertyUnit pu in allUnitsInProp)
                                    {
                                        Logger.Info("BEGIN UNIT...");
                                        Logger.Info("GET HISTORY 2b.) - UNIT [" + n + "] -> [" + pu.UnitId +
                                                    "] in Property: [" + p.PropName + "]");
                                        n += 1;

                                        var allOccupiedUnits = (from c in obj.UnitsOccupiedByTenants
                                                                where c.UnitId == pu.UnitId &&
                                                                      c.IsDeleted == false
                                                                select c).ToList();

                                        Logger.Info("GET HISTORY 3a.) - UOBT Count [" + allOccupiedUnits.Count +
                                                    "] for Unit: [" + pu.UnitId + "]");

                                        #region Loop Through UnitsOccupiedByTenants

                                        x = 1;
                                        // Iterating through each occupied unit and checking if any rent for this unit
                                        foreach (UnitsOccupiedByTenant uobt in allOccupiedUnits)
                                        {
                                            Logger.Info("BEGIN UOBT...");
                                            Logger.Info("GET HISTORY 3b.) - UOBT # [" + x + "], UOBT ID -> [" + uobt.Id +
                                                        "] for Unit: [" + pu.UnitId + "]");
                                            x += 1;

                                            try
                                            {
                                                // Get transctions from Transactions table where tenant was sender and lanlord was receiver and transaction type "Rent"

                                                /*var TenantDetails = (from c in obj.Tenants
                                                                     where c.TenantId == uobt.TenantId
                                                                     select c).FirstOrDefault();*/

                                                Logger.Info("GET HISTORY 4.) - UOBT.TenantId is: [" + uobt.TenantId +
                                                            "]");

                                                Guid tenantMemberGuid =
                                                    new Guid(
                                                        CommonHelper.GetTenantsMemberIdFromTenantId(
                                                            uobt.TenantId.ToString()));

                                                var TenantMemberDetails = (from c in obj.Members
                                                                           where c.MemberId == tenantMemberGuid &&
                                                                                 c.IsDeleted == false
                                                                           select c).FirstOrDefault();


                                                var allTrans = (from c in obj.Transactions
                                                                where c.SenderId == TenantMemberDetails.MemberId &&
                                                                      c.RecipientId == landlordObj.MemberId
                                                                select c).ToList();

                                                foreach (Transaction t in allTrans)
                                                {
                                                    Logger.Info("BEGIN TRANSACTION...");
                                                    Logger.Info("TransactionID: [" + t.TransactionId + "]");

                                                    PaymentHistoryClass phc = new PaymentHistoryClass();
                                                    // Data from Transactions Table
                                                    phc.TransactionDate =
                                                        Convert.ToDateTime(t.TransactionDate).ToShortDateString();
                                                    phc.TransactionId = t.TransactionId.ToString();
                                                    phc.Amount = t.Amount.ToString("n2");
                                                    phc.TransactionStatus = t.TransactionStatus;
                                                    phc.Memo = t.Memo;

                                                    // Data from PropertyUnits Table
                                                    phc.UnitName = pu.UnitNickName;
                                                    phc.UnitNum = pu.UnitNumber;
                                                    phc.UnitId = pu.UnitId.ToString();
                                                    phc.DueDate = pu.DueDate;

                                                    // Data from Property Table
                                                    phc.PropertyId = p.PropertyId.ToString();
                                                    phc.PropertyName = p.PropName;
                                                    phc.PropertyAddress = p.AddressLineOne;

                                                    // Data from Members Table
                                                    phc.TenantId = TenantMemberDetails.MemberId.ToString();
                                                    phc.TenantStatus = TenantMemberDetails.Status;
                                                    phc.TenantName =
                                                        !String.IsNullOrEmpty(TenantMemberDetails.FirstName)
                                                            ? CommonHelper.UppercaseFirst(
                                                                CommonHelper.GetDecryptedData(
                                                                    TenantMemberDetails.FirstName)) + " " +
                                                              CommonHelper.UppercaseFirst(
                                                                  CommonHelper.GetDecryptedData(
                                                                      TenantMemberDetails.LastName))
                                                            : "";
                                                    phc.TenantEmail =
                                                        !String.IsNullOrEmpty(TenantMemberDetails.UserName)
                                                            ? CommonHelper.GetDecryptedData(TenantMemberDetails.UserName)
                                                            : null;

                                                    TransactionsListToRet.Add(phc);
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                Logger.Error(
                                                    "Rent Trans Cntrlr -> GetLandlordsPaymentHistory EXCEPTION (Inner) - [LandlordID: " +
                                                    input.LandlordId + "], [Exception: " + ex.Message + " ]");
                                            }
                                        }

                                        #endregion Loop Through UnitsOccupiedByTenants
                                    }
                                }

                                res.IsSuccess = true;
                                res.Transactions = TransactionsListToRet;
                                res.ErrorMessage = "Success";
                            }
                            else
                            {
                                Logger.Error(
                                    "Rent Trans Cntrlr -> GetLandlordsPaymentHistory LANDLORD NOT FOUND - [LandlordID: " +
                                    input.LandlordId + "]");
                                res.ErrorMessage = "Invalid Landlord ID.";
                            }
                        }
                    }
                    else
                    {
                        Logger.Error(
                            "Rent Trans Cntrlr -> GetLandlordsPaymentHistory AUTH TOKEN FAILURE - [LandlordID: " +
                            input.LandlordId + "]");
                        res.ErrorMessage = "Auth token failure";
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Rent Trans Cntrlr -> GetLandlordsPaymentHistory EXCEPTION (Outer) - [LandlordID: " +
                                 input.LandlordId + "], [Exception: " + ex.InnerException + " ]");
                    res.ErrorMessage = "Server exception.";
                }
            }

            return res;
        }


        // CLIFF (12/8/15): I was playing around with this method because we need a different procedure for
        //                  getting a Landlords transaction history. The above original method works MOST of the
        //                  time, but not for many situations (see Asana).  So this method below does NOT work and is
        //                  unfinished :-).
        [HttpPost]
        [ActionName("GetLandlordsPaymentHistoryTEST")]
        public LandlordsPaymentHistoryClass GetLandlordsPaymentHistoryTEST(basicLandlordPayload input)
        {
            Logger.Info("Rent Trans Cntrlr -> GetLandlordsPaymentHistory Initiated - [LandlordID: " + input.LandlordId +
                        "], [MemberID: " + input.MemberId + "]");

            LandlordsPaymentHistoryClass res = new LandlordsPaymentHistoryClass();
            res.IsSuccess = false;

            if (!String.IsNullOrEmpty(input.LandlordId))
            {
                try
                {
                    Guid landlordGuidId = new Guid(input.LandlordId);

                    res.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordGuidId, input.AccessToken);

                    if (res.AuthTokenValidation.IsTokenOk)
                    {
                        using (NOOCHEntities obj = new NOOCHEntities())
                        {
                            // Get Landlord's details from Landlords Table in DB
                            var landlordObj = (from c in obj.Landlords
                                               where c.LandlordId == landlordGuidId &&
                                                     c.IsDeleted == false
                                               select c).FirstOrDefault();

                            if (landlordObj != null)
                            {
                                List<PaymentHistoryClass> TransactionsListToRet = new List<PaymentHistoryClass>();

                                // First, find all Transactions with this Landlord's MemberID
                                var allTrans1 = (from c in obj.Transactions
                                                 where c.SenderId == landlordObj.MemberId ||
                                                       c.RecipientId == landlordObj.MemberId
                                                 select c).ToList();

                                foreach (Transaction t in allTrans1)
                                {

                                }












                                // Get all PROPERTIES for given Landlord
                                var allProps = (from prop in obj.Properties
                                                where prop.LandlordId == landlordGuidId
                                                select prop).ToList();

                                Logger.Info("GET HISTORY 1.) -> Properties Count in DB: [" + allProps.Count + "]");

                                foreach (Property p in allProps)
                                {
                                    Logger.Info("BEGIN PROPERTY...");
                                    // Get all property UNITS in each property
                                    var allUnitsInProp = (from c in obj.PropertyUnits
                                                          where c.PropertyId == p.PropertyId &&
                                                                c.IsDeleted == false &&
                                                                c.IsOccupied == true
                                                          select c).ToList();

                                    Logger.Info("GET HISTORY 2a.) - UNITS Count [" + allUnitsInProp.Count +
                                                "] in this Property: [" + p.PropertyId + "]");

                                    // Iterating through each occupied unit
                                    short n = 1;
                                    short x = 1;
                                    foreach (PropertyUnit pu in allUnitsInProp)
                                    {
                                        Logger.Info("BEGIN UNIT...");
                                        Logger.Info("GET HISTORY 2b.) - UNIT [" + n + "] -> [" + pu.UnitId +
                                                    "] in Property: [" + p.PropName + "]");
                                        n += 1;

                                        var allOccupiedUnits = (from c in obj.UnitsOccupiedByTenants
                                                                where c.UnitId == pu.UnitId &&
                                                                      c.IsDeleted == false
                                                                select c).ToList();

                                        Logger.Info("GET HISTORY 3a.) - UOBT Count [" + allOccupiedUnits.Count +
                                                    "] for Unit: [" + pu.UnitId + "]");

                                        #region Loop Through UnitsOccupiedByTenants

                                        x = 1;
                                        // Iterating through each occupied unit and checking if any rent for this unit
                                        foreach (UnitsOccupiedByTenant uobt in allOccupiedUnits)
                                        {
                                            Logger.Info("BEGIN UOBT...");
                                            Logger.Info("GET HISTORY 3b.) - UOBT # [" + x + "], UOBT ID -> [" + uobt.Id +
                                                        "] for Unit: [" + pu.UnitId + "]");
                                            x += 1;

                                            try
                                            {
                                                // Get transctions from Transactions table where tenant was sender and lanlord was receiver and transaction type "Rent"

                                                /*var TenantDetails = (from c in obj.Tenants
                                                                     where c.TenantId == uobt.TenantId
                                                                     select c).FirstOrDefault();*/

                                                Logger.Info("GET HISTORY 4.) - UOBT.TenantId is: [" + uobt.TenantId +
                                                            "]");

                                                Guid tenantMemberGuid =
                                                    new Guid(
                                                        CommonHelper.GetTenantsMemberIdFromTenantId(
                                                            uobt.TenantId.ToString()));

                                                var TenantMemberDetails = (from c in obj.Members
                                                                           where c.MemberId == tenantMemberGuid &&
                                                                                 c.IsDeleted == false
                                                                           select c).FirstOrDefault();


                                                var allTrans = (from c in obj.Transactions
                                                                where c.SenderId == TenantMemberDetails.MemberId &&
                                                                      c.RecipientId == landlordObj.MemberId
                                                                select c).ToList();

                                                foreach (Transaction t in allTrans)
                                                {
                                                    Logger.Info("BEGIN TRANSACTION...");
                                                    Logger.Info("TransactionID: [" + t.TransactionId + "]");

                                                    PaymentHistoryClass phc = new PaymentHistoryClass();
                                                    // Data from Transactions Table
                                                    phc.TransactionDate =
                                                        Convert.ToDateTime(t.TransactionDate).ToShortDateString();
                                                    phc.TransactionId = t.TransactionId.ToString();
                                                    phc.Amount = t.Amount.ToString("n2");
                                                    phc.TransactionStatus = t.TransactionStatus;
                                                    phc.Memo = t.Memo;

                                                    // Data from PropertyUnits Table
                                                    phc.UnitName = pu.UnitNickName;
                                                    phc.UnitNum = pu.UnitNumber;
                                                    phc.UnitId = pu.UnitId.ToString();
                                                    phc.DueDate = pu.DueDate;

                                                    // Data from Property Table
                                                    phc.PropertyId = p.PropertyId.ToString();
                                                    phc.PropertyName = p.PropName;
                                                    phc.PropertyAddress = p.AddressLineOne;

                                                    // Data from Members Table
                                                    phc.TenantId = TenantMemberDetails.MemberId.ToString();
                                                    phc.TenantStatus = TenantMemberDetails.Status;
                                                    phc.TenantName =
                                                        !String.IsNullOrEmpty(TenantMemberDetails.FirstName)
                                                            ? CommonHelper.UppercaseFirst(
                                                                CommonHelper.GetDecryptedData(
                                                                    TenantMemberDetails.FirstName)) + " " +
                                                              CommonHelper.UppercaseFirst(
                                                                  CommonHelper.GetDecryptedData(
                                                                      TenantMemberDetails.LastName))
                                                            : "";
                                                    phc.TenantEmail =
                                                        !String.IsNullOrEmpty(TenantMemberDetails.UserName)
                                                            ? CommonHelper.GetDecryptedData(TenantMemberDetails.UserName)
                                                            : null;

                                                    TransactionsListToRet.Add(phc);
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                Logger.Error(
                                                    "Rent Trans Cntrlr -> GetLandlordsPaymentHistory EXCEPTION (Inner) - [LandlordID: " +
                                                    input.LandlordId + "], [Exception: " + ex.Message + " ]");
                                            }
                                        }

                                        #endregion Loop Through UnitsOccupiedByTenants
                                    }
                                }

                                res.IsSuccess = true;
                                res.Transactions = TransactionsListToRet;
                                res.ErrorMessage = "Success";
                            }
                            else
                            {
                                Logger.Error(
                                    "Rent Trans Cntrlr -> GetLandlordsPaymentHistory LANDLORD NOT FOUND - [LandlordID: " +
                                    input.LandlordId + "]");
                                res.ErrorMessage = "Invalid Landlord ID.";
                            }
                        }
                    }
                    else
                    {
                        Logger.Error(
                            "Rent Trans Cntrlr -> GetLandlordsPaymentHistory AUTH TOKEN FAILURE - [LandlordID: " +
                            input.LandlordId + "]");
                        res.ErrorMessage = "Auth token failure";
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Rent Trans Cntrlr -> GetLandlordsPaymentHistory EXCEPTION (Outer) - [LandlordID: " +
                                 input.LandlordId + "], [Exception: " + ex.InnerException + " ]");
                    res.ErrorMessage = "Server exception.";
                }
            }

            return res;
        }


        [HttpPost]
        [ActionName("SaveMemoFormula")]
        public GenericInternalResponse SaveMemoFormula(SaveMemoFormulaInputClass input)
        {
            Logger.Info("RentTrans Cntrlr -> EditUserInfo Initiated - LandlordID: [" + input.User.LandlordId +
                        "], FormulaToUse: [" + input.formulaToUse + "]");

            GenericInternalResponse result = new GenericInternalResponse();
            result.success = false;
            result.msg = "Initial";

            try
            {
                Guid landlordguidId = new Guid(input.User.LandlordId);
                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordguidId, input.User.AccessToken);

                if (result.AuthTokenValidation.IsTokenOk)
                {
                    using (NOOCHEntities obj = new NOOCHEntities())
                    {
                        // Get details from DB
                        var landlordObj = (from c in obj.Landlords
                                           where c.LandlordId == landlordguidId
                                           select c).FirstOrDefault();

                        if (landlordObj != null)
                        {
                            // Key for "FormulaToUse" values -- NOTE for future reference
                            // 1: Property name + Unit number
                            // 2: Property name + Unit number + Month
                            // 3: Property name + Unit number + Tenant Last Name (space permitting)

                            landlordObj.MemoFormula = Convert.ToInt16(input.formulaToUse);
                            landlordObj.DateModified = DateTime.Now;

                            if (obj.SaveChanges() > 0)
                            {
                                result.success = true;
                                result.msg = "OK";
                            }
                            else
                            {
                                Logger.Error(
                                    "RentTrans Cntrlr -> SaveMemoFormula FAILED - Error while saving updates to DB - " +
                                    "LandlordID: " + input.User.LandlordId + "]");
                                result.msg = "Error while saving updates to DB";
                            }
                        }
                        else
                        {
                            Logger.Error(
                                "RentTrans Cntrlr -> SaveMemoFormula FAILED - Landlord ID Not Found - LandlordID: [" +
                                input.User.LandlordId + "]");
                            result.msg = "Given landlord ID not found.";
                        }
                    }
                }
                else
                {
                    result.msg = result.AuthTokenValidation.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("RentTrans Cntrlr  -> SaveMemoFormula FAILED - LandlordID: [" + input.User.LandlordId +
                             "], [Outer Exception: " + ex.ToString() + "]");
                result.msg = "Server error: outer exception.";
            }

            return result;
        }

        // New Transaction History Method which uses new table for information.... RentTransactions
        [HttpPost]
        [ActionName("GetLandlordsPaymentHistoryFromRentTrans")]
        public LandlordsPaymentHistoryClass GetLandlordsPaymentHistoryFromRentTrans(basicLandlordPayload input)
        {
            Logger.Info("Rent Trans Cntrlr -> GetLandlordsPaymentHistoryFromRentTrans Initiated - [LandlordID: " +
                        input.LandlordId + "], [MemberID: " + input.MemberId + "]");

            LandlordsPaymentHistoryClass res = new LandlordsPaymentHistoryClass();
            res.IsSuccess = false;

            if (!String.IsNullOrEmpty(input.LandlordId))
            {
                try
                {
                    Guid landlordGuidId = new Guid(input.LandlordId);

                    res.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordGuidId, input.AccessToken);

                    if (res.AuthTokenValidation.IsTokenOk)
                    {
                        using (NOOCHEntities obj = new NOOCHEntities())
                        {
                            // Get Landlord's details from Landlords Table in DB
                            var landlordObj = (from c in obj.Landlords
                                               where c.LandlordId == landlordGuidId &&
                                                     c.IsDeleted == false
                                               select c).FirstOrDefault();

                            if (landlordObj != null)
                            {
                                List<PaymentHistoryClass> TransactionsListToRet = new List<PaymentHistoryClass>();

                                // getting data from rent trans table
                                var allLandlordTrans =
                                    (from c in obj.RentTransactions
                                     where c.LandlordId == landlordGuidId && c.IsDeleted == false
                                     select c).OrderByDescending(m => m.TransCreatedOn).ToList();

                                foreach (RentTransaction rentTrans in allLandlordTrans)
                                {
                                    PaymentHistoryClass phc = new PaymentHistoryClass();
                                    phc.TenantId = rentTrans.TenantId.ToString();
                                    phc.TenantStatus = rentTrans.TransactionStatus;
                                    phc.TransactionCreateDate = rentTrans.TransCreatedOn.ToString();
                                    phc.TransactionProcessDate = (rentTrans.TransRespondedOn == null)
                                        ? ""
                                        : rentTrans.TransRespondedOn.ToString();
                                    phc.UOBTId = rentTrans.UOBTId.ToString();

                                    phc.IsDisputed = rentTrans.IsDisputed != null &&
                                                     Convert.ToBoolean(rentTrans.IsDisputed);
                                    phc.DisputeStatus = rentTrans.DisputeStatus;

                                    phc.Memo = rentTrans.Memo;
                                    phc.Amount = rentTrans.Amount;

                                    phc.IsRecurringTrans = rentTrans.IsRecurring != null &&
                                                           Convert.ToBoolean(rentTrans.IsRecurring);

                                    if (phc.IsRecurringTrans)
                                    {
                                        phc.NextRecurrTransDueDate = (rentTrans.NextRecurrTransDueDate == null)
                                            ? ""
                                            : rentTrans.NextRecurrTransDueDate.ToString();
                                    }

                                    TransactionsListToRet.Add(phc);
                                }
                                res.IsSuccess = true;
                                res.Transactions = TransactionsListToRet;
                                res.ErrorMessage = "Success";
                            }
                            else
                            {
                                Logger.Error(
                                    "Rent Trans Cntrlr -> GetLandlordsPaymentHistoryFromRentTrans LANDLORD NOT FOUND - [LandlordID: " +
                                    input.LandlordId + "]");
                                res.ErrorMessage = "Invalid Landlord ID.";
                            }
                        }
                    }
                    else
                    {
                        Logger.Error(
                            "Rent Trans Cntrlr -> GetLandlordsPaymentHistoryFromRentTrans AUTH TOKEN FAILURE - [LandlordID: " +
                            input.LandlordId + "]");
                        res.ErrorMessage = "Auth token failure";
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(
                        "Rent Trans Cntrlr -> GetLandlordsPaymentHistoryFromRentTrans EXCEPTION (Outer) - [LandlordID: " +
                        input.LandlordId + "], [Exception: " + ex.InnerException + " ]");
                    res.ErrorMessage = "Server exception.";
                }
            }

            return res;
        }



        [HttpPost]
        [ActionName("ChargeTenantRentTrans")]
        public CreatePropertyResultOutput ChargeTenantRentTrans(ChargeTenantInputClass input)
        {
            Logger.Info("Landlords API -> RentTrans -> ChargeTenantRentTrans - Requested by [" + input.User.LandlordId + "]");

            CreatePropertyResultOutput result = new CreatePropertyResultOutput();
            result.IsSuccess = false;

            try
            {
                Guid Landlord_GUID = CommonHelper.ConvertToGuid(input.User.LandlordId);
                Guid Tenant_GUID = CommonHelper.ConvertToGuid(input.TransRequest.TenantId);
                Guid landlordsMemID = new Guid(CommonHelper.GetLandlordsMemberIdFromLandlordId(Landlord_GUID));

                string requestId = "";

                #region All Checks Before Execution

                // Check uniqueness of requesting and sending user
                if (Landlord_GUID == Tenant_GUID)
                {
                    result.ErrorMessage = "Not allowed to request money from yourself.";
                    return result;
                }

                // Check if request Amount is over per-transaction limit
                decimal transactionAmount = Convert.ToDecimal(input.TransRequest.Amount);

                if (CommonHelper.isOverTransactionLimit(transactionAmount, input.TransRequest.TenantId,
                    input.User.LandlordId))
                {
                    result.ErrorMessage = "To keep Nooch safe, the maximum amount you can request is $" +
                                          Convert.ToDecimal(
                                              CommonHelper.GetValueFromConfig("MaximumTransferLimitPerTransaction"))
                                              .ToString("F2");
                    return result;
                }

                // Get requester and request recepient Members table info
                var requester = CommonHelper.GetMemberByMemberId(landlordsMemID);
                var requesterLandlordObj = CommonHelper.GetLandlordByLandlordId(Landlord_GUID);
                // Only need this for the Photo for the email template... Members table doesn't have it.
                var requestRecipient = CommonHelper.GetMemberByMemberId(Tenant_GUID);


                if (requester == null)
                {
                    Logger.Error(
                        "Landlords API -> RentTrans -> ChargeTenantRentTrans FAILED - Requester Member Not Found - [MemberID: " +
                        Landlord_GUID + "]");
                    result.ErrorMessage = "Requester Member Not Found";

                    return result;
                }
                if (requestRecipient == null)
                {
                    Logger.Error(
                        "Landlords API -> RentTrans -> ChargeTenantRentTrans FAILED - requestRecipient (who would pay the request) Member Not Found - [MemberID: " +
                        Landlord_GUID + "]");
                    result.ErrorMessage = "Request Recipient Member Not Found";

                    return result;
                }

                #region Get Request Sender's Synapse Account Details


                var requestorSynInfo =
                    CommonHelper.GetSynapseBankAndUserDetailsforGivenMemberId(landlordsMemID.ToString());

                if (requestorSynInfo.wereBankDetailsFound != true)
                {
                    Logger.Error(
                        "Landlords API -> RentTrans -> ChargeTenantRentTrans FAILED -> Request ABORTED: Requester's Synapse bank account NOT FOUND - Request Creator MemberId is: [" +
                        requester.MemberId + "]");
                    result.ErrorMessage = "Requester does not have any bank added";

                    return result;
                }

                // Check Requestor's Synapse Bank Account status
                if (requestorSynInfo.BankDetails != null &&
                    requestorSynInfo.BankDetails.Status != "Verified" &&
                    requester.IsVerifiedWithSynapse != true)
                {
                    Logger.Error(
                        "Landlords API -> RentTrans -> ChargeTenantRentTrans FAILED -> Request ABORTED: Requester's Synapse bank account exists but is not Verified and " +
                        "isVerifiedWithSynapse != true - Request Creator memberId is: [" + requester.MemberId + "]");
                    result.ErrorMessage = "Requester does not have any verified bank account.";

                    return result;
                }

                #endregion Get Sender's Synapse Account Details

                // @Cliff.. feel free to comment this section if you don't want to check Tenant synapse details

                #region Get Request Sender's Synapse Account Details

                /*var requestRecipientSynInfo = CommonHelper.GetSynapseBankAndUserDetailsforGivenMemberId(input.TransRequest.TenantId);

                if (requestRecipientSynInfo.wereBankDetailsFound != true)
                {
                    Logger.Error("Landlords API -> RentTrans -> ChargeTenantRentTrans FAILED -> Request ABORTED: Request Recipient's Synapse bank account NOT FOUND - Request Recipient MemberID: [" + input.TransRequest.TenantId + "]");
                    result.ErrorMessage = "Request recipient does not have any bank added";

                    return result;
                }

                // Check Request recepient's Synapse Bank Account status
                if (requestRecipientSynInfo.BankDetails != null &&
                    requestRecipientSynInfo.BankDetails.Status != "Verified" &&
                    requestRecipient.IsVerifiedWithSynapse != true)
                {
                    Logger.Error("Landlords API -> RentTrans -> ChargeTenantRentTrans FAILED -> Request ABORTED: Request Recipient's Synapse bank account exists but is not Verified and " +
                        "isVerifiedWithSynapse != true - Request Recipient MemberID is: [" + input.TransRequest.TenantId + "]");

                    result.ErrorMessage = "Request recipient does not have any verified bank account.";
                    return result;
                }*/

                #endregion Get Sender's Synapse Account Details

                #endregion All Checks Before Execution


                #region Create new transaction in transactions table

                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    RentTransaction tr = new RentTransaction();
                    tr.RentTransactionId = Guid.NewGuid();
                    tr.TenantId = Tenant_GUID;
                    tr.LandlordId = Landlord_GUID;
                    tr.Amount = input.TransRequest.Amount;
                    tr.TransCreatedOn = DateTime.Now;
                    tr.Memo = input.TransRequest.Memo; // this would be the reason why we are charging tenant 
                    tr.DisputeStatus = null;
                    tr.TransactionStatus = "Pending";
                    tr.TransactionType = CommonHelper.GetEncryptedData("Request");
                    tr.UOBTId = input.TransRequest.UOBTId;

                    tr.IsDeleted = false;
                    tr.IsDisputed = false;

                    tr.IsRecurring = false; // TBD with cliff.. if admin can send recurring type trans request.

                    try
                    {
                        obj.RentTransactions.Add(tr);
                        obj.SaveChanges();
                        requestId = tr.RentTransactionId.ToString();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(
                            "Landlords API -> RentTrans -> ChargeTenantRentTrans FAILED - Unable to save RentTransaction in DB - [Requester MemberID:" +
                            input.User.LandlordId + "], [Exception: [ " + ex.InnerException + " ]");
                        result.IsSuccess = false;
                        result.ErrorMessage = "Request failed.";
                        return result;
                    }
                }

                #endregion

                #region Send Notifications

                #region Set Up Variables

                string fromAddress = CommonHelper.GetValueFromConfig("transfersMail");

                string RequesterFirstName = !String.IsNullOrEmpty(requester.FirstName)
                    ? CommonHelper.UppercaseFirst((CommonHelper.GetDecryptedData(requester.FirstName)))
                    : "";
                string RequesterLastName = !String.IsNullOrEmpty(requester.LastName)
                    ? CommonHelper.UppercaseFirst((CommonHelper.GetDecryptedData(requester.LastName)))
                    : "";
                string RequesterEmail = CommonHelper.GetDecryptedData(requester.UserName);

                string RequestReceiverFirstName = !String.IsNullOrEmpty(requestRecipient.FirstName)
                    ? CommonHelper.UppercaseFirst((CommonHelper.GetDecryptedData(requestRecipient.FirstName)))
                    : "";
                string RequestReceiverLastName = !String.IsNullOrEmpty(requestRecipient.LastName)
                    ? CommonHelper.UppercaseFirst((CommonHelper.GetDecryptedData(requestRecipient.LastName)))
                    : "";
                string RequestReceiverFullName = (RequestReceiverFirstName.Length > 2 &&
                                                  RequestReceiverLastName.Length > 2)
                    ? RequestReceiverFirstName + " " + RequestReceiverLastName
                    : CommonHelper.GetDecryptedData(requestRecipient.UserName);

                Logger.Info("RequesterFirstName: [" + RequesterFirstName + "], RequesterLastName: [" + RequesterLastName +
                            "], RequestReceiverFirstName: [" + RequestReceiverFirstName +
                            "], RequestReceiverLastName: [" + RequestReceiverLastName + "], RequestReceiverFullName: [" +
                            RequestReceiverFullName + "]");

                string requesterPic = "https://www.noochme.com/noochweb/Assets/Images/userpic-default.png";
                if (!String.IsNullOrEmpty(requesterLandlordObj.UserPic) && requesterLandlordObj.UserPic.Length > 20)
                {
                    requesterPic = requesterLandlordObj.UserPic;
                }
                else if (!String.IsNullOrEmpty(requester.Photo) && requester.Photo.Length > 20)
                {
                    requesterPic = requester.Photo;
                }

                string cancelLink = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"),
                    "Nooch/CancelRequest?TransactionId=" + requestId + "&MemberId=" + input.User.LandlordId +
                    "&UserType=6KX3VJv3YvoyK+cemdsvMA==");
                //"trans/CancelRequest.aspx?TransactionId=" + requestId + "&MemberId=" + input.User.LandlordId +
                //   "&UserType=6KX3VJv3YvoyK+cemdsvMA==");

                string wholeAmount = Convert.ToDecimal(input.TransRequest.Amount).ToString("n2");
                string[] amountArray = wholeAmount.Split('.');

                string memo = "";
                if (!string.IsNullOrEmpty(input.TransRequest.Memo))
                {
                    if (input.TransRequest.Memo.Length > 3)
                    {
                        string firstThreeChars = input.TransRequest.Memo.Substring(0, 3).ToLower();
                        bool startWithFor = firstThreeChars.Equals("for");

                        if (startWithFor)
                        {
                            memo = input.TransRequest.Memo.ToString();
                        }
                        else
                        {
                            memo = "for " + input.TransRequest.Memo.ToString();
                        }
                    }
                    else
                    {
                        memo = "for " + input.TransRequest.Memo.ToString();
                    }
                }

                #endregion Set Up Variables


                // Send email to REQUESTER (person who sent this request)

                #region Email To Requester

                try
                {
                    var tokens = new Dictionary<string, string>
                    {
                        {Constants.PLACEHOLDER_FIRST_NAME, RequesterFirstName},
                        {Constants.PLACEHOLDER_NEWUSER, RequestReceiverFullName},
                        {Constants.PLACEHOLDER_TRANSFER_AMOUNT, amountArray[0]},
                        {Constants.PLACEHLODER_CENTS, amountArray[1]},
                        {Constants.PLACEHOLDER_OTHER_LINK, cancelLink},
                        {Constants.MEMO, memo}
                    };

                    Logger.Info("RentTrans Ctrlr -> ChargeTenantRentTrans - Memo: [" + memo + "], wholeAmount: [" +
                                wholeAmount + "], cancelLink: [" + cancelLink +
                                "], toAddress: [" + RequesterEmail + "], fromAddress: [" + fromAddress + "]");

                    CommonHelper.SendEmail("requestSent", fromAddress, null, RequesterEmail,
                        "Your payment request to " + RequestReceiverFullName +
                        " is pending", tokens, null, null);

                    Logger.Info("Landlords API -> RentTrans -> ChargeTenantRentTrans -> RequestSent email sent to [" +
                                RequesterEmail + "] successfully.");
                }
                catch (Exception ex)
                {
                    Logger.Error(
                        "Landlords API -> RentTrans -> ChargeTenantRentTrans -> RequestSent email NOT sent to [" +
                        RequesterEmail +
                        "], [Exception: " + ex + "]");
                }

                #endregion Email To Requester


                #region Email To Request Recipient

                // Send email to REQUEST RECIPIENT (person who will pay/reject this request)
                // Include 'UserType', 'LinkSource', and 'TransType' as encrypted along with request
                // In this case UserType would = 'Nonregistered'  ->  6KX3VJv3YvoyK+cemdsvMA==
                //              TransType would = 'Request'
                //              LinkSource would = 'Email'



                // Malkit 14-12-2015 
                // We need to make another landing page for accepting rent payments...or we have to make changes in existing page... To make it work with existing landling page we have to add another variable in query string...this variable
                // will tell us, which table to check for transaction info...but for this we have to make changes every where in the method where user wwas being charged..
                // I recomment separate landing page for handing these types if requests... what you think @Cliff ?

                // this link wouble take user to some new page or modified code if existing page.
                string rejectLink = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"),
                      "Nooch/RejectMoney?TransactionId=" + requestId +
                    "&UserType=6KX3VJv3YvoyK+cemdsvMA==&LinkSource=75U7bZRpVVxLNbQuoMQEGQ==&TransType=T3EMY1WWZ9IscHIj3dbcNw==");
                //"trans/rejectMoney.aspx?TransactionId=" + requestId +
                //"&UserType=6KX3VJv3YvoyK+cemdsvMA==&LinkSource=75U7bZRpVVxLNbQuoMQEGQ==&TransType=T3EMY1WWZ9IscHIj3dbcNw==");

                // this link wouble take user to some new page or modified code if existing page.

                string paylink = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"),
                    "Nooch/PayRequest?TransactionId=" + requestId + "&UserType=6KX3VJv3YvoyK+cemdsvMA==");

                //"trans/payRequest.aspx?TransactionId=" + requestId + "&UserType=6KX3VJv3YvoyK+cemdsvMA==");


                var tokens2 = new Dictionary<string, string>
                {
                    {Constants.PLACEHOLDER_FIRST_NAME, RequestReceiverFirstName},
                    {Constants.PLACEHOLDER_USER_PICTURE, requesterPic},
                    {Constants.PLACEHOLDER_SENDER_FULL_NAME, RequesterFirstName + " " + RequesterLastName},
                    {Constants.PLACEHOLDER_TRANSFER_AMOUNT, amountArray[0]},
                    {Constants.PLACEHLODER_CENTS, amountArray[1]},
                    {Constants.MEMO, memo},
                    {Constants.PLACEHOLDER_REJECT_LINK, rejectLink},
                    {Constants.PLACEHOLDER_PAY_LINK, paylink},
                    {Constants.PLACEHOLDER_FRIEND_FIRST_NAME, RequesterFirstName}
                };

                string toAddress = CommonHelper.GetDecryptedData(requestRecipient.UserName);

                try
                {
                    CommonHelper.SendEmail("requestReceivedToExistingNonRegUser", fromAddress,
                        RequesterFirstName + " " + RequesterLastName, toAddress,
                        RequesterFirstName + " " + RequesterLastName + " requested " + "$" + wholeAmount.ToString() +
                        " with Nooch", tokens2, null, null);

                    Logger.Info("RentTrans -> ChargeTenant ->  ChargeTenantRentTrans email sent to [" + toAddress +
                                "] successfully");
                }
                catch (Exception ex)
                {
                    Logger.Error("RentTrans -> ChargeTenant -> ChargeTenantRentTrans email NOT sent to  [" + toAddress +
                                 "], [Exception: " + ex + "]");
                }

                #endregion Email To Request Recipient


                // Send SMS to REQUEST RECIPIENT (person who will pay/reject this request)
                // CLIFF (10/20/15) This block works (tested successfully) but commenting out b/c the Deposit-Money landing page
                //                  needs to be improved for Mobile screen sizes... not a great experience as it currently is.
                // Malkit Block code fixed to work with landlords api

                #region Send SMS To Non-Nooch Transfer Recipient

                /*  string googleUrlAPIKey = CommonHelper.GetValueFromConfig("GoogleURLAPI");

                // shortening URLs from Google
                string RejectShortLink = rejectLink;
                string AcceptShortLink = paylink;

                #region Call Google URL Shortener API

                try
                {
                    var cli = new WebClient();
                    cli.Headers[HttpRequestHeader.ContentType] = "application/json";
                    string response = cli.UploadString("https://www.googleapis.com/urlshortener/v1/url?key=" + googleUrlAPIKey, "{longUrl:\"" + rejectLink + "\"}");
                    googleURLShortnerResponseClass googlerejectshortlinkresult = JsonConvert.DeserializeObject<googleURLShortnerResponseClass>(response);

                    if (googlerejectshortlinkresult != null)
                    {
                        RejectShortLink = googlerejectshortlinkresult.id;
                    }
                    else
                    {
                        // Google short URL API broke...
                        Logger.Error("Landlords API -> RentTrans -> ChargeTenant -> - GoogleAPI FAILED for Reject Short Link.");
                    }
                    cli.Dispose();

                    // Now shorten Accept link

                    var cli2 = new WebClient();
                    cli2.Headers[HttpRequestHeader.ContentType] = "application/json";
                    string response2 = cli2.UploadString("https://www.googleapis.com/urlshortener/v1/url?key=" + googleUrlAPIKey, "{longUrl:\"" + paylink + "\"}");
                    googleURLShortnerResponseClass googlerejectshortlinkresult2 = JsonConvert.DeserializeObject<googleURLShortnerResponseClass>(response2);

                    if (googlerejectshortlinkresult2 != null)
                    {
                        AcceptShortLink = googlerejectshortlinkresult2.id;
                    }
                    else
                    {
                        // Google short URL API broke...
                        Logger.Error("Landlords API -> RentTrans -> ChargeTenant -> - GoogleAPI FAILED for Accept Short Link.");
                    }
                    cli2.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Error("Landlords API -> RentTrans -> ChargeTenant -> - GoogleAPI FAILED. [Exception: " + ex + "]");
                }

                #endregion Call Google URL Shortener API

                string toPhoneNumber = requestRecipient.ContactNumber;

                try
                {
                    // Example SMS string: "Cliff Canan charged you $10 using Nooch, a free app for {Memo}. Click here to pay: {LINK}. Or here to reject: {LINK}"

                    // Make sure URL is short version (if Google API failued, use long version of Pay link and exclude the Reject link to save space)
                    string SMSContent;

                    if (AcceptShortLink.Length < 30) // Google Short links should be ~ 21 characters
                    {
                        string memoTxtForSms = memo;
                        if (memoTxtForSms.Length < 2)
                        {
                            memoTxtForSms = "from you";
                        }

                        SMSContent = RequesterFirstName + " " + RequesterLastName + " charged you $" +
                                              amountArray[0] + "." + amountArray[1] + " " +
                                              memoTxtForSms +
                                              " using Nooch for " + input.TransRequest.Memo + ". Tap here to pay: " + AcceptShortLink +
                                              ". Or reject: " + RejectShortLink;
                    }
                    else // Google Short link API broke, use long version of Pay link
                    {
                        SMSContent = RequesterFirstName + " " + RequesterLastName + " charged you $" +
                                              amountArray[0] + "." + amountArray[1] +
                                              " using Nooch for " + input.TransRequest.Memo + ". Tap here to pay: " + AcceptShortLink;
                    }

                    string result2 = CommonHelper.SendSMS(toPhoneNumber, SMSContent, "");

                    Logger.Info("Landlords API -> RentTrans -> ChargeTenant -> SMS sent to recipient [" + toPhoneNumber + "] successfully. [Msg: " + result + "]");
                }
                catch (Exception ex)
                {
                    Logger.Error("Landlords API -> RentTrans -> ChargeTenant -> SMS NOT sent to recipient [" + toPhoneNumber +
                                           "], [Exception:" + ex + "]");
                }*/

                #endregion Send SMS To Non-Nooch Transfer Recipient


                #endregion Send Notifications

                result.IsSuccess = true;
                result.ErrorMessage = "Ok";
            }
            catch (Exception ex)
            {
                Logger.Error("Landlords API -> RentTrans -> ChargeTenantRentTrans FAILED - [LandlordID: " +
                             input.User.LandlordId + "], [Exception: [ " + ex + " ]");
                result.ErrorMessage = "Error while chargin tenant. Retry later!";
            }

            return result;
        }


        /*
        public string RequestMoneyToNonNoochUserUsingSynapse(RequestDto requestDto, out string requestId)
        {
            MemberDataAccess mda = new MemberDataAccess();
            var checkuser = mda.GetMemberIdByUserName(requestDto.MoneySenderEmailId);

            if (checkuser == null)
            {
                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    requestId = string.Empty;
                    Logger.Info("TDA -> RequestMoneyToNonNoochUserUsingSynapse - Requestor MemberId: [" + requestDto.MemberId + "].");

                    var noochConnection = new NOOCHEntities();
                    var memberRepository = new Repository<Members, NOOCHEntities>(noochConnection);

                    var ada = new AccountDataAccess();
                    var requester = ada.GetMember(requestDto.MemberId, memberRepository);


                    #region SenderSynapseAccountDetails

                    var accntDA = new AccountDataAccess();

                    var requestorInfo = accntDA.GetSynapseBankAndUserDetailsforGivenMemberId(requestDto.MemberId.ToString());

                    if (requestorInfo.wereBankDetailsFound == null || requestorInfo.wereBankDetailsFound == false)
                    {
                        return "Requester does not have any bank added";
                    }

                    // Check Requestor's Synapse Bank Account status
                    if (requestorInfo.BankDetails != null &&
                        requestorInfo.BankDetails.Status != "Verified")
                    {
                        Logger.Error("RentTransCntrlr - RequestMoneyToNonNoochUserUsingSynapse -> Transfer Aborted: No verified bank account of Sender. Request Creator memberId is: [" +
                            requestDto.MemberId + "]");
                        return "Requester does not have any verified bank account.";
                    }

                    #endregion SenderSynapseAccountDetails

                    //Request Sent to only one nooch member
                    var transaction = new Transactions
                    {
                        TransactionId = Guid.NewGuid(),
                        MembersReference =
                        {
                            EntityKey = new EntityKey(noochConnection.DefaultContainerName +
                                                      ".Members", "MemberId",
                                                      Utility.ConvertToGuid(requestDto.MemberId))
                        },
                        Members1Reference =
                        {
                            EntityKey = new EntityKey(noochConnection.DefaultContainerName +
                                                      ".Members", "MemberId",
                                                      Utility.ConvertToGuid(requestDto.MemberId))
                        },
                        Amount = requestDto.Amount,
                        TransactionDate = DateTime.Now,
                        Picture = (requestDto.Picture != null) ? requestDto.Picture : null,
                        Memo = (requestDto.Memo == "") ? "" : requestDto.Memo,
                        DisputeStatus = null,
                        TransactionStatus = "Pending",
                        TransactionType = CommonHelper.GetEncryptedData("Request"),
                        DeviceId = requestDto.DeviceId,
                        TransactionTrackingId = GetRandomTransactionTrackingId(),
                        TransactionFee = 0,
                        InvitationSentTo = CommonHelper.GetEncryptedData(requestDto.MoneySenderEmailId),
                        GeoLocations = new GeoLocations
                        {
                            LocationId = Guid.NewGuid(),
                            Latitude = requestDto.Latitude,
                            Longitude = requestDto.Longitude,
                            Altitude = requestDto.Altitude,
                            AddressLine1 = requestDto.AddressLine1,
                            AddressLine2 = requestDto.AddressLine2,
                            City = requestDto.City,
                            State = requestDto.State,
                            Country = requestDto.Country,
                            ZipCode = requestDto.ZipCode,
                            DateCreated = DateTime.Now
                        }
                    };

                    var transactionRepository = new Repository<Transactions, NoochDataEntities>(noochConnection);
                    int dbResult = transactionRepository.AddEntity(transaction);

                    if (dbResult > 0)
                    {
                        requestId = transaction.TransactionId.ToString();

                        #region Email Tenant

                        var fromAddress = CommonHelper.GetValueFromConfig("transfersMail");
                        string s22 = requestDto.Amount.ToString("n2");
                        string[] s32 = s22.Split('.');

                        string RequesterFirstName = CommonHelper.UppercaseFirst((CommonHelper.GetDecryptedData(requester.FirstName)).ToString());
                        string RequesterLastName = CommonHelper.UppercaseFirst((CommonHelper.GetDecryptedData(requester.LastName)).ToString());
                        string otherlink = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"), "trans/CancelRequest.aspx?TransactionId=" + requestId + "&MemberId=" + requestDto.MemberId + "&UserType=U6De3haw2r4mSgweNpdgXQ==");

                        string memo = "";
                        if (transaction.Memo != null && transaction.Memo != "")
                        {
                            if (transaction.Memo.Length > 3)
                            {
                                string firstThreeChars = transaction.Memo.Substring(0, 3).ToLower();
                                bool startWithFor = firstThreeChars.Equals("for");

                                if (startWithFor)
                                {
                                    memo = transaction.Memo.ToString();
                                }
                                else
                                {
                                    memo = "For " + transaction.Memo.ToString();
                                }
                            }
                            else
                            {
                                memo = "For " + transaction.Memo.ToString();
                            }
                        }

                        var tokens = new Dictionary<string, string>
                                                 {
                                                    {Constants.PLACEHOLDER_FIRST_NAME, RequesterFirstName},
                                                    {Constants.PLACEHOLDER_NEWUSER,requestDto.MoneySenderEmailId},
                                                    {Constants.PLACEHOLDER_TRANSFER_AMOUNT,s32[0].ToString()},
                                                    {Constants.PLACEHLODER_CENTS,s32[1].ToString()},
                                                    {Constants.PLACEHOLDER_OTHER_LINK,otherlink},
                                                    {Constants.MEMO,memo}
                                                 };

                        // Send email to Request Receiver -- sending UserType LinkSource TransType as encrypted along with request
                        // In this case UserType would = 'New'
                        // TransType would = 'Request'
                        // and link source would = 'Email'
                        otherlink = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"), "trans/rejectMoney.aspx?TransactionId=" + requestId + "&UserType=U6De3haw2r4mSgweNpdgXQ==&LinkSource=75U7bZRpVVxLNbQuoMQEGQ==&TransType=T3EMY1WWZ9IscHIj3dbcNw==");
                        string paylink = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"), "trans/payRequest.aspx?TransactionId=" + requestId);
                        var tokens2 = new Dictionary<string, string>
                                                 {
                                                    {Constants.PLACEHOLDER_FIRST_NAME, RequesterFirstName},
                                                    {Constants.PLACEHOLDER_NEWUSER,requestDto.MoneySenderEmailId},
                                                    {Constants.PLACEHOLDER_TRANSFER_AMOUNT,s32[0].ToString()},
                                                    {Constants.PLACEHLODER_CENTS,s32[1].ToString()},
                                                    {Constants.PLACEHOLDER_REJECT_LINK,otherlink},
                                                    {Constants.PLACEHOLDER_SENDER_FULL_NAME,RequesterFirstName + " " + RequesterLastName},
                                                    {Constants.MEMO,memo},
                                                    {Constants.PLACEHOLDER_PAY_LINK,paylink}
                                                 };
                        try
                        {
                            CommonHelper.SendEmail("requestReceivedToNewUser", fromAddress, requestDto.MoneySenderEmailId, null,
                                RequesterFirstName + " " + RequesterLastName + " requested " + "$" + s22.ToString() + " with Nooch",
                                null, tokens2, null, null, null);

                            Logger.Info("RequestMoneyToNonNoochUserUsingSynapse --> requestReceivedToNewUser email sent to [" + requestDto.MoneySenderEmailId + "] successfully.");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("RequestMoneyToNonNoochUserUsingSynapse --> requestReceivedToNewUser email NOT sent to [" + requestDto.MoneySenderEmailId +
                                                   "], [Exception: " + ex + "]");
                        }

                        #endregion Email Tenant

                        return "Request made successfully.";
                    }
                    else
                    {
                        return "Request failed.";
                    }
                }
            }
            else
            {
                requestId = null;
                return "User Already Exists";
            }
        }
    */


        //To send Trnsaction reminder email to tenants
        [HttpPost]
        [ActionName("SendTransReminderEmail")]
        public GenericInternalResponse SendTransReminderEmail(SendTransactionReminderEmailInputClass input)
        {

            GenericInternalResponse result = new GenericInternalResponse();
            result.success = false;
            result.msg = "Initial";

            Logger.Info("Landlords API -> RentTrans -> SendTransReminderEmail Initiated. " +
                        "MemberID: [" + input.User.LandlordId + "], " +
                        "TransactionId: [" + input.TransactionId + "], " +
                        "ReminderType: [" + input.ReminderType + "]");

            try
            {
                Guid transGuid = CommonHelper.ConvertToGuid(input.TransactionId);

                Guid LandlordGuid = CommonHelper.ConvertToGuid(input.User.LandlordId);
                Guid LandlordMemberIdGuid = CommonHelper.ConvertToGuid(input.User.MemberId);

                Landlord landlordObj = CommonHelper.GetLandlordByLandlordId(LandlordGuid);

                Member landlordMemberObject = CommonHelper.GetMemberByMemberId(LandlordMemberIdGuid);

                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(LandlordGuid, input.User.AccessToken);

                if (result.AuthTokenValidation.IsTokenOk)
                {
                    using (NOOCHEntities obj = new NOOCHEntities())
                    {
                        // Reading Landlord's details from Landlords Table in  DB
                        var transObj = (from c in obj.RentTransactions
                                        where c.RentTransactionId == transGuid &&
                                              c.TransactionStatus == "Pending"
                                        select c).FirstOrDefault();

                        if (transObj != null)
                        {
                            Tenant tenantObject = CommonHelper.GetTenantByTenantId(CommonHelper.ConvertToGuid(transObj.TenantId.ToString()));

                            Member tenantMemberObject = CommonHelper.GetMemberByMemberId(CommonHelper.ConvertToGuid(tenantObject.MemberId.ToString()));

                            #region Preparing and sending reminder email

                            if (input.ReminderType == "RequestMoneyReminderToNewUser" ||
                                input.ReminderType == "RequestMoneyReminderToExistingUser")
                            {
                                #region Setup Common Variables

                                string fromAddress = CommonHelper.GetValueFromConfig("transfersMail");

                                string senderFirstName =
                                    CommonHelper.UppercaseFirst(
                                        CommonHelper.GetDecryptedData(landlordMemberObject.FirstName));
                                string senderLastName =
                                    CommonHelper.UppercaseFirst(
                                        CommonHelper.GetDecryptedData(landlordMemberObject.LastName));


                                // TBD with Cliff about new landing page for handling play rent transactions of modify existing because rent transactions are now stored in separate table and existing landling page check given transaction
                                // id in Transactions table..... that code will fail in this case... page will never get any transaction with this transaction id.
                                string payLink = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"),
                                    "Nooch/PayRequest?TransactionId=" + transObj.RentTransactionId);
                                //"trans/payRequest.aspx?TransactionId=" + transObj.RentTransactionId);
                                // or link to some new landing page here
                                //string payLink = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"),"trans/payRent.aspx?TransactionId=" + transObj.RentTransactionId);

                                string s22 = Convert.ToDecimal(transObj.Amount.ToString()).ToString("n2");
                                string[] s32 = s22.Split('.');

                                string memo = "";
                                if (!string.IsNullOrEmpty(transObj.Memo))
                                {
                                    if (transObj.Memo.Length > 3)
                                    {
                                        string firstThreeChars = transObj.Memo.Substring(0, 3).ToLower();
                                        bool startWithFor = firstThreeChars.Equals("for");

                                        if (startWithFor)
                                        {
                                            memo = transObj.Memo.ToString();
                                        }
                                        else
                                        {
                                            memo = "For " + transObj.Memo.ToString();
                                        }
                                    }
                                    else
                                    {
                                        memo = "For " + transObj.Memo.ToString();
                                    }
                                }

                                // this solved landling page problem I thought above
                                bool isForRentScene = true;
                                var logoToDisplay = "noochlogo";
                                senderFirstName = "Rent Scene";
                                senderLastName = "";
                                logoToDisplay = "rentscenelogo";
                                payLink = payLink + "&rs=1";

                                #endregion Setup Common Variables


                                #region RequestMoneyReminderToExistingUser

                                string rejectLink = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"),
                                    "Nooch/RejectMoney?TransactionId=" + transObj.RentTransactionId +
                                    "&UserType=mx5bTcAYyiOf9I5Py9TiLw==&LinkSource=75U7bZRpVVxLNbQuoMQEGQ==&TransType=T3EMY1WWZ9IscHIj3dbcNw==");

                                //trans/rejectMoney.aspx?TransactionId=" + transObj.RentTransactionId +
                                //    "&UserType=mx5bTcAYyiOf9I5Py9TiLw==&LinkSource=75U7bZRpVVxLNbQuoMQEGQ==&TransType=T3EMY1WWZ9IscHIj3dbcNw==");
                                string paylink = "nooch://";

                                string tenantFirstName =
                                    CommonHelper.UppercaseFirst(
                                        CommonHelper.GetDecryptedData(tenantMemberObject.FirstName));
                                string tenantLastName =
                                    CommonHelper.UppercaseFirst(
                                        CommonHelper.GetDecryptedData(tenantMemberObject.LastName));

                                #region Reminder EMAIL

                                string toAddress = CommonHelper.GetDecryptedData(tenantMemberObject.UserName);

                                var tokens2 = new Dictionary<string, string>
                                {
                                    {"$Logo$", logoToDisplay},
                                    {Constants.PLACEHOLDER_FIRST_NAME, senderFirstName},
                                    {
                                        Constants.PLACEHOLDER_NEWUSER,
                                        CommonHelper.UppercaseFirst(
                                            CommonHelper.GetDecryptedData(tenantMemberObject.FirstName))
                                    },
                                    {Constants.PLACEHOLDER_TRANSFER_AMOUNT, s32[0].ToString()},
                                    {Constants.PLACEHLODER_CENTS, s32[1].ToString()},
                                    {Constants.PLACEHOLDER_REJECT_LINK, rejectLink},
                                    {
                                        Constants.PLACEHOLDER_TRANSACTION_DATE,
                                        Convert.ToDateTime(transObj.TransCreatedOn).ToString("MMM dd yyyy")
                                    },
                                    {Constants.MEMO, memo},
                                    {Constants.PLACEHOLDER_PAY_LINK, paylink}
                                };
                                try
                                {
                                    CommonHelper.SendEmail("requestReminderToExistingUser", fromAddress,
                                        senderFirstName + " " + senderLastName,
                                        toAddress,
                                        senderFirstName + " " + senderLastName + " requested " + "$" + s22.ToString() +
                                        " with Nooch - Reminder",
                                        tokens2, null, null);

                                    Logger.Info(
                                        "Landlords API -> RentTrans -> SendTransReminderEmail - Reminder email (Request, existing user) sent to [" +
                                        toAddress + "] successfully.");
                                }
                                catch (Exception)
                                {
                                    Logger.Error(
                                        "Landlords API -> RentTrans -> SendTransReminderEmail - Reminder email (Request, existing user) NOT sent to [" +
                                        toAddress + "]. Problem occured in sending mail.");
                                }

                                #endregion Reminder EMAIL

                                #region Reminder PUSH NOTIFICATION

                                if (!String.IsNullOrEmpty(tenantMemberObject.DeviceToken) &&
                                    tenantMemberObject.DeviceToken != "(null)")
                                {
                                    try
                                    {
                                        string firstName = (!String.IsNullOrEmpty(tenantMemberObject.FirstName))
                                            ? " " +
                                              CommonHelper.UppercaseFirst(
                                                  CommonHelper.GetDecryptedData(tenantMemberObject.FirstName))
                                            : "";

                                        string msg = "Hey" + firstName + "! Just a reminder that " + senderFirstName +
                                                     " " + senderLastName +
                                                     " sent you a Nooch request for $" +
                                                     Convert.ToDecimal(transObj.Amount.ToString()).ToString("n2") +
                                                     ". Might want to pay up!";

                                        ApplePushNotification.SendNotificationMessage(msg, 1, null,
                                            tenantMemberObject.DeviceToken,
                                            CommonHelper.GetValueFromConfig("AppKey"),
                                            CommonHelper.GetValueFromConfig("MasterSecret"));

                                        Logger.Info("Landlords API -> RentTrans -> SendTransReminderEmail - (B/t 2 Existing Nooch Users) - Push notification sent successfully - [Username: " +
                                                    toAddress + "], [Device Token: " + tenantMemberObject.DeviceToken + "]");
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Error(
                                            "Landlords API -> RentTrans -> SendTransReminderEmail - (B/t 2 Existing Nooch Users) - Push notification NOT sent - [Username: " +
                                            toAddress + "], [Device Token: " + tenantMemberObject.DeviceToken +
                                            "], [Exception: " + ex.Message + "]");
                                    }
                                }

                                #endregion  Reminder PUSH NOTIFICATION

                                result.msg = "Reminder mail sent successfully.";
                                result.success = true;

                                #endregion RequestMoneyReminderToExistingUser
                            }

                            #endregion Preparing and sending reminder email
                        }
                        else
                        {
                            result.msg = "Invalid Transaction Id or Transaction no more pending.";
                        }
                    }
                }
                else
                {
                    result.msg = "Invalid access token.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Landlords API -> RentTrans -> Error details -" + ex.ToString());
                result.msg = "Invalid access token.";
            }

            return result;
        }


        /// <summary>
        /// To request money to existing Tenant
        /// </summary>
        /// <param name="input"></param>
        /// <returns>GenericInternalResponse()</returns>
        [HttpPost]
        [ActionName("RequestRentToExistingTenant")]
        public GenericInternalResponse RequestRentToExistingTenant(SendRentRequestToTenantInputClass input)
        {
            GenericInternalResponse result = new GenericInternalResponse();
            result.success = false;
            result.msg = "Initial";

            Logger.Info("Landlords API -> RentTrans -> RequestRentToExistingTenant Initiated. LandlordsID: [" +
                        input.User.LandlordId + "], " +
                        "Tenants Member ID: [" + input.TenetsMemberId + "], " +
                        "Property Id: [" + input.PropertyId + "]");

            try
            {
                Guid LandlordGuid = CommonHelper.ConvertToGuid(input.User.LandlordId);
                Guid LandlordMemberIdGuid = CommonHelper.ConvertToGuid(input.User.MemberId);
                Guid TenantsMemberGUID = CommonHelper.ConvertToGuid(input.TenetsMemberId);

                Landlord landlordObj = CommonHelper.GetLandlordByLandlordId(LandlordGuid);

                Member landlordMemberObject = CommonHelper.GetMemberByMemberId(LandlordMemberIdGuid);

                Member tenantsMemberObject = CommonHelper.GetMemberByMemberId(TenantsMemberGUID);

                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(LandlordGuid, input.User.AccessToken);


                if (result.AuthTokenValidation.IsTokenOk)
                {
                    using (NOOCHEntities obj = new NOOCHEntities())
                    {
                        Logger.Info("RentTrans -> RequestRentToExistingTenant - Checkpoing #1 - Access Token is OK!");

                        // preparing new Transactions table object

                        #region New Entry in Transactions Table
                        Transaction rentTrans = new Transaction();
                        rentTrans.TransactionId = Guid.NewGuid();
                        rentTrans.SenderId = tenantsMemberObject.MemberId;
                        rentTrans.RecipientId = landlordMemberObject.MemberId;
                        rentTrans.Amount = Convert.ToDecimal(input.Amount);
                        rentTrans.TransactionDate = DateTime.Now;
                        rentTrans.Picture = null;
                        rentTrans.Memo = input.Memo;
                        rentTrans.DisputeStatus = null;
                        rentTrans.TransactionStatus = "Pending";
                        rentTrans.TransactionType = CommonHelper.GetEncryptedData("Rent");
                        rentTrans.DeviceId = null;
                        rentTrans.TransactionTrackingId = CommonHelper.GetRandomTransactionTrackingId();
                        rentTrans.TransactionFee = 0;
                        rentTrans.IsPhoneInvitation = false;
                        rentTrans.InvitationSentTo = null;
                        rentTrans.GeoLocation = new GeoLocation
                        {
                            LocationId = Guid.NewGuid(),
                            Latitude = null,
                            Longitude = null,
                            Altitude = null,
                            AddressLine1 = null,
                            AddressLine2 = null,
                            City = null,
                            State = null,
                            Country = null,
                            ZipCode = null,
                            DateCreated = DateTime.Now
                        };

                        #endregion

                        obj.Transactions.Add(rentTrans);
                        int saveResult = obj.SaveChanges();

                        if (saveResult > 0)
                        {
                            // success
                            #region Send Notifications

                            #region Set Up Variables

                            var fromAddress = CommonHelper.GetValueFromConfig("transfersMail");

                            string RequesterFirstName = CommonHelper.UppercaseFirst((CommonHelper.GetDecryptedData(landlordObj.FirstName)));
                            string RequesterLastName = CommonHelper.UppercaseFirst((CommonHelper.GetDecryptedData(landlordObj.LastName)).ToString());
                            string RequestReceiverFirstName = CommonHelper.UppercaseFirst((CommonHelper.GetDecryptedData(tenantsMemberObject.FirstName)).ToString());
                            string RequestReceiverLastName = CommonHelper.UppercaseFirst((CommonHelper.GetDecryptedData(tenantsMemberObject.LastName)).ToString());

                            string requesterPic = "https://www.noochme.com/noochweb/Assets/Images/userpic-default.png";
                            if (!String.IsNullOrEmpty(tenantsMemberObject.Photo) && tenantsMemberObject.Photo.Length > 20)
                            {
                                requesterPic = tenantsMemberObject.Photo.ToString();
                            }

                            //string cancelLink = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"), "trans/CancelRequest.aspx?TransactionId=" + rentTrans.TransactionId +
                            //                                                                                "&MemberId=" + rentTrans.RecipientId + // or Landlord's MemberId
                            //                                                                                "&UserType=mx5bTcAYyiOf9I5Py9TiLw==");  // mx5bTcAYyiOf9I5Py9TiLw== --->> Existing
                            string cancelLink = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"), "Nooch/CancelRequest?TransactionId=" + rentTrans.TransactionId +
                                                                                           "&MemberId=" + rentTrans.RecipientId + // or Landlord's MemberId
                                                                                           "&UserType=mx5bTcAYyiOf9I5Py9TiLw==");  // mx5bTcAYyiOf9I5Py9TiLw== --->> Existing

                            string wholeAmount = rentTrans.Amount.ToString("n2");
                            string[] amountArray = wholeAmount.Split('.');

                            string memo = "";
                            if (!string.IsNullOrEmpty(input.Memo))
                            {
                                if (input.Memo.Length > 3)
                                {
                                    string firstThreeChars = input.Memo.Substring(0, 3).ToLower();
                                    bool startWithFor = firstThreeChars.Equals("for");

                                    if (startWithFor)
                                    {
                                        memo = input.Memo.ToString();
                                    }
                                    else
                                    {
                                        memo = "for " + input.Memo.ToString();
                                    }
                                }
                                else
                                {
                                    memo = "for " + input.Memo.ToString();
                                }
                            }

                            bool isForRentScene = Convert.ToBoolean(CommonHelper.GetValueFromConfig("isForRentScene"));
                            fromAddress = "payments@rentscene.com";
                            var templateToUse_Recip = isForRentScene ? "requestReceivedToExistingNonRegUser_RentScene" : "requestReceivedToExistingNonRegUser";

                            var templateToUse_Sender = isForRentScene ? "requestSent_RentScene" : "requestSent";



                            #endregion Set Up Variables

                            bool isTesting = Convert.ToBoolean(CommonHelper.GetValueFromConfig("IsRunningOnSandBox"));

                            // Send email to REQUESTER / Landlord (person who sent this request)
                            #region Email To Requester

                            var tokens = new Dictionary<string, string>
                            {
					            {Constants.PLACEHOLDER_FIRST_NAME, RequesterFirstName},
					            {Constants.PLACEHOLDER_NEWUSER, RequestReceiverFirstName + " " + RequestReceiverLastName},
					            {Constants.PLACEHOLDER_TRANSFER_AMOUNT, amountArray[0].ToString()},
					            {Constants.PLACEHLODER_CENTS, amountArray[1].ToString()},
					            {Constants.PLACEHOLDER_OTHER_LINK, cancelLink},
					            {Constants.MEMO, memo}
				            };

                            var toAddress = isTesting ? "testing_request-sender@nooch.com"
                                                      : CommonHelper.GetDecryptedData(landlordMemberObject.UserName);

                            try
                            {
                                CommonHelper.SendEmail(templateToUse_Sender, fromAddress, CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(landlordMemberObject.FirstName)) + " " + CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(landlordMemberObject.LastName)), toAddress,
                                                            "Your payment request to " + RequestReceiverFirstName + " " + RequestReceiverLastName + " is pending"
                                                            , tokens, null, null);

                                Logger.Info("RentTrans -> RequestRentToExistingTenant  -> [" + templateToUse_Sender + "] email sent to [" + toAddress + "] successfully.");
                            }
                            catch (Exception ex)
                            {
                                Logger.Error("RentTrans -> RequestRentToExistingTenant -> [" + templateToUse_Sender + "] email NOT sent to [" + toAddress +
                                                       "], [Exception: " + ex + "]");
                            }

                            #endregion Email To Requester

                            #region Email To Request Recipient

                            // Send email to REQUEST RECIPIENT (person who will pay/reject this request)
                            // Include 'UserType', 'LinkSource', and 'TransType' as encrypted along with request
                            // In this case UserType would = 'Existing'  ->  mx5bTcAYyiOf9I5Py9TiLw==
                            //              TransType would = 'Rent'
                            //              LinkSource would = 'Email'

                            // Check if both user's are actually "Active" and not NonRegistered...
                            string userType = "mx5bTcAYyiOf9I5Py9TiLw=="; // "Existing"

                            string rejectLink = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"),
                                                              "Nooch/RejectMoney?TransactionId=" + rentTrans.TransactionId +
                                                              "&UserType=" + userType +
                                                              "&LinkSource=75U7bZRpVVxLNbQuoMQEGQ==" +
                                                              "&TransType=EnOIzpmFFTEaAP16hm9Wsw==");

                            string paylink = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"),
                                                          "Nooch/PayRequest?TransactionId=" + rentTrans.TransactionId +
                                                          "&UserType=" + userType + "&TransType=" + CommonHelper.GetEncryptedData("RENT"));

                            var tokens2 = new Dictionary<string, string>
                            {
                                {Constants.PLACEHOLDER_FIRST_NAME, RequestReceiverFirstName},
                                {"$UserPicture$", requesterPic},
                                {Constants.PLACEHOLDER_SENDER_FULL_NAME, RequesterFirstName + " " + RequesterLastName},
                                {Constants.PLACEHOLDER_TRANSFER_AMOUNT, amountArray[0].ToString()},
                                {Constants.PLACEHLODER_CENTS, amountArray[1].ToString()},
                                {Constants.MEMO, memo},
                                {Constants.PLACEHOLDER_REJECT_LINK, rejectLink},
                                {Constants.PLACEHOLDER_PAY_LINK, paylink},
                                {Constants.PLACEHOLDER_FRIEND_FIRST_NAME, RequesterFirstName}
                            };

                            toAddress = isTesting ? "testing_request-recip@nooch.com"
                                                  : CommonHelper.GetDecryptedData(tenantsMemberObject.UserName);

                            try
                            {
                                // Still to do: ADD CURRENT MONTH IN BEGINNING OF THE SUBJECT, "December Rent Request from Landlord"
                                string subject = isForRentScene
                                                 ? "$" + wholeAmount + " Payment Request from Rent Scene"
                                                 : "Payment Request from " + RequesterFirstName + " " + RequesterLastName + " - " + "$" + wholeAmount;

                                CommonHelper.SendEmail(templateToUse_Recip, fromAddress, CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(tenantsMemberObject.FirstName)) + " " + CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(tenantsMemberObject.LastName)), toAddress,
                                                            subject, tokens2, null, null);

                                Logger.Info("RentTrans -> RequestRentToExistingTenant  - [" + templateToUse_Recip + "] email sent to [" + toAddress + "] successfully.");
                            }
                            catch (Exception ex)
                            {
                                Logger.Error("RentTrans -> RequestRentToExistingTenant - [" + templateToUse_Recip + "] email NOT sent to [" + toAddress +
                                                       "], [Exception: " + ex + "]");
                            }

                            #endregion Email To Request Recipient

                            #endregion Send Notifications

                            result.success = true;
                            result.msg = "Request made successfully.";
                        }
                        else
                        {
                            // failure while saving
                            result.msg = "Error while saving Transaction. Retyr!";
                        }
                    }
                }
                else
                {
                    result.msg = "Invalid access token.";
                }
            }
            catch (Exception ex)
            {
                result.msg = "Invalid access token.";
                Logger.Error("Landlords API -> RentTrans -> Error details -" +
                             ex.ToString());
            }

            return result;
        }
    }
}