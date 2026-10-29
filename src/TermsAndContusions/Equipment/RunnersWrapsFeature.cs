using ChainedBeasts.ModFoundation;
using ChainedBeasts.ModFoundation.Runtime;
using ChainedBeasts.ModFoundation.Stats;
using System;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

namespace TermsAndContusions.Equipment;

internal static class RunnersWrapsFeature
{
    private static bool Enabled => FoundationServices.Content.TryGet(CustomContentIds.RunnersWrapsKey, out var entry) && entry.Enabled;
    internal const string DisplayName = "Runner's Wraps";
    internal const string Description = "TC: Dodges use 25% less stamina while equipped.";
    internal const string NameTerm = "ModFoundation/RunnersWraps/Name";
    internal const string DescriptionTerm = "ModFoundation/RunnersWraps/Description";

    private const int Price = 40;
    private static readonly PurchaseType RunnersWrapsPurchaseType =
        (PurchaseType)CustomContentIds.RunnersWrapsPurchase;

    private static ManualLogSource _logger;
    private static bool _forceNextOffer;
    private static bool _definitionAdded;
    private static bool _registrationReady;
    private static bool _forceLogWritten;

    internal static void Install(Harmony harmony, ManualLogSource logger, bool forceNextOffer)
    {
        if (Enum.IsDefined(typeof(PurchaseType), CustomContentIds.RunnersWrapsPurchase))
        {
            throw new InvalidOperationException(
                $"PurchaseType value {CustomContentIds.RunnersWrapsPurchase} is now native; Runner's Wraps stopped to avoid a collision.");
        }

        _logger = logger;
        _forceNextOffer = forceNextOffer;
        _definitionAdded = false;
        _registrationReady = false;
        _forceLogWritten = false;

        FoundationServices.RegisterLibraryCallbacks(CustomContentIds.RunnersWrapsKey, GameLibraryInitPrefix, GameLibraryInitPostfix);
        FeaturePatcher.Patch(harmony, typeof(RunnersWrapsFeature),
            FeaturePatcher.RequireMethod(typeof(StringManager), nameof(StringManager.GetPurchaseName), typeof(PurchaseType)),
            prefix: nameof(GetPurchaseNamePrefix));
        FeaturePatcher.Patch(harmony, typeof(RunnersWrapsFeature),
            FeaturePatcher.RequireMethod(typeof(StringManager), nameof(StringManager.GetPurchaseDesc), typeof(PurchaseType)),
            prefix: nameof(GetPurchaseDescPrefix));
        FeaturePatcher.Patch(harmony, typeof(RunnersWrapsFeature),
            FeaturePatcher.RequireMethod(typeof(PurchaseGenerator), nameof(PurchaseGenerator.GenerateShopOptions), typeof(int), typeof(int)),
            postfix: nameof(GenerateShopOptionsPostfix));
        FoundationServices.RegisterModifier(CustomContentIds.RunnersWrapsKey, StatChannel.DodgeStaminaCost,
            context => context.Player?.HasEquipment(RunnersWrapsPurchaseType) == true ? 0.75d : 1d);

        _logger?.LogInfo(
            $"[Terms & Contusions] Runner's Wraps hooks installed: PurchaseType={CustomContentIds.RunnersWrapsPurchase}, " +
            $"price={Price}, forceNextOffer={_forceNextOffer}");
    }

    private static void GameLibraryInitPrefix(GameLibrary __instance)
    {
        var purchases = __instance.allPurchases
            ?? throw new InvalidOperationException("GameLibrary.allPurchases is null before GameLibrary.Init.");

        PurchaseDef assetSource = null;
        for (var index = 0; index < purchases.Count; index++)
        {
            var purchase = purchases[index];
            if (purchase is null)
            {
                continue;
            }

            if ((int)purchase.purchaseType == CustomContentIds.RunnersWrapsPurchase)
            {
                if (_definitionAdded && string.Equals(purchase.name, "RunnersWraps", StringComparison.Ordinal))
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"GameLibrary already contains PurchaseType {CustomContentIds.RunnersWrapsPurchase}; registration stopped.");
            }

            if (purchase.purchaseType == global::PurchaseType.RunningShoes)
            {
                assetSource = purchase;
            }
        }

        if (assetSource is null)
        {
            throw new InvalidOperationException("The Running Shoes equipment definition used for prototype assets was not found.");
        }

        // This is a new independent definition. Only the model and sprite references
        // are reused; no native PurchaseType behavior is inherited.
        purchases.Add(new PurchaseDef
        {
            name = "RunnersWraps",
            purchaseType = RunnersWrapsPurchaseType,
            releaseStatus = ReleaseStatus.InFullGame,
            offerModifiers = (OfferModifiers)0,
            category = PurchaseCategory.Equipment,
            cost = Price,
            multiplayerScaling = 0f,
            tooltipCategories = StatusTooltipType.None,
            modelPrefab = assetSource.modelPrefab,
            sprite = assetSource.sprite
        });

        _definitionAdded = true;
    }

    private static void GameLibraryInitPostfix(GameLibrary __instance)
    {
        _registrationReady = __instance.PurchaseDict is not null &&
                             __instance.PurchaseDict.ContainsKey(RunnersWrapsPurchaseType);

        if (_registrationReady)
        {
            _logger?.LogInfo(
                $"[Terms & Contusions] Runner's Wraps registered in GameLibrary as {CustomContentIds.RunnersWrapsPurchase}");
        }
        else
        {
            throw new InvalidOperationException("Runner's Wraps was not present in PurchaseDict after GameLibrary.Init");
        }
    }

    private static bool GetPurchaseNamePrefix(PurchaseType purchaseType, ref string __result)
    {
        if (!Enabled || purchaseType != RunnersWrapsPurchaseType)
        {
            return true;
        }

        // Shop cards and character-sheet tooltips assign this return value to
        // an I2 Localize.Term. Returning display text makes I2 interpret that
        // text as a missing key and leaves the pooled card's previous text.
        __result = NameTerm;
        return false;
    }

    private static bool GetPurchaseDescPrefix(PurchaseType purchaseType, ref string __result)
    {
        if (!Enabled || purchaseType != RunnersWrapsPurchaseType)
        {
            return true;
        }

        __result = DescriptionTerm;
        return false;
    }

    private static void GenerateShopOptionsPostfix(List<PurchaseType> __result)
    {
        if (!ContentOfferServices.Allows(CustomContentIds.RunnersWrapsKey) || !_forceNextOffer || !SessionAccess.CanUseDeveloperTools || !_registrationReady || __result is null || __result.Count == 0)
        {
            return;
        }

        for (var index = 0; index < __result.Count; index++)
        {
            if (__result[index] == RunnersWrapsPurchaseType)
            {
                return;
            }
        }

        for (var index = 0; index < __result.Count; index++)
        {
            if (!GladiatorConsts.IsEquipment(__result[index]))
            {
                continue;
            }

            __result[index] = RunnersWrapsPurchaseType;
            if (!_forceLogWritten)
            {
                _forceLogWritten = true;
                _logger?.LogInfo("[Terms & Contusions] developer offer override inserted Runner's Wraps into an equipment shop slot");
            }

            return;
        }
    }


}
