# Sprint 5: Yard & Tags

**Duration:** Weeks 9-10  
**Module:** Yard Management & Vehicle Tags  
**Status:** Planning

---

## Overview

Implement yard management for vehicles sent for offloading/redistribution and vehicle tagging system for violation tracking.

---

## Objectives

- Implement yard entry and tracking
- Create vehicle tags management
- Implement tag categories
- Add yard status reporting
- Integrate with case register

---

## Tasks

### Yard Management

- [ ] Create YardEntry entity
- [ ] Implement yard entry repository
- [ ] Create yard entry DTOs
- [ ] Implement yard entry controller
- [ ] Add yard entry from weighing
- [ ] Implement yard status tracking (pending, processing, released, escalated)
- [ ] Create yard release workflow
- [ ] Add yard count/statistics API

### Vehicle Tags

- [ ] Create VehicleTag entity
- [ ] Create TagCategory entity
- [ ] Implement vehicle tag repository
- [ ] Create tag DTOs and validation
- [ ] Implement tag controller
- [ ] Add automatic tag generation logic
- [ ] Implement manual tag creation
- [ ] Add tag lifecycle (open/closed)
- [ ] Implement tag export to external systems (KeNHA)
- [ ] Add vector embeddings for tag reasons

### Testing

- [ ] Unit tests for yard repository
- [ ] Unit tests for tag repository
- [ ] Integration tests for yard API
- [ ] Integration tests for tag API

---

## Acceptance Criteria

- [ ] Yard management complete
- [ ] Vehicle tagging functional
- [ ] Tag export working
- [ ] All tests passing

---

## Estimated Effort: 40-50 hours

