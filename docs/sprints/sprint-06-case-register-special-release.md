# Sprint 6: Case Register & Special Release

**Duration:** Weeks 11-12  
**Module:** Case Management - Case Register & Special Release  
**Status:** Planning

---

## Overview

Implement case register as central hub for all violations and special release workflow for compliant/redistribution cases.

---

## Objectives

- Implement case register (Subfile A)
- Create special release workflow
- Implement load correction memos
- Generate compliance certificates
- Link to weighings and yard

---

## Tasks

### Case Register Core

- [ ] Create CaseRegister entity (Subfile A)
- [ ] Implement case register repository
- [ ] Create case register DTOs
- [ ] Implement case register controller
- [ ] Add auto-creation from weighing/prohibition
- [ ] Implement case number generation
- [ ] Add NTAC number tracking (driver and transporter)
- [ ] Implement OB number tracking
- [ ] Add vector embeddings for violation details

### Special Release Workflow

- [ ] Create SpecialRelease entity
- [ ] Implement special release repository
- [ ] Create special release DTOs
- [ ] Implement special release controller
- [ ] Add admin authorization checks
- [ ] Implement release type logic (redistribution, tolerance, permit, admin)
- [ ] Create special release certificate generation

### Load Correction & Compliance

- [ ] Create LoadCorrectionMemo entity
- [ ] Create ComplianceCertificate entity
- [ ] Implement repositories
- [ ] Create DTOs
- [ ] Implement controllers
- [ ] Add memo PDF generation
- [ ] Add certificate PDF generation

### Case Manager Assignment

- [ ] Create CaseManager entity
- [ ] Create CaseAssignmentLog entity
- [ ] Implement case assignment logic
- [ ] Add supervisor approval workflow
- [ ] Implement re-assignment with audit trail

### Testing

- [ ] Unit tests for case register
- [ ] Unit tests for special release
- [ ] Integration tests for case API
- [ ] Integration tests for special release workflow

---

## Acceptance Criteria

- [ ] Case register fully functional
- [ ] Special release workflow complete
- [ ] Load correction memos and compliance certificates generated
- [ ] Case assignment working
- [ ] All tests passing

---

## Estimated Effort: 60-80 hours

