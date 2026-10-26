using System;
using System.Collections.Generic;

namespace ChainedBeasts.ModFoundation.Logic;

/// <summary>Ephemeral reentrancy guard, not a persistence identity.</summary>
public sealed class MutationGate
{
    private readonly HashSet<string> _active = new(StringComparer.Ordinal);
    public sealed class Ticket
    {
        internal string Key;
        internal bool Owner;
        internal bool Completed;
        public int Before { get; internal set; }
    }
    public Ticket Begin(string key, int before) => new() { Key = key, Owner = _active.Add(key), Before = before };
    public bool Complete(Ticket ticket, int after, bool restoring = false, bool failed = false)
    {
        if (ticket is null || ticket.Completed) return false;
        ticket.Completed = true;
        if (!ticket.Owner) return false;
        _active.Remove(ticket.Key);
        return !restoring && !failed && ticket.Before != after;
    }
    public void Clear() => _active.Clear();
}

public static class HealthTransition
{
    public static int Scale(int current, int oldMaximum, int newMaximum, int attainableMaximum)
    {
        var limit = Math.Max(0, Math.Min(newMaximum, attainableMaximum));
        var proportional = Math.Round(Math.Max(0, current) * (double)Math.Max(0, newMaximum) / Math.Max(1, oldMaximum));
        return (int)Math.Min(limit, Math.Min(int.MaxValue, proportional));
    }
}

public static class SoloSessionPolicy
{
    public static bool Allows(bool isSoloMode, bool sessionExists, bool isHost, int clients, int localClients, int players) =>
        isSoloMode && sessionExists && isHost && clients == 1 && localClients == 1 && players == 1;
}
