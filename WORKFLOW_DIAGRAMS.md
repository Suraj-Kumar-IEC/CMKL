# CMKL ERP — Complete Workflow Diagrams & Step-by-Step Guide

---

## INDEX OF WORKFLOWS

1. [User Login & Session Flow](#1-user-login--session-flow)
2. [Sales Enquiry & Order Acceptance (OA) Lifecycle](#2-sales-enquiry--order-acceptance-lifecycle)
3. [Purchase Cycle — Indent → PO → MRN → Stock](#3-purchase-cycle)
4. [BOM (Bill of Materials) & Production Job Lifecycle](#4-bom--production-job-lifecycle)
5. [Quality Control — Inward Inspection & Line Rejection](#5-quality-control)
6. [Production — Assembly, EWAP Testing & PDI](#6-production--assembly-ewap-testing--pdi)
7. [Dispatch & Customer Feedback](#7-dispatch--customer-feedback)
8. [FG Return Workflow](#8-fg-return-workflow)
9. [Plant Asset Maintenance & Breakdown Management](#9-plant-asset-maintenance--breakdown-management)
10. [Background Jobs (Hangfire Scheduler)](#10-background-jobs--hangfire-scheduler)
11. [User Access & Permission Management](#11-user-access--permission-management)
12. [Complete System Master Workflow](#12-complete-system-master-workflow)

---

## 1. User Login & Session Flow

```
┌─────────────────────────────────────────────────────────────────────┐
│                        USER LOGIN FLOW                              │
└─────────────────────────────────────────────────────────────────────┘

        [User Opens Browser]
               │
               ▼
    ┌──────────────────────┐
    │  Login Page Loads    │  GET /Login/Index
    │  (Login/Index)       │  → GetCompanies() loads
    │                      │    companies + FY dropdown
    └──────────┬───────────┘
               │  Enter Username, Password,
               │  Company, Financial Year
               ▼
    ┌──────────────────────┐
    │  POST Login(TD,      │
    │  finyear)            │
    └──────────┬───────────┘
               │
       ┌───────▼────────┐
       │ Find user in   │
       │tbl_User_Master │
       └───────┬────────┘
               │
       ┌───────▼────────┐         ┌──────────────────────┐
       │ User found?    │──  NO ──▶│  Show error:         │
       └───────┬────────┘         │  "Invalid credentials"│
               │ YES              └──────────────────────┘
               ▼
       ┌───────────────┐          ┌──────────────────────┐
       │ Password      │──  NO ──▶│  Show error:         │
       │ matches?      │          │  "Wrong password"    │
       └───────┬───────┘          └──────────────────────┘
               │ YES
               ▼
       ┌───────────────┐          ┌──────────────────────┐
       │ Company ID    │──  NO ──▶│  Show error:         │
       │ authorized?   │          │  "Company mismatch"  │
       │(bypass ADMIN) │          └──────────────────────┘
       └───────┬───────┘
               │ YES
               ▼
    ┌──────────────────────────────┐
    │  Set 12 Session Keys:        │
    │  User_ID, UserName,          │
    │  User_Role, Company_ID,      │
    │  Company_Name, FinYear,      │
    │  FinYear_End, Designation,   │
    │  Department, SignatureUrl,   │
    │  ProfilePhoto, UserBranchID  │
    └──────────┬───────────────────┘
               │
               ▼
    ┌──────────────────────┐
    │   Redirect to        │
    │   Dashboard /        │
    │   Home/MenuPage      │
    └──────────────────────┘

    ─────── DURING SESSION ───────

    Every page action:
    ┌───────────────────┐    Session      ┌───────────────────┐
    │  Page Request     │──  null? ──YES──▶  Redirect to      │
    └────────┬──────────┘                 │  Login/Index      │
             │ NO                         └───────────────────┘
             ▼
    ┌───────────────────┐    Access       ┌───────────────────┐
    │ Check User_Access │──  denied? YES──▶  Show "Access     │
    │ table for menu    │                 │  Denied" message  │
    └────────┬──────────┘                 └───────────────────┘
             │ Granted
             ▼
    ┌───────────────────┐
    │   Execute Action  │
    └───────────────────┘

    ─────── LOGOUT ───────

    GET /Login/logout  →  Clear ALL session keys  →  Redirect to Login/Index
```

---

### 1. Login — Point-by-Point Steps

1. User navigates to the application URL → system routes to `Login/Index` (default route)
2. Page loads and calls `GetCompanies()` via AJAX → populates Company dropdown with active companies and Financial Year dropdown
3. User selects company, enters username and password, selects financial year, clicks Login
4. POST to `LoginController.Login(TD, finyear)` with form data
5. Query `tbl_User_Master` for matching username
6. If not found → return error "Invalid credentials"
7. If found → compare entered password with stored password (plaintext comparison)
8. If password mismatch → return error "Wrong password"
9. Check if user's `CompanyId` matches selected company (ADMIN/SUPERADMIN roles bypass this check)
10. If mismatch → return error "Company not authorized"
11. All checks pass → set 12 session keys with user details, role, company, and FY info
12. Redirect to `Dashboard/Dashboard` or `Home/MenuPage`
13. On each subsequent request → check `Session["User_ID"]` is not null; if null → redirect to login
14. On each action → query `User_Access` for `UserID + MenuName` → if `Status = false` → deny access
15. On logout → call `Login/logout` → all session keys abandoned → redirect to login page


---

## 2. Sales Enquiry & Order Acceptance Lifecycle

```
┌─────────────────────────────────────────────────────────────────────┐
│              SALES ENQUIRY → ORDER → DISPATCH LIFECYCLE             │
└─────────────────────────────────────────────────────────────────────┘

  ┌─────────────────────┐
  │  ENQUIRY CREATED    │  Table: CMKL_Enquiry
  │  OAStage = 0        │  + CMKL_Enquiry_Item (line items)
  │  (Price Revision)   │
  └──────────┬──────────┘
             │  Sales team reviews & revises price
             ▼
  ┌─────────────────────┐
  │  OAStage = 1        │
  │  Pending Order      │  Customer confirms order
  │  Acceptance         │
  └──────────┬──────────┘
             │
             ├─────────────────────────────────────────┐
             │  Can REVERSE back to Stage 0             │
             │  (ReverseEnquiry → email notification)   │
             └─────────────────────────────────────────┘
             │  OA confirmed
             ▼
  ┌─────────────────────┐
  │  OAStage = 2        │  Upload: GA Drawing, Layout,
  │  Drawing Upload     │  Foundation Drawing
  └──────────┬──────────┘  (saved to \\192.168.0.200\...)
             │
             ▼
  ┌─────────────────────┐
  │  OAStage = 3        │  Customer approves drawings
  │  Drawing Approval   │
  └──────────┬──────────┘
             │
             │  ◄── SPLIT possible here:
             │       OrderDetail(split=true) clones all
             │       60+ fields into a new record,
             │       items redistributed via MoveItems()
             ▼
  ┌─────────────────────┐
  │  OAStage = 4        │  BOM job created for this enquiry
  │  Pending Production │  → triggers BOM workflow
  └──────────┬──────────┘
             │
             ▼
  ┌─────────────────────┐
  │  OAStage = 5        │  BOM approved, production started
  │  In Production      │  Engine + alternator being assembled
  └──────────┬──────────┘
             │
             ▼
  ┌─────────────────────┐
  │  OAStage = 6        │  EWAP testing & PDI in progress
  │  Quality Check      │
  └──────────┬──────────┘
             │
             ▼
  ┌─────────────────────┐
  │  OAStage = 7        │  PDI complete, unit ready
  │  Ready to Dispatch  │  Packing slip generated
  └──────────┬──────────┘
             │
             ▼
  ┌─────────────────────┐
  │  OAStage = 8        │  Invoice raised, LR number,
  │  DISPATCHED         │  Transport details entered
  └──────────┬──────────┘  Table: DispatchDetails
             │
             ▼
  ┌─────────────────────┐
  │  QR Code Generated  │  CMKL_QRLogin credentials created
  │  for Customer       │  QR sent to customer
  └──────────┬──────────┘
             │
             ▼
  ┌─────────────────────┐
  │  Customer Feedback  │  Customer scans QR → logs into
  │  (8 parameters)     │  portal → submits CMKL_Feedback
  └─────────────────────┘  → confirmation email sent
```

---

### 2. Sales Enquiry — Point-by-Point Steps

1. Sales team creates enquiry in `CMKL_Enquiry` with customer details, product specs, KVA rating, quantity — OAStage set to **0** (Price Revision)
2. Enquiry line items added to `CMKL_Enquiry_Item` (product, qty, pricing per unit)
3. Sales team revises pricing, prepares quotation
4. Customer confirms order → OAStage moved to **1** (Pending Order Acceptance)
5. If customer requests changes → `ReverseEnquiry()` rolls back stage; email notification sent automatically
6. Order acceptance confirmed → OAStage moves to **2** (Drawing Upload)
7. Technical team uploads GA Drawing, Layout, Foundation Drawing → files saved to UNC share `\\192.168.0.200\CMKL*` with GUID-prefixed names; path stored in enquiry record
8. Customer reviews and approves drawings → OAStage moves to **3** (Drawing Approval)
9. If order needs to be split (e.g., partial delivery) → `OrderDetail(split=true)` clones all 60+ fields; `MoveItems()` redistributes line items to the new enquiry record
10. Production planning starts → OAStage moves to **4** (Pending Production); BOM job creation begins
11. BOM approved and production starts → OAStage moves to **5** (In Production)
12. Assembly complete; EWAP/PDI testing begins → OAStage moves to **6** (Quality Check)
13. PDI passes → OAStage moves to **7** (Ready to Dispatch); packing slip generated
14. Dispatch billing done → OAStage moves to **8** (Dispatched); `DispatchDetails` record created with invoice no., LR no., transporter, and bill date
15. QR login credentials created in `CMKL_QRLogin`; QR code sent to customer
16. Customer scans QR → logs into `GuestLogin` portal → views test certificate → submits 8-parameter feedback → confirmation email auto-sent


---

## 3. Purchase Cycle

```
┌─────────────────────────────────────────────────────────────────────┐
│           PURCHASE CYCLE: INDENT → PO → MRN → STOCK                │
└─────────────────────────────────────────────────────────────────────┘

  ┌──────────────────────────────┐
  │  INDENT CREATED              │  BOMIndentHead + BomIndentLines
  │  (Manual / BOM-triggered)    │  Voucher No from Bill_Series
  └──────────────┬───────────────┘
                 │  Store reviews items needed
                 ▼
  ┌──────────────────────────────┐     ┌────────────────────┐
  │  INDENT APPROVAL             │─NO──▶  Reject / Return   │
  │  (Plant Head reviews)        │     │  for correction    │
  └──────────────┬───────────────┘     └────────────────────┘
                 │ Approved
                 ▼
  ┌──────────────────────────────┐
  │  PURCHASE ORDER CREATED      │  PurchaseOrderHead +
  │                              │  PurchaseOrderItems
  └──────────────┬───────────────┘  Supplier selected from
                 │                  SupplierMasters
                 ▼
  ┌──────────────────────────────┐     ┌────────────────────┐
  │  PO APPROVAL – LEVEL 1       │─NO──▶  Reject PO         │
  │  (Plant Head)                │     └────────────────────┘
  └──────────────┬───────────────┘
                 │ Approved
                 ▼
  ┌──────────────────────────────┐     ┌────────────────────┐
  │  PO APPROVAL – LEVEL 2       │─NO──▶  Reject PO         │
  │  (Super Admin)               │     └────────────────────┘
  └──────────────┬───────────────┘
                 │ Approved
                 │  PO printed/sent to supplier
                 ▼
  ┌──────────────────────────────┐
  │  GOODS ARRIVE AT GATE        │
  │  Gate Entry / MRN Created    │  IEPLStockIN_Head
  └──────────────┬───────────────┘  (partial receipt allowed)
                 │
                 ▼
  ┌──────────────────────────────┐     ┌────────────────────┐
  │  QUALITY INSPECTION          │─NO──▶  Rejection noted   │
  │  QualityApproved flag set    │     │  Line Rejection     │
  │  per line item               │     │  workflow starts   │
  └──────────────┬───────────────┘     └────────────────────┘
                 │ Approved
                 ▼
  ┌──────────────────────────────┐
  │  MRN APPROVAL – STAGE 1      │  Store approves receipt
  │  (Store)                     │
  └──────────────┬───────────────┘
                 ▼
  ┌──────────────────────────────┐
  │  MRN APPROVAL – STAGE 2      │  Quality dept confirms
  │  (Quality)                   │
  └──────────────┬───────────────┘
                 ▼
  ┌──────────────────────────────┐
  │  MRN APPROVAL – STAGE 3      │  Plant Head authorizes
  │  (Plant Head)                │
  └──────────────┬───────────────┘
                 ▼
  ┌──────────────────────────────┐
  │  MRN APPROVAL – STAGE 4      │  Accounts finalizes
  │  (Accounts)                  │
  └──────────────┬───────────────┘
                 │ All 4 approvals done
                 ▼
  ┌──────────────────────────────┐
  │  STOCK UPDATED               │  StockTables (qty balance)
  │                              │  Stock_lotDetail (if FifoLot=true)
  └──────────────┬───────────────┘  — lot serial, qty, availability
                 │
                 ▼
  ┌──────────────────────────────┐
  │  INDENT AUTO-CLOSE CHECK     │  If ALL lines:
  │                              │  QtyReceived >= ActualRequired
  │                              │  → IsClosed = 1
  └──────────────────────────────┘

  ──── PARTIAL RECEIPT PATH ────

  If supplier delivers partial quantity:
  SavePartialReceipt() → updates received qty per line
  → Indent stays open (IsClosed = 0) until fully received
  → Remaining qty shows in pending indent tracking
```

---

### 3. Purchase Cycle — Point-by-Point Steps

1. Store identifies stock requirement (low stock alert or BOM material demand)
2. Purchase Indent created via `PurchaseController.SaveIndentManual()` — voucher number auto-assigned from `Bill_Series` table
3. `GetItemDetails(itemCode)` called to populate last purchase price and 3-month average consumption
4. Each indent line captures: item code, required qty, UOM, expected date
5. Indent submitted for approval → Plant Head reviews pending indents
6. Plant Head approves indent → status flag updated in `BOMIndentHeads`
7. Approved indent visible in PO creation screen → Purchase Order created in `PurchaseOrderHead` + `PurchaseOrderItems`
8. Supplier selected from `SupplierMasters`; terms, tax, and pricing added
9. PO goes through 2-level approval: Plant Head (Level 1) → Super Admin (Level 2)
10. If rejected at either level → PO returned for correction
11. Both approvals done → PO printed (Rotativa PDF) and sent to supplier
12. Material arrives at plant gate → Gate Entry / MRN created in `IEPLStockIN_Head`
13. Partial receipts are supported — `SavePartialReceipt()` logs qty received per line; indent stays open
14. Quality team inspects each received item → `QualityApproved` flag set per `IEPLStockIN_Detail` line
15. Rejected items go to Line Rejection workflow; accepted qty proceeds
16. MRN goes through 4-stage approval: Store → Quality → Plant Head → Accounts
17. Each stage sets a dedicated approval flag column on the MRN head record
18. After all 4 approvals: `StockTables` updated with received qty
19. For FIFO items (`FifoLot=true`): new `Stock_lotDetail` record created with serial number, qty, availability flag
20. System checks if all indent lines are fully received → if yes, `BOMIndentHeads.IsClosed = 1` (auto-closed)


---

## 4. BOM & Production Job Lifecycle

```
┌─────────────────────────────────────────────────────────────────────┐
│         BOM (BILL OF MATERIALS) & PRODUCTION JOB LIFECYCLE          │
└─────────────────────────────────────────────────────────────────────┘

  ┌──────────────────────────────┐
  │  BOM JOB CREATED             │  BOMVouchers (head)
  │  (Linked to Enquiry /        │  + BOMVoucherlines (items)
  │   Production plan)           │  Voucher No from Bill_Series
  └──────────────┬───────────────┘
                 │  FG product selected (BOMItemMasters)
                 │  Raw materials added per BOM template
                 ▼
  ┌──────────────────────────────┐
  │  FIFO LOT ALLOCATION         │  For each raw item:
  │                              │  IF FifoLot = true:
  │                              │    Query Stock_lotDetail
  │                              │    ORDER BY ID ASC
  │                              │    Allocate from oldest lot
  │                              │    BOMVoucherlines.LotID set
  └──────────────┬───────────────┘
                 │
                 ▼
  ┌──────────────────────────────┐     ┌────────────────────────┐
  │  STOCK AVAILABILITY CHECK    │─NO──▶  Flag insufficient     │
  │  StockTables.Qty >= Required │     │  stock; raise indent   │
  └──────────────┬───────────────┘     └────────────────────────┘
                 │ Stock sufficient
                 ▼
  ┌──────────────────────────────┐
  │  STORE APPROVAL              │  BOMVouchers.StoreApproved
  │  (Store Manager)             │  = 1
  │                              │  Hangfire sends reminder
  │                              │  every 3 hrs if pending
  └──────────────┬───────────────┘
                 │
                 ▼
  ┌──────────────────────────────┐
  │  MANAGEMENT APPROVAL         │  BOMVouchers.MgmtApproved
  │  (Management / Admin)        │  = 1
  └──────────────┬───────────────┘
                 │
                 ▼
  ┌──────────────────────────────┐
  │  BOM APPROVED                │  Materials reserved
  │                              │  Production can begin
  └──────────────┬───────────────┘
                 │
        ┌────────▼────────┐
        │  REQUISITION    │  BOMRequisitionHead +
        │  (if needed)    │  BOMRequisitionLine
        │  Store issues   │  Materials issued from store
        │  materials      │  IEPLStockIssueHead/Detail
        └────────┬────────┘
                 │
                 ▼
  ┌──────────────────────────────┐
  │  PRODUCTION STAGE UPDATE     │  Bom_ProductionUpdate:
  │                              │  - Engine model & serial
  │                              │  - Alternator model & serial
  │                              │  - Assembly stage progress
  └──────────────┬───────────────┘
                 │
                 ▼
  ┌──────────────────────────────┐  (See Section 6)
  │  EWAP TESTING & PDI          │  → BOMEwapDetails
  │                              │  → BOM_TestingUpdate
  └──────────────┬───────────────┘
                 │
                 ▼
  ┌──────────────────────────────┐
  │  BOM JOB COMPLETE            │  BOMVouchers.PDIStatus = 1
  │  Ready for Dispatch          │
  └──────────────────────────────┘

  ──── BOM ALTERATION PATH ────

  ┌──────────────────────────────┐
  │  Admin opens BOMAlteration() │
  └──────────────┬───────────────┘
                 │
        ┌────────▼────────┐
        │ Delete a line?  │  AlterBOMDeleteLine(lineid)
        │                 │  → Soft-delete line
        │                 │  → REVERSE stock deduction
        │                 │  → REVERSE lot allocation
        └────────┬────────┘
                 │  OR
        ┌────────▼────────┐
        │  Add new line?  │  AddBomAlterationItems()
        │                 │  → New FIFO lot allocation
        │                 │  → Update BOMVoucherlines
        └─────────────────┘

  ──── CANCELLATION PATH ────

  BOM can be cancelled (admin) → all stock allocations reversed
  → all lot reservations freed → BOMVoucher marked Cancelled
```

---

### 4. BOM & Production Job — Point-by-Point Steps

1. Production team creates BOM job in `BOMController.BOM()` — linked to a sales enquiry or standalone plan
2. Voucher number auto-assigned from `Bill_Series` (scoped to Company + FY + Type)
3. FG (Finished Goods) product selected from `BOMItemMasters` where `ItemCategory = 1`
4. System loads the standard BOM template — all required raw materials auto-populated as `BOMVoucherlines`
5. For each BOM line item: if `BOMItemMasters.FifoLot = true`, system queries `Stock_lotDetail` ordered by ID ascending (FIFO) — oldest lot allocated first
6. `BOMVoucherlines.LotID` set to the allocated lot's ID
7. Stock availability checked against `StockTables`; insufficient stock flagged for indent generation
8. BOM job saved → status: pending store approval
9. Hangfire reminder job sends email every 3 hours (Mon–Sat, 10 AM–8 PM) if store approval is pending
10. Store Manager reviews BOM in `StoreApproval` view → approves → `BOMVouchers.StoreApproved = 1`
11. Management reviews in `ManagementApproval` view → approves → `BOMVouchers.MgmtApproved = 1`
12. BOM fully approved → materials reserved; production team can start
13. Store issues physical materials via Stock Issue voucher (`IEPLStockIssueHead` / `IEPLStockIssueDetail`)
14. Optionally, Requisition (`BOMRequisitionHead`) created for store to track issues
15. Production team updates `Bom_ProductionUpdate` with engine model, engine serial number, alternator model, alternator serial
16. Assembly progresses through department stages (from `DepartmentMasters` where `isBOMStage = true`)
17. After assembly: EWAP testing begins → see Section 6
18. **BOM Alteration (admin only):** `AlterBOMDeleteLine()` soft-deletes a line and reverses both stock deduction and FIFO lot allocation; `AddBomAlterationItems()` adds a replacement line with fresh FIFO allocation
19. **BOM Cancellation:** Admin cancels BOM → all stock/lot allocations reversed → `BOMVouchers` marked cancelled → appears in `CancelledBOMReport`


---

## 5. Quality Control

```
┌─────────────────────────────────────────────────────────────────────┐
│          QUALITY CONTROL — INWARD INSPECTION & LINE REJECTION       │
└─────────────────────────────────────────────────────────────────────┘

  ════════════ INWARD QUALITY INSPECTION ════════════

  ┌──────────────────────┐
  │  Material Received   │  IEPLStockIN_Head / Detail
  │  (MRN created)       │
  └──────────┬───────────┘
             │  Quality team opens Qualityin view
             ▼
  ┌──────────────────────┐
  │  Inspect each item   │
  │  per MRN line        │
  └──────────┬───────────┘
             │
      ┌──────┴──────┐
      │             │
      ▼             ▼
  ACCEPTED      REJECTED
  QualityApproved  ┌──────────────────────┐
  = true           │  Rejection noted     │
                   │  Reason recorded     │
                   │  → Line Rejection    │
                   │    workflow          │
                   └──────────────────────┘

  ════════════ LINE REJECTION WORKFLOW ════════════

  ┌──────────────────────┐
  │  Quality identifies  │
  │  defective item in   │
  │  production/store    │
  └──────────┬───────────┘
             │
             ▼
  ┌──────────────────────┐
  │  LR Voucher Created  │  LineRejectionHeads +
  │  InventoryController │  LineRejectionDetails
  │  .SaveLR(data)       │  Voucher No from Bill_Series
  └──────────┬───────────┘
             │
             ▼
  ┌───────────────────────────────────────────┐
  │  VALIDATION CHECK                         │
  │  Cumulative LR qty (all previous LRs for  │
  │  this item/lot) + new qty                 │
  │        ≤                                  │
  │  QualityApprovedQty for this MRN line     │
  └───────────┬───────────────────────────────┘
              │
      ┌───────┴────────┐
      │                │
      ▼                ▼
   VALID            INVALID
      │           ┌─────────────────────┐
      │           │  Error: "Rejection  │
      │           │  qty exceeds        │
      │           │  approved qty"      │
      │           └─────────────────────┘
      ▼
  ┌──────────────────────┐
  │  LR APPROVAL         │  ApproveLR(lrId)
  │  Quality Head /      │  → Stock deducted from
  │  Admin approves      │    StockTables
  └──────────┬───────────┘  → Lot qty updated in
             │                Stock_lotDetail
             ▼
  ┌──────────────────────┐
  │  Rejected material   │
  │  returned to vendor  │
  │  / scrapped          │
  └──────────────────────┘
```

---

### 5. Quality Control — Point-by-Point Steps

**Inward Quality Inspection:**
1. Quality team opens `Inventory/Qualityin` view after MRN is created
2. Each MRN line item inspected for quality, quantity, and specification compliance
3. For each accepted line → `IEPLStockIN_Detail.QualityApproved = true`
4. Accepted items proceed to MRN 4-stage approval → stock updated
5. For rejected items → quantity and rejection reason recorded → Line Rejection workflow initiated

**Line Rejection Workflow:**
1. Quality team opens `Inventory/LineRejection` view
2. `InventoryController.SaveLR()` called with item, MRN reference, rejection qty, and reason
3. System validates: sum of all existing LR quantities for this item/MRN + new LR quantity must not exceed `QualityApprovedQty` for that MRN line
4. If validation fails → error returned; LR not saved
5. If valid → `LineRejectionHeads` + `LineRejectionDetails` records created with auto-generated voucher number
6. LR submitted for approval
7. Quality Head or Admin approves via `ApproveLR(lrId)` → `LineRejectionHeads.IsApproved = true`
8. On approval → `StockTables` quantity decremented by rejection qty
9. If FIFO item → `Stock_lotDetail` qty updated accordingly
10. Rejected material physically returned to vendor or sent for scrap disposal


---

## 6. Production — Assembly, EWAP Testing & PDI

```
┌─────────────────────────────────────────────────────────────────────┐
│         PRODUCTION: ASSEMBLY → EWAP TESTING → PDI                  │
└─────────────────────────────────────────────────────────────────────┘

  ┌──────────────────────────────────┐
  │  BOM APPROVED                    │  Materials issued to
  │  (from Section 4)                │  production floor
  └──────────────────┬───────────────┘
                     │
                     ▼
  ┌──────────────────────────────────┐
  │  PRODUCTION UPDATE               │  Bom_ProductionUpdate
  │  (Assembly Stage)                │  Fields:
  │  Production team logs details    │  - Engine Make/Model
  │                                  │  - Engine Serial No
  │                                  │  - Alternator Make/Model
  │                                  │  - Alternator Serial No
  │                                  │  - Phase, KVA Rating
  └──────────────────┬───────────────┘
                     │  Job appears in PendingProductionView
                     ▼
  ┌──────────────────────────────────┐
  │  EWAP (End of Line Testing)      │  BOMEwapDetails
  │  Job appears in PendingEwap()    │  Fields:
  │                                  │  - Engine serial confirmed
  │                                  │  - Alternator serial confirmed
  │                                  │  - Test parameters
  │                                  │  - EWAP pass/fail
  └──────────────────┬───────────────┘
                     │
                     ▼
  ┌──────────────────────────────────┐
  │  TESTING UPDATE                  │  BOM_TestingUpdate
  │                                  │  Fields:
  │                                  │  - Battery type
  │                                  │  - Control Panel type
  │                                  │  - KRM Number
  │                                  │  - Fuel tank capacity
  │                                  │  - Final rating confirmed
  └──────────────────┬───────────────┘
                     │
                     ▼
  ┌──────────────────────────────────┐
  │  PDI (Pre-Delivery Inspection)   │  Triggers UpdatePDIForm()
  │  Job in PendingPDI() queue       │  Updates 4 tables:
  │                                  │  1. BOMVouchers
  │                                  │  2. Bom_ProductionUpdate
  │                                  │  3. BOMEwapDetails
  │                                  │  4. BOM_TestingUpdate
  └──────────────────┬───────────────┘
                     │
             ┌───────┴───────┐
             │               │
             ▼               ▼
         PASS             FAIL
             │               │
             │           ┌───────────────┐
             │           │ PDI Failure   │
             │           │ noted; unit   │
             │           │ returned to   │
             │           │ production    │
             │           └───────────────┘
             │
             ▼
  ┌──────────────────────────────────┐
  │  PDI COMPLETE                    │  BOMEwapDetails.PDIStatus = 1
  │  BOMVouchers updated             │  Job moves to CompletedPDI()
  │                                  │  Test Certificate printable
  └──────────────────┬───────────────┘  (GetBOMPrintData → Rotativa PDF)
                     │
                     ▼
  ┌──────────────────────────────────┐
  │  PACKING SLIP GENERATED          │  Unit packed, tagged
  │                                  │  Ready for dispatch billing
  └──────────────────┬───────────────┘
                     │
                     ▼
  ┌──────────────────────────────────┐
  │  DISPATCH BILLING CHECK          │  BillingPDIDetails(ewapId)
  │  (Pre-condition gate)            │  Checks:
  │                                  │  PDIStatus == 1? → Allow billing
  │                                  │  PDIStatus != 1? → Block billing
  └──────────────────┬───────────────┘
                     │ PDI confirmed
                     ▼
  ┌──────────────────────────────────┐
  │  → Proceeds to DISPATCH          │  (See Section 7)
  └──────────────────────────────────┘
```

---

### 6. Production / EWAP / PDI — Point-by-Point Steps

1. After BOM approval, production team opens `Production/PendingProductionView` — lists all approved BOM jobs
2. Team selects job → enters assembly details in `Bom_ProductionUpdate`: engine make/model, engine serial, alternator make/model, alternator serial, KVA rating, phase
3. Engine and alternator serial numbers retrieved from FIFO lot assignments via `GetSerialNumbers(voucherId)` — sourced from `Stock_lotDetail`
4. Assembly progresses through production stages (departments flagged `isBOMStage = true`)
5. After physical assembly, job appears in `PendingEwap()` queue for end-of-line testing
6. EWAP technician performs electrical/mechanical tests → results saved in `BOMEwapDetails`
7. Testing team also completes `BOM_TestingUpdate`: battery spec, control panel type, KRM number, fuel tank capacity, final confirmed rating
8. After EWAP, job enters `PendingPDI()` queue for Pre-Delivery Inspection
9. PDI inspector performs final walkthrough inspection of the complete genset
10. PDI form submitted via `UpdatePDIForm()` → simultaneously updates 4 tables: `BOMVouchers`, `Bom_ProductionUpdate`, `BOMEwapDetails`, `BOM_TestingUpdate`
11. If PDI fails → notes recorded; unit sent back to production floor for rectification
12. If PDI passes → `BOMEwapDetails.PDIStatus = 1`; job moves to `CompletedPDI()` list
13. Test Certificate printable via `GetBOMPrintData(BOMID)` — Rotativa renders full certificate PDF including company logo, signatures, all test parameters
14. Packing slip generated (can be cancelled by admin via `CancelPackingSlip()`)
15. Before dispatch billing, `BillingPDIDetails(ewapId)` is called as a gate-check — if `PDIStatus != 1`, billing is blocked
16. PDI confirmed → dispatch billing can proceed (Section 7)


---

## 7. Dispatch & Customer Feedback

```
┌─────────────────────────────────────────────────────────────────────┐
│              DISPATCH → CUSTOMER FEEDBACK WORKFLOW                  │
└─────────────────────────────────────────────────────────────────────┘

  ┌──────────────────────────────────┐
  │  PDI COMPLETE                    │
  │  (BOMEwapDetails.PDIStatus = 1)  │
  └──────────────────┬───────────────┘
                     │
                     ▼
  ┌──────────────────────────────────┐
  │  DISPATCH VIEW                   │  Production/Dispatch()
  │  Select unit to dispatch         │
  └──────────────────┬───────────────┘
                     │  Enter dispatch details:
                     ▼
  ┌──────────────────────────────────┐
  │  DISPATCH DETAILS ENTRY          │  DispatchDetails table:
  │                                  │  - Customer name / address
  │                                  │  - Invoice number & date
  │                                  │  - Bill amount
  │                                  │  - Transport company
  │                                  │  - LR (Lorry Receipt) number
  │                                  │  - Vehicle number
  │                                  │  - Dispatch date
  └──────────────────┬───────────────┘
                     │
                     ▼
  ┌──────────────────────────────────┐
  │  ENQUIRY STATUS UPDATED          │  CMKL_Enquiry.OAStage = 8
  │  (DISPATCHED)                    │  (Dispatched)
  └──────────────────┬───────────────┘
                     │
                     ▼
  ┌──────────────────────────────────┐
  │  QR LOGIN CREDENTIALS CREATED   │  CMKL_QRLogin table:
  │  (CreateQRLogin / SaveQRLogin)   │  - Username (unique per unit)
  │                                  │  - Password
  │                                  │  - Linked to enquiry/product
  └──────────────────┬───────────────┘
                     │
                     ▼
  ┌──────────────────────────────────┐
  │  QR CODE GENERATED               │  MessagingToolkit.QRCode
  │  & SENT TO CUSTOMER              │  QR encodes portal login URL
  └──────────────────┬───────────────┘
                     │  Customer receives unit + QR code
                     ▼
  ┌──────────────────────────────────┐
  │  CUSTOMER PORTAL (GuestLogin)    │  /Login/GuestLogin
  │  Customer scans QR               │  /Login/QRLogin(user,pass,id)
  │  → Portal session set            │
  └──────────────────┬───────────────┘
                     │
             ┌───────┴────────────┐
             │                    │
             ▼                    ▼
  ┌──────────────────┐   ┌────────────────────────┐
  │  VIEW TEST       │   │  SUBMIT FEEDBACK        │
  │  CERTIFICATE     │   │  CMKLProduct/           │
  │  (Rotativa PDF)  │   │  CustomerFeedback()     │
  └──────────────────┘   └────────────┬───────────┘
                                      │  Rate 8 parameters:
                                      │  1. Product Quality
                                      │  2. On-time Delivery
                                      │  3. After-sales Support
                                      │  4. Technical Support
                                      │  5. Sales Team
                                      │  6. Documentation
                                      │  7. Packaging
                                      │  8. Overall Satisfaction
                                      ▼
                         ┌────────────────────────┐
                         │  SaveCustomerRating()  │
                         │  → CMKL_Feedback saved │
                         │  → Confirmation email  │
                         │    sent to customer    │
                         └────────────────────────┘
```

---

### 7. Dispatch & Customer Feedback — Point-by-Point Steps

1. Billing team opens `Production/Dispatch()` view — lists units with `PDIStatus = 1` (PDI complete)
2. `BillingPDIDetails(ewapId)` called as a gate check — confirms PDI is done before allowing billing entry
3. Dispatch details entered: customer name, customer address, invoice number, invoice date, bill amount, transporter name, LR number, vehicle number, dispatch date
4. `DispatchDetails` record saved → unit's dispatch information permanently recorded
5. `CMKL_Enquiry.OAStage` updated to **8** (Dispatched)
6. Admin creates QR Login credentials in `CMKL_QRLogin` via `CreateQRLogin()` / `SaveQRLogin()` — unique username/password per dispatched unit
7. QR code generated using `MessagingToolkit.QRCode` — encodes the portal URL with the unit's credentials
8. QR code printed on physical label and attached to the genset / included in delivery documents
9. Customer receives the unit and scans QR code using smartphone
10. QR scan opens browser to `/Login/GuestLogin` → `QRLogin(user, pass, id)` authenticates and sets portal session
11. Customer can view the Test Certificate (PDI report) as PDF via Rotativa
12. Customer opens `CMKLProduct/CustomerFeedback()` — feedback form with 8 satisfaction parameters
13. Customer submits ratings → `SaveCustomerRating()` saves to `CMKL_Feedback` table
14. System automatically sends confirmation email to customer via RazorEngine template


---

## 8. FG Return Workflow

```
┌─────────────────────────────────────────────────────────────────────┐
│              FINISHED GOODS (FG) RETURN WORKFLOW                    │
└─────────────────────────────────────────────────────────────────────┘

  ┌──────────────────────────────────┐
  │  CUSTOMER RETURNS UNIT           │  Physical return
  │  (DispatchReturn view)           │  DispatchReturns table
  └──────────────────┬───────────────┘
                     │
                     ▼
  ┌──────────────────────────────────┐
  │  RETURN LOGGED                   │  DispatchReturns record
  │  Production/DispatchReturn()     │  - Original dispatch ref
  │                                  │  - Return reason
  │                                  │  - Return date
  └──────────────────┬───────────────┘
                     │
                     ▼
  ┌──────────────────────────────────┐
  │  FG RETURN QUALITY INSPECTION    │  FGReturnQualityAction()
  │                                  │  Quality team inspects
  │                                  │  returned unit
  └──────────────────┬───────────────┘
                     │
             ┌───────┴────────┐
             │                │
             ▼                ▼
  ┌──────────────────┐   ┌────────────────────────┐
  │  ACCEPTED        │   │  SCRAPPED / REJECTED   │
  │  (Usable)        │   │  Write-off process     │
  └────────┬─────────┘   └────────────────────────┘
           │
           ▼
  ┌──────────────────────────────────┐
  │  RETURN BOM CREATED              │  BOMController
  │  ReturnBomCreation(itemId)       │  DispatchReturnBOMHead +
  │                                  │  DispatchReturnBOMLine
  └──────────────────┬───────────────┘
                     │  Lists sub-components to
                     │  disassemble from returned unit
                     ▼
  ┌──────────────────────────────────┐
  │  RETURN BOM STORE APPROVAL       │  ReturnBomApproval()
  │                                  │  Store head approves
  │                                  │  components to receive back
  └──────────────────┬───────────────┘
                     │
                     ▼
  ┌──────────────────────────────────┐
  │  SaveReturnBOM()                 │
  │  - DispatchReturn status updated │
  │  - Components returned to stock  │
  │  - StockTables qty incremented   │
  └──────────────────────────────────┘
```

---

### 8. FG Return — Point-by-Point Steps

1. Customer returns a dispatched DG set → return logged in `Production/DispatchReturn()` view
2. `DispatchReturns` record created with reference to original `DispatchDetails`, return reason, and return date
3. Quality team inspects the returned unit via `FGReturnQualityAction()`
4. If unit is unusable → scrap/write-off process (manual); workflow ends
5. If unit is acceptable → `FGReturnBOMCreation()` triggered to create a reverse BOM
6. Return BOM (`DispatchReturnBOMHead` + `DispatchReturnBOMLine`) lists all sub-components expected to be recovered from disassembly
7. Store head reviews and approves the return BOM via `ReturnBomApproval()`
8. `SaveReturnBOM()` executed → `DispatchReturns` status updated → recovered components added back to `StockTables`
9. FIFO lot records in `Stock_lotDetail` updated if applicable — recovered items re-enter the stock pool


---

## 9. Plant Asset Maintenance & Breakdown Management

```
┌─────────────────────────────────────────────────────────────────────┐
│        PLANT ASSET MAINTENANCE & BREAKDOWN MANAGEMENT              │
└─────────────────────────────────────────────────────────────────────┘

  ════════════ PREVENTIVE MAINTENANCE (SCHEDULED) ════════════

  ┌──────────────────────────────────┐
  │  MASTER DATA SETUP               │
  │  (One-time configuration)        │
  │                                  │
  │  MachineMaster → machines        │
  │  SubUnitMaster → sub-units       │
  │  TaskMaster → maintenance tasks  │
  │  FrequencyMaster → intervals     │
  │  AssetAllocationMaster →         │
  │    assigns tasks to machines     │
  └──────────────────┬───────────────┘
                     │
                     ▼
  ┌──────────────────────────────────┐
  │  HANGFIRE DAILY JOB (9 AM IST)  │  AssetMaintenanceCheck()
  │  Runs every morning              │
  └──────────────────┬───────────────┘
                     │  For each AllocationMaster record:
                     │  Calculate: LastDoneOn + Frequency(days)
                     │  If due date <= today:
                     ▼
  ┌──────────────────────────────────┐
  │  LOGBOOK ENTRY CREATED           │  Asset_MaintenanceLogbook +
  │  Automatically                   │  Asset_MaintenanceLogbookItems
  └──────────────────┬───────────────┘
                     │
                     ▼
  ┌──────────────────────────────────┐
  │  EMAIL NOTIFICATION SENT         │  Task-type-specific email
  │  To maintenance team             │  via RazorEngine template
  └──────────────────┬───────────────┘
                     │
                     ▼
  ┌──────────────────────────────────┐
  │  TECHNICIAN COMPLETES TASK       │  Marks logbook entry done
  │  PendingApproval() view          │  with remarks
  └──────────────────┬───────────────┘
                     │
                     ▼
  ┌──────────────────────────────────┐
  │  ADMIN APPROVAL                  │  PendingSAdminApproval()
  │  (ASSETADMIN / SUPERADMIN)       │  Final sign-off
  └──────────────────────────────────┘

  ════════════ BREAKDOWN MANAGEMENT ════════════

  ┌──────────────────────────────────┐
  │  MACHINE BREAKS DOWN             │
  └──────────────────┬───────────────┘
                     │  Staff reports via
                     ▼  BreakdownManagement view
  ┌──────────────────────────────────┐
  │  CHECK: Any open breakdown       │     ┌──────────────────────┐
  │  already exists for this machine?├─YES─▶  Block duplicate log │
  └──────────────────┬───────────────┘     │  Show existing record│
                     │ NO                  └──────────────────────┘
                     ▼
  ┌──────────────────────────────────┐
  │  BREAKDOWN LOGGED                │  Asset_BreakdownLog
  │  ReportBreakdown()               │  Status = DOWN
  │                                  │  Timestamp recorded
  └──────────────────┬───────────────┘
                     │
                     │  Timer starts — elapsed time visible
                     │  on live breakdown dashboard
                     ▼
  ┌──────────────────────────────────┐
  │  TECHNICIAN FIXES MACHINE        │  CompleteMaintenance(logId)
  │  Marks maintenance done          │  Status = MAINT_DONE
  │  + remarks entered               │  Timestamp recorded
  └──────────────────┬───────────────┘
                     │
                     ▼
  ┌──────────────────────────────────┐
  │  ADMIN FINAL AUTHORIZATION       │  FinalResolve(logId)
  │  (ASSETADMIN or SUPERADMIN only) │  Status = WORKING
  │  Verifies machine is operational │  Resolution timestamp recorded
  └──────────────────┬───────────────┘
                     │
                     ▼
  ┌──────────────────────────────────┐
  │  BREAKDOWN CLOSED                │
  │  Machine shows as WORKING        │
  │  Response time calculated        │
  │  KPI dashboard updated           │
  └──────────────────────────────────┘

  KPIs tracked: Machines DOWN count, WORKING count,
  Monthly downtime in hours, Average resolution time
```

---

### 9. Asset Maintenance — Point-by-Point Steps

**Preventive Maintenance:**
1. Admin sets up machine hierarchy: `MachineMaster` → `SubUnitMaster` → `TaskMaster` with `FrequencyMaster` (daily/weekly/monthly intervals)
2. `AssetAllocationMaster` assigns tasks to specific machines with responsible person and frequency
3. Hangfire job `AssetMaintenanceCheck()` runs daily at 9 AM IST
4. For each allocation, calculates: `LastMaintenanceDoneOn + FrequencyDays` — if due date is today or past → logbook entry created automatically
5. `Asset_MaintenanceLogbook` head + `Asset_MaintenanceLogbookItems` detail records created
6. Task-type-specific email sent to maintenance team (template from `Views/EmailManage/`)
7. Technician performs task, opens `PendingApproval()` view, marks logbook entry complete with remarks
8. Supervisor/Admin approves in `PendingSAdminApproval()` — final sign-off
9. Logbook history viewable in `MaintenanceReport()` with date-range filter

**Breakdown Management:**
1. Machine breakdown occurs → staff opens `Asset/BreakdownManagement` dashboard
2. `ReportBreakdown(machineId, subunitId, problem)` called — system first checks if an open breakdown already exists for this machine
3. If duplicate open log found → block new entry; show existing breakdown details
4. If no open log → new `Asset_BreakdownLog` created with `Status = DOWN` and current timestamp
5. Dashboard shows live breakdowns with elapsed time counter (`GetLiveBreakdowns()`)
6. KPI widget shows: total machines down, total working, monthly downtime hours (`GetBreakdownDashboardKPIs()`)
7. Maintenance technician fixes machine → calls `CompleteMaintenance(logId, remarks)` → `Status = MAINT_DONE`
8. ASSETADMIN or SUPERADMIN verifies machine is fully operational → calls `FinalResolve(logId, adminRemarks)` → `Status = WORKING`
9. Breakdown record closed; response time (DOWN to WORKING) calculated and stored
10. Historical breakdown report via `GetBreakdownHistoryData(from, to)` — shows all breakdowns, durations, and resolution times for any date range


---

## 10. Background Jobs — Hangfire Scheduler

```
┌─────────────────────────────────────────────────────────────────────┐
│               HANGFIRE BACKGROUND JOB SCHEDULER                    │
└─────────────────────────────────────────────────────────────────────┘

  Application Startup (OWIN / Startup.cs)
  ┌──────────────────────────────────────┐
  │  Hangfire initialized with           │
  │  SQL Server storage (ConnectionDatas │
  │  table holds connection string)      │
  └──────────────────┬───────────────────┘
                     │  5 recurring jobs registered
                     │
  ┌──────────────────▼────────────────────────────────────────────┐
  │                                                               │
  │  JOB 1: SendCombinedReminderEmail()                          │
  │  ┌────────────────────────────────────────────────────────┐  │
  │  │ Schedule: Every 3 hours, Mon–Sat, 10:00 AM – 8:00 PM  │  │
  │  │ Purpose:  Scan for BOM jobs pending Store Approval     │  │
  │  │           → Send reminder email to approvers           │  │
  │  │ Template: Views/EmailManage/StoreApprovalReminder.cshtml│ │
  │  └────────────────────────────────────────────────────────┘  │
  │                                                               │
  │  JOB 2: SendDailySaleReportDataForEmail()                    │
  │  ┌────────────────────────────────────────────────────────┐  │
  │  │ Schedule: Daily at 11:00 PM IST                        │  │
  │  │ Purpose:  Compile today's dispatch + sale data         │  │
  │  │           → Email daily sale summary to management     │  │
  │  │ Template: Views/EmailManage/DailySaleReport.cshtml     │  │
  │  └────────────────────────────────────────────────────────┘  │
  │                                                               │
  │  JOB 3: SendDailyMaterialRecieptReport() — Company 1         │
  │  ┌────────────────────────────────────────────────────────┐  │
  │  │ Schedule: Daily at 9:30 AM IST                         │  │
  │  │ Purpose:  Yesterday's MRN data for Company 1           │  │
  │  │           → Email to purchase/store team               │  │
  │  └────────────────────────────────────────────────────────┘  │
  │                                                               │
  │  JOB 4: SendDailyMaterialRecieptReport1() — Company 2        │
  │  ┌────────────────────────────────────────────────────────┐  │
  │  │ Schedule: Daily at 9:40 AM IST                         │  │
  │  │ Purpose:  Yesterday's MRN data for Company 2           │  │
  │  │           → Email to purchase/store team               │  │
  │  └────────────────────────────────────────────────────────┘  │
  │                                                               │
  │  JOB 5: AssetMaintenanceCheck()                              │
  │  ┌────────────────────────────────────────────────────────┐  │
  │  │ Schedule: Daily at 9:00 AM IST                         │  │
  │  │ Purpose:  Check all AssetAllocationMaster records      │  │
  │  │           Calculate: LastDoneOn + FrequencyDays        │  │
  │  │           If due today → create Logbook entry          │  │
  │  │           → Send task-specific email to tech team      │  │
  │  └────────────────────────────────────────────────────────┘  │
  └───────────────────────────────────────────────────────────────┘

  All jobs:
  - Enabled/disabled via AutoReminderEmails table flags
  - Email recipients from CMKL_Email table (by type)
  - SMTP settings from CMKL_Email_Setting table
  - HTML body rendered by RazorEngine from .cshtml templates
```

---

### 10. Background Jobs — Point-by-Point Steps

1. Application starts → OWIN `Startup.cs` initializes Hangfire with SQL Server storage
2. Hangfire connection string fetched from `ConnectionDatas` table at runtime
3. Five recurring cron jobs registered with IST-adjusted schedules
4. **Job 1 (Approval Reminder):** Every 3 hours on working days → queries `BOMVouchers` where `StoreApproved = 0` → compiles pending list → renders HTML via RazorEngine → sends email to store approval distribution group
5. **Job 2 (Daily Sale Report):** 11 PM daily → aggregates all `DispatchDetails` for current date → groups by product/KVA → renders sale summary HTML → emails to management distribution group
6. **Job 3 & 4 (MRN Reports):** 9:30 AM and 9:40 AM respectively for Company 1 and Company 2 → queries previous day's `IEPLStockIN_Head` records → renders MRN detail email → sends to purchase/store team
7. **Job 5 (Maintenance Check):** 9 AM daily → loops through all `Asset_AllocationMaster` records → computes `LastMaintenanceDoneOn + Frequency` → if due date ≤ today → creates `Asset_MaintenanceLogbook` entry → sends task-type-specific email to maintenance personnel
8. Each job checks `AutoReminderEmails` table flags before executing — jobs can be individually disabled without code changes
9. Email recipients are managed via `CMKL_Email` table grouped by type (BOMApproval, DailySale, MRN, Maintenance, etc.)
10. SMTP configuration (host, port, username, password) read from `CMKL_Email_Setting` table at send time


---

## 11. User Access & Permission Management

```
┌─────────────────────────────────────────────────────────────────────┐
│              USER ACCESS & PERMISSION MANAGEMENT                    │
└─────────────────────────────────────────────────────────────────────┘

  ┌──────────────────────────────────┐
  │  SUPERADMIN / USERSUPER opens    │
  │  UserAccess/UserAccessView()     │
  └──────────────────┬───────────────┘
                     │
                     ▼
  ┌──────────────────────────────────┐
  │  Select User from dropdown       │
  │  Select Menu Group (MainMenu)    │
  └──────────────────┬───────────────┘
                     │  AJAX call
                     ▼
  ┌──────────────────────────────────┐
  │  GetUserPermissions(userId,      │
  │  mainMenu)                       │
  │  → Returns all User_Menu items   │
  │    in that group with            │
  │    current Access status         │
  └──────────────────┬───────────────┘
                     │
                     ▼
  ┌──────────────────────────────────┐
  │  Permission Matrix displayed     │
  │  Each menu item shows:           │
  │  [✓] or [ ] toggle              │
  └──────────────────┬───────────────┘
                     │  Admin clicks toggle
                     ▼
  ┌──────────────────────────────────┐
  │  TogglePermission(userId,        │
  │  menuName, isChecked)            │
  │  → UPDATE User_Access SET        │
  │    Status = isChecked            │
  │    WHERE UserID = userId AND     │
  │    MenuAccess = menuName         │
  └──────────────────────────────────┘

  ──── HOW ACCESS IS CHECKED ON EVERY PAGE ────

  Controller Action Called
        │
        ▼
  Query User_Access:
  WHERE UserID = Session["User_ID"]
  AND MenuAccess = "PageName"
        │
    ┌───┴───┐
    │       │
    ▼       ▼
  Status  Status
  = true  = false
    │       │
    ▼       ▼
  Proceed  Return
  normally "Access Denied"
           or redirect
```

---

### 11. User Access — Point-by-Point Steps

1. Super Admin opens `UserAccess/UserAccessView()` — accessible only to `SUPERADMIN` and `USERSUPER` roles
2. Admin selects a user from the user dropdown
3. Admin selects a menu group (MainMenu category, e.g., "BOM", "Purchase", "Admin")
4. `GetUserPermissions(userId, mainMenu)` called via AJAX → fetches all `User_Menu` items in that group along with current `User_Access.Status` for the selected user
5. Matrix of checkboxes displayed — checked = access granted, unchecked = denied
6. Admin toggles any checkbox → `TogglePermission(userId, menuName, isChecked)` called → `User_Access` record updated immediately
7. Changes take effect on the user's next request — no app restart needed
8. On every protected controller action → system queries `User_Access` → if `Status = false` → request denied
9. Role-based overrides: `SUPERADMIN`, `ADMIN`, `ASSETADMIN` bypass certain individual page checks within their domain
10. New users added to `tbl_User_Master` must have permissions explicitly granted via this screen before accessing any module


---

## 12. Complete System Master Workflow

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                    CMKL ERP — COMPLETE SYSTEM MASTER WORKFLOW                  │
│                         IEC Gensets DG Set Manufacturing                        │
└─────────────────────────────────────────────────────────────────────────────────┘

  ┌─────────────┐
  │  USER LOGIN │ ──── Session & permissions set
  └──────┬──────┘
         │
         ├──────────────────────────────────────────────────────────────────┐
         │                                                                  │
         ▼                                                                  ▼
  ┌─────────────────┐                                            ┌─────────────────┐
  │  MASTER SETUP   │                                            │  USER ACCESS    │
  │  (Admin)        │                                            │  MANAGEMENT     │
  │                 │                                            │                 │
  │ • Item Master   │                                            │ • Add users     │
  │ • Supplier      │                                            │ • Set role      │
  │ • Department    │                                            │ • Toggle per-   │
  │ • Tax/GST rates │                                            │   page access   │
  │ • KVA ratings   │                                            └─────────────────┘
  │ • Engine models │
  └────────┬────────┘
           │  One-time / periodic setup
           │
  ═══════════════════════════════════════════════════════════
           │
           ▼
  ┌─────────────────────────────────────┐
  │         PURCHASE CYCLE              │
  │                                     │
  │  Low stock / BOM demand detected    │
  │           ↓                         │
  │  INDENT CREATED                     │
  │           ↓                         │
  │  Indent Approved (Plant Head)       │
  │           ↓                         │
  │  PURCHASE ORDER RAISED              │
  │           ↓                         │
  │  PO 2-level approval (PH + SAdmin)  │
  │           ↓                         │
  │  Goods received → GATE ENTRY / MRN  │
  │           ↓                         │
  │  QUALITY INSPECTION                 │
  │           ↓                         │
  │  MRN 4-stage approval               │
  │  (Store→Quality→PlantHead→Accounts) │
  │           ↓                         │
  │  STOCK UPDATED                      │
  │  (StockTables + FIFO lots)          │
  └────────────────┬────────────────────┘
                   │ Stock available
                   │
  ═══════════════════════════════════════════════════════════
                   │
                   ▼
  ┌─────────────────────────────────────┐
  │         SALES CYCLE                 │
  │                                     │
  │  ENQUIRY CREATED (OAStage 0)        │
  │           ↓                         │
  │  Price quoted → Customer confirms   │
  │  ORDER ACCEPTED (OAStage 1)         │
  │           ↓                         │
  │  DRAWINGS UPLOADED (OAStage 2)      │
  │           ↓                         │
  │  DRAWINGS APPROVED (OAStage 3)      │
  │           ↓                         │
  │  Production scheduled               │
  │  PENDING PRODUCTION (OAStage 4)     │
  └────────────────┬────────────────────┘
                   │
  ═══════════════════════════════════════════════════════════
                   │
                   ▼
  ┌─────────────────────────────────────┐
  │         BOM & PRODUCTION            │
  │                                     │
  │  BOM JOB CREATED                    │
  │  (BOMVouchers + lines)              │
  │           ↓                         │
  │  FIFO LOT ALLOCATED for raw items   │
  │           ↓                         │
  │  STORE APPROVAL                     │
  │           ↓                         │
  │  MANAGEMENT APPROVAL                │
  │           ↓                         │
  │  IN PRODUCTION (OAStage 5)          │
  │  Materials issued from store        │
  │           ↓                         │
  │  PRODUCTION UPDATE                  │
  │  (Engine + Alternator details)      │
  └────────────────┬────────────────────┘
                   │
  ═══════════════════════════════════════════════════════════
                   │
                   ▼
  ┌─────────────────────────────────────┐
  │         TESTING & PDI               │
  │                                     │
  │  EWAP END-OF-LINE TESTING           │
  │  (BOMEwapDetails)                   │
  │           ↓                         │
  │  QUALITY CHECK (OAStage 6)          │
  │           ↓                         │
  │  PDI (Pre-Delivery Inspection)      │
  │  UpdatePDIForm() → 4 tables         │
  │           ↓                         │
  │  PDI PASS → PDIStatus = 1           │
  │           ↓                         │
  │  TEST CERTIFICATE generated (PDF)   │
  │           ↓                         │
  │  PACKING SLIP generated             │
  │           ↓                         │
  │  READY TO DISPATCH (OAStage 7)      │
  └────────────────┬────────────────────┘
                   │
  ═══════════════════════════════════════════════════════════
                   │
                   ▼
  ┌─────────────────────────────────────┐
  │         DISPATCH                    │
  │                                     │
  │  PDI gate-check confirmed           │
  │           ↓                         │
  │  DISPATCH DETAILS entered           │
  │  (Invoice, LR, Transport)           │
  │           ↓                         │
  │  DISPATCHED (OAStage 8)             │
  │           ↓                         │
  │  QR CODE generated for customer     │
  └────────────────┬────────────────────┘
                   │
  ═══════════════════════════════════════════════════════════
                   │
                   ▼
  ┌─────────────────────────────────────┐
  │         CUSTOMER PORTAL             │
  │                                     │
  │  Customer scans QR code             │
  │           ↓                         │
  │  GuestLogin → QRLogin session set   │
  │           ↓                         │
  │  View Test Certificate (PDF)        │
  │           ↓                         │
  │  Submit 8-parameter FEEDBACK        │
  │           ↓                         │
  │  Confirmation email sent            │
  └─────────────────────────────────────┘

  ═══════ PARALLEL WORKFLOWS ═══════

  ┌──────────────────────┐    ┌──────────────────────┐
  │  ASSET MAINTENANCE   │    │  QUALITY REJECTION   │
  │                      │    │                      │
  │ Preventive:          │    │ Inward rejection:    │
  │  Hangfire 9AM check  │    │  LR Voucher created  │
  │  → Logbook created   │    │  → Qty validated     │
  │  → Email sent        │    │  → LR approved       │
  │  → Task completed    │    │  → Stock deducted    │
  │  → 2-level approval  │    │                      │
  │                      │    │ FG Return:           │
  │ Breakdown:           │    │  Return logged       │
  │  DOWN→MAINT→WORKING  │    │  → Quality check     │
  │  3-stage resolution  │    │  → Return BOM        │
  │  KPI dashboard live  │    │  → Stock recovered   │
  └──────────────────────┘    └──────────────────────┘

  ┌──────────────────────┐    ┌──────────────────────┐
  │  BACKGROUND EMAILS   │    │  REPORTING           │
  │                      │    │                      │
  │  Approval reminders  │    │  Sale Report         │
  │  Daily sale report   │    │  Dispatch Report     │
  │  MRN reports         │    │  MRN Register        │
  │  Maintenance alerts  │    │  FIFO Lot Report     │
  │                      │    │  Item Consumption    │
  │  (Hangfire cron jobs)│    │  Stock Movement      │
  └──────────────────────┘    │  Purchase Report     │
                              │  Quality Rejection   │
                              └──────────────────────┘
```

---

## Complete System — Point-by-Point Master Steps

### PHASE 1 — SYSTEM SETUP (Admin)
1. Company records created in `Companies` table; financial years defined in `FinYears`
2. User accounts created in `tbl_User_Master` with role assignment
3. User permissions configured per page via `UserAccess/UserAccessView()`
4. Item masters created: raw materials, sub-assemblies, and FG items in `BOMItemMasters`
5. Supplier master data entered in `SupplierMasters`
6. Department master set up; production departments flagged with `isBOMStage = true`
7. KVA ratings, engine models, alternator specs configured in respective master tables
8. GST/tax rates entered in `TaxMasters`
9. Email distribution groups configured in `CMKL_Email`; SMTP settings in `CMKL_Email_Setting`
10. Asset machines, sub-units, task types, frequencies, and allocations set up for maintenance

### PHASE 2 — PURCHASE CYCLE
11. Store identifies low stock via dashboard alert (`GetLowStockItems()`) or BOM material demand
12. Purchase indent created with required items, quantities, and expected delivery dates
13. Indent voucher number auto-assigned from `Bill_Series` (Company + FY scoped)
14. System displays last purchase price and 3-month average consumption for each item
15. Indent submitted → Plant Head reviews and approves
16. Approved indent visible for PO creation; Purchase Order raised with supplier terms
17. PO undergoes 2-level approval: Plant Head → Super Admin
18. Approved PO printed (Rotativa PDF) and sent to supplier
19. Supplier delivers goods → Gate Entry created → MRN number assigned
20. Partial deliveries allowed; system tracks received vs required quantities
21. Quality team inspects each line item → approved/rejected flag set per item
22. Rejected items trigger Line Rejection workflow; accepted qty proceeds
23. MRN progresses through 4-stage approval: Store → Quality → Plant Head → Accounts
24. After all 4 approvals → stock quantities updated in `StockTables`
25. FIFO-enabled items create `Stock_lotDetail` records with serial numbers and lot quantities
26. System auto-closes indent if all lines are fully received

### PHASE 3 — SALES ORDER
27. Sales team creates customer enquiry in `CMKL_Enquiry` with product specs, KVA rating, quantity, pricing — OAStage 0
28. Enquiry line items added per required product model
29. Sales team prepares quotation and shares with customer
30. Customer confirms order → OAStage advances to 1 (Pending Order Acceptance)
31. If rejected → `ReverseEnquiry()` rolls back stage; auto-email notification sent
32. Technical team uploads drawings (GA, Layout, Foundation) → files saved to UNC network share
33. Customer approves drawings → OAStage moves to 2, then 3 (Drawing Approval)
34. Large orders may be split via `OrderDetail(split=true)` → line items redistributed via `MoveItems()`
35. OAStage moved to 4 (Pending Production) → BOM creation triggered

### PHASE 4 — BOM CREATION & APPROVAL
36. BOM job created in `BOMController.BOM()` linked to the enquiry; FG item selected
37. Standard BOM template auto-populates required raw materials as job lines
38. For FIFO items: `Stock_lotDetail` queried by ID ascending; oldest lots allocated first
39. `BOMVoucherlines.LotID` set for each allocated lot; stock temporarily reserved
40. Items with insufficient stock flagged; purchase indent triggered for shortfall
41. BOM submitted for Store Approval → Hangfire sends reminder every 3 hours if pending
42. Store Manager approves → `BOMVouchers.StoreApproved = 1`
43. Management approves → `BOMVouchers.MgmtApproved = 1`
44. Store physically issues materials to production floor → `IEPLStockIssueHead` records created
45. OAStage updated to 5 (In Production)

### PHASE 5 — PRODUCTION
46. Production team opens `PendingProductionView` → selects BOM job
47. Engine and alternator assigned from FIFO lots; serial numbers confirmed via `GetSerialNumbers()`
48. `Bom_ProductionUpdate` filled: engine make/model/serial, alternator make/model/serial, KVA, phase
49. Assembly progresses through configured production stages
50. Any mid-production material shortage → Requisition (`BOMRequisitionHead`) raised → store issues additional material

### PHASE 6 — QUALITY TESTING
51. After physical assembly, job enters EWAP queue (`PendingEwap()`)
52. EWAP technician runs electrical/mechanical end-of-line tests → results logged in `BOMEwapDetails`
53. `BOM_TestingUpdate` completed: battery type, control panel, KRM number, fuel tank capacity, final KVA rating
54. OAStage moves to 6 (Quality Check)
55. Job enters PDI queue (`PendingPDI()`)
56. PDI inspector does complete walkaround inspection of finished genset
57. `UpdatePDIForm()` submitted → simultaneously updates 4 tables with all test parameters
58. PDI fail → notes added; unit returned to production; must be re-inspected
59. PDI pass → `BOMEwapDetails.PDIStatus = 1`; job moves to `CompletedPDI()`
60. Test Certificate (full PDI report) printable as PDF via Rotativa

### PHASE 7 — DISPATCH
61. OAStage moves to 7 (Ready to Dispatch)
62. Packing slip generated; unit physically packed and tagged
63. Billing team opens `Dispatch()` view → `BillingPDIDetails()` gate-checks PDIStatus before allowing entry
64. Dispatch details entered: invoice number, date, amount, transporter, LR number, vehicle number
65. `DispatchDetails` record saved → OAStage updated to 8 (Dispatched)
66. QR login credentials created in `CMKL_QRLogin`; QR code generated and physically attached to unit

### PHASE 8 — CUSTOMER PORTAL & FEEDBACK
67. Customer receives genset; scans QR code with smartphone
68. QR opens portal URL → `QRLogin()` authenticates → customer session established
69. Customer can download/view Test Certificate PDF
70. Customer opens feedback form → submits 8-parameter satisfaction rating
71. `CMKL_Feedback` record saved → confirmation email sent automatically to customer

### PHASE 9 — ONGOING OPERATIONS
72. Low stock continuously monitored via dashboard → repeat purchase cycle as needed
73. Line rejections raised if production finds defective incoming material
74. FG returns handled via `DispatchReturn` → Quality inspection → Return BOM → stock recovered
75. Preventive maintenance auto-scheduled daily at 9 AM by Hangfire; emails sent to maintenance team
76. Machine breakdowns logged → 3-stage resolution (DOWN → MAINT_DONE → WORKING)
77. Background email jobs run automatically: approvals reminder (3-hourly), daily sale report (11 PM), MRN reports (9:30 & 9:40 AM)
78. Management reviews dashboards: KVA dispatch trends, engine model analysis, stock movement, quality rejection rates
79. Admin can generate reports: sale, dispatch, purchase, MRN register, FIFO lot traceability, item consumption, BOM list
80. Financial year rollover: new FY created in `FinYears`; `Bill_Series` sequences start fresh; opening stocks entered via `OpeningStocks`

---

*Document generated: June 2026 — CMKL ERP Complete Workflow Reference*
