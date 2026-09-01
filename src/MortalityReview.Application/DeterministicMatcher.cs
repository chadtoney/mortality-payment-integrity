using System.Globalization;
using System.Text;
using MortalityReview.Domain;

namespace MortalityReview.Application;

public sealed class DeterministicMatcher(decimal reviewThreshold = 0.65m) : IMatcher
{
    private const decimal FamilyNameWeight = 0.30m;
    private const decimal GivenNameWeight = 0.20m;
    private const decimal BirthDateWeight = 0.25m;
    private const decimal StateWeight = 0.10m;
    private const decimal SsnTokenWeight = 0.15m;

    public IReadOnlyList<MatchCandidate> FindCandidates(
        MortalityEvidence evidence,
        IEnumerable<BeneficiaryProjection> beneficiaries)
    {
        return beneficiaries
            .Select(beneficiary => Score(evidence, beneficiary))
            .Where(candidate => candidate.Score >= reviewThreshold && candidate.HasPostDeathPayment)
            .OrderByDescending(candidate => candidate.Score)
            .ToArray();
    }

    private static MatchCandidate Score(
        MortalityEvidence evidence,
        BeneficiaryProjection beneficiary)
    {
        var familySimilarity = Similarity(
            evidence.Person.FamilyName,
            beneficiary.Person.FamilyName);
        var givenSimilarity = BestGivenNameSimilarity(evidence.Person, beneficiary.Person);
        var birthDateSimilarity = evidence.Person.BirthDate is not null &&
                                  evidence.Person.BirthDate == beneficiary.Person.BirthDate
            ? 1m
            : 0m;
        var stateSimilarity = !string.IsNullOrWhiteSpace(evidence.Person.State) &&
                              Normalize(evidence.Person.State) == Normalize(beneficiary.Person.State)
            ? 1m
            : 0m;
        var ssnTokenSimilarity = !string.IsNullOrWhiteSpace(evidence.ProtectedSsnToken) &&
                                 evidence.ProtectedSsnToken == beneficiary.ProtectedSsnToken
            ? 1m
            : 0m;

        var contributions = new[]
        {
            Contribution("family-name", FamilyNameWeight, familySimilarity),
            Contribution("given-name-or-alias", GivenNameWeight, givenSimilarity),
            Contribution("birth-date", BirthDateWeight, birthDateSimilarity),
            Contribution("state", StateWeight, stateSimilarity),
            Contribution("protected-ssn-token", SsnTokenWeight, ssnTokenSimilarity)
        };
        var score = decimal.Round(contributions.Sum(item => item.Contribution), 3);
        var postDeathPayments = beneficiary.Payments
            .Where(payment => payment.PaymentDate > evidence.DeathDate)
            .ToArray();

        return new MatchCandidate(
            evidence,
            beneficiary,
            score,
            score >= 0.85m ? ConfidenceBand.High : ConfidenceBand.Moderate,
            contributions,
            postDeathPayments.Length > 0,
            postDeathPayments.Sum(payment => payment.Amount));
    }

    private static ScoreContribution Contribution(
        string feature,
        decimal weight,
        decimal similarity)
    {
        var contribution = decimal.Round(weight * similarity, 3);
        return new ScoreContribution(
            feature,
            weight,
            similarity,
            contribution,
            similarity == 1m ? "Exact normalized match" :
            similarity == 0m ? "No match" : "Fuzzy normalized match");
    }

    private static decimal BestGivenNameSimilarity(PersonIdentity left, PersonIdentity right)
    {
        var leftNames = new[] { left.GivenName }.Concat(left.Aliases ?? []);
        var rightNames = new[] { right.GivenName }.Concat(right.Aliases ?? []);

        return leftNames
            .SelectMany(leftName => rightNames.Select(rightName => Similarity(leftName, rightName)))
            .DefaultIfEmpty(0m)
            .Max();
    }

    private static decimal Similarity(string? left, string? right)
    {
        var normalizedLeft = Normalize(left);
        var normalizedRight = Normalize(right);

        if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
        {
            return 0m;
        }

        if (normalizedLeft == normalizedRight)
        {
            return 1m;
        }

        var distance = LevenshteinDistance(normalizedLeft, normalizedRight);
        return decimal.Round(
            1m - (decimal)distance / Math.Max(normalizedLeft.Length, normalizedRight.Length),
            3);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark &&
                char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();

        for (var leftIndex = 1; leftIndex <= left.Length; leftIndex++)
        {
            var current = new int[right.Length + 1];
            current[0] = leftIndex;

            for (var rightIndex = 1; rightIndex <= right.Length; rightIndex++)
            {
                var substitutionCost = left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1;
                current[rightIndex] = Math.Min(
                    Math.Min(current[rightIndex - 1] + 1, previous[rightIndex] + 1),
                    previous[rightIndex - 1] + substitutionCost);
            }

            previous = current;
        }

        return previous[right.Length];
    }
}