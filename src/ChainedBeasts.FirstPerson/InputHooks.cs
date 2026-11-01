#if TC_EMBEDDED_CAMERA
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
#endif
using UnityEngine;

#if TC_EMBEDDED_CAMERA
namespace TermsAndContusions.FirstPerson;
#else
namespace ChainedBeasts.FirstPerson;
#endif

internal static class InputHooks
{
    // Rotate the native RESULT so immobilization, input reversal, magnitude and other modifiers still run.
    internal static void Movement(Player __instance, ref Vector3 __result)
    {
        var controller = FirstPersonController.Instance;
        try
        {
            if (controller?.Controls(__instance) != true) return;
            var rotated = ViewMath.Rotate(__result.x, __result.z, controller.Yaw);
            __result = new Vector3(rotated.X, __result.y, rotated.Z);
        }
        catch (Exception error) { controller?.Fail(error); }
    }

    internal static bool MousePoint(Player __instance, ref Vector3 __result)
    {
        var controller = FirstPersonController.Instance;
        try
        {
            if (controller?.Controls(__instance) != true) return true;
            var origin = __instance.transform.position;
            var direction = ViewMath.Rotate(0, 1, controller.Yaw);
            // GetInputToMouse and the native target picker both consume this ground point.
            __result = new Vector3(origin.x + direction.X * 12, 0, origin.z + direction.Z * 12);
            return false;
        }
        catch (Exception error) { controller?.Fail(error); return true; }
    }

    internal static void Facing(NetworkAgent __instance, ref Vector3 movementDirection, ref bool ignoreTargetAgent)
    {
        var controller = FirstPersonController.Instance;
        try
        {
            if (controller?.Controls(__instance) != true) return;
            var direction = ViewMath.Rotate(0, 1, controller.Yaw);
            movementDirection = new Vector3(direction.X, 0, direction.Z);
            ignoreTargetAgent = true;
            // The native function still enforces its rotation locks, knockdowns and attack states.
        }
        catch (Exception error) { controller?.Fail(error); }
    }
}
