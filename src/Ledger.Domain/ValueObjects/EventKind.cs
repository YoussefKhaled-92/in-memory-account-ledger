namespace Ledger.Domain;

/// <summary>Open operation identifier. New handlers can introduce a kind without editing this type.</summary>
public readonly record struct EventKind(string Name)
{
    public static readonly EventKind Credit = new("Credit");

    public static readonly EventKind Debit = new("Debit");

    public static readonly EventKind Authorization = new("Authorization");

    public static readonly EventKind Settlement = new("Settlement");

    public static readonly EventKind Reversal = new("Reversal");

    public static readonly EventKind Release = new("Release");

    public override string ToString() => Name ?? "";
}
