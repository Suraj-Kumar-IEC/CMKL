using CMKL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CMKL.VMModels
{
    public class StatusVM
    {
        public CMKL_Enquiry CE {get; set; }
        public List<CMKL_Stage> CS { get; set; }
    }
}