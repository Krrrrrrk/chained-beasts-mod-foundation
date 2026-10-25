using System;
using System.Globalization;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using ChainedBeasts.ModFoundation.Afflictions;
using ChainedBeasts.ModFoundation.DevTools;
using ChainedBeasts.ModFoundation.Equipment;
using ChainedBeasts.ModFoundation.Surgeries;
using HarmonyLib;
using UnityEngine.InputSystem;

namespace ChainedBeasts.ModFoundation;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "local.chainedbeasts.modfoundation";
    public const string PluginName = "ChainedBeasts.ModFoundation";
    public const string PluginVersion = "0.6.1";

    private static ManualLogSource _logger;
    private static int _diagnosticSnapshotWritten;

    public override void Load()
    {
        _logger = Log;
        Log.LogInfo("[ChainedBeasts.ModFoundation] smoke test loaded");

        var enableDiagnostics = BindToggle(
            "Diagnostics", "EnableDiagnostics", false,
            "Install the read-only UIMainMenu lifecycle diagnostic hook on the next game launch.");
        var enableBulkUp = BindToggle(
            "BulkUp", "EnableBulkUp", true,
            "Register the one-rank Bulk Up skill on the next game launch.");
        var forceBulkUp = BindToggle(
            "BulkUp", "ForceBulkUpFirstOffer", false,
            "Developer override: put Bulk Up in the first eligible solo offer until acquired.");
        var enableRunnersWraps = BindToggle(
            "RunnersWraps", "EnableRunnersWraps", true,
            "Register the Runner's Wraps equipment on the next game launch.");
        var forceRunnersWraps = BindToggle(
            "RunnersWraps", "ForceRunnersWrapsNextOffer", false,
            "Developer override: replace an eligible equipment-shop slot with Runner's Wraps.");
        var enableLeadfoot = BindToggle(
            "Leadfoot", "EnableLeadfoot", true,
            "Register the Leadfoot potion affliction on the next game launch.");
        var forceLeadfoot = BindToggle(
            "Leadfoot", "ForceLeadfootNextOffer", false,
            "Developer override: replace an eligible potion slot with Leadfoot.");
        var enableAdrenalGraft = BindToggle(
            "AdrenalGraft", "EnableAdrenalGraft", true,
            "Register the Adrenal Graft surgery on the next game launch.");
        var forceAdrenalGraft = BindToggle(
            "AdrenalGraft", "ForceAdrenalGraftNextOffer", false,
            "Developer override: show the free Adrenal Graft at the next surgeon.");
        var enableDeveloperMenu = BindToggle(
            "DeveloperMenu", "EnableDeveloperMenu", true,
            "Enable the solo testing panel and its native god-mode/stamina overrides on the next launch.");
        var developerMenuKey = Config.Bind(
            "DeveloperMenu", "MenuKey", Key.F1,
            "Keyboard key that opens and closes the solo testing panel.");

        LogSetting(enableDiagnostics);
        LogSetting(enableBulkUp);
        LogSetting(forceBulkUp);
        LogSetting(enableRunnersWraps);
        LogSetting(forceRunnersWraps);
        LogSetting(enableLeadfoot);
        LogSetting(forceLeadfoot);
        LogSetting(enableAdrenalGraft);
        LogSetting(forceAdrenalGraft);
        LogSetting(enableDeveloperMenu);
        Log.LogInfo($"[ChainedBeasts.ModFoundation] MenuKey={developerMenuKey.Value}");

        InstallFeature(
            "custom localization", "localization",
            enableBulkUp.Value || enableRunnersWraps.Value || enableLeadfoot.Value || enableAdrenalGraft.Value,
            harmony => CustomLocalization.Install(
                harmony,
                Log,
                enableBulkUp.Value,
                enableRunnersWraps.Value,
                enableLeadfoot.Value,
                enableAdrenalGraft.Value));
        InstallFeature(
            "Bulk Up", "bulkup", enableBulkUp.Value,
            harmony => BulkUpFeature.Install(harmony, Log, forceBulkUp.Value));
        InstallFeature(
            "Runner's Wraps", "runnerswraps", enableRunnersWraps.Value,
            harmony => RunnersWrapsFeature.Install(harmony, Log, forceRunnersWraps.Value));
        InstallFeature(
            "Leadfoot", "leadfoot", enableLeadfoot.Value,
            harmony => LeadfootFeature.Install(harmony, Log, forceLeadfoot.Value));
        InstallFeature(
            "Adrenal Graft", "adrenalgraft", enableAdrenalGraft.Value,
            harmony => AdrenalGraftFeature.Install(harmony, Log, forceAdrenalGraft.Value));
        InstallFeature(
            "developer menu", "developermenu", enableDeveloperMenu.Value,
            harmony =>
            {
                DeveloperMenuFeature.Install(harmony, Log, developerMenuKey.Value);
                AddComponent<DeveloperMenuController>();
            });
        InstallFeature(
            "diagnostic", "diagnostics", enableDiagnostics.Value,
            InstallDiagnostics);
    }

    private ConfigEntry<bool> BindToggle(string section, string key, bool defaultValue, string description)
    {
        return Config.Bind(section, key, defaultValue, description);
    }

    private void LogSetting(ConfigEntry<bool> setting)
    {
        Log.LogInfo($"[ChainedBeasts.ModFoundation] {setting.Definition.Key}={setting.Value}");
    }

    private void InstallFeature(string displayName, string harmonySuffix, bool enabled, Action<Harmony> installer)
    {
        if (!enabled)
        {
            Log.LogInfo($"[ChainedBeasts.ModFoundation] {displayName} registration and hooks skipped");
            return;
        }

        var harmony = new Harmony($"{PluginGuid}.{harmonySuffix}");
        try
        {
            installer(harmony);
        }
        catch (Exception exception)
        {
            harmony.UnpatchSelf();
            Log.LogError(
                $"[ChainedBeasts.ModFoundation] {displayName} installation failed: " +
                $"{exception.GetType().Name}: {exception.Message}. That feature's partial hooks were removed.");
        }
    }

    private void InstallDiagnostics(Harmony harmony)
    {
        var target = FeaturePatcher.RequireMethod(
            typeof(UIMainMenu), nameof(UIMainMenu.Initialise), Type.EmptyTypes);
        FeaturePatcher.Patch(
            harmony, typeof(Plugin), target, postfix: nameof(UIMainMenuInitialisePostfix));
        Log.LogInfo("[ChainedBeasts.ModFoundation] diagnostic hook installed: UIMainMenu.Initialise() postfix");
    }

    private static void UIMainMenuInitialisePostfix()
    {
        if (Interlocked.Exchange(ref _diagnosticSnapshotWritten, 1) != 0)
        {
            return;
        }

        _logger?.LogInfo("[ChainedBeasts.ModFoundation] game lifecycle hook fired");

        try
        {
            if (!SettingsManager.InstanceExists)
            {
                _logger?.LogWarning("[ChainedBeasts.ModFoundation] live settings unavailable: SettingsManager instance does not exist");
                return;
            }

            var settingsManager = SettingsManager.Instance;
            var settings = settingsManager?.Current;
            if (settings is null)
            {
                _logger?.LogWarning("[ChainedBeasts.ModFoundation] live settings unavailable: SettingsManager.Current is null");
                return;
            }

            var screenShake = settings.screenShake.ToString("0.###", CultureInfo.InvariantCulture);
            _logger?.LogInfo(
                $"[ChainedBeasts.ModFoundation] live settings: resolution={settings.resolutionWidth}x{settings.resolutionHeight}, " +
                $"vsync={settings.vsync}, screenShake={screenShake}");
        }
        catch (Exception exception)
        {
            _logger?.LogError(
                $"[ChainedBeasts.ModFoundation] live settings read failed: {exception.GetType().Name}: {exception.Message}");
        }
    }
}
