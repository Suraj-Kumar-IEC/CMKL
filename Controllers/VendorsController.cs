using CMKL.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace CMKL.Controllers
{
    // Controllers/VendorsController.cs
    public class VendorsController : Controller
    {
        IECEntities DB = new IECEntities();
        private readonly List<VendorInfo> _vendors = new List<VendorInfo>();

        public ActionResult Index()
        {
            return View(_vendors);
        }
        public ActionResult RefreshVendorList()
        {
            var list = DB.Vendors.OrderByDescending(v => v.Id).ToList(); 
            string html = RenderPartialViewToString("_VendorList", list);
            return Json(new { Html = html }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult Create()
        {
            var list = DB.Vendors.OrderByDescending(v => v.Id).ToList();
            return View(list); // Make sure the view is expecting a List<Vendor>
        }

        [HttpPost]
        public ActionResult Create(string VendorName, string ContactName, string ContactNumber, string Profile)
        {
            Vendor VD = new Vendor();
            if (ModelState.IsValid)
            {
                VD.VendorName = VendorName;
                VD.ContactName = ContactName;
                VD.ContactNumber = ContactNumber;
                VD.Profile = Profile;
                DB.Vendors.Add(VD);
                DB.SaveChanges();

                var list = DB.Vendors.OrderByDescending(v => v.Id).ToList();
                string html = RenderPartialViewToString("_VendorList", list);
                return Json(new { Success = true, Html = html });
            }
            else
            {
                return Json(new { Success = false, ErrorMessage = "Error creating vendor" });
            }
        }

        private string RenderPartialViewToString(string viewName, object model)
        {
            ViewData.Model = model;
            using (StringWriter sw = new StringWriter())
            {
                ViewEngineResult viewResult = ViewEngines.Engines.FindPartialView(ControllerContext, viewName);
                ViewContext viewContext = new ViewContext(ControllerContext, viewResult.View, ViewData, TempData, sw);
                viewResult.View.Render(viewContext, sw);
                return sw.GetStringBuilder().ToString();
            }
        }
    }

    public class VendorInfo
    {
        public string VendorName { get; set; }
        public string ContactName { get; set; }
        public string ContactNumber { get; set; }
        public string Profile { get; set; }
    }
    
}