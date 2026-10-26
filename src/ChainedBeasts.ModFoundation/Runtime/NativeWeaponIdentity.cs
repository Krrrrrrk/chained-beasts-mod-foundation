using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace ChainedBeasts.ModFoundation.Runtime;

/// <summary>A physical object token; wrapper/model pointers are only transient aliases, never saved identities.</summary>
public sealed class PhysicalWeapon
{
    public string Id { get; internal set; } = Guid.NewGuid().ToString("N");
    public PickupData Data { get; internal set; }
    public Weapon Held { get; internal set; }
    public Projectile Flying { get; internal set; }
    public Pickup Ground { get; internal set; }
    internal int FlyingGeneration;
    internal int LastThrower = -1;
    internal bool AwaitingRecovery;
    public int Durability => Flying != null && Flying.LaunchGeneration == FlyingGeneration ? Flying.durability : Data?.durability ?? 0;
    public int MaximumDurability => Data is not null && GameLibrary.InstanceExists && GameLibrary.Instance.PickupDict.ContainsKey(Data.pickupType)
        ? GameLibrary.Instance.PickupDict[Data.pickupType].StartingDurability : 0;
    public bool IsDefault => Held != null && Held.IsDefault;
}

/// <summary>Native representation changes are scoped; equal type/origin values alone never identify a weapon.</summary>
internal static class NativeWeaponIdentity
{
    internal sealed class Scope { internal Scope Parent; internal PhysicalWeapon Weapon; internal Player Receiver; }
    [ThreadStatic] private static Scope _scope;
    // Retain data wrappers for this battle so recycled native addresses cannot alias an old token.
    private static readonly Dictionary<IntPtr, (PickupData Data, PhysicalWeapon Weapon)> Aliases = new();
    private static readonly Dictionary<IntPtr, (int Generation, PhysicalWeapon Weapon)> Flying = new();
    private static readonly Dictionary<string, PhysicalWeapon> ExtraCosts = new(StringComparer.Ordinal);
    [ThreadStatic] private static Weapon _extraCostWeapon;

    internal static PhysicalWeapon For(Weapon weapon)
    {
        if (weapon?.pickupData is null || weapon.IsDefault) return null;
        var result = ForData(weapon.pickupData); result.Held = weapon; return result;
    }
    private static PhysicalWeapon ForData(PickupData data)
    {
        if (data is null) return null;
        if (Aliases.TryGetValue(data.Pointer, out var found)) return found.Weapon;
        var source = _scope?.Weapon;
        if (!Logic.RepresentationTransfer.Matches(source?.Data is not null, (int)(source?.Data?.pickupType ?? PickupType.None), (int)data.pickupType,
            source?.Data?.modelIndex ?? -1, data.modelIndex, source?.Data?.originNetworkObjectID ?? 0, data.originNetworkObjectID)) source = new PhysicalWeapon();
        Aliases[data.Pointer] = (data, source); source.Data = data; return source;
    }
    private static void Bind(PickupData data, PhysicalWeapon weapon)
    { if (data is not null && weapon is not null) { Aliases[data.Pointer] = (data, weapon); weapon.Data = data; } }
    internal static void Launched(Projectile projectile, AttackRecord attack)
    {
        Flying.Remove(projectile.Pointer);
        var weapon = attack?.Weapon;
        if (weapon is null || attack.Kind != DirectAttackKind.ThrownWeapon) return;
        weapon.Held = null; weapon.Ground = null; weapon.Flying = projectile; weapon.FlyingGeneration = projectile.LaunchGeneration;
        weapon.LastThrower = attack.Player.PlayerData.playerID; weapon.AwaitingRecovery = true;
        Flying[projectile.Pointer] = (projectile.LaunchGeneration, weapon);
    }
    private static PhysicalWeapon ProjectileWeapon(Projectile projectile) => Flying.TryGetValue(projectile.Pointer, out var value) && value.Generation == projectile.LaunchGeneration ? value.Weapon : null;
    internal static void ExtraDurability(PhysicalWeapon weapon)
    {
        if (!NativeCombat.Active || weapon is null || weapon.Durability <= 0) return;
        ExtraCosts.TryAdd(weapon.Id, weapon);
    }
    // Defer until the native contact/normal durability work has finished. Flush before any checkpoint too.
    internal static void FlushCosts()
    {
        foreach (var weapon in ExtraCosts.Values.ToArray())
        { ExtraCosts.Remove(weapon.Id); if (NativeCombat.Active) ApplyExtra(weapon); }
    }
    private static void ApplyExtra(PhysicalWeapon weapon)
    {
        if (weapon.Durability <= 0) return;
        if (weapon.Flying != null && weapon.Flying.LaunchGeneration == weapon.FlyingGeneration)
        { weapon.Flying.durability = Math.Max(0, weapon.Flying.durability - 1); return; }
        if (weapon.Held != null && weapon.Held.pickupData?.Pointer == weapon.Data?.Pointer && !weapon.Held.IsDefault)
        {
            var prior = _extraCostWeapon; _extraCostWeapon = weapon.Held;
            try { weapon.Held.LoseDurability(1); } // Native UI/break path, after normal contact work.
            finally { _extraCostWeapon = prior; }
        }
        else if (weapon.Ground != null && weapon.Ground.pickupData?.Pointer == weapon.Data?.Pointer)
        {
            weapon.Data.durability = Math.Max(0, weapon.Data.durability - 1);
            if (weapon.Data.durability == 0 && PickupManager.InstanceExists) PickupManager.Instance.DestroyPickup(weapon.Ground);
        }
    }
    internal static void Install(Harmony harmony)
    {
        void Patch(Type type, string name, string before = null, string after = null, string final = null, params Type[] args) =>
            FeaturePatcher.Patch(harmony, typeof(NativeWeaponIdentity), FeaturePatcher.RequireMethod(type, name, args), before, after, final);
        Patch(typeof(Pickup), nameof(Pickup.RequestPickupRpc), nameof(PickupBefore), final: nameof(Finally), args: new[] { typeof(ulong) });
        Patch(typeof(Player), nameof(Player.ReceivePickup), nameof(ReceiveBefore), final: nameof(Finally), args: new[]
        { typeof(PickupType), typeof(int), typeof(int), typeof(int), typeof(Vector3), typeof(ulong), typeof(int), typeof(bool) });
        Patch(typeof(Player), nameof(Player.EquipWeapon), nameof(EquipBefore), nameof(EquipAfter), args: new[] { typeof(PickupData), typeof(bool), typeof(bool) });
        Patch(typeof(Player), nameof(Player.DropPickupRpc), nameof(DropBefore), final: nameof(Finally), args: new[] { typeof(EquipType), typeof(float), typeof(bool) });
        Patch(typeof(PickupManager), nameof(PickupManager.SpawnPickup), nameof(SpawnBefore), nameof(SpawnAfter), nameof(Finally),
            typeof(Vector3), typeof(Quaternion), typeof(PickupType), typeof(int), typeof(int), typeof(EnemyType), typeof(int), typeof(Vector3), typeof(Vector3), typeof(ulong), typeof(bool), typeof(int));
        Patch(typeof(Projectile), nameof(Projectile.ToPickup), after: nameof(ToPickup));
        Patch(typeof(Player), nameof(Player.HasEquipment), nameof(AdditionalCostEquipment), args: new[] { typeof(PurchaseType) });
        // These observed native scopes cover ordinary landed/dropped/embedded pickups, including recursive helpers.
        foreach (var name in new[] { "FixedUpdate", "LandedOnGround", "HandleTriggerWithOtherHealthObject", "HandleTriggerWithAgent", "EmbedInTarget" })
        {
            var method = typeof(Projectile).GetMethods().Single(value => value.Name == name && value.DeclaringType == typeof(Projectile));
            FeaturePatcher.Patch(harmony, typeof(NativeWeaponIdentity), method, nameof(ProjectileBefore), finalizer: nameof(Finally));
        }
        FoundationServices.RunReset += Reset;
        // Data/physical identity may cross the battle boundary. Only feature allowances reset there.
    }
    private static void PickupBefore(Pickup __instance, out Scope __state)
    {
        __state = _scope;
        if (NativeCombat.Active && __instance.__rpc_exec_stage == NetworkBehaviour.__RpcExecStage.Execute)
            _scope = new Scope { Parent = _scope, Weapon = ForData(__instance.pickupData) };
    }
    private static void ReceiveBefore(Player __instance, out Scope __state)
    { __state = _scope; if (NativeCombat.Active) _scope = new Scope { Parent = _scope, Weapon = _scope?.Weapon, Receiver = __instance }; }
    private static void EquipBefore(PickupData pickupData)
    { if (NativeCombat.Active && !NativePersistence.IsRestoring) ForData(pickupData); }
    private static void EquipAfter(Player __instance, PickupData pickupData, bool setAsDefault)
    {
        if (!NativeCombat.Active || NativePersistence.IsRestoring || setAsDefault || pickupData is null || _scope?.Receiver?.Pointer != __instance.Pointer) return;
        if (!GameLibrary.Instance.PickupDict.ContainsKey(pickupData.pickupType)) return;
        var slot = GameLibrary.Instance.PickupDict[pickupData.pickupType].EquipType;
        if (__instance.equippedWeapons is null || !__instance.equippedWeapons.ContainsKey(slot)) return;
        var weapon = __instance.equippedWeapons[slot];
        if (weapon?.pickupData?.Pointer != pickupData.Pointer || weapon.IsDefault) return;
        var physical = For(weapon); physical.Flying = null; physical.Ground = null;
        if (physical.AwaitingRecovery)
        {
            var own = physical.LastThrower == __instance.PlayerData.playerID;
            physical.AwaitingRecovery = false;
            CombatServices.Recovered(__instance, weapon, physical, own);
        }
    }
    private static void DropBefore(Player __instance, EquipType equipType, out Scope __state)
    {
        __state = _scope;
        if (!NativeCombat.Active || __instance.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || __instance.equippedWeapons?.ContainsKey(equipType) != true) return;
        _scope = new Scope { Parent = _scope, Weapon = For(__instance.equippedWeapons[equipType]) };
    }
    private static void SpawnBefore(PickupType pickupType, ulong originNetworkObjectID, out Scope __state)
    {
        __state = _scope;
        // ReceivePickup drops the previous item before equipping the incoming one. Do not confuse equal native origins.
        var receiver = _scope?.Receiver;
        if (NativeCombat.Active && receiver is not null && GameLibrary.Instance.PickupDict.ContainsKey(pickupType))
        {
            var slot = GameLibrary.Instance.PickupDict[pickupType].EquipType;
            var held = receiver.equippedWeapons?.ContainsKey(slot) == true ? receiver.equippedWeapons[slot] : null;
            if (held?.pickupData?.pickupType == pickupType && held.pickupData.originNetworkObjectID == originNetworkObjectID)
                _scope = new Scope { Parent = _scope, Weapon = For(held) };
        }
    }
    private static void SpawnAfter(Pickup __result)
    {
        if (NativeCombat.Active && __result?.pickupData is not null)
        { var weapon = ForData(__result.pickupData); weapon.Ground = __result; weapon.Held = null; weapon.Flying = null; }
    }
    private static bool AdditionalCostEquipment(Player __instance, PurchaseType purchaseType, ref bool __result)
    {
        if (_extraCostWeapon?.holderAgent?.Pointer != __instance.Pointer || purchaseType != PurchaseType.DoubleDurability) return true;
        __result = false; return false; // The promised additional cost cannot become a random free hit after an equipment change.
    }
    private static void ProjectileBefore(Projectile __instance, out Scope __state)
    { __state = _scope; _scope = new Scope { Parent = _scope, Weapon = ProjectileWeapon(__instance) }; }
    private static void ToPickup(Projectile __instance, PickupData __result) => Bind(__result, ProjectileWeapon(__instance));
    private static Exception Finally(Exception __exception, Scope __state) { _scope = __state; return __exception; }
    private static void Reset() { ExtraCosts.Clear(); if (!NativePersistence.IsRestoring) { Aliases.Clear(); Flying.Clear(); } _scope = null; _extraCostWeapon = null; }
}
