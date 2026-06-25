using CMKL.Models;
using Microsoft.Ajax.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Web;
using System.Web.Mvc;
using System.Web.UI.WebControls;

namespace CMKL.Controllers
{

    public class OEController : Controller
    {
        IECEntities DB = new IECEntities();
        // GET: OE
        public ActionResult Authoriseddealerview()
        {

            string userid = Session["Userid"].ToString();
            int user = Convert.ToInt32(userid);

            var Data = (from ab in DB.Authorizeddealerviews
                        where ab.UserId== user
                        select ab).ToList();

            //ModelState.Clear();
            return View("Authoriseddealerview", Data);

        }
        public ActionResult DealerView()
        {

            var dealername = Session["DealerName"].ToString();

            var Data = (from ab in DB.Authorizeddealerviews
                        where ab.supplier_dealer == dealername
                        select ab).ToList();

            //ModelState.Clear();
            return View("DealerView", Data);

        }
        public ActionResult LogisticViewbranch()
        {

            //var dealername = Session["DealerName"].ToString();

            var Data = (from ab in DB.Authorizeddealerviews
                       // where ab.supplier_dealer == dealername
                        select ab).ToList();

            //ModelState.Clear();
            return View("LogisticApproval", Data);

        }
        public ActionResult LogisticViewdealer()
        {

            //var dealername = Session["DealerName"].ToString();

            var Data = (from ab in DB.Authorizeddealerviews
                            // where ab.supplier_dealer == dealername
                        select ab).ToList();

            //ModelState.Clear();
            return View("LogisticApproval", Data);

        }
        


        [HttpPost]
        public JsonResult GetItem(int id)
        {

            int balanceamountcal = 0;
            int Payments = 0;
            var advance = (from av in DB.OrderDetails
                           where (id) == (av.OrderId)
                           select av.AdvanceDetail).FirstOrDefault();
            int advanceamount = Convert.ToInt32(advance);

            var check = from ab in DB.PaymentsDealers
                        where id == ab.OAnumber
                        select ab;
            if (check.Any())
            {
                var paymentsum = (from ab in DB.PaymentsDealers
                                  where id == ab.OAnumber
                                  select ab.Amount).ToList().Sum();
                Payments = Convert.ToInt32(paymentsum);

            }
            else
            {
                Payments = 0;
            }

            var balance = (from ab in DB.OrderDetails
                           where id == ab.OrderId
                           select ab.EndCusOrderPricewithGST).SingleOrDefault();
            balanceamountcal = Convert.ToInt32(balance);




            //int total = 0;
            var getdataitemwise = (from ab in DB.OrderDetails
                                   join cd in DB.EngineModels on ab.EngineId equals cd.EngineId
                                   join fd in DB.Ratings on ab.RatingId equals fd.RateId



                                   where id == ab.OrderId
                                   select new OA
                                   {
                                       customername = ab.CustomerName,
                                       engine = cd.EngineModel1,
                                       rating = fd.RatingDesc,
                                       incentive = (int)ab.Billingincentive,
                                       basicpricekd = ab.BasicPrice,
                                       //advance = ab.AdvanceDetail,
                                       endcustomerorderprice = (int)ab.BKR, // use a nullable int type in class
                                       KDtotalamountwithGST = ab.TotalPrice,
                                       //totalamounttilldt = advanceamount + Payments,
                                       totalamounttilldt = Payments,
                                       endcustomerorderpricewithGST = ab.EndCusOrderPricewithGST,
                                       //balanceamount = balanceamountcal - (advanceamount + Payments),
                                       balanceamount = balanceamountcal - Payments,
                                       id = ab.OrderId,


                                   }).FirstOrDefault();


            return Json(getdataitemwise);
        }
        [HttpPost]
        public JsonResult GetDealer(int id)
        {

            int balanceamountcal = 0;
            int Payments = 0;
            int totalcal = 0;
            int Paymentsdealer = 0;
            var advance = (from av in DB.OrderDetails
                           where (id) == (av.OrderId)
                           select av.AdvanceDetail).FirstOrDefault();
            int advanceamount = Convert.ToInt32(advance);

            //Check Branch Payments To Dealer
            var check = from ab in DB.PaymentsDealers
                        where id == ab.OAnumber
                        select ab;
            if (check.Any())
            {
                var paymentsum = (from ab in DB.PaymentsDealers
                                  where id == ab.OAnumber
                                  select ab.Amount).ToList().Sum();
                Payments = Convert.ToInt32(paymentsum);

            }
            else
            {
                Payments = 0;
            }


            //Check Dealer Payments TO GOEM
            var checkdealerpayments = from ab in DB.PaymentCompanies
                        where id == ab.OAnumber
                        select ab;
            if (check.Any())
            {
                var paymentsumdealer = (from ab in DB.PaymentCompanies
                                  where id == ab.OAnumber
                                  select ab.Amount).ToList().Sum();
                Paymentsdealer = Convert.ToInt32(paymentsumdealer);

            }
            else
            {
                Paymentsdealer = 0;
            }

            var balance = (from ab in DB.OrderDetails
                           where id == ab.OrderId
                           select ab.EndCusOrderPricewithGST).SingleOrDefault();
            balanceamountcal = Convert.ToInt32(balance);
            var Totalprice = (from ab in DB.OrderDetails
                              where id == ab.OrderId
                              select ab.TotalPrice).SingleOrDefault();
            totalcal = Convert.ToInt32(balance);




            //int total = 0;
            var getdataitemwise = (from ab in DB.OrderDetails
                                   join cd in DB.EngineModels on ab.EngineId equals cd.EngineId
                                   join fd in DB.Ratings on ab.RatingId equals fd.RateId



                                   where id == ab.OrderId
                                   select new OA
                                   {

                                       customername = ab.CustomerName,
                                       engine = cd.EngineModel1,
                                       rating = fd.RatingDesc,
                                       incentive = (int)ab.Billingincentive,
                                       basicpricekd = ab.BasicPrice,
                                       //advance = ab.AdvanceDetail,
                                       endcustomerorderprice = (int)ab.BKR, // use a nullable int type in class
                                       KDtotalamountwithGST = ab.TotalPrice,
                                       KDtotalamounttogoem = totalcal - (int)ab.Billingincentive,

                                       //totalamounttilldt = advanceamount + Payments,
                                       totalamounttilldt = Payments,
                                       endcustomerorderpricewithGST = ab.EndCusOrderPricewithGST,
                                       //balanceamount = balanceamountcal - (advanceamount + Payments),
                                       balanceamount = balanceamountcal - Payments,
                                       id = ab.OrderId,
                                       pendingamounttransfer= (totalcal - (int)ab.Billingincentive) - Paymentsdealer,


                                   }).FirstOrDefault();


            return Json(getdataitemwise);
        }
        [HttpPost]
        public JsonResult updatepayment(int fid, string fpm, int fpr, string fdt, string frn, string fpt)
        
                   
        {
            var date = DateTime.Today.ToString("dd/MM/yyyy");
            var datefinal =date.Replace("-", "/");
            PaymentsDealer ad = new PaymentsDealer();
            ad.OAnumber = fid;
            ad.Mode = fpm;
            ad.Amount = fpr;
            ad.Date = fdt;
            ad.refno = frn;
            ad.paymenttype = fpt;
            ad.updateddt = datefinal;
            DB.PaymentsDealers.Add(ad);
            DB.SaveChanges();

            /*
            PaymentsDealer pd = new PaymentsDealer();

            //Advance Detail to Payment Table
            var detail = (from ab in DB.PaymentsDealers
                          where ab.OAnumber == fid && ab.Mode == "Advance"
                          select ab).ToList();
            if (detail.Count > 0)
            {

            }
            else
            {


                var advance = from ab in DB.OrderDetails
                              where ab.OrderId == fid
                              select ab;

                //select Order Date As Advance Date

                var dt = (from ab in DB.OrderDetails
                          where ab.OrderId == fid
                          select ab).FirstOrDefault();

                // PaymentsDealer PDN = new PaymentsDealer();

                string sql_date = Convert.ToString(dt.OrderDt); // get the SQL datetime value as a string
                DateTime datt = DateTime.ParseExact(sql_date, "dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);

                string newdate = datt.ToString("dd/MM/yyyy");//datt.ToString("dd/MM/yyyy");
                                                             
                string output = newdate.Replace("-", "/");//Change dd-MM-yyyy into dd/mm/yyyy

                foreach (var jj in advance)
                {

                    pd.OAnumber = jj.OrderId;
                    pd.Mode = "Advance";
                    pd.Amount = Convert.ToInt32(jj.AdvanceDetail);
                    pd.Date = output;
                    pd.refno = "Advance";
                    DB.PaymentsDealers.Add(pd);
                     }             
                }
            DB.SaveChanges();

            */


            TempData["message"] = "Payment Has Been Updated";

            return new JsonResult { Data = new { success = true, message = "Payment Has Been Updated" } };

        }
        [HttpPost]
        public JsonResult updatepaymentdealer(int fid, string fpm, int fpr, string fdt, string frn, string fpt, string fcp)
        {
            var date = DateTime.Today.ToString("dd/MM/yyyy");
            var datefinal = date.Replace("-", "/");
            PaymentCompany ad = new PaymentCompany();
            ad.OAnumber = fid;
            ad.Mode = fpm;
            ad.Amount = fpr;
            ad.Date = fdt;
            ad.refno = frn;
            //ad.paymenttype = fpt;
            ad.company= fcp;
            ad.updateddt = datefinal;
            DB.PaymentCompanies.Add(ad);
            DB.SaveChanges();
            


            TempData["message"] = "Payment Has Been Updated";

            return new JsonResult { Data = new { success = true, message = "Payment Has Been Updated" } };

        }
        public ActionResult LogisticView()
        {
            var data = (from ab in DB.PaymentViewCompanies
                        select ab).ToList();
            return View("LogisticView",data);
        }
        public ActionResult LogisticApproval()
        {
            var data = (from ab in DB.Authorizeddealerviews
                        select ab).ToList();
            return View("LogisticApproval", data);
        }
        


        public ActionResult paymentdetail(int id)
        {
             
                        var detail = (from ab in DB.PaymentsDealers
                         where ab.OAnumber == id
                         select ab).ToList();
            
            if (detail.Count > 0)
            {
                TempData["message"] = "Payments Recieved Against This Order";
                return View("BranchPayments",detail);
            
            }
            else
            {
                TempData["message"] = "Sorry No Payment Found Against This Order";
               
                return RedirectToAction("Authoriseddealerview");


            }

        }
        public ActionResult paymentdetaildealer(int id)
        {

            var detail = (from ab in DB.PaymentsDealers
                          where ab.OAnumber == id
                          select ab).ToList();

            if (detail.Count > 0)
            {
                TempData["message"] = "Payments Recieved Against This Order";
                return View("BranchPayments", detail);

            }
            else
            {
                TempData["message"] = "Sorry No Payment Found Against This Order";

                return RedirectToAction("DealerView");


            }

        }
        public ActionResult paymentdetaildealerlogistic(int id)
        {

            var detail = (from ab in DB.PaymentsDealers
                          where ab.OAnumber == id
                          select ab).ToList();

            if (detail.Count > 0)
            {
                TempData["message"] = "Payments Recieved Against This Order";
                return View("BranchPayments", detail);

            }
            else
            {
                TempData["message"] = "Sorry No Payment Found Against This Order";

                return RedirectToAction("LogisticViewbranch");


            }

        }

        
            public ActionResult paymentdetailtransferlogistic(int id)
        {

            var detail = (from ab in DB.PaymentCompanies
                          where ab.OAnumber == id
                          select ab).ToList();



            if (detail.Count > 0)
            {
                TempData["message"] = "Payments Recieved Against This Order";
                return View("DealerPayments", detail);

            }
            else
            {
                TempData["message"] = "Sorry No Payment Found Against This Order";

                return RedirectToAction("LogisticViewdealer");


            }

        }
        public ActionResult paymentdetailtransfer(int id)
        {

            var detail = (from ab in DB.PaymentCompanies
                          where ab.OAnumber == id
                          select ab).ToList();
           


            if (detail.Count > 0)
            {
                TempData["message"] = "Payments Recieved Against This Order";
                return View("DealerPayments", detail);

            }
            else
            {
                TempData["message"] = "Sorry No Payment Found Against This Order";

                return RedirectToAction("DealerView");


            }

        }

        public ActionResult CPCBPermission()
        {
            var data = (from ab in DB.tbl_User_Master
                        select ab).ToList();
            return View(data);
        }
        public ActionResult EnableCPCBII(int Id)
        {
            var update = (from ab in DB.tbl_User_Master
                         where ab.Id ==Id
                         select ab).SingleOrDefault();
            update.ClientIPAddId = 2;
            DB.SaveChanges();
            //ViewBag.msg = "Permission Updated";
            TempData["message"] = "Permission Updated";
            return RedirectToAction("CPCBPermission");
        }
        public ActionResult DisableCPCBII(int Id)
        {
            var update = (from ab in DB.tbl_User_Master
                          where ab.Id == Id
                          select ab).SingleOrDefault();
            update.ClientIPAddId = 1;
            DB.SaveChanges();
           // ViewBag.msg = "Permission Updated";
            TempData["message"] = "Permission Updated";
            return RedirectToAction("CPCBPermission");
        }
        public ActionResult Orderdetail()
        {
            return View();
        }

        public ActionResult GetCompanies()
        {
            return View();
        }

    }
}