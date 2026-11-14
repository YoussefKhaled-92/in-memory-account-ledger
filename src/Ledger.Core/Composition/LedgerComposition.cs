using System.Collections.Generic;
using Ledger.Application;
using Ledger.Domain;
using Ledger.Infrastructure.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace Ledger.Core;

/// <summary>One private provider per ledger; singleton means per ledger, never process-wide.</summary>
internal static class LedgerComposition
{
    internal static ServiceProvider Build(
        IEnumerable<Account> accounts,
        IOverdraftFeePolicy feePolicy,
        IInterestPolicy interestPolicy,
        int windowDays,
        IEnumerable<IEventHandler>? additionalHandlers)
    {
        var services = new ServiceCollection();

        services.AddSingleton<LedgerSession>(_ => new LedgerSession(windowDays));
        services.AddSingleton<ILedgerJournal>(_ => new InMemoryLedgerJournal(accounts));
        services.AddSingleton<LedgerQueries>(provider => new LedgerQueries(
            provider.GetRequiredService<ILedgerJournal>(),
            provider.GetRequiredService<LedgerSession>().WindowDays));
        // Instance registrations remain caller-owned, including disposable client implementations.
        services.AddSingleton(feePolicy);
        services.AddSingleton(interestPolicy);
        services.AddSingleton<IEventHandler, PostCredit>();
        services.AddSingleton<IEventHandler, PostDebit>();
        services.AddSingleton<IEventHandler, AuthorizeFunds>();
        services.AddSingleton<IEventHandler, SettleAuthorization>();
        services.AddSingleton<IEventHandler, ReversePosting>();
        services.AddSingleton<IEventHandler, ReleaseAuthorization>();

        foreach (var handler in additionalHandlers ?? [])
        {
            services.AddSingleton(handler);
        }

        services.AddSingleton<EventHandlerRegistry>();
        services.AddSingleton<AssessOverdraftFees>();
        services.AddSingleton<CapitalizeInterest>();
        services.AddSingleton<ProcessEvent>();
        services.AddSingleton<FinalizeLedger>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }
}
