using System;
using BepInEx.Logging;
using ChainedBeasts.ModFoundation;
using ChainedBeasts.ModFoundation.Runtime;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace TermsAndContusions.Skills;

// Only the inspected directional-hit body sees the temporary mask. Ordinary
// throws, releases and cleanup still use the real native grapple relationship.
internal static class IronGripFeature
{
    internal sealed class HitScope
    {
        internal HitScope Parent;
        internal Player Holder;
        internal Enemy Target;
        internal Coroutine Lift;
        internal bool WaitingForBlock, Masked, Cancelled;
    }
    [ThreadStatic] private static HitScope _current;

    internal static void Install(Harmony harmony, ManualLogSource logger)
    {
        void Patch(Type type, string name, string before = null, string after = null, string final = null, params Type[] args) =>
            FeaturePatcher.Patch(harmony, typeof(IronGripFeature), FeaturePatcher.RequireMethod(type, name, args), before, after, final);
        Patch(typeof(NetworkAgent), nameof(NetworkAgent.OnDirectionalHitTaken), nameof(HitBefore), final: nameof(HitFinally), args: new[]
        {
            typeof(BlockOutcome), typeof(int), typeof(int), typeof(NetworkObject), typeof(NetworkAgent),
            typeof(Vector3), typeof(Vector3), typeof(Vector3), typeof(bool), typeof(DamageType), typeof(bool)
        });
        Patch(typeof(Player), nameof(Player.OnBlock), after: nameof(BlockAfter), args: new[]
        { typeof(Vector3), typeof(int), typeof(NetworkObject), typeof(bool), typeof(bool), typeof(DamageType) });
        Patch(typeof(NetworkAgent), nameof(NetworkAgent.ClearGrapple), nameof(ClearBefore));
        Patch(typeof(NetworkAgent), nameof(NetworkAgent.Die), nameof(TeardownBefore));
        Patch(typeof(NetworkAgent), nameof(NetworkAgent.OnNetworkDespawn), nameof(DespawnBefore));
        logger.LogInfo("[Terms & Contusions] Iron Grip installed: owner hit-reaction carry retention; native block, throw and cleanup paths preserved");
    }

    private static void HitBefore(NetworkAgent __instance, int blockedDamage, out HitScope __state)
    {
        __state = null;
        if (!SessionAccess.GameplayReady) return;
        var holder = __instance?.TryCast<Player>();
        if (holder == null || !holder.IsOwner || !holder.IsSpawned || holder.IsDead || holder.playerData is null ||
            SkillsFeature.Rank(holder.playerData, CustomSkill.IronGrip) == 0) return;
        var target = holder.grappledAgent?.TryCast<Enemy>();
        if (target == null || !target.IsSpawned || target.IsDead || target.IsPlayerTeam ||
            target.grapplingAgent?.Pointer != holder.Pointer) return;
        __state = new HitScope { Parent = _current, Holder = holder, Target = target, WaitingForBlock = blockedDamage > 0 };
        _current = __state;
        // OnBlock must first see the true carried target (vanilla disarm checks
        // and injury self-damage depend on it). Its postfix masks the later drop.
        if (!__state.WaitingForBlock) Mask(__state);
    }

    private static void BlockAfter(Player __instance)
    {
        var scope = _current;
        if (scope is null || scope.Cancelled || !scope.WaitingForBlock || __instance?.Pointer != scope.Holder.Pointer) return;
        scope.WaitingForBlock = false;
        Mask(scope);
    }

    private static void Mask(HitScope scope)
    {
        var holder = scope.Holder;
        var target = scope.Target;
        if (scope.Cancelled || scope.Masked || holder == null || target == null || holder.IsDead || target.IsDead ||
            !holder.IsSpawned || !target.IsSpawned || holder.grappledAgent?.Pointer != target.Pointer ||
            target.grapplingAgent?.Pointer != holder.Pointer) return;
        scope.Lift = holder.grappleCoroutine;
        scope.Masked = true;
        holder.grappledAgent = null;
        // The native hit body cancels the lift coroutine even with a null
        // grappledAgent. Mask both so a hit cannot leave an unfinished pickup.
        holder.grappleCoroutine = null;
    }

    private static bool Expose(HitScope scope)
    {
        if (!scope.Masked) return false;
        scope.Masked = false;
        var holder = scope.Holder;
        var target = scope.Target;
        if (holder == null || target == null || target.grapplingAgent?.Pointer != holder.Pointer ||
            (holder.grappledAgent != null && holder.grappledAgent.Pointer != target.Pointer))
        { ReleaseRetained(scope); return false; }
        holder.grappledAgent = target;
        // Never overwrite a new native coroutine or a replacement relationship.
        if (holder.grappleCoroutine == null) holder.grappleCoroutine = scope.Lift;
        return true;
    }

    private static Exception HitFinally(Exception __exception, HitScope __state)
    {
        if (__state is null) return __exception;
        try
        {
            if (!__state.Cancelled)
            {
                if (__state.Holder != null && __state.Target != null && __state.Holder.IsSpawned && __state.Target.IsSpawned)
                {
                    if (Expose(__state) && (__state.Holder.IsDead || __state.Target.IsDead)) __state.Holder.ClearGrapple();
                }
                else if (__state.Masked) { __state.Masked = false; ReleaseRetained(__state); }
            }
        }
        finally { _current = __state.Parent; }
        return __exception;
    }

    private static void ClearBefore(NetworkAgent __instance) => CancelForCleanup(__instance, false);
    private static void TeardownBefore(NetworkAgent __instance) => CancelForCleanup(__instance, true);
    private static void DespawnBefore(NetworkAgent __instance) => CancelForCleanup(__instance, true, true);
    private static void CancelForCleanup(NetworkAgent agent, bool includeTarget, bool despawning = false)
    {
        if (agent == null) return;
        for (var scope = _current; scope is not null; scope = scope.Parent)
        {
            if (scope.Cancelled || (agent.Pointer != scope.Holder.Pointer && (!includeTarget || agent.Pointer != scope.Target.Pointer))) continue;
            // Re-expose before the original cleanup, including Die's direct RPC
            // path. The finalizer must never recreate a relationship it removed.
            scope.Cancelled = true;
            Expose(scope);
            // A target's Die/despawn does not detach it from its holder, and a
            // holder's despawn has no carry cleanup. Finish that exact old pair
            // while it is still spawned; never clear a replacement target.
            if (agent.Pointer == scope.Target.Pointer || despawning)
            {
                if (scope.Holder != null && scope.Holder.IsSpawned && scope.Target != null && scope.Target.IsSpawned &&
                    scope.Holder.grappledAgent?.Pointer == scope.Target.Pointer &&
                    scope.Target.grapplingAgent?.Pointer == scope.Holder.Pointer)
                    scope.Holder.ClearGrapple();
                else ReleaseRetained(scope);
            }
        }
    }

    private static void ReleaseRetained(HitScope scope)
    {
        var holder = scope.Holder;
        var target = scope.Target;
        if (holder == null) return;
        // Native RPC on the original captive only. Its new holder (if any) and
        // this player's replacement captive must remain completely untouched.
        if (target != null && target.IsSpawned && target.grapplingAgent?.Pointer == holder.Pointer)
            target.UngrappleRPC();
        if (scope.Lift != null)
        {
            holder.StopCoroutine(scope.Lift);
            if (holder.grappleCoroutine?.Pointer == scope.Lift.Pointer) holder.grappleCoroutine = null;
            scope.Lift = null;
        }
    }
}
