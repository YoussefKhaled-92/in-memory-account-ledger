using System.Linq;
using Ledger.Core;
using Ledger.Domain;
using Xunit;

public class AuthorizationExpiryTests
{
    // Deliberate real failure: Finish is a fee/interest watermark, not an expiry scheduler.
    // Production needs a network-specific deadline and an append-only Expired transition.
    // Runs with all other tests: this required design limitation must remain a real failure.
    [Fact]
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
