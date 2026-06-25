using CMKL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using CMKL.VMModels;
using Microsoft.Ajax.Utilities;

namespace CMKL.VMModels
{
    public class TAClass
    {
        public  TA_Master TAMaster { get; set; }
        public  TA_Transport TATransport { get; set; }
    }
    public class SaveTAMaster
    {
        public int id { get; set; }
        public string Name { get; set; }
        public string Designation { get; set; }
        public string JourneyFrom { get; set; }
        public string journeyto { get; set; }
        public DateTime DateofLeaving { get; set; }
        public DateTime DateofArrival { get; set; }
      



        public IEnumerable<TA_Transport> listofobjtamodelview { get; set; }

    }
    public class Splitjobclass
    {  
    
    
        public CMKL_Enquiry CE { get; set; }
        public IEnumerable<CMKL_Enquiry_Item> CEI { get; set; }
    }
   
}