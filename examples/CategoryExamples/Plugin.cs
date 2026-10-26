using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using ChainedBeasts.ModFoundation.Content;
using ChainedBeasts.ModFoundation.Runtime;
using HarmonyLib;

namespace CategoryExamples;

// Optional teaching assembly. Never included in product ZIPs or normal deployment.
[BepInPlugin(Guid, "Foundation Category Examples", "0.8.0")]
[BepInDependency(ChainedBeasts.ModFoundation.Plugin.PluginGuid, ">=0.9.0 <0.10.0")]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "example.chainedbeasts.categoryexamples";
    public override void Load()
    {
        if (!FoundationServices.IsReady) throw new InvalidOperationException("Foundation unavailable");
        var enabled = Config.Bind("Examples", "EnableExamples", true, "Optional tutorial content; restart to apply.").Value;
        var claims = new[] {
            new ContentDescriptor(EquipmentExample.Key, Guid, ContentCategory.Equipment, EquipmentExample.Id, 1, enabled),
            new ContentDescriptor(AfflictionExample.Key, Guid, ContentCategory.Affliction, AfflictionExample.Id, 3, enabled),
            new ContentDescriptor(SurgeryExample.Key, Guid, ContentCategory.Surgery, SurgeryExample.Id, 1, enabled) };
        FoundationServices.Content.RegisterMany(claims);
        if (!enabled) return;
        var harmony = new Harmony(Guid);
        try { EquipmentExample.Install(harmony); AfflictionExample.Install(harmony); SurgeryExample.Install(harmony); }
        catch { foreach (var claim in claims) FoundationServices.Content.Disable(claim.Key); harmony.UnpatchSelf(); throw; }
        Log.LogInfo("Three optional category examples registered through Foundation; native runtime behavior awaits manual testing.");
    }
}
