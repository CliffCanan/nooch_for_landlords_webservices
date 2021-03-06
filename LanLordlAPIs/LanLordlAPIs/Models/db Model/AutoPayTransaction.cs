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
    
    public partial class AutoPayTransaction
    {
        public int Id { get; set; }
        public Nullable<System.Guid> SenderId { get; set; }
        public Nullable<System.Guid> RecepientId { get; set; }
        public Nullable<System.Guid> UnitId { get; set; }
        public Nullable<System.Guid> PropertyId { get; set; }
        public Nullable<int> UOBTId { get; set; }
        public string Memo { get; set; }
        public Nullable<System.DateTime> StartDate { get; set; }
        public Nullable<System.DateTime> EndDate { get; set; }
        public string Frequency { get; set; }
        public Nullable<bool> IsAllCancelledByLandlord { get; set; }
    
        public virtual Member Member { get; set; }
        public virtual Member Member1 { get; set; }
        public virtual Property Property { get; set; }
        public virtual PropertyUnit PropertyUnit { get; set; }
        public virtual UnitsOccupiedByTenant UnitsOccupiedByTenant { get; set; }
    }
}
