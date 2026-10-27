using System;
using System.Linq;
using ChainedBeasts.ModFoundation.Content;

namespace ChainedBeasts.ModFoundation.State;

internal enum ExternalOwnershipField { Level, Order }

/// <summary>Native generators index every registered definition, including unowned zero-rank entries.</summary>
internal static class ExternalOwnershipProjection
{
    internal static void EnsureDefaults(ContentRegistry registry,
        Func<ContentCategory, int, ExternalOwnershipField, bool> contains,
        Action<ContentCategory, int, ExternalOwnershipField, int> assign)
    {
        foreach (var definition in registry.Entries.Where(value => value.Storage == OwnershipStorage.ExternalCheckpoint))
            foreach (var field in new[] { ExternalOwnershipField.Level, ExternalOwnershipField.Order })
                if (!contains(definition.Category, definition.NumericId, field))
                    assign(definition.Category, definition.NumericId, field, 0);
    }

    internal static void Apply(PlayerContentState state, ContentRegistry registry,
        Action<ContentCategory, int, ExternalOwnershipField, int> assign)
    {
        // Validate before changing the destination. Never turn invalid saved identity
        // mappings into a partially cleared live ownership table.
        var active = StateProjection.Active(state, registry);
        foreach (var definition in registry.Entries.Where(value => value.Storage == OwnershipStorage.ExternalCheckpoint))
        {
            assign(definition.Category, definition.NumericId, ExternalOwnershipField.Level, 0);
            assign(definition.Category, definition.NumericId, ExternalOwnershipField.Order, 0);
        }
        foreach (var entry in active.Where(value => value.Value.Level > 0 &&
                     value.Value.Category is ContentCategory.Skill or ContentCategory.Affliction))
        {
            var owned = entry.Value;
            assign(owned.Category, owned.NumericId, ExternalOwnershipField.Level, owned.Level);
            assign(owned.Category, owned.NumericId, ExternalOwnershipField.Order, owned.Order);
        }
    }
}
