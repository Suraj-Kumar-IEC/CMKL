using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CMKL.Models
{
    public class PurchaseOrderclass
    {
        public class POModel
        {
            public POHeadData HeadData { get; set; }
            public List<POTableData> TableData { get; set; }
        }
        public class POHeadData
        {
            public int headid { get; set; }
            public int Supplierid { get; set; }
            public int Companyid { get; set; }
            public string PONumber { get; set; }
            public DateTime PODate { get; set; }
            public string IndentNumber { get; set; }
            public DateTime IndentDate { get; set; }
            public DateTime DeliveryDate { get; set; }
            public decimal BillTaxableAmount { get; set; }
            public decimal BillGrossAmount { get; set; }
            public string frieghttype { get; set; }
            public decimal freightAmount { get; set; }
            public int frieghttax { get; set; }
            public decimal ttlfrieght { get; set; }
            public string insurancetype { get; set; }
            public decimal insuranceAmount { get; set; }
            public int Insurancetax { get; set; }
            public decimal ttlinsurace { get; set; }
            public string pftype { get; set; }
            public decimal pfCharges { get; set; }
            public int PFTax { get; set; }
            public decimal ttlpf { get; set; }
            public string othertype { get; set; }
            public decimal otherCharges { get; set; }
            public int OtherTax { get; set; }
            public decimal ttlother { get; set; }
            public string deliverymode { get; set; }
            public string remarks { get; set; }
            public string taxtypeonbill { get; set; }
            public decimal IGST { get; set; }
            public decimal CGST { get; set; }
            public decimal SGST { get; set; }
            public decimal Billtaxamount { get; set; }
            public decimal frieghttaxamount { get; set; }
            public decimal insurancetaxamount { get; set; }
            public decimal pftaxamount { get; set; }
            public decimal othertaxamount { get; set; }
            public string paymentterms { get; set; }
            public bool IsServiceOrder { get; set; }
        }
        public class POTableData
        {
            public int lineid { get; set; }
            public string itemcode { get; set; }
            public string make { get; set; }
            public int makeid { get; set; }
            public decimal quantity { get; set; } // Assuming quantity is an integer
            public string tax { get; set; }  // Assuming tax is a decimal
            public decimal listprice { get; set; }
            public decimal discount { get; set; }
            public decimal netbasic { get; set; }
            public decimal taxableamount { get; set; }
            public decimal taxamount { get; set; }
            public decimal grossamount { get; set; }
            public int taxid { get; set; } // Assuming taxid is an integer
            public int itemid { get; set; }
        }
    }
}