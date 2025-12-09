# Sprint 3: Weighing Setup

**Duration:** Weeks 5-6  
**Module:** Weighing Module - Setup & Reference Data  
**Status:** Planning

---

## Overview

Implement weighing module foundation including reference data, station configuration, axle configurations, and scale calibration management.

---

## Objectives

- Create weighing module structure
- Implement station management
- Implement axle configuration management
- Implement scale test/calibration management
- Seed reference data (origins, destinations, cargo types)
- Implement fee bands for EAC and Traffic Acts
- Create weighing module API endpoints

---

## Tasks

### Module Structure

- [ ] Create Weighing module folder structure
- [ ] Set up module-specific DbContext configuration
- [ ] Create base entities for weighing module
- [ ] Configure module routing

### Station Management

- [ ] Create Station entity and repository
- [ ] Implement station CRUD operations
- [ ] Create station DTOs and validation
- [ ] Implement station controller
- [ ] Add station bounds support (A/B bidirectional)
- [ ] Implement station-road-county relationships
- [ ] Add GPS coordinate support
- [ ] Create station active/inactive management

### Axle Configuration Management

- [ ] Create AxleConfiguration entity
- [ ] Implement axle configuration repository
- [ ] Create axle configuration DTOs
- [ ] Implement axle configuration controller
- [ ] Seed axle configurations from `AXLECONFIG_DATA.csv`
- [ ] Add axle pattern validation
- [ ] Implement GVW permissible limits
- [ ] Add visual diagram URL support

### Scale Test/Calibration Management

- [ ] Create ScaleTest entity
- [ ] Implement scale test repository
- [ ] Create scale test DTOs
- [ ] Implement scale test controller
- [ ] Add daily calibration check logic
- [ ] Implement test result validation (pass/fail)
- [ ] Create calibration expiry alerts
- [ ] Implement weighing lockout on failed tests

### Reference Data Implementation

- [ ] Create Origins/Destinations entity
- [ ] Create Cargo Types entity
- [ ] Create Roads entity
- [ ] Create Counties/Districts/Subcounties entities
- [ ] Implement reference data repositories
- [ ] Create reference data controllers
- [ ] Seed reference data from CSV/JSON

### Act Definitions & Fee Bands

- [ ] Create ActDefinition entity
- [ ] Create EAC Fee Bands tables (GVW and Axle)
- [ ] Create Traffic Fee Bands tables (GVW)
- [ ] Implement act definition repository
- [ ] Implement fee bands repositories
- [ ] Seed EAC act and fee bands
- [ ] Seed Traffic act and fee bands
- [ ] Create fee lookup logic

### Permit Management

- [ ] Create Permit entity
- [ ] Implement permit repository
- [ ] Create permit DTOs and validation
- [ ] Implement permit controller
- [ ] Add permit validity checks
- [ ] Implement permit extension logic (2A, 3A)
- [ ] Create permit expiry alerts

### API Endpoints

- [ ] Station endpoints (CRUD, search, activate/deactivate)
- [ ] Axle configuration endpoints (CRUD, lookup by axles)
- [ ] Scale test endpoints (CRUD, daily check status)
- [ ] Reference data endpoints (countries, origins, destinations, cargo)
- [ ] Act definition endpoints (lookup)
- [ ] Fee bands endpoints (lookup by overload)
- [ ] Permit endpoints (CRUD, validity check)

### Testing

- [ ] Write unit tests for station repository
- [ ] Write unit tests for axle configuration repository
- [ ] Write unit tests for scale test repository
- [ ] Write unit tests for fee lookup logic
- [ ] Write integration tests for station API
- [ ] Write integration tests for reference data API
- [ ] Write integration tests for permit API

---

## Acceptance Criteria

- [ ] All station management functionality complete
- [ ] Axle configurations seeded and accessible via API
- [ ] Scale test functionality working with daily checks
- [ ] Reference data seeded (origins, destinations, cargo, roads)
- [ ] Act definitions and fee bands seeded
- [ ] Permit management functionality complete
- [ ] All API endpoints documented and tested
- [ ] Unit and integration tests passing
- [ ] Code review completed

---

## Dependencies

- Sprint 1 completed (database, auth, users)
- PostgreSQL database with all tables created
- Axle configuration data CSV available
- Fee bands research data available

---

## Estimated Effort

**Total:** 80-100 hours

- Module Structure: 6-8 hours
- Station Management: 12-15 hours
- Axle Configuration: 10-12 hours
- Scale Test Management: 10-12 hours
- Reference Data: 12-15 hours
- Act Definitions & Fee Bands: 15-18 hours
- Permit Management: 8-10 hours
- Testing: 10-12 hours

---

## Deliverables

1. Station management API
2. Axle configuration management with seeded data
3. Scale test/calibration API
4. Reference data API (origins, destinations, cargo, roads)
5. Act definitions and fee bands seeded
6. Permit management API
7. Unit and integration tests
8. API documentation

