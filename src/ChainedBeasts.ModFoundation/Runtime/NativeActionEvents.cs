using System;
using System.Collections.Generic;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace ChainedBeasts.ModFoundation.Runtime;

/// <summary>Observed native stamina, shield-feedback, dodge-completion and player crowd contributions.</summary>
internal static class NativeActionEvents
{
    internal sealed class SpendScope { internal double Before; internal bool SkipDelay; }
    internal sealed class FeedbackScope { internal IntPtr Target; internal BlockOutcome Outcome; internal bool Published; internal string Id; }
    internal sealed class CrowdScope { internal bool Owner; internal double BeforeFavour, BeforeCombo, Factor; internal IntPtr Data; }
    [ThreadStatic] private static FeedbackScope _feedback;
    [ThreadStatic] private static Player _regenerating;
    [ThreadStatic] private static int _restoreDepth;
    [ThreadStatic] private static bool _oneTimeGrant;
    internal static void GrantStamina(Player player, float amount)
    { var before = _oneTimeGrant; _oneTimeGrant = true; try { player.RestoreStamina(amount); } finally { _oneTimeGrant = before; } }
    private static readonly Dictionary<IntPtr, bool> Dashes = new();
    private static readonly HashSet<IntPtr> CrowdBusy = new();
    private static long _feedbackSequence;
    internal static void Install(Harmony harmony)
    {
        void Patch(Type type, string name, string before = null, string after = null, string final = null, params Type[] args) =>
            FeaturePatcher.Patch(harmony, typeof(NativeActionEvents), FeaturePatcher.RequireMethod(type, name, args), before, after, final);
        Patch(typeof(Player), nameof(Player.ConsumeStamina), nameof(SpendBefore), nameof(SpendAfter), args: new[] { typeof(float), typeof(bool), typeof(float) });
        Patch(typeof(Player), nameof(Player.UpdateStamina), nameof(RecoveryBefore), final: nameof(RecoveryFinally));
        Patch(typeof(Player), nameof(Player.RestoreStamina), nameof(RestoreBefore), final: nameof(RestoreFinally), args: new[] { typeof(float) });
        Patch(typeof(NetworkHealthObject), nameof(NetworkHealthObject.TakeHitRpc), nameof(FeedbackBefore), final: nameof(FeedbackFinally), args: new[]
        { typeof(bool), typeof(BlockOutcome), typeof(int), typeof(int), typeof(ulong), typeof(Vector3), typeof(Vector3), typeof(Vector3), typeof(bool), typeof(DamageType), typeof(bool) });
        Patch(typeof(NetworkAgent), nameof(NetworkAgent.OnBlock), nameof(BlockBefore), args: new[] { typeof(Vector3), typeof(int), typeof(NetworkObject), typeof(bool), typeof(bool), typeof(DamageType) });
        Patch(typeof(Player), nameof(Player.StartDash), nameof(DashBefore), nameof(DashAfter));
        Patch(typeof(Player), nameof(Player.OnDashComplete), nameof(DodgeBefore), nameof(DodgeAfter));
        Patch(typeof(PlayerCrowdData), nameof(PlayerCrowdData.ProcessBattleEvent), nameof(CrowdBefore), nameof(CrowdAfter), nameof(CrowdFinally), typeof(BattleEventData));
        FoundationServices.RunReset += Reset;
        FoundationServices.BattleStarted += _ => Reset();
    }
    private static void SpendBefore(Player __instance, bool skipRecoverDelay, ref float amount, out SpendScope __state)
    {
        __state = null;
        if (!NativeCombat.Active || __instance.IsDead) return;
        var attack = NativeCombat.CurrentPayment;
        if (!skipRecoverDelay && amount > 0 && attack?.Player?.Pointer == __instance.Pointer)
            amount = (float)Math.Clamp(amount * CombatServices.PaymentMultiplier(attack), 0, float.MaxValue);
        __state = new SpendScope { Before = Math.Max(0, __instance.Stamina), SkipDelay = skipRecoverDelay };
    }
    private static void SpendAfter(Player __instance, SpendScope __state)
    {
        if (__state is null) return;
        var actual = Math.Clamp(__state.Before - Math.Max(0, __instance.Stamina), 0, __state.Before);
        if (actual > 0) CombatServices.Spent(__instance, actual, __state.SkipDelay);
    }
    private static void RecoveryBefore(Player __instance, out Player __state)
    { __state = _regenerating; _regenerating = NativeCombat.Active ? __instance : null; }
    private static Exception RecoveryFinally(Exception __exception, Player __state) { _regenerating = __state; return __exception; }
    private static void RestoreBefore(Player __instance, ref float amount, out int __state)
    {
        __state = _restoreDepth; _restoreDepth++;
        if (amount > 0 && !_oneTimeGrant && _restoreDepth == 1 && _regenerating?.Pointer == __instance.Pointer && NativeCombat.Active)
            amount = (float)NativeCatalogAndStats.Evaluate(Stats.StatChannel.RegenerationAmount, amount, __instance.PlayerData, __instance);
    }
    private static Exception RestoreFinally(Exception __exception, int __state) { _restoreDepth = __state; return __exception; }
    private static void FeedbackBefore(NetworkHealthObject __instance, BlockOutcome blockOutcome, out FeedbackScope __state)
    {
        __state = _feedback;
        _feedback = NativeCombat.Active && __instance.__rpc_exec_stage == NetworkBehaviour.__RpcExecStage.Execute
            ? new FeedbackScope { Target = __instance.Pointer, Outcome = blockOutcome, Id = "shield-hit:" + ++_feedbackSequence } : null;
    }
    private static Exception FeedbackFinally(Exception __exception, FeedbackScope __state) { _feedback = __state; return __exception; }
    private static void BlockBefore(NetworkAgent __instance, int damageBlocked)
    {
        if (_feedback is null || _feedback.Published || _feedback.Target != __instance.Pointer || _feedback.Outcome != BlockOutcome.CompleteBlockByShield || damageBlocked <= 0) return;
        var player = __instance.TryCast<Player>(); var shield = __instance.shieldPickupData;
        if (!NativeCombat.Active || player is null || player.IsDead || shield is null || shield.durability <= 0 || !__instance.ShieldCurrentlyActive) return;
        _feedback.Published = true;
        var block = new ShieldBlock { Player = player, Id = _feedback.Id, Shield = shield };
        CombatServices.Blocked(block);
        // Before original OnBlock: its native model pulse, break, unequip and feedback see the real extra loss.
        shield.durability = Math.Max(0, shield.durability - Math.Clamp(block.AdditionalDurabilityLoss, 0, shield.durability));
    }
    private static void DashBefore(Player __instance, out bool __state) => __state = __instance.IsDashing;
    private static void DashAfter(Player __instance, bool __state)
    { if (NativeCombat.Active && !__state && __instance.IsDashing) Dashes[__instance.Pointer] = true; }
    private static void DodgeBefore(Player __instance, out bool __state)
    {
        __state = NativeCombat.Active && Dashes.TryGetValue(__instance.Pointer, out var pending) && pending;
        if (__state) Dashes[__instance.Pointer] = false;
    }
    private static void DodgeAfter(Player __instance, bool __state)
    { if (__state && !__instance.IsDead) CombatServices.Dodged(__instance); }
    private static void CrowdBefore(PlayerCrowdData __instance, BattleEventData eventData, out CrowdScope __state)
    {
        __state = null;
        var player = __instance.player;
        if (!NativeCombat.Active || player is null || player.IsDead || eventData?.actor?.Pointer != player.Pointer) return;
        __state = new CrowdScope { Data = __instance.Pointer, Owner = CrowdBusy.Add(__instance.Pointer), BeforeFavour = __instance.localCrowdExcitement,
            BeforeCombo = __instance.currentCombo, Factor = CombatServices.FavourMultiplier(player) };
    }
    private static void CrowdAfter(PlayerCrowdData __instance, CrowdScope __state)
    {
        if (__state?.Owner != true) return;
        __instance.localCrowdExcitement = (float)Logic.ContributionMath.ScalePositiveGain(__state.BeforeFavour, __instance.localCrowdExcitement, __state.Factor);
        __instance.currentCombo = (float)Logic.ContributionMath.ScalePositiveGain(__state.BeforeCombo, __instance.currentCombo, __state.Factor);
        // Do not change raw damage/variety/tempo score, pool depletion/recharge, decay or fixed combo duration.
    }
    private static Exception CrowdFinally(Exception __exception, CrowdScope __state)
    { if (__state?.Owner == true) CrowdBusy.Remove(__state.Data); return __exception; }
    private static void Reset() { _feedback = null; _regenerating = null; _restoreDepth = 0; Dashes.Clear(); CrowdBusy.Clear(); }
}
