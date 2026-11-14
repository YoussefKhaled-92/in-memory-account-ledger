namespace Ledger.Domain;

public enum EntryKind
{
    Credit,
    Debit,
    Settlement,
    Reversal,
    OverdraftFee,
    Interest
}
