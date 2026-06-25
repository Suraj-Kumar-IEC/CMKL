using CMKL.Models;
using CMKL.Views.BOM;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.EMMA;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Office2010.ExcelAc;
using DocumentFormat.OpenXml.Wordprocessing;
using Grpc.Core;
using Microsoft.Ajax.Utilities;
using Org.BouncyCastle.Bcpg.OpenPgp;
using RazorEngine;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Linq;
using System.Net.Mail;
using System.Reflection.Emit;
using System.Web.Management;
using System.Web.Mvc;

namespace CMKL.Controllers
{
    public class ProductionController : Controller
    {
        IECEntities DB = new IECEntities();
        // GET: Production
        public ActionResult GetDailyProductionData()
        {
            DateTime today = DateTime.Today;

            var productionData = DB.BOMVouchers
                .Where(v => v.VoucherDate.HasValue && EntityFunctions.TruncateTime(v.VoucherDate.Value) == today)
                .GroupBy(v => new { v.FGProductID, v.CreatedBy })
                .Select(g => new
                {
                    FGProductID = g.Key.FGProductID,
                    Quantity = g.Count(),
                    CreatedBy = g.Key.CreatedBy
                })
                .ToList();

            return Json(productionData, JsonRequestBehavior.AllowGet);
        }
        public ActionResult PendingProductionView()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "PendingProductionView" && ab.Status == true
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
        public ActionResult PendingEwap()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "PendingEwap" && ab.Status == true
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
        public ActionResult Dispatch()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "Dispatch" && ab.Status == true
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
        public ActionResult DispatchReturn()
        {

            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "DispatchReturn" && ab.Status == true
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
        public ActionResult ReverseEwap() 
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "ReverseEwap" && ab.Status == true
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
        public ActionResult FGReturnQualityAction()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "FGReturnQualityAction" && ab.Status == true
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
        public ActionResult FGReturnBOMCreation()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "FGReturnBOMCreation" && ab.Status == true
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
        public ActionResult ReturnBomApproval()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "ReturnBomApproval" && ab.Status == true
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
        public ActionResult PendingPDI()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "PendingPDI" && ab.Status == true
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
        public ActionResult CompletedPDI()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "CompletedPDI" && ab.Status == true
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
        public ActionResult GetBOMPrintData(int BOMID)
        {
            var PDIBy = "";
            var Account = "";
            var Ewap = DB.BOMEwapDetails.Where(x => x.BomVoucherHeadID == BOMID).SingleOrDefault();
            PDIBy = Ewap.PDIBy;
            
            var PDISignature = DB.tbl_User_Master.Where(x => x.user_name == PDIBy).Select(x => x.SignURL).SingleOrDefault();


            var ProductionData = (from ab in DB.Bom_ProductionUpdate
                                  join md in DB.EngineModels on ab.EngineModel equals md.EngineId
                                  join rt in DB.Ratings_Production on ab.AlternatorRating equals rt.id into rtgroup
                                  from rt in rtgroup.DefaultIfEmpty()
                                  join ph in DB.Phases on ab.AlternatorPhase equals ph.PhaseId
                                  where ab.BOMHeadid == BOMID
                                  select new
                                  {
                                      emake = ab.EngineMake,
                                      emodel = md.EngineModel1,
                                      engineSerialNo = ab.EngineSerialNumber,
                                      etestcert = ab.EngineTestCert,
                                      eremarks = ab.EngineRemarks,
                                      amake = ab.AlternatorMake,
                                      aframe = ab.AlternatorFrame,
                                      alternatorSerialNo = ab.AlternatorSerialNo,
                                      arating= rt.Rating ?? null,
                                      aphase = ph.PhaseDesc ?? null,
                                      atestcert = ab.AlternatorTestCert,
                                      awarranty = ab.Alternatorwarranty,
                                      aremarks = ab.AlternatorRemarks,
                                      exhaust = ab.ExhaustPipe,
                                      kgsticker = ab.KGSticker,
                                      AVM = ab.AVMPads,
                                      fuelspout = ab.FuelSpout,
                                      flexbel = ab.FlexibleBellow,
                                      ProductionBy = ab.CreatedBy,
                                  }).SingleOrDefault();

            var TestingData = (from bc in DB.BOM_TestingUpdate
                               join cp in DB.ControlPanelTypes on bc.CPType equals cp.id
                               join rt in DB.Ratings_Production on bc.CPRating equals rt.id
                               join gnr in DB.Ratings_Production on bc.GensetRating equals gnr.id
                               join btr in DB.BatteryMasters on bc.BTRating equals btr.id
                               where bc.BOMHeadid == BOMID
                               select new
                               {
                                   CPtype = cp.Type,
                                   cprating = rt.Rating,
                                   PanelSerialNo = bc.CPSerialNumber,
                                   cpotherdetail = bc.CPRemarks,
                                   bmake = bc.BTMake,
                                   BatteryRating = btr.BatteryName,
                                   Batteryqty = bc.BTQty,
                                   BatterySerialNo = bc.BTSerial,
                                   dateOfTesting = bc.TestingDate,
                                   ratingOfGenset = gnr.Rating,
                                   canopy = bc.CanopyQty,
                                   KRMNo = bc.KRMNo,
                                   adblue = bc.ADBlue,
                                   lube = bc.LubeOil,
                                   kcool = bc.KCool,
                                   specialRemarks = bc.SpecialRemarks,
                                   TestingBy=bc.CreatedBy,                                   

                               }).SingleOrDefault();

            var DispatchDetail = (from bb in DB.DispatchDetails
                                  where bb.Ewapid == Ewap.id
                                  select new
                                  {
                                      CustomerName = bb.CustomerName,
                                      customerAddress = bb.ShippingAddress,
                                      InvoiceNo = bb.BillNumber,
                                      InvoiceDate = bb.BillDate,
                                      Erection = bb.Erection,
                                      ErectionDetail = bb.ErectionDetail,
                                      Transport = bb.Transport,
                                      LR = bb.LR,
                                      LRdate = bb.LRDate,
                                      Lorry = bb.LorryNo,
                                      Eway = bb.EwayNo,
                                      VoucherNo = bb.VoucherNumber,
                                      Companyid=bb.CompanyID,
                                      CreatedBy=bb.CreatedBy,
                                  }).SingleOrDefault();
            var Company = (from co in DB.Companies
                           where co.CompanyID == DispatchDetail.Companyid
                           select new
                           {
                               CompanyName= co.CompanyName,
                               Address=co.CompanyAddress,
                               Address1=co.CompanyAddress2,
                               CompanyGST=co.GST,
                               CompanyPAN=co.PAN,

                           }).SingleOrDefault();
            Account = DispatchDetail.CreatedBy;
            var AccSignature = DB.tbl_User_Master.Where(x => x.user_name == Account).Select(x => x.SignURL).SingleOrDefault();

            return Json(new { DispatchDetail, TestingData, ProductionData, Company,PDISignature, AccSignature },JsonRequestBehavior.AllowGet);
        }
        public ActionResult BillingPDIDetails(string engineSerialNumber)
        {
            int ewap = Convert.ToInt32(engineSerialNumber);
            var PDIStatus = 0;
            var ewapid = 0;
            var Status = DB.BOMEwapDetails.Where(x => x.id == ewap && x.PDIStatus == 1).SingleOrDefault();
            if (Status == null)
            {
                PDIStatus = 0;
                return Json(new { success = false, PDIStatus, message = "PDI is Pending for This Case" });
            }
            else
            {
                var ProductionData = (from ab in DB.Bom_ProductionUpdate
                                      where ab.BOMHeadid == Status.BomVoucherHeadID
                                      select new
                                      {
                                          emake = ab.EngineMake,
                                          emodel = ab.EngineModel,
                                          engineSerialNo = ab.EngineSerialNumber,
                                          etestcert = ab.EngineTestCert,
                                          eremarks = ab.EngineRemarks,
                                          amake = ab.AlternatorMake,
                                          aframe = ab.AlternatorFrame,
                                          alternatorSerialNo = ab.AlternatorSerialNo,
                                          atestcert = ab.AlternatorTestCert,
                                          awarranty = ab.Alternatorwarranty,
                                          aremarks = ab.AlternatorRemarks,
                                          exhaust = ab.ExhaustPipe,
                                          kgsticker = ab.KGSticker,
                                          AVM = ab.AVMPads,
                                          fuelspout = ab.FuelSpout,
                                          flexbel = ab.FlexibleBellow,
                                      }).SingleOrDefault();

                var TestingData = (from bc in DB.BOM_TestingUpdate
                                   where bc.BOMHeadid == Status.BomVoucherHeadID
                                   select new
                                   {
                                       CPtype = bc.CPType,
                                       cprating = bc.CPRating,
                                       PanelSerialNo = bc.CPSerialNumber,
                                       cpotherdetail = bc.CPRemarks,
                                       bmake = bc.BTMake,
                                       BatteryRating = bc.BTRating,
                                       Batteryqty = bc.BTQty,
                                       BatterySerialNo = bc.BTSerial,
                                       dateOfTesting = bc.TestingDate,
                                       ratingOfGenset = bc.GensetRating,
                                       canopy = bc.CanopyQty,
                                       KRMNo = bc.KRMNo,
                                       adblue = bc.ADBlue,
                                       lube = bc.LubeOil,
                                       kcool = bc.KCool,
                                       specialRemarks = bc.SpecialRemarks,

                                   }).SingleOrDefault();
                PDIStatus = 1;
                ewapid = Status.id;
                return Json(new { success = true, ProductionData, TestingData, PDIStatus, ewapid }, JsonRequestBehavior.AllowGet);

            }

        }
        [HttpGet]
        public JsonResult GetSerialNumbers(int voucherId)
        {
            try
            {
                // Fetch all lines for this voucher that have an assigned LotID
                var assignedLots = (from line in DB.BOMVoucherlines
                                    join lot in DB.Stock_lotDetail on line.LotID equals lot.id
                                    join item in DB.BOMItemMasters on line.rawitemid equals item.Itemid
                                    join grp in DB.ItemGroups on item.ItemGroup equals grp.id
                                    where line.BOMVoucherid == voucherId && line.Isdeleted == 0
                                    select new
                                    {
                                        item.ItemName,
                                        lot.Lot_SerialNumber, // This is the Serial Number/Lot Number
                                        grp.GroupName
                                    }).ToList();

                // Differentiate between Engine and Alternator based on ItemName keywords
                // You can also use ItemGroupID or CategoryID if preferred
                var engineSerial = assignedLots
                    .FirstOrDefault(x => x.GroupName.Contains("Engine"))?.Lot_SerialNumber ?? "";//x => x.ItemName.ToUpper().Contains("ENGINE"))?.Lot_SerialNumber ?? "";

                var alternatorSerial = assignedLots
                    .FirstOrDefault(x => x.GroupName.Contains("Alternator"))?.Lot_SerialNumber ?? "";//(x => x.ItemName.ToUpper().Contains("ALTERNATOR"))?.Lot_SerialNumber ?? "";

                if (string.IsNullOrEmpty(engineSerial) && string.IsNullOrEmpty(alternatorSerial))
                {
                    return Json(null, JsonRequestBehavior.AllowGet);
                }

                return Json(new
                {
                    engineserial = engineSerial,
                    alternatorserial = alternatorSerial
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                // Log error
                return Json(null, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpPost]
        public ActionResult UpdatePDIForm(
            EngineData engineData,
            Alternator alternatorData,
            OtherData otherData,
            TestingData testingData)
        {
            if (ModelState.IsValid)
            {



                var existingEngineData = DB.Bom_ProductionUpdate.FirstOrDefault(e => e.BOMHeadid == engineData.VoucherId);

                existingEngineData.EngineMake = engineData.emake;
                existingEngineData.EngineModel = engineData.emodel;
                existingEngineData.EngineSerialNumber = engineData.eserial;
                existingEngineData.EngineTestCert = engineData.etestcert;
                existingEngineData.EngineRemarks = engineData.eremarks;
                existingEngineData.AlternatorMake = alternatorData.amake;
                existingEngineData.AlternatorFrame = alternatorData.aframe;
                existingEngineData.AlternatorSerialNo = alternatorData.amcNo;
                existingEngineData.AlternatorRating = alternatorData.arating;
                existingEngineData.AlternatorPhase = alternatorData.aphase;
                existingEngineData.AlternatorTestCert = alternatorData.atestCert;
                existingEngineData.Alternatorwarranty = alternatorData.awarranty;
                existingEngineData.AlternatorRemarks = alternatorData.aremarks;
                existingEngineData.ExhaustPipe = otherData.exhaust;
                existingEngineData.KGSticker = otherData.kgSticker;
                existingEngineData.AVMPads = otherData.avmPads;
                existingEngineData.FuelSpout = otherData.fuelSpout;
                existingEngineData.FlexibleBellow = otherData.flexBellow;
                DB.SaveChanges();

             


                var existingTestingData = DB.BOM_TestingUpdate.FirstOrDefault(e => e.BOMHeadid == engineData.VoucherId);
                existingTestingData.CPType = testingData.CPtype;
                existingTestingData.CPRating = testingData.CPRating;
                existingTestingData.CPSerialNumber = testingData.PanelSerialNo;
                existingTestingData.CPRemarks = testingData.CPOtherDetail;
                existingTestingData.BTMake = testingData.BMake;
                existingTestingData.BTRating = testingData.BatteryRating;
                existingTestingData.BTQty = testingData.BatteryQTY;
                existingTestingData.BTSerial = testingData.BatterySerialNo;
                existingTestingData.TestingDate = testingData.DateOfTesting;
                existingTestingData.GensetRating = testingData.RatingOfGenset;
                existingTestingData.CanopyQty = testingData.Canopy;
                existingTestingData.LubeOil = testingData.Lube;
                existingTestingData.KCool = testingData.KCool;
                existingTestingData.KRMNo = testingData.KRMNo;
                existingTestingData.ADBlue = testingData.ADBlue;
                existingTestingData.SpecialRemarks = testingData.SpecialRemarks;
                DB.SaveChanges();

                //Now Update Status in Ewap Detail Table and Also in Bom Voucher Table
                var bom = DB.BOMVouchers.Where(x=>x.BOMVoucherID== engineData.VoucherId).SingleOrDefault();
                bom.PDIStatus = 1;
                bom.PDIBy = Session["U_Name"].ToString();
                bom.PDIon = System.DateTime.Now;
                //Also Update Data in Ewap Old Table
                var ewap = DB.BOMEwapDetails.Where(x => x.BomVoucherHeadID == engineData.VoucherId).SingleOrDefault();
                ewap.EngineSerialNumber = engineData.eserial;
                ewap.Model = engineData.emodel;
                ewap.GensetRating= Convert.ToString(testingData.RatingOfGenset);
                ewap.PanelSerialNo = testingData.PanelSerialNo;
                ewap.KRMNo= testingData.KRMNo;
                ewap.BatteryRating= testingData.BatteryRating;
                ewap.BatterySerialNo= testingData.BatterySerialNo;
                ewap.BatteryQuantity = testingData.BatteryQTY;
                ewap.AlternatorSerialNumber = alternatorData.amcNo;
                ewap.PDIStatus = 1;
                ewap.PDIBy= Session["U_Name"].ToString();
                ewap.PDIOn = System.DateTime.Now;
                DB.SaveChanges();

            }
            return Json(new { success = true, message = "PDI Data Updated..." });
        }
        public ActionResult GetPDIFormData(int Headid)
        {
            var ProductionStatus = DB.Bom_ProductionUpdate.Where(x => x.BOMHeadid == Headid).SingleOrDefault();
            if(ProductionStatus==null)
            {
                return Json(new { success = false, Message = "Production Data Not Available" }, JsonRequestBehavior.AllowGet);
            }

            var TestingStatus=DB.BOM_TestingUpdate.Where(x => x.BOMHeadid == Headid).SingleOrDefault();
            if (TestingStatus == null)
            {
                return Json(new { success = false, Message = "Testing Data Not Available" }, JsonRequestBehavior.AllowGet);
            }
            //Else Get All Data and send back to Ajax
            var ProductionData = (from ab in DB.Bom_ProductionUpdate
                                  where ab.BOMHeadid == Headid
                                  select new
                                  {
                                      emake = ab.EngineMake,
                                      emodel = ab.EngineModel,
                                      engineSerialNo = ab.EngineSerialNumber,
                                      etestcert = ab.EngineTestCert,
                                      eremarks = ab.EngineRemarks,
                                      amake = ab.AlternatorMake,
                                      aframe = ab.AlternatorFrame,
                                      alternatorSerialNo = ab.AlternatorSerialNo,
                                      aphase=ab.AlternatorPhase ?? 0,
                                      arating=ab.AlternatorRating ?? 0,
                                      atestcert = ab.AlternatorTestCert,
                                      awarranty = ab.Alternatorwarranty,
                                      aremarks = ab.AlternatorRemarks,
                                      exhaust = ab.ExhaustPipe,
                                      kgsticker = ab.KGSticker,
                                      AVM = ab.AVMPads,
                                      fuelspout = ab.FuelSpout,
                                      flexbel = ab.FlexibleBellow,
                                  }).SingleOrDefault();

            var TestingData = (from bc in DB.BOM_TestingUpdate
                                  where bc.BOMHeadid == Headid
                                  select new
                                  {
                                      CPtype = bc.CPType,
                                      cprating = bc.CPRating,
                                      PanelSerialNo = bc.CPSerialNumber,
                                      cpotherdetail = bc.CPRemarks,
                                      bmake = bc.BTMake,
                                      BatteryRating = bc.BTRating,
                                      Batteryqty = bc.BTQty,
                                      BatterySerialNo = bc.BTSerial,
                                      dateOfTesting = bc.TestingDate,
                                      ratingOfGenset = bc.GensetRating,
                                      canopy = bc.CanopyQty,
                                      KRMNo = bc.KRMNo,
                                      adblue = bc.ADBlue,
                                      lube = bc.LubeOil,
                                      kcool = bc.KCool,
                                      specialRemarks = bc.SpecialRemarks,

                                  }).SingleOrDefault();

            return Json(new { success = true, ProductionData, TestingData }, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public ActionResult SaveTestingData(TestingData data)
        {
            var check = DB.BOM_TestingUpdate.Where(x => x.BOMHeadid == data.Id).SingleOrDefault();
            if(check!=null)
            {
                return Json(new { success = false });
            }
            
                BOM_TestingUpdate BTU = new BOM_TestingUpdate();
                BTU.BOMHeadid = data.Id;
                BTU.CPType=data.CPtype;
                BTU.CPRating=data.CPRating;
                BTU.CPSerialNumber=data.PanelSerialNo;
                BTU.CPRemarks = data.CPOtherDetail;
                BTU.BTMake=data.BMake;
                BTU.BTRating=data.BatteryRating;
                BTU.BTQty = data.BatteryQTY;
                BTU.BTSerial = data.BatterySerialNo;
                //BTU.ErectionMaterial = data.Erection;
               // BTU.ErectionDetail=data.ErectionDetail; 
                BTU.TestingDate=data.DateOfTesting;
                BTU.GensetRating=data.RatingOfGenset;
                BTU.CanopyQty = data.Canopy;
                BTU.LubeOil=data.Lube;
                BTU.KCool=data.KCool;
                BTU.KRMNo=data.KRMNo;
                BTU.ADBlue=data.ADBlue;
                BTU.SpecialRemarks=data.SpecialRemarks;
                BTU.CreatedBy= Session["U_Name"].ToString();
                BTU.Createdon=System.DateTime.Now;
                DB.BOM_TestingUpdate.Add(BTU);
                DB.SaveChanges();

            return Json(new { success = true, message = "Testing Detail Saved.." }, JsonRequestBehavior.AllowGet);

        }
        public ActionResult GetRatings()
        {
            var rating = (from ab in DB.Ratings_Production
                          select new
                          {
                              value=ab.id,
                              text=ab.Rating,
                          }).ToList();
            return Json(new { success = true, rating }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetAlternatorPhase()
        {
            var phase = (from ab in DB.Phases
                         where ab.Active == 1
                         select new
                         {
                             value = ab.PhaseId,
                             text = ab.PhaseDesc,
                         }).ToList();
            return Json(new { success = true, phase }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetCPType()
        {
            var type = (from ab in DB.ControlPanelTypes
                        select new
                        {
                            value = ab.id,
                            text = ab.Type,
                        }).ToList();
            return Json(new { success = true, type }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetProdutionUpdate(int BOMID)
        {
            var Getdata = DB.Bom_ProductionUpdate.Where(x => x.BOMHeadid == BOMID).SingleOrDefault();
            //Get Battery Details
            // 1. Fetch all lots linked to this BOM
            var assignedLots = (from line in DB.BOMVoucherlines
                                join lot in DB.Stock_lotDetail on line.LotID equals lot.id into lotJoin
                                from lot in lotJoin.DefaultIfEmpty() // This makes it a Left Join
                                join item in DB.BOMItemMasters on line.rawitemid equals item.Itemid
                                join grp in DB.ItemGroups on item.ItemGroup equals grp.id
                                where line.BOMVoucherid == BOMID && line.Isdeleted == 0
                                select new
                                {
                                    item.ItemName,
                                    // If lot is null, we show "Manual/Pending", otherwise the serial
                                    SerialNumber = lot != null ? lot.Lot_SerialNumber : "Not Assigned",
                                    grp.GroupName
                                }).ToList();

            // 2. Filter specifically for Battery group items
            var batteryItems = assignedLots
                .Where(x => x.GroupName.ToLower().Contains("battery"))
                .ToList();

            // 3. Calculate Count and Join Serials
            int batteryCount = batteryItems.Count;
            var actualSerials = batteryItems
                .Where(x => x.SerialNumber != "Not Assigned")
                .Select(x => x.SerialNumber)
                .ToList();

            // 2. Determine if the field should be editable
            // If no serials were found in the lots, we allow manual editing
            bool canEditBattery = !actualSerials.Any();

            string batterySerialsFormatted = actualSerials.Any()
                ? string.Join(" / ", actualSerials)
                : "";


            if (Getdata == null)
            {
                return Json(new { success = false, message = "No Production Data Found.." }, JsonRequestBehavior.AllowGet);
            }
            else
            {
                var data = new
                {
                    emake = Getdata.EngineMake,
                    emodel = Getdata.EngineModel,
                    engineSerialNo = Getdata.EngineSerialNumber,
                    etestcert = Getdata.EngineTestCert,
                    eremarks = Getdata.EngineRemarks,
                    amake = Getdata.AlternatorMake,
                    aframe = Getdata.AlternatorFrame,
                    alternatorSerialNo = Getdata.AlternatorSerialNo,
                    atestcert = Getdata.AlternatorTestCert,
                    awarranty = Getdata.Alternatorwarranty,
                    aremarks = Getdata.AlternatorRemarks,
                    exhaust = Getdata.ExhaustPipe,
                    kgsticker = Getdata.KGSticker,
                    AVM = Getdata.AVMPads,
                    fuelspout = Getdata.FuelSpout,
                    flexbel = Getdata.FlexibleBellow,
                    aphase=Getdata.AlternatorPhase ?? 0,
                    arating=Getdata.AlternatorRating ?? 0,
                    batterySerial = batterySerialsFormatted,
                    batteryQty = batteryCount,
                    isBatteryEditable = canEditBattery
                };
                return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public ActionResult SaveCompletionDetails(int voucherId,
                                                 EngineData engine,
                                                 Alternator alternator,
                                                 OtherData other)
        {
            // 1.  Validate the data (Important!)
            if (voucherId <= 0)
            {
                return Json(new { success = false, message = "Voucher ID is missing." });
            }

            // 1.5 Validate engine data
            if (engine == null || string.IsNullOrEmpty(engine.emake) || engine.emodel == 0 || string.IsNullOrEmpty(engine.eserial) || string.IsNullOrEmpty(engine.etestcert))
            {
                return Json(new { success = false, message = "Engine Data is not valid." });
            }

            if (alternator == null || string.IsNullOrEmpty(alternator.amake) || string.IsNullOrEmpty(alternator.aframe) || string.IsNullOrEmpty(alternator.amcNo))
            {
                return Json(new { success = false, message = "Alternator Data is not valid." });
            }
            if (other == null) // Add null check
            {
                return Json(new { success = false, message = "Other Data is not valid." });
            }

            try
            {
                // 4.  Update/Insert data using Entity Framework and LINQ
                var existingdata = DB.Bom_ProductionUpdate.Where(x => x.BOMHeadid == voucherId).SingleOrDefault();
                if (existingdata != null)
                {
                    return Json(new { success = false, message = "Production Details are Already Updated.." });
                }

                // Guard: if ProductionCompletionStatus was set by the old ApproveRecord() path
                // without saving Bom_ProductionUpdate, reset the flag so we can proceed normally.
                var voucherCheck = DB.BOMVouchers.Find(voucherId);
                if (voucherCheck != null && voucherCheck.ProductionCompletionStatus == 1)
                {
                    voucherCheck.ProductionCompletionStatus = 0;
                    DB.SaveChanges();
                }

                //Else if Data Not Found

                Bom_ProductionUpdate BPU = new Bom_ProductionUpdate();

                BPU.BOMHeadid = voucherId;
                BPU.EngineMake = engine.emake;
                BPU.EngineModel = engine.emodel;
                BPU.EngineSerialNumber = engine.eserial;
                BPU.EngineTestCert = engine.etestcert;
                BPU.EngineRemarks = engine.eremarks;
                BPU.AlternatorMake = alternator.amake;
                BPU.AlternatorFrame = alternator.aframe;
                BPU.AlternatorSerialNo = alternator.amcNo;
                BPU.AlternatorTestCert = alternator.atestCert;
                BPU.Alternatorwarranty = alternator.awarranty;
                BPU.AlternatorRemarks = alternator.aremarks;
                BPU.AlternatorPhase=alternator.aphase;
                BPU.AlternatorRating = alternator.arating;
                BPU.ExhaustPipe = other.exhaust;
                BPU.KGSticker = other.kgSticker;
                BPU.AVMPads = other.avmPads;
                BPU.FuelSpout = other.fuelSpout;
                BPU.FlexibleBellow = other.flexBellow;
                BPU.CreatedBy = Session["U_Name"].ToString();
                BPU.CreatedOn = System.DateTime.Now;
                DB.Bom_ProductionUpdate.Add(BPU);
                DB.SaveChanges();

                //Now Update Production Status as Previous code
                var record = DB.BOMVouchers.Find(voucherId);

                // 2. Update the record's status (e.g., set an "Approved" flag)
                record.ProductionCompletionStatus = 1;
                record.ProductionCompletionon = System.DateTime.Now;
                DB.SaveChanges();

                return Json(new { success = true, message = "Voucher details saved successfully.", voucherId = voucherId });
            }
            
            catch (Exception ex)
            {
                // 7.  Rollback the transaction on error
                // transaction.Rollback();
                // 8.  Log the error (Important!)
                Console.WriteLine("Error saving voucher details: " + ex.ToString());
                return Json(new { success = false, message = "Error saving voucher details: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
                
            //}
        }
        public ActionResult ReturnBOMApprovalHead(int BOMID)
        {
            var FinYear = Session["Fin_Year"].ToString();
            int CompanyID = (int)Session["Company_ID"];
            //GET Return BOM Data
            var RBOM = (from ab in DB.DispatchReturnBOMHeads
                        where ab.id == BOMID
                        select ab).SingleOrDefault();

            BOMVoucher BV = new BOMVoucher();

            // Get Bill Series
            var GetNumber = (from ab in DB.Bill_Series
                             where ab.Type == "BOM" && ab.CompanyID == CompanyID && ab.Fin_Year == FinYear
                             select ab).SingleOrDefault();
            //Get FG Product ID
            
            //  Generate VoucherNumber (adjust logic if needed)
            var VoucherNumber = GetNumber.Series + GetNumber.Number;

            BV.VoucherNumber = VoucherNumber;
            BV.VoucherDate = System.DateTime.Now;
            BV.Approvalstatus = 0;
            BV.CreatedBy = Session["U_Name"].ToString();
            BV.ApprovedBY = "NA";
            BV.DispatchStatus = 0;
            BV.FGProductID = RBOM.FGItemID;
            BV.Isdeleted = 0;
            BV.CompanyID = CompanyID;
            BV.Finyear = FinYear;
            BV.AllowLotCheck = true;
            BV.AllowNegativeTransaction= false;
            DB.BOMVouchers.Add(BV);
            DB.SaveChanges();
           
            

            var Lines = (from bb in DB.DispatchReturnBOMLines
                         join im in DB.BOMItemMasters on bb.RawitemID equals im.Itemid
                         join uom in DB.BOM_UOM on im.UOM equals uom.id // Join with BOM_UOM
                         where bb.HeadID == RBOM.id
                         select new // Create a new anonymous object to include UOM
                         {
                             
                             bb.CategoryID,
                             bb.SubcategoryID,
                             bb.MasterItemID,
                             bb.RawitemID,
                             bb.Quantity,
                             uom.UOM // Select UOM from BOM_UOM
                         }).ToList();
            // Save BOM Voucher Lines
            foreach (var item in Lines)
            {
                BOMVoucherline BVL = new BOMVoucherline();
                BVL.BOMVoucherid = BV.BOMVoucherID;                
                BVL.Categoryid = item.CategoryID;
                BVL.Subcategoryid = item.SubcategoryID;
                BVL.Finalitemid = item.MasterItemID;
                BVL.rawitemid = item.RawitemID;
                BVL.QuantityRequired = item.Quantity; // No need to multiply by totalQuantity
                BVL.UOM = item.UOM;
                BVL.Stockapproved = 0;
                BVL.Isdeleted = 0;
                DB.BOMVoucherlines.Add(BVL);
            }

            DB.SaveChanges();

            // Update BOM Series (Important: Update after successful save)
            GetNumber.Number++;
            //Now Update Status in Return Tables
            RBOM.HeadApproalStatus = 1;
            RBOM.HeadApprovalDate = System.DateTime.Now;
            RBOM.HeadApprovedBy= Session["U_Name"].ToString();
            RBOM.BOMID = BV.BOMVoucherID;
            //Now Update in Retun table
            var ret=(from hh in DB.DispatchReturns
                    where hh.id==RBOM.DispatchReturnID
                    select hh).SingleOrDefault();
            ret.HeadStatus = 1;
            ret.HeadDate = System.DateTime.Now;

            //Now need to Minus Rejected Stock 
            var stocktable = (from stk in DB.StockTables
                              where stk.itemid == RBOM.FGItemID && stk.CompanyID == CompanyID
                              select stk).SingleOrDefault();
            stocktable.RejectedStock-= 1;
            DB.SaveChanges();

            return Json(new { success = true, Voucher = BV.VoucherNumber }, JsonRequestBehavior.AllowGet);

            //Add BOM in BOM Table


        }
        public ActionResult GetReturnBOMLines(int BOMID)
        {
            var data = (from ab in DB.DispatchReturnBOMLines
                        join im in DB.BOMItemMasters on ab.RawitemID equals im.Itemid
                        join uom in DB.BOM_UOM on im.UOM equals uom.id
                        join cat in DB.BOMCategories on ab.CategoryID equals cat.CategoryID into catJoin
                        from cat in catJoin.DefaultIfEmpty() // Left join for Categories
                        join sub in DB.BOMSubcategories on ab.SubcategoryID equals sub.id into subJoin
                        from sub in subJoin.DefaultIfEmpty() // Left join for Subcategories
                        where ab.HeadID == BOMID && ab.IsDeleted==0
                        select new
                        {
                            ItemCode = im.ItemCode,
                            ItemName = im.ItemName,
                            Category = cat != null ? cat.CategoryDesc : "NA", // Handle null Categories
                            SubCategory = sub != null ? sub.BOMSubCategory1 : "NA", // Handle null Subcategories
                            Quantity = ab.Quantity,
                            UOM = uom.UOM,
                        }).ToList();
            return Json(new { success = true, data }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult PendingBOMReturnforApproval()
        {
            var data = (from ab in DB.DispatchReturns
                        join rbom in DB.DispatchReturnBOMHeads on ab.id equals rbom.DispatchReturnID 
                        join im in DB.BOMItemMasters on rbom.FGItemID equals im.Itemid 
                        where ab.QualityStatus == 1 && ab.ProductionStatus == 1 && ab.HeadStatus == 0
                        where rbom.IsDeleted==0
                        select new
                        {
                            ReturnBOMNo = rbom.VoucherNumber,
                            SaleReturnNo=ab.VoucherNumber,
                            FGItemCode=im.ItemCode,
                            FGItemName=im.ItemName,
                            SaleReturnDate=ab.ReturnDate,
                            BOMCreatedBy=rbom.CreatedBy,
                            SaleRemarks=ab.Remarks,
                            QCRemarks=ab.QualityRemarks,
                            id=rbom.id,
                        }).ToList();
            return Json(new { success = true, data }, JsonRequestBehavior.AllowGet);
        }
       
        public ActionResult FGReturnPendingQC()
        {
            //first filter the pending QC Data 
            var data = (from ab in DB.DispatchReturns
                        join ewap in DB.BOMEwapDetails on ab.Ewapid equals ewap.id
                        join dis in DB.DispatchDetails on ab.Dispatchid equals dis.id
                        where ab.QualityStatus == 0
                        select new
                        {
                            id=ab.id,
                            VoucherNumber = ab.VoucherNumber,
                            DispatchNumber = dis.VoucherNumber,
                            BillNumber = dis.BillNumber,
                            BillDate = dis.BillDate,
                            ReturnDate = ab.ReturnDate,
                            Remarks = ab.Remarks,
                            Createdby = ab.Createdby,
                            
                        }).ToList();
           

            return Json(data, JsonRequestBehavior.AllowGet);

        }
        public ActionResult FGReturnPendingProd()
        {
            //first filter the pending QC Data 
            var data = (from ab in DB.DispatchReturns
                        join ewap in DB.BOMEwapDetails on ab.Ewapid equals ewap.id
                        join dis in DB.DispatchDetails on ab.Dispatchid equals dis.id
                        where ab.QualityStatus == 1 && ab.ProductionStatus==0
                        select new
                        {
                            id = ab.id,
                            VoucherNumber = ab.VoucherNumber,
                            DispatchNumber = dis.VoucherNumber,
                            BillNumber = dis.BillNumber,
                            BillDate = dis.BillDate,
                            ReturnDate = ab.ReturnDate,
                            Remarks = ab.Remarks,
                            QCRemarks = ab.QualityRemarks,

                        }).ToList();


            return Json(data, JsonRequestBehavior.AllowGet);

        }

        public ActionResult SaveQCRemarksFGReturn(int id, string remarks)
        {
            try
            {
                var line = DB.DispatchReturns.Where(x => x.id == id).FirstOrDefault();
                if (line != null)
                {
                    line.QualityRemarks = remarks;
                    line.QualityDate = DateTime.Now;
                    line.QualityStatus = 1;
                }
                else
                {
                    return Json(new { success = false }, JsonRequestBehavior.AllowGet);
                }
                DB.SaveChanges();
                return Json(new { success = true }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex); // Or use a logging framework like NLog or Serilog
                return Json(new { success = false, msg = "An error occurred." }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpGet]
        public JsonResult SearchInvoiceHistory(string term)
        {
            // Queries DispatchDetails to find previous matches by name or invoice
            var history = DB.DispatchDetails.Where(x => x.CustomerName.Contains(term) || x.BillNumber.Contains(term))
                            .Select(x => new {
                                label = x.BillNumber + " | " + x.CustomerName,
                                CustomerName = x.CustomerName,
                                GST = x.CustomerGST,
                                Location = x.CustomerLocation,
                                Shipping = x.ShippingAddress,
                                Billing = x.CustomerAddress
                            }).Take(10).ToList();
            return Json(history, JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetDispatchedSerialNumbers(string itemCode)
        {
            var itemid = DB.BOMItemMasters.Where(x => x.ItemCode == itemCode).Select(x=> x.Itemid).FirstOrDefault();
            var list = (from ab in DB.BOMEwapDetails
                       where ab.ItemID==itemid && ab.DispatchStatus==1 && ab.ReturnStatus==0
                       select new
                       {
                           value=ab.id,
                           text=ab.EngineSerialNumber,
                       }).ToList();
            return Json(new {list},JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetDispatchDetails(int ewapid)
        {
            var detail = DB.DispatchDetails.Where(x => x.Ewapid == ewapid).ToList(); // Use ToList()
            var ewap = DB.BOMEwapDetails.Where(y => y.id == ewapid).ToList(); // Use ToList()
            

            if (detail.Count != 1 || ewap.Count != 1)
            {
                // Handle the error: either no records or multiple records found
                if (detail.Count == 0 || ewap.Count == 0)
                {
                    return Json(new {success=false, error = "Record not found" }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = false, error = "Multiple records found" }, JsonRequestBehavior.AllowGet);
                }
            }

            var singleDetail = detail.FirstOrDefault();
            var singleEwap = ewap.FirstOrDefault();
            var model = DB.EngineModels.Where(z => z.EngineId == singleEwap.Model).Select(z => z.EngineModel1).SingleOrDefault();
            var batteryRating = DB.BatteryMasters.Where(a => a.id == singleEwap.BatteryRating).Select(a => a.BatteryName).SingleOrDefault();

            var data = new
            {
                voucherNumber= singleDetail.VoucherNumber,
                voucherDate= singleDetail.VoucherDate,
                customerName= singleDetail.CustomerName,
                customerAddress= singleDetail.CustomerAddress,
                dateoftesting= singleEwap.Dateoftesting,
                model=model,
                alternatorSerialNumber=singleEwap.AlternatorSerialNumber,
                gensetRating=singleEwap.GensetRating,
                panelSerialNo=singleEwap.PanelSerialNo,
                krmNo=singleEwap.KRMNo,
                batteryRating=batteryRating,
                batterySerialNo=singleEwap.BatterySerialNo,
                batteryQuantity=singleEwap.BatteryQuantity,
                dispatchid=singleDetail.id,

        };
            return Json(new { success = true, data=data }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult SubmitDispatchReturn(string remarks, int ewapid, int dispatchid, string returndate)
        {
            try
            {
                int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
                var finyear = Session["Fin_Year"].ToString();
                //Get Dispatch Return Number
                var billseries = (from ab in DB.Bill_Series
                                  where ab.Type == "DispatchReturn" && ab.CompanyID == companyid && ab.Fin_Year == finyear
                                  select ab).SingleOrDefault();

                
                //Update in Dispatch Table
                var Dispatch = DB.DispatchDetails.Where(x => x.id == dispatchid).SingleOrDefault();
                Dispatch.DispatchReturn = 1;
              

                //Upate Status in Ewap Table
                var ewap = DB.BOMEwapDetails.Where(y => y.id == ewapid).SingleOrDefault();
                ewap.ReturnStatus = 1;
                

                //Update Return Data
                //DispatchReturn
                DispatchReturn DR = new DispatchReturn();
                DR.Dispatchid = dispatchid;
                DR.Ewapid = ewapid;
                DR.Createdon = DateTime.Now;
                DR.Createdby = Session["U_Name"].ToString();
                DR.Remarks = remarks;
                DR.QualityStatus = 0;
                DR.ProductionStatus = 0;
                DR.HeadStatus = 0;
                DR.VoucherNumber = billseries.Series+billseries.Number;
                DR.ReturnDate = Convert.ToDateTime(returndate);
                DR.FGProductID = Dispatch.FGItemCode;
                DR.FinYear = finyear;
                DR.CompanyID = companyid;
                DB.DispatchReturns.Add(DR);
                

                //update Bill Series
                billseries.Number += 1;

                //update Rejected Stock of FG item
                var stock = DB.StockTables.Where(d => d.itemid == ewap.ItemID && d.CompanyID == companyid).SingleOrDefault();
                stock.RejectedStock += 1;
                DB.SaveChanges();
                SendDispatchReturnEmail((DR.id).ToString());
                return Json(new { success = true , data=DR.VoucherNumber}, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex); // Or use a logging framework like NLog or Serilog
                return Json(new { success = false, msg = "An error occurred." }, JsonRequestBehavior.AllowGet);
            }

        }
        public ActionResult GetEwapRecords()
        {
            var data = DB.BOMEwapDetails.Where(a => a.DispatchStatus == 0).Select(a => a.BomVoucherHeadID).ToList();
            var voucherNumbers = DB.BOMVouchers
                       .Where(h => data.Contains(h.BOMVoucherID))
                       .Select(h => new
                       {
                           value = h.BOMVoucherID, // Assuming you want ID as value
                           text = h.VoucherNumber
                       }).ToList();
            return Json(new { success = true, voucherNumbers }, JsonRequestBehavior.AllowGet);

        }
        public ActionResult GetApprovedBOM()
        {
            var get = (from ab in DB.BOMVouchers
                       join item in DB.BOMItemMasters on ab.FGProductID equals item.Itemid
                       where ab.Ewap == 0 && ab.Approvalstatus == 1 && ab.ProductionCompletionStatus == 1 && ab.Isdeleted==0
                       //Check is there any Requisition pending Cases that not to invlove in this list
                       && !DB.BOMRequisitionHeads.Any(req => req.BOMVoucherID == ab.BOMVoucherID && req.IsApproved == 0)
                       select new
                       {
                           value = ab.BOMVoucherID,
                           text = ab.VoucherNumber,                          

                       }).ToList();
            return Json(new { get }, JsonRequestBehavior.AllowGet);

        }
        public ActionResult ReverseApprovedBOM()
        {
            return View();
        }

        public ActionResult GetBOMReversaldataApproved(int voucherid)
        {
            var head = (from ab in DB.BOMVouchers                        
                        join FG in DB.BOMItemMasters on ab.FGProductID equals FG.Itemid into FGs
                        from FG in FGs.DefaultIfEmpty()                        
                        where ab.BOMVoucherID == voucherid
                        select new
                        {
                            id = ab.BOMVoucherID,
                            vouchernumber = ab.VoucherNumber,
                            Voucherdate=ab.VoucherDate,
                            CreatedBy=ab.CreatedBy,
                            ApprovedBy=ab.ApprovedBY,
                            FGCode=FG.ItemCode,

                        }).SingleOrDefault();

            return Json(new { success = true, head }, JsonRequestBehavior.AllowGet);

        }
        public ActionResult GetBOMReversaldata(int voucherid)
        {
            var head = (from ab in DB.BOMVouchers
                        join detail in DB.BOMEwapDetails on ab.BOMVoucherID equals detail.BomVoucherHeadID into details
                        from detail in details.DefaultIfEmpty()
                        join FG in DB.BOMItemMasters on detail.ItemID equals FG.Itemid into FGs
                        from FG in FGs.DefaultIfEmpty()
                        join model in DB.EngineModels on detail.Model equals model.EngineId into models
                        from model in models.DefaultIfEmpty()
                        join bat in DB.BatteryMasters on detail.BatteryRating equals bat.id into bats
                        from bat in bats.DefaultIfEmpty()
                        where ab.BOMVoucherID == voucherid
                        select new
                        {
                            id = ab.BOMVoucherID,
                            vouchernumber = ab.VoucherNumber,
                            testingdate = detail != null ? detail.Dateoftesting : (DateTime?)null,
                            EngineSerial = detail != null ? detail.EngineSerialNumber : null,
                            model = model != null ? model.EngineModel1 : null,
                            alterantorserial = detail != null ? detail.AlternatorSerialNumber : null,
                            panelserial = detail != null ? detail.PanelSerialNo : null,
                            KRM = detail != null ? detail.KRMNo : null,
                            BatteryRating = bat != null ? bat.BatteryName : null,
                            Batteryserial = detail != null ? detail.BatterySerialNo : null,
                            BatteryQTY = detail != null ? detail.BatteryQuantity : (int?)null,
                            FGCode = FG != null ? FG.ItemCode : null,

                        }).SingleOrDefault();

            return Json(new { success = true, head }, JsonRequestBehavior.AllowGet);

        }
        public ActionResult RejectProductionData(int headid)
        {
            var Productiondata = DB.Bom_ProductionUpdate.Where(x => x.BOMHeadid == headid).SingleOrDefault();
            if(Productiondata!=null)
            {
                DB.Bom_ProductionUpdate.Remove(Productiondata);
                //Update Status in BOM Table
                var BOMLine = DB.BOMVouchers.Where(y => y.BOMVoucherID == headid).SingleOrDefault();
                BOMLine.ProductionCompletionStatus = 0;
                BOMLine.ProductionCompletionon = null;
                DB.SaveChanges();
                return Json(new { success = true, message = "Production Data Remeved.." }, JsonRequestBehavior.AllowGet);
            }
            else
            {
                return Json(new { success = false, message = "Production Data Not Found.." }, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult ReverseTestingData(int id)
        {
            try
            {
                int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
                var selectline = DB.BOMVouchers.Where(a => a.BOMVoucherID == id).SingleOrDefault();
                if (selectline != null)
                {
                    //Now Update its Lines
                    selectline.Ewap = 0;
                    selectline.EwapDate = null;
                    selectline.EwapBy = null;

                }
                else
                {
                    return Json(new { success = false, msg = "Problem Updating BOM Vouchers" }, JsonRequestBehavior.AllowGet);
                }
                // DB.SaveChanges();
                //Now Reverse Stock
                var stocktable = DB.StockTables.Where(b => b.itemid == selectline.FGProductID && b.CompanyID == companyid).SingleOrDefault();
                if (stocktable != null)
                {
                    stocktable.Stock -= 1;
                }
                else
                {
                    return Json(new { success = false, msg = "Problem Updating Stock Table" }, JsonRequestBehavior.AllowGet);
                }
                //now remove ewapdetails
                var detail = DB.BOMEwapDetails.Where(c => c.BomVoucherHeadID == id).SingleOrDefault();
                if (detail != null)
                {
                    DB.BOMEwapDetails.Remove(detail);
                }
                else
                {
                    return Json(new { success = false, msg = "Problem Deleting Ewap Details" }, JsonRequestBehavior.AllowGet);
                }
                var TestingRecord = DB.BOM_TestingUpdate.Where(d => d.BOMHeadid == id).SingleOrDefault();
                if(TestingRecord!=null)
                {
                    DB.BOM_TestingUpdate.Remove(TestingRecord);
                }
               // var productionrecord = DB.Bom_ProductionUpdate.Where(p => p.BOMHeadid == id).SingleOrDefault();
                //if(productionrecord!=null)
                //{
                //    DB.Bom_ProductionUpdate.Remove(productionrecord);

                 //   selectline.ProductionCompletionStatus = 0;
                 //   selectline.ProductionCompletionon = null;
               // }
                //Save whole Transaction
                DB.SaveChanges();
                return Json(new { success = true, msg = "Record Updated Successfully.." }, JsonRequestBehavior.AllowGet);

            }
            catch (Exception ex) 
            {
                Console.WriteLine(ex); // Or use a logging framework like NLog or Serilog
                return Json(new { success = false, msg = "An error occurred." }, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult ReverseBOMData(int id)
        {
            try
            {
                int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
                var selectline = DB.BOMVouchers.Where(a => a.BOMVoucherID == id).SingleOrDefault();
                if (selectline != null)
                {
                    //Now Update its Lines
                    selectline.Isdeleted = 1;
                    selectline.CancelledRemarks = "Admin Cancellation on Request";
                    selectline.CancelledBy = Session["U_Name"].ToString();
                    selectline.CancelledOn = DateTime.Now;
                }
                else
                {
                    return Json(new { success = false, msg = "Problem Updating BOM Vouchers" }, JsonRequestBehavior.AllowGet);
                }
                // DB.SaveChanges();
                //Now Select BOM voucher Lines

                var Lines = (from ab in DB.BOMVoucherlines
                             where ab.BOMVoucherid == id && ab.Isdeleted == 0 && ab.Approvedby != null
                             select new
                             {
                                 id = ab.id,
                                 rawitemid = ab.rawitemid,
                                 quantity = ab.ApprovedQuantity,
                             }).ToList();
                if (Lines != null)
                {
                    //update Stock
                    foreach (var su in Lines)
                    {
                        var stockEntity = DB.StockTables.FirstOrDefault(a => a.itemid == su.rawitemid && a.CompanyID == companyid); //Get the entity
                        var linestatus = DB.BOMVoucherlines.Where(b => b.id == su.id).SingleOrDefault();
                        if (stockEntity != null)
                        {
                            stockEntity.Stock += su.quantity; // Update the entity's Stock property
                            if (linestatus != null)
                            {
                                linestatus.Isdeleted = 1;
                            }
                        }
                        else
                        {
                            return Json(new { success = false, msg = "Problem Updating Stock table" }, JsonRequestBehavior.AllowGet);
                        }
                        DB.SaveChanges();
                    }
                }
                // Now Mark Lines Deleted in List

                DB.SaveChanges();
                return Json(new { success = true, msg = "Record Updated Successfully.." }, JsonRequestBehavior.AllowGet);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex); // Or use a logging framework like NLog or Serilog
                return Json(new { success = false, msg = "An error occurred." }, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult GetPendingRecords()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var finyear = Session["Fin_Year"].ToString();

            var get = (from ab in DB.BOMVouchers
                       join item in DB.BOMItemMasters on ab.FGProductID equals item.Itemid
                       where ab.ProductionCompletionStatus == 0
                          && ab.Approvalstatus == 1
                          && ab.Isdeleted == 0
                          && ab.CompanyID == companyid          // fix: filter by company
                          && ab.Finyear == finyear              // fix: filter by financial year
                          && !DB.BOMRequisitionHeads.Any(req => req.BOMVoucherID == ab.BOMVoucherID && req.IsApproved == 0)
                       select new
                       {
                           id = ab.BOMVoucherID,
                           VoucherNumber = ab.VoucherNumber,
                           VoucherDate = ab.VoucherDate,
                           CreatedBy = ab.CreatedBy,
                           FGProductCode = item.ItemCode,
                           FGProductName = item.ItemName,
                       }).ToList();

            return Json(new { get }, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public JsonResult ApproveRecord(int id)
        {
            try
            {
                // 1. Find the record in your database using the 'id'
                var record = DB.BOMVouchers.Find(id);

                // 2. Update the record's status (e.g., set an "Approved" flag)
                record.ProductionCompletionStatus = 1;
                record.ProductionCompletionon = System.DateTime.Now;
                DB.SaveChanges();

                return Json(new { success = true, message = "Record approved successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error approving record: " + ex.Message });
            }
        }

        public ActionResult PendingPDIRecords()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var finyear = Session["Fin_Year"].ToString();
            var get = (from ab in DB.BOMEwapDetails
                       join bh in DB.BOMVouchers on ab.BomVoucherHeadID equals bh.BOMVoucherID
                       join item in DB.BOMItemMasters on ab.ItemID equals item.Itemid
                       where (ab.PDIStatus == 0 || ab.PDIStatus==1 && ab.DispatchStatus!=1) 
                       where ab.Companyid == companyid
                       select new
                       {
                           id=bh.BOMVoucherID,
                           VoucherNumber = bh.VoucherNumber,
                           VoucherDate = bh.VoucherDate,
                           CreatedBy = bh.CreatedBy,
                           FGProductCode = item.ItemCode,
                           FGProductName = item.ItemName,
                           EngineNumber = ab.EngineSerialNumber,
                           AlternatorNumber = ab.AlternatorSerialNumber,
                           Status=ab.PDIStatus
                       }).ToList();
            return Json(new { get }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult CompletedPDIRecords()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var finyear = Session["Fin_Year"].ToString();
            var get = (from ab in DB.BOMEwapDetails
                       join bh in DB.BOMVouchers on ab.BomVoucherHeadID equals bh.BOMVoucherID
                       join item in DB.BOMItemMasters on ab.ItemID equals item.Itemid
                       where ab.PDIStatus == 1 && ab.Companyid == companyid && ab.DispatchStatus==0
                       select new
                       {
                           id = bh.BOMVoucherID,
                           VoucherNumber = bh.VoucherNumber,
                           VoucherDate = bh.VoucherDate,
                           CreatedBy = bh.CreatedBy,
                           FGProductCode = item.ItemCode,
                           FGProductName = item.ItemName,
                           EngineNumber=ab.EngineSerialNumber,
                           AlternatorNumber=ab.AlternatorSerialNumber,
                       }).ToList();
            return Json(new { get }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult PendingEwapCases()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var finyear = Session["Fin_Year"].ToString();

            var get = (from ab in DB.BOMVouchers
                       join item in DB.BOMItemMasters on ab.FGProductID equals item.Itemid
                       where ab.Ewap == 0
                          && ab.Approvalstatus == 1
                          && ab.ProductionCompletionStatus == 1
                          && ab.Isdeleted == 0
                          && ab.CompanyID == companyid          // Bug 1 fix: filter by company
                          && ab.Finyear == finyear              // Bug 2 fix: filter by financial year
                          // Exclude BOMs with any pending (unapproved) requisition
                          && !DB.BOMRequisitionHeads.Any(req => req.BOMVoucherID == ab.BOMVoucherID && req.IsApproved == 0)
                       orderby ab.BOMVoucherID ascending
                       select new
                       {
                           id = ab.BOMVoucherID,
                           VoucherNumber = ab.VoucherNumber,
                           VoucherDate = ab.VoucherDate,
                           CreatedBy = ab.CreatedBy,
                           FGProductCode = item.ItemCode,
                           FGProductName = item.ItemName,
                       }).ToList();
            return Json(new { get }, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public JsonResult SaveEwapStatus(int id,string testingdate,string engineserial,int model,string alternatorserial,string rating,string remarks, string PanelSerialNo, string KRMNo,int BatteryRating, string BatterySerialNo, string Batteryqty)
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var finyear = Session["Fin_Year"].ToString();
            try
            {
                //Also need to update detail in BOM Vouchers
                var voucher = (from ab in DB.BOMVouchers
                               where ab.BOMVoucherID == id
                               select ab).SingleOrDefault();
                voucher.Ewap = 1;
                voucher.EwapDate=System.DateTime.Now;
                voucher.EwapBy = Session["U_Name"].ToString();

                //Add Detail in Status

                BOMEwapDetail BED = new BOMEwapDetail();
                BED.Createdon = DateTime.Now;
                BED.BomVoucherHeadID = id;
                BED.AlternatorSerialNumber = alternatorserial;
                BED.Remarks = remarks;
                BED.Model = model;
                BED.EngineSerialNumber = engineserial;
                BED.GensetRating = rating;
                BED.Dateoftesting = Convert.ToDateTime(testingdate);
                BED.PanelSerialNo = PanelSerialNo;
                BED.KRMNo = KRMNo;
                BED.BatteryRating = BatteryRating;
                BED.BatterySerialNo = BatterySerialNo;
                BED.BatteryQuantity = Convert.ToInt32(Batteryqty);
                BED.Quantity = 1;
                BED.Createdby = Session["U_Name"].ToString();
                BED.ItemID = voucher.FGProductID;
                BED.DispatchStatus = 0;
                BED.ReturnStatus = 0;
                BED.Companyid = companyid;
                BED.FinYear = finyear;
                BED.PDIStatus = 0;
                DB.BOMEwapDetails.Add(BED);
                //Now we also have to create Stock for the item
                var stock = (from bb in DB.StockTables
                            where bb.itemid==voucher.FGProductID && bb.CompanyID==companyid
                            select bb).SingleOrDefault();
                stock.Stock += BED.Quantity;
                DB.SaveChanges();


                return Json(new { success = true, msg="Data Updated Succesfully" },JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                // Log the exception
                return Json(new { success = false, error = ex.Message });
            }
        }
        public JsonResult GetEngineSerialNumbers(int itemId)
        {
            try
            {
                // 1. Get the engine serial numbers based on itemId
                var engineSerialNumbers = (from ab in DB.BOMEwapDetails
                                          where ab.ItemID==itemId && ab.DispatchStatus == 0
                                          select new
                                          {
                                              Value=ab.id,
                                              Engine = ab.EngineSerialNumber,
                                          }).ToList(); // Replace with your data access logic

                // 2. Return the serial numbers as a JSON array
                return Json(engineSerialNumbers, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                // Log the exception
               // Console.error("Error in GetEngineSerialNumbers:", ex);

                // Return an error response
                return Json(new { error = "Failed to fetch engine serial numbers." }, JsonRequestBehavior.AllowGet);
            }
        }
        public JsonResult GetAlternatorSerialNumber(string engineSerialNumber)
        {
            try
            {
                // 1. Get the alternator serial number based on the engineSerialNumber
                var alternatorSerialNumber = (from al in DB.BOMEwapDetails
                                              join mo in DB.EngineModels on al.Model equals mo.EngineId into moGroup
                                              from mo in moGroup.DefaultIfEmpty()
                                              join bt in DB.BatteryMasters on al.BatteryRating equals bt.id into btGroup
                                              from bt in btGroup.DefaultIfEmpty()
                                              where al.EngineSerialNumber == engineSerialNumber && al.DispatchStatus == 0
                                              select new
                                              {
                                                  alternator = al.AlternatorSerialNumber,
                                                  panel = al.PanelSerialNo,
                                                  batteryserial = al.BatterySerialNo,
                                                  KRM = al.KRMNo,
                                                  testingdate = al.Dateoftesting,
                                                  model = mo != null ? mo.EngineModel1 : null, // Use null if no match in EngineModels
                                                  battery = bt != null ? bt.BatteryName : null,
                                                  batteryid = al.BatteryRating,
                                                  id=al.id,// Use null if no match in BatteryMasters
                                              }).SingleOrDefault();

               

                // 2. Return the alternator serial number as JSON
                return Json(new { Alternator = alternatorSerialNumber }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                // Log the exception
               // Console.error("Error in GetAlternatorSerialNumber:", ex);

                // Return an error response
                return Json(new { error = "Failed to fetch alternator serial number." }, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult GetEngineModel()
        {
            // 1. Fetch Engine Models from your data source
            //    (e.g., database, API, or static list)
            var engineModels = (from ab in DB.EngineModels
                                select new
                                {
                                    value = ab.EngineId, // Assuming your model has an 'Id' property
                                    text = ab.EngineModel1,
                                });

            // 3. Return JSON data
            return Json(engineModels, JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetBatteriesModel()
        {
            // 1. Fetch Battery Models from your data source
            var batteryModels = from ab in DB.BatteryMasters
                                select new
                                {
                                    value = ab.id, // Assuming your model has an 'Id' property
                                    text = ab.BatteryName
                                };            

            // 3. Return JSON data
            return Json(batteryModels, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult SaveDispatchData(
                  string dispatchVoucherNumber,
                  string dispatchDate,
                  string customerName,
                  string customerGST,
                  string customerLocation,
                  string shippingAddress,
                  string billingAddress,
                  string fgItemCode,
                  string billdate,
                  int id,
                  string ordernumber,
                  string orderdate,
                  string BillNumber,
                  string basicprice,
                  string gst,
                  string totalamt,
                  string billingamount,
                  string frieght,
                  string transport,
                  string lr,
                  string lrdate,
                  string lorryno,
                  string eway,
                  string erection,
                  string erectiondetail,
                  string taxid,
                  string taxtypeid,
                  string cgst,
                  string sgst,
                  string roundoff
            )
        {
            try
            {

                //Get Voucher Number 


                //first Save Dispatch Detail in Dispatch Table
                int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
                var finyear = Session["Fin_Year"].ToString();

                var voucher = (from ab in DB.Bill_Series
                               where ab.Type == "Dispatch" && ab.CompanyID == companyid && ab.Fin_Year == finyear
                               select new
                               {
                                   vouchernumber = ab.Series + ab.Number,
                               }).SingleOrDefault();
               // var Biltyno = (from bb in DB.Bill_Series
                               //where bb.Type == "Bilty" && bb.CompanyID == companyid && bb.Fin_Year == finyear
                              // select new
                              // {
                              //     biltynumber = bb.Series + bb.Number,
                              // }).SingleOrDefault();

                DispatchDetail DD = new DispatchDetail();
                DD.BillDate = Convert.ToDateTime(billdate);
                DD.Ewapid = Convert.ToInt32(id);
                DD.VoucherNumber = voucher.vouchernumber;
                DD.CustomerName = customerName;
                DD.CustomerGST = customerGST;
                DD.VoucherDate = Convert.ToDateTime(dispatchDate);
                DD.OrderNumber = ordernumber;
                DD.OrderDate = Convert.ToDateTime(orderdate);
                DD.BillNumber = BillNumber;
                DD.BillDate = Convert.ToDateTime(billdate);
                DD.CustomerAddress = billingAddress;
                DD.CustomerName = customerName;
                DD.CustomerLocation = customerLocation;
                DD.ShippingAddress = shippingAddress;
                DD.DispatchReturn = 0;
                DD.CompanyID = companyid;
                DD.FinYear = finyear;
                DD.BasicPrice = Convert.ToDecimal(basicprice);
                DD.GST = Convert.ToDecimal(gst);
                DD.FrieghtAMT = Convert.ToDecimal(frieght);
                DD.TotalAMT = Convert.ToDecimal(totalamt);
                DD.BillingAMT = Convert.ToDecimal(billingamount);
                DD.Transport = transport;
                DD.LR = lr;
                DD.LRDate = Convert.ToDateTime(lrdate);
                DD.LorryNo = lorryno;
                DD.EwayNo = eway;
                DD.Erection = erection;
                DD.ErectionDetail = erectiondetail;
                DD.TaxID = Convert.ToInt32(taxid);
                DD.TaxTypeID = Convert.ToInt32(taxtypeid);
                DD.RoundOff = Convert.ToDecimal(roundoff);
                DD.CGST = Convert.ToDecimal(cgst);
                DD.SGST = Convert.ToDecimal(sgst);

                if (DD.Transport == "IEPL")
                {
                    DD.Biltyno ="IEPL/B/26-27/"+ lr;
                }
                else
                {
                    DD.Biltyno = "NA";
                }

                var item = DB.BOMItemMasters.SingleOrDefault(ab => ab.ItemCode == fgItemCode);
                if (item != null)
                {
                    DD.FGItemCode = Convert.ToInt32(item.Itemid);
                }
                //DD.FGItemCode = DB.BOMItemMasters.SingleOrDefault(ab => ab.ItemCode == fgItemCode)?.Itemid;
                DD.Createdon = DateTime.Now;
                DD.CreatedBy = Session["U_Name"].ToString();
                DB.DispatchDetails.Add(DD);

                // also need to update status in Ewapdetail table

                var selectewapline = (from ab in DB.BOMEwapDetails
                                      where ab.id == id
                                      select ab).SingleOrDefault();
                if (selectewapline != null)
                {
                    selectewapline.DispatchStatus = 1;
                    selectewapline.Dispatchon = Convert.ToDateTime(billdate);

                }
                //Update Status in Bom Voucher Head
                var Selectvoucher = (from bb in DB.BOMVouchers
                                     where bb.BOMVoucherID == selectewapline.BomVoucherHeadID
                                     select bb).SingleOrDefault();
                if (Selectvoucher != null)
                {
                    Selectvoucher.DispatchStatus = 1;
                    Selectvoucher.DispatchDate = Convert.ToDateTime(billdate);
                }
                // at last minus stock of FG item from Stock Table
                var selectstockline = (from ss in DB.StockTables
                                       where ss.itemid == selectewapline.ItemID && ss.CompanyID == companyid
                                       select ss).SingleOrDefault();

                if (selectstockline != null)
                {
                    selectstockline.Stock -= selectewapline.Quantity;
                }

                //Update Bill Series

                // var finyear = Session["Fin_Year"].ToString();
                //int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
                // 1. Generate the dispatch voucher number
                var voucherNumber = (from ab in DB.Bill_Series
                                     where ab.Type == "Dispatch" && ab.CompanyID == companyid && ab.Fin_Year == finyear
                                     select ab).SingleOrDefault();

                voucherNumber.Number += 1;
               // if (DD.Transport == "IEPL")
                //{
                   // var biltyNumber = (from bb in DB.Bill_Series
                                     //  where bb.Type == "bilty" && bb.CompanyID == companyid && bb.Fin_Year == finyear
                                     //  select bb).SingleOrDefault();

                 //   biltyNumber.Number += 1;
               // }

                //Send New Voucher Number to Page

                // 1. Generate the dispatch voucher number

                DB.SaveChanges();
                SendDispatchEmail((DD.id).ToString());
                var voucherNumbernew = (from ab in DB.Bill_Series
                                        where ab.Type == "Dispatch" && ab.CompanyID == companyid && ab.Fin_Year == finyear
                                        select new
                                        {
                                            vouchernumber = ab.Series + ab.Number,
                                        }).SingleOrDefault();
                int BOMID = (int)DB.BOMEwapDetails.Where(x => x.id == id).Select(x => x.BomVoucherHeadID).SingleOrDefault();

                return Json(new { success = true, voucherNumbernew = voucherNumbernew, BOMID, message = "Dispatch order saved successfully!" });
            }
            catch (Exception ex)
            {
                // Log the exception
                // Console.error(ex);
                return Json(new { success = false, message = "An error occurred." });
            }
        }
        public ActionResult PrintTestCertificate(int id)
        {
            // Pass the ID to the view so the AJAX can fetch data
            ViewBag.id = id;
            return View();
        }
        public ActionResult Loadbilty(int id, string type)
        {
            ViewBag.id = id;
            ViewBag.type = type;
            return View();
        }
        [HttpGet]
        public JsonResult GetDetails(int id, string type)
        {
            try
            {
                int companyId = Convert.ToInt32(Session["Company_ID"]);

                if (type == "Challan")
                {
                    // 1. Fetch Data from Stock Issue / Challan Tables
                    var challanResult = (from h in DB.IEPLStockIssueHeads
                                         join co in DB.Companies on h.CompanyID equals co.CompanyID
                                         where h.id == id && h.CompanyID == companyId
                                         select new
                                         {
                                             h.VoucherNumber,
                                             h.VoucherDate,
                                             h.Createdon,
                                             h.VehicleNo,
                                             h.CustomerName,
                                             h.Address,        // Shipping Address
                                             h.BillingAddress, // New Field
                                             h.ContactNo,
                                             h.TransportName,
                                             h.IsBilty,
                                             h.BiltyNo,
                                             h.BiltyDate,
                                             h.BiltyTotalValue,
                                             h.BiltyDescription,                                             
                                             co.CompanyName,
                                             co.CompanyAddress,
                                             co.GST
                                         }).FirstOrDefault();

                    if (challanResult != null)
                    {
                        var data = new
                        {
                            success = true,
                            // Bilty Specifics
                            consignmentNo = challanResult.BiltyNo ?? "NA",
                            date = challanResult.BiltyDate != null ? challanResult.BiltyDate.Value.ToString("dd/MM/yyyy") : challanResult.VoucherDate.Value.ToString("dd/MM/yyyy"),
                            time = challanResult.Createdon != null ? challanResult.Createdon.Value.ToString("hh:mm tt") : "",
                            vehicleNo = challanResult.VehicleNo,

                            // Locations
                            fromLoc = "KATHUA",
                            toLoc = "AS PER ADDRESS",

                            // Consignor (Company)
                            consignorName = $"{challanResult.CompanyName}, {challanResult.CompanyAddress}",
                            consignorGSTN = challanResult.GST,

                            // Consignee (Customer)
                            consigneeName = challanResult.Address,
                            consigneeAddress = challanResult.Address,
                            consigneeBillingAddress = challanResult.BillingAddress,
                            consigneeContact = challanResult.ContactNo,

                            // Cargo Details
                            transport = challanResult.TransportName ?? "NA",
                            description = challanResult.BiltyDescription ?? "GOODS AS PER CHALLAN",
                            billdetail = $"Challan No - {challanResult.VoucherNumber} Dated - {challanResult.VoucherDate.Value.ToString("dd/MM/yyyy")}",

                            value = challanResult.BiltyTotalValue ?? 0,
                            payableAmount = challanResult.BiltyTotalValue ?? 0,
                            weight = "As per Challan"
                        };
                        return Json(data, JsonRequestBehavior.AllowGet);
                    }
                }
                else if (type == "Sale")
                {
                    // --- YOUR EXISTING SALE LOGIC ---
                    var dbResult = (from dd in DB.DispatchDetails
                                    join co in DB.Companies on dd.CompanyID equals co.CompanyID
                                    join ew in DB.BOMEwapDetails on dd.Ewapid equals ew.id
                                    join te in DB.BOM_TestingUpdate on ew.BomVoucherHeadID equals te.BOMHeadid
                                    join pe in DB.Bom_ProductionUpdate on ew.BomVoucherHeadID equals pe.BOMHeadid
                                    join rt in DB.Ratings_Production on te.GensetRating equals rt.id
                                    join em in DB.EngineModels on pe.EngineModel equals em.EngineId
                                    join art in DB.Ratings_Production on pe.AlternatorRating equals art.id
                                    join user in DB.tbl_User_Master on te.CreatedBy equals user.user_name
                                    where (ew.BomVoucherHeadID == id) && dd.CompanyID == companyId
                                    select new
                                    {
                                        dd.VoucherNumber,
                                        dd.VoucherDate,
                                        dd.Createdon,
                                        dd.LorryNo,
                                        dd.CustomerLocation,
                                        dd.CustomerName,
                                        dd.ShippingAddress,
                                        dd.CustomerGST,
                                        dd.BillNumber,
                                        dd.BillDate,
                                        dd.Biltyno,
                                        co.CompanyName,
                                        co.CompanyAddress,
                                        co.GST,
                                        rt.Rating,
                                        dd.BillingAMT,
                                        em.EngineModel1,
                                        pe.EngineSerialNumber,
                                        pe.AlternatorSerialNo,
                                        pe.AlternatorMake,
                                        altrating = art.Rating,
                                        sign = user.SignURL,
                                        DgRating=rt.Rating,
                                    }).FirstOrDefault();

                    if (dbResult != null)
                    {
                        var data = new
                        {
                            success = true,
                            consignmentNo = dbResult.Biltyno ?? "NA",
                            billnumber = dbResult.BillNumber,
                            billdate = dbResult.BillDate != null ? dbResult.BillDate.Value.ToString("dd/MM/yyyy") : "",
                            date = dbResult.VoucherDate != null ? dbResult.VoucherDate.Value.ToString("dd/MM/yyyy") : "",
                            time = dbResult.Createdon != null ? dbResult.Createdon.Value.ToString("hh:mm tt") : "",
                            vehicleNo = dbResult.LorryNo,
                            fromLoc = "KATHUA",
                            toLoc = dbResult.CustomerLocation,
                            consignorName = $"{dbResult.CompanyName} {dbResult.CompanyAddress}",
                            consignorGSTN = dbResult.GST,
                            consigneeName = dbResult.ShippingAddress,
                            consigneeGSTN = dbResult.CustomerGST,
                            sign = dbResult.sign,
                            ratingKVA = dbResult.Rating,
                            DgRating=dbResult.DgRating,
                            altrating=dbResult.altrating,
                            engineModel = dbResult.EngineModel1 ?? "---",
                            engineSerial = dbResult.EngineSerialNumber ?? "---",
                            altSerial = dbResult.AlternatorSerialNo ?? "---",
                            description = $"ELECTRIC GENERATING SET - {dbResult.Rating} KVA",
                            billdetail = $"Bill No - {dbResult.BillNumber ?? "NA"} Dated - {(dbResult.BillDate != null ? dbResult.BillDate.Value.ToString("dd/MM/yyyy") : "NA")}",
                            value = dbResult.BillingAMT,
                            payableAmount = dbResult.BillingAMT
                        };
                        return Json(data, JsonRequestBehavior.AllowGet);
                    }
                }

                return Json(new { success = false, message = "No data found for the selected type." }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpGet]
        public JsonResult GetDetails1(int id)
        {
            try
            {
                // 1. Fetch the data from the database into memory first
                var dbResult = (from dd in DB.DispatchDetails
                                join co in DB.Companies on dd.CompanyID equals co.CompanyID
                                join ew in DB.BOMEwapDetails on dd.Ewapid equals ew.id
                                join te in DB.BOM_TestingUpdate on ew.BomVoucherHeadID equals te.BOMHeadid
                                join rt in DB.Ratings_Production on te.GensetRating equals rt.id
                                
                                where ew.BomVoucherHeadID == id || dd.Ewapid == id
                                select new
                                {
                                    dd.VoucherNumber,
                                    dd.VoucherDate,
                                    dd.Createdon,
                                    dd.LorryNo,
                                    dd.CustomerLocation,
                                    dd.CustomerName,
                                    dd.ShippingAddress,
                                    dd.CustomerGST,
                                    dd.BillNumber,
                                    dd.BillDate,
                                    dd.OrderNumber,
                                    dd.BasicPrice,
                                    dd.FrieghtAMT,
                                    dd.TotalAMT,
                                    co.CompanyName,
                                    co.CompanyAddress,
                                    co.CompanyAddress2,
                                    co.GST,
                                    rt.Rating,
                                    co.DeliveryDestination,
                                    dd.Biltyno,
                                }).FirstOrDefault();

                if (dbResult != null)
                {
                    // 2. Perform the formatting in memory (C# logic)
                    var data = new
                    {
                        consignmentNo = dbResult.Biltyno,
                        billnumber=dbResult.BillNumber,
                        billdate=dbResult.BillDate !=null ? dbResult.BillDate.Value.ToString("dd/MM/yyyy"): "",
                        // Formatting is safe here because it's no longer part of the SQL translation
                        date = dbResult.VoucherDate != null ? dbResult.VoucherDate.Value.ToString("dd/MM/yyyy") : "",
                        time = dbResult.Createdon != null ? dbResult.Createdon.Value.ToString("hh:mm tt") : "",
                        vehicleNo = dbResult.LorryNo,
                        fromLoc = "KATHUA",
                        toLoc = dbResult.CustomerLocation,
                        consignorName = dbResult.CompanyName +" "+ dbResult.CompanyAddress + " " + dbResult.DeliveryDestination,
                        consignorGSTN = dbResult.GST,
                        consigneeName = dbResult.ShippingAddress,
                        consigneeGSTN = dbResult.CustomerGST,
                        description = "ELECTRIC GENERATING SET - "+dbResult.Rating+" KVA | QUANTITY - 1 Nos" ,
                        billdetail = $"Bill No - {dbResult.BillNumber ?? "NA"} Dated - {(dbResult.VoucherDate != null ? dbResult.VoucherDate.Value.ToString("dd/MM/yyyy") : "NA")}",
                    value = dbResult.TotalAMT,
                        weight = "",
                       // freight = dbResult.FrieghtAMT,
                       // grCharges = 0.00,
                       // loadingCharges = 0.00,
                       // tollTax = 0.00,
                       // advance = 0.00,
                        payableAmount = dbResult.TotalAMT
                    };

                    return Json(data, JsonRequestBehavior.AllowGet);
                }

                return Json(new { success = false, message = "No dispatch data found." }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult CancelPackingSlip()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "CancelPackingSlip" && ab.Status == true
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
        public JsonResult GetActiveDispatchVouchers()
        {
            try
            {
                int companyId = Convert.ToInt32(Session["Company_ID"]);
                var finyear = Session["Fin_Year"].ToString();
                // Fetch list of active dispatches
                var list = (from dd in DB.DispatchDetails
                            where dd.CompanyID == companyId && dd.FinYear==finyear
                            orderby dd.id descending
                            select new
                            {
                                Value = dd.VoucherNumber,
                                Text = dd.VoucherNumber + " | " + dd.CustomerName + " | Bill: " + dd.BillNumber
                            }).ToList();

                return Json(list, JsonRequestBehavior.AllowGet);
            }
            catch (Exception)
            {
                return Json(new List<object>(), JsonRequestBehavior.AllowGet);
            }
        }
        [HttpGet]
        public ActionResult GetDispatchReturnDetails(int ewapid)
        {
            var detail = DB.DispatchDetails.Where(x => x.Ewapid == ewapid).ToList(); // Use ToList()
            var ewap = DB.BOMEwapDetails.Where(y => y.id == ewapid).ToList(); // Use ToList()


            if (detail.Count != 1 || ewap.Count != 1)
            {
                // Handle the error: either no records or multiple records found
                if (detail.Count == 0 || ewap.Count == 0)
                {
                    return Json(new { success = false, error = "Record not found" }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = false, error = "Multiple records found" }, JsonRequestBehavior.AllowGet);
                }
            }

            var singleDetail = detail.FirstOrDefault();
            var singleEwap = ewap.FirstOrDefault();
            var model = DB.EngineModels.Where(z => z.EngineId == singleEwap.Model).Select(z => z.EngineModel1).SingleOrDefault();
            var batteryRating = DB.BatteryMasters.Where(a => a.id == singleEwap.BatteryRating).Select(a => a.BatteryName).SingleOrDefault();

            var data = new
            {
                voucherNumber = singleDetail.VoucherNumber,
                voucherDate = singleDetail.VoucherDate,
                customerName = singleDetail.CustomerName,
                customerAddress = singleDetail.CustomerAddress,
                dateoftesting = singleEwap.Dateoftesting,
                model = model,
                alternatorSerialNumber = singleEwap.AlternatorSerialNumber,
                gensetRating = singleEwap.GensetRating,
                panelSerialNo = singleEwap.PanelSerialNo,
                krmNo = singleEwap.KRMNo,
                batteryRating = batteryRating,
                batterySerialNo = singleEwap.BatterySerialNo,
                batteryQuantity = singleEwap.BatteryQuantity,
                dispatchid = singleDetail.id,

            };
            return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet);
        }
        [HttpGet]
        public JsonResult GetDispatchDetails(string voucherNo)
        {
            try
            {
                int companyId = Convert.ToInt32(Session["Company_ID"]);
                var detail = (from dd in DB.DispatchDetails
                              join item in DB.BOMItemMasters on dd.FGItemCode equals item.Itemid
                              where dd.VoucherNumber == voucherNo && dd.CompanyID == companyId
                              select new
                              {
                                  dd.VoucherNumber,
                                  VoucherDate = dd.VoucherDate,
                                  dd.CustomerName,
                                  dd.CustomerLocation,
                                  dd.CustomerAddress,
                                  dd.ShippingAddress,
                                  dd.BillNumber,
                                  BillDate = dd.BillDate,
                                  dd.LorryNo,
                                  dd.LR,
                                  dd.LRDate,
                                  dd.EwayNo,
                                  dd.Transport,
                                  dd.Biltyno,
                                  ItemName = item.ItemName,
                                  dd.BillingAMT
                              }).FirstOrDefault();

                if (detail == null) return Json(new { success = false, message = "Not found" }, JsonRequestBehavior.AllowGet);

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        detail.VoucherNumber,
                        VoucherDate = detail.VoucherDate.HasValue ? detail.VoucherDate.Value.ToString("yyyy-MM-dd") : "",
                        detail.CustomerName,
                        detail.CustomerLocation,
                        detail.CustomerAddress,
                        detail.ShippingAddress,
                        detail.BillNumber,
                        BillDate = detail.BillDate.HasValue ? detail.BillDate.Value.ToString("yyyy-MM-dd") : "",
                        detail.LorryNo,
                        detail.LR,
                        LRDate = detail.LRDate.HasValue ? detail.LRDate.Value.ToString("yyyy-MM-dd") : "",
                        detail.EwayNo,
                        detail.Transport,
                        detail.Biltyno,
                        detail.ItemName,
                        detail.BillingAMT
                    }
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet); }
        }
        [HttpPost]
        public ActionResult ProcessCancellation(string voucherNo)
        {
            // Use a database transaction to ensure all reverts happen together
            using (var transaction = DB.Database.BeginTransaction())
            {
                try
                {
                    if (Session["Company_ID"] == null)
                        return Json(new { success = false, message = "Session expired." });

                    int companyId = Convert.ToInt32(Session["Company_ID"]);
                    string userName = Session["U_Name"]?.ToString() ?? "System";

                    // 1. Locate the Dispatch Detail record
                    var dispatch = DB.DispatchDetails.FirstOrDefault(x => x.VoucherNumber == voucherNo && x.CompanyID == companyId);
                    if (dispatch == null)
                    {
                        return Json(new { success = false, message = "Dispatch record not found." });
                    }

                    // 2. Identify and Reset the Production Line status (BOMEwapDetails)
                    var ewapLine = DB.BOMEwapDetails.FirstOrDefault(x => x.id == dispatch.Ewapid);
                    if (ewapLine != null)
                    {
                        // Reset production unit status to make it available for dispatch again
                        ewapLine.DispatchStatus = 0; 
                        ewapLine.Dispatchon = null;

                        // 3. Restore FG Stock in StockTable
                        var stock = DB.StockTables.FirstOrDefault(s => s.itemid == ewapLine.ItemID && s.CompanyID == companyId);
                        if (stock != null)
                        {
                            stock.Stock += ewapLine.Quantity;
                        }

                        // 4. Reset Status in main BOM Voucher Head
                        var voucher = DB.BOMVouchers.FirstOrDefault(v => v.BOMVoucherID == ewapLine.BomVoucherHeadID);
                        if (voucher != null)
                        {
                            voucher.DispatchStatus = 0;
                            voucher.DispatchDate = null;
                        }
                    }

                    // 5. PERMANENTLY remove the dispatch record from the database
                    DB.DispatchDetails.Remove(dispatch);

                    DB.SaveChanges();
                    transaction.Commit();

                    return Json(new { success = true, message = "Dispatch Order #" + voucherNo + " has been permanently removed. Stock and unit status have been successfully reverted." });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = "Cancellation failed: " + ex.Message });
                }
            }
        }
        [HttpPost]
        public JsonResult UpdateDispatchDetails(DispatchDetail model)
        {
            try
            {
                // 1. Session & Validation Check
                if (Session["Company_ID"] == null)
                    return Json(new { success = false, message = "Session expired. Please login again." });

                int companyId = Convert.ToInt32(Session["Company_ID"]);
                string finYear = Session["Fin_Year"].ToString();

                // 2. Locate the existing record (matching Voucher, Company, and FinYear for safety)
                var existing = DB.DispatchDetails.FirstOrDefault(x =>
                    x.VoucherNumber == model.VoucherNumber &&
                    x.CompanyID == companyId &&
                    x.FinYear == finYear);

                if (existing == null)
                    return Json(new { success = false, message = "Dispatch record not found." });

                // 3. Update MAIN COMPONENTS (The string-based fields)
                existing.CustomerName = model.CustomerName;
                existing.CustomerLocation = model.CustomerLocation;
                existing.CustomerAddress = model.CustomerAddress;
                existing.ShippingAddress = model.ShippingAddress;

                // 4. Update BILLING & LOGISTICS
                existing.BillNumber = model.BillNumber;
                existing.BillDate = model.BillDate;
                existing.LorryNo = model.LorryNo;
                existing.LR = model.LR;
                existing.LRDate = model.LRDate;
                existing.EwayNo = model.EwayNo;
                existing.Transport = model.Transport;
                if(model.Transport=="IEPL")
                {
                existing.Biltyno = "IEPL/B/26-27/"+model.LR;
                }
                //existing.Biltyno = model.Biltyno;

                // 5. Commit Changes
                DB.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = "Intelligence Update Complete: Dispatch #" + model.VoucherNumber + " has been updated."
                });
            }
            catch (Exception ex)
            {
                // Return detailed error if something fails during the SaveChanges
                return Json(new { success = false, message = "Update Failed: " + ex.Message });
            }
        }


        private void SendDispatchEmail(string id)
        {
            try
            {
                // 1. Fetch the necessary data 
                var dispatchDetail = DB.DispatchDetails.Find(Convert.ToInt32(id));

                if (dispatchDetail == null)
                {
                    // Handle the case where the dispatch detail is not found
                    return;
                }

                var ewapDetail = DB.BOMEwapDetails.Find(dispatchDetail.Ewapid);
                var engineModel = DB.EngineModels.Find(ewapDetail.Model);
               // var gensetItem = DB.BOMItemMasters.Find(Convert.ToInt32(ewapDetail.GensetRating));
                var gensetDetailItem = DB.BOMItemMasters.Find(ewapDetail.ItemID);

                // 2. Create the data model for the template
                var model = new
                {
                    VoucherNumber = dispatchDetail.VoucherNumber,
                    OrderNumber = dispatchDetail.OrderNumber,
                    OrderDate = dispatchDetail.OrderDate,
                    Billbumber = dispatchDetail.BillNumber,
                    BillDate = dispatchDetail.BillDate,
                    CustomerName = dispatchDetail.CustomerName,
                    CustomerLocation = dispatchDetail.CustomerLocation,
                    BillingAddress=dispatchDetail.CustomerAddress,
                    ShippingAddress=dispatchDetail.ShippingAddress,
                    Model = engineModel?.EngineModel1,
                    //Genset = gensetItem?.ItemName,
                    GensetDetail = gensetDetailItem?.ItemName,
                    EngineSerial = ewapDetail.EngineSerialNumber,
                    AlternatorSerial = ewapDetail.AlternatorSerialNumber,
                    PanelSerial = ewapDetail.PanelSerialNo,
                    BatterySerial = ewapDetail.BatterySerialNo,
                    BatteryQuantity = ewapDetail.BatteryQuantity,
                    KRMNo = ewapDetail.KRMNo,
                    GensetQuantity = ewapDetail.Quantity,
                    Speciality = ewapDetail.Remarks,
                    BasicPrice=dispatchDetail.BasicPrice,
                    GST=dispatchDetail.GST+dispatchDetail.CGST + dispatchDetail.SGST,
                    TotalAmt=dispatchDetail.TotalAMT,
                    BillingAmount=dispatchDetail.BillingAMT,
                    Frieght=dispatchDetail.FrieghtAMT,
                    CompanyLogo = GetCompanyLogoAsBase64(Server.MapPath("~/images/IEC.jpg")),
                };

                var email = (from ab in DB.CMKL_Email_Setting
                             where ab.id == 2
                             select ab).SingleOrDefault();
                var emailaddress = (from db in DB.CMKL_Email
                                    where db.DDLName == "ERPDispatch" && db.Active==1
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
                                    where db.DDLName == "ERPDispatchCC" // Example: Get CC emails
                                    select db).ToList();
                foreach (var ccRecipient in ccRecipients)
                {
                    mail.CC.Add(ccRecipient.Email);
                }
                mail.Subject = "Order Number -" + model.OrderNumber +" Has Been Dispatched";

                // 3. Get the Razor template
                var templatePath = Server.MapPath("~/Views/EmailManage/DispatchEmailTemplate.cshtml"); // Verify path
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
        private void SendDispatchReturnEmail(string id)
        {
            try
            {
                // 1. Fetch the necessary data 

                var dispatchreturn = DB.DispatchReturns.Find(Convert.ToInt32(id));
                var dispatchDetail = DB.DispatchDetails.Where(y => y.id == dispatchreturn.Dispatchid).SingleOrDefault();

                if (dispatchreturn == null)
                {
                    // Handle the case where the dispatch detail is not found
                    return;
                }

                var ewapDetail = DB.BOMEwapDetails.Find(dispatchreturn.Ewapid);
                var engineModel = DB.EngineModels.Find(ewapDetail.Model);
                // var gensetItem = DB.BOMItemMasters.Find(Convert.ToInt32(ewapDetail.GensetRating));
                var gensetDetailItem = DB.BOMItemMasters.Find(ewapDetail.ItemID);
                

                // 2. Create the data model for the template
                var model = new
                {
                    VoucherNumber = dispatchDetail.VoucherNumber,
                    OrderNumber = dispatchDetail.OrderNumber,
                    OrderDate = dispatchDetail.OrderDate,
                    Billbumber = dispatchDetail.BillNumber,
                    BillDate = dispatchDetail.BillDate,
                    CustomerName = dispatchDetail.CustomerName,
                    CustomerLocation = dispatchDetail.CustomerLocation,
                    BillingAddress = dispatchDetail.CustomerAddress,
                    ShippingAddress = dispatchDetail.ShippingAddress,
                    Model = engineModel?.EngineModel1,
                    //Genset = gensetItem?.ItemName,
                    GensetDetail = gensetDetailItem?.ItemName,
                    EngineSerial = ewapDetail.EngineSerialNumber,
                    AlternatorSerial = ewapDetail.AlternatorSerialNumber,
                    PanelSerial = ewapDetail.PanelSerialNo,
                    BatterySerial = ewapDetail.BatterySerialNo,
                    BatteryQuantity = ewapDetail.BatteryQuantity,
                    KRMNo = ewapDetail.KRMNo,
                    GensetQuantity = ewapDetail.Quantity,
                    Speciality = ewapDetail.Remarks,
                    ReturnRemarks=dispatchreturn.Remarks,
                    ReturnVoucherNumber=dispatchreturn.VoucherNumber,
                    ReturnDate=dispatchreturn.ReturnDate,
                    ReturnCreatedBy=dispatchreturn.Createdby,
                    ReturnCreatedon=dispatchreturn.Createdon,
                    CompanyLogo = GetCompanyLogoAsBase64(Server.MapPath("~/images/IEC.jpg")),
                };

                var email = (from ab in DB.CMKL_Email_Setting
                             where ab.id == 2
                             select ab).SingleOrDefault();
                var emailaddress = (from db in DB.CMKL_Email
                                    where db.DDLName == "ERPDispatchReturn"
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
                                    where db.DDLName == "ERPDispatchReturnCC" // Example: Get CC emails
                                    select db).ToList();
                foreach (var ccRecipient in ccRecipients)
                {
                    mail.CC.Add(ccRecipient.Email);
                }
                mail.Subject = "Sales Return Against Order Number -" + model.OrderNumber;

                // 3. Get the Razor template
                var templatePath = Server.MapPath("~/Views/EmailManage/FGReturnEmailTemplate.cshtml"); // Verify path
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
        private string GetCompanyLogoAsBase64(string imagePath)
        {
            string base64Image = Convert.ToBase64String(System.IO.File.ReadAllBytes(imagePath));
            return $"data:image/jpeg;base64,{base64Image}"; // Adjust MIME type if needed
        }
        public JsonResult GetDispatchNumber()
        {
            try
            {
                var finyear = Session["Fin_Year"].ToString();
                int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
                // 1. Generate the dispatch voucher number
                var voucherNumber = (from ab in DB.Bill_Series
                                     where ab.Type == "Dispatch" && ab.CompanyID == companyid && ab.Fin_Year == finyear
                                     select new
                                     {
                                         vouchernumber = ab.Series + ab.Number,
                                     }).SingleOrDefault()

                                    ; // Implement your logic here

                // 2. Return the voucher number as JSON
                return Json(new { vouchernumber = voucherNumber }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                // Log the exception
               // Console.error(ex);
                return Json(new { error = "An error occurred while generating the dispatch number." }, JsonRequestBehavior.AllowGet);
            }
        }


    }
}