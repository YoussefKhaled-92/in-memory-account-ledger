using System;
using System.Linq;
using Ledger.Domain;

namespace Ledger.Application;

/// <summary>Arrival-order coordinator. Ordering here is an observable business contract.</summary>
public sealed class ProcessEvent(
    ILedgerJournal journal,
    LedgerQueries queries,
    LedgerSession session,
    EventHandlerRegistry handlers,
    AssessOverdraftFees fees)
{
    public EventResult Execute(FinancialEvent input)
    {
        EventResult? prior = journal.Results.FirstOrDefault(r => r.Input.Id == input.Id);

        if (prior != null)
        {
            if (prior.Input != input)
            {
                throw new LedgerRuleException("Event ID reused with different data.");
            }

            return prior;
        }

        if (session.Finished)
        {
            throw new LedgerRuleException("Window is finalized; new input is forbidden.");
        }

        if (string.IsNullOrWhiteSpace(input.Id))
        {
            throw new LedgerRuleException("Event ID is required.");
        }

        if (input.Id.StartsWith("FEE:", StringComparison.Ordinal)
            || input.Id.StartsWith("INTEREST:", StringComparison.Ordinal))
        {
            throw new LedgerRuleException("Event ID uses a reserved system prefix.");
        }

        queries.ValidateDay(input.BookedDay);
        queries.ValidateDay(input.ValueDay);

        // Close earlier days before looking up the account or dispatching, even on a decline.
        if (input.BookedDay > session.RecordedDay)
        {
            session.RecordedDay = input.BookedDay;
            fees.Execute(input.BookedDay - 1, session.RecordedDay);
            session.ClosedThrough = input.BookedDay - 1;
        }

        try
        {
            handlers.Dispatch(input, new OperationContext(
                queries.GetAccount(input.AccountId),
                session.RecordedDay,
                journal,
                queries));
        }
        catch (LedgerRuleException exception)
        {
            return journal.AppendResult(input, session.RecordedDay, false, exception.Message);
        }

        EventResult result = journal.AppendResult(input, session.RecordedDay, true, null);

        // Policy/runtime faults here abort explicitly after the accepted outcome, as before.
        fees.Execute(session.ClosedThrough, session.RecordedDay);

        return result;
    }
}
