using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web;
using LanLordlAPIs.Classes.Crypto;
using LanLordlAPIs.Models.db_Model;
using System.Drawing;
using System.Web.Hosting;

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
            }
            return string.Empty;
        }


        public static bool saveLandlordIp(Guid LandlorId, string IP)
        {
            try
            {
                using (NOOCHEntities obj = new NOOCHEntities())
                {
                    var lanlorddetails =
                        (from c in obj.Landlords

                         where c.LandlordId == LandlorId
                         select
                             c
                     ).FirstOrDefault();

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

                        //updating ip in db

                        lanlorddetails.IpAddresses = IPsListPrepared;
                        obj.SaveChanges();
                        return true;

                    }
                    return false;
                }


            }
            catch (Exception ex)
            {
                Logger.Error("Landlords API -> CommonHelper -> saveLandlordIp. Error while updating IP address - [ " + IP + " ] for Landlor Id [ " + LandlorId + " ]. Error details [" + ex + " ]");
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
                var lanlorddetails =
                    (from c in obj.Landlords

                        where c.LandlordId == LandlorId && c.WebAccessToken==accesstoken && c.IsDeleted==false
                        select
                            c
                        ).FirstOrDefault();

                if (lanlorddetails!=null)
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

                    if (t.TotalMinutes > 10)
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
            }
            return sourceNum;
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
                 filnameMade = fileNametobeused ;

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
    }




    //All utility classes goes here---------------------------------------------------------XXXXXXXXXXXXXXXXXXXXXXXXX--------------------------------------------
  
    public class AccessTokenValidationOutput
    {
        public bool IsTokenOk { get; set; }
        public bool IsTokenUpdated { get; set; }
        public string AccessToken { get; set; }
        public string ErrorMessage { get; set; }
    }
    //All utility classes up there---------------------------------------------------------XXXXXXXXXXXXXXXXXXXXXXXXX--------------------------------------------
}