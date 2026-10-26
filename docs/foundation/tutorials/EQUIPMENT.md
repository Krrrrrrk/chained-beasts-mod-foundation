# End-to-end equipment: Practice Wraps

Build the whole optional [CategoryExamples project](../../../examples/CategoryExamples/CategoryExamples.csproj). Its [loader](../../../examples/CategoryExamples/Plugin.cs) claims all three categories atomically before calling each Install; [separate-plugin setup](../SEPARATE_MOD.md) explains extracting only this category. `Practice Wraps` uses purchase ID 2470, price 35 and a separate namespaced key. Match native equipment classification constraints as well as checking enum vacancy; numbers from another category do not establish eligibility.

<!-- source: examples/CategoryExamples/EquipmentExample.cs -->
```csharp
using System;
using ChainedBeasts.ModFoundation;
using ChainedBeasts.ModFoundation.Runtime;
using ChainedBeasts.ModFoundation.Stats;
using HarmonyLib;

namespace CategoryExamples;

internal static class EquipmentExample
{
    public const string Key = "example.categories:equipment/practice-wraps";
    public const int Id = 2470;
    private static string _name, _description;
    private static bool _added;
    private static bool Enabled => FoundationServices.Content.TryGet(Key, out var content) && content.Enabled;
    public static void Install(Harmony harmony)
    {
        _name = FoundationText.Register(Key, "Name", "Practice Wraps");
        _description = FoundationText.Register(Key, "Description", "Stamina recovers 5% faster while equipped.");
        FoundationServices.RegisterLibraryCallbacks(Key, Add, library => {
            if (library.PurchaseDict?.ContainsKey((PurchaseType)Id) != true) throw new InvalidOperationException("Practice Wraps not indexed"); });
        FoundationServices.RegisterModifier(Key, StatChannel.StaminaRecoveryRate,
            context => SessionAccess.CanUseDeveloperTools && context.Data?.currentEquipment == (PurchaseType)Id ? 1.05 : 1);
        foreach (var pair in new[] { (nameof(StringManager.GetPurchaseName), nameof(Name)), (nameof(StringManager.GetPurchaseDesc), nameof(Description)) })
            FeaturePatcher.Patch(harmony, typeof(EquipmentExample), FeaturePatcher.RequireMethod(typeof(StringManager), pair.Item1, typeof(PurchaseType)), prefix: pair.Item2);
    }
    private static void Add(GameLibrary library)
    {
        PurchaseDef visual = null;
        for (var i = 0; i < library.allPurchases.Count; i++) {
            var item = library.allPurchases[i]; if (item is null) continue;
            if ((int)item.purchaseType == Id) { if (_added && item.name == Key) return; throw new InvalidOperationException("Foreign equipment ID collision"); }
            if (item.purchaseType == PurchaseType.RunningShoes) visual = item;
        }
        if (visual is null) throw new InvalidOperationException("Native visual source missing");
        library.allPurchases.Add(new PurchaseDef { name=Key, purchaseType=(PurchaseType)Id, category=PurchaseCategory.Equipment,
            releaseStatus=ReleaseStatus.InFullGame, offerModifiers=(OfferModifiers)0, cost=35, multiplayerScaling=0,
            tooltipCategories=StatusTooltipType.None, modelPrefab=visual.modelPrefab, sprite=visual.sprite });
        _added = true;
    }
    // These shop consumers expect I2 terms, not final literal text.
    private static bool Name(PurchaseType purchaseType, ref string __result) { if (!Enabled || (int)purchaseType != Id) return true; __result=_name; return false; }
    private static bool Description(PurchaseType purchaseType, ref string __result) { if (!Enabled || (int)purchaseType != Id) return true; __result=_description; return false; }
}
```
<!-- /source -->

`RegisterLibraryCallbacks` adds a fresh PurchaseDef with Equipment category before native PurchaseDict indexing. The RunningShoes donor supplies only sprite/model references; its purchase type/effects are not copied. Foundation exposes the registered enabled ID to native generation. Native shop eligibility, price, money deduction, equipped slot and history stay authoritative. GetPurchaseName/Desc consumers expect localized terms, which Foundation resolves; returning a literal to the wrong pooled view can leave stale donor text.

The 1.05 recovery factor reads actual `currentEquipment` on every evaluation. The native single slot means swapping items removes the old contribution immediately. Re-equipping needs no stat cache and doesn't create a new ownership save record; native equipped/history data already persists. Never add a competing sidecar equipment owner or call ReceiveEquipmentRPC during restoration.

For limited allowances, an equipment switch is **not** a new battle. See pack `EquipmentBattleState` and `EquipmentFeature`: Ledger pays/then confirms once at real battle start, Scabbard reserves against the physical weapon token until an actual hit receipt, and Hook repairs a previously thrown physical weapon once per battle. Keep paid/spent ledgers after unequip; don't rearm on repair/drop/UI refresh. Use native currency/durability APIs and serializable independent tokens, not pointers. [Mechanics](../MECHANICS.md) describes events and [persistence](../PERSISTENCE.md) covers full-state validation and missing arena instances.

Manual check: buy through the normal shop for 35 gold, inspect offer/detail text, compare stamina recovery x1.05, swap/re-equip and verify only the active item contributes. Save/resume equipped/history with another mod's equipment present. Ensure donor shoes' effects didn't transfer, ordinary shops/costs still work and configuration disable follows owned-content restore policy. Build success is not this live evidence; the optional example remains runtime-unverified.
