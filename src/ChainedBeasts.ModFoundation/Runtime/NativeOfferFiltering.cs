using System;
using System.Linq;
using ChainedBeasts.ModFoundation.Content;
using HarmonyLib;

namespace ChainedBeasts.ModFoundation.Runtime;

// Filter generator input, not its returned slots: purchases/reveals keep their
// original indices, native eligibility, random selection and costs.
internal static class NativeOfferFiltering
{
    internal sealed class Scope
    {
        internal GameLibrary Library;
        internal Il2CppSystem.Collections.Generic.List<SkillDef> Skills, FilteredSkills;
        internal Il2CppSystem.Collections.Generic.List<PurchaseDef> Purchases, FilteredPurchases;
        internal Il2CppSystem.Collections.Generic.List<InjuryDef> Injuries, FilteredInjuries;
    }
    internal static void Install(Harmony harmony)
    {
        void Patch(Type type, string name, string before, params Type[] args) => FeaturePatcher.Patch(harmony, typeof(NativeOfferFiltering),
            FeaturePatcher.RequireMethod(type, name, args), prefix: before, finalizer: nameof(Finally));
        Patch(typeof(SkillGenerator), nameof(SkillGenerator.GenerateTrainingSkills), nameof(SkillsBefore), typeof(int),
            typeof(Il2CppSystem.Collections.Generic.Dictionary<int, Il2CppSystem.Collections.Generic.List<SkillType>>));
        Patch(typeof(SkillGenerator), nameof(SkillGenerator.GetRandomSkill), nameof(SkillsBefore), typeof(PlayerData),
            typeof(Il2CppSystem.Collections.Generic.List<SkillType>), typeof(Il2CppSystem.Collections.Generic.List<SkillType>));
        Patch(typeof(PurchaseGenerator), nameof(PurchaseGenerator.GenerateShopOptions), nameof(PurchasesBefore), typeof(int), typeof(int));
        Patch(typeof(InjuryGenerator), nameof(InjuryGenerator.GeneratePotions), nameof(InjuriesBefore), typeof(RunDataManager), typeof(int), typeof(int), typeof(InjuryType).MakeByRefType());
        // Clever Monkey's training event and other native injury rolls bypass
        // GeneratePotions. They must use the same selected affliction pool.
        Patch(typeof(InjuryGenerator), nameof(InjuryGenerator.GetRandomInjury), nameof(InjuriesBefore), typeof(PlayerData), typeof(int), typeof(BodyPart),
            typeof(Il2CppSystem.Collections.Generic.List<InjuryType>), typeof(Il2CppSystem.Collections.Generic.Dictionary<InjuryType, int>), typeof(bool));
    }
    private static bool Allowed(ContentCategory category, int id) => !FoundationServices.Content.TryGet(category, id, out var entry) ||
        (entry.Enabled && ContentOfferServices.Allows(entry.Key));
    private static Il2CppSystem.Collections.Generic.List<T> Filter<T>(Il2CppSystem.Collections.Generic.List<T> source, Func<T, bool> allow)
    {
        if (source is null) return null;
        var changed = false;
        for (var i = 0; i < source.Count; i++) if (!allow(source[i])) { changed = true; break; }
        if (!changed) return null;
        var result = new Il2CppSystem.Collections.Generic.List<T>();
        for (var i = 0; i < source.Count; i++) if (allow(source[i])) result.Add(source[i]);
        return result;
    }
    private static void SkillsBefore(out Scope __state)
    {
        __state = null; if (!GameLibrary.InstanceExists) return;
        var library = GameLibrary.Instance;
        var filtered = Filter(library.allSkills, item => item is null || Allowed(ContentCategory.Skill, (int)item.skillType));
        if (filtered is null) return;
        __state = new Scope { Library = library, Skills = library.allSkills, FilteredSkills = filtered };
        library.allSkills = filtered;
    }
    private static void PurchasesBefore(out Scope __state)
    {
        __state = null; if (!GameLibrary.InstanceExists) return;
        var library = GameLibrary.Instance;
        var filtered = Filter(library.allPurchases, item => item is null || Allowed(ContentCategory.Equipment, (int)item.purchaseType));
        if (filtered is null) return;
        __state = new Scope { Library = library, Purchases = library.allPurchases, FilteredPurchases = filtered };
        library.allPurchases = filtered;
    }
    private static void InjuriesBefore(out Scope __state)
    {
        __state = null; if (!GameLibrary.InstanceExists) return;
        var library = GameLibrary.Instance;
        var filtered = Filter(library.allInjuries, item => item is null || Allowed(ContentCategory.Affliction, (int)item.injuryType));
        if (filtered is null) return;
        __state = new Scope { Library = library, Injuries = library.allInjuries, FilteredInjuries = filtered };
        library.allInjuries = filtered;
    }
    private static Exception Finally(Exception __exception, Scope __state)
    {
        var library = __state?.Library;
        if (library == null) return __exception;
        if (__state.FilteredSkills is not null && library.allSkills?.Pointer == __state.FilteredSkills.Pointer) library.allSkills = __state.Skills;
        if (__state.FilteredPurchases is not null && library.allPurchases?.Pointer == __state.FilteredPurchases.Pointer) library.allPurchases = __state.Purchases;
        if (__state.FilteredInjuries is not null && library.allInjuries?.Pointer == __state.FilteredInjuries.Pointer) library.allInjuries = __state.Injuries;
        return __exception;
    }
}
