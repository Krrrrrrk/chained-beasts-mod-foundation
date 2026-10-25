using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using I2.Loc;
using UnityEngine;

namespace ChainedBeasts.ModFoundation;

internal static class BulkUpFeature
{
    internal const int BulkUpSkillId = CustomContentIds.BulkUpSkill;

    internal const string BulkUpName = "Bulk Up";
    internal const string BulkUpDescription = "Doubles your maximum and current health.";
    internal const string BulkUpNameTerm = "ModFoundation/BulkUp/Name";
    internal const string BulkUpDescriptionTerm = "ModFoundation/BulkUp/Description";

    private static readonly SkillType BulkUpSkillType = (SkillType)BulkUpSkillId;
    private static readonly object StateLock = new();
    private static readonly Dictionary<IntPtr, int> ProcessedBulkUpLevels = new();
    private static readonly HashSet<int> ForcedOfferLogLayers = new();

    private static ManualLogSource _logger;
    private static bool _forceFirstOffer;
    private static bool _registrationReady;
    private static bool _definitionAdded;
    private static int _noneCheckpointIndex = -1;

    private sealed class AcquisitionState
    {
        public PlayerData PlayerData;
        public Player Player;
        public int OwnedLevelBefore;
        public int DataHealthBefore;
        public int RuntimeHealthBefore;
        public bool HasRuntimeHealth;
    }

    private sealed class CheckpointMarkerState
    {
        public PlayerData.Checkpoint Checkpoint;
        public bool HasMarker;
        public int Level;
        public int Order;
    }

    internal static void Install(Harmony harmony, ManualLogSource logger, bool forceFirstOffer)
    {
        if (harmony is null)
        {
            throw new ArgumentNullException(nameof(harmony));
        }

        if (Enum.IsDefined(typeof(SkillType), BulkUpSkillId))
        {
            throw new InvalidOperationException(
                $"SkillType value {BulkUpSkillId} is now defined by the game; Bulk Up registration stopped to avoid a collision.");
        }

        _logger = logger;
        _forceFirstOffer = forceFirstOffer;
        _registrationReady = false;
        _definitionAdded = false;
        _noneCheckpointIndex = FindNativeSkillIndex(SkillType.None);
        ClearRuntimeState();

        var gameLibraryInit = RequireMethod(typeof(GameLibrary), nameof(GameLibrary.Init));
        var getAllSkillTypes = RequireMethod(typeof(GameLibrary), nameof(GameLibrary.GetAllSkillTypes));
        var getSkillName = RequireMethod(typeof(StringManager), nameof(StringManager.GetSkillName), typeof(SkillType));
        var getSkillNameTerm = RequireMethod(typeof(StringManager), nameof(StringManager.GetSkillNameTerm), typeof(SkillType));
        var getSkillDescTerm = RequireMethod(typeof(StringManager), nameof(StringManager.GetSkillDescTerm), typeof(SkillType));
        var getTranslation = RequireMethod(
            typeof(LocalizationManager),
            nameof(LocalizationManager.GetTranslation),
            typeof(string),
            typeof(bool),
            typeof(int),
            typeof(bool),
            typeof(bool),
            typeof(GameObject),
            typeof(string),
            typeof(bool));
        var playerDataMaxHealth = AccessTools.PropertyGetter(typeof(PlayerData), nameof(PlayerData.MaxHealth))
            ?? throw new MissingMethodException(typeof(PlayerData).FullName, "get_MaxHealth");
        var playerMaxAttainableHealth = AccessTools.PropertyGetter(typeof(Player), nameof(Player.MaxAttainableHealth))
            ?? throw new MissingMethodException(typeof(Player).FullName, "get_MaxAttainableHealth");
        var addPlayerSkill = RequireMethod(
            typeof(RunDataManager),
            nameof(RunDataManager.AddPlayerSkillRPC),
            typeof(int),
            typeof(SkillType));
        var getTrainingSkills = RequireMethod(
            typeof(RunDataManager),
            nameof(RunDataManager.GetTrainingSkills),
            typeof(int));
        var createCheckpoint = RequireMethod(typeof(PlayerData), nameof(PlayerData.CreateCheckpoint));
        var restoreCheckpoint = RequireMethod(
            typeof(PlayerData),
            nameof(PlayerData.RestoreCheckpoint),
            typeof(PlayerData.Checkpoint));
        var onCheckpointSynced = RequireMethod(
            typeof(PlayerData),
            nameof(PlayerData.OnCheckpointSynced),
            typeof(PlayerData.Checkpoint),
            typeof(bool));
        var validateCheckpoint = RequireMethod(typeof(PlayerData.Checkpoint), nameof(PlayerData.Checkpoint.Validate));
        var startNewRun = RequireMethod(
            typeof(RunDataManager),
            nameof(RunDataManager.StartNewRunRpc),
            typeof(int),
            typeof(int),
            typeof(AscensionLevel),
            typeof(DifficultyConfig),
            typeof(int));
        var resetRun = RequireMethod(typeof(RunDataManager), nameof(RunDataManager.Reset));

        Patch(harmony, gameLibraryInit, prefix: nameof(GameLibraryInitPrefix), postfix: nameof(GameLibraryInitPostfix));
        Patch(harmony, getAllSkillTypes, postfix: nameof(GetAllSkillTypesPostfix));
        Patch(harmony, getSkillName, prefix: nameof(GetSkillNamePrefix));
        Patch(harmony, getSkillNameTerm, prefix: nameof(GetSkillNameTermPrefix));
        Patch(harmony, getSkillDescTerm, prefix: nameof(GetSkillDescTermPrefix));
        Patch(harmony, getTranslation, prefix: nameof(GetTranslationPrefix));
        Patch(harmony, playerDataMaxHealth, postfix: nameof(PlayerDataMaxHealthPostfix));
        Patch(harmony, playerMaxAttainableHealth, prefix: nameof(PlayerMaxAttainableHealthPrefix));
        Patch(
            harmony,
            addPlayerSkill,
            prefix: nameof(AddPlayerSkillPrefix),
            postfix: nameof(AddPlayerSkillPostfix));
        Patch(harmony, getTrainingSkills, postfix: nameof(GetTrainingSkillsPostfix));
        Patch(harmony, createCheckpoint, postfix: nameof(CreateCheckpointPostfix));
        Patch(
            harmony,
            validateCheckpoint,
            prefix: nameof(CheckpointValidatePrefix),
            postfix: nameof(CheckpointMarkerPostfix),
            finalizer: nameof(CheckpointMarkerFinalizer));
        Patch(
            harmony,
            restoreCheckpoint,
            prefix: nameof(PlayerDataRestoreCheckpointPrefix),
            postfix: nameof(PlayerDataRestoreCheckpointPostfix),
            finalizer: nameof(CheckpointMarkerFinalizer));
        Patch(
            harmony,
            onCheckpointSynced,
            prefix: nameof(PlayerDataOnCheckpointSyncedPrefix),
            postfix: nameof(PlayerDataOnCheckpointSyncedPostfix),
            finalizer: nameof(CheckpointMarkerFinalizer));
        Patch(harmony, startNewRun, prefix: nameof(ClearRuntimeStatePrefix));
        Patch(harmony, resetRun, prefix: nameof(ClearRuntimeStatePrefix));

        _logger?.LogInfo(
            $"[ChainedBeasts.ModFoundation] Bulk Up hooks installed: SkillType={BulkUpSkillId}, " +
            $"maxRank=1, forceFirstOffer={_forceFirstOffer}, checkpointMarkerIndex={_noneCheckpointIndex}");
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameters)
    {
        return AccessTools.DeclaredMethod(type, name, parameters)
            ?? throw new MissingMethodException(type.FullName, name);
    }

    private static void Patch(
        Harmony harmony,
        MethodBase target,
        string prefix = null,
        string postfix = null,
        string finalizer = null)
    {
        if (target is null)
        {
            throw new MissingMethodException("A required Bulk Up patch target was not found.");
        }

        harmony.Patch(
            target,
            prefix: PatchMethod(prefix),
            postfix: PatchMethod(postfix),
            finalizer: PatchMethod(finalizer));
    }

    private static HarmonyMethod PatchMethod(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var method = AccessTools.DeclaredMethod(typeof(BulkUpFeature), name);
        if (method is null)
        {
            throw new MissingMethodException(typeof(BulkUpFeature).FullName, name);
        }

        return new HarmonyMethod(method);
    }

    private static void GameLibraryInitPrefix(GameLibrary __instance)
    {
        var skills = __instance.allSkills
            ?? throw new InvalidOperationException("GameLibrary.allSkills is null before GameLibrary.Init.");

        Sprite icon = null;
        for (var index = 0; index < skills.Count; index++)
        {
            var skill = skills[index];
            if (skill is null)
            {
                continue;
            }

            if ((int)skill.skillType == BulkUpSkillId)
            {
                if (_definitionAdded && string.Equals(skill.name, "BulkUp", StringComparison.Ordinal))
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"GameLibrary already contains SkillType {BulkUpSkillId}; Bulk Up registration stopped to avoid a collision.");
            }

            if (skill.skillType == SkillType.MetalPlate)
            {
                icon = skill.sprite;
            }
        }

        if (icon is null)
        {
            for (var index = 0; index < skills.Count; index++)
            {
                if (skills[index] is not null && skills[index].sprite is not null)
                {
                    icon = skills[index].sprite;
                    break;
                }
            }
        }

        var bulkUp = new SkillDef
        {
            name = "BulkUp",
            skillType = BulkUpSkillType,
            releaseStatus = ReleaseStatus.InFullGame,
            offerModifiers = (OfferModifiers)0,
            offerType = OfferType.Normal,
            maxLevel = 1,
            requiresCategory = (SkillCategory)0,
            unlocksCategory = (SkillCategory)0,
            tooltipCategories = StatusTooltipType.None,
            sprite = icon
        };

        skills.Add(bulkUp);
        _definitionAdded = true;
    }

    private static void GameLibraryInitPostfix(GameLibrary __instance)
    {
        try
        {
            _registrationReady = __instance.SkillDict is not null && __instance.SkillDict.ContainsKey(BulkUpSkillType);
            if (_registrationReady)
            {
                _logger?.LogInfo(
                    $"[ChainedBeasts.ModFoundation] Bulk Up registered in GameLibrary.AllSkills and SkillDict as {BulkUpSkillId}");
            }
            else
            {
                _logger?.LogError(
                    "[ChainedBeasts.ModFoundation] Bulk Up definition was added before GameLibrary.Init, " +
                    "but the initialized SkillDict does not contain it");
            }
        }
        catch (Exception exception)
        {
            _registrationReady = false;
            _logger?.LogError(
                $"[ChainedBeasts.ModFoundation] Bulk Up registration verification failed: " +
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static void GetAllSkillTypesPostfix(Il2CppSystem.Collections.Generic.List<SkillType> __result)
    {
        if (__result is not null && !__result.Contains(BulkUpSkillType))
        {
            __result.Add(BulkUpSkillType);
        }
    }

    private static bool GetSkillNamePrefix(SkillType skillType, ref string __result)
    {
        if (skillType != BulkUpSkillType)
        {
            return true;
        }

        __result = BulkUpName;
        return false;
    }

    private static bool GetSkillNameTermPrefix(SkillType skillType, ref string __result)
    {
        if (skillType != BulkUpSkillType)
        {
            return true;
        }

        __result = BulkUpNameTerm;
        return false;
    }

    private static bool GetSkillDescTermPrefix(SkillType skillType, ref string __result)
    {
        if (skillType != BulkUpSkillType)
        {
            return true;
        }

        __result = BulkUpDescriptionTerm;
        return false;
    }

    private static bool GetTranslationPrefix(string Term, ref string __result)
    {
        if (Term is not null && Term.StartsWith(BulkUpNameTerm, StringComparison.Ordinal))
        {
            __result = BulkUpName;
            return false;
        }

        if (Term is not null && Term.StartsWith(BulkUpDescriptionTerm, StringComparison.Ordinal))
        {
            __result = BulkUpDescription;
            return false;
        }

        return true;
    }

    private static void PlayerDataMaxHealthPostfix(PlayerData __instance, ref int __result)
    {
        try
        {
            if (HasBulkUp(__instance))
            {
                __result = SaturatingDouble(__result);
            }
        }
        catch (Exception exception)
        {
            _logger?.LogError(
                $"[ChainedBeasts.ModFoundation] Bulk Up max-health calculation failed: " +
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static bool PlayerMaxAttainableHealthPrefix(Player __instance, ref int __result)
    {
        try
        {
            var playerData = __instance.PlayerData;
            if (playerData is null || !HasBulkUp(playerData))
            {
                return true;
            }

            __result = playerData.MaxAttainableHealth;
            return false;
        }
        catch
        {
            return true;
        }
    }

    private static void AddPlayerSkillPrefix(
        RunDataManager __instance,
        int playerIndex,
        SkillType skillType,
        out AcquisitionState __state)
    {
        __state = null;
        if (skillType != BulkUpSkillType)
        {
            return;
        }

        try
        {
            var playerData = __instance.GetPlayerDataByIndex(playerIndex);
            if (playerData is null)
            {
                return;
            }

            var state = new AcquisitionState
            {
                PlayerData = playerData,
                OwnedLevelBefore = playerData.HasSkill(BulkUpSkillType),
                DataHealthBefore = playerData.hp
            };

            try
            {
                state.Player = playerData.PlayerObject;
                if (state.Player is not null && state.Player.health is not null)
                {
                    state.RuntimeHealthBefore = state.Player.health.Value;
                    state.HasRuntimeHealth = true;
                }
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(
                    $"[ChainedBeasts.ModFoundation] Bulk Up runtime health snapshot unavailable; " +
                    $"PlayerData health will still be updated: {exception.GetType().Name}: {exception.Message}");
            }

            lock (StateLock)
            {
                if (!ProcessedBulkUpLevels.ContainsKey(playerData.Pointer))
                {
                    ProcessedBulkUpLevels.Add(playerData.Pointer, state.OwnedLevelBefore);
                }
            }

            __state = state;
        }
        catch (Exception exception)
        {
            _logger?.LogError(
                $"[ChainedBeasts.ModFoundation] failed to prepare Bulk Up acquisition: " +
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static void AddPlayerSkillPostfix(AcquisitionState __state)
    {
        if (__state is null)
        {
            return;
        }

        try
        {
            var ownedLevelAfter = __state.PlayerData.HasSkill(BulkUpSkillType);
            var apply = false;

            lock (StateLock)
            {
                ProcessedBulkUpLevels.TryGetValue(__state.PlayerData.Pointer, out var processedLevel);
                if (ownedLevelAfter > __state.OwnedLevelBefore && ownedLevelAfter > processedLevel)
                {
                    ProcessedBulkUpLevels[__state.PlayerData.Pointer] = ownedLevelAfter;
                    apply = true;
                }
            }

            if (!apply)
            {
                return;
            }

            var doubledDataHealth = SaturatingDouble(__state.DataHealthBefore);
            __state.PlayerData.hp = doubledDataHealth;

            var runtimeHealthText = "unavailable";
            if (__state.HasRuntimeHealth && __state.Player is not null && __state.Player.health is not null)
            {
                if (__state.Player.IsServer)
                {
                    var doubledRuntimeHealth = SaturatingDouble(__state.RuntimeHealthBefore);
                    __state.Player.health.Value = doubledRuntimeHealth;
                    runtimeHealthText = doubledRuntimeHealth.ToString();
                }
                else
                {
                    runtimeHealthText = "skipped-non-server";
                }
            }

            _logger?.LogInfo(
                $"[ChainedBeasts.ModFoundation] Bulk Up acquired: dataHealth={doubledDataHealth}, " +
                $"runtimeHealth={runtimeHealthText}, maxHealth={__state.PlayerData.MaxHealth}");
        }
        catch (Exception exception)
        {
            _logger?.LogError(
                $"[ChainedBeasts.ModFoundation] Bulk Up acquisition effect failed: " +
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static void GetTrainingSkillsPostfix(
        RunDataManager __instance,
        int layer,
        Il2CppSystem.Collections.Generic.List<SkillType> __result)
    {
        if (!_forceFirstOffer || !_registrationReady || __result is null || __result.Count == 0)
        {
            return;
        }

        try
        {
            var playerDatas = __instance.playerDatas;
            if (playerDatas is not null)
            {
                for (var playerIndex = 0; playerIndex < playerDatas.Count; playerIndex++)
                {
                    if (HasBulkUp(playerDatas[playerIndex]))
                    {
                        return;
                    }
                }
            }

            if (!__result.Contains(BulkUpSkillType))
            {
                __result[0] = BulkUpSkillType;
            }

            lock (StateLock)
            {
                if (ForcedOfferLogLayers.Add(layer))
                {
                    _logger?.LogInfo(
                        $"[ChainedBeasts.ModFoundation] Bulk Up forced into retrieved training offer for layer {layer}; " +
                        "the override remains active until the skill is owned");
                }
            }
        }
        catch (Exception exception)
        {
            _logger?.LogError(
                $"[ChainedBeasts.ModFoundation] ForceBulkUpFirstOffer failed: " +
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static void CreateCheckpointPostfix(PlayerData __instance, PlayerData.Checkpoint __result)
    {
        if (__result is null || !HasBulkUp(__instance))
        {
            return;
        }

        try
        {
            if (!CanUseCheckpointMarker(__result))
            {
                _logger?.LogError("[ChainedBeasts.ModFoundation] Bulk Up checkpoint marker slot is unavailable");
                return;
            }

            __result.skillLevels[_noneCheckpointIndex] = 1;
            __result.skillOrder[_noneCheckpointIndex] =
                __instance.skillOrder.ContainsKey(BulkUpSkillType)
                    ? __instance.skillOrder[BulkUpSkillType]
                    : __instance.sessionSkillCount;
        }
        catch (Exception exception)
        {
            _logger?.LogError(
                $"[ChainedBeasts.ModFoundation] Bulk Up checkpoint write failed: " +
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static void CheckpointValidatePrefix(
        PlayerData.Checkpoint __instance,
        out CheckpointMarkerState __state)
    {
        __state = HideCheckpointMarker(__instance);
    }

    private static void PlayerDataRestoreCheckpointPrefix(
        PlayerData.Checkpoint checkpoint,
        out CheckpointMarkerState __state)
    {
        __state = HideCheckpointMarker(checkpoint);
    }

    private static void PlayerDataOnCheckpointSyncedPrefix(
        PlayerData.Checkpoint checkpoint,
        out CheckpointMarkerState __state)
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
        RestoreBulkUpOwnership(__instance, __state);
    }

    private static void PlayerDataOnCheckpointSyncedPostfix(PlayerData __instance, CheckpointMarkerState __state)
    {
        RestoreCheckpointMarker(__state);
        RestoreBulkUpOwnership(__instance, __state);
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

        state.Level = checkpoint.skillLevels[_noneCheckpointIndex];
        state.Order = checkpoint.skillOrder[_noneCheckpointIndex];
        state.HasMarker = state.Level > 0;

        if (state.HasMarker)
        {
            checkpoint.skillLevels[_noneCheckpointIndex] = 0;
            checkpoint.skillOrder[_noneCheckpointIndex] = 0;
        }

        return state;
    }

    private static void RestoreCheckpointMarker(CheckpointMarkerState state)
    {
        if (state is null || !state.HasMarker || !CanUseCheckpointMarker(state.Checkpoint))
        {
            return;
        }

        state.Checkpoint.skillLevels[_noneCheckpointIndex] = state.Level;
        state.Checkpoint.skillOrder[_noneCheckpointIndex] = state.Order;
    }

    private static void RestoreBulkUpOwnership(PlayerData playerData, CheckpointMarkerState state)
    {
        if (state is null || !state.HasMarker || playerData is null)
        {
            return;
        }

        try
        {
            if (playerData.skills.ContainsKey(BulkUpSkillType))
            {
                playerData.skills[BulkUpSkillType] = 1;
            }
            else
            {
                playerData.skills.Add(BulkUpSkillType, 1);
            }

            if (playerData.skillOrder.ContainsKey(BulkUpSkillType))
            {
                playerData.skillOrder[BulkUpSkillType] = state.Order;
            }
            else
            {
                playerData.skillOrder.Add(BulkUpSkillType, state.Order);
            }

            lock (StateLock)
            {
                ProcessedBulkUpLevels[playerData.Pointer] = 1;
            }
        }
        catch (Exception exception)
        {
            _logger?.LogError(
                $"[ChainedBeasts.ModFoundation] Bulk Up checkpoint restore failed: " +
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static bool CanUseCheckpointMarker(PlayerData.Checkpoint checkpoint)
    {
        return checkpoint is not null &&
               _noneCheckpointIndex >= 0 &&
               checkpoint.skillLevels is not null &&
               checkpoint.skillOrder is not null &&
               checkpoint.skillLevels.Length > _noneCheckpointIndex &&
               checkpoint.skillOrder.Length > _noneCheckpointIndex;
    }

    private static int FindNativeSkillIndex(SkillType target)
    {
        var values = (SkillType[])Enum.GetValues(typeof(SkillType));
        for (var index = 0; index < values.Length; index++)
        {
            if (values[index] == target)
            {
                return index;
            }
        }

        throw new InvalidOperationException($"Native SkillType {target} was not found in Enum.GetValues.");
    }

    private static bool HasBulkUp(PlayerData playerData)
    {
        return playerData is not null && playerData.HasSkill(BulkUpSkillType) > 0;
    }

    internal static void SynchronizeProcessedLevel(PlayerData playerData)
    {
        if (playerData is null)
        {
            return;
        }

        lock (StateLock)
        {
            ProcessedBulkUpLevels[playerData.Pointer] = playerData.HasSkill(BulkUpSkillType);
        }
    }

    private static void ClearRuntimeStatePrefix()
    {
        ClearRuntimeState();
    }

    private static void ClearRuntimeState()
    {
        lock (StateLock)
        {
            ProcessedBulkUpLevels.Clear();
            ForcedOfferLogLayers.Clear();
        }
    }

    private static int SaturatingDouble(int value)
    {
        if (value > int.MaxValue / 2)
        {
            return int.MaxValue;
        }

        if (value < int.MinValue / 2)
        {
            return int.MinValue;
        }

        return value * 2;
    }
}
