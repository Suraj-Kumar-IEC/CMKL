using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CMKL.Models
{
    public class tableviewmodel
    {
        public int Billid { get; set; }

        public int itemid { get; set; }

        public string company { get; set; }

        public string norms { get; set; }
        public int qty { get; set; }
        public int basicvalue { get; set; }

        public int flag { get; set; }

        public int approved { get; set; }
    }

    public class ajaxModel
    { 
    
        public List<tableviewmodel> data { get; set; }

        public string company { get; set; }

        public string bill { get; set; }

        public string date { get; set; }

        public int basicprice { get; set; }
        public int flag { get; set; }
        public int purchaseapproval { get; set; }


    }


    public class taitems
    {
        
        public int taid { get; set; }
        public string tatype { get; set; }
        public string locationfrom { get; set; }
        public string locationto { get; set; }
        public string mode { get; set; }
        public int amount { get; set; }




        //public List<tableviewmodel> data { get; set; }
    }
    public class tamodel
    {

        public List<taitems> data { get; set; }

        public int id { get; set; }

        public string name { get; set; }

        public string jesignation { get; set; }
        public string jfrom { get; set; }
        public string jto { get; set; }
        public string jstartdt { get; set; }

        public string jenddt { get; set; }
        public int advance { get; set; }
        public int total { get; set; }
        public int balance { get; set; }



    }
}