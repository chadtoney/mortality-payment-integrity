namespace MortalityReview.Domain;

public sealed record PersonIdentity(
    string GivenName,
    string FamilyName,
    DateOnly? BirthDate,
    string? State,
    string? MiddleName = null,
    string? Suffix = null,
    IReadOnlyList<string>? Aliases = null);

public sealed record MortalityEvidence(
    string EvidenceId,
    string SourceName,
    Uri SourceUri,
    DateTimeOffset PublishedAt,
    DateTimeOffset RetrievedAt,
    PersonIdentity Person,
    DateOnly DeathDate,
    string RawContentHash,
    string ExtractionMethod,
    string? ProtectedSsnToken = null);

public sealed record PaymentActivity(DateOnly PaymentDate, decimal Amount);

public sealed record BeneficiaryProjection(
    string BeneficiaryId,
    string? ProtectedSsnToken,
    PersonIdentity Person,
    string BenefitStatus,
    IReadOnlyList<PaymentActivity> Payments);

public sealed record ScoreContribution(
    string Feature,
    decimal Weight,
    decimal Similarity,
    decimal Contribution,
    string Explanation);

public enum ConfidenceBand
{
    Moderate,
    High
}

public sealed record MatchCandidate(
    MortalityEvidence Evidence,
    BeneficiaryProjection Beneficiary,
    decimal Score,
    ConfidenceBand ConfidenceBand,
    IReadOnlyList<ScoreContribution> Contributions,
    bool HasPostDeathPayment,
    decimal PostDeathPaymentAmount);

public enum InvestigationStatus
{
    New,
    InReview,
    Confirmed,
    Rejected,
    Escalated,
    NeedsMoreEvidence
}

public sealed record InvestigationCase(
    string CaseId,
    MatchCandidate Match,
    InvestigationStatus Status,
    DateTimeOffset CreatedAt,
    string MatcherVersion,
    string? ReviewerNote = null);

public sealed record AuditEvent(
    string EventId,
    string CaseId,
    DateTimeOffset OccurredAt,
    string Actor,
    string Action,
    string? Detail);