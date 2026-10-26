using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using ChainedBeasts.ModFoundation;
using ChainedBeasts.ModFoundation.Content;
using ChainedBeasts.ModFoundation.Runtime;
using ChainedBeasts.ModFoundation.Stats;
using HarmonyLib;
using I2.Loc;
using TMPro;
using UnityEngine;

namespace TermsAndContusions.Skills;

internal static class SkillsFeature
{
    private const string FeatureStateKey = "terms-and-contusions:skills/combat-v1";
    private const string PickTag = "terms-and-contusions:pick-ready";
    private const string PlanTag = "terms-and-contusions:hit-plan";
    private static readonly Dictionary<int, SkillCombatState> States = new();
    private static readonly Dictionary<CustomSkill, string> Names = new();
    private static readonly Dictionary<CustomSkill, string> Descriptions = new();
    private static readonly Dictionary<(CustomSkill, int), string> RankTerms = new();
    private static readonly Dictionary<(CustomSkill, int), string> OfferTerms = new();
    private static readonly HashSet<string> Added = new();
    private static readonly Dictionary<int, int> PickEpochs = new();
    private static double Now => Time.timeAsDouble;
    private static double _battleStartedAt;

    internal static bool Enabled(SkillEntry entry) => entry is not null && FoundationServices.Content.TryGet(entry.Key, out var content) && content.Enabled;
    internal static int Rank(PlayerData data, CustomSkill kind)
    {
        var entry = SkillDefinitions.Get(kind);
        return Enabled(entry) && data is not null ? SkillMath.Rank(data.HasSkill((SkillType)entry.Id)) : 0;
    }
    internal static string DescriptionTerm(CustomSkill kind)
    {
        var data = SoloData();
        return data is null ? Descriptions[kind] : RankTerms[(kind, Rank(data, kind))];
    }
    private static SkillCombatState State(Player player)
    {
        var id = player.PlayerData.playerID;
        if (!States.TryGetValue(id, out var state)) States.Add(id, state = new SkillCombatState(_battleStartedAt));
        return state;
    }
    internal static void Install(Harmony harmony, ManualLogSource logger)
    {
        foreach (var entry in SkillDefinitions.All.Where(Enabled))
        {
            Names[entry.Kind] = FoundationText.Register(entry.Key, "Name", entry.Name);
            Descriptions[entry.Kind] = FoundationText.Register(entry.Key, "Description", Summary(entry.Kind));
            for (var rank = 0; rank <= 4; rank++)
            {
                RankTerms[(entry.Kind, rank)] = FoundationText.Register(entry.Key, "Rank/" + rank, SkillMath.Description(entry.Kind, rank));
                OfferTerms[(entry.Kind, rank)] = FoundationText.Register(entry.Key, "Offer/" + rank, SkillMath.OfferDescription(entry.Kind, rank));
            }
            if (entry.Kind != CustomSkill.BulkUp)
                FoundationServices.RegisterLibraryCallbacks(entry.Key, library => AddDefinition(library, entry), library => Verify(library, entry));
        }
        void Patch(Type type, string method, string postfix = null, string prefix = null, params Type[] args) =>
            FeaturePatcher.Patch(harmony, typeof(SkillsFeature), FeaturePatcher.RequireMethod(type, method, args), prefix, postfix);
        Patch(typeof(StringManager), nameof(StringManager.GetSkillName), prefix: nameof(Name), args: new[] { typeof(SkillType) });
        Patch(typeof(StringManager), nameof(StringManager.GetSkillNameTerm), prefix: nameof(NameTerm), args: new[] { typeof(SkillType) });
        Patch(typeof(StringManager), nameof(StringManager.GetSkillDescTerm), prefix: nameof(DescTerm), args: new[] { typeof(SkillType) });
        Patch(typeof(UILobbyTrainerUpgradePage), nameof(UILobbyTrainerUpgradePage.Refresh), nameof(Trainer), args: new[] { typeof(PlayerData), typeof(int), typeof(bool), typeof(bool) });
        Patch(typeof(UILobbyTrainerUpgradePage), nameof(UILobbyTrainerUpgradePage.SetSkill), nameof(SkillSet), args: new[] { typeof(SkillType), typeof(bool) });
        Patch(typeof(UIGameLevelLobbyCharacterSkillInfo), nameof(UIGameLevelLobbyCharacterSkillInfo.Initialise), nameof(Character), args: new[] { typeof(SkillType), typeof(int) });
        Patch(typeof(UIPlayerInfoTooltip), nameof(UIPlayerInfoTooltip.Initialise), nameof(Tooltip), args: new[] { typeof(SkillType), typeof(bool) });

        Register(CustomSkill.CatchYourBreath, StatChannel.RecoveryDelayClockRate,
            context => SkillMath.StationaryDelayRate(Rank(context.Data, CustomSkill.CatchYourBreath), Stationary(context.Player)));
        Register(CustomSkill.ExpensiveTaste, StatChannel.AttackDamage, context => SkillMath.GoldDamage(Rank(context.Data, CustomSkill.ExpensiveTaste), Gold()));
        Register(CustomSkill.PickYourMoment, StatChannel.AttackDamage, PickContribution);
        Register(CustomSkill.Longshot, StatChannel.ThrowRange, context => SkillMath.ThrowMultiplier(Rank(context.Data, CustomSkill.Longshot), false));
        Register(CustomSkill.Longshot, StatChannel.ThrowChargeSpeed, context => SkillMath.ThrowMultiplier(Rank(context.Data, CustomSkill.Longshot), false));
        CombatServices.AttackCommitted += Committed;
        CombatServices.PreparingDirectHit += Preparing;
        CombatServices.DirectHitResolved += Resolved;
        CombatServices.EnemyHpLost += HpLost;
        CombatServices.WeaponBroken += Broken;
        CombatServices.IncomingDamage += Incoming;
        FoundationServices.RunReset += Clear;
        FoundationServices.BattleStarted += _ => Clear();
        FoundationServices.CheckpointCapturing += Capture;
        FoundationServices.CheckpointValidating += ValidateSnapshot;
        FoundationServices.CheckpointRestored += _ => Restore();
        FoundationServices.OwnershipChanged += Ownership;
        logger.LogInfo("[Terms & Contusions] four-rank skills registered; direct-hit/HP-loss/weapon-break and matched checkpoint state enabled");
    }
    private static string Summary(CustomSkill kind) => SkillMath.Effect(kind, 1);
    private static void Register(CustomSkill kind, StatChannel channel, Func<NativeStatContext, double> factor)
    { var entry = SkillDefinitions.Get(kind); if (Enabled(entry)) FoundationServices.RegisterModifier(entry.Key, channel, factor); }
    private static void AddDefinition(GameLibrary library, SkillEntry entry)
    {
        var visuals = new ChainedBeasts.ModFoundation.Logic.VisualReferenceSelection<Sprite>();
        for (var i = 0; i < library.allSkills.Count; i++)
        {
            var skill = library.allSkills[i];
            if (skill is null) continue;
            if ((int)skill.skillType == entry.Id)
            { if (Added.Contains(entry.Key) && skill.name == entry.Key) return; throw new InvalidOperationException("Foreign SkillDef collision: " + entry.Key); }
            var source = entry.Kind == CustomSkill.Longshot ? SkillType.ThrowCharge : SkillType.StrengthTraining;
            if (Enum.IsDefined(typeof(SkillType), (int)skill.skillType)) visuals.Consider(skill.sprite, skill.skillType == source);
        }
        var icon = visuals.Selected;
        if (icon is null) throw new InvalidOperationException("Native skill visual source missing: " + entry.Key);
        library.allSkills.Add(new SkillDef { name = entry.Key, skillType = (SkillType)entry.Id, releaseStatus = ReleaseStatus.InFullGame,
            offerModifiers = (OfferModifiers)0, offerType = OfferType.Normal, maxLevel = 4, requiresCategory = (SkillCategory)0,
            unlocksCategory = (SkillCategory)0, tooltipCategories = StatusTooltipType.None, sprite = icon });
        Added.Add(entry.Key);
    }
    private static void Verify(GameLibrary library, SkillEntry entry)
    { if (library.SkillDict is null || !library.SkillDict.ContainsKey((SkillType)entry.Id)) throw new InvalidOperationException("Skill was not indexed: " + entry.Key); }
    private static bool Name(SkillType skillType, ref string __result)
    {
        var entry = SkillDefinitions.Find((int)skillType);
        if (!Enabled(entry) || entry.Kind == CustomSkill.BulkUp) return true;
        __result = FoundationText.TryGet(Names[entry.Kind], out var text) ? text : entry.Name; return false;
    }
    private static bool NameTerm(SkillType skillType, ref string __result)
    {
        var entry = SkillDefinitions.Find((int)skillType);
        if (!Enabled(entry) || entry.Kind == CustomSkill.BulkUp) return true;
        __result = Names[entry.Kind]; return false;
    }
    private static bool DescTerm(SkillType skillType, ref string __result)
    {
        var entry = SkillDefinitions.Find((int)skillType);
        if (!Enabled(entry)) return true;
        __result = DescriptionTerm(entry.Kind); return false;
    }
    private static void Trainer(UILobbyTrainerUpgradePage __instance, PlayerData playerData)
    {
        var entry = SkillDefinitions.Find((int)__instance.SkillType);
        playerData ??= SoloData();
        if (Enabled(entry)) SetDescription(__instance.itemDescription, OfferTerms[(entry.Kind, SkillMath.Rank(playerData?.HasSkill((SkillType)entry.Id) ?? 0))]);
    }
    private static void SkillSet(UILobbyTrainerUpgradePage __instance) => Trainer(__instance, __instance.playerData);
    private static void Character(UIGameLevelLobbyCharacterSkillInfo __instance, SkillType skill, int level)
    {
        var entry = SkillDefinitions.Find((int)skill);
        if (!Enabled(entry)) return;
        __instance.skillName.text = StringManager.GetSkillName(skill) + " " + SkillMath.Rank(level);
        __instance.skillDescription.text = SkillMath.Description(entry.Kind, level);
    }
    private static void Tooltip(UIPlayerInfoTooltip __instance, SkillType skill)
    {
        var entry = SkillDefinitions.Find((int)skill);
        if (!Enabled(entry)) return;
        var data = SoloData();
        SetDescription(__instance.description, data is null ? Descriptions[entry.Kind] : RankTerms[(entry.Kind, Rank(data, entry.Kind))]);
    }
    private static void SetDescription(Localize label, string term) { if (label is not null) label.SetTerm(term); }
    private static PlayerData SoloData() => SessionAccess.IsSoloMode && NetworkSession.InstanceExists && NetworkSession.Instance.runDataManager?.playerDatas?.Count == 1
        ? NetworkSession.Instance.runDataManager.playerDatas[0] : null;
    private static bool Stationary(Player player)
    { var velocity = player?.CurrentVelocity ?? Vector3.zero; return player is not null && velocity.x * velocity.x + velocity.z * velocity.z <= .0001f; }
    private static int Gold() => NetworkSession.InstanceExists ? NetworkSession.Instance.runDataManager?.Money ?? 0 : 0;
    private static int PickEpoch(PlayerData data) => PickEpochs.TryGetValue(data.playerID, out var value) ? value : 0;
    private static void Committed(AttackRecord attack)
    {
        attack.Tags[PickTag] = State(attack.Player).Commit(Now, Rank(attack.Player.PlayerData, CustomSkill.PickYourMoment));
        attack.Tags[PickTag + "/epoch"] = PickEpoch(attack.Player.PlayerData);
    }
    private static void Preparing(DirectHit hit)
    {
        var player = hit.Attack.Player; var data = player.PlayerData;
        var kind = (HitKind)hit.Kind;
        var plan = State(player).Plan(hit.Id, kind, Now, Rank(data, CustomSkill.MixedMartialArts), Rank(data, CustomSkill.PaybackPlan), data.MaxHealth);
        hit.Tags[PlanTag] = plan;
        hit.FlatDamageBonus += plan.Payback;
        hit.Damage = SkillMath.DirectDamage(hit.Damage, kind == HitKind.Melee, hit.Attack.BeforeCostStamina01,
            hit.HealthBefore, hit.EffectiveMaximumBefore, Rank(data, CustomSkill.MeasuredViolence), Rank(data, CustomSkill.RunningOnFumes),
            hit.TargetIsHostileEnemy ? Rank(data, CustomSkill.FreshMeat) : 0, 0, false, plan.Multiplier, plan.Payback);
    }
    private static double PickContribution(NativeStatContext context)
    {
        var attack = context.Attack;
        if (attack is null || context.Data is null || !attack.Tags.TryGetValue(PickTag, out var ready) || ready is not true ||
            !attack.Tags.TryGetValue(PickTag + "/epoch", out var epoch)) return 1;
        // Attempt-wide bonus also covers an attributed area attack. Direct-contact processing must not add it again.
        return SkillMath.CommittedPick(Rank(context.Data, CustomSkill.PickYourMoment), true, (int)epoch, PickEpoch(context.Data));
    }
    private static void Resolved(DirectHit hit)
    {
        if (hit.Tags.TryGetValue(PlanTag, out var value))
            State(hit.Attack.Player).Resolve((HitPlan)value, hit.ActualHpLoss > 0, Now, Rank(hit.Attack.Player.PlayerData, CustomSkill.MixedMartialArts));
    }
    private static void HpLost(Player player, int actualLoss) => State(player).EnemyDamage(Now, Rank(player.PlayerData, CustomSkill.PaybackPlan), actualLoss, player.MaxHealth);
    private static void Broken(Player player)
    { var amount = SkillMath.BreakStamina(Rank(player.PlayerData, CustomSkill.WasteNot), player.StaminaMax); CombatServices.RestoreStaminaGrant(player, amount); }
    private static double Incoming(Player player, double damage)
    {
        var rank = Rank(player.PlayerData, CustomSkill.BreathingRoom);
        if (rank == 0 || !EnemyManager.InstanceExists) return damage;
        var enemies = EnemyManager.Instance.currentEnemies;
        var count = 0; var position = player.transform.position;
        for (var i = 0; i < (enemies?.Count ?? 0); i++)
        {
            var enemy = enemies[i];
            if (enemy is not null && enemy.isActiveAndEnabled && !enemy.IsDead && !enemy.IsPlayerTeam &&
                (enemy.transform.position - position).sqrMagnitude <= 9f) count++;
        }
        return damage * SkillMath.Incoming(rank, count);
    }
    private static void Ownership(OwnershipChange change)
    {
        if (change.Content.Key == SkillDefinitions.Get(CustomSkill.PickYourMoment).Key && change.After == 0)
            PickEpochs[change.Data.playerID] = PickEpoch(change.Data) + 1;
        if (!States.TryGetValue(change.Data.playerID, out var state)) return;
        state.OwnershipChanged(Rank(change.Data, CustomSkill.MixedMartialArts), Rank(change.Data, CustomSkill.PaybackPlan));
    }
    private static void Clear() { States.Clear(); PickEpochs.Clear(); _battleStartedAt = Now; }
    private static void Capture(RunDataManager manager)
    {
        for (var i = 0; i < manager.playerDatas.Count; i++)
        {
            var data = manager.playerDatas[i];
            if (!States.TryGetValue(data.playerID, out var state)) States[data.playerID] = state = new SkillCombatState(_battleStartedAt);
            FoundationServices.PlayerState(data).SetFeature(FeatureStateKey, state.Capture(Now, data.MaxHealth));
        }
    }
    private static void Restore()
    {
        if (!SessionAccess.CanUseDeveloperTools) return;
        var manager = NetworkSession.Instance.runDataManager;
        for (var i = 0; i < manager.playerDatas.Count; i++)
        {
            var data = manager.playerDatas[i];
            var saved = FoundationServices.PlayerState(data).GetFeature<SkillStateSnapshot>(FeatureStateKey);
            if (saved is not null) States[data.playerID] = SkillCombatState.Restore(saved, Now);
        }
    }
    private static void ValidateSnapshot(ChainedBeasts.ModFoundation.State.CheckpointEnvelope snapshot)
    {
        foreach (var state in snapshot.Players.Values)
        {
            var saved = state.GetFeature<SkillStateSnapshot>(FeatureStateKey);
            if (saved is not null) SkillCombatState.Restore(saved, 0d);
        }
    }
}
