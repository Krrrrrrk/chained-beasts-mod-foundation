using System;
using System.Collections.Generic;
using HarmonyLib;
using I2.Loc;
using TMPro;
using UnityEngine;

namespace ChainedBeasts.ModFoundation.Runtime;

/// <summary>Owned I2 terms for independent plugins. English is the explicit fallback.</summary>
public static class FoundationText
{
    private sealed class Entry
    {
        internal string ContentKey;
        internal Func<string, string> Translate;
    }
    private static readonly Dictionary<string, Entry> Terms = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, IntPtr> RegisteredSources = new(StringComparer.Ordinal);
    public static string Register(string contentKey, string suffix, string english,
        IReadOnlyDictionary<string, string> translations = null)
    {
        if (!FoundationServices.Content.TryGet(contentKey, out _) || string.IsNullOrWhiteSpace(suffix) || string.IsNullOrWhiteSpace(english))
            throw new ArgumentException("Register a content identity and nonempty term/text first.");
        var term = contentKey + "/" + suffix;
        if (!Terms.TryAdd(term, new Entry { ContentKey = contentKey, Translate = language =>
            translations is not null && language is not null && translations.TryGetValue(language, out var text) &&
                !string.IsNullOrWhiteSpace(text) ? text : english }))
            throw new InvalidOperationException("Localization key collision: " + term);
        return term;
    }
    public static bool TryGet(string term, out string text)
    {
        text = null;
        if (term is null || !Terms.TryGetValue(term, out var entry) ||
            !FoundationServices.Content.TryGet(entry.ContentKey, out var content) || !content.Enabled) return false;
        string language;
        try { language = LocalizationManager.CurrentLanguage; }
        catch { language = null; } // Early lookup still has an explicit English fallback.
        text = entry.Translate(language);
        return true;
    }
    internal static void Install(Harmony harmony)
    {
        void Patch(Type type, string name, string prefix = null, string postfix = null, params Type[] args) =>
            FeaturePatcher.Patch(harmony, typeof(FoundationText), FeaturePatcher.RequireMethod(type, name, args), prefix, postfix);
        Patch(typeof(StringManager), nameof(StringManager.GetLocalisedTerm), nameof(Lookup), args: new[] { typeof(string) });
        Patch(typeof(LocalizationManager), nameof(LocalizationManager.GetTranslation), nameof(Translation), args: new[]
            { typeof(string), typeof(bool), typeof(int), typeof(bool), typeof(bool), typeof(GameObject), typeof(string), typeof(bool) });
        Patch(typeof(LocalizationManager), nameof(LocalizationManager.TryGetTranslation), nameof(TryTranslation), args: new[]
            { typeof(string), typeof(string).MakeByRefType(), typeof(bool), typeof(int), typeof(bool), typeof(bool), typeof(GameObject), typeof(string), typeof(bool) });
        Patch(typeof(Localize), nameof(Localize.SetTerm), postfix: nameof(SetTerm), args: new[] { typeof(string) });
        Patch(typeof(GameLibrary), nameof(GameLibrary.Init), postfix: nameof(RegisterNativeTerms));
    }
    private static bool Lookup(string termKey, ref string __result)
    { if (!TryGet(termKey, out var text)) return true; __result = text; return false; }
    private static bool Translation(string Term, string overrideLanguage, ref string __result)
    {
        if (!TryGet(Term, out var text)) return true;
        __result = string.IsNullOrWhiteSpace(overrideLanguage) ? text : Terms[Term].Translate(overrideLanguage);
        return false;
    }
    private static bool TryTranslation(string Term, ref string Translation, string overrideLanguage, ref bool __result)
    {
        var text = string.Empty;
        if (FoundationText.Translation(Term, overrideLanguage, ref text)) return true;
        Translation = text; __result = true; return false;
    }
    private static void SetTerm(Localize __instance, string primary)
    {
        if (__instance is null || !TryGet(primary, out var text)) return;
        var target = __instance.GetComponent<TMP_Text>();
        if (target is not null) target.text = text;
    }
    private static void RegisterNativeTerms()
    {
        try { RegisterNativeTermsCore(); }
        catch (Exception exception)
        {
            FoundationServices.Logger?.LogError("[Foundation] live I2 registration failed; owned lookup fallbacks remain active: " + exception);
        }
    }
    private static void RegisterNativeTermsCore()
    {
        if (Terms.Count == 0) return;
        LocalizationManager.InitializeIfNeeded();
        LanguageSourceData source = null;
        for (var i = 0; i < (LocalizationManager.Sources?.Count ?? 0); i++)
        {
            var candidate = LocalizationManager.Sources[i];
            if ((candidate?.mLanguages?.Count ?? 0) > (source?.mLanguages?.Count ?? 0)) source = candidate;
        }
        if (source is null) { FoundationServices.Logger?.LogWarning("[Foundation] no live I2 source; owned lookup fallbacks remain active"); return; }
        foreach (var entry in Terms)
        {
            if (!FoundationServices.Content.TryGet(entry.Value.ContentKey, out var definition) || !definition.Enabled) continue;
            try
            {
                // A native/foreign source must not already own this namespaced term.
                var term = source.GetTermData(entry.Key);
                if (term is not null && (!RegisteredSources.TryGetValue(entry.Key, out var owner) || owner != source.Pointer))
                    throw new InvalidOperationException("I2 term already exists: " + entry.Key);
                term ??= source.AddTerm(entry.Key, eTermType.Text, false) ?? throw new InvalidOperationException("I2 term creation failed");
                for (var i = 0; i < source.mLanguages.Count; i++) term.SetTranslation(i, entry.Value.Translate(source.mLanguages[i]?.Name));
                RegisteredSources[entry.Key] = source.Pointer;
            }
            catch (Exception exception)
            {
                FoundationServices.Content.Disable(entry.Value.ContentKey);
                FoundationServices.Logger?.LogError("[Foundation] disabled content after localization collision/failure: " + exception);
            }
        }
        source.UpdateDictionary(true);
    }
}
