using System;
using System.Collections.Generic;

namespace ChainedBeasts.ModFoundation.Logic;

/// <summary>Addresses are transient lookup aliases. Serializable tokens are independently generated, never hashes of an address.</summary>
public sealed class SpawnTokenMap<T>
{
    private readonly Dictionary<T, (ulong Generation, string Token)> _tokens = new();
    public string Get(T alias, ulong generation)
    {
        if (_tokens.TryGetValue(alias, out var entry) && entry.Generation == generation) return entry.Token;
        var token = Guid.NewGuid().ToString("N"); _tokens[alias] = (generation, token); return token;
    }
    public void Despawn(T alias) => _tokens.Remove(alias);
    public void Clear() => _tokens.Clear();
}
