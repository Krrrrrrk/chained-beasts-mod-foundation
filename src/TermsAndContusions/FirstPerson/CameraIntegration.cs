using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using ChainedBeasts.ModFoundation.Runtime;
using TermsAndContusions.Afflictions;

namespace TermsAndContusions.FirstPerson;

// The source camera is compiled into TC. No standalone FPS assembly is needed.
// With our optional FPS plugin present, stop its controller before taking over
// the same camera/input targets. TC's selected mode owns offers and controls;
// the standalone config still supplies camera preferences when it is present.
internal static class CameraIntegration
{
    internal const string StandaloneGuid = "local.chainedbeasts.firstperson";
    internal static ConfigEntry<bool> PotionOnlySetting { get; private set; }
    internal static bool HasStandalone => _standalone != null;
    internal static bool LocalPotionOnly => PotionOnlySetting?.Value == true;
    // The host's already-synchronized Eye Level offer bit carries the session
    // mode too. Personal preferences are never overwritten on another client.
    internal static bool PotionOnly => ContentOfferServices.SessionLocked
        ? ContentOfferServices.Allows(FirstPersonPotionFeature.Key) : LocalPotionOnly;
    internal static bool ManualSwitching => !PotionOnly;
    private static BasePlugin _standalone;
    private static FirstPersonController _controller;
    private static Harmony _harmony;
    private static bool _suspendedStandalone;
    private static bool _standaloneDetected, _standaloneSupported;

    internal static void Configure(ConfigFile config)
    {
        if (_controller != null || _suspendedStandalone) throw new InvalidOperationException("Eye Level camera is already initialized.");
        _standalone = null;
        _standaloneDetected = _standaloneSupported = false;
        if (IL2CPPChainloader.Instance.Plugins.TryGetValue(StandaloneGuid, out var info))
        {
            _standaloneDetected = true;
            _standalone = info.Instance as BasePlugin;
            _standaloneSupported = _standalone != null && _standalone.GetType().Assembly.GetName().Name == "ChainedBeasts.FirstPerson" &&
                _standalone.GetType().Assembly.GetName().Version >= new Version(0, 1, 0, 0) &&
                _standalone.GetType().Assembly.GetName().Version <= new Version(0, 2, 0, 0);
        }
        PotionOnlySetting = config.Bind("FirstPerson", "PotionOnly", !HasStandalone,
            "True locks Eye Level offers on and disables manual camera switching. False locks Eye Level offers off and enables F9 switching. Both modes are available with TC alone. Also editable in TC Content before a run; the host's mode controls the party during a session.");
    }

    internal static void Initialize(Plugin plugin, ConfigFile config, ManualLogSource log)
    {
        Configure(config);
        if (_standaloneDetected && !_standaloneSupported)
            throw new NotSupportedException("Unrecognized FPS Mod build; its camera was left alone.");
        NativeSafety.VerifyFingerprint();
        try
        {
            // Our standalone 0.1.x/0.2.0 Unload restores renderers, cursor and
            // Cinemachine and removes its three native input hooks.
            if (HasStandalone)
            {
                if (!_standalone.Unload()) throw new InvalidOperationException("FPS Mod did not release its camera.");
                _suspendedStandalone = true;
            }
            var settings = HasStandalone ? new Settings(_standalone.Config) : new Settings(config, "EyeLevelCamera.");
            _harmony = new Harmony(Plugin.PluginGuid + ".eyelevelcamera");
            NativeSafety.Install(_harmony);
            _controller = plugin.AddComponent<FirstPersonController>();
            _controller.Initialize(settings, log, () => ManualSwitching);
            log.LogInfo("[Terms & Contusions] Eye Level camera ready; standalone FPS=" + HasStandalone +
                ", F9 switching=" + ManualSwitching + ". One camera controller is active.");
        }
        catch { Shutdown(); throw; }
    }

    internal static void Shutdown()
    {
        _controller?.Shutdown();
        if (_controller != null) UnityEngine.Object.Destroy(_controller);
        _controller = null;
        _harmony?.UnpatchSelf();
        _harmony = null;
        if (_suspendedStandalone)
        {
            _suspendedStandalone = false;
            try { _standalone.Load(); }
            finally { _standalone = null; }
        }
        else _standalone = null;
    }
}
