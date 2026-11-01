using System;
using BepInEx.Logging;
using TermsAndContusions.FirstPerson;
using ChainedBeasts.ModFoundation;
using ChainedBeasts.ModFoundation.Runtime;
using HarmonyLib;

namespace TermsAndContusions.Afflictions;

internal static class FirstPersonPotionFeature
{
    internal const int Id = 11021;
    internal const string Key = "terms-and-contusions:affliction/eye-level";
    private static ManualLogSource _log;
    private static bool Enabled => FoundationServices.Content.TryGet(Key, out var entry) && entry.Enabled;
    internal static void Install(Harmony harmony, ManualLogSource log)
    {
        if (!ForcedViewServices.Available) throw new InvalidOperationException("First-person runtime is unavailable; Eye Level cannot be offered.");
        _log = log;
        FeaturePatcher.Patch(harmony, typeof(FirstPersonPotionFeature),
            FeaturePatcher.RequireMethod(typeof(InjuryGenerator), nameof(InjuryGenerator.GeneratePotions), typeof(RunDataManager),
                typeof(int), typeof(int), typeof(InjuryType).MakeByRefType()), postfix: nameof(Potions));
        FeaturePatcher.Patch(harmony, typeof(FirstPersonPotionFeature),
            FeaturePatcher.RequireMethod(typeof(PlayerData), nameof(PlayerData.AddInjury), typeof(InjuryType), typeof(int)),
            prefix: nameof(Adding));
        FeaturePatcher.Patch(harmony, typeof(FirstPersonPotionFeature),
            FeaturePatcher.RequireMethod(typeof(RunDataManager), nameof(RunDataManager.OnLevelComplete)), postfix: nameof(Completed));
        ForcedViewServices.Register(Key, Forced);
        _log.LogInfo("[Terms & Contusions] Eye Level registered: three skulls, strongest generated potion healing, expires on level completion");
    }
    private static bool Forced(Player player) => !CameraIntegration.ManualSwitching && Enabled && SessionAccess.GameplayReady && player != null &&
        SessionAccess.RuntimePlayer(player.playerData)?.Pointer == player.Pointer && player.HasInjury((InjuryType)Id) > 0;

    private static void Adding(PlayerData __instance, InjuryType injuryType, ref int numberOfLevels)
    {
        if (Enabled && __instance is not null && (int)injuryType == Id && numberOfLevels > 0)
            numberOfLevels = Math.Max(0, 3 - __instance.HasInjuryInData(injuryType));
    }

    private static void Potions(Il2CppSystem.Collections.Generic.List<Potion> __result)
    {
        if (!Enabled || __result is null) return;
        var strongest = 0;
        for (var i = 0; i < __result.Count; i++)
        {
            var potion = __result[i];
            if (potion is not null && !potion.IsWater) strongest = Math.Max(strongest, potion._healAmount);
        }
        for (var i = 0; i < __result.Count; i++)
        {
            var potion = __result[i];
            if (potion is null || (int)potion.injury != Id) continue;
            // Keep the actual native offer and index. Only this new entry is
            // promoted. Native attainable-health and healing modifiers still run.
            potion.potionSize = 3; potion._healAmount = strongest;
            potion.nameTerm = Key + "/Name";
            potion.modelIdx = 0; // Valid first large-potion model, independent of its original tier.
        }
    }
    private static void Completed(RunDataManager __instance)
    {
        if (!Enabled || __instance?.IsServer != true || !SessionAccess.GameplayReady || __instance.playerDatas is null) return;
        for (var i = 0; i < __instance.playerDatas.Count; i++)
        {
            var data = __instance.playerDatas[i];
            if (data is null || data.HasInjuryInData((InjuryType)Id) == 0) continue;
            // Food Poisoning is removed through this same native authority/RPC
            // path in OnLevelComplete. This also updates remote ownership and UI.
            __instance.ApplyRemovePlayerInjury(data.playerID, (InjuryType)Id);
        }
    }
}
