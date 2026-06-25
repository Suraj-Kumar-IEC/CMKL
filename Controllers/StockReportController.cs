using CMKL.Models;
using CMKL.Views.BOM;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;
using System.Globalization;
using Newtonsoft.Json;
using System.IO;
using System.Web.Services.Description;
using System.Text.RegularExpressions;




namespace CMKL.Controllers
{
    public class StockReportController : Controller
    {
        private readonly IECEntities _db;
        IECEntities DB = new IECEntities();

        public StockReportController()
        {
            _db = new IECEntities();
        }

        public ActionResult Index()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "Index" && ab.Status == true
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
        public ActionResult PendingQualityReport()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "PendingQualityReport" && ab.Status == true
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
        public ActionResult PendingStockIssueReport()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "PendingStockIssueReport" && ab.Status == true
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
        public ActionResult StockReturnReport()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "StockReturnReport" && ab.Status == true
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

        public ActionResult GetMonthlySaleRatingData()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var finyear = Session["Fin_Year"].ToString();

            DateTime currentDate = DateTime.Now;
            DateTime firstDayOfCurrentMonth = new DateTime(currentDate.Year, currentDate.Month, 1);
            DateTime lastDayOfCurrentMonth = firstDayOfCurrentMonth.AddMonths(1).AddDays(-1);

            var monthlySaleRatings = (from ab in DB.DispatchDetails
                                      join ew in DB.BOMEwapDetails on ab.Ewapid equals ew.id into ewgroup
                                      from ew in ewgroup.DefaultIfEmpty()
                                      join ts in DB.BOM_TestingUpdate on ew.BomVoucherHeadID equals ts.BOMHeadid into tsgroup
                                      from ts in tsgroup.DefaultIfEmpty()
                                      join rt in DB.Ratings_Production on ts.GensetRating equals rt.id into rtgroup
                                      from rt in rtgroup.DefaultIfEmpty()
                                          // Removed join to 'Phases' (ph) since we don't need it for grouping or description
                                      where ab.CompanyID == companyid
                                      && ab.FinYear == finyear
                                      && ab.BillDate >= firstDayOfCurrentMonth
                                      && ab.BillDate <= lastDayOfCurrentMonth
                                      && ab.DispatchReturn == 0
                                      //where ts.GensetRating !=null
                                      // Grouping by rt.Rating only
                                      group ab by rt.Rating into g
                                      select new
                                      {
                                          // Only use rt.Rating for the description
                                          RatingDescription = g.Key ?? "NA", // Use g.Key directly, and handle null if Rating can be null
                                          Count = g.Count() // Count of items for this rating
                                      }).ToList();
            int totalSumOfRatings = monthlySaleRatings.Sum(x => x.Count);
            var saledata = monthlySaleRatings.ToList() // Execute the query and bring data into memory
                                                       .OrderBy(item => GetSortKey(item.RatingDescription)) // Sort using the custom sort key
                                                                                                            //.OrderByDescending(x => x.Count) // Then order by count descending (as in your original query)
                                                       .ToList();
            // If you want to order them, you can do so after ToList()
            // monthlySaleRatings = monthlySaleRatings.OrderByDescending(x => x.Count).ToList();


            return Json(new { monthlySaleRatings, totalSumOfRatings, saledata }, JsonRequestBehavior.AllowGet);
        }
        
        public ActionResult DashboardDataForEwap()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var pendingEwapItemsQuery = (from ab in DB.BOMVouchers
                                         join item in DB.BOMItemMasters on ab.FGProductID equals item.Itemid
                                         where ab.Ewap == 0 && ab.Approvalstatus == 1 && ab.ProductionCompletionStatus == 1 && ab.Isdeleted == 0 && ab.CompanyID==companyid
                                         && !DB.BOMRequisitionHeads.Any(req => req.BOMVoucherID == ab.BOMVoucherID && req.IsApproved == 0)
                                         group ab by item.ItemName into g
                                         select new
                                         {
                                             ItemName = g.Key != null ? g.Key.Contains("Set") ? g.Key.Substring(0, g.Key.IndexOf("Set")).Trim() : g.Key : null,
                                             ItemNameBar = g.Key,
                                             Count = g.Count()
                                         });

            var pendingEwapItems = pendingEwapItemsQuery.ToList() // Execute the query and bring data into memory
                                                       .OrderBy(item => GetSortKey(item.ItemName)) // Sort using the custom sort key
                                                       //.OrderByDescending(x => x.Count) // Then order by count descending (as in your original query)
                                                       .ToList();

            // Calculate the total quantity using LINQ's Sum method
            var totalQuantity = pendingEwapItems.Sum(item => item.Count);

            // Return both the list of items and the total quantity in the JSON response
            return Json(new { pendingEwapItems, totalQuantity }, JsonRequestBehavior.AllowGet);
        }
        public JsonResult GetFinancialYearDispatchData1()
        {
            var currentDate = DateTime.Now;
            int currentYear = currentDate.Year;
            int startYear = currentDate.Month >= 4 ? currentYear : currentYear - 1;
            DateTime financialYearStart = new DateTime(startYear, 4, 1);
            DateTime financialYearEnd = financialYearStart.AddYears(1).AddDays(-1);

            var monthlyDispatchData = DB.DispatchDetails
                .Where(d => d.BillDate >= financialYearStart && d.BillDate <= financialYearEnd)//Removed Return clause to get Total Sale //&& d.DispatchReturn == 0)
                .GroupBy(d => new { d.BillDate.Value.Year, d.BillDate.Value.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TotalDispatch = g.Count(),
                    TotalBasicPrice = g.Sum(d => d.BasicPrice) // Calculate the sum of BasicPrice
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ToList()
                .Select(x => new
                {
                    MonthYear = new DateTime(x.Year, x.Month, 1).ToString("MMMM yyyy"),
                    TotalDispatch = x.TotalDispatch,
                    TotalBasicPrice = x.TotalBasicPrice // Include TotalBasicPrice
                })
                .ToList();

            return Json(new
            {
                monthlyData = monthlyDispatchData
            }, JsonRequestBehavior.AllowGet);
        }
        public JsonResult GetFinancialYearDispatchData()
        {
            if (Session["Company_ID"] == null)
                return Json(new { success = false, message = "Session Expired" }, JsonRequestBehavior.AllowGet);

            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var currentDate = DateTime.Now;
            int currentYear = currentDate.Year;
            int startYear = currentDate.Month >= 4 ? currentYear : currentYear - 1;

            DateTime financialYearStart = new DateTime(startYear, 4, 1);
            DateTime financialYearEnd = financialYearStart.AddYears(1).AddDays(-1);

            // 1. Fetch Gross Sales grouped by Month
            var grossData = (from d in DB.DispatchDetails
                             where d.BillDate >= financialYearStart && d.BillDate <= financialYearEnd && d.CompanyID==companyid
                             && d.CompanyID == companyid
                             group d by new { d.BillDate.Value.Year, d.BillDate.Value.Month } into g
                             select new
                             {
                                 Year = g.Key.Year,
                                 Month = g.Key.Month,
                                 TotalDispatch = g.Count(),
                                 TotalBasicPrice = g.Sum(d => (decimal?)d.BasicPrice ?? 0)
                             }).ToList();

            // 2. Fetch Returns occuring in the months of this FY
            var returnData = (from dr in DB.DispatchReturns
                              join d in DB.DispatchDetails on dr.Dispatchid equals d.id
                              where dr.ReturnDate >= financialYearStart && dr.ReturnDate <= financialYearEnd
                              && dr.CompanyID == companyid
                              group new { dr, d } by new { dr.ReturnDate.Value.Year, dr.ReturnDate.Value.Month } into g
                              select new
                              {
                                  Year = g.Key.Year,
                                  Month = g.Key.Month,
                                  TotalReturns = g.Count(),
                                  TotalReturnPrice = g.Sum(x => (decimal?)x.d.BasicPrice ?? 0)
                              }).ToList();

            // 3. Generate 12 months (April to March) and calculate NET
            var monthlyDispatchData = new List<object>();
            for (int i = 0; i < 12; i++)
            {
                DateTime monthDate = financialYearStart.AddMonths(i);
                var gross = grossData.FirstOrDefault(x => x.Year == monthDate.Year && x.Month == monthDate.Month);
                var ret = returnData.FirstOrDefault(x => x.Year == monthDate.Year && x.Month == monthDate.Month);

                int netCount = (gross?.TotalDispatch ?? 0); //- (ret?.TotalReturns ?? 0);
                decimal netAmount = (gross?.TotalBasicPrice ?? 0); //- (ret?.TotalReturnPrice ?? 0);

                monthlyDispatchData.Add(new
                {
                    MonthYear = monthDate.ToString("MMMM yyyy"),
                    TotalDispatch = netCount,
                    TotalBasicPrice = netAmount
                });
            }

            return Json(new
            {
                monthlyData = monthlyDispatchData
            }, JsonRequestBehavior.AllowGet);
        }
        public JsonResult GetCurrentMonthDispatchData()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var currentDate = DateTime.Now;
            var firstDayOfMonth = new DateTime(currentDate.Year, currentDate.Month, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            var dispatchData = DB.DispatchDetails
                .Where(d => d.BillDate >= firstDayOfMonth && d.BillDate <= lastDayOfMonth && d.CompanyID==companyid)
                .GroupBy(d => DbFunctions.TruncateTime(d.BillDate))
                .Select(g => new
                {
                    Date = g.Key,
                    DispatchCount = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToList()
                .Select(x => new
                {
                    Date = x.Date.Value.ToString("dd/MM/yyyy"),
                    DispatchCount = x.DispatchCount
                })
                .ToList();

            // Calculate the total count
            var totalDispatchCount = dispatchData.Sum(x => x.DispatchCount);

            // Add the total count to the JSON response
            return Json(new
            {
                data = dispatchData,
                totalCount = totalDispatchCount
            }, JsonRequestBehavior.AllowGet);
        }
        public JsonResult GetVoucherData()
        {
            // Fetch data from the database
            var voucherData = DB.Bill_Series.ToList(); // Assuming "Bill_Series" is your table name

            // Group by Type and count the number of vouchers
            var groupedData = voucherData
                .GroupBy(v => v.Type)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToList();

            return Json(groupedData, JsonRequestBehavior.AllowGet);
        }

        public PartialViewResult GetStockReportData()
        {
            var items = _db.BOMItemMasters
                    .OrderBy(i => i.ItemName)
                    .ToList();
            var lowStockItems = items.Where(i => i.Stock < i.Minstklvl).ToList();
            var highStockItems = items.Where(i => i.Stock > i.Maxstklvl).ToList();
            var nearMinStockItems = items.Where(i => i.Stock <= i.Minstklvl * 1.2m && i.Stock > i.Minstklvl).ToList();
            var nearMaxStockItems = items.Where(i => i.Stock >= i.Maxstklvl * 0.8m && i.Stock < i.Maxstklvl).ToList();
            var mainItemStock = items.Where(i => i.ItemCode.Contains("FDG")).ToList();

            ViewBag.LowStockItems = lowStockItems;
            ViewBag.HighStockItems = highStockItems;
            ViewBag.NearMinStockItems = nearMinStockItems;
            ViewBag.NearMaxStockItems = nearMaxStockItems;
            ViewBag.mainItemStock = mainItemStock;

            return PartialView("_StockReportData", items);
        }
        public JsonResult GetLowStockItems()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var finyear = Session["Fin_Year"].ToString();
            
            var lowStockItems = (from ab in DB.BOMItemMasters
                                 join stk in DB.StockTables on ab.Itemid equals stk.itemid // Join with StockTables
                                 where ab.CompanyID == companyid // Filter by CompanyID from BOMItemMasters
                                 && stk.CompanyID == companyid    // Filter by CompanyID from StockTables
                                 && stk.Stock < ab.Minstklvl     // Filter by Stock from StockTables vs Minstklvl from BOMItemMasters
                                 && ab.ItemCategory==2
                                 select new
                                 {
                                     ItemId = ab.Itemid,
                                     ItemCode = ab.ItemCode,
                                     ItemName = ab.ItemName,
                                     Desc = ab.Desc, // Assuming 'Desc' property exists in BOMItemMasters
                                     Minstklvl = ab.Minstklvl,
                                     Stock = stk.Stock           // Get Stock from the joined StockTables
                                 })
                     .ToList();

            return Json(lowStockItems, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetOverStockItems()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var finyear = Session["Fin_Year"].ToString();
            
            var overStockItems = (from ab in DB.BOMItemMasters
                                 join stk in DB.StockTables on ab.Itemid equals stk.itemid // Join with StockTables
                                 where ab.CompanyID == companyid // Filter by CompanyID from BOMItemMasters
                                 && stk.CompanyID == companyid    // Filter by CompanyID from StockTables
                                 && stk.Stock > ab.Maxstklvl     // Filter by Stock from StockTables vs Minstklvl from BOMItemMasters
                                 select new
                                 {
                                     ItemId = ab.Itemid,
                                     ItemCode = ab.ItemCode,
                                     ItemName = ab.ItemName,
                                     Desc = ab.Desc, // Assuming 'Desc' property exists in BOMItemMasters
                                     Maxstklvl = ab.Maxstklvl,
                                     Stock = stk.Stock           // Get Stock from the joined StockTables
                                 })
                    .ToList();

            return Json(overStockItems, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetRejectionData()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var finyear = Session["Fin_Year"].ToString();

            var totalRejections = DB.IEPLStockIN_Detail
                //.Join(DB.IEPLStockIN_Detail)
                .Where(y=>y.CompanyID==companyid)
                .Count(x => x.RejectedQuantity > 0);

            var totalRejectionReturns = DB.IEPLStockIN_Detail
                .Where(y => y.CompanyID == companyid)
                .Count(x => x.RejectedQuantity > 0 && x.RejectionReturn == 1);

            var result = new
            {
                TotalRejections = totalRejections,
                RejectionReturns = totalRejectionReturns
            };

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetChartData()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var targetDate = DateTime.Today.Date; // Get tomorrow's date, but set the time to midnight

            var chartData = DB.IEPLStockIssueDetails
                .Where(issue => DbFunctions.TruncateTime(issue.ApprovedDate) == targetDate) 
                // Use DbFunctions.TruncateTime
                //.Where(x=>x)
                .Join(DB.BOMItemMasters,
                    issue => issue.Itemcodeid,
                    item => item.Itemid,
                    (issue, item) => new { Issue = issue, Item = item })
                .GroupBy(joined => joined.Item.Itemid)
                .Select(group => new
                {
                    itemName = group.FirstOrDefault().Item.ItemName,
                    quantity = group.Sum(joined => joined.Issue.Quantity)
                })
                .ToList();

            return Json(chartData, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetPurchaseChartData()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var finyear = Session["Fin_Year"].ToString();
            // Calculate the date range for the last 7 days
            DateTime today = DateTime.Today;
            DateTime fromDateDt = today.AddDays(-7);
            DateTime toDateDt = today.AddDays(1);

            // Fetch purchase voucher data for the last 7 days
            var purchaseData = DB.IEPLStockIN_Head
                .Where(pv => pv.CreatedDate >= fromDateDt && pv.CreatedDate <= toDateDt)
                .Where(x=>x.CompanyID==companyid && x.Fin_Year==finyear)
                .GroupBy(pv => DbFunctions.TruncateTime(pv.CreatedDate))
                .Select(g => new // Fetch data without formatting
                {
                    Date = g.Key.Value,
                    VoucherCount = g.Count(),
                    TotalBillAmount = g.Sum(pv => pv.BillAmount)
                })
                .ToList() // Materialize the query to fetch data into memory
                .Select(x => new // Format the date in memory using LINQ to Objects
                {
                    Date = x.Date.ToString("o"), // Format the date in ISO 8601 format
                    x.VoucherCount,
                    x.TotalBillAmount
                })
                .ToList();

            return Json(purchaseData, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetQualityChartData()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var finyear = Session["Fin_Year"].ToString();
            // Calculate the date range for the last 7 days
            DateTime today = DateTime.Today;
            DateTime fromDateDt = today.AddDays(-7);
            DateTime toDateDt = today;

            // Fetch the quality data for the last 7 days
            var qualityData = DB.IEPLStockIN_Detail
            .Join(DB.IEPLStockIN_Head, d => d.HeadId, h => h.Id, (d, h) => new { d, h }) // Join with IEPLStockIN_Head
            .Where(q => DbFunctions.TruncateTime(q.h.CreatedDate) >= fromDateDt && DbFunctions.TruncateTime(q.h.CreatedDate) <= toDateDt)
            .Where(x=> x.h.CompanyID==companyid && x.h.Fin_Year==finyear)// Filter based on CreatedDate from the head table
            .GroupBy(q => q.d.QualityApproved)
            .Select(g => new
            {
                QualityStatus = g.Key == 1 ? "Completed" : "Pending",
                LineCount = g.Count()
            })
            .ToList();


            return Json(qualityData, JsonRequestBehavior.AllowGet);
        }
        
        [HttpGet]
        public JsonResult GetStockMovementChartData(string fromDate = null, string toDate = null)
        {
            // Date Range Handling (Current Month if no parameters provided)
            DateTime today = DateTime.Now;
            DateTime fromDateDt = fromDate != null ? DateTime.ParseExact(fromDate, "yyyy-MM-dd", CultureInfo.InvariantCulture) : new DateTime(today.Year, today.Month, 1);
            DateTime toDateDt = toDate != null ? DateTime.ParseExact(toDate, "yyyy-MM-dd", CultureInfo.InvariantCulture) : today;
            toDateDt = toDateDt.AddDays(1).AddTicks(-1);
            // Fetch stock-in data within the date range
            var stockInData = DB.IEPLStockIN_Detail
                .Join(DB.IEPLStockIN_Head, d => d.HeadId, h => h.Id, (d, h) => new { d.ItemCode, d.Quantity, h.CreatedDate })
                .Select(x => new
                {
                    ItemCode = x.ItemCode,
                    StockInQuantity = x.Quantity,
                    StockInDate = x.CreatedDate
                })
                .ToList();

            // Fetch stock-out data within the date range
            var stockIssueData = DB.IEPLStockIssueDetails
                .Where(i => i.ApprovedDate != null)
                .Select(g => new
                {
                    ItemCode = g.Itemcodeid,
                    IssueQuantity = g.Quantity,
                    IssueDate = g.ApprovedDate
                })
                .ToList();

            // Fetch opening stock data from OpeningStock table
            var openingStockData = DB.OpeningStocks
    .Select(os => new
    {
        ItemId = os.Itemid,
        OpeningStock = os.OpeningStock1,
        OpeningDate = os.CreatedDate
    })
    .ToList();


            //var allBomItemMasters = DB.BOMItemMasters.ToList();
            var allBomItemMasters = DB.BOMItemMasters
    .Where(m => m.ItemCategory == 2) // Add your filtering condition here
    .ToList();

            // Step 4: Combine data with Item Masters and Opening Stock (IN MEMORY)
            var combinedData = allBomItemMasters
                .GroupJoin(openingStockData, m => m.Itemid, os => os.ItemId, (m, os) => new { m, os })
                .SelectMany(x => x.os.DefaultIfEmpty(), (x, os) => new
                {
                    ItemId = x.m.Itemid,
                    ItemCode = x.m.ItemCode,
                    ItemName = x.m.ItemName,
                    InitialStock = x.m.Stock,
                    OpeningStock = os?.OpeningStock ?? 0m,
                    OpeningDate = os?.OpeningDate,
                    StockIns = stockInData.Where(s => s.ItemCode == x.m.Itemid).ToList(),
                    StockOuts = stockIssueData.Where(i => i.ItemCode == x.m.Itemid).ToList()
                });

            // Step 5: Calculate Closing Stock (IN MEMORY)
            var result = combinedData
                .Select(x =>
                {
                    // Handle null OpeningDate - If null, consider all transactions
                    var startDateForCalculations = x.OpeningDate ?? DateTime.MinValue;

                    return new
                    {
                        ItemId = x.ItemId,
                        ItemCode = x.ItemCode,
                        ItemName = x.ItemName,
                        StockAsOnToday = x.InitialStock,

                        // Calculate OpeningStock using the formula, but consider transactions only on or after OpeningDate
                        //  OpeningStock = x.OpeningStock
                        //  + x.StockIns.Where(s => s.StockInDate > startDateForCalculations && s.StockInDate < fromDateDt).Sum(s => s.StockInQuantity)
                        //  - x.StockOuts.Where(s => s.IssueDate >= startDateForCalculations && s.IssueDate < fromDateDt).Sum(s => s.IssueQuantity),

                        OpeningStock = x.OpeningStock
                                       + x.StockIns.Where(s => s.StockInDate < fromDateDt && s.StockInDate >= startDateForCalculations).Sum(s => s.StockInQuantity)
                                       - x.StockOuts.Where(s => s.IssueDate < fromDateDt && s.IssueDate >= startDateForCalculations).Sum(s => s.IssueQuantity),

                        StockIn = x.StockIns.Where(s => s.StockInDate >= fromDateDt && s.StockInDate <= toDateDt).Sum(s => s.StockInQuantity),
                        IssueQuantity = x.StockOuts.Where(s => s.IssueDate >= fromDateDt && s.IssueDate <= toDateDt).Sum(s => s.IssueQuantity),

                        // Calculate ClosingStock based on OpeningStock and transactions within the date range
                        // ClosingStock = x.OpeningStock
                        //     + x.StockIns.Where(s => s.StockInDate >= fromDateDt && s.StockInDate <= toDateDt).Sum(s => s.StockInQuantity)
                        //     - x.StockOuts.Where(s => s.IssueDate <= toDateDt && s.IssueDate >= startDateForCalculations).Sum(s => s.IssueQuantity)

                        ClosingStock =
                       x.OpeningStock // Base opening stock
                        + x.StockIns.Where(s => s.StockInDate >= startDateForCalculations && s.StockInDate <= toDateDt).Sum(s => s.StockInQuantity)  // Stock-ins within the provided range
                        - x.StockOuts.Where(s => s.IssueDate >= startDateForCalculations && s.IssueDate <= toDateDt).Sum(s => s.IssueQuantity) // Stock-outs within the provided range


                    };
                })
                .ToList();

            return Json(result, JsonRequestBehavior.AllowGet);
        }


        public JsonResult GetBOMChartDataAll()
        {
            using (var dbContext = new IECEntities())
            {
                var data = dbContext.BOMVouchers
                  .Where(b => b.VoucherDate >= DateTime.Now.AddDays(-7))
                  .GroupBy(b => b.VoucherDate)
                  .Select(g => new
                  {
                      Date = g.Key,
                      NotApproved = g.Count(b => b.Approvalstatus == 0),
                      Approved = g.Count(b => b.Approvalstatus == 1)
                  })
                  .ToList();

                return Json(data, JsonRequestBehavior.AllowGet);
            }
        }

        public ActionResult Stockinprocess()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "Stockinprocess" && ab.Status == true
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
        public ActionResult InProcessStockData()
        {

            var voucherIds = (from ab in DB.BOMVouchers
                              join ewap in DB.BOMEwapDetails on ab.BOMVoucherID equals ewap.BomVoucherHeadID into ewapGroup
                              from ewap in ewapGroup.DefaultIfEmpty()
                              where (ewap != null && ewap.DispatchStatus == 0) || ewap == null
                              where ab.Approvalstatus == 1 && ab.Isdeleted==0
                              select ab.BOMVoucherID).ToList();

            List<object> stockReportData = new List<object>(); // Initialize stockReportData

            if (voucherIds != null && voucherIds.Any())
            {
                var voucherLineData = (from vl in DB.BOMVoucherlines
                                       where voucherIds.Contains((int)vl.BOMVoucherid)
                                              && vl.ApprovedQuantity != null
                                              && vl.Isdeleted == 0
                                       select new
                                       {
                                           vl.rawitemid,
                                           vl.ApprovedQuantity
                                       }).ToList();

                stockReportData = (from vl in voucherLineData
                                   join item in DB.BOMItemMasters on vl.rawitemid equals item.Itemid
                                   group vl by new { vl.rawitemid, item.ItemName, item.ItemCode, item.BasicPrice } into g
                                   select new
                                   {
                                       ItemId = g.Key.rawitemid,
                                       ItemName = g.Key.ItemName,
                                       ItemCode = g.Key.ItemCode,
                                       BasicPrice=g.Key.BasicPrice,
                                       Quantity = g.Sum(x => x.ApprovedQuantity)
                                   }).ToList<object>(); // Cast to List<object>
            }

            return Json(new { success = true, data = stockReportData }, JsonRequestBehavior.AllowGet);

        }
        public class StockReportViewModel
        {
            public int ItemId { get; set; }
            public string ItemCode { get; set; }
            public string ItemName { get; set; }
            public int BOMVoucherID { get; set; }
            public string VoucherNumber { get; set; }
            public DateTime VoucherDate { get; set; }
            public int Finalitemid { get; set; }
            public decimal TotalQuantityRequired { get; set; }
            public decimal TotalApprovedQuantity { get; set; }
        }

        public JsonResult GetStockReport(string stockLevel)
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var report = (from ab in DB.BOMItemMasters
                          join uom in DB.BOM_UOM on ab.UOM equals uom.id into uomJoin
                          from uom in uomJoin.DefaultIfEmpty()
                          join ic in DB.ItemMasterClasses on ab.ItemClass equals ic.id into icJoin
                          from ic in icJoin.DefaultIfEmpty()
                          join ig in DB.ItemGroups on ab.ItemGroup equals ig.id into igjoin
                          from ig in igjoin.DefaultIfEmpty()
                          join bomvl in DB.BOMVoucherlines on ab.Itemid equals bomvl.rawitemid into bomvlJoin
                          from bomvl in bomvlJoin.DefaultIfEmpty()
                          join loc in DB.StoreLocationMasters on ab.ItemLocation equals loc.id into locjoin
                          from loc in locjoin.DefaultIfEmpty()
                         // join stockInDetail in DB.IEPLStockIN_Detail on ab.Itemid equals stockInDetail.ItemCode into stockInDetailJoin
                          //from stockInDetail in stockInDetailJoin.DefaultIfEmpty()
                          join os in DB.OpeningStocks on ab.Itemid equals os.Itemid into osJoin
                          from os in osJoin.DefaultIfEmpty()
                          join stk in DB.StockTables on ab.Itemid equals stk.itemid into stkgroup
                          from stk in stkgroup.DefaultIfEmpty()
                          where (ab.ItemCategory == 2 || ab.ItemCategory == 4 && ab.CompanyID == companyid) && stk.CompanyID == companyid //&& stk.Stock!=0 //&& stockInDetail.IsDeleted==0 //&& stk.Stock != 0 // Filter for stock != 0
                          group new { ab, uom, bomvl,  os, ic, ig } by new { ab.Itemid, ab.ItemCode, ab.ItemName, ab.ItemCategory, uom.UOM, ab.Minstklvl, ab.Maxstklvl, stk.Stock, stk.RejectedStock, os.OpeningStock1, ab.BasicPrice, ic.Class, ig.GroupName, loc.LocationName } into g // Include BasicPrice in grouping
                          //let pendingQualityQty = g.Sum(x => x.stockInDetail.Quantity - x.stockInDetail.QualityApprovedQty - x.stockInDetail.RejectedQuantity)
                          select new
                          {
                              g.Key.Itemid,
                              g.Key.ItemCode,
                              g.Key.ItemName,
                              g.Key.ItemCategory,
                              UOMText = g.Key.UOM == null ? "UOM Allocation Pending" : g.Key.UOM,
                              itemclass=g.Key.Class== null? "Not Defiend" : g.Key.Class,
                              itemgroup=g.Key.GroupName==null? "Not Defined" : g.Key.GroupName,
                              location=g.Key.LocationName == null ? "Not Defined" : g.Key.LocationName,
                              g.Key.Minstklvl,
                              g.Key.Maxstklvl,
                              g.Key.Stock,
                              g.Key.RejectedStock,
                              OpeningStock = (decimal?)g.Key.OpeningStock1,
                              TotalQuantityRequired = g.Sum(x => x.bomvl.QuantityRequired ?? 0),
                              TotalApprovedQuantity = g.Sum(x => x.bomvl.ApprovedQuantity ?? 0),
                              // PendingQualityQty = pendingQualityQty,
                              BasicPrice = g.Key.BasicPrice ?? 0m  // Include BasicPrice in the result
                          }).AsQueryable();

            if (stockLevel == "Low")
            {
                report = report.Where(x => x.Stock < x.Minstklvl);
            }
            else if (stockLevel == "High")
            {
                report = report.Where(x => x.Stock > x.Maxstklvl);
            }
            else if (stockLevel == "Engine|Alternator|Battery")
            {
                report = report.Where(x => x.ItemCode.Contains("FDG"));
            }
            else if (stockLevel == "FG Items Stock")
            {
                report = report.Where(x => x.ItemCategory==4);
            }
            else if (stockLevel == "-Ve Stock")
            {
                report = report.Where(x => x.Stock < 0);

            }

            // If stockLevel is "All", return all records
            else if (stockLevel == "All")
            {
                report = report;
            }

            return Json(report.OrderBy(s => s.ItemName).ToList(), JsonRequestBehavior.AllowGet);
        }

        public ActionResult StockMovementReport()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "StockMovementReport" && ab.Status == true
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
        public ActionResult StockMovementReportold()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "StockMovementReportold" && ab.Status == true
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
        public JsonResult GetStockMovementReport(string fromDate, string toDate , string Groupid)
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var finyear = Session["Fin_Year"].ToString();
            // Parse the input dates
            DateTime fromDateDt = DateTime.ParseExact(fromDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            DateTime toDateDt = DateTime.ParseExact(toDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

            // Make toDateDt inclusive of the entire day
            toDateDt = toDateDt.AddDays(1).AddTicks(-1);

            // Fetch WIP data (using the logic from InProcessStockData)
          
            var voucherIds = (from ab in DB.BOMVouchers
                              join ewap in DB.BOMEwapDetails on ab.BOMVoucherID equals ewap.BomVoucherHeadID into ewapGroup
                              from ewap in ewapGroup.DefaultIfEmpty()
                              where (ewap == null || (ewap != null && (ewap.Dispatchon == null || ewap.Dispatchon >= toDateDt))) // Condition for WIP
                              where ab.Approvalstatus == 1 && ab.Isdeleted==0                            
                              select ab.BOMVoucherID).ToList();

            List<object> wipData = new List<object>();

            if (voucherIds != null && voucherIds.Any())
            {
                var voucherLineData = (from vl in DB.BOMVoucherlines
                                       where voucherIds.Contains((int)vl.BOMVoucherid)
                                           && vl.ApprovedQuantity != null
                                           && vl.Isdeleted == 0 && vl.Approveddate<=toDateDt
                                       select new
                                       {
                                           vl.rawitemid,
                                           vl.ApprovedQuantity
                                       }).ToList();

                wipData = (from vl in voucherLineData
                           join item in DB.BOMItemMasters on vl.rawitemid equals item.Itemid
                           group vl by new { vl.rawitemid, item.ItemName, item.ItemCode } into g
                           select new
                           {
                               ItemId = g.Key.rawitemid,
                               ItemName = g.Key.ItemName,
                               ItemCode = g.Key.ItemCode,
                               Quantity = g.Sum(x => x.ApprovedQuantity)
                           }).ToList<object>();
            }



            // Fetch stock-in data within the date range
            var stockInData = DB.IEPLStockIN_Detail
            .Where(d => d.IsDeleted == 0) // Add where condition for IsDeleted in Detail table
            .Join(DB.IEPLStockIN_Head, d => d.HeadId, h => h.Id, (d, h) => new { d, h }) // Join the tables and keep both entities
            .Where(combined => combined.h.Isdeleted== 0) // Add where condition for IsDeleted in Head table
            .Select(combined => new
            {
                ItemCode = combined.d.ItemCode,
                StockInQuantity = combined.d.Quantity,
                // StockInDate = combined.h.CreatedDate,
                StockInDate = combined.d.QApprovedDate, // Assuming QApprovedDate is in the Detail table
                QualityInQuantity = combined.d.QualityApprovedQty ?? 0m, // Handle null values
                QualityRejectQuantity = combined.d.RejectedQuantity ?? 0m,

            })
            .ToList();

            // Fetch stock-out data within the date range
            var stockIssueData = DB.IEPLStockIssueDetails
                .Where(i => i.ApprovedDate != null && i.IsDeleted==null )
                .Select(g => new
                {
                    ItemCode = g.Itemcodeid,
                    IssueQuantity = g.Quantity,
                    IssueDate = g.ApprovedDate
                })
                .ToList();
            var stockReturnData = DB.IEPLStockReturnDetails
                .Where(i => i.ApprovedDate != null)
                .Select(g => new
                {
                    ItemCode = g.Itemcodeid,
                    IssueQuantity = g.Quantity,
                    IssueDate = g.ApprovedDate
                })
                .ToList();
            var directsale = DB.IEPL_MiscSale_Detail
                .Where(i => i.IsDeleted == false)
                .Select(g => new
                {
                    ItemCode=g.ItemID,
                    Quantity=g.Qty,
                    IssueDate=g.DateCreated,

                }).ToList();
            var LineRejectionData = DB.LineRejectionDetails // Changed variable name to avoid shadowing
            .Where(i => i.ApprovedDate != null && i.ApprovedStatus == 1)
            .Select(g => new
            {
                ItemCode = g.Itemid,
                RejectionQuantity = g.ApprovedQuantity, // Changed property name to RejectionQuantity
                RejectionDate = g.ApprovedDate // Changed property name to RejectionDate
            }).ToList();

            var bomIssueData = (from bb in DB.BOMVouchers
                                join bl in DB.BOMVoucherlines on bb.BOMVoucherID equals bl.BOMVoucherid into blgroup
                                from bl in blgroup.DefaultIfEmpty()
                                where bb.Isdeleted==0
                                where bl.Isdeleted == 0 
                                where bl.Approveddate !=null//&& bl.Approveddate != null //&& bb.Finyear == finyear //donot pick records of last fin year
                                select new
                                {
                                    ItemCode = bl.rawitemid,
                                    IssueQuantity = bl.ApprovedQuantity,
                                    IssueDate = bl.Approveddate,
                                    Isdeleted=bl.Isdeleted,
                                }).ToList();

            
            var openingStockData = DB.OpeningStocks.Where(x=>x.Companyid==companyid)
                .Select(os => new
                {
                    ItemId = os.Itemid,
                    OpeningStock = os.OpeningStock1,
                    OpeningDate = os.CreatedDate
                })
                .ToList();
            //Added this line to get stock from New Tables that is Stocktable
            var stockTableData = DB.StockTables
            .Where(st => st.CompanyID == companyid) // Add your filter condition here
            .Select(st => new { st.itemid, st.Stock, st.BasicPrice })
            .ToList();


           // var allBomItemMasters = DB.BOMItemMasters
              //  .Where(m => m.ItemCategory == 2 && m.CompanyID == companyid) // Add your filtering condition here
              //  .ToList();
            // --- Corrected BOMItemMasters Filtering and Scoping ---

            IQueryable<BOMItemMaster> bomItemMastersBaseQuery = DB.BOMItemMasters
                .Where(m => m.ItemCategory == 2 && m.CompanyID == companyid);

            IQueryable<BOMItemMaster> allBomItemMastersQuery; // Declare as IQueryable outside if/else

            if (Convert.ToInt32(Groupid) == 0)
            {
                allBomItemMastersQuery = bomItemMastersBaseQuery; // No additional filter for Groupid == 0
            }
            else
            {
                int groupIdInt = Convert.ToInt32(Groupid);
                allBomItemMastersQuery = bomItemMastersBaseQuery.Where(m => m.ItemGroup == groupIdInt && m.ItemCategory==2 && m.CompanyID==companyid);
            }

            var allBomItemMasters = allBomItemMastersQuery.ToList();


            var indentData = (from head in DB.BOMIndentHeads
                              join line in DB.BomIndentLines on head.id equals line.Headid
                              where head.CompanyID == companyid
                                    && head.ApprovalStatus == 1 // Approved by Admin
                                    && head.SAdminApproval == 1 // Approved by SAdmin
                                    && line.IsApproved==1
                                    && head.CompanyID==companyid// Assuming Isdeleted is a boolean in BOMIndentHead
                              select new
                              {
                                  ItemId = line.Itemid,
                                  ActualRequired= line.TotalQuantityRequired,
                                  IndentCreatedOn = head.SAdminApprovalDate,
                              }).ToList();
            var purchaseOrderData = (from head in DB.PurchaseOrderHeads
                                     join detail in DB.PurchaseOrderItems on head.id equals detail.HeadID // Assuming PurchaseOrderDetails table
                                     where head.CompanyID == companyid
                                           && head.AdminApproval == 1 // Approved by Admin
                                           && head.SAdminApproval == 1 // Approved by SAdmin
                                           && head.Isdeleted == 0 // Assuming Isdeleted is a boolean in PurchaseOrderHead
                                           && detail.IsDeleted==0
                                     select new
                                     {
                                         ItemId = detail.ItemID, // Assuming ItemId in PurchaseOrderDetails
                                         POQuantity = detail.Quantity, // Assuming Quantity in PurchaseOrderDetails
                                         PODate = head.PODate,
                                     }).ToList();
            // Step 4: Combine data with Item Masters and Opening Stock (IN MEMORY)
            var combinedData = allBomItemMasters
                .GroupJoin(openingStockData, m => m.Itemid, os => os.ItemId, (m, os) => new { m, os })
                .SelectMany(x => x.os.DefaultIfEmpty(), (x, os) => new
                {
                    ItemId = x.m.Itemid,
                    ItemCode = x.m.ItemCode,
                    ItemName = x.m.ItemName,
                    BasicPrice= stockTableData.FirstOrDefault(st => st.itemid == x.m.Itemid)?.BasicPrice ?? 0m,
                    InitialStock = stockTableData.FirstOrDefault(st => st.itemid == x.m.Itemid)?.Stock ?? 0m, 
                    //Below Line Disabled as it picks data from BOM Item Master Table
                    //InitialStock = x.m.Stock,//Yet Current Stock is coming fromBOM Item Master need to update it from stock table
                    OpeningStock = os?.OpeningStock ?? 0m,
                    OpeningDate = os?.OpeningDate,
                    StockIns = stockInData.Where(s => s.ItemCode == x.m.Itemid).ToList(),
                    StockOuts = stockIssueData.Where(i => i.ItemCode == x.m.Itemid).ToList(),
                    StockReturn=stockReturnData.Where(i => i.ItemCode == x.m.Itemid).ToList(),
                    LineRejection = LineRejectionData.Where(i => i.ItemCode == x.m.Itemid).ToList(),
                    //New Addition for BOm Quantity
                    BOMIssues = bomIssueData.Where(b => b.ItemCode == x.m.Itemid).ToList(), // Add BOMIssues
                    IndentedItems = indentData.Where(i => i.ItemId == x.m.Itemid).ToList(),
                    PoItems = purchaseOrderData.Where(po => po.ItemId == x.m.Itemid).ToList(), // Add PO Items
                    saleitems=directsale.Where(sl=> sl.ItemCode==x.m.Itemid).ToList(),
                    // Add Indented Items
                    // BOMReturn=bomReturnData.Where(b =>b.ItemCode==x.m.Itemid ).ToList(),
                });

            

            // Step 5: Calculate Closing Stock (IN MEMORY)
            var result = combinedData
                .Select(x =>
                {
                    var wipQuantity = ((IEnumerable<dynamic>)wipData)
                    .Where(w => w.ItemId == x.ItemId)
                    .Sum(w => (decimal?)w.Quantity) ?? 0m;

                    // Get WIP details for the current item
                    var wipItems = ((IEnumerable<dynamic>)wipData)
                        .Where(w => w.ItemId == x.ItemId)
                        .Select(w => new
                        {
                            ItemId = w.ItemId,
                            ItemName = w.ItemName,
                            WIPQuantity = w.Quantity
                        })
                        .ToList();
                    // Handle null OpeningDate - If null, consider all transactions
                    var startDateForCalculations = x.OpeningDate ?? DateTime.MinValue;
                    decimal OpeningStock1c = (decimal)(x.OpeningStock // as on os date
                     + x.StockIns.Where(s => s.StockInDate < fromDateDt && s.StockInDate >= startDateForCalculations).Sum(s => s.QualityInQuantity)
                     - x.StockOuts.Where(s => s.IssueDate < fromDateDt && s.IssueDate >= startDateForCalculations).Sum(s => s.IssueQuantity)
                     + x.StockReturn.Where(s => s.IssueDate < fromDateDt && s.IssueDate >= startDateForCalculations).Sum(s => s.IssueQuantity)
                    // + x.BOMReturn.Where(b => b.IssueDate < fromDateDt && b.IssueDate >= startDateForCalculations).Sum(b => b.IssueQuantity)
                     - x.BOMIssues.Where(b => b.IssueDate < fromDateDt && b.IssueDate >= startDateForCalculations).Sum(b => b.IssueQuantity)
                     - x.LineRejection.Where(lr => lr.RejectionDate < fromDateDt && lr.RejectionDate >= startDateForCalculations).Sum(lr => lr.RejectionQuantity))
                     -x.saleitems.Where(sl=> sl.IssueDate<fromDateDt && sl.IssueDate >= startDateForCalculations).Sum(sl=>sl.Quantity);

                    decimal StockInc = x.StockIns.Where(s => s.StockInDate >= fromDateDt && s.StockInDate <= toDateDt).Sum(s => s.StockInQuantity);
                    decimal QualityInc = x.StockIns.Where(s => s.StockInDate >= fromDateDt && s.StockInDate <= toDateDt).Sum(s => s.QualityInQuantity);
                    decimal IssueQuantityc = (decimal)x.StockOuts.Where(s => s.IssueDate >= fromDateDt && s.IssueDate <= toDateDt).Sum(s => s.IssueQuantity);
                    decimal ReturnQuantityc = (decimal)x.StockReturn.Where(s => s.IssueDate >= fromDateDt && s.IssueDate <= toDateDt).Sum(s => s.IssueQuantity);
                    decimal BOMIssueQuantityc = (decimal)x.BOMIssues.Where(b => b.IssueDate >= fromDateDt && b.IssueDate <= toDateDt).Sum(b => b.IssueQuantity);
                    decimal SaleQuantityc = (decimal)x.saleitems.Where(b => b.IssueDate >= fromDateDt && b.IssueDate <= toDateDt).Sum(b => b.Quantity);
                    decimal RejectionQuantity = (decimal)x.LineRejection.Where(lr => lr.RejectionDate >= fromDateDt && lr.RejectionDate <= toDateDt).Sum(lr => lr.RejectionQuantity);
                    decimal indentedQuantity = x.IndentedItems
                                        .Where(i => i.IndentCreatedOn >= fromDateDt && i.IndentCreatedOn <= toDateDt)
                                        .Sum(i => i.ActualRequired) ?? 0m;
                    decimal poQuantity = x.PoItems
                                .Where(po => po.PODate >= fromDateDt && po.PODate <= toDateDt)
                                .Sum(po => (decimal?)po.POQuantity) ?? 0m;
                    // decimal BOMReturnQuantityc = (decimal)x.BOMReturn.Where(b => b.IssueDate >= fromDateDt && b.IssueDate <= toDateDt).Sum(b => b.IssueQuantity);
                    return new
                    {   //ItemCode=x.ItemId,
                        ItemId = x.ItemId,
                        ItemCode = x.ItemCode,
                        ItemName = x.ItemName,
                        BasicPrice = x.BasicPrice,
                        StockAsOnToday = x.InitialStock,
                        WIPQuantity = wipQuantity,

                        //  OpeningStock = x.OpeningStock
                        // + x.StockIns.Where(s => s.StockInDate < fromDateDt && s.StockInDate >= startDateForCalculations).Sum(s => s.QualityInQuantity)
                        // - x.StockOuts.Where(s => s.IssueDate < fromDateDt && s.IssueDate >= startDateForCalculations).Sum(s => s.IssueQuantity),
                        //OpeningStock1=final2,

                        OpeningStock1 = x.OpeningStock // as on os date
                      + x.StockIns.Where(s => s.StockInDate < fromDateDt && s.StockInDate >= startDateForCalculations).Sum(s => s.QualityInQuantity)
                      - x.StockOuts.Where(s => s.IssueDate < fromDateDt && s.IssueDate >= startDateForCalculations).Sum(s => s.IssueQuantity)
                      + x.StockReturn.Where(s => s.IssueDate < fromDateDt && s.IssueDate >= startDateForCalculations).Sum(s => s.IssueQuantity)
                     // + x.BOMReturn.Where(s => s.IssueDate < fromDateDt && s.IssueDate >= startDateForCalculations).Sum(s => s.IssueQuantity)
                      - x.BOMIssues.Where(b => b.IssueDate < fromDateDt && b.IssueDate >= startDateForCalculations).Sum(b => b.IssueQuantity)
                      - x.LineRejection.Where(lr => lr.RejectionDate < fromDateDt && lr.RejectionDate >= startDateForCalculations).Sum(lr => lr.RejectionQuantity)
                      - x.saleitems.Where(sl => sl.IssueDate < fromDateDt && sl.IssueDate >= startDateForCalculations).Sum(sl => sl.Quantity),




                        StockIn = x.StockIns.Where(s => s.StockInDate >= fromDateDt && s.StockInDate <= toDateDt).Sum(s => s.StockInQuantity),
                        QualityIn = x.StockIns.Where(s => s.StockInDate >= fromDateDt && s.StockInDate <= toDateDt).Sum(s => s.QualityInQuantity),
                        QualityRejected = x.StockIns.Where(s => s.StockInDate >= fromDateDt && s.StockInDate <= toDateDt).Sum(s => s.QualityRejectQuantity),
                        IssueQuantity = x.StockOuts.Where(s => s.IssueDate >= fromDateDt && s.IssueDate <= toDateDt).Sum(s => s.IssueQuantity),
                        ReturnQuantity= x.StockReturn.Where(s => s.IssueDate >= fromDateDt && s.IssueDate <= toDateDt).Sum(s => s.IssueQuantity),
                       // BOMReturnQuantity= x.BOMReturn.Where(b => b.IssueDate >= fromDateDt && b.IssueDate <= toDateDt).Sum(b => b.IssueQuantity),
                        BOMIssueQuantity = x.BOMIssues.Where(b=> b.IssueDate>= fromDateDt && b.IssueDate<=toDateDt).Sum(b => b.IssueQuantity),
                        SaleQuantity=x.saleitems.Where(b=> b.IssueDate>=fromDateDt && b.IssueDate<=toDateDt).Sum(b=> b.Quantity),
                        RejectionQuantity = RejectionQuantity,
                        IndentedQuantity = indentedQuantity,
                        PoQuantity = poQuantity,


                        // ClosingStock = x.OpeningStock
                        //  + x.StockIns.Where(s => s.StockInDate >= startDateForCalculations && s.StockInDate <= toDateDt).Sum(s => s.QualityInQuantity)
                        //  - x.StockOuts.Where(s => s.IssueDate >= startDateForCalculations && s.IssueDate <= toDateDt).Sum(s => s.IssueQuantity)

                        // ClosingStock = x.OpeningStock
                        // + x.StockIns.Where(s => s.StockInDate >= startDateForCalculations && s.StockInDate <= toDateDt).Sum(s => s.QualityInQuantity)
                        // - x.StockOuts.Where(s => s.IssueDate >= startDateForCalculations && s.IssueDate <= toDateDt).Sum(s => s.IssueQuantity)
                        // - x.BOMIssues.Where(b=> b.IssueDate>= startDateForCalculations && b.IssueDate <=toDateDt).Sum(b => b.IssueQuantity)  // Subtract BOM issue quantity

                        //  ClosingStock = OpeningStock1c+QualityInc-IssueQuantityc+ReturnQuantityc-BOMIssueQuantityc,
                        ClosingStock = OpeningStock1c + QualityInc - IssueQuantityc + ReturnQuantityc - BOMIssueQuantityc - RejectionQuantity - SaleQuantityc,
                        //x.OpeningStock // as on os date
                        // + x.StockIns.Where(s => s.StockInDate < fromDateDt && s.StockInDate >= startDateForCalculations).Sum(s => s.QualityInQuantity)
                        // - x.StockOuts.Where(s => s.IssueDate < fromDateDt && s.IssueDate >= fromDateDt).Sum(s => s.IssueQuantity)
                        // - x.BOMIssues.Where(b => b.IssueDate < fromDateDt && b.IssueDate >= startDateForCalculations).Sum(b => b.IssueQuantity)
                        //+ x.StockIns.Where(s => s.StockInDate >= fromDateDt && s.StockInDate <= toDateDt).Sum(s => s.QualityInQuantity)
                        //- x.StockOuts.Where(s => s.IssueDate >= fromDateDt && s.IssueDate <= toDateDt).Sum(s => s.IssueQuantity)
                        //- x.BOMIssues.Where(b=> b.IssueDate>= fromDateDt && b.IssueDate <=toDateDt).Sum(b => b.IssueQuantity)

                    };
                })
                .ToList();

            return Json(result, JsonRequestBehavior.AllowGet);
        }
       

        public JsonResult GetStockMovementReport2(string fromDate, string toDate)
        {
            // Parse the input dates
            DateTime fromDateDt = DateTime.ParseExact(fromDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            DateTime toDateDt = DateTime.ParseExact(toDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

            // Make toDateDt inclusive of the entire day
            toDateDt = toDateDt.AddDays(1).AddTicks(-1);

            // Fetch ALL stock-in data
            var stockInData = DB.IEPLStockIN_Detail
                .Join(DB.IEPLStockIN_Head, d => d.HeadId, h => h.Id, (d, h) => new { d.ItemCode, d.Quantity, h.CreatedDate })
                .Select(x => new
                {
                    ItemCode = x.ItemCode,
                    StockInQuantity = x.Quantity,
                    StockInDate = x.CreatedDate
                })
                .ToList(); // Execute the query

            // Fetch ALL stock-out data
            var stockIssueData = DB.IEPLStockIssueDetails
                .Where(i => i.ApprovedDate != null)
                .Select(g => new
                {
                    ItemCode = g.Itemcodeid,
                    IssueQuantity = g.Quantity,
                    IssueDate = g.ApprovedDate
                })
                .ToList();

            var allBomItemMasters = DB.BOMItemMasters.ToList();

            // Step 4: Combine data with Item Masters (IN MEMORY)
            var combinedData = allBomItemMasters
                .Select(m => new
                {
                    ItemId = m.Itemid,
                    ItemCode = m.ItemCode,
                    ItemName = m.ItemName,
                    InitialStock = m.Stock,
                    StockIns = stockInData.Where(s => s.ItemCode == m.Itemid).ToList(),
                    StockOuts = stockIssueData.Where(i => i.ItemCode == m.Itemid).ToList()
                });

            // Step 5: Calculate Opening and Closing Stock (IN MEMORY)
            var result = combinedData
                .Select(x => new
                {
                    ItemId = x.ItemId,
                    ItemCode = x.ItemCode,
                    ItemName = x.ItemName,
                    StockAsOnToday = x.InitialStock,  // Directly use InitialStock as Stock as on Today
                    OpeningStock = x.InitialStock + x.StockIns.Where(s => s.StockInDate < fromDateDt).Sum(s => s.StockInQuantity) - x.StockOuts.Where(s => s.IssueDate < fromDateDt).Sum(s => s.IssueQuantity),
                    StockIn = x.StockIns.Where(s => s.StockInDate >= fromDateDt && s.StockInDate <= toDateDt).Sum(s => s.StockInQuantity),
                    IssueQuantity = x.StockOuts.Where(s => s.IssueDate >= fromDateDt && s.IssueDate <= toDateDt).Sum(s => s.IssueQuantity),
                    ClosingStock = x.InitialStock + x.StockIns.Sum(s => s.StockInQuantity) - x.StockOuts.Sum(s => s.IssueQuantity)
                })
                .ToList();

            return Json(result, JsonRequestBehavior.AllowGet);
        }


        public JsonResult GetStockMovementReport1(string fromDate, string toDate)
        {

            // Assuming fromDate and toDate are strings in "yyyy-MM-dd" format
            DateTime fromDateDt = DateTime.ParseExact(fromDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            DateTime toDateDt = DateTime.ParseExact(toDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

            var stockInData = DB.IEPLStockIN_Detail
                .Join(DB.IEPLStockIN_Head, d => d.HeadId, h => h.Id, (d, h) => new { d.ItemCode, d.Quantity, h.CreatedDate })
                .Where(x => DbFunctions.TruncateTime(x.CreatedDate) >= fromDateDt.Date && DbFunctions.TruncateTime(x.CreatedDate) <= toDateDt.Date)
                .GroupBy(x => x.ItemCode)
                .Select(g => new
                {
                    ItemCode = g.Key,
                    StockInQuantity = g.Sum(x => x.Quantity),
                    FirstStockInDate = g.Min(x => x.CreatedDate)
                });


            var stockIssueData = DB.IEPLStockIssueDetails
                .Where(i => i.ApprovedDate != null &&
                            DbFunctions.TruncateTime(i.ApprovedDate) >= fromDateDt.Date &&
                            DbFunctions.TruncateTime(i.ApprovedDate) <= toDateDt.Date)
                .GroupBy(i => i.Itemcodeid)
                .Select(g => new
                {
                    ItemCode = g.Key,
                    IssueQuantity = g.Sum(i => i.Quantity)
                });


            var result = DB.BOMItemMasters
                .GroupJoin(stockInData, m => m.Itemid, s => s.ItemCode, (m, s) => new { m, s })
                .SelectMany(x => x.s.DefaultIfEmpty(), (x, s) => new
                {
                    ItemId = x.m.Itemid,
                    ItemCode = x.m.ItemCode,
                    ItemName = x.m.ItemName,
                    InitialStock = x.m.Stock,
                    StockInQuantity = s != null ? s.StockInQuantity : 0,
                    FirstStockInDate = s != null ? s.FirstStockInDate : (DateTime?)null
                })
                .GroupJoin(stockIssueData, m => m.ItemId, i => i.ItemCode, (m, i) => new { m, i })
                .SelectMany(x => x.i.DefaultIfEmpty(), (x, i) => new
                {
                    x.m.ItemId,
                    x.m.ItemCode,
                    x.m.ItemName,
                    x.m.InitialStock,
                    x.m.StockInQuantity,
                    x.m.FirstStockInDate,
                    IssueQuantity = i != null ? i.IssueQuantity : 0
                })
                .ToList();
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult ReadytoDispatch()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "ReadytoDispatch" && ab.Status == true
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
        private string GetSortKey(string itemName)
        {
            if (string.IsNullOrEmpty(itemName))
            {
                return itemName;
            }

            Match match = Regex.Match(itemName, "^\\d{1,4}"); // Match 1 to 3 digits at the beginning

            if (match.Success)
            {
                return match.Value.PadLeft(4, '0') + itemName.Substring(match.Length); // Pad with leading zeros for correct numeric sorting, and keep the rest of the string
            }

            return itemName; // If no numeric prefix, sort alphabetically
        }

        public ActionResult GetReportData(DateTime? startDate, DateTime? endDate)
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var reportData = (from ed in DB.BOMEwapDetails
                              join im in DB.BOMItemMasters on ed.ItemID equals im.Itemid into imGroup
                              from im in imGroup.DefaultIfEmpty()
                              join md in DB.EngineModels on ed.Model equals md.EngineId into mdGroup
                              from md in mdGroup.DefaultIfEmpty()
                              where ed.DispatchStatus == 0 && (ed.PDIStatus==0 || ed.PDIStatus==1) && ed.Companyid==companyid
                              select new
                              {
                                  ed = ed,
                                  im = im,
                                  md = md
                              });

            var data = reportData.GroupBy(x => new { x.ed.ItemID, Itemcode = x.im.ItemCode, ItemName = x.im.ItemName, x.md.EngineModel1 })
                                 .Select(g => new
                                 {
                                     ItemID = g.Key.ItemID,
                                     ItemCode = g.Key.Itemcode,
                                     ItemName = g.Key.ItemName != null ? g.Key.ItemName.Contains("Set") ? g.Key.ItemName.Substring(0, g.Key.ItemName.IndexOf("Set")).Trim() : g.Key.ItemName : null,
                                     ItemNameBar = g.Key.ItemName,
                                     TotalQuantity = g.Sum(x => x.ed.Quantity),
                                     Model = g.FirstOrDefault().md.EngineModel1
                                 })
                                 .ToList(); // Execute the query and bring data into memory
            var grandTotalQuantity = data.Sum(item => item.TotalQuantity);
            // Now you can use your custom GetSortKey function for sorting in memory
            data = data.OrderBy(item => GetSortKey(item.ItemName)).ToList();
            var data1= new
            {
                grandTotalQuantity,
                data
            };
            return Json(data1, JsonRequestBehavior.AllowGet);
        }
        public class EwapReportViewModel
        {
            public int ItemID { get; set; }
            public int TotalQuantity { get; set; }
            public string Model { get; set; }
            public string EngineSerialNumber { get; set; }
            //... other properties
        }

        public JsonResult PendingQuality(string stockLevel)
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var Quality = (from ab in DB.IEPLStockIN_Detail
                           join item in DB.BOMItemMasters on ab.ItemCode equals item.Itemid
                           join uom in DB.BOM_UOM on item.UOM equals uom.id
                           join bill in DB.IEPLStockIN_Head on ab.HeadId equals bill.Id
                           where ab.QualityApproved == 0 && ab.CompanyID == companyid && ab.IsDeleted==0
                           where bill.Isdeleted==0
                           select new
                           {
                               BillNo = bill.BillNumber,
                               BillDate = bill.BillDate,
                               GRNumber = bill.GRNumber,
                               GRDate = bill.GRDate,
                               ItemCode = item.ItemCode,
                               ItemName = item.ItemName,
                               UOMText = uom.UOM,
                               totalqty = ab.Quantity,
                               PendingQty = ab.Quantity - ab.QualityApprovedQty,
                               Stock = item.Stock

                           }).AsEnumerable();


            return Json(Quality.ToList(), JsonRequestBehavior.AllowGet);
        }
        public ActionResult BOMItemIssueDetail()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "BOMItemIssueDetail" && ab.Status == true
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
        public ActionResult BomDetailsReport(string itemCode)
        {
            try
            {
                int companyid = Convert.ToInt32(Session["Company_ID"].ToString());

                // Get Itemid
                var itemid = DB.BOMItemMasters.Where(x => x.ItemCode == itemCode).Select(x => x.Itemid).SingleOrDefault();
                if (itemid != null)
                {
                    DateTime today = DateTime.Now;
                    int currentYear = today.Year;
                    int startYear;

                    // If we are in Jan, Feb, or March, the financial year started last year
                    if (today.Month < 4)
                    {
                        startYear = currentYear - 1;
                    }
                    else
                    {
                        startYear = currentYear;
                    }

                    DateTime startDate = new DateTime(startYear, 4, 1);
                    DateTime endDate = new DateTime(startYear + 1, 3, 31);

                    var data = (from ab in DB.BOMVoucherlines
                                join BH in DB.BOMVouchers on ab.BOMVoucherid equals BH.BOMVoucherID into BHGroup // Left join
                                from BH in BHGroup.DefaultIfEmpty()
                                join IT in DB.BOMItemMasters on ab.rawitemid equals IT.Itemid into ITGroup
                                from IT in ITGroup.DefaultIfEmpty()
                                where ab.rawitemid == itemid
                                where BH.CompanyID == companyid && ab.Approveddate >= startDate && ab.Approveddate <= endDate // Filter by date range
                                select new
                                {
                                    BOMNumber = BH.VoucherNumber,
                                    ItemCode = IT.ItemCode,
                                    ItemName = IT.ItemName,
                                    UOM = ab.UOM,
                                    ApprovedQuantity = ab.ApprovedQuantity ?? 0, // Use?? to handle nulls
                                    RequiredQuantity = ab.QuantityRequired ?? 0, // Use?? to handle nulls
                                    ApprovedOn = ab.Approveddate,
                                    ApprovedBy = ab.Approvedby,
                                    Action = ab.Isdeleted == 1 ? "Cancelled" : (ab.Isdeleted == 0 ? "No Action" : "Unknown"),

                                }).ToList();

                    // Convert to JSON and return
                    return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet); // Important: AllowGet!
                }
                else
                {
                    return Json(new { success = false }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet); // Handle errors
            }
        }
        public ContentResult StockIssueReport(string stockLevel)
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var FinYear = Session["Fin_Year"].ToString();

            var Quality = (from ab in DB.IEPLStockIssueDetails
                           join item in DB.BOMItemMasters on ab.Itemcodeid equals item.Itemid
                           join uom in DB.BOM_UOM on item.UOM equals uom.id
                           join bill in DB.IEPLStockIssueHeads on ab.Voucherid equals bill.id
                           join depa in DB.DepartmentMasters on bill.Departmentid equals depa.id
                           where bill.CompanyID == companyid && bill.Fin_Year==FinYear && bill.IsChallan==false// Uncomment and ensure companyid is valid
                                                             // where ab.QualityApproved == 0 // Uncomment if needed
                           orderby bill.VoucherDate descending
                           select new
                           {
                               VN = bill.VoucherNumber,     // Shorter property name
                               VD = bill.VoucherDate,       // Shorter property name
                               Dept = depa.DepartmentName,  // Shorter property name
                               IC = item.ItemCode,          // Shorter property name
                               IN = item.ItemName,          // Shorter property name
                               Rem = ab.Remarks,            // Shorter property name
                               Qty = ab.Quantity,           // Shorter property name
                               AppQty = ab.ApprovedQuantity, // Shorter property name
                               UOM = uom.UOM,              // Shorter property name
                                                           // PendingQty = ab.Quantity - ab.ApprovedQuantity,  // Calculate on client-side if needed
                               AppBy = ab.ApprovedBy,       // Shorter property name
                                                            // AppDate = ab.ApprovedDate,     // Remove if not essential
                               Stock = item.Stock           // Shorter property name
                           }).AsEnumerable();

            // Serialize using Json.NET and return as ContentResult
            return Content(JsonConvert.SerializeObject(Quality.ToList()), "application/json");
        }
        public ContentResult StockReturnReportData(string stockLevel)
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var FinYear = Session["Fin_Year"].ToString();

            var Quality = (from ab in DB.IEPLStockReturnDetails
                           join item in DB.BOMItemMasters on ab.Itemcodeid equals item.Itemid
                           join uom in DB.BOM_UOM on item.UOM equals uom.id
                           join bill in DB.IEPLStockReturnHeads on ab.Voucherid equals bill.id
                           join depa in DB.DepartmentMasters on bill.Departmentid equals depa.id
                           where bill.CompanyID == companyid && bill.Fin_Year==FinYear// Uncomment and ensure companyid is valid
                                                             // where ab.QualityApproved == 0 // Uncomment if needed
                           orderby bill.VoucherDate descending
                           select new
                           {
                               VN = bill.VoucherNumber,     // Shorter property name
                               VD = bill.VoucherDate,       // Shorter property name
                               Dept = depa.DepartmentName,  // Shorter property name
                               IC = item.ItemCode,          // Shorter property name
                               IN = item.ItemName,          // Shorter property name
                               Rem = ab.Remarks,            // Shorter property name
                               Qty = ab.Quantity,           // Shorter property name
                               AppQty = ab.ApprovedQuantity, // Shorter property name
                               UOM = uom.UOM,              // Shorter property name
                                                           // PendingQty = ab.Quantity - ab.ApprovedQuantity,  // Calculate on client-side if needed
                               AppBy = ab.ApprovedBy,       // Shorter property name
                                                            // AppDate = ab.ApprovedDate,     // Remove if not essential
                               Stock = item.Stock           // Shorter property name
                           }).AsEnumerable();

            // Serialize using Json.NET and return as ContentResult
            return Content(JsonConvert.SerializeObject(Quality.ToList()), "application/json");
        }

        private IActionResult BadRequest(string v)
        {
            throw new NotImplementedException();
        }

        private IActionResult Ok(string v)
        {
            throw new NotImplementedException();
        }
        public class StockMovementReportModel
        {
            public DateTime Date { get; set; }
            public string ItemCode { get; set; }
            public string ItemName { get; set; }
            public decimal OpeningStock { get; set; }
            public decimal PurchaseQuantity { get; set; }
            public decimal IssueQuantity { get; set; }
            public decimal QualityApprovedQuantity { get; set; }
            public decimal QualityRejectedQuantity { get; set; }
            public decimal QualityReturnQuantity { get; set; }
            public decimal ClosingStock { get; set; }
            public decimal StockAvailable { get; internal set; }
        }
    }
}