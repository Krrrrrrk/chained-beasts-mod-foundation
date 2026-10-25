using System;
using System.Reflection;
using HarmonyLib;

namespace ChainedBeasts.ModFoundation;

internal static class FeaturePatcher
{
    internal static MethodInfo RequireMethod(Type type, string name, params Type[] parameters)
    {
        return AccessTools.DeclaredMethod(type, name, parameters)
            ?? throw new MissingMethodException(type.FullName, name);
    }

    internal static MethodInfo RequirePropertyGetter(Type type, string name)
    {
        return AccessTools.PropertyGetter(type, name)
            ?? throw new MissingMethodException(type.FullName, $"get_{name}");
    }

    internal static void Patch(
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

        harmony.Patch(
            target,
            prefix: GetPatchMethod(patchType, prefix),
            postfix: GetPatchMethod(patchType, postfix),
            finalizer: GetPatchMethod(patchType, finalizer));
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
