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
