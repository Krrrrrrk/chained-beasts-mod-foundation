using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace ChainedBeasts.FirstPerson;

[BepInPlugin(Id, Name, Version)]
public sealed class Plugin : BasePlugin
{
    public const string Id = "local.chainedbeasts.firstperson";
    public const string Name = "Chained Beasts First Person";
    public const string Version = "0.2.1";
    private Harmony _harmony;
    private FirstPersonController _controller;

    public override void Load()
    {
        var settings = new Settings(Config);
        try
        {
            NativeSafety.VerifyFingerprint();
            _harmony = new Harmony(Id);
            NativeSafety.Install(_harmony);
            _controller = AddComponent<FirstPersonController>();
            _controller.Initialize(settings, Log);
            Log.LogInfo($"First Person {Version}; manual toggle enabled={settings.Enabled}; temporary content locks supported. Gameplay verification pending.");
        }
        catch (Exception error)
        {
            _controller?.Shutdown();
            _harmony?.UnpatchSelf();
            Log.LogError("First Person could not start; normal camera retained. " + error);
        }
    }

    public override bool Unload()
    {
        _controller?.Shutdown();
        _harmony?.UnpatchSelf();
        if (_controller != null) UnityEngine.Object.Destroy(_controller);
        return true;
    }
}
