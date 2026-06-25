# Changelog

All notable changes to TruLoad Backend will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.3.0](https://github.com/Bengo-Hub/truload-backend/compare/v1.2.0...v1.3.0) (2026-06-25)


### Features

* **acts:** expose FlatFeeKes + ConvictionNumber on AxleFeeScheduleDto ([2b44503](https://github.com/Bengo-Hub/truload-backend/commit/2b44503c928e1d3ef5f89ca85161aa40f1a12cc8))
* **backup:** pluggable rclone backup destinations (local primary + fallback) ([8544a04](https://github.com/Bengo-Hub/truload-backend/commit/8544a04288ffe0f8c2d3f5a62ecea8d4490c80f7))
* **backup:** pluggable rclone backup destinations (local primary + fallback) ([c733292](https://github.com/Bengo-Hub/truload-backend/commit/c733292bc90fd743f464353525d4ae47c82b0f60))
* **documents:** include LCM + compliance certificate in case documents ([46aae81](https://github.com/Bengo-Hub/truload-backend/commit/46aae81d128c8598f24f802be21600faa907acef))
* **offline:** idempotent create across weighing→case→prosecution (Phase 1) ([9ba5f0a](https://github.com/Bengo-Hub/truload-backend/commit/9ba5f0aa2f41522b9b9a1776280c903e83c67ea9))
* **offline:** recent-convictions endpoint for offline conviction-tier cache ([dc843da](https://github.com/Bengo-Hub/truload-backend/commit/dc843dacd161dbbf20008f03c87cdcaf06003ec9))


### Bug Fixes

* **audit:** skip DB audit-log persistence for cross-tenant SUPERUSERs ([0f0b491](https://github.com/Bengo-Hub/truload-backend/commit/0f0b491e8608aaa7507d4fd6f5fa3e61b5cc3d51))
* **cases:** case-from-weighing path must ignore tenant filter for superuser ([5cf6e16](https://github.com/Bengo-Hub/truload-backend/commit/5cf6e16f63d0b6d7b5a1c39afdd9587a485c2184))
* **cases:** scope case-from-weighing to the weighing's org (superuser create) ([99b2bc8](https://github.com/Bengo-Hub/truload-backend/commit/99b2bc827eed147d0cbba9fc47dd69917c1acdd2))
* **drivers:** handle duplicate ID/license on update with a clear 409 ([5c27fd7](https://github.com/Bengo-Hub/truload-backend/commit/5c27fd7d3f7e6b5d2f42212793fe1912a8c3032a))
* **drivers:** handle duplicate ID/license on update with a clear 409 ([d27ac87](https://github.com/Bengo-Hub/truload-backend/commit/d27ac873114b7832e66ec6345486c4463fb39b38))
* **hangfire:** use a direct PostgreSQL connection, not PgBouncer ([90e9914](https://github.com/Bengo-Hub/truload-backend/commit/90e99147157f166954732b0ff56e9daa39f62011))
* **nats:** queue-group tenant.subscription.updated subscriber (multi-replica safe) ([#19](https://github.com/Bengo-Hub/truload-backend/issues/19)) ([9c3b0a2](https://github.com/Bengo-Hub/truload-backend/commit/9c3b0a2c40ed3fba64fe8720cffc1aab854f5038))
* **offline-sync:** idempotency lookups must ignore tenant filter (superuser) ([d1046fd](https://github.com/Bengo-Hub/truload-backend/commit/d1046fd65bffdfebe7d1c72a4d66404e2e742904))
* **payments:** monotonic invoice numbering + origin-based eCitizen redirect ([e3dad65](https://github.com/Bengo-Hub/truload-backend/commit/e3dad65b5a0c842b915d76810ff2db1e648198dd))
* **payments:** reconcile/webhook 500 — nullable load-correction-memo issuer ([750546a](https://github.com/Bengo-Hub/truload-backend/commit/750546a4e483a3bd89c1e6e874cd905d1111f78a))
* **prosecutions:** constrain GetById to {id:guid} so literal routes resolve ([bc2e8c3](https://github.com/Bengo-Hub/truload-backend/commit/bc2e8c3c8e94d96497ced346bff84b7231246e14))
* **truload:** idempotent invoice generation, role-permission set semantics, shift CRUD + receipt void exposure ([b904c14](https://github.com/Bengo-Hub/truload-backend/commit/b904c14c96ba9caf838c46eefb8caa5e8602dc57))
* **weighing/cases:** stamp weighing OrganizationId so the case chain has a valid org ([b04a11b](https://github.com/Bengo-Hub/truload-backend/commit/b04a11b50cf780dc9d4f757071c00d87f4f156d0))
* **weighing:** resolve station org ignoring tenant filter (superuser create) ([c372a5f](https://github.com/Bengo-Hub/truload-backend/commit/c372a5fa39533b2ee040a051737964f1a5300ecd))

## [1.2.0](https://github.com/Bengo-Hub/truload-backend/compare/v1.1.2...v1.2.0) (2026-06-03)


### Features

* add AppUrl to Organization for per-tenant email deep links ([64f0660](https://github.com/Bengo-Hub/truload-backend/commit/64f06600e334cadbd35d64ff8e8d9e174f8c47bd))
* add commercial and transporter portal roles, permissions, and demo users ([285e3cb](https://github.com/Bengo-Hub/truload-backend/commit/285e3cbbadf4ccc2a48a199bf88799e916a2ce7c))
* cargo type quality fields, 4 new commercial reports, org TareGracePeriodDays ([b339179](https://github.com/Bengo-Hub/truload-backend/commit/b3391792a524183d9e8027fef61b786c2e96f301))
* commercial payment flows — invoice creation, manual reconcile, billing proxy ([961aff6](https://github.com/Bengo-Hub/truload-backend/commit/961aff6eb82c42264084432bc9bb98691b3b0aaa))
* commercial weighing improvements — snapshots, scale type, void, paged vehicles, tenant-scoped drivers ([42386be](https://github.com/Bengo-Hub/truload-backend/commit/42386be86817138b1055a49da614ba162c627ecb))
* commercial weighing PDF axle weights, tolerance approval, controller endpoints ([39a9958](https://github.com/Bengo-Hub/truload-backend/commit/39a9958d1a95e49d180dc12dfa10e0deb723ed1e))
* **commercial-weighing:** BUG-1, GAP-1/2/6, BUG-2, QD-1, NOTIF-1 fixes ([a046084](https://github.com/Bengo-Hub/truload-backend/commit/a04608467a696c86a167dbfd26bbb56eff043d24))
* **commercial:** completion notifications, subscription gate, and demo org module fix ([11542ab](https://github.com/Bengo-Hub/truload-backend/commit/11542ab060db2bcc83948198aacca182f3291372))
* **commercial:** DefaultTareExpiryDays org setting + commercial settings PATCH endpoint + DELETE tolerance ([9937447](https://github.com/Bengo-Hub/truload-backend/commit/9937447dd011dec08e81c3878beaa8e763be3419))
* implement use case segregation for roles and permissions ([b004263](https://github.com/Bengo-Hub/truload-backend/commit/b004263c1514225135ada5c3e6a394f7485b918e))
* **infra:** auto-migrate and seed all tenant databases on startup ([847ff6b](https://github.com/Bengo-Hub/truload-backend/commit/847ff6b6df2eff08add69e1e0eb46f0718d01179))
* link driver to transporter/employer + portal driver resolution improvement ([ac30825](https://github.com/Bengo-Hub/truload-backend/commit/ac308253de490b6013063f383b95e73c80c08ec0))
* multi-currency analytics — split revenue into KES/USD across all money DTOs and endpoints ([88d8328](https://github.com/Bengo-Hub/truload-backend/commit/88d8328a1acca64db469d2b4b4637bc4761b2263))
* **notifications:** add provider proxy, workflow prefs, scheduled reports, and domain updates ([3ceb836](https://github.com/Bengo-Hub/truload-backend/commit/3ceb836866032ba71cc9fd99120e061aa3e7527a))
* **notifications:** inject tenant branding into all outgoing emails ([5d7386b](https://github.com/Bengo-Hub/truload-backend/commit/5d7386b3e8d343e8d460213012940eff3e6a4c5c))
* **notifications:** workflow group default recipients + per-workflow CC ([eb0424e](https://github.com/Bengo-Hub/truload-backend/commit/eb0424e5c6917cc788c7ce4ab799bcf7248c059c))
* **org:** add structured address fields and fix tenant branding in document generation ([fffc2d9](https://github.com/Bengo-Hub/truload-backend/commit/fffc2d93a908e0e03c4c7fbb30838a28bf853c5a))
* platform refactor, hard delete, eCitizen fixes, tenant scoping ([852626f](https://github.com/Bengo-Hub/truload-backend/commit/852626f95ff383595e2030317bbd6d292f154031))
* **portal:** add portal auth fields to Driver and VehicleOwner models + EF migration ([48cdaa7](https://github.com/Bengo-Hub/truload-backend/commit/48cdaa7261fb1e9c3ec8c08cd9b501855fdb9af3))
* **portal:** PORTAL-1 — subscription enforcement, tier limits, PDF ticket download ([508c349](https://github.com/Bengo-Hub/truload-backend/commit/508c349ef9d040f56cc3e83f9244ea9670967589))
* **portal:** replace plan-name feature gating with subscriptions-api feature codes ([be7a2f3](https://github.com/Bengo-Hub/truload-backend/commit/be7a2f3cfc899ab8ed106e10c9cd6ec9b7d786ed))
* **portal:** team management, daily summary + anomaly alert jobs, bulk ZIP download, bulk vehicle CSV import ([6967eea](https://github.com/Bengo-Hub/truload-backend/commit/6967eeaa61c8d5ffa9a7e6ca780085336e4f479a))
* **security+tests:** production hardening, 0-failure test suite, docs overhaul ([493f019](https://github.com/Bengo-Hub/truload-backend/commit/493f019aac329afae66872d7850850805995be30))
* **subscription:** uniform subscription integration — bypass logic, NATS cache invalidation, JWT alignment ([30ab752](https://github.com/Bengo-Hub/truload-backend/commit/30ab752ca3c6c8206333c585d7589c287d719b97))
* **weighing:** axle-config tolerance override, tenant DB routing, dashboard filters ([3128301](https://github.com/Bengo-Hub/truload-backend/commit/3128301607ceeeea373abbac10dc5ec14a0c6e6c))
* **weighing:** prohibition-order PDF endpoint + tolerance-gated case capture ([bbff804](https://github.com/Bengo-Hub/truload-backend/commit/bbff8044ec133ab83a9c1cc9db661eda645aadde))


### Bug Fixes

* add pesaflow payment method display in ReceiptDocument ([1696754](https://github.com/Bengo-Hub/truload-backend/commit/169675407e49a49045a6b95d33bb493a5acd8461))
* **analytics:** analytics dashboard bugs, processing time, email notifications ([fbef39b](https://github.com/Bengo-Hub/truload-backend/commit/fbef39bfde1a334addb19cbc68ea4f88ae45703a))
* **analytics:** end-of-day dateTo, ManualReconcile case-close, callback URL fallbacks ([3cb5a49](https://github.com/Bengo-Hub/truload-backend/commit/3cb5a49dde39255fba316620bb8c60f880e7dbb0))
* **analytics:** fix chart shape mismatches, dateTo filter, station grouping + reconcile fixes ([90a5012](https://github.com/Bengo-Hub/truload-backend/commit/90a5012e8e4af4032b4321f2a15be641745d55b9))
* **branding:** add logos/ subdirectory, fix seeder paths, copy all brand assets ([a01ae2d](https://github.com/Bengo-Hub/truload-backend/commit/a01ae2d8c536267457174b57238749dc2f9f1358))
* **branding:** logo fallback to truload-logo.svg + tenant-aware background emails ([e841cfb](https://github.com/Bengo-Hub/truload-backend/commit/e841cfbed8416f1a97d7308c643d16a6503d2341))
* compress InvoiceDocument layout to fit on a single A4 page ([b0fe467](https://github.com/Bengo-Hub/truload-backend/commit/b0fe4673d76426ba724a8146bbe9f965d6bfc357))
* correct production service domains to match devops-k8s ingress hosts ([f46e1d4](https://github.com/Bengo-Hub/truload-backend/commit/f46e1d4ae05bfd97bd4aafab84dd57b0c87d522f))
* correct Traffic Act compliance tolerance and case auto-close flow ([13d0daa](https://github.com/Bengo-Hub/truload-backend/commit/13d0daa9a1d6656becbe246f10785e3f68d23126))
* **cors:** restore kuraweightest.masterspace.co.ke to allowed origins ([caa1d17](https://github.com/Bengo-Hub/truload-backend/commit/caa1d17bb683b85ab1a995b152b22b61b487d499))
* **drivers:** admin hard-delete (no soft-delete) + exclude soft-deleted from search ([e421ec2](https://github.com/Bengo-Hub/truload-backend/commit/e421ec21bfc63f53c34853d209d631db953cff44))
* eCitizen base_url normalization, stale notification job, tenant scoping ([671687b](https://github.com/Bengo-Hub/truload-backend/commit/671687bd4491d2a72c65f6a459acf74692b384f8))
* **ecitizen:** surface Pesaflow OAuth 422 response body in error message ([f401278](https://github.com/Bengo-Hub/truload-backend/commit/f401278bc7d6eb547cb66e421643fc253afa93bf))
* **ecitizen:** update billDesc to 'Axle Load Overload Fine' to better match eCitizen service name ([fa87056](https://github.com/Bengo-Hub/truload-backend/commit/fa8705638d74fe59014307c4ee08b94eab15e034))
* **email:** normalize recipients/CC into individual addresses (no dropped emails) ([5508d67](https://github.com/Bengo-Hub/truload-backend/commit/5508d67adc7682403e3fa6ffb4551d7148183cca))
* **email:** tenant branding/From-name on background flow, full template fields, subjects, CTA links, invoice-paid email ([b92b41d](https://github.com/Bengo-Hub/truload-backend/commit/b92b41d06cf2ebbadd22cacd01ba2537226fef86))
* **enforcement:** axle config tolerance takes precedence over global tolerance ([10684a2](https://github.com/Bengo-Hub/truload-backend/commit/10684a258e8a3b983291a5aa8581a93fcfc47f47))
* **enforcement:** separate GVW config tolerance from axle-group tolerance ([49eb832](https://github.com/Bengo-Hub/truload-backend/commit/49eb8328dca86930b2fe5659c0ec8fcd94b58c8c))
* extend CaptureStatus/CaptureSource VARCHAR(20→50) to fix 500 on first-weight ([313e272](https://github.com/Bengo-Hub/truload-backend/commit/313e272d9c9be176543d03914ee63512d53756ec))
* filter plans by service=truload in GetPlansJsonAsync ([98345e4](https://github.com/Bengo-Hub/truload-backend/commit/98345e4d94c6c1a16c468430fa8336c20ec377cb))
* **invoices:** validate manual reconcile channel against allowed payment methods ([a7f5ca4](https://github.com/Bengo-Hub/truload-backend/commit/a7f5ca41330cb665d25bf6b2ae726b7b5e7f0312))
* kura DB routing, axle config validator, notifications workflow prefs ([3bad167](https://github.com/Bengo-Hub/truload-backend/commit/3bad167b873078c3fc1ebdde98f2ea4e8be6de5a))
* **multi-tenant:** resolve superuser home DB from JWT org claims in Layer 2 ([9cf8bd3](https://github.com/Bengo-Hub/truload-backend/commit/9cf8bd353d179388172e1dd679d97d5a152fdfe9))
* **multi-tenant:** tenant-aware credential cache keys + superuser X-Org-Slug header ([8f34dcb](https://github.com/Bengo-Hub/truload-backend/commit/8f34dcb771df8f0e4c28883f745e62a5c05ed4ce))
* **notifications:** dynamic tenant from logged-in user's org context ([d69ec4e](https://github.com/Bengo-Hub/truload-backend/commit/d69ec4e97d754a091224b5f92d0ddd1261051fc9))
* **notifications:** revert to system_test template (now registered in notifications-api) ([09a6ef3](https://github.com/Bengo-Hub/truload-backend/commit/09a6ef30ad91789e4b1a2dd4720a70a8d055180a))
* **notifications:** use test_email template with correct variables ([be5347e](https://github.com/Bengo-Hub/truload-backend/commit/be5347e4b577cbda4a5c26fde87559586a4b6ed7))
* **notifications:** wire truload to notifications-api correctly ([fbe1543](https://github.com/Bengo-Hub/truload-backend/commit/fbe1543bb52ef4f96c01b6f9662df51d2ee3946e))
* **payments,cases,drivers,notifications:** sync eCitizen payments, escalation UX, driver dedup, reliable workflow emails ([#16](https://github.com/Bengo-Hub/truload-backend/issues/16)) ([0841f61](https://github.com/Bengo-Hub/truload-backend/commit/0841f6121b6acc978c74c840e166d4477eed8cf5))
* **payments:** reconcile alternate eCitizen refs, redirect mode for live checkout, driver ID enforcement, real org payment settings on PDFs ([86d10b9](https://github.com/Bengo-Hub/truload-backend/commit/86d10b90157bdbc17f64790f1befaeeb242b5fb3))
* **pdf:** centralize org branding in all document headers ([14d2de8](https://github.com/Bengo-Hub/truload-backend/commit/14d2de8fd37164cf97d95b1435623e7e1287574d))
* prevent act-specific 0% axle tolerance from falling through to standard law ([30c67a5](https://github.com/Bengo-Hub/truload-backend/commit/30c67a5a5f273debe51525e2836691ed652f0e51))
* remove codevertex host check that matched the API's own domain ([2ac14d8](https://github.com/Bengo-Hub/truload-backend/commit/2ac14d8fc5461d7b1a125c3c3a36e9403d47d9d7))
* remove redundant migration job — startup handles migrations ([b3128b5](https://github.com/Bengo-Hub/truload-backend/commit/b3128b5c283948ae0486c017fd24aaec6815ab18))
* remove tolerance from all 3+ axle configs in seed data ([a34791e](https://github.com/Bengo-Hub/truload-backend/commit/a34791e87f0b32ce5724621bd3de21dcfbd2793e))
* robust kura tenant DB routing via Origin header + org_code JWT claim ([e4e9ca8](https://github.com/Bengo-Hub/truload-backend/commit/e4e9ca8c8c1d178c11f118408ce8f366dc566ad9))
* **seed:** handle duplicate ISSUED/IN_FORCE warrant status on reseed ([13246da](https://github.com/Bengo-Hub/truload-backend/commit/13246da4827d329e5aba292da783e7849ee31ebd))
* use correct permission name for payment settings PUT endpoint ([4abe702](https://github.com/Bengo-Hub/truload-backend/commit/4abe7026adcc04ce6ba81da66d6b5386483ea51b))
* use DROP INDEX IF EXISTS in commercial weighing migration ([4fd3b3e](https://github.com/Bengo-Hub/truload-backend/commit/4fd3b3edd8c4336d7af1a1ea2b24b4851abf5a22))
* wire Superset__Password into K8s secret via deploy pipeline ([cb65cec](https://github.com/Bengo-Hub/truload-backend/commit/cb65cec973a4de296f8a0022f1fef4912f0cc833))

## [1.3.1] — 2026-05-21

### Bug Fixes
- fix(enforcement): axle config tolerance now highest priority in `CalculateGroupToleranceAsync`
- fix(enforcement): axle tolerance display shows "X kg (config)" when per-config override is active
- fix(axle-config): standard config tolerance/notes updates no longer return 400
- feat(infra): auto-migrate and seed all dedicated tenant databases on startup

## [1.3.0] — 2026-05-20

### Features
- feat(commercial): two-pass resume flow — `GET /commercial-weighing/pending-by-plate/{regNo}`
- feat(commercial): stale transaction notification job (Hangfire, 30 min interval)
- feat(commercial): completion and tolerance exception notifications
- feat(commercial): subscription validation before weighing initiation (HTTP 402 on inactive)
- feat(config): `Treasury:PayPortalBaseUrl` read from config (was hardcoded)
- feat(seeding): `commercial.pending_weighing_threshold_hours` system setting seeded

## [1.2.0] — 2026-04-22

### Features
- feat(enforcement): driver + owner joint-liability charge split (Cap 403 / EAC VLC)
- feat(enforcement): special release approval queue (supervisor workflow)
- feat(vehicles): vehicle registration normalisation

## [1.1.2](https://github.com/Bengo-Hub/truload-backend/compare/v1.1.1...v1.1.2) (2026-04-20)


### Bug Fixes

* bypass PgBouncer for EF Core startup migrations ([e445dcf](https://github.com/Bengo-Hub/truload-backend/commit/e445dcf6304eaffcfb44bc04af3555485937ef3c))
* remove redundant migration job — startup handles migrations ([b3128b5](https://github.com/Bengo-Hub/truload-backend/commit/b3128b5c283948ae0486c017fd24aaec6815ab18))
* use DROP INDEX IF EXISTS in commercial weighing migration ([4fd3b3e](https://github.com/Bengo-Hub/truload-backend/commit/4fd3b3edd8c4336d7af1a1ea2b24b4851abf5a22))

## [1.1.1](https://github.com/Bengo-Hub/truload-backend/compare/v1.1.0...v1.1.1) (2026-04-20)


### Bug Fixes

* bypass PgBouncer for EF Core startup migrations ([e445dcf](https://github.com/Bengo-Hub/truload-backend/commit/e445dcf6304eaffcfb44bc04af3555485937ef3c))
* remove redundant migration job — startup handles migrations ([b3128b5](https://github.com/Bengo-Hub/truload-backend/commit/b3128b5c283948ae0486c017fd24aaec6815ab18))
* use DROP INDEX IF EXISTS in commercial weighing migration ([4fd3b3e](https://github.com/Bengo-Hub/truload-backend/commit/4fd3b3edd8c4336d7af1a1ea2b24b4851abf5a22))

## [1.1.0](https://github.com/Bengo-Hub/truload-backend/compare/v1.0.29...v1.1.0) (2026-04-17)


### Features

* add commercial weighing backend (Sprint 1-2) ([109a9f1](https://github.com/Bengo-Hub/truload-backend/commit/109a9f1356ebbbce295cc2a3b4b59895b1502fa7))
* add transporter self-service portal backend (Sprint 4) ([076c109](https://github.com/Bengo-Hub/truload-backend/commit/076c109e4ce2fc54befd8c07f5e1700f15edcbde))


### Bug Fixes

* **deploy:** add port extraction for PgBouncer in migration script ([1b0bf41](https://github.com/Bengo-Hub/truload-backend/commit/1b0bf41d51b5d14a6a8525236950a3c59af63513))
* **deploy:** migration always bypasses PgBouncer, connects direct to PostgreSQL ([28bdcfb](https://github.com/Bengo-Hub/truload-backend/commit/28bdcfb3f553c5d826c6a106a45ab9450b030bf1))

## [1.0.29](https://github.com/Bengo-Hub/truload-backend/compare/v1.0.28...v1.0.29) (2026-04-15)


### Bug Fixes

* **backup:** install postgresql-client-17 so pg_dump matches server major ([a4a888f](https://github.com/Bengo-Hub/truload-backend/commit/a4a888f83022085bcc7dd0e2c76b9bb9c294b1da))

## [1.0.28](https://github.com/Bengo-Hub/truload-backend/compare/v1.0.27...v1.0.28) (2026-04-15)


### Bug Fixes

* **backup:** install postgresql-client-17 so pg_dump matches server major ([a4a888f](https://github.com/Bengo-Hub/truload-backend/commit/a4a888f83022085bcc7dd0e2c76b9bb9c294b1da))

## [1.0.27](https://github.com/Bengo-Hub/truload-backend/compare/v1.0.26...v1.0.27) (2026-04-14)


### Bug Fixes

* **backup:** point default backup storage path at /app/backups/truload PVC ([e8d9e24](https://github.com/Bengo-Hub/truload-backend/commit/e8d9e240e6a352a85ba44333111038b73b06539a))

## [Unreleased]

### Fixed (Sprint 22.1 - Production Bug Fixes - 2026-02-18)

#### Weighing Operations
- **DbContext concurrency error**: Replaced `Task.WhenAll` in `WeighingController.Create` with sequential calls to prevent "A second operation was started on this context instance" error
- **Document naming convention not applied**: `InitiateWeighingAsync` now calls `DocumentNumberService.GenerateNumberAsync()` instead of using frontend-provided ticket number (eliminates `MOB` prefix)
- **CaptureStatus lifecycle**: Set `CaptureStatus = "pending"` on transaction creation; transitions to `"captured"` when weights are submitted via `CaptureWeightsAsync` (handles both frontend-initiated and autoweigh flows)
- **DocumentSequence concurrency**: Added `[Timestamp] RowVersion` concurrency token to `DocumentSequence` model; added retry loop with `DbUpdateConcurrencyException` handling in `DocumentNumberService`

#### CORS & Error Handling
- **CORS on error responses**: Moved `app.UseCors()` before `UseExceptionHandler` in middleware pipeline so error responses include CORS headers
- **Disposition breakdown 500**: Added try-catch to `GetDispositionBreakdown` and `GetCaseTrend` endpoints in `CaseRegisterController`; added `ILogger` field for structured error logging
- **Driver creation 500**: Wrapped `DriverController.Create` in try-catch with proper error responses; added `Guid.NewGuid()` for empty IDs

#### PDF Documents
- **Logo sizing**: Increased logo dimensions from 80x65 to 100x80 points for better visibility; both left (KURA) and right (Coat of Arms) logos now render at equal size

### Added
- Weighing Operations module with transaction management
- Vehicle management with registration validation
- Yard management for vehicle tracking
- Case Management module for prosecutions
- User Management with ASP.NET Core Identity integration
- System configuration endpoints
- Authentication endpoints with JWT support
- Authorization middleware with permission-based policies
- Comprehensive repository pattern implementation
- Service layer with business logic separation
- DTOs for clean API contracts
- Validation using FluentValidation
- Global error handling middleware
- Health check endpoints for monitoring
- Database schema with comprehensive ERD documentation
- Support for multiple weighing modes (Static, WIM, Axle)
- EAC Vehicle Load Control Act (2016) compliance rules
- Kenya Traffic Act (Cap 403) compliance rules
- Reweigh workflow with cycle limits
- Special release processing
- Prosecution case workflows

### Planned
- ONNX model integration for analytics
- Superset dashboard integration
- TruConnect microservice integration for scale data
- RabbitMQ event publishing for background tasks
- Redis caching for hot read paths
- Background job processing for heavy operations
- Webhook support for external integrations
- Advanced reporting and analytics
- Audit trail enhancements
- Multi-language support
- SMS/Email notifications

## [0.2.0] - 2026-02-02

### Changed
- Upgraded to .NET 10 LTS from .NET 8 (January 2026)
- Upgraded Entity Framework Core to 10.0
- Upgraded Npgsql.EntityFrameworkCore.PostgreSQL to 10.0
- Updated all ASP.NET Core packages to 10.0
- Enhanced LINQ support with native left/right joins
- Improved performance with .NET 10 JIT optimizations
- Refined modular architecture with clear feature boundaries
- Updated documentation with current implementation status

### Added
- pgvector support for semantic search (7 tables with vector embeddings)
- Table partitioning for weighing_transactions (monthly range partitions)
- 6 materialized views for dashboard performance
- 8 regular views for real-time filtered data
- HNSW indexes for vector similarity search
- Integration with centralized devops-k8s repository
- Comprehensive .NET 10 upgrade analysis documentation
- Collaboration guidelines and coding standards
- Security policy and incident response procedures

## [0.1.0] - 2025-10-28

### Added
- Project initialization with .NET 8
- Basic modular folder structure
- Controllers for Auth, System, UserManagement, WeighingOperations, CaseManagement, Yard
- Database migrations setup
- Docker and Kubernetes configuration
- CI/CD pipeline via GitHub Actions
- Documentation framework (README, ERD, Implementation Plan)
- Build and deployment scripts
