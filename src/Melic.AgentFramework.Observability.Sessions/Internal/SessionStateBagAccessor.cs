// SPDX-License-Identifier: MIT
using Microsoft.Agents.AI;

namespace Melic.AgentFramework.Observability.Sessions.Internal;

/// <summary>
/// Thread-safe helpers for reading and writing <see cref="SessionStateBlock"/> in an <see cref="AgentSessionStateBag"/>.
/// </summary>
internal static class SessionStateBagAccessor
{
    // Per-StateBag lock objects keyed by bag identity to prevent concurrent read-check-write races.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<AgentSessionStateBag, object> _locks = new();

    /// <summary>
    /// Returns the existing <see cref="SessionStateBlock"/> from the bag, or a freshly initialised default
    /// if no block exists yet. Thread-safe: uses a per-bag lock to prevent concurrent initialisation races.
    /// </summary>
    internal static SessionStateBlock GetOrInitialise(AgentSessionStateBag stateBag, string key)
    {
        var lockObj = _locks.GetOrCreateValue(stateBag);
        lock (lockObj)
        {
            if (stateBag.TryGetValue<SessionStateBlock>(key, out var existing) && existing is not null)
            {
                return existing;
            }

            var fresh = new SessionStateBlock();
            stateBag.SetValue(key, fresh);
            return fresh;
        }
    }

    /// <summary>
    /// Writes <paramref name="block"/> to the bag under <paramref name="key"/>.
    /// Returns <see langword="true"/> on success, <see langword="false"/> if an exception occurs (exception is swallowed).
    /// </summary>
    internal static bool TryPersist(AgentSessionStateBag stateBag, string key, SessionStateBlock block)
    {
        try
        {
            stateBag.SetValue(key, block);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to read a <see cref="SessionStateBlock"/> from the bag.
    /// Returns <see langword="true"/> and sets <paramref name="block"/> when successful.
    /// </summary>
    internal static bool TryRead(AgentSessionStateBag stateBag, string key, out SessionStateBlock? block)
    {
        if (stateBag.TryGetValue<SessionStateBlock>(key, out block) && block is not null)
        {
            return true;
        }

        block = null;
        return false;
    }
}
