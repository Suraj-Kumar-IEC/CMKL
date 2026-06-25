using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.Diagnostics;
using System.EnterpriseServices.CompensatingResourceManager;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Web;
using System.Web.Management;
using System.Web.Mvc;
using CMKL.Models;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CMKL.Controllers
{
    public class LoginController : Controller
    {
        IECEntities DB = new IECEntities();
        // GET: Login
        public ActionResult Index()
        {
            ViewBag.IsAdmin = false;
            return View();
        }
        public ActionResult GuestLogin()
        {
            if (Request.QueryString["OrderId"] != null)
            {
                string orderId = Request.QueryString["OrderId"];

                // Store the OrderId in ViewBag
                ViewBag.OrderId = orderId;
            }
            return View("GuestLogin");
        }
        public ActionResult QRLogin(string user,string pass,string id)
        {
            var check = (from ab in DB.CMKL_QRLogin
                        where ab.email == user && ab.password==pass
                        select ab).FirstOrDefault();
            if(check==null)
            {
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);
            }
            else
            {
                Session["Login"] ="Yes";
                Session["id"]=id;
                return Json(new { success = true }, JsonRequestBehavior.AllowGet);
            }
           
        }
        [HttpPost]
        public ActionResult Login(tbl_User_Master TD, string finyear)
        {
            string loca1 = string.Empty;
            if (ModelState.IsValid)
            {
                var check = (from ab in DB.tbl_User_Master
                             where ab.user_name == TD.user_name && ab.user_password == TD.user_password
                             select ab).FirstOrDefault();
                if (check == null)
                {
                    loca1 = "Index";
                    return Json(data: new { success = false, message = "Wrong User Information", location = loca1 }, JsonRequestBehavior.AllowGet);
                    // ViewBag.err = "Login Failed, Pleae check Credentials and Try Again.";
                    // return View("Index");
                }
                //Check Company Access to User
                // Bypass Company Access check if user_Role is admin
                  if (!check.user_Role.Contains("ADMIN") && check.CompanyId != TD.CompanyId)
                    
                    {                        
                            loca1 = "Index";
                            return Json(data: new { success = false, message = "You Are Not Authorize to Log In to This Company", location = loca1 }, JsonRequestBehavior.AllowGet);
                                              
                    }

                    // return View("~Views/Home/Index.cshtml");
                    else if (!string.IsNullOrEmpty(check.ToString()))
                    {

                    // if (check.user_Role == "CADMIN")

                    Session["U_Name"] = Convert.ToString(check.user_name);
                    Session["Fin_Year"] = (finyear);
                    Session["LoggedIn"] = "YES";
                    Session["Company_ID"] = TD.CompanyId;
                    int Comapnyid =Convert.ToInt32(Session["Company_ID"]);
                    //Get Company Name 
                    var CompanyName = DB.Companies.Where(x => x.CompanyID == Comapnyid).Select(x=>x.CompanyName).SingleOrDefault();
                    var Companyshort = DB.Companies.Where(x => x.CompanyID == Comapnyid).Select(x => x.CompanyAddress2).SingleOrDefault();
                    Session["Company_Name"] = CompanyName;
                    Session["CShortName"] = Companyshort;
                    Session["U_Role"] = Convert.ToString(check.user_Role);
                    Session["U_FirstName"] = Convert.ToString(check.FirstName);
                    Session["U_LastName"] = Convert.ToString(check.LastName);
                    Session["DealerName"] = Convert.ToString(check.User_Email3);
                    Session["EndCustomer"] = Convert.ToString(check.EndCustomer);
                    Session["Userid"] = Convert.ToString(check.Id);
                    string Role = Session["U_Role"].ToString();


                    {
                        loca1 = "/Dashboard/Dashboard";
                    }
                    //loca1 = "/OE/Authoriseddealerview";
                    //return View("~/Views/Home/Index.cshtml");
                    ViewBag.CompanyID= TD.CompanyId;
                    return Json(data: new { success = true, message = "Welcome To Cloud ERP System", loca = loca1 }, JsonRequestBehavior.AllowGet);

                }
                else
                {
                    loca1 = "Index";
                    return Json(data: new { success = false, message = "Wrong User Information", loca = loca1 });

                    // return View("Index");
                }



            }
            else
            {
                return Json(data: new { success = false, message = "Test" });
            }




        }
        public ActionResult GetCompanies()
        {
            var Company = (from ab in DB.Companies
                           where ab.IsActive==1
                           select new
                           {
                               value = ab.CompanyID,
                               text = ab.CompanyName,
                           }).ToList();
            var FinYear = (from bb in DB.FinYears
                          select new
                          {
                              value=bb.id,
                              text=bb.FinYear1,
                          }).ToList();

            return Json(new {success=true,Company,FinYear},JsonRequestBehavior.AllowGet);
        }
        public ActionResult logout()
        {
            Session["U_Name"] = "";
            Session["LoggedIn"] = "";
            Session["U_Role"] = "";
            Session["U_FirstName"] = "";
            Session["U_LastName"] = "";
            Session["DealerName"] = "";
            Session["EndCustomer"] = null;
            Session["Userid"] = "";
            Session["Company_Name"] = "";
            Session["Fin_Year"] = "";
            //string Role = "";
            return View("index");
        }
        public ActionResult ChangePassword()
        {
            return View();
        }
        [HttpPost]
        public JsonResult UpdatePassword(string username, string oldPassword, string newPassword)
        {
            try
            {
                // Get the user from the database
                var user = DB.tbl_User_Master.FirstOrDefault(u => u.user_name == username);

                if (user != null)
                {
                    // Check if the old password is correct
                    if (user.user_password == oldPassword)
                    {
                        // Update the password
                        user.user_password = newPassword;
                        DB.SaveChanges();

                        return Json(new { success = true });
                    }
                    else
                    {
                        return Json(new { success = false, error = "The existing password is incorrect." });
                    }
                }
                else
                {
                    return Json(new { success = false, error = "The user name is not found." });
                }
            }
            catch (DbEntityValidationException ex)
            {
                foreach (var validationError in ex.EntityValidationErrors)
                {
                    foreach (var error in validationError.ValidationErrors)
                    {
                        Console.WriteLine("Error: {0} - {1}", error.PropertyName, error.ErrorMessage);
                    }
                }
                return Json(new { success = false, error = "Validation error occurred." });
            }
        }
        public ActionResult CheckSessionTimeout()
        {
            if (Session["LoggedIn"] == null)
            {
                return Json(new { timedOut = true }, JsonRequestBehavior.AllowGet);
            }
            else
            {
                return Json(new { timedOut = false }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}