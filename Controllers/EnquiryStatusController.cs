using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CMKL.Models;
using CMKL.VMModels;
using Microsoft.Ajax.Utilities;
using System.Net.Mail;
using System.Net;
using System.Data.Entity.Core.Metadata.Edm;

namespace CMKL.Controllers
{
    public class EnquiryStatusController : Controller
    {
        
        IECEntities DB = new IECEntities();
        // GET: EnquiryStatus
        public ActionResult Status()
        {                  

            return View();
            
        }

        public ActionResult SplitEnquirySearch()
        {

            return View();

        }
        [HttpGet]
        public ActionResult GetStatus(int EnquiryNo, string Billseries, string Year,string ddlstage)
        {
            var stage = (from ab in DB.CMKL_Enquiry
                         where ab.EnquiryNo == EnquiryNo && ab.Billseries == Billseries && ab.Year == Year
                         select ab).FirstOrDefault();

            ViewBag.Enable = "ok";
            return View("Status",stage);            
        }
       [HttpPost]
        public ActionResult GetStatus(CMKL_Enquiry model, string ddlstage)
        {
            var stage = (from ab in DB.CMKL_Enquiry
                         where ab.EnquiryNo == model.EnquiryNo && ab.Billseries == model.Billseries && ab.Year ==model.Year && ab.WiringDrawing==ddlstage
                         select ab).FirstOrDefault();

            ViewBag.Enable = "Ok";
            return View("Status", stage);
        }
        public ActionResult Clear()
        {
            ModelState.Clear();
            
            return View("Status");
        }
       
        public ActionResult ReverseEnquiry()
        {
            var stages = (from a in DB.CMKL_Stage
                          select a.StageName).ToList();
            ViewBag.Stagelist = new SelectList(stages.AsEnumerable(), "StageName");
            return View();
        
        }
        public ActionResult Email(string Mtype, string IDS, string DST, string IP, string CL, string subject)
        {
            var data = (from ab in DB.CMKL_Email
                       where ab.DDLName == Mtype && ab.Active==1
                        select ab.Email).ToList();

            {
                SmtpClient ss = new SmtpClient("mail.iecgensets.com", 587);
                MailMessage mm = new MailMessage(new MailAddress("It@iecgensets.com", "Record Reversed"), new MailAddress("it@iecgensets.com"));
                //ss.Port = 25;
                mm.Subject = subject;
                mm.Body = "Dear Sir," + "<br>" +
                           "<br> " +
                           subject + "<br> " +
                           "ID Number :" +IDS+ "<br>"+
                           "Reversed From :" + CL + "<br>"+
                           "Reversed To  :" + DST + "<br>" +
                           "User Name :" + Session["U_Name"] + "<br>"+
                          // "IP Address :" + IP + "<br>" +
                           "<br>" +
                          
                           "This is an auto generated mail. Please Do Not reply to this message.";
              

                foreach (var bb in data)
                {
                    mm.CC.Add(bb);
                }
              
                mm.IsBodyHtml = true;
                ss.EnableSsl = false;
                NetworkCredential credntials = new NetworkCredential("it@iecgensets.com", "JwmG8998*Hf");
                ss.Credentials = credntials;
                ss.Send(mm);
               
                return Json(data: new { success = true, message = "Record Has Been Updated" }, JsonRequestBehavior.AllowGet);
            }

            
        }
        [HttpPost]
        public ActionResult Reverse(int ?  ID,  int ? Done , string ddlstage = "")
        {
            if (Done!=1)
            {
            string IP = Dns.GetHostAddresses(Environment.MachineName)[0].ToString();
            string IDS =Convert.ToString(ID);
            string DST = ddlstage;
            string Mtype = "Enquiry Reverse";
                string ST = "";
                string SS = "";
                string subject = "Record Has Been Reversed By User";
                string CL = "";
                var CurrentLocation = (from ab in DB.CMKL_Enquiry
                                       where ab.ID == ID
                                       select ab).SingleOrDefault();
                if (CurrentLocation.Statuss == "IN PROCESS" && CurrentLocation.OAStage==null )
                {
                    CL = "Price Revision";
                }
               else  if (CurrentLocation.Statuss == "WON" && CurrentLocation.OAStage == 1)
                {
                    CL = "Pending OA";
                }
               else if (CurrentLocation.Statuss == "WON" && CurrentLocation.OAStage == 2)
                {
                    CL = "Drawing Upload";
                }
               else if (CurrentLocation.Statuss == "WON" && CurrentLocation.OAStage == 3)
                {
                    CL = "Drawing Approval";
                }
               else if (CurrentLocation.Statuss == "WON" && CurrentLocation.OAStage == 4)
                {
                    CL = "Pending Production";
                }
               else if (CurrentLocation.Statuss == "WON" && CurrentLocation.OAStage == 5)
                {
                    CL = "IN Production";
                }
                else if (CurrentLocation.Statuss == "WON" && CurrentLocation.OAStage == 6)
                {
                    CL = "Quality Check";
                }
                else if (CurrentLocation.Statuss == "WON" && CurrentLocation.OAStage == 7)
                {
                    CL = "Ready To Dispatch";
                }
                else if (CurrentLocation.Statuss == "WON" && CurrentLocation.OAStage == 8)
                {
                    CL = "Dispatched";
                }




                if (ModelState.IsValid)
                {

                    var Update = (from ab in DB.CMKL_Enquiry
                                  where ab.ID == ID
                                  select ab);
                    string N = ID.ToString();
                    if (Update.Any())
                    {
                        if (ddlstage == "Price Revision")
                        {
                            SS = "IN PROCESS";
                            ST = null;
                        }
                        //if (!string.IsNullOrEmpty(Update.ToString()))
                        else if (ddlstage == "Pending OA")
                        {
                            SS = "WON";
                            ST = "1";
                        }
                        else if (ddlstage == "Drawing Upload")
                        {
                            SS = "WON";
                            ST = "2";
                        }
                        else if (ddlstage == "Drawing Approval")
                        {
                            SS = "WON";
                            ST = "3";
                        }
                        else if (ddlstage == "Pending Production")
                        {
                            SS = "WON";
                            ST = "4";
                        }
                        else if (ddlstage == "IN Production")
                        {
                            SS = "WON";
                            ST = "5";
                        }
                        else if (ddlstage == "Quality Check")
                        {
                            SS = "WON";
                            ST = "6";
                        }
                        else if (ddlstage == "Ready To Dispatch")
                        {
                            SS = "WON";
                            ST = "7";
                        }
                        else if (ddlstage == "Dispatched")
                        {
                            SS = "WON";
                            ST = "8";
                        }


                        foreach (var h in Update)
                        {
                            h.Statuss = SS;
                            h.OAStage =Convert.ToInt32(ST);
                          //  if (ST=="null")
                          //  {
                           //     h.OAStage = "null";
                           // }
                           // else
                           // {
                            //    h.OAStage = Convert.ToInt32(ST);
                           // }
                        }
                        DB.SaveChanges();
                        ModelState.Clear();

                        return RedirectToAction("Email", new { Mtype, IDS, DST, IP ,CL,subject });
                        //return Json(data: new { success = true, message = "Record has been Updated" }, JsonRequestBehavior.AllowGet);

                    }
                    else
                    {
                        return Json(data: new { success = false, message = "Record Not Found" }, JsonRequestBehavior.AllowGet);
                    }
                }
                

                

               
            }
            else
            {
                return Json(data: new { success = true, message = "Record Has Been Updated" }, JsonRequestBehavior.AllowGet);
            }



            return View("ReverseEnquiry");
        }
        public ActionResult CustomerDetail()
        {
            return View();
        }

        [HttpGet]
        public ActionResult GetCustomerDetail(int ID)
        {
            var Detail = (from ab in DB.CMKL_Enquiry
                   where ab.ID == ID
                 select ab).SingleOrDefault();
            // return View();
            return Json(Detail, JsonRequestBehavior.AllowGet );
        }


        [HttpPost]
        public ActionResult UpdateCustomerDetail(CMKL_Enquiry CE)
        {


            var Detail = (from ab in DB.CMKL_Enquiry
                          where ab.ID == CE.ID
                          select ab).SingleOrDefault();
            Detail.Project_Client = CE.Project_Client;
            Detail.Contact = CE.Contact;
            Detail.Contact_Name = CE.Contact_Name;
            Detail.TechnicalContact = CE.TechnicalContact;
            Detail.FinancialContact = CE.FinancialContact;
            Detail.Email = CE.Email;
            Detail.Address = CE.Address;
            DB.SaveChanges();

            return Json(data: new { success = true, message = "Record has been Updated" }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult OrderDetail(CMKL_Enquiry CE, string split)
        {
            string oldenquiry = CE.EnquiryN;
            string enquirynew = split;
            Splitjobclass model = new Splitjobclass();
            if (split != null)
            {
                var Master = (from ab in DB.CMKL_Enquiry
                              where ab.ID == CE.ID
                              //where ab.EnquiryN == "1" && ab.Billseries=="CHD" && ab.Year=="2018-19"
                              select ab).SingleOrDefault();
                string en = Master.EnquiryN;
                string bills = Master.Billseries;
                string fyear = Master.Year;

                var items = (from bc in DB.CMKL_Enquiry_Item
                             where bc.EnquiryN == en && bc.Billseries == bills && bc.Year == fyear && bc.IsDeleted!=1
                             select bc).ToList();


                {
                    CMKL_Enquiry CEN = new CMKL_Enquiry();
                    CEN.EnquiryN = split;
                    CEN.Billseries = Master.Billseries;
                    CEN.Year = Master.Year;
                    CEN.Enquiry_Month = Master.Enquiry_Month;
                    CEN.Project_Client = Master.Project_Client;
                    CEN.Industry = Master.Industry;
                    CEN.Contact_Name = Master.Contact_Name;

                    CEN.TechnicalContact = Master.TechnicalContact;
                    CEN.FinancialContact = Master.FinancialContact;
                    CEN.Contact = Master.Contact;
                    CEN.Email = Master.Email;
                    CEN.Address = Master.Address;
                    CEN.ReceivedFrom = Master.ReceivedFrom;
                    CEN.ReceivedDT = Master.ReceivedDT;
                    CEN.SentDT = Master.SentDT;
                    CEN.Statuss = Master.Statuss;
                    CEN.Remarks = Master.Remarks;
                    CEN.EnquiryType = Master.EnquiryType;
                    CEN.Year = Master.Year;
                    CEN.EnterDT = Master.EnterDT;
                    CEN.EnterTM = Master.EnterTM;
                    CEN.EnterBy = Master.EnterBy;
                    CEN.TotalBasicValue = Master.TotalBasicValue;
                    CEN.TotalTaxValue = Master.TotalTaxValue;
                    CEN.GrossTotalAmount = Master.GrossTotalAmount;
                    CEN.Industry = Master.Industry;
                    CEN.OANumber = Master.OANumber;
                    CEN.OADT = Master.OADT;
                    CEN.PONumber = Master.PONumber;
                    CEN.PODT = Master.PODT;
                    CEN.GST = Master.GST;
                    CEN.GST1 = Master.GST1;
                    CEN.PAN = Master.PAN;
                    CEN.PAN1 = Master.PAN1;
                    CEN.BillingAddress = Master.BillingAddress;
                    CEN.ShippingAddress = Master.ShippingAddress;
                    CEN.Jobnumber = Master.Jobnumber;
                    CEN.GADrawing = Master.GADrawing;
                    CEN.SingleDrawing = Master.SingleDrawing;
                    CEN.WiringDrawing = Master.WiringDrawing;
                    CEN.BillDrawing = Master.BillDrawing;
                    CEN.Freight = Master.Freight;
                    CEN.Insurance = Master.Insurance;
                    CEN.DeliveryDT = Master.DeliveryDT;
                    CEN.DeliveryRemarks = Master.DeliveryRemarks;
                    CEN.ApprovedBy = Master.ApprovedBy;
                    CEN.PaymentTerms = Master.PaymentTerms;
                    CEN.OAStage = Master.OAStage;
                    CEN.DrawingApp = Master.DrawingApp;
                    CEN.POCopy = Master.POCopy;
                    CEN.OARemarks = Master.OARemarks;
                    CEN.DAppDT = Master.DAppDT;
                    CEN.DAppTM = Master.DAppTM;
                    CEN.DAP = Master.DAP;
                    CEN.PCD = Master.PCD;
                    CEN.ProductionDoc = Master.ProductionDoc;
                    CEN.ProdComplete = Master.ProdComplete;
                    CEN.QualityDoc = Master.QualityDoc;
                    CEN.QualityComplete = Master.QualityComplete;
                    CEN.Billcopy = Master.Billcopy;
                    CEN.LRCopy = Master.LRCopy;
                    CEN.EwayCopy = Master.EwayCopy;
                    CEN.TruckCopy = Master.TruckCopy;
                    CEN.PackingList = Master.PackingList;
                    CEN.DispatchComplete = Master.DispatchComplete;
                    CEN.DriverName = Master.DriverName;
                    CEN.TruckNumber = Master.TruckNumber;
                    CEN.DriverContact = Master.DriverContact;
                    CEN.MechanicalPart = Master.MechanicalPart;
                    CEN.AsbuiltDRW = Master.AsbuiltDRW;
                    CEN.CustomerRPT = Master.CustomerRPT;
                    CEN.EnquiryStatusDT = Master.EnquiryStatusDT;
                    CEN.DrawingUploadDT = Master.DrawingUploadDT;
                    CEN.OAApproveDT = Master.OAApproveDT;
                    CEN.OAEnterDT = Master.OAEnterDT;


                    DB.CMKL_Enquiry.Add(CEN);
                    DB.SaveChanges();
                }

                model.CE = Master;
                model.CEI = items;
            }
            if (split == "NO COndition")
            {
                CMKL_Enquiry CEN = new CMKL_Enquiry();
                CEN.EnquiryN = split;
                CEN.Billseries = CE.Billseries;
                CEN.Year = CE.Year;
                CEN.Enquiry_Month = CE.Enquiry_Month;
                CEN.Project_Client = CE.Project_Client;
                CEN.Industry = CE.Industry;
                CEN.Contact_Name = CE.Contact_Name;
                CEN.TechnicalContact = CE.TechnicalContact;
                CEN.FinancialContact = CE.FinancialContact;
                CEN.Contact = CE.Contact;
                CEN.Email = CE.Email;
                CEN.Address = CE.Address;
                CEN.ReceivedFrom = CE.ReceivedFrom;
                CEN.ReceivedDT = CE.ReceivedDT;
                CEN.SentDT = CE.SentDT;
                CEN.Statuss = CE.Statuss;
                CEN.Remarks = CE.Remarks;
                CEN.EnquiryType = CE.EnquiryType;
                CEN.Year = CE.Year;
                CEN.EnterDT = CE.EnterDT;
                CEN.EnterTM = CE.EnterTM;
                CEN.EnterBy = CE.EnterBy;
                CEN.TotalBasicValue = CE.TotalBasicValue;
                CEN.TotalTaxValue = CE.TotalTaxValue;
                CEN.GrossTotalAmount = CE.GrossTotalAmount;
                CEN.Industry = CE.Industry;
                CEN.OANumber = CE.OANumber;
                CEN.OADT = CE.OADT;
                CEN.PONumber = CE.PONumber;
                CEN.PODT = CE.PODT;
                CEN.GST = CE.GST;
                CEN.GST1 = CE.GST1;
                CEN.PAN = CE.PAN;
                CEN.PAN1 = CE.PAN1;
                CEN.BillingAddress = CE.BillingAddress;
                CEN.ShippingAddress = CE.ShippingAddress;
                CEN.Jobnumber = CE.Jobnumber;
                CEN.GADrawing = CE.GADrawing;
                CEN.SingleDrawing = CE.SingleDrawing;
                CEN.WiringDrawing = CE.WiringDrawing;
                CEN.BillDrawing = CE.BillDrawing;
                CEN.Freight = CE.Freight;
                CEN.Insurance = CE.Insurance;
                CEN.DeliveryDT = CE.DeliveryDT;
                CEN.DeliveryRemarks = CE.DeliveryRemarks;
                CEN.ApprovedBy = CE.ApprovedBy;
                CEN.PaymentTerms = CE.PaymentTerms;
                CEN.OAStage = CE.OAStage;
                CEN.DrawingApp = CE.DrawingApp;
                CEN.POCopy = CE.POCopy;
                CEN.OARemarks = CE.OARemarks;
                CEN.DAppDT = CE.DAppDT;
                CEN.DAppTM = CE.DAppTM;
                CEN.DAP = CE.DAP;
                CEN.PCD = CE.PCD;
                CEN.ProductionDoc = CE.ProductionDoc;
                CEN.ProdComplete = CE.ProdComplete;
                CEN.QualityDoc = CE.QualityDoc;
                CEN.QualityComplete = CE.QualityComplete;
                CEN.Billcopy = CE.Billcopy;
                CEN.LRCopy = CE.LRCopy;
                CEN.EwayCopy = CE.EwayCopy;
                CEN.TruckCopy = CE.TruckCopy;
                CEN.PackingList = CE.PackingList;
                CEN.DispatchComplete = CE.DispatchComplete;
                CEN.DriverName = CE.DriverName;
                CEN.TruckNumber = CE.TruckNumber;
                CEN.DriverContact = CE.DriverContact;
                CEN.MechanicalPart = CE.MechanicalPart;
                CEN.AsbuiltDRW = CE.AsbuiltDRW;
                CEN.CustomerRPT = CE.CustomerRPT;
                CEN.EnquiryStatusDT = CE.EnquiryStatusDT;
                CEN.DrawingUploadDT = CE.DrawingUploadDT;
                CEN.OAApproveDT = CE.OAApproveDT;
                CEN.OAEnterDT = CE.OAEnterDT;
                // CEN.EnquiryN=CE.EnquiryN;




                DB.CMKL_Enquiry.Add(CEN);
                DB.SaveChanges();
            }

                {
                  var Master = (from ab in DB.CMKL_Enquiry
                                  where ab.ID == CE.ID                                 
                                 select ab).SingleOrDefault();
                   string en = Master.EnquiryN;
                   string bills = Master.Billseries;
                   string fyear = Master.Year;

                    var items = (from bc in DB.CMKL_Enquiry_Item
                                 where bc.EnquiryN == en && bc.Billseries == bills && bc.Year == fyear && bc.IsDeleted != 1
                                 select bc).ToList();



                    model.CE = Master;
                    model.CEI = items;

                    // model = split;
                    ViewBag.NEN = enquirynew;
                    ViewBag.ONEN = CE.EnquiryN;
                     ViewBag.ID = CE.ID;
                    ViewBag.HID = CE.ID;

                }
                ViewBag.Saved = "Enquiry Processed Sucessfully";
               
                
            

            if (split == null)
            {
                return View("SplitEnquiry", model);
            }
            else
            {
                return View("SplitItems", model);
                
            }



        }
        public ActionResult MoveItems(CMKL_Enquiry_Item CE, string item , int ? masterid, int? HID)
        {
            Splitjobclass model = new Splitjobclass();
            //int A = CE.ID;
            var Items = (from ab in DB.CMKL_Enquiry_Item
                            //  where ab.ID == A
                        where ab.ID==CE.ID 
                        select ab);

            //CMKL_Enquiry_Item CEI = new CMKL_Enquiry_Item();
            //Items = "1-1";
            
            foreach (var bb in Items)
            {
                bb.EnquiryN = item;
                //DB.SaveChanges();
            };
           // Items = item;
           DB.SaveChanges();

            {
                var Master = (from ab in DB.CMKL_Enquiry
                              where ab.ID == (HID)
                              select ab).SingleOrDefault();
                string en = Master.EnquiryN;
                string bills = Master.Billseries;
                string fyear = Master.Year;

                var itemlist = (from bc in DB.CMKL_Enquiry_Item
                             where bc.EnquiryN == en && bc.Billseries == bills && bc.Year == fyear && bc.IsDeleted != 1
                             select bc).ToList();
                model.CEI = itemlist;

                ViewBag.NEN = item;
                ViewBag.ONEN = CE.EnquiryN;
                ViewBag.HID = HID;
                ViewBag.msg = "Item Moved in - " + item +"" ;
            }
            return View("SplitItems", model);

            
        }





    }
}