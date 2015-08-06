using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using log4net.Repository.Hierarchy;
using LanLordlAPIs.Classes.Utility;
namespace LanLordlAPIs.Classes.Crypto
{
    public enum AESHashAlgorithm
    {
        SHA1,
        MD5
    }

    public enum AESKeySize
    {
        K128 = 128,
        K192 = 192,
        K256 = 256
    }
    public class AES : CryptographyBase
    {
        #region CryptographyBase Members

        public override string Encrypt(string plainString, string cryptographyKey)
        {
            return Encrypt(plainString, "Pa55Frase", "S@ltV@lyu", AESHashAlgorithm.MD5, 3, "1n1tVect0r!@#$<.", AESKeySize.K256);
        }

        public override string Decrypt(string encryptedString, string cryptographyKey)
        {
            return Decrypt(encryptedString, "Pa55Frase", "S@ltV@lyu", AESHashAlgorithm.MD5, 3, "1n1tVect0r!@#$<.", AESKeySize.K256);
        }

        #endregion

        private static string Encrypt(string plainText, string password, string salt, AESHashAlgorithm hashAlgorithm, int passwordIterations, string initialVector, AESKeySize keySize)
        {
            try
            {
                byte[] initialVectorBytes = Encoding.ASCII.GetBytes(initialVector);
                byte[] saltValueBytes = Encoding.ASCII.GetBytes(salt);
                byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);

                var derivedPassword = new PasswordDeriveBytes(password, saltValueBytes, hashAlgorithm.ToString(), passwordIterations);
                byte[] keyBytes = derivedPassword.GetBytes(Convert.ToInt32(keySize) / 8);
                var symmetricKey = new RijndaelManaged { Mode = CipherMode.CBC };
                byte[] cipherTextBytes = null;

                using (ICryptoTransform encryptor = symmetricKey.CreateEncryptor(keyBytes, initialVectorBytes))
                {
                    using (var memStream = new MemoryStream())
                    {
                        using (var cryptoStream = new CryptoStream(memStream, encryptor, CryptoStreamMode.Write))
                        {
                            cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                            cryptoStream.FlushFinalBlock();
                            cipherTextBytes = memStream.ToArray();
                            memStream.Close();
                            cryptoStream.Close();
                        }
                    }
                }

                return Convert.ToBase64String(cipherTextBytes);
            }
            catch (Exception a)
            {
                LanLordlAPIs.Classes.Utility.Logger.Error("Exception occured on encryption" + a.Message);
                LanLordlAPIs.Classes.Utility.Logger.Error("Stack trace" + a.StackTrace);
                throw a;
            }
        }
        /// <summary>
        /// Decrypts a string
        /// </summary>
        /// <param name="cipherText">Text to be decrypted</param>
        /// <param name="password">Password to decrypt with</param>
        /// <param name="salt">Salt to decrypt with</param>
        /// <param name="hashAlgorithm">Can be either SHA1 or MD5</param>
        /// <param name="passwordIterations">Number of iterations to do</param>
        /// <param name="initialVector">Needs to be 16 ASCII characters long</param>
        /// <param name="keySize">Can be 128, 192, or 256</param>
        /// <returns>A decrypted string</returns>
        private static string Decrypt(string cipherText, string password, string salt, AESHashAlgorithm hashAlgorithm, int passwordIterations, string initialVector, AESKeySize keySize)
        {
            try
            {
                byte[] initialVectorBytes = Encoding.ASCII.GetBytes(initialVector);
                byte[] saltValueBytes = Encoding.ASCII.GetBytes(salt);
                byte[] cipherTextBytes = Convert.FromBase64String(cipherText);

                var derivedPassword = new PasswordDeriveBytes(password, saltValueBytes, hashAlgorithm.ToString(), passwordIterations);
                byte[] keyBytes = derivedPassword.GetBytes(Convert.ToInt32(keySize) / 8);
                var symmetricKey = new RijndaelManaged();
                symmetricKey.Mode = CipherMode.CBC;
                var plainTextBytes = new byte[cipherTextBytes.Length];
                int byteCount = 0;

                using (ICryptoTransform decryptor = symmetricKey.CreateDecryptor(keyBytes, initialVectorBytes))
                {
                    using (var memStream = new MemoryStream(cipherTextBytes))
                    {
                        using (var cryptoStream = new CryptoStream(memStream, decryptor, CryptoStreamMode.Read))
                        {

                            byteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
                            memStream.Close();
                            cryptoStream.Close();
                        }
                    }
                }

                return Encoding.UTF8.GetString(plainTextBytes, 0, byteCount);
            }
            catch (Exception a)
            {
                LanLordlAPIs.Classes.Utility.Logger.Error("Exception occured on decryption" + a.Message);
                LanLordlAPIs.Classes.Utility.Logger.Error("Stack trace" + a.StackTrace);
                throw a;
            }
        }
    }
}