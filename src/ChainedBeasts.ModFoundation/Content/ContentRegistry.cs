using System;
using System.Collections.Generic;
using System.Linq;

namespace ChainedBeasts.ModFoundation.Content;

public enum ContentCategory { Skill, Equipment, Affliction, Surgery }
public enum OwnershipStorage { ExternalCheckpoint, Native }

public sealed class LegacyMarker
{
    public ContentCategory ArrayCategory { get; }
    public int NativeMarkerId { get; }
    public LegacyMarker(ContentCategory arrayCategory, int nativeMarkerId) { ArrayCategory = arrayCategory; NativeMarkerId = nativeMarkerId; }
    public override bool Equals(object other) => other is LegacyMarker value && value.ArrayCategory == ArrayCategory && value.NativeMarkerId == NativeMarkerId;
    public override int GetHashCode() => HashCode.Combine(ArrayCategory, NativeMarkerId);
}

public sealed class ContentDescriptor
{
    public string Key { get; }
    public string OwnerGuid { get; }
    public ContentCategory Category { get; }
    public int NumericId { get; }
    public int MaximumLevel { get; }
    public bool Enabled { get; }
    public LegacyMarker Legacy { get; }
    public ContentDescriptor(string key, string ownerGuid, ContentCategory category, int numericId, int maximumLevel,
        bool enabled = true, LegacyMarker legacy = null)
    { Key = key; OwnerGuid = ownerGuid; Category = category; NumericId = numericId; MaximumLevel = maximumLevel; Enabled = enabled; Legacy = legacy; }
    public OwnershipStorage Storage => Category is ContentCategory.Skill or ContentCategory.Affliction
        ? OwnershipStorage.ExternalCheckpoint : OwnershipStorage.Native;
}

/// <summary>Stable identities; registration order is never a save key.</summary>
public sealed class ContentRegistry
{
    private readonly Dictionary<string, ContentDescriptor> _keys = new(StringComparer.Ordinal);
    private readonly Dictionary<(ContentCategory, int), ContentDescriptor> _ids = new();
    private readonly Func<ContentCategory, int, bool> _isNative;

    public ContentRegistry(Func<ContentCategory, int, bool> isNative = null) => _isNative = isNative;
    public IReadOnlyCollection<ContentDescriptor> Entries => _keys.Values.ToArray();

    public void RegisterMany(IEnumerable<ContentDescriptor> definitions)
    {
        var incoming = definitions?.ToArray() ?? throw new ArgumentNullException(nameof(definitions));
        // Startup registration is synchronous. Validate the entire batch before
        // publishing any claim, so a failed plugin cannot leave orphan offer IDs.
        var preview = new ContentRegistry(_isNative);
        foreach (var existing in _keys.Values) preview.Register(existing);
        foreach (var definition in incoming) preview.Register(definition);
        foreach (var definition in incoming)
        { _keys.Add(definition.Key, definition); _ids.Add((definition.Category, definition.NumericId), definition); }
    }

    public void Register(ContentDescriptor definition)
    {
        if (definition is null || string.IsNullOrWhiteSpace(definition.Key) ||
            !definition.Key.Contains(':') || definition.Key.Any(char.IsWhiteSpace) ||
            string.IsNullOrWhiteSpace(definition.OwnerGuid) || definition.NumericId < 0)
            throw new ArgumentException("Content requires a namespaced key, owner GUID, and nonnegative numeric ID.");
        if (!Enum.IsDefined(typeof(ContentCategory), definition.Category)) throw new ArgumentException("Unknown content category.");
        if (definition.Legacy is not null && (definition.Legacy.ArrayCategory != definition.Category ||
            definition.Storage != OwnershipStorage.ExternalCheckpoint))
            throw new ArgumentException("Legacy markers must match an external ownership category.");
        var maximum = definition.Category == ContentCategory.Skill ? 4 :
            definition.Category == ContentCategory.Affliction ? 3 : 1;
        if (definition.MaximumLevel < 1 || definition.MaximumLevel > maximum)
            throw new ArgumentOutOfRangeException(nameof(definition), "Invalid category rank/severity limit.");
        if (_keys.ContainsKey(definition.Key)) throw new InvalidOperationException("Content key collision: " + definition.Key);
        if (_ids.TryGetValue((definition.Category, definition.NumericId), out var other))
            throw new InvalidOperationException($"Content ID collision: {definition.Key} and {other.Key}");
        if (_isNative?.Invoke(definition.Category, definition.NumericId) == true)
            throw new InvalidOperationException($"Native ID collision: {definition.Category} {definition.NumericId}");
        if (definition.Legacy is not null && _keys.Values.Any(value => definition.Legacy.Equals(value.Legacy)))
            throw new InvalidOperationException("Legacy marker is already claimed by another content key.");
        _keys.Add(definition.Key, definition);
        _ids.Add((definition.Category, definition.NumericId), definition);
    }

    public bool TryGet(string key, out ContentDescriptor definition) => _keys.TryGetValue(key, out definition);
    public bool TryGet(ContentCategory category, int id, out ContentDescriptor definition) => _ids.TryGetValue((category, id), out definition);
    public void Disable(string key)
    {
        if (!_keys.TryGetValue(key, out var definition)) return;
        var disabled = new ContentDescriptor(definition.Key, definition.OwnerGuid, definition.Category,
            definition.NumericId, definition.MaximumLevel, false, definition.Legacy);
        _keys[key] = disabled;
        _ids[(definition.Category, definition.NumericId)] = disabled;
    }
}
