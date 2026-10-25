using System;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

namespace ChainedBeasts.ModFoundation.Afflictions;

internal static class LeadfootFeature
{
    internal const string DisplayName = "Leadfoot";

    private const string NameTerm = "ModFoundation/Leadfoot/Name";
    private const string DescriptionTermPrefix = "ModFoundation/Leadfoot/Description/";
    private const string ThreatTerm = "ModFoundation/Leadfoot/Threat";

    private static readonly InjuryType LeadfootInjuryType =
        (InjuryType)CustomContentIds.LeadfootInjury;
    private static ManualLogSource _logger;
    private static bool _forceNextOffer;
    private static bool _definitionAdded;
    private static bool _registrationReady;
    private static bool _forceLogWritten;
    private static int _checkpointMarkerIndex = -1;

    private sealed class CheckpointMarkerState
    {
        public PlayerData.Checkpoint Checkpoint;
        public bool HasMarker;
        public int Level;
        public int Order;
    }

    internal static void Install(Harmony harmony, ManualLogSource logger, bool forceNextOffer)
    {
        if (Enum.IsDefined(typeof(InjuryType), CustomContentIds.LeadfootInjury))
        {
            throw new InvalidOperationException(
                $"InjuryType value {CustomContentIds.LeadfootInjury} is now native; Leadfoot stopped to avoid a collision.");
        }

        _logger = logger;
        _forceNextOffer = forceNextOffer;
        _definitionAdded = false;
        _registrationReady = false;
        _forceLogWritten = false;
        _checkpointMarkerIndex = FindNativeInjuryIndex(InjuryType.EmptySlot1);

        FeaturePatcher.Patch(harmony, typeof(LeadfootFeature),
            FeaturePatcher.RequireMethod(typeof(GameLibrary), nameof(GameLibrary.Init)),
            prefix: nameof(GameLibraryInitPrefix), postfix: nameof(GameLibraryInitPostfix));
        FeaturePatcher.Patch(harmony, typeof(LeadfootFeature),
            FeaturePatcher.RequireMethod(typeof(GameLibrary), nameof(GameLibrary.GetAllInjuryTypes)),
            postfix: nameof(GetAllInjuryTypesPostfix));
        FeaturePatcher.Patch(harmony, typeof(LeadfootFeature),
            FeaturePatcher.RequireMethod(typeof(StringManager), nameof(StringManager.GetInjuryNameTerm), typeof(InjuryType)),
            prefix: nameof(GetInjuryNameTermPrefix));
        FeaturePatcher.Patch(harmony, typeof(LeadfootFeature),
            FeaturePatcher.RequireMethod(typeof(StringManager), nameof(StringManager.GetInjuryName), typeof(InjuryType), typeof(int)),
            prefix: nameof(GetInjuryNamePrefix));
        FeaturePatcher.Patch(harmony, typeof(LeadfootFeature),
            FeaturePatcher.RequireMethod(typeof(StringManager), nameof(StringManager.GetInjuryDescTerm), typeof(InjuryType), typeof(int)),
            prefix: nameof(GetInjuryDescTermPrefix));
        FeaturePatcher.Patch(harmony, typeof(LeadfootFeature),
            FeaturePatcher.RequireMethod(typeof(InjuryGenerator), nameof(InjuryGenerator.GeneratePotions),
                typeof(RunDataManager), typeof(int), typeof(int), typeof(InjuryType).MakeByRefType()),
            postfix: nameof(GeneratePotionsPostfix));
        FeaturePatcher.Patch(harmony, typeof(LeadfootFeature),
            FeaturePatcher.RequirePropertyGetter(typeof(Player), nameof(Player.FinalMovementSpeed)),
            postfix: nameof(FinalMovementSpeedPostfix));

        var createCheckpoint = FeaturePatcher.RequireMethod(typeof(PlayerData), nameof(PlayerData.CreateCheckpoint));
        var validateCheckpoint = FeaturePatcher.RequireMethod(typeof(PlayerData.Checkpoint), nameof(PlayerData.Checkpoint.Validate));
        var restoreCheckpoint = FeaturePatcher.RequireMethod(typeof(PlayerData), nameof(PlayerData.RestoreCheckpoint), typeof(PlayerData.Checkpoint));
        var checkpointSynced = FeaturePatcher.RequireMethod(typeof(PlayerData), nameof(PlayerData.OnCheckpointSynced), typeof(PlayerData.Checkpoint), typeof(bool));
        FeaturePatcher.Patch(harmony, typeof(LeadfootFeature), createCheckpoint, postfix: nameof(CreateCheckpointPostfix));
        FeaturePatcher.Patch(harmony, typeof(LeadfootFeature), validateCheckpoint,
            prefix: nameof(CheckpointValidatePrefix), postfix: nameof(CheckpointMarkerPostfix), finalizer: nameof(CheckpointMarkerFinalizer));
        FeaturePatcher.Patch(harmony, typeof(LeadfootFeature), restoreCheckpoint,
            prefix: nameof(PlayerDataRestoreCheckpointPrefix), postfix: nameof(PlayerDataRestoreCheckpointPostfix), finalizer: nameof(CheckpointMarkerFinalizer));
        FeaturePatcher.Patch(harmony, typeof(LeadfootFeature), checkpointSynced,
            prefix: nameof(PlayerDataOnCheckpointSyncedPrefix), postfix: nameof(PlayerDataOnCheckpointSyncedPostfix), finalizer: nameof(CheckpointMarkerFinalizer));

        _logger?.LogInfo(
            $"[ChainedBeasts.ModFoundation] Leadfoot hooks installed: InjuryType={CustomContentIds.LeadfootInjury}, " +
            $"maxSeverity=3, forceNextOffer={_forceNextOffer}, checkpointMarkerIndex={_checkpointMarkerIndex}");
    }

    private static void GameLibraryInitPrefix(GameLibrary __instance)
    {
        var injuries = __instance.allInjuries
            ?? throw new InvalidOperationException("GameLibrary.allInjuries is null before GameLibrary.Init.");

        Sprite icon = null;
        for (var index = 0; index < injuries.Count; index++)
        {
            var injury = injuries[index];
            if (injury is null)
            {
                continue;
            }

            if ((int)injury.injuryType == CustomContentIds.LeadfootInjury)
            {
                if (_definitionAdded && string.Equals(injury.name, "Leadfoot", StringComparison.Ordinal))
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"GameLibrary already contains InjuryType {CustomContentIds.LeadfootInjury}; registration stopped.");
            }

            if (injury.injuryType == InjuryType.WalkWithALimp)
            {
                icon = injury.sprite;
            }
        }

        if (icon is null)
        {
            throw new InvalidOperationException("The Broken Foot injury definition used for the Leadfoot icon was not found.");
        }

        injuries.Add(new InjuryDef
        {
            name = "Leadfoot",
            injuryType = LeadfootInjuryType,
            releaseStatus = ReleaseStatus.InFullGame,
            offerModifiers = (OfferModifiers)0,
            offerType = OfferType.Normal,
            maxLevel = 3,
            canBeDisabled = false,
            requiresCategory = (SkillCategory)0,
            unlocksCategory = (SkillCategory)0,
            tooltipCategories = StatusTooltipType.None,
            bodyPart = BodyPart.Legs,
            sprite = icon,
            test = false
        });

        _definitionAdded = true;
    }

    private static void GameLibraryInitPostfix(GameLibrary __instance)
    {
        _registrationReady = __instance.InjuryDict is not null &&
                             __instance.InjuryDict.ContainsKey(LeadfootInjuryType);

        if (_registrationReady)
        {
            _logger?.LogInfo(
                $"[ChainedBeasts.ModFoundation] Leadfoot registered in GameLibrary as {CustomContentIds.LeadfootInjury}");
        }
        else
        {
            _logger?.LogError("[ChainedBeasts.ModFoundation] Leadfoot was not present in InjuryDict after GameLibrary.Init");
        }
    }

    private static void GetAllInjuryTypesPostfix(List<InjuryType> __result)
    {
        if (__result is not null && !__result.Contains(LeadfootInjuryType))
        {
            __result.Add(LeadfootInjuryType);
        }
    }

    private static bool GetInjuryNameTermPrefix(InjuryType injuryType, ref string __result)
    {
        if (injuryType != LeadfootInjuryType)
        {
            return true;
        }

        __result = NameTerm;
        return false;
    }

    private static bool GetInjuryNamePrefix(InjuryType injuryType, ref string __result)
    {
        if (injuryType != LeadfootInjuryType)
        {
            return true;
        }

        __result = CustomLocalization.GetText(NameTerm, DisplayName);
        return false;
    }

    private static bool GetInjuryDescTermPrefix(InjuryType injuryType, int level, ref string __result)
    {
        if (injuryType != LeadfootInjuryType)
        {
            return true;
        }

        __result = DescriptionTermPrefix + ClampSeverity(level);
        return false;
    }

    private static void GeneratePotionsPostfix(List<Potion> __result)
    {
        if (!_forceNextOffer || !_registrationReady || __result is null || __result.Count == 0)
        {
            return;
        }

        for (var index = 0; index < __result.Count; index++)
        {
            if (__result[index] is not null && __result[index].injury == LeadfootInjuryType)
            {
                return;
            }
        }

        for (var index = 0; index < __result.Count; index++)
        {
            var original = __result[index];
            if (original is null || original.IsWater)
            {
                continue;
            }

            // Preserve the generator's healing amount, severity tier, and visual model.
            // Only the affliction identity and displayed name are replaced.
            __result[index] = new Potion
            {
                nameTerm = NameTerm,
                _healAmount = original._healAmount,
                injury = LeadfootInjuryType,
                potionSize = ClampSeverity(original.potionSize),
                shared = false,
                modelIdx = original.modelIdx
            };

            if (!_forceLogWritten)
            {
                _forceLogWritten = true;
                _logger?.LogInfo("[ChainedBeasts.ModFoundation] developer offer override inserted Leadfoot into a normal injury-potion slot");
            }

            return;
        }
    }

    private static void FinalMovementSpeedPostfix(Player __instance, ref float __result)
    {
        if (__instance is null)
        {
            return;
        }

        // Player.HasInjury is deliberately used instead of raw PlayerData state: the
        // native method applies ordinary suppression and skull-reduction equipment.
        var effectiveSeverity = ClampSeverityAllowZero(__instance.HasInjury(LeadfootInjuryType));
        if (effectiveSeverity > 0)
        {
            __result *= 1f - (0.10f * effectiveSeverity);
        }
    }

    private static void CreateCheckpointPostfix(PlayerData __instance, PlayerData.Checkpoint __result)
    {
        if (__instance is null || __result is null)
        {
            return;
        }

        var level = ClampSeverityAllowZero(__instance.HasInjuryInData(LeadfootInjuryType));
        if (level == 0)
        {
            return;
        }

        try
        {
            if (!CanUseCheckpointMarker(__result))
            {
                _logger?.LogError("[ChainedBeasts.ModFoundation] Leadfoot checkpoint marker slot is unavailable");
                return;
            }

            __result.injuryLevels[_checkpointMarkerIndex] = level;
            __result.injuryOrder[_checkpointMarkerIndex] =
                __instance.injuryOrder.ContainsKey(LeadfootInjuryType)
                    ? __instance.injuryOrder[LeadfootInjuryType]
                    : __instance.sessionInjuryCount;
        }
        catch (Exception exception)
        {
            _logger?.LogError(
                $"[ChainedBeasts.ModFoundation] Leadfoot checkpoint write failed: {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static void CheckpointValidatePrefix(PlayerData.Checkpoint __instance, out CheckpointMarkerState __state)
    {
        __state = HideCheckpointMarker(__instance);
    }

    private static void PlayerDataRestoreCheckpointPrefix(PlayerData.Checkpoint checkpoint, out CheckpointMarkerState __state)
    {
        __state = HideCheckpointMarker(checkpoint);
    }

    private static void PlayerDataOnCheckpointSyncedPrefix(PlayerData.Checkpoint checkpoint, out CheckpointMarkerState __state)
    {
        __state = HideCheckpointMarker(checkpoint);
    }

    private static void CheckpointMarkerPostfix(CheckpointMarkerState __state)
    {
        RestoreCheckpointMarker(__state);
    }

    private static void PlayerDataRestoreCheckpointPostfix(PlayerData __instance, CheckpointMarkerState __state)
    {
        RestoreCheckpointMarker(__state);
        RestoreLeadfoot(__instance, __state);
    }

    private static void PlayerDataOnCheckpointSyncedPostfix(PlayerData __instance, CheckpointMarkerState __state)
    {
        RestoreCheckpointMarker(__state);
        RestoreLeadfoot(__instance, __state);
    }

    private static Exception CheckpointMarkerFinalizer(Exception __exception, CheckpointMarkerState __state)
    {
        RestoreCheckpointMarker(__state);
        return __exception;
    }

    private static CheckpointMarkerState HideCheckpointMarker(PlayerData.Checkpoint checkpoint)
    {
        var state = new CheckpointMarkerState { Checkpoint = checkpoint };
        if (!CanUseCheckpointMarker(checkpoint))
        {
            return state;
        }

        state.Level = ClampSeverityAllowZero(checkpoint.injuryLevels[_checkpointMarkerIndex]);
        state.Order = checkpoint.injuryOrder[_checkpointMarkerIndex];
        state.HasMarker = state.Level > 0;
        if (state.HasMarker)
        {
            checkpoint.injuryLevels[_checkpointMarkerIndex] = 0;
            checkpoint.injuryOrder[_checkpointMarkerIndex] = 0;
        }

        return state;
    }

    private static void RestoreCheckpointMarker(CheckpointMarkerState state)
    {
        if (state is null || !state.HasMarker || !CanUseCheckpointMarker(state.Checkpoint))
        {
            return;
        }

        state.Checkpoint.injuryLevels[_checkpointMarkerIndex] = state.Level;
        state.Checkpoint.injuryOrder[_checkpointMarkerIndex] = state.Order;
    }

    private static void RestoreLeadfoot(PlayerData playerData, CheckpointMarkerState state)
    {
        if (playerData is null || state is null || !state.HasMarker)
        {
            return;
        }

        try
        {
            if (playerData.injuries.ContainsKey(LeadfootInjuryType))
            {
                playerData.injuries[LeadfootInjuryType] = state.Level;
            }
            else
            {
                playerData.injuries.Add(LeadfootInjuryType, state.Level);
            }

            if (playerData.injuryOrder.ContainsKey(LeadfootInjuryType))
            {
                playerData.injuryOrder[LeadfootInjuryType] = state.Order;
            }
            else
            {
                playerData.injuryOrder.Add(LeadfootInjuryType, state.Order);
            }
        }
        catch (Exception exception)
        {
            _logger?.LogError(
                $"[ChainedBeasts.ModFoundation] Leadfoot checkpoint restore failed: {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static bool CanUseCheckpointMarker(PlayerData.Checkpoint checkpoint)
    {
        return checkpoint is not null &&
               _checkpointMarkerIndex >= 0 &&
               checkpoint.injuryLevels is not null &&
               checkpoint.injuryOrder is not null &&
               checkpoint.injuryLevels.Length > _checkpointMarkerIndex &&
               checkpoint.injuryOrder.Length > _checkpointMarkerIndex;
    }

    private static int FindNativeInjuryIndex(InjuryType target)
    {
        var values = (InjuryType[])Enum.GetValues(typeof(InjuryType));
        for (var index = 0; index < values.Length; index++)
        {
            if (values[index] == target)
            {
                return index;
            }
        }

        throw new InvalidOperationException($"Native InjuryType {target} was not found in Enum.GetValues.");
    }

    private static int ClampSeverity(int value)
    {
        return Math.Max(1, Math.Min(3, value));
    }

    private static int ClampSeverityAllowZero(int value)
    {
        return Math.Max(0, Math.Min(3, value));
    }
}
