using System;
using System.Collections.Generic;

namespace ChainedBeasts.ModFoundation.Logic;

/// <summary>One wrapper per native entry; multiple patches on that same wrapper are allowed.</summary>
public sealed class NativeEntryClaims<T>
{
    private readonly Dictionary<long, T> _entries = new();
    public void RequireCompatible(long address, T wrapper)
    {
        if (address == 0) throw new InvalidOperationException("A native patch entry cannot be null.");
        if (_entries.TryGetValue(address, out var existing) && !EqualityComparer<T>.Default.Equals(existing, wrapper))
            throw new InvalidOperationException($"Native entry 0x{address:x} already belongs to {existing}; refusing a second wrapper {wrapper}.");
    }
    // Call only after a successful patch installation; failed attempts own nothing.
    public void Commit(long address, T wrapper)
    {
        RequireCompatible(address, wrapper);
        _entries[address] = wrapper;
    }
}
