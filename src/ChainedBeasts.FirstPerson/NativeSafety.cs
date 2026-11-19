#if TC_EMBEDDED_CAMERA
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
#endif
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using BepInEx;
using HarmonyLib;
using Il2CppInterop.Common;
using UnityEngine;

#if TC_EMBEDDED_CAMERA
namespace TermsAndContusions.FirstPerson;
#else
namespace ChainedBeasts.FirstPerson;
#endif

internal static class NativeSafety
{
    internal const string GameHash = "ae48a9c3f8018d3f996c4e692a3736f8b5dcb4ead5c8d0e6e40f3e83f0004a6a";
    private static readonly List<(MethodBase Method, long Entry)> Selected = new();

    internal static void VerifyFingerprint()
    {
        using var input = File.OpenRead(Path.Combine(Paths.GameRootPath, "GameAssembly.dll"));
        using var algorithm = SHA256.Create();
        var actual = Convert.ToHexString(algorithm.ComputeHash(input)).ToLowerInvariant();
        if (actual != GameHash)
            throw new NotSupportedException("Uninspected game build; First Person disabled. GameAssembly SHA256=" + actual);
    }

    private static long Entry(MethodBase method)
    {
        var field = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(method);
        if (field == null) return 0;
        var info = (IntPtr)(field.GetValue(null) ?? IntPtr.Zero);
        return info == IntPtr.Zero ? 0 : Marshal.ReadIntPtr(info).ToInt64();
    }

    private static void CheckAliases(MethodBase target, long entry)
    {
        foreach (var other in Harmony.GetAllPatchedMethods())
            if (other != target && Entry(other) == entry)
                throw new InvalidOperationException("Patched native alias for " + target + ": " + other);
    }

    internal static void Install(Harmony harmony)
    {
        Selected.Clear();
        using var process = Process.GetCurrentProcess();
        var module = process.Modules.Cast<ProcessModule>().Single(m =>
            string.Equals(m.ModuleName, "GameAssembly.dll", StringComparison.OrdinalIgnoreCase));
        InstallOne(typeof(Player), "GetModifiedPlayerInput", new[] { typeof(Vector2), typeof(bool) },
            0x722970, nameof(InputHooks.Movement), false);
        InstallOne(typeof(Player), "GetMouseGroundPos", Type.EmptyTypes,
            0x723610, nameof(InputHooks.MousePoint), true);
        InstallOne(typeof(NetworkAgent), "UpdateModelFacing", new[] { typeof(Vector3), typeof(bool) },
            0x6d6fe0, nameof(InputHooks.Facing), true);

        void InstallOne(Type owner, string name, Type[] signature, int rva, string callback, bool prefix)
        {
            var method = AccessTools.DeclaredMethod(owner, name, signature) ?? throw new MissingMethodException(owner.Name, name);
            var entry = Entry(method);
            if (entry != module.BaseAddress.ToInt64() + rva)
                throw new InvalidOperationException("Unexpected native entry: " + method);
            if (Selected.Any(x => x.Entry == entry)) throw new InvalidOperationException("Own native alias: " + method);
            CheckAliases(method, entry);
            var patch = new HarmonyMethod(typeof(InputHooks), callback) { priority = Priority.Last };
            harmony.Patch(method, prefix: prefix ? patch : null, postfix: prefix ? null : patch);
            Selected.Add((method, entry));
        }
    }

    // Another plugin may install after us. Repeat before any input adaptation is activated.
    internal static void Audit()
    {
        foreach (var item in Selected) CheckAliases(item.Method, item.Entry);
    }
}
