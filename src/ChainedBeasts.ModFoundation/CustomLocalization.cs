using System;
using BepInEx.Logging;
using HarmonyLib;
using I2.Loc;
using UnityEngine;

namespace ChainedBeasts.ModFoundation;

/// <summary>
/// Supplies text at both localization boundaries used by the game.
///
/// StringManager is used by most generated tooltips, while several offer and
/// character-sheet panels assign an I2 term directly. Unknown enum values are
/// formatted as their numeric value, so both numeric and readable aliases are
/// intentionally recognized.
/// </summary>
internal static class CustomLocalization
{
    private static bool _runnersWrapsEnabled;
    private static bool _leadfootEnabled;
    private static bool _adrenalGraftEnabled;
    private static bool _bulkUpEnabled;

    internal static void Install(
        Harmony harmony,
        ManualLogSource logger,
        bool bulkUpEnabled,
        bool runnersWrapsEnabled,
        bool leadfootEnabled,
        bool adrenalGraftEnabled)
    {
        _bulkUpEnabled = bulkUpEnabled;
        _runnersWrapsEnabled = runnersWrapsEnabled;
        _leadfootEnabled = leadfootEnabled;
        _adrenalGraftEnabled = adrenalGraftEnabled;

        FeaturePatcher.Patch(
            harmony,
            typeof(CustomLocalization),
            FeaturePatcher.RequireMethod(typeof(StringManager), nameof(StringManager.GetLocalisedTerm), typeof(string)),
            prefix: nameof(GetLocalisedTermPrefix));
        FeaturePatcher.Patch(
            harmony,
            typeof(CustomLocalization),
            FeaturePatcher.RequireMethod(
                typeof(LocalizationManager),
                nameof(LocalizationManager.GetTranslation),
                typeof(string), typeof(bool), typeof(int), typeof(bool), typeof(bool),
                typeof(GameObject), typeof(string), typeof(bool)),
            prefix: nameof(GetTranslationPrefix));
        FeaturePatcher.Patch(
            harmony,
            typeof(CustomLocalization),
            FeaturePatcher.RequireMethod(
                typeof(LocalizationManager),
                nameof(LocalizationManager.TryGetTranslation),
                typeof(string), typeof(string).MakeByRefType(), typeof(bool), typeof(int), typeof(bool), typeof(bool),
                typeof(GameObject), typeof(string), typeof(bool)),
            prefix: nameof(TryGetTranslationPrefix));

        logger?.LogInfo(
            "[ChainedBeasts.ModFoundation] custom localization hooks installed for StringManager and I2 Get/TryGetTranslation");
    }

    private static bool GetLocalisedTermPrefix(string termKey, ref string __result)
    {
        if (!TryGetText(termKey, out var text))
        {
            return true;
        }

        __result = text;
        return false;
    }

    private static bool GetTranslationPrefix(string Term, ref string __result)
    {
        if (!TryGetText(Term, out var text))
        {
            return true;
        }

        __result = text;
        return false;
    }

    private static bool TryGetTranslationPrefix(string Term, ref string Translation, ref bool __result)
    {
        if (!TryGetText(Term, out var text))
        {
            return true;
        }

        Translation = text;
        __result = true;
        return false;
    }

    private static bool TryGetText(string term, out string text)
    {
        text = null;
        if (string.IsNullOrEmpty(term))
        {
            return false;
        }

        if (_bulkUpEnabled)
        {
            if (Matches(term,
                    BulkUpFeature.BulkUpNameTerm,
                    $"Skills/{CustomContentIds.BulkUpSkill}_Name"))
            {
                text = BulkUpFeature.BulkUpName;
                return true;
            }

            if (Matches(term,
                    BulkUpFeature.BulkUpDescriptionTerm,
                    $"Skills/{CustomContentIds.BulkUpSkill}_Desc"))
            {
                text = BulkUpFeature.BulkUpDescription;
                return true;
            }
        }

        if (_runnersWrapsEnabled)
        {
            if (Matches(term,
                    $"Purchases/{CustomContentIds.RunnersWrapsPurchase}_Name",
                    "Purchases/RunnersWraps_Name",
                    Equipment.RunnersWrapsFeature.NameTerm))
            {
                text = Equipment.RunnersWrapsFeature.DisplayName;
                return true;
            }

            if (Matches(term,
                    $"Purchases/{CustomContentIds.RunnersWrapsPurchase}_Desc",
                    "Purchases/RunnersWraps_Desc",
                    Equipment.RunnersWrapsFeature.DescriptionTerm))
            {
                text = Equipment.RunnersWrapsFeature.Description;
                return true;
            }
        }

        if (_leadfootEnabled)
        {
            if (Matches(term,
                    $"Injuries/{CustomContentIds.LeadfootInjury}_Name",
                    "Injuries/Leadfoot_Name",
                    "ModFoundation/Leadfoot/Name"))
            {
                text = Afflictions.LeadfootFeature.DisplayName;
                return true;
            }

            if (Matches(term,
                    $"Injuries/{CustomContentIds.LeadfootInjury}_Threat",
                    "Injuries/Leadfoot_Threat",
                    "ModFoundation/Leadfoot/Threat"))
            {
                text = "slow you down";
                return true;
            }

            if (TryGetLeadfootSeverity(term, out var severity))
            {
                text = $"Movement speed reduced by {severity * 10}%.";
                return true;
            }
        }

        if (_adrenalGraftEnabled)
        {
            if (Matches(term,
                    $"Surgeries/{CustomContentIds.AdrenalGraftSurgery}_Name",
                    "Surgeries/AdrenalGraft_Name",
                    Surgeries.AdrenalGraftFeature.NameTerm))
            {
                text = Surgeries.AdrenalGraftFeature.DisplayName;
                return true;
            }

            if (Matches(term,
                    $"Surgeries/{CustomContentIds.AdrenalGraftSurgery}_Desc",
                    "Surgeries/AdrenalGraft_Desc",
                    Surgeries.AdrenalGraftFeature.DescriptionTerm))
            {
                text = Surgeries.AdrenalGraftFeature.Description;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetLeadfootSeverity(string term, out int severity)
    {
        var prefixes = new[]
        {
            "ModFoundation/Leadfoot/Description/",
            $"Injuries/{CustomContentIds.LeadfootInjury}_Desc_",
            "Injuries/Leadfoot_Desc_"
        };

        foreach (var prefix in prefixes)
        {
            if (!term.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var suffix = term.Substring(prefix.Length);
            severity = int.TryParse(suffix, out var parsed)
                ? Math.Max(1, Math.Min(3, parsed))
                : 1;
            return true;
        }

        severity = 0;
        return false;
    }

    private static bool Matches(string value, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.Equals(value, candidate, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
