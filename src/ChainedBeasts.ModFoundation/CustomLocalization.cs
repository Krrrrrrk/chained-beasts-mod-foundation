using System;
using BepInEx.Logging;
using HarmonyLib;
using I2.Loc;
using TMPro;
using UnityEngine;

namespace ChainedBeasts.ModFoundation;

/// <summary>
/// Supplies localized text at both lookup boundaries used by the game while
/// CustomTranslations registers the same keys in the live I2 term source.
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
    private static ManualLogSource _logger;
    private static int _bulkUpSetTermTargets;

    internal static void Install(
        Harmony harmony,
        ManualLogSource logger,
        bool bulkUpEnabled,
        bool runnersWrapsEnabled,
        bool leadfootEnabled,
        bool adrenalGraftEnabled)
    {
        _logger = logger;
        _bulkUpEnabled = bulkUpEnabled;
        _runnersWrapsEnabled = runnersWrapsEnabled;
        _leadfootEnabled = leadfootEnabled;
        _adrenalGraftEnabled = adrenalGraftEnabled;
        _bulkUpSetTermTargets = 0;
        CustomTranslations.Configure(
            logger,
            bulkUpEnabled,
            runnersWrapsEnabled,
            leadfootEnabled,
            adrenalGraftEnabled);

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
        FeaturePatcher.Patch(
            harmony,
            typeof(CustomLocalization),
            FeaturePatcher.RequireMethod(typeof(Localize), nameof(Localize.SetTerm), typeof(string)),
            postfix: nameof(LocalizeSetTermPostfix));
        FeaturePatcher.Patch(
            harmony,
            typeof(CustomLocalization),
            FeaturePatcher.RequireMethod(typeof(GameLibrary), nameof(GameLibrary.Init)),
            postfix: nameof(GameLibraryInitPostfix));

        logger?.LogInfo(
            "[ChainedBeasts.ModFoundation] custom localization hooks installed; runtime I2 term registration is pending GameLibrary.Init");
    }

    internal static string GetText(string term, string fallback)
    {
        return TryGetText(term, out var text) ? text : fallback;
    }

    private static void GameLibraryInitPostfix()
    {
        CustomTranslations.RegisterRuntimeTerms();
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

    private static void LocalizeSetTermPostfix(Localize __instance, string primary)
    {
        if (__instance is null || !TryGetText(primary, out var text))
        {
            return;
        }

        try
        {
            var target = __instance.GetComponent<TMP_Text>();
            if (target is null)
            {
                return;
            }

            // SetTerm has already completed. Writing the resolved value now
            // prevents a pooled widget from retaining its preceding native
            // string when its bound I2 source rejects a runtime-added term.
            target.text = text;

            if (_bulkUpEnabled)
            {
                if (Matches(primary,
                        BulkUpFeature.BulkUpNameTerm,
                        $"Skills/{CustomContentIds.BulkUpSkill}_Name"))
                {
                    _bulkUpSetTermTargets |= 1;
                }
                else if (Matches(primary,
                             BulkUpFeature.BulkUpDescriptionTerm,
                             $"Skills/{CustomContentIds.BulkUpSkill}_Desc"))
                {
                    _bulkUpSetTermTargets |= 2;
                }

                if (_bulkUpSetTermTargets == 3)
                {
                    _logger?.LogInfo(
                        "[ChainedBeasts.ModFoundation] Bulk Up Localize.SetTerm targets applied (name and description)");
                    _bulkUpSetTermTargets = 4;
                }
            }
        }
        catch (Exception exception)
        {
            _logger?.LogError(
                $"[ChainedBeasts.ModFoundation] custom Localize.SetTerm target override failed for '{primary}': " +
                $"{exception.GetType().Name}: {exception.Message}");
        }
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
                text = CustomTranslations.Get(CustomText.BulkUpName);
                return true;
            }

            if (Matches(term,
                    BulkUpFeature.BulkUpDescriptionTerm,
                    $"Skills/{CustomContentIds.BulkUpSkill}_Desc"))
            {
                text = CustomTranslations.Get(CustomText.BulkUpDescription);
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
                text = CustomTranslations.Get(CustomText.RunnersWrapsName);
                return true;
            }

            if (Matches(term,
                    $"Purchases/{CustomContentIds.RunnersWrapsPurchase}_Desc",
                    "Purchases/RunnersWraps_Desc",
                    Equipment.RunnersWrapsFeature.DescriptionTerm))
            {
                text = CustomTranslations.Get(CustomText.RunnersWrapsDescription);
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
                text = CustomTranslations.Get(CustomText.LeadfootName);
                return true;
            }

            if (Matches(term,
                    $"Injuries/{CustomContentIds.LeadfootInjury}_Threat",
                    "Injuries/Leadfoot_Threat",
                    "ModFoundation/Leadfoot/Threat"))
            {
                text = CustomTranslations.Get(CustomText.LeadfootThreat);
                return true;
            }

            if (TryGetLeadfootSeverity(term, out var severity))
            {
                text = CustomTranslations.Get(CustomText.LeadfootDescription, severity);
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
                text = CustomTranslations.Get(CustomText.AdrenalGraftName);
                return true;
            }

            if (Matches(term,
                    $"Surgeries/{CustomContentIds.AdrenalGraftSurgery}_Desc",
                    "Surgeries/AdrenalGraft_Desc",
                    Surgeries.AdrenalGraftFeature.DescriptionTerm))
            {
                text = CustomTranslations.Get(CustomText.AdrenalGraftDescription);
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
