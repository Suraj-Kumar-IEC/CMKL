using CMKL.Models;
using CMKL.Views.BOM;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Microsoft.Ajax.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Dynamic;
using System.Linq;
using System.Web;
using System.Web.Management;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using System.Windows.Media.Animation;
using static CMKL.Controllers.InventoryController;
using static CMKL.Controllers.PurchaseController;
using static Microsoft.IO.RecyclableMemoryStreamManager;



namespace CMKL.Controllers
{
    public class InventoryController : Controller
    {
        IECEntities DB = new IECEntities();
        // GET: Inventory
        public ActionResult Itemmaster()
        {
            return View("ItemMaster");



        }
        public ActionResult AddLotManually()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "AddLotManually" && ab.Status == true
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
        public JsonResult SaveManualLot(Stock_lotDetail model)
        {
            try
            {
                if (model.ItemID <= 0 || model.OriginalQuantity <= 0)
                    return Json(new { success = false, message = "Invalid Item or Quantity" });

                // System fills
                model.CreatedOn = DateTime.Now;
                model.CreatedBy = Session["U_Name"]?.ToString() ?? "Manual Entry";
                model.CompanyID = Convert.ToInt32(Session["Company_ID"]);
                model.IsAvailable = true;
                model.IsDeleted = false;
                model.ReceivingLineID = 0;
                model.SupplierID = 0;
                model.IsReserved = false;

                // Ensure CurrentQty matches OriginalQty at start
                model.CurrentQuantity = model.OriginalQuantity;

                // model.RecieptDateTime will come from the View's date picker
                DB.Stock_lotDetail.Add(model);
                DB.SaveChanges();

                return Json(new { success = true, message = "Stock Lot Saved Successfully!" });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }
        public ActionResult LotMRNUpdate()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "LotMRNUpdate" && ab.Status == true
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
        // Fetch top 200 lots for the explorer
        [HttpGet]
        public JsonResult GetAllLots()
        {
            var list = (from l in DB.Stock_lotDetail
                        join im in DB.BOMItemMasters on l.ItemID equals im.Itemid
                        where l.IsDeleted == false
                        orderby l.id descending
                        select new
                        {
                            l.id,
                            l.Lot_SerialNumber,
                            l.CurrentQuantity,
                            l.ItemID
                        }).ToList();

            return Json(list, JsonRequestBehavior.AllowGet);
        }

        // Fetch specific lot with MRN check
        [HttpGet]
        public JsonResult GetLotInfo(int id)
        {
            var lot = (from ab in DB.Stock_lotDetail
                       join im in DB.BOMItemMasters on ab.ItemID equals im.Itemid
                       //join bom in DB.BOMVoucherlines
                       where ab.id == id
                       select new
                       {
                           // Fields from Stock_lotDetail
                           LotID = ab.id,
                           ab.ItemID,
                           ab.Lot_SerialNumber,
                           ab.CurrentQuantity,
                           ab.ReceivingLineID,
                           // Fields from BOMItemMasters
                           ItemName = im.ItemName,
                           ItemCode = im.ItemCode
                       }).FirstOrDefault();
            //var lot = DB.Stock_lotDetail.FirstOrDefault(x => x.id == id);
            if (lot == null) return Json(new { success = false, message = "Lot missing" }, JsonRequestBehavior.AllowGet);

            return Json(new { success = true, data = lot }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult UpdateLotSerial(int id, string serial, bool shouldSync)
        {
            try
            {
                var lot = DB.Stock_lotDetail.Find(id);
                if (lot == null) return Json(new { success = false, message = "Lot record not found." });

                // Update Lot Table
                lot.Lot_SerialNumber = serial;
                //lot.CreatedOn = DateTime.Now;
                // lot.CreatedBy = Session["U_Name"]?.ToString() ?? "System";

                // Update MRN (IEPLStockIssueDetail) Table via ReceivingLineID
                if (shouldSync && lot.ReceivingLineID > 0)
                {
                    var mrnLine = DB.IEPLStockIN_Detail.Find(lot.ReceivingLineID);
                    if (mrnLine != null)
                    {
                        mrnLine.LotNo = serial;
                    }
                }
                //Check weather Its Already in Production table and ewap Table


                DB.SaveChanges();
                return Json(new { success = true, message = "Records updated successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "System Error: " + ex.Message });
            }
        }
        public ActionResult ItemTrendAnalysis()
        {
            return View();
        }
        [HttpGet]
        public JsonResult SearchItemsByName(string term)
        {
            if (Session["Company_ID"] == null) return Json(null, JsonRequestBehavior.AllowGet);
            int companyId = Convert.ToInt32(Session["Company_ID"]);

            // Fetch items matching the name, limited to top 15 for performance
            var items = DB.BOMItemMasters
                .Where(x => x.CompanyID == companyId && x.ItemName.Contains(term) && x.ItemCategory == 2)
                .Select(x => new {
                    label = x.ItemName, // Shown in dropdown
                    value = x.ItemName, // Value in text box
                    code = x.ItemCode   // Internal ID used for the report
                })
                .Take(15)
                .ToList();

            return Json(items, JsonRequestBehavior.AllowGet);
        }
        [HttpGet]
        public JsonResult GetItemTrendData(string itemCode, int interval = 1)
        {
            try
            {
                // 1. Session & Master Check
                if (Session["Company_ID"] == null)
                    return Json(new { success = false, message = "Session expired. Please re-login." }, JsonRequestBehavior.AllowGet);

                int companyid = Convert.ToInt32(Session["Company_ID"]);
                var item = DB.BOMItemMasters.FirstOrDefault(x => x.ItemCode == itemCode && x.CompanyID == companyid);

                if (item == null)
                    return Json(new { success = false, message = "Part code not found in Item Master." }, JsonRequestBehavior.AllowGet);

                // 2. Define Financial Year Boundaries (April to March)
                int currentYear = DateTime.Now.Year;
                // If current month is Jan-Mar, the FY started last year
                DateTime fyStart = new DateTime(DateTime.Now.Month >= 4 ? currentYear : currentYear - 1, 4, 1);
                DateTime fyEnd = fyStart.AddYears(1).AddDays(-1);

                // 3. Batch Fetch all transactions to optimize performance (Prevents N+1 queries in the loop)
                var stockIns = DB.IEPLStockIN_Detail
                    .Where(d => d.ItemCode == item.Itemid && d.IsDeleted == 0 && d.QApprovedDate <= fyEnd)
                    .Select(d => new { Qty = d.QualityApprovedQty ?? 0m, Date = d.QApprovedDate })
                    .ToList();

                var stockIssues = DB.IEPLStockIssueDetails
                    .Where(i => i.Itemcodeid == item.Itemid && i.IsDeleted == null && i.ApprovedDate <= fyEnd)
                    .Select(i => new { Qty = i.Quantity, Date = i.ApprovedDate })
                    .ToList();

                var stockReturns = DB.IEPLStockReturnDetails
                    .Where(i => i.Itemcodeid == item.Itemid && i.ApprovedDate <= fyEnd)
                    .Select(i => new { Qty = i.Quantity, Date = i.ApprovedDate })
                    .ToList();

                var lineRejections = DB.LineRejectionDetails
                    .Where(i => i.Itemid == item.Itemid && i.ApprovedStatus == 1 && i.ApprovedDate <= fyEnd)
                    .Select(i => new { Qty = i.ApprovedQuantity, Date = i.ApprovedDate })
                    .ToList();

                var bomIssues = DB.BOMVoucherlines
                    .Where(bl => bl.rawitemid == item.Itemid && bl.Isdeleted == 0 && bl.Approveddate <= fyEnd)
                    .Select(bl => new { Qty = bl.ApprovedQuantity ?? 0m, Date = bl.Approveddate })
                    .ToList();

                // Baseline: Opening Stock record
                var openingStock = DB.OpeningStocks
                    .Where(os => os.Itemid == item.Itemid && os.Companyid == companyid)
                    .Select(os => new { Qty = os.OpeningStock1, Date = os.CreatedDate })
                    .FirstOrDefault();

                decimal baseOpening = openingStock?.Qty ?? 0m;
                DateTime? openingDate = openingStock?.Date;

                // 4. Generate Snapshots based on Interval Resolution
                List<object> chartPoints = new List<object>();
                DateTime loopEnd = (fyEnd > DateTime.Now) ? DateTime.Now : fyEnd;

                DateTime currentDate = fyStart;
                while (currentDate <= loopEnd)
                {
                    // snapshotPoint is the end of the current iteration day (23:59:59)
                    DateTime snapshotPoint = currentDate.Date.AddDays(1).AddTicks(-1);

                    // Calculation: Cumulative sum of all historical movements up to this specific snapshotPoint
                    // Note: We check if the transaction date is >= openingDate to avoid double counting baseline figures
                    decimal totalIn = stockIns.Where(s => s.Date <= snapshotPoint && (openingDate == null || s.Date >= openingDate)).Sum(s => s.Qty);

                    decimal totalOut = (decimal)(stockIssues.Where(s => s.Date <= snapshotPoint && (openingDate == null || s.Date >= openingDate)).Sum(s => s.Qty)
                                     + bomIssues.Where(s => s.Date <= snapshotPoint && (openingDate == null || s.Date >= openingDate)).Sum(s => s.Qty)
                                     + lineRejections.Where(s => s.Date <= snapshotPoint && (openingDate == null || s.Date >= openingDate)).Sum(s => s.Qty));

                    decimal totalReturn = (decimal)stockReturns.Where(s => s.Date <= snapshotPoint && (openingDate == null || s.Date >= openingDate)).Sum(s => s.Qty);

                    decimal closingBalanceAtPoint = baseOpening + totalIn - totalOut + totalReturn;

                    chartPoints.Add(new
                    {
                        Label = currentDate.ToString("dd MMM yyyy"),
                        Value = closingBalanceAtPoint,
                        Min = item.Minstklvl ?? 0,
                        Max = item.Maxstklvl ?? 0
                    });

                    // Advance the date based on UI Interval choice
                    if (interval == 30) // Monthly logic
                    {
                        // Snaps to the 1st of the next month
                        currentDate = new DateTime(currentDate.Year, currentDate.Month, 1).AddMonths(1);
                    }
                    else
                    {
                        // Staggered days logic (1, 5, 10, 15)
                        currentDate = currentDate.AddDays(interval);
                    }
                }

                // 5. Final Output
                return Json(new
                {
                    success = true,
                    data = chartPoints,
                    itemName = item.ItemName,
                    itemCode = item.ItemCode
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                // In production, log the full error stack here
                return Json(new { success = false, message = "System Error: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult ItemmasterFG()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "ItemmasterFG" && ab.Status == true
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
        public ActionResult StockReturn()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "StockReturn" && ab.Status == true
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
        public ActionResult LineRejection()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "LineRejection" && ab.Status == true
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
        public ActionResult PendingLineRejectionApproval()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "PendingLineRejectionApproval" && ab.Status == true
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
        public ActionResult PackingSlipPrint(int BOMID)
        {
            ViewBag.BOMID = BOMID;
            return View();
        }
        [HttpPost]
        public ActionResult ApproveLR(int lrId)

        {
            int companyid = (int)Session["Company_ID"];
            var lr = DB.LineRejectionDetails.Where(x=>x.LRHeadid==lrId).FirstOrDefault();
            if (lr == null)
            {
                return Json(new { success = false, message = "Line Rejection not found." });
            }
            lr.ApprovedDate = System.DateTime.Now;
            lr.ApprovedQuantity = lr.Quantity;
            lr.ApprovedStatus = 1;
            lr.ApprovedBy = Session["U_Name"].ToString();

            //also we need to update stock in stock table
            var stock= DB.StockTables.Where(x=>x.itemid==lr.Itemid && x.CompanyID==companyid).FirstOrDefault();
            if (stock != null)
            {
                stock.Stock-=lr.Quantity;
            }
            DB.SaveChanges();

            return Json(new { success = true, message = "Line Rejection approved successfully." });
        }
        public ActionResult GetLRDetailsJson(int lrId)
        {
            //var headid = DB.LineRejectionHeads.Where(x => x.id == lrId).SingleOrDefault();

            var LRDet = (from ab in DB.LineRejectionHeads
                         join bb in DB.LineRejectionDetails on ab.id equals bb.LRHeadid
                         join im in DB.BOMItemMasters on bb.Itemid equals im.Itemid
                         join sup in DB.SupplierMasters on ab.SupplierID equals sup.id
                         join mr in DB.IEPLStockIN_Head on bb.BillHeadID equals mr.Id
                         where ab.id == lrId
                         select new
                         {
                             LRId = ab.id,
                             VoucherNumber = ab.VoucherNumber,
                             VoucherDate = ab.Createdon,
                             SupplierName = sup.SupplierName,
                             BillNumber = mr.BillNumber,
                             LRDate = ab.VoucherDate,
                             CreatedBy = ab.Createdby,
                             CreatedOn = ab.Createdon,
                             Remarks = bb.Remarks,
                             ItemName = im.ItemName,
                             Quantity = bb.Quantity,
                         }).SingleOrDefault();
            

            if (LRDet == null)
            {
                return Json(new { error = "Line Rejection not found" }, JsonRequestBehavior.AllowGet);
            }

            return Json(LRDet, JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetPendingLRsJson()
        {
            var FinYear = Session["Fin_Year"].ToString();
            int companyid = (int)Session["Company_ID"];
            var data = (from ab in DB.LineRejectionDetails
                        join hd in DB.LineRejectionHeads on ab.LRHeadid equals hd.id
                        join sup in DB.SupplierMasters on hd.SupplierID equals sup.id
                        join bill in DB.IEPLStockIN_Head on ab.BillHeadID equals bill.Id
                       where ab.ApprovedStatus==0 && ab.IsDeleted==0 && hd.CompanyID==companyid && hd.Fin_Year==FinYear
                       select new
                       {
                           lrId=hd.id,
                           voucherNumber =hd.VoucherNumber,
                           supplierName=sup.SupplierName,
                           billNumber=bill.BillNumber,
                           billdate=bill.BillDate,

                       }).ToList();
            return Json(new { data }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult LRNumber()
        {
            var FinYear = Session["Fin_Year"].ToString();
            int companyid = (int)Session["Company_ID"];
            var LRnumber = DB.Bill_Series.Where(x => x.Type == "LineRejection" && x.Fin_Year == FinYear && x.CompanyID == companyid).SingleOrDefault();
            var number = LRnumber.Series + LRnumber.Number;
            return Json(new { number },JsonRequestBehavior.AllowGet) ;
        }
        public ActionResult GetSupplierBills(int supplierid)
        {
            var FinYear = Session["Fin_Year"].ToString();
            int companyid = (int)Session["Company_ID"];
            var bills = (from ab in DB.IEPLStockIN_Head
                         where ab.Supplierid == supplierid && ab.CompanyID == companyid && ab.Isdeleted == 0 && ab.BillClosed == 1
                         select new
                         {
                             value = ab.Id,
                             text = ab.BillNumber,
                             date=ab.BillDate,
                             MRN=ab.Vouchernumber,
                         }).ToList();
            return Json(new { bills }, JsonRequestBehavior.AllowGet);               

        }
        public ActionResult GetIncomingItems (int billid)
        {
            var items = (from ab in DB.IEPLStockIN_Detail
                         join im in DB.BOMItemMasters on ab.ItemCode equals im.Itemid into imgroup
                         from im in imgroup.DefaultIfEmpty()
                         where ab.HeadId == billid && ab.IsDeleted == 0
                         select new
                         {
                             value = ab.Id,
                             itemname = im.ItemName,
                             itemid = ab.ItemCode,
                             quantity = ab.QualityApprovedQty,
                             lot=ab.LotNo,
                         }).ToList();
            return Json(new { items }, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public ActionResult SaveLR(LrData data)
                        {
            var FinYear = Session["Fin_Year"].ToString();
            int companyid = (int)Session["Company_ID"];
            decimal totalExistingQuantity = 0;
            decimal Totalquantity = 0;
            //First Check LR Already Exist or Not
            var check = DB.LineRejectionDetails.Where(x => x.BillHeadID == data.BillId && x.BillDetailID == data.Billitemid).ToList();
            //Get Quality Approved Quantity
            var ApprovedQty = DB.IEPLStockIN_Detail.Where(x => x.Id == data.Billitemid).Select(x=>x.QualityApprovedQty).SingleOrDefault();
            if (check.Any())
            {
                // Calculate the sum of quantities from existing records
                totalExistingQuantity = (decimal)check.Sum(x => x.Quantity); // Assuming Quantity is an integer property                
            }
            //Total Inculding Prvious LR
            Totalquantity = totalExistingQuantity + data.Quantity;
            if (Totalquantity > ApprovedQty)
            {

                return Json(new { success = false, msg = "LR Quantity exceeding with Total Approved Quantity" }, JsonRequestBehavior.AllowGet);

            }
            else
            {
                LineRejectionHead lineRejectionHead = new LineRejectionHead();
                lineRejectionHead.Createdon = DateTime.Now;
                lineRejectionHead.Createdby = Session["U_Name"].ToString();
                lineRejectionHead.VoucherDate = data.LRDate;
                lineRejectionHead.CompanyID = companyid;
                lineRejectionHead.Fin_Year = FinYear;
                lineRejectionHead.Isdeleted = 0;
                lineRejectionHead.SupplierID=data.SupplierId;
                //Get Latest Voucher Number
                var LRnumber = DB.Bill_Series.Where(x => x.Type == "LineRejection" && x.Fin_Year == FinYear && x.CompanyID == companyid).SingleOrDefault();                
                var number = LRnumber.Series + LRnumber.Number;
                lineRejectionHead.VoucherNumber = number;
                DB.LineRejectionHeads.Add(lineRejectionHead);
                DB.SaveChanges();

                //Now Update Its Line
                LineRejectionDetail LRD = new LineRejectionDetail();
                LRD.LRHeadid=lineRejectionHead.id;
                LRD.Itemid=data.ItemId;
                LRD.Remarks=data.Remarks;
                LRD.Quantity=data.Quantity;
                LRD.ApprovedQuantity = 0;
                LRD.ApprovedStatus = 0;
                LRD.IsDeleted = 0;
                LRD.BillHeadID=data.BillId;
                LRD.BillDetailID = data.Billitemid;
                DB.LineRejectionDetails.Add(LRD);

                //Update Bill Series
                LRnumber.Number += 1;
                DB.SaveChanges();
                int LRId= lineRejectionHead.id;
               
                return Json(new { success = true, msg = "LR has been Saved.." }, JsonRequestBehavior.AllowGet);
            }


        }
        private void SendLREmail(int LRId, int companyid)
        {

        }
        [HttpPost]
        public ActionResult SaveFGitem(FGitemData data)
        {
            using (var transaction = DB.Database.BeginTransaction())
            {
                try
                {
                    int companyid = (int)Session["Company_ID"];
                    var itemExists = DB.BOMItemMasters.Any(ab => ab.ItemCode == data.masteritemcode);

                    if (itemExists)
                    {
                        return Json(new { success = false, msg = "Item code already exists." }, JsonRequestBehavior.AllowGet);
                    }
                    else
                    {
                        // Save to BOMItemMaster
                        BOMItemMaster BIM = new BOMItemMaster();
                        BIM.ItemCode = data.masteritemcode;
                        BIM.ItemName = data.masteritemname;
                        BIM.UOM = 1;
                        BIM.Desc = data.masteritemname;
                        BIM.HSNCode = "NA";
                        BIM.BasicPrice = 0;
                        BIM.Minstklvl = 0;
                        BIM.Maxstklvl = 0;
                        BIM.MinimumOrderQuantity = 0;
                        BIM.Stock = 0;
                        BIM.ItemCategory = 4;
                        BIM.ItemCreatedon = System.DateTime.Now;
                        BIM.Createdby = Session["U_Name"].ToString();
                        BIM.RejectedStock = 0;
                        BIM.CompanyID = companyid;
                        DB.BOMItemMasters.Add(BIM);
                        DB.SaveChanges();

                        // Save to StockTable
                        StockTable ST = new StockTable();
                        ST.itemid = BIM.Itemid;
                        ST.Stock = 0;
                        ST.Minstklvl = 0;
                        ST.Maxstklvl = 0;
                        ST.MOQ = 0;
                        ST.CompanyID = companyid;
                        ST.BasicPrice = 0;
                        ST.LastDiscount = 0;
                        ST.LastSupplierid = 0;
                        ST.RejectedStock = 0;
                        DB.StockTables.Add(ST);
                        DB.SaveChanges();

                        // Save to BOMFinalProductCombination
                        BOMFinalProductCombination BPC = new BOMFinalProductCombination();
                        BPC.MasterProduct = data.finalItem;
                        BPC.Canopy = data.canopy;
                        BPC.FuelTank = data.fueltank;
                        BPC.Exhaust = data.exhaustsystem;
                        BPC.AcousticTreatment = data.acoustictreatment;
                        BPC.FinalPacking = data.finalpacking;
                        BPC.Assembly = data.assembly;
                        BPC.BaseFrame = data.baseframe;
                        BPC.DGType = data.dgassy;
                        BPC.Panel = data.panel;
                        BPC.Alternator = data.alternator;
                        BPC.Electrical = data.electrical;
                        BPC.ItemName = BIM.ItemName;
                        BPC.ItemCode = BIM.ItemCode;
                        BPC.ItemMasterID = BIM.Itemid;
                        BPC.Createdon = System.DateTime.Now;
                        DB.BOMFinalProductCombinations.Add(BPC);
                        DB.SaveChanges();

                        transaction.Commit();
                    }

                    return Json(new { success = true, msg = "Item saved.." }, JsonRequestBehavior.AllowGet);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    // Log the exception (ex) for debugging 

                    return Json(new { success = false, msg = "Error saving item." }, JsonRequestBehavior.AllowGet);
                }
            }
        }

        public ActionResult Purchase()
        {
            return View("BPRPurchase");


            // return View("Detail",items);
        }

        //Pending BPR Cases to list
        public ActionResult BPRPendingCases()
        {

            var pending = from ab in DB.PurchaseGFEs
                          join bn in DB.Companies on ab.company equals bn.CompanyID
                          where ab.purchaseapproval == 1
                          select new Pendingpurchase
                          { 
                              id=ab.id,
                              company=bn.CompanyName,
                              Billno=ab.Billno,
                              billdate=ab.Billno,
                              Billqty=(int)ab.Billqty,
                          
                          };
            return View("BPRPendingCases", pending);


            // return View("Detail",items);
        }


        //Get Stock RPT
        public ActionResult StockRPT()
        {

            var Stock = (from ST in DB.ItemMasterGFEs
                         join CD in DB.Companies on ST.Companyid equals CD.CompanyID
                         orderby ST.id descending
                        select new stockreport
                        {
                            Itemname=ST.Itemname,
                            itemdesc=ST.itemdesc,
                            Itemcategory= ST.Itemcategory,
                            unitofmeasurement=ST.unitofmeasurement,
                            presentstock=(int)ST.presentstock,
                            transit=(int)ST.transit,
                            phylogistic=(int)ST.phylogistic,
                            phyplant=(int)ST.phyplant,
                            readyforallocation=(int)ST.readyforallocation,
                            wip=(int)ST.wip,
                            testing=(int)ST.testing,
                            rejected=(int)ST.Rejectedstk,
                            Phase=ST.Phase,
                            company=CD.CompanyName,
                            norms=ST.norms,
                            

                        }                     
                        ).ToList();

            
                        
            return View("StockRPT",Stock);


            // return View("Detail",items);
        }

        //Get Item Details in BPR
      
        public JsonResult itemdetail(string dd, string dd1)
        {

            int company = 0;
            if (dd1 == "GFE Kathua")
            {
                company = 4;
            }

            else if (dd1 == "IEC Barwala")
            {
                company = 1;
            }
            else if (dd1 == "IEC Panchkula")
            {
                company = 14;
            }

            var items = (from c in DB.ItemMasterGFEs
                            where c.Itemname.Contains(dd) && c.Companyid == company
                         select c).ToList();
            return new JsonResult { Data = items, JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            
        }
        public JsonResult Getitemdetails(string dd)
        {

            var items = (from c in DB.ItemMasterGFEs
                         where c.Itemname==dd
                         select c).FirstOrDefault();
            return new JsonResult { Data = items, JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            //return Json(items, JsonRequestBehavior.AllowGet);
        }

        public ActionResult itemmasternew()
        {
            //int company = 2;
            itemcombine IC = new itemcombine();
            // itemmaster IMN = new itemmaster();

            var itemmasterdata = (from ab in DB.ItemMasterGFEs
                                      //where ab.Companyid == company
                                  join CD in DB.Companies on ab.Companyid equals CD.CompanyID
                                 // select ab).ToList();
                                  select new itemcombine

                                  { 
                                     Productcode=ab.Productcode,
                                      Itemname= ab.Itemname,
                                      itemdesc= ab.itemdesc,
                                      Itemcategory= ab.Itemcategory,
                                      norms = ab.norms,
                                      Phase= ab.Phase,
                                      company= CD.CompanyName,
                                  
                                  }).ToList();

           // return itemmasterdata.ToList();


           // return new JsonResult { Data = itemmasterdata, JsonRequestBehavior = JsonRequestBehavior.AllowGet };
           return PartialView("ItemGridView", itemmasterdata);
        }

        public JsonResult getproductdetail(string dd, string dd1)
        {
            int company = 0;
            if (dd1 == "GFE Kathua")
            {
                company = 4;
            }

            else if (dd1== "IEC Barwala")
            {
                company = 1;
            }
            else if (dd1 == "IEC Panchkula")
            {
                company = 14;
            }



            var items = (from c in DB.ItemMasterGFEs
                         where c.Productcode == dd && c.Companyid==company
                         select c).FirstOrDefault();
            return new JsonResult { Data = items, JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            //return Json(items, JsonRequestBehavior.AllowGet);
        }
        public ActionResult PartailItemmaster()
        {

            var items = (from ab in DB.ItemMasterGFEs
                        // join CD in DB.Companies on ab.Companyid equals CD.CompanyID
                         orderby ab.id descending
                         select ab).ToList();
        
            return PartialView("ItemGridView", items);
            
        }

        public ActionResult dropdownquality(string selectedValue)
        {

            List<string> data = new List<string>();
            if (selectedValue == "Passed")
            {

                data.Add("NA");
                //data.Add("3 Phase");

            }

            else if (selectedValue == "Not Passed")
            {

                data.Add("Short Supply");
                data.Add("Wrong Supply");
                data.Add("Physical Damage");
                data.Add("Line Rejection");

            }

            else
            {
                data.Add("NA");

            }

            return new JsonResult { Data = data, JsonRequestBehavior = JsonRequestBehavior.AllowGet };


        }

        //Get Dropdown menu according to category
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
        //Loginstic In Item Wise Code (List View)
        public ActionResult logisticin()
        {
           
            var getdataitemwise = (from ab in DB.PurchaseitemGFEs
                                   join bc in DB.PurchaseGFEs on ab.Billid equals bc.id
                                   join cd in DB.Companies on bc.company equals cd.CompanyID
                                   join dc in DB.ItemMasterGFEs on ab.itemid equals dc.id
                                   where ab.approved == 1
                                   select new Logisticin 
                                   {
                                       CompanyName=cd.CompanyName,
                                       Billno=bc.Billno,
                                       billdate=bc.billdate,
                                       Itemname=dc.Itemname,
                                       Productcode=dc.Productcode,
                                       logisticindate=ab.Logisticindate,
                                       plantindate=ab.Plantindate,
                                       qty=(int)ab.qty,
                                       id=(int)ab.id,
                                   
                                   }).ToList();
            return View("Logisticin", getdataitemwise);
        }

        //Plant In Item Wise Code (List View)
        public ActionResult Plantin()
        {

            var getdataitemwise = (from ab in DB.PurchaseitemGFEs
                                   join bc in DB.PurchaseGFEs on ab.Billid equals bc.id
                                   join cd in DB.Companies on bc.company equals cd.CompanyID
                                   join dc in DB.ItemMasterGFEs on ab.itemid equals dc.id
                                   where ab.approved == 2
                                   select new Logisticin
                                   {
                                       CompanyName = cd.CompanyName,
                                       Billno = bc.Billno,
                                       billdate = bc.billdate,
                                       Itemname = dc.Itemname,
                                       Productcode = dc.Productcode,
                                       qty = (int)ab.qty,
                                       id = (int)ab.id,

                                   }).ToList();
            return View("Plantin", getdataitemwise);
        }
        public ActionResult Qualityin()
        {

            var getdataitemwise = (from ab in DB.PurchaseitemGFEs
                                   join bc in DB.PurchaseGFEs on ab.Billid equals bc.id
                                   join cd in DB.Companies on bc.company equals cd.CompanyID
                                   join dc in DB.ItemMasterGFEs on ab.itemid equals dc.id
                                   where ab.approved == 3
                                   select new Logisticin
                                   {
                                       CompanyName = cd.CompanyName,
                                       Billno = bc.Billno,
                                       billdate = bc.billdate,
                                       Itemname = dc.Itemname,
                                       Productcode = dc.Productcode,
                                       qty = (int)ab.qty,
                                       id = (int)ab.id,

                                   }).ToList();
            //ModelState.Clear();
            return View("Qualityin", getdataitemwise);

        }

        public ActionResult ItemGrid()
        {

            var items = (from ab in DB.ItemMasterGFEs
                         orderby ab.id descending
                         select ab).ToList();


            return PartialView("ItemGridView", items);

        }
        public ActionResult Qualityindetail(int ids)

        {
            var getdataitemwise = (from ab in DB.PurchaseitemGFEs
                                   join bc in DB.PurchaseGFEs on ab.Billid equals bc.id
                                   join cd in DB.Companies on bc.company equals cd.CompanyID
                                   join dc in DB.ItemMasterGFEs on ab.itemid equals dc.id
                                   where ab.id==ids
                                   select new Logisticin
                                   {
                                       CompanyName = cd.CompanyName,
                                       Billno = bc.Billno,
                                       billdate = bc.billdate,
                                       Itemname = dc.Itemname,
                                       Productcode = dc.Productcode,
                                       qty = (int)ab.qty,
                                       id = (int)ab.id,

                                   }).SingleOrDefault();
            return PartialView("_Qualitytest", getdataitemwise);

        }

        [HttpPost]
        public JsonResult Getitem(int id)
        {
            var getdataitemwise = (from ab in DB.PurchaseitemGFEs
                                   join bc in DB.PurchaseGFEs on ab.Billid equals bc.id
                                   join cd in DB.Companies on bc.company equals cd.CompanyID
                                   join dc in DB.ItemMasterGFEs on ab.itemid equals dc.id
                                   where ab.id == id
                                   select new Logisticin
                                   {
                                       CompanyName = cd.CompanyName,
                                       Billno = bc.Billno,
                                       billdate = bc.billdate,
                                       Itemname = dc.Itemname,
                                       Itemdesc=dc.itemdesc,
                                       Productcode = dc.Productcode,
                                       logisticindate = ab.Logisticindate,
                                       plantindate = ab.Plantindate,
                                       qty = (int)ab.qty,
                                       id = (int)ab.id,

                                   }).SingleOrDefault();
            return Json(getdataitemwise);

        }
        public ActionResult approveplantin(int ids)
        {
            //Select Purchase Item Line and Update value of Approval
            var selectline = (from ab in DB.PurchaseitemGFEs
                              where ab.id == ids
                              select ab).SingleOrDefault();
            

            //Select Item and update Stock in Plant in 

            var updatestock = (from bb in DB.ItemMasterGFEs
                               where bb.id == selectline.itemid
                               select bb).SingleOrDefault();

            string oldplantin = (updatestock.phyplant).ToString();
            string newplantin = (selectline.qty).ToString();
            string logisticinnow = (updatestock.phylogistic).ToString();

            updatestock.phyplant = Convert.ToInt32(oldplantin) + Convert.ToInt32(newplantin);
            updatestock.phylogistic = Convert.ToInt32(logisticinnow) - Convert.ToInt32(newplantin);

            selectline.approved = 3;
            selectline.Plantindate = DateTime.Now.ToString("dd/MM/yyyy");
            DB.SaveChanges();
            TempData["message"] = "Selected Item Stock has been updated";
            //ViewBag.msg = "1";
            ModelState.Clear();
            return RedirectToAction("Plantin");

        }

        public ActionResult approvelogisticin(int ids)
        {
            //Select Purchase Item Line and Update value of Approval
            var selectline = (from ab in DB.PurchaseitemGFEs
                             where ab.id == ids
                             select ab).SingleOrDefault();
            

            //Select Item and update Stock in Logistic in 

            var updatestock = (from bb in DB.ItemMasterGFEs
                               where bb.id == selectline.itemid
                               select bb).SingleOrDefault();
           
          

                string oldlogisticin = (updatestock.phylogistic).ToString();
                string newlogisticin = (selectline.qty).ToString();                
                string transitnow = (updatestock.transit).ToString();

                //updatestock.transit = Convert.ToInt32(transitnow-newlogisticin);
                updatestock.phylogistic = Convert.ToInt32(oldlogisticin) + Convert.ToInt32(newlogisticin);
                updatestock.transit = Convert.ToInt32(transitnow) - Convert.ToInt32(newlogisticin);

            selectline.approved = 2;
            selectline.Logisticindate = DateTime.Now.ToString("dd/MM/yyyy");


            DB.SaveChanges();
            TempData["message"] = "Selected Item Stock has been updated";
            //ViewBag.msg = "1";
            ModelState.Clear();
            return RedirectToAction("Logisticin");

        }
        public ActionResult approvepurchase(int ids)
        {

            try
            {
                var result = (from s in DB.ItemMasterGFEs//DB.PurchaseitemGFEs
                              join p in DB.PurchaseitemGFEs on s.id equals p.itemid
                              where p.Billid == ids
                              select new { p.qty,s.transit, s.Itemname,  s.id, s.presentstock,  }).ToList();
                foreach (var st in result)
                {
                    int stock = Convert.ToInt32(st.qty + st.presentstock);
                    int transit = Convert.ToInt32(st.transit - st.qty);

                    var item = DB.ItemMasterGFEs.Single(i => i.id == st.id);
                    item.presentstock = stock;
                    item.transit = transit;
                    DB.SaveChanges();
                }
                DB.SaveChanges();
            }

            catch (Exception ex)
            {
                var hh = ex;
            }



        //BPR to Purchase Status update

            var update = (from ab in DB.PurchaseGFEs
                         where ab.id == ids
                         select ab).SingleOrDefault();
            update.purchaseapproval = 2;
            DB.SaveChanges();

            //Status update in Purcahse items
            int billid = update.id;
            var getbillinfoinitems = (from ID in DB.PurchaseitemGFEs
                                     where ID.Billid == ids
                                     select ID).ToList();
            foreach (var UP in getbillinfoinitems)
            {
                UP.approved = Convert.ToInt32("2");
                //UP.itemid
            };
            DB.SaveChanges();          




            var pending = from ab in DB.PurchaseGFEs
                          where ab.purchaseapproval == 1
                          select ab;
            ViewBag.msg = "Purchase Approved and Stock Updated";
            ModelState.Clear();
            // ModelState.Remove(ids);
            return RedirectToAction("BPRPendingCases");
           

        }

        public ActionResult GoodsReciept()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "GoodsReciept" && ab.Status == true
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
        public ActionResult GetPurchaseLines(int selectedPO)
        {
            var head = (from h in DB.PurchaseOrderHeads
                        join su in DB.SupplierMasters on h.SupplierID equals su.id into sugroup
                        from su in sugroup.DefaultIfEmpty()
                        where h.id == selectedPO
                        select new
                        {
                            Supplier=su.SupplierName,
                            PO=h.PONumber,
                            PODate=h.PODate,
                            Indent=h.IndentNumber,
                            IndentDate=h.IndentDate,
                        }).SingleOrDefault();
            var lines = (from ab in DB.PurchaseOrderItems
                         join im in DB.BOMItemMasters on ab.ItemID equals im.Itemid into imgroup
                         from im in imgroup.DefaultIfEmpty()
                         join uom in DB.BOM_UOM on im.UOM equals uom.id into uomgroup
                         from uom in uomgroup.DefaultIfEmpty()
                         where ab.HeadID == selectedPO && ab.IsDeleted == 0
                         select new
                         {
                             ItemCode = im.ItemCode,
                             ItemName = im.ItemName,
                             UOM = uom.UOM,
                             Quantity = ab.Quantity,
                             NetBasic = ab.NetBasic,
                             TotalBasic=ab.TaxableAmount,
                             TotalTax=ab.TaxAmount,
                             Taxid=ab.ItemTaxID,
                             GrossAmount=ab.GrossAmount,
                         }).ToList();
            return Json(new { success = true, lines, head }, JsonRequestBehavior.AllowGet);
        }


        [HttpPost]             
        public JsonResult SaveNew(string data)
        {

            var status = "";
           
            
                ajaxModel model = JsonConvert.DeserializeObject<ajaxModel>(data);
            if (!string.IsNullOrEmpty(model.bill))
            {

                int company = 0;

                if (model.company == "GFE Kathua")
                {
                    company = 4;
                }
                else if (model.company == "IEC Barwala")
                {
                    company = 1;
                }
                else if (model.company == "IEC Panchkula")
                {
                    company = 14;
                }

                PurchaseGFE PG = new PurchaseGFE();

                PG.Billno = model.bill;
                PG.billdate = model.date;
                PG.flag = 1;
                PG.company = company;
                PG.purchaseapproval = 1;
                PG.createdon = DateTime.Today.ToString("dd/MM/yyyy");
                //Date
                //User
                int purchaseqty = 0;

                //Count Qty of purchase

                foreach (var ab in model.data)
                {
                    purchaseqty = purchaseqty + ab.qty;
                }
                PG.Billqty = purchaseqty;
               

                DB.PurchaseGFEs.Add(PG);
                DB.SaveChanges();

                //get id of last record
                var getdata = (from ab in DB.PurchaseGFEs
                              where ab.Billno == model.bill && ab.billdate == model.date
                              select ab.id).FirstOrDefault();

                //Save items according to Purchase id

                PurchaseitemGFE PI = new PurchaseitemGFE();

                int billid =Convert.ToInt32(getdata);
                 model.data.ForEach(m =>
                {
                    DB.PurchaseitemGFEs.Add(new PurchaseitemGFE
                    {
                        itemid = m.itemid,
                        Billid = billid,
                        norms = m.norms,
                        qty = m.qty,
                        basicprice=m.basicvalue,
                        flag = 1,
                        approved = 1,



                     }) ; 

                    DB.SaveChanges();                                     

                });

               

                // add stock in item master
              
                foreach(var abb in model.data)
                {
                    var nn = (from st in DB.ItemMasterGFEs
                              where st.id == abb.itemid
                              select st).FirstOrDefault();
                    int oldtransit = Convert.ToInt32(nn.transit);
                    int newtransit = Convert.ToInt32(abb.qty);
                    int total = oldtransit + newtransit;
                    
                        //Transit Total is in Total
                    nn.transit = total;

                    //int oldtotal = Convert.ToInt32(nn.total);
                    int stocknow = Convert.ToInt32(nn.presentstock);
                    int finaltotal =  stocknow + newtransit;
                   // nn.total = finaltotal; Stoped

                    // Balance Update STK Report

                   // int allocation = Convert.ToInt32(nn.allocated); Stopped
                   // nn.balancestock = finaltotal; Stopped
                };
                DB.SaveChanges();
            }
            
           else
            {
                    return new JsonResult { Data = new { status, message = "Enter Item Code or Select Item" } };
            }
            return new JsonResult { Data = new { status, message = "Entry saved successfully" } };

        }
        [HttpPost]
        public ActionResult updatequality( int fid, string fserno, string fquality, string frejection,string fremarks, HttpPostedFileBase qualityupload)
        {
          
            //Save Quality Document in Specific Folder
            string folder = "~/Qualitydocuments/";
            string physicalfolder = Server.MapPath(folder);
            string filename1 = System.IO.Path.GetFileNameWithoutExtension(qualityupload.FileName);
            string ext = System.IO.Path.GetExtension(qualityupload.FileName);
            string filename = filename1 + "-Quality-"+fid.ToString()+"-"+ fserno.ToString() + ext;
            qualityupload.SaveAs(System.IO.Path.Combine(physicalfolder, filename));
           // int status = 1;

            // Get Quality Status ID

            var quality = (from jj in DB.PurchaseStatusQualities
                          where jj.purchasestatus == fquality
                          select jj.id).SingleOrDefault();
            //Get Quality Rejection ID
            var reason = (from ii in DB.PurchaseQualityRejectionreasons
                          where ii.Rejectionreason == frejection
                          select ii.id).SingleOrDefault();
            if (quality == 1)
            {
                              

               //Select Updated Line from Purchase item

                var updatequalitydata = (from ab in DB.PurchaseitemGFEs
                                         where ab.id == fid
                                         select ab).SingleOrDefault();

                //Save values in Purchase Item Line

                updatequalitydata.productserialno = fserno;
                updatequalitydata.Quaitystatus = quality;
                updatequalitydata.Qualityreason = reason;
                updatequalitydata.QualityDocument = folder + filename;
                updatequalitydata.Qualitydate = DateTime.Today.ToString("dd/MM/yyyy");
                updatequalitydata.QualityRemarks = fremarks;
                updatequalitydata.approved = 4;

                //Select Item master line 
                var itemline = (from kk in DB.ItemMasterGFEs
                                where kk.id == updatequalitydata.itemid
                                select kk).SingleOrDefault();
                string oldreadyforallocation = itemline.readyforallocation.ToString();
                string newreadyallocation = updatequalitydata.qty.ToString();
                string phyqty = itemline.phyplant.ToString();

                itemline.readyforallocation = Convert.ToInt32(oldreadyforallocation) + Convert.ToInt32(newreadyallocation);
                itemline.phyplant = Convert.ToInt32(phyqty) - Convert.ToInt32(newreadyallocation);

                DB.SaveChanges();
                ModelState.Clear();
            }
            else if (quality == 2)
            {
                //Select Updated Line from Purchase item

                var updatequalitydata = (from ab in DB.PurchaseitemGFEs
                                         where ab.id == fid
                                         select ab).SingleOrDefault();

                //Save values in Purchase Item Line

                updatequalitydata.productserialno = fserno;
                updatequalitydata.Quaitystatus = quality;
                updatequalitydata.Qualityreason = reason;
                updatequalitydata.QualityDocument = folder + filename;
                updatequalitydata.Qualitydate = DateTime.Today.ToString("dd/MM/yyyy");
                updatequalitydata.QualityRemarks = fremarks;
                updatequalitydata.approved = 11;

                //Select Item master line 
                var itemline = (from kk in DB.ItemMasterGFEs
                                where kk.id == updatequalitydata.itemid
                                select kk).SingleOrDefault();
                string oldrejectedstock = itemline.Rejectedstk.ToString();               
                string newreadyallocation = updatequalitydata.qty.ToString();
                string phyqty = itemline.phyplant.ToString();

                itemline.Rejectedstk = Convert.ToInt32(oldrejectedstock) + Convert.ToInt32(newreadyallocation);
                itemline.phyplant = Convert.ToInt32(phyqty) - Convert.ToInt32(newreadyallocation);

                DB.SaveChanges();
                ModelState.Clear();

            }

            TempData["message"] = "Selected Quality Details has been updated";

            //return RedirectToAction("Qualityin");
            return new JsonResult { Data = new { success = true, message = "Quality Status Updated" } };




            //return new JsonResult { Data = new { success = true, message = "Entry saved successfully" } };
        }




        [HttpPost]
        public ActionResult AddProduct(string Itemname, string itemdesc, string Itemcategory, string unit, string phase, string company, string Productcode, string norms)
        {
            if (string.IsNullOrEmpty(Itemname))
            {
                return Json(data: new { success = false, message = "There is Something Wrong" }, JsonRequestBehavior.AllowGet);
            }
            else
            // if (ModelState.IsValid)
            {
                var phaseup = phase;
                int companyid = 0;
                if (company == "GFE-Kathua")
                {
                    companyid = 4;
                }
                else if (company == "IEC-Barwala")
                {
                    companyid = 1;
                }
                if (string.IsNullOrEmpty(phase))
                {
                    phaseup = "NA";
                }

                ItemMasterGFE IM = new ItemMasterGFE();
                IM.Itemname = Itemname;
                IM.itemdesc = itemdesc;
                IM.Itemcategory = Itemcategory;
                IM.norms = norms;
                IM.unitofmeasurement = unit;
                IM.Phase = phaseup;
                IM.Companyid = companyid;
                IM.Productcode = Productcode;
                IM.phyplant = 0;
                IM.presentstock = 0;
                IM.transit = 0;
                IM.phylogistic = 0;
                IM.readyforallocation = 0;
                IM.wip = 0;
                IM.testing = 0;
                DB.ItemMasterGFEs.Add(IM);
                DB.SaveChanges();
                return Json(data: new { success = true, message = "Item Has Been Added" }, JsonRequestBehavior.AllowGet);
            }

            //var getitems = (from ab in DB.ItemMasterGFEs
            //   select ab).ToList();

            //return View(getitems);             


        }
        public class ChallanPrintVM
        {
            public IEPLStockIssueHead Header { get; set; }
            public Company Company { get; set; } // Replace 'Company' with your actual Entity class name
            public List<dynamic> Lines { get; set; }
        }
        [HttpGet]
        public JsonResult GetPendingChallans()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"]);

            // A Challan remains in the list if it has at least one Item ID 
            // that has NOT yet appeared in any return record for this specific Challan.
            var data = (from h in DB.IEPLStockIssueHeads
                        where h.CompanyID == companyid
                        && h.IsChallan == true
                        && h.IsReturnable == true
                        // Logic: Keep the Challan if there is ANY issue line (d) 
                        // that DOES NOT have a matching return record for that Item ID.
                        && DB.IEPLStockIssueDetails.Any(d => d.Voucherid == h.id &&
                            !(from rd in DB.IEPLStockReturnDetails
                              join rh in DB.IEPLStockReturnHeads on rd.Voucherid equals rh.id
                              where rh.RefChallanId == h.id && rd.Itemcodeid == d.Itemcodeid
                              select rd).Any())

                        select new
                        {
                            id = h.id,
                            VN = h.VoucherNumber,
                            Cust = h.CustomerName
                        }).ToList();

            return Json(data, JsonRequestBehavior.AllowGet);
        }
        [HttpGet]
        public JsonResult GetReturnItemDetails(string itemCode, int? challanId)
        {
            var item = DB.BOMItemMasters.FirstOrDefault(x => x.ItemCode == itemCode);
            if (item == null) return Json(new { success = false, error = "Item not found in Master." }, JsonRequestBehavior.AllowGet);

            bool isConversion = false;
            decimal originalQty = 0;

            if (challanId.HasValue)
            {
                // Check if the scanned item exists in the original Challan
                var originalDetail = DB.IEPLStockIssueDetails.FirstOrDefault(x => x.Voucherid == challanId && x.Itemcodeid == item.Itemid);

                if (originalDetail == null)
                {
                    // Scanned item is different from issued item = CONVERSION
                    isConversion = true;
                }
                else
                {
                    originalQty = (decimal)originalDetail.ApprovedQuantity;
                }
            }

            return Json(new
            {
                success = true,
                itemname = item.ItemName,
                uomText = DB.BOM_UOM.Where(x => x.id == item.UOM).Select(x => x.UOM).FirstOrDefault() ?? "NA",
                stock = item.Stock,
                qty = originalQty,
                isConversion = isConversion
            }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult FGStockMovement()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "FGStockMovement" && ab.Status == true
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
        public JsonResult GetFGStockLedger(string fromDate, string toDate)
        {
            try
            {
                int compId = Convert.ToInt32(Session["Company_ID"]);
                DateTime sDate = DateTime.Parse(fromDate).Date;
                DateTime eDate = DateTime.Parse(toDate).Date.AddDays(1).AddTicks(-1);

                // 1. Fetch ALL relevant BOMs for this company (Matches your SQL 332 count)
                var reportData = (from bom in DB.BOMVouchers
                                  join item in DB.BOMItemMasters on bom.FGProductID equals item.Itemid into itemJoin
                                  from i in itemJoin.DefaultIfEmpty()
                                  join ewap in DB.BOMEwapDetails on bom.BOMVoucherID equals ewap.BomVoucherHeadID into ewapJoin
                                  from e in ewapJoin.DefaultIfEmpty()
                                  join model in DB.EngineModels on e.Model equals model.EngineId into modelJoin
                                  from m in modelJoin.DefaultIfEmpty()
                                  where bom.CompanyID == compId
                                  && (bom.Isdeleted == 0 || bom.Isdeleted == null)
                                  && bom.Approvalstatus == 1
                                  select new
                                  {
                                      // Stage 1: Approval (WIP Start)
                                      AppDate = bom.VoucherDate,
                                      // Stage 2: Production Completion
                                      ProdStatus = bom.ProductionCompletionStatus ?? 0,
                                      ProdDate = bom.ProductionCompletionon,
                                      // Stage 3: EWAP/Testing
                                      EwapStatus = (e != null) ? 1 : 0,
                                      EwapDate = e != null ? (DateTime?)e.Createdon : null,
                                      // Outward: Dispatch
                                      DispStatus = (e != null && e.DispatchStatus == 1) ? 1 : 0,
                                      DispDate = e != null ? e.Dispatchon : null,

                                      ItemName = i != null ? i.ItemName : "Unknown Item",
                                      ModelName = m != null ? m.EngineModel1 : "Model Pending"
                                  }).ToList();

                // 2. The Calculation Logic (The "Missing" Piece)
                var ledger = reportData.GroupBy(x => new { x.ItemName, x.ModelName })
                    .Select(g => new
                    {
                        Description = g.Key.ItemName + " [" + g.Key.ModelName + "]",

                        // --- STAGE 1: ASSEMBLY WIP ---
                        // Opening: Approved before sDate AND (Not yet Prod-Completed OR Completed after sDate)
                        W_Op = g.Count(x => x.AppDate < sDate && (x.ProdStatus == 0 || x.ProdDate >= sDate)),
                        W_In = g.Count(x => x.AppDate >= sDate && x.AppDate <= eDate),
                        W_Ou = g.Count(x => x.ProdStatus == 1 && x.ProdDate >= sDate && x.ProdDate <= eDate),

                        // --- STAGE 2: PRODUCTION COMPLETED ---
                        // Opening: Prod-Completed before sDate AND (Not yet EWAPed OR EWAPed after sDate)
                        P_Op = g.Count(x => x.ProdStatus == 1 && x.ProdDate < sDate && (x.EwapStatus == 0 || x.EwapDate >= sDate)),
                        P_In = g.Count(x => x.ProdStatus == 1 && x.ProdDate >= sDate && x.ProdDate <= eDate), // Transfer from Stage 1
                        P_Ou = g.Count(x => x.EwapStatus == 1 && x.EwapDate >= sDate && x.EwapDate <= eDate),

                        // --- STAGE 3: TESTED FG ---
                        // Opening: EWAPed before sDate AND (Not yet Dispatched OR Dispatched after sDate)
                        E_Op = g.Count(x => x.EwapStatus == 1 && x.EwapDate < sDate && (x.DispStatus == 0 || x.DispDate >= sDate)),
                        E_In = g.Count(x => x.EwapStatus == 1 && x.EwapDate >= sDate && x.EwapDate <= eDate), // Transfer from Stage 2
                        E_Ou = g.Count(x => x.DispStatus == 1 && x.DispDate >= sDate && x.DispDate <= eDate)
                    })
                    .Select(x => new
                    {
                        x.Description,
                        x.W_Op,
                        x.W_In,
                        x.W_Ou,
                        W_Cl = (x.W_Op + x.W_In - x.W_Ou),
                        x.P_Op,
                        x.P_In,
                        x.P_Ou,
                        P_Cl = (x.P_Op + x.P_In - x.P_Ou),
                        x.E_Op,
                        x.E_In,
                        x.E_Ou,
                        E_Cl = (x.E_Op + x.E_In - x.E_Ou)
                    })
                    .Where(x => (x.W_Op + x.W_In + x.P_Op + x.P_In + x.E_Op + x.E_In) > 0) // Only show rows with movement
                    .OrderBy(x => x.Description).ToList();

                return Json(new { success = true, data = ledger, totalRecordsFound = reportData.Count }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpGet]
        public JsonResult GetFGStockLedger1(string fromDate, string toDate)
        {
            try
            {
                int compId = Convert.ToInt32(Session["Company_ID"]);
                DateTime sDate = DateTime.Parse(fromDate).Date;
                DateTime eDate = DateTime.Parse(toDate).Date.AddDays(1).AddTicks(-1);

                var reportData = (from bom in DB.BOMVouchers
                                  join ewap in DB.BOMEwapDetails on bom.BOMVoucherID equals ewap.BomVoucherHeadID into ewapJoin
                                  from e in ewapJoin.DefaultIfEmpty()
                                  join item in DB.BOMItemMasters on bom.FGProductID equals item.Itemid into itemJoin
                                  from i in itemJoin.DefaultIfEmpty()
                                  join model in DB.EngineModels on e.Model equals model.EngineId into modelJoin
                                  from m in modelJoin.DefaultIfEmpty()
                                  where bom.CompanyID == compId && bom.Isdeleted == 0
                                  select new
                                  {
                                      ProdStatus = bom.ProductionCompletionStatus ?? 0,
                                      ProdDate = bom.ProductionCompletionon,
                                      EwapStatus = (e != null) ? 1 : 0,
                                      EwapDate = e != null ? (DateTime?)e.Createdon : null,
                                      DispStatus = (e != null && e.DispatchStatus == 1) ? 1 : 0,
                                      DispDate = e != null ? e.Dispatchon : null,
                                      ItemName = i != null ? i.ItemName : "Unknown Item",
                                      ModelName = m != null ? m.EngineModel1 : "Model Pending"
                                  }).ToList();

                var ledger = reportData.GroupBy(x => new { x.ItemName, x.ModelName })
                    .Select(g => new
                    {
                        Description = g.Key.ItemName + " [" + g.Key.ModelName + "]",
                        P_Op = g.Count(x => x.ProdStatus == 1 && x.ProdDate < sDate && (x.EwapStatus == 0 || x.EwapDate >= sDate)),
                        P_In = g.Count(x => x.ProdStatus == 1 && x.ProdDate >= sDate && x.ProdDate <= eDate),
                        P_Out = g.Count(x => x.EwapStatus == 1 && x.EwapDate >= sDate && x.EwapDate <= eDate),
                        E_Op = g.Count(x => x.EwapStatus == 1 && x.EwapDate < sDate && (x.DispStatus == 0 || x.DispDate >= sDate)),
                        E_In = g.Count(x => x.EwapStatus == 1 && x.EwapDate >= sDate && x.EwapDate <= eDate),
                        E_Out = g.Count(x => x.DispStatus == 1 && x.DispDate >= sDate && x.DispDate <= eDate)
                    })
                    .Select(x => new
                    {
                        x.Description,
                        x.P_Op,
                        x.P_In,
                        x.P_Out,
                        P_Cl = (x.P_Op + x.P_In - x.P_Out),
                        x.E_Op,
                        x.E_In,
                        x.E_Out,
                        E_Cl = (x.E_Op + x.E_In - x.E_Out)
                    }).OrderBy(x => x.Description).ToList();

                return Json(new { success = true, data = ledger }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult PrintChallan(int? ID)
        {
            ViewBag.VoucherID = ID;
            return View();
        }
        [HttpGet]
        public JsonResult GetChallanPrintData(int id)
        {
            try
            {
                var headData = DB.IEPLStockIssueHeads
                    .Where(x => x.id == id)
                    .Select(h => new {
                        h.VoucherNumber,
                        h.VoucherDate,
                        h.CustomerName,
                        h.Address,
                        h.BillingAddress,
                        h.ContactNo,
                        h.VehicleNo,
                        h.Remarks,
                        h.IsReturnable,
                        h.CompanyID,
                        h.TransportName,
                        h.IsBilty,
                        h.BiltyNo,
                        h.BiltyDate,
                        h.BiltyTotalValue,
                        h.BiltyDescription
                    }).FirstOrDefault();

                if (headData == null) return Json(new { success = false, message = "Head not found" }, JsonRequestBehavior.AllowGet);

                var companyData = DB.Companies
                    .Where(x => x.CompanyID == headData.CompanyID)
                    .Select(c => new { c.CompanyName, c.CompanyAddress,c.DeliveryDestination, c.GST, c.PAN, c.Contact }).FirstOrDefault();

                // Join with Item Master to get HSN Code
                var lines = (from d in DB.IEPLStockIssueDetails
                             join i in DB.BOMItemMasters on d.Itemcodeid equals i.Itemid
                             join u in DB.BOM_UOM on i.UOM equals u.id
                             where d.Voucherid == id
                             select new
                             {
                                 i.ItemCode,
                                 i.ItemName,
                                 HSN = i.HSNCode ?? "---", // Ensure HSN is in your ItemMaster
                                 d.Quantity,
                                 UOM = u.UOM,
                                 d.Remarks
                             }).ToList();

                return Json(new
                {
                    success = true,
                    head = headData,
                    company = companyData,
                    lines = lines
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet); }
        }
        public ActionResult Stockissue()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "Stockissue" && ab.Status == true
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
        public JsonResult GetDepartment()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var depa = (from ab in DB.DepartmentMasters
                     where ab.CompanyID==companyid
                    select new
                    { 
                    text=ab.DepartmentName,
                    value=ab.id
                    }).ToList();
            return Json(depa, JsonRequestBehavior.AllowGet);
        }
        public JsonResult GetIssueItemDetails(string itemCode)
        {
            var role = Session["U_Role"].ToString();
            var user = Session["U_Name"].ToString();
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());

            var itemdetail = (from ab in DB.BOMItemMasters
                              join uom in DB.BOM_UOM on ab.UOM equals uom.id
                              join bb in DB.StockTables on ab.Itemid equals bb.itemid
                              join cc in DB.ItemMasterClasses on ab.ItemClass equals cc.id into ccGroup
                              from cc in ccGroup.DefaultIfEmpty()
                              join gg in DB.ItemGroups on ab.ItemGroup equals gg.id into gggroup
                              from gg in gggroup.DefaultIfEmpty()
                              join ll in DB.StoreLocationMasters on ab.ItemLocation equals ll.id into llgrounp
                              from ll in llgrounp.DefaultIfEmpty()
                              where ab.ItemCode == itemCode && bb.CompanyID == companyid // Filter by companyid in the join
                              select new { ab, bb, cc, gg, ll,uom }).ToList(); // Project both ab and bb

            if (itemdetail.Count == 0)
            {
                return Json(new { success = false, error = "Item code not found or no stock for this company" }, JsonRequestBehavior.AllowGet);
            }
            else if (itemdetail.Count > 1)
            {
                return Json(new { success = false, error = "Item code is assigned to multiple items" }, JsonRequestBehavior.AllowGet);
            }
            else
            {
                var item = itemdetail.FirstOrDefault();
                var id = item.ab.Itemid;
                var basicprice = item.ab.BasicPrice;
                var itemname = item.ab.ItemName;
                var Desc = item.ab.Desc;
                var MinStkLvl = item.ab.Minstklvl;
                var MaxStkLvl = item.ab.Maxstklvl;
                var uomvalue = item.ab.UOM;
                var uomText = item.uom.UOM;//DB.BOM_UOM.FirstOrDefault(u => u.id == uomvalue).UOM;
                var moq = item.ab.MinimumOrderQuantity;
                var stock = item.bb.Stock;
                var HSN = item.ab.HSNCode;// Get stock directly from the joined result
                var itemclass = item.cc?.id ?? 0;
                var itemgroup = item.gg?.id ?? 0;
                var itemlocation = item.ll?.id ?? 0;

                return Json(new { success = true, itemname, uomText, stock, Desc, MinStkLvl, MaxStkLvl, moq, id, HSN, itemclass, itemgroup, basicprice, role, user, itemlocation }, JsonRequestBehavior.AllowGet);
            }
        }

        public ActionResult ChallanIssueReportView()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "ChallanIssueReportView" && ab.Status == true
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
        public ContentResult ChallanIssueReport()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var FinYear = Session["Fin_Year"].ToString();

            // 1. Fetch Issues with Item Names and Bilty Status
            var issues = (from ab in DB.IEPLStockIssueDetails
                          join item in DB.BOMItemMasters on ab.Itemcodeid equals item.Itemid
                          join bill in DB.IEPLStockIssueHeads on ab.Voucherid equals bill.id
                          where bill.CompanyID == companyid && bill.Fin_Year == FinYear && bill.IsChallan == true
                          select new
                          {
                              IssueId = bill.id,
                              VN = bill.VoucherNumber,
                              VD = bill.VoucherDate,
                              Cust = bill.CustomerName,
                              IsRet = bill.IsReturnable ?? false,
                              IsBilty = bill.IsBilty ?? false,
                              ItemId = item.Itemid,
                              ItemCode = item.ItemCode,
                              ItemName = item.ItemName, // Included for display
                              Qty = ab.Quantity
                          }).ToList();

            var issueIds = issues.Select(x => x.IssueId).Distinct().ToList();

            // 2. Fetch Returns with Item Names
            var returns = (from rd in DB.IEPLStockReturnDetails
                           join rh in DB.IEPLStockReturnHeads on rd.Voucherid equals rh.id
                           join item in DB.BOMItemMasters on rd.Itemcodeid equals item.Itemid
                           where rh.RefChallanId != null && issueIds.Contains((int)rh.RefChallanId)
                           select new
                           {
                               IssueId = (int)rh.RefChallanId,
                               RetVN = rh.VoucherNumber,
                               RetItemId = item.Itemid,
                               RetItemCode = item.ItemCode,
                               RetItemName = item.ItemName, // Included for display
                               RetQty = rd.Quantity
                           }).ToList();

            // 3. Grouping Logic
            var finalReport = issues.GroupBy(x => new { x.IssueId, x.VN, x.VD, x.Cust, x.IsRet, x.IsBilty })
                .Select(g => {
                    var issueItems = g.Select(i => i.ItemId).Distinct().ToList();
                    var returnedItems = returns.Where(r => r.IssueId == g.Key.IssueId).Select(r => r.RetItemId).Distinct().ToList();

                    string status = "PARTIAL";
                    if (!g.Key.IsRet) status = "COMPLETED";
                    else if (issueItems.All(id => returnedItems.Contains(id))) status = "COMPLETED";
                    else if (returnedItems.Count == 0) status = "PENDING";
                    
                    return new
                    {
                        IssueId = g.Key.IssueId,
                        VN = g.Key.VN,
                        Date = g.Key.VD.HasValue ? g.Key.VD.Value.ToString("dd-MMM-yyyy") : "",
                        Cust = g.Key.Cust,
                        IsRet = g.Key.IsRet,
                        IsBilty = g.Key.IsBilty,
                        LifecycleStatus = status,
                        IssuedItems = g.Select(i => new { i.ItemCode, i.ItemName, i.Qty, i.ItemId }).ToList(),
                        PhysicalReturns = returns.Where(r => r.IssueId == g.Key.IssueId)
                                                 .Select(r => new {
                                                     r.RetVN,
                                                     r.RetItemCode,
                                                     r.RetItemName,
                                                     r.RetQty,
                                                     IsNewItem = !g.Any(issueItem => issueItem.ItemId == r.RetItemId)
                                                 }).ToList()
                    };
                }).OrderByDescending(x => x.VN).ToList();

            return Content(JsonConvert.SerializeObject(finalReport), "application/json");
        }
        [HttpPost]
        public JsonResult issueStock(IssueModel model)
        {
            if (ModelState.IsValid && model.HeadData != null && model.TableData != null && model.TableData.Count > 0)
            {
                using (var transaction = DB.Database.BeginTransaction())
                {
                    try
                    {
                        string billseries = null;
                        int billseriesid = 0;
                        bool isreturnable = false;

                        // 1. Get Session Information
                        int companyid = Convert.ToInt32(Session["Company_ID"]);
                        string finYear = Session["Fin_Year"].ToString();
                        string userName = Session["U_Name"].ToString();

                        // Get Bill Series According to Type
                        if (model.HeadData.IsChallan == true)
                        {
                            if (model.HeadData.ChallanNature == "Returnable")
                            {
                                var seriesR = (from ab in DB.Bill_Series
                                               where ab.CompanyID == companyid && ab.Type == "ReturnableChallan" && ab.Fin_Year == finYear
                                               select ab).SingleOrDefault();
                                billseriesid = seriesR.id;
                                billseries = seriesR.Series + seriesR.Number;
                                isreturnable = true;
                            }
                            else if (model.HeadData.ChallanNature == "Non-Returnable")
                            {
                                var seriesNR = (from ab in DB.Bill_Series
                                                where ab.CompanyID == companyid && ab.Type == "NonReturnableChallan" && ab.Fin_Year == finYear
                                                select ab).SingleOrDefault();
                                billseriesid = seriesNR.id;
                                billseries = seriesNR.Series + seriesNR.Number;
                                isreturnable = false;
                            }
                            else if (model.HeadData.ChallanNature == "Job Work")
                            {
                                var seriesNR = (from ab in DB.Bill_Series
                                                where ab.CompanyID == companyid && ab.Type == "JobWorkChallan" && ab.Fin_Year == finYear
                                                select ab).SingleOrDefault();
                                billseriesid = seriesNR.id;
                                billseries = seriesNR.Series + seriesNR.Number;
                                isreturnable = true;
                            }
                        }
                        else
                        {
                            var seriesIS = DB.Bill_Series.FirstOrDefault(ab =>
                                ab.Type == "StockIssue" &&
                                ab.CompanyID == companyid &&
                                ab.Fin_Year == finYear);
                            billseriesid = seriesIS.id;
                            billseries = seriesIS.Series + seriesIS.Number;
                        }

                        if (billseries == null)
                        {
                            return Json(new { success = false, message = "Bill series not defined for the current financial year." });
                        }

                        // 3. Save the Head Data (AMENDED WITH BILTY & ADDRESS FIELDS)
                        IEPLStockIssueHead billHead = new IEPLStockIssueHead
                        {
                            VoucherNumber = billseries,
                            VoucherDate = DateTime.Now,
                            Departmentid = model.HeadData.Departmentid,
                            Createdby = userName,
                            Createdon = DateTime.Now,
                            CompanyID = companyid,
                            Fin_Year = finYear,

                            // BASIC CHALLAN FIELDS
                            IsChallan = model.HeadData.IsChallan,
                            IsReturnable = isreturnable,//model.HeadData.IsReturnable,
                            CustomerName = model.HeadData.CustomerName,
                            ContactNo = model.HeadData.ContactNo,
                            VehicleNo = model.HeadData.VehicleNo,
                            Remarks = model.HeadData.Remarks,

                            // NEW AMENDMENTS: ADDRESSES & TRANSPORT
                            BillingAddress = model.HeadData.BillingAddress, // New Field
                            Address = model.HeadData.Address,               // This is Shipping Address
                            TransportName = model.HeadData.TransportName,   // New Field

                            // NEW AMENDMENTS: BILTY (LR) INTELLIGENCE
                            IsBilty = model.HeadData.IsBilty,               // New Toggle
                            BiltyNo = model.HeadData.BiltyNo,
                            BiltyDate = model.HeadData.BiltyDate,           // Ensure this is passed as DateTime?
                            BiltyTotalValue = model.HeadData.BiltyTotalValue,
                            BiltyDescription = model.HeadData.BiltyDescription
                        };

                        DB.IEPLStockIssueHeads.Add(billHead);
                        DB.SaveChanges();

                        int headId = billHead.id;

                        // 4. Process Line Items and Update Stock
                        foreach (var detail in model.TableData)
                        {
                            if (string.IsNullOrEmpty(detail.ItemCode)) continue;

                            var itemMaster = DB.BOMItemMasters.FirstOrDefault(im => im.ItemCode == detail.ItemCode);
                            if (itemMaster == null) continue;

                            decimal issueQty = decimal.Parse(detail.Quantity);

                            IEPLStockIssueDetail billDetail = new IEPLStockIssueDetail
                            {
                                Voucherid = headId,
                                Itemcodeid = itemMaster.Itemid,
                                Quantity = issueQty,
                                Remarks = detail.Remarks,
                                ApprovedQuantity = issueQty,
                                ApprovedStatus = 1,
                                ApprovedBy = userName,
                                ApprovedDate = DateTime.Now,
                                Voucher = "AutoApproved",
                                RejectedQuantity = 0
                            };
                            DB.IEPLStockIssueDetails.Add(billDetail);

                            // Update Stock Logic
                            itemMaster.Stock -= issueQty;
                            var stockTableRecord = DB.StockTables.FirstOrDefault(s =>
                                s.itemid == itemMaster.Itemid &&
                                s.CompanyID == companyid);

                            if (stockTableRecord != null)
                            {
                                stockTableRecord.Stock -= issueQty;
                            }
                        }

                        // 5. Increment Bill Series and Commit
                        var selectbillseries = DB.Bill_Series.Find(billseriesid);
                        if (selectbillseries != null)
                        {
                            selectbillseries.Number += 1;
                        }

                        DB.SaveChanges();
                        transaction.Commit();

                        return Json(new
                        {
                            success = true,
                            message = "Voucher " + billHead.VoucherNumber + " generated successfully!",
                            voucherNumber = billHead.VoucherNumber,
                            id = billHead.id
                        });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return Json(new { success = false, message = "Error processing transaction: " + ex.Message });
                    }
                }
            }
            return Json(new { success = false, message = "Invalid data submitted." });
        }
        [HttpPost]
        public JsonResult ReturnStock(IssueModel model)
        {
            if (ModelState.IsValid)
            {
                using (var dbContextTransaction = DB.Database.BeginTransaction())
                {
                    try
                    {
                        string billseries = null;
                        int billseriesid = 0;
                        int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
                        var FinYear = Session["Fin_Year"].ToString();
                        var userName = Session["U_Name"].ToString();

                        if(model.HeadData.IsChallanReturn==true)
                        {
                            var seriesrt = (from ab in DB.Bill_Series
                                            where ab.Type == "ChallanReturn" && ab.CompanyID == companyid && ab.Fin_Year == FinYear
                                            select ab).SingleOrDefault();
                            billseriesid = seriesrt.id;
                            billseries = seriesrt.Series + seriesrt.Number;
                        }
                        else
                        {
                            var series = (from ab in DB.Bill_Series
                                          where ab.Type == "StockReturn" && ab.CompanyID == companyid && ab.Fin_Year == FinYear
                                          select ab).SingleOrDefault();
                            billseriesid = series.id;
                            billseries = series.Series + series.Number;

                        }
                            // 1. Get Bill Series for Stock Return
                           

                        if (billseries == null)
                        {
                            return Json(new { success = false, message = "Bill series not defined for StockReturn." });
                        }

                        // 2. Save the bill head data
                        IEPLStockReturnHead billHead = new IEPLStockReturnHead
                        {
                            VoucherNumber = billseries,
                            VoucherDate = System.DateTime.Now,
                            Departmentid = model.HeadData.Departmentid,

                            // --- RECONCILIATION FIELDS ---
                            // RefChallanId will be the ID of the Issue Voucher if "Against Challan" mode was used
                            RefChallanId = model.HeadData.RefChallanId > 0 ? model.HeadData.RefChallanId : (int?)null,
                            IsChallanReturn = model.HeadData.IsChallanReturn,

                            Createdby = userName,
                            Createdon = System.DateTime.Now,
                            CompanyID = companyid,
                            BomVoucherid = 0,
                            Fin_Year = FinYear,
                        };

                        DB.IEPLStockReturnHeads.Add(billHead);
                        DB.SaveChanges();

                        int headId = billHead.id;

                        // 3. Loop through items
                        foreach (var detail in model.TableData)
                        {
                            if (string.IsNullOrEmpty(detail.ItemCode)) continue;

                            var itemMaster = DB.BOMItemMasters.FirstOrDefault(im => im.ItemCode == detail.ItemCode);
                            if (itemMaster == null)
                            {
                                throw new Exception($"ItemCode {detail.ItemCode} not found in Master.");
                            }

                            decimal returnQty = decimal.Parse(detail.Quantity);

                            // A. Save the bill detail data
                            IEPLStockReturnDetail billDetail = new IEPLStockReturnDetail
                            {
                                Voucherid = headId,
                                Itemcodeid = itemMaster.Itemid,
                                Quantity = returnQty,
                                Remarks = detail.Remarks,
                                ApprovedQuantity = returnQty,
                                ApprovedStatus = 1,
                                ApprovedBy = userName,
                                ApprovedDate = System.DateTime.Now,
                                Voucher = "AutoApproved",
                                RejectedQuantity = 0
                            };
                            DB.IEPLStockReturnDetails.Add(billDetail);

                            // B. Update Live Stock Table (Inventory Update)
                            var stockRecord = DB.StockTables.FirstOrDefault(ll => ll.itemid == itemMaster.Itemid && ll.CompanyID == companyid);

                            if (stockRecord != null)
                            {
                                stockRecord.Stock += returnQty;
                            }
                            else
                            {
                                DB.StockTables.Add(new StockTable
                                {
                                    itemid = itemMaster.Itemid,
                                    CompanyID = companyid,
                                    Stock = returnQty
                                });
                            }
                        }

                        // 4. Update the Original Challan Status if applicable
                        // This marks the original issue as "Received" in your reporting logic
                        if (billHead.RefChallanId.HasValue)
                        {
                            // Optionally, if you have a status flag on the Issue table, update it here.
                            // var originalChallan = DB.IEPLStockIssueHeads.Find(billHead.RefChallanId);
                            // originalChallan.Status = "Closed"; 
                        }

                        // 5. Increment and Update Bill Series
                        //billseries.Number += 1;
                        var selectbillseries = (from ab in DB.Bill_Series
                                                where ab.id == billseriesid
                                                select ab).SingleOrDefault();
                        selectbillseries.Number += 1;

                        DB.SaveChanges();
                        dbContextTransaction.Commit();

                        return Json(new
                        {
                            success = true,
                            message = "Stock Return saved successfully!",
                            voucherNumber = billHead.VoucherNumber
                        });
                    }
                    catch (Exception ex)
                    {
                        dbContextTransaction.Rollback();
                        return Json(new { success = false, message = "Error: " + ex.Message });
                    }
                }
            }
            return Json(new { success = false, message = "Invalid data model." });
        }


        public class StockData
        {
            public int ItemId { get; set; } // Integer
            public decimal ClosingStock { get; set; } // Decimal
        }
        [HttpPost]
        public JsonResult StockAdjustment2(List<StockData> stockData)
        {
            // Log the incoming data for debugging
            System.Diagnostics.Debug.WriteLine("Received stock data: " + JsonConvert.SerializeObject(stockData));

            // Check if the model state is valid
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return Json(new { success = false, message = "Invalid data received.", errors });
            }
            return Json(new { success = false, message = "Invalid data received.", });

            // Your existing logic...
        }
        [HttpPost]
        public JsonResult StockAdjustment(List<StockData> stockData)
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());

            if (stockData == null || stockData.Count == 0)
            {
                return Json(new { success = false, message = "No data received." });
            }

            try
            {
                using (var transaction = DB.Database.BeginTransaction())
                {
                    foreach (var item in stockData)
                    {
                        int itemId = item.ItemId; // Get itemId from the item
                        decimal closingStock = item.ClosingStock; // Get closingStock from the item

                        // 1. Find ALL items with the given itemId and CompanyID
                        var itemsToUpdate = DB.StockTables.Where(ab => ab.itemid == itemId && ab.CompanyID == companyid).ToList();

                        if (itemsToUpdate.Count == 0)
                        {
                            return Json(new { success = false, message = $"No items with ID {itemId} found for Company {companyid}." });
                        }

                        // 2. Update the Stock for ALL matching items
                        foreach (var itemToUpdate in itemsToUpdate)
                        {
                            itemToUpdate.Stock = closingStock; // Update the Stock property
                        }
                    }

                    DB.SaveChanges();
                    transaction.Commit();
                    return Json(new { success = true, message = "Stock updated successfully." });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                return Json(new { success = false, message = "Error Updating Record." + ex.Message });
            }
        }
        public ActionResult GetHeads()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "GetHeads" && ab.Status == true
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

        public ActionResult GetQualityData()
        {
            var FinYear = Session["Fin_Year"].ToString();
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var heads = from h in DB.IEPLStockIN_Head
                        join s in DB.SupplierMasters on h.Supplierid equals s.id
                        where h.BillClosed==0 && h.CompanyID==companyid && h.Fin_Year==FinYear && h.Isdeleted==0
                        orderby h.CreatedDate ascending
                        select new HeadModel
                        
                        {
                            Id = h.Id,
                            BillNumber = h.BillNumber,
                            BillDate = h.BillDate,
                            CreatedBy = h.CreatedBy,
                            CreatedDate = (DateTime)h.CreatedDate,
                            Vouchernumber = h.Vouchernumber,
                            GRNumber = h.GRNumber,
                            GRDate = (DateTime)h.GRDate,
                            Supplierid = (int)h.Supplierid,
                            SupplierName = s.SupplierName // add the supplier name to the model
                        };

            return Json(heads.ToList(), JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetQualityLines(int headId)
        {
            var lines = (from ab in DB.IEPLStockIN_Detail
                        join s in DB.BOMItemMasters on ab.ItemCode equals s.Itemid
                        join u in DB.BOM_UOM on s.UOM equals u.id
                        where ab.HeadId == headId && ab.QualityApproved == 0
                        select new
                        {
                            ItemCode= ab.ItemCode,
                            ItemCodeA= s.ItemCode,
                            ItemName = s.ItemName + " - Lot/Serial No - " + (ab.LotNo ?? "NA"),
                            Quantity = ab.Quantity,
                            QualityApprovedQty= ab.QualityApprovedQty,
                            Id= ab.Id,
                            RejectedQty=ab.RejectedQuantity,
                            UOM=u.UOM,
                        }).ToList();
            var qualityLines = DB.IEPLStockIN_Detail
               .Where(ql => ql.HeadId == headId && ql.QualityApproved==0)
               .ToList();

            var jsonSettings = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
            var json = JsonConvert.SerializeObject(lines, jsonSettings);

            return Content(json, "application/json");
        }
        [HttpPost]
        public ActionResult ApproveQuantity(int itemId, decimal approvedQty, int itemCode, string Remarks)
        {
            if (Session["U_Name"] == null)
            {
                return RedirectToAction("Login", "Index"); // redirect to Account controller, Login action
            }
            // update line of stockin table
            if (itemId != 0)
            {
                var selectline = (from ab in DB.IEPLStockIN_Detail
                                  where ab.Id == itemId
                                  select ab).FirstOrDefault();
                decimal quantity = selectline.Quantity;
                decimal approved = (decimal)selectline.QualityApprovedQty;
                decimal rejected = (decimal)selectline.RejectedQuantity;

                //Check if coming multiple Clicks and updated multiple times
                decimal countexisting = approved + rejected;

                if (selectline.QualityApproved==1)
                {
                    return Json(new { success = false, message = "Transaction stopped due to duplicate approval attempt." });
                }
                else if ((countexisting + approvedQty) > quantity)
                {
                    return Json(new { success = false, message = "Transaction QTY Approved + Rejected is Going More than Quantity Recieved in Purchase" });
                }
                else
                {

                    decimal TT = (decimal)(selectline.QualityApprovedQty + approvedQty + selectline.RejectedQuantity);
                    if (selectline.Quantity == TT)
                    {
                        selectline.QualityApprovedQty = selectline.QualityApprovedQty + approvedQty;
                        selectline.QApprovedDate = System.DateTime.Now;
                        selectline.QualityApproved = 1;
                        selectline.RejectionRemarks = Remarks;
                    }
                    else
                    {

                        selectline.QualityApprovedQty = selectline.QualityApprovedQty + approvedQty;
                        selectline.QApprovedDate = System.DateTime.Now;
                    }
                    DB.SaveChanges();

                    int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
                    // Update stock in BOMItemMasters table
                    var bomItemMaster = (from bb in DB.BOMItemMasters
                                         where bb.Itemid == itemCode
                                         select bb).FirstOrDefault();
                    var stocktable = (from st in DB.StockTables
                                      where st.itemid == itemCode && st.CompanyID== companyid
                                      select st).FirstOrDefault();
                    if (stocktable == null)
                    {
                        //if value is null means there is no record updated yet
                        //now we have to first create line to hold stock
                        StockTable ST = new StockTable();
                        ST.itemid = bomItemMaster.Itemid;
                        ST.CompanyID = companyid;
                        ST.BasicPrice = bomItemMaster.BasicPrice;
                        ST.RejectedStock = bomItemMaster.RejectedStock;
                        ST.Stock = 0;
                        ST.Minstklvl = bomItemMaster.Minstklvl;
                        ST.Maxstklvl = bomItemMaster.Maxstklvl;
                        ST.MOQ = bomItemMaster.MinimumOrderQuantity;
                        ST.LastDiscount = 0;
                        ST.LastSupplierid = 0;
                        DB.StockTables.Add(ST);
                        DB.SaveChanges();
                        //Here we have created entry in stock table if there is not Record against company id

                        //after creating line in this table we need to add stock 
                        var updatestocktable = (from nn in DB.StockTables
                                                where nn.itemid == itemCode && nn.CompanyID == companyid
                                                select nn).FirstOrDefault();
                        updatestocktable.Stock += approvedQty;
                        DB.SaveChanges();

                    }
                    if (stocktable!=null)
                    {
                        //if coming in this condition means record is already avaibale in stock table
                        //now we need to add stock here in this table
                        stocktable.Stock += approvedQty;
                        DB.SaveChanges();
                        

                    }
                    if (bomItemMaster != null)
                    {
                        bomItemMaster.Stock += approvedQty;
                        //Now also Update Stock in new table i.e stock table

                        DB.SaveChanges();
                    }

                    // Add line update information to another table
                    IEPLStockInQualityTransaction IP = new IEPLStockInQualityTransaction();
                    {
                        IP.StockinLiineID = itemId;
                        IP.Approvedqty = approvedQty;
                        IP.Approvedtime = System.DateTime.Now;
                        IP.Approvedby = Session["U_Name"].ToString();
                    }
                    //check if LotDetails are Enabled for specific Category
                    var itemmaster = DB.BOMItemMasters.Where(x=> x.Itemid==selectline.ItemCode && x.CompanyID==companyid).SingleOrDefault();
                    var checkcategory=DB.EnableLotDetails.Where(x=> x.GroupID == itemmaster.ItemGroup && x.IsActive==true && x.CompanyID==companyid).SingleOrDefault();
                    if (checkcategory != null)
                        //If Not Null Means Lot Is Enabled
                    {
                        //Get Head Details
                        var Head = (from ab in DB.IEPLStockIN_Head
                                    where ab.Id == selectline.HeadId
                                    select ab).SingleOrDefault();
                        //Add LotNumber Entry and QUantity in Lot Table
                        Stock_lotDetail SDL = new Stock_lotDetail();
                        SDL.ItemID = selectline.ItemCode;
                        SDL.RecieptDateTime = Head.BillDate;
                        SDL.Lot_SerialNumber = selectline.LotNo;
                        SDL.OriginalQuantity = selectline.QualityApprovedQty;
                        SDL.CurrentQuantity = selectline.QualityApprovedQty; ;
                        SDL.IsAvailable = true;
                        SDL.ReceivingLineID = selectline.Id;
                        SDL.SupplierID = Head.Supplierid;
                        SDL.CreatedOn = DateTime.Now;
                        SDL.CreatedBy = Session["U_Name"].ToString();
                        SDL.IsDeleted = false;
                        SDL.IsReserved = false;
                        SDL.CompanyID = companyid;
                        DB.Stock_lotDetail.Add(SDL);
                        DB.SaveChanges();
                    }


                    DB.IEPLStockInQualityTransactions.Add(IP);
                    DB.SaveChanges();
                    return Json(new { success = true, message = "Quantity Approved successfully For Item Code - " + itemCode });
                }
            }
            return Json(new {success=false, message = "There is Something Wrong" });


        }
        public class MiscSaleViewModel
        {
            // Header Data
            public string CustomerName { get; set; }
            public string CustomerAddress { get; set; }
            public string GSTNo { get; set; }
            public string InvoiceDate { get; set; }
            public decimal TotalBasic { get; set; }
            public decimal TotalTax { get; set; }
            public decimal NetAmount { get; set; }
            public string bill { get; set; }
            public string TaxType { get; set; }
            public decimal TCS_Percent { get; set; }
            public decimal TCS_Amount { get; set; }

            // List of Items (Table Data)
            public List<MiscSaleItemDetail> Items { get; set; }
        }

        public class MiscSaleItemDetail
        {
            public string ItemCode { get; set; }
            public string Description { get; set; }
            public decimal Rate { get; set; }
            public decimal Qty { get; set; }
            public decimal TaxAmount { get; set; }
            public int TaxID { get; set; }
            public decimal LineTotal { get; set; }
        }
        public ActionResult MiscBilling()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "MiscBilling" && ab.Status == true
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
        public JsonResult SaveMiscSale(string jsonData)
        {
            var model = JsonConvert.DeserializeObject<MiscSaleViewModel>(jsonData);
            int companyid = Convert.ToInt32(Session["Company_ID"]);

            using (var trans = DB.Database.BeginTransaction())
            {
                try
                {
                    // 1. Generate Invoice Number (Logic similar to your MRN series)
                   // var series = DB.Bill_Series.FirstOrDefault(x => x.Type == "SALE" && x.CompanyID == companyid);
                   // string invNo = series.Series + series.Number;
                   // series.Number += 1;

                    // 2. Save Head
                    var head = new IEPL_MiscSale_Head
                    {
                        InvoiceNo = model.bill,
                        InvoiceDate = DateTime.Parse(model.InvoiceDate),
                        CustomerName = model.CustomerName,
                        CustomerAddress = model.CustomerAddress,
                        GSTNo = model.GSTNo,
                        TotalBasic = model.TotalBasic,
                        TotalTax = model.TotalTax,
                        NetAmount = model.NetAmount,
                        CompanyID = companyid,
                        TaxType=model.TaxType,
                        CreatedBy = Session["U_Name"].ToString(),
                        CreatedDate = DateTime.Now,
                        TCS_Percent = model.TCS_Percent,
                        TCS_Amount = model.TCS_Amount
                    };
                    DB.IEPL_MiscSale_Head.Add(head);
                    DB.SaveChanges();

                    // 3. Save Details
                    foreach (var item in model.Items)
                    {
                        var itemid = DB.BOMItemMasters
                                       .Where(x => x.ItemCode == item.ItemCode)
                                       .Select(x => x.Itemid)
                                       .SingleOrDefault();

                        var detail = new IEPL_MiscSale_Detail
                        {
                            HeadId = head.Id,
                            ItemCode = item.ItemCode,
                            ItemDescription = item.Description,
                            Qty = item.Qty,
                            Rate = item.Rate,
                            TaxID = item.TaxID,
                            TaxAmount = item.TaxAmount,
                            LineTotal = item.LineTotal,
                            ItemID = itemid,
                            IsDeleted = false,
                            DateCreated = DateTime.Now
                        };
                        DB.IEPL_MiscSale_Detail.Add(detail);

                        // --- CORRECT STOCK UPDATE LOGIC ---
                        if (itemid > 0)
                        {
                            // 1. Fetch the actual object row from the Stock table
                            var stockRecord = DB.StockTables.FirstOrDefault(ab => ab.itemid == itemid && ab.CompanyID == companyid);

                            if (stockRecord != null)
                            {
                                // 2. Subtract the quantity from the object property
                                stockRecord.Stock -= item.Qty;

                                // Entity Framework tracks this change automatically
                            }
                        }
                    }

                    // 4. This will now save both the new Detail rows AND the updated Stock levels
                    DB.SaveChanges();
                    trans.Commit();
                    return Json(new { success = true, invoiceNo = model.bill });
                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    return Json(new { success = false, message = ex.Message });
                }
            }
        }
        [HttpPost]
        public ActionResult RejectQuantity(int itemId, decimal approvedQty, int itemCode, string Remarks)
        {
            if (Session["U_Name"] == null)
            {
                return RedirectToAction("Login", "Index"); // redirect to Account controller, Login action
            }
            // update line of stockin table
            if (itemId != 0)
            {
                var selectline = (from ab in DB.IEPLStockIN_Detail
                                  where ab.Id == itemId
                                  select ab).FirstOrDefault();
                decimal TT = (decimal)(selectline.QualityApprovedQty + approvedQty+selectline.RejectedQuantity);
                if (selectline.Quantity == TT)
                {
                    selectline.RejectedQuantity = selectline.RejectedQuantity + approvedQty;
                    selectline.QApprovedDate = System.DateTime.Now;
                    selectline.RejectionRemarks= Remarks;
                    selectline.QualityApproved = 1;
                    
                }
                else
                {

                    selectline.RejectedQuantity =selectline.RejectedQuantity+ approvedQty;
                    selectline.QApprovedDate = System.DateTime.Now;
                    selectline.RejectionRemarks = Remarks;
                }
                DB.SaveChanges();

                int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
                var stocktable = (from st in DB.StockTables
                                  where st.itemid == itemCode && st.CompanyID == companyid
                                  select st).FirstOrDefault();
                // Update stock in BOMItemMasters table
                var bomItemMaster = (from bb in DB.BOMItemMasters
                                     where bb.Itemid == itemCode
                                     select bb).FirstOrDefault();
                if (bomItemMaster != null)
                {
                    bomItemMaster.RejectedStock += approvedQty;
                    DB.SaveChanges();
                }

                //Check New table if Line Exist or not (if not create line first)
                if (stocktable == null)
                {
                    //if value is null means there is no record updated yet
                    //now we have to first create line to hold stock
                    StockTable ST = new StockTable();
                    ST.itemid = bomItemMaster.Itemid;
                    ST.CompanyID = companyid;
                    ST.BasicPrice = bomItemMaster.BasicPrice;
                    ST.RejectedStock = bomItemMaster.RejectedStock;
                    ST.Stock = 0;
                    ST.Minstklvl = bomItemMaster.Minstklvl;
                    ST.Maxstklvl = bomItemMaster.Maxstklvl;
                    ST.MOQ = bomItemMaster.MinimumOrderQuantity;
                    ST.LastDiscount = 0;
                    ST.LastSupplierid = 0;
                    DB.StockTables.Add(ST);
                    DB.SaveChanges();
                    //Here we have created entry in stock table if there is not Record against company id

                    //after creating line in this table we need to add stock 
                    var updatestocktable = (from nn in DB.StockTables
                                            where nn.itemid == itemCode && nn.CompanyID == companyid
                                            select nn).FirstOrDefault();
                    updatestocktable.RejectedStock += approvedQty;
                    DB.SaveChanges();

                }
                if (stocktable != null)
                {
                    //if coming in this condition means record is already avaibale in stock table
                    //now we need to add stock here in this table
                    stocktable.RejectedStock += approvedQty;
                    DB.SaveChanges();


                }

                // Add line update information to another table
                IEPLStockInQualityTransaction IP = new IEPLStockInQualityTransaction();
                {
                    IP.StockinLiineID = itemId;
                    IP.Rejectedqty = approvedQty;
                    IP.Approvedtime = System.DateTime.Now;
                    IP.Approvedby = Session["U_Name"].ToString();
                    IP.RejectionRemarks = Remarks;
                }

                DB.IEPLStockInQualityTransactions.Add(IP);
                DB.SaveChanges();
            }

            return Json(new { success = true, message = "Quantity Rejected For Item Code - " + itemCode });
        }
        [HttpPost]
        public JsonResult CloseQuanlityBill(int headId)
        {
            try
            {

                var head = (from ab in DB.IEPLStockIN_Head
                            where ab.Id == headId
                            select ab).FirstOrDefault();
                //here i want to check status of everyline
                var lines = (from lin in DB.IEPLStockIN_Detail
                            where lin.HeadId == headId && lin.QualityApproved == 0
                            select lin).ToList();
                if (lines.Any())
                {
                    return Json(new { success = false, message = "Few Items are Pending for approval." });
                }

                if (head != null)
                    {
                    head.BillClosed= 1; 
                    head.BillClosedatetime=System.DateTime.Now;
                    head.BillCloseby = Session["U_Name"].ToString();
                    head.MRNQualityApproval = 1;
                    //Update MRN Detail in Purchase Head 
                    var selectlog = (from ab in DB.MRNDetailLogs
                                     where ab.HeadID == headId
                                     select ab).FirstOrDefault();
                    {
                        selectlog.QualityApprovalBY = Session["U_Name"].ToString();
                        selectlog.QualityADate = System.DateTime.Now;

                    }

                    // Assuming you have a boolean field to track if the bill is closed
                    DB.SaveChanges();

                        return Json(new { success = true, message = "Bill closed successfully" });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Bill not found" });
                    }
                
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error closing bill: " + ex.Message });
            }
        }

        public ActionResult UpdateItem()
        {
            //Check Authorization
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "UpdateItem" && ab.Status == true
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
        public ActionResult UpdateItemMaster(ItemMasterModel model)
        {
            try
            {
                int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
                var FinYear = Session["Fin_Year"].ToString(); // This variable is not used in the provided logic, but retained.

                // Use a single DbContext instance for all operations within this action
                using (var dbContext = new IECEntities()) // Ensure IECEntities is your actual DbContext class
                {
                    // 1. Find the ItemMaster record
                    var itemMaster = dbContext.BOMItemMasters
                                              .FirstOrDefault(ab => ab.ItemCode == model.ItemCode);

                    if (itemMaster == null)
                    {
                        // Item Master not found, return an error message
                        return Json(new { success = false, message = "Item Master with provided Item Code not found." });
                    }

                    // 2. Find the corresponding StockTable record using the SAME dbContext instance
                    // It's good practice to also include CompanyID in this query for multi-company setups
                    var stocktable = dbContext.StockTables
                                              .FirstOrDefault(bc => bc.itemid == itemMaster.Itemid && bc.CompanyID == companyid);

                    if (stocktable == null)
                    {
                        // Stock record not found, handle this case (e.g., log, return error)
                        return Json(new { success = false, message = "Stock record not found for the item. Update aborted." });
                    }

                    // 3. Store old values for logging before updating
                    string oldItemName = itemMaster.ItemName;
                    string oldDesc = itemMaster.Desc;
                    decimal oldMinStock = (decimal)itemMaster.Minstklvl;
                    decimal oldMaxStock = (decimal)itemMaster.Maxstklvl;
                    // Removed oldStock and NewStock because you commented out itemMaster.Stock = model.Stock;
                    decimal oldMoq = (decimal)itemMaster.MinimumOrderQuantity;
                    //decimal oldBasicPrice = itemMaster.BasicPrice; // Get old BasicPrice for logging


                    // 4. Update ItemMaster properties (tracked by dbContext)
                    itemMaster.ItemName = model.ItemName;
                    itemMaster.Desc = model.ItemDesc;
                    itemMaster.Minstklvl = model.MinStock;
                    itemMaster.Maxstklvl = model.MaxStock;
                    itemMaster.ItemClass = Convert.ToInt32(model.itemclass);
                    itemMaster.ItemGroup = Convert.ToInt32(model.itemgroup);
                    itemMaster.ItemLocation= Convert.ToInt32(model.itemlocation);
                    itemMaster.BasicPrice = model.basicprice; // Set BasicPrice on itemMaster

                    // 5. Update StockTable properties (also tracked by dbContext)
                    stocktable.Minstklvl = model.MinStock;
                    stocktable.Maxstklvl = model.MaxStock;
                    stocktable.BasicPrice = model.basicprice; // Set BasicPrice on stocktable (THIS IS THE KEY FIX)


                    // 6. Handle DrawingData update
                    if (!string.IsNullOrEmpty(model.DrawingData))
                    {
                        // Ensure it's a valid data URL (contains a comma separating metadata from base64)
                        if (model.DrawingData.Contains(","))
                        {
                            string base64Data = model.DrawingData.Split(',')[1];
                            byte[] fileBytes = Convert.FromBase64String(base64Data);
                            itemMaster.DrawingData = fileBytes; // Save the file data
                        }
                        else
                        {
                            // Handle case where DrawingData is not empty but not a valid data URL, e.g., log a warning
                            // You might want to throw an error or skip update based on your requirements
                            System.Diagnostics.Debug.WriteLine($"Warning: DrawingData for ItemCode {model.ItemCode} is not a valid data URL.");
                        }
                    }
                    else
                    {
                        // If DrawingData is explicitly empty or null from the model, you might want to clear it in DB
                        itemMaster.DrawingData = null;
                    }

                    itemMaster.MinimumOrderQuantity = model.MOQ;
                    itemMaster.HSNCode = model.HSN;

                    // 7. Create and add a new log entry (tracked by dbContext)
                    var logEntry = new BOMItemMasterLog
                    {
                        ItemCode = model.ItemCode,
                        UpdatedBy = Session["U_Name"].ToString(),
                        OldItemName = oldItemName != null && oldItemName.Length > 100 ? oldItemName.Substring(0, 100) : oldItemName,
                        NewItemName = model.ItemName != null && model.ItemName.Length > 100 ? model.ItemName.Substring(0, 100) : model.ItemName,
                        OldDesc = oldDesc,
                        NewDesc = model.ItemDesc,
                        OldMinStock = oldMinStock,
                        NewMinStock = model.MinStock,
                        OldMaxStock = oldMaxStock,
                        NewMaxStock = model.MaxStock,
                        OldMOQ = oldMoq,
                        NewMOQ = model.MOQ,
                       // OldBasicPrice = oldBasicPrice, // Log old basic price
                       // NewBasicPrice = model.basicPrice, // Log new basic price
                        UpdatedDate = System.DateTime.Now,
                        // Make sure your BOMItemMasterLog model has OldBasicPrice and NewBasicPrice properties
                    };
                    dbContext.BOMItemMasterLogs.Add(logEntry);

                    // 8. Save all changes to the database in one go
                    dbContext.SaveChanges();
                }

                return Json(new { success = true, message = "Item Has Been Updated" });
            }
            catch (Exception ex)
            {
                // Log the full exception details for debugging
                System.Diagnostics.Debug.WriteLine($"Error in UpdateItemMaster: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                return Json(new { success = false, error = "An error occurred during update: " + ex.Message });
            }
        }
        public ActionResult QualityRejectionReturn()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "QualityRejectionReturn" && ab.Status == true
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
        public JsonResult GetBills()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            // fetch bills where Rejection quantity is greater than 0
            var bills = (from detail in DB.IEPLStockIN_Detail
                         join Product in DB.BOMItemMasters on detail.ItemCode equals Product.Itemid
                         join master in DB.IEPLStockIN_Head on detail.HeadId equals master.Id
                         join supplier in DB.SupplierMasters on master.Supplierid equals supplier.id
                         where detail.RejectedQuantity > 0 && master.BillClosed == 1 && detail.RejectionReturn==0 && master.CompanyID==companyid
                         select new
                         {
                             id = master.Id,
                             BillNo = master.BillNumber,
                             BillDate = master.BillDate,
                             Material = Product.ItemName,
                             RejectionQty = detail.RejectedQuantity,
                             Supplier = supplier.SupplierName,
                             itemlineid=detail.Id,
                             itemid=Product.Itemid,
                             // other properties...
                         }).ToList();

            return Json(bills, JsonRequestBehavior.AllowGet);

            //return Json(bills, JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetRejectionTypes()
        {
            //int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            //var rejectionTypes = DB.RejectionTypes.ToList();

            var rejectionTypes = (from ab in DB.RejectionTypes
                                  where ab.CompanyID == 17 && ab.IsDeleted == 0
                                  select new
                                  {
                                      Value = ab.id,
                                      Text= ab.RejectionType1,

                                  }).ToList();
            return Json(rejectionTypes, JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetRejectionAreas(int rejectionTypeId)
        {
            var rejectionAreas = (from ab in DB.RejectionAreas
                                  where ab.RejectionTypeID == rejectionTypeId
                                  select new
                                  {
                                      Value = ab.id,
                                      Text = ab.RejectionArea1,
                                  }).ToList();
            //var rejectionAreas = DB.RejectionAreas.Where(ra => ra.RejectionTypeID == rejectionTypeId).ToList();
            return Json(rejectionAreas, JsonRequestBehavior.AllowGet);
        }
        
         public ActionResult GetBillSuppliers(int billId)
        {
            var suppliername= (from ab in DB.IEPLStockIN_Head
                           join supplier in DB.SupplierMasters on ab.Supplierid equals supplier.id
                           where ab.Id == billId
                                  select new
                                  {
                                      Value = supplier.id,
                                      Text =  supplier.SupplierName,
                                  }).ToList();
            //var rejectionAreas = DB.RejectionAreas.Where(ra => ra.RejectionTypeID == rejectionTypeId).ToList();
            return Json(suppliername, JsonRequestBehavior.AllowGet);
        }

        // POST: CreateReturn
        [HttpPost]
        public ActionResult SaveQualityReturn(string itemid, int billId, int itemLineId, decimal rejectionQty, string supplier, string billNo, string material, string remarks, string rejectionArea, int returnType)
        {
            try
            {
                int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
                var FinYear = Session["Fin_Year"].ToString();
                // Validate the input data
                if (billId == 0 || itemLineId == 0 || rejectionQty == 0)
                {
                    return Json(new { success = false, message = "Invalid input data" });
                }

                //Update In Purchase Detail Table
                var selectline = (from ab in DB.IEPLStockIN_Detail
                                  where ab.Id == itemLineId
                                  select ab).SingleOrDefault();
                {
                    selectline.RejectionReturn = 1;
                }
                //Update Rejection Stock 

                var itemmaster = (from bb in DB.BOMItemMasters
                                  where bb.Itemid == selectline.ItemCode
                                  select bb).SingleOrDefault();
                //Update in New StockTable
                var stocktable = (from ac in DB.StockTables
                                  where ac.itemid == selectline.ItemCode && ac.CompanyID == companyid
                                  select ac).SingleOrDefault();
                //Update stock in Stock Table New
                if (stocktable.RejectedStock >= rejectionQty)
                {
                    stocktable.RejectedStock -= rejectionQty;
                }
                //Update Stock in Old BOM Item Master table
                if (itemmaster.RejectedStock >= rejectionQty)
                {

                    {
                        itemmaster.RejectedStock -= rejectionQty;
                    }


                    // Save the quality return data to the database
                    using (var dbContext = new IECEntities())
                    {
                        var id = (from ab in DB.IEPLQualityRejectionHeads
                                  orderby ab.id descending
                                  select ab.id).FirstOrDefault();
                        //var voucher = DB.IEPLQualityRejectionHeads.LastOrDefault()?.id;
                        var qualityReturn = new IEPLQualityRejectionHead
                        {
                            VoucherNo = "QRV-" + (id + 1),
                            VoucherDate = System.DateTime.Now,
                            Createdby = Session["U_Name"].ToString(),
                            CreatedDate = System.DateTime.Now,  
                            CompanyID=companyid,
                            Fin_Year=FinYear,

                        };
                        dbContext.IEPLQualityRejectionHeads.Add(qualityReturn);
                        dbContext.SaveChanges();
                        var qualityReturnId = qualityReturn.id;
                        // dbContext.SaveChanges();

                        // Save Item Detail
                        IEPLQualityRejectionDetail qualityitem;

                        if (rejectionArea == null)
                        {
                            qualityitem = new IEPLQualityRejectionDetail
                            {
                                Headid = qualityReturnId,
                                Itemid = Convert.ToInt32(itemid),
                                PurchaseDetailid = itemLineId,
                                RejectionQuantity = rejectionQty,
                                RejectiontoPlant = 0,
                                RejectionLocation = 0,
                                RejectiontoVendor = 1,
                                RejectionType = returnType,
                                CreatedOn = System.DateTime.Now,
                                CreatedBy = Session["U_Name"].ToString(),
                                Companyid = companyid,
                                FinYear= FinYear,
                                Remarks = remarks,
                            };
                        }
                        else
                        {
                            qualityitem = new IEPLQualityRejectionDetail
                            {
                                Headid = qualityReturnId,
                                Itemid = Convert.ToInt32(itemid),
                                PurchaseDetailid = itemLineId,
                                RejectionQuantity = rejectionQty,
                                RejectiontoPlant = 1,
                                RejectionLocation = Convert.ToInt32(rejectionArea),
                                RejectiontoVendor = 0,
                                RejectionType = returnType,
                                CreatedOn = System.DateTime.Now,
                                CreatedBy = Session["U_Name"].ToString(),
                                Companyid = companyid,
                                FinYear=FinYear,
                                Remarks = remarks,
                            };
                        }

                        dbContext.IEPLQualityRejectionDetails.Add(qualityitem);
                        dbContext.SaveChanges();


                    }
                    DB.SaveChanges();
                    return Json(new { success = true, message = "Quality return saved successfully" });
                }
                else
                {
                    return Json(new { success = false, message = "Stock Table is Having Less Quanity in Rejected Stock" });
                }



            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        public ActionResult OpeningStock()
        {

            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "OpeningStock" && ab.Status == true
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
        public ActionResult AddOpeningStock(int id, decimal currentStock, decimal openingStock)
        {
            try
            {
                // Check if item already exists
                if (DB.OpeningStocks.Any(op => op.Itemid == id))
                {
                    return Json(new { success = false, message = "Item already exists" });
                }

                // Add new opening stock
                OpeningStock OP = new OpeningStock();
                OP.Itemid = id;
                OP.CurrentStock = currentStock;
                OP.OpeningStock1 = openingStock;
                OP.CreatedDate = DateTime.Now;
                OP.Companyid = 17;
                OP.UpdatedBy = Session["U_Name"].ToString();
                DB.OpeningStocks.Add(OP);
                DB.SaveChanges();

                return Json(new { success = true, message = "Opening stock added successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        [HttpGet]
        public ActionResult GetOpeningStocks()
        {
            var openingStocks = (from ab in DB.OpeningStocks
                                 join item in DB.BOMItemMasters on ab.Itemid equals item.Itemid
                                 select new
                                 {
                                    ItemCode = item.ItemCode,
                                    ItemName = item.ItemName,
                                    CurrentStock = ab.CurrentStock,
                                    OpeningStock = ab.OpeningStock1,
                                 }).ToList();



            return Json(openingStocks, JsonRequestBehavior.AllowGet);
        }


        public class HeadModel
        {
            public int Id { get; set; }
            public string BillNumber { get; set; }
            public DateTime? BillDate { get; set; }
            public string CreatedBy { get; set; }
            public DateTime? CreatedDate { get; set; }
            public string Vouchernumber { get; set; }
            public string GRNumber { get; set; }
            public DateTime? GRDate { get; set; }
            public int? Supplierid { get; set; }
            public string SupplierName { get; set; }
        }

        public class IssueModel
        {
            public IssueData HeadData { get; set; }
            public List<IssueTableData> TableData { get; set; }
        }

        public class IssueData
        {

            public int Departmentid { get; set; }
            public bool IsChallan { get; set; }
            public bool IsReturnable { get; set; }
            public bool IsChallanReturn { get; set; }
            public string ChallanNature { get; set; }
            public int? RefChallanId { get; set; } // For tracking original challan in returns
            public string Remarks { get; set; }

            // --- Main Components: Customer & Logistics ---
            public string CustomerName { get; set; }
            public string ContactNo { get; set; }
            public string Address { get; set; }        // This is your Shipping Address
            public string BillingAddress { get; set; } // New: From the Modal
            public string TransportName { get; set; }  // New: From the Modal
            public string VehicleNo { get; set; }      // From the Modal

            // --- Bilty (LR) Intelligence: The New Fields ---
            public bool IsBilty { get; set; }           // Toggle switch status
            public string BiltyNo { get; set; }
            public DateTime? BiltyDate { get; set; }
            public decimal? BiltyTotalValue { get; set; }
            public string BiltyDescription { get; set; }


        }
        public class IssueTableData
        {
            public string ItemCode { get; set; }
            public string ItemName { get; set; }
            public string UOM { get; set; }
            public string Quantity { get; set; }
            public string Remarks { get; set; }
        }
        public class ItemMasterModel
        {
            public string ItemCode { get; set; }
            public string HSN { get; set; }
            public string ItemName { get; set; }
            public string ItemDesc { get; set; }
            public decimal MinStock { get; set; }
            public decimal MaxStock { get; set; }
            public decimal Stock { get; set; }
            public decimal MOQ { get; set; }
            public string DrawingData { get; set; }
            public string itemclass { get; set; }
            public string itemgroup { get; set; }
            public string itemlocation { get; set; }
            public decimal basicprice { get; set; }

        }

    }
}