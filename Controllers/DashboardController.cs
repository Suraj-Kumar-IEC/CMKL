using CMKL.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace CMKL.Views.BOM
{
    public class DashboardController : Controller
    {
        IECEntities DB = new IECEntities();
        // GET: Dashboard
        public ActionResult Dashboard()
        {
            return View();
        }
        public ActionResult RatingTrendGraphs()
        {
            return View();
        }
        [HttpGet]
        public JsonResult GetDistinctRatings()
        {
            try
            {
                if (Session["Company_ID"] == null) return Json(new { success = false }, JsonRequestBehavior.AllowGet);
                int companyId = Convert.ToInt32(Session["Company_ID"]);

                var distinctRatings = (from btu in DB.BOM_TestingUpdate
                                       join bv in DB.BOMVouchers on btu.BOMHeadid equals bv.BOMVoucherID
                                       join rt in DB.Ratings_Production on btu.GensetRating equals rt.id
                                       where bv.CompanyID == companyId && bv.Isdeleted == 0
                                       select rt.Rating)
                                       .Distinct()
                                       .Where(r => !string.IsNullOrEmpty(r))
                                       .ToList()
                                       .OrderBy(r => {
                                           decimal.TryParse(r, out decimal val);
                                           return val;
                                       })
                                       .ToList();

                return Json(new { success = true, ratings = distinctRatings }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetMonthlyDispatchDataByRating(string rating)
        {
            try
            {
                if (Session["Company_ID"] == null || Session["Fin_Year"] == null)
                    return Json(new { success = false, message = "Session Expired" }, JsonRequestBehavior.AllowGet);

                int companyId = Convert.ToInt32(Session["Company_ID"]);
                string finYear = Session["Fin_Year"].ToString();
                int startYear = int.Parse(finYear.Split('-')[0]);
                DateTime fyStart = new DateTime(startYear, 4, 1);
                DateTime fyEnd = fyStart.AddYears(1).AddDays(-1);

                // 1. Get Rating ID
                var ratingId = DB.Ratings_Production.Where(x => x.Rating == rating).Select(x => x.id).FirstOrDefault();

                // 2. Fetch ALL dispatches for this FY (to calculate Rating vs Total)
                var allDispatches = DB.DispatchDetails
                    .Where(x => x.CompanyID == companyId && x.BillDate >= fyStart && x.BillDate <= fyEnd)// && x.DispatchReturn == 0)
                    .Select(x => new {
                        x.id,
                        x.BillDate,
                        x.Ewapid // Link to find Rating
                    }).ToList();

                // 3. Link to Ratings (In-Memory for complex join optimization)
                var ewapRatingMap = (from ew in DB.BOMEwapDetails
                                     join btu in DB.BOM_TestingUpdate on ew.BomVoucherHeadID equals btu.BOMHeadid
                                     where ew.Companyid == companyId
                                     select new { ew.id, btu.GensetRating }).ToList();

                string[] monthLabels = { "April", "May", "June", "July", "August", "September", "October", "November", "December", "January", "February", "March" };
                double[] ratingData = new double[12];
                double[] totalData = new double[12];

                foreach (var d in allDispatches)
                {
                    int month = d.BillDate.Value.Month;
                    int idx = (month >= 4) ? (month - 4) : (month + 8);

                    // Add to Total Plant Volume
                    totalData[idx]++;

                    // Add to Specific Rating Volume
                    var rId = ewapRatingMap.FirstOrDefault(x => x.id == d.Ewapid)?.GensetRating;
                    if (rId == ratingId)
                    {
                        ratingData[idx]++;
                    }
                }

                var chartData = new
                {
                    labels = monthLabels,
                    // FIXED: Changed new[] to new object[] to allow datasets with different properties (like borderDash)
                    datasets = new object[]
                    {
                        new {
                            label = $"{rating} KVA Dispatch",
                            data = ratingData,
                            borderColor = "#00698f",
                            backgroundColor = "rgba(0, 105, 143, 0.1)",
                            fill = true,
                            tension = 0.4,
                            borderWidth = 3,
                            pointRadius = 4
                        },
                        new {
                            label = "Total Plant Output (All Ratings)",
                            data = totalData,
                            borderColor = "#cbd5e1",
                            backgroundColor = "transparent",
                            fill = false,
                            tension = 0.4,
                            borderWidth = 2,
                            borderDash = new int[] { 5, 5 },
                            pointRadius = 0
                        }
                    }
                };

                return Json(new
                {
                    success = true,
                    chartData = chartData,
                    totalUnits = ratingData.Sum(),
                    contribution = totalData.Sum() > 0 ? Math.Round((ratingData.Sum() / totalData.Sum()) * 100, 1) : 0
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpGet]
        public JsonResult GetDistinctEngineModels()
        {
            try
            {
                int companyId = Convert.ToInt32(Session["Company_ID"]);
                // Fetch engine models that have actually been used in EWAP dispatches
                var models = (from ew in DB.BOMEwapDetails
                              join em in DB.EngineModels on ew.Model equals em.EngineId
                              where ew.Companyid == companyId
                              select em.EngineModel1).Distinct().OrderBy(x => x).ToList();

                return Json(new { success = true, models = models }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet); }
        }
        [HttpGet]
        public JsonResult GetDistinctEngineConfigs()
        {
            try
            {
                int companyId = Convert.ToInt32(Session["Company_ID"]);

                var configs = (from ew in DB.BOMEwapDetails
                               join btu in DB.BOM_TestingUpdate on ew.BomVoucherHeadID equals btu.BOMHeadid
                               join em in DB.EngineModels on ew.Model equals em.EngineId
                               join rp in DB.Ratings_Production on btu.GensetRating equals rp.id
                               where ew.Companyid == companyId
                               select new
                               {
                                   rp.Rating,
                                   EngineModel = em.EngineModel1,
                                   Value = rp.id + "_" + em.EngineId
                               }).ToList()
                               .Select(x => new {
                                   // .Trim() removes trailing spaces and hidden characters like \r\n
                                   DisplayText = x.Rating + " KVA | " + x.EngineModel.Trim(),
                                   x.Value,
                                   SortOrder = double.TryParse(x.Rating, out double r) ? r : 0
                               })
                               .Distinct()
                               .OrderBy(x => x.SortOrder)
                               .ToList();

                return Json(new { success = true, configs = configs }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet); }
        }
        [HttpGet]
        public JsonResult GetMonthlyDispatchDataByConfig(string configValue)
        {
            try
            {
                int companyId = Convert.ToInt32(Session["Company_ID"]);
                string finYear = Session["Fin_Year"].ToString();
                int startYear = int.Parse(finYear.Split('-')[0]);

                // Current FY Range
                DateTime curStart = new DateTime(startYear, 4, 1);
                DateTime curEnd = curStart.AddYears(1).AddDays(-1);

                // Previous FY Range
                DateTime prevStart = curStart.AddYears(-1);
                DateTime prevEnd = curEnd.AddYears(-1);

                var ids = configValue.Split('_');
                int targetRatingId = int.Parse(ids[0]);
                int targetEngineId = int.Parse(ids[1]);

                // Fetch Dispatches for both years
                var allDispatches = DB.DispatchDetails
                    .Where(x => x.CompanyID == companyId && x.BillDate >= prevStart && x.BillDate <= curEnd)
                    .Select(x => new { x.BillDate, x.Ewapid }).ToList();

                var ewapMap = (from ew in DB.BOMEwapDetails
                               join btu in DB.BOM_TestingUpdate on ew.BomVoucherHeadID equals btu.BOMHeadid
                               where ew.Companyid == companyId
                               select new { ew.id, ew.Model, btu.GensetRating }).ToList();

                string[] monthLabels = { "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec", "Jan", "Feb", "Mar" };
                double[] currentYearData = new double[12];
                double[] previousYearData = new double[12];

                foreach (var d in allDispatches)
                {
                    var map = ewapMap.FirstOrDefault(x => x.id == d.Ewapid);
                    if (map != null && map.GensetRating == targetRatingId && map.Model == targetEngineId)
                    {
                        int month = d.BillDate.Value.Month;
                        int idx = (month >= 4) ? (month - 4) : (month + 8);

                        if (d.BillDate >= curStart) currentYearData[idx]++;
                        else if (d.BillDate >= prevStart && d.BillDate <= prevEnd) previousYearData[idx]++;
                    }
                }

                var chartData = new
                {
                    labels = monthLabels,
                    datasets = new object[] {
                new {
                    label = $"Current FY ({finYear})",
                    data = currentYearData,
                    borderColor = "#00698f",
                    backgroundColor = "rgba(0, 105, 143, 0.1)",
                    fill = true, tension = 0.4, borderWidth = 3, pointRadius = 4
                },
                new {
                    label = $"Previous FY ({startYear-1}-{startYear})",
                    data = previousYearData,
                    borderColor = "#94a3b8",
                    borderDash = new int[] { 5, 5 },
                    fill = false, tension = 0.4, borderWidth = 2, pointRadius = 3
                }
            }
                };

                double curTotal = currentYearData.Sum();
                double prevTotal = previousYearData.Sum();
                double growth = prevTotal > 0 ? Math.Round(((curTotal - prevTotal) / prevTotal) * 100, 1) : 0;

                return Json(new
                {
                    success = true,
                    chartData = chartData,
                    totalUnits = curTotal,
                    prevTotal = prevTotal,
                    growth = growth
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet); }
        }

        [HttpGet]
        public JsonResult GetMonthlyDispatchDataByEngine(string engineModel)
        {
            try
            {
                int companyId = Convert.ToInt32(Session["Company_ID"]);
                string finYear = Session["Fin_Year"].ToString();
                int startYear = int.Parse(finYear.Split('-')[0]);
                DateTime fyStart = new DateTime(startYear, 4, 1);
                DateTime fyEnd = fyStart.AddYears(1).AddDays(-1);

                // 1. Get Engine ID
                var engineId = DB.EngineModels.Where(x => x.EngineModel1 == engineModel).Select(x => x.EngineId).FirstOrDefault();

                // 2. Fetch Dispatches with Engine Link
                var allDispatches = DB.DispatchDetails
                    .Where(x => x.CompanyID == companyId && x.BillDate >= fyStart && x.BillDate <= fyEnd)
                    .Select(x => new { x.BillDate, x.Ewapid }).ToList();

                var ewapModelMap = DB.BOMEwapDetails
                    .Where(x => x.Companyid == companyId)
                    .Select(x => new { x.id, x.Model }).ToList();

                string[] monthLabels = { "April", "May", "June", "July", "August", "September", "October", "November", "December", "January", "February", "March" };
                double[] engineData = new double[12];
                double[] totalData = new double[12];

                foreach (var d in allDispatches)
                {
                    int month = d.BillDate.Value.Month;
                    int idx = (month >= 4) ? (month - 4) : (month + 8);
                    totalData[idx]++;

                    var mId = ewapModelMap.FirstOrDefault(x => x.id == d.Ewapid)?.Model;
                    if (mId == engineId) engineData[idx]++;
                }

                var chartData = new
                {
                    labels = monthLabels,
                    datasets = new object[] {
                new {
                    label = $"{engineModel} Sales",
                    data = engineData,
                    borderColor = "#0ea5e9", // Blue for Engine
                    backgroundColor = "rgba(14, 165, 233, 0.1)",
                    fill = true, tension = 0.4, borderWidth = 3, pointRadius = 4
                },
                new {
                    label = "Total Plant Dispatches",
                    data = totalData,
                    borderColor = "#cbd5e1",
                    borderDash = new int[] { 5, 5 },
                    fill = false, tension = 0.4, borderWidth = 2, pointRadius = 0
                }
            }
                };

                return Json(new
                {
                    success = true,
                    chartData = chartData,
                    totalUnits = engineData.Sum(),
                    contribution = totalData.Sum() > 0 ? Math.Round((engineData.Sum() / totalData.Sum()) * 100, 1) : 0
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet); }
        }
    }

    //}
}