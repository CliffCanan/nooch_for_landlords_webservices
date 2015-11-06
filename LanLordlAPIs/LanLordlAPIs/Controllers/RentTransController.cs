using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Web.UI.WebControls;
using LanLordlAPIs.Classes.Utility;
using LanLordlAPIs.Models.db_Model;
using LanLordlAPIs.Models.Input_Models;
using LanLordlAPIs.Models.Output_Models;
using Newtonsoft.Json;

namespace LanLordlAPIs.Controllers
{
    public class RentTransController : ApiController
    {
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

                if (CommonHelper.isOverTransactionLimit(transactionAmount, input.TransRequest.TenantId, input.User.LandlordId))
                {
                    result.ErrorMessage = "To keep Nooch safe, the maximum amount you can request is $" + Convert.ToDecimal(CommonHelper.GetValueFromConfig("MaximumTransferLimitPerTransaction")).ToString("F2");
                    return result;
                }

                // Get requester and request recepient Members table info
                var requester = CommonHelper.GetMemberByMemberId(landlordsMemID);
                var requesterLandlordObj = CommonHelper.GetLandlordByLandlordId(Landlord_GUID); // Only need this for the Photo for the email template... Members table doesn't have it.
                var requestRecipient = CommonHelper.GetMemberByMemberId(Tenant_GUID);


                if (requester == null)
                {
                    Logger.Error("Landlords API -> RentTrans -> ChargeTenant FAILED - Requester Member Not Found - [MemberID: " + Landlord_GUID + "]");
                    result.ErrorMessage = "Requester Member Not Found";

                    return result;
                }
                if (requestRecipient == null)
                {
                    Logger.Error("Landlords API -> RentTrans -> ChargeTenant FAILED - requestRecipient (who would pay the request) Member Not Found - [MemberID: " + Landlord_GUID + "]");
                    result.ErrorMessage = "Request Recipient Member Not Found";

                    return result;
                }

                #region Get Request Sender's Synapse Account Details


                var requestorSynInfo = CommonHelper.GetSynapseBankAndUserDetailsforGivenMemberId(landlordsMemID.ToString());

                if (requestorSynInfo.wereBankDetailsFound != true)
                {
                    Logger.Error("Landlords API -> RentTrans -> ChargeTenant FAILED -> Request ABORTED: Requester's Synapse bank account NOT FOUND - Request Creator MemberId is: [" + requester.MemberId + "]");
                    result.ErrorMessage = "Requester does not have any bank added";

                    return result;
                }

                // Check Requestor's Synapse Bank Account status
                if (requestorSynInfo.BankDetails != null &&
                    requestorSynInfo.BankDetails.Status != "Verified" &&
                    requester.IsVerifiedWithSynapse != true)
                {
                    Logger.Error("Landlords API -> RentTrans -> ChargeTenant FAILED -> Request ABORTED: Requester's Synapse bank account exists but is not Verified and " +
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
                    //tr.RecipientId = requester.MemberId;
                    //tr.SenderId = requestRecipient.MemberId;
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
                        Logger.Error("Landlords API -> RentTrans -> ChargeTenant FAILED - Unable to save Transaction in DB - [Requester MemberID:" + input.User.LandlordId + "], [Exception: [ " + ex.InnerException + " ]");
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
                string RequestReceiverFullName = (RequestReceiverFirstName.Length > 2 && RequestReceiverLastName.Length > 2)
                                                 ? RequestReceiverFirstName + " " + RequestReceiverLastName
                                                 : CommonHelper.GetDecryptedData(requestRecipient.UserName);

                Logger.Info("RequesterFirstName: [" + RequesterFirstName + "], RequesterLastName: [" + RequesterLastName + "], RequestReceiverFirstName: [" + RequestReceiverFirstName +
                            "], RequestReceiverLastName: [" + RequestReceiverLastName + "], RequestReceiverFullName: [" + RequestReceiverFullName + "]");

                string requesterPic = "https://www.noochme.com/noochweb/Assets/Images/userpic-default.png";
                if (!String.IsNullOrEmpty(requesterLandlordObj.UserPic) && requesterLandlordObj.UserPic.Length > 20)
                {
                    requesterPic = requesterLandlordObj.UserPic;
                }
                else if (!String.IsNullOrEmpty(requester.Photo) && requester.Photo.Length > 20)
                {
                    requesterPic = requester.Photo;
                }

                string cancelLink = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"), "trans/CancelRequest.aspx?TransactionId=" + requestId + "&MemberId=" + input.User.LandlordId + "&UserType=6KX3VJv3YvoyK+cemdsvMA==");

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

                    Logger.Info("memo: [" + memo + "], wholeAmount: [" + wholeAmount + "], cancelLink: [" + cancelLink +
                            "], toAddress: [" + RequesterEmail + "], fromAddress: [" + fromAddress + "]");

                    CommonHelper.SendEmail("requestSent", fromAddress, null, RequesterEmail,
                        "Your payment request to " + RequestReceiverFullName +
                        " is pending", tokens, null, null);

                    Logger.Info("Landlords API -> RentTrans -> ChargeTenant -> RequestSent email sent to [" + RequesterEmail + "] successfully.");
                }
                catch (Exception ex)
                {
                    Logger.Error("Landlords API -> RentTrans -> ChargeTenant -> RequestSent email NOT sent to [" + RequesterEmail +
                                           "], [Exception: " + ex + "]");
                }

                #endregion Email To Requester


                #region Email To Request Recipient

                // Send email to REQUEST RECIPIENT (person who will pay/reject this request)
                // Include 'UserType', 'LinkSource', and 'TransType' as encrypted along with request
                // In this case UserType would = 'Nonregistered'  ->  6KX3VJv3YvoyK+cemdsvMA==
                //              TransType would = 'Request'
                //              LinkSource would = 'Email'
                string rejectLink = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"), "trans/rejectMoney.aspx?TransactionId=" + requestId + "&UserType=6KX3VJv3YvoyK+cemdsvMA==&LinkSource=75U7bZRpVVxLNbQuoMQEGQ==&TransType=T3EMY1WWZ9IscHIj3dbcNw==");
                string paylink = String.Concat(CommonHelper.GetValueFromConfig("ApplicationURL"), "trans/payRequest.aspx?TransactionId=" + requestId + "&UserType=6KX3VJv3YvoyK+cemdsvMA==");

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
                    CommonHelper.SendEmail("requestReceivedToExistingNonRegUser", fromAddress, RequesterFirstName + " " + RequesterLastName, toAddress,
                    RequesterFirstName + " " + RequesterLastName + " requested " + "$" + wholeAmount.ToString() + " with Nooch", tokens2, null, null);

                    Logger.Info("RentTrans -> ChargeTenant ->  requestReceivedToNewUser email sent to [" + toAddress + "] successfully");

                }
                catch (Exception ex)
                {
                    Logger.Error("RentTrans -> ChargeTenant -> requestReceivedToNewUser email NOT sent to  [" + toAddress +
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
                Logger.Error("Landlords API -> RentTrans -> ChargeTenant FAILED - [LandlordID: " + input.User.LandlordId + "], [Exception: [ " + ex + " ]");
                result.ErrorMessage = "Error while chargin tenant. Retry later!";
            }
            return result;
        }


        [HttpPost]
        [ActionName("GetLandlordsPaymentHistory")]
        public LandlordsPaymentHistoryClass GetLandlordsPaymentHistory(basicLandlordPayload landlord)
        {
            Logger.Info("Rent Trans Cntrlr -> GetLandlordsPaymentHistory Initiated - [LandlordID: " + landlord.LandlordId + "], [MemberID: " + landlord.MemberId + "]");

            LandlordsPaymentHistoryClass res = new LandlordsPaymentHistoryClass();
            res.IsSuccess = false;

            if (!String.IsNullOrEmpty(landlord.LandlordId))
            {
                try
                {
                    Guid landlordGuidId = new Guid(landlord.LandlordId);

                    res.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordGuidId, landlord.AccessToken);

                    if (res.AuthTokenValidation.IsTokenOk)
                    {
                        using (NOOCHEntities obj = new NOOCHEntities())
                        {
                            // Get Landlord's details from Landlords Table in DB
                            var landlordObj = (from c in obj.Landlords
                                               where c.LandlordId == landlordGuidId && c.IsDeleted == false
                                               select c).FirstOrDefault();

                            if (landlordObj != null)
                            {
                                List<PaymentHistoryClass> TransactionsListToRet = new List<PaymentHistoryClass>();

                                // Get all PROPERTIES for given Landlord
                                var allProps = (from prop in obj.Properties
                                                where prop.LandlordId == landlordGuidId
                                                select prop).ToList();

                                foreach (Property p in allProps)
                                {
                                    // Get all property UNITS in each property
                                    var allUnitsInProp = (from c in obj.PropertyUnits
                                                          where c.PropertyId == p.PropertyId &&
                                                                c.IsDeleted == false &&
                                                                c.IsOccupied == true
                                                          select c).ToList();

                                    //Logger.Info("1. - THERE ARE [" + allUnitsInProp.Count + "] UNITS in this Property: [" + p.PropName + "]");

                                    // Iterating through each occupied unit
                                    foreach (PropertyUnit pu in allUnitsInProp)
                                    {
                                        var allOccupiedUnits = (from c in obj.UnitsOccupiedByTenants
                                                                where c.UnitId == pu.UnitId && c.IsDeleted == false
                                                                select c).ToList();

                                        //Logger.Info("2. - THERE ARE [" + allOccupiedUnits.Count + "] UOBT for Unit Num: [" + pu.UnitNumber + "]");

                                        #region Loop Through UnitsOccupiedByTenants

                                        // Iterating through each occupied unit and checking if any rent for this unit
                                        foreach (UnitsOccupiedByTenant uobt in allOccupiedUnits)
                                        {
                                            try
                                            {
                                                // Get transctions from Transactions table where tenant was sender and lanlord was receiver and transaction type "Rent"

                                                /*var TenantDetails = (from c in obj.Tenants
                                                                     where c.TenantId == uobt.TenantId
                                                                     select c).FirstOrDefault();*/

                                                //Logger.Info("3. - UOBT.TenantId is: [" + uobt.TenantId + "]");

                                                Guid tenantMemberGuid = new Guid(CommonHelper.GetTenantsMemberIdFromTenantId(uobt.TenantId.ToString()));

                                                var TenantMemberDetails = (from c in obj.Members
                                                                           where c.MemberId == tenantMemberGuid &&
                                                                                 c.IsDeleted == false
                                                                           select c).FirstOrDefault();


                                                var allTrans = (from c in obj.Transactions
                                                                where c.SenderId == TenantMemberDetails.MemberId &&
                                                                      c.RecipientId == landlordObj.MemberId
                                                                select c).ToList();

                                                Logger.Info("3. - THERE ARE [" + allTrans.Count + "] TRANSACTIONS where SenderId = [" + TenantMemberDetails.MemberId + "], RecipientId = [" + landlordObj.MemberId + "]");

                                                foreach (Transaction t in allTrans)
                                                {
                                                    PaymentHistoryClass phc = new PaymentHistoryClass();
                                                    // Data from Transactions Table
                                                    phc.TransactionDate = Convert.ToDateTime(t.TransactionDate).ToShortDateString();
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
                                                    phc.TenantName = !String.IsNullOrEmpty(TenantMemberDetails.FirstName)
                                                                 ? CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(TenantMemberDetails.FirstName)) + " " +
                                                                   CommonHelper.UppercaseFirst(CommonHelper.GetDecryptedData(TenantMemberDetails.LastName))
                                                                 : "";
                                                    phc.TenantEmail = !String.IsNullOrEmpty(TenantMemberDetails.UserName) ? CommonHelper.GetDecryptedData(TenantMemberDetails.UserName) : null;

                                                    TransactionsListToRet.Add(phc);
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                Logger.Error("Rent Trans Cntrlr -> GetLandlordsPaymentHistory EXCEPTION (Inner) - [LandlordID: " + landlord.LandlordId + "], [Exception: " + ex.Message + " ]");
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
                                Logger.Error("Rent Trans Cntrlr -> GetLandlordsPaymentHistory LANDLORD NOT FOUND - [LandlordID: " + landlord.LandlordId + "]");
                                res.ErrorMessage = "Invalid Landlord ID.";
                            }
                        }
                    }
                    else
                    {
                        Logger.Error("Rent Trans Cntrlr -> GetLandlordsPaymentHistory AUTH TOKEN FAILURE - [LandlordID: " + landlord.LandlordId + "]");
                        res.ErrorMessage = "Auth token failure";
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Rent Trans Cntrlr -> GetLandlordsPaymentHistory EXCEPTION (Outer) - [LandlordID: " + landlord.LandlordId + "], [Exception: " + ex.InnerException + " ]");
                    res.ErrorMessage = "Server exception.";
                }
            }

            return res;
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
    }
}