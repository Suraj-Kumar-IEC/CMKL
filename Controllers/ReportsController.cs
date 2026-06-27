using ClosedXML.Excel;
using CMKL.Models;
using Microsoft.Ajax.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;
using ClosedXML.Excel;
using static ClosedXML.Excel.XLPredefinedFormat;
using DocumentFormat.OpenXml.EMMA;
using Grpc.Core;
using System.Reflection.Emit;
using Org.BouncyCastle.Asn1.Mozilla;
using DocumentFormat.OpenXml.Vml;
using System.Globalization; // Required for CultureInfo
using System.Web.Services.Description;
using System.Text.RegularExpressions;
using System.Web.Management;
using DateTime = System.DateTime;


namespace CMKL.Controllers
{
    public class ReportsController : Controller
    {
        IECEntities DB = new IECEntities();
        // GET: Reports
        
        public ActionResult Index()
        {
            // var finyear = from DB.
            var Fin = (from ab in DB.CMKL_FinYear
                      select ab).ToList();
            ViewBag.finyear = new SelectList(Fin, "id", "finyear");
            return View();
        }
        public ActionResult ItemMasterReport()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "ItemMasterReport" && ab.Status == true
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
        public ActionResult SaleReportStores()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "SaleReportStores" && ab.Status == true
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
        public ActionResult MaterialRecieptReport()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "MaterialRecieptReport" && ab.Status == true
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
        public ActionResult GetMaterialReceiptReport(string from, string to, int? supplierId = 0)
        {
            try
            {
                // Parse dates
                DateTime fromDateDt = DateTime.ParseExact(from, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                DateTime toDateDt = DateTime.ParseExact(to, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture).AddDays(1).AddTicks(-1);

                int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
                var FinYear = Session["Fin_Year"].ToString();

                // 1. Fetch data from DB and sort by MRN Date (CreatedDate) Ascending
                var queryData = (from ab in DB.IEPLStockIN_Head
                                 join bb in DB.IEPLStockIN_Detail on ab.Id equals bb.HeadId
                                 join sup in DB.SupplierMasters on ab.Supplierid equals sup.id
                                 join item in DB.BOMItemMasters on bb.ItemCode equals item.Itemid
                                 join uom in DB.BOM_UOM on item.UOM equals uom.id
                                 where ab.CompanyID == companyid
                                 && ab.Fin_Year == FinYear
                                 && ab.Isdeleted == 0
                                 && bb.IsDeleted == 0
                                 && ab.CreatedDate >= fromDateDt
                                 && ab.CreatedDate <= toDateDt
                                 && (supplierId == 0 || ab.Supplierid == supplierId)
                                 orderby ab.Id ascending // Changed from CreatedBy to MRN Date Ascending
                                 select new
                                 {
                                     VendorName = sup.SupplierName,
                                     InvoiceNo = ab.BillNumber,
                                     InvoiceDate = ab.BillDate,
                                     MRNNo = ab.Vouchernumber,
                                     MRNDate = ab.CreatedDate, // Sorting basis
                                     GateEntryNo = ab.GateEntry,
                                     GateEntryDate = ab.GateEntryDate,
                                     ItemName = item.ItemName,
                                     ItemCode = item.ItemCode,
                                     LotNo = bb.LotNo ?? "NA",
                                     Qty = bb.BillQuantity,
                                     UOM = uom.UOM,
                                 }).ToList();

                // 2. Format dates and project for Frontend JSON
                var result = queryData.Select((x, index) => new {
                    SNo = index + 1, // Optional: add serial number back if needed for internal logic
                    x.VendorName,
                    x.InvoiceNo,
                    // Using System.DateTime to avoid ambiguous reference errors
                    InvoiceDate = x.InvoiceDate != null ? ((System.DateTime)x.InvoiceDate).ToString("dd-MMM-yyyy") : "---",
                    x.MRNNo,
                    GateEntryNo = x.GateEntryNo ?? "---",
                    GateEntryDate = x.GateEntryDate != null ? ((System.DateTime)x.GateEntryDate).ToString("dd-MMM-yyyy") : "---",
                    x.ItemName,
                    x.ItemCode,
                    x.UOM,
                    x.Qty
                }).ToList();

                return Json(new { success = true, data = result }, JsonRequestBehavior.AllowGet);

                
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpGet]
        public JsonResult GetInwardRejectionsFinYear()
        {
            try
            {
                if (Session["Company_ID"] == null) return Json(new { success = false }, JsonRequestBehavior.AllowGet);
                int companyId = Convert.ToInt32(Session["Company_ID"]);

                int currentYear = System.DateTime.Today.Year;
                System.DateTime fyStart = new System.DateTime(System.DateTime.Today.Month >= 4 ? currentYear : currentYear - 1, 4, 1);

                // Fetch rejections happening at the point of receipt (Inward QC)
                var inwardData = (from det in DB.IEPLStockIN_Detail
                                  join head in DB.IEPLStockIN_Head on det.HeadId equals head.Id
                                  join item in DB.BOMItemMasters on det.ItemCode equals item.Itemid
                                  where det.CompanyID == companyId && head.BillDate >= fyStart && det.IsDeleted == 0 && det.RejectedQuantity > 0
                                  group det by item.ItemName into g
                                  select new
                                  {
                                      ItemName = g.Key,
                                      Count = g.Sum(x => (double)x.RejectedQuantity)
                                  })
                                  .OrderByDescending(x => x.Count)
                                  .Take(5)
                                  .ToList();

                return Json(new { success = true, data = inwardData }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpGet]
        public JsonResult GetDashboardRatingMatrix()
        {
            try
            {
                if (Session["Company_ID"] == null)
                    return Json(new { success = false, message = "Session Expired" }, JsonRequestBehavior.AllowGet);

                int companyId = Convert.ToInt32(Session["Company_ID"]);
                System.DateTime today = System.DateTime.Today;

                // Logic for Financial Year (Starts April)
                int startYear = (today.Month >= 4) ? today.Year : today.Year - 1;
                System.DateTime fyStart = new System.DateTime(startYear, 4, 1);
                System.DateTime startOfMonth = new System.DateTime(today.Year, today.Month, 1);

                // Define Quarter Boundaries
                var q1Start = new System.DateTime(startYear, 4, 1);
                var q1End = new System.DateTime(startYear, 6, 30);

                var q2Start = new System.DateTime(startYear, 7, 1);
                var q2End = new System.DateTime(startYear, 9, 30);

                var q3Start = new System.DateTime(startYear, 10, 1);
                var q3End = new System.DateTime(startYear, 12, 31);

                var q4Start = new System.DateTime(startYear + 1, 1, 1);
                var q4End = new System.DateTime(startYear + 1, 3, 31);

                // Determine how many quarters to show based on current date
                int visibleQuarters = 1;
                if (today >= q4Start) visibleQuarters = 4;
                else if (today >= q3Start) visibleQuarters = 3;
                else if (today >= q2Start) visibleQuarters = 2;

                var rawData = (from ab in DB.DispatchDetails
                               join ew in DB.BOMEwapDetails on ab.Ewapid equals ew.id into ewgroup
                               from ew in ewgroup.DefaultIfEmpty()
                               join ts in DB.BOM_TestingUpdate on ew.BomVoucherHeadID equals ts.BOMHeadid into tsgroup
                               from ts in tsgroup.DefaultIfEmpty()
                               join rt in DB.Ratings_Production on ts.GensetRating equals rt.id into rtgroup
                               from rt in rtgroup.DefaultIfEmpty()
                               where ab.CompanyID == companyId && ab.BillDate >= fyStart
                               select new { BillDate = ab.BillDate, RatingRaw = rt.Rating }).ToList();

                var processedData = rawData.Select(x => new { x.BillDate, Range = MapRatingToRange(x.RatingRaw) }).ToList();
                var definedRanges = new[] { "7.5-35 KVA", "40-160 KVA", "200-250 KVA", "320-750 KVA", "1010-1250 KVA" };

                var matrix = definedRanges.Select(range => new {
                    RangeName = range,
                    MonthCount = processedData.Count(x => x.Range == range && x.BillDate >= startOfMonth),
                    Q1Count = processedData.Count(x => x.Range == range && x.BillDate >= q1Start && x.BillDate <= q1End),
                    Q2Count = processedData.Count(x => x.Range == range && x.BillDate >= q2Start && x.BillDate <= q2End),
                    Q3Count = processedData.Count(x => x.Range == range && x.BillDate >= q3Start && x.BillDate <= q3End),
                    Q4Count = processedData.Count(x => x.Range == range && x.BillDate >= q4Start && x.BillDate <= q4End),
                    FYCount = processedData.Count(x => x.Range == range)
                }).ToList();

                return Json(new
                {
                    success = true,
                    data = matrix,
                    visibleQuarters = visibleQuarters,
                    currentMonthName = today.ToString("MMMM")
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpGet]
        public JsonResult GetDashboardRatingMatrixold()
        {
            try
            {
                if (Session["Company_ID"] == null)
                    return Json(new { success = false, message = "Session Expired" }, JsonRequestBehavior.AllowGet);

                int companyId = Convert.ToInt32(Session["Company_ID"]);
                System.DateTime today = System.DateTime.Today;

                // 1. Calculate Date Thresholds
                System.DateTime startOfMonth = new System.DateTime(today.Year, today.Month, 1);

                // Financial Year Logic (Starts April 1st)
                int startYear = (today.Month >= 4) ? today.Year : today.Year - 1;
                System.DateTime fyStart = new System.DateTime(startYear, 4, 1);

                // Quarterly Logic (Q1: Apr-Jun, Q2: Jul-Sep, Q3: Oct-Dec, Q4: Jan-Mar)
                string currentQuarterLabel;
                System.DateTime startOfQuarter;

                if (today.Month >= 4 && today.Month <= 6)
                {
                    startOfQuarter = new System.DateTime(today.Year, 4, 1); currentQuarterLabel = "Q1";
                }
                else if (today.Month >= 7 && today.Month <= 9)
                {
                    startOfQuarter = new System.DateTime(today.Year, 7, 1); currentQuarterLabel = "Q2";
                }
                else if (today.Month >= 10 && today.Month <= 12)
                {
                    startOfQuarter = new System.DateTime(today.Year, 10, 1); currentQuarterLabel = "Q3";
                }
                else
                {
                    startOfQuarter = new System.DateTime(today.Year, 1, 1); currentQuarterLabel = "Q4";
                }

                // 2. Fetch Data
                var rawData = (from ab in DB.DispatchDetails
                               join ew in DB.BOMEwapDetails on ab.Ewapid equals ew.id into ewgroup
                               from ew in ewgroup.DefaultIfEmpty()
                               join ts in DB.BOM_TestingUpdate on ew.BomVoucherHeadID equals ts.BOMHeadid into tsgroup
                               from ts in tsgroup.DefaultIfEmpty()
                               join rt in DB.Ratings_Production on ts.GensetRating equals rt.id into rtgroup
                               from rt in rtgroup.DefaultIfEmpty()
                               where ab.CompanyID == companyId && ab.BillDate >= fyStart
                               select new
                               {
                                   BillDate = ab.BillDate,
                                   RatingRaw = rt.Rating
                               }).ToList();

                // 3. Pre-Process
                var processedData = rawData.Select(x => new {
                    x.BillDate,
                    Range = MapRatingToRange(x.RatingRaw)
                }).ToList();

                var definedRanges = new[] { "7.5-35 KVA", "40-160 KVA", "200-250 KVA", "320-750 KVA", "1010-1250 KVA" };

                // 4. Build Matrix
                var matrix = definedRanges.Select(range => new {
                    RangeName = range,
                    MonthCount = processedData.Count(x => x.Range == range && x.BillDate >= startOfMonth),
                    QuarterCount = processedData.Count(x => x.Range == range && x.BillDate >= startOfQuarter),
                    FYCount = processedData.Count(x => x.Range == range)
                }).ToList();

                return Json(new
                {
                    success = true,
                    data = matrix,
                    currentQuarter = currentQuarterLabel
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "System Error: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        private string MapRatingToRange(string ratingStr)
        {
            if (string.IsNullOrEmpty(ratingStr) || !decimal.TryParse(ratingStr, out decimal r)) return "Others";
            if (r >= 7.5m && r <= 35) return "7.5-35 KVA";
            if (r >= 40 && r <= 160) return "40-160 KVA";
            if (r >= 200 && r <= 250) return "200-250 KVA";
            if (r >= 320 && r <= 750) return "320-750 KVA";
            if (r >= 1010 && r <= 1250) return "1010-1250 KVA";
            return "Others";
        }
        [HttpGet]
        public JsonResult GetDashboardRatingMatrixold1()
        {
            try
            {
                if (Session["Company_ID"] == null)
                    return Json(new { success = false, message = "Session Expired" }, JsonRequestBehavior.AllowGet);

                int companyId = Convert.ToInt32(Session["Company_ID"]);
                System.DateTime today = System.DateTime.Today;

                // 1. Calculate Date Thresholds
                System.DateTime startOfMonth = new System.DateTime(today.Year, today.Month, 1);

                // Financial Year starts April 1st. If today is Jan-Mar, FY started last year.
                int startYear = (today.Month >= 4) ? today.Year : today.Year - 1;
                System.DateTime fyStart = new System.DateTime(startYear, 4, 1);

                // 2. Fetch relevant dispatch data for the current Financial Year
                // Joined with testing and ratings to get the KVA value
                var rawData = (from ab in DB.DispatchDetails
                               join ew in DB.BOMEwapDetails on ab.Ewapid equals ew.id into ewgroup
                               from ew in ewgroup.DefaultIfEmpty()
                               join ts in DB.BOM_TestingUpdate on ew.BomVoucherHeadID equals ts.BOMHeadid into tsgroup
                               from ts in tsgroup.DefaultIfEmpty()
                               join rt in DB.Ratings_Production on ts.GensetRating equals rt.id into rtgroup
                               from rt in rtgroup.DefaultIfEmpty()
                               where ab.CompanyID == companyId
                                  && ab.BillDate >= fyStart
                                  //&& ab.DispatchReturn == 0// Getting Net Sale By Redusing Returns.
                               select new
                               {
                                   BillDate = ab.BillDate,
                                   RatingRaw = rt.Rating
                               }).ToList();

                // 3. Define Categorization Logic (Internal Helper)
                string GetRange(string ratingStr)
                {
                    decimal r = 0;
                    if (!decimal.TryParse(ratingStr, out r)) return "Others";

                    if (r >= 7 && r <= 35) return "7.5-35 KVA";
                    if (r >= 40 && r <= 160) return "40-160 KVA";
                    if (r >= 200 && r <= 250) return "200-250 KVA";
                    if (r >= 320 && r <= 750) return "320-750 KVA";
                    if (r >= 1010 && r <= 1250) return "1010-1250 KVA";

                    return "Others";
                }

                // 4. Transform data into the final Matrix
                var definedRanges = new[] {
                    "7.5-35 KVA",
                    "40-160 KVA",
                    "200-250 KVA",
                    "320-750 KVA",
                    "1010-1250 KVA"
                };

                var matrix = definedRanges.Select(range => new {
                    RangeName = range,
                    // Count for the current month only
                    MonthCount = rawData.Count(x => GetRange(x.RatingRaw) == range && x.BillDate >= startOfMonth),
                    // Count for the entire financial year
                    FYCount = rawData.Count(x => GetRange(x.RatingRaw) == range),
                    // Total Month-on-Month Growth or Status can be added here if needed
                }).ToList();

                return Json(new { success = true, data = matrix }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                // Return clear error to the AJAX caller
                return Json(new { success = false, message = "System Error: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Fetches top 5 items rejected on the production line for the current FY.
        /// Helps identify process issues or hidden material defects.
        /// </summary>
        [HttpGet]
        public JsonResult GetTopRejectionsFinYear()
        {
            try
            {
                if (Session["Company_ID"] == null) return Json(new { success = false }, JsonRequestBehavior.AllowGet);
                int companyId = Convert.ToInt32(Session["Company_ID"]);

                int currentYear = System.DateTime.Today.Year;
                System.DateTime fyStart = new System.DateTime(System.DateTime.Today.Month >= 4 ? currentYear : currentYear - 1, 4, 1);

                var data = (from lr in DB.LineRejectionDetails
                            join item in DB.BOMItemMasters on lr.Itemid equals item.Itemid
                            join lrh in DB.LineRejectionHeads on lr.LRHeadid equals lrh.id
                            where lrh.CompanyID == companyId && lr.ApprovedDate >= fyStart && lr.ApprovedStatus == 1
                            group lr by item.ItemName into g
                            select new
                            {
                                ItemName = g.Key,
                                Count = g.Count()
                            })
                            .OrderByDescending(x => x.Count)
                            .Take(5)
                            .ToList();

                return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult LotStockReport()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "LotStockReport" && ab.Status == true
                               select ab).SingleOrDefault();
            if (checkaccess == null)
            {
                // 3. Store error message in TempData (survives one redirect)
                TempData["ErrorMessage"] = "Access Denied: You are not authorized to view This Page";

                // 4. Redirect to Home Controller, Index Action
                return RedirectToAction("Dashboard", "Dashboard");
            }
            ViewBag.Groups = DB.ItemGroups.OrderBy(x => x.GroupName).ToList();
            return View();
        }
        [HttpGet]
        public JsonResult GetStockLotData(string status, int? groupId)
        {
            try
            {
                // 1. Session Verification & Local Variable Extraction
                // Extract values FIRST to avoid "LINQ to Entities does not recognize method get_Item"
                if (Session["Company_ID"] == null || Session["U_Name"] == null)
                {
                    return Json(new { success = false, message = "Session Expired" }, JsonRequestBehavior.AllowGet);
                }

                int companyId = Convert.ToInt32(Session["Company_ID"]);
                string currentUserName = Session["U_Name"].ToString();

                // 2. Identify User Role (Outside the main data query)
                bool isSuperAdmin = false;
                var userRecord = DB.tbl_User_Master.FirstOrDefault(ab => ab.user_name == currentUserName);

                if (userRecord != null && userRecord.user_Role == "SUPERADMIN")
                {
                    isSuperAdmin = true;
                }

                // 3. Base Query with Joins (Using local variable 'companyId')
                var query = from lot in DB.Stock_lotDetail
                            join item in DB.BOMItemMasters on lot.ItemID equals item.Itemid
                            join grp in DB.ItemGroups on item.ItemGroup equals grp.id
                            where lot.CompanyID == companyId && lot.IsDeleted == false
                            select new { lot, item, grp };

                // 4. Application of Logical Filters
                if (status == "Available")
                {
                    query = query.Where(x => x.lot.IsAvailable == true && (x.lot.IsReserved == false || x.lot.IsReserved == null));
                }
                else if (status == "Consumed")
                {
                    query = query.Where(x => x.lot.IsAvailable == false);
                }
                else if (status == "Reserved")
                {
                    query = query.Where(x => x.lot.IsReserved == true);
                }

                if (groupId.HasValue && groupId > 0)
                {
                    query = query.Where(x => x.item.ItemGroup == groupId);
                }

                // 5. Execution and Materialization
                var rawResults = query.OrderByDescending(x => x.lot.RecieptDateTime).ToList();

                // 6. Aggregate Summary Calculations
                decimal totalOriginal = rawResults.Sum(x => (decimal?)x.lot.OriginalQuantity ?? 0);
                decimal totalAvailable = rawResults.Sum(x => (decimal?)x.lot.CurrentQuantity ?? 0);
                decimal totalConsumed = totalOriginal - totalAvailable;

                // 7. Data Projection with Traceability Logic
                var finalData = rawResults.Select(x =>
                {
                    string usageRef = "---";

                    if (x.lot.IsAvailable == false)
                    {
                        var usage = (from bvl in DB.BOMVoucherlines
                                     join bv in DB.BOMVouchers on bvl.BOMVoucherid equals bv.BOMVoucherID
                                     where bvl.LotID == x.lot.id && bvl.Isdeleted == 0
                                     select bv.VoucherNumber).FirstOrDefault();

                        usageRef = usage ?? "Manual Adjustment";
                    }

                    return new
                    {
                        x.lot.id,
                        x.item.ItemCode,
                        x.item.ItemName,
                        GroupName = x.grp.GroupName,
                        RecieptDate = x.lot.RecieptDateTime?.ToString("dd-MM-yyyy HH:mm") ?? "N/A",
                        x.lot.Lot_SerialNumber,
                        x.lot.OriginalQuantity,
                        x.lot.CurrentQuantity,
                        IsAvailable = x.lot.IsAvailable,
                        IsReserved = x.lot.IsReserved ?? false,
                        ReservedTo = x.lot.ReservedTo ?? "---",
                        UsageReference = usageRef,
                        x.lot.CreatedBy,
                        CreatedOn = x.lot.CreatedOn?.ToString("dd-MM-yyyy") ?? "N/A"
                    };
                }).ToList();

                // 8. Final Consolidated Response
                return Json(new
                {
                    success = true,
                    data = finalData,
                    summary = new
                    {
                        count = finalData.Count,
                        totalOriginal = totalOriginal,
                        totalAvailable = totalAvailable,
                        totalConsumed = totalConsumed,
                        isSuperAdmin = isSuperAdmin // Move it inside here to match your JS
                    }
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Internal Server Error: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult MiscSaleReport()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "MiscSaleReport" && ab.Status == true
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
        public JsonResult GetItemWiseReport(string fromDate, string toDate)
        {
            var start = DateTime.Parse(fromDate);
            var end = DateTime.Parse(toDate).AddDays(1);

            var data = (from d in DB.IEPL_MiscSale_Detail
                        join h in DB.IEPL_MiscSale_Head on d.HeadId equals h.Id
                        where h.InvoiceDate >= start && h.InvoiceDate < end
                        select new
                        {
                            h.InvoiceNo,
                            h.InvoiceDate,
                            h.CustomerName,
                            d.ItemCode,
                            d.ItemDescription,
                            d.Qty,
                            d.Rate,
                            d.LineTotal
                        }).OrderByDescending(x => x.InvoiceDate).ToList()
                        .Select(x => new {
                            x.InvoiceNo,
                            Date = x.InvoiceDate.ToString("dd-MMM-yyyy"),
                            x.CustomerName,
                            x.ItemCode,
                            x.ItemDescription,
                            x.Qty,
                            x.Rate,
                            x.LineTotal
                        }).ToList();

            return Json(new { success = true, data }, JsonRequestBehavior.AllowGet);
        }
        [HttpGet]
        public JsonResult GetBillWiseReport(string fromDate, string toDate)
        {
            var start = DateTime.Parse(fromDate);
            var end = DateTime.Parse(toDate).AddDays(1);

            var data = DB.IEPL_MiscSale_Head
                         .Where(h => h.InvoiceDate >= start && h.InvoiceDate < end)
                         .Select(h => new {
                             h.InvoiceNo,
                             Date = h.InvoiceDate,
                             h.CustomerName,
                             h.TaxType,
                             h.TotalBasic,
                             h.TotalTax, // GST
                             h.TCS_Percent,
                             h.TCS_Amount,
                             h.NetAmount,
                             h.CreatedBy
                         }).OrderByDescending(x => x.Date).ToList()
                         .Select(x => new {
                             x.InvoiceNo,
                             InvoiceDate = x.Date.ToString("dd-MMM-yyyy"),
                             x.CustomerName,
                             x.TaxType,
                             x.TotalBasic,
                             x.TotalTax,
                             TCS = x.TCS_Percent + "%",
                             x.TCS_Amount,
                             x.NetAmount,
                             x.CreatedBy
                         }).ToList();

            return Json(new { success = true, data }, JsonRequestBehavior.AllowGet);
        }
        [HttpGet]
        public JsonResult GetMiscSaleReport(string fromDate, string toDate)
        {
            try
            {
                int companyid = Convert.ToInt32(Session["Company_ID"]);
                DateTime start = DateTime.Parse(fromDate);
                DateTime end = DateTime.Parse(toDate).AddDays(1);

                // Fetch everything in one go to be fast
                var rawData = DB.IEPL_MiscSale_Head
                                .Where(h => h.CompanyID == companyid && h.InvoiceDate >= start && h.InvoiceDate < end)
                                .OrderByDescending(x => x.Id)
                                .ToList();

                var data = rawData.Select(h => new
                {
                    h.Id,
                    h.InvoiceNo,
                    InvoiceDate = h.InvoiceDate.ToString("dd-MMM-yyyy"),
                    h.CustomerName,
                    h.TotalBasic,
                    h.TotalTax,
                    h.TCS_Amount,
                    h.NetAmount,
                    h.TaxType,
                    // Fetch Details for each Head
                    Items = DB.IEPL_MiscSale_Detail.Where(d => d.HeadId == h.Id).Select(d => new {
                        d.ItemCode,
                        d.ItemDescription,
                        d.Qty,
                        d.Rate,
                        d.LineTotal
                    }).ToList()
                }).ToList();

                var summary = new
                {
                    TotalInvoices = data.Count,
                    Revenue = data.Sum(x => x.TotalBasic),
                    TaxCollected = data.Sum(x => x.TotalTax + x.TCS_Amount),
                    NetTotal = data.Sum(x => x.NetAmount)
                };

                return Json(new { success = true, data, summary }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet); }
        }
        [HttpPost]
        public JsonResult ReserveStockLot(int lotId, string remarks)
        {
            try
            {
                var lot = DB.Stock_lotDetail.Find(lotId);
                if (lot == null) return Json(new { success = false, message = "Lot not found" });

                lot.IsReserved = true;
                lot.ReservedTo = remarks; // Using ReservedTo to store the remarks/reference
                DB.SaveChanges();

                return Json(new { success = true, message = "Stock reserved successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        [HttpPost]
        public JsonResult UnreserveStockLot(int lotId)
        {
            try
            {
                var lot = DB.Stock_lotDetail.Find(lotId);
                if (lot == null) return Json(new { success = false, message = "Lot not found" });

                lot.IsReserved = false;
                lot.ReservedTo = null;
                DB.SaveChanges();

                return Json(new { success = true, message = "Stock unreserved successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        [HttpPost]
        public JsonResult DeleteStockLot(int lotId)
        {
            try
            {
                // Only allow SuperAdmin to delete (Security Check)
                if (Session["U_Role"]?.ToString() != "SUPERADMIN")
                {
                    return Json(new { success = false, message = "Unauthorized: Only SuperAdmin can delete stock." });
                }

                var lot = DB.Stock_lotDetail.Find(lotId);
                if (lot == null) return Json(new { success = false, message = "Lot not found" });

                lot.IsDeleted = true;
                DB.SaveChanges();

                return Json(new { success = true, message = "Stock lot deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        public ActionResult GetItemMasterData()
        {
            int companyId = Convert.ToInt32(Session["Company_ID"]);
            string finYear = Session["Fin_Year"].ToString();

            var data = (from ab in DB.BOMItemMasters
                        join grp in DB.ItemGroups on ab.ItemGroup equals grp.id into grpgroup
                        from grp in grpgroup.DefaultIfEmpty()
                        join loc in DB.StoreLocationMasters on ab.ItemLocation equals loc.id into locgroup
                        from loc in locgroup.DefaultIfEmpty()
                        join uom in DB.BOM_UOM on ab.UOM equals uom.id into uomgroup
                        from uom in uomgroup.DefaultIfEmpty()
                        where ab.CompanyID == companyId
                        orderby ab.ItemCategory ascending
                        select new
                        {
                            ItemCode = ab.ItemCode,
                            ItemName = ab.ItemName,
                            Description = ab.Desc,
                            ItemCategory = ab.ItemCategory == 1 ? "Final Item" :
                                ab.ItemCategory == 2 ? "Raw Material" :
                                ab.ItemCategory == 18 ? "Service Item" : 
                                ab.ItemCategory == 4 ? "FG Product" : "Other",
                            ItemGroup = grp.GroupName,
                            ItemLocation = loc.LocationName,
                            MOQ=ab.MinimumOrderQuantity,
                            UOM = uom.UOM,

                        }).ToList();
            return Json(new { success = true, data }, JsonRequestBehavior.AllowGet);

        }
        public ActionResult SaleReport()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "SaleReport" && ab.Status == true
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
        public ActionResult LRReport()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "LRReport" && ab.Status == true
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
        public ActionResult PendingPurchaseOrderReport()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "PendingPurchaseOrderReport" && ab.Status == true
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
        public JsonResult GetPendingPurchaseOrders()
        {
            try
            {
                // Retrieve CompanyID and FinYear from session
                // Add null checks for session variables in a real application
                int companyId = Convert.ToInt32(Session["Company_ID"]);
                string finYear = Session["Fin_Year"].ToString();

                // Query PurchaseOrderHead table for pending orders
                // Pending means either AdminApproval or SAdminApproval is 0
                var pendingOrders = (from po in DB.PurchaseOrderHeads
                                     join s in DB.SupplierMasters on po.SupplierID equals s.id // Join with SupplierMaster table
                                     where po.CompanyID == companyId &&
                                           po.Fin_Year == finYear &&
                                           (po.AdminApproval == 0 || po.SAdminApproval == 0) &&
                                           po.Isdeleted == 0 // Assuming Isdeleted = 0 for active orders
                                     select new
                                     {
                                         Id = po.id,
                                         PONumber = po.PONumber,
                                         PODate = po.PODate,
                                         SupplierName = s.SupplierName, // Now we get the actual Supplier Name
                                         BillGrossAmount = po.BillGrossAmount,
                                         AdminApproval = po.AdminApproval,
                                         SAdminApproval = po.SAdminApproval,
                                         EnteredBy = po.EnteredBy,
                                         EnteredOn = po.EnteredOn
                                     })
                    .OrderByDescending(po => po.EnteredOn) // Order by latest first
                    .ToList();

                // If you need supplier names, you'll need to join with your Supplier table:
                /*
                var pendingOrdersWithSupplier = (from po in DB.PurchaseOrderHeads
                                                 join s in DB.SupplierMasters on po.SupplierID equals s.SupplierID // Replace SupplierMasters and SupplierID as per your model
                                                 where po.CompanyID == companyId &&
                                                       po.Fin_Year == finYear &&
                                                       (po.AdminApproval == 0 || po.SAdminApproval == 0) &&
                                                       po.Isdeleted == false
                                                 select new
                                                 {
                                                     Id = po.id,
                                                     PONumber = po.PONumber,
                                                     PODate = po.PODate,
                                                     SupplierName = s.SupplierName, // Assuming SupplierMaster has a SupplierName property
                                                     BillGrossAmount = po.BillGrossAmount,
                                                     AdminApproval = po.AdminApproval,
                                                     SAdminApproval = po.SAdminApproval,
                                                     EnteredBy = po.EnteredBy,
                                                     EnteredOn = po.EnteredOn
                                                 })
                                                .OrderByDescending(po => po.EnteredOn)
                                                .ToList();
                */

                return Json(new { success = true, data = pendingOrders }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                // Log the exception (e.g., using a logging framework like NLog, Log4Net)
                System.Diagnostics.Debug.WriteLine("Error fetching pending purchase orders: " + ex.Message);
                return Json(new { success = false, message = "An error occurred while fetching data." }, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult ApprovedLR()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var finyear = Session["Fin_Year"].ToString();
            var data = (from ab in DB.LineRejectionDetails
                        join head in DB.LineRejectionHeads on ab.LRHeadid equals head.id
                        join im in DB.BOMItemMasters on ab.Itemid equals im.Itemid
                        join sup in DB.SupplierMasters on head.SupplierID equals sup.id
                        join rechead in DB.IEPLStockIN_Head on ab.BillHeadID equals rechead.Id
                        join lot in DB.IEPLStockIN_Detail on ab.BillDetailID equals lot.Id
                        where ab.IsDeleted==0 && ab.ApprovedStatus==1 && head.Isdeleted==0 && head.CompanyID==companyid && head.Fin_Year==finyear
                        
                       select new
                       {
                           Voucher=head.VoucherNumber,
                           Date=head.VoucherDate,
                           Supplier=sup.SupplierName,
                           BillNo=rechead.BillNumber,
                           Item=im.ItemName,
                           Remarks=ab.Remarks,
                           Quantity=ab.ApprovedQuantity,
                           CreatedBy=head.Createdby,
                           ApprovedBy=ab.ApprovedBy,
                           ApprovedDate=ab.ApprovedDate,
                           Lot=lot.LotNo ?? "NA",


                       }).ToList();

            return Json(new { success = true, data }, JsonRequestBehavior.AllowGet);
        }
        public JsonResult SaleReportData(string fromdt, string to)
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var finyear = Session["Fin_Year"].ToString();

            // Parse the input dates - explicitly use System.DateTime
            System.DateTime fromDateDt = System.DateTime.ParseExact(fromdt, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            System.DateTime toDateDt = System.DateTime.ParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture);

            // Make toDateDt inclusive of the entire day (up to the last tick of that day)
            toDateDt = toDateDt.Date.AddDays(1).AddTicks(-1);

            // CORRECTED: Changed alias from 'ewap' to 'ewapDetail' to avoid shadowing
            var sale = (from ab in DB.DispatchDetails
                        join ewapDetail in DB.BOMEwapDetails on ab.Ewapid equals ewapDetail.id into ewapGroup // Changed 'ewap' to 'ewapDetail'
                        from ewapDetail in ewapGroup.DefaultIfEmpty() // Changed 'ewap' to 'ewapDetail'
                        join ts in DB.BOM_TestingUpdate on ewapDetail.BomVoucherHeadID equals ts.BOMHeadid into tsgroup
                        from ts in tsgroup.DefaultIfEmpty()
                        join ps in DB.Bom_ProductionUpdate on ewapDetail.BomVoucherHeadID equals ps.BOMHeadid into psgroup
                        from ps in psgroup.DefaultIfEmpty()
                        join rt in DB.Ratings_Production on ts.GensetRating equals rt.id into rtgroup
                        from rt in rtgroup.DefaultIfEmpty()
                        join ph in DB.Phases on ps.AlternatorPhase equals ph.PhaseId into phgroup
                        from ph in phgroup.DefaultIfEmpty()
                        join en in DB.EngineModels on ewapDetail.Model equals en.EngineId into engroup // Changed 'ewap' to 'ewapDetail'
                        from en in engroup.DefaultIfEmpty()
                        join bt in DB.BatteryMasters on ewapDetail.BatteryRating equals bt.id into btgroup // Changed 'ewap' to 'ewapDetail'
                        from bt in btgroup.DefaultIfEmpty()
                        join cp in DB.ControlPanelTypes on ts.CPType equals cp.id into cpgrou
                        from cp in cpgrou.DefaultIfEmpty()
                        join im in DB.BOMItemMasters on ewapDetail.ItemID equals im.Itemid into imgroup
                        from im in imgroup.DefaultIfEmpty()
                        where ab.CompanyID == companyid
                        && ab.FinYear == finyear
                        // Filter by date range (inclusive)
                        && ab.BillDate >= fromDateDt
                        && ab.BillDate <= toDateDt
                        orderby ab.BillDate ascending
                        select new
                        {
                            Billno = ab.BillNumber ?? "NA",
                            Billdt = ab.BillDate,
                            CustomerName = ab.CustomerName ?? "NA",
                            Billing = ab.CustomerAddress ?? "NA", // Assuming this is billing address
                            Shipping = ab.ShippingAddress ?? "NA", // Assuming this is shipping address
                            CustomerGST = ab.CustomerGST ?? "NA",
                            Rating = (rt.Rating ?? "NA") + "KVA - " + (ph.PhaseDesc ?? "NA"),
                            Emodel = en.EngineModel1 ?? "NA", // Engine Model
                            ESerial = ewapDetail.EngineSerialNumber ?? "NA", // Engine Serial Number - Changed 'ewap' to 'ewapDetail'
                            AlternatorSerial = ewapDetail.AlternatorSerialNumber ?? "NA", // Changed 'ewap' to 'ewapDetail'
                            KRM = ewapDetail.KRMNo ?? "NA", // Changed 'ewap' to 'ewapDetail'
                            Quantity = 1, // Quantity is integer, so no null check needed if not nullable.
                            BRating = bt.BatteryName ?? "NA", // Battery Rating
                            BSerial = ts.BTSerial ?? "NA", // Battery Serial
                            PSerial = ts.CPSerialNumber ?? "NA", // Panel Serial
                            ControlPanel = cp.Type ?? "NA", // Control Panel Type
                            BasicPrice = ab.BasicPrice ?? 0m, // Ensure numeric default
                            Frieght = ab.FrieghtAMT ?? 0m,     // Ensure numeric default
                            TaxAmount = ab.GST ?? 0m,          // Ensure numeric default
                            CGST = ab.CGST ?? 0m,
                            SGST = ab.SGST ?? 0m,
                            Total = ab.BillingAMT ?? 0m,         // Ensure numeric default
                            Transport = ab.Transport ?? "NA",
                            VehicleNumber = ab.LorryNo ?? "NA",
                            BatteryQty = ts.BTQty ?? 0,
                            OA = ab.OrderNumber ?? "NA",
                            IM = im.ItemName,
                        }).ToList();
            
            // Fetch sales return data
            var returnData = (from dr in DB.DispatchReturns
                              join ab in DB.DispatchDetails on dr.Dispatchid equals ab.id
                              join ewapDetail in DB.BOMEwapDetails on ab.Ewapid equals ewapDetail.id into ewapGroup
                              from ewapDetail in ewapGroup.DefaultIfEmpty()
                              join ts in DB.BOM_TestingUpdate on ewapDetail.BomVoucherHeadID equals ts.BOMHeadid into tsgroup
                              from ts in tsgroup.DefaultIfEmpty()
                              join ps in DB.Bom_ProductionUpdate on ewapDetail.BomVoucherHeadID equals ps.BOMHeadid into psgroup
                              from ps in psgroup.DefaultIfEmpty()
                              join rt in DB.Ratings_Production on ts.GensetRating equals rt.id into rtgroup
                              from rt in rtgroup.DefaultIfEmpty()
                              join ph in DB.Phases on ps.AlternatorPhase equals ph.PhaseId into phgroup
                              from ph in phgroup.DefaultIfEmpty()
                              join en in DB.EngineModels on ewapDetail.Model equals en.EngineId into engroup
                              from en in engroup.DefaultIfEmpty()
                              join bt in DB.BatteryMasters on ewapDetail.BatteryRating equals bt.id into btgroup
                              from bt in btgroup.DefaultIfEmpty()
                              join cp in DB.ControlPanelTypes on ts.CPType equals cp.id into cpgrou
                              from cp in cpgrou.DefaultIfEmpty()
                              join nbm in DB.DispatchReturnBOMHeads on dr.id equals nbm.DispatchReturnID into nbmgroup
                              from nbm in  nbmgroup.DefaultIfEmpty()
                              join bom in DB.BOMVouchers on nbm.BOMID equals bom.BOMVoucherID into bomgroup
                              from bom in bomgroup.DefaultIfEmpty()
                              where dr.CompanyID == companyid
                              && dr.FinYear == finyear
                              // Filter by date range (inclusive)
                              && dr.ReturnDate >= fromDateDt
                              && dr.ReturnDate <= toDateDt
                              orderby dr.ReturnDate ascending
                              select new
                              {
                                  ReturnVoucher = dr.VoucherNumber ?? "NA",
                                  ReturnDate = dr.ReturnDate,
                                  InvoiceNo = ab.BillNumber ?? "NA",
                                  InvoiceDate=ab.BillDate,
                                  CustomerName = ab.CustomerName ?? "NA",
                                  Billing = ab.CustomerAddress ?? "NA",
                                  Shipping = ab.ShippingAddress ?? "NA",
                                  CustomerGST = ab.CustomerGST ?? "NA",
                                  Rating = (rt.Rating ?? "NA") + "KVA - " + (ph.PhaseDesc ?? "NA"),
                                  Emodel = en.EngineModel1 ?? "NA",
                                  ESerial = ewapDetail.EngineSerialNumber ?? "NA",
                                  AlternatorSerial = ewapDetail.AlternatorSerialNumber ?? "NA",
                                  KRM = ewapDetail.KRMNo ?? "NA",
                                  Quantity = 1,
                                  BRating = bt.BatteryName ?? "NA", // Battery Rating
                                  BSerial = ts.BTSerial ?? "NA", // Battery Serial
                                  PSerial = ts.CPSerialNumber ?? "NA", // Panel Serial
                                  ControlPanel = cp.Type ?? "NA",
                                  Problem = dr.Remarks ?? "NA",
                                  BasicPrice = ab.BasicPrice ?? 0m,
                                  Frieght = ab.FrieghtAMT ?? 0m,
                                  TaxAmount = ab.GST ?? 0m,
                                  CGST = ab.CGST ?? 0m,
                                  SGST = ab.SGST ?? 0m,
                                  Total = ab.BillingAMT ?? 0m,
                                  DispatchReturnBOM=nbm.VoucherNumber ?? "NA",
                                  NewBOM=bom.VoucherNumber ?? "NA",
                                  BatteryQty = ts.BTQty ?? 0,
                                  OA = ab.OrderNumber ?? "NA"
                              }).ToList();

            // Return JSON data with increased MaxJsonLength for large reports
            return Json(new { sale, returnData }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult BOMItemList() 
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "BOMItemList" && ab.Status == true
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
        public ActionResult CancelledBOMReport()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "CancelledBOMReport" && ab.Status == true
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
        [HttpPost]
        public JsonResult GetCancelledBOMData()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var finyear = Session["Fin_Year"].ToString();
            try
            {
               
                    var cancelledBOMs = DB.BOMVouchers
                        .Where(bv => bv.Isdeleted == 1 && bv.Finyear==finyear && bv.CompanyID==companyid)
                        .Select(bv => new
                        {
                            bv.BOMVoucherID,
                            bv.VoucherNumber,
                            bv.VoucherDate,
                            bv.Approvalstatus,
                            bv.CreatedBy,
                            bv.ApprovedBY,
                            bv.Ewap,
                            bv.EwapDate,
                            bv.EwapBy,
                            bv.FGProductID,
                            bv.ProductionCompletionStatus,
                            bv.ProductionCompletionon,
                            bv.DispatchStatus,
                            bv.DispatchDate,
                            bv.CancelledRemarks,
                            bv.CancelledBy,
                            bv.CancelledOn,
                            bv.CompanyID,
                            bv.Finyear
                        })
                        .ToList();

                    return Json(new { data = cancelledBOMs }, JsonRequestBehavior.AllowGet);
               
            }
            catch (Exception ex)
            {
                // Log the exception
                // You might want to return a specific error message to the client
                return Json(new { error = "An error occurred while fetching data." }, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult GetBomNumbers()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var finyear = Session["Fin_Year"].ToString();
            var list = (from ab in DB.BOMVouchers
                        where ab.Approvalstatus == 1 && ab.Isdeleted == 0 && ab.CompanyID == companyid && ab.Finyear==finyear
                        select new
                        {
                            value = ab.BOMVoucherID,
                            text = ab.VoucherNumber,
                        }).ToList();
            return Json(new { success = true, list }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetBOMItems(int bomidnumber)
        {
            var head = (from ab in DB.BOMVouchers
                       join item in DB.BOMItemMasters on ab.FGProductID equals item.Itemid into itemgrp
                       from item in itemgrp.DefaultIfEmpty()
                       where ab.BOMVoucherID == bomidnumber
                       select new
                       {
                           fgcode=item.ItemCode,
                           fgdesc=item.ItemName,

                       }).SingleOrDefault();
            var items = (from bb in DB.BOMVoucherlines
                         join item in DB.BOMItemMasters on bb.rawitemid equals item.Itemid into itemgrp
                         from item in itemgrp.DefaultIfEmpty()
                         join cat in DB.BOMCategories on bb.Categoryid equals cat.CategoryID into catgrp
                         from cat in catgrp.DefaultIfEmpty()
                         join subcat in DB.BOMSubcategories on bb.Subcategoryid equals subcat.id into subcatgrp
                         from subcat in subcatgrp.DefaultIfEmpty()
                         join lot in DB.Stock_lotDetail on bb.LotID equals lot.id into lotgrp
                         from lot in lotgrp.DefaultIfEmpty()
                         where bb.BOMVoucherid == bomidnumber
                         orderby cat.CategoryDesc
                         //select new
                        select new
                        {
                            RItemCode = item.ItemCode,
                            ItemName = item.ItemName,
                            Category = cat.CategoryDesc,
                            SubCategory = subcat.BOMSubCategory1,
                            Quantity = bb.ApprovedQuantity ?? 0,
                            UOM = bb.UOM,
                            ApprovedBy = bb.Approvedby,
                            ApprovedDate = bb.Approveddate,
                            // Add this line to pass the raw flag
                            IsDeleted = bb.Isdeleted,
                            Status = bb.Isdeleted == 1 ? "Cancelled" : "Active",
                            Lot = lot.Lot_SerialNumber ?? "NA",
                            LotDate = lot.RecieptDateTime,
                            BasicPrice = item.BasicPrice ?? 0,
                        }).ToList();
            var finalitem = DB.BOMVoucherlines.Where(x => x.BOMVoucherid == bomidnumber).FirstOrDefault();
            var finalitemdesc = DB.BOMItemMasters.Where(y => y.Itemid == finalitem.Finalitemid).Select(y => y.ItemName).SingleOrDefault();
            
            return Json(new { success = true, items,head,finalitemdesc }, JsonRequestBehavior.AllowGet);

        }

        [HttpPost]
        // public List<object> Reports()
        public JsonResult Reports(string year)
        {
            List<object> data = new List<object>();

            var result = DB.CMKL_Enquiry
                .Where(e => e.Year == year && e.OAStage == 8)
                .GroupBy(e => e.Billseries)
                .Select(g => new BillSeriesRecord
                {
                    Billseries = g.Key,
                    TotalRecords = g.Count()
                })
                .ToList();
            List<string> seriesNames = result.Select(d => d.Billseries).ToList();
            List<int> seriesTotals = result.Select(d => d.TotalRecords).ToList();
            data.Add(seriesNames);
            data.Add(seriesTotals);
            return Json(data);

        }

        public ActionResult ItemConsumptionReport()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "ItemConsumptionReport" && ab.Status == true
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

        public ActionResult CalculateConsumption()
        {
            // Calculate the start date based on the selected duration
            //Last 90 Days average
            System.DateTime startDate = System.DateTime.Now.AddMonths(-90);

            // Fetch data from the database
            var stockIssueData = DB.IEPLStockIssueDetails.Where(s => s.ApprovedDate >= startDate).ToList();
            var bomIssueData = DB.BOMVoucherlines.Where(b => b.Approveddate >= startDate && b.Isdeleted == 0).ToList();
            var itemMasterData = DB.BOMItemMasters.Where(item => item.ItemCategory == 2).ToList();
            var stocktable = DB.StockTables.Where(stock => stock.CompanyID == 17).ToList();

            // Group and calculate consumption
            var consumptionData = (
               from item in itemMasterData
               join availablestock in stocktable on item.Itemid equals availablestock.itemid
               join uom in DB.BOM_UOM on item.UOM equals uom.id
               select new ConsumptionViewModel
               {
                   ItemCode = item.ItemCode,
                   ItemName = item.ItemName,
                   UOM = uom.UOM,
                   AvailableStock = (decimal)availablestock.Stock,
                   IssuedQuantity = (decimal)stockIssueData.Where(s => s.Itemcodeid == item.Itemid).Sum(s => s.ApprovedQuantity),
                   BOMIssueQuantity = (decimal)bomIssueData.Where(b => b.rawitemid == item.Itemid).Sum(b => b.ApprovedQuantity)
               }
           ).ToList();

            foreach (var item in consumptionData)
            {
                // Calculate the total consumption from both sources
                decimal totalConsumption = item.IssuedQuantity + item.BOMIssueQuantity;
                item.Last3MonthsAvg = Math.Round(CalculateAverage(totalConsumption, 3), 3);

            }

            return Json(consumptionData, JsonRequestBehavior.AllowGet);
        }
        public ActionResult DispatchReport()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "DispatchReport" && ab.Status == true
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
        public JsonResult GetDispatchData()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var finyear = Session["Fin_Year"].ToString();
            var data = (from d in DB.DispatchDetails
                        join fg in DB.BOMItemMasters on d.FGItemCode equals fg.Itemid
                        join ewap in DB.BOMEwapDetails on d.Ewapid equals ewap.id
                        join bom in DB.BOMVouchers on ewap.BomVoucherHeadID equals bom.BOMVoucherID
                        join IM in DB.BOMItemMasters on bom.FGProductID equals IM.Itemid
                        where d.CompanyID==companyid && d.FinYear==finyear
                        orderby d.Createdon descending
                       select new 
                
                       {
                            BOMID=bom.BOMVoucherID,
                            VoucherNumber=d.VoucherNumber,
                            ItemName=IM.ItemName,
                            VoucherDate = d.VoucherDate, // Format date
                            OrderNumber= d.OrderNumber,
                            OrderDate = d.OrderDate,
                            BillNumber=d.BillNumber,
                            BillDate = d.BillDate,
                            ItemCode=fg.ItemCode,
                            CustomerName=d.CustomerName,
                            ESerialNumber=ewap.EngineSerialNumber,
                            BOMNumber=bom.VoucherNumber,
                            basicprice=d.BasicPrice,
                            gst=d.GST,
                            Frieght=d.FrieghtAMT,
                            Transporter=d.Transport,
                           // Createdon = d.Createdon,
                       })
                .ToList();

            return Json(data, JsonRequestBehavior.AllowGet);
        }
        public class ConsumptionViewModel
        {
            public string ItemCode { get; set; }
            public string UOM { get; set; }
            public string ItemName { get; set; }
            public decimal AvailableStock { get; set; }
            public decimal IssuedQuantity { get; set; }
            public decimal BOMIssueQuantity { get; set; }
            public decimal Last1MonthAvg { get; set; }
            public decimal Last3MonthsAvg { get; set; }
            public decimal Last6MonthsAvg { get; set; }
            public decimal Last12MonthsAvg { get; set; }
        }

        // Helper function to calculate average
        private decimal CalculateAverage(decimal quantity, int months)
        {
            return months > 0 ? quantity / months : 0;
        }
        public ActionResult PurcahseReport()
        {
            return View();
        }
               
        public ActionResult PurchaseReport()
        {
            return View();
        }
        public ActionResult PurcahseReportBill()
        {
            return View();
        }
        public ActionResult ApprovedBOMReportStores()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "ApprovedBOMReportStores" && ab.Status == true
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
        public JsonResult GetApprovalData()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var finyear = Session["Fin_Year"].ToString();
            // Use LINQ to query your database with a join
            var data = (from joined in DB.BOMVouchers
                       join Master in DB.BOMItemMasters on joined.FGProductID equals Master.Itemid
                       where joined.Approvalstatus==1 && joined.Isdeleted==0 && joined.CompanyID==companyid && joined.Finyear==finyear
                       orderby joined.BOMVoucherID descending
                       select new 
                         {
                             
                             VoucherNumber = joined.VoucherNumber,
                             VoucherDate = joined.VoucherDate,                             
                             CreatedBy = joined.CreatedBy,
                             Createdon = joined.VoucherDate,
                             ApprovedBY = joined.ApprovedBY,                            
                             FGProductCode = Master.ItemCode,
                             ItemName = Master.ItemName,
                             
                         })
                         .ToList();

            return Json(data, JsonRequestBehavior.AllowGet);
        }
        public JsonResult GetPurchaseReport()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var FinYear = Session["Fin_Year"].ToString();
            var heads = DB.IEPLStockIN_Head.Where(x=> x.Fin_Year==FinYear && x.CompanyID==companyid).ToList();
            var result = new List<object>();

            foreach (var head in heads)
            {
                var items = DB.IEPLStockIN_Detail.Where(d => d.HeadId == head.Id).ToList();
                var supplierName = DB.SupplierMasters.Where(s => s.id == head.Supplierid).Select(s => s.SupplierName).FirstOrDefault();
                var viewModel = new
                {
                    BillNumber = head.BillNumber,
                    BillDate = head.BillDate.ToString("yyyy-MM-dd"),
                    SupplierName = supplierName,//DB.SupplierMasters.Where(s => s.id== head.Supplierid).FirstOrDefault() , // assuming you have a Supplier table
                    TotalAmount = head.BillAmount,
                    GRNumber=head.GRNumber,
                    GRDate=head.GRDate,
                    FrieghtAmount=head.frieghtamount,
                    TotalTaxAmount=head.TaxAmount,


                    Items = items.Select(i => new
                    {
                        ItemName = DB.BOMItemMasters.Where(b => b.Itemid == i.ItemCode).Select(b => b.ItemCode).FirstOrDefault(),
                        BillQuantity=i.BillQuantity,
                        Quantity = i.Quantity,
                        BasicPrice = i.BasicPrice,
                        TaxAmount = i.TaxAmount,
                        TotalAmount = i.TotalAmount,
                        QualityApprove=i.QualityApprovedQty,
                        QualityRejected=i.RejectedQuantity,
                    }).ToList()
                };

                result.Add(viewModel);
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetPurchaseReportExcel()
        {
            var heads = DB.IEPLStockIN_Head.ToList();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Purchase Report");

                // Set header row
                worksheet.Cell(1, 1).Value = "Bill Number";
                worksheet.Cell(1, 2).Value = "Bill Date";
                worksheet.Cell(1, 3).Value = "GR Number";
                worksheet.Cell(1, 4).Value = "GR Date";
                worksheet.Cell(1, 5).Value = "Supplier Name";
                worksheet.Cell(1, 6).Value = "Frieght";
                worksheet.Cell(1, 7).Value = "Total Tax Amount";
                worksheet.Cell(1, 8).Value = "Total Amount";

                // Add sub-table headers
                worksheet.Cell(1, 9).Value = "Item Code";
                worksheet.Cell(1, 10).Value = "Bill Quantity";
                worksheet.Cell(1, 11).Value = "Quanity Recieved";
                worksheet.Cell(1, 12).Value = "Basic Price";
                worksheet.Cell(1, 13).Value = "Tax Amount";
                worksheet.Cell(1, 14).Value = "Total Amount";
                worksheet.Cell(1, 15).Value = "Quality Approved";
                worksheet.Cell(1, 16).Value = "Quality Rejected";

                worksheet.Range(1, 1, 1, 16).Style.Font.Bold = true;

                // Add data to the worksheet
                int row = 2;
                foreach (var head in heads)
                {
                    var items = DB.IEPLStockIN_Detail.Where(d => d.HeadId == head.Id).ToList();
                    var supplierName = DB.SupplierMasters.Where(s => s.id == head.Supplierid).Select(s => s.SupplierName).FirstOrDefault();

                    worksheet.Cell(row, 1).Value = head.BillNumber;
                    worksheet.Cell(row, 2).Value = head.BillDate.ToString("dd-MM-yyyy");
                    worksheet.Cell(row, 3).Value = head.GRNumber;
                    worksheet.Cell(row, 4).Value = head.GRDate;//.ToString("dd-MM-yyyy");
                    worksheet.Cell(row, 5).Value = supplierName;
                    worksheet.Cell(row, 6).Value = head.frieghtamount;
                    worksheet.Cell(row, 7).Value = head.TaxAmount;
                    worksheet.Cell(row, 8).Value = head.BillAmount;

                    row++;

                    foreach (var item in items)
                    {
                        worksheet.Cell(row, 5).Value = DB.BOMItemMasters.Where(b => b.Itemid == item.ItemCode).Select(b => b.ItemCode).FirstOrDefault();
                        worksheet.Cell(row, 6).Value = item.Quantity;
                        worksheet.Cell(row, 7).Value = item.BasicPrice;
                        worksheet.Cell(row, 8).Value = item.TaxAmount;
                        worksheet.Cell(row, 9).Value = item.TotalAmount;

                        row++;
                    }
                }

                string filePath = Server.MapPath("~/Files/Purchase Report.xlsx");
                workbook.SaveAs(filePath);

                // Return the file path and allow download
                return File(filePath, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Purchase Report.xlsx");
            }
        }
        public ActionResult QualityRejectionReturnReport()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "QualityRejectionReturnReport" && ab.Status == true
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
        [HttpPost]
        public JsonResult GetQualityRejectionData()
        {
            // Get data from the database

            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var FinYear = Session["Fin_Year"].ToString();
            var reportdata = (from ab in DB.IEPLQualityRejectionDetails
                              join item in DB.BOMItemMasters on ab.Itemid equals item.Itemid
                              join Purchaseline in DB.IEPLStockIN_Detail on ab.PurchaseDetailid equals Purchaseline.Id
                              join PurchaseHead in DB.IEPLStockIN_Head on Purchaseline.HeadId equals PurchaseHead.Id
                              join Supplier in DB.SupplierMasters on PurchaseHead.Supplierid equals Supplier.id into SupplierGroup
                              from Supplier in SupplierGroup.DefaultIfEmpty()
                              join Rejection in DB.RejectionTypes on ab.RejectiontoPlant equals Rejection.id into RejectionGroup
                              from Rejection in RejectionGroup.DefaultIfEmpty()
                                  //join Rejection in DB.RejectionTypes on ab.RejectiontoPlant 
                              join location in DB.RejectionAreas on ab.RejectionLocation equals location.id into locationGroup // LEFT JOIN
                              from location in locationGroup.DefaultIfEmpty() // Handle missing locations
                              join Head in DB.IEPLQualityRejectionHeads on ab.Headid equals Head.id
                              where ab.Companyid==companyid && ab.FinYear==FinYear
                              select new
                              {
                                  VoucherNo = Head.VoucherNo,
                                  VoucherDate = Head.VoucherDate,
                                  RejectionQuantity = ab.RejectionQuantity,
                                  ItemCode = item.ItemCode,
                                  ItemName = item.ItemName,
                                  Remarks=ab.Remarks,
                                  SBillNo=PurchaseHead.BillNumber,
                                  RejectionLocation = location != null ? location.RejectionArea1 : "Returned to Vendor", // Conditional for missing location
                                  RejectionTo = Supplier != null ? Supplier.SupplierName : "Plant Rejection",
                                  RejectionType = Rejection != null ? Rejection.RejectionType1 : "Vendor Rejection",
                              }).ToList();



            // Return the report data as JSON
            return Json(reportdata, JsonRequestBehavior.AllowGet);
        }
        public ActionResult PurchaseReportBillwise()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "PurchaseReportBillwise" && ab.Status == true
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
        public ActionResult IndentReportItemwise()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "IndentReportItemwise" && ab.Status == true
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
        public ActionResult IndentItemwiseData()
        {
            try
            {
                if (Session["Fin_Year"] == null || Session["Company_ID"] == null)
                {
                    return Json(new { success = false, message = "Session Expired" }, JsonRequestBehavior.AllowGet);
                }

                string FinYear = Session["Fin_Year"].ToString();
                int CompanyID = (int)Session["Company_ID"];

                // 1. Fetch PO lines including Promised Delivery Date
                var poLookup = (from ph in DB.PurchaseOrderHeads
                                join pi in DB.PurchaseOrderItems on ph.id equals pi.HeadID
                                join su in DB.SupplierMasters on ph.SupplierID equals su.id
                                where ph.CompanyID == CompanyID
                                //Lookup in all fin year   
                                // && ph.Fin_Year == FinYear
                                   && ph.Isdeleted == 0
                                   && pi.IsDeleted == 0
                                select new
                                {
                                    ph.IndentNumber,
                                    pi.ItemID,
                                    ph.PONumber,
                                    ph.PODate,
                                    ph.DeliveryDate,
                                    su.SupplierName
                                }).ToList();

                // 2. Fetch MRN data
                var mrnLookup = (from mh in DB.IEPLStockIN_Head
                                 join md in DB.IEPLStockIN_Detail on mh.Id equals md.HeadId
                                 where mh.CompanyID == CompanyID
                                    && mh.Fin_Year == FinYear
                                    && mh.Isdeleted == 0
                                    && md.IsDeleted == 0
                                 select new
                                 {
                                     md.ItemCode,
                                     mh.PoNumber,
                                     mh.Vouchernumber,
                                     mh.CreatedDate
                                 }).ToList();

                // 3. Fetch Main Indent Data
                var rawIndentData = (from bb in DB.BomIndentLines
                                     join ab in DB.BOMIndentHeads on bb.Headid equals ab.id
                                     join im in DB.BOMItemMasters on bb.Itemid equals im.Itemid
                                     join uom in DB.BOM_UOM on im.UOM equals uom.id
                                     join stk in DB.StockTables on im.Itemid equals stk.itemid
                                     join sp in DB.SupplierMasters on bb.PreviousSupplierid equals sp.id into spj
                                     from sp in spj.DefaultIfEmpty()
                                     where ab.CompanyID == CompanyID
                                        && ab.FinYear == FinYear
                                        && stk.CompanyID == CompanyID
                                     orderby ab.id descending
                                     select new
                                     {
                                         IndentNumber = ab.VoucherNumber,
                                         IndentDate = ab.CreatedOn,
                                         Itemid = im.Itemid,
                                         ItemCode = im.ItemCode,
                                         ItemName = im.ItemName,
                                         LastBasicRate = bb.LastBasicRate,
                                         TotalQuantity = bb.TotalQuantityRequired,
                                         ShortQuantity = bb.ShortQuantity,
                                         ActualRequirement = bb.ActualRequired,
                                         ManualIndent = bb.IsManualIndent,
                                         LastOrderDate = bb.LastOrderDate,
                                         LastOrderQuantity = bb.LastOrderQuantity,
                                         PreviousSupplier = sp == null ? "NA" : sp.SupplierName,
                                         StockIndent = bb.AvailableStock,
                                         AvailableStock = stk.Stock,
                                         UOM = uom.UOM,
                                         Remarks = bb.UserRemarks
                                     }).ToList();

                // 4. Final Join In-Memory with Delivery Variance Calculation
                var IndentData = (from ind in rawIndentData
                                  join po in poLookup on new { Num = ind.IndentNumber, Item = (int?)ind.Itemid }
                                                   equals new { Num = po.IndentNumber, Item = po.ItemID } into poGroup
                                  from po in poGroup.DefaultIfEmpty()

                                  join mrn in mrnLookup on new { P = po?.PONumber, I = (int?)ind.Itemid }
                                                    equals new { P = mrn.PoNumber, I = (int?)mrn.ItemCode } into mrnGroup
                                  from mrn in mrnGroup.DefaultIfEmpty()

                                  select new
                                  {
                                      ind.IndentNumber,
                                      ind.IndentDate,
                                      ind.ItemCode,
                                      ind.ItemName,
                                      ind.LastBasicRate,
                                      ind.ActualRequirement,
                                      ind.UOM,
                                      ind.Remarks,
                                      ind.StockIndent,
                                      PONumber = po != null ? po.PONumber : "Not Generated",
                                      PODate = po != null ? po.PODate : (System.DateTime?)null,
                                      SupplierName= po != null ? po.SupplierName : "Not Generated",
                                      PromisedDelivery = po != null ? po.DeliveryDate : (System.DateTime?)null,
                                      MRNNumber = mrn != null ? mrn.Vouchernumber : "Not Received",
                                      ActualReceipt = mrn != null ? mrn.CreatedDate : (System.DateTime?)null,

                                      // VARIANCE CALCULATION
                                      DelayStatus = (po != null && po.DeliveryDate.HasValue && mrn != null && mrn.CreatedDate.HasValue)
                                            ? (mrn.CreatedDate.Value.Date <= po.DeliveryDate.Value.Date
                                                ? "In time received"
                                                : "Received " + (mrn.CreatedDate.Value.Date - po.DeliveryDate.Value.Date).Days + " days later")
                                            : "---"
                                  }).ToList();

                // 5. KPI SUMMARY CALCULATIONS
                var summary = new
                {
                    TotalLines = IndentData.Count(),
                    TotalValue = IndentData.Sum(x => (x.LastBasicRate ?? 0) * (x.ActualRequirement ?? 0)),
                    POCreated = IndentData.Count(x => x.PONumber != "Not Generated"),
                    ItemsReceived = IndentData.Count(x => x.MRNNumber != "Not Received")
                };

                return Json(new { success = true, IndentData = IndentData, Summary = summary }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Backend Error: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult IndentItemwiseDataold()
        {
            var FinYear = Session["Fin_Year"].ToString();
            int CompanyID = (int)Session["Company_ID"];
            var IndentData = (from bb in DB.BomIndentLines
                              join ab in DB.BOMIndentHeads on bb.Headid equals ab.id
                              // join bb in DB.BomIndentLines on ab.id equals bb.Headid
                              join im in DB.BOMItemMasters on bb.Itemid equals im.Itemid
                              join uom in DB.BOM_UOM on im.UOM equals uom.id
                              join sp in DB.SupplierMasters on bb.PreviousSupplierid equals sp.id into spj
                              from sp in spj.DefaultIfEmpty()
                              join stk in DB.StockTables on im.Itemid equals stk.itemid
                              orderby ab.id descending
                              where ab.CompanyID == CompanyID && ab.FinYear == FinYear && stk.CompanyID == CompanyID
                              select new
                              {
                                  IndentNumber = ab.VoucherNumber,
                                  IndentDate = ab.CreatedOn,
                                  ItemCode = im.ItemCode,
                                  ItemName = im.ItemName,
                                  LastBasicRate = bb.LastBasicRate,
                                  TotalQuantity = bb.TotalQuantityRequired,
                                  ShortQuantity = bb.ShortQuantity,
                                  ActualRequirement = bb.ActualRequired,
                                  ManualIndent = bb.IsManualIndent,
                                  LastOrderDate = bb.LastOrderDate,
                                  LastOrderQuantity = bb.LastOrderQuantity,
                                  PreviousSupplier = sp == null ? "NA" : sp.SupplierName, // Conditional expression
                                  StockIndent = bb.AvailableStock,
                                  AvailableStock = stk.Stock,
                                  UOM = uom.UOM,
                                  Remarks=bb.UserRemarks,

                              }).ToList();
            return Json(new { IndentData }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult PurchaseOrderItemsReport()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "PurchaseOrderItemsReport" && ab.Status == true
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
        public ActionResult GetPurchaseReportItemWise()
        {
            var FinYear = Session["Fin_Year"].ToString();
            int CompanyID = (int)Session["Company_ID"];
            var head = DB.PurchaseOrderHeads.Where(x => x.Fin_Year == FinYear && x.CompanyID == CompanyID && x.Isdeleted == 0).ToList();

            var result = (from ab in DB.PurchaseOrderHeads
                          join im in DB.PurchaseOrderItems on ab.id equals im.HeadID into imgroup
                          from im in imgroup.DefaultIfEmpty()
                          join make in DB.Make_Master on im.MakeID equals make.id into makegroup
                          from make in makegroup.DefaultIfEmpty()
                          join item in DB.BOMItemMasters on im.ItemID equals item.Itemid into itemgroup
                          from item in itemgroup.DefaultIfEmpty()
                          join sup in DB.SupplierMasters on ab.SupplierID equals sup.id into supgroup
                          from sup in supgroup.DefaultIfEmpty()
                          where ab.Fin_Year == FinYear && ab.CompanyID == CompanyID && ab.Isdeleted == 0 && ab.AdminApproval == 1 && ab.SAdminApproval == 1
                          where im.IsDeleted == 0
                          select new
                          {
                              OrderNo=ab.PONumber,
                              IndentNo=ab.IndentNumber,
                              itemcode = item.ItemCode,
                              itemName = item.ItemName,
                              Make = make.Make,
                              Quantity = im.Quantity,
                              basicPrice = im.NetBasic,
                              SupplierName = sup.SupplierName,
                          }).ToList();
            return Json(new { success = true, result }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetPurchaseBills()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var FinYear = Session["Fin_Year"].ToString();
            var query = (from ab in DB.IEPLStockIN_Head
                         join supplier in DB.SupplierMasters on ab.Supplierid equals supplier.id
                         where ab.CompanyID == companyid && ab.Fin_Year == FinYear && ab.Isdeleted == 0
                         orderby ab.CreatedDate descending
                         select new
                         {
                             ab.BillNumber,
                             ab.BillDate,
                             ab.Vouchernumber,
                             ab.CreatedDate,
                             ab.PoNumber,
                             ab.PoDate,
                             ab.GRNumber,
                             supplier.SupplierName,
                             ab.frieghtamount,
                             ab.TaxAmount,
                             ab.OtherTaxAmount,
                             ab.BillAmount
                         }).ToList();



            return Json(query, JsonRequestBehavior.AllowGet);
        }

        public ActionResult DispatchReturnReport()
        {
            return View();
        }
        [HttpGet]
        public JsonResult GetDispatchReturnReport(string fromDate, string toDate)
        {
            try
            {
                int compId = Convert.ToInt32(Session["Company_ID"]);
                DateTime sDate = DateTime.Parse(fromDate).Date;
                DateTime eDate = DateTime.Parse(toDate).Date.AddDays(1).AddTicks(-1);

                var data = (from dr in DB.DispatchReturns
                                // Join to Rework BOM Head
                            join rHead in DB.DispatchReturnBOMHeads on dr.id equals rHead.DispatchReturnID into rGroup
                            from r in rGroup.DefaultIfEmpty()
                                // Join to Final BOM Voucher (Conversion)
                            join finalBom in DB.BOMVouchers on r.BOMID equals finalBom.BOMVoucherID into bGroup
                            from b in bGroup.DefaultIfEmpty()
                                // Join to Item Master for the FG Name
                            join item in DB.BOMItemMasters on dr.FGProductID equals item.Itemid into iGroup
                            from i in iGroup.DefaultIfEmpty()

                            where dr.CompanyID == compId && dr.ReturnDate >= sDate && dr.ReturnDate <= eDate
                            select new
                            {
                                ReturnID = dr.id,
                                ReturnNo = dr.VoucherNumber,
                                ReturnDt = dr.ReturnDate,
                                Customer = i != null ? i.ItemName : "Unknown Item",
                                Reason = dr.Remarks,

                                // Rework Status
                                ReworkVoucher = r != null ? r.VoucherNumber : "Not Created",
                                ReworkID = r != null ? (int?)r.id : null,
                                IsConverted = r != null && r.BOMID > 0,

                                // Final Conversion
                                FinalBOMNo = b != null ? b.VoucherNumber : "Pending",

                                // Fetching Rework Line Items
                                ReworkItems = (from l in DB.DispatchReturnBOMLines
                                               join raw in DB.BOMItemMasters on l.RawitemID equals raw.Itemid
                                               where l.HeadID == r.id && l.IsDeleted == 0
                                               select new
                                               {
                                                   Item = raw.ItemName,
                                                   Qty = l.Quantity
                                               }).ToList()
                            }).ToList();

                return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }
        public class QualityRejectionReport
        {
            public string VoucherNo { get; set; }
            public System.DateTime VoucherDate { get; set; }
            public int Itemid { get; set; }
            public decimal RejectionQuantity { get; set; }
            public int RejectionLocation { get; set; }
            public string RejectionTo { get; set; }

            
        }

    }
}