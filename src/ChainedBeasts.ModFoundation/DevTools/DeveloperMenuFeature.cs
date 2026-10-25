using System;
using System.Collections.Generic;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChainedBeasts.ModFoundation.DevTools;

/// <summary>
/// Solo testing helpers. God mode and infinite stamina deliberately feed the
/// game's existing DebugOptions checks instead of replacing damage or stamina
/// methods. The panel uses normal host-side run APIs for mutable run data.
/// </summary>
internal static class DeveloperMenuFeature
{
    internal static bool GodModeEnabled;
    internal static bool InfiniteStaminaEnabled;
    internal static ManualLogSource Logger;
    internal static Key MenuKey = Key.F1;

    internal static void Install(Harmony harmony, ManualLogSource logger, Key menuKey)
    {
        Logger = logger;
        MenuKey = menuKey == Key.None ? Key.F1 : menuKey;
        GodModeEnabled = false;
        InfiniteStaminaEnabled = false;

        FeaturePatcher.Patch(
            harmony,
            typeof(DeveloperMenuFeature),
            FeaturePatcher.RequirePropertyGetter(typeof(DebugOptions), nameof(DebugOptions.GodMode)),
            postfix: nameof(GodModePostfix));
        FeaturePatcher.Patch(
            harmony,
            typeof(DeveloperMenuFeature),
            FeaturePatcher.RequirePropertyGetter(typeof(DebugOptions), nameof(DebugOptions.IgnoreStamina)),
            postfix: nameof(IgnoreStaminaPostfix));

        logger?.LogInfo(
            "[ChainedBeasts.ModFoundation] developer menu native overrides installed: " +
            "DebugOptions.GodMode and DebugOptions.IgnoreStamina");
        logger?.LogInfo(
            $"[ChainedBeasts.ModFoundation] developer menu ready; press {MenuKey} to toggle it");
    }

    private static void GodModePostfix(ref bool __result)
    {
        if (GodModeEnabled)
        {
            __result = true;
        }
    }

    private static void IgnoreStaminaPostfix(ref bool __result)
    {
        if (InfiniteStaminaEnabled)
        {
            __result = true;
        }
    }
}

public sealed class DeveloperMenuController : MonoBehaviour
{
    private readonly List<SkillType> _skills = new();
    private readonly List<InjuryType> _injuries = new();

    private bool _menuOpen;
    private bool _catalogReady;
    private int _skillIndex;
    private int _injuryIndex;
    private int _rerollSequence;
    private string _status = "Open a solo run or lobby to edit run data.";
    private bool _cursorVisibleBeforeOpen;
    private CursorLockMode _cursorLockBeforeOpen;
    private bool _drawFailureLogged;

    public DeveloperMenuController(IntPtr pointer)
        : base(pointer)
    {
    }

    public void Update()
    {
        try
        {
            var keyboard = Keyboard.current;
            var menuKey = DeveloperMenuFeature.MenuKey;
            if (keyboard is not null && keyboard[menuKey] is not null && keyboard[menuKey].wasPressedThisFrame)
            {
                SetMenuOpen(!_menuOpen);
            }
        }
        catch (Exception exception)
        {
            DeveloperMenuFeature.Logger?.LogError(
                $"[ChainedBeasts.ModFoundation] developer menu key handling failed: " +
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    public void OnGUI()
    {
        if (!_menuOpen)
        {
            return;
        }

        var width = Math.Min(520f, Math.Max(360f, Screen.width - 40f));
        var height = Math.Min(760f, Math.Max(360f, Screen.height - 40f));
        var area = new Rect(20f, 20f, width, height);

        GUI.depth = -1000;
        GUILayout.BeginArea(area, $"Chained Beasts Dev Menu ({DeveloperMenuFeature.MenuKey})", GUI.skin.window);
        try
        {
            GUILayout.Space(22f);
            // BeginScrollView is stripped from this game's IL2CPP player. The
            // basic GUILayout controls below are retained by a shipped demo.
            DrawPanel();
            _drawFailureLogged = false;
        }
        catch (Exception exception)
        {
            _status = $"UI error: {exception.GetType().Name}: {exception.Message}";
            if (!_drawFailureLogged)
            {
                _drawFailureLogged = true;
                DeveloperMenuFeature.Logger?.LogError(
                    $"[ChainedBeasts.ModFoundation] developer menu draw failed: {_status}");
            }
        }
        finally
        {
            GUILayout.EndArea();
        }
    }

    public void OnDestroy()
    {
        if (_menuOpen)
        {
            RestoreCursor();
        }
    }

    private void DrawPanel()
    {
        GUILayout.Label("Solo testing only. Changes use the current run's host-owned data.");
        GUILayout.Space(6f);

        var godMode = GUILayout.Toggle(DeveloperMenuFeature.GodModeEnabled, "God mode");
        if (godMode != DeveloperMenuFeature.GodModeEnabled)
        {
            DeveloperMenuFeature.GodModeEnabled = godMode;
            SetStatus($"God mode {(godMode ? "enabled" : "disabled")}");
        }

        var infiniteStamina = GUILayout.Toggle(
            DeveloperMenuFeature.InfiniteStaminaEnabled,
            "Infinite stamina");
        if (infiniteStamina != DeveloperMenuFeature.InfiniteStaminaEnabled)
        {
            DeveloperMenuFeature.InfiniteStaminaEnabled = infiniteStamina;
            SetStatus($"Infinite stamina {(infiniteStamina ? "enabled" : "disabled")}");
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Heal to full"))
        {
            HealToFull();
        }

        if (GUILayout.Button("Refresh catalogs"))
        {
            _catalogReady = false;
            RefreshCatalogs();
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Reroll current offers"))
        {
            RerollCurrentOffers();
        }

        GUILayout.Space(10f);
        DrawMoneyControls();

        GUILayout.Space(10f);
        RefreshCatalogs();
        DrawSkillControls();

        GUILayout.Space(10f);
        DrawInjuryControls();

        GUILayout.Space(12f);
        GUILayout.Label($"Status: {_status}");
        GUILayout.Space(4f);
        if (GUILayout.Button($"Close ({DeveloperMenuFeature.MenuKey})"))
        {
            SetMenuOpen(false);
        }
    }

    private void DrawMoneyControls()
    {
        var moneyText = "unavailable";
        if (TryGetRunContext(out var manager, out _, out _))
        {
            moneyText = manager.Money.ToString();
        }

        GUILayout.Label($"Gold: {moneyText}");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("-100"))
        {
            ChangeMoney(-100);
        }
        if (GUILayout.Button("-10"))
        {
            ChangeMoney(-10);
        }
        if (GUILayout.Button("+10"))
        {
            ChangeMoney(10);
        }
        if (GUILayout.Button("+100"))
        {
            ChangeMoney(100);
        }
        if (GUILayout.Button("Set 0"))
        {
            SetMoneyToZero();
        }
        GUILayout.EndHorizontal();
    }

    private void DrawSkillControls()
    {
        GUILayout.Label("Perks / skills");
        if (_skills.Count == 0)
        {
            GUILayout.Label("Skill catalog is not available yet.");
            return;
        }

        _skillIndex = ClampIndex(_skillIndex, _skills.Count);
        var selected = _skills[_skillIndex];
        var owned = 0;
        if (TryGetRunContext(out _, out var playerData, out _))
        {
            owned = playerData.HasSkill(selected);
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<"))
        {
            _skillIndex = WrapIndex(_skillIndex - 1, _skills.Count);
        }
        GUILayout.Label($"{GetSkillName(selected)}  [{(int)selected}]  owned: {owned}");
        if (GUILayout.Button(">"))
        {
            _skillIndex = WrapIndex(_skillIndex + 1, _skills.Count);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Add one rank"))
        {
            AddSkill(selected);
        }
        if (GUILayout.Button("Remove one rank"))
        {
            RemoveSkill(selected);
        }
        GUILayout.EndHorizontal();
    }

    private void DrawInjuryControls()
    {
        GUILayout.Label("Debuffs / injuries");
        if (_injuries.Count == 0)
        {
            GUILayout.Label("Injury catalog is not available yet.");
            return;
        }

        _injuryIndex = ClampIndex(_injuryIndex, _injuries.Count);
        var selected = _injuries[_injuryIndex];
        var severity = 0;
        if (TryGetRunContext(out _, out var playerData, out _))
        {
            severity = playerData.HasInjuryInData(selected);
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<"))
        {
            _injuryIndex = WrapIndex(_injuryIndex - 1, _injuries.Count);
        }
        GUILayout.Label($"{GetInjuryName(selected)}  [{(int)selected}]  severity: {severity}");
        if (GUILayout.Button(">"))
        {
            _injuryIndex = WrapIndex(_injuryIndex + 1, _injuries.Count);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Add one level"))
        {
            AddInjury(selected);
        }
        if (GUILayout.Button("Remove one level"))
        {
            RemoveInjury(selected);
        }
        GUILayout.EndHorizontal();
    }

    private void RefreshCatalogs()
    {
        if (_catalogReady || !GameLibrary.InstanceExists)
        {
            return;
        }

        try
        {
            _skills.Clear();
            var skills = GameLibrary.Instance.GetAllSkillTypes();
            if (skills is not null)
            {
                for (var index = 0; index < skills.Count; index++)
                {
                    var skillType = skills[index];
                    if ((int)skillType < 0 || !GameLibrary.Instance.SkillDict.ContainsKey(skillType))
                    {
                        continue;
                    }

                    var definition = GameLibrary.Instance.SkillDict[skillType];
                    if (definition is not null && definition.MaxLevelOfferable > 0)
                    {
                        _skills.Add(skillType);
                    }
                }
            }

            _injuries.Clear();
            var injuries = GameLibrary.Instance.GetAllInjuryTypes();
            if (injuries is not null)
            {
                for (var index = 0; index < injuries.Count; index++)
                {
                    var injuryType = injuries[index];
                    var enumName = Enum.GetName(typeof(InjuryType), injuryType) ?? string.Empty;
                    if ((int)injuryType < 0 || enumName.StartsWith("EmptySlot", StringComparison.Ordinal) ||
                        !GameLibrary.Instance.InjuryDict.ContainsKey(injuryType))
                    {
                        continue;
                    }

                    var definition = GameLibrary.Instance.InjuryDict[injuryType];
                    if (definition is not null && definition.MaxLevelOfferable > 0)
                    {
                        _injuries.Add(injuryType);
                    }
                }
            }

            _skills.Sort((left, right) => ((int)left).CompareTo((int)right));
            _injuries.Sort((left, right) => ((int)left).CompareTo((int)right));
            _skillIndex = ClampIndex(_skillIndex, _skills.Count);
            _injuryIndex = ClampIndex(_injuryIndex, _injuries.Count);
            _catalogReady = _skills.Count > 0 || _injuries.Count > 0;
        }
        catch (Exception exception)
        {
            SetStatus($"Catalog refresh failed: {exception.GetType().Name}: {exception.Message}", true);
        }
    }

    private void HealToFull()
    {
        if (!TryGetMutableRunContext(out var manager, out var playerData, out var playerIndex))
        {
            return;
        }

        try
        {
            SetCurrentHealth(playerData, playerData.MaxHealth);
            manager.NotifyPlayerDataChanged(playerIndex);
            SetStatus($"Player {playerIndex + 1} healed to {playerData.MaxHealth}");
        }
        catch (Exception exception)
        {
            SetStatus($"Heal failed: {exception.GetType().Name}: {exception.Message}", true);
        }
    }

    private void RerollCurrentOffers()
    {
        if (!TryGetMutableRunContext(out var manager, out _, out _))
        {
            return;
        }

        try
        {
            _rerollSequence++;
            var success = OfferRerollFeature.TryRerollActiveScreen(
                manager,
                _rerollSequence,
                out var message);
            SetStatus(message, !success);
        }
        catch (Exception exception)
        {
            SetStatus($"Offer reroll failed: {exception.GetType().Name}: {exception.Message}", true);
        }
    }

    private void ChangeMoney(int amount)
    {
        if (!TryGetMutableRunContext(out var manager, out _, out _))
        {
            return;
        }

        try
        {
            manager.ModifyMoney(amount);
            SetStatus($"Gold changed by {amount:+#;-#;0}; new balance {manager.Money}");
        }
        catch (Exception exception)
        {
            SetStatus($"Gold change failed: {exception.GetType().Name}: {exception.Message}", true);
        }
    }

    private void SetMoneyToZero()
    {
        if (!TryGetMutableRunContext(out var manager, out _, out _))
        {
            return;
        }

        try
        {
            manager.ModifyMoney(-manager.Money);
            SetStatus("Gold set to 0");
        }
        catch (Exception exception)
        {
            SetStatus($"Gold reset failed: {exception.GetType().Name}: {exception.Message}", true);
        }
    }

    private void AddSkill(SkillType skillType)
    {
        if (!TryGetMutableRunContext(out var manager, out var playerData, out var playerIndex))
        {
            return;
        }

        try
        {
            var definition = GameLibrary.Instance.SkillDict[skillType];
            var owned = playerData.HasSkill(skillType);
            if (definition is not null && owned >= definition.MaxLevelOfferable)
            {
                SetStatus($"{GetSkillName(skillType)} is already at its maximum rank ({owned})");
                return;
            }

            manager.AddPlayerSkillRPC(playerIndex, skillType);
            SetStatus($"Requested one rank of {GetSkillName(skillType)} for player {playerIndex + 1}");
        }
        catch (Exception exception)
        {
            SetStatus($"Add perk failed: {exception.GetType().Name}: {exception.Message}", true);
        }
    }

    private void RemoveSkill(SkillType skillType)
    {
        if (!TryGetMutableRunContext(out var manager, out var playerData, out var playerIndex))
        {
            return;
        }

        try
        {
            var owned = playerData.HasSkill(skillType);
            if (owned <= 0)
            {
                SetStatus($"{GetSkillName(skillType)} is not owned");
                return;
            }

            var oldMaximum = Math.Max(1, playerData.MaxHealth);
            var oldHealth = GetCurrentHealth(playerData);
            playerData.RemoveSkill(skillType, 1);

            if ((int)skillType == CustomContentIds.BulkUpSkill)
            {
                BulkUpFeature.SynchronizeProcessedLevel(playerData);
            }

            var newMaximum = Math.Max(1, playerData.MaxHealth);
            var proportionalHealth = (int)Math.Round(oldHealth * (double)newMaximum / oldMaximum);
            SetCurrentHealth(playerData, Math.Max(0, Math.Min(newMaximum, proportionalHealth)));
            manager.NotifyPlayerDataChanged(playerIndex);
            SetStatus($"Removed one rank of {GetSkillName(skillType)} from player {playerIndex + 1}");
        }
        catch (Exception exception)
        {
            SetStatus($"Remove perk failed: {exception.GetType().Name}: {exception.Message}", true);
        }
    }

    private void AddInjury(InjuryType injuryType)
    {
        if (!TryGetMutableRunContext(out var manager, out var playerData, out var playerIndex))
        {
            return;
        }

        try
        {
            var definition = GameLibrary.Instance.InjuryDict[injuryType];
            var severity = playerData.HasInjuryInData(injuryType);
            if (definition is not null && severity >= definition.MaxLevelOfferable)
            {
                SetStatus($"{GetInjuryName(injuryType)} is already at maximum severity ({severity})");
                return;
            }

            manager.AddPlayerInjuryRPC(playerIndex, injuryType, 1, true);
            SetStatus($"Requested one level of {GetInjuryName(injuryType)} for player {playerIndex + 1}");
        }
        catch (Exception exception)
        {
            SetStatus($"Add debuff failed: {exception.GetType().Name}: {exception.Message}", true);
        }
    }

    private void RemoveInjury(InjuryType injuryType)
    {
        if (!TryGetMutableRunContext(out var manager, out var playerData, out var playerIndex))
        {
            return;
        }

        try
        {
            if (playerData.HasInjuryInData(injuryType) <= 0)
            {
                SetStatus($"{GetInjuryName(injuryType)} is not present");
                return;
            }

            manager.RemovePlayerInjuryRpc(playerIndex, injuryType);
            SetStatus($"Requested removal of one {GetInjuryName(injuryType)} level for player {playerIndex + 1}");
        }
        catch (Exception exception)
        {
            SetStatus($"Remove debuff failed: {exception.GetType().Name}: {exception.Message}", true);
        }
    }

    private bool TryGetRunContext(out RunDataManager manager, out PlayerData playerData, out int playerIndex)
    {
        manager = null;
        playerData = null;
        playerIndex = 0;

        try
        {
            if (!NetworkSession.InstanceExists)
            {
                return false;
            }

            manager = NetworkSession.Instance?.runDataManager;
            if (manager is null)
            {
                return false;
            }

            // This tool is intentionally scoped to the first local player in a
            // solo run. Multiplayer targeting is not claimed by this build.
            playerData = manager.GetPlayerDataByIndex(playerIndex);
            return playerData is not null;
        }
        catch
        {
            manager = null;
            playerData = null;
            playerIndex = 0;
            return false;
        }
    }

    private bool TryGetMutableRunContext(out RunDataManager manager, out PlayerData playerData, out int playerIndex)
    {
        if (!TryGetRunContext(out manager, out playerData, out playerIndex))
        {
            SetStatus("No local run player is available yet");
            return false;
        }

        if (!manager.IsServer)
        {
            SetStatus("Run-data editing is disabled because this client is not the host");
            return false;
        }

        return true;
    }

    private static int GetCurrentHealth(PlayerData playerData)
    {
        try
        {
            var player = playerData.PlayerObject;
            if (player is not null && player.health is not null)
            {
                return player.health.Value;
            }
        }
        catch
        {
            // PlayerData.hp is the authoritative fallback outside an arena.
        }

        return playerData.hp;
    }

    private static void SetCurrentHealth(PlayerData playerData, int health)
    {
        playerData.hp = health;
        try
        {
            var player = playerData.PlayerObject;
            if (player is not null && player.IsServer && player.health is not null)
            {
                player.health.Value = health;
            }
        }
        catch
        {
            // The PlayerData value still covers lobbies and scene transitions.
        }
    }

    private static string GetSkillName(SkillType skillType)
    {
        try
        {
            var name = StringManager.GetSkillName(skillType);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }
        catch
        {
            // Fall back to the stable enum/numeric identity below.
        }

        return Enum.GetName(typeof(SkillType), skillType) ?? $"Skill {(int)skillType}";
    }

    private static string GetInjuryName(InjuryType injuryType)
    {
        try
        {
            var term = StringManager.GetInjuryNameTerm(injuryType);
            var name = StringManager.GetLocalisedTerm(term);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }
        catch
        {
            // Fall back to the stable enum/numeric identity below.
        }

        return Enum.GetName(typeof(InjuryType), injuryType) ?? $"Injury {(int)injuryType}";
    }

    private void SetMenuOpen(bool open)
    {
        if (_menuOpen == open)
        {
            return;
        }

        _menuOpen = open;
        if (open)
        {
            _drawFailureLogged = false;
            _cursorVisibleBeforeOpen = Cursor.visible;
            _cursorLockBeforeOpen = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            RefreshCatalogs();
        }
        else
        {
            RestoreCursor();
        }
    }

    private void RestoreCursor()
    {
        Cursor.visible = _cursorVisibleBeforeOpen;
        Cursor.lockState = _cursorLockBeforeOpen;
    }

    private void SetStatus(string message, bool error = false)
    {
        _status = message;
        if (error)
        {
            DeveloperMenuFeature.Logger?.LogError($"[ChainedBeasts.ModFoundation] developer menu: {message}");
        }
        else
        {
            DeveloperMenuFeature.Logger?.LogInfo($"[ChainedBeasts.ModFoundation] developer menu: {message}");
        }
    }

    private static int ClampIndex(int value, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        return Math.Max(0, Math.Min(count - 1, value));
    }

    private static int WrapIndex(int value, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        value %= count;
        return value < 0 ? value + count : value;
    }
}
