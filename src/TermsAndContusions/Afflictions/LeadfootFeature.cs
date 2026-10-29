using ChainedBeasts.ModFoundation;
using ChainedBeasts.ModFoundation.Runtime;
using ChainedBeasts.ModFoundation.Stats;
using System;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

namespace TermsAndContusions.Afflictions;

internal static class LeadfootFeature
{
    private static bool Enabled => FoundationServices.Content.TryGet(CustomContentIds.LeadfootKey, out var entry) && entry.Enabled;
    internal const string DisplayName = "Leadfoot";

    private const string NameTerm = "ModFoundation/Leadfoot/Name";
    private const string DescriptionTermPrefix = "ModFoundation/Leadfoot/Description/";
    private const string ThreatTerm = "ModFoundation/Leadfoot/Threat";

    private static readonly InjuryType LeadfootInjuryType =
        (InjuryType)CustomContentIds.LeadfootInjury;
    private static ManualLogSource _logger;
    private static bool _forceNextOffer;
    private static bool _definitionAdded;
    private static bool _registrationReady;
    private static bool _forceLogWritten;
    internal static void Install(Harmony harmony, ManualLogSource logger, bool forceNextOffer)
    {
        if (Enum.IsDefined(typeof(InjuryType), CustomContentIds.LeadfootInjury))
        {
            throw new InvalidOperationException(
                $"InjuryType value {CustomContentIds.LeadfootInjury} is now native; Leadfoot stopped to avoid a collision.");
        }

        _logger = logger;
        _forceNextOffer = forceNextOffer;
        _definitionAdded = false;
        _registrationReady = false;
        _forceLogWritten = false;


        FoundationServices.RegisterLibraryCallbacks(CustomContentIds.LeadfootKey, GameLibraryInitPrefix, GameLibraryInitPostfix);
        FeaturePatcher.Patch(harmony, typeof(LeadfootFeature),
            FeaturePatcher.RequireMethod(typeof(StringManager), nameof(StringManager.GetInjuryNameTerm), typeof(InjuryType)),
            prefix: nameof(GetInjuryNameTermPrefix));
        FeaturePatcher.Patch(harmony, typeof(LeadfootFeature),
            FeaturePatcher.RequireMethod(typeof(StringManager), nameof(StringManager.GetInjuryName), typeof(InjuryType), typeof(int)),
            prefix: nameof(GetInjuryNamePrefix));
        FeaturePatcher.Patch(harmony, typeof(LeadfootFeature),
            FeaturePatcher.RequireMethod(typeof(StringManager), nameof(StringManager.GetInjuryDescTerm), typeof(InjuryType), typeof(int)),
            prefix: nameof(GetInjuryDescTermPrefix));
        FeaturePatcher.Patch(harmony, typeof(LeadfootFeature),
            FeaturePatcher.RequireMethod(typeof(InjuryGenerator), nameof(InjuryGenerator.GeneratePotions),
                typeof(RunDataManager), typeof(int), typeof(int), typeof(InjuryType).MakeByRefType()),
            postfix: nameof(GeneratePotionsPostfix));
        FoundationServices.RegisterModifier(CustomContentIds.LeadfootKey, StatChannel.MovementSpeed,
            context => 1d - 0.10d * ClampSeverityAllowZero(context.Player?.HasInjury(LeadfootInjuryType) ?? 0));
        // HasInjury applies the game's suppression and severity-reduction rules.

        _logger?.LogInfo(
            $"[Terms & Contusions] Leadfoot hooks installed: InjuryType={CustomContentIds.LeadfootInjury}, " +
            $"maxSeverity=3, forceNextOffer={_forceNextOffer}, persistence=Foundation");
    }

    private static void GameLibraryInitPrefix(GameLibrary __instance)
    {
        var injuries = __instance.allInjuries
            ?? throw new InvalidOperationException("GameLibrary.allInjuries is null before GameLibrary.Init.");

        Sprite icon = null;
        for (var index = 0; index < injuries.Count; index++)
        {
            var injury = injuries[index];
            if (injury is null)
            {
                continue;
            }

            if ((int)injury.injuryType == CustomContentIds.LeadfootInjury)
            {
                if (_definitionAdded && string.Equals(injury.name, "Leadfoot", StringComparison.Ordinal))
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"GameLibrary already contains InjuryType {CustomContentIds.LeadfootInjury}; registration stopped.");
            }

            if (injury.injuryType == InjuryType.WalkWithALimp)
            {
                icon = injury.sprite;
            }
        }

        if (icon is null)
        {
            throw new InvalidOperationException("The Broken Foot injury definition used for the Leadfoot icon was not found.");
        }

        injuries.Add(new InjuryDef
        {
            name = "Leadfoot",
            injuryType = LeadfootInjuryType,
            releaseStatus = ReleaseStatus.InFullGame,
            offerModifiers = (OfferModifiers)0,
            offerType = OfferType.Normal,
            maxLevel = 3,
            canBeDisabled = false,
            requiresCategory = (SkillCategory)0,
            unlocksCategory = (SkillCategory)0,
            tooltipCategories = StatusTooltipType.None,
            bodyPart = BodyPart.Legs,
            sprite = icon,
            test = false
        });

        _definitionAdded = true;
    }

    private static void GameLibraryInitPostfix(GameLibrary __instance)
    {
        _registrationReady = __instance.InjuryDict is not null &&
                             __instance.InjuryDict.ContainsKey(LeadfootInjuryType);

        if (_registrationReady)
        {
            _logger?.LogInfo(
                $"[Terms & Contusions] Leadfoot registered in GameLibrary as {CustomContentIds.LeadfootInjury}");
        }
        else
        {
            throw new InvalidOperationException("Leadfoot was not present in InjuryDict after GameLibrary.Init");
        }
    }

    private static bool GetInjuryNameTermPrefix(InjuryType injuryType, ref string __result)
    {
        if (!Enabled || injuryType != LeadfootInjuryType)
        {
            return true;
        }

        __result = NameTerm;
        return false;
    }

    private static bool GetInjuryNamePrefix(InjuryType injuryType, ref string __result)
    {
        if (!Enabled || injuryType != LeadfootInjuryType)
        {
            return true;
        }

        __result = CustomLocalization.GetText(NameTerm, DisplayName);
        return false;
    }

    private static bool GetInjuryDescTermPrefix(InjuryType injuryType, int level, ref string __result)
    {
        if (!Enabled || injuryType != LeadfootInjuryType)
        {
            return true;
        }

        __result = DescriptionTermPrefix + ClampSeverity(level);
        return false;
    }

    private static void GeneratePotionsPostfix(List<Potion> __result)
    {
        if (!ContentOfferServices.Allows(CustomContentIds.LeadfootKey) || !_forceNextOffer || !SessionAccess.CanUseDeveloperTools || !_registrationReady || __result is null || __result.Count == 0)
        {
            return;
        }

        for (var index = 0; index < __result.Count; index++)
        {
            if (__result[index] is not null && __result[index].injury == LeadfootInjuryType)
            {
                return;
            }
        }

        for (var index = 0; index < __result.Count; index++)
        {
            var original = __result[index];
            if (original is null || original.IsWater)
            {
                continue;
            }

            // Preserve the generator's healing amount, severity tier, and visual model.
            // Only the affliction identity and displayed name are replaced.
            __result[index] = new Potion
            {
                nameTerm = NameTerm,
                _healAmount = original._healAmount,
                injury = LeadfootInjuryType,
                potionSize = ClampSeverity(original.potionSize),
                shared = false,
                modelIdx = original.modelIdx
            };

            if (!_forceLogWritten)
            {
                _forceLogWritten = true;
                _logger?.LogInfo("[Terms & Contusions] developer offer override inserted Leadfoot into a normal injury-potion slot");
            }

            return;
        }
    }

    private static int ClampSeverity(int value)
    {
        return Math.Max(1, Math.Min(3, value));
    }

    private static int ClampSeverityAllowZero(int value)
    {
        return Math.Max(0, Math.Min(3, value));
    }
}
