using CMKL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace CMKL.Controllers
{
    public class UserAccessController : Controller
    {
        IECEntities DB = new IECEntities();

        public ActionResult UserAccessView()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "UserAccessView" && ab.Status == true
                               select ab).SingleOrDefault();
            if (checkaccess == null)
            {
                // 3. Store error message in TempData (survives one redirect)
                TempData["ErrorMessage"] = "Access Denied: You are not authorized to view This Page.";

                // 4. Redirect to Home Controller, Index Action
                return RedirectToAction("Dashboard", "Dashboard");
            }
            // Populate User Dropdown
            ViewBag.Users = new SelectList(DB.tbl_User_Master.Where(u => u.User_status == "ERPUSER"), "Id", "user_name");
            var modules = DB.User_Menu
                    .Select(m => m.MainMenu)
                    .Distinct()
                    .OrderBy(m => m)
                    .ToList();

            ViewBag.Modules = new SelectList(modules);

            return View();
        }

        // AJAX Call: Fetch all menus and mark which ones this user has
        [HttpGet]
        public JsonResult GetUserPermissions(int userId, string mainMenu)
        {
            // Get all menus filtered by the selected MainMenu category
            var filteredMenus = DB.User_Menu.Where(m => m.MainMenu == mainMenu).ToList();

            // Get current user access
            var userAccess = DB.User_Access.Where(a => a.UserID == userId && a.Status == true).ToList();

            var result = filteredMenus.Select(m => new {
                SystemName = m.UserMenu,      // Used for saving (e.g., "ItemmasterFG")
                DisplayName = m.DetailedOption, // Used for showing to user (e.g., "Item Master FG")
                HasAccess = userAccess.Any(a => a.MenuAccess == m.UserMenu)
            }).ToList();

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        // AJAX Call: Update or Create access record
        [HttpPost]
        public JsonResult TogglePermission(int userId, string menuName, bool isChecked)
        {
            try
            {
                var record = DB.User_Access.FirstOrDefault(a => a.UserID == userId && a.MenuAccess == menuName);
                bool statusValue = isChecked ? true : false;

                if (record != null)
                {
                    record.Status = statusValue;
                }
                else if (isChecked)
                {
                    // Create new record only if they are turning access ON
                    DB.User_Access.Add(new User_Access
                    {
                        UserID = userId,
                        MenuAccess = menuName,
                        Status = true
                    });
                }

                DB.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}