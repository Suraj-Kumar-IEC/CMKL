using CMKL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using System.IO;
using System.Web.Configuration;
using System.Data.SqlClient;
using System.Data;


namespace CMKL.Controllers
{
    public class MasterController : Controller
    {
        // GET: Master
        IECEntities DB = new IECEntities();
        public ActionResult SupplierMaster()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "SupplierMaster" && ab.Status == true
                               select ab).SingleOrDefault();
            if (checkaccess == null)
            {
                // 3. Store error message in TempData (survives one redirect)
                TempData["ErrorMessage"] = "Access Denied: You are not authorized to view This Page";

                // 4. Redirect to Home Controller, Index Action
                return RedirectToAction("Dashboard", "Dashboard");
            }
            var suppliers = DB.SupplierMasters.ToList();

            ViewBag.Suppliers = suppliers;
            return View();
        }

        public ActionResult CopyData()
        {
            return View();
        }
        public ActionResult CopyDatatoTestingtable()
        {
            //First Need to Filter Data
            /* var Dispatch = (from ab in DB.DispatchDetails
                             join ewap in DB.BOMEwapDetails on ab.Ewapid equals ewap.id
                             join bb in DB.BOM_TestingUpdate on ewap.BomVoucherHeadID equals bb.BOMHeadid
                             where
                             !DB.BOM_TestingUpdate.Any(btd => btd.BOMHeadid == ewap.BomVoucherHeadID)
                             select new
                             {
                                 ewap.BomVoucherHeadID,
                                 ewap.BatteryQuantity,
                                 ewap.BatteryRating,
                                 ewap.Dateoftesting,
                                 ewap.BatterySerialNo,
                                 ewap.EngineSerialNumber,

                             }).ToList();
            */
            DateTime Start = DateTime.Parse("2025-04-01");

            var Dispatch1 = (from ab in DB.DispatchDetails
                             join ewap in DB.BOMEwapDetails on ab.Ewapid equals ewap.id
                             join Rat in DB.Ratings_Production on ewap.GensetRating equals Rat.Rating into ratgroup
                             from Rat in ratgroup.DefaultIfEmpty() // Rat can be null here
                             where
                                 ab.BillDate.HasValue && ab.BillDate.Value >= Start &&
                                 ewap.BomVoucherHeadID.HasValue &&
                                 ewap.Dateoftesting.HasValue
                             orderby ab.BillDate ascending
                             select new
                             {
                                 BOMHeadidE = ewap.BomVoucherHeadID.Value,
                                 ewap.BatteryQuantity,
                                 ewap.BatteryRating,
                                 ewap.Dateoftesting,
                                 ewap.BatterySerialNo,
                                 ewap.EngineSerialNumber,
                                 ab.BillDate,
                                 ewap.KRMNo,
                                 ewap.PanelSerialNo,
                                 
                                 

                                 // >>> CORRECTED LINE HERE <<<
                                 // Check if 'Rat' (the object from the left join) is null.
                                 // If 'Rat' is null, return "NA".
                                 // Otherwise (if 'Rat' is not null), convert its 'id' to string.
                                 RatingId = Rat != null ? Rat.id.ToString() : "NA",
                             }).ToList();
            var filter = Dispatch1.Where(x => x.RatingId == "NA").ToList();

            // 2. Get a list of BOMHeadids from BOM_TestingUpdate, ensuring non-nullable int values
            var Testing = (from bb in DB.BOM_TestingUpdate
                           where bb.BOMHeadid.HasValue // <<< IMPORTANT: Only include non-null BOMHeadid values
                           select bb.BOMHeadid.Value) // <<< Use .Value to get non-nullable int
                          .Distinct() // Get unique values for efficiency in Contains
                          .ToList(); // Materialize Testing into memory
            var Result = Dispatch1.Where(d => !Testing.Contains(d.BOMHeadidE)).ToList();

            //Insert These Record in Table

           
            foreach (var nn in Result)
            {
                BOM_TestingUpdate BM = new BOM_TestingUpdate();
                BM.BOMHeadid = nn.BOMHeadidE;
                BM.BTSerial = nn.BatterySerialNo;
                BM.KRMNo = nn.KRMNo;
                BM.BTMake = "Kirloskar";
                BM.CPSerialNumber = nn.PanelSerialNo;
                BM.BTRating = nn.BatteryRating;
                BM.BTQty = nn.BatteryQuantity;
                BM.TestingDate = nn.Dateoftesting;
                BM.GensetRating = Convert.ToInt32(nn.RatingId);
                BM.CanopyQty = 1;
                BM.LubeOil = "NA";
                BM.KCool = "NA";
                BM.ADBlue = "NA";
                BM.SpecialRemarks = "Migrated Data";
                BM.CreatedBy = "Administrator";
                BM.Createdon = nn.Dateoftesting;
                BM.CPType = 2;
                BM.CPRating = Convert.ToInt32(nn.RatingId);
                BM.CPRemarks = "Migrated";
                DB.BOM_TestingUpdate.Add(BM);

            }
            DB.SaveChanges();



            return Json(filter);// 
        }
        [HttpPost]
        public ActionResult SaveMake(string make)
        {
            if (string.IsNullOrWhiteSpace(make))
            {
                return Json("Make cannot be empty"); // Or return a JSON error
            }

            var exists = DB.Make_Master.Any(m => m.Make == make);

            if (exists)
            {
                return Json(new { success = false, message = "Make already exists!" });
            }

            try
            {
                var newMake = new Make_Master { Make = make };
                DB.Make_Master.Add(newMake);
                DB.SaveChanges();
                var list = (from ab in DB.Make_Master
                           select ab).ToList();

                return Json(new { success = true, make = newMake, list=list }); // Return the saved object
            }
            catch (Exception ex)
            {
                // Log the error (using a logging framework like Serilog or NLog)
                return Json(new {success=false});
            }
        }

        [HttpPost]
        public JsonResult SaveSupplier(FormCollection form)
        {
            // Retrieve SupplierId from the form. It will be "0" for new, or the actual ID for edit.
            int supplierId = 0;
            if (!string.IsNullOrEmpty(form["SupplierId"]))
            {
                int.TryParse(form["SupplierId"], out supplierId);
            }

            var supplierName = form["SupplierName"];

            // Check for existing supplier name
            // For a new supplier, check if the name exists at all.
            // For an existing supplier (edit), check if the name exists for *another* supplier.
            bool supplierNameExists = DB.SupplierMasters.Any(
                ab => ab.SupplierName == supplierName && ab.id != supplierId
            );

            if (supplierNameExists)
            {
                return Json(new { success = false, msg = "Supplier Name Already Exists!" });
            }

            SupplierMaster supplier;

            if (supplierId == 0) // This is a new supplier
            {
                supplier = new SupplierMaster();
                // Populate common fields
                supplier.SupplierName = form["SupplierName"];
                supplier.SupplierContactName = form["SupplierContactName"];
                supplier.ContactNumber = form["ContactNumber"];
                supplier.Email = form["Email"];
                supplier.SupplierAddress = form["SupplierAddress"];
                supplier.SupplierAddress1 = form["SupplierAddress1"];
                supplier.State = form["SupplierState"]; // Assuming 'State' is the property name in your model
                supplier.GSTNumber = form["GSTNumber"];

                DB.SupplierMasters.Add(supplier);
                DB.SaveChanges();
                return Json(new { success = true, msg = "Supplier Saved Successfully!" });
            }
            else // This is an existing supplier being updated
            {
                supplier = DB.SupplierMasters.FirstOrDefault(s => s.id == supplierId);

                if (supplier == null)
                {
                    return Json(new { success = false, msg = "Supplier not found for update." });
                }

                // Update properties of the existing supplier
                supplier.SupplierName = form["SupplierName"];
                supplier.SupplierContactName = form["SupplierContactName"];
                supplier.ContactNumber = form["ContactNumber"];
                supplier.Email = form["Email"];
                supplier.SupplierAddress = form["SupplierAddress"];
                supplier.SupplierAddress1 = form["SupplierAddress1"];
                supplier.State = form["SupplierState"];
                supplier.GSTNumber = form["GSTNumber"];

                DB.SaveChanges(); // Save changes to the existing entity
                return Json(new { success = true, msg = "Supplier Updated Successfully!" });
            }
        }
        [HttpGet]
        public JsonResult GetSupplierById(int id)
        {
            var supplier = DB.SupplierMasters.FirstOrDefault(s => s.id == id);

            if (supplier != null)
            {
                // Return the supplier data as JSON
                // Ensure property names match what your JavaScript expects (e.g., SupplierId, SupplierName)
                return Json(new
                {
                    SupplierId = supplier.id,
                    SupplierName = supplier.SupplierName,
                    SupplierContactName = supplier.SupplierContactName,
                    ContactNumber = supplier.ContactNumber,
                    Email = supplier.Email,
                    SupplierAddress = supplier.SupplierAddress,
                    SupplierAddress1 = supplier.SupplierAddress1,
                    SupplierState = supplier.State, // Ensure this matches your model property name
                    GSTNumber = supplier.GSTNumber
                }, JsonRequestBehavior.AllowGet);
            }
            return Json(null, JsonRequestBehavior.AllowGet);
        }

        public ActionResult SupplierList()
        {
            var suppliers = DB.SupplierMasters.ToList();

            // Assign the suppliers list to ViewBag
            ViewBag.Suppliers = suppliers;

            // Return the partial view. Since data is in ViewBag, no model argument is needed here.
            return PartialView("_SupplierList"); // <--- No model argument needed here
        }
        public ActionResult DepartmentMaster()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "DepartmentMaster" && ab.Status == true
                               select ab).SingleOrDefault();
            if (checkaccess == null)
            {
                // 3. Store error message in TempData (survives one redirect)
                TempData["ErrorMessage"] = "Access Denied: You are not authorized to view This Page";

                // 4. Redirect to Home Controller, Index Action
                return RedirectToAction("Dashboard", "Dashboard");
            }
            return View();
        }
        [HttpGet]
        public JsonResult GetDepartments()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            // Retrieve departments from database
            // For demo purposes, I'm just returning a hardcoded list
            var departments = DB.DepartmentMasters.ToList().Where(x=> x.CompanyID==companyid);
            return Json(departments, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public JsonResult SaveDepartment(string departmentName)
        {
            if (string.IsNullOrWhiteSpace(departmentName))
            {
                return Json("error: Department name is required!", JsonRequestBehavior.AllowGet);
            }

            if (DB.DepartmentMasters.Any(d => d.DepartmentName.ToLower() == departmentName.ToLower()))
            {
                return Json("error: Department name already exists!", JsonRequestBehavior.AllowGet);
            }

            // If no errors, save the department and return a success message


            DepartmentMaster department = new DepartmentMaster();
            department.DepartmentName = departmentName;
            DB.DepartmentMasters.Add(department);
            DB.SaveChanges();
            return Json("Department saved successfully!", JsonRequestBehavior.AllowGet);
        }
        public ActionResult SQLbackup()
        {
            return View();
        }
        [HttpPost]
        public JsonResult CreateBackup()
        {
            try
            {
                // Get the current date and time
                DateTime currentDate = DateTime.Now;

                // Create a backup file name with the current date
                string backupFileName = $"IEC_{currentDate.ToString("yyyy-MM-dd_HH-mm-ss")}.bak";

                // Create a backup file path on the server
                string backupDirectory = Server.MapPath("~/Backup");
                if (!Directory.Exists(backupDirectory))
                {
                    Directory.CreateDirectory(backupDirectory);
                }
                string backupFilePath = Path.Combine(backupDirectory, backupFileName);

                // Use a parameterized query to execute the backup command
                string sql = "BACKUP DATABASE IEC TO DISK = @backupFilePath WITH CHECKSUM";

                using (var connection = DB.Database.Connection)
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = sql;
                        var parameter = new SqlParameter("@backupFilePath", SqlDbType.NVarChar, 4000);
                        parameter.Value = backupFilePath;
                        command.Parameters.Add(parameter);
                        command.ExecuteNonQuery();
                    }
                }

                // Return a JSON object indicating success
                return Json(new { success = true });
            }
            catch (SqlException ex)
            {
                // Return a JSON object indicating failure and the error message
                return Json(new { success = false, error = ex.Message });
            }
            catch (Exception ex)
            {
                // Return a JSON object indicating failure and the error message
                return Json(new { success = false, error = ex.Message });
            }
        }
    }
}