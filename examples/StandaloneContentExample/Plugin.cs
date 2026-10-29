using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using ChainedBeasts.ModFoundation;
using ChainedBeasts.ModFoundation.Content;
using ChainedBeasts.ModFoundation.Runtime;
using ChainedBeasts.ModFoundation.Stats;
using HarmonyLib;
using UnityEngine;

namespace StandaloneContentExample;

// Optional teaching plugin, outside the 46-entry pack. Build scripts never deploy it.
[BepInPlugin(Guid, "Standalone Content Example", "0.8.0")]
[BepInDependency(ChainedBeasts.ModFoundation.Plugin.PluginGuid, ">=1.0.0 <2.0.0")]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "example.chainedbeasts.standalonecontent";
    public const string Key = "example.standalone:skill/training-footwork";
    public const int NumericId = 12000; // Verify against the installed enum and other cooperating plugins.
    private static string _name, _description;
    private static bool _added;

    public override void Load()
    {
        if (!FoundationServices.IsReady) throw new InvalidOperationException("Foundation runtime unavailable");
        FoundationServices.Content.Register(new ContentDescriptor(Key, Guid, ContentCategory.Skill, NumericId, 1));
        var harmony = new Harmony(Guid);
        try
        {
            _name = FoundationText.Register(Key, "Name", "Training Footwork");
            _description = FoundationText.Register(Key, "Description", "Dodges use 5% less stamina.");
            FoundationServices.RegisterLibraryCallbacks(Key, AddDefinition, VerifyDefinition);
            FoundationServices.RegisterModifier(Key, StatChannel.DodgeStaminaCost,
                context => SessionAccess.CanUseDeveloperTools && context.Data?.HasSkill((SkillType)NumericId) > 0 ? 0.95d : 1d);
            void Patch(string method, string prefix) => FeaturePatcher.Patch(harmony, typeof(Plugin),
                FeaturePatcher.RequireMethod(typeof(StringManager), method, typeof(SkillType)), prefix: prefix);
            Patch(nameof(StringManager.GetSkillName), nameof(Name));
            Patch(nameof(StringManager.GetSkillNameTerm), nameof(NameTerm));
            Patch(nameof(StringManager.GetSkillDescTerm), nameof(DescriptionTerm));
            Log.LogInfo("Standalone skill registered; Foundation stores its rank/order independently without a new native marker slot.");
        }
        catch
        {
            FoundationServices.Content.Disable(Key);
            harmony.UnpatchSelf();
            throw;
        }
    }

    private static void AddDefinition(GameLibrary library)
    {
        Sprite icon = null;
        for (var i = 0; i < library.allSkills.Count; i++)
        {
            var skill = library.allSkills[i];
            if (skill is null) continue;
            if ((int)skill.skillType == NumericId)
            {
                if (_added && skill.name == Key) return;
                throw new InvalidOperationException("Foreign/native SkillDef collision");
            }
            if (skill.skillType == SkillType.ReducedDashStaminaCost) icon = skill.sprite;
        }
        if (icon is null) throw new InvalidOperationException("Native dodge skill sprite missing");
        library.allSkills.Add(new SkillDef
        {
            name = Key, skillType = (SkillType)NumericId, releaseStatus = ReleaseStatus.InFullGame,
            offerModifiers = (OfferModifiers)0, offerType = OfferType.Normal, maxLevel = 1,
            requiresCategory = (SkillCategory)0, unlocksCategory = (SkillCategory)0,
            tooltipCategories = StatusTooltipType.None, sprite = icon
        });
        _added = true;
    }
    private static void VerifyDefinition(GameLibrary library)
    {
        if (library.SkillDict is null || !library.SkillDict.ContainsKey((SkillType)NumericId))
            throw new InvalidOperationException("Skill definition not indexed");
    }
    private static bool IsEnabled(SkillType id) => (int)id == NumericId &&
        FoundationServices.Content.TryGet(Key, out var content) && content.Enabled;
    private static bool Name(SkillType skillType, ref string __result)
    {
        if (!IsEnabled(skillType)) return true;
        __result = FoundationText.TryGet(_name, out var text) ? text : "Training Footwork";
        return false;
    }
    private static bool NameTerm(SkillType skillType, ref string __result)
    { if (!IsEnabled(skillType)) return true; __result = _name; return false; }
    private static bool DescriptionTerm(SkillType skillType, ref string __result)
    { if (!IsEnabled(skillType)) return true; __result = _description; return false; }
}
