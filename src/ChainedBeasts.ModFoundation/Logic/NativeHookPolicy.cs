using System;

namespace ChainedBeasts.ModFoundation.Logic;

/// <summary>Known unsafe native targets on the inspected build. A managed wrapper does not prove a unique native body.</summary>
public static class NativeHookPolicy
{
    // Both wrappers share RVA 0x7c44d0 and only conditionally call Weapon.Attack,
    // which has its own unique observed body. Observe that base call instead.
    public static bool ObserveWeaponMethod(string declaringType, string method) =>
        method != "Attack" || declaringType is not ("WeaponCharge" or "WeaponAxeCharge");

    public static void RequireSafe(string declaringType, string method)
    {
        if (declaringType == "Projectile" && method == "OnDespawn")
            throw new InvalidOperationException("Projectile.OnDespawn is a folded native no-op (ret); detouring it can intercept unrelated empty methods. Use the inspected nontrivial OnDestroy path and launch-generation invalidation.");
        if (!ObserveWeaponMethod(declaringType, method))
            throw new InvalidOperationException("Charged-weapon Attack wrappers share one native body and delegate to Weapon.Attack; observe the unique base method instead of stacking native detours.");
    }
}
