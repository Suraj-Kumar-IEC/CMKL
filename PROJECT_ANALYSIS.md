# CMKL ERP — Complete Project Analysis

---

## 1. Project Overview

**Project Name:** CMKL ERP  
**Company:** IEC Gensets / CMKL  
**Domain:** Diesel Generator (DG) Set Manufacturing  
**Purpose:** Full-lifecycle manufacturing ERP — from sales enquiry through production, purchase, quality, dispatch, and after-sales service.

The system covers every business function: sales order management, bill of materials, production scheduling, purchase & inventory, quality control, plant asset maintenance, employee TA claims, tender tracking, and management reporting.

---

## 2. Technology Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET MVC 5, .NET 4.7.2 |
| ORM | Entity Framework 6.5.1 — Database-First (EDMX) |
| Database | SQL Server Express — catalog `IEC` |
| View Engine | Razor (.cshtml) |
| CSS Framework | Bootstrap 5.3.3 |
| Icons | Font Awesome 4.7.0 + Linearicons |
| JS Libraries | jQuery 3.7.0, jQuery UI 1.13.2, Select2, Chart.js |
| Notifications | Toastr (in-page alerts) + audio (Save.wav / Error.mp3) |
| PDF Generation | Rotativa (wkhtmltopdf wrapper) |
| Excel Export | ClosedXML + EPPlus |
| Email | System.Net.Mail + MimeKit; RazorEngine for HTML templates |
| Background Jobs | Hangfire 1.8.17 (SQL Server backed) |
| QR Codes | MessagingToolkit.QRCode |
| Bundling/Minification | ASP.NET Web Optimization |
| Authentication | Custom session-based (no ASP.NET Identity) |
| DI / IoC | None — DbContext instantiated directly in each controller |


---

## 3. Architecture

- **Pattern:** Standard MVC 5 — Controllers → EF DbContext → SQL Server; Razor Views
- **No repository or service layer** — all business logic lives directly in controller action methods
- **AJAX-heavy UI** — most CRUD operations use `JsonResult` actions; pages update without full reload
- **Multi-company design** — every key table has `CompanyID`; all queries filter by `Session["Company_ID"]`
- **Multi-financial-year** — key tables also carry `Fin_Year`; voucher numbering and reports are FY-scoped
- **Role-based access control** — each protected action queries `User_Access` table to check `MenuAccess` permission; no MVC `[Authorize]` attribute used
- **FIFO inventory** — items flagged `FifoLot=true` use `Stock_lotDetail` for lot-level traceability; allocation ordered by ID

### Routing (RouteConfig.cs)

```
{controller}/{action}/{id}
Default: Login/Index
```

### Global Filter (FilterConfig.cs)

Only `HandleErrorAttribute` is registered globally — no custom auth filter.

### Session Keys Set on Login

| Key | Value |
|---|---|
| `User_ID` | Logged-in user ID |
| `UserName` | Display name |
| `User_Role` | Role string (SUPERADMIN, ADMIN, etc.) |
| `Company_ID` | Active company |
| `Company_Name` | Display name |
| `FinYear` | Selected financial year |
| `FinYear_End` | FY end date |
| `Designation` | User designation |
| `Department` | User department |
| `SignatureUrl` | Digital signature image path |
| `ProfilePhoto` | Profile photo path |
| `UserBranchID` | Branch identifier |


---

## 4. Database Structure

**Connection:** `data source=DESKTOP\SQLEXPRESS; initial catalog=IEC; user id=sa`

### Core Master Tables

| Table | Purpose |
|---|---|
| `tbl_User_Master` | Users — login, role, company, designation, digital signature URL |
| `User_Access` | Permission matrix: UserID → MenuAccess (page name) → Status (bool) |
| `User_Menu` | Menu catalog with MainMenu grouping and display names |
| `Companies` | Multi-company master |
| `FinYears` | Financial year list |
| `Bill_Series` | Voucher number sequences per Type/Company/FinYear |
| `BOMItemMasters` | Central item master — code, name, UOM, price, min/max stock, FifoLot flag |
| `StockTables` | Current stock balance per item per company |
| `Stock_lotDetail` | FIFO lot records — serial numbers, qty available, reservation flag |
| `SupplierMasters` | Vendor master |
| `DepartmentMasters` | Departments; `isBOMStage` flag marks production stages |
| `TaxMasters` | GST/tax rate definitions |
| `Ratings` / `Ratings_Production` | DG set KVA rating catalog |
| `EngineModels` / `Phases` / `BatteryMasters` / `ControlPanelTypes` | Component specification masters |
| `Make_Master` / `StoreLocationMaster` / `ItemGroups` | Supporting item masters |

### Purchase / Inventory Tables

| Table | Purpose |
|---|---|
| `BOMIndentHeads` / `BomIndentLines` | Purchase indent / requisition |
| `PurchaseOrderHeads` / `PurchaseOrderItems` | Purchase orders |
| `IEPLStockIN_Head` / `IEPLStockIN_Detail` | Material receipt notes (MRN) — 4-stage approval |
| `IEPLStockIssueHead` / `IEPLStockIssueDetail` | Stock issue (Challan) |
| `IEPLStockReturnHead` / `IEPLStockReturnDetail` | Stock return |
| `LineRejectionHeads` / `LineRejectionDetails` | Production line rejection vouchers |
| `OpeningStocks` | FY opening stock entries |
| `IEPL_MiscSale_Head` / `IEPL_MiscSale_Detail` | Miscellaneous sales billing |

### Production / BOM Tables

| Table | Purpose |
|---|---|
| `BOMVouchers` | Production job header — FG product, approval flags, PDI/dispatch status |
| `BOMVoucherlines` | BOM job lines — raw items, lot IDs, approved quantities |
| `BOMCategories` / `BOMSubcategories` | Item classification for BOM |
| `BOMRequisitionHead` / `BOMRequisitionLine` | Store material requisition |
| `Bom_ProductionUpdate` | Engine/alternator assembly details per BOM |
| `BOM_TestingUpdate` | Final testing — battery, control panel, KRM no, rating |
| `BOMEwapDetails` | End-of-line EWAP testing — PDI info, engine/alternator serial |
| `BOMFinalProductCombinations` | FG product composition — canopy, fuel tank, panel, alternator config |
| `DispatchDetails` | FG dispatch — customer, invoice, transport, LR number, bill date |
| `DispatchReturns` | FG return records |
| `DispatchReturnBOMHead` / `DispatchReturnBOMLine` | BOM for returned FG items |

### Sales / CRM Tables

| Table | Purpose |
|---|---|
| `CMKL_Enquiry` | Sales order/enquiry with full lifecycle OAStage (0–8) |
| `CMKL_Enquiry_Item` | Line items per enquiry |
| `CMKL_Feedback` | 8-parameter customer satisfaction ratings |
| `CMKL_QRLogin` | QR-code-based customer portal credentials |
| `OrderDetails` | OE orders — pricing, incentives, engine/rating details |
| `PaymentsDealers` / `PaymentCompanies` | Payment tracking |

### Asset / Maintenance Tables

| Table | Purpose |
|---|---|
| `Asset_MachineMaster` / `Asset_SubunitMaster` | Plant machine registry |
| `Asset_TastMaster` / `Asset_TaskType` / `Asset_FrequencyMaster` | Maintenance task definitions |
| `Asset_AllocationMaster` | Scheduled maintenance assignments |
| `Asset_MaintenanceLogbook` / `Asset_MaintenanceLogbookItems` | Maintenance execution records |
| `Asset_BreakdownLog` | Machine breakdown — 3-stage resolution |

### Other Tables

| Table | Purpose |
|---|---|
| `CMKL_Email` / `CMKL_Email_Setting` | Email distribution groups and SMTP config |
| `AutoReminderEmails` | Hangfire job activation flags |
| `TA_head` / `TA_items` / `TA_Master` | Travelling allowance claims |
| `Tenders` | Government/GeM tender tracking |
| `ConnectionDatas` | Hangfire SQL connection string storage |
| `Authorizeddealerviews` / `PaymentViewCompanies` | DB views for dealer payment summaries |


---

## 5. Modules and Controllers

### 5.1 LoginController — Authentication

- `GET Index` — login page
- `POST Login(TD, finyear)` — validates credentials from `tbl_User_Master`, checks company access, sets 12 session keys
- `GET GetCompanies()` — returns active companies + financial years for login dropdown
- `GET logout()` — clears all session keys
- `GET/POST ChangePassword / UpdatePassword` — password change with old password verification
- `GET CheckSessionTimeout()` — AJAX poll for session alive check
- `GET GuestLogin / QRLogin` — QR-based customer portal login

**Security note:** Passwords are stored and compared in plaintext. No hashing is implemented.

---

### 5.2 DashboardController — Analytics

- `GET Dashboard()` — main dashboard view (Chart.js charts)
- `GET RatingTrendGraphs()` — KVA rating analytics
- `GET GetDistinctRatings()` — KVA ratings present in dispatch data
- `GET GetMonthlyDispatchDataByRating(rating)` — monthly trend for a KVA vs total output
- `GET GetDistinctEngineModels()` — engine models used in dispatches
- `GET GetDistinctEngineConfigs()` — rating + engine combos for chart filters
- `GET GetMonthlyDispatchDataByConfig(configValue)` — current FY vs previous FY comparison
- `GET GetMonthlyDispatchDataByEngine(engineModel)` — engine model trend vs total volume

---

### 5.3 BOMController — Bill of Materials & Production Job Management (largest: ~4,877 lines)

**BOM Job Lifecycle:**

| Action | Purpose |
|---|---|
| `BOM()` | Create new production job |
| `BOMAlteration()` | Alter an approved BOM |
| `AlterBOMDeleteLine(lineid)` | Soft-delete BOM line; reverses stock and FIFO lot allocation |
| `AddBomAlterationItems(...)` | Add item to BOM with FIFO lot allocation |
| `ReturnBomCreation(itemId)` | BOM for a returned FG item |
| `SaveReturnBOM(headData, lineData)` | Save return BOM; update dispatch return status |
| `GetBOMAlterationItems(bomidnumber)` | Fetch all lines for alteration |
| `UpdateBOMQtyPercentage()` | Admin utility: bulk quantity reduction |
| `ApplyPercentageReduction(model)` | Apply percentage reduction to BOM lines |
| `GetBOMStages()` | Department-based production stage dropdown |
| `GetFinalItems()` / `GetFinalItemsBOM()` | FG item dropdowns |

Also handles: BOM planning, store/management approval, cancellation, requisition management, item linking, category/subcategory masters.

---

### 5.4 PurchaseController — Procurement (~3,304 lines)

| Action | Purpose |
|---|---|
| `Indent()` | Purchase indent view |
| `SaveIndentManual(headData, lineData)` | Save manual indent; auto-assigns voucher number |
| `IndentVoucherNumber()` | Next indent voucher number from `Bill_Series` |
| `GetItemDetails(itemCode)` | Item info + last purchase price + 3-month average consumption |
| `GetItemPendingIndents(itemId)` | Open approved indents for an item |
| `SavePartialReceipt(items)` | Update received qty; auto-closes fully received indents |
| `GetIndentTrackingAnalytics()` | Indent status summary (Pending / Partial / Completed) |
| `PurchaseVoucher()` | MRN creation view |
| `MRNStatus()` | MRN status tracking view |
| `ApprovalPlantHead()` / `ApprovalPlantAccounts()` | Multi-stage MRN approval views |
| `DeletePurchaseOrder(poId)` | Soft-delete a PO |
| `GetSupplierwisePO(supplierid)` | PO list filtered by supplier |

Also handles: PO print, MRN print, indent approval, BPR (Basic Purchase Report), purchase reports.

---

### 5.5 InventoryController — Stock & Item Management (~2,529 lines)

| Action | Purpose |
|---|---|
| `Itemmaster()` / `ItemmasterFG()` | Item master views (raw / FG) |
| `AddLotManually()` / `SaveManualLot(model)` | Manual FIFO lot entry |
| `LotMRNUpdate()` | Lot serial number correction utility |
| `UpdateLotSerial(id, serial, shouldSync)` | Update lot serial + sync to MRN |
| `GetItemTrendData(itemCode, interval)` | Daily/weekly/monthly closing balance chart data |
| `SaveFGitem(data)` | Create FG item master + stock table + product combination |
| `SaveLR(data)` | Create line rejection voucher; validates against quality-approved qty |
| `ApproveLR(lrId)` | Approve LR; deduct quantity from stock |
| `StockReturn()` / `LineRejection()` | Stock return and line rejection views |

Also handles: Goods receipt, quality-in, opening stock, stock issue, packing slip, BPR.

---

### 5.6 ProductionController — Assembly, Testing & Dispatch

| Action | Purpose |
|---|---|
| `PendingProductionView()` | Pending production jobs list |
| `PendingEwap()` | Pending EWAP testing queue |
| `Dispatch()` / `DispatchReturn()` | FG dispatch and return views |
| `PendingPDI()` / `CompletedPDI()` | PDI (Pre-Delivery Inspection) queue |
| `UpdatePDIForm(...)` | Full PDI save — updates 4 tables simultaneously |
| `GetBOMPrintData(BOMID)` | Full test certificate data for printing |
| `BillingPDIDetails(ewapId)` | PDI completion check before dispatch billing |
| `GetSerialNumbers(voucherId)` | Engine/alternator serials from FIFO lot |
| `FGReturnQualityAction()` / `FGReturnBOMCreation()` | FG return quality and BOM workflow |
| `ReturnBomApproval()` | Store approval for return BOM |
| `ReverseEwap()` / `ReverseApprovedBOM()` | Admin reversal operations |
| `CancelPackingSlip()` | Cancel packing slip |


---

### 5.7 ReportsController — Business Reports (~1,712 lines)

Key reports provided:

| Report | Description |
|---|---|
| `SaleReport` | Sales summary |
| `MaterialRecieptReport` | MRN register |
| `LotStockReport` | FIFO lot-level stock traceability |
| `GetDashboardRatingMatrix` | Quarterly dispatch count grouped by KVA ranges |
| `GetTopRejectionsFinYear` | Top rejection reasons in current FY |
| `GetInwardRejectionsFinYear` | Inward quality rejection summary |
| `PurchaseReport` | Purchase register |
| `DispatchReport` | Dispatch register |
| `DispatchReturnReport` | FG return report |
| `QualityRejectionReturnReport` | Quality rejection returns |
| `ItemConsumptionReport` | Item-wise consumption |
| `ItemMasterReport` | Item master listing |
| `BOMItemList` | BOM item catalogue |
| `CancelledBOMReport` | Cancelled production jobs |
| `IndentReportItemwise` | Full indent lifecycle — created → approved → PO → received |

---

### 5.8 StockReportController — Dashboard Analytics (~1,501 lines)

| Action | Description |
|---|---|
| `GetFinancialYearDispatchData()` | Monthly gross/net sales for current FY |
| `GetCurrentMonthDispatchData()` | Daily dispatch chart for current month |
| `GetMonthlySaleRatingData()` | Monthly sales split by KVA rating |
| `GetLowStockItems()` | Items below minimum stock level |
| `GetOverStockItems()` | Items above maximum stock level |
| `GetStockMovementChartData()` | Opening / In / Out / Closing per item |
| `DashboardDataForEwap()` | Pending EWAP count by product |
| `GetPurchaseChartData()` | Purchase trend chart data |
| `GetQualityChartData()` | Quality rejection trend chart data |
| `PendingQualityReport` | Items pending quality inspection |
| `PendingStockIssueReport` | Pending stock issue requests |
| `StockReturnReport` | Stock return register |

---

### 5.9 AssetController — Plant Maintenance

**Breakdown 3-stage flow:** DOWN → MAINT_DONE → WORKING

| Action | Description |
|---|---|
| `BreakdownManagement()` | Live breakdown dashboard |
| `ReportBreakdown(machineId, subunitId, problem)` | Log new breakdown (prevents duplicate open logs) |
| `CompleteMaintenance(logId, remarks)` | Stage 1: technician marks done |
| `FinalResolve(logId, adminRemarks)` | Stage 2: ASSETADMIN/SUPERADMIN final authorization |
| `GetBreakdownDashboardKPIs()` | Machines down/working + monthly downtime KPIs |
| `GetLiveBreakdowns()` | Active breakdowns with elapsed time |
| `GetBreakdownHistoryData(from, to)` | Historical breakdown report with response times |
| Masters | `MachineMaster`, `SubUnitMaster`, `TaskMaster`, `AssetAllocationMaster` |
| Approval workflow | `PendingApproval()`, `PendingSAdminApproval()` |
| `MaintenanceReport()` | Date-range filtered maintenance history |

---

### 5.10 TaskSchedulerController — Hangfire Background Jobs

OWIN startup registers recurring jobs via Hangfire:

| Job | Schedule | Description |
|---|---|---|
| `SendCombinedReminderEmail()` | Every 3 hrs, Mon–Sat, 10 AM–8 PM IST | BOM store pending approval reminders |
| `SendDailySaleReportDataForEmail()` | Daily 11 PM IST | Daily sale summary email |
| `SendDailyMaterialRecieptReport()` (Company 1) | Daily 9:30 AM IST | MRN report email |
| `SendDailyMaterialRecieptReport1()` (Company 2) | Daily 9:40 AM IST | MRN report email |
| `AssetMaintenanceCheck()` | Daily 9 AM IST | Creates logbook entries for due tasks; sends task-type emails |

All email bodies are HTML templates rendered via RazorEngine from `~/Views/EmailManage/*.cshtml`.

---

### 5.11 EnquiryStatusController — Sales Order Lifecycle

Manages the OAStage workflow for `CMKL_Enquiry`:

| OAStage | Meaning |
|---|---|
| 0 | Price Revision |
| 1 | Pending Order Acceptance |
| 2 | Drawing Upload |
| 3 | Drawing Approval |
| 4 | Pending Production |
| 5 | In Production |
| 6 | Quality Check |
| 7 | Ready to Dispatch |
| 8 | Dispatched |

Key actions:
- `ReverseEnquiry / Reverse(ID, Done, ddlstage)` — rolls back OAStage with email notification
- `OrderDetail(CE, split)` — splits enquiry into two records (copies all 60+ fields)
- `MoveItems(...)` — moves line items between split enquiries
- `UpdateCustomerDetail(CE)` — updates customer contact information

---

### 5.12 OEController — Order Entry / Dealer Payments

- `Authoriseddealerview()` / `DealerView()` — dealer-level order views
- `GetItem(id)` — order detail with payment calculation
- `updatepayment(...)` / `updatepaymentdealer(...)` — record payment receipts
- `LogisticView()` / `LogisticApproval()` — logistics coordination
- `CPCBPermission()` / `EnableCPCBII(Id)` / `DisableCPCBII(Id)` — toggle CPCB-II user permissions

---

### 5.13 CMKLProductController — Customer Portal (QR-based)

- `CustomerFeedback(co)` — QR-linked 8-parameter satisfaction survey
- `SaveCustomerRating(model)` — saves rating, sends confirmation email
- `ProductInformation()` — customer self-service portal
- `CreateQRLogin()` / `SaveQRLogin()` — QR login credential management
- `CMKLDocumentUpload()` / `UploadDocumentsFormData(model)` — routes 14 document types to UNC shares (`\\192.168.0.200\CMKL*`) with GUID-prefixed filenames
- `Enquiry()` — enquiry view for customer

---

### 5.14 MasterController — Reference Data Management

- `SupplierMaster()` / `SaveSupplier()` / `GetSupplierById()` — vendor CRUD
- `DepartmentMaster()` / `SaveDepartment()` — department management
- `SaveMake()` — make/brand master
- `CopyDatatoTestingtable()` — admin data migration utility
- `SQLbackup()` / `CreateBackup()` — triggers `BACKUP DATABASE IEC TO DISK` via parameterized SQL

---

### 5.15 UserAccessController — Permission Management

- `UserAccessView()` — permission matrix UI
- `GetUserPermissions(userId, mainMenu)` — all menu items with current access flags
- `TogglePermission(userId, menuName, isChecked)` — toggle individual page access on/off

---

### 5.16 EmailManageController — Email Group Management

- `EmailView()` — email group list UI
- `EmailData(typeEmail)` — fetch emails by type
- `AddEmail(Email, typeEmail)` — add email to group
- `DeleteEmail(id, EType)` — soft-delete (sets `Active=2`)

---

### 5.17 Other Controllers

| Controller | Status | Description |
|---|---|---|
| `TAController` | Active | Travelling allowance head + multi-line item claim save |
| `TenderController` | Active | Government/GeM tender tracking with due date ordering |
| `VendorsController` | Active | Vendor CRUD with partial view returned as JSON for AJAX |
| `ServiceController` | Stub | `RegisterComplaint()` view only — no DB logic |
| `EmployeeController` | Stub | `AddEmployee()` view only |
| `PhotographyController` | Stub | `Index()` placeholder |
| `ActivityController` | Partial | `LogUserActivity()` static helper — audit logging mostly commented out |
| `HomeController` | Active | `Index`, `ERPCommandCenter`, `MenuPage`, `Home`, `About`, `Contact` |


---

## 6. Core Data Flows

### 6.1 Manufacturing Lifecycle

```
1. Sales Enquiry Created (CMKL_Enquiry — OAStage 0)
      ↓
2. Order Accepted (OAStage 1–3: Drawing upload → approval)
      ↓
3. BOM Job Created (BOMVouchers + BOMVoucherlines)
      ← FIFO lot allocation from StockTables / Stock_lotDetail
      ↓
4. Store Approval → Management Approval (BOMVoucher approval flags)
      ↓
5. Production Update (Bom_ProductionUpdate: engine + alternator details)
      ↓
6. EWAP Testing (BOMEwapDetails + BOM_TestingUpdate: PDI data)
      ↓
7. PDI Complete (BOMVoucher.PDIStatus = 1) — prerequisite for dispatch
      ↓
8. Dispatch (DispatchDetails: invoice, LR, customer, transport)
      ↓
9. Customer Feedback via QR link (CMKL_Feedback — 8 parameters)
```

### 6.2 Purchase Cycle

```
1. Indent Created (BOMIndentHead / BomIndentLines)
      ↓
2. Indent Approved (Plant Head level)
      ↓
3. Purchase Order (PurchaseOrderHead / PurchaseOrderItems) — 2-level approval
      ↓
4. Gate Entry (IEPLStockIN_Head) — MRN created
      ↓
5. Quality Check (IEPLStockIN_Detail.QualityApproved)
      ↓
6. MRN 4-stage Approval: Store → Quality → Plant Head → Accounts
      ↓
7. Stock Updated (StockTables + Stock_lotDetail for FIFO items)
```

### 6.3 Line Rejection Cycle

```
Quality identifies rejection
      ↓
LR Voucher Created (LineRejectionHeads / LineRejectionDetails)
      ← Validated: cumulative LR qty ≤ QualityApprovedQty
      ↓
LR Approved → Stock deducted
```

### 6.4 Asset Breakdown Cycle

```
Breakdown Reported (Asset_BreakdownLog — Status: DOWN)
      ← Prevents duplicate open breakdown for same machine
      ↓
Technician Completes Maintenance (Status: MAINT_DONE)
      ↓
ASSETADMIN / SUPERADMIN Final Authorization (Status: WORKING)
```

---

## 7. Key Business Rules

1. **Multi-company isolation** — all queries filter by `Session["Company_ID"]`; login rejected if company mismatch (bypassed for ADMIN roles)
2. **FIFO lot allocation** — items with `FifoLot=true` allocated from `Stock_lotDetail` ordered by ID ascending; BOM line stores `LotID` reference
3. **Voucher numbering** — all voucher numbers sourced from `Bill_Series` table (Series prefix + incremented Number), scoped per Type/Company/FinYear
4. **BOM approval chain** — BOM Created → Store Approval → Management Approval → Production → EWAP/Testing → PDI → Dispatch; each stage gated
5. **MRN 4-stage approval** — Store, Quality, Plant Head, Accounts each set a dedicated flag column
6. **Indent auto-close** — when all lines have `QuantityReceived >= ActualRequired`, head `IsClosed` is set to 1 automatically
7. **Line rejection quantity cap** — LR qty checked against `QualityApprovedQty`; cumulative across multiple LRs validated
8. **PDI prerequisite for dispatch** — `BillingPDIDetails()` checks `BOMEwapDetails.PDIStatus == 1` before allowing dispatch billing
9. **No duplicate breakdown** — system prevents logging a new breakdown for a machine that already has an open (DOWN/MAINT_DONE) log
10. **Maintenance scheduling** — Hangfire computes `LastMaintenanceDoneOn + FrequencyDays` in memory to identify tasks due today
11. **KVA range grouping for analytics** — hardcoded in `MapRatingToRange()`: 7.5–35, 40–160, 200–250, 320–750, 1010–1250 KVA
12. **Document storage path** — files saved to UNC share `\\192.168.0.200\CMKL*` with GUID-prefixed filenames; path stored in enquiry record
13. **Enquiry split** — an enquiry can be split into two records (all 60+ fields cloned, items redistributed)
14. **QR customer portal** — each dispatched unit gets unique QR credentials; customer logs in to view PDI certificate and submit feedback

---

## 8. Navigation Structure

The top navbar (visible to authenticated users) exposes these menu groups:

| Menu | Key Sections |
|---|---|
| **Master** | Supplier, Department, Make, Item master |
| **Inventory** | Goods receipt, Quality, Stock issue/return, Line rejection, Misc billing |
| **Quality** | Inward quality, Line rejection, LR approval |
| **BOM** | Create BOM, BOM planning, Requisition, Indent |
| **Approvals** | Store approval, Management approval, MRN approval stages |
| **Asset** | Machine/task masters, Breakdown management, Maintenance logbook |
| **Reports** | Sale, dispatch, MRN, lot stock, consumption, item master reports |
| **Admin** | User access, BOM alteration, PO reversal, lot correction, SQL backup |
| **Other** | Tender management, TA claims, Email groups, OE/Dealer views |

---

## 9. Authentication & Authorization

- **Login:** POST to `LoginController.Login()` — validates against `tbl_User_Master`; sets 12 session keys on success
- **Session check:** `CheckSessionTimeout()` AJAX call; redirects to login if session is null
- **Page-level access:** Each controller action queries `User_Access` table for the user's permission on that menu name
- **Role-based shortcuts:** Roles `SUPERADMIN`, `ADMIN`, `ASSETADMIN`, `USERSUPER` bypass certain restrictions
- **Password storage:** Plaintext — no hashing or salting implemented
- **No global auth filter** — each action must manually check session/access; missing checks are a vulnerability

---

## 10. Email System

- **SMTP config** stored in `CMKL_Email_Setting` table (host, port, credentials)
- **Distribution groups** managed via `CMKL_Email` table with type categories
- **HTML templates** rendered by RazorEngine from `.cshtml` files in `~/Views/EmailManage/`
- **Automated emails:** Daily sale report, MRN report, BOM approval reminders, maintenance task alerts, customer feedback confirmation
- **Trigger emails:** Enquiry stage change, order reversal, QR credential delivery

---

## 11. PDF & Excel Generation

- **PDF (Rotativa):** Test certificates, MRN prints, indent prints, packing slips, dispatch documents — all rendered from Razor views using `ViewAsPdf()`
- **Excel (ClosedXML / EPPlus):** Purchase report, BOM item list, stock reports exported to `.xlsx`

---

## 12. Frontend Assets

| Asset | Path | Purpose |
|---|---|---|
| Bootstrap 5.3.3 | `Content/bootstrap.css` | Responsive grid and components |
| Font Awesome 4.7.0 | `fonts/font-awesome-4.7.0/` | Icon set |
| Linearicons | `fonts/Linearicons-Free-v1.0.0/` | Additional icons |
| Montserrat font | `fonts/montserrat/` | Custom typography |
| Toastr | `toastr/` | Toast notifications |
| Select2 | `vendor/select2/` | Enhanced dropdowns |
| DateRangePicker | `vendor/daterangepicker/` | Date range inputs |
| Perfect Scrollbar | `vendor/perfect-scrollbar/` | Custom scrollbars |
| Site.css | `Content/Site.css` | Base styles |
| login.css | `Content/login.css` | Login page styles |
| main.css | `css/main.css` | Layout utilities |
| Save.wav / Error.mp3 | `Sound/` | Audio feedback on save/error |

---

## 13. Known Issues & Gaps

| Issue | Details |
|---|---|
| Plaintext passwords | No hashing in `tbl_User_Master` — security risk |
| No global auth attribute | Each action must manually check session; some actions may be unprotected |
| No service/repository layer | All business logic in controllers — tight coupling, difficult to test |
| No DI container | `IECEntities DB = new IECEntities()` in every controller — no lifecycle management |
| Hardcoded connection string | `sa` credentials in `Web.config` — should use Windows Auth or encrypted config |
| Hardcoded UNC paths | Document storage paths (`\\192.168.0.200\...`) hardcoded in controller |
| Stub controllers | `ServiceController`, `EmployeeController`, `PhotographyController` are empty placeholders |
| Audit log disabled | `ActivityController.LogUserActivity()` mostly commented out — no change tracking |
| Hangfire memory scheduling | Maintenance due-date calculation done in C# memory loop, not in SQL — may be slow at scale |

---

## 14. File Structure Summary

```
CMKLMVC/
├── App_Start/          BundleConfig, FilterConfig, RouteConfig
├── Controllers/        24 controller files
├── Models/             EF EDMX + partial classes
├── Views/              Razor views per controller + Shared layouts
├── Content/            Bootstrap CSS + custom CSS
├── Scripts/            jQuery, Bootstrap JS, Grid.Mvc, Toastr
├── fonts/              Font Awesome, Linearicons, Montserrat
├── css/                main.css, sidebar, util
├── vendor/             Select2, DateRangePicker, Perfect Scrollbar
├── Sound/              Save.wav, Error.mp3
├── Files/              BOM_List.xlsx, Purchase Report.xlsx
├── App_Data/           IndentPDFs folder (generated PDFs)
└── Web.config          Connection string, app settings
```

---

*Document generated: June 2026 — based on full project source analysis.*
