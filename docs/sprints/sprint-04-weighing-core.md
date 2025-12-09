# Sprint 4: Weighing Core

**Duration:** Weeks 7-8  
**Module:** Weighing Module - Core Weighing Flow  
**Status:** Planning

---

## Overview

Implement core weighing functionality including vehicle entry, weight capture, compliance evaluation, and ticket generation.

---

## Objectives

- Implement vehicle and driver management
- Create weighing transaction flow
- Implement weight capture (Static, WIM, Axle-by-Axle)
- Implement compliance evaluation logic
- Implement tolerance and permit checking
- Create prohibition orders
- Implement reweigh cycle logic
- Generate weight tickets and certificates

---

## Tasks

### Vehicle & Driver Management

- [ ] Create Vehicle entity
- [ ] Create Driver entity
- [ ] Create VehicleOwner entity
- [ ] Create Transporter entity
- [ ] Implement vehicle repository
- [ ] Implement driver repository
- [ ] Create vehicle DTOs and validation
- [ ] Create driver DTOs and validation
- [ ] Implement vehicle controller
- [ ] Implement driver controller
- [ ] Add vehicle search by registration
- [ ] Add driver search by ID/license
- [ ] Implement vehicle flagging logic
- [ ] Add NTSA integration placeholders

### Weighing Transaction Core

- [ ] Create Weighing entity
- [ ] Create WeighingAxle entity
- [ ] Implement weighing repository
- [ ] Create weighing DTOs
- [ ] Implement weighing controller
- [ ] Add client-generated UUID support (offline)
- [ ] Implement idempotency checks
- [ ] Add sync_status field support
- [ ] Create weighing initiation endpoint

### Weight Capture Implementation

- [ ] Implement Static mode capture logic
- [ ] Implement WIM mode capture logic
- [ ] Implement Axle-by-Axle mode capture logic
- [ ] Create weight stabilization validation
- [ ] Implement per-axle weight assignment
- [ ] Add GVW calculation logic
- [ ] Create weight stream event logging
- [ ] Implement TruConnect integration placeholders

### Compliance Evaluation Logic

- [ ] Implement GVW overload calculation
- [ ] Implement axle overload calculation
- [ ] Create tolerance application logic
- [ ] Implement permit extension checking
- [ ] Create compliance decision engine
- [ ] Add special release auto-determination (≤200kg)
- [ ] Implement prohibition decision logic
- [ ] Add violation reason generation

### Prohibition Orders

- [ ] Create ProhibitionOrder entity
- [ ] Implement prohibition order repository
- [ ] Create prohibition order DTOs
- [ ] Implement prohibition order generation
- [ ] Add prohibition order PDF generation
- [ ] Link prohibition to case register

### Reweigh Cycle Management

- [ ] Implement reweigh cycle counter logic
- [ ] Create reweigh limit enforcement (max 8)
- [ ] Link reweigh to original weighing
- [ ] Implement compliance certificate generation
- [ ] Add reweigh scheduling logic

### Document Generation

- [ ] Implement weight ticket generation
- [ ] Create weight ticket PDF template
- [ ] Implement compliance certificate generation
- [ ] Create prohibition order PDF template
- [ ] Add document storage to blob storage
- [ ] Implement document retrieval API

### Offline Support

- [ ] Implement device_sync_events queue
- [ ] Create offline weighing submission endpoint
- [ ] Add correlation_id deduplication
- [ ] Implement sync status tracking
- [ ] Create background sync job
- [ ] Add conflict resolution logic

### Testing

- [ ] Write unit tests for vehicle repository
- [ ] Write unit tests for weighing repository
- [ ] Write unit tests for compliance evaluation
- [ ] Write unit tests for tolerance logic
- [ ] Write integration tests for weighing flow
- [ ] Write integration tests for offline sync
- [ ] Write E2E tests for complete weighing cycle

---

## Acceptance Criteria

- [ ] Vehicle and driver management complete
- [ ] Weighing transaction flow working end-to-end
- [ ] All three weight capture modes implemented
- [ ] Compliance evaluation logic working correctly
- [ ] Tolerance and permit checking functional
- [ ] Prohibition orders generated correctly
- [ ] Reweigh cycle logic working (max 8 cycles)
- [ ] Weight tickets and certificates generated
- [ ] Offline support with idempotency working
- [ ] All tests passing
- [ ] Code review completed

---

## Dependencies

- Sprint 3 completed (stations, axle configs, fee bands)
- TruConnect service available for testing
- Blob storage configured
- PDF generation library configured

---

## Estimated Effort

**Total:** 100-120 hours

- Vehicle & Driver Management: 15-18 hours
- Weighing Transaction Core: 18-20 hours
- Weight Capture: 15-18 hours
- Compliance Evaluation: 18-20 hours
- Prohibition Orders: 8-10 hours
- Reweigh Cycle: 8-10 hours
- Document Generation: 10-12 hours
- Offline Support: 10-12 hours
- Testing: 12-15 hours

---

## Deliverables

1. Vehicle and driver management API
2. Core weighing transaction flow
3. Weight capture for all modes (Static, WIM, Axle)
4. Compliance evaluation engine
5. Prohibition order generation
6. Reweigh cycle management
7. Weight ticket and certificate generation
8. Offline support with sync queue
9. Comprehensive tests
10. API documentation

