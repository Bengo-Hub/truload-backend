# Appendix

# Key Process Flows

## A. 1 – Weighing Process

| Service | Weighing Process |
| :---- | :---- |
| Where is the service initiated? | Frontend (PWA) \- Weighing screen / Station dashboard |
| Does the service have a Portal Transaction? | No (transactions happen via backend APIs; PWA supports offline queuing and later sync) |
| Service Description | Overloaded or suspected vehicles are stopped, weighed (mobile), evaluated againstTraffic rules and permits. Compliant vehicles get a weight ticket or special release Non-compliant trucks are prohibited, sent to the yard and enter prosecution flows. |
| Who can perform this process? | KURA Field Officers with weighing module access & assigned shift |
| Actors | Field Officer (user) Truck driver TruConnect middleware (node/electron), ANPR, Booms & camera systems (if integrated). |
| Pre-conditions (front end) | Officer logged in (or PWA has offline credentials cached). PWA service-worker registered and IndexedDB initialized (persistent storage requested). TruConnect running and connected to attached scale(s) (serial/ethernet/RF). Scale calibration certificate valid and scale test completed for the day. Permissions: officer role authorizes weighing/prosecution actions. |
| Documents | Weight Ticket Permit Document Special release certificate(in case of open previous manual tag) |
|  Post-conditions | Weight Ticket generated for compliant vehicles. Prohibition Order generated and case queued for prosecution for non-compliant vehicles. Invoice generated when applicable and queued for eCitizen submission/payment. Local IndexedDB updated with synced statuses; server stores final record and audit logs. |
| Trigger (if any) | A field officer flags down a vehicle suspected of being overloaded or violating traffic rules. UI: The officer selects “Weighing” on the PWA. The PWA detects whether the device is online or offline Sync/Submission to Backend (Online) / Deferred Submission (Offline) If device is online: Frontend calls backend API(s) to submit final weight & actions. The backend responds with a new record created. Frontend updates IndexedDB: local cached is removed, mark “synced \= true”. If device is offline: frontend leaves records flagged queued; a sync engine monitors connectivity (or allows users to manually “Sync Now”). Once the network returns, the engine picks up all queued items in logical dependency order (weight record → action forms → and so on). UI: Show “Sync in progress / Success / Failed – retry” messages. For each submission, handle errors: e.g., duplicate submission, validation error, server offline. The UI should provide an error queue list for the officer to check. NOTE: The backend will use UUIDs for the object ids or primary keys to be consistent with frontend IndexedDB cached offline data and avoid mapping IDs |
| Controls | Role-based access control (RBAC) for weighing and prosecution actions. Validation: scale test done, calibration certificate valid, required fields filled before finalise. Business rules engine enforces Act/permit limits and tolerance. Audit logging for every action (officer id, timestamps). Idempotency tokens on API calls to prevent duplicate records. Persistent local storage with tamper-evident metadata (clientSignature, timestamps). |
| Basic Flow (TruConnect) | Start middleware(added to startup apps and capable of running in the background) on client (node/electron). Middleware connects to scale(s) and exposes a local proxy API. Streams live weight and device health (battery, temp, connectivity) as JSON to frontend. On initial axle readings, middleware posts an auto-weigh event to the backend (when online) for audit. Supports both mobile axle-by-axle output and multideck JSON shapes; middleware normalizes payloads.  |
| Basic Flow (Frontend) | Initiate Weighing Session The officer navigates to the weighing screen after logging into the system. The scale connection metrics ia automatically shown on the weighing screen, if connected, shows green with scale info and if not connected, shows red with a button to connect to scales which shows “Scales Off” or “Could Not connect to scales” and advices user to power on the scales if off before weighing. If a scale test has been done for the day, the user is allowed to start weighing.  Initial Weight Capture  The weighing process has several process screens as discussed:  Vehicle Details Screen Vehicle registration/Number Plate(auto-captured by ANPR cameras if cameras integrated). Note that if the vehicle is a relief truck being weighed to relieve another truck which had overloaded and needs to offload the extra weight for compliance to be achieved before its cleared, the truck to relieve the number plate is linked on this screen. Auto-pull Vehicle Owner, Vehicle Make and Model from vehicle search(triggered if stable internet is detected and search api is up) Axle Configuration(auto-detected for consecutive weighing) Front View & Overview images of the vehicle if cameras integrated and healthy Weight Capture Screen The weight is captured axle by axle depending on the axle configuration set in screen A, if 2A/2\*/3A and so on, the assign weight button appears to assign weight for each weighted axle until the last one then the limits are checked based on the axle limits, GVW is calculated and populated, limits are checked and any excess is shown next to each axle or GWV value. The ACT or permit applied is checked for compliance and any allowances allowed, if violations are detected, the vehicle is sent to yard on the next screen, screen C. Prohibition order is auto-generated in the background when weight is taken and on proceeding to screen C, the option to send vehicle to yard is auto-selected. If the vehicle overloaded within the allowable tolerance limits(if GVW tolerance is active), the system auto-special releases the truck and on screen C, the vehicle is exited. A weight ticket is generated automatically at the end of each weighing session. Other details captured on this screen are: Permit \- if the vehicle has a permit, pull the permit details and apply the permit limits during compliance check. Note that the Traffic Act tolerance does not apply for vehicle with permits Transporter(auto-detected for consecutive weighing or add button appears that opens the add transporter modal to add a new transporter where Name and Address are captured).  Driver(auto-detected). For new entry, the add button opens a modal where the driver details(ID / Passport No, Driving License No,Full Names, Surname, Gender, Nationality, Age, Address) are captured Cargo(auto-detected). The add button opens up a modal where the cargo name is captured e.g Mollases. Origin(auto-detected). The add button opens up a modal where the origin name is captured e.g Nairobi. Destination(auto-detected). The add button opens up a modal where the origin name is captured e.g Nakuru. Route(auto-detected or added e.g Nairobi-Nakuru on S12) Exit/send vehicle to yard screen On this screen, compliant vehicles or overloaded within permit or tolerance limits are exited from the system Non-compliant vehicles are sent to yard(the action triggers case register entry) to undergo prosecution process If a vehicle has an open tag from external systems like KeNHA or from within KURA, the tag pop-up is shown on this screen. If the tag is an automatic tag and does not necessarily bar the vehicle from exiting the system, the weighing officer can special release the vehicle with a  reason(triggers case register entry and auto-close the case with valid special release reason). If the tag is open and is a manual tag and can bar the vehicle from exiting the system, the vehicle is sent to yard(trigger case register entry) even if it is compliant with the weight limits |
| Basic Flow(Backend) | Backend accepts client UUIDs and uses GUIDs as primary keys (or stores clientUuid column) to keep consistency with frontend IndexedDB. Server validates weights against current Act/permit rules, computes fines (Traffic logic, given in Ksh, no currency conversion needed), converts currency using authoritative FX rate if applicable, and returns computed amounts \+ serverIds. Backend generates authoritative documents (Traffic Act certificate, invoice, prohibition order) and stores media/evidence (images, ANPR snapshots). Records are stored in Postgres; Redis/RabbitMQ used for asynchronous tasks (e.g., eCitizen submission), retry queues and notifications. All operations are audit-logged with officer id and timestamps; APIs are idempotent (idempotency-key accepted). |
| Printable outputs | Weight Ticket, Prohibition Order,Traffic Act Certificate, Special Release Certificate, Invoice, Receipt, Load Correction Memo, Compliance Certificate. |
| Fees/Charges | N/A |
| Exceptions & Error Handling | Scale disconnect: show warning, allow retry or abort; abort logs session as aborted. Sync failure: keep local queued, exponential backoff; surface errors in UI and Sync Log.  Storage full or persistent permission denied: warn users and refuse new sessions until space is freed. Duplicate ANPR or duplicate submissions: idempotency prevents duplicate server records; show dedupe message. Business rule conflicts (e.g., permit status changed server-side): create conflict log and require manual resolution. |
| Audit & Logging | Full audit trail for each weight/action (who, what, when, offline/online flag). Sync logs record each attempt and server response. Evidence attachments (photos, ANPR) stored with checksum and linked to the record. |
| Security | TLS for all network calls JWT \+ RBAC for auth Encryption at rest for attachments Least privilege for APIs Audit retention policy. |
| Data Requirements | WeighbridgeTransaction, Vehicle, Driver, Transporter, AxleWeights, Permit(optional), ProhibitionOrder, Invoice, PaymentStatus, SpecialRelease, AuditLog, MediaAttachment. Each entity includes clientLocalId: UUID, serverId: UUID, status, timestamps, and syncMetadata. |
| New Process Requirements  | Implement persistent IndexedDB (Dexie.js) \+ persistent storage API. Use client-generated UUIDs to avoid ID mapping problems. Implement sync engine (parents-first sync; batched commits). Background sync via service worker if supported; manual sync button fallback. Idempotency keys for all create operations. Reweigh flow up to 8 cycles(custom setting, mutable) with full audit trail.  |
| Third Party Integrations | ANPR & Camera system KeNHA Tags API KeNHA Permit API NTSA APIs(vehicle search) eCitizen payment gateway (invoice submission & status) Media/file storage (cloud blob) GPS/location service |

## 

1. ## 1.1 Weighing Business Process Model  and Notation(BPMN)

   ## 

## A. 2 – Tags process

| Service | Tags Process |
| :---- | :---- |
| Where is the service initiated? | Frontend (PWA) \- Tags Screen |
| Does the service have a Portal Transaction? | Yes. External tags systems like KeNHA |
| Service Description | This covers the process from detection of a violation at/around a weighbridge station, through tagging (creating a violation record), notification, enforcement, and resolution. Applies to all heavy vehicles/trucks required to use a weighbridge under Kenyan law/regulations Excludes regular light vehicles not subject to weighbridge control. |
| Who can perform this process? | KURA Field Officers with weighing module access & assigned shift |
| Actors | Officer (Frontend User) System (Backend/Automated Processes) Administrator  External Systems (KeNHA) |
| Pre-conditions (front end) | The officer must be **logged in** to the portal. Vehicles must **exist in the registry** (verified from NTSA API). Officers must have **permission** to assign tags. GPS/location must be **active** for geotagging. |
| Documents | Tag Document |
|  Post-conditions | Vehicle tagged with violation record. Tag entry logged in audit trail. Notification sent to relevant systems and stakeholders. Tag triggers potential enforcement workflow (e.g., penalty issuance, case review). |
| Trigger (if any) | Manual: Officer selects a vehicle → clicks “Tag Violation”. Automated: Camera/ANPR detects bypass → backend auto-tags. |
| Controls | RBAC (Role-Based Access Control) for tagging privileges. Duplicate prevention – cannot tag the same violation twice. Mandatory tagging for detected violations. Naming & classification rules enforced. |
| Basic Flow (Frontend) | Officer logs in. System loads assigned weighbridge session & vehicles. Officer identifies violation (manual or from system alert). Officer selects **“Add Violation Tag.”** The officer selects the violation type (Bypass, Overload, etc.). Adds notes, attachments (photos, docs). Confirms and submits. The system confirms success or shows error. |
| Basic Flow(Backend) | Receive Tag Request from frontend. Validate: Officer permissions. Vehicle registration (via NTSA API). Tag duplication rules. Create a Tag Record with a unique UUID. Store Metadata: Officer ID, station, location, timestamp, media URLs. Sync with External APIs: KeNHA Tag Registry. eCitizen for invoice creation (if applicable). Log Activity in audit trail. Return Response (success/failure) to frontend. |
| Printable outputs | Violation Tag Report (per vehicle or batch). Audit Log Summary. Enforcement Dashboard Analytics. |
| Fees/Charges | N/A for tagging. Applicable penalties or reweigh fees charged via eCitizen (based on tag outcome). |
| Exceptions & Error Handling | Tag not found → prompt user to select/create valid tag. Permission denied → display access error. Duplicate tag assignment → prevent duplication, alert user. System/database error → retry mechanism or error notification. |
| Audit & Logging | Log each tagging action: User ID, Timestamp, Item ID, Tag ID, Action (create/update/delete). Maintain history for compliance, tracking, and reporting.  |
| Security | RBAC: restrict who can assign or create tags. Input validation to prevent injection attacks. Secure HTTPS communication between frontend and backend. |
| Data Requirements | Tag master table (Tag ID, Name, Description, Created By, Creation Date). Item-tag mapping table. Audit trail table with timestamps and actions. |
| Third Party Integrations | ANPR & Camera system KeNHA Tags API NTSA APIs(vehicle search) Media/file storage (cloud blob) GPS/location service |

### 

2. ## 2.1 Tags Business Process Model  and Notation(BP

## A. 3 – Scale Test

| Service | Scale Calibration & Weighing Readiness Process |
| :---- | :---- |
| Where is the service initiated? | Frontend (PWA) \- Scale Test  Screen |
| Does the service have a Portal Transaction? | Yes. External tags systems like KeNHA |
| Service Description | This process verifies the **accuracy, stability, and linearity** of the weighing instrument. Includes **zero test**, **span test**, and **linearity test** using certified test weights. |
| Who can perform this process? | KURA Field Officers with weighing module access & assigned shift |
| Actors | Officer (Frontend User) System (Backend/Automated Processes) Administrator  External Systems (KeNHA) |
| Pre-conditions (front end) | The officer must be **logged in** to the portal. The scale platform is clean and empty. Environment stable (no wind, vibration, or temperature fluctuation). Certified test weights are available and verified. The system is in “calibration mode.” |
| Documents | Calibration procedure SOP. Test weight certification records |
|  Post-conditions | Scale is verified and marked **“Calibrated — Ready for Use.”** Calibration data logged and saved. |
| Trigger (if any) | Before each new weighing batch, After scale relocation, After maintenance, or scheduled calibration intervals (daily, weekly, monthly). |
| Controls | Use of standard certified test weights. Calibration performed by authorized personnel only |
| Basic Flow (Frontend) | The officer logs  into the calibration system. Selects “Calibrate Scale.” System prompts to perform **Zero Test** — ensure no load  Use Truck / Vehicle  known — system displays reading. Compare displayed weight vs. actual weight. Adjust if deviation exceeds tolerance. Repeat for different weights (linearity test). Save results and print calibration report. |
| Basic Flow(Backend) | Records calibration readings. Computes deviation and error percentages. Flags any out-of-tolerance results. Updates calibration status in database. Enables or disables weighing function based on outcome. |
| Printable outputs | Calibration certificate or report. Test readings summary (zero, span, linearity). Timestamp, operator name, and approval signature. |
| Fees/Charges | N/A for Scale Test. |
| Exceptions & Error Handling | Scale not stable: Retry after stabilizing environment. Deviation exceeds tolerance: Adjust calibration or report fault. Sensor or load cell failure: Escalate to maintenance team. Portal connection lost: Store readings locally and sync later. |
| Audit & Logging | Automatic record of calibration date/time, operator ID, and device ID. All changes logged for traceability. |
| Security | Only authorized KURA Officers can initiate or approve calibration. Digital signatures for report approval. Data encrypted and backed up. |
| Data Requirements | Certified test weight data (ID, nominal value, tolerance). Scale ID, location, calibration frequency. Measurement readings and deviations. Audit trail table with timestamps and actions. |
| Third Party Integrations | ANPR & Camera system KeNHA Tags API |

### 

## 

## B. 1 – Case Register/Special Release processes

| Service | Case Register(Weighing(Prohibition)  → Case register) |
| :---- | :---- |
| Where is the service initiated? | Frontend (Weighing → auto-create Case Register entry OR manual entry into Case Register by officer) |
| Does the service have a Portal Transaction? | No. |
| Service Description | Capture initial case details (Subfile A) for every violation. Provide fast-path dispositions for offenders for special releases (redistribution(\>200≤500 OR ≤ 1500 and redistributable/load correction/ OR ≤200 GVW overloads) or pay & exit which is handled by the prosecution module. If matter proceeds to court, the Case Register packages required subfiles and escalates to Case Manager. |
| Who can perform this process? | Weighing Officer Prosecution Officer (for register entry),  Supervisor (for manual release approvals). |
| Actors | Field Officer Truck Driver Owner/Agent Supervisor. |
| Pre-conditions | Weighing session and Prohibition Order (if any) already created or officer manually creates a Case Register entry.  Minimal evidence (weigh log) attached to Subfile A.  The officer has permission to escalate to prosecution mobile for payment processing or request special/manual release. |
| Documents | Case Register (Subfile A) Notice to Attend Court (NTAC) Load Correction Memo Compliance Certificate Special Release Certificate |
| Post-conditions | If offender chooses to pay:case is escalated to prosecution module for  payment processes handling If reweigh is within limits → Compliance Certificate generated and case automatically CLOSED in Case Register with disposition, Special release, redistributed and compliance achieved. If the offender chooses court: case remains Pending(Escalate), package prepared for Case Manager.  If redistribution needed: conditional Load Correction Memo created followed by compliance certificate if reweigh is within limits. |
| Trigger | Automatic: Weighing → overload → system auto-creates Case Register (Subfile A) and flags Pending.  Manual: Officer creates a Case Register entry (Subfile A) for offences observed without weighing.  If a tag from KeNHA or other system indicates a block, the register captures tag details on Subfile A. |
| Controls | Manual special release requires authorized supervisor signature and justification; logged in Subfile I / Subfile J as appropriate.  All quick dispositions must attach minimal evidence: weigh logs, photos. |
| Basic Flow (Frontend) | 1\.  System pre-fills case from Prohibition Order and Weighing Data. Officer verifies/edits/captures vehicle, driver, owner, prohibition id, location, time, officer.  2\. Present three clear paths: Special Release, Pay Now(Handled by prosecution module) or Settle in Court(Case Manager).  **Pay Now**: Push to Prosecution Module(compute charges, raise Traffic Act Charge Sheet, Generate invoice (eCitizen Integration). On payment confirmation attach receipt → generate Load Correction Memo → schedule reweigh → Generate Compliance certificate.  **Court Process:** Escalate to Court: If Settle in Court, to Case Manager, packing required subfiles. **Special Release:** Request admin authorization → Create conditional Load Correction Memo → Redistribute & schedule reweigh(optional, for redistributable load only) → Compliance certificate(optional, if redistributed) →  Special release certificate 4\. Update case status & show required checklist for finalisation. |
| Basic Flow (Backend) | Case Register serves as a single source of truth for Subfile A. Only escalates to Case Manager after Subfile A validated and evidence minimum is present.  Payment callbacks from eCitizen update case and trigger load correction / reweigh workflows via background jobs in Prosecution Module. |
| Printable outputs | Load Correction Memo Compliance Certificate Special release certificate |
| Third Party Integrations | SMS/Email notification. |
| Exceptions & Error Handling | Reweigh fails (still overloaded): update case to Sent to Yard / Escalate.  Supervisor unavailable for manual release: queue approval task; block exit. |
| Audit & Logging | Every closing action (payment, compliance certificate, manual release) is logged to Subfile J (Minute sheet & correspondences) with authorizer, timestamps, and attached receipts. |
| Security | Ensure receipts and payment details are stored securely and associated only to the case id; supervisor approval requires multi-factor (if policy requires). |

## 

## B. 1.1 – Case Register Business Process Model  and Notation(BPMN)

## B. 2 – Prosecution Process

| Service | Prosecution(Case Register → Prosecution) |
| :---- | :---- |
| Where is the service initiated? | Frontend(User escalates case from case register to prosecution module) |
| Does the service have a Portal Transaction? | Yes — Cases may generate portal transactions (eCitizen invoices/payments) via backend integrations. |
| Service Description | Create a prosecution event from a case register entry. Raise Charge Sheet → Invoice(eCitizen) → Receipt → Load Correction memo → Compliance Certificate(Trigger case closure in case register and link payment receipt, with result, charged and paid) |
| Who can perform this process? | Prosecution Officers Enforcement Inspectors Authorized Admins. |
| Actors | Officer (prosecution) Prosecutor Defendant/driver/owner eCitizen/Road Authority portals. |
| Pre-conditions | A Prohibition Order (PO) exists for the vehicle.  A Case Register entry exists (Subfile A created) with Prohibition Order or weighing data linked. Officer credentials & RBAC permit prosecution actions. |
|  Documents | Prohibition Order Charge Sheet Traffic Act Charge Sheet Invoice Receipt  Load Correction Memo Compliance Certificate |
| Post-conditions |  Charge Sheet and invoice generated and linked to Case Register (Subfile A). Case resolved in prosecution module(paid → load correction → compliance certificate → case closed) Audit log linking all steps to officer and timestamps. |
| Trigger | Manual: Officer flags an existing Case Register entry for prosecution. |
| Controls | Must verify Case Details completeness before charge computation. Idempotency on invoice / charge sheet generation. Supervisor authorization for manual overrides. |
| Basic Flow (Frontend) | 1\. The officer flags a case for prosecution from the Case Register queue. 2\. System computes charges (Traffic rules): applies GVW charges and generates Charge Sheet. 3\. Prosecution  steps:  Officer generates Charge Sheet.  Create Invoice; push invoice to eCitizen for payment(if system is online) or mark for offline queue for re-submission.  If payment received → record receipt, generate Load Correction Memo and schedule reweigh. If compliant, auto generate compliance certificates. This action triggers case closure in the case register module |
| Basic Flow (Backend) | Server uses deterministic fee computation with current FX and fee bands (stored config) if applicable(in case EAC Act case is involved) since Traffic Act charges are already in Kenyan Shillings.  Case record links to weighing evidence, prohibition order, invoices and receipts.  RabbitMQ enqueues eCitizen submissions and notifications; retry logic on failure.  |
| Printable outputs | Charge Sheet Notice to Attend Court Invoice |
| New Process Requirements | Integrate case lifecycle events with CMS (internal or external).  |
|  Third Party Integrations | eCitizen (payments) Road Authority portals Email/SMS notification service. |
| Exceptions & Error Handling | Fee computation mismatch: store computed result and reason; allow manual override with audit.  eCitizen rejection: queue and notify officer for manual resubmission.  Missing evidence: block NTAC until evidence is attached or noted as an exception with reason. |
| Audit & Logging | Full case audit trail: who created/modified case, when, what changed. Payment and NTAC assignment logged with external reference IDs. |
| Security | RBAC for case viewing/editing, PII protection for driver/owner data, encrypted attachments, audit retention and access logs. |

## 

## B. 2.1 – Prosecution Business Process Model  and Notation(BPMN)

## B. 3 – Case Management Process

| Service | Case Management (Case Register → Case Management) |
| :---- | :---- |
| Where is the service initiated? | Frontend. Officer flags case register entry for court process |
| Does the service have a Portal Transaction? | No |
| Service Description | The Case Manager receives escalated cases and is responsible for building and maintaining Subfiles A–J (evidence, expert reports, witness statements, accused records, investigation diary, charge documents, minute sheets), scheduling hearings, tracking arrests/arrest warrants (active/dropped/executed), coordinating finance (payments/receipts), and moving cases through status lifecycle until finalization (Withdrawn/Discharged/Charged & Paid/Charged & Jailed). |
| Who can perform this process? | Case Manager Prosecutor |
| Actors | Case Manager Prosecutor Investigators Witnesses |
| Pre-conditions | Case Register entry exists and Subfile A populated.  Case fagged for Court process. Evidence attachments available or flagged as outstanding. |
| Documents | Subfile A (Initial Case Details) must be present. On escalation, Case Manager must ensure presence of required records in Subfiles: B — Document Evidence: weight tickets, photos, ANPR footage, permit docs.  C — Expert Reports: engineering/forensic reports (if any).  D — Witness Statements: statements from inspector/driver/witnesses.  E — Accused Statement / Reweigh & Compliance (accused’s statements & reweigh docs).  F — Investigation Diary: investigation steps, timelines.  G — Charge Sheets / Bonds / NTAC / Arrest Warrants (copies).  H — Accused Records: prior offences, identification documents.  I — Covering Report: prosecutorial cover memo summarising case.  J — Minute Sheet & Correspondences: court minutes, adjournments, correspondence. |
| Post-conditions | Case status transitions captured in Case Register and Case Management (e.g., PBC → In Court → Judgment → Closed).  Case subfiles are complete per the legally required closure checklist for the final disposition chosen (Withdrawn, Discharged under various CPC sections, Charged & Paid, Charged & Jailed).  Arrest warrants tracked and updated (active/executed/dropped) and recorded in Subfile G and case timeline. |
| Trigger(s) | Case escalation from Case Register.  |
| Controls | Mandatory closure checklist: For a case to be finalized, required records in Subfiles (A plus a set subset of B–J depending on final outcome) must be present.  The system enforces checklists and blocks closure until required documents exist.  Workflow & state machine: only valid state transitions allowed; history recorded.  Arrest warrant lifecycle management: enforce statuses (issued, active, executed, dropped).  Role approvals: certain actions (withdrawals under CPC sections, discharge, plea acceptance) require prosecutor or supervisor authorization. |
| Basic Flow (Case Manager) | 1.Escalete case from case register to Case Manager: Case Manager claims the escalated case. The system surfaces Subfile A and missing subfile checklist.  2\. Populate Subfiles: Populate B–J as evidence arrives: upload documents (B), commission/attach expert reports (C), collect witness statements (D), record accused statements (E), maintain investigation diary (F).  3\. Prepare Charge & NTAC: Prepare charge sheet and NTAC (Subfile G). Attach copies to Subfile G and submit to the court registry where required. 4\. Schedule & Track Hearings: Use built-in calendar, create minute sheet entries (Subfile J) for each hearing; update case status on each outcome.  5\. Manage Arrest Warrants: Issue warrant (record in Subfile G), track execution or dropping, update accused records (H).  6\. Financial reconciliation: Link invoices/receipts (Subfile D or separate financial index) and update case financial status. If payment occurs pre-court, note and optionally withdraw or resolve the case per policy.  7\. Finalise: Based on outcome (Withdrawn under section X CPC, Discharged under Y PC, Charged & Paid, Charged & Jailed), ensure required subfile records exist, complete closing checklist, produce Case Closure Report and archive. Update Case Register entry with final disposition and references to closure documents. |
| Basic Flow (Backend / Integrations) | APIs to create and update subfiles Tracker for arrest warrant statuses and notifications.  Notification integrations to summon witnesses/defendants (SMS/email). Audit logs include immutable copies of court documents and minute sheets. |
| Subfile closure checklist (examples) | For finalization, require different subfile sets depending on disposition:  Withdrawn (Section 87A / 202 / 204 CPC): Subfile A, I (Covering Report) with approval memo, J (Minute Sheet), Documented prosecutor approval.  Discharged (Sections 35/210/215 PC): Subfile A, B (evidence review), C (expert reports if applicable), J (minutes) and Covering Report (I) documenting reasoning.  Charged & Paid: Subfile A, G (Charge sheet copies), D/H (accused details), financial receipt attached (D or specific financial subfile).  Charged & Jailed: Subfile A, G (Charge sheet & warrant), D (witness statements), E (accused statements), J (minute sheets), I (covering report). |
| Printable outputs | Charge Sheets (G) Covering Reports (I) Minute Sheets (J) Consolidated Case File |

## B. 3.1 – Case Management Business Process Model and Notation(BPMN)