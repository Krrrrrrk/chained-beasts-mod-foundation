using System;
using ChainedBeasts.ModFoundation;
using ChainedBeasts.ModFoundation.Runtime;
using ChainedBeasts.ModFoundation.Stats;
using HarmonyLib;
using TermsAndContusions.Skills;

namespace TermsAndContusions.Equipment;

internal static class CounterweightSlingFeature
{
    internal const string Key = "terms-and-contusions:equipment/counterweight-sling";
    internal const int Id = 2498;
    internal const int Price = 45; // Runner's Wraps is 40; native Running Shoes/Double Durability are the comparison group.
    private static string _name, _description;
    private static bool _added;
    internal static void Install(Harmony harmony)
    {
        _name = FoundationText.Register(Key, "Name", "Counterweight Sling");
        _description = FoundationText.Register(Key, "Description", PackText.Description("Doubles throw range and charge speed."));
        FoundationServices.RegisterLibraryCallbacks(Key, Add, library =>
        { if (library.PurchaseDict is null || !library.PurchaseDict.ContainsKey((PurchaseType)Id)) throw new InvalidOperationException("Sling not indexed"); });
        foreach (var channel in new[] { StatChannel.ThrowRange, StatChannel.ThrowChargeSpeed })
            FoundationServices.RegisterModifier(Key, channel, context => SkillMath.ThrowMultiplier(0, context.Player?.HasEquipment((PurchaseType)Id) == true));
        FeaturePatcher.Patch(harmony, typeof(CounterweightSlingFeature),
            FeaturePatcher.RequireMethod(typeof(StringManager), nameof(StringManager.GetPurchaseName), typeof(PurchaseType)), prefix: nameof(Name));
        FeaturePatcher.Patch(harmony, typeof(CounterweightSlingFeature),
            FeaturePatcher.RequireMethod(typeof(StringManager), nameof(StringManager.GetPurchaseDesc), typeof(PurchaseType)), prefix: nameof(Description));
    }
    private static bool Enabled => FoundationServices.Content.TryGet(Key, out var entry) && entry.Enabled;
    private static bool Name(PurchaseType purchaseType, ref string __result)
    { if (!Enabled || (int)purchaseType != Id) return true; __result = _name; return false; }
    private static bool Description(PurchaseType purchaseType, ref string __result)
    { if (!Enabled || (int)purchaseType != Id) return true; __result = _description; return false; }
    private static void Add(GameLibrary library)
    {
        PurchaseDef visual = null;
        for (var i = 0; i < library.allPurchases.Count; i++)
        {
            var item = library.allPurchases[i]; if (item is null) continue;
            if ((int)item.purchaseType == Id)
            { if (_added && item.name == Key) return; throw new InvalidOperationException("Foreign sling purchase ID collision"); }
            if (item.purchaseType == PurchaseType.RunningShoes) visual = item;
        }
        if (visual is null) throw new InvalidOperationException("Sling visual reference missing");
        library.allPurchases.Add(new PurchaseDef { name = Key, purchaseType = (PurchaseType)Id, releaseStatus = ReleaseStatus.InFullGame,
            offerModifiers = (OfferModifiers)0, category = PurchaseCategory.Equipment, cost = Price, multiplayerScaling = 0,
            tooltipCategories = StatusTooltipType.None, modelPrefab = visual.modelPrefab, sprite = visual.sprite });
        _added = true;
    }
}
