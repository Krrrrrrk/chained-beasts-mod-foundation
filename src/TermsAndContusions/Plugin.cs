using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using ChainedBeasts.ModFoundation.Configuration;
using ChainedBeasts.ModFoundation.DevTools;
using ChainedBeasts.ModFoundation.Content;
using ChainedBeasts.ModFoundation.Runtime;
using ChainedBeasts.ModFoundation.Networking;
using HarmonyLib;
using TermsAndContusions.Afflictions;
using TermsAndContusions.Equipment;
using TermsAndContusions.Surgeries;
using TermsAndContusions.Skills;
using System.Collections.Generic;
using System.Linq;
using FoundationPlugin = ChainedBeasts.ModFoundation.Plugin;
using TermsAndContusions.ContentMenu;

namespace TermsAndContusions;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(FoundationPlugin.PluginGuid, FoundationVersionRange)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "local.chainedbeasts.termsandcontusions";
    public const string PluginName = "Terms & Contusions";
    public const string PluginVersion = "1.0.0";
    public const string FoundationVersionRange = ">=1.0.0 <2.0.0";
    private readonly Dictionary<string, ConfigEntry<bool>> _offerSettings = new(StringComparer.Ordinal);
    private void Track(string key, ConfigEntry<bool> setting) => _offerSettings.Add(key, setting);

    public override void Load()
    {
        if (!FoundationServices.IsReady)
            throw new InvalidOperationException("Foundation shared infrastructure did not initialize; content pack refused to load.");
        Log.LogInfo("[Terms & Contusions] content pack loaded; shared identities and checkpoint persistence enabled");
        var previousAutoSave = Config.SaveOnConfigSet;
        Config.SaveOnConfigSet = false;
        ConfigEntry<bool> enableBulkUp;
        ConfigEntry<bool> forceBulkUp;
        ConfigEntry<bool> enableRunnersWraps;
        ConfigEntry<bool> forceRunnersWraps;
        ConfigEntry<bool> enableLeadfoot;
        ConfigEntry<bool> forceLeadfoot;
        ConfigEntry<bool> enableAdrenalGraft;
        ConfigEntry<bool> forceAdrenalGraft;
        try
        {
            var migration = Config.Bind("Migration", "LegacyConfigurationImported", false,
                "Set after the one-time import of content settings from the old Foundation configuration. " +
                "Existing values in this pack's configuration always take precedence.");
            var legacyPath = Path.Combine(Paths.ConfigPath, FoundationPlugin.PluginGuid + ".cfg");
            var legacy = LegacyConfigurationMigration.Read(legacyPath, migration.Value);
            enableBulkUp = BindToggle(legacy, migration.Value, "BulkUp", "EnableBulkUp", true,
                "Allow Bulk Up in new training offers (four ranks). Also editable in the main-menu TC Content panel.");
            forceBulkUp = BindToggle(legacy, migration.Value, "BulkUp", "ForceBulkUpFirstOffer", false,
                "Developer override: keep Bulk Up in an eligible training offer until acquired.");
            enableRunnersWraps = BindToggle(legacy, migration.Value, "RunnersWraps", "EnableRunnersWraps", true,
                "Allow Runner's Wraps in new equipment offers. Also editable in TC Content.");
            forceRunnersWraps = BindToggle(legacy, migration.Value, "RunnersWraps", "ForceRunnersWrapsNextOffer", false,
                "Developer override: replace an eligible equipment-shop slot with Runner's Wraps.");
            enableLeadfoot = BindToggle(legacy, migration.Value, "Leadfoot", "EnableLeadfoot", true,
                "Allow Leadfoot in new potion offers. Also editable in TC Content.");
            forceLeadfoot = BindToggle(legacy, migration.Value, "Leadfoot", "ForceLeadfootNextOffer", false,
                "Developer override: replace an eligible potion slot with Leadfoot.");
            enableAdrenalGraft = BindToggle(legacy, migration.Value, "AdrenalGraft", "EnableAdrenalGraft", true,
                "Allow Adrenal Graft in new surgery offers. Also editable in TC Content.");
            forceAdrenalGraft = BindToggle(legacy, migration.Value, "AdrenalGraft", "ForceAdrenalGraftNextOffer", false,
                "Developer override: show free Adrenal Graft at the next surgeon.");
            if (!migration.Value)
            {
                migration.Value = true;
                Log.LogInfo("[Terms & Contusions] legacy content configuration import completed; destination values preserved");
            }

            Config.Save();
        }
        finally
        {
            Config.SaveOnConfigSet = previousAutoSave;
        }

        // Old testing flags may have been imported from the monolithic config.
        // They must not silently prioritize the original examples in normal play.
        var allowForcedOffers = Config.Bind("DeveloperOffers", "EnableForcedOffers", false,
            "Testing only: allow the four Force* offer overrides. False keeps all content in normal pools, even if old Force* settings are true. Restart to apply.").Value;
        Log.LogInfo($"[Terms & Contusions] EnableForcedOffers={allowForcedOffers}");
        Track(CustomContentIds.BulkUpKey, enableBulkUp);
        Track(CustomContentIds.RunnersWrapsKey, enableRunnersWraps);
        Track(CustomContentIds.LeadfootKey, enableLeadfoot);
        Track(CustomContentIds.AdrenalGraftKey, enableAdrenalGraft);
        foreach (var skill in SkillDefinitions.All.Where(value => value.Kind != CustomSkill.BulkUp))
            Track(skill.Key, BindToggle(LegacyConfigurationSnapshot.Parse(null), true, "Skills", "Enable" + skill.Kind, true,
                "Allow " + skill.Name + " in newly generated offers. Also editable from the main-menu TC Content panel."));
        Track(CounterweightSlingFeature.Key, Config.Bind("CounterweightSling", "EnableCounterweightSling", true,
            "Allow Counterweight Sling in newly generated offers. Also editable in TC Content."));
        foreach (var entry in EquipmentDefinitions.All)
            Track(entry.Key, Config.Bind("Equipment", "Enable" + entry.Kind, true, "Allow " + entry.Name + " in newly generated offers. Also editable in TC Content."));
        foreach (var entry in AfflictionDefinitions.All)
            Track(entry.Key, Config.Bind("Afflictions", "Enable" + entry.Kind, true, "Allow " + entry.Name + " potion offers. Also editable in TC Content."));
        foreach (var entry in SurgeryDefinitions.All.Where(e => e.Kind != CustomSurgery.AdrenalGraft))
            Track(entry.Key, Config.Bind("Surgeries", "Enable" + entry.Kind, true, "Allow " + entry.Name + " surgery offers. Also editable in TC Content."));
        var surgeryPrices = SurgeryDefinitions.All.ToDictionary(entry => entry.Kind, entry => entry.Kind == CustomSurgery.AdrenalGraft ? 0 :
            Config.Bind("SurgeryPrices", entry.Kind.ToString(), entry.DefaultPrice,
                new ConfigDescription("Gold cost. Technical examples, not balanced. Restart to apply.", new AcceptableValueRange<int>(1, 10000))).Value);
        var claims = new List<ContentDescriptor>
        {
        new ContentDescriptor(CustomContentIds.BulkUpKey, PluginGuid,
            ContentCategory.Skill, CustomContentIds.BulkUpSkill, 4, true,
            new LegacyMarker(ContentCategory.Skill, (int)SkillType.None)),
        new ContentDescriptor(CustomContentIds.RunnersWrapsKey, PluginGuid,
            ContentCategory.Equipment, CustomContentIds.RunnersWrapsPurchase, 1, true),
        new ContentDescriptor(CustomContentIds.LeadfootKey, PluginGuid,
            ContentCategory.Affliction, CustomContentIds.LeadfootInjury, 3, true,
            new LegacyMarker(ContentCategory.Affliction, (int)InjuryType.EmptySlot1)),
        new ContentDescriptor(CustomContentIds.AdrenalGraftKey, PluginGuid,
            ContentCategory.Surgery, CustomContentIds.AdrenalGraftSurgery, 1, true)
        };
        claims.AddRange(SkillDefinitions.All.Where(value => value.Kind != CustomSkill.BulkUp).Select(skill =>
            new ContentDescriptor(skill.Key, PluginGuid, ContentCategory.Skill, skill.Id, skill.MaxRank)));
        claims.Add(new ContentDescriptor(CounterweightSlingFeature.Key, PluginGuid, ContentCategory.Equipment,
            CounterweightSlingFeature.Id, 1));
        claims.AddRange(EquipmentDefinitions.All.Select(entry => new ContentDescriptor(entry.Key, PluginGuid,
            ContentCategory.Equipment, entry.Id, 1)));
        claims.AddRange(AfflictionDefinitions.All.Select(entry => new ContentDescriptor(entry.Key, PluginGuid,
            ContentCategory.Affliction, entry.Id, 3)));
        claims.AddRange(SurgeryDefinitions.All.Where(entry => entry.Kind != CustomSurgery.AdrenalGraft).Select(entry =>
            new ContentDescriptor(entry.Key, PluginGuid, ContentCategory.Surgery, entry.Id, 1)));
        FoundationServices.Content.RegisterMany(claims);
        foreach (var setting in _offerSettings) ContentOfferServices.Register(setting.Key, setting.Value.Value);
        MultiplayerServices.RegisterGoldRule(EquipmentDefinitions.Get(CustomEquipment.PickpocketsMitts).Key, true, 1);
        MultiplayerServices.RegisterGoldRule(AfflictionDefinitions.Get(CustomAffliction.LooseChange).Key, false, 3);
        MultiplayerServices.RegisterCompatibility(PluginGuid, PluginVersion, string.Join(";", surgeryPrices.OrderBy(x => x.Key).Select(x => x.Key + "=" + x.Value)));
        MultiplayerServices.RegisterBinary(typeof(Plugin).Assembly);
        MultiplayerServices.RegisterHealthGrantRule(EquipmentDefinitions.Get(CustomEquipment.ButchersApron).Key, .03, true);
        MultiplayerServices.RegisterHealthGrantRule(SurgeryDefinitions.Get(CustomSurgery.EmergencyClotting).Key, .25, false);
        ActivationFeedback.Install(Config, Log);

        InstallFeature("custom localization", "localization",
            true, harmony => CustomLocalization.Install(harmony, Log, true, true, true, true));
        InstallFeature("Bulk Up", "bulkup", true, harmony =>
        {
            BulkUpFeature.Install(harmony, Log, allowForcedOffers && forceBulkUp.Value);
        });
        InstallFeature("skill catalog", "skills", true, harmony => SkillsFeature.Install(harmony, Log));
        InstallFeature("Iron Grip", "irongrip", true, harmony => IronGripFeature.Install(harmony, Log));
        InstallFeature("Counterweight Sling", "sling", true, CounterweightSlingFeature.Install);
        InstallFeature("equipment catalog", "equipment", true, harmony => EquipmentFeature.Install(harmony, Log));
        InstallFeature("Runner's Wraps", "runnerswraps", true,
            harmony => RunnersWrapsFeature.Install(harmony, Log, allowForcedOffers && forceRunnersWraps.Value));
        InstallFeature("Leadfoot", "leadfoot", true,
            harmony => LeadfootFeature.Install(harmony, Log, allowForcedOffers && forceLeadfoot.Value));
        InstallFeature("potion affliction catalog", "afflictions", true, harmony => AfflictionsFeature.Install(harmony, Log));
        InstallFeature("surgery catalog", "surgeries", true,
            harmony => SurgeriesFeature.Install(harmony, Log, surgeryPrices, allowForcedOffers && forceAdrenalGraft.Value));
        ContentMenuSettings.Initialize(Config, _offerSettings, Log);
        InstallFeature("TC content menu", "contentmenu", true, harmony =>
        {
            ContentMenuFeature.Install(harmony);
            AddComponent<TcContentMenuController>();
        });
    }

    private ConfigEntry<bool> BindToggle(LegacyConfigurationSnapshot legacy, bool migrationCompleted,
        string section, string key, bool defaultValue, string description)
    {
        var setting = LegacyConfigurationMigration.BindBoolean(
            Config, legacy, migrationCompleted, section, key, defaultValue, description);
        Log.LogInfo($"[Terms & Contusions] {key}={setting.Value}");
        return setting;
    }

    private void InstallFeature(string name, string ownerSuffix, bool enabled, Action<Harmony> install)
    {
        if (!enabled)
        {
            Log.LogInfo($"[Terms & Contusions] {name} registration and hooks skipped");
            return;
        }

        var harmony = new Harmony($"{PluginGuid}.{ownerSuffix}");
        try
        {
            install(harmony);
        }
        catch (Exception exception)
        {
            harmony.UnpatchSelf();
            var key = ownerSuffix switch
            {
                "bulkup" => CustomContentIds.BulkUpKey,
                "runnerswraps" => CustomContentIds.RunnersWrapsKey,
                "leadfoot" => CustomContentIds.LeadfootKey,
                "adrenalgraft" => CustomContentIds.AdrenalGraftKey,
                "sling" => CounterweightSlingFeature.Key,
                "irongrip" => SkillDefinitions.Get(CustomSkill.IronGrip).Key,
                _ => null
            };
            if (key is not null) FoundationServices.Content.Disable(key);
            else if (ownerSuffix == "skills")
                foreach (var entry in FoundationServices.Content.Entries.Where(entry => entry.OwnerGuid == PluginGuid && entry.Category == ContentCategory.Skill))
                    FoundationServices.Content.Disable(entry.Key);
            else if (ownerSuffix == "equipment")
                foreach (var entry in EquipmentDefinitions.All) FoundationServices.Content.Disable(entry.Key);
            else if (ownerSuffix == "afflictions")
                foreach (var entry in AfflictionDefinitions.All) FoundationServices.Content.Disable(entry.Key);
            else if (ownerSuffix == "surgeries")
                foreach (var entry in SurgeryDefinitions.All) FoundationServices.Content.Disable(entry.Key);
            else if (ownerSuffix == "localization")
                foreach (var entry in System.Linq.Enumerable.ToArray(FoundationServices.Content.Entries))
                    if (entry.OwnerGuid == PluginGuid) FoundationServices.Content.Disable(entry.Key);
            Log.LogError($"[Terms & Contusions] {name} installation failed: {exception}. Partial hooks removed.");
        }
    }

}
