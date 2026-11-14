namespace Ledger.Application;

/// <summary>Final-input watermark. Does not expire holds or reopen a finalized window.</summary>
public sealed class FinalizeLedger(LedgerSession session, AssessOverdraftFees fees, CapitalizeInterest interest)
{
    public void Execute()
    {
        if (session.Finished)
        {
            return;
        }

        session.RecordedDay = session.WindowDays;
        fees.Execute(session.WindowDays, session.RecordedDay);
        session.ClosedThrough = session.WindowDays;
        interest.Execute(session.WindowDays, session.RecordedDay);
        session.Finished = true;
    }
}
