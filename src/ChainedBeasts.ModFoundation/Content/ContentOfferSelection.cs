using System;
using System.Collections.Generic;
using System.Linq;

namespace ChainedBeasts.ModFoundation.Content;

/// <summary>Offer preferences are separate from registered mechanics and saved ownership.</summary>
public sealed class ContentOfferSelection
{
    private readonly Dictionary<string, bool> _local = new(StringComparer.Ordinal);
    private Dictionary<string, bool> _session;
    public bool SessionLocked => _session is not null;
    public void Register(string key, bool enabled = true)
    {
        if (SessionLocked || string.IsNullOrWhiteSpace(key) || key.Length > 256 || _local.Count >= 512 || !_local.TryAdd(key, enabled))
            throw new InvalidOperationException("Invalid or duplicate offer preference.");
    }
    public bool Allows(string key) => !(_session ?? _local).TryGetValue(key, out var enabled) || enabled;
    public Dictionary<string, bool> Local() => new(_local, StringComparer.Ordinal);
    public Dictionary<string, bool> Effective() => new(_session ?? _local, StringComparer.Ordinal);
    public void Validate(IReadOnlyDictionary<string, bool> values)
    {
        if (values is null || values.Count != _local.Count || values.Keys.Any(key => !_local.ContainsKey(key)))
            throw new InvalidOperationException("Host offer selection does not match installed content.");
    }
    public void SaveLocal(IReadOnlyDictionary<string, bool> values, Action persist)
    {
        if (SessionLocked) throw new InvalidOperationException("Leave the party before changing offer preferences.");
        Validate(values);
        var next = new Dictionary<string, bool>(values, StringComparer.Ordinal);
        persist?.Invoke(); // Failed disk writes must not partially change active settings.
        foreach (var item in next) _local[item.Key] = item.Value;
    }
    public void AdoptHost(IReadOnlyDictionary<string, bool> values)
    {
        Validate(values);
        if (_session is not null && values.Any(item => _session[item.Key] != item.Value))
            throw new InvalidOperationException("Host offer selection cannot change during a session.");
        _session = new(values, StringComparer.Ordinal);
    }
    public void EndSession() => _session = null;
}
