using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CMKL.Models
{
    public class Purcahse
    {
    }
    // IEPLStockIN_Head model
    public class PurchaseBillHead
    {
        public int Id { get; set; }
        public string BillNumber { get; set; }
        public DateTime BillDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Vouchernumber { get; set; }
        public string GRNumber { get; set; }
        public DateTime GRDate { get; set; }
        public int Supplierid { get; set; }
        public bool BillClosed { get; set; }
        public string BillCloseby { get; set; }
        public DateTime BillClosedatetime { get; set; }
        public int Taxid { get; set; }
        public decimal frieghtamount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal BillAmount { get; set; }
    }

    // IEPLStockIN_Detail model
    public class PurchaseBillDetail
    {
        public int Id { get; set; }
        public int HeadId { get; set; }
        public string ItemCode { get; set; }
        public decimal Quantity { get; set; }
        public bool QualityApproved { get; set; }
        public decimal QualityApprovedQty { get; set; }
        public DateTime QApprovedDate { get; set; }
        public decimal RejectedQuantity { get; set; }
        public string RejectionRemarks { get; set; }
        public decimal BasicPrice { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
    }
}