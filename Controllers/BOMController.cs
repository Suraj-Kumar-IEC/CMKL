using CMKL.Models;
using Microsoft.Ajax.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using static System.Net.Mime.MediaTypeNames;
using System.Web.UI.WebControls;
using System.Data.Entity.SqlServer;
using OfficeOpenXml;
using System.Security.Cryptography;
using ClosedXML.Excel;
using System.IO;
//using OfficeOpenXml.Table.PivotTable;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Drawing.Diagrams;
using DocumentFormat.OpenXml.Presentation;
using System.Data.Entity;
using DocumentFormat.OpenXml.Wordprocessing;
using CMKL.Views.BOM;
using static ClosedXML.Excel.XLPredefinedFormat;
using System.Windows.Interop;
using System.Data.Entity.Validation;
using System.Data;
using System.Web.WebSockets;
using DocumentFormat.OpenXml.Office2010.Excel;
using RazorEngine;
using System.Net.Mail;
using Rotativa;
using System.Globalization;
using System.ComponentModel.DataAnnotations;




namespace CMKL.Controllers
{
    public class BOMController : Controller
    {
        // GET: BOM
        IECEntities DB = new IECEntities();
        private int categoryid;
        
        public ActionResult UpdateBOMQtyPercentage()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "UpdateBOMQtyPercentage" && ab.Status == true
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
        public JsonResult GetItemDetailsForAdjustment(string itemCode)
        {
            try
            {
                var item = (from ab in DB.BOMItemMasters
                            join uom in DB.BOM_UOM on ab.UOM equals uom.id
                            where ab.ItemCode == itemCode
                            select new
                            {
                                ItemId = ab.Itemid,
                                ItemCode = ab.ItemCode,
                                ItemName = ab.ItemName,
                                ItemDesc = ab.Desc,
                                UOM = uom.UOM
                            }).SingleOrDefault();
                
                if (item != null)
                {
                    return Json(new { success = true, item = item }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = false, message = "Item not found or invalid Item Code." }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching item details for adjustment: {ex.Message}");
                return Json(new { success = false, message = "Error fetching item details." }, JsonRequestBehavior.AllowGet);
            }
        }
        public JsonResult GetBOMVoucherlinesForAdjustment(int rawItemId, string fromDate, string toDate)
        {
            try
            {
                // Ensure toDate includes the entire day
                //DateTime adjustedToDate = toDate.Date.AddDays(1).AddTicks(-1);
                System.DateTime fromDateDt = System.DateTime.ParseExact(fromDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                System.DateTime toDateDt = System.DateTime.ParseExact(toDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                toDateDt = toDateDt.Date.AddDays(1).AddTicks(-1);
                var lines = (from bvl in DB.BOMVoucherlines
                             join im in DB.BOMItemMasters on bvl.rawitemid equals im.Itemid
                             where bvl.rawitemid == rawItemId &&
                                   bvl.Isdeleted == 0 && // Only non-deleted lines
                                   bvl.ApprovedQuantity.HasValue && // Only lines with an approved quantity
                                   bvl.Approveddate.HasValue && bvl.Approveddate.Value >= fromDateDt && bvl.Approveddate.Value <= toDateDt
                             select new //BOMVoucherlineAdjustmentDTO
                             {
                                 Id = bvl.id,
                                 BOMVoucherId = bvl.BOMVoucherid ?? 0, // Default to 0 if nullable
                                 ItemCode = im.ItemCode, // From BOMItemMasters
                                 ItemName = im.ItemName, // From BOMItemMasters
                                 QuantityRequired = bvl.QuantityRequired ?? 0m,
                                 ApprovedQuantity = bvl.ApprovedQuantity.Value, // Get non-nullable value
                                 UOM = bvl.UOM,
                                 RawItemId = bvl.rawitemid ?? 0 // Ensure non-nullable for DTO
                             }).ToList();

                if (lines.Any())
                {
                    return Json(new { success = true, data = lines }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = false, message = "No records found for the selected item and date range." }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching BOMVoucherlines for adjustment: {ex.Message}");
                return Json(new { success = false, message = "Error fetching records." }, JsonRequestBehavior.AllowGet);
            }
        }
        public class LineAdjustmentData
        {
            /// <summary>
            /// The primary key (Id) of the BOMVoucherlines record to be updated.
            /// </summary>
            [Required]
            public int LineId { get; set; }

            /// <summary>
            /// The new calculated ApprovedQuantity for this line item after reduction.
            /// </summary>
            [Required]
            [Range(0, double.MaxValue, ErrorMessage = "New Approved Quantity must be a non-negative number.")]
            public decimal NewApprovedQuantity { get; set; }
        }
        public class BulkQuantityAdjustmentViewModel
        {
            /// <summary>
            /// The Raw Item ID for which the adjustment is being applied.
            /// </summary>
            public int RawItemId { get; set; }

            /// <summary>
            /// The start date of the range for which records were fetched.
            /// </summary>
            [Required(ErrorMessage = "From Date is required.")]
            public System.DateTime FromDate { get; set; } // <<< FIX: Explicitly use System.DateTime

            /// <summary>
            /// The end date of the range for which records were fetched.
            /// </summary>
            [Required(ErrorMessage = "To Date is required.")]
            public System.DateTime ToDate { get; set; } // <<< FIX: Explicitly use System.DateTime

            /// <summary>
            /// The percentage by which to reduce the ApprovedQuantity (e.g., 10 for 10%).
            /// </summary>
            [Required(ErrorMessage = "Percentage Reduction is required.")]
            [Range(0, 100, ErrorMessage = "Percentage reduction must be between 0 and 100.")]
            public decimal PercentageReduction { get; set; }

            /// <summary>
            /// A list of specific line items (by ID and new quantity) to be updated.
            /// </summary>
            [Required(ErrorMessage = "At least one line item must be selected for update.")]
            public List<LineAdjustmentData> LinesToUpdate { get; set; } = new List<LineAdjustmentData>();
        }
        [HttpPost]
        public JsonResult ApplyPercentageReduction(BulkQuantityAdjustmentViewModel model)
        {
            if (model.LinesToUpdate == null || !model.LinesToUpdate.Any())
            {
                return Json(new { success = false, message = "No lines provided for update." });
            }
            if (model.PercentageReduction < 0 || model.PercentageReduction > 100)
            {
                return Json(new { success = false, message = "Percentage reduction must be between 0 and 100." });
            }

            try
            {
                // Retrieve CompanyID and CreatedBy from session (if needed for audit/log)
                // int companyId = Convert.ToInt32(Session["Company_ID"]);
                // string updatedBy = Session["U_Name"]?.ToString() ?? "System";

                foreach (var lineToUpdate in model.LinesToUpdate)
                {
                    var existingLine = DB.BOMVoucherlines.Find(lineToUpdate.LineId); // Find by primary key

                    if (existingLine != null && existingLine.ApprovedQuantity.HasValue)
                    {
                        decimal currentApprovedQuantity = existingLine.ApprovedQuantity.Value;
                        decimal reductionFactor = model.PercentageReduction / 100m;
                        decimal newQuantity = currentApprovedQuantity * (1m - reductionFactor);

                        // Round the new quantity to 2 decimal places (or as needed)
                        existingLine.ApprovedQuantity = Math.Round(newQuantity, 2, MidpointRounding.AwayFromZero);
                        // You might also update Approvedby, Approveddate for audit
                        // existingLine.Approvedby = updatedBy;
                        // existingLine.Approveddate = DateTime.Now;
                    }
                }
                DB.SaveChanges(); // Save all changes in one transaction

                return Json(new { success = true, message = "Approved Quantities updated successfully!" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying percentage reduction: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                return Json(new { success = false, message = "An error occurred while updating quantities: " + ex.Message });
            }
        }
    
        public ActionResult ReturnBomCreation(int? itemId)
        {           
            ViewBag.itemId = itemId; // Access the value of itemId
            return View();
        }
        public ActionResult BOMAlteration()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "BOMAlteration" && ab.Status == true
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
        public ActionResult AlterBOMDeleteLine(int lineid)
        {
            var FinYear = Session["Fin_Year"].ToString();
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());

            // 1. Mark Line to Remove
            var remove = (from ab in DB.BOMVoucherlines
                          where ab.id == lineid
                          select ab).SingleOrDefault();

            if (remove == null) return Json(new { success = false, msg = "Line not found" });

            remove.Isdeleted = 1;
            remove.Approvedby = Session["U_Name"].ToString();
            remove.Approveddate = System.DateTime.Now;

            // 2. Update Master Stock Table
            var itemid = remove.rawitemid;
            var stocktable = (from bb in DB.StockTables
                              where bb.itemid == itemid && bb.CompanyID == companyid
                              select bb).SingleOrDefault();

            if (stocktable != null)
            {
                stocktable.Stock += remove.ApprovedQuantity;
            }

            // 3. Reverse Lot Number to Available
            if (remove.LotID != null)
            {
                int lotid = Convert.ToInt32(remove.LotID);
                var lot = (from ab in DB.Stock_lotDetail
                           where ab.id == lotid
                           select ab).SingleOrDefault();

                if (lot != null)
                {
                    // Since your case is "Qty 1", this returns it to 1
                    lot.CurrentQuantity += remove.ApprovedQuantity;
                    lot.IsAvailable = true;
                }
            }

            // 4. SAVE ONCE (Atomic Transaction)
            DB.SaveChanges();

            return Json(new { success = true }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult AddBomAlterationItems(int bomidnumber, int itemid, string uom, int category, int subcategory, decimal quantityRequired)
        {
            var FinYear = Session["Fin_Year"].ToString();
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            string userName = Session["U_Name"].ToString();

            using (var dbContextTransaction = DB.Database.BeginTransaction())
            {
                try
                {
                    // 1. Get Final Item Info from existing BOM
                    var final = DB.BOMVoucherlines.FirstOrDefault(ab => ab.BOMVoucherid == bomidnumber);
                    if (final == null) return Json(new { success = false, msg = "BOM Head not found" });

                    // 2. Check if the item is Lot-Tracked (Assuming a flag in your Item Master)
                    var itemMaster = DB.BOMItemMasters.FirstOrDefault(x => x.Itemid == itemid);
                    bool isLotItem = itemMaster?.FifoLot ?? false; // Replace with your actual column name

                    decimal remainingToAllocate = quantityRequired;

                    if (isLotItem)
                    {
                        // FIFO: Get available lots for this item
                        var availableLots = DB.Stock_lotDetail
                            .Where(x => x.ItemID == itemid && x.CompanyID == companyid && x.CurrentQuantity > 0 && x.IsDeleted == false && x.IsReserved == false)
                            .OrderBy(x => x.id) // FIFO logic
                            .ToList();

                        foreach (var lot in availableLots)
                        {
                            if (remainingToAllocate <= 0) break;

                            // Use the available quantity (which is 1 in your serialized case)
                            decimal allocateFromThisLot = Math.Min(lot.CurrentQuantity ?? 0, remainingToAllocate);

                            if (allocateFromThisLot > 0)
                            {
                                // Create a separate line for each Lot allocated
                                BOMVoucherline BVL = new BOMVoucherline();
                                BVL.BOMVoucherid = bomidnumber;
                                BVL.Categoryid = category;
                                BVL.Subcategoryid = subcategory;
                                BVL.Finalitemid = (int)final.Finalitemid; // Cast to int
                                BVL.rawitemid = itemid;
                                BVL.LotID = lot.id;
                                BVL.QuantityRequired = allocateFromThisLot;
                                BVL.ApprovedQuantity = allocateFromThisLot;
                                BVL.UOM = uom;
                                BVL.Stockapproved = 1;
                                BVL.Approvedby = userName;
                                BVL.Approveddate = System.DateTime.Now;
                                BVL.Isdeleted = 0;
                                DB.BOMVoucherlines.Add(BVL);

                                // Update Lot Table
                                lot.CurrentQuantity -= allocateFromThisLot;

                                // FIX: Use '=' for assignment, not '=='
                                // Since your case is Qty 1, if current quantity is 0, it is no longer available
                                if (lot.CurrentQuantity <= 0)
                                {
                                    lot.IsAvailable = false;
                                }

                                remainingToAllocate -= allocateFromThisLot;
                            }
                        }

                        if (remainingToAllocate > 0)
                        {
                            return Json(new { success = false, msg = "Insufficient Lot Stock. Shortfall: " + remainingToAllocate });
                        }
                    }
                    else
                    {
                        // Non-Lot Item: standard behavior
                        BOMVoucherline BVL = new BOMVoucherline();
                        BVL.BOMVoucherid = bomidnumber;
                        BVL.Categoryid = category;
                        BVL.Subcategoryid = subcategory;
                        BVL.Finalitemid = final.Finalitemid;
                        BVL.rawitemid = itemid;
                        BVL.QuantityRequired = quantityRequired;
                        BVL.ApprovedQuantity = quantityRequired;
                        BVL.UOM = uom;
                        BVL.Stockapproved = 0;
                        BVL.Approvedby = userName;
                        BVL.Approveddate = System.DateTime.Now;
                        BVL.Isdeleted = 0;
                        DB.BOMVoucherlines.Add(BVL);
                    }

                    // 3. Update General Stock Table
                    var stock = DB.StockTables.SingleOrDefault(bb => bb.itemid == itemid && bb.CompanyID == companyid);
                    if (stock != null)
                    {
                        stock.Stock -= quantityRequired;
                    }

                    DB.SaveChanges();
                    dbContextTransaction.Commit();

                    return Json(new { success = true, msg = "Item added and lots allocated successfully" });
                }
                catch (Exception ex)
                {
                    dbContextTransaction.Rollback();
                    return Json(new { success = false, msg = "Error: " + ex.Message });
                }
            }
        }
        public ActionResult AddBomAlterationItemsold(int bomidnumber, int itemid, string uom, int category, int subcategory, decimal quantityRequired)
        {
            var FinYear = Session["Fin_Year"].ToString();
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());

            //get Final Itemid
            var final = (from ab in DB.BOMVoucherlines
                         where ab.BOMVoucherid == bomidnumber
                         select ab).FirstOrDefault();
            //add line to the Bom Voucher Lines Table


            BOMVoucherline BVL = new BOMVoucherline();
            BVL.BOMVoucherid = bomidnumber;
            BVL.Categoryid = category;
            BVL.Subcategoryid = subcategory;
            BVL.Finalitemid = (int)final.Finalitemid;
            BVL.rawitemid = itemid;
            BVL.QuantityRequired = quantityRequired;
            BVL.ApprovedQuantity = quantityRequired;
            BVL.UOM = uom;
            BVL.Stockapproved = 0;
            BVL.Approvedby = Session["U_Name"].ToString();
            BVL.Approveddate = System.DateTime.Now;
            BVL.Isdeleted = 0;
            DB.BOMVoucherlines.Add(BVL);
            //DB.SaveChanges();

            //Update Stock
            var stock = (from bb in DB.StockTables
                         where bb.itemid == itemid && bb.CompanyID == companyid
                         select bb).SingleOrDefault();
            stock.Stock -= quantityRequired;
            DB.SaveChanges();

            return Json(new { success = true, msg = "item Has Been Added" }, JsonRequestBehavior.AllowGet);


        }
        public ActionResult GetBOMAlterationItems(int bomidnumber)
        {
            var head = (from ab in DB.BOMVouchers
                        join item in DB.BOMItemMasters on ab.FGProductID equals item.Itemid into itemgrp
                        from item in itemgrp.DefaultIfEmpty()
                        where ab.BOMVoucherID == bomidnumber
                        select new
                        {
                            fgcode = item.ItemCode,
                            fgdesc = item.ItemName,

                        }).SingleOrDefault();
            var items = (from ab in DB.BOMVoucherlines
                        join im in DB.BOMItemMasters on ab.rawitemid equals im.Itemid
                        join cat in DB.BOMCategories on ab.Categoryid equals cat.CategoryID
                        join sub in DB.BOMSubcategories on ab.Subcategoryid equals sub.id
                        where ab.BOMVoucherid == bomidnumber && ab.Isdeleted==0
                        orderby cat.CategoryDesc
                        select new
                        {
                            Lineid = ab.id,
                            RItemCode=im.ItemCode,
                            ItemName = im.ItemName,
                            Category = cat.CategoryDesc,
                            SubCategory = sub.BOMSubCategory1,
                            Quantity = ab.ApprovedQuantity,
                            UOM = ab.UOM,
                            ApprovedBy = ab.Approvedby,
                            ApprovedDate = ab.Approveddate,

                        }).ToList();
            var finalitem = DB.BOMVoucherlines.Where(x => x.BOMVoucherid == bomidnumber).FirstOrDefault();
            var finalitemdesc = DB.BOMItemMasters.Where(y => y.Itemid == finalitem.Finalitemid).Select(y => y.ItemName).SingleOrDefault();
            return Json(new { success = true, items, head, finalitemdesc }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetReturnVoucherNumber(int Returnid)
        {
            var voucher = DB.DispatchReturns.Where(x => x.id == Returnid).SingleOrDefault();
            var itemcode = DB.BOMItemMasters.Where(y => y.Itemid == voucher.FGProductID).SingleOrDefault();
            var masterproduct = DB.BOMFinalProductCombinations.Where(z => z.ItemMasterID == voucher.FGProductID).SingleOrDefault();
            var masteritemname = DB.BOMItemMasters.Where(a => a.Itemid == masterproduct.MasterProduct).SingleOrDefault();

            return Json(new {success=true,data=voucher.VoucherNumber, itemcode=itemcode.ItemCode, desc=itemcode.ItemName, fgitemid=voucher.FGProductID, series=masteritemname.ItemName, masteritemid=masterproduct.MasterProduct},JsonRequestBehavior.AllowGet);
        }
        public ActionResult SaveReturnBOM(BOMReturnHead headData, List<BOMReturnLine> lineData)
        {
            try
            {
                var FinYear = Session["Fin_Year"].ToString();
                int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
                if (headData != null)
                {
                    using (var db = new IECEntities()) // Replace YourDbContext with your actual context
                    {
                        var dispatchReturnHead = new DispatchReturnBOMHead
                        {
                            CreatedBy = Session["U_Name"]?.ToString(), // Null-conditional operator
                            VoucherDate = System.DateTime.Now,
                            FGItemID = headData.fgitemid,
                            DispatchReturnID = headData.returnid,
                            HeadApproalStatus = 0,
                            BOMID = 0,
                            IsDeleted = 0,
                            CompanyID=companyid,
                            FinYear=FinYear,


                        };

                        db.DispatchReturnBOMHeads.Add(dispatchReturnHead);

                        var dispatchReturnTable = db.DispatchReturns.SingleOrDefault(x => x.id == headData.returnid);
                        if (dispatchReturnTable != null)
                        {
                            dispatchReturnTable.ProductionStatus = 1;
                            dispatchReturnTable.ProductionDate = System.DateTime.Now;
                        }
                        //Save Head Data to get Head id for Lines Table
                        db.SaveChanges();

                        if (lineData != null && lineData.Count > 0)
                        {
                            foreach (var line in lineData)
                            {
                                var dispatchReturnLine = new DispatchReturnBOMLine
                                {
                                    MasterItemID = headData.masteritemid, // Corrected to fgitemid.
                                    RawitemID = line.itemid,
                                    CategoryID = line.categoryid,
                                    SubcategoryID = line.subcategoryid,
                                    Quantity = line.quantityrequired,
                                    IsDeleted = 0,
                                    HeadID=dispatchReturnHead.id,
                                };

                                db.DispatchReturnBOMLines.Add(dispatchReturnLine);
                            }
                        }
                        //Now Get BOM Series to Alot
                        var billseries = db.Bill_Series.Where(j => j.Type == "DispatchReturnBOM" && j.Fin_Year==FinYear && j.CompanyID==companyid).SingleOrDefault();
                        dispatchReturnHead.VoucherNumber = billseries.Series + billseries.Number;
                        //now update Bill Series
                        billseries.Number += 1;

                        db.SaveChanges(); // Save changes to the database

                        return Json(new { success = true, vouchernumber= dispatchReturnHead.VoucherNumber }, JsonRequestBehavior.AllowGet);
                    }
                }
                else
                {
                    return Json(new { success = false, message = "Head data is null" }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
               
                return Json(new { success = false, message = "An error occurred: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult BOM()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "BOM" && ab.Status == true
                               select ab).SingleOrDefault();
            if (checkaccess == null)
            {
                // 3. Store error message in TempData (survives one redirect)
                TempData["ErrorMessage"] = "Access Denied: You are not authorized to view This Page";

                // 4. Redirect to Home Controller, Index Action
                return RedirectToAction("Dashboard", "Dashboard");
            }
            //var categories = DB.BOMCategories.ToList();
            return View();
            //return View();
        }
        public ActionResult ProductionJob()
        {
            return View();
        }
        public JsonResult GetFinalItems()
        {
            var getData = (from ab in DB.BOMItemMasters
                           where ab.ItemCategory == 1
                           select new SelectListItem { Value = ab.Itemid.ToString(), Text = ab.ItemName }).ToList();

            return Json(getData, JsonRequestBehavior.AllowGet);
        }
        public JsonResult GetFinalItemsBOM()
        {
            var getData = (from ab in DB.BOMItemMasters
                           where ab.ItemCategory == 1
                           select new { Value = ab.Itemid, Text = ab.ItemName }).ToList();

            return Json(getData, JsonRequestBehavior.AllowGet);
        }
        [HttpGet]
        public JsonResult GetBOMStages()
        {
            try
            {
                if (Session["Company_ID"] == null) return Json(null, JsonRequestBehavior.AllowGet);
                int companyId = Convert.ToInt32(Session["Company_ID"]);

                var stages = (from dm in DB.DepartmentMasters
                              where dm.CompanyID == companyId && dm.isBOMStage == true
                              orderby dm.OrderSequence
                              select new { Value = dm.id, Text = dm.DepartmentName }).ToList();

                return Json(stages, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(null, JsonRequestBehavior.AllowGet);
            }
        }

        public JsonResult GetItemTypes()
        {
            var getvalue = (from ab in DB.BOMCategories
                            where ab.Type==1
                            select new { Value = ab.CategoryID, Text = ab.CategoryDesc }).ToList();
            return Json(getvalue, JsonRequestBehavior.AllowGet);
        }
        public JsonResult Getclass()
        {
            var getvalue = (from ab in DB.ItemMasterClasses
                           // where ab.CategoryID == 1 || ab.CategoryID == 2
                            select new { Value = ab.id, Text = ab.Class}).ToList();
            return Json(getvalue, JsonRequestBehavior.AllowGet);
        }
        public JsonResult Getlocation()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var getvalue = (from ab in DB.StoreLocationMasters
                            where ab.Companyid==companyid
                            select new { Value = ab.id, Text = ab.LocationName }).ToList();
            return Json(getvalue, JsonRequestBehavior.AllowGet);
        }
        public JsonResult Getgroup()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var getvalue = (from ab in DB.ItemGroups
                            where ab.Companyid==companyid
                            orderby ab.GroupName
                                // where ab.CategoryID == 1 || ab.CategoryID == 2
                            select new { Value = ab.id, Text = ab.GroupName }).ToList();
            return Json(getvalue, JsonRequestBehavior.AllowGet);
        }
        public JsonResult GetUOMs()
        {
            var getvalue = (from ab in DB.BOM_UOM
                                // where ab.CategoryID == 1 || ab.CategoryID == 2
                            select new { Value = ab.id, Text = ab.UOM }).ToList();
            return Json(getvalue, JsonRequestBehavior.AllowGet);
        }
        public JsonResult GetUOMByRawItemId(int rawItemId)
        {
            var getValue = (from ab in DB.BOMItemMasters
                            join u in DB.BOM_UOM on ab.UOM equals u.id
                            where ab.Itemid == rawItemId
                            select new
                            {
                                Value = u.id,
                                Text = u.UOM
                            }).FirstOrDefault();

            if (getValue != null)
            {
                return Json(getValue, JsonRequestBehavior.AllowGet);
            }
            else
            {
                return Json(new { Error = "UOM not Found Against This Item - Please Check Item Master Data" }, JsonRequestBehavior.AllowGet);
            }
        }

        public ActionResult BOMChangeRequisition()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "BOMChangeRequisition" && ab.Status == true
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
        public ActionResult BOMRequisitionApproval()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "BOMRequisitionApproval" && ab.Status == true
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
        public JsonResult GetBOMVouchers()
        {
            var vouchers = DB.BOMVouchers
               // .Where(v => v.Ewap == 0) // Filter for vouchers with pending EWAP
               .Where (v=> v.ProductionCompletionStatus==0 && v.Isdeleted==0)
                .Select(v => new
                {
                    value = v.BOMVoucherID,
                    text = v.VoucherNumber
                })
                .ToList();

            return Json(new { BOMVoucher = vouchers }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult BOMChangeRequisitionAll()
        { return View(); }
        public JsonResult GetBOMVouchersallstages()
        {
            
            var vouchers = DB.BOMVouchers                
                //.Where(v => v.Ewap == 0) // Filter for vouchers with pending EWAP
                .Select(v => new
                {
                    value = v.BOMVoucherID,
                    text = v.VoucherNumber
                })
                .ToList();

            return Json(new { BOMVoucher = vouchers }, JsonRequestBehavior.AllowGet);
        }
        public JsonResult GetVoucherLineData(int voucherId)
        {
            var lines = DB.BOMVoucherlines
                .Where(vl => vl.BOMVoucherid == voucherId && vl.Isdeleted == 0)
                .GroupBy(vl => vl.Subcategoryid)
                .Select(group => new
                {
                    SubcategoryId = group.Key,
                    SubcategoryName = DB.BOMSubcategories  // Access the BOMSubcategory table
                        .Where(sc => sc.id == group.Key)
                        .Select(sc => sc.BOMSubCategory1)
                        .FirstOrDefault(),
                    LineItems = group.Select(vl => new
                    {
                        vl.id,
                        vl.Categoryid,
                        vl.Finalitemid,
                        vl.rawitemid,
                        vl.QuantityRequired,
                        vl.UOM,
                        ItemName = DB.BOMItemMasters  // Access BOMItemMaster table
                        .Where(im => im.Itemid == vl.rawitemid)
                        .Select(im => im.ItemName)
                        .FirstOrDefault()
                        // ... other properties you need
                    })
                })
                .ToList();

            return Json(lines, JsonRequestBehavior.AllowGet);
        }
        public ActionResult FinalProduct(int id) // Assuming 'id' is the Finalitemid
        {
            // 1. Get the Final Product details (replace with your actual data access logic)
            var finalProduct = DB.BOMItemMasters.Find(id); // Or however you fetch from your database

            if (finalProduct != null)
            {
                // 2. Create a SelectListItem for the dropdown
                var finalItem = new SelectListItem
                {
                    Value = finalProduct.Itemid.ToString(), // Assuming 'Id' is the primary key
                    Text = finalProduct.ItemName // Assuming 'Name' is the property for the product name
                };

                // 3. Return the SelectListItem as JSON
                return Json(finalItem, JsonRequestBehavior.AllowGet);
            }
            else
            {
                // Handle case where the product is not found
                return Json(null, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpPost] // Make sure to add the [HttpPost] attribute
        public JsonResult SaveBOMRequsition(int BOMid, int Voucherid,string finalItem, int alternator, int panel, int dgassy, int baseframe, int canopy, int fueltank, int assembly, int acoustictreatment, int exhaustsystem, int final, int electrical, string FinalProductCode)
        {
            try
            {
                var FinYear = Session["Fin_Year"].ToString();
                int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
                //int companyid = (int)Session["Company_ID"];
                //Pick Details of Voucher id
                var voucherdetail = (from ab in DB.BOMVouchers
                                     where ab.BOMVoucherID == Voucherid
                                     select ab).SingleOrDefault();
                var finalproductid = (from pp in DB.BOMItemMasters
                                      where pp.ItemCode == FinalProductCode
                                      select pp).SingleOrDefault();
                //Get Requisition Number
                var Requistionno = (from rn in DB.Bill_Series
                                    where rn.Type == "BOM Requisition" && rn.CompanyID == companyid && rn.Fin_Year==FinYear
                                    select rn).SingleOrDefault();
                // Check for Duplicate entry
                var check = (from nn in DB.BOMRequisitionHeads
                             where nn.BOMVoucherID == Voucherid && nn.CompanyID == companyid && nn.Fin_Year == FinYear && nn.IsDeleted==0
                             select nn).Any();

                if (check ==true)
                {
                    // Entry already exists, throw an error 
                    return Json(new { success = false, msg = "Case Already in Line" });

                    // Or you can return an error response if you're in a controller action
                    // return Json(new { success = false, error = "Entry already exists" });
                }

                if (voucherdetail != null)
                {
                    BOMRequisitionHead BRH = new BOMRequisitionHead();
                    // Save Entries in BOM Requisition Head
                    BRH.OldFGProductID = voucherdetail.FGProductID;
                    BRH.NewFGProductID = finalproductid.Itemid;
                    BRH.BOMVoucherID = Voucherid;
                    BRH.Createdby = Session["U_Name"].ToString();
                    BRH.Createdon = System.DateTime.Now;
                    BRH.IsApproved = 0;
                    BRH.IsDeleted = 0;
                    BRH.CompanyID = companyid;
                    BRH.Fin_Year = FinYear;
                    BRH.BOMReqeusitionNo = Requistionno.Series + Requistionno.Number;
                    DB.BOMRequisitionHeads.Add(BRH);
                    DB.SaveChanges();

                    //Now update Bill Series
                    Requistionno.Number += 1;
                    DB.SaveChanges();
                    //Now time to insert BomRequisition lines
                    var getitemid = (from ab in DB.BOMItemMasters
                                     where ab.ItemName == finalItem
                                     select ab.Itemid).SingleOrDefault();
                    var BOM = (from bc in DB.BOMs
                               where bc.MasterItem == getitemid && (bc.Parentsub == alternator || bc.Parentsub == panel || bc.Parentsub == dgassy || bc.Parentsub == baseframe || bc.Parentsub == canopy || bc.Parentsub == fueltank || bc.Parentsub == assembly || bc.Parentsub == acoustictreatment || bc.Parentsub == exhaustsystem || bc.Parentsub == final || bc.Parentsub == electrical)
                               join raw in DB.BOMItemMasters on bc.RawItem equals raw.Itemid
                               join master in DB.BOMItemMasters on bc.MasterItem equals master.Itemid
                               join cat in DB.BOMCategories on bc.ParentItem equals cat.CategoryID
                               join subcat in DB.BOMSubcategories on bc.Parentsub equals subcat.id
                               select new BOMItem
                               {
                                   FinalItem = (master.ItemName),
                                   RawItemName = raw.ItemName,
                                   RawItemID = raw.Itemid,
                                   ItemCode = raw.ItemCode,
                                   Category = cat.CategoryDesc,
                                   CategoryID = cat.CategoryID,
                                   subCategory = subcat.BOMSubCategory1,
                                   SubCategoryID = subcat.id,
                                   Quantity = (bc.Quantity ?? 0),
                                   Stock = (raw.Stock ?? 0),
                                   UOM = bc.UOM
                               }).ToList();

                    
                    foreach (var items in BOM)
                    {
                        BOMRequisitionLine BRL = new BOMRequisitionLine();
                        BRL.BomRequisitionHeadid = BRH.id;
                        BRL.BomVoucherHeadid = Voucherid;
                        BRL.Categoryid = items.CategoryID;
                        BRL.Subcategoryid = items.SubCategoryID;
                        BRL.FinalItemID = getitemid;
                        BRL.RawItemID = items.RawItemID;
                        BRL.Quantity = items.Quantity;
                        BRL.UOM = items.UOM;
                        BRL.IsDeleted = 0;
                        DB.BOMRequisitionLines.Add(BRL);
                    }                    
                    DB.SaveChanges();

                    var getSavedno = BRH.BOMReqeusitionNo;

                    return Json(new { success = true, msg = getSavedno }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = false, msg = "Error Saving Voucher Detail"  });

                }

            }
            catch (Exception ex)
            {
                // 4. Handle errors and return an error response
                return Json(new { success = false, error = "An error occurred: " + ex.Message });
            }
        }
        public JsonResult SaveBOMRequsitionUpdated(int BOMid, int Voucherid, string finalItem, int alternator, int panel, int dgassy, int baseframe, int canopy, int fueltank, int assembly, int acoustictreatment, int exhaustsystem, int final, int electrical, string FinalProductCode)
        {
            try
            {
                // check wheather it is return bom or not
                
                if (DB.DispatchReturnBOMHeads.Any(ab => ab.BOMID == BOMid))
                {
                    return Json(new { success = false, msg = "Cannot Proceed With BOM Requisition as It is a Return BOM.." }, JsonRequestBehavior.AllowGet);
                }
                var FinYear = Session["Fin_Year"].ToString();
                int companyid = Convert.ToInt32(Session["Company_ID"].ToString());

                var voucherdetail = (from ab in DB.BOMVouchers
                                     where ab.BOMVoucherID == Voucherid
                                     select ab).SingleOrDefault();
                var finalproductid = (from pp in DB.BOMItemMasters
                                      where pp.ItemCode == FinalProductCode
                                      select pp).SingleOrDefault();

                // 1. Get list of all records with BOMid
                var oldBOMLines = DB.BOMVoucherlines
                    .Where(brl => brl.BOMVoucherid == Voucherid && brl.Isdeleted== 0)
                    .ToList();

                var masteritemid = (from bb in DB.BOMVoucherlines
                                    where bb.BOMVoucherid == Voucherid && bb.Isdeleted == 0
                                    select bb.Finalitemid).FirstOrDefault();
                // 2. Create new list with new combination
                var getitemid = (from ab in DB.BOMItemMasters
                                 where ab.ItemCode == FinalProductCode
                                 select ab.Itemid).SingleOrDefault();
                var Requistionno = (from rn in DB.Bill_Series
                                    where rn.Type == "BOM Requisition" && rn.CompanyID == companyid && rn.Fin_Year==FinYear
                                    select rn).SingleOrDefault();
                // Check for Duplicate entry
                var check = (from nn in DB.BOMRequisitionHeads
                             where nn.BOMVoucherID == Voucherid && nn.CompanyID == companyid && nn.Fin_Year == FinYear && nn.IsDeleted == 0 && nn.IsApproved==0
                             select nn).Any();
                if (check == true)
                {
                    // Entry already exists, throw an error 
                    return Json(new { success = false, msg = "Case Already in Line" });

                    // Or you can return an error response if you're in a controller action
                    // return Json(new { success = false, error = "Entry already exists" });
                }

                var newBOMLines = (from bc in DB.BOMs
                           where bc.MasterItem == masteritemid && (bc.Parentsub == alternator ||
                           bc.Parentsub == panel || bc.Parentsub == dgassy || bc.Parentsub == baseframe ||
                           bc.Parentsub == canopy || bc.Parentsub == fueltank || bc.Parentsub == assembly ||
                           bc.Parentsub == acoustictreatment || bc.Parentsub == exhaustsystem || bc.Parentsub == final ||
                           bc.Parentsub == electrical)
                           join raw in DB.BOMItemMasters on bc.RawItem equals raw.Itemid
                           //join master in DB.BOMItemMasters on bc.MasterItem equals master.Itemid
                           join cat in DB.BOMCategories on bc.ParentItem equals cat.CategoryID
                           join subcat in DB.BOMSubcategories on bc.Parentsub equals subcat.id
                           select new 
                           {
                              //final = getitemid,
                               finalitemid=getitemid,                               
                               RawItemID = raw.Itemid,
                               CategoryID = cat.CategoryID,                               
                               SubCategoryID = subcat.id,
                               Quantity = (bc.Quantity ?? 0),                               
                               UOM = bc.UOM
                           }).ToList();
                
                // 3. Compare and identify entries to remove
                var toRemove = oldBOMLines.Where(oldLine =>
                    !newBOMLines.Any(newLine =>
                        newLine.CategoryID == oldLine.Categoryid &&
                        newLine.SubCategoryID == oldLine.Subcategoryid &&
                        //newLine.FinalItem == oldLine.Finalitemid &&
                        newLine.RawItemID == oldLine.rawitemid &&
                        newLine.Quantity == oldLine.ApprovedQuantity &&
                        newLine.UOM == oldLine.UOM
                    )).ToList();

                // 4. Compare and identify entries to add
                var toAdd = newBOMLines.Where(newLine =>
                    !oldBOMLines.Any(oldLine =>
                        newLine.CategoryID == oldLine.Categoryid &&
                        newLine.SubCategoryID == oldLine.Subcategoryid &&
                       // newLine.FinalItemID == oldLine.Finalitemid &&
                        newLine.RawItemID == oldLine.rawitemid &&
                        newLine.Quantity == oldLine.ApprovedQuantity &&
                        newLine.UOM == oldLine.UOM
                    )).ToList();

                // ... (Your existing code to handle check and voucherdetail) ...
                if(toRemove !=null || toAdd !=null)
                {
                    //Create Requition Head ID
                    BOMRequisitionHead BRH = new BOMRequisitionHead();
                    // Save Entries in BOM Requisition Head
                    BRH.OldFGProductID = voucherdetail.FGProductID;
                    BRH.NewFGProductID = finalproductid.Itemid;
                    BRH.BOMVoucherID = Voucherid;
                    BRH.Createdby = Session["U_Name"].ToString();
                    BRH.Createdon = System.DateTime.Now;
                    BRH.IsApproved = 0;
                    BRH.IsDeleted = 0;
                    BRH.CompanyID = companyid;
                    BRH.Fin_Year = FinYear;
                    BRH.BOMReqeusitionNo = Requistionno.Series + Requistionno.Number;
                    DB.BOMRequisitionHeads.Add(BRH);
                    DB.SaveChanges();

                    //Now update Bill Series
                    Requistionno.Number += 1;
                    DB.SaveChanges();

                    if (toRemove != null)
                    {
                        // Remove old entries
                        foreach (var item in toRemove)
                        {

                            BOMRequisitionLine BRL = new BOMRequisitionLine();
                            BRL.BomRequisitionHeadid = BRH.id;
                            BRL.BomVoucherHeadid = Voucherid;
                            BRL.Categoryid = item.Categoryid;
                            BRL.Subcategoryid = item.Subcategoryid;
                            BRL.FinalItemID = masteritemid;
                            BRL.RawItemID = item.rawitemid;
                            BRL.Quantity = item.ApprovedQuantity;
                            BRL.UOM = item.UOM;
                            BRL.IsDeleted = 0;
                            BRL.Action = "Delete";
                            DB.BOMRequisitionLines.Add(BRL);
                            DB.SaveChanges();
                           
                        }
                    }
                    if (toAdd != null)
                    {

                        foreach (var item in toAdd)
                        {
                            BOMRequisitionLine BRL = new BOMRequisitionLine();
                            BRL.BomRequisitionHeadid = BRH.id;
                            BRL.BomVoucherHeadid = Voucherid;
                            BRL.Categoryid = item.CategoryID;
                            BRL.Subcategoryid = item.SubCategoryID;
                            BRL.FinalItemID = masteritemid;
                            BRL.RawItemID = item.RawItemID;
                            BRL.Quantity = item.Quantity;
                            BRL.UOM = item.UOM;
                            BRL.IsDeleted = 0;
                            BRL.Action = "Add";
                            DB.BOMRequisitionLines.Add(BRL);
                            DB.SaveChanges();

                        }
                    }

                }

                DB.SaveChanges();
                //Send Auto Mail To Plant Head 




                return Json(new {success=true, msg="Updation Done.."  },JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {

                return Json("Exception..");// ... (Your existing code to handle exception) ...
            }
        }

        public JsonResult GetAllSubcategories()
        {
            var subcategories = DB.BOMSubcategories
            .GroupBy(s => s.BOMCategoryID)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g)
            .ToList() // Execute the query and materialize the results
            .Select(s => new {
                value = s.id,
                Text = $"{DB.BOMCategories.Where(c => c.CategoryID == s.BOMCategoryID).Select(c => c.CategoryDesc).FirstOrDefault()} - {s.BOMSubCategory1}"
            })
            .ToList();

            return Json(subcategories, JsonRequestBehavior.AllowGet);
        }

        public ActionResult Downloaditemdrawing()
        {
            return View();
        }

        [HttpGet]
        public ActionResult DownloadDrawing(string itemCode)
        {
            try
            {
                // Retrieve the drawing data from the database
                using (var context = new IECEntities())
                {
                    var item = context.BOMItemMasters.FirstOrDefault(i => i.ItemCode == itemCode);

                    if (item == null || item.DrawingData == null)
                    {
                        // Handle the case where no drawing is found
                        return Json("Drawing not Available..");
                    }

                    // Set the content type to PDF and return the file
                    return File(item.DrawingData, "application/pdf", $"{itemCode}.pdf");
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., log the error)
                return Json("Error Downloading File");
            }
        }
        [HttpPost]
        
        public JsonResult SaveItem(string ItemName, string ItemDesc, int ItemType, string ItemCode, int MinStk, int MaxStk, int AvlStl, int Uom, int MOQ, string HSN, string DrawingData, int itemclass, int itemgroup, int itemlocation, bool enableLot)
        {
            int CompanyID = (int)Session["Company_ID"];
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Invalid model state" });
            }

            // Check if the item already exists with the same ItemName and ItemType
            var existingItem = DB.BOMItemMasters.FirstOrDefault(i => i.ItemCode == ItemCode && i.ItemCategory == ItemType);

            if (existingItem != null)
            {
                return Json(new { success = false, message = "Item Code already exists" });
            }

            // Save the item to the database
            BOMItemMaster BI = new BOMItemMaster();
            BI.ItemCode = ItemCode;
            BI.ItemName = ItemName;
            BI.Desc = ItemDesc;
            BI.HSNCode= HSN;
            BI.UOM = Uom;
            BI.ItemClass = itemclass;
            BI.ItemGroup = itemgroup;
            BI.Minstklvl = MinStk;
            BI.Maxstklvl = MaxStk;
            BI.Stock = AvlStl;
            BI.MinimumOrderQuantity = MOQ;
            BI.BasicPrice = 0;
            BI.RejectedStock = 0;
            BI.ItemCategory = ItemType;
            BI.ItemLocation=itemlocation;
            BI.ItemCreatedon = System.DateTime.Now;
            BI.Createdby = Session["U_Name"].ToString();
            BI.CompanyID = CompanyID;
            BI.FifoLot = enableLot;
            if (!string.IsNullOrEmpty(DrawingData))
            {
                // Extract the base64-encoded content from the data URL
                string base64Data = DrawingData.Split(',')[1];
                byte[] fileBytes = Convert.FromBase64String(base64Data);

                BI.DrawingData = fileBytes; // Save the file data to the database
            }
            DB.BOMItemMasters.Add(BI);
            DB.SaveChanges();

            //Save Item Detail in Stock Table to Update Stock on Transactions
            //Check Active Company in CompanyActiveFor Stock (Accordingly Create Product Lines For Companies)

           // var activecompanies = (from ab in DB.CompanyActiveForStocks
                                 //  where ab.Active == 1
                                 //  select ab).ToList();
            //foreach (var nn in activecompanies)
            //{
                StockTable ST = new StockTable();
                ST.CompanyID = CompanyID;
                ST.itemid = BI.Itemid;
                ST.Minstklvl = BI.Minstklvl;
                ST.Maxstklvl = BI.Maxstklvl;
                ST.Stock = BI.Stock;
                ST.MOQ = BI.MinimumOrderQuantity;
                ST.BasicPrice = 0;
                ST.RejectedStock = 0;
                ST.LastDiscount = 0;
                ST.LastSupplierid = 0;
                DB.StockTables.Add(ST);
                DB.SaveChanges();

           // }
           
            

            return Json(new { success = true, message = "Item Saved" });
        }

    
        public JsonResult CheckProductCombination(int finalItem, int canopy, int fueltank, int exhaustsystem,
                                         int acoustictreatment, int finalpacking, int assembly,
                                         int baseframe, int dgassy, int panel, int alternator, int electrical)
        {
            var existingProduct = DB.BOMFinalProductCombinations.FirstOrDefault(p =>
                p.MasterProduct == finalItem &&
                p.Canopy == canopy &&
                p.FuelTank == fueltank &&
                p.Exhaust == exhaustsystem &&
                p.AcousticTreatment == acoustictreatment &&
                p.FinalPacking == finalpacking &&
                p.Assembly == assembly &&
                p.BaseFrame == baseframe &&
                p.DGType == dgassy &&
                p.Panel == panel &&
                p.Alternator == alternator &&
                p.Electrical == electrical
            );

            if (existingProduct == null)
            {
                return Json(new { success = false, msg = "FG Item Does not Exist, Please Contact Stores for FG Item Creation" },JsonRequestBehavior.AllowGet);
            }
            else
            {
                int finalProductID = (int)existingProduct.ItemMasterID;

                var products = DB.BOMItemMasters
                .Where(ab => ab.Itemid == finalProductID)
                .Select(ab => new { ab.ItemCode, ab.ItemName }) // Select both ItemCode and ItemName
                .ToList();

                if (products.Count > 1)
                {
                    // Handle error: More than one item code found for the given ID
                    return Json(new
                    {
                        success = false,
                        msg = "Multiple item codes found for this product combination."
                    }, JsonRequestBehavior.AllowGet);
                }
                else if (products.Count == 1)
                {
                    var product = products.First(); // Get the first (and only) product
                    return Json(new
                    {
                        success = true,
                        msg = "FG Item Exist with Item Code - " + product.ItemCode,
                        itemcode = product.ItemCode,
                        itemname = product.ItemName,
                    }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    // Handle error: No item code found for the given ID
                    return Json(new
                    {
                        success = false,
                        msg = "No item code found for this product combination."
                    }, JsonRequestBehavior.AllowGet);
                }
            }
        }

        [HttpPost]
      
        public ActionResult CreateBOM(string finalItem, int totalQuantity, int alternator, int panel, int dgassy, int baseframe, int canopy, int fueltank, int assembly, int acoustictreatment, int exhaustsystem,int final, int electrical)
        {
            //Get Id OF the Product from Table
            var getitemid = (from ab in DB.BOMItemMasters
                            where ab.ItemName == finalItem
                            select ab.Itemid).SingleOrDefault();
            int CompanyID = (int)Session["Company_ID"];
            //Select Items with relevant ID

            var BOM = (from bc in DB.BOMs
                           // where bc.MasterItem == getitemid && (bc.Parentsub == alternator || bc.Parentsub == panel || bc.Parentsub == dgassy || bc.Parentsub == baseframe || bc.Parentsub == canopy || bc.Parentsub == fueltank || bc.Parentsub == assembly || bc.Parentsub == acoustictreatment || bc.Parentsub == exhaustsystem || bc.Parentsub == final || bc.Parentsub == electrical)
                       where bc.MasterItem == getitemid && (bc.Parentsub == alternator || bc.Parentsub == panel || bc.Parentsub == dgassy || bc.Parentsub == baseframe || bc.Parentsub == canopy || bc.Parentsub == fueltank || bc.Parentsub == assembly || bc.Parentsub == acoustictreatment || bc.Parentsub == exhaustsystem || bc.Parentsub == final || bc.Parentsub == electrical)
                       // group by b
                       join raw in DB.BOMItemMasters on bc.RawItem equals raw.Itemid
                       join stk in DB.StockTables on bc.RawItem equals stk.itemid
                       join master in DB.BOMItemMasters on bc.MasterItem equals master.Itemid
                       join cat in DB.BOMCategories on bc.ParentItem equals cat.CategoryID
                       join subcat in DB.BOMSubcategories on bc.Parentsub equals subcat.id
                       where stk.CompanyID==CompanyID
                       select new BOMItem
                       {
                           //FinalItem = ((int)bc.MasterItem),
                           FinalItem = (master.ItemName),
                           RawItemName = raw.ItemName,
                           ItemCode = raw.ItemCode,
                           //Category = ((int)bc.ParentItem),
                           Category = cat.CategoryDesc,
                           subCategory = subcat.BOMSubCategory1,
                           Quantity = (bc.Quantity ?? 0) * totalQuantity,
                           //Stock = (raw.Stock ?? 0),
                           Stock = (stk.Stock ?? 0),
                           UOM = bc.UOM,

                       }).ToList();
            return PartialView("_BOMList", BOM);
            // return Json(BOM);
        }

        public ActionResult SaveBOMDataVouchers(string finalItem, int totalQuantity, int alternator, int panel, int dgassy, int baseframe, int canopy, int fueltank, int assembly, int acoustictreatment, int exhaustsystem, int final, int electrical, string FinalProductCode)
        {
            var FinYear = Session["Fin_Year"].ToString();
            int CompanyID = (int)Session["Company_ID"];

            //Get Final Product ID for Bom Vouchers
            var finalid = (from bb in DB.BOMItemMasters
                           where bb.ItemCode == FinalProductCode
                           select bb.Itemid).SingleOrDefault();
            // Get Id OF the Product from Table
            var getitemid = (from ab in DB.BOMItemMasters
                             where ab.ItemName == finalItem
                             select ab.Itemid).SingleOrDefault();

            // Get All Items of BOM 
            var BOM = (from bc in DB.BOMs
                       where bc.MasterItem == getitemid && (bc.Parentsub == alternator || bc.Parentsub == panel || bc.Parentsub == dgassy || bc.Parentsub == baseframe || bc.Parentsub == canopy || bc.Parentsub == fueltank || bc.Parentsub == assembly || bc.Parentsub == acoustictreatment || bc.Parentsub == exhaustsystem || bc.Parentsub == final || bc.Parentsub == electrical)
                       join raw in DB.BOMItemMasters on bc.RawItem equals raw.Itemid
                       join master in DB.BOMItemMasters on bc.MasterItem equals master.Itemid
                       join cat in DB.BOMCategories on bc.ParentItem equals cat.CategoryID
                       join subcat in DB.BOMSubcategories on bc.Parentsub equals subcat.id
                       select new BOMItem
                       {
                           FinalItem = (master.ItemName),
                           RawItemName = raw.ItemName,
                           RawItemID = raw.Itemid,
                           ItemCode = raw.ItemCode,
                           Category = cat.CategoryDesc,
                           CategoryID = cat.CategoryID,
                           subCategory = subcat.BOMSubCategory1,
                           SubCategoryID = subcat.id,
                           Quantity = (bc.Quantity ?? 0),
                           Stock = (raw.Stock ?? 0),
                           UOM = bc.UOM,
                           
                       }).ToList();

            // List to store voucher numbers
            var voucherNumbers = new List<string>();

            // Loop for BOM Save based on Quantity to create that number of vouchers
            for (int i = 0; i < totalQuantity; i++)
            {
                using (var transaction = DB.Database.BeginTransaction())
                {
                    try
                    {
                        // Save BOM Voucher Head first
                        BOMVoucher BV = new BOMVoucher();

                        // Get Bill Series
                        var GetNumber = (from ab in DB.Bill_Series
                                         where ab.Type == "BOM" && ab.CompanyID == CompanyID && ab.Fin_Year == FinYear
                                         select ab).SingleOrDefault();
                        //Get FG Product ID
                        var FGID = from jj in DB.BOMItemMasters
                                   where jj.ItemCode == FinalProductCode
                                   select jj.Itemid;
                        //  Generate VoucherNumber (adjust logic if needed)
                        var VoucherNumber = GetNumber.Series + GetNumber.Number;

                        BV.VoucherNumber = VoucherNumber;
                        BV.VoucherDate = System.DateTime.Now;
                        BV.Approvalstatus = 0;
                        BV.CreatedBy = Session["U_Name"].ToString();
                        BV.ApprovedBY = "NA";
                        BV.DispatchStatus = 0;
                        BV.PDIStatus = 0;
                        BV.FGProductID = finalid;
                        BV.Isdeleted = 0;
                        BV.Finyear = FinYear;
                        BV.CompanyID = CompanyID;
                        BV.AllowNegativeTransaction = false;
                        BV.AllowLotCheck = true;
                        DB.BOMVouchers.Add(BV);
                        DB.SaveChanges();

                        // Add voucher number to the list
                        voucherNumbers.Add(VoucherNumber);

                        // Save BOM Voucher Lines
                        foreach (var item in BOM)
                        {
                            BOMVoucherline BVL = new BOMVoucherline();
                            BVL.BOMVoucherid = BV.BOMVoucherID;
                            BVL.Categoryid = item.CategoryID;
                            BVL.Subcategoryid = item.SubCategoryID;
                            BVL.Finalitemid = getitemid;
                            BVL.rawitemid = item.RawItemID;
                            BVL.QuantityRequired = item.Quantity; // No need to multiply by totalQuantity
                            BVL.UOM = item.UOM;
                            BVL.Stockapproved = 0;
                            BVL.Isdeleted =0;
                            DB.BOMVoucherlines.Add(BVL);
                        }

                        DB.SaveChanges();

                        // Update BOM Series (Important: Update after successful save)
                        GetNumber.Number++;
                        DB.SaveChanges();

                        // Commit transaction
                        transaction.Commit();
                    }
                    catch (DbEntityValidationException ex)
                    {
                        // Log validation errors (see previous response for details)
                        // ... error logging code ...

                        // Rollback transaction on error
                        transaction.Rollback();
                        return Json(new { success = false, message = "Validation failed: " + ex.Message }, JsonRequestBehavior.AllowGet);
                    }
                    catch (Exception ex)
                    {
                        // Log other exceptions
                        // ... error logging code ...

                        transaction.Rollback();
                        return Json(new { success = false, message = "An error occurred: " + ex.Message }, JsonRequestBehavior.AllowGet);
                    }
                }
            }

            // Return the list of voucher numbers
            return Json(new { success = true, voucherNumbers = voucherNumbers }, JsonRequestBehavior.AllowGet);
        }

        //Working Transfer Excel Code to Generate and save file on drive
        [HttpPost]
        public ActionResult GenerateExcel(List<List<string>> data)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("BOM List");

                // Set header row
                worksheet.Cell(1, 1).Value = "Category";
                worksheet.Cell(1, 2).Value = "Sub Category";
                worksheet.Cell(1, 3).Value = "Item Code";
                worksheet.Cell(1, 4).Value = "Final Item";                
                worksheet.Cell(1, 5).Value = "Raw Item Name";
                worksheet.Cell(1, 6).Value = "Quantity";
                worksheet.Cell(1, 7).Value = "Stock";
                worksheet.Cell(1, 8).Value = "UOM";
                worksheet.Range(1, 1, 1, 8).Style.Font.Bold = true;

                // Add data to the worksheet
                int row = 2;
                foreach (var rowValues in data)
                {
                    for (int col = 1; col <= rowValues.Count; col++)
                    {
                        worksheet.Cell(row, col).Value = rowValues[col - 1];
                    }
                    row++;
                }

                string filePath = Server.MapPath("~/Files/BOM_List.xlsx");
                workbook.SaveAs(filePath);

                // Return the file path and allow download
                return new JsonResult { Data = new { filePath = "BOM_List.xlsx" } };
            }
        }
        public ActionResult DownloadExcel(byte[] fileBytes)
        {
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "BOM_List.xlsx");
        }

        public ActionResult ItemMaster()
        {
            //Check Authorization
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "ItemMaster" && ab.Status==true
                               select ab).SingleOrDefault();
           if (checkaccess == null)
            {
                // 3. Store error message in TempData (survives one redirect)
                TempData["ErrorMessage"] = "Access Denied: You are not authorized to view This Page";

                // 4. Redirect to Home Controller, Index Action
                return RedirectToAction("Dashboard", "Dashboard");
            }
            return View("BOMItemmaster", BOMItemmaster());
        }
     
        public class BOMItemMasterViewModel
        {
            public string Itemcode { get; set; }
            public string ItemName { get; set; }
            public string ItemDesc { get; set; }
            public string HSN { get; set; }
            public string UOM { get; set; }
            public string ItemType { get; set; }
            // Add all other properties used in the form here...
        }
    
    public ActionResult BOMItemmaster()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var itemmaster = (from ab in DB.BOMItemMasters
                              join raw in DB.BOMItemMasters on ab.Itemid equals raw.Itemid
                              where ab.ItemCategory == 2 && ab.CompanyID==companyid
                              orderby ab.ItemName
                              select new BOMItem
                              {
                                  ItemCode=raw.ItemCode,
                                  RawItemName = raw.ItemName,
                                  Desc = raw.Desc,
                                  //Quantity = (decimal)qty.Quantity,
                                  //UOM = qty.UOM,
                              }).ToList();
            return PartialView("_ItemMasterView", itemmaster);
        }
        public ActionResult BOMmainitems()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var itemmastermain = (from ab in DB.BOMItemMasters
                              join raw in DB.BOMItemMasters on ab.Itemid equals raw.Itemid
                              where ab.ItemCategory == 1 && ab.CompanyID==companyid
                              select new BOMItem
                              {
                                  ItemCode = raw.ItemCode,
                                  RawItemName = raw.ItemName,
                                  Desc = raw.Desc,
                                  //Quantity = (decimal)qty.Quantity,
                                  //UOM = qty.UOM,
                              }).ToList();
            return PartialView("_ItemMasterViewMain", itemmastermain);
        }
        public ActionResult dashboard()
        {
            return View();

        }
        public ActionResult BOMItemLink()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "BOMItemLink" && ab.Status == true
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
        public JsonResult linkmasterlist()
        {
            var masterlist = (from ab in DB.BOMItemMasters
                              where ab.ItemCategory == 1
                              orderby ab.ItemName
                              select new
                              {
                                  value = ab.Itemid,
                                  text = ab.ItemName
                              }).ToList();
            var categorylist= (from bb in DB.BOMCategories
                              where bb.CategoryID!=1 && bb.CategoryID!=2
                              select new
                              {
                                  value = bb.CategoryID,
                                  text = bb.CategoryDesc

                              }).ToList();
            var rawlist =(from cc in DB.BOMItemMasters
                          where cc.ItemCategory == 2
                          orderby cc.ItemCode
                          select new
                          { value = cc.Itemid,
                          text= cc.ItemCode + " --- " + cc.ItemName
                          }).ToList();
            var uomlist = (from dd in DB.BOM_UOM
                           select new
                           {
                               value = dd.id,
                               text = dd.UOM
                           }).ToList();

            return Json(new { masterlist, categorylist, rawlist,uomlist }, JsonRequestBehavior.AllowGet);
        }
        public JsonResult GetSubCategories(int categoryId)
        {
            var subcategory = (from ab in DB.BOMSubcategories
                              where ab.BOMCategoryID==categoryId
                              select new
                              {
                                  value=ab.id,
                                  text=ab.BOMSubCategory1

                              }).ToList();
            return Json(new { subcategory }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult savebomlink(int masterItem, int category, int rawItem, decimal quantity, string uom, int subcategory)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Json(new { success = false, message = "Invalid model state" });
                }
               // Check if the item already exists with the same ItemName and ItemType
                //There are chances that same item can be repeat in same category
                //Now Disabling the Same Item Check in same category due to battery use 
               // var existingItem = DB.BOMs.FirstOrDefault(i => i.MasterItem == masterItem && i.Parentsub == subcategory && i.RawItem == rawItem);

                //if (existingItem != null)
               // {
               //    return Json(new { success = false, message = "Item already exists" });
               // }
                BOM BM = new BOM();
                BM.MasterItem = masterItem;
                BM.ParentItem = category;
                BM.Parentsub=subcategory;
                BM.RawItem = rawItem;
                BM.Quantity = quantity;
                BM.UOM = uom;
                BM.CreatedBy = Session["U_Name"].ToString();
                BM.BOMCreated = System.DateTime.Now;
                DB.BOMs.Add(BM);
                DB.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }



        public ActionResult BOMPartialView(int masterItemId)
        {

            
            var mymodel = (from ab in DB.BOMs
                           where ab.MasterItem == masterItemId
                           join raw in DB.BOMItemMasters on ab.RawItem equals raw.Itemid
                           //join master in DB.BOMItemMasters on ab.MasterItem equals master.Itemid
                           join cat in DB.BOMCategories on ab.ParentItem equals cat.CategoryID
                           join dog in DB.BOMSubcategories on ab.Parentsub equals dog.id

                           select new BOMItem
                           {
                              // FinalItem = (master.ItemName),
                               id=ab.BOMid,
                               RawItemName = raw.ItemName,
                               ItemCode= raw.ItemCode,
                               //Category = ((int)bc.ParentItem),
                               Category = cat.CategoryDesc,                              
                               UOM = ab.UOM,
                               subCategory=dog.BOMSubCategory1,
                               Quantity= (decimal)(ab.Quantity),

                           }
                           
                           ).ToList();

            return PartialView("_BOMlinkPartialview", mymodel);
        }
        public JsonResult searchraw(string itemname)
        {
            var items = (from ab in DB.BOMItemMasters
                         where ab.ItemName.Contains(itemname)
                         select new
                         {
                             label = ab.ItemName, // Changed from 'text' to 'label'
                             value = ab.Itemid
                         }).ToList();
            return new JsonResult { Data = items, JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }
        public JsonResult BOMMasterDropdown()
        {
            var baseframe = (from ab in DB.BOMSubcategories
                             join bc in DB.BOMCategories on ab.BOMCategoryID equals bc.CategoryID
                             where bc.CategoryDesc == "Base Frame"
                             select new
                             {
                                 value = ab.id,
                                 text = ab.BOMSubCategory1
                             }).ToList();
            var DG = (from ab in DB.BOMSubcategories
                             join bc in DB.BOMCategories on ab.BOMCategoryID equals bc.CategoryID
                             where bc.CategoryDesc == "DG Type"
                             select new
                             {
                                 value = ab.id,
                                 text = ab.BOMSubCategory1
                             }).ToList();
            var panel = (from ab in DB.BOMSubcategories
                      join bc in DB.BOMCategories on ab.BOMCategoryID equals bc.CategoryID
                      where bc.CategoryDesc == "Panel"
                      select new
                      {
                          value = ab.id,
                          text = ab.BOMSubCategory1
                      }).ToList();
            var Alternator = (from ab in DB.BOMSubcategories
                      join bc in DB.BOMCategories on ab.BOMCategoryID equals bc.CategoryID
                      where bc.CategoryDesc == "Alternator"
                      select new
                      {
                          value = ab.id,
                          text = ab.BOMSubCategory1
                      }).ToList();
            var canopy = (from ab in DB.BOMSubcategories
                              join bc in DB.BOMCategories on ab.BOMCategoryID equals bc.CategoryID
                              where bc.CategoryDesc == "Canopy"
                              select new
                              {
                                  value = ab.id,
                                  text = ab.BOMSubCategory1
                              }).ToList();
            var fueltank = (from ab in DB.BOMSubcategories
                              join bc in DB.BOMCategories on ab.BOMCategoryID equals bc.CategoryID
                              where bc.CategoryDesc == "Fuel Tank"
                              select new
                              {
                                  value = ab.id,
                                  text = ab.BOMSubCategory1
                              }).ToList();
            var exhaust = (from ab in DB.BOMSubcategories
                              join bc in DB.BOMCategories on ab.BOMCategoryID equals bc.CategoryID
                              where bc.CategoryDesc == "Exhaust System"
                              select new
                              {
                                  value = ab.id,
                                  text = ab.BOMSubCategory1
                              }).ToList();
            var acoustic = (from ab in DB.BOMSubcategories
                              join bc in DB.BOMCategories on ab.BOMCategoryID equals bc.CategoryID
                              where bc.CategoryDesc == "Acoustic Treatment"
                              select new
                              {
                                  value = ab.id,
                                  text = ab.BOMSubCategory1
                              }).ToList();
            var assembly = (from ab in DB.BOMSubcategories
                              join bc in DB.BOMCategories on ab.BOMCategoryID equals bc.CategoryID
                              where bc.CategoryDesc == "Assembly"
                              select new
                              {
                                  value = ab.id,
                                  text = ab.BOMSubCategory1
                              }).ToList();
            var final = (from ab in DB.BOMSubcategories
                            join bc in DB.BOMCategories on ab.BOMCategoryID equals bc.CategoryID
                            where bc.CategoryDesc == "Final Packing"
                            select new
                            {
                                value = ab.id,
                                text = ab.BOMSubCategory1
                            }).ToList();
            var electrical = (from ab in DB.BOMSubcategories
                         join bc in DB.BOMCategories on ab.BOMCategoryID equals bc.CategoryID
                         where bc.CategoryDesc == "Electrical"
                         select new
                         {
                             value = ab.id,
                             text = ab.BOMSubCategory1
                         }).ToList();


            return Json(new { baseframe, DG, panel, Alternator,canopy,fueltank,exhaust,acoustic,assembly,final,electrical }, JsonRequestBehavior.AllowGet); 
        }
        
        public ActionResult BOMCategoryMaster()
        {
            return View("BOMCategoryMaster", _CategoryList());
        }
        public PartialViewResult _CategoryList()
        {
            var categories = DB.BOMCategories.Where(c => c.CategoryID != 1 && c.CategoryID != 2).ToList();
            return PartialView("_CategoryList", categories);
            //return View("BOMItemmaster", BOMItemmaster());
        }
        public JsonResult Categories(string category)
        {

            var options = from ab in DB.BOMCategories
                          join bc in DB.BOMSubcategories on ab.CategoryID equals bc.BOMCategoryID
                          where ab.CategoryID != 1 && ab.CategoryID != 2
                          select new
                          {
                              Value= bc.id,
                              Text= bc.BOMSubCategory1,
                              TextLabel= ab.CategoryDesc
                              
                          };
           // List<cate> options = DB.Options.Where(o => o.Category == category).ToList();
            return Json(options, JsonRequestBehavior.AllowGet);
        }
        
        [HttpPost]
        public JsonResult SaveBOMData(List<BOMItem> tableData)
        {
            // generate voucher number with current date and time
            var lastvoucherid = (from ab in DB.BOMVouchers
                                 select ab.BOMVoucherID).OrderByDescending(x => x).FirstOrDefault();
            //string Voucherdate = DateTime.Now();
            var Vonumber = "BOM-"+(lastvoucherid + 1);

            // save voucher number to first table
            BOMVoucher BV = new BOMVoucher();
            {
                BV.VoucherDate = System.DateTime.Now;
                BV.Approvalstatus = 0;
                //BV.ApprovedBY= Session["U_Name"].ToString();
                BV.CreatedBy= Session["U_Name"].ToString();
                BV.VoucherNumber = Vonumber;
                DB.BOMVouchers.Add(BV);
                DB.SaveChanges();
            }
           

            // save table data to second table with voucher id reference
            foreach (var data in tableData)
            {
                BOMVoucherline tableLine = new BOMVoucherline
                {
                    BOMVoucherid = lastvoucherid + 1,
                    Categoryid = GetCategoryId(data.Category),
                    Subcategoryid = GetSubCategoryId(data.subCategory),                    
                    rawitemid = GetRawItemCode(data.ItemCode),
                    Finalitemid= GetFinalItemid(data.FinalItem),
                    QuantityRequired = data.Quantity,
                    UOM = data.UOM,
                    Stockapproved = 0,
                    
                };
               DB.BOMVoucherlines.Add(tableLine);
            }
            DB.SaveChanges();


            return Json(new { success = true, voucherNumber = Vonumber });
        }
        private int GetCategoryId(string category)
        {
            var categoryid = (from ab in DB.BOMCategories
                             where ab.CategoryDesc == category
                             select ab.CategoryID).FirstOrDefault();
            // implement logic to get category id from database
            return categoryid;
        }

        private int GetSubCategoryId(string subCategory)
        {
            var subcategoryid = (from ab in DB.BOMSubcategories
                              where ab.BOMSubCategory1 == subCategory
                              select ab.id).FirstOrDefault();
            // implement logic to get category id from database
            return subcategoryid;
            // implement logic to get subcategory id from database
        }
        private int GetRawItemCode(string ItemCode)
        {
            var itemid = (from ab in DB.BOMItemMasters
                                 where ab.ItemCode == ItemCode
                                 select ab.Itemid).FirstOrDefault();
            // implement logic to get category id from database
            return itemid;
            // implement logic to get subcategory id from database
        }
        private int GetFinalItemid(string FinalItem)
        {
            var subcategoryid = (from ab in DB.BOMItemMasters
                                 where ab.ItemName == FinalItem
                                 select ab.Itemid).FirstOrDefault();
            // implement logic to get category id from database
            return subcategoryid;
            // implement logic to get subcategory id from database
        }
        // GET: GetVouchers
        public ActionResult ManagementApproval()
        {
            return View();
        }
        public ActionResult GetVouchers()
        {
            // Retrieve vouchers from database or data source
            
            var list = (from ab in DB.BOMVouchers
                       where ab.Approvalstatus==0 && ab.Isdeleted==0
                       select ab).ToList();

            return Json(list, JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetRequisitions()
        {
            // Retrieve vouchers from database or data source

            var list = (from ab in DB.BOMRequisitionHeads
                        join bb in DB.BOMVouchers on ab.BOMVoucherID equals bb.BOMVoucherID
                        join oim in DB.BOMItemMasters on ab.OldFGProductID equals oim.Itemid
                        join nim in DB.BOMItemMasters on ab.NewFGProductID equals nim.Itemid
                        where ab.IsApproved == 0
                        select new
                        {
                            id=ab.id,
                            BOMReqeusitionNo =ab.BOMReqeusitionNo,
                            BOMNumber=bb.VoucherNumber,
                            OldFG=oim.ItemCode,
                            NewFG=nim.ItemCode,
                            Createdon=ab.Createdon,
                            Createdby=ab.Createdby

                        }).ToList();

            return Json(list, JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetLines(int voucherId)
        {
            var viewModel = new List<BOMVoucherLine>();
            var lines = (from ab in DB.BOMVoucherlines
                         where ab.BOMVoucherid == voucherId && ab.Isdeleted==0
                         select ab).ToList();
            foreach (var line in lines)
            {
                viewModel.Add(new BOMVoucherLine
                {
                    CategoryName = GetCategoryName(line.Categoryid),
                    SubcategoryName = GetSubcategoryName(line.Subcategoryid),
                    FinalItemName = GetFinalItemName(line.Finalitemid),
                    RawItemcode = GetRawItemCode(line.rawitemid),
                    RawItemName = GetRawItemName(line.rawitemid),
                    AvailableStock = DB.StockTables.Where(x => x.CompanyID == 17 && x.itemid == line.rawitemid).Select(x => x.Stock).SingleOrDefault() ?? 0m,
                    QuantityRequired = ((decimal)line.QuantityRequired),
                    UOM = line.UOM,
                    //...
                });
            }
            viewModel = viewModel.OrderBy(v => v.CategoryName).ToList();
            return Json(viewModel, JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetRequisitionLines(int voucherId)
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var viewModel = new List<BOMVoucherLine>();
            var lines = (from ab in DB.BOMRequisitionLines
                         where ab.BomRequisitionHeadid == voucherId
                         select ab).ToList();
            foreach (var line in lines)
            {
                // Get the stock for the current RawItemID
                var stock = DB.StockTables.Where(s => s.itemid == line.RawItemID && s.CompanyID ==companyid ).Select(x=> x.Stock).SingleOrDefault();
                viewModel.Add(new BOMVoucherLine
                {
                    CategoryName = GetCategoryName(line.Categoryid),
                    SubcategoryName = GetSubcategoryName(line.Subcategoryid),
                    FinalItemName = GetFinalItemName(line.FinalItemID),
                    RawItemName = GetRawItemName(line.RawItemID),
                    QuantityRequired = ((decimal)line.Quantity),
                    UOM = line.UOM,
                    Action=line.Action,
                    Stock = (decimal)stock
                    //...
                });
            }
            return Json(viewModel, JsonRequestBehavior.AllowGet);
        }
        private string GetCategoryName(int? categoryId)
        {
            if (!categoryId.HasValue)
            {
                return string.Empty;
            }
            var category = DB.BOMCategories.FirstOrDefault(c => c.CategoryID == categoryId);
            return category != null ? category.CategoryDesc : string.Empty;
        }

        private string GetSubcategoryName(int? subcategoryId)
        {
            if (!subcategoryId.HasValue)
            {
                return string.Empty;
            }
            var subcategory = DB.BOMSubcategories.FirstOrDefault(sc => sc.id == subcategoryId);
            return subcategory != null ? subcategory.BOMSubCategory1 : string.Empty;
        }

        private string GetFinalItemName(int? finalItemId)
        {
            if (!finalItemId.HasValue)
            {
                return string.Empty;
            }
            var finalItem = DB.BOMItemMasters.FirstOrDefault(fi => fi.Itemid == finalItemId);
            return finalItem != null ? finalItem.ItemName : string.Empty;
        }

        private string GetRawItemName(int? rawItemId)
        {
            if (!rawItemId.HasValue)
            {
                return string.Empty;
            }
            var rawItem = DB.BOMItemMasters.FirstOrDefault(ri => ri.Itemid == rawItemId);
            return rawItem != null ? rawItem.ItemName : string.Empty;
        }
        private string GetRawItemCode(int? rawItemId)
        {
            if (!rawItemId.HasValue)
            {
                return string.Empty;
            }
            var rawItem = DB.BOMItemMasters.FirstOrDefault(ri => ri.Itemid == rawItemId);
            return rawItem != null ? rawItem.ItemCode : string.Empty;
        }
        public ActionResult ManualSerialSwap()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "ManualSerialSwap" && ab.Status == true
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

        /// <summary>
        /// Fetches available serial numbers (Lots) for a specific item, sorted by oldest first.
        /// </summary>
        [HttpGet]
        public JsonResult GetAvailableLotsForItem(int itemId)
        {
            try
            {
                if (Session["Company_ID"] == null)
                    return Json(new { success = false, message = "Session Expired" }, JsonRequestBehavior.AllowGet);

                int companyId = Convert.ToInt32(Session["Company_ID"]);

                // 1. Fetch raw data from DB first (SQL execution)
                var rawLots = DB.Stock_lotDetail
                    .Where(x => x.ItemID == itemId
                             && x.IsAvailable == true
                             && x.CompanyID == companyId
                             && x.IsDeleted == false
                             && x.CurrentQuantity > 0)
                    .OrderBy(x => x.RecieptDateTime)
                    .Select(x => new { x.id, x.Lot_SerialNumber, x.RecieptDateTime })
                    .ToList();

                // 2. Perform string formatting in-memory (C# execution)
                var lots = rawLots.Select(x => new {
                    Value = x.id,
                    Text = x.Lot_SerialNumber + " (Recv: " + (x.RecieptDateTime.HasValue ? x.RecieptDateTime.Value.ToString("dd/MM/yyyy") : "NA") + ")"
                }).ToList();

                return Json(new { success = true, data = lots }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// MANUALLY SWAP SERIAL: Deallocates the old and assigns the selected new serial.
        /// Synchronizes StockTables and Stock_lotDetail.
        /// </summary>
        [HttpPost]
        public JsonResult ProcessManualSwap(int lineId, int newLotId)
        {
            using (var transaction = DB.Database.BeginTransaction())
            {
                try
                {
                    int companyId = Convert.ToInt32(Session["Company_ID"]);
                    var line = DB.BOMVoucherlines.Find(lineId);
                    if (line == null) return Json(new { success = false, message = "BOM record not found." });

                    decimal qty = line.ApprovedQuantity ?? 1;

                    // 1. DEALLOCATE OLD (If assigned)
                    if (line.LotID != null)
                    {
                        var oldLot = DB.Stock_lotDetail.Find(line.LotID);
                        if (oldLot != null)
                        {
                            oldLot.CurrentQuantity += qty;
                            oldLot.IsAvailable = true;
                        }
                        var stockRestore = DB.StockTables.FirstOrDefault(s => s.itemid == line.rawitemid && s.CompanyID == companyId);
                        if (stockRestore != null) stockRestore.Stock += qty;
                    }

                    // 2. ALLOCATE NEW
                    var newLot = DB.Stock_lotDetail.Find(newLotId);
                    if (newLot == null || newLot.IsAvailable == false)
                        return Json(new { success = false, message = "Selected serial is no longer available." });

                    newLot.CurrentQuantity -= qty;
                    if (newLot.CurrentQuantity <= 0) newLot.IsAvailable = false;

                    var stockDeduct = DB.StockTables.FirstOrDefault(s => s.itemid == line.rawitemid && s.CompanyID == companyId);
                    if (stockDeduct != null) stockDeduct.Stock -= qty;

                    line.LotID = newLotId;

                    

                    DB.SaveChanges();
                    transaction.Commit();
                    return Json(new { success = true, message = "Serial swapped and stock synchronized." });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = ex.Message });
                }
            }
        }

        /// <summary>
        /// Retrieves records for the Swap Screen.
        /// </summary>
        [HttpGet]
        public JsonResult GetSerializedBOMRecords()
        {
            try
            {
                int companyId = Convert.ToInt32(Session["Company_ID"]);
                var data = (from line in DB.BOMVoucherlines
                            join head in DB.BOMVouchers on line.BOMVoucherid equals head.BOMVoucherID
                            join item in DB.BOMItemMasters on line.rawitemid equals item.Itemid
                            join lot in DB.Stock_lotDetail on line.LotID equals lot.id into lotgroup
                            from lot in lotgroup.DefaultIfEmpty()
                            where head.CompanyID == companyId && line.Isdeleted == 0 && line.LotID != null
                               && item.FifoLot == true && head.ProductionCompletionStatus == 0
                            orderby head.BOMVoucherID descending
                            select new
                            {
                                line.id,
                                head.VoucherNumber,
                                item.ItemName,
                                item.Itemid,
                                SerialNumber = lot.Lot_SerialNumber,// lot != null ? lot.Lot_SerialNumber : "",
                                InvoiceDate = lot.RecieptDateTime// lot != null ? lot.RecieptDateTime : (System.DateTime?)null
                            }).ToList();

                return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet); }
        }
        [HttpPost]
        public ActionResult ApproveVouchernew(int voucherId)
        {
            try
            {
                int CompanyID = (int)Session["Company_ID"];
                string userName = Session["U_Name"].ToString();

                // 1. Fetch the master voucher record
                var voucher = DB.BOMVouchers.SingleOrDefault(ab => ab.BOMVoucherID == voucherId);
                if (voucher == null)
                {
                    return Json(new { success = false, message = "Error: Voucher not found." });
                }

                // 2. Fetch all active lines for this voucher
                var voucherLines = (from nn in DB.BOMVoucherlines
                                    where nn.BOMVoucherid == voucherId && nn.Isdeleted == 0
                                    select nn).ToList();

                // --- STEP 1: VIRTUAL CONSOLIDATION ---
                // Group all lines by Item ID to get the TOTAL requirement for validation.
                // This solves the "Battery" problem where 4 lines of 1 Qty each require 4 total in stock.
                var totalRequirements = voucherLines
                    .GroupBy(l => l.rawitemid)
                    .Select(g => new {
                        ItemId = g.Key,
                        TotalRequired = g.Sum(x => x.QuantityRequired)
                    }).ToList();

                // --- STEP 2: CONSOLIDATED PRE-VALIDATION ---
                var validationErrors = new List<string>();

                foreach (var req in totalRequirements)
                {
                    var itemMaster = DB.BOMItemMasters.FirstOrDefault(it => it.Itemid == req.ItemId);
                    if (itemMaster == null) continue;

                    // A. Check Standard Stock
                    if (voucher.AllowNegativeTransaction == false)
                    {
                        var stockRecord = DB.StockTables.FirstOrDefault(stk => stk.itemid == req.ItemId && stk.CompanyID == CompanyID);
                        decimal currentStock = (decimal)(stockRecord != null ? stockRecord.Stock : 0);

                        if (currentStock < req.TotalRequired)
                        {
                            validationErrors.Add($"[Stock] {itemMaster.ItemName} (Total Req: {req.TotalRequired}, Total Avail: {currentStock})");
                        }
                    }

                    // B. Check FIFO Lot Availability
                    if (itemMaster.FifoLot == true && voucher.AllowLotCheck == true)
                    {
                        var totalLotStock = DB.Stock_lotDetail
                                              .Where(l => l.ItemID == req.ItemId &&
                                                          l.IsAvailable == true &&
                                                          l.IsReserved == false &&
                                                          l.CompanyID == CompanyID &&
                                                          l.IsDeleted == false)
                                              .Sum(l => (decimal?)l.CurrentQuantity) ?? 0;

                        if (totalLotStock < req.TotalRequired)
                        {
                            validationErrors.Add($"[Lot] {itemMaster.ItemName} (Total FIFO Req: {req.TotalRequired}, Total FIFO Stock: {totalLotStock})");
                        }
                    }
                }

                if (validationErrors.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = "Validation failed. Insufficient stock for consolidated requirements.",
                        details = validationErrors.OrderBy(x => x.StartsWith("[Stock]") ? 0 : 1).ToList()
                    });
                }

                voucher.Approvalstatus = 1;
                voucher.ApprovedBY = userName;
                //voucher.ApprovedDate = DateTime.Now;
                voucher.Ewap = 0;
                voucher.ProductionCompletionStatus = 0;

                List<string> allottedSerials = new List<string>();

                foreach (var item in voucherLines)
                {
                    var itemMaster = DB.BOMItemMasters.FirstOrDefault(it => it.Itemid == item.rawitemid);

                    // A. Assign FIFO Lot (Sequential Deduction)
                    if (itemMaster != null && itemMaster.FifoLot == true && voucher.AllowLotCheck == true)
                    {
                        decimal remainingToDeduct = (decimal)item.QuantityRequired;

                        // FETCH ALL POTENTIAL LOTS FOR THIS ITEM ONCE
                        // This prevents the query from getting stuck on the same lot
                        var availableLots = DB.Stock_lotDetail
                                               .Where(l => l.ItemID == item.rawitemid &&
                                                           l.CurrentQuantity > 0 &&
                                                           l.IsDeleted == false &&
                                                           l.IsAvailable == true &&
                                                           l.IsReserved == false &&
                                                           l.CompanyID == CompanyID)
                                               .OrderBy(l => l.RecieptDateTime)
                                               .ThenBy(l => l.id)
                                               .ToList();

                        foreach (var lot in availableLots)
                        {
                            if (remainingToDeduct <= 0) break;

                            decimal lotQty = (decimal)lot.CurrentQuantity;
                            decimal canTake = Math.Min(lotQty, remainingToDeduct);

                            // ONLY PROCEED IF WE ARE ACTUALLY TAKING SOMETHING
                            if (canTake > 0)
                            {
                                lot.CurrentQuantity -= canTake;
                                remainingToDeduct -= canTake;

                                if (lot.CurrentQuantity <= 0)
                                {
                                    lot.CurrentQuantity = 0;
                                    lot.IsAvailable = false;
                                }

                                item.LotID = lot.id;

                                // This will now only add to the list if quantity > 0
                                allottedSerials.Add($"{itemMaster.ItemName} : {lot.Lot_SerialNumber ?? lot.id.ToString()} (Qty: {canTake:N3})");
                            }
                        }
                    }

                    // B. Standard Stock Deduction
                    var stockRecord = DB.StockTables.FirstOrDefault(stk => stk.itemid == item.rawitemid && stk.CompanyID == CompanyID);
                    if (stockRecord != null)
                    {
                        stockRecord.Stock -= item.QuantityRequired;
                        // ... (Negative stock logging logic) ...

                        item.Approveddate = System.DateTime.Now;
                        item.Approvedby = userName;
                        item.ApprovedQuantity = item.QuantityRequired;
                    }
                }

                DB.SaveChanges();
                SendBOMApprovalEmail(voucherId);

                string serialList = allottedSerials.Any() ? string.Join(", ", allottedSerials.Distinct()) : "None";
                return Json(new
                {
                    success = true,
                    message = "Voucher Approved. Stock and FIFO Lots updated successfully! Serial Details: " + serialList
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Exception: " + ex.Message });
            }
        }

        [HttpPost]
        public ActionResult ApproveVouchernewold(int voucherId)
        {
            try
            {
                int CompanyID = (int)Session["Company_ID"];
                string userName = Session["U_Name"].ToString();

                // 1. Fetch the master voucher record
                var voucher = DB.BOMVouchers.SingleOrDefault(ab => ab.BOMVoucherID == voucherId);
                if (voucher == null)
                {
                    return Json(new { success = false, message = "Error: Voucher not found." });
                }

                // 2. Fetch all active lines for this voucher
                var voucherLines = (from nn in DB.BOMVoucherlines
                                    where nn.BOMVoucherid == voucherId && nn.Isdeleted == 0
                                    select nn).ToList();

                // --- STEP 1: CONSOLIDATED PRE-VALIDATION ---
                var validationErrors = new List<string>();

                foreach (var line in voucherLines)
                {
                    var itemMaster = DB.BOMItemMasters.FirstOrDefault(it => it.Itemid == line.rawitemid);
                    if (itemMaster == null) continue;

                    // A. Check Standard Stock (If Negative Transactions are NOT allowed)
                    if (voucher.AllowNegativeTransaction == false)
                    {
                        var stockRecord = DB.StockTables.FirstOrDefault(stk => stk.itemid == line.rawitemid && stk.CompanyID == CompanyID);
                        if (stockRecord == null || stockRecord.Stock < line.QuantityRequired)
                        {
                            decimal currentStock = (decimal)(stockRecord != null ? stockRecord.Stock : 0);
                            validationErrors.Add($"[Stock] {itemMaster.ItemName} (Req: {line.QuantityRequired}, Avail: {currentStock})");
                        }
                    }

                    // B. Check FIFO Lot Availability (Always required if FifoLot is true)
                    if (itemMaster.FifoLot == true && voucher.AllowLotCheck == true)
                    {
                        var availableLot = DB.Stock_lotDetail
                                             .Where(l => l.ItemID == line.rawitemid &&
                                                         l.CurrentQuantity >= line.QuantityRequired &&
                                                         l.IsAvailable == true &&
                                                         l.IsReserved == false &&
                                                         l.CompanyID == CompanyID &&
                                                         l.IsDeleted == false)
                                             .OrderBy(l => l.RecieptDateTime)
                                             .ThenBy(l => l.id)
                                             .FirstOrDefault();

                        if (availableLot == null)
                        {
                            validationErrors.Add($"[Lot] {itemMaster.ItemName} (No available FIFO lot found)");
                        }
                    }
                }

                // --- STEP 2: BLOCK IF ANY VALIDATION FAILED ---
                if (validationErrors.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = "Validation failed. Please review stock and lot availability.",
                        details = validationErrors.OrderBy(x => x.StartsWith("[Stock]") ? 0 : 1).ToList()
                    });
                }

                // --- STEP 3: PROCEED WITH UPDATES ---
                voucher.Approvalstatus = 1;
                voucher.ApprovedBY = userName;
                // voucher.ApprovedDate = DateTime.Now;
                voucher.Ewap = 0;
                voucher.ProductionCompletionStatus = 0;

                List<string> allottedSerials = new List<string>();

                foreach (var item in voucherLines)
                {
                    var itemMaster = DB.BOMItemMasters.FirstOrDefault(it => it.Itemid == item.rawitemid);

                    // A. Assign FIFO Lot
                    if (itemMaster != null && itemMaster.FifoLot == true && voucher.AllowLotCheck == true)
                    {
                        var oldestLot = DB.Stock_lotDetail
                                          .Where(l => l.ItemID == item.rawitemid &&
                                                      l.CurrentQuantity >= item.QuantityRequired &&
                                                      l.IsDeleted == false &&
                                                      l.IsAvailable == true &&
                                                      l.IsReserved == false &&
                                                      l.CompanyID == CompanyID)
                                          .OrderBy(l => l.RecieptDateTime)
                                          .ThenBy(l => l.id)
                                          .FirstOrDefault();

                        if (oldestLot != null)
                        {
                            item.LotID = oldestLot.id; // Map to your specific column name
                            oldestLot.CurrentQuantity -= item.QuantityRequired;

                            if (oldestLot.CurrentQuantity == 0) oldestLot.IsAvailable = false;

                            // Collect details for the return message
                            allottedSerials.Add($"{itemMaster.ItemName}: {oldestLot.Lot_SerialNumber ?? oldestLot.id.ToString()}");
                        }
                    }

                    // B. Standard Stock Deduction
                    var stockRecord = DB.StockTables.FirstOrDefault(stk => stk.itemid == item.rawitemid && stk.CompanyID == CompanyID);
                    if (stockRecord != null)
                    {
                        stockRecord.Stock -= item.QuantityRequired;

                        if (stockRecord.Stock <= 0)
                        {
                            BOM_NegativeStock BNS = new BOM_NegativeStock
                            {
                                BOMVoucherid = voucherId,
                                ItemID = stockRecord.itemid,
                                Stock = stockRecord.Stock,
                                Createdon = System.DateTime.Now,
                            };
                            DB.BOM_NegativeStock.Add(BNS);
                        }

                        item.Approveddate = System.DateTime.Now;
                        item.Approvedby = userName;
                        item.ApprovedQuantity = item.QuantityRequired;
                    }
                }

                DB.SaveChanges();
                SendBOMApprovalEmail(voucherId);

                string serialList = allottedSerials.Any() ? string.Join(", ", allottedSerials) : "None";
                return Json(new
                {
                    success = true,
                    message = "Voucher Approved. Stock and FIFO Lots updated successfully! Allotted Serial Numbers Are - " + serialList
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Exception: " + ex.Message });
            }
        }
        private void SendBOMApprovalEmail(int voucherId)
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            try
            {
                var company = DB.Companies.Where(x => x.CompanyID == companyid).Select(x => x.CompanyName).FirstOrDefault();

                // 1. Fetch Voucher Header
                var voucher = DB.BOMVouchers.FirstOrDefault(v => v.BOMVoucherID == voucherId);
                if (voucher == null) return;

                // 2. Fetch Detailed Lines with FIFO Allotment and Stock Context
                var bomLines = (from vl in DB.BOMVoucherlines
                                join item in DB.BOMItemMasters on vl.rawitemid equals item.Itemid
                                join uom in DB.BOM_UOM on item.UOM equals uom.id into uomGroup
                                from uom in uomGroup.DefaultIfEmpty()
                                join lot in DB.Stock_lotDetail on vl.LotID equals lot.id into lotGroup
                                from lot in lotGroup.DefaultIfEmpty()
                                join stk in DB.StockTables on vl.rawitemid equals stk.itemid into stkGroup
                                from stk in stkGroup.Where(s => s.CompanyID == companyid).DefaultIfEmpty()
                                where vl.BOMVoucherid == voucherId && vl.Isdeleted == 0
                                select new
                                {
                                    item.Itemid,
                                    item.ItemCode,
                                    item.ItemName,
                                    UOM = uom != null ? uom.UOM : "NA",
                                    Quantity = vl.QuantityRequired,
                                    AllottedSerial = lot != null ? (lot.Lot_SerialNumber ?? lot.id.ToString()) : "Bulk/Manual",
                                    AvailableStock = stk != null ? stk.Stock : 0,
                                    MOQ = stk != null ? stk.MOQ : 0
                                }).ToList();

                // 3. Fetch Finished Good Name
                var fgName = DB.BOMItemMasters.Where(x => x.Itemid == voucher.FGProductID).Select(x => x.ItemName).FirstOrDefault();

                // 4. Construct Model for Razor Template
                var model = new
                {
                    VoucherNumber = voucher.VoucherNumber,
                    FGName = fgName ?? "NA",
                    CompanyName = company,
                    ApprovedBy = voucher.ApprovedBY,
                    ApprovalDate = "",
                    Items = bomLines
                };

                // 5. Setup Email Configuration
                var settings = DB.CMKL_Email_Setting.FirstOrDefault(x => x.id == 2);
                var toList = DB.CMKL_Email.Where(x => x.DDLName == "BOMApprovalEmail" && x.CompanyID == companyid && x.Active == 1).ToList();
                var ccList = DB.CMKL_Email.Where(x => x.DDLName == "BOMApprovalEmailCC" && x.CompanyID == companyid && x.Active == 1).ToList();
                var sender = DB.CMKL_Email.FirstOrDefault(x => x.DDLName == "ERPSender");

                if (settings == null || sender == null) return;

                MailMessage mail = new MailMessage();
                mail.From = new MailAddress(sender.Email, "IEPL BOM Advisory");
                foreach (var r in toList) mail.To.Add(r.Email);
                foreach (var c in ccList) mail.CC.Add(c.Email);

                mail.Subject = "BOM Stock Issued for " + voucher.VoucherNumber + " | " + model.FGName;

                // 6. Render Body using Template
                string templatePath = Server.MapPath("~/Views/EmailManage/BOMApprovalEmailTemplate.cshtml");
                string templateContent = System.IO.File.ReadAllText(templatePath);

                // Note: Using RazorEngine or your existing View Rendering helper
                string body = RazorEngine.Razor.Parse(templateContent, model);

                

                mail.Body = body;
                mail.IsBodyHtml = true;


                // 8. SMTP Transmission
                using (SmtpClient client = new SmtpClient(settings.smtp))
                {
                    client.Port = Convert.ToInt32(settings.port);
                    client.Credentials = new System.Net.NetworkCredential(sender.Email, settings.IT_password);
                    client.EnableSsl = Convert.ToBoolean(settings.ssl);
                    client.Send(mail);
                }
            }
            catch (Exception ex)
            {
                // Error handling logic (e.g. logging to database or file)
            }
        }

        [HttpPost]
        public ActionResult ApproveVoucher(int voucherId)
        {
            try
            {
                int CompanyID = (int)Session["Company_ID"];
                string userName = Session["U_Name"].ToString();

                // 1. Fetch the master voucher record
                var voucher = DB.BOMVouchers.SingleOrDefault(ab => ab.BOMVoucherID == voucherId);
                if (voucher == null)
                {
                    return Json(new { success = false, message = "Error: Voucher not found." });
                }

                // 2. Fetch all active lines for this voucher
                var voucherLines = (from nn in DB.BOMVoucherlines
                                    where nn.BOMVoucherid == voucherId && nn.Isdeleted == 0
                                    select nn).ToList();
                //Check Lot Allocation True or Not for all items
                var Lotdetail = new List<string>();
                foreach (var nn in voucherLines)
                {
                   
                }

                //Check Records if -ve Transaction is Alloweded for the same
                if (voucher.AllowNegativeTransaction == false)
                {

                    // --- STEP 1: PRE-VALIDATION (Strict Check) ---
                    // We check all lines first to see if any would result in negative stock.
                    var insufficientStockItems = new List<string>();

                    foreach (var item in voucherLines)
                    {
                        var stockRecord = (from stk in DB.StockTables
                                           where stk.itemid == item.rawitemid && stk.CompanyID == CompanyID
                                           select stk).SingleOrDefault();

                        // If stock record doesn't exist or available stock is less than required
                        if (stockRecord == null || stockRecord.Stock < item.QuantityRequired)
                        {
                            var itemName = (from it in DB.BOMItemMasters
                                            where it.Itemid == item.rawitemid
                                            select it.ItemName).FirstOrDefault();

                            decimal currentStock = (decimal)(stockRecord != null ? stockRecord.Stock : 0);
                            insufficientStockItems.Add($"{itemName} (Req: {item.QuantityRequired}, Avail: {currentStock})");
                        }
                    }


                    // --- STEP 2: BLOCK TRANSACTION ---
                    if (insufficientStockItems.Any())
                    {
                        return Json(new
                        {
                            success = false,
                            message = "Insufficient Stock. Please review Current Stock.",
                            details = insufficientStockItems
                        });
                    }
                }

                // --- STEP 3: PROCEED WITH UPDATES (Stock is safe) ---
                voucher.Approvalstatus = 1;
                voucher.Ewap = 0;
                voucher.ProductionCompletionStatus = 0;
                voucher.ApprovedBY = userName;

                foreach (var item in voucherLines)
                {
                    var stockRecord = (from stk in DB.StockTables
                                       where stk.itemid == item.rawitemid && stk.CompanyID == CompanyID
                                       select stk).SingleOrDefault();

                    if (stockRecord != null)
                    {
                        // Deduct stock
                        stockRecord.Stock -= item.QuantityRequired;

                        // Log if stock reached zero or became exactly negative (though pre-check should prevent negative)
                        if (stockRecord.Stock <= 0)
                        {
                            BOM_NegativeStock BNS = new BOM_NegativeStock
                            {
                                BOMVoucherid = voucherId,
                                ItemID = stockRecord.itemid,
                                Stock = stockRecord.Stock,
                                Createdon = System.DateTime.Now,
                            };
                            DB.BOM_NegativeStock.Add(BNS);
                        }

                        // Update detail line with approval info
                        item.Approveddate = System.DateTime.Now;
                        item.Approvedby = userName;
                        item.ApprovedQuantity = item.QuantityRequired;
                    }
                }

                DB.SaveChanges();
                return Json(new { success = true, message = "Voucher Approved and Stock updated successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Exception: " + ex.Message });
            }
        }

        public ActionResult ApproveReqisitionVoucher(int voucherId)
        {
            var finyear = Session["Fin_Year"].ToString();
            int CompanyID = (int)Session["Company_ID"];
            var update = (from ab in DB.BOMRequisitionHeads
                          where ab.id == voucherId
                          select ab).SingleOrDefault();


            BOMVoucher BV = new BOMVoucher();

            //Update BOMRequisition Table Head
            update.IsApproved = 1;
            update.IsDeleted = 0;
            update.ApprovedBy = Session["U_Name"].ToString();
            //update.Ewap = 0;
            DB.SaveChanges();

            //Get List of Records that need to delete 
            var getremovedatalist = (from rd in DB.BOMRequisitionLines
                                     where rd.BomRequisitionHeadid == voucherId && rd.Action == "Delete"
                                     select rd).ToList();
            //Get List of records that need to add
            var getadditionlist = (from rd in DB.BOMRequisitionLines
                                   where rd.BomRequisitionHeadid == voucherId && rd.Action == "Add"
                                   select rd).ToList();
            //if Remove record list is not null then remove items
            if (getremovedatalist != null)
            {
                var Bomlist = DB.BOMVoucherlines.Where(x => x.BOMVoucherid == update.BOMVoucherID).ToList();
                foreach (var item in getremovedatalist)
                {
                    var bomVoucherLine = DB.BOMVoucherlines
    .FirstOrDefault(x => x.BOMVoucherid == update.BOMVoucherID &&
                        x.Categoryid == item.Categoryid &&
                        x.Subcategoryid == item.Subcategoryid &&
                        x.Finalitemid == item.FinalItemID &&
                        x.rawitemid == item.RawItemID &&
                        x.ApprovedQuantity == item.Quantity &&
                        x.UOM == item.UOM &&
                        x.Isdeleted == 0);
                    if (bomVoucherLine != null)
                    {
                        bomVoucherLine.Isdeleted = 1;
                    }
                    //Add Back Stock into Stock tables

                    var stocktable = DB.StockTables.FirstOrDefault(s => s.itemid == item.RawItemID && s.CompanyID == CompanyID);
                    if (stocktable != null)
                    {
                        stocktable.Stock += item.Quantity;
                    }
                    DB.SaveChanges();
                }

            }
            if (getadditionlist != null)
            {
                foreach (var up in getadditionlist)
                {
                    BOMVoucherline BVL = new BOMVoucherline();
                    BVL.BOMVoucherid = (int)up.BomVoucherHeadid;
                    BVL.Categoryid = (int)up.Categoryid;
                    BVL.Subcategoryid = (int)up.Subcategoryid;
                    BVL.Finalitemid = (int)up.FinalItemID;
                    BVL.rawitemid = (int)up.RawItemID;
                    BVL.QuantityRequired = (decimal)up.Quantity;
                    BVL.ApprovedQuantity = (decimal)up.Quantity;
                    BVL.Stockapproved = 0;
                    BVL.UOM = up.UOM;
                    BVL.Approvedby = Session["U_Name"].ToString();
                    BVL.Approveddate = System.DateTime.Now;
                    BVL.Isdeleted = 0;
                    DB.BOMVoucherlines.Add(BVL);

                    //Remove Stock From Stock Table
                    var stocktable = DB.StockTables.FirstOrDefault(s => s.itemid == up.RawItemID && s.CompanyID == CompanyID);
                    if (stocktable != null)
                    {
                        stocktable.Stock -= up.Quantity;
                    }
                    DB.SaveChanges();
                }

            }


            var voucherhead = (from vh in DB.BOMVouchers
                               where vh.BOMVoucherID == update.BOMVoucherID
                               select vh).SingleOrDefault();
            voucherhead.FGProductID = update.NewFGProductID;
            DB.SaveChanges();
            //Check for Manual Returns if Bom is of Other Financial Year

            var bomdetail = (from ab in DB.BOMRequisitionHeads
                             where ab.id == voucherId
                             select ab).SingleOrDefault();
            var bom = DB.BOMVouchers.Where(x => x.BOMVoucherID == bomdetail.BOMVoucherID).SingleOrDefault();
            var bomlines = (from bs in DB.BOMRequisitionLines
                            where bs.BomRequisitionHeadid == voucherId && bs.Action == "Delete"
                            select bs).ToList();
            var billseries = (from bb in DB.Bill_Series
                              where bb.Type == "StockReturn" && bb.CompanyID == CompanyID && bb.Fin_Year == finyear
                              select bb).SingleOrDefault();

            if (bom.Finyear != finyear)
            {
                //Do Stock Return Entries Automatically
                IEPLStockReturnHead SR = new IEPLStockReturnHead();
                SR.Createdon = System.DateTime.Now;
                SR.Createdby = Session["U_Name"].ToString();
                SR.VoucherDate = System.DateTime.Now;
                SR.BomVoucherid = bom.BOMVoucherID;
                SR.CompanyID = CompanyID;
                SR.Fin_Year = finyear;
                SR.Departmentid = 15;// Department ID for Requisition Return is 15
                SR.VoucherNumber = billseries.Series + billseries.Number;
                DB.IEPLStockReturnHeads.Add(SR);
                DB.SaveChanges();
                //Add Return Lines
                foreach (var lin in bomlines)
                {
                    IEPLStockReturnDetail SRL = new IEPLStockReturnDetail();
                    SRL.Voucherid = SR.id;
                    SRL.Itemcodeid = lin.RawItemID;
                    SRL.Remarks = "BOM Requistion Return of Other Fin Year";
                    SRL.Quantity = lin.Quantity;
                    SRL.ApprovedQuantity = lin.Quantity;
                    SRL.ApprovedStatus = 1;
                    SRL.ApprovedBy = Session["U_Name"].ToString();
                    SRL.ApprovedDate = System.DateTime.Now;
                    SRL.Voucher = "AutoApproved - Requisition";
                    SRL.RejectedQuantity = 0;
                    SRL.IsDeleted = 0;
                    DB.IEPLStockReturnDetails.Add(SRL);
                    //Add Stock to Table
                    var stock = DB.StockTables.Where(x => x.itemid == lin.RawItemID && x.CompanyID == CompanyID).SingleOrDefault();
                    if (stock != null)
                    {
                        stock.Stock += lin.Quantity;
                    }
                }
                //Add to Stock Items


                //Update Bill Series
                billseries.Number += 1;
                //var Stockreturnnumber = SR.VoucherNumber;
                DB.SaveChanges();
            }


            //Send Email Notification to Store 
            SendRequitionApprovalMail(voucherId);

            return Json(new { success = true, msg = "Voucher Approved" });

        }
        [HttpPost]
        public ActionResult ApproveReqisitionVouchernew(int voucherId)
        {
            using (var transaction = DB.Database.BeginTransaction())
            {
                try
                {
                    var finyear = Session["Fin_Year"]?.ToString();
                    int CompanyID = Convert.ToInt32(Session["Company_ID"]);
                    string userName = Session["U_Name"]?.ToString();

                    // 1. Fetch the Requisition Head
                    var requisitionHead = DB.BOMRequisitionHeads.SingleOrDefault(ab => ab.id == voucherId);
                    if (requisitionHead == null)
                        return Json(new { success = false, msg = "Voucher not found." });

                    var bomHead = DB.BOMVouchers.FirstOrDefault(ab => ab.BOMVoucherID == requisitionHead.BOMVoucherID);
                    bool isLotCheckEnabled = bomHead != null && bomHead.AllowLotCheck == true;

                    var removeLines = DB.BOMRequisitionLines.Where(rd => rd.BomRequisitionHeadid == voucherId && rd.Action == "Delete").ToList();
                    var addLines = DB.BOMRequisitionLines.Where(rd => rd.BomRequisitionHeadid == voucherId && rd.Action == "Add").ToList();

                    // --- STEP 1: CONSOLIDATED VIRTUAL & LOT VALIDATION ---
                    var validationErrors = new List<string>();
                    var allItemIds = removeLines.Select(x => x.RawItemID).Union(addLines.Select(x => x.RawItemID)).Distinct().ToList();

                    foreach (var itemId in allItemIds)
                    {
                        var itemMaster = DB.BOMItemMasters.FirstOrDefault(it => it.Itemid == itemId);
                        if (itemMaster == null) continue;

                        decimal qtyToRemove = (decimal)(removeLines.Where(x => x.RawItemID == itemId).Sum(x => x.Quantity) ?? 0);
                        decimal qtyToAdd = (decimal)(addLines.Where(x => x.RawItemID == itemId).Sum(x => x.Quantity) ?? 0);

                        var stockRecord = DB.StockTables.FirstOrDefault(s => s.itemid == itemId && s.CompanyID == CompanyID);
                        decimal currentStock = (decimal)(stockRecord != null ? stockRecord.Stock : 0);
                        decimal virtualPhysicalBalance = currentStock + qtyToRemove - qtyToAdd;

                        if (virtualPhysicalBalance < 0)
                            validationErrors.Add($"[Stock] {itemMaster.ItemName} Shortfall. (Virtual Bal: {virtualPhysicalBalance})");

                        if (isLotCheckEnabled && itemMaster.FifoLot == true && qtyToAdd > 0)
                        {
                            decimal currentLotStock = DB.Stock_lotDetail.Where(l => l.ItemID == itemId && l.IsAvailable == true && l.IsReserved == false && l.CompanyID == CompanyID && l.IsDeleted == false).Sum(l => (decimal?)l.CurrentQuantity) ?? 0;
                            decimal virtualLotBalance = currentLotStock + qtyToRemove - qtyToAdd;

                            if (virtualLotBalance < 0)
                                validationErrors.Add($"[Lot] {itemMaster.ItemName} Insufficient FIFO Lots. (Virtual Lot Bal: {virtualLotBalance})");
                        }
                    }

                    if (validationErrors.Any())
                    {
                        transaction.Rollback();
                        return Json(new { success = false, msg = "Validation failed.", details = validationErrors });
                    }

                    // --- STEP 2: EXECUTE REMOVALS & REVERSE LOTS ---
                    // Track processed IDs to ensure we don't pick the same VoucherLine twice for multiple battery lines
                    List<int> processedVoucherLineIds = new List<int>();

                    foreach (var item in removeLines)
                    {
                        var vLine = DB.BOMVoucherlines.FirstOrDefault(x =>
                            x.BOMVoucherid == requisitionHead.BOMVoucherID &&
                            x.rawitemid == item.RawItemID &&
                            x.Categoryid == item.Categoryid &&
                            x.Subcategoryid == item.Subcategoryid &&
                            x.Isdeleted == 0 &&
                            !processedVoucherLineIds.Contains(x.id)); // Ensures unique line selection

                        if (vLine != null)
                        {
                            processedVoucherLineIds.Add(vLine.id);

                            if (vLine.LotID.HasValue && vLine.LotID > 0 && isLotCheckEnabled)
                            {
                                var lot = DB.Stock_lotDetail.FirstOrDefault(l => l.id == vLine.LotID);
                                if (lot != null)
                                {
                                    lot.CurrentQuantity += (vLine.ApprovedQuantity ?? 0);
                                    lot.IsAvailable = true;
                                }
                            }
                            vLine.Isdeleted = 1;
                        }
                        var sTab = DB.StockTables.FirstOrDefault(s => s.itemid == item.RawItemID && s.CompanyID == CompanyID);
                        if (sTab != null) sTab.Stock += (decimal)item.Quantity;
                    }
                    DB.SaveChanges(); // CRITICAL: Save reversals so additions see restored quantities

                    // --- STEP 3: ALLOCATE NEW STOCK & LOTS ---
                    List<string> allottedSerials = new List<string>();
                    foreach (var up in addLines)
                    {
                        var itemMaster = DB.BOMItemMasters.FirstOrDefault(it => it.Itemid == up.RawItemID);
                        int? lastLotId = null;

                        if (itemMaster != null && itemMaster.FifoLot == true && isLotCheckEnabled)
                        {
                            decimal rem = (decimal)up.Quantity;
                            // Memory-safe deduction to prevent infinite loops or redundant picks
                            var availableLots = DB.Stock_lotDetail
                                .Where(l => l.ItemID == up.RawItemID && l.CurrentQuantity > 0 && l.IsAvailable == true && l.IsReserved == false && l.CompanyID == CompanyID && l.IsDeleted == false)
                                .OrderBy(l => l.RecieptDateTime).ThenBy(l => l.id).ToList();

                            foreach (var lot in availableLots)
                            {
                                if (rem <= 0) break;
                                decimal take = Math.Min((decimal)lot.CurrentQuantity, rem);
                                if (take > 0)
                                {
                                    lot.CurrentQuantity -= take;
                                    rem -= take;
                                    if (lot.CurrentQuantity <= 0) { lot.CurrentQuantity = 0; lot.IsAvailable = false; }
                                    lastLotId = lot.id;
                                    allottedSerials.Add($"{itemMaster.ItemName}: {lot.Lot_SerialNumber ?? "NA"} (Qty: {take:N3})");
                                }
                            }
                        }

                        // Add unique new line item
                        DB.BOMVoucherlines.Add(new BOMVoucherline
                        {
                            BOMVoucherid = (int)requisitionHead.BOMVoucherID,
                            rawitemid = (int)up.RawItemID,
                            QuantityRequired = (decimal)up.Quantity,
                            ApprovedQuantity = (decimal)up.Quantity,
                            UOM = up.UOM,
                            Approvedby = userName,
                            Approveddate =System.DateTime.Now,
                            LotID = lastLotId,
                            Isdeleted = 0,
                            Categoryid = up.Categoryid ?? 0,
                            Subcategoryid = up.Subcategoryid ?? 0,
                            Finalitemid = up.FinalItemID ?? 0
                        });

                        var sTab = DB.StockTables.FirstOrDefault(s => s.itemid == up.RawItemID && s.CompanyID == CompanyID);
                        if (sTab != null) sTab.Stock -= (decimal)up.Quantity;
                    }

                    requisitionHead.IsApproved = 1;
                    requisitionHead.ApprovedBy = userName;
                    if (bomHead != null) bomHead.FGProductID = requisitionHead.NewFGProductID;

                    DB.SaveChanges();
                    transaction.Commit();

                    string serialMsg = allottedSerials.Any() ? " Serials: " + string.Join(", ", allottedSerials.Distinct()) : "";
                    SendRequitionApprovalMail(voucherId);
                    return Json(new { success = true, msg = "Voucher Approved and BOM Synchronized." + serialMsg });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, msg = "Error: " + ex.Message });
                }
            }
        }
        [HttpPost]
        public ActionResult ApproveReqisitionVouchernewold(int voucherId)
        {
            using (var transaction = DB.Database.BeginTransaction())
            {
                try
                {
                    var finyear = Session["Fin_Year"]?.ToString();
                    int CompanyID = Convert.ToInt32(Session["Company_ID"]);
                    string userName = Session["U_Name"]?.ToString();

                    // 1. Fetch the Requisition Head
                    var requisitionHead = DB.BOMRequisitionHeads.SingleOrDefault(ab => ab.id == voucherId);
                    if (requisitionHead == null)
                        return Json(new { success = false, msg = "Voucher not found." });

                    // Check Lot Check Status on the Parent BOM Voucher
                    var bomHead = DB.BOMVouchers.Where(ab => ab.BOMVoucherID == requisitionHead.BOMVoucherID)
                                               .Select(ab => new { ab.AllowLotCheck }).SingleOrDefault();
                    bool isLotCheckEnabled = bomHead != null && bomHead.AllowLotCheck == true;

                    // 2. Separate lines into Deletions and Additions
                    var removeLines = DB.BOMRequisitionLines
                        .Where(rd => rd.BomRequisitionHeadid == voucherId && rd.Action == "Delete").ToList();

                    var addLines = DB.BOMRequisitionLines
                        .Where(rd => rd.BomRequisitionHeadid == voucherId && rd.Action == "Add").ToList();

                    // --- STEP 1: TRULY VIRTUAL STOCK VALIDATION ---
                    var validationErrors = new List<string>();
                    var allItemIds = removeLines.Select(x => x.RawItemID)
                                                .Union(addLines.Select(x => x.RawItemID))
                                                .Distinct().ToList();

                    foreach (var itemId in allItemIds)
                    {
                        var itemMaster = DB.BOMItemMasters.FirstOrDefault(it => it.Itemid == itemId);
                        if (itemMaster == null) continue;

                        decimal qtyToBeRemoved = (decimal)(removeLines.Where(x => x.RawItemID == itemId).Sum(x => x.Quantity) ?? 0);
                        decimal qtyToBeAdded = (decimal)(addLines.Where(x => x.RawItemID == itemId).Sum(x => x.Quantity) ?? 0);

                        var stockRecord = DB.StockTables.FirstOrDefault(s => s.itemid == itemId && s.CompanyID == CompanyID);
                        decimal currentPhysicalStock = (decimal)(stockRecord != null ? stockRecord.Stock : 0);

                        decimal virtualBalance = currentPhysicalStock + qtyToBeRemoved - qtyToBeAdded;

                        if (virtualBalance < 0)
                        {
                            validationErrors.Add($"[Stock] {itemMaster.ItemName} (Shortfall: {Math.Abs(virtualBalance)})");
                        }
                    }

                    // Early exit if stock validation fails
                    if (validationErrors.Any())
                    {
                        transaction.Rollback();
                        return Json(new
                        {
                            success = false,
                            msg = "Validation failed. Insufficient stock detected.",
                            details = validationErrors.OrderBy(x => x.StartsWith("[Stock]") ? 0 : 1).ToList()
                        });
                    }

                    // --- STEP 2: EXECUTE REMOVALS & REVERSE LOTS ---
                    if (removeLines.Any())
                    {
                        foreach (var item in removeLines)
                        {
                            var voucherLine = DB.BOMVoucherlines.FirstOrDefault(x =>
                                x.BOMVoucherid == requisitionHead.BOMVoucherID &&
                                x.Categoryid == item.Categoryid &&
                                x.Subcategoryid == item.Subcategoryid &&
                                x.Finalitemid == item.FinalItemID &&
                                x.rawitemid == item.RawItemID &&
                                x.Isdeleted == 0);

                            if (voucherLine != null)
                            {
                                if (voucherLine.LotID.HasValue && voucherLine.LotID > 0 && isLotCheckEnabled)
                                {
                                    var lotDetail = DB.Stock_lotDetail.FirstOrDefault(l => l.id == voucherLine.LotID);
                                    if (lotDetail != null)
                                    {
                                        lotDetail.CurrentQuantity += voucherLine.ApprovedQuantity ?? 0;
                                        lotDetail.IsAvailable = true;
                                    }
                                }
                                voucherLine.Isdeleted = 1;
                            }

                            var stockTable = DB.StockTables.FirstOrDefault(s => s.itemid == item.RawItemID && s.CompanyID == CompanyID);
                            if (stockTable != null) stockTable.Stock += (decimal)item.Quantity;
                        }
                    }

                    DB.SaveChanges(); // Important: Save removals so Lot Query in Step 3 sees freed quantity

                    // --- STEP 3: FIFO LOT VALIDATION ---
                    if (isLotCheckEnabled)
                    {
                        foreach (var add in addLines)
                        {
                            var itemMaster = DB.BOMItemMasters.FirstOrDefault(it => it.Itemid == add.RawItemID);
                            if (itemMaster != null && itemMaster.FifoLot == true)
                            {
                                var availableLot = DB.Stock_lotDetail
                                    .Where(l => l.ItemID == add.RawItemID &&
                                                l.CurrentQuantity >= add.Quantity &&
                                                l.IsAvailable == true &&
                                                l.IsReserved==false &&
                                                l.CompanyID == CompanyID &&
                                                l.IsDeleted == false)
                                    .OrderBy(l => l.RecieptDateTime).ThenBy(l => l.id).FirstOrDefault();

                                if (availableLot == null)
                                {
                                    validationErrors.Add($"[Lot] {itemMaster.ItemName} (No available FIFO lot for requested quantity)");
                                }
                            }
                        }
                    }

                    if (validationErrors.Any())
                    {
                        transaction.Rollback();
                        return Json(new
                        {
                            success = false,
                            msg = "Validation failed. Lot availability issue.",
                            details = validationErrors.OrderBy(x => x.StartsWith("[Stock]") ? 0 : 1).ToList()
                        });
                    }

                    // --- STEP 4: EXECUTE ADDITIONS & ASSIGN NEW LOTS ---
                    List<string> allottedSerials = new List<string>();
                    if (addLines.Any())
                    {
                        foreach (var up in addLines)
                        {
                            var itemMaster = DB.BOMItemMasters.FirstOrDefault(it => it.Itemid == up.RawItemID);
                            int? newLotId = null;

                            if (itemMaster != null && itemMaster.FifoLot == true && isLotCheckEnabled)
                            {
                                var oldestLot = DB.Stock_lotDetail
                                    .Where(l => l.ItemID == up.RawItemID && l.CurrentQuantity >= up.Quantity &&
                                                l.IsDeleted == false && l.IsAvailable == true && l.IsReserved==false && l.CompanyID == CompanyID)
                                    .OrderBy(l => l.RecieptDateTime).ThenBy(l => l.id).FirstOrDefault();

                                if (oldestLot != null)
                                {
                                    newLotId = oldestLot.id;
                                    oldestLot.CurrentQuantity -= (decimal)up.Quantity;
                                    if (oldestLot.CurrentQuantity == 0) oldestLot.IsAvailable = false;
                                    allottedSerials.Add($"{itemMaster.ItemName}: {oldestLot.Lot_SerialNumber ?? "NA"}");
                                }
                            }

                            BOMVoucherline BVL = new BOMVoucherline
                            {
                                BOMVoucherid = (int)up.BomVoucherHeadid,
                                Categoryid = (int)up.Categoryid,
                                Subcategoryid = (int)up.Subcategoryid,
                                Finalitemid = (int)up.FinalItemID,
                                rawitemid = (int)up.RawItemID,
                                QuantityRequired = (decimal)up.Quantity,
                                ApprovedQuantity = (decimal)up.Quantity,
                                Stockapproved=0,
                                UOM=up.UOM,
                                Approvedby = userName,
                                Approveddate =System.DateTime.Now,
                                Isdeleted = 0,
                                LotID = newLotId
                            };
                            DB.BOMVoucherlines.Add(BVL);

                            var stockTable = DB.StockTables.FirstOrDefault(s => s.itemid == up.RawItemID && s.CompanyID == CompanyID);
                            if (stockTable != null) stockTable.Stock -= (decimal)up.Quantity;
                        }
                    }

                    requisitionHead.IsApproved = 1;
                    requisitionHead.ApprovedBy = userName;
                   // requisitionHead.ApprovedDate = DateTime.Now;

                    var voucherHead = DB.BOMVouchers.FirstOrDefault(vh => vh.BOMVoucherID == requisitionHead.BOMVoucherID);
                    if (voucherHead != null) voucherHead.FGProductID = requisitionHead.NewFGProductID;

                    DB.SaveChanges();
                    transaction.Commit();

                    string serialMsg = allottedSerials.Any() ? " Allotted Serial Numbers: " + string.Join(", ", allottedSerials) : "";
                    SendRequitionApprovalMail(voucherId);
                    return Json(new { success = true, msg = "Voucher Approved and BOM Synchronized." + serialMsg });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, msg = "Error: " + ex.Message });
                }
            }
        }
        public ActionResult ApproveReqisitionVoucher1(int voucherId)
        {
            try
            {
                var finyear = Session["Fin_Year"].ToString();
                int CompanyID = (int)Session["Company_ID"];
                string userName = Session["U_Name"].ToString();

                var update = (from ab in DB.BOMRequisitionHeads
                              where ab.id == voucherId
                              select ab).SingleOrDefault();

                if (update == null) return Json(new { success = false, msg = "Voucher not found." });

                // 1. Get the lists of actions
                var getremovedatalist = DB.BOMRequisitionLines
                    .Where(rd => rd.BomRequisitionHeadid == voucherId && rd.Action == "Delete").ToList();

                var getadditionlist = DB.BOMRequisitionLines
                    .Where(rd => rd.BomRequisitionHeadid == voucherId && rd.Action == "Add").ToList();

                // --- STEP 1: VIRTUAL STOCK VALIDATION ---
                // We need to calculate the NET change for each item to handle transfers 
                // (e.g., removing from Cat A and adding to Cat B)

                var netStockRequired = new List<string>();

                // Group additions by ItemID
                var addSummary = getadditionlist.GroupBy(x => x.RawItemID)
                    .Select(g => new { ItemID = g.Key, TotalAdd = g.Sum(x => x.Quantity) });

                foreach (var add in addSummary)
                {
                    // Calculate how much is being returned to stock for this specific item in this voucher
                    var qtyBeingReturned = getremovedatalist
                        .Where(x => x.RawItemID == add.ItemID)
                        .Sum(x => x.Quantity) ?? 0;

                    // Get current physical stock
                    var stockRecord = DB.StockTables.FirstOrDefault(s => s.itemid == add.ItemID && s.CompanyID == CompanyID);
                    decimal currentPhysicalStock = (decimal)(stockRecord != null ? stockRecord.Stock : 0);

                    // VIRTUAL CALCULATION: Physical Stock + Returns - New Additions
                    decimal virtualBalance = currentPhysicalStock + qtyBeingReturned - (add.TotalAdd ?? 0);

                    if (virtualBalance < 0)
                    {
                        var itemName = DB.BOMItemMasters.Where(it => it.Itemid == add.ItemID).Select(it => it.ItemName).FirstOrDefault();
                        netStockRequired.Add($"{itemName} (Shortfall: {Math.Abs(virtualBalance)})");
                    }
                }

                // --- STEP 2: BLOCK TRANSACTION IF SHORTFALL EXISTS ---
                if (netStockRequired.Any())
                {
                    return Json(new
                    {
                        success = false,
                        msg = "Insufficient Stock. Please review Current Stock.",
                        Negativeitems = netStockRequired
                    });
                }

                // --- STEP 3: EXECUTE UPDATES (Only reached if stock is valid) ---

                // Update Requisition Head
                update.IsApproved = 1;
                update.IsDeleted = 0;
                update.ApprovedBy = userName;
                DB.SaveChanges();

                // Remove records and return stock
                if (getremovedatalist != null && getremovedatalist.Any())
                {
                    foreach (var item in getremovedatalist)
                    {
                        var bomVoucherLine = DB.BOMVoucherlines.FirstOrDefault(x =>
                            x.BOMVoucherid == update.BOMVoucherID &&
                            x.Categoryid == item.Categoryid &&
                            x.Subcategoryid == item.Subcategoryid &&
                            x.Finalitemid == item.FinalItemID &&
                            x.rawitemid == item.RawItemID &&
                            x.Isdeleted == 0);

                        if (bomVoucherLine != null)
                        {
                            bomVoucherLine.Isdeleted = 1;
                        }

                        var stocktable = DB.StockTables.FirstOrDefault(s => s.itemid == item.RawItemID && s.CompanyID == CompanyID);
                        if (stocktable != null)
                        {
                            stocktable.Stock += (decimal)item.Quantity;
                        }
                        DB.SaveChanges();
                    }
                }

                // Add records and deduct stock
                if (getadditionlist != null && getadditionlist.Any())
                {
                    foreach (var up in getadditionlist)
                    {
                        BOMVoucherline BVL = new BOMVoucherline
                        {
                            BOMVoucherid = (int)up.BomVoucherHeadid,
                            Categoryid = (int)up.Categoryid,
                            Subcategoryid = (int)up.Subcategoryid,
                            Finalitemid = (int)up.FinalItemID,
                            rawitemid = (int)up.RawItemID,
                            QuantityRequired = (decimal)up.Quantity,
                            ApprovedQuantity = (decimal)up.Quantity,
                            Stockapproved = 0,
                            UOM = up.UOM,
                            Approvedby = userName,
                            Approveddate = System.DateTime.Now,
                            Isdeleted = 0
                        };
                        DB.BOMVoucherlines.Add(BVL);

                        var stocktable = DB.StockTables.FirstOrDefault(s => s.itemid == up.RawItemID && s.CompanyID == CompanyID);
                        if (stocktable != null)
                        {
                            stocktable.Stock -= (decimal)up.Quantity;
                        }
                        DB.SaveChanges();
                    }
                }

                // Update FG Product on main Voucher
                var voucherhead = DB.BOMVouchers.FirstOrDefault(vh => vh.BOMVoucherID == update.BOMVoucherID);
                if (voucherhead != null)
                {
                    voucherhead.FGProductID = update.NewFGProductID;
                    DB.SaveChanges();
                }

                // --- STEP 4: FINANCIAL YEAR CROSS-OVER LOGIC (Stock Return) ---
                var bom = DB.BOMVouchers.FirstOrDefault(x => x.BOMVoucherID == update.BOMVoucherID);
                if (bom != null && bom.Finyear != finyear)
                {
                    var billseries = DB.Bill_Series.FirstOrDefault(bb => bb.Type == "StockReturn" && bb.CompanyID == CompanyID && bb.Fin_Year == finyear);
                    if (billseries != null)
                    {
                        IEPLStockReturnHead SR = new IEPLStockReturnHead
                        {
                            Createdon = System.DateTime.Now,
                            Createdby = userName,
                            VoucherDate = System.DateTime.Now,
                            BomVoucherid = bom.BOMVoucherID,
                            CompanyID = CompanyID,
                            Fin_Year = finyear,
                            Departmentid = 15,
                            VoucherNumber = billseries.Series + billseries.Number
                        };
                        DB.IEPLStockReturnHeads.Add(SR);
                        DB.SaveChanges();

                        foreach (var lin in getremovedatalist)
                        {
                            IEPLStockReturnDetail SRL = new IEPLStockReturnDetail
                            {
                                Voucherid = SR.id,
                                Itemcodeid = (int)lin.RawItemID,
                                Remarks = "BOM Requisition Return of Other Fin Year",
                                Quantity = lin.Quantity,
                                ApprovedQuantity = lin.Quantity,
                                ApprovedStatus = 1,
                                ApprovedBy = userName,
                                ApprovedDate = System.DateTime.Now,
                                Voucher = "AutoApproved - Requisition",
                                RejectedQuantity = 0,
                                IsDeleted = 0
                            };
                            DB.IEPLStockReturnDetails.Add(SRL);
                        }
                        billseries.Number += 1;
                        DB.SaveChanges();
                    }
                }

              //  SendRequitionApprovalMail(voucherId);
                return Json(new { success = true, msg = "Voucher Approved Successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = "Error: " + ex.Message });
            }
        }

        public ActionResult StoreApproval()
        {
            var vouchers = (from ab in DB.BOMVouchers
                            where ab.Approvalstatus == 1
                            select ab).ToList();
            return View(vouchers);
        }

        // GET: GetBomVoucherDetails
        public JsonResult GetBomLineDetails()
        {
            var data = (from ab in DB.BOMVoucherlines
                        join bc in DB.BOMVouchers on ab.BOMVoucherid equals bc.BOMVoucherID
                        join cd in DB.BOMCategories on ab.Categoryid equals cd.CategoryID
                        join de in DB.BOMSubcategories on ab.Subcategoryid equals de.id
                        join gg in DB.BOMItemMasters on ab.Finalitemid equals gg.Itemid
                        join fg in DB.BOMItemMasters on ab.rawitemid equals fg.Itemid
                        join st in DB.BOMItemMasters on ab.rawitemid equals st.Itemid
                        where bc.Approvalstatus == 1 && (ab.QuantityRequired != ab.ApprovedQuantity)
                        //orderby cd.CategoryDesc
                        select new
                        {
                            lineid = ab.id,
                            Vouchernumber = bc.VoucherNumber,
                            VoucherDate = bc.VoucherDate,
                            Category = cd.CategoryDesc,
                            Subcategory = de.BOMSubCategory1,
                            FinalItemCode = gg.ItemCode,
                            RawItemCode = fg.ItemCode,
                            QuantityRequired = ab.QuantityRequired,
                            ApprovedQuantity = ab.ApprovedQuantity,
                            StockAvailable = st.Stock,
                            UOM = ab.UOM,
                        }).ToList();

            return Json(data, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public ActionResult SaveLineIssueStock(int lineId, decimal? quantity, string rawItemCode, decimal? currentstock, decimal? approvedstock)
        {
            try
            {
                decimal stock = (decimal)currentstock;
                // Get the line issue stock from the database
                var lineIssueStock = (from ab in DB.BOMVoucherlines
                                     where ab.id == lineId
                                     select ab).SingleOrDefault();

                BOMVoucherLine BL = new BOMVoucherLine();
                lineIssueStock.ApprovedQuantity=approvedstock + quantity;
                lineIssueStock.Approvedby= Session["U_Name"].ToString();
                lineIssueStock.Approveddate = System.DateTime.Now;
                

                //minus Stock 

                var stocktable = (from bb in DB.BOMItemMasters
                                 where bb.ItemCode == rawItemCode
                                 select bb).SingleOrDefault();
                stocktable.Stock = stock-quantity;

                //Voucherline Part Transations Details

                BOMVPartwiseApprove BVP = new BOMVPartwiseApprove();
                BVP.BOMVoucherlineid = (lineId);
                BVP.Approvedquantity = quantity;
                BVP.Createdby= Session["U_Name"].ToString();
                BVP.Datetime = System.DateTime.Now;
                DB.BOMVPartwiseApproves.Add(BVP);

                DB.SaveChanges();

                // Check if the line issue stock exists
                if (lineIssueStock == null)
                {
                    return Json(new { success = false, error = "Line issue stock not found." });
                }

                // Update the line issue stock with the approved quantity
               // lineIssueStock.ApprovedQuantity = // Get the approved quantity from the UI

                // Save the changes to the database
               // _context.SaveChanges();

                // Return a success message
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Log the exception
                //_logger.LogError(ex, "Error saving line issue stock.");

                // Return an error message
                return Json(new { success = false, error = "Error saving line issue stock." });
            }
        }
        public ActionResult BOMPlanning()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "BOMPlanning" && ab.Status == true
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
        public JsonResult GetMasterItems()
        {
            var masterItems = DB.BOMItemMasters
                .Where(ab => ab.ItemCategory == 1)
                .Select(ab => new SelectListItem
                {
                    Value = ab.Itemid.ToString(),
                    Text = ab.ItemName
                })
                .ToList();
            var Baseframe = (from ab in DB.BOMSubcategories
                             join bc in DB.BOMCategories on ab.BOMCategoryID equals bc.CategoryID
                             where bc.CategoryDesc == "Base Frame"
                             select new
                             {
                                 value = ab.id,
                                 text = ab.BOMSubCategory1
                             }).ToList();
            var DG = (from ab in DB.BOMSubcategories
                      join bc in DB.BOMCategories on ab.BOMCategoryID equals bc.CategoryID
                      where bc.CategoryDesc == "DG Type"
                      select new
                      {
                          value = ab.id,
                          text = ab.BOMSubCategory1
                      }).ToList();
            var Panel = (from ab in DB.BOMSubcategories
                         join bc in DB.BOMCategories on ab.BOMCategoryID equals bc.CategoryID
                         where bc.CategoryDesc == "Panel"
                         select new
                         {
                             value = ab.id,
                             text = ab.BOMSubCategory1
                         }).ToList();
            var Alternator = (from ab in DB.BOMSubcategories
                              join bc in DB.BOMCategories on ab.BOMCategoryID equals bc.CategoryID
                              where bc.CategoryDesc == "Alternator"
                              select new
                              {
                                  value = ab.id,
                                  text = ab.BOMSubCategory1
                              }).ToList();
            var canopy = (from ab in DB.BOMSubcategories
                          join bc in DB.BOMCategories on ab.BOMCategoryID equals bc.CategoryID
                          where bc.CategoryDesc == "Canopy"
                          select new
                          {
                              value = ab.id,
                              text = ab.BOMSubCategory1
                          }).ToList();
            var fueltank = (from ab in DB.BOMSubcategories
                            join bc in DB.BOMCategories on ab.BOMCategoryID equals bc.CategoryID
                            where bc.CategoryDesc == "Fuel Tank"
                            select new
                            {
                                value = ab.id,
                                text = ab.BOMSubCategory1
                            }).ToList();
            var exhaust = (from ab in DB.BOMSubcategories
                           join bc in DB.BOMCategories on ab.BOMCategoryID equals bc.CategoryID
                           where bc.CategoryDesc == "Exhaust System"
                           select new
                           {
                               value = ab.id,
                               text = ab.BOMSubCategory1
                           }).ToList();
            var acoustic = (from ab in DB.BOMSubcategories
                            join bc in DB.BOMCategories on ab.BOMCategoryID equals bc.CategoryID
                            where bc.CategoryDesc == "Acoustic Treatment"
                            select new
                            {
                                value = ab.id,
                                text = ab.BOMSubCategory1
                            }).ToList();
            var assembly = (from ab in DB.BOMSubcategories
                            join bc in DB.BOMCategories on ab.BOMCategoryID equals bc.CategoryID
                            where bc.CategoryDesc == "Assembly"
                            select new
                            {
                                value = ab.id,
                                text = ab.BOMSubCategory1
                            }).ToList();
            var final = (from ab in DB.BOMSubcategories
                         join bc in DB.BOMCategories on ab.BOMCategoryID equals bc.CategoryID
                         where bc.CategoryDesc == "Final Packing"
                         select new
                         {
                             value = ab.id,
                             text = ab.BOMSubCategory1
                         }).ToList();
            var electrical = (from ab in DB.BOMSubcategories
                              join bc in DB.BOMCategories on ab.BOMCategoryID equals bc.CategoryID
                              where bc.CategoryDesc == "Electrical"
                              select new
                              {
                                  value = ab.id,
                                  text = ab.BOMSubCategory1
                              }).ToList();
            return Json(new { Baseframe, DG, Panel, Alternator, masterItems, canopy, fueltank, exhaust, acoustic, assembly, final, electrical }, JsonRequestBehavior.AllowGet);
            //return Json(masterItems, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public ActionResult GenerateReport1(List<BOMInputModel> bomInputs)
        {
            var combinedBOM = new List<BOMItem>();
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());

            foreach (var input in bomInputs)
            {
                var getitemid = DB.BOMItemMasters.SingleOrDefault(ab => ab.ItemName == input.FinalItem)?.Itemid;

                if (getitemid != null)
                {
                    var BOM = (from bc in DB.BOMs
                               where bc.MasterItem == getitemid && (bc.Parentsub == input.BaseFrame || bc.Parentsub == input.DGType || bc.Parentsub == input.Alternator
                               || bc.Parentsub == input.Panel || bc.Parentsub == input.Canopy || bc.Parentsub == input.FuelTank || bc.Parentsub == input.Exhaust || bc.Parentsub == input.Acoustic
                               || bc.Parentsub == input.Packing || bc.Parentsub == input.Assembly || bc.Parentsub == input.Electrical)
                               join raw in DB.BOMItemMasters on bc.RawItem equals raw.Itemid                               
                               join master in DB.BOMItemMasters on bc.MasterItem equals master.Itemid
                               join cat in DB.BOMCategories on bc.ParentItem equals cat.CategoryID
                               join subcat in DB.BOMSubcategories on bc.Parentsub equals subcat.id
                               select new BOMItem
                               {
                                   FinalItem = master.ItemName,
                                   RawItemName = raw.ItemName,
                                   ItemCode = raw.ItemCode,
                                   Category = cat.CategoryDesc,
                                   subCategory = subcat.BOMSubCategory1,
                                   Quantity = (bc.Quantity ?? 0) * input.TotalQuantity,
                                   Stock = (raw.Stock ?? 0),
                                   UOM = bc.UOM,
                                   Minimumstocklevel = (decimal)raw.Minstklvl,
                                   MOQ = (decimal)raw.MinimumOrderQuantity
                               }).ToList();

                    combinedBOM.AddRange(BOM);
                }
            }

            // Aggregate quantities for the same raw items
            var aggregatedBOM = combinedBOM.GroupBy(b => new { b.RawItemName, b.ItemCode, b.Category, b.subCategory, b.UOM, b.Minimumstocklevel , b.MOQ })
                .Select(group => new BOMItem
                {
                    FinalItem = group.First().FinalItem,
                    RawItemName = group.Key.RawItemName,
                    ItemCode = group.Key.ItemCode,
                    Category = group.Key.Category,
                    subCategory = group.Key.subCategory,
                    Quantity = group.Sum(b => b.Quantity),
                    Stock = group.Sum(b => b.Stock),
                    UOM = group.Key.UOM,
                    Minimumstocklevel=group.Key.Minimumstocklevel,
                    MOQ=group.Key.MOQ,
                    
                })
                .ToList();
            //modification in stock table

            // Convert aggregated BOM to ReportItem format
           // var reportData = aggregatedBOM.Select(b => new ReportItem
           // {
           //     ItemCode = b.ItemCode,
           //     RawMaterial = b.RawItemName,
            //    TotalQuantityRequired =b.Quantity, // cast to int               
            //    AvailableStock = (DB.BOMItemMasters.FirstOrDefault(m => m.ItemCode == b.ItemCode)?.Stock ?? 0), // cast to int
            //    ShortageExcess = (DB.BOMItemMasters.FirstOrDefault(m => m.ItemCode == b.ItemCode)?.Stock ?? 0 - b.Quantity), // cast to int
             //   UOM =b.UOM, 
             //   MinimumStockLevel= b.Minimumstocklevel,
            //    MOQ= b.MOQ,
                
           // }).ToList();

            var reportData = aggregatedBOM.Select(b =>
            {
                var itemid = (DB.BOMItemMasters.FirstOrDefault(m => m.ItemCode == b.ItemCode)?.Itemid);
                return new ReportItem
                {
                    ItemCode = b.ItemCode,
                    RawMaterial = b.RawItemName,
                    TotalQuantityRequired = b.Quantity,
                    AvailableStock = (DB.StockTables.FirstOrDefault(s => s.itemid == itemid && s.CompanyID == companyid)?.Stock ?? 0),
                    ShortageExcess = (DB.StockTables.FirstOrDefault(s => s.itemid == itemid && s.CompanyID == companyid)?.Stock ?? 0) - b.Quantity,
                    UOM = b.UOM,                   
                    MinimumStockLevel = b.Minimumstocklevel,
                    MOQ = b.MOQ,
                 
                };
            }).ToList();

            // Group reportData by ItemCode and calculate sums
            var groupedReportData = reportData.GroupBy(r => r.ItemCode)
             .Select(g => new ReportItem
             {
                 ItemCode = g.Key,
                 RawMaterial = g.First().RawMaterial,         
                 TotalQuantityRequired = g.Sum(r => r.TotalQuantityRequired),
                 AvailableStock = g.First().AvailableStock,
                 ShortageExcess = g.First().AvailableStock- g.Sum(r => r.TotalQuantityRequired),
                 UOM = g.First().UOM,
                 MinimumStockLevel=g.First().MinimumStockLevel,
                 MOQ=g.First().MOQ,
                // ActualRequired= (g.First().MinimumStockLevel- g.First().AvailableStock)+ g.Sum(r => r.TotalQuantityRequired)

             })
             .ToList();

            return Json(groupedReportData, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public JsonResult GenerateReportold(List<BOMInputModel> bomInputs)
        {
            var combinedBOM = new List<BOMItem>();

            foreach (var input in bomInputs)
            {
                var getitemid = DB.BOMItemMasters.SingleOrDefault(ab => ab.ItemName == input.FinalItem)?.Itemid;

                if (getitemid != null)
                {
                    var BOM = (from bc in DB.BOMs
                               where bc.MasterItem == getitemid
                               join raw in DB.BOMItemMasters on bc.RawItem equals raw.Itemid
                               join master in DB.BOMItemMasters on bc.MasterItem equals master.Itemid
                               join cat in DB.BOMCategories on bc.ParentItem equals cat.CategoryID
                               join subcat in DB.BOMSubcategories on bc.Parentsub equals subcat.id
                               select new BOMItem
                               {
                                   FinalItem = master.ItemName,
                                   RawItemName = raw.ItemName,
                                   ItemCode = raw.ItemCode,
                                   Category = cat.CategoryDesc,
                                   subCategory = subcat.BOMSubCategory1,
                                   Quantity = (bc.Quantity ?? 0) * input.TotalQuantity,
                                   Stock = (raw.Stock ?? 0),
                                   UOM = bc.UOM
                                   
                               }).ToList();

                    combinedBOM.AddRange(BOM);
                }
            }

            // Aggregate quantities for the same raw items
            var aggregatedBOM = combinedBOM.GroupBy(b => new { b.RawItemName, b.ItemCode, b.Category, b.subCategory, b.UOM })
                .Select(group => new BOMItem
                {
                    FinalItem = group.First().FinalItem,
                    RawItemName = group.Key.RawItemName,
                    ItemCode = group.Key.ItemCode,
                    Category = group.Key.Category,
                    subCategory = group.Key.subCategory,
                    Quantity = group.Sum(b => b.Quantity),
                    Stock = group.First().Stock,
                    UOM = group.Key.UOM,
                    
                })
                .ToList();

            return Json(aggregatedBOM, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult LoadBOMData(int masterItemId)
        {
            try
            {
                var bomList = (from bc in DB.BOMs
                               where bc.MasterItem == masterItemId
                               join raw in DB.BOMItemMasters on bc.RawItem equals raw.Itemid
                               join master in DB.BOMItemMasters on bc.MasterItem equals master.Itemid
                               join cat in DB.BOMCategories on bc.ParentItem equals cat.CategoryID
                               join subcat in DB.BOMSubcategories on bc.Parentsub equals subcat.id
                               // Left join with DepartmentMaster
                               join dept in DB.DepartmentMasters on bc.DepartmentID equals dept.id into deptGroup
                               from dept in deptGroup.DefaultIfEmpty()
                               select new
                               {
                                   BOMid = bc.BOMid,
                                   FinalItem = master.ItemName,
                                   RawItemName = raw.ItemName,
                                   ItemCode = raw.ItemCode,
                                   Category = cat.CategoryDesc,
                                   subCategory = subcat.BOMSubCategory1,
                                   Quantity = bc.Quantity,
                                   UOM = bc.UOM,
                                   DepartmentID = bc.DepartmentID, // Current ID for dropdown binding
                                   DepartmentName = dept != null ? dept.DepartmentName : "Unassigned"
                               }).ToList();

                return Json(bomList, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult EditBOMLink()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "EditBOMLink" && ab.Status == true
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
        [HttpPost]
        public JsonResult DeleteBOMItem(int bomId)
        {
            try
            {
                // Server-side Permission Check
                bool canDelete = (Session["U_Role"]?.ToString() == "ADMIN" && Session["EndCustomer"]?.ToString() == "ADMIN") || (Session["U_Role"]?.ToString() == "SUPERADMIN");
                if (!canDelete) return Json(new { success = false, message = "Access Denied: You do not have permission to delete BOM records." });

                var record = DB.BOMs.FirstOrDefault(x => x.BOMid == bomId);
                if (record == null) return Json(new { success = false, message = "Record not found." });

                DB.BOMs.Remove(record);
                DB.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        public ActionResult BOMCancellation()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "BOMCancellation" && ab.Status == true
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
        [HttpPost]
        public ActionResult CancelBOM(int BOMVoucherID, string Remarks)
        {
            var selecthead = DB.BOMVouchers.Where(x => x.BOMVoucherID == BOMVoucherID).SingleOrDefault();
            //Update Deleted Status in Head Then In Lines
            selecthead.Isdeleted = 1;
            selecthead.CancelledRemarks = Remarks;
            selecthead.CancelledOn = System.DateTime.Now;
            selecthead.CancelledBy = Session["U_Name"].ToString();


            var selectlines = DB.BOMVoucherlines.Where(y => y.BOMVoucherid == BOMVoucherID).ToList();
            if (selectlines.Count > 0)
            {
                foreach (var data in selectlines)
                {
                    data.Isdeleted = 1;
                }
                DB.SaveChanges();

            }
            return Json(new{ success= true}, JsonRequestBehavior.AllowGet);

        }
        [HttpPost]
        public JsonResult UpdateBOMQuantity(int bomId, decimal quantity)
        {
            try
            {
                // Server-side Permission Check
                bool canEdit = (Session["U_Role"]?.ToString() == "ADMIN" && Session["EndCustomer"]?.ToString() == "ADMIN") || (Session["U_Role"]?.ToString() == "SUPERADMIN");
                if (!canEdit) return Json(new { success = false, message = "Access Denied: You do not have permission to edit BOM quantities." });

                var record = DB.BOMs.FirstOrDefault(x => x.BOMid == bomId);
                if (record == null) return Json(new { success = false, message = "Linkage record not found." });

                record.Quantity = (decimal?)(double)quantity;
                DB.SaveChanges();

                return Json(new { success = true, message = "Quantity updated successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        [HttpPost]
        public JsonResult UpdateBOMDepartment(int bomId, int deptId)
        {
            try
            {
                var record = DB.BOMs.FirstOrDefault(x => x.BOMid == bomId);
                if (record == null) return Json(new { success = false, message = "Linkage record not found." });

                record.DepartmentID = deptId > 0 ? deptId : (int?)null;
                DB.SaveChanges();

                return Json(new { success = true, message = "Department mapping updated." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public ActionResult BOMIndentVoucher(List<IndentDataModel> indentData) // <-- Change the parameter type
        {
            if (indentData == null || indentData.Count == 0) // Check if the data is null or empty
            {
                return Json(new { success = false, message = "No data received" });
            }

            try
            {
                int totalitems = indentData.Count();
                //Save BOM Indent Head
                BOMIndentHead BIH = new BOMIndentHead();
                
                var Voucher = "BIV-";

                // Let the database generate a unique ID
                BIH.id = 0; // or default value for your ID type

                BIH.TotalItems = totalitems;
                BIH.CreatedBy = Session["U_Name"].ToString();
                BIH.CreatedOn = System.DateTime.Now;
                BIH.Isclosed = 0;
                BIH.ApprovalStatus = 0;

                DB.BOMIndentHeads.Add(BIH);
                DB.SaveChanges(); // Save changes to generate the ID

                // Retrieve the latest ID
                var lastid = BIH.id;

                // Update the voucher number
                BIH.VoucherNumber = Voucher + lastid.ToString();

                // Update the entity
                //DB.Entry(BIH).State = EntityState.Modified;

                // Save the changes again
                DB.SaveChanges();

                using (var dbContext = new IECEntities())
                {
                    foreach (var item in indentData)
                    {
                        var itemid = DB.BOMItemMasters.FirstOrDefault(x => x.ItemCode == item.ItemCode);
                        dbContext.BomIndentLines.Add(new BomIndentLine
                        {
                            Headid = lastid,
                            Itemid=itemid.Itemid,
                            TotalQuantityRequired=item.TotalQuantityRequired,
                            ShortQuantity = Math.Abs(item.ShortageExcess),
                            ActualRequired=item.ActualRequirement,
                            CreatedBy= Session["U_Name"].ToString(),
                            CreatedOn=System.DateTime.Now,
                            IsApproved=0,
                            IsIndentRaised=0,

                    }) ;
                    }
                    dbContext.SaveChanges();
                }

                return Json(new
                {
                    success = true,
                    message = "Indent voucher generated successfully!",
                    voucherNumber = BIH.VoucherNumber,
                    totalitems=totalitems
                   
                });
            }
            catch (Exception ex)
            {
                // Log the exception for debugging
                return Json(new { success = false, message = "Error generating indent voucher: " + ex.Message });
            }
        }

        public ActionResult BOMIndentApproval()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "BOMIndentApproval" && ab.Status == true
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
        public ActionResult SAdminBOMIndentApproval()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "SAdminBOMIndentApproval" && ab.Status == true
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

        public ActionResult GetBOMIndents()
        {
            // Retrieve vouchers from database or data source
            var finyear = Session["Fin_Year"].ToString();
            int CompanyID = (int)Session["Company_ID"];
            var list = (from ab in DB.BOMIndentHeads
                        where ab.ApprovalStatus == 0 && ab.CompanyID==CompanyID
                        select ab).ToList();

            return Json(list, JsonRequestBehavior.AllowGet);
        }
        public ActionResult HoldIndent(int itemId, string remarks)
        {
            var indent = DB.BOMIndentHeads.Find(itemId);

            if (indent != null)
            {
                indent.Onhold = 1;
                indent.HoldRemarks = remarks;
                indent.Holddatetime = System.DateTime.Now;
                indent.Holdby = Session["U_Name"].ToString();
                DB.SaveChanges();
                var subject = "Indent Order is on Hold | Order Number - ";
                var to = "IndentHoldTo";
                var cc = "IndentHoldCC";
                int headId = itemId;
                SendIndentEmail(headId, subject, to, cc,remarks);
                return Json(new { success = true });
            }
            else
            {
                return Json(new { success = false, error = "Indent not found" });
            }
        }
        public ActionResult GetBOMIndentsSeniorAdmin()
        {
            // Retrieve vouchers from database or data source
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());

            var list = (from ab in DB.BOMIndentHeads
                        where ab.ApprovalStatus == 1 && ab.SAdminApproval==0 && ab.CompanyID==companyid
                        select new
                        {
                            id = ab.id,
                            VoucherNumber = ab.VoucherNumber,
                            TotalItems = ab.TotalItems,
                            CreatedBy = ab.CreatedBy,
                            CreatedOn = ab.CreatedOn,                            
                            Onhold = ab.Onhold
                        }).ToList();

            return Json(list, JsonRequestBehavior.AllowGet);
        }
        
        public ActionResult GetIndentLines(int headId)
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            // Retrieve the item lines for the given head ID

            var itemLines = (from bb in DB.BomIndentLines
                             join sup in DB.SupplierMasters on bb.PreviousSupplierid equals sup.id into sups
                             from sup in sups.DefaultIfEmpty()
                             join item in DB.BOMItemMasters on bb.Itemid equals item.Itemid into items
                             from item in items.DefaultIfEmpty()
                             join UOM in DB.BOM_UOM on item.UOM equals UOM.id into UOMs
                             from UOM in UOMs.DefaultIfEmpty()
                             join stk in DB.StockTables on bb.Itemid equals stk.itemid into stocks
                             from stk in stocks.DefaultIfEmpty()
                             where bb.Headid == headId  && stk.CompanyID == companyid && bb.IsApproved==0 && bb.Rejected==0
                             select new
                             {
                                 id=bb.id,
                                 ItemName = item != null ? item.ItemName : "NA", // Ternary operator fix
                                 ItemCode = item != null ? item.ItemCode : "NA",
                                 UOM = UOM != null ? UOM.UOM : "NA",
                                 Quantity = bb.TotalQuantityRequired,
                                 LastOrderDate = bb.LastOrderDate,
                                 LastOrderQuantity = bb.LastOrderQuantity,
                                 LastSupplier = sup != null ? sup.SupplierName : "NA",
                                 LastPrice = bb.LastBasicRate,
                                 AvailableStock = bb.AvailableStock,
                                 MOQ = stk.MOQ,
                                 MinimumLevel = stk.Minstklvl,
                                 Average=bb.AverageMonthlyConsumption,
                                 ExpectedStkDate=bb.ExpectedStkEndDate,
                                 HeadId= headId,
                                 userremarks=bb.UserRemarks,
                             }).ToList();

            return Json(itemLines, JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetIndentLinesAccepted(int headId)
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            // Retrieve the item lines for the given head ID

            var itemLines = (from bb in DB.BomIndentLines
                             join sup in DB.SupplierMasters on bb.PreviousSupplierid equals sup.id into sups
                             from sup in sups.DefaultIfEmpty()
                             join item in DB.BOMItemMasters on bb.Itemid equals item.Itemid into items
                             from item in items.DefaultIfEmpty()
                             join UOM in DB.BOM_UOM on item.UOM equals UOM.id into UOMs
                             from UOM in UOMs.DefaultIfEmpty()
                             join stk in DB.StockTables on bb.Itemid equals stk.itemid into stocks
                             from stk in stocks.DefaultIfEmpty()
                             where bb.Headid == headId && stk.CompanyID == companyid && bb.IsApproved==1 && bb.Rejected==0
                             select new
                             {
                                 id = bb.id,
                                 ItemName = item != null ? item.ItemName : "NA", // Ternary operator fix
                                 ItemCode = item != null ? item.ItemCode : "NA",
                                 UOM = UOM != null ? UOM.UOM : "NA",
                                 Quantity = bb.TotalQuantityRequired,
                                 LastOrderDate = bb.LastOrderDate,
                                 LastOrderQuantity = bb.LastOrderQuantity,
                                 LastSupplier = sup != null ? sup.SupplierName : "NA",
                                 LastPrice = bb.LastBasicRate,
                                 AvailableStock = bb.AvailableStock,
                                 MOQ = stk.MOQ,
                                 MinimumLevel = stk.Minstklvl,
                                 Average = bb.AverageMonthlyConsumption,
                                 ExpectedStkDate = bb.ExpectedStkEndDate,
                             }).ToList();

            return Json(itemLines, JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetIndentLinesRejected(int headId)
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            // Retrieve the item lines for the given head ID

            var itemLines = (from bb in DB.BomIndentLines
                             join sup in DB.SupplierMasters on bb.PreviousSupplierid equals sup.id into sups
                             from sup in sups.DefaultIfEmpty()
                             join item in DB.BOMItemMasters on bb.Itemid equals item.Itemid into items
                             from item in items.DefaultIfEmpty()
                             join UOM in DB.BOM_UOM on item.UOM equals UOM.id into UOMs
                             from UOM in UOMs.DefaultIfEmpty()
                             join stk in DB.StockTables on bb.Itemid equals stk.itemid into stocks
                             from stk in stocks.DefaultIfEmpty()
                             where bb.Headid == headId && stk.CompanyID == companyid && bb.IsApproved == 0 && bb.Rejected == 1
                             select new
                             {
                                 id = bb.id,
                                 ItemName = item != null ? item.ItemName : "NA", // Ternary operator fix
                                 ItemCode = item != null ? item.ItemCode : "NA",
                                 UOM = UOM != null ? UOM.UOM : "NA",
                                 Quantity = bb.TotalQuantityRequired,
                                 LastOrderDate = bb.LastOrderDate,
                                 LastOrderQuantity = bb.LastOrderQuantity,
                                 LastSupplier = sup != null ? sup.SupplierName : "NA",
                                 LastPrice = bb.LastBasicRate,
                                 AvailableStock = bb.AvailableStock,
                                 MOQ = stk.MOQ,
                                 MinimumLevel = stk.Minstklvl,
                                 Average = bb.AverageMonthlyConsumption,
                                 ExpectedStkDate = bb.ExpectedStkEndDate,
                             }).ToList();

            return Json(itemLines, JsonRequestBehavior.AllowGet);
        }
        public ActionResult ApproveBOMIndent(int headId)
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var update = (from ab in DB.BOMIndentHeads
                          where ab.id == headId && ab.CompanyID==companyid
                          select ab).SingleOrDefault();
            BOMVoucher BV = new BOMVoucher();
            update.ApprovalStatus = 1;
            update.ApprovedBy= Session["U_Name"].ToString();
            update.ApprovedDate = System.DateTime.Now;
            update.SAdminApproval = 0;
            update.Onhold = 0;


            //Update Lines
            var Linesupdate = (from bb in DB.BomIndentLines
                              where bb.Headid==headId
                              select bb).ToList();
            foreach (var u in Linesupdate)
            {
                u.IsApproved = 0;

            }
            DB.SaveChanges();
            var subject = "Indent Order Pending For Management Approval | Order Number - ";
            var to = "IndentToSAdmin";
            var cc = "IndentCCSAdmin";
            var remark = "";
            SendIndentEmail(headId,subject,to,cc,remark);

            return Json(new { success = true, msg = "Voucher Approved" });

        }
        public ActionResult ApproveSAdminBOMIndent(int headId)
        {
            var update = (from ab in DB.BOMIndentHeads
                          where ab.id == headId
                          select ab).SingleOrDefault();
            BOMVoucher BV = new BOMVoucher();
            update.SAdminApproval = 1;
            update.SAdminApprovalBy = Session["U_Name"].ToString();
            update.SAdminApprovalDate = System.DateTime.Now;

            //Update Lines
            var Linesupdate = (from bb in DB.BomIndentLines
                               where bb.Headid == headId
                               select bb).ToList();
            foreach (var u in Linesupdate)
            {
                u.IsApproved = 1;

            }
            DB.SaveChanges();
            // var pdfResult = IndentPDF(headId) as ViewAsPdf;
            //byte[] pdfBytes = pdfResult.BuildPdf(this.ControllerContext);
            var subject = "Indent Order Has Been Approved | Order Number - ";
            var to = "IndentTo";
            var cc = "IndentCC";
            var remarks = "";
            SendIndentEmail(headId, subject, to, cc, remarks);

            return Json(new { success = true, msg = "Voucher Approved" });

        }
        public ActionResult UpdateIndentPartial(int headId)
        {
            var update = (from ab in DB.BOMIndentHeads
                          where ab.id == headId
                          select ab).SingleOrDefault();
            BOMVoucher BV = new BOMVoucher();
            update.SAdminApproval = 1;
            update.SAdminApprovalBy = Session["U_Name"].ToString();
            update.SAdminApprovalDate = System.DateTime.Now;
            
            DB.SaveChanges();
            // var pdfResult = IndentPDF(headId) as ViewAsPdf;
            //byte[] pdfBytes = pdfResult.BuildPdf(this.ControllerContext);
            var subject = "";
            var to = "IndentTo";
            var cc = "IndentCC";
            var remarks = "";
            //First Send Email of Approved Items
           
            //Second Send Email of Unapproved Items if Any
            var unapproved = (from ab in DB.BomIndentLines
                              where ab.Headid == headId && ab.Rejected == 1 && ab.IsApproved == 0
                              select ab.id).ToList();
            if (unapproved.Count > 0)
            {
                subject = "Indent Order Has Been Approved But Contains Rejected Items | Order Number - ";
                SendIndentEmail(headId, subject, to, cc, remarks);
            }

            else
            {
                subject = "Indent Order Has Been Approved | Order Number -";
                SendIndentEmail(headId, subject, to, cc, remarks);
            }


            return Json(new { success = true, msg = "Voucher Approved" });

        }
        
        public ActionResult ApproveIndentItemWise(int itemid)
        {
            var selectitem = DB.BomIndentLines.Where(x => x.id == itemid).SingleOrDefault();
            var headId = selectitem.Headid;
            selectitem.IsApproved = 1;
            selectitem.Rejected = 0;
            DB.SaveChanges();

            return Json(new { success = true, data = "Item Has Been Approved", headId }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult RejectIndentItem(int itemId, string remarks)
        {
            var selectitem = DB.BomIndentLines.Where(x => x.id == itemId).SingleOrDefault();
            var headId = selectitem.Headid;
            selectitem.IsApproved = 0;
            selectitem.Rejected = 1;
            selectitem.Remarks = remarks;
            DB.SaveChanges();

            return Json(new { success = true, data = "Item Has Been Approved", headId }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult IndentPDF(int headId)
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var imagePath = "";
            var simagePath = "";
            var Signatory = "";
            var SignatoryName = "";
            var SignatoryEmail = "";
            var SAdminSignature = "";
            var SAdminName = "";
            var SAdminEmail = "";
            //Check if Order is Approved
            var check = (from ab in DB.BOMIndentHeads
                         where ab.id == headId
                         select ab).SingleOrDefault();
            if (check.ApprovalStatus == 1)
            {
                imagePath = "~/Images/sign-planthead.png";
            }
            if (check.SAdminApproval == 1)
            {
                if (check.SAdminApprovalBy == "Ajay")
                {
                    simagePath = "~/Images/SAdminSign.png";
                }
                else if (check.SAdminApprovalBy == "SatishGoel")
                {
                    simagePath = "~/Images/S1AdminSign.png";
                }
            }
            // 1. Fetch PO Head DataGetPrintDataMRN

            var IndentHeadData = (from ab in DB.BOMIndentHeads

                                  where ab.id == headId
                                  select new
                                  {
                                      Image = imagePath,
                                      Signatory = Signatory,
                                      SignatoryName = SignatoryName,
                                      SignatoryEmail = SignatoryEmail,
                                      IndentNumber = ab.VoucherNumber,
                                      IndentDate = ab.CreatedOn,
                                      TotalQuantity = ab.TotalItems,
                                      CreatedBy = ab.CreatedBy,
                                      ApprovedBy = ab.ApprovedBy,
                                      Headid = ab.id,
                                      ImagePath = imagePath,
                                      simagepath = simagePath,
                                  }).SingleOrDefault();
            var IndentItemDetail = (from bb in DB.BomIndentLines
                                    join sup in DB.SupplierMasters on bb.PreviousSupplierid equals sup.id into sups
                                    from sup in sups.DefaultIfEmpty()
                                    join item in DB.BOMItemMasters on bb.Itemid equals item.Itemid into items
                                    from item in items.DefaultIfEmpty()
                                    join UOM in DB.BOM_UOM on item.UOM equals UOM.id into UOMs
                                    from UOM in UOMs.DefaultIfEmpty()
                                    join stk in DB.StockTables on bb.Itemid equals stk.itemid into stocks
                                    from stk in stocks.DefaultIfEmpty()
                                    where bb.Headid == headId && stk.CompanyID == companyid
                                    select new IndentItem
                                    {
                                        ItemName = item != null ? item.ItemName : "NA", // Ternary operator fix
                                        ItemCode = item != null ? item.ItemCode : "NA",
                                        UOM = UOM != null ? UOM.UOM : "NA",
                                        Quantity = bb.TotalQuantityRequired,
                                        LastOrderDate = bb.LastOrderDate,
                                        LastOrderQuantity = bb.LastOrderQuantity,
                                        LastSupplier = sup != null ? sup.SupplierName : "NA",
                                        LastPrice = bb.LastBasicRate,
                                        AvailableStock = bb.AvailableStock,
                                        MOQ = stk.MOQ,
                                        MinimumLevel = stk.Minstklvl,
                                        Average = bb.AverageMonthlyConsumption,
                                    }).ToList();

            var viewModel = new IndentViewModel
            {
                IndentHeadData = IndentHeadData,
                IndentItemDetail = IndentItemDetail
            };

            string htmlContent = RenderRazorViewToString("~/Views/Purchase/IndentPrint.cshtml", viewModel);

            // Extract the content of the printContent function
            string printContentFunction = @"
        function printContent() {
            var printWindow = window.open('', '', 'height=794,width=612'); 
            printWindow.document.write('<html><head><title></title></head><body>');
            printWindow.document.write(document.querySelector('.setting').innerHTML);
            printWindow.document.write('</body></html>');
            printWindow.document.close();
            printWindow.print();
        }
    ";

            // Inject the printContent function into the HTML
            htmlContent = htmlContent.Replace("</body>", $"<script>{printContentFunction}</script></body>");

            // Generate the PDF using the modified HTML content
            var pdf = new ViewAsPdf
            {
                FileName = "Indent_" + IndentHeadData.IndentNumber + ".pdf",
                ViewName = "~/Views/Purchase/IndentPrint.cshtml",
                Model = viewModel,
                CustomSwitches = "--enable-local-file-access",
               // HtmlToPdfPartial = htmlContent
            };

            byte[] pdfBytes = pdf.BuildPdf(this.ControllerContext);


            string folderPath = Server.MapPath("~/App_Data/");
            string filePath = Path.Combine(folderPath, "IndentPDFs");

            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
            }

            filePath = Path.Combine(filePath, "Indent_" + IndentHeadData.IndentNumber);

            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
            }

            // ... and so on for other levels, if necessary ...

            filePath = Path.Combine(filePath, "9.pdf");

            System.IO.File.WriteAllBytes(filePath, pdfBytes);

            return pdf;
        }
        private string RenderRazorViewToString(string viewName, object model)
        {
            ViewData.Model = model;
            using (var sw = new StringWriter())
            {
                var viewResult = ViewEngines.Engines.FindPartialView(ControllerContext, viewName);
                var viewContext = new ViewContext(ControllerContext, viewResult.View, ViewData, TempData, sw);
                viewResult.View.Render(viewContext, sw);
                viewResult.ViewEngine.ReleaseView(ControllerContext, viewResult.View);
                return sw.GetStringBuilder().ToString();
            }
        }
        private void SendRequitionApprovalMail(int voucherId)
        {
            var finyear = Session["Fin_Year"].ToString();
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            try
            {
                // 1. Fetch the necessary data 
                var RequsitionHead = DB.BOMRequisitionHeads.Where(ih => ih.id == voucherId).SingleOrDefault();
                var RequsitionLines = (from bb in DB.BOMRequisitionLines
                                       join im in DB.BOMItemMasters on bb.RawItemID equals im.Itemid into imgroup
                                       from im in imgroup.DefaultIfEmpty()
                                       
                                       where bb.BomRequisitionHeadid == voucherId
                                       select new
                                       {
                                           ItemCode=im.ItemCode,
                                           ItemName=im.ItemName,
                                           Quantity=bb.Quantity,
                                           UOM=bb.UOM,
                                           Action=bb.Action,

                                       }).ToList();               

                if (RequsitionHead == null)
                {
                    // Handle the case where the dispatch detail is not found
                    return;
                }
                // 2. Create the data model for the template
                //Get BOM Details
                var bom = DB.BOMVouchers.Where(x => x.BOMVoucherID == RequsitionHead.BOMVoucherID).SingleOrDefault();
                var model = new
                {
                    RequsitionHead = new
                    {
                        RequisitionNumber = RequsitionHead.BOMReqeusitionNo,
                        RequisitionDate = RequsitionHead.Createdon,
                        CreatedBy = RequsitionHead.Createdby,
                        ApprovedBy = RequsitionHead.ApprovedBy,
                        BOMNumber =bom.VoucherNumber,
                        BOMDate=bom.VoucherDate,
                        Finyear=bom.Finyear,
                        
                    },
                    RequsitionLines
                };

                var email = (from ab in DB.CMKL_Email_Setting
                             where ab.id == 2
                             select ab).SingleOrDefault();
                var emailaddress = (from db in DB.CMKL_Email
                                    where db.DDLName == "RequsitionApproval"
                                    select db).ToList();
                var emailsender = (from se in DB.CMKL_Email
                                   where se.DDLName == "ERPSender"
                                   select se).FirstOrDefault();

                MailMessage mail = new MailMessage();
                SmtpClient SmtpServer = new SmtpClient(email.smtp);
                mail.From = new MailAddress(emailsender.Email);
                foreach (var recipient in emailaddress)
                {
                    mail.To.Add(recipient.Email);
                }
                // Add CC recipients
                var ccRecipients = (from db in DB.CMKL_Email // Replace with your CC logic
                                    where db.DDLName == "RequsitionApprovalCC" // Example: Get CC emails
                                    select db).ToList();
                foreach (var ccRecipient in ccRecipients)
                {
                    mail.CC.Add(ccRecipient.Email);
                }
                var Subject = "Requsition Has Been Approved for - " + model.RequsitionHead.RequisitionNumber;
                //Check FinYear of BOM For Subject
              //  if (model.RequsitionHead.Finyear == finyear)
               // {
               //     Subject = "Requsition Has Been Approved for - " + model.RequsitionHead.RequisitionNumber;

               // }
               // else
               // {
               //     Subject="Stock Return Created for Deleted Items as BOM is of Another Fin Year - " + model.RequsitionHead.RequisitionNumber;
               // }
                mail.Subject = Subject;

                // 3. Get the Razor template
                var templatePath = Server.MapPath("~/Views/EmailManage/RequisitionApprovalEmailTemplate.cshtml"); // Verify path
                var template = System.IO.File.ReadAllText(templatePath);

                // 4. Render the template using RazorEngine
                var body = Razor.Parse(template, model, null, null);
                

                // 5. Set the email body
                mail.Body = body;
                mail.IsBodyHtml = true;                

                SmtpServer.Port = Convert.ToInt32(email.port);
                SmtpServer.Credentials = new System.Net.NetworkCredential(emailsender.Email, email.IT_password);
                SmtpServer.EnableSsl = Convert.ToBoolean(email.ssl);

                SmtpServer.Send(mail);
            }
            catch (Exception ex)
            {
                // ... (Your existing error handling for email sending) ...
            }
        }

        private void SendIndentEmail(int headId, string subject, string to , string cc, string remarks)
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            try
            {
                var company= DB.Companies.Where(x=>x.CompanyID==companyid).Select(x=>x.CompanyName).FirstOrDefault();
                // 1. Fetch the necessary data 
                var IndentHead = DB.BOMIndentHeads.Where(ih => ih.id == headId).SingleOrDefault();
                var IndentLines = (from bb in DB.BomIndentLines
                                   join sup in DB.SupplierMasters on bb.PreviousSupplierid equals sup.id into sups
                                   from sup in sups.DefaultIfEmpty()
                                   join item in DB.BOMItemMasters on bb.Itemid equals item.Itemid into items
                                   from item in items.DefaultIfEmpty()
                                   join UOM in DB.BOM_UOM on item.UOM equals UOM.id into UOMs
                                   from UOM in UOMs.DefaultIfEmpty()
                                   join stk in DB.StockTables on bb.Itemid equals stk.itemid into stocks
                                   from stk in stocks.DefaultIfEmpty()
                                   where bb.Headid == headId && stk.CompanyID == companyid// Assuming you have companyId
                                   //let Status = GetStatus((int)bb.IsApproved, (int)bb.Rejected)
                                   select new   // Assuming you have this ViewModel
                                   {
                                       //CompanyName=company,
                                       ItemName = item != null ? item.ItemName : "NA",
                                       ItemCode = item != null ? item.ItemCode : "NA",
                                       UOM = UOM != null ? UOM.UOM : "NA",
                                       Quantity = bb.TotalQuantityRequired,
                                       LastOrderDate = bb.LastOrderDate,
                                       LastOrderQuantity = bb.LastOrderQuantity,
                                       LastSupplier = sup != null ? sup.SupplierName : "NA",
                                       LastPrice = bb.LastBasicRate,
                                       AvailableStock = bb.AvailableStock,
                                       MOQ = stk != null ? stk.MOQ : 0,
                                       MinimumLevel = stk != null ? stk.Minstklvl : 0,
                                       Average = bb.AverageMonthlyConsumption,
                                       id = bb.Itemid,
                                       IsApproved = bb.IsApproved,  // Include IsApproved
                                       IsRejected = bb.Rejected,
                                       // Status= GetStatus(bb.IsApproved,bb.Rejected),
                                       // Status = bb.IsApproved == 0 ? "Item Rejected" : "Item Approved",
                                       Remarks = bb.Remarks == null ? "NA" : bb.Remarks,
                                   }).ToList();

                if (IndentHead == null)
                {
                    // Handle the case where the dispatch detail is not found
                    return;
                }
                // 2. Create the data model for the template
                var model = new 
                {
                    IndentHead = new
                    {
                        CompanyName = company,
                        IndentNumber = IndentHead.VoucherNumber,
                        IndentDate = IndentHead.CreatedOn,                        
                        CreatedBy = IndentHead.CreatedBy,
                        ApprovedBy = IndentHead.ApprovedBy,
                        SAdminApproval = IndentHead.SAdminApprovalBy ?? "NA",
                        HoldBy = IndentHead.Holdby ?? "NA",
                        TotalQuantity =IndentHead.TotalItems,                        
                    },
                    IndentLines
                };

                var email = (from ab in DB.CMKL_Email_Setting
                             where ab.id == 2
                             select ab).SingleOrDefault();
                var emailaddress = (from db in DB.CMKL_Email
                                    where db.DDLName == to && db.CompanyID==companyid
                                    select db).ToList();
                var emailsender = (from se in DB.CMKL_Email
                                   where se.DDLName == "ERPSender"
                                   select se).FirstOrDefault();

                MailMessage mail = new MailMessage();
                SmtpClient SmtpServer = new SmtpClient(email.smtp);
                mail.From = new MailAddress(emailsender.Email);
                foreach (var recipient in emailaddress)
                {
                    mail.To.Add(recipient.Email);
                }
                // Add CC recipients
                var ccRecipients = (from db in DB.CMKL_Email // Replace with your CC logic
                                    where db.DDLName == cc && db.CompanyID == companyid // Example: Get CC emails
                                    select db).ToList();
                foreach (var ccRecipient in ccRecipients)
                {
                    mail.CC.Add(ccRecipient.Email);
                }
                mail.Subject = subject + model.IndentHead.IndentNumber;

                // 3. Get the Razor template
                var templatePath = Server.MapPath("~/Views/EmailManage/IndentEmailTemplate.cshtml"); // Verify path
                var template = System.IO.File.ReadAllText(templatePath);
                
                // 4. Render the template using RazorEngine
                var body = Razor.Parse(template, model, null, null);
                if (remarks != "")
                {
                    var additionalContent = "<p>Need to Discuss -" + remarks + "</p>";
                    body = additionalContent + body;
                }               

                // 5. Set the email body
                mail.Body = body;
                mail.IsBodyHtml = true;
                foreach (var line in IndentLines)
                {
                    var itemMaster = DB.BOMItemMasters.FirstOrDefault(im => im.Itemid == line.id);
                    if (itemMaster != null && itemMaster.DrawingData != null)
                    {
                        string fileName = $"{itemMaster.ItemCode}_{itemMaster.Itemid}.pdf"; // Or appropriate extension
                        MemoryStream stream = new MemoryStream(itemMaster.DrawingData);
                        mail.Attachments.Add(new Attachment(stream, fileName));
                    }
                }

                SmtpServer.Port = Convert.ToInt32(email.port);
                SmtpServer.Credentials = new System.Net.NetworkCredential(emailsender.Email, email.IT_password);
                SmtpServer.EnableSsl = Convert.ToBoolean(email.ssl);

                SmtpServer.Send(mail);
            }
            catch (Exception ex)
            {
                // ... (Your existing error handling for email sending) ...
            }
        }
        private string GetStatus(int? isApproved, int? Rejected)
        {
            int App = Convert.ToInt32(isApproved);
            int Rej = Convert.ToInt32(Rejected);
            if (App == 1 && Rej ==0 )
            {
                return "Item Approved";
            }
            else if (App == 0 && Rej == 0)
            {
                return "NA";
            }
            else if (App == 0 && Rej == 1)
            {
                return "Item Rejected";
            }
            else // Handle any other cases (e.g., isApproved == 0 and isRejected is something else)
            {
                return "Unknown Status"; // Or throw an exception, or handle as needed
            }
        }
        public ActionResult BOMlinkandItemDetail()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "BOMlinkandItemDetail" && ab.Status == true
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

        public ActionResult SearchItem(string itemCode)
        {
            // Search for item information
            var itemInfo = DB.BOMItemMasters.Where(x => x.ItemCode == itemCode).SingleOrDefault();
            if (itemInfo == null)
            {
                return Json(new { success = false, error = "Item Not Found" }, JsonRequestBehavior.AllowGet);
            }
            // Check if itemInfo is not null
            if (itemInfo == null)
            {
                return Json(new { itemInfo = new object(), rawItemInfo = new List<object>() }, JsonRequestBehavior.AllowGet);
            }

            // Search for raw item information
            //var rawItemInfo = DB.BOMs.Where(x => x.RawItem == itemInfo.Itemid).ToList();
            var rawItemInfo = (from ab in DB.BOMs
                               join master in DB.BOMItemMasters on ab.MasterItem equals master.Itemid
                               join cat in DB.BOMCategories on ab.ParentItem equals cat.CategoryID
                               join subcat in DB.BOMSubcategories on ab.Parentsub equals subcat.id
                               where ab.RawItem == itemInfo.Itemid
                               select new
                               {
                                   BOMid = ab.BOMid,
                                   MasterItem = master.ItemName,
                                   ParentItem = cat.CategoryDesc,
                                   ParentSub = subcat.BOMSubCategory1,
                                   RawItem = master.ItemName,
                                   Quantity = ab.Quantity,
                                   UOM = ab.UOM

                               }).ToList();
            

            return Json(new { itemInfo, rawItemInfo }, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public ActionResult DeleteItem(int itemId)
        {
            using (var db = new IECEntities()) // Replace with your DbContext class
            {
                var item = db.BOMItemMasters.FirstOrDefault(b => b.Itemid== itemId);
                if (item != null)
                {
                    db.BOMItemMasters.Remove(item);
                    db.SaveChanges();
                    return Json(new { success = true });
                }
                else
                {
                    return Json(new { success = false });
                }
            }
        }
       
        public class ItemLineViewModel
        {
            public string ItemName { get; set; }
            public decimal TotalQuantityRequired { get; set; }
            public decimal ShortQuantity { get; set; }
            public string CreatedBy { get; set; }
            public int AprovalStatus { get; set; } 
            //public DateTime CreatedOn { get; set; }
        }


        public class IndentDataModel
        {
            public string ItemCode { get; set; }
            public string RawMaterial { get; set; }
            public decimal TotalQuantityRequired { get; set; }
            public decimal AvailableStock { get; set; }
            public decimal ShortageExcess { get; set; }
            public decimal ActualRequirement { get; set; } // Add the ActualRequirement property
        }
        public class BOMInputModel
        {
            public int bomid { get; set; }
            public string FinalItem { get; set; }
            public int TotalQuantity { get; set; }
            public int BaseFrame { get; set; }
            public int DGType { get; set; }
            public int Panel { get; set; }
            public int Alternator { get; set; }
            public int Canopy { get; set; }
            public int FuelTank { get; set; }
            public int Exhaust { get; set; }
            public int Acoustic { get; set; }   
            public int Packing { get; set; }
            public int Assembly { get; set; }
            public int Electrical { get; set;}
        }


        public class ReportItem
        {
            public string ItemCode { get; set; }
            public string RawMaterial { get; set; }
            public decimal TotalQuantityRequired { get; set; }
            public decimal AvailableStock { get; set; }
            public decimal ShortageExcess { get; set; }
            public string UOM { get; set; }
            public decimal MOQ { get; set; }
            public decimal ActualRequired { get; set; }
            public decimal MinimumStockLevel { get; set; }
        }




    }


    // Example implementation of GetCategoryName



}

