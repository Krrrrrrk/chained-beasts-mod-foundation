using System;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections.Generic;
using Unity.Netcode;

namespace ChainedBeasts.ModFoundation.Surgeries;

internal static class AdrenalGraftFeature
{
    internal const string DisplayName = "Adrenal Graft";
    internal const string Description = "Stamina regenerates 25% faster for the rest of this run.";
    internal const string NameTerm = "ModFoundation/AdrenalGraft/Name";
    internal const string DescriptionTerm = "ModFoundation/AdrenalGraft/Description";

    private const int Price = 0;
    private static readonly SurgeryType AdrenalGraftSurgeryType =
        (SurgeryType)CustomContentIds.AdrenalGraftSurgery;

    private static ManualLogSource _logger;
    private static bool _forceNextOffer;
    private static bool _forceLogWritten;

    private sealed class AcquisitionState
    {
        public PlayerData PlayerData;
        public Surgery Surgery;
        public int MoneyBefore;
        public bool AlreadyOwned;
        public bool IsExecutingRpc;
    }

    internal static void Install(Harmony harmony, ManualLogSource logger, bool forceNextOffer)
    {
        if (Enum.IsDefined(typeof(SurgeryType), CustomContentIds.AdrenalGraftSurgery))
        {
            throw new InvalidOperationException(
                $"SurgeryType value {CustomContentIds.AdrenalGraftSurgery} is now native; Adrenal Graft stopped to avoid a collision.");
        }

        _logger = logger;
        _forceNextOffer = forceNextOffer;
        _forceLogWritten = false;

        FeaturePatcher.Patch(harmony, typeof(AdrenalGraftFeature),
            FeaturePatcher.RequireMethod(typeof(SurgeryGenerator), nameof(SurgeryGenerator.GetSurgeries),
                typeof(PlayerData), typeof(int), typeof(int), typeof(bool), typeof(int), typeof(RunDataManager),
                typeof(Dictionary<InjuryType, int>)),
            postfix: nameof(GetSurgeriesPostfix));
        FeaturePatcher.Patch(harmony, typeof(AdrenalGraftFeature),
            FeaturePatcher.RequireMethod(typeof(StringManager), nameof(StringManager.GetSurgeryName), typeof(Surgery)),
            prefix: nameof(GetSurgeryNamePrefix));
        FeaturePatcher.Patch(harmony, typeof(AdrenalGraftFeature),
            FeaturePatcher.RequireMethod(typeof(StringManager), nameof(StringManager.GetSurgeryDesc),
                typeof(Surgery), typeof(InjuryContainer),
                typeof(Il2CppReferenceArray<Dictionary<InjuryType, int>>), typeof(int), typeof(bool)),
            prefix: nameof(GetSurgeryDescPrefix));
        FeaturePatcher.Patch(harmony, typeof(AdrenalGraftFeature),
            FeaturePatcher.RequireMethod(typeof(RunDataManager), nameof(RunDataManager.PerformSurgeryRpc),
                typeof(int), typeof(int), typeof(int)),
            prefix: nameof(PerformSurgeryPrefix), postfix: nameof(PerformSurgeryPostfix));
        FeaturePatcher.Patch(harmony, typeof(AdrenalGraftFeature),
            FeaturePatcher.RequirePropertyGetter(typeof(Player), nameof(Player.StaminaRecoveryRate)),
            postfix: nameof(StaminaRecoveryRatePostfix));

        _logger?.LogInfo(
            $"[ChainedBeasts.ModFoundation] Adrenal Graft hooks installed: SurgeryType={CustomContentIds.AdrenalGraftSurgery}, " +
            $"price={Price}, forceNextOffer={_forceNextOffer}");
    }

    private static void GetSurgeriesPostfix(
        PlayerData playerData,
        int remainingMoney,
        RunDataManager runDataManager,
        List<Surgery> __result)
    {
        if (playerData is null || __result is null || HasAdrenalGraft(playerData))
        {
            return;
        }

        // The native generator builds and chooses its candidates inside one method,
        // so a runtime-added SurgeryType cannot be inserted into a separate registry.
        // We preserve the option count by substituting one generated slot. With the
        // developer override off, a deterministic one-in-four eligible visit gate
        // approximates participation in the native random pool without disturbing RNG.
        if (!_forceNextOffer)
        {
            if (__result.Count == 0 || remainingMoney < Price)
            {
                return;
            }

            var seed = (runDataManager?.RunSeed ?? 0) ^
                       ((runDataManager?.CompletedLevels ?? 0) * 397) ^
                       (playerData.playerID * 7919);
            if ((seed & 3) != 0)
            {
                return;
            }
        }

        if (__result.Count == 0)
        {
            __result.Add(CreateSurgery());
        }
        else
        {
            var replacementIndex = _forceNextOffer ? 0 : __result.Count - 1;
            __result[replacementIndex] = CreateSurgery();
        }

        if (_forceNextOffer && !_forceLogWritten)
        {
            _forceLogWritten = true;
            _logger?.LogInfo(
                $"[ChainedBeasts.ModFoundation] developer offer override inserted Adrenal Graft into a surgery slot; " +
                $"remainingMoney={remainingMoney}, cost={Price}");
        }
    }

    private static Surgery CreateSurgery()
    {
        return new Surgery(AdrenalGraftSurgeryType, SurgeryCategoryIcon.Enhancements)
        {
            isDonation = false,
            isRare = false,
            damage = 0,
            moneyCost = Price
        };
    }

    private static bool GetSurgeryNamePrefix(Surgery surgery, ref string __result)
    {
        if (surgery is null || surgery.surgeryType != AdrenalGraftSurgeryType)
        {
            return true;
        }

        // Surgeon option widgets consume a localization term here, not final
        // display text. A literal string otherwise leaves stale pooled text.
        __result = NameTerm;
        return false;
    }

    private static bool GetSurgeryDescPrefix(Surgery surgery, ref string __result)
    {
        if (surgery is null || surgery.surgeryType != AdrenalGraftSurgeryType)
        {
            return true;
        }

        // Unlike the name widget, the surgeon's description field consumes
        // final display text and does not pass it through I2 localization.
        __result = CustomLocalization.GetText(DescriptionTerm, Description);
        return false;
    }

    private static void PerformSurgeryPrefix(
        RunDataManager __instance,
        int playerIndex,
        int surgeryIndex,
        out AcquisitionState __state)
    {
        __state = null;

        try
        {
            var playerData = __instance.GetPlayerDataByIndex(playerIndex);
            if (playerData?.surgeryOptions is null || surgeryIndex < 0 || surgeryIndex >= playerData.surgeryOptions.Count)
            {
                return;
            }

            var surgery = playerData.surgeryOptions[surgeryIndex];
            if (surgery is null || surgery.surgeryType != AdrenalGraftSurgeryType)
            {
                return;
            }

            __state = new AcquisitionState
            {
                PlayerData = playerData,
                Surgery = surgery,
                MoneyBefore = __instance.Money,
                AlreadyOwned = HasAdrenalGraft(playerData),
                IsExecutingRpc = __instance.__rpc_exec_stage == NetworkBehaviour.__RpcExecStage.Execute
            };
        }
        catch (Exception exception)
        {
            _logger?.LogError(
                $"[ChainedBeasts.ModFoundation] Adrenal Graft acquisition snapshot failed: {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static void PerformSurgeryPostfix(RunDataManager __instance, AcquisitionState __state)
    {
        if (__state is null || !__state.IsExecutingRpc || __state.AlreadyOwned ||
            __state.PlayerData is null || __state.Surgery is null)
        {
            return;
        }

        try
        {
            // The execution-stage check distinguishes the received RPC from its
            // zero-cost outbound wrapper. The exact delta remains valid for both
            // free and priced copies of this example.
            if (__instance.Money != __state.MoneyBefore - __state.Surgery.moneyCost)
            {
                return;
            }

            if (HasAdrenalGraft(__state.PlayerData))
            {
                return;
            }

            __state.PlayerData.previousSurgeries.Add(AdrenalGraftSurgeryType);
            _logger?.LogInfo(
                $"[ChainedBeasts.ModFoundation] Adrenal Graft acquired by player {__state.PlayerData.playerID}");
        }
        catch (Exception exception)
        {
            _logger?.LogError(
                $"[ChainedBeasts.ModFoundation] Adrenal Graft acquisition bookkeeping failed: {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static void StaminaRecoveryRatePostfix(Player __instance, ref float __result)
    {
        if (__instance is not null && HasAdrenalGraft(__instance.PlayerData))
        {
            __result *= 1.25f;
        }
    }

    private static bool HasAdrenalGraft(PlayerData playerData)
    {
        return playerData?.previousSurgeries is not null &&
               playerData.previousSurgeries.Contains(AdrenalGraftSurgeryType);
    }
}
