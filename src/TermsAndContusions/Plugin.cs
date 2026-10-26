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

namespace TermsAndContusions;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(FoundationPlugin.PluginGuid, FoundationVersionRange)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "local.chainedbeasts.termsandcontusions";
    public const string PluginName = "Terms & Contusions";
    public const string PluginVersion = "0.9.1";
    public const string FoundationVersionRange = ">=0.9.1 <0.10.0";

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
                "Register Bulk Up with four ranks on the next launch (2x/3x/4x/5x maximum HP).");
            forceBulkUp = BindToggle(legacy, migration.Value, "BulkUp", "ForceBulkUpFirstOffer", false,
                "Developer override: keep Bulk Up in an eligible training offer until acquired.");
            enableRunnersWraps = BindToggle(legacy, migration.Value, "RunnersWraps", "EnableRunnersWraps", true,
                "Register Runner's Wraps on the next launch.");
            forceRunnersWraps = BindToggle(legacy, migration.Value, "RunnersWraps", "ForceRunnersWrapsNextOffer", false,
                "Developer override: replace an eligible equipment-shop slot with Runner's Wraps.");
            enableLeadfoot = BindToggle(legacy, migration.Value, "Leadfoot", "EnableLeadfoot", true,
                "Register Leadfoot on the next launch.");
            forceLeadfoot = BindToggle(legacy, migration.Value, "Leadfoot", "ForceLeadfootNextOffer", false,
                "Developer override: replace an eligible potion slot with Leadfoot.");
            enableAdrenalGraft = BindToggle(legacy, migration.Value, "AdrenalGraft", "EnableAdrenalGraft", true,
                "Register Adrenal Graft on the next launch.");
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
        var skillEnabled = new Dictionary<CustomSkill, bool> { [CustomSkill.BulkUp] = enableBulkUp.Value };
        foreach (var skill in SkillDefinitions.All.Where(value => value.Kind != CustomSkill.BulkUp))
            skillEnabled[skill.Kind] = BindToggle(LegacyConfigurationSnapshot.Parse(null), true, "Skills", "Enable" + skill.Kind, true,
                "Register the four-rank " + skill.Name + " skill on the next launch.").Value;
        var slingEnabled = Config.Bind("CounterweightSling", "EnableCounterweightSling", true,
            "Register Counterweight Sling equipment on the next launch.").Value;
        var equipmentEnabled = EquipmentDefinitions.All.ToDictionary(entry => entry.Kind, entry =>
            Config.Bind("Equipment", "Enable" + entry.Kind, true, "Register " + entry.Name + " on the next launch.").Value);
        var afflictionEnabled = AfflictionDefinitions.All.ToDictionary(entry => entry.Kind, entry =>
            Config.Bind("Afflictions", "Enable" + entry.Kind, true, "Register the three-severity " + entry.Name + " potion affliction on the next launch.").Value);
        var surgeryEnabled = SurgeryDefinitions.All.ToDictionary(entry => entry.Kind, entry => entry.Kind == CustomSurgery.AdrenalGraft ? enableAdrenalGraft.Value :
            Config.Bind("Surgeries", "Enable" + entry.Kind, true, "Offer " + entry.Name + " once per player/run on the next launch.").Value);
        var surgeryPrices = SurgeryDefinitions.All.ToDictionary(entry => entry.Kind, entry => entry.Kind == CustomSurgery.AdrenalGraft ? 0 :
            Config.Bind("SurgeryPrices", entry.Kind.ToString(), entry.DefaultPrice,
                new ConfigDescription("Gold cost. Technical examples, not balanced. Restart to apply.", new AcceptableValueRange<int>(1, 10000))).Value);
        var claims = new List<ContentDescriptor>
        {
        new ContentDescriptor(CustomContentIds.BulkUpKey, PluginGuid,
            ContentCategory.Skill, CustomContentIds.BulkUpSkill, 4, enableBulkUp.Value,
            new LegacyMarker(ContentCategory.Skill, (int)SkillType.None)),
        new ContentDescriptor(CustomContentIds.RunnersWrapsKey, PluginGuid,
            ContentCategory.Equipment, CustomContentIds.RunnersWrapsPurchase, 1, enableRunnersWraps.Value),
        new ContentDescriptor(CustomContentIds.LeadfootKey, PluginGuid,
            ContentCategory.Affliction, CustomContentIds.LeadfootInjury, 3, enableLeadfoot.Value,
            new LegacyMarker(ContentCategory.Affliction, (int)InjuryType.EmptySlot1)),
        new ContentDescriptor(CustomContentIds.AdrenalGraftKey, PluginGuid,
            ContentCategory.Surgery, CustomContentIds.AdrenalGraftSurgery, 1, enableAdrenalGraft.Value)
        };
        claims.AddRange(SkillDefinitions.All.Where(value => value.Kind != CustomSkill.BulkUp).Select(skill =>
            new ContentDescriptor(skill.Key, PluginGuid, ContentCategory.Skill, skill.Id, 4, skillEnabled[skill.Kind])));
        claims.Add(new ContentDescriptor(CounterweightSlingFeature.Key, PluginGuid, ContentCategory.Equipment,
            CounterweightSlingFeature.Id, 1, slingEnabled));
        claims.AddRange(EquipmentDefinitions.All.Select(entry => new ContentDescriptor(entry.Key, PluginGuid,
            ContentCategory.Equipment, entry.Id, 1, equipmentEnabled[entry.Kind])));
        claims.AddRange(AfflictionDefinitions.All.Select(entry => new ContentDescriptor(entry.Key, PluginGuid,
            ContentCategory.Affliction, entry.Id, 3, afflictionEnabled[entry.Kind])));
        claims.AddRange(SurgeryDefinitions.All.Where(entry => entry.Kind != CustomSurgery.AdrenalGraft).Select(entry =>
            new ContentDescriptor(entry.Key, PluginGuid, ContentCategory.Surgery, entry.Id, 1, surgeryEnabled[entry.Kind])));
        FoundationServices.Content.RegisterMany(claims);
        MultiplayerServices.RegisterGoldRule(EquipmentDefinitions.Get(CustomEquipment.PickpocketsMitts).Key, true, 1);
        MultiplayerServices.RegisterGoldRule(AfflictionDefinitions.Get(CustomAffliction.LooseChange).Key, false, 3);
        MultiplayerServices.RegisterCompatibility(PluginGuid, PluginVersion, string.Join(";", surgeryPrices.OrderBy(x => x.Key).Select(x => x.Key + "=" + x.Value)));
        MultiplayerServices.RegisterBinary(typeof(Plugin).Assembly);
        MultiplayerServices.RegisterHealthGrantRule(EquipmentDefinitions.Get(CustomEquipment.ButchersApron).Key, .03, true);
        MultiplayerServices.RegisterHealthGrantRule(SurgeryDefinitions.Get(CustomSurgery.EmergencyClotting).Key, .25, false);
        ActivationFeedback.Install(Config, Log);

        InstallFeature("custom localization", "localization",
            enableBulkUp.Value || enableRunnersWraps.Value || enableLeadfoot.Value || enableAdrenalGraft.Value,
            harmony => CustomLocalization.Install(harmony, Log, enableBulkUp.Value, enableRunnersWraps.Value,
                enableLeadfoot.Value, enableAdrenalGraft.Value));
        InstallFeature("Bulk Up", "bulkup", enableBulkUp.Value, harmony =>
        {
            BulkUpFeature.Install(harmony, Log, allowForcedOffers && forceBulkUp.Value);
        });
        InstallFeature("four-rank skills", "skills", skillEnabled.Values.Any(value => value), harmony => SkillsFeature.Install(harmony, Log));
        InstallFeature("Counterweight Sling", "sling", slingEnabled, CounterweightSlingFeature.Install);
        InstallFeature("equipment catalog", "equipment", equipmentEnabled.Values.Any(value => value), EquipmentFeature.Install);
        InstallFeature("Runner's Wraps", "runnerswraps", enableRunnersWraps.Value,
            harmony => RunnersWrapsFeature.Install(harmony, Log, allowForcedOffers && forceRunnersWraps.Value));
        InstallFeature("Leadfoot", "leadfoot", enableLeadfoot.Value,
            harmony => LeadfootFeature.Install(harmony, Log, allowForcedOffers && forceLeadfoot.Value));
        InstallFeature("potion affliction catalog", "afflictions", afflictionEnabled.Values.Any(value => value), harmony => AfflictionsFeature.Install(harmony, Log));
        InstallFeature("surgery catalog", "surgeries", surgeryEnabled.Values.Any(value => value),
            harmony => SurgeriesFeature.Install(harmony, Log, surgeryPrices, allowForcedOffers && forceAdrenalGraft.Value));
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
