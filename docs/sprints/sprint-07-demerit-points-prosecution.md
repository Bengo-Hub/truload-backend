# Sprint 7: Demerit Points System & Prosecution Integration

**Duration:** Weeks 13-14  
**Module:** Traffic Management & Prosecution  
**Status:** Planned  
**Prerequisites:** Sprint 4 (Weighing Core), Sprint 6 (Case Register & Special Release)

---

## Overview

Implement comprehensive demerit points tracking system with NTSA integration, driver license management, and prosecution workflow integration for overload violations.

---

## Implementation Notes

### Architecture
- **Driver Management:** Centralized driver entity with NTSA integration (ntsa_id, license_no)
- **Demerit Tracking:** DriverDemeritRecord table tracks violations with 36-month expiry window
- **Legal Frameworks:** Support both EAC (5% tolerance) and Kenya Traffic Act (stricter enforcement)
- **Suspension Logic:** Automatic threshold checking (12 points standard, 8 points probationary, +2 allowance for professional drivers)
- **Background Jobs:** Hangfire daily cron to expire old points automatically

### Database Schema (Already Created)
- ✅ `drivers` table: NTSA integration, license management, cached current_demerit_points, suspension dates
- ✅ `driver_demerit_records` table: Violation history with points_assigned, expiry_date, payment_status
- ✅ Indexes: ntsa_id, license_no, driver_id+violation_date, expiry_date (filtered)
- ✅ Check constraints: license_status, payment_status, legal_framework, violation_type enums
- ✅ Foreign keys: driver_id (CASCADE delete), fee_schedule_id (SET NULL), case_register_id/weighing_id (future)

### Completed Items (Phase 1)
- ✅ Driver entity model with NTSA fields
- ✅ DriverDemeritRecord entity model with expiry logic
- ✅ DbContext configuration with relationships
- ✅ Migration: `20251209104934_AddDriverAndDemeritPointsTables`
- ✅ Database tables created in PostgreSQL

---

## Objectives

- Implement DemeritPointsService with business logic
- Integrate demerit points with weighing workflow
- Integrate demerit points with prosecution/case register workflow
- Implement NTSA synchronization service
- Create driver management APIs
- Implement license suspension workflow with notifications
- Create background jobs for points expiry and sync
- Build reporting and analytics endpoints

---

## Tasks

### Phase 2: Service Layer Implementation

- [ ] **Create DemeritPointsService**
  - Implement `AssignDemeritPoints(driverId, points, violationDetails)` method
  - Implement `CalculateCurrentPoints(driverId)` method (sum non-expired records)
  - Implement `CheckLicenseSuspension(driverId)` method (threshold comparison)
  - Implement `ExpireOldPoints()` background job method (36-month window)
  - Implement `GetDriverHistory(driverId)` method (paginated history)
  - Implement `GenerateSuspensionNotice(driverId)` method (PDF generation)
  - Add logging and error handling for all methods
  - Inject IConfiguration for thresholds (suspension_threshold_standard: 12, suspension_threshold_probationary: 8, professional_driver_bonus: 2)

- [ ] **Configure Background Jobs**
  - Set up Hangfire recurring job for `ExpireOldPoints()` (daily at 02:00 UTC)
  - Add job dashboard endpoint `/hangfire` (admin-only access)
  - Monitor job execution history and failures
  - Add retry policies for failed point expiry operations

### Phase 3: Weighing Workflow Integration

- [ ] **Update Weighing Service**
  - Add driver lookup/selection during weighing entry
  - Validate driver license status (reject if suspended/revoked/expired)
  - Create pending DriverDemeritRecord when overload detected (payment_status='pending')
  - Link DriverDemeritRecord to weighing_id and case_register_id
  - Calculate points from AxleFeeSchedule based on overload_kg and legal_framework
  - Log driver validation events in audit logs

- [ ] **Update Case Register Service**
  - Link case registers to driver via driver_id foreign key
  - Create pending demerit record on case creation
  - Validate driver status before case registration
  - Add driver details to case register DTOs

### Phase 4: Prosecution Integration

- [ ] **Update Prosecution Service**
  - On payment confirmation: Call `DemeritPointsService.AssignDemeritPoints()`
  - Update DriverDemeritRecord.payment_status from 'pending' to 'paid'
  - Trigger `CheckLicenseSuspension()` after points assigned
  - Generate suspension notice if threshold exceeded
  - Send SMS notification to driver (via notifications-service)
  - Update Driver.license_status to 'suspended' with suspension dates
  - Log all prosecution actions in audit logs

- [ ] **Create Prosecution Webhooks**
  - Implement webhook receiver for payment confirmations
  - Implement webhook receiver for court convictions
  - Validate HMAC signatures on webhook requests
  - Handle idempotency for duplicate webhook deliveries

### Phase 5: NTSA Integration

- [ ] **Create NTSA Sync Service**
  - Implement driver lookup by ntsa_id or license_no
  - Fetch license details (class, issue/expiry dates, status)
  - Sync driver demographic data (full_names, surname, id_number)
  - Update local driver record with NTSA data
  - Handle NTSA API errors and rate limiting
  - Cache NTSA responses (Redis, 24hr TTL)

- [ ] **Create NTSA Background Jobs**
  - Daily sync for active drivers (update license status/expiry)
  - Weekly full sync for all drivers (reconciliation)
  - Push suspension notices to NTSA (if API available)
  - Monitor sync failures and send alerts

### Phase 6: Driver Management APIs

- [ ] **Create DriverController**
  - `POST /api/v1/drivers` - Create driver (validate NTSA ID)
  - `GET /api/v1/drivers/{id}` - Get driver details with current points
  - `GET /api/v1/drivers` - List drivers (filters: status, points range, station)
  - `PUT /api/v1/drivers/{id}` - Update driver (local fields only, not NTSA data)
  - `POST /api/v1/drivers/{id}/sync-ntsa` - Manual NTSA sync trigger
  - `GET /api/v1/drivers/{id}/demerit-history` - Get violation history (paginated)
  - `GET /api/v1/drivers/search` - Search by license_no, id_number, phone

- [ ] **Create Driver DTOs & Validation**
  - CreateDriverRequest DTO (require ntsa_id or license_no)
  - UpdateDriverRequest DTO (validate phone/email format)
  - DriverResponse DTO (include current_points, days_until_suspension_end)
  - DemeritRecordResponse DTO (include violation details, expiry countdown)
  - FluentValidation rules (license_no format, id_number length, etc.)

- [ ] **Create DriverRepository**
  - Implement IDriverRepository interface
  - Methods: GetByIdAsync, GetByNtsaIdAsync, GetByLicenseNoAsync, GetAllAsync (with filters)
  - Include demerit records in queries (use `.Include(d => d.DemeritRecords)`)
  - Add caching for frequently accessed drivers
  - Implement soft delete pattern

### Phase 7: License Suspension Workflow

- [ ] **Implement Suspension Logic**
  - Calculate threshold: 12 points (standard), 8 points (probationary), +2 for professional
  - Set Driver.license_status = 'suspended'
  - Set Driver.suspension_start_date = now, suspension_end_date = now + 90 days
  - Update Driver.current_demerit_points (cached sum)
  - Generate suspension notice PDF (include violation history, appeal instructions)

- [ ] **Create Notification Integration**
  - Send SMS to driver: "Your license has been suspended for 90 days due to accumulating X demerit points. Appeal: [URL]"
  - Send email with suspension notice PDF attachment
  - Log notification delivery status
  - Handle notification failures with retry queue

- [ ] **Create Suspension Appeals**
  - `POST /api/v1/drivers/{id}/appeal` - Submit appeal with reason
  - Link appeal to case_register for review
  - Update suspension dates if appeal approved
  - Log appeal decisions in audit logs

### Phase 8: Reporting & Analytics

- [ ] **Create Demerit Points Reports**
  - Report: Drivers by points range (0-3, 4-7, 8-11, 12+)
  - Report: Suspended drivers by station/month
  - Report: Top violations by type (GVW_OVERLOAD, AXLE_OVERLOAD, etc.)
  - Report: Professional drivers vs standard drivers violation rates
  - Report: Points expiring in next 30/60/90 days
  - Export reports to CSV/Excel

- [ ] **Create Dashboard Endpoints**
  - `GET /api/v1/analytics/demerit-summary` - Overall stats (total drivers, suspended, average points)
  - `GET /api/v1/analytics/violations-by-month` - Trend analysis
  - `GET /api/v1/analytics/drivers-at-risk` - Drivers with 9+ points (near suspension)
  - Cache dashboard queries (Redis, 1hr TTL)

### Phase 9: Testing & Documentation

- [ ] **Unit Tests**
  - Test DemeritPointsService.CalculateCurrentPoints() with expired records
  - Test CheckLicenseSuspension() with different thresholds
  - Test ExpireOldPoints() background job logic
  - Test suspension logic with professional driver bonus
  - Test payment status transitions

- [ ] **Integration Tests**
  - Test weighing workflow creates pending demerit record
  - Test prosecution payment triggers point assignment
  - Test suspension triggers SMS notification
  - Test NTSA sync updates driver data
  - Test webhook idempotency

- [ ] **API Documentation**
  - Document all driver endpoints with Swagger
  - Add request/response examples for demerit workflows
  - Document NTSA integration requirements
  - Document suspension appeal process
  - Document background job schedules

- [ ] **User Documentation**
  - Write operator guide for driver management
  - Write guide for handling suspended drivers
  - Document appeal process for drivers
  - Create troubleshooting guide for NTSA sync failures

---

## Acceptance Criteria

- [ ] Demerit points automatically assigned on prosecution payment
- [ ] License suspension triggered when points exceed threshold
- [ ] SMS/email notifications sent to suspended drivers
- [ ] Background job expires points after 36 months
- [ ] NTSA sync updates driver license status daily
- [ ] Driver search and filtering works correctly
- [ ] Suspension notices generated as PDF
- [ ] Appeal workflow functional
- [ ] All unit and integration tests passing
- [ ] API documentation complete
- [ ] Dashboard shows real-time demerit statistics

---

## Dependencies

- Sprint 4: Weighing Core (Weighing entity, weight capture flow)
- Sprint 6: Case Register & Special Release (CaseRegister entity, prosecution workflow)
- Notifications-service: SMS/email delivery
- Auth-service: User authentication for driver lookup
- NTSA API: License verification and sync (optional for MVP)

---

## Estimated Effort

**Total:** 70-85 hours

- Service Layer: 12-15 hours
- Weighing Integration: 8-10 hours
- Prosecution Integration: 8-10 hours
- NTSA Integration: 10-12 hours
- Driver Management APIs: 10-12 hours
- Suspension Workflow: 8-10 hours
- Reporting & Analytics: 6-8 hours
- Testing & Documentation: 8-10 hours

---

## Risks & Mitigation

**Risk:** NTSA API unavailability or rate limiting  
**Mitigation:** Implement caching, graceful degradation, allow manual entry if sync fails, queue sync requests

**Risk:** SMS delivery failures for suspension notices  
**Mitigation:** Use notifications-service retry queue, fall back to email, log all notification attempts

**Risk:** Points expiry job performance with large dataset  
**Mitigation:** Process in batches (1000 records at a time), add indexes on points_expiry_date, run during off-peak hours

**Risk:** Duplicate point assignment from webhook retries  
**Mitigation:** Implement idempotency keys, check payment_status before assignment, use database transactions

**Risk:** Driver disputes incorrect point assignments  
**Mitigation:** Comprehensive audit logging, appeal workflow, manual override capability for admins

---

## Notes

- Demerit points are **not seeded** - they are transactional data created during operations
- Driver entity can be created without NTSA sync (manual entry fallback)
- Professional driver flag must be verified by admin (requires proof of PSV license)
- Suspension period is configurable (default 90 days) in appsettings.json
- Points expiry is automatic - no manual intervention needed
- All demerit actions must be audited for compliance

---

## Deliverables

1. DemeritPointsService with business logic implementation
2. Weighing workflow integration (pending demerit records)
3. Prosecution workflow integration (point assignment on payment)
4. Driver management APIs with CRUD operations
5. NTSA synchronization service
6. License suspension workflow with notifications
7. Background jobs for points expiry and sync
8. Reporting and analytics endpoints
9. Comprehensive test suite
10. API and user documentation
11. Admin dashboard for demerit points monitoring
