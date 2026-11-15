using System.Linq;
using Ledger.Core;
using Ledger.Domain;
using Xunit;

public class AuthorizationExpiryTests
{
    // Deliberate real failure: Finish is a fee/interest watermark, not an expiry scheduler.
    // Production needs a network-specific deadline and an append-only Expired transition.
    // This project is outside the standard solution; run it explicitly. Never Skip this test.
    [Fact]
    [Trait("Category", "KnownFailure")]
    public void Abandoned_authorization_should_eventually_expire()
    {
        using var ledger = AssessmentScenario.Create();
        foreach (var input in AssessmentScenario.Events.Take(3))
        {
            ledger.Process(input);
        }
        ledger.Finish();
        Assert.Equal(0, ledger.CurrentAuthorizations("ACC-001").Count(a => a.Status == AuthorizationStatus.Active));
    }
}
