using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using ChainedBeasts.ModFoundation.Content;
using ChainedBeasts.ModFoundation.Runtime;
using TermsAndContusions.Skills;
using TermsAndContusions.Equipment;
using TermsAndContusions.Afflictions;
using TermsAndContusions.Surgeries;

namespace TermsAndContusions.ContentMenu;

internal static class ContentMenuSettings
{
    internal static MenuEntry[] Entries;
    internal static ManualLogSource Logger;
    private static ConfigFile _config;
    private static Dictionary<string, ConfigEntry<bool>> _settings;
    internal static void Initialize(ConfigFile config, Dictionary<string, ConfigEntry<bool>> settings, ManualLogSource logger)
    {
        _config = config; _settings = settings; Logger = logger;
        var entries = new List<MenuEntry>();
        entries.AddRange(SkillDefinitions.All.Select(e => new MenuEntry(e.Key, ContentCategory.Skill, e.Name, SkillMath.Effect(e.Kind, 1))));
        entries.Add(new MenuEntry(CustomContentIds.RunnersWrapsKey, ContentCategory.Equipment, "Runner's Wraps", RunnersWrapsFeature.Description));
        entries.Add(new MenuEntry(CounterweightSlingFeature.Key, ContentCategory.Equipment, "Counterweight Sling", "TC: Doubles throw range and charge speed."));
        entries.AddRange(EquipmentDefinitions.All.Select(e => new MenuEntry(e.Key, ContentCategory.Equipment, e.Name, e.Description)));
        entries.Add(new MenuEntry(CustomContentIds.LeadfootKey, ContentCategory.Affliction, "Leadfoot", "TC: Movement speed reduced by 10% per severity."));
        entries.AddRange(AfflictionDefinitions.All.Select(e => new MenuEntry(e.Key, ContentCategory.Affliction, e.Name, AfflictionMath.Description(e.Kind, 1))));
        entries.AddRange(SurgeryDefinitions.All.Select(e => new MenuEntry(e.Key, ContentCategory.Surgery, e.Name, e.Description)));
        Entries = entries.OrderBy(e => e.Category).ThenBy(e => e.Name, StringComparer.Ordinal).ToArray();
        if (Entries.Length != settings.Count || Entries.Any(e => !settings.ContainsKey(e.Key)))
            throw new InvalidOperationException("TC content-menu configuration map is incomplete.");
    }
    internal static void Save(Dictionary<string, bool> draft)
    {
        if (!ContentMenuFeature.CanEdit) throw new InvalidOperationException("Return to the main menu and leave the party/run first.");
        var all = ContentOfferServices.LocalPreferences();
        if (draft.Count != _settings.Count || _settings.Keys.Any(k => !draft.ContainsKey(k))) throw new InvalidOperationException("Incomplete TC content selection.");
        foreach (var item in draft) all[item.Key] = item.Value;
        ContentOfferServices.SaveLocal(all, () =>
        {
            var before = _settings.ToDictionary(e => e.Key, e => e.Value.Value);
            var automatic = _config.SaveOnConfigSet;
            _config.SaveOnConfigSet = false;
            try
            {
                foreach (var item in draft) _settings[item.Key].Value = item.Value;
                _config.Save();
            }
            catch
            {
                foreach (var item in before) _settings[item.Key].Value = item.Value;
                throw;
            }
            finally { _config.SaveOnConfigSet = automatic; }
        });
        Logger.LogInfo("[Terms & Contusions] saved TC offer selection: " + draft.Count(e => e.Value) + "/" + draft.Count + " enabled; host selection overrides clients for each session");
    }
}
