using MortalityReview.Domain;

namespace MortalityReview.Application;

public interface IMatcher
{
    IReadOnlyList<MatchCandidate> FindCandidates(
        MortalityEvidence evidence,
        IEnumerable<BeneficiaryProjection> beneficiaries);
}