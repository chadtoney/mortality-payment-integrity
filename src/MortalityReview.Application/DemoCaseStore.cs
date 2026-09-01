using MortalityReview.Domain;

namespace MortalityReview.Application;

public sealed record DashboardSnapshot(
    int EvidenceRecords,
    int OpenCases,
    int HighConfidenceCases,
    int NeedsReviewCases,
    decimal PostDeathPayments,
    DateTimeOffset LastIngestionAt);

public sealed class DemoCaseStore
{
    private readonly object gate = new();
    private readonly IMatcher matcher;
    private List<InvestigationCase> cases = [];
    private List<AuditEvent> auditEvents = [];
    private int evidenceRecordCount;
    private DateTimeOffset lastIngestionAt;

    public DemoCaseStore(IMatcher matcher)
    {
        this.matcher = matcher;
        Reset();
    }

    public DashboardSnapshot GetDashboard()
    {
        lock (gate)
        {
            return new DashboardSnapshot(
                evidenceRecordCount,
                cases.Count(item => item.Status is InvestigationStatus.New or InvestigationStatus.InReview),
                cases.Count(item => item.Match.ConfidenceBand == ConfidenceBand.High),
                cases.Count(item => item.Match.ConfidenceBand == ConfidenceBand.Moderate),
                cases.Sum(item => item.Match.PostDeathPaymentAmount),
                lastIngestionAt);
        }
    }

    public IReadOnlyList<InvestigationCase> GetCases()
    {
        lock (gate)
        {
            return cases
                .OrderByDescending(item => item.Match.Score)
                .ThenBy(item => item.CaseId)
                .ToArray();
        }
    }

    public InvestigationCase? GetCase(string caseId)
    {
        lock (gate)
        {
            return cases.SingleOrDefault(item => item.CaseId == caseId);
        }
    }

    public IReadOnlyList<AuditEvent> GetAuditEvents(string caseId)
    {
        lock (gate)
        {
            return auditEvents
                .Where(item => item.CaseId == caseId)
                .OrderByDescending(item => item.OccurredAt)
                .ToArray();
        }
    }

    public InvestigationCase? RecordDecision(
        string caseId,
        InvestigationStatus status,
        string? note,
        string actor = "demo.investigator")
    {
        if (status is InvestigationStatus.New or InvestigationStatus.InReview)
        {
            throw new ArgumentOutOfRangeException(nameof(status), "A reviewer decision is required.");
        }

        lock (gate)
        {
            var index = cases.FindIndex(item => item.CaseId == caseId);
            if (index < 0)
            {
                return null;
            }

            var current = cases[index];
            var updated = current with { Status = status, ReviewerNote = note };
            cases[index] = updated;
            auditEvents.Add(new AuditEvent(
                $"audit-{auditEvents.Count + 1:000}",
                caseId,
                DateTimeOffset.UtcNow,
                actor,
                $"Case marked {status}",
                note));

            return updated;
        }
    }

    public DashboardSnapshot Reset()
    {
        var evidence = CreateEvidence();
        var beneficiaries = CreateBeneficiaries();
        var generatedCases = evidence
            .SelectMany(item => matcher.FindCandidates(item, beneficiaries))
            .Select((candidate, index) => new InvestigationCase(
                $"MR-2026-{index + 1:000}",
                candidate,
                InvestigationStatus.New,
                new DateTimeOffset(2026, 8, 31, 13, index, 0, TimeSpan.Zero),
                "deterministic-v1"))
            .ToList();

        lock (gate)
        {
            cases = generatedCases;
            evidenceRecordCount = evidence.Count;
            lastIngestionAt = new DateTimeOffset(2026, 8, 31, 13, 5, 0, TimeSpan.Zero);
            auditEvents = cases
                .Select((item, index) => new AuditEvent(
                    $"audit-{index + 1:000}",
                    item.CaseId,
                    item.CreatedAt,
                    "system.matcher",
                    "Investigation case created",
                    "Synthetic demonstration data"))
                .ToList();

            return GetDashboard();
        }
    }

    private static IReadOnlyList<MortalityEvidence> CreateEvidence()
    {
        return
        [
            Evidence("ev-001", "Elena", "Marin", new DateOnly(1948, 2, 17), "VA", new DateOnly(2026, 7, 12), "notices/001"),
            Evidence("ev-002", "Jon", "Whitaker", new DateOnly(1952, 9, 4), "MD", new DateOnly(2026, 7, 19), "notices/002"),
            Evidence("ev-003", "Amara", "Oneal", new DateOnly(1946, 11, 23), "DC", new DateOnly(2026, 8, 2), "notices/003", "token-demo-003"),
            Evidence("ev-004", "Rina", "Patel", new DateOnly(1950, 6, 8), "VA", new DateOnly(2026, 8, 12), "notices/004"),
            Evidence("ev-005", "Olivia", "Chen", new DateOnly(1939, 5, 30), "CA", new DateOnly(2026, 8, 7), "notices/005")
        ];
    }

    private static IReadOnlyList<BeneficiaryProjection> CreateBeneficiaries()
    {
        return
        [
            Beneficiary("bn-1042", "Elena", "Marin", new DateOnly(1948, 2, 17), "VA", null,
                new PaymentActivity(new DateOnly(2026, 7, 3), 1842m),
                new PaymentActivity(new DateOnly(2026, 8, 3), 1842m)),
            Beneficiary("bn-2088", "John", "Whitaker", new DateOnly(1952, 9, 4), "MD", null,
                new PaymentActivity(new DateOnly(2026, 8, 3), 1675m)),
            Beneficiary("bn-3177", "Amara", "O'Neal", new DateOnly(1946, 11, 23), "DC", "token-demo-003",
                new PaymentActivity(new DateOnly(2026, 8, 3), 2104m)),
            Beneficiary("bn-4220", "Rina", "Patel", new DateOnly(1950, 6, 8), "VA", null,
                new PaymentActivity(new DateOnly(2026, 8, 3), 1900m)),
            Beneficiary("bn-5519", "Olivia", "Chen", new DateOnly(1970, 5, 30), "CA", null,
                new PaymentActivity(new DateOnly(2026, 8, 20), 1510m)),
            Beneficiary("bn-6001", "Noah", "Williams", new DateOnly(1960, 3, 15), "TX", null,
                new PaymentActivity(new DateOnly(2026, 8, 3), 1430m))
        ];
    }

    private static MortalityEvidence Evidence(
        string evidenceId,
        string givenName,
        string familyName,
        DateOnly birthDate,
        string state,
        DateOnly deathDate,
        string path,
        string? protectedSsnToken = null)
    {
        return new MortalityEvidence(
            evidenceId,
            "Approved demonstration feed",
            new Uri($"https://fixtures.example.gov/{path}"),
            new DateTimeOffset(2026, 8, 30, 14, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 31, 13, 5, 0, TimeSpan.Zero),
            new PersonIdentity(givenName, familyName, birthDate, state),
            deathDate,
            $"sha256:synthetic-{evidenceId}",
            "fixture-v1",
            protectedSsnToken);
    }

    private static BeneficiaryProjection Beneficiary(
        string beneficiaryId,
        string givenName,
        string familyName,
        DateOnly birthDate,
        string state,
        string? protectedSsnToken,
        params PaymentActivity[] payments)
    {
        return new BeneficiaryProjection(
            beneficiaryId,
            protectedSsnToken,
            new PersonIdentity(givenName, familyName, birthDate, state),
            "Active",
            payments);
    }
}