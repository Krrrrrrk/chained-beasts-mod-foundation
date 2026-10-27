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
using UnityEngine;

namespace TermsAndContusions.Equipment;

internal static class EquipmentFeature
{
    private const string StateKey = "terms-and-contusions:equipment/battle-v1";
    private const string OpeningTag = "terms-and-contusions:scabbard-plan";
    private const string ChalkTag = "terms-and-contusions:chalk-release";
    private const string ChalkFeedbackTag = "terms-and-contusions:chalk-confirmed";
    private const string HoodTag = "terms-and-contusions:hood-hit";
    private const string SpursTag = "terms-and-contusions:spurs-hit";
    private static int _scabbardHitsLogged, _chalkHitsLogged, _chalkReleasesLogged;
    private static ManualLogSource _logger;
    private static readonly Dictionary<int, EquipmentBattleState> States = new();
    private static readonly Dictionary<CustomEquipment, string> Names = new(), Descriptions = new();
    private static readonly HashSet<string> Added = new();
    private static double Now => Time.timeAsDouble;
    private static bool Active => SessionAccess.GameplayReady;
    private static EquipmentBattleState State(PlayerData data)
    { if (!States.TryGetValue(data.playerID, out var value)) States.Add(data.playerID, value = new EquipmentBattleState()); return value; }
    private static bool Enabled(EquipmentEntry entry) => entry is not null && FoundationServices.Content.TryGet(entry.Key, out var value) && value.Enabled;
    private static bool Has(PlayerData data, CustomEquipment kind) => Enabled(EquipmentDefinitions.Get(kind)) && data?.currentEquipment == (PurchaseType)EquipmentDefinitions.Get(kind).Id;
    private static bool Has(Player player, CustomEquipment kind) => player is not null && Has(player.PlayerData, kind);
    private static int Gold => NetworkSession.Instance.runDataManager.Money;
    internal static void Install(Harmony harmony, ManualLogSource logger)
    {
        _logger = logger;
        foreach (var entry in EquipmentDefinitions.All.Where(Enabled))
        {
            Names[entry.Kind] = FoundationText.Register(entry.Key, "Name", entry.Name);
            Descriptions[entry.Kind] = FoundationText.Register(entry.Key, "Description", entry.Description);
            FoundationServices.RegisterLibraryCallbacks(entry.Key, library => Add(library, entry), library =>
            { if (library.PurchaseDict?.ContainsKey((PurchaseType)entry.Id) != true) throw new InvalidOperationException("Equipment not indexed: " + entry.Key); });
        }
        FeaturePatcher.Patch(harmony, typeof(EquipmentFeature), FeaturePatcher.RequireMethod(typeof(StringManager), nameof(StringManager.GetPurchaseName), typeof(PurchaseType)), prefix: nameof(Name));
        FeaturePatcher.Patch(harmony, typeof(EquipmentFeature), FeaturePatcher.RequireMethod(typeof(StringManager), nameof(StringManager.GetPurchaseDesc), typeof(PurchaseType)), prefix: nameof(Description));
        if (Enabled(EquipmentDefinitions.Get(CustomEquipment.MercenarysLedger)))
            FoundationServices.RegisterModifier(EquipmentDefinitions.Get(CustomEquipment.MercenarysLedger).Key, StatChannel.AttackDamage,
                context => Active && context.Player is not null && context.Player.InBattle
                    ? EquipmentMath.Ledger(Has(context.Data, CustomEquipment.MercenarysLedger), SessionAccess.IsSoloMode ? State(context.Data).LedgerPaid : MultiplayerServices.LedgerPaid(context.Data.playerID)) : 1d);
        CombatServices.PreparingDirectHit += Prepare;
        CombatServices.DirectHitResolved += Resolved;
        CombatServices.IncomingDamage += Incoming;
        CombatServices.EnemyHpLost += LostHp;
        CombatServices.ExecutionConfirmed += Execution;
        CombatServices.AttackCommitted += Committed;
        CombatServices.WeaponRecovered += Recovered;
        FoundationServices.GameplayTick += Tick;
        FoundationServices.EquipmentChanged += (data, before, after) => State(data).EquipmentChanged();
        FoundationServices.RunReset += () => States.Clear();
        FoundationServices.BattleStarted += Battle;
        FoundationServices.CheckpointCapturing += Capture;
        FoundationServices.CheckpointValidating += Validate;
        FoundationServices.CheckpointRestored += _ => Restore();
    }
    private static void Add(GameLibrary library, EquipmentEntry entry)
    {
        PurchaseDef visual = null;
        for (var i = 0; i < library.allPurchases.Count; i++)
        {
            var item = library.allPurchases[i]; if (item is null) continue;
            if ((int)item.purchaseType == entry.Id)
            { if (Added.Contains(entry.Key) && item.name == entry.Key) return; throw new InvalidOperationException("Foreign equipment ID collision: " + entry.Key); }
            if (item.purchaseType == PurchaseType.RunningShoes) visual = item;
        }
        if (visual is null) throw new InvalidOperationException("Native equipment visual reference missing");
        library.allPurchases.Add(new PurchaseDef { name = entry.Key, purchaseType = (PurchaseType)entry.Id, releaseStatus = ReleaseStatus.InFullGame,
            offerModifiers = (OfferModifiers)0, category = PurchaseCategory.Equipment, cost = entry.Price, multiplayerScaling = 0,
            tooltipCategories = StatusTooltipType.None, modelPrefab = visual.modelPrefab, sprite = visual.sprite });
        Added.Add(entry.Key);
    }
    private static bool Name(PurchaseType purchaseType, ref string __result)
    { var entry = EquipmentDefinitions.Find((int)purchaseType); if (!Enabled(entry)) return true; __result = Names[entry.Kind]; return false; }
    private static bool Description(PurchaseType purchaseType, ref string __result)
    { var entry = EquipmentDefinitions.Find((int)purchaseType); if (!Enabled(entry)) return true; __result = Descriptions[entry.Kind]; return false; }
    private static void Battle(string key)
    {
        States.Clear();
        if (!Active || (!SessionAccess.IsSoloMode && !MultiplayerServices.IsHost)) return;
        var manager = NetworkSession.Instance.runDataManager;
        for (var i = 0; i < manager.playerDatas.Count; i++)
        {
            var data = manager.playerDatas[i]; var state = State(data); var before = manager.Money;
            if (state.BeginBattle(Has(data, CustomEquipment.MercenarysLedger), before) == 0) continue;
            manager.ModifyMoney(-5);
            state.ConfirmPayment(before, manager.Money);
            MultiplayerServices.SetLedgerPaid(data.playerID, state.LedgerPaid);
        }
        MultiplayerServices.PublishSharedMoney();
    }
    private static void Prepare(DirectHit hit)
    {
        if (!Active) return;
        var player = hit.Attack.Player; var melee = hit.Kind == DirectAttackKind.Melee;
        var hood = EquipmentMath.Hood(Has(player, CustomEquipment.ExecutionersHood), melee, hit.TargetIsHostileEnemy, hit.HealthBefore, hit.EffectiveMaximumBefore);
        var spurs = EquipmentMath.Spurs(Has(player, CustomEquipment.DuelistsSpurs), melee, SoleHostile(player, hit.Target));
        hit.MultiplyDamage(hood); hit.MultiplyDamage(spurs);
        if (hood > 1d) hit.Tags[HoodTag] = true;
        if (spurs > 1d) hit.Tags[SpursTag] = true;
        if (hit.Kind == DirectAttackKind.ThrownWeapon && Has(player, CustomEquipment.ChalkBag) && hit.Attack.Tags.TryGetValue(ChalkTag, out var chalk) && chalk is true) hit.MultiplyDamage(1.4d);
        var weapon = hit.Attack.Weapon;
        if (weapon is not null)
        {
            var plan = State(player.PlayerData).PlanOpening(hit.Id, weapon.Id, Has(player, CustomEquipment.BorrowersScabbard),
                EquipmentMath.Full(hit.Attack.WeaponDurabilityAtCommit, hit.Attack.WeaponMaximumDurabilityAtCommit, hit.Attack.WeaponIsDefaultAtCommit));
            hit.Tags[OpeningTag] = plan;
            if (plan.Bonus) hit.MultiplyDamage(1.5d);
        }
    }
    private static void Resolved(DirectHit hit)
    {
        if (!Active) return;
        var player = hit.Attack.Player; var state = State(player.PlayerData);
        if (hit.Tags.TryGetValue(OpeningTag, out var plan) && state.ResolveOpening((OpeningPlan)plan, hit.ActualHpLoss > 0))
        {
            CombatServices.ConsumeOpeningDurability(hit.Attack.Weapon);
            CombatServices.NotifyActivation(player, EquipmentDefinitions.Get(CustomEquipment.BorrowersScabbard).Key, hit.ActualHpLoss);
            if (_scabbardHitsLogged++ < 3)
                _logger?.LogInfo(FormattableString.Invariant(
                    $"[Terms & Contusions] Borrower's Scabbard opening hit confirmed: kind={hit.Kind}, durabilityAtCommit={hit.Attack.WeaponDurabilityAtCommit}/{hit.Attack.WeaponMaximumDurabilityAtCommit}, multiplier=1.5, preparedDamage={hit.Damage:0.###}, actualHpLoss={hit.ActualHpLoss}; additional durability requested (clamped if already broken)"));
        }
        if (hit.ActualHpLoss > 0)
        {
            if (hit.Tags.ContainsKey(HoodTag)) CombatServices.NotifyActivation(player, EquipmentDefinitions.Get(CustomEquipment.ExecutionersHood).Key, hit.ActualHpLoss);
            if (hit.Tags.ContainsKey(SpursTag)) CombatServices.NotifyActivation(player, EquipmentDefinitions.Get(CustomEquipment.DuelistsSpurs).Key, hit.ActualHpLoss);
            if (hit.Kind == DirectAttackKind.ThrownWeapon && hit.Attack.Tags.TryGetValue(ChalkTag, out var chalk) && chalk is true && hit.Attack.Tags.TryAdd(ChalkFeedbackTag, true))
            {
                CombatServices.NotifyActivation(player, EquipmentDefinitions.Get(CustomEquipment.ChalkBag).Key, hit.ActualHpLoss);
                if (_chalkHitsLogged++ < 3)
                    _logger?.LogInfo(FormattableString.Invariant(
                        $"[Terms & Contusions] Chalk Bag prepared throw hit confirmed: multiplier=1.4, preparedDamage={hit.Damage:0.###}, actualHpLoss={hit.ActualHpLoss}"));
            }
        }
        if (hit.TargetIsHostileEnemy && state.ClaimBackstab(Has(player, CustomEquipment.PickpocketsMitts), hit.ActualHpLoss > 0 && hit.IsBackstab, hit.TargetId))
            AddGold(player);
    }
    private static void AddGold(Player player)
    {
        if (!SessionAccess.IsSoloMode) { MultiplayerServices.RequestGold(player, EquipmentDefinitions.Get(CustomEquipment.PickpocketsMitts).Key, 1); return; }
        var manager = NetworkSession.Instance.runDataManager; var before = manager.Money;
        if (before < int.MaxValue) manager.ModifyMoney(1);
        if (manager.Money > before) CombatServices.NotifyActivation(player, EquipmentDefinitions.Get(CustomEquipment.PickpocketsMitts).Key, manager.Money - before);
    }
    private static double Incoming(Player player, double damage) => damage * EquipmentMath.Purse(Active && Has(player, CustomEquipment.LeadLinedPurse), Active ? Gold : 0);
    private static void LostHp(Player player, int actualLoss)
    {
        if (Active && State(player.PlayerData).ClaimLocket(Has(player, CustomEquipment.LastGaspLocket), actualLoss, CombatServices.ConfirmedHealth(player), player.MaxHealth))
        {
            var granted = CombatServices.RestoreStaminaGrantWithReceipt(player, .5d * player.StaminaMax);
            if (granted > 0) CombatServices.NotifyActivation(player, EquipmentDefinitions.Get(CustomEquipment.LastGaspLocket).Key, granted);
        }
    }
    private static void Execution(Player player, Enemy enemy, string targetId)
    {
        if (!Active || player.IsDead || !State(player.PlayerData).ClaimExecution(Has(player, CustomEquipment.ButchersApron), true, targetId)) return;
        CombatServices.RestoreHealthGrant(player, EquipmentDefinitions.Get(CustomEquipment.ButchersApron).Key, EquipmentMath.ExecutionHealBase(player.MaxHealth));
    }
    private static bool Stationary(Player player)
    { var velocity = player.CurrentVelocity; return velocity.x * velocity.x + velocity.z * velocity.z <= .0001f; }
    private static bool AimEligible(Player player) => CombatServices.IsPreparingNormalThrow(player) && Stationary(player);
    private static void Tick()
    {
        if (!Active || SessionAccess.RuntimePlayerManager is not { } manager)
        { foreach (var state in States.Values) state.EquipmentChanged(); return; }
        var datas = manager.playerDatas;
        for (var i = 0; i < datas.Count; i++)
        {
            var data = datas[i]; if (data is null) continue;
            var player = SessionAccess.RuntimePlayer(data);
            State(data).Aim(Now, player is not null && Has(player, CustomEquipment.ChalkBag) && AimEligible(player));
        }
    }
    private static void Committed(AttackRecord attack)
    {
        if (!Active || attack.Kind != DirectAttackKind.ThrownWeapon || attack.Player is not { } player) return;
        // Native FinalizeWindup has already changed AttackState to WindDown before
        // ExecuteThrow commits this action. IsAimingThrow is false at this point.
        // The accumulated preparation still comes solely from eligible aiming ticks.
        var eligible = EquipmentMath.ChalkReleaseEligible(!player.IsDead, player.IsOwner, player.ThrowOffhandActive,
            player.CurrentWeapon is not null, Stationary(player));
        var state = State(player.PlayerData);
        var equipped = Has(player, CustomEquipment.ChalkBag);
        var heldSeconds = state.ChalkPreparationSeconds(Now);
        var ready = state.ReleaseThrow(Now, equipped, eligible);
        attack.Tags[ChalkTag] = ready;
        if (equipped && _chalkReleasesLogged < 3)
        {
            _chalkReleasesLogged++;
            _logger?.LogInfo(FormattableString.Invariant(
                $"[Terms & Contusions] Chalk Bag release: preparedSeconds={heldSeconds:0.###}, releaseEligible={eligible}, empowered={ready}"));
        }
    }
    private static void Recovered(Player player, Weapon weapon, PhysicalWeapon physical, bool ownThrow)
    {
        if (!Active || !State(player.PlayerData).ClaimRepair(Has(player, CustomEquipment.SalvagersHook), ownThrow, physical.Id, physical.Durability, physical.MaximumDurability)) return;
        var before = physical.Data.durability;
        physical.Data.durability = EquipmentMath.Repair(before, physical.MaximumDurability);
        player.RefreshWeaponDurabilityDisplay(weapon);
        weapon.CheckDurabilityPulse();
        if (physical.Data.durability > before) CombatServices.NotifyActivation(player, EquipmentDefinitions.Get(CustomEquipment.SalvagersHook).Key, physical.Data.durability - before);
    }
    private static bool SoleHostile(Player player, NetworkHealthObject target)
    {
        if (!EnemyManager.InstanceExists) return false;
        var enemies = EnemyManager.Instance.currentEnemies; Enemy sole = null; var count = 0;
        for (var i = 0; i < (enemies?.Count ?? 0); i++)
        {
            var enemy = enemies[i];
            if (enemy is null || !enemy.isActiveAndEnabled || enemy.IsDead || enemy.IsPlayerTeam || (enemy.transform.position - player.transform.position).sqrMagnitude > 16f) continue;
            sole = enemy; if (++count > 1) return false;
        }
        return count == 1 && sole?.Pointer == target.Pointer;
    }
    private static void Capture(RunDataManager manager)
    { for (var i = 0; i < manager.playerDatas.Count; i++) { var data = manager.playerDatas[i]; if (!MultiplayerServices.CapturePlayer(data)) continue; FoundationServices.PlayerState(data).SetFeature(StateKey, State(data).Capture(Now)); } }
    private static void Validate(CheckpointEnvelope snapshot)
    { foreach (var player in snapshot.Players.Values) { var saved = player.GetFeature<EquipmentSnapshot>(StateKey); if (saved is not null) EquipmentBattleState.Restore(saved); } }
    private static void Restore()
    {
        if (!Active) return;
        var manager = NetworkSession.Instance.runDataManager;
        for (var i = 0; i < manager.playerDatas.Count; i++)
        { var data = manager.playerDatas[i]; var saved = FoundationServices.PlayerState(data).GetFeature<EquipmentSnapshot>(StateKey); if (saved is not null) States[data.playerID] = EquipmentBattleState.Restore(saved); }
    }
}
