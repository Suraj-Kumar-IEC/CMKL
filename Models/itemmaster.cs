using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using CMKL.Models;

namespace CMKL.Models
{
    public class LrData
    {
        public int SupplierId { get; set; }
        public int BillId { get; set; }
        public int Billitemid { get; set; }
        public int ItemId {  get; set; }
        public DateTime LRDate { get; set; }
        public decimal Quantity { get; set; }
        public string Remarks { get; set; }
    }
    public class itemmaster
    {
        public ItemMasterGFE itemMasters { get; set; }
        public Company companies { get; set; }

    }
    public class FGitemData
    {
        public int finalItem { get; set; }
        public int canopy { get; set; }
        public int fueltank { get; set; }
        public int exhaustsystem { get; set; }
        public int acoustictreatment { get; set; }
        public int finalpacking { get; set; }
        public int assembly { get; set; }
        public int baseframe { get; set; }
        public int dgassy { get; set; }
        public int panel { get; set; }
        public int alternator { get; set; }
        public int electrical { get; set; }
        public string masteritemcode { get; set; }
        public string masteritemname { get; set; }
        public string masteruom { get; set; }

        public static implicit operator FGitemData(string v)
        {
            throw new NotImplementedException();
        }
    }
    public class itemcombine
    { 
     // public itemmaster itemdata { get; set; }

        public string company { get; set; }

        public string Productcode { get; set; }

        public string Itemname { get; set; }

        public string itemdesc { get; set; }

        public string Itemcategory { get; set; }

        public string norms { get; set; }

        public string Phase { get; set; }


    }

    public class stockreport
    {
        // public itemmaster itemdata { get; set; }

        public string company { get; set; }

        public string Productcode { get; set; }

        public string Itemname { get; set; }

        public string itemdesc { get; set; }

        public string Itemcategory { get; set; }

        public string unitofmeasurement { get; set; }

        public string Phase { get; set; }
        public int presentstock { get; set; }
        public int transit { get; set; }
        public int phylogistic { get; set; }
        public int phyplant { get; set; }
        public int readyforallocation { get; set; }
        public int wip { get; set; }
        public int testing { get; set; }

        public int rejected { get; set; }
        

        public string norms { get; set; }



    }

    public class Pendingpurchase
    {
        public int id { get; set; }
        public string company { get; set; }
        public string Billno { get; set; }
        public string billdate { get; set; }
        public int Billqty { get; set; }

        
    }
    public class PendingBPR
    {
        // public itemmaster itemdata { get; set; }
       
        public string company { get; set; }             

       public string Billno { get; set; }

        public string billdate { get; set; }

        public string Billqty { get; set; }



    }
   public class purchaseclass
    {
        public int itemid { get; set; }
        public int qty { get; set; }
        //public int total { get; set; }

        public IEnumerable<purchaseclass> Pur { get; set; }
    }
    

    public class stockclass
    {
        public int id { get; set; }
        public int transit { get; set; }
        public int total { get; set; }

        public IEnumerable<stockclass> stk { get; set; }
    }

    public class Logisticin
    {
        //Purcahse Head
        public int id { get; set; }
        public string CompanyName { get; set; }
        public string Billno { get; set; }
        public string billdate { get; set; }

        //Purchase Items
        public int Billid { get; set; }

        public int itemid { get; set; }

        public string Itemname { get; set; }
        public string Itemdesc { get; set; }
        public string logisticindate { get; set; }
        public string plantindate { get; set; }
        public string Productcode { get; set; }
        public int qty { get; set; }

        public int approved { get; set; }
        public IEnumerable<Logisticin> log { get; set; }


    }

    public class employeemaster
    {
        public int id { get; set; }
        public int transit { get; set; }
        public int total { get; set; }

        //public IEnumerable<stockclass> stk { get; set; }
    }
    public class IndentHead
    {
        public string VoucherNumber { get; set; }
        public DateTime VoucherDate { get; set; }
        // ... other properties for the indent header
    }

    public class IndentLine
    {
        public string expectedstkdate { get; set; }
        public int itemid { get; set; }
        public decimal basicrate { get; set; }
        public decimal stock { get; set; }
        public decimal average { get; set; }
        public decimal quantityrequired { get; set; }
        public decimal lastorderquantity { get; set; }
        public string lastorderdate { get; set; }
        public int previoussupplier { get; set; }
        public string userremarks { get; set; }
        public decimal QuantityRecieved { get; set; }   
        // ... other properties for the indent line
    }

}