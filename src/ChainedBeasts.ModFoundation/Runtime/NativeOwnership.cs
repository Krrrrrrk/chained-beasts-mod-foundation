using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ChainedBeasts.ModFoundation.Content;
using ChainedBeasts.ModFoundation.Logic;

namespace ChainedBeasts.ModFoundation.Runtime;

internal static class NativeOwnership
{
    private static readonly MutationGate Gate = new();
    internal sealed class ChangeState
    {
        internal MutationGate.Ticket Ticket;
        internal ContentDescriptor Definition;
        internal PlayerData Data;
        internal int Health;
        internal int RuntimeHealth;
        internal bool HasRuntimeHealth;
        internal int Maximum;
    }
    internal static void Install(Harmony harmony)
    {
        foreach (var add in new[] { true, false })
        {
            FeaturePatcher.Patch(harmony, typeof(NativeOwnership),
                FeaturePatcher.RequireMethod(typeof(PlayerData), add ? nameof(PlayerData.AddSkill) : nameof(PlayerData.RemoveSkill), typeof(SkillType), typeof(int)),
                prefix: add ? nameof(AddSkillBefore) : nameof(RemoveSkillBefore), postfix: nameof(After), finalizer: nameof(Finish));
            FeaturePatcher.Patch(harmony, typeof(NativeOwnership),
                FeaturePatcher.RequireMethod(typeof(PlayerData), add ? nameof(PlayerData.AddInjury) : nameof(PlayerData.RemoveInjury), typeof(InjuryType), typeof(int)),
                prefix: add ? nameof(AddInjuryBefore) : nameof(RemoveInjuryBefore), postfix: nameof(After), finalizer: nameof(Finish));
        }
        FeaturePatcher.Patch(harmony, typeof(NativeOwnership),
            FeaturePatcher.RequireMethod(typeof(Player), nameof(Player.ReceiveEquipmentRPC), typeof(PurchaseType)),
            prefix: nameof(EquipmentBefore), postfix: nameof(EquipmentAfter));
        FeaturePatcher.Patch(harmony, typeof(NativeOwnership),
            FeaturePatcher.RequireMethod(typeof(PlayerData), nameof(PlayerData.RemoveAllInjuries)),
            prefix: nameof(BulkBefore), postfix: nameof(BulkAfter), finalizer: nameof(BulkFinally));
    }

    private static bool AddSkillBefore(PlayerData __instance, SkillType skillType, ref int numberOfLevels, out ChangeState __state) =>
        Before(__instance, ContentCategory.Skill, (int)skillType, true, ref numberOfLevels, out __state);
    private static bool RemoveSkillBefore(PlayerData __instance, SkillType skillType, ref int numberOfLevels, out ChangeState __state) =>
        Before(__instance, ContentCategory.Skill, (int)skillType, false, ref numberOfLevels, out __state);
    private static bool AddInjuryBefore(PlayerData __instance, InjuryType injuryType, ref int numberOfLevels, out ChangeState __state) =>
        Before(__instance, ContentCategory.Affliction, (int)injuryType, true, ref numberOfLevels, out __state);
    private static bool RemoveInjuryBefore(PlayerData __instance, InjuryType injuryType, ref int numberOfLevels, out ChangeState __state) =>
        Before(__instance, ContentCategory.Affliction, (int)injuryType, false, ref numberOfLevels, out __state);

    private static bool Before(PlayerData player, ContentCategory category, int id, bool adding, ref int amount, out ChangeState state)
    {
        state = null;
        if (player is null || !FoundationServices.Content.TryGet(category, id, out var definition) || !definition.Enabled) return true;
        var owned = Level(player, definition);
        amount = OwnershipDelta.Clamp(owned, amount, definition.MaximumLevel, adding);
        if (amount == 0) return false;
        state = new ChangeState
        {
            Definition = definition, Data = player,
            Health = player.hp, Maximum = player.MaxHealth
        };
        try
        {
            var runtime = PlayerManager.InstanceExists && PlayerManager.Instance.PlayerCount > 0 ? player.PlayerObject : null;
            if (runtime?.health is not null) { state.RuntimeHealth = runtime.health.Value; state.HasRuntimeHealth = true; }
        }
        catch { /* Lobby data still has a valid HP baseline. */ }
        state.Ticket = Gate.Begin(player.Pointer + ":" + definition.Key, owned);
        return true;
    }
    private static int Level(PlayerData player, ContentDescriptor descriptor) => descriptor.Category == ContentCategory.Skill
        ? player.HasSkill((SkillType)descriptor.NumericId) : player.HasInjuryInData((InjuryType)descriptor.NumericId);
    private static void BulkBefore(PlayerData __instance, out List<ChangeState> __state)
    {
        __state = new List<ChangeState>();
        foreach (var definition in FoundationServices.Content.Entries.Where(value => value.Enabled && value.Category == ContentCategory.Affliction))
        {
            var amount = Level(__instance, definition);
            if (amount > 0 && Before(__instance, ContentCategory.Affliction, definition.NumericId, false, ref amount, out var state) && state is not null)
                __state.Add(state);
        }
    }
    private static void BulkAfter(List<ChangeState> __state)
    { if (__state is not null) foreach (var state in __state) After(state); }
    private static Exception BulkFinally(Exception __exception, List<ChangeState> __state)
    { if (__state is not null) foreach (var state in __state) Finish(__exception, state); return __exception; }
    private static void After(ChangeState __state)
    {
        if (__state is null) return;
        var after = Level(__state.Data, __state.Definition);
        if (Gate.Complete(__state.Ticket, after, NativePersistence.IsRestoring))
            FoundationServices.PublishOwnership(new OwnershipChange(__state.Data, __state.Definition,
                __state.Ticket.Before, after, __state.Health, __state.RuntimeHealth, __state.HasRuntimeHealth, __state.Maximum));
    }
    private static Exception Finish(Exception __exception, ChangeState __state)
    {
        if (__state?.Ticket is not null) Gate.Complete(__state.Ticket, __state.Ticket.Before, failed: true);
        return __exception;
    }
    private static void EquipmentBefore(Player __instance, out PurchaseType __state) =>
        __state = __instance?.PlayerData?.currentEquipment ?? (PurchaseType)(-1);
    private static void EquipmentAfter(Player __instance, PurchaseType __state)
    {
        var data = __instance?.PlayerData;
        if (data is not null && data.currentEquipment != __state)
            FoundationServices.PublishEquipment(data, __state, data.currentEquipment);
    }
    internal static void Reset() => Gate.Clear();
}
