using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using ChainedBeasts.ModFoundation;
using ChainedBeasts.ModFoundation.Runtime;
using ChainedBeasts.ModFoundation.Networking;
using ChainedBeasts.ModFoundation.State;
using ChainedBeasts.ModFoundation.Stats;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace TermsAndContusions.Surgeries;

internal static class SurgeriesFeature
{
    private const string StateKey = "terms-and-contusions:surgeries/battle-v1";
    private const string CounterTag = "terms-and-contusions:counterweight-hit";
    private const string KnockbackTag = "terms-and-contusions:counterweight-knockback";
    private static readonly Dictionary<CustomSurgery, string> Names = new();
    private static readonly Dictionary<CustomSurgery, string> Descriptions = new();
    private static readonly Dictionary<int, SurgeryBattleState> States = new();
    private static ManualLogSource _logger;
    private static bool Active => SessionAccess.GameplayReady;
    private static double Now => Time.timeAsDouble;
    private static bool Enabled(SurgeryEntry entry) => entry is not null && FoundationServices.Content.TryGet(entry.Key, out var content) && content.Enabled;
    private static bool Has(PlayerData data, CustomSurgery kind) => Active && Enabled(SurgeryDefinitions.Get(kind)) && SurgeryServices.IsOwned(data, SurgeryDefinitions.Get(kind).Key);
    private static SurgeryBattleState State(PlayerData data)
    { if (!States.TryGetValue(data.playerID, out var state)) States[data.playerID] = state = new SurgeryBattleState(); return state; }
    internal static void Install(Harmony harmony, ManualLogSource logger, IReadOnlyDictionary<CustomSurgery, int> prices, bool forceAdrenal)
    {
        _logger = logger;
        foreach (var entry in SurgeryDefinitions.All.Where(Enabled))
        {
            if (entry.Kind == CustomSurgery.AdrenalGraft)
            { Names[entry.Kind] = AdrenalGraftFeature.NameTerm; Descriptions[entry.Kind] = AdrenalGraftFeature.DescriptionTerm; }
            else
            { Names[entry.Kind] = FoundationText.Register(entry.Key, "Name", entry.Name); Descriptions[entry.Kind] = FoundationText.Register(entry.Key, "Description", entry.Description); }
            var price = prices[entry.Kind];
            SurgeryServices.Register(entry.Key, price, () => new Surgery((SurgeryType)entry.Id, SurgeryCategoryIcon.Enhancements)
                { moneyCost = price, damage = 0, isDonation = false, isRare = false }, entry.Kind == CustomSurgery.AdrenalGraft && forceAdrenal);
        }
        void Patch(Type type, string method, string before = null, string after = null, params Type[] args) =>
            FeaturePatcher.Patch(harmony, typeof(SurgeriesFeature), FeaturePatcher.RequireMethod(type, method, args), before, after);
        Patch(typeof(StringManager), nameof(StringManager.GetSurgeryName), nameof(Name), args: new[] { typeof(Surgery) });
        Patch(typeof(StringManager), nameof(StringManager.GetSurgeryDesc), nameof(Description), args: new[] { typeof(Surgery), typeof(InjuryContainer), typeof(Il2CppReferenceArray<Il2CppSystem.Collections.Generic.Dictionary<InjuryType, int>>), typeof(int), typeof(bool) });
        if (Enabled(SurgeryDefinitions.Get(CustomSurgery.FuryPacemaker)))
        {
            // Closed enum instantiations at the actual timer/stack writes, below local and RPC wrappers.
            Patch(typeof(NetworkAgentEffect<NetworkAttackEffects.eType>), nameof(NetworkAgentEffect<NetworkAttackEffects.eType>.ModifyTimedEffect), nameof(FuryTimed), args: new[] { typeof(NetworkAttackEffects.eType), typeof(float) });
            Patch(typeof(NetworkAgentEffect<NetworkAttackEffects.eType>), nameof(NetworkAgentEffect<NetworkAttackEffects.eType>.ModifyEffectStack), nameof(FuryStack), args: new[] { typeof(NetworkAttackEffects.eType), typeof(int) });
        }
        Register(CustomSurgery.AdrenalGraft, StatChannel.StaminaRecoveryRate);
        Register(CustomSurgery.AnchorHeels, StatChannel.IncomingKnockback);
        Register(CustomSurgery.AnchorHeels, StatChannel.DodgeDistance);
        Register(CustomSurgery.SpringLoadedKnees, StatChannel.DodgeDistance);
        Register(CustomSurgery.SpringLoadedKnees, StatChannel.DodgeStaminaCost);
        Register(CustomSurgery.DenseSkeleton, StatChannel.MaximumStamina);
        Register(CustomSurgery.AuxiliaryTendons, StatChannel.MaximumHealth);
        Register(CustomSurgery.MarrowEnrichment, StatChannel.MaximumHealth);
        Register(CustomSurgery.MarrowEnrichment, StatChannel.HealingAmount);
        Register(CustomSurgery.PainConverter, StatChannel.MaximumStamina);
        var talons = SurgeryDefinitions.Get(CustomSurgery.RetractableTalons);
        if (Enabled(talons)) FoundationServices.RegisterModifier(talons.Key, StatChannel.AttackDamage,
            context => context.Attack is null ? 1 : SurgeryMath.WeaponDamage(Has(context.Data, talons.Kind), context.Attack.IsUnarmed));
        CombatServices.IncomingDamage += (player, amount) => amount * (Has(player.PlayerData, CustomSurgery.DenseSkeleton) ? .85 : 1);
        CombatServices.MeleePaymentMultiplier += attack => SurgeryMath.RegularMelee(Has(attack.Player.PlayerData, CustomSurgery.AuxiliaryTendons), CombatServices.IsSpecialMeleePayment);
        CombatServices.EnemyHpLost += EnemyDamage;
        CombatServices.PreparingDirectHit += Preparing;
        CombatServices.DirectHitResolved += Resolved;
        CombatServices.OutgoingKnockbackMultiplier += Knockback;
        FoundationServices.GameplayTick += Tick;
        FoundationServices.RunReset += States.Clear;
        FoundationServices.BattleStarted += _ => { foreach (var state in States.Values) state.ResetBattle(); };
        FoundationServices.CheckpointCapturing += Capture;
        FoundationServices.CheckpointValidating += Validate;
        FoundationServices.CheckpointRestored += _ => Restore();
        logger.LogInfo("[Terms & Contusions] eleven-surgery catalog installed; shared offers, native completion receipts and native-history ownership enabled");
    }
    private static void Register(CustomSurgery kind, StatChannel channel)
    { var entry = SurgeryDefinitions.Get(kind); if (Enabled(entry)) FoundationServices.RegisterModifier(entry.Key, channel, context => Has(context.Data, kind) ? SurgeryMath.Factor(kind, channel) : 1); }
    private static bool Name(Surgery surgery, ref string __result)
    { var entry = surgery is null ? null : SurgeryDefinitions.Find((int)surgery.surgeryType); if (!Enabled(entry)) return true; __result = Names[entry.Kind]; return false; }
    private static bool Description(Surgery surgery, ref string __result)
    {
        var entry = surgery is null ? null : SurgeryDefinitions.Find((int)surgery.surgeryType); if (!Enabled(entry)) return true;
        __result = entry.Kind == CustomSurgery.AdrenalGraft ? CustomLocalization.GetText(Descriptions[entry.Kind], entry.Description)
            : FoundationText.TryGet(Descriptions[entry.Kind], out var text) ? text : entry.Description;
        return false;
    }
    private static void EnemyDamage(Player player, int actual)
    {
        if (!Active || !CombatServices.ConfirmedAlive(player) || !player.IsOwner || actual <= 0) return;
        if (Has(player.PlayerData, CustomSurgery.PainConverter)) RestoreStamina(player, CustomSurgery.PainConverter, SurgeryMath.Pain(actual, player.MaxHealth, player.StaminaMax));
        State(player.PlayerData).Clot(Has(player.PlayerData, CustomSurgery.EmergencyClotting), actual, player.MaxHealth, true, Now);
    }
    private static void RestoreStamina(Player player, CustomSurgery kind, double amount)
    {
        var restored = CombatServices.RestoreStaminaGrantWithReceipt(player, amount);
        CombatServices.NotifyActivation(player, SurgeryDefinitions.Get(kind).Key, restored);
    }
    private static void Preparing(DirectHit hit)
    {
        if (!Active || hit.Kind != DirectAttackKind.Melee) return;
        var player = hit.Attack.Player;
        if (State(player.PlayerData).ReserveCounterweight(hit.Id, Has(player.PlayerData, CustomSurgery.CounterweightImplant), Now))
        {
            hit.Tags[CounterTag] = true; hit.MultiplyDamage(1.25);
            if (!hit.Attack.Tags.TryGetValue(KnockbackTag, out var value)) hit.Attack.Tags[KnockbackTag] = value = new Dictionary<string, DirectHit>();
            ((Dictionary<string, DirectHit>)value)[hit.TargetId] = hit;
        }
    }
    private static void Resolved(DirectHit hit)
    {
        if (!Active) return;
        var player = hit.Attack.Player; var state = State(player.PlayerData);
        if (hit.Tags.ContainsKey(CounterTag))
        {
            if (state.ResolveCounterweight(hit.Id, hit.ActualHpLoss))
                CombatServices.NotifyActivation(player, SurgeryDefinitions.Get(CustomSurgery.CounterweightImplant).Key, hit.ActualHpLoss);
            if (hit.ActualHpLoss <= 0 && hit.Attack.Tags.TryGetValue(KnockbackTag, out var value)) ((Dictionary<string, DirectHit>)value).Remove(hit.TargetId);
        }
        if (hit.ActualHpLoss > 0 && hit.TargetIsHostileEnemy && CombatServices.ConfirmedAlive(hit.Target) && !string.IsNullOrWhiteSpace(hit.TargetId) && hit.Kind == DirectAttackKind.Melee && hit.Attack.IsUnarmed && Has(player.PlayerData, CustomSurgery.RetractableTalons))
        {
            state.BleedHit(hit.TargetId, Now);
            CombatServices.NotifyActivation(player, SurgeryDefinitions.Get(CustomSurgery.RetractableTalons).Key, hit.ActualHpLoss);
        }
    }
    private static double Knockback(AttackRecord attack, NetworkAgent target)
    {
        if (!Active || !Has(attack.Player.PlayerData, CustomSurgery.CounterweightImplant)) return 1;
        if (!attack.Tags.TryGetValue(KnockbackTag, out var value)) return 1;
        var hits = (Dictionary<string, DirectHit>)value; var id = CombatServices.TargetIdentity(target);
        if (!hits.Remove(id, out var hit) || (hit.Resolved && hit.ActualHpLoss <= 0)) return 1;
        // The native successful TakeDamage call precedes TryKnockback in AttackEffect. If its HP RPC is deferred,
        // the same contact stays reserved until the receipt; no other target can take this charge meanwhile.
        return 1.75;
    }
    private static bool FuryTimed(NetworkAgentEffect<NetworkAttackEffects.eType> __instance, NetworkAttackEffects.eType effect, float duration) => Fury(__instance, effect, duration > 0);
    private static bool FuryStack(NetworkAgentEffect<NetworkAttackEffects.eType> __instance, NetworkAttackEffects.eType effect, int delta) => Fury(__instance, effect, delta > 0);
    private static bool Fury(NetworkAgentEffect<NetworkAttackEffects.eType> effects, NetworkAttackEffects.eType effect, bool positive)
    {
        if (!Active || effect != NetworkAttackEffects.eType.Rage || !positive) return true;
        if (SessionAccess.RuntimePlayerManager is not { } manager) return true;
        Player player = null;
        var dataList = manager.playerDatas;
        for (var i = 0; i < dataList.Count; i++)
        {
            var candidate = SessionAccess.RuntimePlayer(dataList[i]);
            if (candidate?.AttackEffects?.Pointer == effects.Pointer) { player = candidate; break; }
        }
        if (player is null || !Has(player.PlayerData, CustomSurgery.FuryPacemaker)) return true;
        // Reserve before restoring stamina. Nested/repeated callbacks still suppress fury without another grant.
        if (player.IsOwner && CombatServices.ConfirmedAlive(player) && State(player.PlayerData).Fury(true, Now)) RestoreStamina(player, CustomSurgery.FuryPacemaker, .3 * player.StaminaMax);
        return false;
    }
    private static bool Incapacitated(Player player) => player.IsEffectActive(NetworkAttackEffects.eType.Stun) || player.IsEffectActive(NetworkAttackEffects.eType.StunSilent) || player.IsEffectActive(NetworkAttackEffects.eType.Trip);
    private static void Tick()
    {
        if (!Active || SessionAccess.RuntimePlayerManager is not { } manager) return;
        var dataList = manager.playerDatas;
        for (var i = 0; i < dataList.Count; i++)
        {
            var data = dataList[i]; var player = SessionAccess.RuntimePlayer(data); if (player is null || !player.IsOwner) continue;
            var state = State(data);
            if (player.IsDead || !player.InBattle) { state.DiscardLiveEffects(); continue; }
            state.Recovery(Incapacitated(player), Has(data, CustomSurgery.CounterweightImplant), Now, !player.IsIncapacitated);
            var heal = state.ClotTick(Now);
            if (heal > 0 && Has(data, CustomSurgery.EmergencyClotting))
                CombatServices.RestoreHealthGrant(player, SurgeryDefinitions.Get(CustomSurgery.EmergencyClotting).Key, heal);
            foreach (var tick in state.BleedTick(Now))
            {
                var target = CombatServices.FindLiveTarget(tick.Key); var enemy = target?.TryCast<Enemy>();
                if (!Has(data, CustomSurgery.RetractableTalons) || enemy is null || enemy.IsDead || enemy.IsPlayerTeam) { state.RemoveBleed(tick.Key); continue; }
                // Coalesce overdue cadence ticks into one bounded native call after a slow frame.
                CombatServices.DealGeneratedDamage(player, enemy, tick.Value);
            }
        }
    }
    private static void Capture(RunDataManager manager)
    { for (var i = 0; i < manager.playerDatas.Count; i++) { var data = manager.playerDatas[i]; if (!MultiplayerServices.CapturePlayer(data)) continue; FoundationServices.PlayerState(data).SetFeature(StateKey, State(data).Capture(Now)); } }
    private static void Validate(CheckpointEnvelope snapshot)
    { foreach (var player in snapshot.Players.Values) { var value = player.GetFeature<SurgerySnapshot>(StateKey); if (value is not null) SurgeryBattleState.Restore(value, 0); } }
    private static void Restore()
    {
        States.Clear(); if (!Active) return;
        var manager = NetworkSession.Instance.runDataManager;
        for (var i = 0; i < manager.playerDatas.Count; i++)
        {
            var data = manager.playerDatas[i]; var saved = FoundationServices.PlayerState(data).GetFeature<SurgerySnapshot>(StateKey); if (saved is null) continue;
            var state = SurgeryBattleState.Restore(saved, Now); var player = SessionAccess.RuntimePlayer(data);
            state.RebaseIncapacitation(player is not null && Incapacitated(player));
            foreach (var bleed in saved.Bleeds) if (!CombatServices.HasLiveTarget(bleed.Target))
            { state.RemoveBleed(bleed.Target); _logger?.LogInfo("[Terms & Contusions] discarded restored Talons bleed whose native arena target no longer exists"); }
            States[data.playerID] = state;
        }
    }
}
