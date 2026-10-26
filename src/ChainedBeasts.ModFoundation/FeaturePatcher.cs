using System;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using Il2CppInterop.Common;
using ChainedBeasts.ModFoundation.Logic;

namespace ChainedBeasts.ModFoundation;

/// <summary>Exact-signature Harmony helpers shared by Foundation-based plugins.</summary>
public static class FeaturePatcher
{
    private static readonly NativeEntryClaims<MethodBase> Entries = new();
    public static MethodInfo RequireMethod(Type type, string name, params Type[] parameters)
    {
        return AccessTools.DeclaredMethod(type, name, parameters)
            ?? throw new MissingMethodException(type.FullName, name);
    }

    public static MethodInfo RequirePropertyGetter(Type type, string name)
    {
        return AccessTools.PropertyGetter(type, name)
            ?? throw new MissingMethodException(type.FullName, $"get_{name}");
    }

    public static void Patch(
        Harmony harmony,
        Type patchType,
        MethodBase target,
        string prefix = null,
        string postfix = null,
        string finalizer = null)
    {
        if (target is null)
        {
            throw new MissingMethodException("A required patch target was not found.");
        }

        NativeHookPolicy.RequireSafe(target.DeclaringType?.FullName, target.Name);
        // Resolve exactly as the installed Harmony IL2CPP patcher does. The
        // inspected Unity 6 native MethodInfo begins with its method pointer.
        var pointerField = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(target);
        var methodInfo = pointerField is null ? IntPtr.Zero : (IntPtr)pointerField.GetValue(null);
        if (pointerField is not null && methodInfo == IntPtr.Zero)
            throw new InvalidOperationException("Native MethodInfo is unavailable for " + target);
        var address = methodInfo == IntPtr.Zero ? 0L : Marshal.ReadIntPtr(methodInfo).ToInt64();
        if (pointerField is not null) Entries.RequireCompatible(address, target);
        harmony.Patch(
            target,
            prefix: GetPatchMethod(patchType, prefix),
            postfix: GetPatchMethod(patchType, postfix),
            finalizer: GetPatchMethod(patchType, finalizer));
        if (pointerField is not null) Entries.Commit(address, target);
    }

    private static HarmonyMethod GetPatchMethod(Type patchType, string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var method = AccessTools.DeclaredMethod(patchType, name);
        if (method is null)
        {
            throw new MissingMethodException(patchType.FullName, name);
        }

        return new HarmonyMethod(method);
    }
}
