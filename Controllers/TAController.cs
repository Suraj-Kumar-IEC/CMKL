using CMKL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Web;
using System.Web.Mvc;
using CMKL.VMModels;
using Microsoft.Ajax.Utilities;
using System.Web.Script.Serialization;
using Newtonsoft.Json;


namespace CMKL.Controllers
{
    public class TAController : Controller
    {
        IECEntities DB = new IECEntities();
        // GET: TA
        public ActionResult TAView()
        {
            //DropDown List Pass
          //  var TAtype = (from a in DB.TA_TypeMaster
                 //        select a.TAType).ToList();
          //  ViewBag.Type = new SelectList(TAtype.AsEnumerable(), "TAType");

          //  var Transport = (from ab in DB.TA_Transport
                   //          select new
                       //      {
                        //         ab.Mode,
                         //        ab.id,
                         //    }).ToList();
          //  var Master = (from bc in DB.TA_Master
                       //   select new
                       //   {
                             // bc.HotelExpenseTTL,
                         //     bc.id,
//}).ToList();
            //TAClass TAC = new TAClass();   
            

            
            return View("TAView");
        }
        
        [HttpPost]
        public JsonResult SaveRecord(SaveTAMaster TA)
        {
            TA_Master TM = new TA_Master();

            TM.Name = TA.Name;
            TM.Designation = TA.Designation;
            TM.JourneyFrom = TA.JourneyFrom;
            TM.journeyto = TA.journeyto;
            TM.DateofLeaving =Convert.ToDateTime(TA.DateofLeaving);
            TM.DateofArrival = Convert.ToDateTime(TA.DateofArrival);
            DB.TA_Master.Add(TM);
            DB.SaveChanges();
            return Json(data: "", JsonRequestBehavior.AllowGet);
            

           // return View("TAView");
           //ST.STAMaster DD = new ST.STAMaster()

        }

        [HttpPost]
        public JsonResult SaveNew(string data)
        {
            //Deserialize Ajax data

            tamodel model = JsonConvert.DeserializeObject<tamodel>(data);
            TA_head TH = new TA_head();
            
            
            //Save Entries in TA_Head

            TH.Name = model.name;
            TH.Designation = model.jesignation;
            TH.journeyfrom = model.jfrom;
            TH.journeyto = model.jto;
            TH.Dateofleavinghq = model.jstartdt;
            TH.Dateofarrivalhq = model.jenddt;
            TH.Advance = model.advance;
            TH.total = model.total;
            TH.balance = model.balance;
            TH.Approvalstatus = 0;
            DB.TA_head.Add(TH);
            DB.SaveChanges();
            var TAID = (from ab in DB.TA_head
                        where ab.Name == model.name && ab.Designation == model.jesignation && ab.Approvalstatus == 0
                        select ab.id).SingleOrDefault();
            int TA = Convert.ToInt32(TAID);

            //save Ta Items Code


            
            model.data.ForEach(m =>
            {
                DB.TA_items.Add(new TA_items
                {
                    
                    taid=TA,
                    tatype=m.tatype,
                    locationfrom=m.locationfrom,
                    locationto=m.locationto,
                    mode=m.mode,
                    amount=m.amount,                 


                });

                DB.SaveChanges();

            });


            return Json(data: "", JsonRequestBehavior.AllowGet);
        }


    }
   
}