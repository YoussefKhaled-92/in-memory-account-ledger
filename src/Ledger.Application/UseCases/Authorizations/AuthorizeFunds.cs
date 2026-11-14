using System.Linq;
using Ledger.Domain;

namespace Ledger.Application;

public sealed class AuthorizeFunds : IEventHandler
{
    public EventKind Kind => EventKind.Authorization;

    public void Handle(FinancialEvent input, OperationContext context)
    {
        PostingRules.RequireReference(input);

        if (context.Queries.CurrentAuthorizations(context.Account.Id).Any(c => c.AuthorizationId == input.Reference))
        {
            throw new LedgerRuleException("Authorization ID already used for this account.");
        }

        decimal amount = PostingRules.PositiveAmount(input, context.Account);

        if (!Authorization.CanReserve(context.Queries.Available(context.Account.Id, context.RecordedDay), amount))
        {
            context.Transition(input, amount, AuthorizationStatus.Rejected);

            throw new LedgerRuleException("Insufficient available balance for authorization.");
        }

        context.Transition(input, amount, AuthorizationStatus.Active);
    }
}
