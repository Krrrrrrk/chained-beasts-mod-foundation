using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using ChainedBeasts.ModFoundation.Logic;
using ChainedBeasts.ModFoundation.Networking;

namespace ChainedBeasts.ModFoundation.Runtime;

internal static class NativeCombat
{
    internal sealed class Contact
    {
        internal AttackRecord Attack;
        internal Contact Parent;
        internal IntPtr Weapon;
        internal AttackRecord AttributionBefore;
    }
    internal sealed class DamageScope
    {
        internal DamageScope Parent;
        internal DirectHit Hit;
        internal NetworkHealthObject Target;
        internal ulong Source;
        internal double Time;
        internal bool Executed;
        internal bool Warned;
        internal ExecutionReceipt Execution;
        internal AttackRecord AttributedAttack;
    }
    internal sealed class Receipt
    {
        internal int Before;
        internal NetworkAgent Source;
        internal Player Player;
        internal DamageScope Scope;
        internal ExecutionReceipt Execution;
        internal string NetworkKey;
        internal ulong Origin;
    }
    internal sealed class ExecutionReceipt { internal Player Player; internal Enemy Enemy; internal ulong Source; internal string TargetId; }
    [ThreadStatic] private static ExecutionReceipt _execution;
    private static readonly List<ExecutionReceipt> Executions = new();
    private static readonly Dictionary<IntPtr, NetworkHealthObject> TargetReferences = new();
    private static readonly SpawnTokenMap<IntPtr> Targets = new();
    internal static string TargetIdentity(NetworkHealthObject target)
    {
        if (target is null) return null;
        TargetReferences[target.Pointer] = target;
        return Targets.Get(target.Pointer, target.NetworkObjectId);
    }
    internal static bool HasLiveTarget(string token) => token is not null && TargetReferences.Values.Any(target => target != null && !target.IsDead && Targets.Get(target.Pointer, target.NetworkObjectId) == token);
    internal sealed class BreakScope
    {
        internal Player Player;
        internal PickupData Pickup;
        internal int Before;
        internal bool Owner;
        internal IntPtr Weapon;
        internal int Amount;
        internal bool Destroyed;
        internal BreakScope Parent;
    }
    [ThreadStatic] private static Contact _contact;
    [ThreadStatic] private static AttackRecord _attribution;
    [ThreadStatic] private static AttackRecord _payment;
    [ThreadStatic] internal static bool SpecialPayment;
    internal sealed class PaymentScope { internal AttackRecord Attack; internal bool Special; }
    [ThreadStatic] private static bool _generated;
    [ThreadStatic] private static DamageScope _damage;
    [ThreadStatic] private static BreakScope _breakingScope;
    private static readonly AttackAttemptLedger<AttackRecord> Actions = new();
    private static readonly Dictionary<IntPtr, (float Started, AttackRecord Attack)> WeaponAttempts = new();
    private static readonly List<DamageScope> Pending = new();
    private static readonly DamageReceiptQueue<DamageScope> Dispatches = new();
    private static readonly HashSet<IntPtr> Breaking = new();
    private static long _sequence;
    internal static bool Active => SessionAccess.GameplayReady;
    internal static AttackRecord CurrentAttack => _contact?.Attack;
    internal static AttackRecord AttributedAttack => _contact?.Attack ?? _attribution;
    internal static AttackRecord CurrentPayment => _payment;
    internal static NetworkHealthObject FindLiveTarget(string token) => TargetReferences.Values.FirstOrDefault(target => target != null && !target.IsDead && Targets.Get(target.Pointer, target.NetworkObjectId) == token);
    internal static void GeneratedDamage(Player source, NetworkHealthObject target, int amount)
    {
        if (!Active || source == null || !source.IsOwner || source.IsDead || target == null || target.IsDead || amount <= 0) return;
        var before = _generated; _generated = true;
        try { target.TakeDamage(amount, 0, DamageType.Slash, new DamageEventSourceExternalDirectionless(source.NetworkObject), false, false, false, false); }
        finally { _generated = before; }
    }

    internal static void Install(Harmony harmony)
    {
        // All declared overrides have the same inspected native AttackEffect contract.
        // This covers bow/flail/special weapons as well as the base contact path.
        foreach (var type in typeof(Weapon).Assembly.GetTypes().Where(type => type == typeof(Weapon) || type.IsSubclassOf(typeof(Weapon))))
        {
            var method = type.GetMethod(nameof(Weapon.AttackEffect), BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (method is not null) FeaturePatcher.Patch(harmony, typeof(NativeCombat), method, nameof(WeaponBefore), finalizer: nameof(ContactFinally));
            var commit = type.GetMethod(nameof(Weapon.Attack), BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (commit is not null && NativeHookPolicy.ObserveWeaponMethod(type.FullName, commit.Name))
                FeaturePatcher.Patch(harmony, typeof(NativeCombat), commit, nameof(AttackBefore), finalizer: nameof(ContactFinally));
        }
        FoundationServices.Logger?.LogInfo("[Foundation] charged-weapon Attack wrappers left native; their unique Weapon.Attack delegate is observed once");
        void Patch(Type type, string name, string before, string after = null, string finallyName = null, params Type[] args) =>
            FeaturePatcher.Patch(harmony, typeof(NativeCombat), FeaturePatcher.RequireMethod(type, name, args), before, after, finallyName);
        Patch(typeof(Player), nameof(Player.ConsumeMeleeStamina), nameof(CostBefore), finallyName: nameof(CostFinally), args: new[] { typeof(bool) });
        Patch(typeof(Player), nameof(Player.ModifyAttackDamage), nameof(BackstabBefore), args: new[]
        { typeof(float), typeof(PickupType), typeof(NetworkAgent.eAttackType), typeof(NetworkHealthObject), typeof(bool), typeof(bool), typeof(bool) });
        Patch(typeof(Player), nameof(Player.CompleteExecution), nameof(ExecutionBefore), finallyName: nameof(ExecutionFinally), args: new[] { typeof(Enemy), typeof(ExecutionDef), typeof(PickupType) });
        Patch(typeof(Enemy), nameof(Enemy.OnNetworkSpawn), nameof(EnemySpawned));
        Patch(typeof(NetworkAgent), nameof(NetworkAgent.OnNetworkDespawn), nameof(AgentDespawned));
        Patch(typeof(WeaponFlail), nameof(WeaponFlail.PerformCollision), nameof(WeaponBefore), finallyName: nameof(ContactFinally), args: new[] { typeof(bool), typeof(bool) });
        Patch(typeof(Projectile), nameof(Projectile.DealDamage), nameof(ProjectileBefore), finallyName: nameof(ProjectileFinally), args: new[]
            { typeof(NetworkHealthObject), typeof(bool), typeof(BattleEventData), typeof(bool) });
        Patch(typeof(NetworkHealthObject), nameof(NetworkHealthObject.TakeDamage), nameof(DamageBefore), nameof(DamageAfter), nameof(DamageFinally),
            typeof(float), typeof(int), typeof(DamageType), typeof(DamageEventSource), typeof(bool), typeof(bool), typeof(bool), typeof(bool));
        Patch(typeof(NetworkHealthObject), "TakeDamageRpc", nameof(ReceiptBefore), nameof(ReceiptAfter), null,
            typeof(int), typeof(ulong), typeof(bool), typeof(DamageType), typeof(bool), typeof(bool), typeof(bool), typeof(RpcParams));
        Patch(typeof(Weapon), nameof(Weapon.LoseDurability), nameof(BreakBefore), nameof(BreakAfter), nameof(BreakFinally), typeof(int));
        Patch(typeof(NetworkAgent), nameof(NetworkAgent.DestroyWeapon), nameof(DestroyedDuringLoss), args: new[] { typeof(EquipType), typeof(bool) });
        FoundationServices.RunReset += Reset;
        FoundationServices.BattleStarted += _ => Reset();
    }
    private static void AttackBefore(Weapon __instance, out Contact __state)
    {
        __state = _contact;
        if (!Active) return;
        var player = __instance.holderAgent?.TryCast<Player>();
        if (player is null || player.IsDead || !player.IsOwner) return;
        var kind = __instance.TryCast<WeaponBow>() is not null ? DirectAttackKind.OtherRanged : DirectAttackKind.Melee;
        var attack = _contact?.Weapon == __instance.Pointer && _contact.Attack?.Player?.Pointer == player.Pointer
            ? _contact.Attack : Commit(player, __instance, kind);
        WeaponAttempts[__instance.Pointer] = (player.AttackStartTimestamp, attack);
        _contact = new Contact { Parent = _contact, Attack = attack, Weapon = __instance.Pointer };
    }

    internal static AttackRecord Commit(Player player, Weapon weapon, DirectAttackKind kind)
    {
        var signature = player.AttackStartTimestamp.ToString("R", CultureInfo.InvariantCulture) + ":" +
            (weapon?.Pointer.ToString() ?? "none") + ":" + (weapon?.AttackIndex ?? 0) + ":" + kind;
        var id = player.PlayerData.playerID;
        var attack = Actions.GetOrCreate(id, signature, () => new AttackRecord { Id = "action:" + ++_sequence, Player = player, Kind = kind, Weapon = NativeWeaponIdentity.For(weapon),
            IsUnarmed = weapon?.WeaponClass == WeaponClass.Unarmed,
            BeforeCostStamina01 = player.Stamina / Math.Max(.001d, player.StaminaMax) }, out var created);
        if (created) CombatServices.Commit(attack);
        return attack;
    }
    private static void WeaponBefore(Weapon __instance, out Contact __state)
    {
        __state = _contact;
        if (!Active) return;
        var player = __instance.holderAgent?.TryCast<Player>();
        if (player is null || player.IsDead || !player.IsOwner) return;
        var kind = __instance.TryCast<WeaponBow>() is not null ? DirectAttackKind.OtherRanged : DirectAttackKind.Melee;
        var attack = WeaponAttempts.TryGetValue(__instance.Pointer, out var attempt) && attempt.Started == player.AttackStartTimestamp
            ? attempt.Attack : Commit(player, __instance, kind);
        _contact = new Contact { Parent = _contact, Attack = attack, Weapon = __instance.Pointer };
    }
    private static void CostBefore(Player __instance, bool specialMelee, out PaymentScope __state)
    {
        __state = new PaymentScope { Attack = _payment, Special = SpecialPayment };
        if (!Active || __instance.IsDead || !__instance.IsOwner) return;
        var attack = _contact?.Attack ?? Commit(__instance, __instance.CurrentWeapon, DirectAttackKind.Melee);
        _payment = attack;
        SpecialPayment = specialMelee;
        if (!attack.CostCaptured)
        { attack.BeforeCostStamina01 = __instance.Stamina / Math.Max(.001d, __instance.StaminaMax); attack.CostCaptured = true; }
    }
    private static Exception CostFinally(Exception __exception, PaymentScope __state) { _payment = __state.Attack; SpecialPayment = __state.Special; return __exception; }
    private static void BackstabBefore(Player __instance, NetworkHealthObject target, bool isBackstab)
    {
        if (Active && target is not null && _contact?.Attack?.Player?.Pointer == __instance.Pointer)
            _contact.Attack.Backstabs[target.Pointer] = isBackstab;
    }
    private static void ExecutionBefore(Player __instance, Enemy targetAgent, out ExecutionReceipt __state)
    {
        __state = _execution;
        _execution = Active && __instance.IsOwner && !__instance.IsDead && targetAgent is not null && !targetAgent.IsDead && !targetAgent.IsPlayerTeam
            ? new ExecutionReceipt { Player = __instance, Enemy = targetAgent, Source = __instance.NetworkObjectId, TargetId = TargetIdentity(targetAgent) } : null;
    }
    private static Exception ExecutionFinally(Exception __exception, ExecutionReceipt __state) { _execution = __state; return __exception; }
    private static void EnemySpawned(Enemy __instance) => TargetIdentity(__instance);
    private static void AgentDespawned(NetworkAgent __instance)
    { if (__instance.TryCast<Enemy>() is not null) { Targets.Despawn(__instance.Pointer); TargetReferences.Remove(__instance.Pointer); } }
    private static void ProjectileBefore(Projectile __instance, bool isAOE, out Contact __state)
    {
        __state = new Contact { Parent = _contact, AttributionBefore = _attribution };
        // Only a mapped, directly launched attack qualifies. Explosions and
        // other generated damage never inherit a nearby/last attack by accident.
        _contact = null;
        _attribution = null;
        if (Active && NativeThrowing.TryGetAttack(__instance, out var attack) && attack.Player?.IsOwner == true)
        { _attribution = attack; if (!isAOE) _contact = new Contact { Parent = __state.Parent, Attack = attack }; }
    }
    private static Exception ProjectileFinally(Exception __exception, Contact __state)
    { _contact = __state?.Parent; _attribution = __state?.AttributionBefore; return __exception; }
    private static Exception ContactFinally(Exception __exception, Contact __state)
    { _contact = __state; return __exception; }

    private static void DamageBefore(NetworkHealthObject __instance, DamageEventSource damageSource, bool ignoreInvulnerabilityThisIsExecution,
        ref float damage, out DamageScope __state)
    {
        __state = new DamageScope { Parent = _damage, Target = __instance, Time = Time.unscaledTimeAsDouble };
        var dealer = damageSource?.DamageDealer;
        __state.Source = damageSource?.SourceNetworkObject?.NetworkObjectId ?? ulong.MaxValue;
        if (Active && damage > 0)
        {
            if (!_generated && !ignoreInvulnerabilityThisIsExecution && _damage is null && AttributedAttack?.Player?.IsOwner == true &&
                dealer?.Pointer == AttributedAttack.Player.Pointer) __state.AttributedAttack = AttributedAttack;
            if (ignoreInvulnerabilityThisIsExecution && _execution?.Enemy?.Pointer == __instance.Pointer && !Executions.Contains(_execution))
            { __state.Execution = _execution; Executions.Add(_execution); }
            var victim = __instance.TryCast<Player>();
            if (victim is not null) damage = (float)CombatServices.Reduce(victim, damage);
            var enemy = __instance.TryCast<Enemy>();
            var attack = _contact?.Attack;
            if (CombatServices.NeedsDirectContacts && !_generated && _damage is null && !ignoreInvulnerabilityThisIsExecution && damageSource?.TryCast<DamageEventSourceDirectionalAttack>() is not null &&
                !__instance.IsDead && attack?.Player is not null && __instance.Pointer != attack.Player.Pointer &&
                dealer?.Pointer == attack.Player.Pointer)
            {
                __state.Hit = new DirectHit { Id = "hit:" + ++_sequence, Attack = attack, Target = __instance, TargetId = TargetIdentity(__instance), TargetOrder = __instance.NetworkObjectId,
                    Kind = attack.Kind == DirectAttackKind.Melee && !damageSource.isMelee ? DirectAttackKind.OtherRanged : attack.Kind,
                    TargetIsHostileEnemy = enemy is not null && !enemy.IsPlayerTeam,
                    IsBackstab = attack.Backstabs.TryGetValue(__instance.Pointer, out var backstab) && backstab,
                    HealthBefore = Math.Max(0, __instance.health.Value), EffectiveMaximumBefore = __instance.MaxAttainableHealth, Damage = damage };
                CombatServices.Prepare(__state.Hit);
                damage = (float)Math.Clamp(__state.Hit.Damage, 0d, int.MaxValue / 4d);
                Pending.Add(__state);
            }
        }
        _damage = __state;
    }
    private static void DamageAfter(DamageEventData __result, DamageScope __state)
    {
        if (__state.Execution is not null && (__result is null || __result.damageAmount <= 0)) Executions.Remove(__state.Execution);
        if (__state.Hit is null || __state.Hit.Resolved) return;
        if (!__state.Executed && (__result is null || __result.damageAmount <= 0))
        { CombatServices.Resolve(__state.Hit, 0); Pending.Remove(__state); }
    }
    private static Exception DamageFinally(Exception __exception, DamageScope __state)
    {
        _damage = __state?.Parent;
        if (__exception is not null && __state?.Hit is not null && !__state.Hit.Resolved)
            FoundationServices.Logger?.LogError("[Foundation] damage attempt failed with an unresolved native receipt; charge remains reserved until receipt or run/battle reset");
        return __exception;
    }
    private static void ReceiptBefore(NetworkHealthObject __instance, ulong damageSourceID, bool isInternalDamageSource, RpcParams rpcParams, out Receipt __state)
    {
        __state = null;
        if (!Active) return;
        var route = __instance.Pointer.ToString() + ":" + damageSourceID;
        var current = _damage?.Target?.Pointer == __instance.Pointer && _damage.Source == damageSourceID ? _damage : null;
        if (__instance.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute)
        {
            // Preserve dispatch order for ALL HP RPCs, not only direct hits. A later bleed receipt from the
            // same attacker/target must never settle a different pending direct contact or execution.
            Dispatches.Enqueue(route, current); return;
        }
        if (!Dispatches.TryDequeue(route, out var scope)) scope = current;
        if (scope is not null) scope.Executed = true;
        NetworkAgent source = null;
        if (!isInternalDamageSource && __instance.NetworkManager?.SpawnManager?.SpawnedObjects is not null &&
            __instance.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(damageSourceID))
            source = __instance.NetworkManager.SpawnManager.SpawnedObjects[damageSourceID]?.GetComponent<NetworkAgent>();
        var execution = scope?.Execution;
        if (execution is not null) Executions.Remove(execution);
        __state = new Receipt { Before = Math.Max(0, __instance.health.Value), Player = __instance.TryCast<Player>(), Source = source, Scope = scope, Execution = execution };
        if (NativeMultiplayer.Online)
        {
            __state.Origin = rpcParams?.Receive.SenderClientId ?? __instance.NetworkManager.LocalClientId;
            __state.NetworkKey = NetworkCombat.Begin(__state.Origin, __instance.NetworkObjectId, damageSourceID);
        }
    }
    private static void ReceiptAfter(NetworkHealthObject __instance, Receipt __state)
    {
        if (__state is null) return;
        if (__state.NetworkKey is not null)
        {
            void Apply(MultiplayerPacket packet)
            {
                var actual = Math.Clamp(packet.Before - packet.After, 0, packet.Before);
                if (__state.Scope is not null)
                { CombatServices.Resolve(__state.Scope.Hit, actual); CombatServices.ConfirmAttack(__state.Scope.AttributedAttack, actual); Pending.Remove(__state.Scope); }
                if (actual > 0 && __state.Player?.IsOwner == true && packet.EnemySource) CombatServices.LostHp(__state.Player, actual);
                if (actual > 0 && __state.Execution is not null && packet.Dead) CombatServices.Executed(__state.Execution.Player, __state.Execution.Enemy, __state.Execution.TargetId);
            }
            if (__instance.IsServer) NetworkCombat.Confirm(__state.NetworkKey, __instance, __state.Source, __state.Origin, __state.Before, Apply);
            else if (__state.Scope?.Hit is not null || __state.Scope?.AttributedAttack is not null || __state.Execution is not null || (__state.Player?.IsOwner == true && __state.Source?.TryCast<Enemy>() is not null))
                NetworkCombat.Await(__state.NetworkKey, Apply);
            return;
        }
        var loss = Math.Clamp(__state.Before - Math.Max(0, __instance.health.Value), 0, __state.Before);
        if (__state.Scope is not null)
        { CombatServices.Resolve(__state.Scope.Hit, loss); CombatServices.ConfirmAttack(__state.Scope.AttributedAttack, loss); Pending.Remove(__state.Scope); }
        if (loss > 0 && __state.Player is not null && __state.Source is not null && __state.Source.isEnemy && !__state.Source.IsPlayerTeam)
            CombatServices.LostHp(__state.Player, loss);
        if (loss > 0 && __state.Execution is not null && __instance.IsDead)
            CombatServices.Executed(__state.Execution.Player, __state.Execution.Enemy, __state.Execution.TargetId);
    }
    private static void BreakBefore(Weapon __instance, int amount, out BreakScope __state)
    {
        __state = null;
        if (!Active || amount <= 0 || __instance.pickupData is null || __instance.IsDefault) return;
        __state = new BreakScope { Player = __instance.holderAgent?.TryCast<Player>(), Pickup = __instance.pickupData,
            Before = __instance.GetWeaponDisplayDurability(), Owner = Breaking.Add(__instance.Pointer), Weapon = __instance.Pointer,
            Amount = amount, Parent = _breakingScope };
        if (__state.Owner) _breakingScope = __state;
    }
    private static void BreakAfter(BreakScope __state)
    {
        if (__state is not null && __state.Owner && __state.Player?.IsOwner == true && !__state.Player.IsDead &&
            DurabilityTransition.IsBreak(__state.Before, __state.Pickup.durability, __state.Amount, __state.Destroyed, false) &&
            NativeWeaponIdentity.MarkBroken(__state.Pickup)) CombatServices.Broken(__state.Player);
    }
    private static void DestroyedDuringLoss(NetworkAgent __instance, bool destroyedOnExecution)
    { if (!destroyedOnExecution && _breakingScope?.Player?.Pointer == __instance.Pointer) _breakingScope.Destroyed = true; }
    private static Exception BreakFinally(Exception __exception, BreakScope __state)
    { if (__state?.Owner == true) { Breaking.Remove(__state.Weapon); _breakingScope = __state.Parent; } return __exception; }
    internal static void Tick()
    {
        NativeWeaponIdentity.FlushCosts();
        foreach (var pending in Pending.Where(value => !value.Warned && Time.unscaledTimeAsDouble - value.Time > 2d).ToArray())
        {
            pending.Warned = true;
            // A delayed receipt may still arrive. Releasing its reservation on
            // a timeout could let the same damage bonus be spent twice.
            FoundationServices.Logger?.LogWarning("[Foundation] direct hit awaits native HP confirmation; consumable bonus remains reserved, not replayed");
        }
    }
    internal static void RequireSettledCheckpoint()
    {
        NetworkCombat.RequireSettled();
        NativeWeaponIdentity.FlushCosts();
        if (Pending.Any(value => value.Hit?.Resolved == false) || Executions.Count > 0 || Dispatches.Count > 0)
            throw new InvalidOperationException("Custom checkpoint awaits unresolved direct-hit HP receipts; previous save preserved");
    }
    private static void Reset()
    { NetworkCombat.Reset(); foreach (var pending in Pending.ToArray()) CombatServices.Resolve(pending.Hit, 0); Pending.Clear(); Dispatches.Clear(); Executions.Clear(); Actions.Clear(); WeaponAttempts.Clear(); Breaking.Clear(); if (!NativePersistence.IsRestoring) { Targets.Clear(); TargetReferences.Clear(); } _contact = null; _attribution = null; _payment = null; SpecialPayment = false; _generated = false; _damage = null; _breakingScope = null; _execution = null; }
}
