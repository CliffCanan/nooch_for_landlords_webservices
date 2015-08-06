using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LanLordlAPIs.Classes.Crypto
{
    public abstract class CryptographyBase
    {
        public abstract string Encrypt(string plainString, string cryptographyKey);

        public abstract string Decrypt(string encryptedString, string cryptographyKey);
    }
}