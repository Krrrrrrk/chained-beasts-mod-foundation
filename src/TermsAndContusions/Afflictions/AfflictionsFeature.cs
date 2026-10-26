using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using ChainedBeasts.ModFoundation;
using ChainedBeasts.ModFoundation.Runtime;
using ChainedBeasts.ModFoundation.State;
using ChainedBeasts.ModFoundation.Stats;
using HarmonyLib;
using I2.Loc;
using UnityEngine;

namespace TermsAndContusions.Afflictions;

internal static class AfflictionsFeature
{
    private const string StateKey = "terms-and-contusions:afflictions/battle-v1";
    private const string AttackTag = "terms-and-contusions:affliction-attack";
    private static readonly Dictionary<int, AfflictionBattleState> States = new();
    private static readonly Dictionary<IntPtr, (Player Player, ExertionDelayState Delay)> Delays = new();
    private static readonly Dictionary<CustomAffliction, string> Names = new();
    private static readonly Dictionary<(CustomAffliction, int), string> RevealNames = new();
    private static readonly Dictionary<(CustomAffliction, int), string> Descriptions = new();
    private static readonly HashSet<string> Added = new();
    private static double _battleStart;
    private static ManualLogSource _logger;
    private static double Now => Time.timeAsDouble;
    private static bool Active => SessionAccess.CanUseDeveloperTools;
    private static bool Enabled(AfflictionEntry entry) => entry is not null && FoundationServices.Content.TryGet(entry.Key, out var value) && value.Enabled;
    private static int Effective(Player player, CustomAffliction kind) => Active && player is not null && Enabled(AfflictionDefinitions.Get(kind))
        ? AfflictionMath.Severity(player.HasInjury((InjuryType)AfflictionDefinitions.Get(kind).Id)) : 0;
    private static AfflictionBattleState State(PlayerData data)
    { if (!States.TryGetValue(data.playerID, out var state)) States.Add(data.playerID, state = new AfflictionBattleState(_battleStart)); return state; }
    private static AfflictionBattleState Sync(Player player)
    {
        var state = State(player.PlayerData);
        state.Sync(Effective(player, CustomAffliction.BrittleGuard), Effective(player, CustomAffliction.HeavyLanding),
            Effective(player, CustomAffliction.ObsessiveFocus), Effective(player, CustomAffliction.Overheating));
        return state;
    }
    private static AfflictionAttack Attack(AttackRecord record)
    {
        if (!record.Tags.TryGetValue(AttackTag, out var value)) record.Tags[AttackTag] = value = new AfflictionAttack();
        return (AfflictionAttack)value;
    }
    internal static void Install(Harmony harmony, ManualLogSource logger)
    {
        _logger = logger; _battleStart = Now;
        foreach (var entry in AfflictionDefinitions.All.Where(Enabled))
        {
            Names[entry.Kind] = FoundationText.Register(entry.Key, "Name", entry.Name);
            for (var severity = 0; severity <= 3; severity++)
                RevealNames[(entry.Kind, severity)] = FoundationText.Register(entry.Key, "RevealName/" + severity,
                    AfflictionMath.DisplayName(entry.Name, severity));
            for (var severity = 0; severity <= 3; severity++)
                Descriptions[(entry.Kind, severity)] = FoundationText.Register(entry.Key, "Description/" + severity, AfflictionMath.Description(entry.Kind, severity));
            FoundationServices.RegisterLibraryCallbacks(entry.Key, library => Add(library, entry), library =>
            { if (library.InjuryDict?.ContainsKey((InjuryType)entry.Id) != true) throw new InvalidOperationException("Affliction not indexed: " + entry.Key); });
        }
        void Patch(string name, string prefix, params Type[] args) => FeaturePatcher.Patch(harmony, typeof(AfflictionsFeature), FeaturePatcher.RequireMethod(typeof(StringManager), name, args), prefix: prefix);
        Patch(nameof(StringManager.GetInjuryNameTerm), nameof(NameTerm), typeof(InjuryType));
        Patch(nameof(StringManager.GetInjuryName), nameof(Name), typeof(InjuryType), typeof(int));
        Patch(nameof(StringManager.GetInjuryDescTerm), nameof(Description), typeof(InjuryType), typeof(int));
        // The reveal helper formats Injuries/{numeric ID}_Name directly; it never
        // calls GetInjuryNameTerm. Give custom injuries an owned term and skip it.
        Patch(nameof(StringManager.SetInjuryNameTermAndNumerals), nameof(RevealName), typeof(Localize), typeof(InjuryType), typeof(int));
        Register(CustomAffliction.BurningLungs, StatChannel.RegenerationAmount, context => AfflictionMath.Recovery(Effective(context.Player, CustomAffliction.BurningLungs), Moving(context.Player)));
        Register(CustomAffliction.HeavyLanding, StatChannel.MovementSpeed, context => context.Player is null || context.Player.IsDashing ? 1d
            : Sync(context.Player).Movement(Now, Effective(context.Player, CustomAffliction.HeavyLanding)));
        Register(CustomAffliction.ColdStart, StatChannel.AttackDamage, context => Active && context.Player?.InBattle == true
            ? AfflictionMath.Cold(Effective(context.Player, CustomAffliction.ColdStart), State(context.Data).BattleAge(Now)) : 1d);
        Register(CustomAffliction.ObsessiveFocus, StatChannel.AttackDamage, Focus);
        CombatServices.AttackCommitted += Committed;
        CombatServices.DirectHitResolved += Resolved;
        CombatServices.MeleePaymentMultiplier += Payment;
        CombatServices.StaminaSpent += Spent;
        CombatServices.RecoveryDelayUpdating += DelayUpdating;
        CombatServices.EnemyHpLost += EnemyDamage;
        CombatServices.ShieldBlocked += Blocked;
        CombatServices.DodgeCompleted += Dodged;
        CombatServices.CrowdFavourMultiplier += player => AfflictionMath.Favour(Effective(player, CustomAffliction.TinEar));
        FoundationServices.GameplayTick += Tick;
        FoundationServices.OwnershipChanged += change => { if (change.Content.Category == ChainedBeasts.ModFoundation.Content.ContentCategory.Affliction) RefreshRuntime(); };
        FoundationServices.EquipmentChanged += (_, _, _) => RefreshRuntime();
        FoundationServices.RunReset += Clear;
        FoundationServices.BattleStarted += _ => Clear();
        FoundationServices.CheckpointCapturing += Capture;
        FoundationServices.CheckpointValidating += Validate;
        FoundationServices.CheckpointRestored += _ => Restore();
        logger.LogInfo("[Terms & Contusions] ten potion afflictions registered; effective severity, native healing and schema-1 timed state enabled");
    }
    private static void Register(CustomAffliction kind, StatChannel channel, Func<NativeStatContext, double> factor)
    { var entry = AfflictionDefinitions.Get(kind); if (Enabled(entry)) FoundationServices.RegisterModifier(entry.Key, channel, factor); }
    private static void Add(GameLibrary library, AfflictionEntry entry)
    {
        Sprite visual = null;
        for (var i = 0; i < library.allInjuries.Count; i++)
        {
            var item = library.allInjuries[i]; if (item is null) continue;
            if ((int)item.injuryType == entry.Id)
            { if (Added.Contains(entry.Key) && item.name == entry.Key) return; throw new InvalidOperationException("Foreign affliction ID collision: " + entry.Key); }
            if (item.injuryType == InjuryType.WalkWithALimp) visual = item.sprite;
        }
        if (visual is null) throw new InvalidOperationException("Native affliction visual source missing");
        library.allInjuries.Add(new InjuryDef { name = entry.Key, injuryType = (InjuryType)entry.Id, releaseStatus = ReleaseStatus.InFullGame,
            offerModifiers = (OfferModifiers)0, offerType = OfferType.Normal, maxLevel = 3, canBeDisabled = true,
            requiresCategory = (SkillCategory)0, unlocksCategory = (SkillCategory)0, tooltipCategories = StatusTooltipType.None,
            bodyPart = entry.Kind switch {
                CustomAffliction.BrittleGuard => BodyPart.Arms,
                CustomAffliction.HeavyLanding => BodyPart.Legs,
                CustomAffliction.ShakyResolve or CustomAffliction.TinEar or CustomAffliction.ObsessiveFocus => BodyPart.Brain,
                CustomAffliction.LooseChange or CustomAffliction.ColdStart or CustomAffliction.Overheating => BodyPart.Behavioural,
                _ => BodyPart.None }, sprite = visual, test = false });
        Added.Add(entry.Key);
    }
    private static bool NameTerm(InjuryType injuryType, ref string __result)
    { var entry = AfflictionDefinitions.Find((int)injuryType); if (!Enabled(entry)) return true; __result = Names[entry.Kind]; return false; }
    private static bool Name(InjuryType injuryType, ref string __result)
    { var entry = AfflictionDefinitions.Find((int)injuryType); if (!Enabled(entry)) return true; __result = FoundationText.TryGet(Names[entry.Kind], out var text) ? text : entry.Name; return false; }
    private static bool Description(InjuryType injuryType, int level, ref string __result)
    { var entry = AfflictionDefinitions.Find((int)injuryType); if (!Enabled(entry)) return true; __result = Descriptions[(entry.Kind, AfflictionMath.Severity(level))]; return false; }
    private static bool RevealName(Localize localize, InjuryType injuryType, int severityLevel)
    {
        var entry = AfflictionDefinitions.Find((int)injuryType);
        if (!Enabled(entry)) return true;
        if (localize is not null) localize.SetTerm(RevealNames[(entry.Kind, AfflictionMath.Severity(severityLevel))]);
        return false;
    }
    private static bool Moving(Player player)
    { var velocity = player?.CurrentVelocity ?? Vector3.zero; return velocity.x * velocity.x + velocity.z * velocity.z > .0001f; }
    private static void Committed(AttackRecord record)
    { record.Tags[AttackTag] = Sync(record.Player).Commit(Now, record.Kind == DirectAttackKind.Melee, Effective(record.Player, CustomAffliction.Overheating)); }
    private static double Payment(AttackRecord record) => Sync(record.Player).MeleeMultiplier(Attack(record), Effective(record.Player, CustomAffliction.Overheating));
    private static double Focus(NativeStatContext context)
    {
        if (!Active || context.Player is null || context.Target?.TryCast<Enemy>() is not { } enemy || enemy.IsDead || enemy.IsPlayerTeam) return 1;
        var state = Sync(context.Player);
        var attempt = context.Attack is null ? new AfflictionAttack() : Attack(context.Attack);
        return state.FocusMultiplier(attempt, CombatServices.TargetIdentity(context.Target), Now, Effective(context.Player, CustomAffliction.ObsessiveFocus));
    }
    private static void Resolved(DirectHit hit)
    {
        if (!Active || !hit.TargetIsHostileEnemy || hit.ActualHpLoss <= 0) return;
        var state = Sync(hit.Attack.Player); var attempt = Attack(hit.Attack);
        state.SuccessfulDirectHit(attempt, hit.TargetId, hit.TargetOrder, Now, Effective(hit.Attack.Player, CustomAffliction.ObsessiveFocus), hit.ActualHpLoss);
    }
    private static void Spent(Player player, double actual, bool skipDelay)
    {
        if (!Active || actual <= 0 || skipDelay || !Enabled(AfflictionDefinitions.Get(CustomAffliction.LeadBelly))) return;
        if (!Delays.TryGetValue(player.Pointer, out var value)) Delays.Add(player.Pointer, value = (player, new ExertionDelayState()));
        player.staminaRecoveryTimer = (float)value.Delay.Begin(player.staminaRecoveryTimer, AfflictionMath.LeadDelay(Effective(player, CustomAffliction.LeadBelly)));
    }
    private static void DelayUpdating(Player player)
    {
        if (!Active) return;
        Sync(player);
        if (Delays.TryGetValue(player.Pointer, out var value))
            player.staminaRecoveryTimer = (float)value.Delay.Reconcile(player.staminaRecoveryTimer, AfflictionMath.LeadDelay(Effective(player, CustomAffliction.LeadBelly)));
    }
    private static void EnemyDamage(Player player, int actualLoss)
    {
        if (!Active) return;
        var state = Sync(player); var shaky = Effective(player, CustomAffliction.ShakyResolve); var loose = Effective(player, CustomAffliction.LooseChange);
        if (state.Shaky(Now, shaky, actualLoss))
        {
            var drain = AfflictionMath.ShakyDrain(shaky, player.StaminaMax, player.Stamina);
            if (drain > 0) player.ConsumeStamina((float)drain, true, 0); // Resource drain, not a new exertion/payment.
        }
        if (state.Loose(Now, loose, actualLoss))
        {
            var manager = NetworkSession.Instance.runDataManager; var loss = AfflictionMath.GoldLoss(loose, manager.Money);
            if (loss > 0) manager.ModifyMoney(-loss);
        }
    }
    private static void Blocked(ShieldBlock block)
    { if (Active && Sync(block.Player).Block(Effective(block.Player, CustomAffliction.BrittleGuard))) block.AdditionalDurabilityLoss++; }
    private static void Dodged(Player player)
    { if (Active) Sync(player).DodgeCompleted(Now, Effective(player, CustomAffliction.HeavyLanding)); }
    private static void RefreshRuntime()
    {
        if (!Active || !PlayerManager.InstanceExists || PlayerManager.Instance.PlayerCount == 0) return;
        var data = NetworkSession.Instance.runDataManager.playerDatas;
        for (var i = 0; i < data.Count; i++) { var player = data[i].PlayerObject; if (player is not null) DelayUpdating(player); }
    }
    private static void Tick() => RefreshRuntime();
    private static void Clear()
    {
        foreach (var value in Delays.Values)
            if (value.Player != null) value.Player.staminaRecoveryTimer = (float)value.Delay.Reconcile(value.Player.staminaRecoveryTimer, 0);
        Delays.Clear(); States.Clear(); _battleStart = Now;
    }
    private static void Capture(RunDataManager manager)
    { for (var i = 0; i < manager.playerDatas.Count; i++) { var data = manager.playerDatas[i]; FoundationServices.PlayerState(data).SetFeature(StateKey, State(data).Capture(Now)); } }
    private static void Validate(CheckpointEnvelope snapshot)
    { foreach (var player in snapshot.Players.Values) { var value = player.GetFeature<AfflictionSnapshot>(StateKey); if (value is not null) AfflictionBattleState.Restore(value, 0); } }
    private static void Restore()
    {
        if (!Active) return;
        var manager = NetworkSession.Instance.runDataManager;
        for (var i = 0; i < manager.playerDatas.Count; i++)
        {
            var data = manager.playerDatas[i]; var value = FoundationServices.PlayerState(data).GetFeature<AfflictionSnapshot>(StateKey);
            if (value is null) continue;
            var state = AfflictionBattleState.Restore(value, Now);
            if (state.FocusTarget is not null && !CombatServices.HasLiveTarget(state.FocusTarget))
            { state.ClearFocus(); _logger?.LogInfo("[Terms & Contusions] cleared restored focus whose arena target no longer exists"); }
            States[data.playerID] = state;
        }
    }
}
