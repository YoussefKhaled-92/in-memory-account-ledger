using System;

namespace Ledger.Domain;

public sealed class LedgerRuleException(string message) : Exception(message);
