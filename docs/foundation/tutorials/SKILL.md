# End-to-end skill: Training Footwork

Use the complete [StandaloneContentExample project](../../../examples/StandaloneContentExample/StandaloneContentExample.csproj), following [separate-plugin setup](../SEPARATE_MOD.md). This optional example has one rank, skill ID 12000 and its own GUID/key; it isn't one of the pack's twelve four-rank skills. Change all identities before creating another copy. No LegacyMarker belongs on new content.

The full compiled plugin below claims the identity first, registers text and a default-neutral dodge-cost factor, then schedules a fresh SkillDef before native indexing. It reuses only ReducedDashStaminaCost's sprite. Release/normal-offer/category flags keep native trainer generation in charge; no acquisition is simulated. Post-init verifies SkillDict, and targeted StringManager prefixes handle direct names versus term consumers. Unknown IDs/views continue to the original methods.

<!-- source: examples/StandaloneContentExample/Plugin.cs -->
```csharp
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
[BepInDependency(ChainedBeasts.ModFoundation.Plugin.PluginGuid, ">=0.8.0 <0.9.0")]
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
```
<!-- /source -->

## Acquisition, removal and saves

Foundation's normal skill enumeration exposes the enabled registered ID. Native selection/training cost and `PlayerData.AddSkill` retain their bookkeeping. The getter reads current ownership and contributes 0.95 while owned in real Solo; it never changes a baseline or heals on a read. Normal `RemoveSkill` immediately removes the factor, including signed canonical paths. Shared schema-1 state preserves its independently keyed rank/order and associates it with the exact native checkpoint. The sample needs no additional timer state. Restoring ownership is assignment, not another AddSkill or cost/effect grant.

For a four-rank skill, use maxLevel/descriptor.MaximumLevel 4, actual owned rank clamped 0–4, rank-zero neutral contribution and a pure formula. Do not change vanilla maximums. The pack's complete implementation is `src/TermsAndContusions/Skills/SkillsFeature.cs`: `UILobbyTrainerUpgradePage.Refresh(PlayerData,int,bool,bool)` and `SetSkill(SkillType,bool)` route short next-rank offers using the actual viewed player, while character `UIGameLevelLobbyCharacterSkillInfo.Initialise(SkillType,int)` and `UIPlayerInfoTooltip.Initialise(SkillType,bool)` handle their own views. `SkillMath.Description` supplies owned-rank text and `OfferDescription` supplies the rank being purchased; a global GetSkillDescTerm with no player argument cannot invent the viewed player's rank.

Bulk Up's historical forced-offer banner problem was a mismatch between the displayed row and the backing training row used by acquisition. Its pack implementation updates that backing row; changing arbitrary full-screen text or scanning native pointers is not an appropriate general fix. Verify the selected ID reaches the banner, not only the visible card.

## Manual verification

After a closed-game backup/install, acquire normally, inspect trainer/banner/detail/tooltip, compare normal dodge stamina cost x0.95, remove/reacquire normally, save/exit/restore and verify no acquisition replay. Acquire it with Bulk Up and an independently renamed copy; both ranks/order must restore independently. Test Foundation alone and alongside the pack, a missing dependency and a genuine ID collision. The example is compiled/metadata checked; all live behavior is pending. See [release notes](https://github.com/Krrrrrrk/chained-beasts-mod-foundation/releases/tag/v0.8.3).
