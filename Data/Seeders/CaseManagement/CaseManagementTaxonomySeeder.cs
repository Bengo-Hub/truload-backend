using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models.CaseManagement;

namespace TruLoad.Backend.Data.Seeders.CaseManagement;

/// <summary>
/// Seeds all case management taxonomy tables with production-ready reference data.
/// Taxonomies include: CaseStatuses, DispositionTypes, ViolationTypes, ReleaseTypes,
/// ClosureTypes, HearingTypes, HearingStatuses, SubfileTypes, CaseReviewStatuses, WarrantStatuses.
/// </summary>
public static class CaseManagementTaxonomySeeder
{
    public static async Task SeedAsync(TruLoadDbContext context)
    {
        // Seed in dependency order
        await SeedCaseStatusesAsync(context);
        await SeedDispositionTypesAsync(context);
        await SeedViolationTypesAsync(context);
        await SeedReleaseTypesAsync(context);
        await SeedClosureTypesAsync(context);
        await SeedHearingTypesAsync(context);
        await SeedHearingStatusesAsync(context);
        await SeedHearingOutcomesAsync(context);
        await SeedSubfileTypesAsync(context);
        await SeedCaseReviewStatusesAsync(context);
        await SeedWarrantStatusesAsync(context);
    }

    private static async Task SeedCaseStatusesAsync(TruLoadDbContext context)
    {
        if (await context.CaseStatuses.AnyAsync()) return;

        var statuses = new List<CaseStatus>
        {
            new()
            {
                Code = "OPEN",
                Name = "Open",
                Description = "Case is actively being investigated. Initial status for all new cases.",
                IsActive = true
            },
            new()
            {
                Code = "PENDING",
                Name = "Pending",
                Description = "Case awaiting action or external input. Temporarily paused for missing information or external dependencies.",
                IsActive = true
            },
            new()
            {
                Code = "ESCALATED",
                Name = "Escalated",
                Description = "Case escalated to case manager for complex handling, legal review, or prosecution preparation.",
                IsActive = true
            },
            new()
            {
                Code = "IN_COURT",
                Name = "In Court",
                Description = "Case proceedings are underway in court. Active prosecution phase.",
                IsActive = true
            },
            new()
            {
                Code = "CLOSED",
                Name = "Closed",
                Description = "Case resolved and closed. Final status after disposition is complete.",
                IsActive = true
            },
            new()
            {
                Code = "ARCHIVED",
                Name = "Archived",
                Description = "Case closed and archived for long-term retention. Read-only status.",
                IsActive = true
            }
        };

        await context.CaseStatuses.AddRangeAsync(statuses);
        await context.SaveChangesAsync();
    }

    private static async Task SeedDispositionTypesAsync(TruLoadDbContext context)
    {
        if (await context.DispositionTypes.AnyAsync()) return;

        var dispositions = new List<DispositionType>
        {
            new()
            {
                Code = "PENDING",
                Name = "Pending",
                Description = "No disposition decision made yet. Awaiting investigation or case manager review.",
                IsActive = true
            },
            new()
            {
                Code = "SPECIAL_RELEASE",
                Name = "Special Release",
                Description = "Vehicle released under special conditions (redistribution, tolerance, administrative discretion). Fast-path resolution.",
                IsActive = true
            },
            new()
            {
                Code = "PAID",
                Name = "Paid & Released",
                Description = "Fines paid in full and vehicle released. Case closed without court intervention.",
                IsActive = true
            },
            new()
            {
                Code = "COURT_ESCALATION",
                Name = "Court Escalation",
                Description = "Case escalated to court for prosecution. Formal legal proceedings initiated.",
                IsActive = true
            },
            new()
            {
                Code = "COMPLIANCE_ACHIEVED",
                Name = "Compliance Achieved",
                Description = "Vehicle reweighed and found compliant. Load corrected, clearance certificate issued.",
                IsActive = true
            },
            new()
            {
                Code = "DISMISSED",
                Name = "Dismissed",
                Description = "Case dismissed due to procedural errors, lack of evidence, or administrative decision.",
                IsActive = true
            },
            new()
            {
                Code = "WITHDRAWN",
                Name = "Withdrawn",
                Description = "Case withdrawn by enforcement officer or supervisor before formal proceedings.",
                IsActive = true
            }
        };

        await context.DispositionTypes.AddRangeAsync(dispositions);
        await context.SaveChangesAsync();
    }

    private static async Task SeedViolationTypesAsync(TruLoadDbContext context)
    {
        if (await context.ViolationTypes.AnyAsync()) return;

        var violations = new List<ViolationType>
        {
            new()
            {
                Code = "OVERLOAD",
                Name = "Gross Vehicle Weight Overload",
                Description = "Vehicle's total weight exceeds permissible GVW limits. Most common violation in axle load control.",
                Severity = "high",
                IsActive = true
            },
            new()
            {
                Code = "AXLE_OVERLOAD",
                Name = "Single Axle Overload",
                Description = "One or more axles exceed individual axle weight limits despite compliant GVW.",
                Severity = "high",
                IsActive = true
            },
            new()
            {
                Code = "EXTREME_OVERLOAD",
                Name = "Extreme Overload",
                Description = "Vehicle exceeds permissible weight by more than 30%. Critical safety violation requiring immediate prohibition.",
                Severity = "critical",
                IsActive = true
            },
            new()
            {
                Code = "PERMIT_VIOLATION",
                Name = "Permit Violation",
                Description = "Operating without valid overload permit or exceeding permit conditions.",
                Severity = "high",
                IsActive = true
            },
            new()
            {
                Code = "DOCUMENT_FRAUD",
                Name = "Document Fraud",
                Description = "Falsified weighing certificates, permits, or compliance documents.",
                Severity = "critical",
                IsActive = true
            },
            new()
            {
                Code = "SCALE_EVASION",
                Name = "Scale Evasion",
                Description = "Deliberate avoidance of weighbridge station or refusal to be weighed.",
                Severity = "critical",
                IsActive = true
            },
            new()
            {
                Code = "REPEAT_VIOLATION",
                Name = "Repeat Violation",
                Description = "Vehicle or transporter with multiple overload violations within specified timeframe.",
                Severity = "high",
                IsActive = true
            },
            new()
            {
                Code = "UNAUTHORIZED_REDISTRIBUTION",
                Name = "Unauthorized Redistribution",
                Description = "Load redistribution or offloading without supervision or proper authorization.",
                Severity = "medium",
                IsActive = true
            },
            new()
            {
                Code = "SEAL_TAMPERING",
                Name = "Seal Tampering",
                Description = "Tampering with or breaking official seals placed on detained vehicles or cargo.",
                Severity = "critical",
                IsActive = true
            },
            new()
            {
                Code = "TAG",
                Name = "Vehicle Tag Violation",
                Description = "Vehicle held due to manual KeNHA tag. Administrative hold pending tag resolution.",
                Severity = "medium",
                IsActive = true
            },
            new()
            {
                Code = "OTHER",
                Name = "Other Violation",
                Description = "Miscellaneous traffic or load control violations not categorized above.",
                Severity = "medium",
                IsActive = true
            }
        };

        await context.ViolationTypes.AddRangeAsync(violations);
        await context.SaveChangesAsync();
    }

    private static async Task SeedReleaseTypesAsync(TruLoadDbContext context)
    {
        if (await context.ReleaseTypes.AnyAsync()) return;

        var releaseTypes = new List<ReleaseType>
        {
            new()
            {
                Code = "REDISTRIBUTION",
                Name = "Redistribution Release",
                Description = "Vehicle released for supervised load redistribution. Must reweigh to verify compliance before departure.",
                IsActive = true
            },
            new()
            {
                Code = "TOLERANCE",
                Name = "Tolerance Release",
                Description = "Vehicle overload within acceptable tolerance range (typically <5%). Released with warning.",
                IsActive = true
            },
            new()
            {
                Code = "PERMIT_VALID",
                Name = "Valid Permit Release",
                Description = "Vehicle has valid overload permit matching current configuration. Released with permit verification.",
                IsActive = true
            },
            new()
            {
                Code = "ADMIN_DISCRETION",
                Name = "Administrative Discretion",
                Description = "Supervisor discretionary release for exceptional circumstances (humanitarian, emergency, force majeure).",
                IsActive = true
            },
            new()
            {
                Code = "TECHNICAL_ERROR",
                Name = "Technical Error Release",
                Description = "Release due to scale malfunction, calibration issues, or procedural errors during weighing.",
                IsActive = true
            },
            new()
            {
                Code = "COURT_ORDER",
                Name = "Court Order Release",
                Description = "Vehicle released by court order or legal directive pending case resolution.",
                IsActive = true
            }
        };

        await context.ReleaseTypes.AddRangeAsync(releaseTypes);
        await context.SaveChangesAsync();
    }

    private static async Task SeedClosureTypesAsync(TruLoadDbContext context)
    {
        if (await context.ClosureTypes.AnyAsync()) return;

        var closureTypes = new List<ClosureType>
        {
            new()
            {
                Code = "CONVICTION",
                Name = "Conviction",
                Description = "Case closed with guilty verdict. Penalties imposed by court.",
                IsActive = true
            },
            new()
            {
                Code = "ACQUITTAL",
                Name = "Acquittal",
                Description = "Case closed with not guilty verdict. Defendant exonerated.",
                IsActive = true
            },
            new()
            {
                Code = "PLEA_BARGAIN",
                Name = "Plea Bargain",
                Description = "Case resolved through negotiated plea agreement. Reduced charges or penalties.",
                IsActive = true
            },
            new()
            {
                Code = "WITHDRAWN",
                Name = "Withdrawn",
                Description = "Prosecution withdrawn before trial. Insufficient evidence or procedural issues.",
                IsActive = true
            },
            new()
            {
                Code = "SETTLED",
                Name = "Settled Out of Court",
                Description = "Case settled through alternative dispute resolution or administrative settlement.",
                IsActive = true
            },
            new()
            {
                Code = "DISMISSED",
                Name = "Dismissed",
                Description = "Case dismissed by court. Technical grounds, lack of jurisdiction, or legal defects.",
                IsActive = true
            },
            new()
            {
                Code = "NOLLE_PROSEQUI",
                Name = "Nolle Prosequi",
                Description = "Prosecutor's decision not to pursue charges. Case closed without prejudice.",
                IsActive = true
            }
        };

        await context.ClosureTypes.AddRangeAsync(closureTypes);
        await context.SaveChangesAsync();
    }

    private static async Task SeedHearingTypesAsync(TruLoadDbContext context)
    {
        var hearingTypes = new List<HearingType>
        {
            new()
            {
                Code = "MENTION",
                Name = "Mention Hearing",
                Description = "Initial court appearance for case status, plea entry, and scheduling.",
                IsActive = true
            },
            new()
            {
                Code = "PLEA",
                Name = "Plea Hearing",
                Description = "Formal plea entry (guilty, not guilty, no contest).",
                IsActive = true
            },
            new()
            {
                Code = "TRIAL",
                Name = "Trial Hearing",
                Description = "Full trial proceedings with evidence presentation and witness examination.",
                IsActive = true
            },
            new()
            {
                Code = "SENTENCING",
                Name = "Sentencing Hearing",
                Description = "Imposition of penalties and sentence following conviction.",
                IsActive = true
            },
            new()
            {
                Code = "BAIL",
                Name = "Bail Hearing",
                Description = "Application and determination of bail conditions for defendant.",
                IsActive = true
            },
            new()
            {
                Code = "RULING",
                Name = "Ruling Hearing",
                Description = "Court ruling on motions, objections, or legal arguments.",
                IsActive = true
            },
            new()
            {
                Code = "REVIEW",
                Name = "Review Hearing",
                Description = "Periodic case review for progress assessment and directive issuance.",
                IsActive = true
            },
            new()
            {
                Code = "CONVICTION",
                Name = "Conviction",
                Description = "Court proceeding for formal conviction and recording of guilty verdict.",
                IsActive = true
            },
            new()
            {
                Code = "WARRANT_EXECUTION",
                Name = "Execution of Arrest Warrant",
                Description = "Hearing following execution of an arrest warrant against the defendant.",
                IsActive = true
            },
            new()
            {
                Code = "WARRANT_ISSUED",
                Name = "Warrant of Arrest Issued",
                Description = "Court session where a warrant of arrest is issued against the defendant.",
                IsActive = true
            },
            new()
            {
                Code = "PLEA_GUILTY",
                Name = "Plea of Guilty",
                Description = "Hearing where defendant enters a formal plea of guilty.",
                IsActive = true
            },
            new()
            {
                Code = "PLEA_NOT_GUILTY",
                Name = "Plea of Not Guilty",
                Description = "Hearing where defendant enters a formal plea of not guilty.",
                IsActive = true
            },
            new()
            {
                Code = "HEARING",
                Name = "Hearing",
                Description = "Generic hearing type for general court proceedings not categorized elsewhere.",
                IsActive = true
            },
            new()
            {
                Code = "PRE_TRIAL",
                Name = "Pre-Trial",
                Description = "Pre-trial conference for case management, disclosure, and trial preparation.",
                IsActive = true
            },
            new()
            {
                Code = "DEFENSE",
                Name = "Defense Hearing",
                Description = "Hearing for presentation of defense case, evidence, and witnesses.",
                IsActive = true
            },
            new()
            {
                Code = "JUDGMENT",
                Name = "Judgment",
                Description = "Court session for delivery of final judgment on the case.",
                IsActive = true
            }
        };

        var existingCodes = await context.HearingTypes
            .Select(h => h.Code)
            .ToListAsync();

        var newTypes = hearingTypes
            .Where(h => !existingCodes.Contains(h.Code))
            .ToList();

        if (newTypes.Count > 0)
        {
            await context.HearingTypes.AddRangeAsync(newTypes);
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedHearingStatusesAsync(TruLoadDbContext context)
    {
        if (await context.HearingStatuses.AnyAsync()) return;

        var statuses = new List<HearingStatus>
        {
            new()
            {
                Code = "SCHEDULED",
                Name = "Scheduled",
                Description = "Hearing scheduled and confirmed. Awaiting hearing date.",
                IsActive = true
            },
            new()
            {
                Code = "COMPLETED",
                Name = "Completed",
                Description = "Hearing successfully completed. Minutes recorded.",
                IsActive = true
            },
            new()
            {
                Code = "ADJOURNED",
                Name = "Adjourned",
                Description = "Hearing postponed to a future date. New date to be scheduled.",
                IsActive = true
            },
            new()
            {
                Code = "CANCELLED",
                Name = "Cancelled",
                Description = "Hearing cancelled. May be rescheduled or case may be resolved alternatively.",
                IsActive = true
            },
            new()
            {
                Code = "NO_SHOW",
                Name = "No Show",
                Description = "Defendant or key party failed to appear. Warrant may be issued.",
                IsActive = true
            }
        };

        await context.HearingStatuses.AddRangeAsync(statuses);
        await context.SaveChangesAsync();
    }

    private static async Task SeedHearingOutcomesAsync(TruLoadDbContext context)
    {
        if (await context.HearingOutcomes.AnyAsync()) return;

        var outcomes = new List<HearingOutcome>
        {
            new()
            {
                Code = "CONVICTED",
                Name = "Convicted",
                Description = "Defendant found guilty. Sentence and/or fine to be imposed.",
                IsActive = true
            },
            new()
            {
                Code = "ACQUITTED",
                Name = "Acquitted",
                Description = "Defendant found not guilty. Case dismissed on merits.",
                IsActive = true
            },
            new()
            {
                Code = "ADJOURNED",
                Name = "Adjourned",
                Description = "Hearing postponed to a future date for further proceedings.",
                IsActive = true
            },
            new()
            {
                Code = "DISMISSED",
                Name = "Dismissed",
                Description = "Case dismissed by court on procedural or jurisdictional grounds.",
                IsActive = true
            },
            new()
            {
                Code = "PLEA_ENTERED",
                Name = "Plea Entered",
                Description = "Defendant entered a plea (guilty or not guilty). Trial to proceed.",
                IsActive = true
            },
            new()
            {
                Code = "SENTENCED",
                Name = "Sentenced",
                Description = "Sentence pronounced following conviction. Fine, imprisonment, or both.",
                IsActive = true
            },
            new()
            {
                Code = "WITHDRAWN",
                Name = "Withdrawn",
                Description = "Prosecution withdrew charges before verdict.",
                IsActive = true
            }
        };

        await context.HearingOutcomes.AddRangeAsync(outcomes);
        await context.SaveChangesAsync();
    }

    private static async Task SeedSubfileTypesAsync(TruLoadDbContext context)
    {
        if (await context.SubfileTypes.AnyAsync()) return;

        var subfileTypes = new List<SubfileType>
        {
            new()
            {
                Code = "EVIDENCE",
                Name = "Evidence Documentation",
                Description = "Photos, videos, witness statements, and physical evidence documentation.",
                IsActive = true
            },
            new()
            {
                Code = "WEIGHING_RECORDS",
                Name = "Weighing Records",
                Description = "Weight tickets, scale calibration certificates, and axle measurement data.",
                IsActive = true
            },
            new()
            {
                Code = "VEHICLE_DOCS",
                Name = "Vehicle Documentation",
                Description = "Vehicle registration, insurance, roadworthiness certificates, and permits.",
                IsActive = true
            },
            new()
            {
                Code = "DRIVER_DOCS",
                Name = "Driver Documentation",
                Description = "Driver's license, NTAC card, and driver identification documents.",
                IsActive = true
            },
            new()
            {
                Code = "LEGAL_NOTICES",
                Name = "Legal Notices",
                Description = "Prohibition orders, summonses, court notices, and official correspondence.",
                IsActive = true
            },
            new()
            {
                Code = "PAYMENT_RECORDS",
                Name = "Payment Records",
                Description = "Receipts, invoices, fee schedules, and payment confirmation documentation.",
                IsActive = true
            },
            new()
            {
                Code = "CORRESPONDENCE",
                Name = "Correspondence",
                Description = "Letters, emails, memos, and other case-related communication.",
                IsActive = true
            },
            new()
            {
                Code = "COURT_FILINGS",
                Name = "Court Filings",
                Description = "Charge sheets, affidavits, motions, rulings, and court judgments.",
                IsActive = true
            }
        };

        await context.SubfileTypes.AddRangeAsync(subfileTypes);
        await context.SaveChangesAsync();
    }

    private static async Task SeedCaseReviewStatusesAsync(TruLoadDbContext context)
    {
        if (await context.CaseReviewStatuses.AnyAsync()) return;

        var statuses = new List<CaseReviewStatus>
        {
            new()
            {
                Code = "PENDING",
                Name = "Pending Review",
                Description = "Case review requested. Awaiting case manager evaluation.",
                IsActive = true
            },
            new()
            {
                Code = "IN_REVIEW",
                Name = "In Review",
                Description = "Case currently under active review by case manager or legal team.",
                IsActive = true
            },
            new()
            {
                Code = "APPROVED",
                Name = "Approved",
                Description = "Case review completed and approved for next action (prosecution, closure, etc.).",
                IsActive = true
            },
            new()
            {
                Code = "REJECTED",
                Name = "Rejected",
                Description = "Case review rejected. Case requires additional investigation or correction.",
                IsActive = true
            },
            new()
            {
                Code = "RETURNED",
                Name = "Returned for Action",
                Description = "Case returned to investigating officer for specific action or clarification.",
                IsActive = true
            }
        };

        await context.CaseReviewStatuses.AddRangeAsync(statuses);
        await context.SaveChangesAsync();
    }

    private static async Task SeedWarrantStatusesAsync(TruLoadDbContext context)
    {
        var statuses = new List<WarrantStatus>
        {
            new()
            {
                Code = "IN_FORCE",
                Name = "In Force",
                Description = "Warrant is active and enforceable.",
                IsActive = true
            },
            new()
            {
                Code = "EXECUTED",
                Name = "Executed",
                Description = "Warrant successfully executed. Defendant apprehended or compliance achieved.",
                IsActive = true
            },
            new()
            {
                Code = "RECALLED",
                Name = "Recalled",
                Description = "Warrant recalled by court. No longer enforceable.",
                IsActive = true
            },
            new()
            {
                Code = "EXPIRED",
                Name = "Expired",
                Description = "Warrant expired due to time limit or superseding order.",
                IsActive = true
            },
            new()
            {
                Code = "PENDING",
                Name = "Pending Execution",
                Description = "Warrant issued but not yet executed. Law enforcement actively pursuing.",
                IsActive = true
            },
            new()
            {
                Code = "LIFTED",
                Name = "Lifted",
                Description = "Court has lifted the warrant.",
                IsActive = true
            }
        };

        var existingCodes = await context.WarrantStatuses
            .Select(w => w.Code)
            .ToListAsync();

        // Update legacy ISSUED status to IN_FORCE if it exists
        var issuedStatus = await context.WarrantStatuses
            .FirstOrDefaultAsync(w => w.Code == "ISSUED");
        if (issuedStatus != null)
        {
            issuedStatus.Code = "IN_FORCE";
            issuedStatus.Name = "In Force";
            issuedStatus.Description = "Warrant is active and enforceable.";
            existingCodes.Remove("ISSUED");
            existingCodes.Add("IN_FORCE");
        }

        var newStatuses = statuses
            .Where(s => !existingCodes.Contains(s.Code))
            .ToList();

        if (newStatuses.Count > 0)
        {
            await context.WarrantStatuses.AddRangeAsync(newStatuses);
        }

        await context.SaveChangesAsync();
    }
}
