using CMKL.Models;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Wordprocessing;
using RazorEngine;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;
using System.Web.Services.Description;
using System.Web.UI.WebControls;


namespace CMKL.Controllers
{
    public class CMKLProductController : Controller
    {
        // GET: CMKLProduct
        IECEntities DB = new IECEntities();

        public class CustomerRatingModel
        {
            
            public int id { get; set; } 
            public int OrderDelivery { get; set; }
            public int PanelPacking { get; set; }
            public int InteractionCall { get; set; }
            public int ProductQuality { get; set; }
            public int Documentaion { get; set; }
            public int PlantRating { get; set; }
            public int OverAll { get; set; }
            public int AfterSale { get; set; }
            public string Comments { get; set; }

            
        }
        [HttpPost]
        public JsonResult SaveCustomerRating(CustomerRatingModel model)
        {
            try
            {
                // --- 1. Server-Side Validation (Ensuring all required ratings are 1 or higher) ---
                // Assuming 1 is the minimum rating for required fields.
                if (model.OrderDelivery < 1 ||
                    model.PanelPacking < 1 ||
                    model.InteractionCall < 1 ||
                    model.ProductQuality < 1 ||
                    model.Documentaion < 1 ||
                    model.PlantRating < 1 ||
                    model.OverAll < 1 ||
                    model.AfterSale < 1)
                {
                    // This message will be sent back to the AJAX 'response.message'
                    return Json(new
                    {
                        success = false,
                        message = "Server validation failed: Please ensure all eight rating fields are selected."
                    });
                }
                // Select Line 

                var line = (from ab in DB.CMKL_Feedback
                            where ab.id == model.id
                            select ab).SingleOrDefault();
                line.AfterSale = model.AfterSale;
                line.OtherFeedback = model.Comments;
                line.DateCreated = DateTime.Now;
                line.Qualityofpanel = model.ProductQuality;
                line.PackingofPanel = model.PanelPacking;
                line.Documentation = model.Documentaion;
                line.InteractionCall = model.InteractionCall;
                line.OverallRating = model.OverAll;
                line.PlantRating = model.PlantRating;
                line.DeliveryTime = model.OrderDelivery;
                DB.SaveChanges();

                SendCustomerFeedbackReply(Convert.ToInt32(line.OrderID));
                // --- 3. Return Success ---
                return Json(new
                {
                    success = true,
                    message = "Thank you! Your feedback has been submitted successfully. - Team CMKL"
                });
            }
            catch (DbUpdateException dbEx) // Specific exception handling for DB issues
            {
                // Log the detailed DB error (dbEx.InnerException)
                return Json(new
                {
                    success = false,
                    message = "A database error occurred while saving your ratings."
                });
            }
            catch (Exception ex)
            {
                // Log the general error
                return Json(new
                {
                    success = false,
                    message = "An unexpected error occurred. Please try again."
                });
            }
        }
        private void SendCustomerFeedbackReply(int OrderID)
        {
            try
            {
                // 1. Fetch the necessary data 
                var data = DB.CMKL_Enquiry.Find(OrderID);

                if (data == null)
                {
                    // Handle the case where the dispatch detail is not found
                    return;
                }



                // 2. Create the data model for the template
                var model = new
                {
                    CustomerName = data.Project_Client,
                    PONumber=data.PONumber,
                    Email=data.Email,

                };

                var email = (from ab in DB.CMKL_Email_Setting
                             where ab.id == 1
                             select ab).SingleOrDefault();
                var emailaddress = (from ab in DB.CMKL_Enquiry
                                   where ab.ID==OrderID
                                   select ab.Email).SingleOrDefault();
                //var emailsender = (from se in DB.CMKL_Email
                                 //  where se.DDLName == "ERPSender"
                                 //  select se).FirstOrDefault();

                MailMessage mail = new MailMessage();
                SmtpClient SmtpServer = new SmtpClient(email.smtp);
                mail.From = new MailAddress("it@iecgensets.com", "CMKL Feedback");
                mail.To.Add(emailaddress);
                
                // Add CC recipients
                //var ccRecipients = (from db in DB.CMKL_Email // Replace with your CC logic
                //                    where db.DDLName == "ERPDispatchCC" // Example: Get CC emails
                //                    select db).ToList();
               // foreach (var ccRecipient in ccRecipients)
               // {
               //     mail.CC.Add(ccRecipient.Email);
               // }
                mail.Subject = "Feedback Confirmation - Work Order Number -" + model.PONumber + "";

                // 3. Get the Razor template
                var templatePath = Server.MapPath("~/Views/EmailManage/FeedbackConfirmationEmailTemplate.cshtml"); // Verify path
                var template = System.IO.File.ReadAllText(templatePath);

                // 4. Render the template using RazorEngine
                var body = Razor.Parse(template, model, null, null);

                // 5. Set the email body
                mail.Body = body;
                mail.IsBodyHtml = true;

                SmtpServer.Port = Convert.ToInt32(email.port);
                SmtpServer.Credentials = new System.Net.NetworkCredential("it@iecgensets.com", email.IT_password);
                SmtpServer.EnableSsl = Convert.ToBoolean(email.ssl);

                SmtpServer.Send(mail);
            }
            catch (Exception ex)
            {
                // ... (Your existing error handling for email sending) ...
            }
        }
        public ActionResult Index()
        {
            return View();
        }
        public ActionResult CustomerFeedback(string co)
        {
            //ViewBag.msg ="";
            var getorderid = (from aa in DB.CMKL_Feedback
                             where aa.UniqueKey == co
                             select aa).SingleOrDefault();
            if (getorderid==null)
            {
                ViewBag.msg = "Invalid Order Detail";
            }
            else
            {
                ViewBag.msg = "Order Details Reterived";
                var data = (from ab in DB.CMKL_Enquiry
                            where ab.ID == getorderid.OrderID
                            select ab).SingleOrDefault();
                ViewBag.Data = data;
                ViewBag.id = getorderid.id;
                if(getorderid.DateCreated.HasValue)
                {
                    ViewBag.exist = 1;
                    ViewBag.detail = getorderid;
                }
                else
                {
                    ViewBag.exist = 0;
                    
                }
            }

            
            return View();

        }
        public ActionResult Enquiry()
        {
            return View();
        }
        public ActionResult ProductInformation()
        {
            return View();
        }
        public ActionResult CreateQRLogin()
        {
            return View();
        }
        public class QRLogin
        {
            public string email { get; set; }
            public string password { get; set; }
            public string OrderID { get; set; }
        }

        [HttpPost]
        public ActionResult SaveQRLogin(QRLogin newLogin)
        {
            int Orderid = Convert.ToInt32(newLogin.OrderID);
            // Basic server-side validation to check for empty fields
            if (string.IsNullOrEmpty(newLogin.email) || string.IsNullOrEmpty(newLogin.password) || string.IsNullOrEmpty(newLogin.OrderID))
            {
                // Returning a JSON object for the AJAX success callback
                return Json(new { success = false, message = "All fields are required." });
            }
           
            // Custom validation: check if an entry with this email and order ID already exists
            if (DB.CMKL_QRLogin.Any(q => q.email == newLogin.email && q.OrderID == Orderid))
            {
                return Json(new { success = false, message = "An entry with this email already exists for this Order ID." });
            }

            // Save to the database
            try
            {
                CMKL_QRLogin QR = new CMKL_QRLogin
                {
                    email = newLogin.email,
                    password = newLogin.password,
                    OrderID = Orderid
                };

                DB.CMKL_QRLogin.Add(QR);
                DB.SaveChanges();
                return Json(new { success = true, message = "QR Login saved successfully!" });
            }
            catch (System.Exception ex)
            {
                // TODO: Log the exception for debugging purposes
                return Json(new { success = false, message = "An unexpected error occurred while saving the data." });
            }
        }
        public ActionResult GetQRLoginDetails()
        {
            var records = (from ab in DB.CMKL_QRLogin
                           select new
                           {
                               id = ab.id,
                               email = ab.email,
                               password = ab.password,
                               orderid = ab.OrderID
                           }).ToList();
            return Json(new { success = true, records }, JsonRequestBehavior.AllowGet);
            
        }

        public ActionResult ProductInformationData() 
      {
            //string filePath = Path.Combine(Server.MapPath("~/.."), "IEC_Final", folderPath, fileName);
            var id = Session["id"].ToString();
            int enquiryId = Convert.ToInt32(id);
            var GetData = (from ab in DB.CMKL_Enquiry
                           where ab.ID == enquiryId
                           select ab).SingleOrDefault();
           
            return Json(new { success = true, GetData }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult CMKLDocumentUpload()
        {
            return View();
        }
        public class FileData
        {
            public string content { get; set; }     // Base64 content of the file
            public string extension { get; set; }   // File extension (e.g., "pdf", "jpg")
        }
        public class CMKLDocumentUploadViewModel
        {
            // The OrderId is important for creating a subfolder, as per your previous code
            public int OrderId { get; set; }

            // This dictionary holds all your files by their ID (e.g., "PO", "GAdrg")
            public Dictionary<string, FileData> Files { get; set; }
        }

        public class CMKLDocumentUploadFormDataViewModel
        {
            /// <summary>
            /// Binds to the "OrderId" field appended in JavaScript's FormData.
            /// </summary>
            public int OrderId { get; set; }

            /// <summary>
            /// Binds to the file uploaded via the HTML input with id="PO".
            /// The property name must match the 'id' (or 'name') attribute of the file input.
            /// </summary>
            public HttpPostedFileBase PO { get; set; }

            /// <summary>
            /// Binds to the file uploaded via the HTML input with id="GAdrg".
            /// </summary>
            public HttpPostedFileBase GAdrg { get; set; }

            /// <summary>
            /// Binds to the file uploaded via the HTML input with id="SLdrg".
            /// </summary>
            public HttpPostedFileBase SLdrg { get; set; }

            /// <summary>
            /// Binds to the file uploaded via the HTML input with id="WIdrg".
            /// </summary>
            public HttpPostedFileBase WIdrg { get; set; }

            /// <summary>
            /// Binds to the file uploaded via the HTML input with id="MPlist".
            /// </summary>
            public HttpPostedFileBase MPlist { get; set; }

            /// <summary>
            /// Binds to the file uploaded via the HTML input with id="Productiondoc".
            /// </summary>
            public HttpPostedFileBase Productiondoc { get; set; }

            /// <summary>
            /// Binds to the file uploaded via the HTML input with id="CIReport".
            /// </summary>
            public HttpPostedFileBase CIReport { get; set; }

            /// <summary>
            /// Binds to the file uploaded via the HTML input with id="ABdrg".
            /// </summary>
            public HttpPostedFileBase ABdrg { get; set; }

            /// <summary>
            /// Binds to the file uploaded via the HTML input with id="QDoc".
            /// </summary>
            public HttpPostedFileBase QDoc { get; set; }

            /// <summary>
            /// Binds to the file uploaded via the HTML input with id="Bill".
            /// </summary>
            public HttpPostedFileBase Bill { get; set; }

            /// <summary>
            /// Binds to the file uploaded via the HTML input with id="Eway".
            /// </summary>
            public HttpPostedFileBase Eway { get; set; }

            /// <summary>
            /// Binds to the file uploaded via the HTML input with id="LR".
            /// </summary>
            public HttpPostedFileBase LR { get; set; }

            /// <summary>
            /// Binds to the file uploaded via the HTML input with id="truck".
            /// </summary>
            public HttpPostedFileBase Truck { get; set; } // Note: 'truck' in HTML maps to 'Truck' here (case-insensitive binding by default, but exact match is best practice).

            /// <summary>
            /// Binds to the file uploaded via the HTML input with id="Packing".
            /// </summary>
            public HttpPostedFileBase Packing { get; set; }

            // --- Important Note ---
            // If you had a file input that allowed multiple files and you appended them like:
            // formData.append("generalFiles[]", file1);
            // formData.append("generalFiles[]", file2);
            // ... then you would have a property like this:
            // public IEnumerable<HttpPostedFileBase> generalFiles { get; set; }
            // But for your current setup with distinct IDs, individual properties are correct.
        }
        private const string BaseUploadDirectory = @"\\192.168.0.200\CMKLDispatch\"; // truck -  Bill - LR - Packing - EwayEtc
        private const string POUpload = @"\\192.168.0.200\CMKLPO\"; // PO Copy
        private const string DrawingUpload = @"\\192.168.0.200\CMKLOAUpload\"; // GA- SLD - Wiring - BOM
        private const string QualityUpload = @"\\192.168.0.200\CMKLQualityUpload\"; // Quality Doc Upload
        private const string AsBuiltUpload = @"\\192.168.0.200\CMKLAsbuiltDRW\"; // As Built Drawing
        private const string CustomerRPTUpload = @"\\192.168.0.200\CMKLCustomerRPT\"; // Quality Customer Report
        private const string MechanicalUpload = @"\\192.168.0.200\CMKLMechanical\"; // Mechanical
        private const string ProductionUpload = @"\\192.168.0.200\CMKLProductionUpload\";
        


        [HttpPost]
        public JsonResult UploadDocumentsFormData(CMKLDocumentUploadFormDataViewModel model) // Action name and ViewModel
        {
            // Basic validation for Order ID
            if (model.OrderId <= 0)
            {
                return Json(new { success = false, message = "Invalid Order ID provided." });
            }

            try
            {
                // Get session data for logging in DB
                string uploadedBy = Session["U_Name"]?.ToString() ?? "System";
                // You might also need CompanyID and FinYear from session if CMKLDOC stores them
                // int companyId = Convert.ToInt32(Session["Company_ID"]);
                // string finYear = Session["Fin_Year"].ToString();

                string orderSpecificDirectory = "";//Path.Combine(BaseUploadDirectory);//, model.OrderId.ToString());

                // Create directory if it doesn't exist, handling potential permissions issues
                /*try
                {
                    if (!Directory.Exists(orderSpecificDirectory))
                    {
                        Directory.CreateDirectory(orderSpecificDirectory);
                    }
                }
                catch (UnauthorizedAccessException uexDir)
                {
                    System.Diagnostics.Debug.WriteLine($"UnauthorizedAccessException creating directory: {uexDir.Message}");
                    return Json(new { success = false, message = $"Access denied creating folder. Check permissions for '{BaseUploadDirectory}'. Error: {uexDir.Message}" });
                }
                catch (Exception exDir)
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating directory: {exDir.Message}");
                    return Json(new { success = false, message = $"Error creating upload folder: {exDir.Message}" });
                }
                */


                int uploadedFileCount = 0;
                List<string> uploadedFileNames = new List<string>();
                //List<CMKLDOC> docEntriesToSave = new List<CMKLDOC>(); // To save to DB in batch

                // Use reflection to iterate through all HttpPostedFileBase properties in the ViewModel
                // This makes the code scalable; you don't need to list each file explicitly.
                foreach (var prop in typeof(CMKLDocumentUploadFormDataViewModel).GetProperties())
                {
                    // Check if the property is of type HttpPostedFileBase
                    if (prop.PropertyType == typeof(HttpPostedFileBase))
                    {
                        HttpPostedFileBase file = (HttpPostedFileBase)prop.GetValue(model);
                        string documentType = prop.Name; // Use the property name (e.g., "PO", "GAdrg") as the document type

                        if (file != null && file.ContentLength > 0) // Check if a file was actually uploaded for this specific input
                        {
                            string fileExtension = Path.GetExtension(file.FileName)?.TrimStart('.'); // Get extension, remove leading dot
                            if (string.IsNullOrEmpty(fileExtension))
                            {
                                fileExtension = "unknown"; // Assign a default if no extension
                            }

                            if (documentType == "PO")
                            {
                                orderSpecificDirectory = POUpload;
                            }
                            else if(documentType == "GAdrg" || documentType == "SLdrg" || documentType == "WIdrg" )
                            {
                                orderSpecificDirectory = DrawingUpload;
                            }
                            else if(documentType== "MPlist")
                            {
                                orderSpecificDirectory = MechanicalUpload;
                            }
                            else if (documentType == "Productiondoc")
                            {
                                orderSpecificDirectory = ProductionUpload;
                            }
                            else if (documentType == "CIReport")
                            {
                                orderSpecificDirectory = CustomerRPTUpload;
                            }
                            else if (documentType == "ABdrg")
                            {
                                orderSpecificDirectory = AsBuiltUpload;
                            }
                            else if (documentType == "QDoc")
                            {
                                orderSpecificDirectory = QualityUpload;
                            }
                            else if (documentType == "Bill" || documentType== "Eway" || documentType== "LR" || documentType== "Truck" || documentType== "Packing")
                            {
                                orderSpecificDirectory = BaseUploadDirectory;
                            }
                            //Get Job Details
                            var job = (from ab in DB.CMKL_Enquiry
                                       where ab.ID == model.OrderId
                                       select ab).SingleOrDefault();

                            //string FileName=
                            string uniqueFileName = $"{Guid.NewGuid().ToString()}_{documentType}.{fileExtension}";
                            string fullFilePath = Path.Combine(orderSpecificDirectory, uniqueFileName);
                            //Update Path in Database
                            if (documentType == "PO")
                            {
                                job.POCopy = "~/CMKLPO/" + uniqueFileName;
                            }
                            else if (documentType == "GAdrg") //|| documentType == "SLdrg" || documentType == "WIdrg")
                            {
                                job.GADrawing = "~/CMKLOAUpload/" + uniqueFileName;
                            }
                            else if (documentType == "SLdrg") //|| documentType == "SLdrg" || documentType == "WIdrg")
                            {
                                job.SingleDrawing = "~/CMKLOAUpload/" + uniqueFileName;
                            }
                            else if (documentType == "WIdrg") //|| documentType == "SLdrg" || documentType == "WIdrg")
                            {
                                job.WiringDrawing = "~/CMKLOAUpload/" + uniqueFileName;
                            }
                            else if (documentType == "MPlist")
                            {
                                job.MechanicalPart = "~/CMKLMechanical/" + uniqueFileName; 
                            }
                            else if (documentType == "Productiondoc")
                            {
                                job.ProductionDoc = "~/CMKLProductionUpload/" + uniqueFileName; 
                            }
                            else if (documentType == "CIReport")
                            {
                                job.CustomerRPT = "~/CMKLCustomerRPT/" + uniqueFileName;
                            }
                            else if (documentType == "ABdrg")
                            {
                                job.AsbuiltDRW = "~/CMKLAsbuiltDRW/" + uniqueFileName;
                            }
                            else if (documentType == "QDoc")
                            {
                                job.QualityDoc = "~/CMKLQualityUpload/" + uniqueFileName;
                            }
                            else if (documentType == "Bill")// || documentType == "Eway" || documentType == "LR" || documentType == "truck" || documentType == "Packing")
                            {
                                job.Billcopy = "~/CMKLDispatch/" + uniqueFileName;
                            }
                            else if (documentType == "Eway")// || documentType == "Eway" || documentType == "LR" || documentType == "truck" || documentType == "Packing")
                            {
                                job.EwayCopy = "~/CMKLDispatch/" + uniqueFileName;
                            }
                            else if (documentType == "LR")// || documentType == "Eway" || documentType == "LR" || documentType == "truck" || documentType == "Packing")
                            {
                                job.LRCopy = "~/CMKLDispatch/" + uniqueFileName;
                            }
                            else if (documentType == "Truck")// || documentType == "Eway" || documentType == "LR" || documentType == "truck" || documentType == "Packing")
                            {
                                job.TruckCopy = "~/CMKLDispatch/" + uniqueFileName;
                            }
                            else if (documentType == "Packing")// || documentType == "Eway" || documentType == "LR" || documentType == "truck" || documentType == "Packing")
                            {
                                job.PackingList = "~/CMKLDispatch/" + uniqueFileName;
                            }
                            try
                            {
                                file.SaveAs(fullFilePath); // Save the file directly to the disk

                                DB.SaveChanges();
                                uploadedFileCount++;
                                uploadedFileNames.Add(uniqueFileName);

                                
                            }
                            catch (UnauthorizedAccessException uexFile)
                            {
                                System.Diagnostics.Debug.WriteLine($"UnauthorizedAccessException saving file '{documentType}': {uexFile.Message}");
                                return Json(new { success = false, message = $"Access denied when saving file '{documentType}'. Check network share permissions. Error: {uexFile.Message}" });
                            }
                            catch (Exception exFile)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error saving file '{documentType}': {exFile.Message}");
                                return Json(new { success = false, message = $"Error saving file '{documentType}': {exFile.Message}" });
                            }
                        }
                    }
                }

                // Save all document records to the database in a single batch
                //if (docEntriesToSave.Any())
                {
                    //DB.CMKLDOCs.AddRange(docEntriesToSave); // Assumes you have DbSet<CMKLDOC> CMKLDOCs in your DbContext
                //    DB.SaveChanges();
                }

                if (uploadedFileCount > 0)
                {
                    return Json(new { success = true, message = $"{uploadedFileCount} files uploaded successfully for Order ID: {model.OrderId}." });
                }
                else
                {
                    return Json(new { success = false, message = "No files were actually selected or processed for upload." });
                }
            }
            catch (Exception ex) // Catch any other unexpected errors at the top level
            {
                System.Diagnostics.Debug.WriteLine($"General error in UploadDocumentsFormData: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                return Json(new { success = false, message = $"An unexpected error occurred: {ex.Message}" });
            }
        }
    
        [HttpPost]
        public JsonResult UpdateItemDocuments(CMKLDocumentUploadViewModel model)
        {
            // Basic validation for Order ID
            if (model.OrderId <= 0)
            {
                return Json(new { success = false, message = "Invalid Order ID provided." });
            }

            // Ensure the files collection is not null
            if (model.Files == null || !model.Files.Any())
            {
                return Json(new { success = false, message = "No files were received for upload." });
            }

            try
            {
                // Get session data for logging
                //string uploadedBy = Session["U_Name"]?.ToString() ?? "System"; // Handle null if session not set

                // Create an order-specific subfolder if it doesn't exist
                // This helps organize files and prevent too many files in one directory.
                string orderSpecificDirectory = Path.Combine(BaseUploadDirectory);// model.OrderId.ToString());

                // Use a try-catch for directory creation as well
                try
                {
                    if (!Directory.Exists(orderSpecificDirectory))
                    {
                        Directory.CreateDirectory(orderSpecificDirectory);
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    // This often means IIS App Pool lacks permissions on the network share
                    System.Diagnostics.Debug.WriteLine($"UnauthorizedAccessException during directory creation: {ex.Message}");
                    return Json(new { success = false, message = $"Access denied to network share. Please check permissions. Error: {ex.Message}" });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating directory {orderSpecificDirectory}: {ex.Message}");
                    return Json(new { success = false, message= "Error creating upload folder"});
                }


                foreach (var fileEntry in model.Files)
                {
                    string documentType = fileEntry.Key; // e.g., "PO", "GAdrg"
                    FileData fileData = fileEntry.Value; // Contains content (Base64) and extension

                    // Only process if file content exists
                    if (fileData?.content != null && !string.IsNullOrWhiteSpace(fileData.content))
                    {
                        // Extract base64 part (remove "data:image/png;base64," prefix)
                        string base64Content = fileData.content.Split(',')[1];
                        byte[] fileBytes = Convert.FromBase64String(base64Content);

                        // Generate a unique file name to avoid collisions
                        string uniqueFileName = $"{Guid.NewGuid().ToString()}_{documentType}.{fileData.extension}";
                        string fullFilePath = Path.Combine(orderSpecificDirectory, uniqueFileName);

                        try
                        {
                            // Save the file to the specified network folder
                            System.IO.File.WriteAllBytes(fullFilePath, fileBytes);

                            // Save the file path and metadata to SQL database
                           

                            //DB.CMKLDOCs.Add(docEntry);
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"UnauthorizedAccessException saving file {uniqueFileName}: {ex.Message}");
                            // Consider logging this specific file failure and continuing, or aborting the whole transaction.
                            // For now, we'll return an error and stop.
                            return Json(new { success = false, message = $"Access denied when saving file '{documentType}'. Check network share permissions. Error: {ex.Message}" });
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error saving file {uniqueFileName}: {ex.Message}");
                            // Handle other file I/O errors
                            return Json(new { success = false, message = $"Error saving file '{documentType}': {ex.Message}" });
                        }
                    }
                }

                // Save all database changes (document paths) in one transaction
                DB.SaveChanges();

                return Json(new { success = true, message = "Documents uploaded and paths saved successfully!" });
            }
            catch (Exception ex)
            {
                // Catch any other unexpected errors
                System.Diagnostics.Debug.WriteLine($"General error in UpdateItemDocuments: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                return Json(new { success = false, message = $"An unexpected error occurred: {ex.Message}" });
            }
        }
    }
}