# End-to-end surgery: Practice Graft

Use the complete [CategoryExamples loader/project](../../../examples/CategoryExamples/Plugin.cs), then [rename identities](../SEPARATE_MOD.md). Practice Graft uses SurgeryType 12020 and price 20. No native GameLibrary surgery registry exists; don't register one or add another replace-the-last-slot postfix.

<!-- source: examples/CategoryExamples/SurgeryExample.cs -->
```csharp
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
```
<!-- /source -->

The loader claims the Surgery descriptor before Install. `SurgeryServices.Register` joins the one shared eligible pool with a native factory for its claimed ID/quote. The new Surgery constructor supplies empty native effect/injury/skill lists. Category Enhancements selects a reused native icon. No donor surgery effects are copied. Native returned options plus unowned/affordable enabled custom candidates are sampled deterministically without advancing Unity RNG; donations/first targeted cure are protected. The old Adrenal one-in-four gate is gone. Same-visit sampling doesn't promise fresh surgery rerolls.

Names return an I2 term; surgery descriptions return **final text**, because that field does not localize a term. This deliberate distinction fixes the raw-key description error from the older example. Normal selection, money, Soap healing, codex and award bookkeeping remain native.

The core guard checks custom ownership/enable/provider/quote/funds before payment. It grants ownership only after the same player's actual `ModifyAwardStat(MostSurgeries,1)` completion-tail callback, successful surrounding `PerformSurgeryRpc(int,int,int)` return, expected payment and absent prior ownership. A received RPC or unchanged money alone isn't proof, especially for free operations. Native `previousSurgeries` is the sole ownership truth and already appears in native checkpoints. A zero-price copy still needs genuine completion. There is no arbitrary remove/rebuy API: surgery lasts this run; a fresh run resets history.

The factor reads confirmed history and returns 1.05 thereafter. No one-time resource effect is replayed on restore. If you add max-HP/stamina surgery factors, the core confirmed transition scales current percentages once. For timers such as Clot/Talons, use a separate versioned per-player feature ledger with capture/validation/remaining-time restoration, explicit generated-damage provenance and lifecycle cleanup. See [native hook safety](../MECHANICS.md#native-hook-safety) and [actual advanced surgery code and limits](../../terms-and-contusions/SURGERIES.md).

Manual check normal offer/name/description/category, exact 20-gold payment, one native history entry and x1.05 recovery; insufficient funds/repeated stale selections must not grant or charge again. Save/resume with Adrenal Graft and another independent surgery, no purchase/grant replay; fresh run unowned. Test Foundation-only plus examples and coexistence with the pack. This compiled example is not a verified native runtime result.
