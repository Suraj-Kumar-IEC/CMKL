using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Web;
using System.Web.Mvc;
using CMKL.Models;

namespace CMKL.Controllers
{
    public class EmailManageController : Controller
    {
        //cmklentities cm = new cmklentities();
        IECEntities DB = new IECEntities();
        // GET: EmailManage
        public ActionResult EmailView()
        {
            var Email = (from ab in DB.CMKL_Email
                         select ab.DDLName).Distinct().ToList();
            ViewBag.EmailType = new SelectList(Email.AsEnumerable(), "DDLName");
            return View();
        }
        public ActionResult EmailData(string typeEmail)
        {
            var Email = (from ab in DB.CMKL_Email
                         where ab.DDLName == typeEmail && ab.Active==1
                         select ab).ToList();
           
            return View(Email);
        }
        public ActionResult CreateNew()
        {       

            return View();
        }
        [HttpPost]
        public ActionResult AddEmail(string Email, string typeEmail)
        {
            if (ModelState.IsValid)
            {
                //string type = "";
                CMKL_Email CE = new CMKL_Email();

               // CE.Type = type;
                CE.Email = Email;
                CE.DDLName = typeEmail;
                CE.Active = 1;
                DB.CMKL_Email.Add(CE);
                DB.SaveChanges();
                return Json(data: new { success = true, message = "Record Has Been Added" }, JsonRequestBehavior.AllowGet);
            }
            else
            {
                return Json(data: new { success = false , message = "There is Something Wrong" }, JsonRequestBehavior.AllowGet);
            }
            
        }
        public ActionResult DeleteEmail(int id, string EType)
        {
            var GetRC = (from ab in DB.CMKL_Email
                         where ab.id == id
                         select ab).FirstOrDefault();
            GetRC.Active =2;
            DB.SaveChanges();

            var Refresh = (from bc in DB.CMKL_Email
                           where bc.DDLName == EType && bc.Active == 1 
                           select bc).ToList();
            ViewBag.Delete = "Record Has Been Deleted";

            return View("EmailData", Refresh);
        }
       

    }
}