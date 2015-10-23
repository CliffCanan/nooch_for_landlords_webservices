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

namespace LanLordlAPIs.Controllers
{
    public class RentTransController : ApiController
    {
        [HttpPost]
        [ActionName("AddNewProperty")]
        public CreatePropertyResultOutput AddNewProperty(AddNewPropertyClass Property)
        {
            Logger.Info("Landlords API -> Properties -> AddNewProperty - Requested by [" + Property.User.LandlordId + "]");

            CreatePropertyResultOutput result = new CreatePropertyResultOutput();
            result.IsSuccess = false;

            try
            {
                Guid landlordguidId = new Guid(Property.User.LandlordId);

                result.AuthTokenValidation = CommonHelper.AuthTokenValidation(landlordguidId, Property.User.AccessToken);

                if (result.AuthTokenValidation.IsTokenOk)
                {
                    // all set... Data is ready to be saved in DB
                    string propertyImagePath = CommonHelper.GetValueFromConfig("PhotoUrl") + "propertyDefault.png";

                    if (Property.IsPropertyImageAdded)
                    {
                        // Get image URL from Base64 string
                        string fileName = Property.PropertyName.Trim().Replace("-", "_").Replace(" ", "_").Replace("'", "") + ".png";
                        propertyImagePath = CommonHelper.SaveBase64AsImage(landlordguidId.ToString().Replace("-", "_") +
                                            "_property_" + fileName, Property.PropertyImage);
                    }

                    using (NOOCHEntities obj = new NOOCHEntities())
                    {
                        Property prop = new Property
                        {
                            PropertyId = Guid.NewGuid(),
                            PropType = Property.IsMultipleUnitsAdded ? "Multi Unit" : "Single Unit",
                            PropStatus = "Not Published",
                            PropName = Property.PropertyName.Trim(),
                            AddressLineOne = Property.Address.Trim(),
                            City = Property.City.Trim(),
                            Zip = Property.Zip.Trim(),
                            DateAdded = DateTime.Now,
                            LandlordId = landlordguidId,
                            MemberId = new Guid(Property.User.MemberId),
                            PropertyImage = propertyImagePath,
                            IsSingleUnit = !Property.IsMultipleUnitsAdded,
                            IsDeleted = false,
                            DefaultDueDate = "1st of Month"
                        };

                        obj.Properties.Add(prop);
                        obj.SaveChanges();

                        // Set type to single unit if only one item passed in property units
                        if (Property.Unit.Length < 2)
                        {
                            prop.IsSingleUnit = true;
                            prop.PropType = "Single Unit";
                            obj.SaveChanges();
                        }

                        // Saving Units (if any)
                        foreach (AddNewUnitClass unitItem in Property.Unit)
                        {
                            PropertyUnit pu = new PropertyUnit();
                            pu.UnitId = Guid.NewGuid();
                            pu.PropertyId = prop.PropertyId;
                            pu.LandlordId = landlordguidId;
                            pu.DateAdded = DateTime.Now;
                            pu.IsDeleted = false;
                            pu.Status = "Not Published";

                            if (prop.IsSingleUnit != true)
                            {
                                pu.UnitNumber = unitItem.UnitNum;
                            }
                            else
                            {
                                pu.UnitNumber = "1";
                            }
                            pu.UnitRent = unitItem.Rent;
                            pu.IsHidden = true;
                            pu.IsOccupied = false;
                            pu.MemberId = new Guid(CommonHelper.GetMemberIdOfLandlord(landlordguidId));

                            obj.PropertyUnits.Add(pu);
                            obj.SaveChanges();
                        }
                        result.PropertyIdGenerated = prop.PropertyId.ToString();
                        result.IsSuccess = true;
                        result.ErrorMessage = "OK";
                    }
                }
                else
                {
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Landlords API -> Properties -> AddNewProperty FAILED - [LandlordID: " + Property.User.LandlordId + "], [Exception: [ " + ex + " ]");
                result.ErrorMessage = "Error while creating property. Retry later!";
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
    }
}