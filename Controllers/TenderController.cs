using CMKL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace CMKL.Controllers
{
    public class TenderController : Controller
    {
        IECEntities DB = new IECEntities();
        // GET: Tender
        public ActionResult Index()
        {
            
            return View();
        }
        public ActionResult ActiveBids(string Del = "")
        {
            if (Del == "1")
            {
                ViewBag.update = "Bid Participated Successfully";
                
            }
            if (Del == "2")
            {
                ViewBag.update = "Bid Not Participated";

            }


            var Data = (from ab in DB.Tenders
                            where ab.Active == 1 orderby ab.DueDate ascending
                            select ab).ToList();
               // return View(Data);
            
            
            return View(Data);
        }

        public ActionResult ParticationDone(int id)
        {
            var Data = (from ab in DB.Tenders
                        where ab.ID == id
                        select ab).SingleOrDefault();
            Data.Active = 2;
            DB.SaveChanges();
            string Del = "1";
            
          
            
            return RedirectToAction("ActiveBids", new { Del});
        }
        public ActionResult ParticationNotDone(int id)
        {
            var Data = (from ab in DB.Tenders
                        where ab.ID == id
                        select ab).SingleOrDefault();
            Data.Active = 3;
            DB.SaveChanges();
            string Del = "2";



            return RedirectToAction("ActiveBids", new { Del });
        }

        [HttpPost]
        public ActionResult SaveTender(Tender TR)
        {
           if (ModelState.IsValid)
            {
                Tender TD = new Tender();
                TD.Buyer = TR.Buyer;
                TD.Rating = TR.Rating;
               // TD.FeesAmount = TR.FeesAmount;
                TD.TenderNumber = TR.TenderNumber;
                TD.DueDate = TR.DueDate;
                TD.Quantity = TR.Quantity;
                TD.Portal = TR.Portal;
                TD.EMDExemption = TR.EMDExemption;
                TD.Participated = TR.Participated;
                TD.ParticipatedDate = TR.ParticipatedDate;
                TD.Active = 1;
                DB.Tenders.Add(TD);
                DB.SaveChanges();
                ModelState.Clear();
                ViewBag.Status = "Data Saved Sucessfully";
            }
          else
           {
               ViewBag.err = "Something Is Wrong";
           }
            
            
            return View("Index");
        }
        
    }
}