//------------------------------------------------------------------------------
// <auto-generated>
//    This code was generated from a template.
//
//    Manual changes to this file may cause unexpected behavior in your application.
//    Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace LanLordlAPIs.Models.db_Model
{
    using System;
    using System.Collections.Generic;
    
    public partial class MemberNotification
    {
        public System.Guid NotificationId { get; set; }
        public System.Guid MemberId { get; set; }
        public Nullable<bool> FriendRequest { get; set; }
        public Nullable<bool> InviteRequestAccept { get; set; }
        public Nullable<bool> TransferSent { get; set; }
        public Nullable<bool> TransferReceived { get; set; }
        public Nullable<bool> TransferAttemptFailure { get; set; }
        public Nullable<bool> EmailFriendRequest { get; set; }
        public Nullable<bool> EmailInviteRequestAccept { get; set; }
        public Nullable<bool> EmailTransferSent { get; set; }
        public Nullable<bool> EmailTransferReceived { get; set; }
        public Nullable<bool> EmailTransferAttemptFailure { get; set; }
        public Nullable<bool> NoochToBank { get; set; }
        public Nullable<bool> BankToNooch { get; set; }
        public Nullable<bool> TransferUnclaimed { get; set; }
        public Nullable<bool> BankToNoochRequested { get; set; }
        public Nullable<bool> BankToNoochCompleted { get; set; }
        public Nullable<bool> NoochToBankRequested { get; set; }
        public Nullable<bool> NoochToBankCompleted { get; set; }
        public Nullable<bool> InviteReminder { get; set; }
        public Nullable<bool> LowBalance { get; set; }
        public Nullable<bool> ValidationRemainder { get; set; }
        public Nullable<bool> ProductUpdates { get; set; }
        public Nullable<bool> NewAndUpdate { get; set; }
        public Nullable<System.DateTime> DateCreated { get; set; }
        public Nullable<System.DateTime> DateModified { get; set; }
    
        public virtual Member Member { get; set; }
    }
}
