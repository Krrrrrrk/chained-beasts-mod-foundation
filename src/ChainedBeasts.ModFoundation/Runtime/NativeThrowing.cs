using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Il2CppSystem.Collections;
using ChainedBeasts.ModFoundation.Logic;
using ChainedBeasts.ModFoundation.Stats;

namespace ChainedBeasts.ModFoundation.Runtime;

internal static class NativeThrowing
{
    internal sealed class LaunchInfo
    {
        internal AttackRecord Attack;
        internal double Range;
        internal double Created;
    }
    internal sealed class PreviewScope
    {
        internal PreviewScope Parent;
        internal double Range;
        internal bool Updated;
        internal double LandingDistance;
    }
    internal sealed class ClockScope
    {
        internal bool First;
        internal double Extra;
    }
    [ThreadStatic] private static LaunchInfo _release;
    [ThreadStatic] private static LaunchInfo _launch;
    [ThreadStatic] private static PreviewScope _preview;
    private static readonly Dictionary<string, LaunchInfo> Requests = new(StringComparer.Ordinal);
    private static readonly Dictionary<IntPtr, LaunchInfo> Coroutines = new();
    private static readonly Dictionary<IntPtr, (int Generation, AttackRecord Attack)> Projectiles = new();

    internal static void Install(Harmony harmony)
    {
        void Patch(Type type, string name, string before = null, string after = null, string final = null, params Type[] args) =>
            FeaturePatcher.Patch(harmony, typeof(NativeThrowing), FeaturePatcher.RequireMethod(type, name, args), before, after, final);
        Patch(typeof(NetworkAgent._RunWindupLoop_d__443), "MoveNext", nameof(ClockBefore), nameof(ClockAfter));
        Patch(typeof(NetworkAgent), nameof(NetworkAgent.ExecuteThrow), nameof(ReleaseBefore), final: nameof(ReleaseFinally), args: new[] { typeof(Weapon), typeof(NetworkAgent.eAttackMode) });
        Patch(typeof(NetworkAgent), nameof(NetworkAgent.LaunchThrowProjectileRPC), nameof(RequestBefore), final: nameof(RequestFinally), args: new[]
        { typeof(double), typeof(float), typeof(PickupType), typeof(int), typeof(int), typeof(Vector3), typeof(Vector3), typeof(Quaternion), typeof(float), typeof(float), typeof(float), typeof(float), typeof(ulong), typeof(int), typeof(bool), typeof(bool), typeof(Vector3) });
        Patch(typeof(NetworkAgent), "LaunchThrowPostDelay", after: nameof(Created), args: new[]
        { typeof(float), typeof(PickupType), typeof(int), typeof(int), typeof(Vector3), typeof(Vector3), typeof(Quaternion), typeof(float), typeof(float), typeof(float), typeof(float), typeof(ulong), typeof(int), typeof(bool), typeof(bool), typeof(Vector3) });
        Patch(typeof(NetworkAgent._LaunchThrowPostDelay_d__476), "MoveNext", nameof(LaunchBefore), nameof(LaunchAfter), nameof(LaunchFinally));
        Patch(typeof(Projectile), nameof(Projectile.Launch), nameof(ProjectileBefore), nameof(ProjectileAfter), args: new[]
        { typeof(Vector3), typeof(float), typeof(float), typeof(float), typeof(bool), typeof(Unity.Netcode.NetworkObject), typeof(PickupType), typeof(int), typeof(int), typeof(ulong), typeof(int), typeof(float), typeof(bool), typeof(bool), typeof(bool) });
        // OnDespawn is a folded native ret shared with unrelated no-ops; never detour it.
        // Pooled launches invalidate by LaunchGeneration; destruction and battle/reset clear cached entries.
        Patch(typeof(Projectile), nameof(Projectile.OnDestroy), after: nameof(Despawned));
        Patch(typeof(TelegrapherArc), nameof(TelegrapherArc.SetScale), nameof(PreviewBefore), nameof(PreviewAfter), nameof(PreviewFinally), typeof(float));
        Patch(typeof(TelegrapherArc), nameof(TelegrapherArc.SetLineRenderer), nameof(ArcBefore), args: new[] { typeof(float), typeof(float), typeof(float), typeof(float) });
        Patch(typeof(Player), nameof(Player.FindTargetAgent), nameof(TargetBefore), args: new[] { typeof(Vector3), typeof(float), typeof(float), typeof(float) });
        FoundationServices.RunReset += Reset;
        FoundationServices.BattleStarted += _ => Reset();
    }
    private static double Factor(Player player, StatChannel channel) => NativeCatalogAndStats.Evaluate(channel, 1d, player.PlayerData, player);

    // One canonical clock augmentation for both native throw windup and charging.
    // Do not also divide durations or modify charge-scale getters.
    private static void ClockBefore(NetworkAgent._RunWindupLoop_d__443 __instance, out ClockScope __state)
    {
        __state = null;
        if (!NativeCombat.Active || __instance.attackMode != NetworkAgent.eAttackMode.ThrowWeapon || __instance.__1__state is not (0 or 1)) return;
        var player = __instance.__4__this?.TryCast<Player>();
        if (player is null || player.IsDead || __instance.weaponInstance is null ||
            player.CurrentWeapon?.Pointer != __instance.weaponInstance.Pointer) return;
        __state = new ClockScope { First = __instance.__1__state == 0,
            Extra = ThrowPhysics.ExtraClockStep(Time.deltaTime, Factor(player, StatChannel.ThrowChargeSpeed)) };
        if (!__state.First) AdvanceClock(__instance, __state.Extra);
    }
    private static void ClockAfter(NetworkAgent._RunWindupLoop_d__443 __instance, bool __result, ClockScope __state)
    {
        // The first native step initializes waitTimer; augment it only after initialization.
        if (__result && __state?.First == true) AdvanceClock(__instance, __state.Extra);
    }
    private static void AdvanceClock(NetworkAgent._RunWindupLoop_d__443 clock, double extra)
    {
        if (extra <= 0) return;
        if (clock.__4__this.IsChargingUp) clock.weaponInstance.chargeUpTimer += (float)extra;
        else clock._waitTimer_5__2 -= (float)extra;
    }
    private static void ReleaseBefore(NetworkAgent __instance, Weapon weaponInstance, NetworkAgent.eAttackMode attackMode, out LaunchInfo __state)
    {
        __state = _release;
        if (!NativeCombat.Active || attackMode != NetworkAgent.eAttackMode.ThrowWeapon || weaponInstance is null) return;
        var player = __instance.TryCast<Player>();
        if (player is null || player.IsDead) return;
        _release = new LaunchInfo { Attack = NativeCombat.Commit(player, weaponInstance, DirectAttackKind.ThrownWeapon),
            Range = Factor(player, StatChannel.ThrowRange), Created = Time.unscaledTimeAsDouble };
    }
    private static Exception ReleaseFinally(Exception __exception, LaunchInfo __state) { _release = __state; return __exception; }
    private static string RequestKey(NetworkAgent player, double serverTime, PickupType pickup, ulong origin) =>
        player.NetworkObjectId + ":" + serverTime.ToString("R", CultureInfo.InvariantCulture) + ":" + (int)pickup + ":" + origin;
    private static void RequestBefore(NetworkAgent __instance, double startServerTime, PickupType pickupType, ulong originNetworkObjectID, out LaunchInfo __state)
    {
        __state = _launch;
        if (!NativeCombat.Active || __instance.TryCast<Player>() is null) return;
        var key = RequestKey(__instance, startServerTime, pickupType, originNetworkObjectID);
        if (_release is not null) Requests.TryAdd(key, _release);
        // Bow projectiles use this same native RPC but are not normal held throws.
        else if (NativeCombat.CurrentAttack?.Kind == DirectAttackKind.OtherRanged)
            Requests.TryAdd(key, new LaunchInfo { Attack = NativeCombat.CurrentAttack, Range = 1, Created = Time.unscaledTimeAsDouble });
        _launch = Requests.TryGetValue(key, out var info) ? info : null;
    }
    private static Exception RequestFinally(Exception __exception, LaunchInfo __state) { _launch = __state; return __exception; }
    private static void Created(IEnumerator __result)
    { if (_launch is not null && __result is not null) Coroutines[__result.Pointer] = _launch; }
    private static void LaunchBefore(NetworkAgent._LaunchThrowPostDelay_d__476 __instance, out LaunchInfo __state)
    { __state = _launch; _launch = Coroutines.TryGetValue(__instance.Pointer, out var info) ? info : null; }
    private static void LaunchAfter(NetworkAgent._LaunchThrowPostDelay_d__476 __instance, bool __result)
    { if (!__result) Coroutines.Remove(__instance.Pointer); }
    private static Exception LaunchFinally(Exception __exception, LaunchInfo __state) { _launch = __state; return __exception; }
    private static void ProjectileBefore(ref float throwForce, ref float launchAngle, out LaunchInfo __state)
    {
        __state = _launch;
        if (_launch is null || _launch.Range == 1) return;
        var extended = ThrowPhysics.Extend(throwForce, launchAngle, _launch.Range);
        throwForce = (float)extended.Force; launchAngle = (float)extended.AngleDegrees;
    }
    private static void ProjectileAfter(Projectile __instance, LaunchInfo __state)
    {
        Projectiles.Remove(__instance.Pointer);
        if (__state is not null)
        {
            Projectiles[__instance.Pointer] = (__instance.LaunchGeneration, __state.Attack);
            NativeWeaponIdentity.Launched(__instance, __state.Attack);
        }
    }
    internal static bool TryGetAttack(Projectile projectile, out AttackRecord attack)
    {
        attack = null;
        if (!Projectiles.TryGetValue(projectile.Pointer, out var info) || info.Generation != projectile.LaunchGeneration) return false;
        attack = info.Attack; return true;
    }
    private static void Despawned(Projectile __instance) { if (__instance is not null) Projectiles.Remove(__instance.Pointer); }
    private static void PreviewBefore(TelegrapherArc __instance, out PreviewScope __state)
    {
        __state = _preview;
        var player = NativeCombat.Active ? __instance.agent?.TryCast<Player>() : null;
        _preview = new PreviewScope { Parent = _preview, Range = player is null || player.ThrowOffhandActive ? 1d : Factor(player, StatChannel.ThrowRange) };
    }
    private static void ArcBefore(float startHeight, ref float force, ref float angle, float gravity)
    {
        if (_preview is null) return;
        _preview.Updated = true;
        var extended = ThrowPhysics.Extend(force, angle, _preview.Range);
        force = (float)extended.Force; angle = (float)extended.AngleDegrees;
        _preview.LandingDistance = ThrowPhysics.HorizontalRange(force, angle, startHeight, Math.Abs(gravity));
    }
    private static void PreviewAfter(TelegrapherArc __instance)
    {
        if (_preview?.Updated != true || __instance.landingPoint is null || _preview.Range == 1) return;
        var position = __instance.landingPoint.localPosition;
        // Native SetScale uses half-distance in this marker's local space. Use
        // the same force/angle/gravity as its arc, including weapon overrides.
        position.z = double.IsFinite(_preview.LandingDistance) ? (float)(_preview.LandingDistance * .5d) : position.z * (float)_preview.Range;
        __instance.landingPoint.localPosition = position;
    }
    private static Exception PreviewFinally(Exception __exception, PreviewScope __state) { _preview = __state; return __exception; }
    private static void TargetBefore(Player __instance, ref float maxDistance)
    { if (NativeCombat.Active && __instance.IsAimingThrow && !__instance.ThrowOffhandActive) maxDistance *= (float)Factor(__instance, StatChannel.ThrowRange); }
    internal static void Tick()
    {
        foreach (var key in Requests.Where(value => Time.unscaledTimeAsDouble - value.Value.Created > 10d).Select(value => value.Key).ToArray()) Requests.Remove(key);
    }
    private static void Reset() { Requests.Clear(); Coroutines.Clear(); Projectiles.Clear(); _release = null; _launch = null; _preview = null; }
}
