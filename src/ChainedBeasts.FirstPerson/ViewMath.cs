#if TC_EMBEDDED_CAMERA
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
#endif
#if TC_EMBEDDED_CAMERA
namespace TermsAndContusions.FirstPerson;
#else
namespace ChainedBeasts.FirstPerson;
#endif

internal static class ViewMath
{
    internal static float Bounded(float value, float fallback, float min, float max) =>
        float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    internal static float Wrap(float angle) => float.IsFinite(angle) ? ((angle % 360) + 360) % 360 : 0;

    internal static (float X, float Z) Rotate(float x, float z, float yaw)
    {
        if (!float.IsFinite(x) || !float.IsFinite(z)) return (0, 0);
        var radians = Wrap(yaw) * Math.PI / 180;
        var sin = (float)Math.Sin(radians);
        var cos = (float)Math.Cos(radians);
        return (x * cos + z * sin, z * cos - x * sin);
    }

    internal static (float Yaw, float Pitch) Look(float yaw, float pitch, float x, float y, bool invertY)
    {
        x = Bounded(x, 0, -90, 90);
        y = Bounded(y, 0, -90, 90);
        return (Wrap(yaw + x), Math.Clamp(pitch + (invertY ? y : -y), -80, 80));
    }
}
