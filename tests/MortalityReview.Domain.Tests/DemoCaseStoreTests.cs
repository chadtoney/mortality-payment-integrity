using MortalityReview.Application;
using MortalityReview.Domain;

namespace MortalityReview.Domain.Tests;

public sealed class DemoCaseStoreTests
{
    [Fact]
    public void ResetProducesKnownSyntheticScenario()
    {
        var store = new DemoCaseStore(new DeterministicMatcher());

        var dashboard = store.GetDashboard();

        Assert.Equal(5, dashboard.EvidenceRecords);
        Assert.Equal(3, dashboard.OpenCases);
        Assert.Equal(2, dashboard.HighConfidenceCases);
        Assert.Equal(1, dashboard.NeedsReviewCases);
        Assert.Equal(5621m, dashboard.PostDeathPayments);
    }

    [Fact]
    public void ReviewerDecisionUpdatesCaseAndAppendsAuditEvent()
    {
        var store = new DemoCaseStore(new DeterministicMatcher());
        var caseId = store.GetCases()[0].CaseId;

        var updated = store.RecordDecision(
            caseId,
            InvestigationStatus.Escalated,
            "Request official vital record.");

        Assert.NotNull(updated);
        Assert.Equal(InvestigationStatus.Escalated, updated.Status);
        Assert.Equal(2, store.GetAuditEvents(caseId).Count);
        Assert.Contains(store.GetAuditEvents(caseId), item => item.Action == "Case marked Escalated");
    }

    [Fact]
    public void StoreDoesNotExposeBeneficiaryWriteOperation()
    {
        var publicMethods = typeof(DemoCaseStore).GetMethods()
            .Where(method => method.DeclaringType == typeof(DemoCaseStore))
            .Select(method => method.Name)
            .ToArray();

        Assert.DoesNotContain(publicMethods, method =>
            method.Contains("benefit", StringComparison.OrdinalIgnoreCase) ||
            method.Contains("payment", StringComparison.OrdinalIgnoreCase));
    }
}