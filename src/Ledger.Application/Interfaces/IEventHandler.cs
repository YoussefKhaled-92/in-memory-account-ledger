using Ledger.Domain;

namespace Ledger.Application;

/// <summary>Application use case selected by operation kind. Instances must be deterministic and single-writer.</summary>
public interface IEventHandler
{
    EventKind Kind
    {
        get;
    }

    void Handle(FinancialEvent input, OperationContext context);
}
