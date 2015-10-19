using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using LanLordlAPIs.Classes.Utility;

namespace LanLordlAPIs.Models.Output_Models
{
    public class LandlordsPaymentHistoryClass
    {

        public AccessTokenValidationOutput AuthTokenValidation { get; set; }

        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }

        public List<PaymentHistoryClass> Transactions { get; set; }

    }


    public class PaymentHistoryClass
    {
        public string TransactionId { get; set; }
        public string TransactionDate { get; set; }
        public string TenantId { get; set; }
        public string TenantName { get; set; }
        public string PropertyId { get; set; }
        public string UnitId { get; set; }
        public string UnitName { get; set; }
        public string UnitNum { get; set; }
        public string PropertyName { get; set; }

       public string PropertyAddress { get; set; }
        public string Amount { get; set; }
        public string TransactionStatus { get; set; }

    }
}