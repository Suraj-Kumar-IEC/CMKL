using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CMKL.Models
{
    public class BOMItem
    {

     public int BOMid {  get; set; }   
     public string FinalItem { get; set; }
     public string RawItemName { get; set; }
        public int RawItemID { get; set; }
     public string Desc { get; set; }
     public string Category { get; set; }
     public int CategoryID { get; set; }
     public string subCategory { get; set; }
     public int SubCategoryID { get; set; }  
     public decimal Quantity { get; set; }
     public string UOM { get; set; }
     public decimal Stock { get; set; }
     public string ItemCode { get; set; }
     public int id { get; set; }
     public decimal MOQ { get; set; }    

     public decimal Minimumstocklevel { get; set; }


        
    }
    public class Category
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public List<SubCategory> SubCategories { get; set; }
    }

    public class SubCategory
    {
        public int SubCategoryId { get; set; }
        public int CategoryId { get; set; }
        public string SubCategoryName { get; set; }
        public Category Category { get; set; }
    }

    public class BOMVoucherclass
    {
        public int BOMVoucherID { get; set; }
        public string VoucherNumber { get; set; }
        public DateTime VoucherDate { get; set; }
        public string Approvalstatus { get; set; }
        public string ApprovedBY { get; set; }
        public virtual ICollection<BOMVoucherLine> BOMVoucherLines { get; set; }
    }

    public class BOMVoucherLine
    {
        public int id { get; set; }
        public int BOMVoucherid { get; set; }
        public string CategoryName { get; set; }
        public string SubcategoryName { get; set; }
        public decimal AvailableStock {  get; set; }
        public string RawItemcode { get; set; }
        public string RawItemName { get; set; }
        public string FinalItemName { get; set; }
        public int Rawitemid { get; set; }

        public decimal Stock { get; set; }  

        public string Action {  get; set; }


        public int Categoryid { get; set; }
        public int Subcategoryid { get; set; }
        public int Finalitemid { get; set; }
        public int rawitemid { get; set; }
        public decimal QuantityRequired { get; set; }
        public decimal ApprovedQuantity { get; set; }
        public string UOM { get; set; }
        public bool Stockapproved { get; set; }
        public string Approvedby { get; set; }
        public DateTime Approveddate { get; set; }
        public virtual BOMVoucherclass BOMVoucher { get; set; }
    }
    public class BOMReturnHead
    {
        public int returnid { get; set; }
        public int fgitemid { get; set; }
        public int masteritemid { get; set; }
        // ... other properties for the indent header
    }

    public class BOMReturnLine
    {
       
        public int itemid { get; set; }        
        public decimal quantityrequired { get; set; }
        public int categoryid {  get; set; }
        public int subcategoryid { get; set;}
        // ... other properties for the indent line
    }


}