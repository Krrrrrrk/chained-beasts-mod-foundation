# End-to-end potion affliction: Practice Limp

Use the complete [CategoryExamples loader/project](../../../examples/CategoryExamples/Plugin.cs) and [identity/setup guide](../SEPARATE_MOD.md). This example uses InjuryType 12010, three levels and a separate key. It reuses only a native limp sprite, not its injury identity or implementation.

<!-- source: examples/CategoryExamples/AfflictionExample.cs -->
```csharp
using System;
using ChainedBeasts.ModFoundation;
using ChainedBeasts.ModFoundation.Runtime;
using ChainedBeasts.ModFoundation.Stats;
using HarmonyLib;
using UnityEngine;

namespace CategoryExamples;

internal static class AfflictionExample
{
    public const string Key = "example.categories:affliction/practice-limp";
    public const int Id = 12010;
    private static string _name;
    private static readonly string[] Descriptions = new string[4];
    private static bool _added;
    private static bool Enabled => FoundationServices.Content.TryGet(Key, out var content) && content.Enabled;
    public static void Install(Harmony harmony)
    {
        _name=FoundationText.Register(Key,"Name","Practice Limp");
        for(var severity=0;severity<=3;severity++) Descriptions[severity]=FoundationText.Register(Key,"Description/"+severity,
            severity==0 ? "No active effect (cured or suppressed)." : "Movement is reduced by "+(2*severity)+"%.");
        FoundationServices.RegisterLibraryCallbacks(Key,Add,library=> {
            if(library.InjuryDict?.ContainsKey((InjuryType)Id)!=true) throw new InvalidOperationException("Practice Limp not indexed"); });
        FoundationServices.RegisterModifier(Key,StatChannel.MovementSpeed,context=>
            SessionAccess.CanUseDeveloperTools && context.Player is not null ? 1-.02*Math.Clamp(context.Player.HasInjury((InjuryType)Id),0,3) : 1);
        FeaturePatcher.Patch(harmony,typeof(AfflictionExample),FeaturePatcher.RequireMethod(typeof(StringManager),nameof(StringManager.GetInjuryNameTerm),typeof(InjuryType)),prefix:nameof(NameTerm));
        FeaturePatcher.Patch(harmony,typeof(AfflictionExample),FeaturePatcher.RequireMethod(typeof(StringManager),nameof(StringManager.GetInjuryName),typeof(InjuryType),typeof(int)),prefix:nameof(Name));
        FeaturePatcher.Patch(harmony,typeof(AfflictionExample),FeaturePatcher.RequireMethod(typeof(StringManager),nameof(StringManager.GetInjuryDescTerm),typeof(InjuryType),typeof(int)),prefix:nameof(Description));
    }
    private static void Add(GameLibrary library)
    {
        Sprite icon=null;
        for(var i=0;i<library.allInjuries.Count;i++) {
            var item=library.allInjuries[i];if(item is null)continue;
            if((int)item.injuryType==Id){if(_added&&item.name==Key)return;throw new InvalidOperationException("Foreign affliction ID collision");}
            if(item.injuryType==InjuryType.WalkWithALimp)icon=item.sprite;
        }
        if(icon is null)throw new InvalidOperationException("Native injury visual missing");
        library.allInjuries.Add(new InjuryDef {name=Key,injuryType=(InjuryType)Id,releaseStatus=ReleaseStatus.InFullGame,
            offerModifiers=(OfferModifiers)0,offerType=OfferType.Normal,maxLevel=3,canBeDisabled=true,
            requiresCategory=(SkillCategory)0,unlocksCategory=(SkillCategory)0,tooltipCategories=StatusTooltipType.None,
            bodyPart=BodyPart.Legs,sprite=icon,test=false});
        _added=true;
    }
    private static bool Match(InjuryType value)=>Enabled&&(int)value==Id;
    private static bool NameTerm(InjuryType injuryType,ref string __result){if(!Match(injuryType))return true;__result=_name;return false;}
    private static bool Name(InjuryType injuryType,ref string __result){if(!Match(injuryType))return true;__result=FoundationText.TryGet(_name,out var text)?text:"Practice Limp";return false;}
    private static bool Description(InjuryType injuryType,int level,ref string __result){if(!Match(injuryType))return true;__result=Descriptions[Math.Clamp(level,0,3)];return false;}
}
```
<!-- /source -->

The factory runs before native InjuryDict indexing and provides normal release/offer flags, maxLevel 3, leg body-part eligibility and `canBeDisabled`. Shared enumeration lets normal injury/potion generation consider it. The plugin does **not** replace a potion, recalculate healing, or invoke AddInjury to manufacture acquisition. Native potion price/healing/selection and injury bookkeeping remain intact. The pack's original optional Leadfoot forced replacement additionally preserves each potion's heal/model/size; that is a test override, not required for a normal new affliction.

Mechanics read `Player.HasInjury`, which returns the native **effective severity** after suppression/reduction, clamped 0–3. Raw `HasInjuryInData` is appropriate for ownership/history, not active penalties. Here effective 1/2/3 yields movement 0.98/0.96/0.94; zero is neutral. Direct names, name terms and severity descriptions are distinct views; GetInjuryName consumes final text while GetInjuryDescTerm returns a term. Character/doctor/tooltip consumers must receive the correct variant rather than a reused prior label.

Native cure, reduction, suppression and bulk RemoveAllInjuries remove/update the effect immediately; no per-frame multiplication is cached. Foundation's independent rank/severity/order snapshot handles raw state and canonical transitions. For timed afflictions subscribe to lifecycle/capture/validation, preserve remaining durations, and clear pending effects when **effective** severity changes; the pack's `AfflictionBattleState.Sync` and `AfflictionsFeature` demonstrate suppression, cure/reacquisition and bulk-cure handling. A copied affliction never gets Leadfoot's old EmptySlot1 marker.

Manual check all three severities through normal potions, unchanged potion healing/cost/model, effective suppression/reduction/cure and character/tooltips after viewing a different injury. Acquire alongside Leadfoot and an independently keyed copy, save/exit/resume, cure each separately and start a fresh run. Confirm no stale timer/penalty after same-frame cure/reacquisition if you add timed state. This example has no timer to serialize; all native runtime behavior remains pending manual verification.
