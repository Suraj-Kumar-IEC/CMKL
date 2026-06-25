using CMKL.Models;
using CMKL.Views.BOM;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Math;
using DocumentFormat.OpenXml.Office.CustomUI;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Owin.BuilderProperties;
using Rotativa;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Web;
using System.Web.Configuration;
using System.Web.Helpers;
using System.Web.Management;
using System.Web.Mvc;
using System.Web.Razor.Parser.SyntaxTree;
using static CMKL.Models.PurchaseOrderclass;


//using static ClosedXML.Excel.XLPredefinedFormat;

namespace CMKL.Controllers
{
    public class PurchaseController : Controller
    {
        // GET: Purchase
        IECEntities DB = new IECEntities();
        public ActionResult PurchaseRPTEmpty()
        {
            return View();
        }
        [HttpPost]
        public JsonResult GetBulkItemNames(List<string> codes)
        {
            var items = DB.BOMItemMasters
                .Where(i => codes.Contains(i.ItemCode))
                .Select(i => new { i.ItemCode, i.ItemName })
                .ToList();
            return Json(new { success = true, data = items });
        }
        public ActionResult PurchaseVoucher()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "PurchaseVoucher" && ab.Status == true
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
        public ActionResult IndentStatusReport()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "IndentStatusReport" && ab.Status == true
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
        public ActionResult UpdateReceipt() 
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "UpdateReceipt" && ab.Status == true
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
        public JsonResult GetItemPendingIndents(int itemId)
        {
            try
            {
                if (Session["Company_ID"] == null)
                    return Json(new { success = false, msg = "Session Expired" }, JsonRequestBehavior.AllowGet);

                int companyId = Convert.ToInt32(Session["Company_ID"]);

                // Join Indent Lines with the Head table to verify status and get Voucher Numbers
                var data = (from l in DB.BomIndentLines
                            join h in DB.BOMIndentHeads on l.Headid equals h.id
                            where l.Itemid == itemId
                               && h.CompanyID == companyId
                               && h.Isclosed == 0        // Only fetch open indents
                               && h.ApprovalStatus == 1  // Only fetch approved indents
                               orderby h.id descending
                            select new
                            {
                                IndentLineId = l.id,
                                VoucherNumber = h.VoucherNumber,
                                ActualRequired = l.ActualRequired,
                                QuantityRecieved = l.QuantityRecieved ?? 0,
                                // Calculate the remaining balance
                                PendingQty = l.ActualRequired - (l.QuantityRecieved ?? 0)
                            })
                            .Where(x => x.PendingQty > 0) // Only show lines that still need items
                            .ToList();

                return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                // In production, log the error (ex)
                return Json(new { success = false, msg = "Error: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpGet]
        public JsonResult GetIndentForReceipt(string indentNo)
        {
            var data = (from h in DB.BOMIndentHeads
                        join l in DB.BomIndentLines on h.id equals l.Headid
                        join i in DB.BOMItemMasters on l.Itemid equals i.Itemid
                        join b in DB.BOM_UOM on i.UOM equals b.id
                        where h.VoucherNumber == indentNo && h.Isclosed == 1
                        select new
                        {
                            LineId = l.id,
                            i.ItemCode,
                            i.ItemName,
                            l.ActualRequired,
                            l.QuantityRecieved,
                            Pending = l.ActualRequired - (l.QuantityRecieved ?? 0),
                            b.UOM
                        }).ToList();

            return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public JsonResult SavePartialReceipt(List<ReceiptUpdateModel> items)
        {
            using (var trans = DB.Database.BeginTransaction())
            {
                try
                {
                    if (items == null || !items.Any()) return Json(new { success = false, msg = "No data provided" });

                    int headId = 0;
                    foreach (var item in items)
                    {
                        var line = DB.BomIndentLines.Find(item.LineId);
                        if (line != null)
                        {
                            headId =(int)line.Headid;
                            // Increment the received quantity
                            line.QuantityRecieved = (line.QuantityRecieved ?? 0) + item.ReceivingQty;

                            // Log who updated it
                            line.Remarks = $"Last receipt of {item.ReceivingQty} by {Session["U_Name"]} on {DateTime.Now:dd/MM/yyyy}";
                        }
                    }

                    DB.SaveChanges();

                    // Check if entire Indent is now completed
                    var allLines = DB.BomIndentLines.Where(x => x.Headid == headId).ToList();
                    bool isFullyComplete = allLines.All(x => x.QuantityRecieved >= x.ActualRequired);

                    if (isFullyComplete)
                    {
                        var head = DB.BOMIndentHeads.Find(headId);
                        head.Isclosed = 1;
                        head.HoldRemarks = "Auto-Closed: All items received.";
                    }

                    DB.SaveChanges();
                    trans.Commit();
                    return Json(new { success = true, msg = "Receipt updated successfully!" });
                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    return Json(new { success = false, msg = ex.Message });
                }
            }
        }
        [HttpGet]
        public JsonResult GetIndentTrackingAnalytics()
        {
            try
            {
                if (Session["Company_ID"] == null)
                    return Json(new { success = false, msg = "Session Expired" }, JsonRequestBehavior.AllowGet);

                // FIX: Extract Session value to local variable to prevent 'System.Object get_Item' error
                int companyId = Convert.ToInt32(Session["Company_ID"]);

                var result = (from l in DB.BomIndentLines
                              join h in DB.BOMIndentHeads on l.Headid equals h.id
                              join i in DB.BOMItemMasters on l.Itemid equals i.Itemid
                              where h.CompanyID == companyId
                              select new
                              {
                                  h.VoucherNumber,
                                  h.CreatedOn,
                                  i.ItemName,
                                  l.ActualRequired,
                                  Recv = l.QuantityRecieved ?? 0,
                                  l.LastBasicRate
                              }).ToList() // Fetch to memory
                              .Select(x => new
                              {
                                  x.VoucherNumber,
                                  x.CreatedOn,
                                  x.ItemName,
                                  x.ActualRequired,
                                  QuantityRecieved = x.Recv,
                                  x.LastBasicRate,
                                  // Logic handled in memory to ensure stability
                                  Status = (x.Recv == 0) ? "Pending" :
                                           (x.Recv >= x.ActualRequired) ? "Completed" : "Partial"
                              }).ToList();

                return Json(new { data = result }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        public class ReceiptUpdateModel
        {
            public int LineId { get; set; }
            public decimal ReceivingQty { get; set; }
        }
        [Obsolete]
        public ActionResult GetPurchaseOrderChartData()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var startDate = DateTime.Today.AddDays(-7).Date;
            var endDate = DateTime.Today.Date.AddDays(1); // Add 1 day to endDate to include the entire current day

            var poData = DB.PurchaseOrderHeads
                .Where(po => po.EnteredOn >= startDate && po.EnteredOn < endDate)
                .Where(x => x.CompanyID == companyid)
                .GroupBy(po => EntityFunctions.TruncateTime(po.EnteredOn))
                .Select(group => new
                {
                    Date = group.Key,
                    TotalOrders = group.Count(),
                    ApprovedOrders = group.Count(po => po.AdminApproval == 1)
                })
                .OrderBy(x => x.Date)
                .ToList();

            return Json(poData, JsonRequestBehavior.AllowGet);
        }

        [Obsolete]
        public ActionResult GetPurchaseOrderChartDatadir()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var startDate = DateTime.Today.AddDays(-7).Date;
            var endDate = DateTime.Today.Date.AddDays(1); // Add 1 day to endDate to include the entire current day

            var poData = DB.PurchaseOrderHeads
                .Where(po => po.EnteredOn >= startDate && po.EnteredOn < endDate)
                .Where(x=>x.CompanyID==companyid)
                .GroupBy(po => EntityFunctions.TruncateTime(po.EnteredOn))
                .Select(group => new
                {
                    Date = group.Key,
                    TotalOrders = group.Count(),
                    ApprovedOrders = group.Count(po => po.SAdminApproval == 1) // Note: Assuming 'SAdminApproval' is the correct property
                })
                .OrderBy(x => x.Date)
                .ToList();

            return Json(poData, JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetSupplierwisePO(int supplierid)
        {
            try
            { 
            var FinYear = Session["Fin_Year"].ToString();
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
                var polist = (from ab in DB.PurchaseOrderHeads
                              where ab.SupplierID == supplierid && ab.CompanyID == companyid  && ab.Isdeleted== 0 //&& ab.Fin_Year == FinYear
                              orderby ab.id descending
                              select ab.PONumber).ToList();
                return Json(new { success = true,msg="Purchase Orders Retrieved..", polist }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = "Error Getting Purchase Orders" }, JsonRequestBehavior.AllowGet);
            }
        }
        public JsonResult GetItemDetails(string itemCode)
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            decimal Lastquantity = 0;
            string LastDate = "NA";
            var suppllierID = 0;
            string LastSupplier = "NA";
            object get = "NA"; // Initialize get with a default value

            var itemdetail = (from ab in DB.BOMItemMasters
                              where ab.ItemCode == itemCode && ab.CompanyID==companyid
                              select ab).ToList();
            

            if (itemdetail.Count == 0)
            {
                return Json(new { success = false, error = "Item code not found" }, JsonRequestBehavior.AllowGet);
            }
            else if (itemdetail.Count > 1)
            {
                return Json(new { success = false, error = "Item code is assigned to 1 or more items" }, JsonRequestBehavior.AllowGet);
            }
            else
            {
                var enablelot = false;
                var item = itemdetail.FirstOrDefault();
                var itemname = item.ItemName;
                var itemdesc = item.Desc;
                var uomvalue = item.UOM;
                var Basic = item.BasicPrice;
                var stock = DB.StockTables.Where(s => s.itemid == item.Itemid && s.CompanyID == companyid)
                          .Select(s => s.Stock) // Select the Stock property
                          .FirstOrDefault();    // Get the first result or default value
                var uomText = DB.BOM_UOM.FirstOrDefault(u => u.id == uomvalue).UOM;
                var itemid = item.Itemid;


                var LastPurchase = DB.IEPLStockIN_Detail
                 .Where(ab => ab.ItemCode == itemid && ab.IsDeleted == 0)
                .OrderByDescending(ab => ab.Id)
                .FirstOrDefault(); // No need to call ToList() before FirstOrDefault()

                // 1. Find the HeadId of the very last purchase for this item
                var lastPurchaseHeadId = DB.IEPLStockIN_Detail
                    .Where(ab => ab.ItemCode == itemid && ab.IsDeleted == 0)
                    .OrderByDescending(ab => ab.Id)
                    .Select(ab => ab.HeadId) // Get the Bill/Head ID
                    .FirstOrDefault();

                decimal lastQuantitySum = 0;

                if (lastPurchaseHeadId > 0)
                {
                    // 2. Sum the Quantity of this specific item from that entire Bill (all lots)
                    lastQuantitySum = DB.IEPLStockIN_Detail
                        .Where(ab => ab.HeadId == lastPurchaseHeadId
                                  && ab.ItemCode == itemid
                                  && ab.IsDeleted == 0)
                        .Sum(ab => (decimal?)ab.Quantity) ?? 0;
                }

                // Convert to int if necessary, though decimal is safer for quantities
                int lastQuantity = (int)lastQuantitySum;

                //Enable LotNumber Details Check

                var GroupID = item.ItemGroup;
                //Now Check Group ID is enabled for Lot Number in Incoming Or Outgoing
                var LotStatus=(from ab in DB.EnableLotDetails
                              where ab.GroupID ==GroupID && ab.CompanyID==companyid
                              select ab).SingleOrDefault();
                if(LotStatus!=null)  
                {
                  if(LotStatus.Incoming == true && LotStatus.IsActive == true)
                    {
                        enablelot = true;
                    }
                }


                if (LastPurchase != null)
                {
                    Lastquantity = lastQuantity;

                    var GetLastDate = (from bb in DB.IEPLStockIN_Head
                                       where bb.Id == LastPurchase.HeadId 
                                       select bb).SingleOrDefault();

                    get = (from ii in DB.IEPLStockIN_Head
                           where ii.Id == LastPurchase.HeadId
                           select new
                           {
                               Date = ii.CreatedDate,
                               suppllierID = ii.Supplierid
                           }).ToList();

                    // LastDate = GetLastDate.CreatedDate.ToString("dd/MM/yyyy"); // Format the date
                    LastSupplier = (from ss in DB.SupplierMasters
                                    where ss.id == GetLastDate.Supplierid
                                    select ss.SupplierName).SingleOrDefault();
                    suppllierID = (from tt in DB.SupplierMasters
                                   where tt.id == GetLastDate.Supplierid
                                   select tt.id).SingleOrDefault();

                }
                //Calculate Average Monthly Consumtion
                System.DateTime startDate = System.DateTime.Now.AddDays(-90);
                var stockIssueData = DB.IEPLStockIssueDetails.Where(s => s.ApprovedDate >= startDate && s.Itemcodeid==itemid).ToList();
                var bomIssueData = DB.BOMVoucherlines.Where(b => b.Approveddate >= startDate && b.Isdeleted == 0 && b.rawitemid==itemid).ToList();

                // Calculate the sums directly:
                decimal issuedQuantity = (decimal)stockIssueData.Sum(s => s.ApprovedQuantity);
                decimal bomIssueQuantity = (decimal)bomIssueData.Sum(b => b.ApprovedQuantity);

                // Calculate total consumption
                decimal totalConsumption = issuedQuantity + bomIssueQuantity;
                int numberOfMonths = 3; // For the last 3 months average

                // Calculate the average for the last 3 months
                decimal average = Math.Round(CalculateAverage(totalConsumption, numberOfMonths), 3);

                // No need for an else block since 'get' is already initialized

                return Json(new { success = true, itemname, uomText, Basic, stock, itemdesc, itemid, Lastquantity, LastDate, LastSupplier, suppllierID, get, average, enablelot }, JsonRequestBehavior.AllowGet);
            }
        }
        private decimal CalculateAverage(decimal quantity, int months)
        {
            return months > 0 ? quantity / months : 0;
        }
        public ActionResult IndentVoucherNumber()
        {
            var FinYear = Session["Fin_Year"].ToString();
            int CompanyID = (int)Session["Company_ID"];
            var GetNumber = (from ab in DB.Bill_Series
                                 where ab.Type == "Indent" && ab.CompanyID == CompanyID && ab.Fin_Year == FinYear
                                 select ab).SingleOrDefault();
            var VoucherNumber = GetNumber.Series + GetNumber.Number;

            return Json(new {success=true, VoucherNumber }, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public ActionResult SaveIndentManual(IndentHead headData, List<IndentLine> lineData)
        {
            try
            {
                var FinYear = Session["Fin_Year"].ToString();
                int CompanyID = (int)Session["Company_ID"];

                //Get Voucher Number
                var billno = (from ab in DB.Bill_Series
                             where ab.Type == "Indent" && ab.CompanyID == CompanyID && ab.Fin_Year == FinYear
                             select ab).SingleOrDefault();

                // Save Head Data
                BOMIndentHead BIH = new BOMIndentHead();
                BIH.VoucherNumber = billno.Series+billno.Number;
                BIH.CreatedOn = System.DateTime.Now;
                BIH.TotalItems = lineData.Count; // Set TotalItems here
                BIH.CreatedBy = Session["U_Name"].ToString();
                BIH.Isclosed = 1;
                BIH.ApprovalStatus = 0;
                BIH.FinYear = FinYear;
                BIH.CompanyID = CompanyID;               
                DB.BOMIndentHeads.Add(BIH);
                DB.SaveChanges(); // SaveChanges here to get the generated Head id

                // Get Saved Headid
                int Headid = BIH.id;

                // Save Indent Lines (now with Headid)
                foreach (var HH in lineData)
                {
                    BomIndentLine BIL = new BomIndentLine();
                    BIL.Headid = Headid; // Assign the Headid to each line
                    BIL.Itemid = HH.itemid;
                    BIL.TotalQuantityRequired = HH.quantityrequired;
                    BIL.ShortQuantity = HH.quantityrequired;
                    BIL.ActualRequired = HH.quantityrequired;
                    BIL.CreatedBy = Session["U_Name"].ToString();
                    BIL.CreatedOn = System.DateTime.Now;
                    BIL.IsApproved = 0;
                    BIL.IsIndentRaised = 1;
                    BIL.IsManualIndent = 1;
                    BIL.LastOrderDate = HH.lastorderdate;
                    BIL.LastOrderQuantity = HH.lastorderquantity;
                    BIL.PreviousSupplierid = HH.previoussupplier;
                    BIL.LastBasicRate = HH.basicrate;
                    BIL.AvailableStock = HH.stock;
                    BIL.AverageMonthlyConsumption = HH.average;
                    BIL.UserRemarks=HH.userremarks;
                    BIL.Rejected = 0;
                    BIL.QuantityRecieved = 0;   
                    BIL.ExpectedStkEndDate=Convert.ToDateTime(HH.expectedstkdate);

                    DB.BomIndentLines.Add(BIL); // Add each line to the context
                }

                DB.SaveChanges(); // SaveChanges again to save the lines

                //Update Bill Series


                var number = (from jj in DB.Bill_Series
                              where jj.Type == "Indent" && jj.CompanyID == CompanyID && jj.Fin_Year == FinYear
                              select jj).SingleOrDefault();

                if (number != null)
                {
                    number.Number += 1;
                    DB.SaveChanges();
                }

                //Get New Voucher Number
                var GetNumber = (from ab in DB.Bill_Series
                                 where ab.Type == "Indent" && ab.CompanyID == CompanyID && ab.Fin_Year == FinYear
                                 select ab).SingleOrDefault();
                var VoucherNumber = GetNumber.Series + GetNumber.Number;

                return Json(new { success = true, message = "Indent saved successfully!", VoucherNumber });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error saving indent: " + ex.Message });
            }
        }
        
        public ActionResult GetAvailablePONumbers()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var data = (from ab in DB.PurchaseOrderHeads
                        where ab.Isdeleted==0 && ab.CompanyID==companyid
                        select new
                        {
                            value=ab.id,
                            text=ab.PONumber,
                        }).ToList();
            return Json(new { success = true, data }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult MRNStatus()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "MRNStatus" && ab.Status == true
                               select ab).SingleOrDefault();
            if (checkaccess == null)
            {
                // 3. Store error message in TempData (survives one redirect)
                TempData["ErrorMessage"] = "Access Denied: You are not authorized to view This Page.";

                // 4. Redirect to Home Controller, Index Action
                return RedirectToAction("Dashboard", "Dashboard");
            }
            return View();
        }
        public ActionResult ApprovalPlantHead()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "ApprovalPlantHead" && ab.Status == true
                               select ab).SingleOrDefault();
            if (checkaccess == null)
            {
                // 3. Store error message in TempData (survives one redirect)
                TempData["ErrorMessage"] = "Access Denied: You are not authorized to view This Page.";

                // 4. Redirect to Home Controller, Index Action
                return RedirectToAction("Dashboard", "Dashboard");
            }
            return View();
        }
        public ActionResult ApprovalPlantAccounts()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "ApprovalPlantAccounts" && ab.Status == true
                               select ab).SingleOrDefault();
            if (checkaccess == null)
            {
                // 3. Store error message in TempData (survives one redirect)
                TempData["ErrorMessage"] = "Access Denied: You are not authorized to view This Page.";

                // 4. Redirect to Home Controller, Index Action
                return RedirectToAction("Dashboard", "Dashboard");
            }
            return View();
        }
        public ActionResult PendingMRNPlantHead()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var selectrows = (from ab in DB.IEPLStockIN_Head
                              where ab.CompanyID==companyid && ab.MRNStatus == 1 && ab.MRNStoreApproval == 1 && ab.MRNQualityApproval == 1 && ab.MRNPlantHeadApproval == 0 && ab.Isdeleted==0
                              select new
                              {
                                  id = ab.Id,
                                  billnumber = ab.BillNumber,
                                  billdate = ab.BillDate,
                                  ponumber = ab.PoNumber,
                                  mrnnumber = ab.Vouchernumber,
                                  mrndate = ab.CreatedDate,
                                  gateentry = ab.GateEntry,
                                  gentrydate = ab.GateEntryDate,

                              }).ToList();
            return Json(new { success = true, selectrows }, JsonRequestBehavior.AllowGet);

        }
        public ActionResult DeletePurchaseOrder(int? poId)
        {
            var selectrecord = (from ab in DB.PurchaseOrderHeads
                                where ab.id == poId
                                select ab).SingleOrDefault();
            //Update Purchase Order Had status
            selectrecord.Isdeleted = 1;
            DB.SaveChanges();
            var selectline = (from bb in DB.PurchaseOrderItems
                              where bb.HeadID == poId
                              select bb).ToList();
            foreach (var nm in selectline)
            {
                nm.IsDeleted = 1;
            }
            DB.SaveChanges();

            return Json(new {success=true,Deleted=1},JsonRequestBehavior.AllowGet);
        }
        public ActionResult PendingMRNAccounts()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var selectrows = (from ab in DB.IEPLStockIN_Head
                              where ab.CompanyID==companyid && ab.MRNStatus == 1 && ab.MRNStoreApproval == 1 && ab.MRNQualityApproval == 1 && ab.MRNPlantHeadApproval == 1 && ab.MRNAccountApproval==0 && ab.Isdeleted==0
                              select new
                              {
                                  id = ab.Id,
                                  billnumber = ab.BillNumber,
                                  billdate = ab.BillDate,
                                  ponumber = ab.PoNumber,
                                  mrnnumber = ab.Vouchernumber,
                                  mrndate = ab.CreatedDate,
                                  gateentry = ab.GateEntry,
                                  gentrydate = ab.GateEntryDate,

                              }).ToList();
            return Json(new { success = true, selectrows }, JsonRequestBehavior.AllowGet);

        }
        
        public ActionResult ApprovePlantHeadMRN(int id)
        {
            var selectrecord = (from ab in DB.IEPLStockIN_Head
                                where ab.Id == id
                                select ab).SingleOrDefault();
            //Update Approval Status
            selectrecord.MRNPlantHeadApproval = 1;
            //Update MRN Log
            var selectlog = (from ab in DB.MRNDetailLogs
                             where ab.HeadID == id
                             select ab).FirstOrDefault();
            {
                selectlog.PlantHeadApprovalBY = Session["U_Name"].ToString();
                selectlog.PlantHeadADate = System.DateTime.Now;

            }
            DB.SaveChanges();
            return Json("Record Updated..");

        }
        public ActionResult ApprovePlantAccountsMRN(int id)
        {
            var selectrecord = (from ab in DB.IEPLStockIN_Head
                                where ab.Id == id
                                select ab).SingleOrDefault();
            //Update Approval Status
            selectrecord.MRNAccountApproval = 1;
            //Update MRN Log
            var selectlog = (from ab in DB.MRNDetailLogs
                             where ab.HeadID == id
                             select ab).FirstOrDefault();
            {
                selectlog.AccountApprovalBY = Session["U_Name"].ToString();
                selectlog.AccountADate = System.DateTime.Now;

            }
            DB.SaveChanges();
            return Json("Record Updated..");

        }
        
        public ActionResult GetMRNStatus()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var data = (from x in DB.IEPLStockIN_Head
                        where x.MRNStoreApproval==0 || x.MRNQualityApproval==0 || x.MRNPlantHeadApproval==0 || x.MRNAccountApproval==0
                        where x.CompanyID==companyid
                        where x.Isdeleted==0
                        orderby x.Id descending
                       select new 
            {
                x.Id,
                x.BillNumber,
                x.Vouchernumber,
                x.GateEntry,
                // ... other properties you want to include
                x.MRNStoreApproval,
                x.MRNQualityApproval,
                x.MRNPlantHeadApproval,
                x.MRNAccountApproval
            }).ToList();

            return Json(data, JsonRequestBehavior.AllowGet);
        }
        public ActionResult MRNPrintout(int headid)
        {
            ViewBag.headid=headid;
            return View();
        }
        [HttpPost]
        public JsonResult SavePurchaseOrder(PurchaseOrderclass.POModel model)
        {
            if (Session["U_Name"].ToString() !=null)
            {
                try
                {
                    int companyid1 = Convert.ToInt32(Session["Company_ID"].ToString());
                    var finyear1 = Session["Fin_Year"].ToString();
                    //using (var transaction = DB.Database.BeginTransaction())
                    //{
                    try
                    {
                        if (model.HeadData.headid > 0)
                        {
                            var existingPO = DB.PurchaseOrderHeads.Find(model.HeadData.headid);

                            if (existingPO == null)
                            {
                                return Json(new { success = false, message = "Purchase order not found!" });
                            }
                            //Update Purchase Order Head
                            var selecthead = (from ab in DB.PurchaseOrderHeads
                                              where ab.id == model.HeadData.headid
                                              select ab).SingleOrDefault();
                            selecthead.SupplierID = model.HeadData.Supplierid;
                            selecthead.CompanyID = model.HeadData.Companyid;
                            selecthead.PODate = model.HeadData.PODate;
                            selecthead.IndentNumber = model.HeadData.IndentNumber;
                            selecthead.IndentDate = model.HeadData.IndentDate;
                            selecthead.DeliveryDate = model.HeadData.DeliveryDate;
                            selecthead.FrieghtType = model.HeadData.frieghttype;
                            selecthead.FreightAmount = model.HeadData.freightAmount;
                            selecthead.FrieghtTaxID = model.HeadData.frieghttax;
                            selecthead.FrieghtTax = model.HeadData.frieghttaxamount;
                            selecthead.TotalFrieght = model.HeadData.ttlfrieght;
                            selecthead.InsuranceType = model.HeadData.insurancetype;
                            selecthead.InsuranceAmount = model.HeadData.insuranceAmount;
                            selecthead.InsuranceTaxID = model.HeadData.Insurancetax;
                            selecthead.InsuranceTax = model.HeadData.insurancetaxamount;
                            selecthead.Totalinsurance = model.HeadData.ttlinsurace;
                            selecthead.PFType = model.HeadData.pftype;
                            selecthead.PFAmount = model.HeadData.pfCharges;
                            selecthead.PFTaxID = model.HeadData.PFTax;
                            selecthead.PFTax = model.HeadData.pftaxamount;
                            selecthead.TotalPF = model.HeadData.ttlpf;
                            selecthead.OtherType = model.HeadData.othertype;
                            selecthead.OtherAmount = model.HeadData.otherCharges;
                            selecthead.OtherTaxID = model.HeadData.OtherTax;
                            selecthead.OtherTax = model.HeadData.othertaxamount;
                            selecthead.TotalOther = model.HeadData.ttlother;
                            selecthead.DeliveryMode = model.HeadData.deliverymode;
                            selecthead.Remarks = model.HeadData.remarks;
                            selecthead.PaymentTerms = model.HeadData.paymentterms;
                            selecthead.BillTaxableAmount = model.HeadData.BillTaxableAmount;
                            selecthead.TaxTypeOnBill = model.HeadData.taxtypeonbill;
                            selecthead.IGST = model.HeadData.IGST;
                            selecthead.SGST = model.HeadData.SGST;
                            selecthead.CGST = model.HeadData.CGST;
                            selecthead.BillTotalTax = model.HeadData.Billtaxamount;
                            selecthead.BillGrossAmount = model.HeadData.BillGrossAmount;
                            selecthead.IsservicePO = model.HeadData.IsServiceOrder;
                            //poHead.IsServiceOrder = model.HeadData.IsServiceOrder;
                            // selecthead.Fin_Year=
                            DB.SaveChanges();

                            //now save updation log
                            PurchaseOrderUpdationlog PL = new PurchaseOrderUpdationlog();
                            PL.POid = model.HeadData.headid;
                            PL.Updatedby = Session["U_Name"].ToString();
                            PL.Updatedon = System.DateTime.Now;
                            DB.PurchaseOrderUpdationlogs.Add(PL);
                            DB.SaveChanges();

                            foreach (var tableLine in model.TableData)
                            {
                                if (tableLine.lineid == 0) // New item
                                {
                                    var POI = new PurchaseOrderItem
                                    {
                                        ItemID = tableLine.itemid,
                                        MakeID = tableLine.makeid,
                                        HeadID = model.HeadData.headid,
                                        Quantity = tableLine.quantity,
                                        ItemTaxID = tableLine.taxid,
                                        ListPrice = tableLine.listprice,
                                        DiscountPercent = tableLine.discount,
                                        NetBasic = tableLine.netbasic,
                                        TaxableAmount = tableLine.taxableamount,
                                        TaxAmount = tableLine.taxamount,
                                        GrossAmount = tableLine.grossamount,
                                        IsDeleted = 0
                                        // ... map other properties as needed
                                    };
                                    DB.PurchaseOrderItems.Add(POI);
                                }
                                else if (tableLine.lineid > 0) // Existing item
                                {
                                    var existingDetail = DB.PurchaseOrderItems.Find(tableLine.lineid);
                                    if (existingDetail != null)
                                    {
                                        // Update properties of existingDetail from tableLine
                                        existingDetail.ItemID = tableLine.itemid;
                                        existingDetail.MakeID = tableLine.makeid;
                                        existingDetail.Quantity = tableLine.quantity;
                                        existingDetail.ItemTaxID = tableLine.taxid;
                                        existingDetail.ListPrice = tableLine.listprice;
                                        existingDetail.DiscountPercent = tableLine.discount;
                                        existingDetail.NetBasic = tableLine.netbasic;
                                        existingDetail.TaxableAmount = tableLine.taxableamount;
                                        existingDetail.TaxAmount = tableLine.taxamount;
                                        existingDetail.GrossAmount = tableLine.grossamount;
                                        // ... update other properties as needed
                                    }
                                    else
                                    {
                                        return Json(new { success = false, message = "Item to update not found!" });
                                    }
                                }
                            }

                            // Delete items not found in model.TableData
                            var existingItemIds = DB.PurchaseOrderItems
                                .Where(item => item.HeadID == model.HeadData.headid)
                                .Select(item => item.id)
                                .ToList();

                            var newItemIds = model.TableData.Select(item => item.lineid).ToList();

                            var idsToDelete = existingItemIds.Except(newItemIds).ToList();

                            foreach (var idToDelete in idsToDelete)
                            {
                                var itemToDelete = DB.PurchaseOrderItems.Find(idToDelete);
                                if (itemToDelete != null)
                                {
                                    itemToDelete.IsDeleted = 1; // Mark as deleted
                                }
                            }

                            DB.SaveChanges(); // Save all changes (updates, additions, and deletions)
                                              // transaction.Commit();
                            var Printid = model.HeadData.headid;
                            return Json(new { success = true, message = "Purchase order saved successfully!", Printid });
                        }

                        else
                        {
                            // 1. Save Table Lines (PurchaseOrderItems) First
                            foreach (var tableLine in model.TableData)
                            {
                                var detail = new PurchaseOrderItem
                                {
                                    ItemID = tableLine.itemid, // Assuming you have ItemID in your POTableData
                                    MakeID = tableLine.makeid,
                                    HeadID = 0,
                                    Quantity = tableLine.quantity,
                                    ItemTaxID = tableLine.taxid,
                                    ListPrice = tableLine.listprice,
                                    DiscountPercent = tableLine.discount,
                                    NetBasic = tableLine.netbasic,
                                    TaxableAmount = tableLine.taxableamount,
                                    TaxAmount = tableLine.taxamount,
                                    GrossAmount = tableLine.grossamount,
                                    IsDeleted = 0,

                                    // ... map other properties as needed
                                };
                                DB.PurchaseOrderItems.Add(detail);
                            }

                            DB.SaveChanges(); // Attempt to save table lines

                            // 2. If table lines save successfully, save the header (PurchaseOrderHead)
                            POHeadData POH = model.HeadData;

                            PurchaseOrderHead PO = new PurchaseOrderHead();
                            var FinYear = Session["Fin_Year"].ToString();

                            PO.SupplierID = model.HeadData.Supplierid;
                            PO.CompanyID = model.HeadData.Companyid;
                            PO.PODate = model.HeadData.PODate;
                            PO.IndentNumber = model.HeadData.IndentNumber;
                            PO.IndentDate = model.HeadData.IndentDate;
                            PO.DeliveryDate = model.HeadData.DeliveryDate;
                            PO.FrieghtType = model.HeadData.frieghttype;
                            PO.FreightAmount = model.HeadData.freightAmount;
                            PO.FrieghtTaxID = model.HeadData.frieghttax;
                            PO.FrieghtTax = model.HeadData.frieghttaxamount;
                            PO.TotalFrieght = model.HeadData.ttlfrieght;
                            PO.InsuranceType = model.HeadData.insurancetype;
                            PO.InsuranceAmount = model.HeadData.insuranceAmount;
                            PO.InsuranceTaxID = model.HeadData.Insurancetax;
                            PO.InsuranceTax = model.HeadData.insurancetaxamount;
                            PO.Totalinsurance = model.HeadData.ttlinsurace;
                            PO.PFType = model.HeadData.pftype;
                            PO.PFAmount = model.HeadData.pfCharges;
                            PO.PFTaxID = model.HeadData.PFTax;
                            PO.PFTax = model.HeadData.pftaxamount;
                            PO.TotalPF = model.HeadData.ttlpf;
                            PO.OtherType = model.HeadData.othertype;
                            PO.OtherAmount = model.HeadData.otherCharges;
                            PO.OtherTaxID = model.HeadData.OtherTax;
                            PO.OtherTax = model.HeadData.othertaxamount;
                            PO.TotalOther = model.HeadData.ttlother;
                            PO.DeliveryMode = model.HeadData.deliverymode;
                            PO.Remarks = model.HeadData.remarks;
                            PO.PaymentTerms = model.HeadData.paymentterms;
                            PO.BillTaxableAmount = model.HeadData.BillTaxableAmount;
                            PO.TaxTypeOnBill = model.HeadData.taxtypeonbill;
                            PO.IGST = model.HeadData.IGST;
                            PO.SGST = model.HeadData.SGST;
                            PO.CGST = model.HeadData.CGST;
                            PO.BillTotalTax = model.HeadData.Billtaxamount;
                            PO.BillGrossAmount = model.HeadData.BillGrossAmount;
                            PO.EnteredBy = Session["U_Name"].ToString();
                            PO.EnteredOn = DateTime.Now;
                            PO.AdminApproval = 0;
                            PO.SAdminApproval = 0;
                            PO.Isdeleted = 0;
                            PO.Fin_Year = FinYear;



                            DB.PurchaseOrderHeads.Add(PO);
                            DB.SaveChanges(); // Save the header to get the generated PO ID

                            // 3. Generate PO Number and update table lines with HeadID
                            // int id = (from ab in DB.PurchaseOrderHeads
                            //         orderby ab.id descending
                            //       select ab.id).FirstOrDefault();



                            //int newPoNumber = id + 1; ; // Implement your PO number generation logic
                            //PO.PONumber = "IEPL-" + newPoNumber;
                            var getponumber = (from ab in DB.Bill_Series
                                               orderby ab.id descending
                                               where ab.Type == "Purchase Order" && ab.CompanyID==companyid1 && ab.Fin_Year==finyear1
                                               select ab).FirstOrDefault();
                            string poNumber = getponumber.Series + getponumber.Number;
                            PO.PONumber = poNumber;
                            string Generatednumber = PO.PONumber;

                            //update bill series
                            //BillSeries BS = new BillSeries();
                            var updatebillseries = (from nn in DB.Bill_Series
                                                    orderby nn.id descending
                                                    where nn.Type == "Purchase Order" && nn.CompanyID == companyid1 && nn.Fin_Year == finyear1
                                                    select nn).FirstOrDefault();
                            updatebillseries.Number = updatebillseries.Number + 1;
                            DB.SaveChanges();
                            // Update the HeadID in PurchaseOrderItems
                            foreach (var detail in DB.PurchaseOrderItems.Where(d => d.HeadID == 0)) // Find the newly added details
                            {
                                detail.HeadID = PO.id; // Link them to the saved PO
                            }
                            //Update Purcahse Log
                            PurchaseOrderUpdationlog POU = new PurchaseOrderUpdationlog();
                            POU.Updatedon = DateTime.Now;
                            POU.Updatedby = Session["U_Name"].ToString();
                            POU.POid = PO.id;

                            DB.PurchaseOrderUpdationlogs.Add(POU);


                            DB.SaveChanges(); // Save the updated PO and details

                            // transaction.Commit(); // Commit the transaction
                            int Printid = (from ab in DB.PurchaseOrderHeads
                                           orderby ab.id descending
                                           select ab.id).FirstOrDefault();
                            //Send Email to Purcahse Manager
                            SendNewPurchaseOrderEmail(Generatednumber);


                            return Json(new { success = true, message = "Purchase order saved successfully!", Printid });
                        }
                    }
                    catch (Exception ex)
                    {
                        //transaction.Rollback();
                        return Json(new { success = false, message = "An error occurred while saving: " + ex.Message });
                    }
                    // }
                }

                catch (Exception ex)
                {
                    return Json(new { success = false, message = "An error occurred while saving item details: " + ex.Message });
                }
            }
            else
            {
                return Json(new{ success = false, message = "An error occurred while saving item details: "});
            }
        }
        private void SendNewPurchaseOrderEmail(string poNumber)
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            try
            {
                var email = (from ab in DB.CMKL_Email_Setting
                             where ab.id == 1
                             select ab).SingleOrDefault();
                var emailaddress = (from db in DB.CMKL_Email
                                    where db.DDLName == "Purchase Admin" && db.CompanyID== companyid
                                    select db).ToList();
                var emailsender = (from se in DB.CMKL_Email
                                   where se.DDLName == "Sender"
                                   select se).FirstOrDefault();
                // Configure email settings (replace with your actual settings)
                MailMessage mail = new MailMessage();
                SmtpClient SmtpServer = new SmtpClient(email.smtp);
                mail.From = new MailAddress(emailsender.Email);
                foreach (var recipient in emailaddress)
                {
                    mail.To.Add(recipient.Email); // Assuming the column name is EmailAddress
                }
                //mail.To.Add("recipient_email_address");
                mail.Subject = "New Purchase Order Created";
                mail.Body = mail.Body = "<tr>A new purchase order with PO Number"+ poNumber + "has been created.</tr><tr>Please review and process it accordingly.</tr><<tr>http://www.cmklqrcode.in:91</tr>";

                SmtpServer.Port = Convert.ToInt32(email.port); // Or the appropriate port for your SMTP server
                SmtpServer.Credentials = new System.Net.NetworkCredential(emailsender.Email,email.IT_password);
                SmtpServer.EnableSsl = Convert.ToBoolean(email.ssl);

                SmtpServer.Send(mail);
            }
            catch (Exception ex)
            {
                // Handle email sending errors (log or display an error message)
                // You might want to log the error here instead of returning a JSON result
                // since this method is called internally
                // Example: Logger.LogError("Failed to send email: " + ex.Message);
            }
        }
        public ActionResult MakeMaster()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "MakeMaster" && ab.Status == true
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
        public ActionResult POPrint(int poId)
        {
            ViewBag.PoId = poId;
            return View();
        }
        public ActionResult PONotApproved()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "PONotApproved" && ab.Status == true
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
        public ActionResult PurchaseOrderHODApproval()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "PurchaseOrderHODApproval" && ab.Status == true
                               select ab).SingleOrDefault();
            if (checkaccess == null)
            {
                // 3. Store error message in TempData (survives one redirect)
                TempData["ErrorMessage"] = "Access Denied: You are not authorized to view This Page.";

                // 4. Redirect to Home Controller, Index Action
                return RedirectToAction("Dashboard", "Dashboard");
            }
            return View();
        }
        public ActionResult PurchaseOrderSAdminApproval()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "PONotApproved" && ab.Status == true
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
        public ActionResult ApprovePurchaseOrderAdmin(int id)
        {
            try 
            {
                var getline = (from ab in DB.PurchaseOrderHeads
                               where ab.id == id
                               select ab).SingleOrDefault();
                //Update Admin Approval and SAdmin Approval On the basis of Amount
                //Updateinmg both Admin Approval and Sadmin Approval if Amount is less than 50000
                if (getline.BillGrossAmount <= 50000)
                {
                    getline.AdminApproval = 1;
                    getline.SAdminApproval = 1;
                                      

                }
                //Updating only Admin Approval if amount is greater than 50000
                else if (getline.BillGrossAmount > 50000)
                {
                    getline.AdminApproval = 1;

                }
                DB.SaveChanges();
                //Send Approval Email
                SendEmailOnPOApprove(id);
                return Json(new { success = true, message = "Order Approved Sucessfully" }, JsonRequestBehavior.AllowGet);

            }
            catch (Exception ex)
            {
                // Handle potential errors
                return Json(new { success = false, message = "An error occurred: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        private void SendEmailOnPOApprove(int id)
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var companyname=DB.Companies.Where(x=>x.CompanyID==companyid).Select(x=>x.CompanyName).FirstOrDefault();
            var get = (from ab in DB.PurchaseOrderHeads
                       where ab.id == id
                       select ab).SingleOrDefault();
            if (get.AdminApproval == 1 && get.SAdminApproval == 0)
            {
                try
                {
                    var email = (from ab in DB.CMKL_Email_Setting
                                 where ab.id == 1
                                 select ab).SingleOrDefault();
                    var emailaddress = (from db in DB.CMKL_Email
                                        where db.DDLName == "Purchase SAdmin" && db.CompanyID==companyid
                                        select db).ToList();
                    var emailsender = (from se in DB.CMKL_Email
                                       where se.DDLName == "Sender"
                                       select se).FirstOrDefault();
                    // Configure email settings (replace with your actual settings)
                    MailMessage mail = new MailMessage();
                    SmtpClient SmtpServer = new SmtpClient(email.smtp);
                    mail.From = new MailAddress(emailsender.Email);
                    foreach (var recipient in emailaddress)
                    {
                        mail.To.Add(recipient.Email); // Assuming the column name is EmailAddress
                    }
                    //mail.To.Add("recipient_email_address");
                    mail.Subject = "New Purchase Order Created";
                    mail.Body = mail.Body = "<tr>PO Number" + get.PONumber + "is pending for approval.</tr>" +
                        "<tr>Company - " + companyname + "</tr>" +
                        "<tr>http://www.cmklqrcode.in:91</tr>";

                    SmtpServer.Port = Convert.ToInt32(email.port); // Or the appropriate port for your SMTP server
                    SmtpServer.Credentials = new System.Net.NetworkCredential(emailsender.Email, email.IT_password);
                    SmtpServer.EnableSsl = Convert.ToBoolean(email.ssl);

                    SmtpServer.Send(mail);
                }
                catch (Exception ex)
                {
                    
                }
            }
            else if (get.AdminApproval == 1 && get.SAdminApproval == 1)
            {                
                try
                {
                    var email = (from ab in DB.CMKL_Email_Setting
                                 where ab.id == 1
                                 select ab).SingleOrDefault();
                    var emailaddress = (from db in DB.CMKL_Email
                                        where db.DDLName == "Purchase" || db.DDLName=="Purchase Admin" || db.DDLName=="Purchase SAdmin" && db.CompanyID==companyid
                                        select db).ToList();
                    var emailsender = (from se in DB.CMKL_Email
                                       where se.DDLName == "Sender"
                                       select se).FirstOrDefault();
                    // Configure email settings (replace with your actual settings)
                    MailMessage mail = new MailMessage();
                    SmtpClient SmtpServer = new SmtpClient(email.smtp);
                    mail.From = new MailAddress(emailsender.Email);
                    foreach (var recipient in emailaddress)
                    {
                        mail.To.Add(recipient.Email); // Assuming the column name is EmailAddress
                    }
                    //mail.To.Add("recipient_email_address");
                    mail.Subject = "New Purchase Order Created";
                    mail.Body = mail.Body = "<tr>PO Number" + get.PONumber + "has been Approved.</tr>" +
                        "<tr>Company - "+ companyname + "</tr>"+
                        "<tr>http://www.cmklqrcode.in:91</tr>";

                    var poDetails = DB.PurchaseOrderItems.Where(pod => pod.HeadID == id).ToList();
                    foreach (var detail in poDetails)
                    {
                        var itemMaster = DB.BOMItemMasters.FirstOrDefault(im => im.Itemid == detail.ItemID);
                        if (itemMaster != null && itemMaster.DrawingData != null)
                        {
                            string fileName = $"{itemMaster.ItemCode}_{itemMaster.ItemName}.pdf"; // Or appropriate extension
                            MemoryStream stream = new MemoryStream(itemMaster.DrawingData);
                            mail.Attachments.Add(new Attachment(stream, fileName));
                        }
                    }

                    SmtpServer.Port = Convert.ToInt32(email.port); // Or the appropriate port for your SMTP server
                    SmtpServer.Credentials = new System.Net.NetworkCredential(emailsender.Email, email.IT_password);
                    SmtpServer.EnableSsl = Convert.ToBoolean(email.ssl);

                    SmtpServer.Send(mail);
                }
                catch (Exception ex)
                {
                    
                }
               
            }
            
        }
        public ActionResult ApprovePurchaseOrderSAdmin(int id)
        {
            try
            {
                var getline = (from ab in DB.PurchaseOrderHeads
                               where ab.id == id
                               select ab).SingleOrDefault();

                getline.SAdminApproval = 1;
                DB.SaveChanges();
                ApprovePurchaseOrderAdmin(id);
                return Json(new { success = true, message = "Order Approved Sucessfully" }, JsonRequestBehavior.AllowGet);

            }
            catch (Exception ex)
            {
                // Handle potential errors
                return Json(new { success = false, message = "An error occurred: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult ApprovedPurchaseOrders()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "ApprovedPurchaseOrders" && ab.Status == true
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
        public ActionResult ApprovedPurchaseOrderList()
        {
            var finyear = Session["Fin_Year"].ToString();
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var list = (from ab in DB.PurchaseOrderHeads
                        join su in DB.SupplierMasters on ab.SupplierID equals su.id
                        where ab.AdminApproval == 1 && ab.SAdminApproval == 1 && ab.Isdeleted == 0 && ab.CompanyID == companyid && ab.Fin_Year == finyear
                        orderby ab.id descending
                        // join up in DB.PurchaseOrderUpdationlogs on ab.id equals up.POid
                        select new
                        {
                            id = ab.id,
                            PONumber = ab.PONumber,
                            PODate = ab.PODate,
                            SupplierName = su.SupplierName,
                            EnteredBy = ab.EnteredBy,
                            // LastModified = up.Updatedon,
                            //Status = p.Status
                        }).ToList();



            return Json(list, JsonRequestBehavior.AllowGet);

        }
        public JsonResult ListPurchaseOrders()
        {
            var finyear = Session["Fin_Year"].ToString();
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var list = (from ab in DB.PurchaseOrderHeads
                       join su in DB.SupplierMasters on ab.SupplierID equals su.id
                       where ab.AdminApproval==0 && ab.Isdeleted==0 && ab.CompanyID==companyid && ab.Fin_Year == finyear
                        // join up in DB.PurchaseOrderUpdationlogs on ab.id equals up.POid
                        select new
                       {
                           id =ab.id,
                           PONumber = ab.PONumber,
                           PODate = ab.PODate,
                           SupplierName = su.SupplierName,
                           EnteredBy=ab.EnteredBy,
                          // LastModified = up.Updatedon,
                           //Status = p.Status
                       }).ToList();

            

            return Json(list, JsonRequestBehavior.AllowGet);
        }
        public JsonResult PurchaseOrderPendingAdmin()
        {
            var finyear = Session["Fin_Year"].ToString();
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var list = (from ab in DB.PurchaseOrderHeads
                        join su in DB.SupplierMasters on ab.SupplierID equals su.id
                        //join log in DB.PurchaseOrderUpdationlogs on ab.id equals log.POid
                        where ab.AdminApproval==0 && ab.Isdeleted == 0 && ab.CompanyID == companyid && ab.Fin_Year == finyear
                        // join up in DB.PurchaseOrderUpdationlogs on ab.id equals up.POid
                        select new
                        {
                            id = ab.id,
                            PONumber = ab.PONumber,
                            PODate = ab.PODate,
                            SupplierName = su.SupplierName,
                            EnteredBy = ab.EnteredBy,
                            // LastModified = up.Updatedon,
                            //Status = p.Status
                        }).ToList();

            return Json(list, JsonRequestBehavior.AllowGet);
        }
        public JsonResult PurchaseOrderPendingSAdmin()
        {
            var finyear = Session["Fin_Year"].ToString();
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var list = (from ab in DB.PurchaseOrderHeads
                        join su in DB.SupplierMasters on ab.SupplierID equals su.id
                        //join log in DB.PurchaseOrderUpdationlogs on ab.id equals log.POid
                        where ab.AdminApproval == 1 && ab.SAdminApproval==0 && ab.Isdeleted == 0 && ab.CompanyID == companyid && ab.Fin_Year==finyear
                        // join up in DB.PurchaseOrderUpdationlogs on ab.id equals up.POid
                        select new
                        {
                            id = ab.id,
                            PONumber = ab.PONumber,
                            PODate = ab.PODate,
                            SupplierName = su.SupplierName,
                            EnteredBy = ab.EnteredBy,
                            // LastModified = up.Updatedon,
                            //Status = p.Status
                        }).ToList();

            return Json(list, JsonRequestBehavior.AllowGet);
        }
        public JsonResult EditPurchaseOrder(int id)
        {
            var poHead = DB.PurchaseOrderHeads.Find(id);
            var poItems = DB.PurchaseOrderItems.Where(i => i.HeadID == id).ToList();

            var poHeadData = new // Create a separate object for poHead data
            {
                id = poHead.id,
                Supplierid = poHead.SupplierID,
                Companyid = poHead.CompanyID,
                PODate = poHead.PODate?.ToString("yyyy-MM-dd"),
                PONumber=poHead.PONumber,
                IndentNumber = poHead.IndentNumber,
                IndentDate = poHead.IndentDate?.ToString("yyyy-MM-dd"),
                DeliveryDate = poHead.DeliveryDate?.ToString("yyyy-MM-dd"),
                BillTaxableAmount = poHead.BillTaxableAmount,
                taxtypeonbill = poHead.TaxTypeOnBill,
                IGST = poHead.IGST,
                CGST = poHead.CGST,
                SGST = poHead.SGST,
                Billtaxamount = poHead.BillTotalTax,
                BillGrossAmount = poHead.BillGrossAmount,
                frieghttype = poHead.FrieghtType,
                freightAmount = poHead.FreightAmount,
                frieghttax = poHead.FrieghtTaxID,
                frieghttaxamount = poHead.FrieghtTax,
                ttlfrieght = poHead.TotalFrieght,
                insurancetype = poHead.InsuranceType,
                insuranceAmount = poHead.InsuranceAmount,
                Insurancetax = poHead.InsuranceTaxID,
                insurancetaxamount = poHead.InsuranceTax,
                ttlinsurace = poHead.Totalinsurance,
                pftype = poHead.PFType,
                pfCharges = poHead.PFAmount,
                PFTax = poHead.PFTaxID,
                pftaxamount = poHead.PFTax,
                ttlpf = poHead.TotalPF,
                othertype = poHead.OtherType,
                otherCharges = poHead.OtherAmount,
                OtherTax = poHead.OtherTaxID,
                othertaxamount = poHead.OtherTax,
                ttlother = poHead.TotalOther,
                deliverymode = poHead.DeliveryMode,
                remarks = poHead.Remarks,
                paymentterms = poHead.PaymentTerms,
                IsServiceOrder = poHead.IsservicePO,
            };

            var poItemsData = (from ab in DB.PurchaseOrderItems
                               join item in DB.BOMItemMasters on ab.ItemID equals item.Itemid
                               join UOM in DB.BOM_UOM on item.UOM equals UOM.id
                               join tax in DB.TaxMasters on ab.ItemTaxID equals tax.id
                               join make in DB.Make_Master on ab.MakeID equals make.id
                               where ab.HeadID == id && ab.IsDeleted==0
                               select new
                               {
                                   // Include the item ID
                                   lineid=ab.id,
                                   itemid = item.Itemid,
                                   makeid = ab.MakeID,
                                   itemcode = item.ItemCode,
                                   itemname = item.ItemName,
                                   make = make.Make,
                                   uomText = UOM.UOM,
                                   quantity = ab.Quantity,
                                   taxid = tax.id,
                                   tax = tax.Taxname,
                                   listprice = ab.ListPrice,
                                   discount = ab.DiscountPercent,
                                   netbasic = ab.NetBasic,
                                   taxableamount = ab.TaxableAmount,
                                   taxamount = ab.TaxAmount,
                                   grossamount = ab.GrossAmount
                               }).ToList();

            return Json(new { poHead = poHeadData, poItems = poItemsData }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetPrintData(int poId)
        {
            var imagePath = "";
            var Signatory = "";
            var SignatoryName = "";
            var SignatoryEmail = "";
            var Heading = "";
            var Approval = (from ss in DB.PurchaseOrderHeads
                            where ss.id == poId
                            select ss).SingleOrDefault();
            if (Approval.AdminApproval == 1 && Approval.SAdminApproval == 1)
            {
                if (Approval.BillGrossAmount > 50000)

                {
                    //imagePath = Url.Content("~/images/SignSAdmin.jpg");
                    imagePath = "~/Images/SAdminSign.png"; // Or retrieve image data and encode it
                    Signatory = "Executive Director";
                    SignatoryName = "Ajay Garg";
                    SignatoryEmail = "ajay.garg@iecgensets.com";
                    
                }
                else if (Approval.BillGrossAmount <= 50000)
                {
                    imagePath = "~/Images/AdminSign.png"; // Or retrieve image data and encode it
                    Signatory = "General Manager - Purchase";
                    SignatoryName = "Sandeep Kumar Tuteja";
                    SignatoryEmail = "+91 9316369029 | sandeep.tuteja@iecgensets.com";
                    
                }
            }
            // 1. Fetch PO Head DataGetPrintDataMRN
            var poHeadData = (from ab in DB.PurchaseOrderHeads
                              join su in DB.SupplierMasters on ab.SupplierID equals su.id
                              join co in DB.Companies on ab.CompanyID equals co.CompanyID
                              where ab.id == poId
                              select new
                              {
                                  Image = imagePath,
                                  Signatory = Signatory,
                                  SignatoryName = SignatoryName,
                                  SignatoryEmail = SignatoryEmail,
                                  CAddress=co.CompanyAddress,
                                  CAddress1=co.CompanyAddress2,
                                  CGST=co.GST,
                                  CPAN=co.PAN,
                                  CContact=co.Contact,
                                  CDestination=co.DeliveryDestination,
                                  Clogo=co.CompanyLogo,
                                  PONumber = ab.PONumber,
                                  PODate = ab.PODate,
                                  IndentNumber = ab.IndentNumber,
                                  IndentDate = ab.IndentDate,
                                  DeliveryDate = ab.DeliveryDate,
                                  companyname = co.CompanyName,
                                  comadd1 = co.CompanyAddress,
                                  comadd2 = co.CompanyAddress2,
                                  SupplierName = su.SupplierName,
                                  supplierAddress = su.SupplierAddress,
                                  supplierAddress1 = su.SupplierAddress1,
                                  supplierState = su.State,
                                  SEmail = su.Email,
                                  SupplierGST = su.GSTNumber,
                                  SupplierContactPerson = su.SupplierContactName,
                                  SupplierContact = su.ContactNumber,
                                  paymentterms = ab.PaymentTerms,
                                  remarks = ab.Remarks,
                                  deliverymode = ab.DeliveryMode,
                                  FrieghtCharge = ab.TotalFrieght,
                                  InsuranceCharge = ab.Totalinsurance,
                                  PFCharge = ab.TotalPF,
                                  OtherCharge = ab.TotalOther,
                                  TaxableAmount = ab.BillTaxableAmount,
                                  IGSTAmount = ab.IGST,
                                  SGSTAmount = ab.SGST,
                                  CGSTAmount = ab.CGST,
                                  TotalAmount = ab.BillGrossAmount,
                                  FrieghtType = ab.FrieghtType,
                                  InsuranceType = ab.InsuranceType,
                                  PFType = ab.PFType,
                                  OtherType = ab.OtherType,
                                  Frieghtamt = ab.FreightAmount,
                                  Insuranceamt = ab.InsuranceAmount,
                                  PFChargeamt = ab.PFAmount,
                                  OtherChargeamt = ab.OtherAmount,
                                  FrieghtTax = ab.FrieghtTax,
                                  InsuranceTax = ab.InsuranceTax,
                                  PFChargeTax = ab.PFTax,
                                  OtherChargeTax = ab.OtherTax,
                                  //Heading = Heading,
                                  Admin=ab.AdminApproval,
                                  SAdmin= ab.SAdminApproval,
                                  servicepo = ab.IsservicePO ?? false,



                              }).SingleOrDefault();
            var POItemDetail = (from bb in DB.PurchaseOrderItems
                                join item in DB.BOMItemMasters on bb.ItemID equals item.Itemid
                                join make in DB.Make_Master on bb.MakeID equals make.id
                                join UOM in DB.BOM_UOM on item.UOM equals UOM.id
                                join tax in DB.TaxMasters on bb.ItemTaxID equals tax.id

                                where bb.HeadID == poId && bb.IsDeleted == 0
                                select new
                                {
                                    ItemName = item.ItemName,
                                    ItemCode = item.ItemCode,
                                    UOM = UOM.UOM,
                                    Make = make.Make,
                                    NetBasic = bb.NetBasic,
                                    Quantity = bb.Quantity,
                                    ListPrice = bb.ListPrice,
                                    Discount = bb.DiscountPercent,
                                    TaxableAmount = bb.TaxableAmount,
                                    TaxAmount = bb.TaxAmount,
                                    TaxType = tax.taxpercent,
                                    GrossAmount = bb.GrossAmount,

                                }).ToList();
            if (poHeadData.Admin == 1 && poHeadData.SAdmin == 1)
            {
                Heading = "Purchase Order";
            }
            else
            {
                Heading = "Draft Copy Purchase Order";
            }

            return Json(new { poHeadData, POItemDetail, Heading }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult IndentApprovalPrint()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "IndentApprovalPrint" && ab.Status == true
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
        public JsonResult GetIndents()
        {
            var finyear = Session["Fin_Year"].ToString();
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var pendingIndents = DB.BOMIndentHeads
                .Where(i => i.ApprovalStatus == 1 && i.CompanyID==companyid && i.FinYear==finyear && i.SAdminApproval==1)
                .Select(i => new // Create an anonymous object with the data you need
                {
                    id = i.id,
                    VoucherNumber = i.VoucherNumber,
                    TotalItems = i.TotalItems,
                    CreatedBy = i.CreatedBy,
                    CreatedOn = i.CreatedOn, // Format date as needed
                })
                .ToList();

            return Json(pendingIndents, JsonRequestBehavior.AllowGet);
        }
       
        public ActionResult IndentPrintOutData(int id)
        {
            int userid =Convert.ToInt32(Session["Userid"].ToString());// = Convert.ToString();
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
                         where ab.id == id
                         select ab).SingleOrDefault();

            var getsign = DB.tbl_User_Master.Where(x => x.user_name == check.ApprovedBy).Select(x => x.SignURL).SingleOrDefault();
            if (check.ApprovalStatus == 1 )
            {
                imagePath = getsign;
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
            var CompanyDetail = (from bc in DB.Companies
                                 where bc.CompanyID == companyid
                                 select new
                                 {
                                     CompanyName = bc.CompanyName,
                                     CAddress=bc.CompanyAddress,
                                     CAddress1=bc.CompanyAddress2,
                                     CGST=bc.GST,
                                     CPAN=bc.PAN,
                                     CContact=bc.Contact,
                                     CDestination=bc.DeliveryDestination,

                                 }).SingleOrDefault();

            var IndentHeadData = (from ab in DB.BOMIndentHeads

                                      where ab.id == id
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
                                          simagepath=simagePath,
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
                                        where bb.Headid == id && stk.CompanyID==companyid && bb.IsApproved==1 && bb.Rejected==0
                                        select new
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
                                            MOQ=stk.MOQ,
                                            MinimumLevel=stk.Minstklvl,
                                            Average=bb.AverageMonthlyConsumption,
                                            Remarks=bb.UserRemarks,
                                        }).ToList();
            
            return Json(new { IndentHeadData, IndentItemDetail, CompanyDetail }, JsonRequestBehavior.AllowGet);
            
        }       

        public ActionResult IndentPrint(int id)
        {
            ViewBag.id = id;
            return View();
        }
        public ActionResult GetPrintDataMRN(int poId)
        {
            var Purchase = (from ab in DB.IEPLStockIN_Head
                            where ab.Id == poId
                            select ab).SingleOrDefault();

            // Helper function to get signature URL
            Func<string, string> GetSignatureUrl = (username) =>
            {
                if (string.IsNullOrEmpty(username))
                    return null; // Or a default image path, or an empty string

                var user = DB.tbl_User_Master.FirstOrDefault(u => u.user_name == username);
                return user != null ? user.SignURL : null; // Or a default image path
            };

            // Initialize signature variables
            string signatureImageStore = null;
            string signatureImageQuality = null;
            string signatureImagePlant = null;
            string signatureImageAccount = null;

            // Fetch usernames from MRNDetailLog and then get the signature
            var mrnLogData = DB.MRNDetailLogs.FirstOrDefault(log => log.HeadID == poId);

            if (mrnLogData != null)
            {
                signatureImageStore = GetSignatureUrl(mrnLogData.StoreApprovalBY);
                signatureImageQuality = GetSignatureUrl(mrnLogData.QualityApprovalBY);
                signatureImagePlant = GetSignatureUrl(mrnLogData.PlantHeadApprovalBY);
                signatureImageAccount = GetSignatureUrl(mrnLogData.AccountApprovalBY);
            }
            // 1. Fetch PO Head Data
            var poHeadData = (from ab in DB.IEPLStockIN_Head
                              join su in DB.SupplierMasters on ab.Supplierid equals su.id
                              join mr in DB.MRNDetails on ab.Id equals mr.HeadID into MRNGroup
                              from mr in MRNGroup.DefaultIfEmpty()
                              join mrnlog in DB.MRNDetailLogs on ab.Id equals mrnlog.HeadID into MRNDetail
                              from mrnlog in MRNDetail.DefaultIfEmpty()
                              join co in DB.Companies on ab.CompanyID equals co.CompanyID into Companygroup
                              from co in Companygroup.DefaultIfEmpty()
                              where ab.Id == poId
                              select new
                              {
                                  CAddress = co.CompanyAddress,
                                  CAddress1 = co.CompanyAddress2,
                                  CGST = co.GST,
                                  CPAN = co.PAN,
                                  CContact = co.Contact,
                                  CDestination = co.DeliveryDestination,
                                  companyname = co.CompanyName,
                                  signatureImageStore = signatureImageStore,
                                  signatureImageQuality = signatureImageQuality,
                                  signatureImagePlant = signatureImagePlant,
                                  signatureImageAccount = signatureImageAccount,
                                  storeadt = mrnlog.StoreADate,
                                  qualityadt = mrnlog.QualityADate,
                                  plantadt = mrnlog.PlantHeadADate,
                                  accountadt = mrnlog.AccountADate,
                                  PONumber = ab.PoNumber,
                                  PODate = ab.PoDate,
                                  GateEntry = ab.GateEntry,
                                  GateDate = ab.GateEntryDate,
                                  totalbillamount = ab.BillAmount,
                                  totalbillamountonbill = ab.BillAmountOnBill,
                                  MRN = ab.Vouchernumber,
                                  MRNDate = ab.CreatedDate,
                                  sbillno = ab.BillNumber,
                                  SupplierName = su.SupplierName,
                                  supplierAddress = su.SupplierAddress,
                                  supplierAddress1 = su.SupplierAddress1,
                                  supplierState = su.State,
                                  SEmail = su.Email,
                                  SupplierGST = su.GSTNumber,
                                  SupplierContactPerson = su.SupplierContactName,
                                  SupplierContact = su.ContactNumber,
                                  sbilldate=ab.BillDate,
                                  TaxAmount=ab.TaxAmount,
                                  BillTaxAmount=ab.TaxAmountOnBill,

                              }).SingleOrDefault();
            var POItemDetail = (from bb in DB.IEPLStockIN_Detail
                                join item in DB.BOMItemMasters on bb.ItemCode equals item.Itemid
                                join rejection in DB.IEPLQualityRejectionDetails on bb.Id equals rejection.PurchaseDetailid into rejectionGroup
                                from rejection in rejectionGroup.DefaultIfEmpty()
                                join reas in DB.RejectionTypes on rejection.RejectionType equals reas.id into reasGroup
                                from reas in reasGroup.DefaultIfEmpty()
                                join UOM in DB.BOM_UOM on item.UOM equals UOM.id

                                where bb.HeadId == poId
                                select new
                                {
                                    ItemName = item.ItemName,
                                    ItemCode = item.ItemCode,
                                    UOM = UOM.UOM,
                                    Quantity = bb.BillQuantity,
                                    QuantityRecieved = bb.Quantity,
                                    QuantityApproved = bb.QualityApprovedQty,
                                    QualityRejected = bb.RejectedQuantity,
                                    RejectionReturn = reas != null ? reas.RejectionType1 : "NA",

                                }).ToList();
          


            return Json(new { poHeadData, POItemDetail }, JsonRequestBehavior.AllowGet);
        }




        [HttpGet] // Specify that this action handles GET requests
        public JsonResult NewPONumber()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var finyear = Session["Fin_Year"].ToString();
            try
            {
                var getponumber = (from ab in DB.Bill_Series
                                   where ab.Type == "Purchase Order" && ab.CompanyID==companyid && ab.Fin_Year==finyear
                                   select ab).SingleOrDefault();
                string poNumber = getponumber.Series + getponumber.Number;
                

                return Json(new { success = true, poNumber = poNumber }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                // Handle potential errors
                return Json(new { success = false, message = "An error occurred: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpGet] // Specify that this action handles GET requests
        public JsonResult NewGRNumber()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var finyear = Session["Fin_Year"].ToString();
            try
            {
                var getGRnumber = (from ab in DB.Bill_Series
                                   where ab.Type == "Gate Reciept" && ab.CompanyID==companyid && ab.Fin_Year==finyear
                                   select ab).SingleOrDefault();
                string GRNumber = getGRnumber.Series + getGRnumber.Number;
               

                return Json(new { success = true, GRNumber = GRNumber }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                // Handle potential errors
                return Json(new { success = false, message = "An error occurred: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpPost]
        public JsonResult SaveStockinold(BillModel model)
        {
            this.Server.ScriptTimeout = 300;
            //this.HttpContext.Server.ScriptTimeout = 300;
            using (var dbContextTransaction = DB.Database.BeginTransaction())
            {
                try
                {
                    int companyid = Convert.ToInt32(Session["Company_ID"]);
                    var FinYear = Session["Fin_Year"].ToString();
                    IEPLStockIN_Head billHead;

                    // --- 1. HEAD LOGIC (SAVE or UPDATE) ---
                    if (model.HeadData.Id > 0)
                    {
                        billHead = DB.IEPLStockIN_Head.Find(model.HeadData.Id);
                        // Clear old details for replacement
                        var oldDetails = DB.IEPLStockIN_Detail.Where(x => x.HeadId == billHead.Id).ToList();
                        DB.IEPLStockIN_Detail.RemoveRange(oldDetails);
                    }
                    else
                    {
                        var MRNNo = DB.Bill_Series.SingleOrDefault(x => x.Type == "MRN" && x.CompanyID == companyid && x.Fin_Year == FinYear);
                        billHead = new IEPLStockIN_Head
                        {
                            Vouchernumber = MRNNo.Series + MRNNo.Number,
                            CreatedBy = Session["U_Name"].ToString(),
                            CreatedDate = DateTime.Now
                        };
                        DB.IEPLStockIN_Head.Add(billHead);
                        MRNNo.Number += 1;
                    }

                    // Map Head Data
                    billHead.BillNumber = model.HeadData.BillNumber;
                    billHead.BillDate = DateTime.Parse(model.HeadData.BillDate);
                    billHead.PoNumber = model.HeadData.pono;
                    billHead.PoDate = DateTime.Parse(model.HeadData.podate);
                    billHead.GRNumber = model.HeadData.GRNumber;
                    billHead.GRDate = DateTime.Parse(model.HeadData.GRDate);
                    billHead.Supplierid = model.HeadData.Supplier;
                    billHead.Taxid = model.HeadData.TaxHead;
                    billHead.TaxAmount = decimal.Parse(model.HeadData.Totaltax);
                    billHead.BillAmount = decimal.Parse(model.HeadData.BillAmount);
                    billHead.frieghtamount = decimal.Parse(model.HeadData.FrieghtAmount);
                    billHead.FrieghtTaxAmount = decimal.Parse(model.HeadData.frieghttax);
                    billHead.TaxAmountOnBill = decimal.Parse(model.HeadData.ActualTaxAmount);
                    billHead.BillAmountOnBill = decimal.Parse(model.HeadData.ActualBillAmount);
                    billHead.OtherTaxPercent = model.HeadData.OtherTaxPercent;
                    billHead.OtherTaxAmount = model.HeadData.OtherTaxAmount;
                    billHead.GateEntry = model.HeadData.GateEntry;
                    billHead.GateEntryDate = DateTime.Parse(model.HeadData.GateEntryDate);
                    billHead.PurchaseType = model.HeadData.purchasetype;
                    billHead.CompanyID = companyid;
                    billHead.Fin_Year = FinYear;
                    billHead.Isdeleted = 0;
                    billHead.BillClosed = 0;
                    billHead.MRNStatus = 1;
                    billHead.MRNStoreApproval = 1;
                    billHead.MRNQualityApproval = 0;
                    billHead.MRNPlantHeadApproval = 0;
                    billHead.MRNAccountApproval = 0;
                    billHead.DeletionRemarks = "NA";

                    DB.SaveChanges(); // Persist Head

                    // --- 2. DETAIL LOGIC ---
                    foreach (var det in model.TableData)
                    {
                        int itemid = DB.BOMItemMasters.Where(m => m.ItemCode == det.ItemCode).Select(m => m.Itemid).FirstOrDefault();
                        DB.IEPLStockIN_Detail.Add(new IEPLStockIN_Detail
                        {
                            HeadId = billHead.Id,
                            ItemCode = itemid,
                            BillQuantity = decimal.Parse(det.BillQuantity),
                            Quantity = decimal.Parse(det.Quantity),
                            BasicPrice = decimal.Parse(det.BasicPrice),
                            LotNo = det.LotNo,
                            TaxAmount = decimal.Parse(det.Tax),
                            TotalAmount = decimal.Parse(det.TotalPrice),
                            Taxid = det.taxid,
                            CompanyID = companyid,
                            IsDeleted = 0,
                            QualityApprovedQty = 0,
                            RejectedQuantity = 0,
                            RejectionReturn = 0,
                        });
                    }
                    var check = DB.MRNDetailLogs.FirstOrDefault(ab => ab.HeadID == billHead.Id);

                    // 2. If check is null, it means the record does NOT exist
                    if (check == null)
                    {
                        MRNDetailLog mdl = new MRNDetailLog();

                        mdl.HeadID = billHead.Id;

                        // 3. Safe Session check using null-conditional operator
                        mdl.StoreApprovalBY = Session["U_Name"]?.ToString() ?? "System";
                        mdl.StoreADate = DateTime.Now;

                        DB.MRNDetailLogs.Add(mdl);

                        // Note: Don't forget to call DB.SaveChanges() later in your code!
                    }

                    DB.SaveChanges();
                    dbContextTransaction.Commit();
                    return Json(new
                    {
                        success = true,
                        message = "Bill saved successfully!",
                        voucherNumber = billHead.Vouchernumber // Make sure this property is sent!
                    });
                }
                catch (Exception ex)
                {
                    dbContextTransaction.Rollback();
                    return Json(new { success = false, message = "Critical Error: " + ex.Message });
                }
            }
        }
        [HttpPost]
        public JsonResult SaveStockin(string jsonData)
        {
            // Increase server timeout for large batch processing
            this.Server.ScriptTimeout = 600;

            // Manually deserialize the string into your BillModel class
            var model = Newtonsoft.Json.JsonConvert.DeserializeObject<BillModel>(jsonData);

            using (var dbContextTransaction = DB.Database.BeginTransaction())
            {
                try
                {
                    if (model == null || model.HeadData == null)
                    {
                        return Json(new { success = false, message = "Invalid data received." });
                    }

                    int companyid = Convert.ToInt32(Session["Company_ID"]);
                    var FinYear = Session["Fin_Year"].ToString();
                    IEPLStockIN_Head billHead;

                    // --- 1. HEAD LOGIC ---
                    if (model.HeadData.Id > 0)
                    {
                        billHead = DB.IEPLStockIN_Head.Find(model.HeadData.Id);
                        var oldDetails = DB.IEPLStockIN_Detail.Where(x => x.HeadId == billHead.Id).ToList();
                        DB.IEPLStockIN_Detail.RemoveRange(oldDetails);
                    }
                    else
                    {
                        var MRNNo = DB.Bill_Series.SingleOrDefault(x => x.Type == "MRN" && x.CompanyID == companyid && x.Fin_Year == FinYear);
                        billHead = new IEPLStockIN_Head
                        {
                            Vouchernumber = MRNNo.Series + MRNNo.Number,
                            CreatedBy = Session["U_Name"]?.ToString() ?? "System",
                            CreatedDate = DateTime.Now
                        };
                        DB.IEPLStockIN_Head.Add(billHead);
                        MRNNo.Number += 1;
                    }

                    // Map Head Fields
                    billHead.BillNumber = model.HeadData.BillNumber;
                    billHead.BillDate = DateTime.Parse(model.HeadData.BillDate);
                    billHead.PoNumber = model.HeadData.pono;
                    billHead.PoDate = DateTime.Parse(model.HeadData.podate);
                    billHead.GRNumber = model.HeadData.GRNumber;
                    billHead.GRDate = DateTime.Parse(model.HeadData.GRDate);
                    billHead.Supplierid = model.HeadData.Supplier;
                    billHead.Taxid = model.HeadData.TaxHead;
                    billHead.TaxAmount = decimal.Parse(model.HeadData.Totaltax);
                    billHead.BillAmount = decimal.Parse(model.HeadData.BillAmount);
                    billHead.frieghtamount = decimal.Parse(model.HeadData.FrieghtAmount);
                    billHead.FrieghtTaxAmount = decimal.Parse(model.HeadData.frieghttax);
                    billHead.TaxAmountOnBill = decimal.Parse(model.HeadData.ActualTaxAmount);
                    billHead.BillAmountOnBill = decimal.Parse(model.HeadData.ActualBillAmount);
                    billHead.OtherTaxPercent = model.HeadData.OtherTaxPercent;
                    billHead.OtherTaxAmount = model.HeadData.OtherTaxAmount;
                    billHead.GateEntry = model.HeadData.GateEntry;
                    billHead.GateEntryDate = DateTime.Parse(model.HeadData.GateEntryDate);
                    billHead.PurchaseType = model.HeadData.purchasetype;
                    billHead.CompanyID = companyid;
                    billHead.Fin_Year = FinYear;
                    billHead.Isdeleted = 0;
                    billHead.BillClosed = 0;
                    billHead.MRNStatus = 1;
                    billHead.MRNStoreApproval = 1;
                    billHead.MRNQualityApproval = 0;
                    billHead.MRNPlantHeadApproval = 0;
                    billHead.MRNAccountApproval = 0;
                    billHead.DeletionRemarks = "NA";

                    DB.SaveChanges();

                    // --- 2. DETAIL LOGIC ---
                    foreach (var det in model.TableData)
                    {
                        // Optimization: You might want to cache ItemCodes in a dictionary if this loop is too slow
                        int itemid = DB.BOMItemMasters.Where(m => m.ItemCode == det.ItemCode).Select(m => m.Itemid).FirstOrDefault();

                        DB.IEPLStockIN_Detail.Add(new IEPLStockIN_Detail
                        {
                            HeadId = billHead.Id,
                            ItemCode = itemid,
                            BillQuantity = decimal.Parse(det.BillQuantity),
                            Quantity = decimal.Parse(det.Quantity),
                            BasicPrice = decimal.Parse(det.BasicPrice),
                            LotNo = det.LotNo,
                            TaxAmount = decimal.Parse(det.Tax),
                            TotalAmount = decimal.Parse(det.TotalPrice),
                            Taxid = det.taxid,
                            CompanyID = companyid,
                            IsDeleted = 0,
                            QualityApprovedQty = 0,
                            RejectedQuantity = 0,
                            RejectionReturn = 0,
                        });
                    }

                    // --- 3. LOGGING ---
                    var check = DB.MRNDetailLogs.FirstOrDefault(ab => ab.HeadID == billHead.Id);
                    if (check == null)
                    {
                        DB.MRNDetailLogs.Add(new MRNDetailLog
                        {
                            HeadID = billHead.Id,
                            StoreApprovalBY = Session["U_Name"]?.ToString() ?? "System",
                            StoreADate = DateTime.Now
                        });
                    }

                    DB.SaveChanges();
                    dbContextTransaction.Commit();

                    return Json(new { success = true, message = "MRN saved successfully!", voucherNumber = billHead.Vouchernumber });
                }
                catch (Exception ex)
                {
                    dbContextTransaction.Rollback();
                    return Json(new { success = false, message = "Error: " + ex.Message });
                }
            }
        }
        [HttpGet]
        public JsonResult GetMRNDetails(string vouchernumber)
        {
            try
            {
                IECEntities DB = new IECEntities();

                // 1. Retrieve required session variables
                int companyid = Convert.ToInt32(Session["Company_ID"]);
                string currentFinYear = Session["Fin_Year"].ToString(); // Get active Fin Year from Session

                // 2. Fetch the Header Record with Fin_Year filter
                var head = DB.IEPLStockIN_Head.FirstOrDefault(x =>
                                x.Vouchernumber == vouchernumber &&
                                x.CompanyID == companyid &&
                                x.Fin_Year == currentFinYear && // <-- CRITICAL: Check current fin year
                                x.Isdeleted == 0);

                if (head == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"MRN {vouchernumber} not found in current Financial Year ({currentFinYear})."
                    }, JsonRequestBehavior.AllowGet);
                }

                // 3. Optional: Prevent modification of Closed/Approved bills
                if (head.BillClosed == 1)
                {
                    return Json(new { success = false, message = "This MRN is already finalized/closed and cannot be modified." }, JsonRequestBehavior.AllowGet);
                }

                // 4. Prepare Header Data
                var headData = new
                {
                    Id = head.Id,
                    BillNumber = head.BillNumber,
                    BillDate = head.BillDate.ToString("yyyy-MM-dd"),
                    Supplier = head.Supplierid,
                    pono = head.PoNumber,
                    podate = head.PoDate?.ToString("yyyy-MM-dd"),
                    GRNumber = head.GRNumber,
                    GRDate = head.GRDate?.ToString("yyyy-MM-dd"),
                    GateEntry = head.GateEntry,
                    GateEntryDate = head.GateEntryDate?.ToString("yyyy-MM-dd"),
                    TaxHead = head.Taxid,
                    Totaltax = head.TaxAmount,
                    BillAmount = head.BillAmount,
                    FrieghtAmount = head.frieghtamount,
                    frieghttax = head.FrieghtTaxAmount,
                    ActualBillAmount = head.BillAmountOnBill,
                    ActualTaxAmount = head.TaxAmountOnBill,
                    OtherTaxPercent = head.OtherTaxPercent,
                    OtherTaxAmount = head.OtherTaxAmount,
                    purchasetype = head.PurchaseType
                };

                // 5. Fetch Details
                var details = (from det in DB.IEPLStockIN_Detail
                               join item in DB.BOMItemMasters on det.ItemCode equals item.Itemid
                               where det.HeadId == head.Id && det.IsDeleted == 0
                               select new
                               {
                                   ItemCode = item.ItemCode,
                                   ItemName = item.ItemName,
                                   LotNo = det.LotNo,
                                   BasicPrice = det.BasicPrice,
                                   Quantity = det.Quantity,
                                   Tax = det.TaxAmount,
                                   TotalPrice = det.TotalAmount,
                                   taxid = det.Taxid
                               }).ToList();

                return Json(new { success = true, head = headData, details = details }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "System Error: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpPost]
        public JsonResult SaveStockinold1(BillModel model)
        {
            if (ModelState.IsValid)
            {

                int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
                var FinYear = Session["Fin_Year"].ToString();

                IECEntities DB = new IECEntities();


                var MRNNo = (from bb in DB.Bill_Series
                             where bb.Type == "MRN" && bb.CompanyID == companyid && bb.Fin_Year == FinYear
                             select bb).SingleOrDefault();
                // Save the bill head data
                IEPLStockIN_Head billHead = new IEPLStockIN_Head
                {

                    BillNumber = model.HeadData.BillNumber,
                    BillDate = DateTime.Parse(model.HeadData.BillDate),
                    PoNumber = model.HeadData.pono,
                    PoDate = DateTime.Parse(model.HeadData.podate),
                    GRNumber = model.HeadData.GRNumber,
                    GRDate = DateTime.Parse(model.HeadData.GRDate),
                    Supplierid = (model.HeadData.Supplier),
                    Vouchernumber = MRNNo.Series + MRNNo.Number,
                    Taxid = model.HeadData.TaxHead,
                    TaxAmount = decimal.Parse(model.HeadData.Totaltax),
                    BillAmount = decimal.Parse(model.HeadData.BillAmount),
                    frieghtamount = decimal.Parse(model.HeadData.FrieghtAmount),
                    FrieghtTaxAmount=decimal.Parse(model.HeadData.frieghttax),
                    CreatedBy = Session["U_Name"].ToString(),
                    CreatedDate = System.DateTime.Now,
                    BillClosed = 0,
                    MRNStatus = 1,
                    TaxAmountOnBill = decimal.Parse(model.HeadData.ActualTaxAmount),
                    BillAmountOnBill = decimal.Parse(model.HeadData.ActualBillAmount),
                    OtherTaxPercent = (decimal?)(model.HeadData.OtherTaxPercent) ?? 0,
                    OtherTaxAmount = (decimal?)(model.HeadData.OtherTaxAmount) ?? 0,
                    GateEntry = model.HeadData.GateEntry,
                    GateEntryDate = DateTime.Parse(model.HeadData.GateEntryDate),
                    CompanyID = companyid,
                    Fin_Year = FinYear,
                    MRNStoreApproval = 1,
                    MRNQualityApproval = 0,
                    MRNPlantHeadApproval = 0,
                    MRNAccountApproval = 0,
                    PurchaseType = model.HeadData.purchasetype,
                    Isdeleted = 0,
                    DeletionRemarks="NA",

                };


                DB.IEPLStockIN_Head.Add(billHead);

                //Increase Number is Series
                var updateseries = (from jj in DB.Bill_Series
                                    where jj.Type == "MRN" && jj.CompanyID == companyid && jj.Fin_Year == FinYear
                                    select jj).SingleOrDefault();
                if (updateseries != null)
                {
                    updateseries.Number += 1;
                }
                else
                {
                    return Json(new { success = false, message = "Bill Series Not Updated" });
                }

                DB.SaveChanges();

                // Save the bill detail data
                int headId = billHead.Id;
                foreach (var detail in model.TableData)
                {
                    int itemCodeId = DB.BOMItemMasters
                        .Where(im => im.ItemCode == detail.ItemCode)
                        .Select(im => im.Itemid)
                        .FirstOrDefault();

                    if (itemCodeId > 0)
                    {
                        IEPLStockIN_Detail billDetail = new IEPLStockIN_Detail
                        {
                            HeadId = headId,
                            ItemCode = itemCodeId, // use the ID instead of Code
                            BillQuantity = decimal.Parse(detail.BillQuantity),
                            Quantity = decimal.Parse(detail.Quantity),
                            BasicPrice = decimal.Parse(detail.BasicPrice),
                            LotNo = detail.LotNo,
                            TaxAmount = decimal.Parse(detail.Tax),
                            TotalAmount = decimal.Parse(detail.TotalPrice),
                            QualityApproved = 0, // default value, replace with actual value
                            QualityApprovedQty = 0,
                            RejectedQuantity = 0,
                            Taxid = detail.taxid,
                            RejectionReturn = 0,
                            CompanyID = companyid,
                            IsDeleted=0,

                            //QApprovedDate = null
                        };

                        DB.IEPLStockIN_Detail.Add(billDetail);
                    }


                    else
                    {
                        // Handle the case where the ItemCode is not found
                        Console.WriteLine($"ItemCode {detail.ItemCode} not found.");
                    }
                }
                //Update Basic Price if its not coming back from production Line Supplier id of production line is 157
                if (model.HeadData.Supplier != 157)
                {
                    foreach (var br in model.TableData)
                    {
                        var RU = (from ab in DB.BOMItemMasters
                                  where ab.ItemCode == br.ItemCode
                                  select ab).FirstOrDefault();
                        RU.BasicPrice = decimal.Parse(br.BasicPrice);
                        //Update Basic Price in Stock Table
                        //Get Item ID
                        int itemCodeId = DB.BOMItemMasters
                        .Where(im => im.ItemCode ==br.ItemCode)
                        .Select(im => im.Itemid)
                        .FirstOrDefault();

                        var RU1 = (from ab in DB.StockTables
                                   where ab.itemid == itemCodeId && ab.CompanyID == companyid
                                   select ab).FirstOrDefault();
                        if (RU1 != null)
                        {
                            RU1.BasicPrice = decimal.Parse(br.BasicPrice);
                        }
                    }
                }
                //Update MRN Log
                MRNDetailLog MDL = new MRNDetailLog();
                {
                    MDL.HeadID = billHead.Id;
                    MDL.StoreApprovalBY = Session["U_Name"].ToString();
                    MDL.StoreADate = System.DateTime.Now;
                    DB.MRNDetailLogs.Add(MDL);

                }


                DB.SaveChanges();

                var getlastvoucher = (from ab in DB.IEPLStockIN_Head
                                      orderby ab.Id descending
                                      select ab.Id).FirstOrDefault();

                return Json(new
                {
                    success = true,
                    message = "Bill saved successfully!",
                    voucherNumber = billHead.Vouchernumber
                });
            }
            else
            {
                return Json(new { success = false, message = "Error saving bill. Please check the input data." });
            }
        }
        public ActionResult GetPurchaseType()
        {
            var purchaseTypes = DB.PurchaseTypes
            .Select(pt => new 
            {
                value = pt.id,
                text = pt.Type
                
            })
            .ToList();

            return Json(new { success=true, data = purchaseTypes },JsonRequestBehavior.AllowGet) ;
        }

        //Used for MRN Generation
        public ActionResult GetItemsDetail(int id)
        {
           // int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            // Retrieve the items detail data from the database or repository
            var itemsDetail = (from ab in DB.IEPLStockIN_Detail
                               join item in DB.BOMItemMasters on ab.ItemCode equals item.Itemid                               
                               join rejection in DB.IEPLQualityRejectionDetails on ab.Id equals rejection.PurchaseDetailid into rejectionGroup
                               from rejection in rejectionGroup.DefaultIfEmpty()
                               join reas in DB.RejectionTypes on rejection.RejectionType equals reas.id into reasGroup
                               from reas in reasGroup.DefaultIfEmpty()
                               join tax in DB.TaxMasters on ab.Taxid equals tax.id
                               where ab.HeadId == id //&& item.CompanyID==companyid
                               select new
                               {
                                   ItemCode = item.ItemCode,
                                   BillQuantity = ab.BillQuantity,
                                   Quantity = ab.Quantity,
                                   QualityApprovedQty = ab.QualityApprovedQty,
                                   RejectedQuantity = ab.RejectedQuantity,
                                   BasicPrice = ab.BasicPrice,
                                   Tax = tax.Taxname,
                                   TaxAmount = ab.TaxAmount,
                                   TotalAmount = ab.TotalAmount,                                   
                                   RejectionReturn = reas != null ? reas.RejectionType1 : "NA",
                                   
                               }).ToList();

            // Retrieve the bill details data from the database or repository
            var billDetails = (from bh in DB.IEPLStockIN_Head
                               join supplier in DB.SupplierMasters on bh.Supplierid equals supplier.id
                               join MRN in DB.MRNDetails on bh.Id equals MRN.HeadID into mrnGroup // Use 'into' to create a group
                               from MRN in mrnGroup.DefaultIfEmpty() // Allow nulls from the MRN join
                               where bh.Id == id
                               select new
                               {
                                   BillNo = bh.BillNumber,
                                   BillDate = bh.BillDate,
                                   SupplierName = supplier.SupplierName,
                                   TotalAmount = bh.BillAmount,
                                   GRNumber = bh.GRNumber,
                                   GRDate = bh.GRDate,
                                   PONumber = bh.PoNumber,
                                   PODate = bh.PoDate,
                                   MRNNo = MRN != null ? MRN.MRNNumber : null, // Handle null MRN
                                   MRNDate = MRN != null ? MRN.MRNDate : null // Handle null MRN
                               }).FirstOrDefault();

            // Return the data as JSON
            return Json(new { itemsDetail, billDetails }, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public ActionResult generateMRN(int BILLID)
        {
            try
            {
                //check MRN if already exist
                var check = (from nn in DB.MRNDetails
                             where nn.HeadID == BILLID
                             select nn).Count();
                if (check > 0)
                {
                    return Json(new { success = false, error= "MRN Already Exist Against this Voucher"});
                    }
                else
                { 
                //Create MRN Number 
                MRNDetail MR = new MRNDetail();
                var lastRecordId = (from ab in DB.MRNDetails
                                    select ab.id).ToList().LastOrDefault();
                MR.MRNNumber = "MRN-" + (Convert.ToInt32(lastRecordId) + 1);
                MR.MRNDate = System.DateTime.Now;
                MR.CreatedBy = Session["U_Name"].ToString();
                MR.HeadID = BILLID;
                DB.MRNDetails.Add(MR);



                //Update MRN Status in Purchase Head Table

                var selectline = (from ab in DB.IEPLStockIN_Head
                                  where ab.Id == BILLID
                                  select ab).SingleOrDefault();
                    selectline.MRNStatus = 1;
                    selectline.MRNStoreApproval = 1;
                    DB.SaveChanges();
                    return Json(new
                    {
                        success = true,
                        mrn = MR.MRNNumber,  // Use 'mrn' instead of 'mrnNumber'
                        mrndate = MR.MRNDate  // Format the date as needed
                    });
                }

            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error generating MRN: " + ex.Message });

            }

                
        }
        public ActionResult GetItemsDetailold(int id)
        {
            // Retrieve the items detail data from the database or repository
            var itemsDetail = (from ab in DB.IEPLStockIN_Detail
                               join item in DB.BOMItemMasters on ab.ItemCode equals item.Itemid
                               join tax in DB.TaxMasters on ab.Taxid equals tax.id
                               where ab.HeadId == id
                               select new
                               {
                                   ItemCode=item.ItemCode,
                                   BillQuantity=ab.BillQuantity,
                                   Quantity=ab.Quantity,
                                   QualityApprovedQty = ab.QualityApprovedQty,
                                   RejectedQuantity=ab.RejectedQuantity,
                                   BasicPrice=ab.BasicPrice,
                                   Tax=tax.Taxname,
                                   TaxAmount=ab.TaxAmount,
                                   TotalAmount=ab.TotalAmount,

                               }).ToList();

            // Return the data as JSON
            return Json(itemsDetail, JsonRequestBehavior.AllowGet);
        }
        public JsonResult GetTaxes()
        {
            // Replace with your data access logic to retrieve the list of taxes
            var tax = (from ab in DB.TaxMasters
                       select new
                       {
                           value = ab.id,
                           text = ab.Taxname,
                           percent = ab.taxpercent,
                           type = ab.Type

                       }).ToList();

            // Convert the list to a JSON-friendly format


            return Json(tax, JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetTaxType()
        {
            var taxtype = (from ab in DB.TaxTypes
                           where ab.Isdeleted == false
                           select new
                           {
                               value = ab.ID,
                               text = ab.TaxType1
                           }).ToList();
            return Json(taxtype, JsonRequestBehavior.AllowGet);
        }
        public ActionResult Indent()

        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "Indent" && ab.Status == true
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
        public ActionResult dropdown(string selectedValue)
        {

            List<string> data = new List<string>();
            if (selectedValue == "Alternator")
            {

                data.Add("1 Phase");
                data.Add("3 Phase");

            }

            else if (selectedValue == "Engine")
            {

                data.Add("L1");
                data.Add("L3");

            }

            else
            {
                data.Add("NA");

            }

            return new JsonResult { Data = data, JsonRequestBehavior = JsonRequestBehavior.AllowGet };


        }

        public ActionResult MRNPrint()
        {
            return View();
        }
        public ActionResult MRNReprint()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "MRNReprint" && ab.Status == true
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
        public JsonResult GetMRNData()
        {
            // Execute the SQL query using your data access layer
            var mrndata = (from ab in DB.IEPLStockIN_Head
                               //join item in DB.IEPLStockIN_Detail on ab.Id equals item.HeadId
                           join supplier in DB.SupplierMasters on ab.Supplierid equals supplier.id
                           where ab.BillClosed == 1 && ab.MRNStatus == 0
                           select new
                           {
                               id = ab.Id,
                               SupplierName = supplier.SupplierName,
                               BillNo = ab.BillNumber,
                               BillDate = ab.BillDate,
                               VoucherNo = ab.Vouchernumber,
                               VoucherDate = ab.CreatedDate,
                               GRNo = ab.GRNumber,
                               GRDate = ab.GRDate,
                               FrieghtAmount = ab.frieghtamount,
                               Taxamount = ab.TaxAmount,
                               OtherTax = ab.OtherTaxAmount,
                               BillAmount = ab.BillAmount,
                               BillAmountonBill = ab.BillAmountOnBill,
                               TaxAmountonBill = ab.TaxAmountOnBill

                           }).ToList();


            return Json(mrndata, JsonRequestBehavior.AllowGet);
        }
        public JsonResult ReprintMRNData()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var FinYear = Session["Fin_Year"].ToString();
            var mrndata = (from ab in DB.IEPLStockIN_Head
                               //join item in DB.IEPLStockIN_Detail on ab.Id equals item.HeadId
                           join supplier in DB.SupplierMasters on ab.Supplierid equals supplier.id
                           where ab.BillClosed == 1 && ab.MRNStatus == 1 && ab.CompanyID==companyid && ab.Fin_Year==FinYear && ab.MRNStoreApproval==1 && ab.MRNQualityApproval==1 && ab.MRNPlantHeadApproval==1 && ab.MRNAccountApproval==1
                           orderby ab.Id descending
                           select new
                           {
                               id = ab.Id,
                               SupplierName = supplier.SupplierName,
                               BillNo = ab.BillNumber,
                               BillDate = ab.BillDate,
                               VoucherNo = ab.Vouchernumber,
                               VoucherDate = ab.CreatedDate,
                               GRNo = ab.GRNumber,
                               GRDate = ab.GRDate,
                               FrieghtAmount = ab.frieghtamount,
                               Taxamount = ab.TaxAmount,
                               OtherTax = ab.OtherTaxAmount,
                               BillAmount = ab.BillAmount,
                               BillAmountonBill = ab.BillAmountOnBill,
                               TaxAmountonBill = ab.TaxAmountOnBill

                           }).ToList();


            return Json(mrndata, JsonRequestBehavior.AllowGet);

        }

        public ActionResult PurchaseDetailReport()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "PurchaseDetailReport" && ab.Status == true
                               select ab).SingleOrDefault();
            if (checkaccess == null)
            {
                // 3. Store error message in TempData (survives one redirect)
                TempData["ErrorMessage"] = "Access Denied: You are not authorized to view This Page.";

                // 4. Redirect to Home Controller, Index Action
                return RedirectToAction("Dashboard", "Dashboard");
            }
            return View();
        }
        public ActionResult PurchaseDetailData(string from, string to)
        {
            // Parse dates from the string parameters
            DateTime fromDateDt = DateTime.ParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            DateTime toDateDt = DateTime.ParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture);

            // FIX: Set the 'to' date to the end of the day (23:59:59) 
            // This ensures records created during today's business hours are included.
            toDateDt = toDateDt.AddDays(1).AddTicks(-1);

            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var FinYear = Session["Fin_Year"].ToString();

            var data = (from ab in DB.IEPLStockIN_Head
                        join bb in DB.IEPLStockIN_Detail on ab.Id equals bb.HeadId
                        join sup in DB.SupplierMasters on ab.Supplierid equals sup.id
                        join item in DB.BOMItemMasters on bb.ItemCode equals item.Itemid
                        join UOM in DB.BOM_UOM on item.UOM equals UOM.id
                        where ab.CompanyID == companyid
                        && ab.Fin_Year == FinYear
                        && ab.Isdeleted == 0
                        && bb.IsDeleted == 0
                        && ab.CreatedDate >= fromDateDt
                        && ab.CreatedDate <= toDateDt // Now correctly includes the full 'to' day
                        orderby ab.CreatedDate descending
                        select new
                        {
                            BillNumber = ab.BillNumber,
                            BillDate = ab.BillDate,
                            PONumber = ab.PoNumber,
                            PODate = ab.PoDate,
                            VoucherDate = ab.CreatedDate,
                            SupplierName = sup.SupplierName,
                            ItemName = item.ItemName,
                            ItemCode = item.ItemCode,
                            LotNo=bb.LotNo ?? "NA",
                            UOM = UOM.UOM,
                            BasicPrice = bb.BasicPrice,
                            Quantity = bb.Quantity,
                            Approved = bb.QualityApprovedQty,
                            Rejected = bb.RejectedQuantity,
                            TotalTax = bb.TaxAmount,
                            TotalAmount = bb.TotalAmount,
                            GateEntryDate = ab.GateEntryDate,
                            GateEntryNo = ab.GateEntry,
                            GRNo = ab.GRNumber,
                            GRDate = ab.GRDate,
                           
                        }).ToList();

            return Json(data, JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetCompanies()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var Companies = from ab in DB.Companies
                            where ab.CompanyID == companyid
                             select new
                             {
                                 value = ab.CompanyID,
                                 text = ab.CompanyName,
                             };
            return Json(Companies, JsonRequestBehavior.AllowGet);

        }
        [HttpPost]
        public ActionResult TestAction()
        {
            return Json("Test successful!");
        }

        public ActionResult PurchaseOrder(int? id)
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "PurchaseOrder" && ab.Status == true
                               select ab).SingleOrDefault();
            if (checkaccess == null)
            {
                // 3. Store error message in TempData (survives one redirect)
                TempData["ErrorMessage"] = "Access Denied: You are not authorized to view This Page";

                // 4. Redirect to Home Controller, Index Action
                return RedirectToAction("Dashboard", "Dashboard");
            }
            return View(id);
        }
        public ActionResult GetIndentNumbers()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var FinYear = Session["Fin_Year"].ToString();
            var indent = (from ab in DB.BOMIndentHeads
                          where ab.CompanyID == companyid && ab.FinYear == FinYear
                          select new
                          {
                              value = ab.id,
                              text = ab.VoucherNumber,
                          }).ToList();
            return Json(new { success = true, indent }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetIndentDate(int indid)
        {
            var date = (from ab in DB.BOMIndentHeads
                        where ab.id == indid
                        select ab.CreatedOn).SingleOrDefault();
            return Json(new { success=true,IndentDate=date},JsonRequestBehavior.AllowGet);

        }

        public JsonResult GetSupplier()
        {
            var depa = (from ab in DB.SupplierMasters
                        select new
                        {
                            text = ab.SupplierName,
                            value = ab.id
                        }).ToList();
            return Json(depa, JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetMake()
        {
            var Make = from ab in DB.Make_Master
                       select new
                       {
                           value = ab.id,
                           text = ab.Make,

                       };
            return Json(Make, JsonRequestBehavior.AllowGet);

        }


        public JsonResult GetSupplierDetail(int Supplierid)
        {
            var SupplierData = (from ab in DB.SupplierMasters
                                where ab.id == Supplierid
                                select new
                                {
                                    Address = ab.SupplierAddress,
                                    Email = ab.Email,
                                    Contact = ab.ContactNumber,
                                    GST = ab.GSTNumber,
                                }).SingleOrDefault();
            return Json(SupplierData, JsonRequestBehavior.AllowGet);
        }
        public ActionResult ReversePO()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "ReversePO" && ab.Status == true
                               select ab).SingleOrDefault();
            if (checkaccess == null)
            {
                // 3. Store error message in TempData (survives one redirect)
                TempData["ErrorMessage"] = "Access Denied: You are not authorized to view This Page.";

                // 4. Redirect to Home Controller, Index Action
                return RedirectToAction("Dashboard", "Dashboard");
            }
            return View();
        }
        [HttpGet]
        public JsonResult GetAllPurchaseOrders()
        {
            try
            {
                int compId = Convert.ToInt32(Session["Company_ID"]);
                string finYear = Session["Fin_Year"].ToString();

                var poList = (from h in DB.PurchaseOrderHeads
                              join s in DB.SupplierMasters on h.SupplierID equals s.id
                              where h.CompanyID == compId && h.Fin_Year == finYear && h.Isdeleted == 0
                              orderby h.PODate descending
                              select new
                              {
                                  h.id,
                                  h.PONumber,
                                  PODate = h.PODate,
                                  Supplier = s.SupplierName,
                                  Amount = h.BillGrossAmount,
                                  Admin = h.AdminApproval ?? 0,
                                  SAdmin = h.SAdminApproval ?? 0
                              }).ToList()
                              .Select(x => new {
                                  x.id,
                                  x.PONumber,
                                  Date = x.PODate.Value.ToString("dd-MM-yyyy"),
                                  x.Supplier,
                                  Amount = x.Amount.Value.ToString("N2"),
                                  // A PO is reversible if AT LEAST one person has approved it
                                  IsReversible = (x.Admin == 1 || x.SAdmin == 1),
                                  StatusText = (x.Admin == 1 && x.SAdmin == 1) ? "Fully Approved" :
                                               (x.Admin == 1 || x.SAdmin == 1) ? "Partially Approved" : "Pending"
                              }).ToList();

                return Json(new { success = true, data = poList }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }
        [HttpGet]
        public JsonResult GetPODetails(string poNumber)
        {
            try
            {
                int compId = Convert.ToInt32(Session["Company_ID"]);

                var po = (from h in DB.PurchaseOrderHeads
                          join s in DB.SupplierMasters on h.SupplierID equals s.id
                          where h.PONumber == poNumber && h.CompanyID == compId && h.Isdeleted == 0
                          select new
                          {
                              h.id,
                              h.PONumber,
                              h.PODate,
                              Supplier = s.SupplierName,
                              Amount = h.BillGrossAmount,
                              Status = (h.AdminApproval == 1 && h.SAdminApproval == 1) ? "Fully Approved" : "Pending/Revised"
                          }).FirstOrDefault();

                if (po == null) return Json(new { success = false, message = "PO Number not found." }, JsonRequestBehavior.AllowGet);

                return Json(new { success = true, data = po }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet); }
        }

        [HttpPost]
        public JsonResult ExecuteReversePO(int id)
        {
            try
            {
                var po = DB.PurchaseOrderHeads.FirstOrDefault(x => x.id == id);
                if (po == null) return Json(new { success = false, message = "Record not found." });

                // RESET APPROVALS TO ZERO
                po.AdminApproval = 0;
                po.SAdminApproval = 0;

                DB.SaveChanges();

                return Json(new { success = true, message = "PO status has been successfully reset to Pending." });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }


        public class BillModel
        {
            public HeadData HeadData { get; set; }
            public List<TableData> TableData { get; set; }
        }

        public class HeadData
        {
            public int? Id { get; set; } // Added for Modification
            public string BillNumber { get; set; }
            public string BillDate { get; set; }
            public string pono { get; set; }
            public string podate { get; set; }
            public string GRNumber { get; set; }
            public string GRDate { get; set; }
            public string GateEntry { get; set; }
            public string GateEntryDate { get; set; }
            public int Supplier { get; set; }
            public int TaxHead { get; set; }
            public string Totaltax { get; set; }
            public string BillAmount { get; set; }
            public string FrieghtAmount { get; set; }
            public string frieghttax { get; set; }
            public string ActualBillAmount { get; set; }
            public string ActualTaxAmount { get; set; }
            public decimal OtherTaxPercent { get; set; }
            public decimal OtherTaxAmount { get; set; }
            public int purchasetype { get; set; }
        
        }

        public class TableData
        {
            public string ItemCode { get; set; }
            public string ItemName { get; set; }
            public string UOM { get; set; }
            public string LotNo { get; set; }
            public string BillQuantity { get; set; }
            public string Quantity { get; set; }
            public string BasicPrice { get; set; }
            public string Tax { get; set; }
            public string TotalPrice { get; set; }
            public int taxid { get; set; }
        }

    }


}

