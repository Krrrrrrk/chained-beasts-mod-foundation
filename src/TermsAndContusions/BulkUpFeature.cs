using System;
using System.Collections.Generic;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using ChainedBeasts.ModFoundation;
using ChainedBeasts.ModFoundation.Logic;
using ChainedBeasts.ModFoundation.Runtime;
using ChainedBeasts.ModFoundation.Stats;
using TermsAndContusions.Skills;

namespace TermsAndContusions;

internal static class BulkUpFeature
{
    private static bool Enabled => FoundationServices.Content.TryGet(CustomContentIds.BulkUpKey, out var entry) && entry.Enabled;
    internal const int BulkUpSkillId = CustomContentIds.BulkUpSkill;
    internal const string BulkUpName = "Bulk Up";
    internal const string BulkUpDescription = "TC: Doubles your maximum and current health.";
    internal const string BulkUpNameTerm = "ModFoundation/BulkUp/Name";
    internal const string BulkUpDescriptionTerm = "ModFoundation/BulkUp/Description";
    private static readonly SkillType BulkUpSkillType = (SkillType)BulkUpSkillId;
    private static readonly object StateLock = new();
    private static readonly HashSet<int> ForcedOfferLogLayers = new();
    private static ManualLogSource _logger;
    private static bool _forceFirstOffer;
    private static bool _registrationReady;
    private static bool _definitionAdded;

    internal static void Install(Harmony harmony, ManualLogSource logger, bool forceFirstOffer)
    {
        _logger = logger;
        _forceFirstOffer = forceFirstOffer;
        _registrationReady = false;
        _definitionAdded = false;
        ForcedOfferLogLayers.Clear();
        FoundationServices.RegisterLibraryCallbacks(CustomContentIds.BulkUpKey, GameLibraryInitPrefix, GameLibraryInitPostfix);
        void Patch(string name, Type[] parameters, string prefix = null, string postfix = null) =>
            FeaturePatcher.Patch(harmony, typeof(BulkUpFeature),
                FeaturePatcher.RequireMethod(typeof(StringManager), name, parameters), prefix, postfix);
        Patch(nameof(StringManager.GetSkillName), new[] { typeof(SkillType) }, nameof(GetSkillNamePrefix));
        Patch(nameof(StringManager.GetSkillNameTerm), new[] { typeof(SkillType) }, nameof(GetSkillNameTermPrefix));
        Patch(nameof(StringManager.GetSkillDescTerm), new[] { typeof(SkillType) }, nameof(GetSkillDescTermPrefix));
        FeaturePatcher.Patch(harmony, typeof(BulkUpFeature),
            FeaturePatcher.RequireMethod(typeof(RunDataManager), nameof(RunDataManager.GetTrainingSkills), typeof(int)),
            postfix: nameof(GetTrainingSkillsPostfix));
        FoundationServices.RegisterModifier(CustomContentIds.BulkUpKey, StatChannel.MaximumHealth,
            context => SkillMath.BulkUp(SkillsFeature.Rank(context.Data, CustomSkill.BulkUp)));
        FoundationServices.OwnershipChanged += OnOwnershipChanged;
        FoundationServices.RunReset += () => ForcedOfferLogLayers.Clear();
        _logger?.LogInfo("[Terms & Contusions] Bulk Up uses canonical skill mutations and shared checkpoint persistence; maxRank=4");
    }

    private static void GameLibraryInitPrefix(GameLibrary __instance)
    {
        var skills = __instance.allSkills
            ?? throw new InvalidOperationException("GameLibrary.allSkills is null before GameLibrary.Init.");

        Sprite icon = null;
        for (var index = 0; index < skills.Count; index++)
        {
            var skill = skills[index];
            if (skill is null)
            {
                continue;
            }

            if ((int)skill.skillType == BulkUpSkillId)
            {
                if (_definitionAdded && string.Equals(skill.name, "BulkUp", StringComparison.Ordinal))
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"GameLibrary already contains SkillType {BulkUpSkillId}; Bulk Up registration stopped to avoid a collision.");
            }

            if (skill.skillType == SkillType.MetalPlate)
            {
                icon = skill.sprite;
            }
        }

        if (icon is null)
        {
            for (var index = 0; index < skills.Count; index++)
            {
                if (skills[index] is not null && skills[index].sprite is not null)
                {
                    icon = skills[index].sprite;
                    break;
                }
            }
        }

        var bulkUp = new SkillDef
        {
            name = "BulkUp",
            skillType = BulkUpSkillType,
            releaseStatus = ReleaseStatus.InFullGame,
            offerModifiers = (OfferModifiers)0,
            offerType = OfferType.Normal,
            maxLevel = 4,
            requiresCategory = (SkillCategory)0,
            unlocksCategory = (SkillCategory)0,
            tooltipCategories = StatusTooltipType.None,
            sprite = icon
        };

        skills.Add(bulkUp);
        _definitionAdded = true;
    }

    private static void GameLibraryInitPostfix(GameLibrary __instance)
    {
        _registrationReady = __instance.SkillDict is not null && __instance.SkillDict.ContainsKey(BulkUpSkillType);
        if (!_registrationReady) throw new InvalidOperationException("Bulk Up was not indexed after GameLibrary.Init");
        _logger?.LogInfo($"[Terms & Contusions] Bulk Up registered in GameLibrary.AllSkills and SkillDict as {BulkUpSkillId}");
    }

    private static bool GetSkillNamePrefix(SkillType skillType, ref string __result)
    {
        if (!Enabled || skillType != BulkUpSkillType)
        {
            return true;
        }

        __result = CustomLocalization.GetText(BulkUpNameTerm, BulkUpName);
        return false;
    }

    private static bool GetSkillNameTermPrefix(SkillType skillType, ref string __result)
    {
        if (!Enabled || skillType != BulkUpSkillType)
        {
            return true;
        }

        __result = BulkUpNameTerm;
        return false;
    }

    private static bool GetSkillDescTermPrefix(SkillType skillType, ref string __result)
    {
        if (!Enabled || skillType != BulkUpSkillType)
        {
            return true;
        }

        __result = SkillsFeature.DescriptionTerm(CustomSkill.BulkUp);
        return false;
    }


    private static void OnOwnershipChanged(OwnershipChange change)
    {
        if (change.Content.Key != CustomContentIds.BulkUpKey || change.Before == change.After) return;
        var data = change.Data;
        var maximum = data.MaxHealth;
        var attainable = data.MaxAttainableHealth;
        data.hp = HealthTransition.Scale(change.HealthBefore, change.MaximumBefore, maximum, attainable);
        if (change.HasRuntimeHealth)
        {
            var player = data.PlayerObject;
            if (player is not null && player.IsServer && player.health is not null)
                player.health.Value = HealthTransition.Scale(change.RuntimeHealthBefore, change.MaximumBefore, maximum, attainable);
        }
        _logger?.LogInfo($"[Terms & Contusions] Bulk Up ownership changed {change.Before}->{change.After}; hp={data.hp}, max={maximum}");
    }

    private static void GetTrainingSkillsPostfix(
        RunDataManager __instance,
        int layer,
        Il2CppSystem.Collections.Generic.List<SkillType> __result)
    {
        if (!SessionAccess.CanUseDeveloperTools || !_forceFirstOffer || !_registrationReady || __result is null || __result.Count == 0)
        {
            return;
        }

        try
        {
            var playerDatas = __instance.playerDatas;
            if (playerDatas is not null)
            {
                for (var playerIndex = 0; playerIndex < playerDatas.Count; playerIndex++)
                {
                    if (HasBulkUp(playerDatas[playerIndex]))
                    {
                        return;
                    }
                }
            }

            // GetTrainingSkills returns a new list on every call. Acquisition
            // and the later reveal each request their own copy. Persist the
            // forced slot in the backing row so ownership cannot make the
            // reveal fall back to the native skill that occupied this slot.
            var layers = __instance.trainingSkills;
            if (layers is null || layer < 0 || layer >= layers.Count)
            {
                return;
            }

            var storedOffer = layers[layer];
            var offerIndex = 0;
            for (var index = 0; index < __result.Count; index++)
            {
                if (__result[index] == BulkUpSkillType)
                {
                    offerIndex = index;
                    break;
                }
            }

            if (storedOffer is null || offerIndex >= storedOffer.Count)
            {
                return;
            }

            storedOffer[offerIndex] = BulkUpSkillType;
            __result[offerIndex] = BulkUpSkillType;

            lock (StateLock)
            {
                if (ForcedOfferLogLayers.Add(layer))
                {
                    _logger?.LogInfo(
                        $"[Terms & Contusions] Bulk Up stored in training offer layer {layer}, slot {offerIndex}; " +
                        "selection, acquisition, and reveal use the same offer");
                }
            }
        }
        catch (Exception exception)
        {
            _logger?.LogError(
                $"[Terms & Contusions] ForceBulkUpFirstOffer failed: " +
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }


    private static bool HasBulkUp(PlayerData data) => data is not null && data.HasSkill(BulkUpSkillType) > 0;
}
