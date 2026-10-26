using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ChainedBeasts.ModFoundation.Content;
using ChainedBeasts.ModFoundation.Stats;

namespace ChainedBeasts.ModFoundation.Runtime;

internal static class NativeCatalogAndStats
{
    private static readonly HashSet<string> LoggedErrors = new();
    internal static void Install(Harmony harmony)
    {
        void Patch(Type type, string name, string prefix = null, string postfix = null, params Type[] parameters) =>
            FeaturePatcher.Patch(harmony, typeof(NativeCatalogAndStats), FeaturePatcher.RequireMethod(type, name, parameters), prefix, postfix);
        Patch(typeof(GameLibrary), nameof(GameLibrary.Init), nameof(LibraryBefore), nameof(LibraryAfter));
        Patch(typeof(GameLibrary), nameof(GameLibrary.GetAllSkillTypes), postfix: nameof(Skills));
        Patch(typeof(GameLibrary), nameof(GameLibrary.GetAllPurchaseTypes), postfix: nameof(Equipment));
        Patch(typeof(GameLibrary), nameof(GameLibrary.GetAllInjuryTypes), postfix: nameof(Afflictions));
        foreach (var target in new[]
        {
            (typeof(PlayerData), nameof(PlayerData.MaxHealth), nameof(MaximumHealth)),
            (typeof(Player), nameof(Player.FinalMovementSpeed), nameof(Movement)),
            (typeof(Player), nameof(Player.StaminaCostDash), nameof(Dodge)),
            (typeof(Player), nameof(Player.StaminaRecoveryRate), nameof(Recovery))
        })
            FeaturePatcher.Patch(harmony, typeof(NativeCatalogAndStats), FeaturePatcher.RequirePropertyGetter(target.Item1, target.Item2), postfix: target.Item3);
        FeaturePatcher.Patch(harmony, typeof(NativeCatalogAndStats),
            FeaturePatcher.RequirePropertyGetter(typeof(Player), nameof(Player.MaxAttainableHealth)), prefix: nameof(Attainable));
        Patch(typeof(Player), nameof(Player.UpdateStamina), prefix: nameof(RecoveryClock));
        Patch(typeof(Player), nameof(Player.ModifyAttackDamage), postfix: nameof(AttackDamage), parameters: new[]
            { typeof(float), typeof(PickupType), typeof(NetworkAgent.eAttackType), typeof(NetworkHealthObject), typeof(bool), typeof(bool), typeof(bool) });
    }

    private static void LibraryBefore(GameLibrary __instance) => InvokeLibrary(__instance, false);
    private static void LibraryAfter(GameLibrary __instance) => InvokeLibrary(__instance, true);
    private static void InvokeLibrary(GameLibrary library, bool after)
    {
        foreach (var entry in FoundationServices.LibraryCallbacks.OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            if (!FoundationServices.Content.TryGet(entry.Key, out var content) || !content.Enabled) continue;
            try { (after ? entry.Value.After : entry.Value.Before)?.Invoke(library); }
            catch (Exception exception)
            {
                FoundationServices.Content.Disable(entry.Key);
                FoundationServices.Logger?.LogError($"[Foundation] disabled {entry.Key} after library registration failure: {exception}");
            }
        }
    }

    private static void Skills(Il2CppSystem.Collections.Generic.List<SkillType> __result)
    {
        if (__result is null) return;
        foreach (var value in FoundationServices.Content.Entries.Where(value => value.Enabled && value.Category == ContentCategory.Skill))
            if (!__result.Contains((SkillType)value.NumericId)) __result.Add((SkillType)value.NumericId);
    }
    private static void Equipment(Il2CppSystem.Collections.Generic.List<PurchaseType> __result)
    {
        if (__result is null) return;
        foreach (var value in FoundationServices.Content.Entries.Where(value => value.Enabled && value.Category == ContentCategory.Equipment))
            if (!__result.Contains((PurchaseType)value.NumericId)) __result.Add((PurchaseType)value.NumericId);
    }
    private static void Afflictions(Il2CppSystem.Collections.Generic.List<InjuryType> __result)
    {
        if (__result is null) return;
        foreach (var value in FoundationServices.Content.Entries.Where(value => value.Enabled && value.Category == ContentCategory.Affliction))
            if (!__result.Contains((InjuryType)value.NumericId)) __result.Add((InjuryType)value.NumericId);
    }

    internal static double Evaluate(StatChannel channel, double original, PlayerData data, Player player, NetworkHealthObject target = null, AttackRecord attack = null)
    {
        if (Networking.NativeMultiplayer.Online && !SessionAccess.GameplayReady) return original;
        try { return FoundationServices.Modifiers.Evaluate(channel, original, new NativeStatContext(data, player, target, attack)); }
        catch (Exception exception)
        {
            if (LoggedErrors.Add(channel.ToString())) FoundationServices.Logger?.LogError("[Foundation] modifier error: " + exception);
            return original;
        }
    }
    private static void AttackDamage(Player __instance, NetworkHealthObject target, ref float __result) =>
        __result = (float)Evaluate(StatChannel.AttackDamage, __result, __instance.PlayerData, __instance, target,
            NativeCombat.AttributedAttack?.Player?.Pointer == __instance.Pointer ? NativeCombat.AttributedAttack : null);
    private static void RecoveryClock(Player __instance)
    {
        if (!SessionAccess.GameplayReady || !__instance.IsOwner) return;
        CombatServices.UpdatingRecovery(__instance);
        if (__instance.staminaRecoveryTimer <= 0) return;
        var rate = Evaluate(StatChannel.RecoveryDelayClockRate, 1d, __instance.PlayerData, __instance);
        __instance.staminaRecoveryTimer = (float)Math.Max(0d, __instance.staminaRecoveryTimer - Math.Max(0d, rate - 1d) * UnityEngine.Time.deltaTime);
    }
    private static void MaximumHealth(PlayerData __instance, ref int __result) =>
        __result = (int)Math.Clamp(Math.Round(Evaluate(StatChannel.MaximumHealth, __result, __instance, null)), 0d, int.MaxValue);
    private static void Movement(Player __instance, ref float __result) =>
        __result = (float)Evaluate(StatChannel.MovementSpeed, __result, __instance?.PlayerData, __instance);
    private static void Dodge(Player __instance, ref float __result) =>
        __result = (float)Evaluate(StatChannel.DodgeStaminaCost, __result, __instance?.PlayerData, __instance);
    private static void Recovery(Player __instance, ref float __result) =>
        __result = (float)Evaluate(StatChannel.StaminaRecoveryRate, __result, __instance?.PlayerData, __instance);
    private static bool Attainable(Player __instance, ref int __result)
    {
        var data = __instance?.PlayerData;
        if (data is null || Evaluate(StatChannel.MaximumHealth, 1d, data, __instance) == 1d) return true;
        __result = data.MaxAttainableHealth;
        return false;
    }
}
