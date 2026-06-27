using CMKL.Models;
using DocumentFormat.OpenXml.EMMA;
using DocumentFormat.OpenXml.Office2010.ExcelAc;
using DocumentFormat.OpenXml.Office2013.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Grpc.Core;
using Hangfire;
using Microsoft.Ajax.Utilities;
using Microsoft.Owin;
using Owin;
using RazorEngine;
using RazorEngine.Compilation.ImpromptuInterface;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web;
using System.Web.Helpers;
using System.Web.Hosting;
using System.Web.Mvc;

[assembly: OwinStartup(typeof(CMKL.Startup))]
namespace CMKL
{

    public class Startup
    {
        public static string DispatchTemplatePath;
        public void Configuration(IAppBuilder app)
        {
            DispatchTemplatePath = System.Web.Hosting.HostingEnvironment.MapPath("~/Views/EmailManage/BOMStorePendingApprovalAutoMailTemplate.cshtml");
            var db = new IECEntities();
            string Connection = db.ConnectionDatas
     .Where(i => i.Active == 1)
     .Select(i => i.ConnectionString)
     .SingleOrDefault();

            GlobalConfiguration.Configuration
                .UseSqlServerStorage(Connection);

            app.UseHangfireDashboard();
            app.UseHangfireServer();


            // Run After 2 Mins
            //  RecurringJob.AddOrUpdate(
            // "SendCombinedReminderEmail",
            // () => SendCombinedReminderEmail(),
            // "*/2 * * * *" // Run every 2 minutes
            //);
            //Run After Every 3 Hours from Morning 10 to evening 9 except Sunday.
            RecurringJob.AddOrUpdate(
            "SendCombinedReminderEmail",
            () => SendCombinedReminderEmail(),
            "0 10-20/3 * * 1-6", // Run every 3 hours from 10 AM to 8 PM (20) except Sunday
            TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")
            );

            // Corrected: Schedule SendFGStockReport to run once daily at 9:00 PM (21:00)
            RecurringJob.AddOrUpdate(
               "SendDailySaleReportDataForEmail", // Unique ID for the job
               () => SendDailySaleReportDataForEmail(),
               //"*/5 * * * *",
               "0 23 * * *", // Run at 11:00 PM daily (0 minutes, 23 hours, any day of month, any month, any day of week)
               TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")
            );

            //Auto Mail Purchase
            RecurringJob.AddOrUpdate(
            "SendDailyMaterialRecieptReport", // Unique ID for the job
            () => SendDailyMaterialRecieptReport(),
            "30 9 * * *", // Run at 9:30 AM daily (30 minutes, 10 hours)
            TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")
            );
            RecurringJob.AddOrUpdate(
           "SendDailyMaterialRecieptReport1", // Unique ID for the job
           () => SendDailyMaterialRecieptReport1(),
           "40 9 * * *", // Run at 9:30 AM daily (30 minutes, 10 hours)
           TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")
           );
            RecurringJob.AddOrUpdate(
                "AssetMaintenanceCheck",
                () => AssetMaintenanceCheck(), // Adjust namespace and method name
                                               //"*/5 * * * *", // <<< FIX: Run every 3 minutes (0, 3, 6, 9, etc.)
                "0 9 * * *", // <<< FIX: Run daily at 9:00 AM (0 minutes, 9 hours, any day, any month, any day of week)
                             //"0 6 * * *", // Run daily at 6:00 AM
                TimeZoneInfo.FindSystemTimeZoneById("India Standard Time") // Or your desired timezone
            );



        }

        
        public void AssetMaintenanceCheck()
        {
            string logFilePath = System.Web.Hosting.HostingEnvironment.MapPath("~/EmailLog_MaintenanceScheduleReport.txt"); // Adjust log file name

            try
            {
                using (var DB = new IECEntities()) // Replace IECEntities with your actual DbContext name
                {
                    // Check if report is active
                    var autoReminderSetting = DB.AutoReminderEmails.FirstOrDefault(x => x.AutoReminderEmailFor == "Daily Maintenance Report" && x.Active == 1);
                    if (autoReminderSetting == null)
                    {
                        WriteLog(logFilePath, "Daily Maintenance Report: Auto Reminder Email setting not found or inactive.");
                        return;
                    }

                    DateTime today = DateTime.Today;
                    int companyId = 17; // Adjust company ID

                    // --- STEP 1: Fetch RAW data from DB (NO date calculations here that EF can't translate) ---
                    var rawAllocationData = (from alloc in DB.Asset_AllocationMaster
                                             where alloc.IsDeleted == 0 && alloc.CompanyID == companyId
                                             // Joins to get display names
                                             join machine in DB.Asset_MachineMaster on alloc.MachineID equals machine.id into machGroup
                                             from machine in machGroup.DefaultIfEmpty()
                                             join subunit in DB.Asset_SubunitMaster on alloc.SubUnitID equals subunit.id into subGroup
                                             from subunit in subGroup.DefaultIfEmpty()
                                             join equipment in DB.Asset_TastMaster on alloc.TaskId equals equipment.id into equipGroup
                                             from equipment in equipGroup.DefaultIfEmpty()
                                             join freq in DB.Asset_FrequencyMaster on alloc.FrequencyID equals freq.id // Assuming Frequency is always linked
                                             select new // Anonymous type to hold raw data from DB
                                             {
                                                 // Select all raw fields needed for later calculation and display
                                                 AllocId = alloc.id,
                                                 MachineName = machine.MachineName,
                                                 SubunitName = subunit.SubUnitName,
                                                 EquipmentName = equipment.TaskName, // Assumed TaskName from Equipment
                                                 FrequencyName = freq.FrequencyName,
                                                 AllocatedOn = alloc.CreatedOn, // Use CreatedOn as a potential fallback baseline
                                                 LastMaintenanceDoneOn = alloc.LastMaintenanceDoneOn, // Original nullable DateTime?
                                                 Days = alloc.Days, // Original nullable int?
                                                 taskid=alloc.TaskType,

                                                 // Fetch associated items (sub-query for collection)
                                                 Items = (from taskItem in DB.Asset_TaskItems
                                                          where taskItem.AllocationMasterId == alloc.id && taskItem.IsDeleted == 0
                                                          join itemMaster in DB.BOMItemMasters on taskItem.ItemId equals itemMaster.Itemid
                                                          join UOM in DB.BOM_UOM on itemMaster.UOM equals UOM.id
                                                          select new //MaintenanceItemDetailDTO
                                                          {
                                                              ItemID= taskItem.ItemId,
                                                              ItemName = itemMaster.ItemName,
                                                              Quantity = taskItem.Quantity,
                                                              UOM = UOM.UOM,
                                                              //UOMID=
                                                          }).ToList()
                                             }).ToList(); // <<< CRUCIAL: .ToList() materializes data into memory here!

                    // --- STEP 2: Perform calculations (like AddDays) and filtering in memory (LINQ to Objects) ---
                    var allMaintenanceSchedules = rawAllocationData.Select(rawItem => {
                        // Determine the baseline date (non-nullable DateTime)
                        DateTime baselineDate = rawItem.LastMaintenanceDoneOn.HasValue ? rawItem.LastMaintenanceDoneOn.Value : rawItem.AllocatedOn.Value; // Assuming CreatedOn is always non-null

                        // Calculate NextDueDate using in-memory AddDays method
                        DateTime nextDueDate = baselineDate.AddDays(rawItem.Days ?? 0); // Handles nullable 'Days'

                        return new //DueMaintenanceTaskDTO // Project to your final email DTO
                        {
                            AllocateID=rawItem.AllocId,
                            MachineName = rawItem.MachineName ?? "N/A",
                            SubunitName = rawItem.SubunitName ?? "N/A",
                            EquipmentName = rawItem.EquipmentName ?? "N/A",
                            FrequencyName = rawItem.FrequencyName,
                            AllocatedOn = rawItem.AllocatedOn.Value, // Assuming non-nullable
                            LastMaintenanceOn = rawItem.LastMaintenanceDoneOn, // Keep nullable
                            NextDueDate = nextDueDate, // This is the calculated, non-nullable date
                            Items = rawItem.Items,
                            TaskID=rawItem.taskid,
                            TaskName= DB.Asset_TastMaster.Where(x=> x.id== rawItem.taskid).Select(x=> x.TaskName),

                        };
                    })
                    // --- Filter here based on NextDueDate (now that it's calculated) ---
                    .Where(task => task.NextDueDate.Date <= today.Date) // Filter for due today or in the past
                    .ToList(); // Final list of due maintenance schedules

                    if (!allMaintenanceSchedules.Any())
                    {
                        WriteLog(logFilePath, "No new maintenance tasks are due today. No logs created.");
                        return;
                    }
                    // --- Step 2: Create Logbook Entries and Gather Email Data ---
                    foreach (var allocDue in allMaintenanceSchedules)
                    {
                        // Create a new MaintenanceLogbook entry
                        Asset_MaintenanceLogbook AML = new Asset_MaintenanceLogbook();
                        {
                            AML.AllocationMasterId = allocDue.AllocateID;
                            AML.DueDate = allocDue.NextDueDate;
                            AML.IsCompleted = false;
                            AML.IsDeleted = 0;
                            AML.CreatedBy = "Hangfire Job";
                            AML.CreatedOn = DateTime.Now;
                            AML.AdminApproval = false;
                            AML.ApprovedBY = "NA";
                            AML.CompanyID = companyId;
                            AML.SAdminApproval = false;
                            AML.SAdminBy = "NA";
                        }
                        ;
                        DB.Asset_MaintenanceLogbook.Add(AML);
                        DB.SaveChanges(); // Save to get the new log ID

                        // --- Add associated items to MaintenanceLogbookItems table ---
                        if (allocDue.Items.Any())
                        {
                            foreach (var item in allocDue.Items)
                            {
                                Asset_MaintenanceLogbookItems AMLI = new Asset_MaintenanceLogbookItems();
                                {
                                    AMLI.LogbookId = AML.Id;
                                    AMLI.ItemId = item.ItemID;
                                    AMLI.Quantity = item.Quantity;
                                    AMLI.UOM = item.UOM;
                                    AMLI.IsDeleted = 0;
                                    AMLI.CreatedBy = "Hangfire Job";
                                    AMLI.CreatedOn = DateTime.Now;
                                }
                                ;
                                DB.Asset_MaintenanceLogbookItems.Add(AMLI);
                            }
                            DB.SaveChanges();
                        }



                        // --- Prepare data for email ---
                        var emailDueTask = new //DueMaintenanceTaskDTO
                        {
                            MachineName = allocDue.MachineName ?? "N/A",
                            SubunitName = allocDue.SubunitName ?? "N/A",
                            EquipmentName = allocDue.EquipmentName ?? "N/A",
                            FrequencyName = allocDue.FrequencyName,
                           // TaskID=allocDue.
                            // AllocatedOn = allocDue.Allocation.CreatedOn.Value,
                            // LastMaintenanceOn = allocDue.Allocation.LastMaintenanceDoneOn,
                            // NextDueDate = allocDue.CalculatedNextDueDate,
                            // Items = allocDue.TaskItems
                        };
                        //emailModels.Add(emailDueTask);
                    }

                    //Now Update Current Date as Last Schedule Date

                    foreach(var nn in allMaintenanceSchedules)
                    {
                        var update = (from ab in DB.Asset_AllocationMaster
                                      where ab.id == nn.AllocateID
                                      select ab).SingleOrDefault();
                        update.LastMaintenanceDoneOn = DateTime.Now;
                        DB.SaveChanges();

                    }

                    // --- Overall totals/counts ---

                    int totalSchedules = allMaintenanceSchedules.Count;
                    int totalItemsInvolved = allMaintenanceSchedules.Sum(s => s.Items.Count);



                    if (allMaintenanceSchedules.Any()) // Only send email if there's data
                    {
                        //Send Email According to Task
                        var distinctTypeIds = allMaintenanceSchedules.Select(x => x.TaskID).Distinct().ToList();
                        

                        foreach (var typeId in distinctTypeIds)
                        {
                            var TypeIDName= (from ab in DB.Asset_TaskType
                                            where ab.ID == typeId
                                            select ab).SingleOrDefault();
                            var emailSettings1 = DB.CMKL_Email_Setting.SingleOrDefault(ab => ab.id == 2);
                            var toAddresses1 = DB.CMKL_Email.Where(cd => cd.DDLName == TypeIDName.TaskType && cd.Active == 1).ToList(); //DB.CMKL_Email.Where(bc => bc.DDLName == "MaintananceSchdeule" && bc.Active == 1).ToList();
                            var ccAddresses1 = DB.CMKL_Email.Where(cd => cd.DDLName == "MaintainanceCC" && cd.Active == 1).ToList();
                            var senderEmail1 = DB.CMKL_Email.FirstOrDefault(se => se.DDLName == "ERPSender");
                            //var emailSettings1 = DB.CMKL_Email_Setting.SingleOrDefault(ab => ab.id == 2);


                            MailMessage mail1 = new MailMessage();
                            SmtpClient SmtpServer1 = new SmtpClient(emailSettings1.smtp);
                            mail1.From = new MailAddress(senderEmail1.Email);

                            foreach (var recipient in toAddresses1) { mail1.To.Add(recipient.Email); }
                            foreach (var ccRecipient in ccAddresses1) { mail1.CC.Add(ccRecipient.Email); }
                            //foreach (var recipient in toAddresses1) { mail1.To.Add(recipient); }
                            // foreach (var ccRecipient in ccAddresses1) { mail1.CC.Add(ccRecipient.Email); }
                            // 2. Filter the jobs for THIS specific type ID
                            var pendingJobsForThisUserGroup = allMaintenanceSchedules.Where(x => x.TaskID == typeId).ToList();
                            

                            // 3. Find specific users assigned to this Task Type ID
                            var emailModel1 = new //MaintenanceDueEmailModel
                            {
                                Title = autoReminderSetting.EmailSubject ?? $"Maintenance Due for {TypeIDName.TaskType} - {today:dd-MM-yyyy}",
                                ReportDate = today,
                                DueTasks = pendingJobsForThisUserGroup // Pass the filtered, calculated list
                            };

                            var templatePath1 = System.Web.Hosting.HostingEnvironment.MapPath("~/Views/EmailManage/MaintananceScheduleEmailTemplate.cshtml"); // Or use Startup.SaleReportTemplatePath if defined there
                            var template1 = System.IO.File.ReadAllText(templatePath1);
                            var body1 = Razor.Parse(template1, emailModel1, null, null);

                            mail1.Subject = "Machine Maintanance Due on -" + today.ToString("dd-MM-yyyy");
                            mail1.Body = body1;
                            mail1.IsBodyHtml = true;

                            SmtpServer1.Port = Convert.ToInt32(emailSettings1.port);
                            SmtpServer1.Credentials = new System.Net.NetworkCredential(senderEmail1.Email, emailSettings1.IT_password);
                            SmtpServer1.EnableSsl = Convert.ToBoolean(emailSettings1.ssl);

                            SmtpServer1.Send(mail1);
                            WriteLog(logFilePath, "Daily Maintanance Schedule Mail Sent successfully for - " + TypeIDName.TaskType);

                        }
                    }

                    else
                    {
                        WriteLog(logFilePath, $" Jobs are pending but no mail was sent.");
                    }
                        
                    
                    
                    if (!allMaintenanceSchedules.Any())
                    {
                        WriteLog(logFilePath, "No asset maintenance tasks are due today.");
                    }
                }
            }
            catch (SmtpException ex)
            {
                WriteLog(logFilePath, $"SMTP Error sending Daily Material Receipt Report - {ex.Message}");
            }
            catch (Exception ex)
            {
                WriteLog(logFilePath, $"Error sending Daily Material Receipt Report email - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            }
        }

        public void SendDailyMaterialRecieptReport1()
        {
            string logFilePath = System.Web.Hosting.HostingEnvironment.MapPath("~/EmailLog_RecieptReport.txt"); // Dedicated log file

            try
            {
                var DB = new IECEntities();
                var check = DB.AutoReminderEmails.Where(x => x.AutoReminderEmailFor == "Daily Material Report" && x.Active == 1).ToList();
                if (check.Count > 0)
                {

                    // Define date range for today's sales
                    System.DateTime today = System.DateTime.Today;
                    System.DateTime previousDay = today.AddDays(-1);
                    System.DateTime startOfPreviousDay = previousDay;
                    System.DateTime endOfPreviousDay = previousDay.AddDays(1).AddTicks(-1);


                    int companyid = 2;// Use fixed ID or retrieve from a configuration/DB lookup
                    string finyear = "2025-26"; // Or retrieve dynamically if needed
                    var Bill = (from bl in DB.IEPLStockIN_Head
                                join sup in DB.SupplierMasters on bl.Supplierid equals sup.id into supgrp
                                from sup in supgrp.DefaultIfEmpty()
                                where bl.CompanyID == companyid && bl.CreatedDate >= startOfPreviousDay && bl.CreatedDate <= endOfPreviousDay
                                where bl.Isdeleted == 0
                                //where line.IsDeleted == 0
                                select new
                                {
                                    Supplier = sup.SupplierName,
                                    billnumber = bl.BillNumber,
                                    billdate = bl.BillDate,
                                    ponumber = bl.PoNumber,
                                    MRN = bl.Vouchernumber,
                                    Gateentry = bl.GateEntry ?? "NA",
                                    gateentrydate = bl.GateEntryDate,
                                    frieght = bl.frieghtamount,
                                    FrieghtTaxAmount = bl.FrieghtTaxAmount,// ?? 0,
                                    othertax = bl.OtherTaxAmount,
                                    totaltax = bl.TaxAmount,
                                    totalamount = bl.BillAmount

                                }).ToList();
                    var TotalBillAmount = Bill.Sum(i => i.totalamount);
                    var MaterialReciept = (from ab in DB.IEPLStockIN_Head
                                           join sup in DB.SupplierMasters on ab.Supplierid equals sup.id into supgrp
                                           from sup in supgrp.DefaultIfEmpty()
                                           join line in DB.IEPLStockIN_Detail on ab.Id equals line.HeadId// into linegrp
                                                                                                         //from line in linegrp.DefaultIfEmpty()
                                           join taxty in DB.TaxMasters on line.Taxid equals taxty.id into taxgrp
                                           from taxty in taxgrp.DefaultIfEmpty()
                                           join im in DB.BOMItemMasters on line.ItemCode equals im.Itemid into imgrp
                                           from im in imgrp.DefaultIfEmpty()
                                           where ab.CompanyID == companyid && ab.CreatedDate >= startOfPreviousDay && ab.CreatedDate <= endOfPreviousDay
                                           where ab.Isdeleted == 0
                                           where line.IsDeleted == 0
                                           select new
                                           {
                                               InvoiceNo = ab.BillNumber,
                                               InvoiceDate = ab.BillDate,
                                               SupplierName = sup.SupplierName,
                                               ItemName = im.ItemName,
                                               Quantity = line.Quantity,
                                               BasicAmount = line.BasicPrice,
                                               TotalBasic = (line.BasicPrice ?? 0m) * (line.Quantity),
                                               TaxType = taxty.Taxname,
                                               TotalTaxAmount = line.TaxAmount,
                                               Total = line.TotalAmount,
                                               OtherTax = ab.OtherTaxAmount,
                                               FrightAmount = ab.frieghtamount,
                                               FrieghtTaxAmount = ab.FrieghtTaxAmount,
                                               BillTotal = ab.BillAmount,
                                               RecievingDate = ab.CreatedDate,
                                           }).ToList();
                    var totalBasic = MaterialReciept.Sum(item => item.TotalBasic);
                    var totalTaxAmount = MaterialReciept.Sum(item => item.TotalTaxAmount);
                    var LineTotal = MaterialReciept.Sum(item => item.Total);
                    var Othertaxtotal = Bill.Sum(item => item.othertax);
                    //var frieghtamounttotal = Bill.Sum(item => item.frieght + item.FrieghtTaxAmount);
                    var frieghtamounttotal = Bill.Sum(item => (item.frieght ?? 0m) + (item.FrieghtTaxAmount ?? 0m));
                    var billamounttotal = Bill.Sum(item => item.totalamount);



                    // Fetch email settings and recipients (similar to SendFGStockReport in Startup.cs)
                    var emailSettings = DB.CMKL_Email_Setting.SingleOrDefault(ab => ab.id == 2);
                    var toAddresses = DB.CMKL_Email.Where(bc => bc.DDLName == "MaterialRecieptIEC" && bc.Active == 1).ToList();
                    var ccAddresses = DB.CMKL_Email.Where(cd => cd.DDLName == "MaterialRecieptIECCC" && cd.Active == 1).ToList();
                    var senderEmail = DB.CMKL_Email.FirstOrDefault(se => se.DDLName == "ERPSender");

                    if (emailSettings == null || senderEmail == null || !toAddresses.Any())
                    {
                        WriteLog(logFilePath, "Email configuration or recipients missing for Daily Sale Report.");
                        return; // Exit if config is missing
                    }

                    if (MaterialReciept.Any()) // Only send email if there's data
                    {
                        MailMessage mail = new MailMessage();
                        SmtpClient SmtpServer = new SmtpClient(emailSettings.smtp);
                        mail.From = new MailAddress(senderEmail.Email);

                        foreach (var recipient in toAddresses) { mail.To.Add(recipient.Email); }
                        foreach (var ccRecipient in ccAddresses) { mail.CC.Add(ccRecipient.Email); }

                        mail.Subject = "Daily Material Reciept Report - " + previousDay.ToString("dd-MM-yyyy");

                        // Use the new template path for Sale Report
                        var templatePath = System.Web.Hosting.HostingEnvironment.MapPath("~/Views/EmailManage/AutoMailMaterialReciept.cshtml"); // Or use Startup.SaleReportTemplatePath if defined there
                        var template = System.IO.File.ReadAllText(templatePath);

                        var model = new // Create an instance of your defined model
                        {
                            Title = "Daily Material Reciept Report for IEC" + previousDay.ToString("dd/MM/yyyy"),
                            BillData = Bill,
                            RecipetData = MaterialReciept,
                            totalBasic = totalBasic,
                            totalTaxAmount = totalTaxAmount,
                            LineTotal = LineTotal,
                            Othertaxtotal = Othertaxtotal,
                            frieghtamounttotal = frieghtamounttotal,
                            billamounttotal = billamounttotal,
                            TotalBillAmount = TotalBillAmount,
                        }
                    ;

                        var body = Razor.Parse(template, model, null, null);

                        mail.Body = body;
                        mail.IsBodyHtml = true;

                        SmtpServer.Port = Convert.ToInt32(emailSettings.port);
                        SmtpServer.Credentials = new System.Net.NetworkCredential(senderEmail.Email, emailSettings.IT_password);
                        SmtpServer.EnableSsl = Convert.ToBoolean(emailSettings.ssl);

                        SmtpServer.Send(mail);
                        WriteLog(logFilePath, "Daily Material Reciept Report successfully.");
                    }
                    else
                    {
                        WriteLog(logFilePath, "No Material Reciept data found for Yesterday to send in email report.");
                    }
                }
            }
            catch (SmtpException ex)
            {
                WriteLog(logFilePath, $"SMTP Error sending Daily Reciept Report - {ex.Message}");
            }
            catch (Exception ex)
            {
                WriteLog(logFilePath, $"Error sending Daily Reciept  Report email - {ex.Message}");
            }
        }
        public void SendDailyMaterialRecieptReport()
        {
            string logFilePath = System.Web.Hosting.HostingEnvironment.MapPath("~/EmailLog_RecieptReport.txt"); // Dedicated log file

            try
            {
                var DB = new IECEntities();
                var check = DB.AutoReminderEmails.Where(x => x.AutoReminderEmailFor == "Daily Material Report" && x.Active == 1).ToList();
                if (check.Count > 0)
                {

                    // Define date range for today's sales
                    System.DateTime today = System.DateTime.Today;
                    System.DateTime previousDay = today.AddDays(-1);
                    System.DateTime startOfPreviousDay = previousDay;
                    System.DateTime endOfPreviousDay = previousDay.AddDays(1).AddTicks(-1);


                    int companyid = 17;// Use fixed ID or retrieve from a configuration/DB lookup
                    string finyear = "2025-26"; // Or retrieve dynamically if needed
                    var Bill = (from bl in DB.IEPLStockIN_Head
                                join sup in DB.SupplierMasters on bl.Supplierid equals sup.id into supgrp
                                from sup in supgrp.DefaultIfEmpty()
                                where bl.CompanyID == companyid && bl.CreatedDate >= startOfPreviousDay && bl.CreatedDate <= endOfPreviousDay
                                where bl.Isdeleted == 0
                                //where line.IsDeleted == 0
                                select new
                                {
                                    Supplier = sup.SupplierName,
                                    billnumber = bl.BillNumber,
                                    billdate = bl.BillDate,
                                    ponumber = bl.PoNumber,
                                    MRN = bl.Vouchernumber,
                                    Gateentry = bl.GateEntry ?? "NA",
                                    gateentrydate = bl.GateEntryDate,
                                    frieght = bl.frieghtamount,
                                    FrieghtTaxAmount = bl.FrieghtTaxAmount,// ?? 0,
                                    othertax = bl.OtherTaxAmount,
                                    totaltax = bl.TaxAmount,
                                    totalamount = bl.BillAmount

                                }).ToList();
                    var TotalBillAmount = Bill.Sum(i => i.totalamount);
                    var MaterialReciept = (from ab in DB.IEPLStockIN_Head
                                           join sup in DB.SupplierMasters on ab.Supplierid equals sup.id into supgrp
                                           from sup in supgrp.DefaultIfEmpty()
                                           join line in DB.IEPLStockIN_Detail on ab.Id equals line.HeadId// into linegrp
                                                                                                         //from line in linegrp.DefaultIfEmpty()
                                           join taxty in DB.TaxMasters on line.Taxid equals taxty.id into taxgrp
                                           from taxty in taxgrp.DefaultIfEmpty()
                                           join im in DB.BOMItemMasters on line.ItemCode equals im.Itemid into imgrp
                                           from im in imgrp.DefaultIfEmpty()
                                           join uom in DB.BOM_UOM on im.UOM equals uom.id into uomgrp
                                           from uom in uomgrp.DefaultIfEmpty()
                                           where ab.CompanyID == companyid && ab.CreatedDate >= startOfPreviousDay && ab.CreatedDate <= endOfPreviousDay
                                           where ab.Isdeleted == 0
                                           where line.IsDeleted == 0
                                           select new
                                           {
                                               InvoiceNo = ab.BillNumber,
                                               InvoiceDate = ab.BillDate,
                                               SupplierName = sup.SupplierName,
                                               ItemName = im.ItemName,
                                               Quantity = line.Quantity,
                                               BasicAmount = line.BasicPrice,
                                               TotalBasic = (line.BasicPrice ?? 0m) * (line.Quantity),
                                               TaxType = taxty.Taxname,
                                               TotalTaxAmount = line.TaxAmount,
                                               Total = line.TotalAmount,
                                               OtherTax = ab.OtherTaxAmount,
                                               FrightAmount = ab.frieghtamount,
                                               FrieghtTaxAmount = ab.FrieghtTaxAmount,
                                               BillTotal = ab.BillAmount,
                                               RecievingDate = ab.CreatedDate,
                                               LotNo = line.LotNo,
                                               UOM = uom.UOM,
                                           }).ToList();
                    var totalBasic = MaterialReciept.Sum(item => item.TotalBasic);
                    var totalTaxAmount = MaterialReciept.Sum(item => item.TotalTaxAmount);
                    var LineTotal = MaterialReciept.Sum(item => item.Total);
                    var Othertaxtotal = Bill.Sum(item => item.othertax);
                    //var frieghtamounttotal = Bill.Sum(item => item.frieght + item.FrieghtTaxAmount);
                    var frieghtamounttotal = Bill.Sum(item => (item.frieght ?? 0m) + (item.FrieghtTaxAmount ?? 0m));
                    var billamounttotal = Bill.Sum(item => item.totalamount);



                    // Fetch email settings and recipients (similar to SendFGStockReport in Startup.cs)
                    var emailSettings = DB.CMKL_Email_Setting.SingleOrDefault(ab => ab.id == 2);
                    var toAddresses = DB.CMKL_Email.Where(bc => bc.DDLName == "MaterialRecieptIEPL" && bc.Active == 1).ToList();
                    var ccAddresses = DB.CMKL_Email.Where(cd => cd.DDLName == "MaterialRecieptIEPLCC" && cd.Active == 1).ToList();
                    var senderEmail = DB.CMKL_Email.FirstOrDefault(se => se.DDLName == "ERPSender");

                    if (emailSettings == null || senderEmail == null || !toAddresses.Any())
                    {
                        WriteLog(logFilePath, "Email configuration or recipients missing for Daily Sale Report.");
                        return; // Exit if config is missing
                    }

                    if (MaterialReciept.Any()) // Only send email if there's data
                    {
                        MailMessage mail = new MailMessage();
                        SmtpClient SmtpServer = new SmtpClient(emailSettings.smtp);
                        mail.From = new MailAddress(senderEmail.Email);

                        foreach (var recipient in toAddresses) { mail.To.Add(recipient.Email); }
                        foreach (var ccRecipient in ccAddresses) { mail.CC.Add(ccRecipient.Email); }

                        mail.Subject = "Daily Material Reciept Report - " + previousDay.ToString("dd-MM-yyyy");

                        // Use the new template path for Sale Report
                        var templatePath = System.Web.Hosting.HostingEnvironment.MapPath("~/Views/EmailManage/AutoMailMaterialReciept.cshtml"); // Or use Startup.SaleReportTemplatePath if defined there
                        var template = System.IO.File.ReadAllText(templatePath);

                        var model = new // Create an instance of your defined model
                        {
                            Title = "Daily Material Reciept Report for IEPL" + previousDay.ToString("dd/MM/yyyy"),
                            BillData = Bill,
                            RecipetData = MaterialReciept,
                            totalBasic = totalBasic,
                            totalTaxAmount = totalTaxAmount,
                            LineTotal = LineTotal,
                            Othertaxtotal = Othertaxtotal,
                            frieghtamounttotal = frieghtamounttotal,
                            billamounttotal = billamounttotal,
                            TotalBillAmount = TotalBillAmount,
                        }
                    ;

                        var body = Razor.Parse(template, model, null, null);

                        mail.Body = body;
                        mail.IsBodyHtml = true;

                        SmtpServer.Port = Convert.ToInt32(emailSettings.port);
                        SmtpServer.Credentials = new System.Net.NetworkCredential(senderEmail.Email, emailSettings.IT_password);
                        SmtpServer.EnableSsl = Convert.ToBoolean(emailSettings.ssl);

                        SmtpServer.Send(mail);
                        WriteLog(logFilePath, "Daily Material Reciept Report successfully.");
                    }
                    else
                    {
                        WriteLog(logFilePath, "No Material Reciept data found for Yesterday to send in email report.");
                    }
                }
            }
            catch (SmtpException ex)
            {
                WriteLog(logFilePath, $"SMTP Error sending Daily Reciept Report - {ex.Message}");
            }
            catch (Exception ex)
            {
                WriteLog(logFilePath, $"Error sending Daily Reciept  Report email - {ex.Message}");
            }
        }
        public void SendDailySaleReportDataForEmail()
        {
            string logFilePath = System.Web.Hosting.HostingEnvironment.MapPath("~/EmailLog_DailySaleReport.txt");

            try
            {
                var DB = new IECEntities();
                // 1. Check if the automation is enabled
                var autoConfig = DB.AutoReminderEmails.FirstOrDefault(x => x.AutoReminderEmailFor == "Daily Report" && x.Active == 1);

                if (autoConfig != null)
                {
                    System.DateTime today = System.DateTime.Today;
                    System.DateTime startOfToday = today;
                    System.DateTime endOfToday = today.AddDays(1).AddTicks(-1);
                    System.DateTime startOfMonth = new System.DateTime(today.Year, today.Month, 1);

                    int companyid = 17;

                    // 1. Fetch Today's Detailed Sale Data with LOGISTICS details
                    var saleReportData = (from ab in DB.DispatchDetails
                                          join ew in DB.BOMEwapDetails on ab.Ewapid equals ew.id into ewgroup
                                          from ew in ewgroup.DefaultIfEmpty()
                                          join ts in DB.BOM_TestingUpdate on ew.BomVoucherHeadID equals ts.BOMHeadid into tsgroup
                                          from ts in tsgroup.DefaultIfEmpty()
                                          join rt in DB.Ratings_Production on ts.GensetRating equals rt.id into rtgroup
                                          from rt in rtgroup.DefaultIfEmpty()
                                          where ab.CompanyID == companyid && ab.BillDate >= startOfToday && ab.BillDate <= endOfToday
                                          orderby ab.BillDate descending
                                          select new
                                          {
                                              Billno = ab.BillNumber ?? "NA",
                                              Billdt = ab.BillDate,
                                              OA = ab.OrderNumber ?? "NA",
                                              CustomerName = ab.CustomerName ?? "NA",
                                              Shipping = ab.ShippingAddress ?? "NA",
                                              Rating = (rt.Rating ?? "NA") + " KVA",
                                              ESerial = ew.EngineSerialNumber ?? "NA",
                                              BatteryQty = ts.BTQty ?? 0,

                                              // --- CRITICAL LOGISTICS FIELDS ---
                                              Transporter = ab.Transport ?? "NA",
                                              VehicleNo = ab.LorryNo ?? "NA",
                                              LRNo = ab.LR ?? "NA",
                                             // DriverContact = ab.DriverNo ?? "NA",

                                              // Financials for Header
                                              BasicPrice = ab.BasicPrice ?? 0m,
                                              TotalTaxAmount = (ab.CGST ?? 0m) + (ab.SGST ?? 0m) + (ab.GST ?? 0m),
                                              Total = ab.BillingAMT ?? 0m
                                          }).ToList();

                    // 2. Fetch Month-to-Date Volume for the Range Matrix
                    var monthlyDataRaw = (from ab in DB.DispatchDetails
                                          join ew in DB.BOMEwapDetails on ab.Ewapid equals ew.id into ewgroup
                                          from ew in ewgroup.DefaultIfEmpty()
                                          join ts in DB.BOM_TestingUpdate on ew.BomVoucherHeadID equals ts.BOMHeadid into tsgroup
                                          from ts in tsgroup.DefaultIfEmpty()
                                          join rt in DB.Ratings_Production on ts.GensetRating equals rt.id into rtgroup
                                          from rt in rtgroup.DefaultIfEmpty()
                                          where ab.CompanyID == companyid && ab.BillDate >= startOfMonth && ab.BillDate <= endOfToday
                                          select new { BillDate = ab.BillDate, RatingRaw = rt.Rating }).ToList();

                    Func<string, string> getRangeName = (ratingStr) => {
                        decimal r = 0; decimal.TryParse(ratingStr, out r);
                        if (r >= 7 && r <= 35) return "7.5-35 KVA";
                        if (r >= 40 && r <= 160) return "40-160 KVA";
                        if (r >= 200 && r <= 250) return "200-250 KVA";
                        if (r >= 320 && r <= 750) return "320-750 KVA";
                        if (r >= 1010 && r <= 1250) return "1010-1250 KVA";
                        return "Others";
                    };

                    var definedRanges = new[] { "7.5-35 KVA", "40-160 KVA", "200-250 KVA", "320-750 KVA", "1010-1250 KVA" };
                    var ratingSummary = definedRanges.Select(range => new {
                        RangeName = range,
                        Count = monthlyDataRaw.Count(x => getRangeName(x.RatingRaw) == range && x.BillDate >= startOfToday),
                        MonthlyCount = monthlyDataRaw.Count(x => getRangeName(x.RatingRaw) == range)
                    }).Where(x => x.MonthlyCount > 0).ToList();

                    if (saleReportData.Any() || monthlyDataRaw.Any())
                    {
                        var emailSettings = DB.CMKL_Email_Setting.SingleOrDefault(ab => ab.id == 2);
                        var toAddresses = DB.CMKL_Email.Where(bc => bc.DDLName == "SaleReportIEPL" && bc.Active == 1).ToList();
                        var ccAddresses = DB.CMKL_Email.Where(cd => cd.DDLName == "SaleReportIEPLCC" && cd.Active == 1).ToList();
                        var senderEmail = DB.CMKL_Email.FirstOrDefault(se => se.DDLName == "ERPSender");

                        var model = new
                        {
                            Title = "Daily Logistics & Dispatch Brief",
                            SaleItems = saleReportData,
                            RatingWiseSummary = ratingSummary,
                            TotalBasicPrice = saleReportData.Sum(item => item.BasicPrice),
                            TotalTaxAmount = saleReportData.Sum(item => item.TotalTaxAmount),
                            TotalGrandTotal = saleReportData.Sum(item => item.Total)
                        };

                        var templatePath = System.Web.Hosting.HostingEnvironment.MapPath("~/Views/EmailManage/AutoMailSaleReport.cshtml");
                        var template = System.IO.File.ReadAllText(templatePath);

                        // Using Razor.Parse (ensure RazorEngine is configured in your project)
                        var body = RazorEngine.Razor.Parse(template, model);

                        MailMessage mail = new MailMessage();
                        mail.From = new MailAddress(senderEmail.Email, "IEPL Logistics Intel");
                        foreach (var recipient in toAddresses) { mail.To.Add(recipient.Email); }
                        foreach (var cc in ccAddresses) { mail.CC.Add(cc.Email); }

                        mail.Subject = "Daily Dispatch & Logistics Intelligence - " + today.ToString("dd-MM-yyyy");
                        mail.Body = body;
                        mail.IsBodyHtml = true;

                        using (SmtpClient client = new SmtpClient(emailSettings.smtp))
                        {
                            client.Port = Convert.ToInt32(emailSettings.port);
                            client.Credentials = new System.Net.NetworkCredential(senderEmail.Email, emailSettings.IT_password);
                            client.EnableSsl = Convert.ToBoolean(emailSettings.ssl);
                            client.Send(mail);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                // Log Error
            }
        }
        public void SendDailySaleReportDataForEmailold1()
        {
            string logFilePath = System.Web.Hosting.HostingEnvironment.MapPath("~/EmailLog_DailySaleReport.txt");

            try
            {
                var DB = new IECEntities();
                var autoConfig = DB.AutoReminderEmails.FirstOrDefault(x => x.AutoReminderEmailFor == "Daily Report" && x.Active == 1);

                if (autoConfig != null)
                {
                    // Fully qualified System.DateTime to avoid ambiguity with ClosedXML
                    System.DateTime today = System.DateTime.Today;
                    System.DateTime startOfToday = today;
                    System.DateTime endOfToday = today.AddDays(1).AddTicks(-1);
                    System.DateTime startOfMonth = new System.DateTime(today.Year, today.Month, 1);

                    int companyid = 17;

                    // 1. Fetch Today's Detailed Sale Data
                    var saleReportData = (from ab in DB.DispatchDetails
                                          join ew in DB.BOMEwapDetails on ab.Ewapid equals ew.id into ewgroup
                                          from ew in ewgroup.DefaultIfEmpty()
                                          join ts in DB.BOM_TestingUpdate on ew.BomVoucherHeadID equals ts.BOMHeadid into tsgroup
                                          from ts in tsgroup.DefaultIfEmpty()
                                          join ps in DB.Bom_ProductionUpdate on ew.BomVoucherHeadID equals ps.BOMHeadid into psgroup
                                          from ps in psgroup.DefaultIfEmpty()
                                          join rt in DB.Ratings_Production on ts.GensetRating equals rt.id into rtgroup
                                          from rt in rtgroup.DefaultIfEmpty()
                                          where ab.CompanyID == companyid && ab.BillDate >= startOfToday && ab.BillDate <= endOfToday
                                          orderby ab.BillDate descending
                                          select new
                                          {
                                              Billno = ab.BillNumber ?? "NA",
                                              Billdt = ab.BillDate,
                                              OA = ab.OrderNumber ?? "NA",
                                              CustomerName = ab.CustomerName ?? "NA",
                                              CustomerGST = ab.CustomerGST ?? "NA",
                                              Shipping = ab.ShippingAddress ?? "NA",
                                              Rating = (rt.Rating ?? "NA") + " KVA",
                                              Emodel = (from en in DB.EngineModels where ew.Model == en.EngineId select en.EngineModel1).FirstOrDefault() ?? "NA",
                                              ESerial = ew.EngineSerialNumber ?? "NA",
                                              Quantity = 1,
                                              BatteryQty = ts.BTQty ?? 0,
                                              BasicPrice = ab.BasicPrice ?? 0m,
                                              Frieght = ab.FrieghtAMT ?? 0m,
                                              CGST = ab.CGST ?? 0m,
                                              SGST = ab.SGST ?? 0m,
                                              IGST = ab.GST ?? 0m,
                                              TotalTaxAmount = (ab.CGST ?? 0m) + (ab.SGST ?? 0m) + (ab.GST ?? 0m),
                                              Total = ab.BillingAMT ?? 0m
                                          }).ToList();

                    // 2. Fetch Month-to-Date Volume for the Range Matrix
                    var monthlyDataRaw = (from ab in DB.DispatchDetails
                                          join ew in DB.BOMEwapDetails on ab.Ewapid equals ew.id into ewgroup
                                          from ew in ewgroup.DefaultIfEmpty()
                                          join ts in DB.BOM_TestingUpdate on ew.BomVoucherHeadID equals ts.BOMHeadid into tsgroup
                                          from ts in tsgroup.DefaultIfEmpty()
                                          join rt in DB.Ratings_Production on ts.GensetRating equals rt.id into rtgroup
                                          from rt in rtgroup.DefaultIfEmpty()
                                          where ab.CompanyID == companyid && ab.BillDate >= startOfMonth && ab.BillDate <= endOfToday
                                          select new
                                          {
                                              BillDate = ab.BillDate,
                                              RatingRaw = rt.Rating
                                          }).ToList();

                    // Range Categories Helper
                    Func<string, string> getRangeName = (ratingStr) => {
                        decimal r = 0;
                        decimal.TryParse(ratingStr, out r);
                        if (r >= 7 && r <= 35) return "7.5-35 KVA";
                        if (r >= 40 && r <= 160) return "40-160 KVA";
                        if (r >= 200 && r <= 250) return "200-250 KVA";
                        if (r >= 320 && r <= 750) return "320-750 KVA";
                        if (r >= 1010 && r <= 1250) return "1010-1250 KVA";
                        return "Others";
                    };

                    // 3. Construct the Rating Summary Matrix
                    var definedRanges = new[] { "7.5-35 KVA", "40-160 KVA", "200-250 KVA", "320-750 KVA", "1010-1250 KVA" };
                    var ratingSummary = definedRanges.Select(range => new {
                        RangeName = range,
                        Count = monthlyDataRaw.Count(x => getRangeName(x.RatingRaw) == range && x.BillDate >= startOfToday), // Today
                        MonthlyCount = monthlyDataRaw.Count(x => getRangeName(x.RatingRaw) == range) // Month Total
                    }).Where(x => x.MonthlyCount > 0).ToList();

                    if (saleReportData.Any() || monthlyDataRaw.Any())
                    {
                        var emailSettings = DB.CMKL_Email_Setting.SingleOrDefault(ab => ab.id == 2);
                        var toAddresses = DB.CMKL_Email.Where(bc => bc.DDLName == "SaleReportIEPL" && bc.Active == 1).ToList();
                        var ccAddresses = DB.CMKL_Email.Where(cd => cd.DDLName == "SaleReportIEPLCC" && cd.Active == 1).ToList();
                        var senderEmail = DB.CMKL_Email.FirstOrDefault(se => se.DDLName == "ERPSender");

                        var model = new
                        {
                            Title = "Daily Sale & Monthly Fulfillment Brief",
                            SaleItems = saleReportData,
                            RatingWiseSummary = ratingSummary,
                            TotalBasicPrice = saleReportData.Sum(item => item.BasicPrice),
                            TotalFrieght = saleReportData.Sum(item => item.Frieght),
                            TotalTaxAmount = saleReportData.Sum(item => item.TotalTaxAmount),
                            TotalGrandTotal = saleReportData.Sum(item => item.Total)
                        };

                        var templatePath = System.Web.Hosting.HostingEnvironment.MapPath("~/Views/EmailManage/AutoMailSaleReport.cshtml");
                        var template = System.IO.File.ReadAllText(templatePath);

                        // Using RazorEngine/Razor.Parse based on your project setup
                        var body = Razor.Parse(template, model);

                        MailMessage mail = new MailMessage();
                        mail.From = new MailAddress(senderEmail.Email, "IEPL ERP Business Intel");
                        foreach (var recipient in toAddresses) { mail.To.Add(recipient.Email); }
                        foreach (var ccRecipient in ccAddresses) { mail.CC.Add(ccRecipient.Email); }

                        mail.Subject = "Daily Sales Analytics - " + today.ToString("dd-MM-yyyy");
                        mail.Body = body;
                        mail.IsBodyHtml = true;

                        SmtpClient SmtpServer = new SmtpClient(emailSettings.smtp);
                        SmtpServer.Port = Convert.ToInt32(emailSettings.port);
                        SmtpServer.Credentials = new System.Net.NetworkCredential(senderEmail.Email, emailSettings.IT_password);
                        SmtpServer.EnableSsl = Convert.ToBoolean(emailSettings.ssl);
                        SmtpServer.Send(mail);

                        WriteLog(logFilePath, "Report sent successfully with Monthly Range metrics.");
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog(logFilePath, "Error: " + ex.Message);
            }
        }
        public void SendDailySaleReportDataForEmailold() // Void return type as Hangfire calls it, not AJAX
        {
            string logFilePath = System.Web.Hosting.HostingEnvironment.MapPath("~/EmailLog_DailySaleReport.txt"); // Dedicated log file

            try
            {
                var DB = new IECEntities();
                var check = DB.AutoReminderEmails.Where(x => x.AutoReminderEmailFor == "Daily Report" && x.Active == 1).ToList();
                if(check.Count>0)
                {
                   
                        // Define date range for today's sales
                    System.DateTime today = System.DateTime.Today;
                    System.DateTime startOfToday = today;
                    System.DateTime endOfToday = today.AddDays(1).AddTicks(-1);

                    // Replicate the core data fetching logic from SaleReportData
                    // Assuming company ID 17 is fixed for this automated report, or you get it from config/DB
                    int companyid = 17; // Use fixed ID or retrieve from a configuration/DB lookup
                    string finyear = "2025-26"; // Or retrieve dynamically if needed
                   
                    var saleReportData = (from ab in DB.DispatchDetails
                                          join ew in DB.BOMEwapDetails on ab.Ewapid equals ew.id into ewgroup
                                          from ew in ewgroup.DefaultIfEmpty()
                                          join ts in DB.BOM_TestingUpdate on ew.BomVoucherHeadID equals ts.BOMHeadid into tsgroup
                                          from ts in tsgroup.DefaultIfEmpty()
                                          join ps in DB.Bom_ProductionUpdate on ew.BomVoucherHeadID equals ps.BOMHeadid into psgroup
                                          from ps in psgroup.DefaultIfEmpty()
                                          join rt in DB.Ratings_Production on ts.GensetRating equals rt.id into rtgroup
                                          from rt in rtgroup.DefaultIfEmpty()
                                          join ph in DB.Phases on ps.AlternatorPhase equals ph.PhaseId into phgroup
                                          from ph in phgroup.DefaultIfEmpty()
                                          join en in DB.EngineModels on ew.Model equals en.EngineId into engroup
                                          from en in engroup.DefaultIfEmpty()
                                          join bt in DB.BatteryMasters on ew.BatteryRating equals bt.id into btgroup
                                          from bt in btgroup.DefaultIfEmpty()
                                          join cp in DB.ControlPanelTypes on ts.CPType equals cp.id into cpgrou
                                          from cp in cpgrou.DefaultIfEmpty()
                                          join tax in DB.TaxMasters on ab.TaxID equals tax.id into taxgroup
                                          from tax in taxgroup.DefaultIfEmpty()
                                          join taxtype in DB.TaxTypes on ab.TaxTypeID equals taxtype.ID into taxtypegroup
                                          from taxtype in taxtypegroup.DefaultIfEmpty()
                                          where ab.CompanyID == companyid
                                         // && ab.FinYear == finyear // Keep finyear if relevant
                                          && ab.BillDate >= startOfToday
                                          && ab.BillDate <= endOfToday
                                          orderby ab.BillDate descending
                                          select new // Project to the SaleReportItem DTO
                                          {
                                              Billno = ab.BillNumber ?? "NA",
                                              Billdt = ab.BillDate,
                                              CustomerName = ab.CustomerName ?? "NA",
                                              Billing = ab.CustomerAddress ?? "NA",
                                              Shipping = ab.ShippingAddress ?? "NA",
                                              CustomerGST = ab.CustomerGST ?? "NA",
                                              Rating = (rt.Rating ?? "NA") + "KVA - " + (ph.PhaseDesc ?? "NA"),
                                              Emodel = en.EngineModel1 ?? "NA",
                                              ESerial = ew.EngineSerialNumber ?? "NA",
                                              AlternatorSerial = ew.AlternatorSerialNumber ?? "NA",
                                              KRM = ew.KRMNo ?? "NA",
                                              Quantity = 1,
                                              BRating = bt.BatteryName ?? "NA",
                                              BSerial = ts.BTSerial ?? "NA",
                                              PSerial = ts.CPSerialNumber ?? "NA",
                                              ControlPanel = cp.Type ?? "NA",
                                              BasicPrice = ab.BasicPrice ?? 0m,
                                              Frieght = ab.FrieghtAMT ?? 0m,
                                              TaxAmount = ab.GST ?? 0m,
                                              Total = ab.BillingAMT ?? 0m,
                                              Transport = ab.Transport ?? "NA",
                                              VehicleNumber = ab.LorryNo ?? "NA",
                                              TaxType=taxtype.TaxType1,
                                              IGST = ab.GST ?? 0m,
                                              SGST = ab.SGST ?? 0m,
                                              CGST = ab.CGST ?? 0m,
                                              RoundOff = ab.RoundOff ?? 0m,

                                          }).ToList();

                    // Calculate totals for the email model
                    var totalBasicPrice = saleReportData.Sum(item => item.BasicPrice);
                    var totalFrieght = saleReportData.Sum(item => item.Frieght);
                    var totalTaxAmount = saleReportData.Sum(item => item.TaxAmount);
                    var totalCGST = saleReportData.Sum(item => item.CGST);
                    var totalSGST = saleReportData.Sum(item => item.SGST);
                    var totalGrandTotal = saleReportData.Sum(item => item.Total);

                    // Fetch email settings and recipients (similar to SendFGStockReport in Startup.cs)
                    var emailSettings = DB.CMKL_Email_Setting.SingleOrDefault(ab => ab.id == 2);
                    var toAddresses = DB.CMKL_Email.Where(bc => bc.DDLName == "SaleReportIEPL" && bc.Active == 1).ToList();
                    var ccAddresses = DB.CMKL_Email.Where(cd => cd.DDLName == "SaleReportIEPLCC" && cd.Active == 1).ToList();
                    var senderEmail = DB.CMKL_Email.FirstOrDefault(se => se.DDLName == "ERPSender");

                    if (emailSettings == null || senderEmail == null || !toAddresses.Any())
                    {
                        WriteLog(logFilePath, "Email configuration or recipients missing for Daily Sale Report.");
                        return; // Exit if config is missing
                    }

                    if (saleReportData.Any()) // Only send email if there's data
                    {
                        MailMessage mail = new MailMessage();
                        SmtpClient SmtpServer = new SmtpClient(emailSettings.smtp);
                        mail.From = new MailAddress(senderEmail.Email);

                        foreach (var recipient in toAddresses) { mail.To.Add(recipient.Email); }
                        foreach (var ccRecipient in ccAddresses) { mail.CC.Add(ccRecipient.Email); }

                        mail.Subject = "Daily Sale Report - " + today.ToString("dd-MM-yyyy");

                        // Use the new template path for Sale Report
                        var templatePath = System.Web.Hosting.HostingEnvironment.MapPath("~/Views/EmailManage/AutoMailSaleReport.cshtml"); // Or use Startup.SaleReportTemplatePath if defined there
                        var template = System.IO.File.ReadAllText(templatePath);

                        var model = new
                        {
                            Title = "Daily Sale Report for " + today.ToString("dd/MM/yyyy"),
                            SaleItems = saleReportData,
                            TotalBasicPrice = totalBasicPrice,
                            TotalFrieght = totalFrieght,
                            TotalTaxAmount = totalTaxAmount,
                            TotalCGST = totalCGST,  // ADDED this line
                            TotalSGST = totalSGST,  // ADDED this line
                            TotalGrandTotal = totalGrandTotal
                        };
                        var body = Razor.Parse(template, model, null, null);

                        mail.Body = body;
                        mail.IsBodyHtml = true;

                        SmtpServer.Port = Convert.ToInt32(emailSettings.port);
                        SmtpServer.Credentials = new System.Net.NetworkCredential(senderEmail.Email, emailSettings.IT_password);
                        SmtpServer.EnableSsl = Convert.ToBoolean(emailSettings.ssl);

                        SmtpServer.Send(mail);
                        WriteLog(logFilePath, "Daily Sale Report email sent successfully.");
                    }
                    else
                    {
                        WriteLog(logFilePath, "No sale data found for today to send in email report.");
                    }
                }
            }
            catch (SmtpException ex)
            {
                WriteLog(logFilePath, $"SMTP Error sending Daily Sale Report - {ex.Message}");
            }
            catch (Exception ex)
            {
                WriteLog(logFilePath, $"Error sending Daily Sale Report email - {ex.Message}");
            }
        }
        public void SendFGStockReport()
        {
            string logFilePath = System.Web.Hosting.HostingEnvironment.MapPath("~/EmailLog.txt");

            try
            {
                
                using (var db = new IECEntities())
                {
                    List<object> Reportdata = new List<object>();
                    string emailSubject = "";
                    string modelTitle = "";
                    string toAddressListName = "";
                    string ccAddressListName = "";
                    var dailyreport = db.AutoReminderEmails.Where(x => x.AutoReminderEmailFor == "Daily Report").ToList();
                    
                    foreach (var DR in dailyreport)
                    {
                        if (DR.EmailSubject == "Sale Report")
                        {
                            emailSubject = DR.EmailSubject;
                            modelTitle = DR.EmailTitle;
                            toAddressListName = DR.EmailTo;
                            ccAddressListName = DR.EmailCC;

                            var reportData = (from ed in db.BOMEwapDetails
                                              join im in db.BOMItemMasters on ed.ItemID equals im.Itemid into imGroup
                                              from im in imGroup.DefaultIfEmpty()
                                              join md in db.EngineModels on ed.Model equals md.EngineId into mdGroup
                                              from md in mdGroup.DefaultIfEmpty()
                                              where ed.DispatchStatus == 0
                                              select new
                                              {
                                                  ed = ed,
                                                  im = im,
                                                  md = md
                                              });

                            var data = reportData.GroupBy(x => new { x.ed.ItemID, x.im.ItemName, x.md.EngineModel1 })
                              .Select(g => new
                              {
                                  ItemID = g.Key.ItemID,
                                  ItemName = g.Key.ItemName,
                                  TotalQuantity = g.Sum(x => x.ed.Quantity),
                                  Model = g.FirstOrDefault().md.EngineModel1,
                              }).ToList();

                            Reportdata.AddRange(data);
                        }                      

                       if (Reportdata.Count>0)

                       {
                        var emailSettings = db.CMKL_Email_Setting.SingleOrDefault(ab => ab.id == 2);
                        var toAddresses = db.CMKL_Email.Where(bc => bc.DDLName == toAddressListName && bc.Active == 1).ToList(); // Ensure you have this DDLName
                        var ccAddresses = db.CMKL_Email.Where(cd => cd.DDLName == ccAddressListName && cd.Active == 1).ToList();// Ensure you have this DDLName
                        var senderEmail = db.CMKL_Email.FirstOrDefault(se => se.DDLName == "ERPSender");

                        if (emailSettings == null || senderEmail == null || !toAddresses.Any())
                        {
                            WriteLog(logFilePath, "Email configuration or recipients missing for FG Stock Report.");
                            return;
                        }

                        MailMessage mail = new MailMessage();
                        SmtpClient SmtpServer = new SmtpClient(emailSettings.smtp);
                        mail.From = new MailAddress(senderEmail.Email);

                        foreach (var recipient in toAddresses)
                        {
                            mail.To.Add(recipient.Email);
                        }

                        foreach (var ccRecipient in ccAddresses)
                        {
                            mail.CC.Add(ccRecipient.Email);
                        }

                        mail.Subject = "FG Stock Report";

                        var templatePath = Startup.DispatchTemplatePath;
                        var template = System.IO.File.ReadAllText(templatePath);

                        var model = new
                        {
                            Approvals = Reportdata, // Assuming data is what you want to send
                            Title = "FG Stock Report"
                        };

                        var body = Razor.Parse(template, model, null, null);

                        mail.Body = body;
                        mail.IsBodyHtml = true;

                        SmtpServer.Port = Convert.ToInt32(emailSettings.port);
                        SmtpServer.Credentials = new System.Net.NetworkCredential(senderEmail.Email, emailSettings.IT_password);
                        SmtpServer.EnableSsl = Convert.ToBoolean(emailSettings.ssl);

                        SmtpServer.Send(mail);
                        WriteLog(logFilePath, "FG Stock Report email sent successfully.");
                    }
                    else
                    {
                        WriteLog(logFilePath, "No FG Stock data found to send.");
                    }
                    }
                }
            }
            catch (SmtpException ex)
            {
                WriteLog(logFilePath, $"SMTP Error - {ex.Message}");
            }
            catch (Exception ex)
            {
                WriteLog(logFilePath, $"Error sending FG Stock Report email - {ex.Message}");
            }
        }

        public void SendBomVoucherReminderEmail()
        {
            string logFilePath = System.Web.Hosting.HostingEnvironment.MapPath("~/EmailLog.txt");

            try
            {
                using (var db = new IECEntities())
                {
                    var pendingApprovals = db.BOMVouchers
                        .Where(approval => approval.Approvalstatus == 0 && approval.Isdeleted == 0)
                        .ToList();
                    if (pendingApprovals == null || !pendingApprovals.Any())
                    {
                        WriteLog(logFilePath, "No Record To Send.");
                        return;
                    }

                    if (pendingApprovals.Any())
                    {
                        var emailSettings = db.CMKL_Email_Setting.SingleOrDefault(ab => ab.id == 2);
                        var toAddresses = db.CMKL_Email.Where(bc => bc.DDLName == "ReminderAutoMail" && bc.Active == 1).ToList();
                        var ccAddresses = db.CMKL_Email.Where(cd => cd.DDLName == "ReminderAutoMailCC" && cd.Active == 1).ToList();
                        var senderEmail = db.CMKL_Email.FirstOrDefault(se => se.DDLName == "ERPSender");

                        if (emailSettings == null || senderEmail == null || !toAddresses.Any())
                        {
                            WriteLog(logFilePath, "Email configuration or recipients missing.");
                            return;
                        }

                        MailMessage mail = new MailMessage();
                        SmtpClient SmtpServer = new SmtpClient(emailSettings.smtp);
                        mail.From = new MailAddress(senderEmail.Email);

                        foreach (var recipient in toAddresses)
                        {
                            mail.To.Add(recipient.Email);
                        }

                        // Add CC addresses
                        foreach (var ccRecipient in ccAddresses)
                        {
                            mail.CC.Add(ccRecipient.Email);
                        }

                        mail.Subject = "Store - Pending BOM Approvals";

                        var templatePath = Startup.DispatchTemplatePath;
                        var template = System.IO.File.ReadAllText(templatePath);

                        // Pass the entire list of pending approvals to the template
                        var body = Razor.Parse(template, pendingApprovals, null, null);

                        mail.Body = body;
                        mail.IsBodyHtml = true;

                        SmtpServer.Port = Convert.ToInt32(emailSettings.port);
                        SmtpServer.Credentials = new System.Net.NetworkCredential(senderEmail.Email, emailSettings.IT_password);
                        SmtpServer.EnableSsl = Convert.ToBoolean(emailSettings.ssl);

                        SmtpServer.Send(mail);
                        WriteLog(logFilePath, "Reminder email sent successfully.");

                    }
                }
            }
            catch (SmtpException ex)
            {
                WriteLog(logFilePath, $"SMTP Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                WriteLog(logFilePath, $"Error sending email: {ex.Message}");
            }
        }
        public void SendCombinedReminderEmail()
        {
            string logFilePath = System.Web.Hosting.HostingEnvironment.MapPath("~/EmailLog.txt");

            try
            {
                using (var db = new IECEntities())
                {
                    var jobs = (from ab in db.AutoReminderEmails
                                where ab.Active == 1
                                select ab).ToList();

                    foreach (var nm in jobs)
                    {
                        object pendingApprovals = null;
                        string emailSubject = "";
                        string modelTitle = "";
                        string toAddressListName = "";
                        string ccAddressListName = "";

                        if (nm.AutoReminderEmailFor == "BOMStoreApprovalReminder")
                        {
                            pendingApprovals = db.BOMVouchers
                            .Where(approval => approval.Approvalstatus == 0 && approval.Isdeleted == 0 && DbFunctions.AddHours(approval.VoucherDate, 24) < DateTime.Now)
                            .Select(v => new
                            {
                                VoucherNumber = v.VoucherNumber,
                                VoucherDate = v.VoucherDate,
                                CreatedBy = v.CreatedBy
                            })
                            .ToList();

                            emailSubject = nm.EmailSubject;
                            modelTitle = nm.EmailTitle;
                            toAddressListName = nm.EmailTo;
                            ccAddressListName = nm.EmailCC;
                        }
                        else if (nm.AutoReminderEmailFor == "IndentAdminApprovalReminder")
                        {
                            pendingApprovals = db.BOMIndentHeads
                                .Where(approval => approval.ApprovalStatus == 0 && DbFunctions.AddHours(approval.CreatedOn, 24) < DateTime.Now)
                                .Select(i => new
                                {
                                    VoucherNumber = i.VoucherNumber,
                                    VoucherDate = i.CreatedOn,
                                    CreatedBy = i.CreatedBy
                                })
                                .ToList();

                            emailSubject = nm.EmailSubject;
                            modelTitle = nm.EmailTitle;
                            toAddressListName = nm.EmailTo;
                            ccAddressListName = nm.EmailCC;
                        }
                        else if (nm.AutoReminderEmailFor == "BOMRequisitionApprovalReminder")
                        {
                            pendingApprovals = db.BOMRequisitionHeads
                                .Where(approval => approval.IsApproved == 0 && DbFunctions.AddHours(approval.Createdon, 24) < DateTime.Now)
                                .Select(i => new
                                {
                                    VoucherNumber = i.BOMReqeusitionNo,
                                    VoucherDate = i.Createdon,
                                    CreatedBy = i.Createdby
                                })
                                .ToList();

                            emailSubject = nm.EmailSubject;
                            modelTitle = nm.EmailTitle;
                            toAddressListName = nm.EmailTo;
                            ccAddressListName = nm.EmailCC;
                        }
                        else if (nm.AutoReminderEmailFor == "MRNQualityINApprovalReminder")
                        {
                            pendingApprovals = db.IEPLStockIN_Head
                                .Where(approval => approval.MRNStoreApproval == 1 && approval.MRNQualityApproval == 0 && DbFunctions.AddHours(approval.CreatedDate, 48) < DateTime.Now)
                                .Select(i => new
                                {
                                    VoucherNumber = i.Vouchernumber,
                                    VoucherDate = i.CreatedDate,
                                    CreatedBy = i.CreatedBy
                                })
                                .ToList();

                            emailSubject = nm.EmailSubject;
                            modelTitle = nm.EmailTitle;
                            toAddressListName = nm.EmailTo;
                            ccAddressListName = nm.EmailCC;
                        }
                        else if (nm.AutoReminderEmailFor == "MRNAdminApprovalReminder")
                        {
                            pendingApprovals = db.IEPLStockIN_Head
                                .Where(approval => approval.MRNStoreApproval == 1 && approval.MRNQualityApproval == 1 && approval.MRNPlantHeadApproval == 0 && DbFunctions.AddHours(approval.CreatedDate, 48) < DateTime.Now)
                                .Select(i => new
                                {
                                    VoucherNumber = i.Vouchernumber,
                                    VoucherDate = i.CreatedDate,
                                    CreatedBy = i.CreatedBy
                                })
                                .ToList();

                            emailSubject = nm.EmailSubject;
                            modelTitle = nm.EmailTitle;
                            toAddressListName = nm.EmailTo;
                            ccAddressListName = nm.EmailCC;
                        }
                        else if (nm.AutoReminderEmailFor == "MRNAccountsApprovalReminder")
                        {
                            pendingApprovals = db.IEPLStockIN_Head
                                .Where(approval => approval.MRNStoreApproval == 1 && approval.MRNQualityApproval == 1 && approval.MRNPlantHeadApproval == 1 && approval.MRNAccountApproval == 0 && DbFunctions.AddHours(approval.CreatedDate, 48) < DateTime.Now)
                                .Select(i => new
                                {
                                    VoucherNumber = i.Vouchernumber,
                                    VoucherDate = i.CreatedDate,
                                    CreatedBy = i.CreatedBy
                                })
                                .ToList();

                            emailSubject = nm.EmailSubject;
                            modelTitle = nm.EmailTitle;
                            toAddressListName = nm.EmailTo;
                            ccAddressListName = nm.EmailCC;
                        }

                        var pendingList = pendingApprovals as System.Collections.IList;
                        if (pendingList != null && pendingList.Count > 0)
                        {
                            var emailSettings = db.CMKL_Email_Setting.SingleOrDefault(ab => ab.id == 2);
                            var toAddresses = db.CMKL_Email.Where(bc => bc.DDLName == toAddressListName && bc.Active == 1).ToList();
                            var ccAddresses = db.CMKL_Email.Where(cd => cd.DDLName == ccAddressListName && cd.Active == 1).ToList();
                            var senderEmail = db.CMKL_Email.FirstOrDefault(se => se.DDLName == "ERPSender");

                            if (emailSettings == null || senderEmail == null || !toAddresses.Any())
                            {
                                WriteLog(logFilePath, "Email configuration or recipients missing.");
                                continue; // Go to the next job
                            }

                            MailMessage mail = new MailMessage();
                            SmtpClient SmtpServer = new SmtpClient(emailSettings.smtp);
                            mail.From = new MailAddress(senderEmail.Email);

                            foreach (var recipient in toAddresses)
                            {
                                mail.To.Add(recipient.Email);
                            }

                            foreach (var ccRecipient in ccAddresses)
                            {
                                mail.CC.Add(ccRecipient.Email);
                            }

                            mail.Subject = emailSubject;

                            var templatePath = Startup.DispatchTemplatePath;
                            var template = System.IO.File.ReadAllText(templatePath);

                            var model = new
                            {
                                Approvals = pendingApprovals,
                                Title = modelTitle
                            };

                            var body = Razor.Parse(template, model, null, null);

                            mail.Body = body;
                            mail.IsBodyHtml = true;

                            SmtpServer.Port = Convert.ToInt32(emailSettings.port);
                            SmtpServer.Credentials = new System.Net.NetworkCredential(senderEmail.Email, emailSettings.IT_password);
                            SmtpServer.EnableSsl = Convert.ToBoolean(emailSettings.ssl);

                            SmtpServer.Send(mail);
                            WriteLog(logFilePath, $"Reminder email sent successfully for job: {nm.AutoReminderEmailFor}.");
                        }
                    }
                }
            }
            catch (SmtpException ex)
            {
                WriteLog(logFilePath, $"SMTP Error - {ex.Message}");
            }
            catch (Exception ex)
            {
                WriteLog(logFilePath, $"Error sending email - {ex.Message}");
            }
        }

        private void WriteLog(string logFilePath, string message)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(logFilePath, true))
                {
                    writer.WriteLine($"{DateTime.Now}: {message}");
                }
            }
            catch (Exception)
            {
                // If logging fails, there's not much we can do.
            }
        }
    }
}