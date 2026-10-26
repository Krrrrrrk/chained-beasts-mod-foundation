using ChainedBeasts.ModFoundation;
using ChainedBeasts.ModFoundation.Runtime;
using ChainedBeasts.ModFoundation.Stats;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace CategoryExamples;

internal static class SurgeryExample
{
    public const string Key="example.categories:surgery/practice-graft";
    public const int Id=12020;
    private static string _name,_description;
    private static bool Enabled=>FoundationServices.Content.TryGet(Key,out var content)&&content.Enabled;
    public static void Install(Harmony harmony)
    {
        _name=FoundationText.Register(Key,"Name","Practice Graft");
        _description=FoundationText.Register(Key,"Description","Stamina recovers 5% faster for this run. Once per player per run.");
        SurgeryServices.Register(Key,20,()=>new Surgery((SurgeryType)Id,SurgeryCategoryIcon.Enhancements)
            {moneyCost=20,damage=0,isDonation=false,isRare=false});
        FoundationServices.RegisterModifier(Key,StatChannel.StaminaRecoveryRate,
            context=>SessionAccess.CanUseDeveloperTools&&SurgeryServices.IsOwned(context.Data,Key)?1.05:1);
        FeaturePatcher.Patch(harmony,typeof(SurgeryExample),FeaturePatcher.RequireMethod(typeof(StringManager),nameof(StringManager.GetSurgeryName),typeof(Surgery)),prefix:nameof(Name));
        FeaturePatcher.Patch(harmony,typeof(SurgeryExample),FeaturePatcher.RequireMethod(typeof(StringManager),nameof(StringManager.GetSurgeryDesc),typeof(Surgery),typeof(InjuryContainer),
            typeof(Il2CppReferenceArray<Il2CppSystem.Collections.Generic.Dictionary<InjuryType,int>>),typeof(int),typeof(bool)),prefix:nameof(Description));
    }
    private static bool Name(Surgery surgery,ref string __result){if(!Enabled||surgery is null||(int)surgery.surgeryType!=Id)return true;__result=_name;return false;}
    // Surgery descriptions consume final display text. Returning a term here would display its raw key.
    private static bool Description(Surgery surgery,ref string __result){if(!Enabled||surgery is null||(int)surgery.surgeryType!=Id)return true;__result=FoundationText.TryGet(_description,out var text)?text:"Stamina recovers 5% faster for this run.";return false;}
}
