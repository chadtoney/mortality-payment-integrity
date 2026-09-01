using MortalityReview.Application;
using MortalityReview.Domain;

namespace MortalityReview.Domain.Tests;

public sealed class DeterministicMatcherTests
{
    private static readonly DateOnly DeathDate = new(2026, 7, 12);

    [Fact]
    public void ExactIdentityWithLaterPaymentCreatesHighConfidenceCandidate()
    {
        var evidence = Evidence("Elena", "Marin", new DateOnly(1948, 2, 17), "VA");
        var beneficiary = Beneficiary(
            "Elena",
            "Marin",
            new DateOnly(1948, 2, 17),
            "VA",
            new PaymentActivity(new DateOnly(2026, 8, 1), 1842m));

        var candidate = Assert.Single(new DeterministicMatcher().FindCandidates(evidence, [beneficiary]));

        Assert.Equal(0.85m, candidate.Score);
        Assert.Equal(ConfidenceBand.High, candidate.ConfidenceBand);
        Assert.Equal(1842m, candidate.PostDeathPaymentAmount);
        Assert.Equal(5, candidate.Contributions.Count);
    }

    [Fact]
    public void MinorNameVariationProducesExplainableReviewCandidate()
    {
        var evidence = Evidence("Jon", "Whitaker", new DateOnly(1952, 9, 4), "MD");
        var beneficiary = Beneficiary(
            "John",
            "Whitaker",
            new DateOnly(1952, 9, 4),
            "MD",
            new PaymentActivity(new DateOnly(2026, 7, 31), 1675m));

        var candidate = Assert.Single(new DeterministicMatcher().FindCandidates(evidence, [beneficiary]));

        Assert.Equal(ConfidenceBand.Moderate, candidate.ConfidenceBand);
        Assert.Contains(candidate.Contributions, item =>
            item.Feature == "given-name-or-alias" && item.Explanation == "Fuzzy normalized match");
    }

    [Fact]
    public void DifferentBirthDateDoesNotCrossReviewThreshold()
    {
        var evidence = Evidence("Marcus", "Lee", new DateOnly(1940, 1, 1), "DC");
        var beneficiary = Beneficiary(
            "Marcus",
            "Lee",
            new DateOnly(1978, 1, 1),
            "DC",
            new PaymentActivity(new DateOnly(2026, 8, 1), 1400m));

        var candidates = new DeterministicMatcher().FindCandidates(evidence, [beneficiary]);

        Assert.Empty(candidates);
    }

    [Fact]
    public void PaymentBeforeDeathDoesNotCreateCaseCandidate()
    {
        var evidence = Evidence("Rina", "Patel", new DateOnly(1950, 6, 8), "VA");
        var beneficiary = Beneficiary(
            "Rina",
            "Patel",
            new DateOnly(1950, 6, 8),
            "VA",
            new PaymentActivity(new DateOnly(2026, 7, 1), 1900m));

        var candidates = new DeterministicMatcher().FindCandidates(evidence, [beneficiary]);

        Assert.Empty(candidates);
    }

    private static MortalityEvidence Evidence(
        string givenName,
        string familyName,
        DateOnly birthDate,
        string state)
    {
        return new MortalityEvidence(
            "evidence-001",
            "Approved demonstration feed",
            new Uri("https://fixtures.example.gov/notices/001"),
            new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 14, 12, 5, 0, TimeSpan.Zero),
            new PersonIdentity(givenName, familyName, birthDate, state),
            DeathDate,
            "sha256:demonstration-only",
            "fixture-v1");
    }

    private static BeneficiaryProjection Beneficiary(
        string givenName,
        string familyName,
        DateOnly birthDate,
        string state,
        params PaymentActivity[] payments)
    {
        return new BeneficiaryProjection(
            "beneficiary-001",
            null,
            new PersonIdentity(givenName, familyName, birthDate, state),
            "Active",
            payments);
    }
}