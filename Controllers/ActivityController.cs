using CMKL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web; // Needed for HttpContextBase
using System.Web.Mvc;
using System.Configuration;


namespace CMKL.Controllers
{
    public class ActivityController : Controller
    {
        // No longer declare DB context at the class level
        // IECEntities DB = new IECEntities(); 

        // GET: Activity
        public ActionResult Index()
        {
            return View();
        }

        // --- FIX: This is the correct, reusable, static helper method ---
        // It now takes HttpContextBase and the DbContext as parameters.
        public static void LogUserActivity(string action, HttpContextBase httpContext, IECEntities dbContext)
        {
            try
            {
                var request = httpContext.Request;
                var session = httpContext.Session;

                string userId = session?["U_Name"]?.ToString();
                int? companyId = session?["Company_ID"] != null ? (int?)Convert.ToInt32(session["Company_ID"]) : null;

                string clientIP = request?.UserHostAddress;
                string userAgent = request?.UserAgent;
                string clientHostName = request?.UserHostName;

                string deviceType = "Unknown";
                if (!string.IsNullOrEmpty(userAgent))
                {
                    if (userAgent.Contains("Mobi") || userAgent.Contains("Android") || userAgent.Contains("iPhone"))
                    {
                        deviceType = "Mobile";
                    }
                    else if (userAgent.Contains("iPad") || userAgent.Contains("Tablet"))
                    {
                        deviceType = "Tablet";
                    }
                    else
                    {
                        deviceType = "Desktop";
                    }
                }

               // AccessLog logEntry = new AccessLog
                {
               //     CompanyID = companyId,
              //      UserId = userId,
               //     ActionPerformed = action,
               //     ClientIPAddress = clientIP,
               //     ClientUserAgent = userAgent,
               //     DeviceType = deviceType,
               //     ClientHostName = clientHostName,
               //     LogTime = DateTime.Now
                };

               // dbContext.AccessLogs.Add(logEntry);
                dbContext.SaveChanges(); // Save changes to the database
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error logging user activity: {ex.Message}");
            }
        }
    }
}