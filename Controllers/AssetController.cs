using CMKL.Models;
using DocumentFormat.OpenXml.Office2010.Excel;
using RazorEngine;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;
using System.Web.Services.Description;

namespace CMKL.Controllers
{
    public class AssetController : Controller
    {
        // GET: Assest
        IECEntities DB = new IECEntities();
        public ActionResult BreakdownManagement()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "BreakdownManagement" && ab.Status == true
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
        //=> View();

        // GET: View for historical downtime analysis
        public ActionResult BreakdownHistory() => View();
        [HttpGet]
        public JsonResult GetBreakdownDashboardKPIs()
        {
            try
            {
                int companyId = Convert.ToInt32(Session["Company_ID"]);
                DateTime now = DateTime.Now;
                DateTime monthStart = new DateTime(now.Year, now.Month, 1);

                int totalMachines = DB.Asset_MachineMaster.Count(x => x.CompanyID == companyId && x.Isdeleted == 0);
                int machinesDown = DB.Asset_BreakdownLog.Count(x => x.CompanyID == companyId && x.Status != "WORKING" && x.IsDeleted == false);
                int machinesWorking = totalMachines - machinesDown;

                var monthLogs = DB.Asset_BreakdownLog
                    .Where(x => x.CompanyID == companyId && x.IsDeleted == false && (x.StartTime >= monthStart || x.Status != "WORKING"))
                    .ToList();

                double totalMinutesMonth = 0;
                foreach (var log in monthLogs)
                {
                    DateTime startRange = log.StartTime < monthStart ? monthStart : log.StartTime;
                    DateTime endRange = (log.Status != "WORKING" || !log.EndTime.HasValue) ? now : log.EndTime.Value;
                    if (endRange > startRange) totalMinutesMonth += (endRange - startRange).TotalMinutes;
                }

                return Json(new
                {
                    success = true,
                    machinesDown = machinesDown,
                    machinesWorking = machinesWorking,
                    monthlyDowntime = string.Format("{0}h {1}m", (int)(totalMinutesMonth / 60), (int)(totalMinutesMonth % 60))
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet); }
        }
        [HttpGet]
        public JsonResult GetLiveBreakdowns()
        {
            try
            {
                if (Session["Company_ID"] == null) return Json(new { success = false, message = "Session Expired" }, JsonRequestBehavior.AllowGet);

                // FIX: Extract Session to local variable before LINQ query
                int companyId = Convert.ToInt32(Session["Company_ID"]);

                var data = (from b in DB.Asset_BreakdownLog
                            join m in DB.Asset_MachineMaster on b.MachineID equals m.id
                            join s in DB.Asset_SubunitMaster on b.SubUnitID equals s.id into subGroup
                            from s in subGroup.DefaultIfEmpty()
                            where b.Status != "WORKING" && b.CompanyID == companyId && b.IsDeleted == false
                            orderby b.StartTime ascending
                            select new
                            {
                                b.id,
                                m.MachineName,
                                SubUnit = s != null ? s.SubUnitName : "Whole Machine",
                                b.StartTime,
                                b.ProblemDescription,
                                b.ReportedBy,
                                b.Status,
                                b.IsMaintDone,
                                b.MaintDoneBy,
                                b.MaintRemarks
                            }).ToList()
                            .Select(x => new {
                                x.id,
                                x.MachineName,
                                x.SubUnit,
                                x.Status,
                                x.ProblemDescription,
                                x.ReportedBy,
                                x.IsMaintDone,
                                x.MaintDoneBy,
                                x.MaintRemarks,
                                FormattedStart = x.StartTime.ToString("dd/MM HH:mm"),
                                MinutesDown = (DateTime.Now - x.StartTime).TotalMinutes
                            }).ToList();

                return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet); }
        }

        [HttpPost]
        public JsonResult ReportBreakdown(int machineId, int? subunitId, string problem)
        {
            try
            {
                if (Session["Company_ID"] == null) return Json(new { success = false, message = "Session Expired" });

                // FIX: Extract Session variables
                int companyId = Convert.ToInt32(Session["Company_ID"]);
                string userName = Session["U_Name"]?.ToString();

                var existing = DB.Asset_BreakdownLog.FirstOrDefault(x => x.MachineID == machineId && x.Status != "WORKING" && x.IsDeleted == false);
                if (existing != null) return Json(new { success = false, message = "Machine is already reported as DOWN." });

                var log = new Asset_BreakdownLog
                {
                    MachineID = machineId,
                    SubUnitID = subunitId,
                    ProblemDescription = problem,
                    StartTime = DateTime.Now,
                    Status = "DOWN",
                    ReportedBy = userName,
                    CompanyID = companyId,
                    IsMaintDone = false,
                    IsAdminDone = false,
                    IsDeleted = false
                };
                DB.Asset_BreakdownLog.Add(log);
                DB.SaveChanges();
                return Json(new { success = true, message = "Breakdown logged. Maintenance notified." });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }
        [HttpPost]
        public JsonResult CompleteMaintenance(int logId, string remarks)
        {
            try
            {
                // 1. Capture Session values into local variables (prevents NotSupportedException)
                string userName = Session["U_Name"]?.ToString();
                if (string.IsNullOrEmpty(userName)) return Json(new { success = false, message = "Session Expired" });

                var entry = DB.Asset_BreakdownLog.Find(logId);
                if (entry == null) return Json(new { success = false, message = "Record not found." });

                entry.IsMaintDone = true;
                entry.MaintDoneDate = DateTime.Now;
                entry.MaintDoneBy = userName;
                entry.MaintRemarks = remarks;

                // FIX: Shortened to 10 chars to fit NVARCHAR(20) column
                entry.Status = "MAINT_DONE";

                DB.SaveChanges();
                return Json(new { success = true, message = "Stage 1: Maintenance Completed." });
            }
            catch (Exception ex)
            {
                // Return inner exception details if available
                string error = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Server Error: " + error });
            }
        }

        [HttpPost]
        public JsonResult FinalResolve(int logId, string adminRemarks)
        {
            try
            {
                string role = Session["U_Role"]?.ToString();
                if (role != "ASSETADMIN" && role != "SUPERADMIN")
                    return Json(new { success = false, message = "Access Denied: Only Admin can authorize final completion." });

                var entry = DB.Asset_BreakdownLog.Find(logId);
                if (entry == null) return Json(new { success = false, message = "Record not found." });

                entry.IsAdminDone = true;
                entry.AdminDoneDate = DateTime.Now;
                entry.AdminDoneBy = Session["U_Name"]?.ToString();
                entry.ResolutionDetails = adminRemarks;
                entry.EndTime = DateTime.Now;
                entry.Status = "WORKING";

                DB.SaveChanges();
                return Json(new { success = true, message = "Stage 2: Machine Restored to WORKING pool." });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }
        // Dropdown data fetchers
        public JsonResult GetMachineList()
        {
            int companyId = Convert.ToInt32(Session["Company_ID"]);
            var list = DB.Asset_MachineMaster.Where(x => x.CompanyID == companyId && x.Isdeleted == 0)
                         .Select(x => new { value = x.id, text = x.MachineName }).ToList();
            return Json(list, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetSubUnits(int machineId)
        {
            var list = DB.Asset_SubunitMaster.Where(x => x.MachineID == machineId && x.isdeleted == 0)
                         .Select(x => new { value = x.id, text = x.SubUnitName }).ToList();
            return Json(list, JsonRequestBehavior.AllowGet);
        }
        [HttpGet]
        public JsonResult GetMachineKPIs()
        {
            try
            {
                int companyId = Convert.ToInt32(Session["Company_ID"]);
                int total = DB.Asset_MachineMaster.Count(x => x.CompanyID == companyId && x.Isdeleted == 0);
                int withManuals = DB.Asset_MachineMaster.Count(x => x.CompanyID == companyId && x.Isdeleted == 0 && x.UserManual != null);
                int totalDepts = DB.Asset_MachineMaster.Where(x => x.CompanyID == companyId && x.Isdeleted == 0).Select(x => x.Department).Distinct().Count();

                return Json(new { success = true, total = total, manuals = withManuals, depts = totalDepts }, JsonRequestBehavior.AllowGet);
            }
            catch { return Json(new { success = false }, JsonRequestBehavior.AllowGet); }
        }
        [HttpGet]
        public JsonResult GetSubunitKPIs()
        {
            try
            {
                int companyId = Convert.ToInt32(Session["Company_ID"]);
                var allSubunits = DB.Asset_SubunitMaster.Where(x => x.CompanyID == companyId && x.isdeleted == 0).ToList();

                int totalSubunits = allSubunits.Count;
                int uniqueMachines = allSubunits.Select(x => x.MachineID).Distinct().Count();
                int recentEntries = allSubunits.Count(x => x.Createdon >= DateTime.Now.AddDays(-30));

                return Json(new { success = true, total = totalSubunits, machines = uniqueMachines, recent = recentEntries }, JsonRequestBehavior.AllowGet);
            }
            catch { return Json(new { success = false }, JsonRequestBehavior.AllowGet); }
        }
        [HttpGet]
        public JsonResult GetBreakdownHistoryData(string from, string to)
        {
            try
            {
                // 1. Session and Security Check
                if (Session["Company_ID"] == null)
                    return Json(new { success = false, message = "Session Expired" }, JsonRequestBehavior.AllowGet);

                int companyId = Convert.ToInt32(Session["Company_ID"]);

                // 2. Parse Date Range
                DateTime dtFrom = DateTime.Parse(from);
                DateTime dtTo = DateTime.Parse(to).AddDays(1).AddTicks(-1); // Include full end day

                // 3. Database Query with Joins
                var rawData = (from b in DB.Asset_BreakdownLog
                               join m in DB.Asset_MachineMaster on b.MachineID equals m.id
                               join s in DB.Asset_SubunitMaster on b.SubUnitID equals s.id into subGroup
                               from s in subGroup.DefaultIfEmpty()
                               where b.CompanyID == companyId
                                  && b.IsDeleted == false
                                  && b.StartTime >= dtFrom
                                  && b.StartTime <= dtTo
                               orderby b.StartTime descending
                               select new
                               {
                                   b.id,
                                   m.MachineName,
                                   SubUnit = s != null ? s.SubUnitName : "Whole Machine",
                                   b.StartTime,
                                   b.MaintDoneDate,
                                   b.EndTime,
                                   b.ProblemDescription,
                                   b.MaintRemarks,
                                   b.ResolutionDetails, // This maps to Admin Authorization Notes
                                   b.ReportedBy,
                                   b.MaintDoneBy,
                                   b.AdminDoneBy,
                                   b.Status
                               }).ToList();

                // 4. Memory-Side Processing (Formatting & Time Calculations)
                var processedData = rawData.Select(x => {

                    // Total Downtime logic (Start to End or Start to Now if Active)
                    double? totalMins = x.EndTime.HasValue
                        ? (x.EndTime.Value - x.StartTime).TotalMinutes
                        : (double?)null;

                    // Maintenance response logic (Start to MaintDoneDate)
                    double? maintMins = x.MaintDoneDate.HasValue
                        ? (x.MaintDoneDate.Value - x.StartTime).TotalMinutes
                        : (double?)null;

                    return new
                    {
                        x.id,
                        x.MachineName,
                        x.SubUnit,
                        x.Status,
                        FormattedStart = x.StartTime.ToString("dd/MM HH:mm"),
                        FormattedMaint = x.MaintDoneDate?.ToString("dd/MM HH:mm") ?? "Pending",
                        FormattedEnd = x.EndTime?.ToString("dd/MM HH:mm") ?? "Active",

                        // Raw minutes for JS KPI calculations
                        DurationMinutes = totalMins ?? (DateTime.Now - x.StartTime).TotalMinutes,

                        // Text for Display
                        DurationText = totalMins.HasValue
                            ? string.Format("{0}h {1}m", (int)(totalMins.Value / 60), (int)(totalMins.Value % 60))
                            : "Ongoing",

                        MaintResponse = maintMins.HasValue
                            ? string.Format("{0}h {1}m", (int)(maintMins.Value / 60), (int)(maintMins.Value % 60))
                            : "--",

                        Problem = x.ProblemDescription ?? "---",
                        MaintFix = x.MaintRemarks ?? "---",
                        AdminFinal = x.ResolutionDetails ?? "---",

                        // Concatenated staff string for compact view
                        Staff = $"Rep: {x.ReportedBy} | Maint: {x.MaintDoneBy ?? "--"} | Auth: {x.AdminDoneBy ?? "--"}"
                    };
                }).ToList();

                return Json(new { success = true, data = processedData }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                // Return clear error message to frontend toastr
                return Json(new { success = false, message = "Report Error: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult Index()
        {
            return View();
        }
        public ActionResult CreateBreakdown()
        {
            return View();
        }
        public class breakdownmodel
        {
            public int machineid { get; set; }
            public int subunitid { get; set; }
            public string ProblemDescription { get; set; }
        }
        
       
        public ActionResult MaintenanceReport()
        {
           
            ViewBag.Title = "Maintenance Report";
            // Initialize ViewModel with default values
            var model = new
            {
                // Default to last 30 days
                StartDate = DateTime.Today.AddDays(-30),
                EndDate = DateTime.Today,
                CompletionStatus = "All"
            };
            return View(model);
        
        }
        // AJAX endpoint to fetch report data based on filters
        public JsonResult GetReportData(DateTime? startDate, DateTime? endDate, string completionStatus)
        {
            try
            {
                // Get CompanyID from session
                int companyId = Convert.ToInt32(Session["Company_ID"]);

                // Ensure dates are valid
                if (startDate == null || endDate == null || startDate > endDate)
                {
                    return Json(new { success = false, message = "Invalid date range provided." }, JsonRequestBehavior.AllowGet);
                }

                // Adjust endDate to include the entire day
                DateTime adjustedEndDate = endDate.Value.Date.AddDays(1).AddTicks(-1);

                // Start with a base query on MaintenanceLogbook
                var baseQuery = (from log in DB.Asset_MaintenanceLogbook
                                 where log.IsDeleted == 0 && log.CreatedOn >= startDate && log.CreatedOn <= adjustedEndDate
                                 join alloc in DB.Asset_AllocationMaster on log.AllocationMasterId equals alloc.id
                                 where alloc.CompanyID == companyId
                                 // Joins to get display names
                                 join machine in DB.Asset_MachineMaster on alloc.MachineID equals machine.id into machGroup
                                 from machine in machGroup.DefaultIfEmpty()
                                 join subunit in DB.Asset_SubunitMaster on alloc.SubUnitID equals subunit.id into subGroup
                                 from subunit in subGroup.DefaultIfEmpty()
                                 join equipment in DB.Asset_TastMaster on alloc.TaskId equals equipment.id into equipGroup
                                 from equipment in equipGroup.DefaultIfEmpty()
                                 join freq in DB.Asset_FrequencyMaster on alloc.FrequencyID equals freq.id into freqGroup
                                 from freq in freqGroup.DefaultIfEmpty()
                                 select new //MaintenanceReportRecordDTO // Project to DTO
                                 {
                                     Id = log.Id,
                                     MachineName = machine.MachineName ?? "N/A",
                                     SubunitName = subunit.SubUnitName ?? "N/A",
                                     EquipmentName = equipment.TaskName ?? "N/A",
                                     FrequencyName = freq.FrequencyName ?? "N/A",
                                     DueDate = log.DueDate,
                                     IsCompleted = log.IsCompleted,
                                     CompletionDate = log.CompletionDate,
                                     CompletionRemarks = log.CompletionRemarks
                                 });

                // Apply filtering based on completion status
                if (completionStatus == "Completed")
                {
                    baseQuery = baseQuery.Where(r => r.IsCompleted == true);
                }
                else if (completionStatus == "Pending")
                {
                    baseQuery = baseQuery.Where(r => r.IsCompleted == false);
                }
                // If "All", no additional filter is needed

                var records = baseQuery.OrderByDescending(r => r.DueDate).ToList();

                if (records.Any())
                {
                    return Json(new { success = true, data = records, message = $"{records.Count} records found." }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = true, message = "No maintenance records found for the selected filters." }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching maintenance report: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                return Json(new { success = false, message = "An error occurred while retrieving the report." }, JsonRequestBehavior.AllowGet);
            }
        }
    
        public ActionResult PendingApproval()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "PendingApproval" && ab.Status == true
                               select ab).SingleOrDefault();
            if (checkaccess == null)
            {
                // 3. Store error message in TempData (survives one redirect)
                TempData["ErrorMessage"] = "Access Denied: You are not authorized to view This Page.";

                // 4. Redirect to Home Controller, Index Action
                return RedirectToAction("Dashboard", "Dashboard");
            }
            ViewBag.Title = "Maintenance Log Book (Pending Approval)";
            return View();
        }

        // GET: MaintenanceLog/GetLogsForApproval
        // AJAX endpoint to fetch all completed logs that are awaiting approval.
        public JsonResult GetLogsForApproval()
        {
            try
            {
                // Assuming CompanyId is accessible from a BaseController property or Session
                int companyId = Convert.ToInt32(Session["Company_ID"]);

                var logsForApproval = (from log in DB.Asset_MaintenanceLogbook
                                           // Only select logs that are completed but not yet approved
                                       where log.IsCompleted == true && log.AdminApproval == false && log.IsDeleted == 0
                                       join alloc in DB.Asset_AllocationMaster on log.AllocationMasterId equals alloc.id
                                       join machine in DB.Asset_MachineMaster on alloc.MachineID equals machine.id
                                       join sub in DB.Asset_SubunitMaster on alloc.SubUnitID equals sub.id
                                       join task in DB.Asset_TastMaster on alloc.TaskId equals task.id
                                       where log.CompanyID == companyId
                                       orderby log.CompletionDate descending
                                       select new
                                       {
                                           Id = log.Id,
                                           MachineName = machine.MachineName,
                                           Subunit=sub.SubUnitName,
                                           DueDate = log.DueDate,
                                           CompletionDate = log.CompletionDate,
                                           Task= task.TaskName,
                                           CompletionRemarks = log.CompletionRemarks,
                                           CompletedBy = log.CompletedBy // Assuming CompletedBy is in your logbook model
                                       }).ToList();

                if (logsForApproval.Any())
                {
                    return Json(new { success = true, data = logsForApproval, message = "Logs for approval loaded successfully." }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = true, data = new List<object>(), message = "No pending maintenance logs for approval found." }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching logs for approval: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                return Json(new { success = false, message = "An error occurred while retrieving logs for approval." }, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult PendingSAdminApproval()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "PendingSAdminApproval" && ab.Status == true
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
        public JsonResult GetLogsForApprovalSAdmin()
        {
            try
            {
                // Assuming CompanyId is accessible from a BaseController property or Session
                int companyId = Convert.ToInt32(Session["Company_ID"]);

                var logsForApproval = (from log in DB.Asset_MaintenanceLogbook
                                           // Only select logs that are completed but not yet approved
                                       where log.IsCompleted == true && log.AdminApproval == true && log.SAdminApproval==false && log.IsDeleted == 0
                                       join alloc in DB.Asset_AllocationMaster on log.AllocationMasterId equals alloc.id
                                       join machine in DB.Asset_MachineMaster on alloc.MachineID equals machine.id
                                       where log.CompanyID == companyId
                                       orderby log.CompletionDate descending
                                       select new
                                       {
                                           Id = log.Id,
                                           MachineName = machine.MachineName,
                                           DueDate = log.DueDate,
                                           CompletionDate = log.CompletionDate,
                                           CompletionRemarks = log.CompletionRemarks,
                                           CompletedBy = log.CompletedBy // Assuming CompletedBy is in your logbook model
                                       }).ToList();

                if (logsForApproval.Any())
                {
                    return Json(new { success = true, data = logsForApproval, message = "Logs for approval loaded successfully." }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = true, data = new List<object>(), message = "No pending maintenance logs for approval found." }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching logs for approval: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                return Json(new { success = false, message = "An error occurred while retrieving logs for approval." }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpPost]
        public JsonResult CompleteApproval(int logId)
        {
            try
            {
                var logbookEntry = DB.Asset_MaintenanceLogbook.FirstOrDefault(l => l.Id == logId && l.IsDeleted == 0);

                if (logbookEntry == null)
                {
                    return Json(new { success = false, message = "Logbook entry not found." });
                }

                // Update the approval fields
                logbookEntry.AdminApproval = true;
                logbookEntry.AdminApprovalDate = DateTime.Now;
                logbookEntry.ApprovedBY = Session["U_Name"]?.ToString() ?? "System";
                // You can also add who approved it if you have that field
                // logbookEntry.ApprovedBy = Session["U_Name"]?.ToString() ?? "System";

                DB.SaveChanges();

                return Json(new { success = true, message = "Maintenance log approved successfully." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error approving log ID {logId}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                return Json(new { success = false, message = "An error occurred while approving the log." });
            }
        }
        [HttpPost]
        public JsonResult CompleteApprovalSAdmin(int logId)
        {
            try
            {
                var logbookEntry = DB.Asset_MaintenanceLogbook.FirstOrDefault(l => l.Id == logId && l.IsDeleted == 0);

                if (logbookEntry == null)
                {
                    return Json(new { success = false, message = "Logbook entry not found." });
                }

                // Update the approval fields
                logbookEntry.SAdminApproval = true;
                logbookEntry.SAdminApprovaDate = DateTime.Now;
                logbookEntry.SAdminBy = Session["U_Name"]?.ToString() ?? "System";
                // You can also add who approved it if you have that field
                // logbookEntry.ApprovedBy = Session["U_Name"]?.ToString() ?? "System";

                DB.SaveChanges();

                return Json(new { success = true, message = "Maintenance log approved successfully." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error approving log ID {logId}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                return Json(new { success = false, message = "An error occurred while approving the log." });
            }
        }

        public ActionResult PendingMaintananceLogBook()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "PendingMaintananceLogBook" && ab.Status == true
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
        public JsonResult GetPendingTasks()
        {
            try
            {
                // Assuming CompanyId is accessible from a BaseController property or Session
                int companyId = Convert.ToInt32(Session["Company_ID"]);
                int userid = Convert.ToInt32(Session["Userid"]);

                var getaccess = (from ab in DB.Asset_UserTaskAllocation
                                 where ab.isdeleted == false && ab.UserId == userid
                                 select ab.TaskTypeID).ToList();

                var pendingTasks = (from log in DB.Asset_MaintenanceLogbook
                                    where log.IsCompleted == false && log.IsDeleted == 0

                                    // Join to Allocation Master to get the TaskId/TaskTypeID
                                    join alloc in DB.Asset_AllocationMaster on log.AllocationMasterId equals alloc.id

                                    // --- APPLY FILTER HERE: Only include logs where the TaskTypeID is in the authorized list ---
                                    where getaccess.Contains(alloc.TaskType)

                                    // Joins to get display names
                                    join machine in DB.Asset_MachineMaster on alloc.MachineID equals machine.id into machGroup
                                    from machine in machGroup.DefaultIfEmpty()
                                    join subunit in DB.Asset_SubunitMaster on alloc.SubUnitID equals subunit.id into subGroup
                                    from subunit in subGroup.DefaultIfEmpty()
                                    join own in DB.Asset_TaskType on alloc.TaskType equals own.ID into ownGroup
                                    from own in ownGroup.DefaultIfEmpty()

                                        // Join to TaskMaster (which I assume contains the task type)
                                    join equipment in DB.Asset_TastMaster on alloc.TaskId equals equipment.id into equipGroup
                                    from equipment in equipGroup.DefaultIfEmpty()

                                    join freq in DB.Asset_FrequencyMaster on alloc.FrequencyID equals freq.id into freqGroup
                                    from freq in freqGroup.DefaultIfEmpty()

                                        // Existing company filter and final checks
                                    where log.CompanyID == companyId && alloc.IsDeleted == 0

                                    orderby log.DueDate descending
                                    select new
                                    {
                                        Id = log.Id,
                                        AllocationId = alloc.id,
                                        MachineName = machine.MachineName ?? "N/A",
                                        SubunitName = subunit.SubUnitName ?? "N/A",
                                        EquipmentName = equipment.TaskName ?? "N/A",
                                        FrequencyName = freq.FrequencyName ?? "N/A",
                                        DueDate = log.DueDate,
                                        Owner=own.TaskType,
                                        HasItems = DB.Asset_MaintenanceLogbookItems.Any(item => item.LogbookId == log.Id && item.IsDeleted == 0)
                                    }).ToList();

                if (pendingTasks.Any())
                {
                    return Json(new { success = true, data = pendingTasks, message = "Pending tasks loaded successfully." }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = true, data = new List<object>(), message = "No pending maintenance tasks found." }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching pending maintenance tasks: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                return Json(new { success = false, message = "An error occurred while retrieving pending tasks." }, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult MaintenanceScheduleReport()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "MaintenanceScheduleReport" && ab.Status == true
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
        public class PendingTaskViewModel
        {
            public int Id { get; set; }
            public int AllocationMasterId { get; set; }
            public string MachineName { get; set; }
            public string SubUnit { get; set; }
            public string TaskName { get; set; }
            public DateTime? DueDate { get; set; }
            public bool IsCompleted { get; set; }
            public bool AdminApproval { get; set; }
            public string CreatedBy { get; set; }
            public string StatusDescription { get; set; }
            public DateTime? CreatedOn { get; set; }
            public DateTime? CompeltionDate { get; set; }
            public string Action { get; set; }
        }
        [HttpPost]
        public JsonResult GetPendingTasksData(string fromDate, string toDate, string pendingType)
        {
            try
            {
                int companyId = Convert.ToInt32(Session["Company_ID"]);
                DateTime start = string.IsNullOrEmpty(fromDate) ? DateTime.MinValue : DateTime.Parse(fromDate);
                DateTime end = string.IsNullOrEmpty(toDate) ? DateTime.MaxValue : DateTime.Parse(toDate).AddDays(1).AddTicks(-1);

                var query = from log in DB.Asset_MaintenanceLogbook
                            join alloc in DB.Asset_AllocationMaster on log.AllocationMasterId equals alloc.id
                            join machine in DB.Asset_MachineMaster on alloc.MachineID equals machine.id
                            join sub in DB.Asset_SubunitMaster on alloc.SubUnitID equals sub.id
                            join task in DB.Asset_TastMaster on alloc.TaskId equals task.id
                            join own in DB.Asset_TaskType on alloc.TaskType equals own.ID into ownGroup
                            from own in ownGroup.DefaultIfEmpty()
                            where log.IsDeleted == 0 && log.CompanyID==companyId
                            && log.DueDate >= start
                            && log.DueDate <= end
                            select new { log, machine, task,own,sub };

                // Apply specific "Pending From" Logic
                if (pendingType == "Owner")
                {
                    // Task is not finished yet
                    query = query.Where(x => x.log.IsCompleted == false);
                }
                else if (pendingType == "Admin")
                {
                    // Task finished by owner, but not yet approved by admin
                    query = query.Where(x => x.log.IsCompleted == true && x.log.AdminApproval == false);
                }
                else if (pendingType == "SAdmin")
                {
                    // Task finished by owner, but not yet approved by admin
                    query = query.Where(x => x.log.IsCompleted == true && x.log.AdminApproval == true && x.log.SAdminApproval==false);
                }
                else if (pendingType == "Completed")
                {
                    // Task finished by owner, but not yet approved by admin
                    query = query.Where(x => x.log.IsCompleted == true && x.log.AdminApproval == true && x.log.SAdminApproval==true);
                }

                var result = query.OrderByDescending(x => x.log.Id).ToList().Select(x => new PendingTaskViewModel
                {
                    Id = x.log.Id,
                    AllocationMasterId = x.log.AllocationMasterId,
                    MachineName = x.machine.MachineName,
                    SubUnit=x.sub.SubUnitName,
                    TaskName = x.task.TaskName,
                    DueDate = x.log.DueDate,
                    IsCompleted = x.log.IsCompleted,// ?? false,
                    AdminApproval = x.log.AdminApproval ?? false,
                    CreatedBy = x.own.TaskType,
                    CreatedOn = x.log.CreatedOn,
                    CompeltionDate=x.log.CompletionDate,
                    Action = x.log.CompletionRemarks ?? "N/A",                    
                    //StatusDescription = (x.log.IsCompleted == false) ? "Pending from Owner" : "Awaiting Admin Approval"
                    StatusDescription = (x.log.IsCompleted == false) ? "Pending from Owner" : (x.log.AdminApproval == false ? "Awaiting Admin Approval" : "Completed")
                }).ToList();

                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }
        // GET: MaintenanceLog/GetLogbookItems
        // AJAX endpoint to fetch items for a specific logbook entry.
        public JsonResult GetLogbookItems(int logbookId)
        {
            try
            {
                var items = (from logItem in DB.Asset_MaintenanceLogbookItems
                             join itemMaster in DB.BOMItemMasters on logItem.ItemId equals itemMaster.Itemid
                             where logItem.LogbookId == logbookId && logItem.IsDeleted == 0
                             select new
                             {
                                 ItemName = itemMaster.ItemName ?? "N/A",
                                 Quantity = logItem.Quantity,
                                 UOM = logItem.UOM ?? "N/A"
                             }).ToList();

                if (items.Any())
                {
                    return Json(new { success = true, data = items, message = "Items retrieved successfully." }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = true, data = new List<object>(), message = "No items found for this task." }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching logbook items for ID {logbookId}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                return Json(new { success = false, message = "Error retrieving items for this task." }, JsonRequestBehavior.AllowGet);
            }
        }
        // POST: MaintenanceLog/CompleteTask
        // AJAX endpoint to mark a task as completed with user remarks.
        [HttpPost]
        public JsonResult CompleteTask(int logbookId, string remarks)
        {
            try
            {
                // Assuming you have CompanyID, U_Name (UserID) in session
                string completedBy = Session["U_Name"]?.ToString() ?? "System";

                var logbookEntry = DB.Asset_MaintenanceLogbook.FirstOrDefault(l => l.Id == logbookId && l.IsDeleted == 0);

                if (logbookEntry == null)
                {
                    return Json(new { success = false, message = "Logbook entry not found." });
                }

                // Update the logbook entry
                logbookEntry.IsCompleted = true;
                logbookEntry.CompletionDate = DateTime.Now;
                logbookEntry.CompletionRemarks = remarks;
                logbookEntry.CompletedBy = completedBy;
                // logbookEntry.CompletedOn= DateTime.Now;

                // Get the parent allocation master record
                var allocationMaster = DB.Asset_AllocationMaster.FirstOrDefault(a => a.id == logbookEntry.AllocationMasterId);
                if (allocationMaster != null)
                {
                    // Update LastMaintenanceDoneOn in the parent record
                    allocationMaster.LastMaintenanceDoneOn = DateTime.Now;
                }

                DB.SaveChanges();
                int ID = logbookId;
                // Fetch all details needed for the email model
                

                //if (emailDetails != null)
                {
                    SendEmail(ID); // Call the helper method to send the email
                }


                return Json(new { success = true, message = "Maintenance task marked as completed successfully." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error completing task for ID {logbookId}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                return Json(new { success = false, message = "An error occurred while completing the task." });
            }
        }
        
        private void SendEmail(int ID)
        {
            try
            {
                var emailDetails = (from log in DB.Asset_MaintenanceLogbook
                                    where log.Id == ID
                                    join alloc in DB.Asset_AllocationMaster on log.AllocationMasterId equals alloc.id
                                    join machine in DB.Asset_MachineMaster on alloc.MachineID equals machine.id
                                    join sub in DB.Asset_SubunitMaster on alloc.SubUnitID equals sub.id
                                    join task in DB.Asset_TastMaster on alloc.TaskId equals task.id
                                    // Get all items for this log
                                    // let itemsUsed = (from logItem in DB.Asset_MaintenanceLogbookItems
                                    //    where logItem.LogbookId == log.Id
                                    //    join itemMaster in DB.BOMItemMasters on logItem.ItemId equals itemMaster.Itemid
                                    //    select new
                                    //   {
                                    //      ItemName = itemMaster.ItemName,
                                    //    Quantity = logItem.Quantity,
                                    //    UOM = logItem.UOM
                                    // }).ToList()
                                    select new
                                    {
                                        AssetID=machine.AssetID,
                                        Subject = "Maintanance Completed",
                                        MachineName = machine.MachineName,
                                        SubunitName = sub.SubUnitName,
                                        EquipmentName = task.TaskName,
                                        CompletedBy = log.CompletedBy,
                                        CompletionDate = log.CompletionDate.Value,
                                        DueDate = log.DueDate,
                                        CompletionRemarks = log.CompletionRemarks,
                                        // ItemsUsed = itemsUsed,
                                        CompanyID = log.CompanyID
                                    }).SingleOrDefault();
                var itemused = (from ab in DB.Asset_MaintenanceLogbookItems
                                join im in DB.BOMItemMasters on ab.ItemId equals im.Itemid
                                //join um in DB.BOM_UOM on a
                                where ab.LogbookId == ID
                                select new
                                {
                                    ItemName=im.ItemName,
                                    Quantity=ab.Quantity,
                                    UOM=ab.UOM

                                }).ToList();

                var emailSettings = DB.CMKL_Email_Setting.SingleOrDefault(s => s.id == 2);
                var toAddresses = DB.CMKL_Email.Where(r => r.DDLName == "MaintenanceCompletionRecipients" && r.Active == 1 && r.CompanyID==emailDetails.CompanyID).ToList();
                var senderEmail = DB.CMKL_Email.FirstOrDefault(se => se.DDLName == "ERPSender");

                if (emailSettings == null || senderEmail == null || !toAddresses.Any())
                {
                    System.Diagnostics.Debug.WriteLine("Maintenance Completion Email: Email config or recipients missing.");
                    return;
                }

                var emailModel = new
                {
                    MachineName=emailDetails.MachineName,
                    DueDate=emailDetails.DueDate,
                    CompletedBy=emailDetails.CompletedBy,
                    CompletionDate = emailDetails.CompletionDate,
                    CompletionRemarks=emailDetails.CompletionRemarks,
                    ItemsUsed= itemused,
                    Subject= "Maintenance Pending For Approval"
                };
                var templatePath = System.Web.Hosting.HostingEnvironment.MapPath("~/Views/EmailManage/MaintenanceCompletionEmail.cshtml");
                if (!System.IO.File.Exists(templatePath))
                {
                    System.Diagnostics.Debug.WriteLine($"Maintenance Completion Email: Template not found at {templatePath}.");
                    return;
                }
                var template = System.IO.File.ReadAllText(templatePath);
                var body = Razor.Parse(template, emailModel);

                MailMessage mail = new MailMessage();
                SmtpClient SmtpServer = new SmtpClient(emailSettings.smtp);
                mail.From = new MailAddress(senderEmail.Email);
                foreach (var r in toAddresses) mail.To.Add(r.Email);
                mail.Subject = "Maintenance Pending For Approval";
                mail.Body = body;
                mail.IsBodyHtml = true;
                SmtpServer.Port = Convert.ToInt32(emailSettings.port);
                SmtpServer.Credentials = new System.Net.NetworkCredential(senderEmail.Email, emailSettings.IT_password);
                SmtpServer.EnableSsl = Convert.ToBoolean(emailSettings.ssl);
                SmtpServer.Send(mail);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending completion email: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            }
        }
        public ActionResult AssetAllocationMaster()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "AssetAllocationMaster" && ab.Status == true
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
        public ActionResult MachineMaster()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "MachineMaster" && ab.Status == true
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
        public ActionResult GetSubUnit(int? MachineID)
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var subunit = (from ab in DB.Asset_SubunitMaster
                           where ab.isdeleted == 0 && ab.CompanyID == companyid && ab.MachineID==MachineID
                           select new
                           {
                               value = ab.id,
                               text = ab.SubUnitName
                           }).ToList();
            if(subunit.Any())
            {
                return Json(new { success = true, subunit }, JsonRequestBehavior.AllowGet);
            }
            else
            {
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult DownloadUserManual(int id) // 'id' parameter corresponds to machine.Id
        {
            try
            {
                // Retrieve the machine record, focusing on the UserManual data
                var machine = DB.Asset_MachineMaster
                                .Where(m => m.id == id && m.Isdeleted == 0)
                                .Select(m => new {
                                    m.AssetID, // For generating a sensible filename
                                    m.MachineName, // For generating a sensible filename
                                    m.UserManual // The VARBINARY(MAX) content
                                })
                                .SingleOrDefault();

                if (machine == null || machine.UserManual == null || machine.UserManual.Length == 0)
                {
                    // No machine found, no manual, or empty manual
                    return HttpNotFound("User Manual not found or is empty.");
                }

               
                string fileName = $"{machine.MachineName.Replace(" ", "_")}_{machine.AssetID}_Manual.pdf"; // Example filename
                string contentType = "application/pdf"; // Defaulting to PDF as it's common for manuals

                
                return File(machine.UserManual, contentType, fileName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error downloading user manual for ID {id}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                // Return a generic error message or a 404
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Error retrieving user manual.");
            }
        }
        public ActionResult GetMachineMasters()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var data = (from ab in DB.Asset_MachineMaster
                        join dep in DB.DepartmentMasters on ab.Department equals dep.id
                        where ab.CompanyID == companyid && ab.Isdeleted == 0
                        select new
                        {
                            AssetID = ab.AssetID,
                            MachineName = ab.MachineName,
                            DepartmentName = dep.DepartmentName,
                            HasUserManual = ab.AssetID,
                            Id =ab.id,
                        }).ToList();

            if (data.Any())
            { 
                return Json(new { success = true, data }, JsonRequestBehavior.AllowGet);
            }
            else
            {
                return Json(new { success = false },JsonRequestBehavior.AllowGet);
            }

        }
        [HttpGet]
        public JsonResult GetMTTRTrend()
        {
            try
            {
                if (Session["Company_ID"] == null) return Json(new { success = false });
                int companyId = Convert.ToInt32(Session["Company_ID"]);

                // Look at the last 6 months of resolved breakdowns
                DateTime sixMonthsAgo = DateTime.Today.AddMonths(-6);

                var rawData = DB.Asset_BreakdownLog
                                .Where(x => x.CompanyID == companyId
                                         && x.Status == "WORKING"
                                         && x.EndTime != null
                                         && x.StartTime >= sixMonthsAgo)
                                .ToList();

                var trend = rawData
                    .GroupBy(x => new { x.EndTime.Value.Year, x.EndTime.Value.Month })
                    .Select(g => new
                    {
                        Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yy"),
                        // Calculate average hours per month: (Total Hours Down / Incident Count)
                        AvgHours = Math.Round(g.Average(x => (x.EndTime.Value - x.StartTime).TotalHours), 1),
                        SortKey = g.Key.Year * 100 + g.Key.Month
                    })
                    .OrderBy(x => x.SortKey)
                    .ToList();

                return Json(new { success = true, data = trend }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        public JsonResult GetSubunitMasters()
        {
            try
            {
                // Assuming your Asset_SubunitMaster table has MachineID (FK to MachineMaster)
                // You'll need to join to Asset_MachineMaster to get the MachineName for display.
                int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
                var subunits = (from s in DB.Asset_SubunitMaster
                                join m in DB.Asset_MachineMaster on s.MachineID equals m.id into machineGroup // Join to get MachineName
                                from m in machineGroup.DefaultIfEmpty() // LEFT JOIN to include subunits even if machine is missing
                                where s.isdeleted == 0 && s.CompanyID==companyid
                                orderby s.SubUnitName // Order for consistent display
                                select new
                                {
                                    Id = s.id,
                                    SubUnitName = s.SubUnitName,
                                    Description = s.Desc,
                                    MachineId = s.MachineID, // ID
                                    MachineName = m.MachineName ?? "N/A" // Display name from machine master
                                }).ToList();

                if (subunits.Any())
                {
                    return Json(new { success = true, data = subunits }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = true, data = new List<object>(), message = "No subunit records found." }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching subunit masters: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                return Json(new { success = false, message = "Error fetching subunit records." }, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult GetmachineData()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var machine = (from ab in DB.Asset_MachineMaster
                           where ab.Isdeleted == 0 && ab.CompanyID == companyid
                           select new
                           {
                               value = ab.id,
                               text = ab.MachineName
                           }).ToList();
            var subunit = (from ab in DB.Asset_SubunitMaster
                           where ab.isdeleted == 0 && ab.CompanyID == companyid
                           select new
                           {
                               value = ab.id,
                               text = ab.SubUnitName
                           }).ToList();
            var taskname = (from ab in DB.Asset_TastMaster
                           where ab.IsDeleted== 0 && ab.CompanyID == companyid
                           select new
                           {
                               value = ab.id,
                               text = ab.TaskName
                           }).ToList();
            var Frequency = (from ab in DB.Asset_FrequencyMaster
                             where ab.isdeleted == 0// && ab.companyid == companyid
                             select new
                             {
                                 value = ab.id,
                                 text = ab.FrequencyName,
                                 Days = ab.Days,
                             }).ToList();
            var TaskType=(from ab in DB.Asset_TaskType
                          where ab.IsDeleted==false && ab.CompanyID == companyid    
                          select new
                          {
                              value=ab.ID,
                              text=ab.TaskType

                          }).ToList();

            return Json(new {success=true, machine, subunit, taskname, Frequency, TaskType},JsonRequestBehavior.AllowGet );
        }
        public ActionResult GetDepartments()
        {
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
            var data = (from ab in DB.DepartmentMasters
                       where ab.CompanyID==companyid
                       select new
                       {
                           value= ab.id,
                           text=ab.DepartmentName,
                       }).ToList(); 
            return Json(new {success=true,data},JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public JsonResult SaveMachineMaster(MachineMasterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                            .Select(e => e.ErrorMessage)
                                            .ToList();
                return Json(new { success = false, message = "Validation failed: " + string.Join("; ", errors) });
            }
            if (DB.Asset_MachineMaster.Any(ab => ab.AssetID == model.AssetID && ab.Isdeleted == 0)) // Added Isdeleted check for robustness
            {
                return Json(new { success = false, message = "Asset ID Already Exists." }, JsonRequestBehavior.AllowGet);
            }

            try
            {
                int companyId = Convert.ToInt32(Session["Company_ID"]);
                string createdBy = Session["U_Name"]?.ToString() ?? "System";

                // --- MODIFIED: Handle User Manual File Upload to VARBINARY(MAX) ---
                byte[] userManualBytes = null;
                if (model.UserManualFile != null && model.UserManualFile.ContentLength > 0)
                {
                    // Read the file content into a byte array
                    using (BinaryReader br = new BinaryReader(model.UserManualFile.InputStream))
                    {
                        userManualBytes = br.ReadBytes(model.UserManualFile.ContentLength);
                    }
                }

                Asset_MachineMaster AMM = new Asset_MachineMaster();
                AMM.AssetID=model.AssetID;
                AMM.MachineName = model.MachineName;
                AMM.MachineDesc = model.MachineDesc;    
                AMM.Department=model.DepartmentId;
                AMM.UserManual= userManualBytes;
                AMM.CompanyID = companyId;
                AMM.Isdeleted = 0;
                AMM.CommisoningDate = model.CDate;
                AMM.Createdon = DateTime.Now;
                AMM.CreatedBy = createdBy;

                // Create a new Asset_MachineMaster entity to save to DB
               

                DB.Asset_MachineMaster.Add(AMM);
                DB.SaveChanges();

                return Json(new { success = true, message = "Machine Master saved successfully!" });
            }
            
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving machine master: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                return Json(new { success = false, message = "An error occurred while saving machine master. Please try again." });
            }
        }

        public class MachineMasterViewModel
        {

            public string AssetID { get; set; }

            [Required(ErrorMessage = "Machine Name is required.")]
            public string MachineName { get; set; }


            public string MachineDesc { get; set; }

            [Required(ErrorMessage = "Department is required.")]
            public int? DepartmentId { get; set; } // Matches the 'name' attribute of your dropdown in HTML
            public DateTime CDate { get; set; }

            public HttpPostedFileBase UserManualFile { get; set; }


        }
        public ActionResult SubUnitMaster()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "SubUnitMaster" && ab.Status == true
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
        public class SubunitMasterViewModel
        {
            [Required(ErrorMessage = "Subunit Name is required.")]
            public string SubUnitName { get; set; }

            public string Desc { get; set; }
            public int MachineID { get; set; }

        }
        [HttpPost]
        public JsonResult SaveSubunitMaster(SubunitMasterViewModel model)
        {
            // Basic server-side validation
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                            .Select(e => e.ErrorMessage)
                                            .ToList();
                return Json(new { success = false, message = "Validation failed: " + string.Join("; ", errors) });
            }

            try
            {
                //check duplicate
                var check = DB.Asset_SubunitMaster.Any(x => x.SubUnitName == model.SubUnitName && x.MachineID == model.MachineID && x.isdeleted==0);
                if (check) // No need for '== true'
                {
                    // Duplicate found
                    return Json(new { success = false, message = "Subunit Name already exists for this Machine." });
                }

                // Retrieve CompanyID and CreatedBy from session (assuming they're always available)
                int companyId = Convert.ToInt32(Session["Company_ID"]);
                string createdBy = Session["U_Name"]?.ToString() ?? "System";

                // Create a new Asset_SubunitMaster entity
                Asset_SubunitMaster newSubunit = new Asset_SubunitMaster
                {
                    SubUnitName = model.SubUnitName,
                    MachineID = model.MachineID,
                    Desc = model.Desc,
                    isdeleted = 0, // Default to not deleted
                    CompanyID = companyId,
                    CreatedBy = createdBy,
                    Createdon = DateTime.Now
                };

                DB.Asset_SubunitMaster.Add(newSubunit);
                DB.SaveChanges();

                return Json(new { success = true, message = "Subunit Master saved successfully!" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving subunit master: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                return Json(new { success = false, message = "An error occurred while saving subunit master. Please try again." });
            }
        }
        
        public ActionResult TaskMaster()
        {
            int currentUserId = Convert.ToInt32(Session["Userid"].ToString());
            var checkaccess = (from ab in DB.User_Access
                               where ab.UserID == currentUserId && ab.MenuAccess == "TaskMaster" && ab.Status == true
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
        public class TaskMasterViewModel
        {
            [Required(ErrorMessage = "Equipment Name is required.")]
            public string TaskName { get; set; }

            public string TaskDesc { get; set; }
            public int MachineID {  get; set; }
            public int SubUnitID { get; set; }  

            // Hidden fields, typically passed from client or session
            // public int CompanyID { get; set; } 
        }

        [HttpPost]
        public JsonResult SaveTaskMaster(TaskMasterViewModel model)
        {
            // Basic server-side validation
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                            .Select(e => e.ErrorMessage)
                                            .ToList();
                return Json(new { success = false, message = "Validation failed: " + string.Join("; ", errors) });
            }

            try
            {

                //check Duplicate

                var check = DB.Asset_TastMaster.Any(x => x.MachineID == model.MachineID && x.SubUnitID == model.SubUnitID && x.TaskName == model.TaskName && x.IsDeleted == 0);
                if(check)
                {
                    return Json(new { success = false, message = "Task Name already exists for this Combination." });

                }

                // Retrieve CompanyID and CreatedBy from session (assuming they're always available)
                int companyId = Convert.ToInt32(Session["Company_ID"]);
                string createdBy = Session["U_Name"]?.ToString() ?? "System";

                // Create a new Asset_EquipmentMaster entity
                Asset_TastMaster newTask = new Asset_TastMaster
                //Asset_EquipmentMaster newEquipment = new Asset_EquipmentMaster
                {
                    TaskName = model.TaskName,
                    TaskDesc = model.TaskDesc,
                    MachineID=model.MachineID,
                    SubUnitID=model.SubUnitID,
                    IsDeleted = 0, // Default to not deleted
                    CompanyID = companyId,
                    CreatedBy = createdBy,
                    CreatedOn = DateTime.Now
                };

                DB.Asset_TastMaster.Add(newTask);
                DB.SaveChanges();

                return Json(new { success = true, message = "Task Master saved successfully!" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving equipment master: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                return Json(new { success = false, message = "An error occurred while saving equipment master. Please try again." });
            }
        }
        public ActionResult GetTasks()
        {
            try
            {
                int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
                var data = (from ab in DB.Asset_TastMaster
                            join mc in DB.Asset_MachineMaster on ab.MachineID equals mc.id
                            join sub in DB.Asset_SubunitMaster on ab.SubUnitID equals sub.id

                            where ab.CompanyID == companyid && ab.IsDeleted == 0
                            select new
                            {
                                MachineName = mc.MachineName,
                                SubUnitName = sub.SubUnitName,
                                TaskName = ab.TaskName,
                                Desc = ab.TaskDesc,
                            }).ToList();
                if (data.Any())
                {
                    return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = true, data = new List<object>(), message = "No subunit records found." }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching subunit masters: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                return Json(new { success = false, message = "Error fetching subunit records." }, JsonRequestBehavior.AllowGet);
            }

        }
        public ActionResult GetTaskBYID(int MachineID, int SubunitId)
        {
            try
            {
                int companyid = Convert.ToInt32(Session["Company_ID"].ToString());
                var data = (from ab in DB.Asset_TastMaster
                           // join mc in DB.Asset_MachineMaster on ab.MachineID equals mc.id
                           // join sub in DB.Asset_SubunitMaster on ab.SubUnitID equals sub.id

                            where ab.CompanyID == companyid && ab.IsDeleted == 0 && ab.MachineID==MachineID && ab.SubUnitID==SubunitId
                            select new
                            {
                                text= ab.TaskName,
                                value=ab.id,
                            }).ToList();
                if (data.Any())
                {
                    return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = true, data = new List<object>(), message = "No Tasks Found." }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching subunit masters: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                return Json(new { success = false, message = "Error fetching subunit records." }, JsonRequestBehavior.AllowGet);
            }
        }
        public class AllocatedItemDetailDTO
        {
            // These properties match what you store in your JavaScript 'allocatedItemsData' array
            public string ItemCode { get; set; } // Display/lookup key
            public int ItemId { get; set; }      // Actual Item ID from BOMItemMasters
            public decimal Quantity { get; set; }
            //public string UOMText { get; set; }  // Display UOM
           // public int? UOMId { get; set; }      // Actual UOM ID (if available from lookup)
        }

        public class AssetAllocationViewModel
        {            
            public int? MachineId { get; set; } // Corresponds to the selected Machine Master ID
            public int? SubunitId { get; set; } // Corresponds to the selected Subunit Master ID
            public int? TaskId { get; set; } // Corresponds to the selected Equipment Master ID (TaskID in HTML was typo)

            public int? FrequencyId { get; set; } // Corresponds to the selected Frequency ID            
            public int? TaskTypeId { get; set; }
            public List<AllocatedItemDetailDTO> AllocatedItems { get; set; } = new List<AllocatedItemDetailDTO>();
            
        }
        [HttpPost]
        public JsonResult SaveAllocation(AssetAllocationViewModel model) // Receives the ViewModel
        {
            
            try
            {
                int companyId = Convert.ToInt32(Session["Company_ID"]);
                string createdBy = Session["U_Name"]?.ToString() ?? "System";

                //Check For Duplicate Records
                var duplicate = (from ab in DB.Asset_AllocationMaster
                                where ab.MachineID == model.MachineId && ab.SubUnitID == model.SubunitId && ab.TaskId == model.TaskId && ab.IsDeleted == 0 && ab.IsActive == 1 && ab.FrequencyID==model.FrequencyId
                                select ab).ToList();
                if(duplicate.Count>0)
                {
                    return Json(new { success = false, message = "Asset Allocation Already There..!" });
                }
                //Get Number Of Days 
                var Days = (from ab in DB.Asset_FrequencyMaster
                            where ab.id == model.FrequencyId
                            select ab.Days).SingleOrDefault();
                // 1. Save Asset_AllocationMaster record
                Asset_AllocationMaster newAllocationMaster = new Asset_AllocationMaster // Assuming this is your master table name
                {
                    MachineID = model.MachineId ?? 0,
                    SubUnitID = model.SubunitId,
                    TaskId = model.TaskId,
                    FrequencyID = model.FrequencyId ?? 0,
                    TaskType=model.TaskTypeId,
                    IsDeleted = 0,
                    LastMaintenanceDoneOn=System.DateTime.Now,
                    Days= Days,
                    IsActive=1,
                    CompanyID = companyId,
                    CreatedBy = createdBy,
                    CreatedOn = DateTime.Now
                };

                DB.Asset_AllocationMaster.Add(newAllocationMaster);
                DB.SaveChanges(); // Save to get the ID for related items

                if(model.AllocatedItems.Count!=0)
                { 
                // 2. Save associated items to Asset_TaskItems table
                foreach (var item in model.AllocatedItems)
                {
                    Asset_TaskItems newTaskItem = new Asset_TaskItems // Assuming this is your detail table name
                    {
                        AllocationMasterId = newAllocationMaster.id, // Foreign key to the newly created master record
                        ItemId = item.ItemId,
                        Quantity = item.Quantity,
                       // UOMId = item.UOMId, // UOMId from client-side
                        IsDeleted = 0,
                        CreatedBy = createdBy,
                        CreatedOn = DateTime.Now
                    };
                    DB.Asset_TaskItems.Add(newTaskItem);
                    }
                    DB.SaveChanges(); // Save all task items in a single batch
                }

                return Json(new { success = true, message = "Asset Allocation and Items saved successfully!" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving asset allocation and items: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                return Json(new { success = false, message = "An error occurred while saving allocation. Please try again: " + ex.Message });
            }
        }
        public ActionResult GetAllocatedTasks(int MachineID)
        {
            // Retrieve companyId from session (assuming BaseController or direct access)
            // If not using BaseController, ensure 'companyid' is defined like:
            int companyid = Convert.ToInt32(Session["Company_ID"].ToString());

            try
            {
                var data = (from ab in DB.Asset_AllocationMaster
                            join mc in DB.Asset_MachineMaster on ab.MachineID equals mc.id // Inner Join (MachineID is required)

                            // FIX: Use LEFT JOIN for SubunitMaster as ab.SubUnitID can be null
                            join su in DB.Asset_SubunitMaster on ab.SubUnitID equals su.id into suGroup // Using into for Left Join
                            from su in suGroup.DefaultIfEmpty() // Performs the LEFT JOIN

                                // FIX: Use LEFT JOIN for EquipmentMaster (assuming TaskId maps to Equipment)
                                // Assuming Asset_TastMaster is typo for Asset_EquipmentMaster
                            join eq in DB.Asset_TastMaster on ab.TaskId equals eq.id into eqGroup // Using into for Left Join
                            from eq in eqGroup.DefaultIfEmpty() // Performs the LEFT JOIN
                            join ta in DB.Asset_TaskType on ab.TaskType equals ta.ID into tagroup
                            from ta in tagroup.DefaultIfEmpty()

                            join fr in DB.Asset_FrequencyMaster on ab.FrequencyID equals fr.id // Inner Join (FrequencyID is required)
                            where ab.MachineID == MachineID && ab.IsDeleted == 0 && ab.CompanyID == companyid // Add CompanyID filter
                            select new
                            {
                                Id = ab.id, // Include Allocation ID for client-side actions (Edit/Delete)
                                MachineName = mc.MachineName, // From INNER JOIN, so always non-null
                                SubUnitName = su.SubUnitName ?? "N/A", // FIX: Handle null from LEFT JOIN
                                TaskName = eq.TaskName ?? "N/A", // FIX: Handle null from LEFT JOIN (using EquipmentName for TaskName)
                                Frequency = fr.FrequencyName, 
                                TaskType=ta.TaskType// From INNER JOIN, so always non-null
                                                              // Add other fields you might need in the table or for client-side operations
                                                              // e.g., TaskId = ab.TaskId, // If you need the ID
                                                              // FrequencyId = ab.FrequencyID // If you need the ID
                            }).ToList();

                // FIX: Correct typo 'successs' to 'success'
                if (data.Any()) // Use Any() for cleaner check
                {
                    return Json(new { success = true, data = data, message = "Allocations found for selected machine." }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = true, data = new List<object>(), message = "No allocations found for selected machine." }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception e)
            {
                // FIX: Correct typo 'successs' to 'success'
                System.Diagnostics.Debug.WriteLine($"Error in GetAllocatedTasks: {e.Message}{Environment.NewLine}{e.StackTrace}");
                return Json(new { success = false, message = "Error retrieving allocations." }, JsonRequestBehavior.AllowGet);
            }
        }
        public JsonResult GetAllocatedItemsByAllocationId(int allocationId) // Parameter name must match JS 'data: { allocationId: ... }'
        {
            try
            {
                // Optional: Add CompanyID filter for security and data isolation
                // int companyId = Convert.ToInt32(Session["Company_ID"]); // Or from BaseController

                var items = (from taskItem in DB.Asset_TaskItems
                             join itemMaster in DB.BOMItemMasters on taskItem.ItemId equals itemMaster.Itemid into imGroup
                             from itemMaster in imGroup.DefaultIfEmpty() // Left join for item details
                             join UOM in DB.BOM_UOM on itemMaster.UOM equals UOM.id
                             where taskItem.AllocationMasterId == allocationId &&
                                   taskItem.IsDeleted == 0 // Only non-deleted task items
                                                           // && taskItem.CompanyID == companyId // Uncomment if Asset_TaskItems has CompanyID
                             select new
                             {
                                 ItemCode = itemMaster.ItemCode ?? "N/A", // From BOMItemMasters
                                 ItemName = itemMaster.ItemName ?? "N/A", // From BOMItemMasters
                                 Quantity = taskItem.Quantity, // From Asset_TaskItems
                                 UOMText = UOM.UOM ?? "N/A" // Assuming UOM is on BOMItemMasters
                                 // Add other fields you might need
                             }).ToList();

                if (items.Any())
                {
                    return Json(new { success = true, data = items, message = "Items retrieved successfully." }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = true, data = new List<object>(), message = "No items found for this allocation." }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching allocated items for allocation ID {allocationId}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                return Json(new { success = false, message = "Error retrieving items for this allocation." }, JsonRequestBehavior.AllowGet);
            }
        }

    }

}