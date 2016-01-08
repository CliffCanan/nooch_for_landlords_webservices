using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using LanLordlAPIs.Models.db_Model;
using LanLordlAPIs.Models.Input_Models;

namespace LanLordlAPIs.Models.Output_Models
{
    public class TransactionsOutputClass
    {
    }



    // to send reminder emails to tenansts
    public class SendTransactionReminderEmailInputClass
    {
        public basicLandlordPayload User { get; set; }

        public string  ReminderType{ get; set; }
        public string  TransactionId{ get; set; }

        
    }


    // Charge Tenant
    public class ChargeTenantInputClass
    {
        public basicLandlordPayload User { get; set; }
        public ChargeTenantInputTransDetailsClass TransRequest { get; set; }
    }
    public class ChargeTenantInputTransDetailsClass
    {
        public string Memo { get; set; }
        public string Amount { get; set; }
        public string TenantId { get; set; }
        public bool IsRecurring { get; set; }
        public int UOBTId { get; set; }

    }

    // Send Reminder Email
    public class ReminderMailInputClass
    {
        public basicLandlordPayload User { get; set; }
        public SendReminderEmailInputClass Trans { get; set; }
    }
    public class SendReminderEmailInputClass
    {
        public string TenantId { get; set; }
        public string TransactionId { get; set; }
        public string ReminderType { get; set; }
    }

    // Cancel Transaction
    public class CancelTransInput
    {
        public basicLandlordPayload User { get; set; }
        public string TransId { get; set; }
    }


    public class SynapseDetailsClass
    {
        public SynapseBanksOfMember BankDetails { get; set; }
        public SynapseCreateUserResult UserDetails { get; set; }

        public bool wereBankDetailsFound { get; set; }
        public bool wereUserDetailsFound { get; set; }

        public string UserDetailsErrMessage { get; set; }
        public string AccountDetailsErrMessage { get; set; }
    }

    public class googleURLShortnerResponseClass
    {
        public string kind { get; set; }
        public string id { get; set; }
        public string longUrl { get; set; }
    }
}