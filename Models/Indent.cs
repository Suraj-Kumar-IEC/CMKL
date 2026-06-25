using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CMKL.Models
{
    public class IndentItem
    {
        public string ItemName { get; set; }
        public string ItemCode { get; set; }
        public string UOM { get; set; }
        public decimal? Quantity { get; set; }
        public string LastOrderDate { get; set; }
        public decimal? LastOrderQuantity { get; set; }
        public string LastSupplier { get; set; }
        public decimal? LastPrice { get; set; }
        public decimal? AvailableStock { get; set; }
        public decimal? MOQ { get; set; }
        public decimal? MinimumLevel { get; set; }
        public decimal? Average { get; set; }
    }
    public class IndentViewModel
    {
        public object IndentHeadData { get; set; }
        public List<IndentItem> IndentItemDetail { get; set; }  // Correct type
    }
}