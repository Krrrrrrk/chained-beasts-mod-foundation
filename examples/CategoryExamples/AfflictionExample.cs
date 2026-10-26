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
