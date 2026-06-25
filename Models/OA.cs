using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CMKL.Models
{
    public class OA
    {
        public int id { get; set; }
        public string customername { get; set; }
        public string engine { get; set; }
        public string rating { get; set; }

        //Purchase Items
        public string basicpricekd { get; set; }
        public int? incentive { get; set; } //used ? means null value can be pass in int type

        public int orderprice { get; set; }
        public int orderpricegst { get; set; }
        public int billingamount { get; set; }
        public string advance { get; set; }
        public int? endcustomerorderprice { get; set; }// used ? means null value can be pass in int type
        public int? endcustomerorderpricewithGST { get; set; }// used ? means null value can be pass in int type
        public string KDtotalamountwithGST { get; set; }
        public int KDtotalamounttogoem { get; set; }
        public int totalamounttilldt { get; set; }
        public int balanceamount { get; set; }
        public int amountrecieved { get; set; }
        public string paymentmode { get; set; }
        public string paymentdt { get; set; }
        public int pendingamounttransfer { get; set; }



        public IEnumerable<OA> log { get; set; }
    }
    // SalesViewModel.cs

    public class SaleOrderSummary
    {
        public int SaleYears { get; set; }
        public string SaleMonth { get; set; }
        public int OrderCount { get; set; }
        public long TotalTaxValue { get; set; }
        public long GrossTotalAmount { get; set; }
    }
    public class SalesViewModel
    {
        public int SaleYear { get; set; }
        public string SaleMonth { get; set; }
        public int OrderCount { get; set; }
        public decimal TotalTaxValue { get; set; }
        public decimal GrossTotalAmount { get; set; }
    }

    // MonthlySalesViewModel.cs
    public class MonthlySalesViewModel
    {
        public int Year { get; set; }
        public string Month { get; set; }
        public int TotalSales { get; set; }
    }

    public class Authoriseddealer
    {
        public int oaid { get; set; }
        public string branch { get; set; }
        public string dealername { get; set; }
        public string dealerincentive { get; set; }
        public string enginemodel { get; set; }
        public string rating { get; set; }
        public string phase { get; set; }
        public IEnumerable<Authoriseddealer> log { get; set; }


    }
    public class MyViewModel
    {
        public bool IsAuthenticated { get; set; }
        // other properties
    }
    public class BillSeriesRecord
    {
        public string Billseries { get; set; }
        public int TotalRecords { get; set; }
        public int OAStage { get; set; }
    }


    public class orderdetailmodel
    { 
     public string bookingoffice { get; set; }
     public string ExecutionDate { get; set; }
     public string reasonforold { get; set; }
        public string typeoforder { get; set; }
        public string jointlyperson { get; set; }
        public string OE { get; set; }
        public string companyname { get; set; }
        public string customercontactname { get; set; }
        public string customermobile { get; set; }
        public string customeremail{ get; set; }
        public string billingcompany { get; set; }
        public string installationdestination { get; set; }
        public string consigneeaddress { get; set; }
        public bool consigneeaddressactive { get; set; }



    }

}