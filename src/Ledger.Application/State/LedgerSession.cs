using System;

namespace Ledger.Application;

/// <summary>Single-writer workflow state; not financial history or a wall-clock abstraction.</summary>
public sealed class LedgerSession
{
    public int WindowDays
    {
        get;
    }

    public int RecordedDay
    {
        get; internal set;
    } = 1;

    public int ClosedThrough
    {
        get; internal set;
    }

    public bool Finished
    {
        get; internal set;
    }

    public LedgerSession(int windowDays)
    {
        if (windowDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowDays));
        }

        WindowDays = windowDays;
    }
}
