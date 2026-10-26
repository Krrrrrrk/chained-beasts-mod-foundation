using System;

namespace ChainedBeasts.ModFoundation.Logic;

public sealed class ThrowLaunch
{
    public double Force { get; }
    public double AngleDegrees { get; }
    public ThrowLaunch(double force, double angleDegrees) { Force = force; AngleDegrees = angleDegrees; }
}

public static class ThrowPhysics
{
    // The native RPC serializes the owner's launch vector. Only the observed
    // normal-held-throw dispatch may change it; Execute/replays and offhands
    // must use those exact serialized values on every peer.
    public static ThrowLaunch ForDispatch(double force, double angleDegrees, double multiplier,
        bool owner, bool nativeExecute, bool normalHeldThrow) => owner && !nativeExecute && normalHeldThrow
            ? Extend(force, angleDegrees, multiplier) : new ThrowLaunch(force, angleDegrees);

    // Scale horizontal velocity only. Vertical flight, launch height, gravity and
    // time to ground remain unchanged, so unobstructed horizontal range scales M.
    public static ThrowLaunch Extend(double force, double angleDegrees, double multiplier)
    {
        if (!double.IsFinite(force) || !double.IsFinite(angleDegrees) || !double.IsFinite(multiplier) || multiplier < 1)
            throw new ArgumentException("Invalid throw parameters");
        if (multiplier == 1) return new ThrowLaunch(force, angleDegrees);
        var angle = angleDegrees * Math.PI / 180d;
        var horizontal = Math.Cos(angle) * force * multiplier;
        var vertical = Math.Sin(angle) * force;
        return new ThrowLaunch(Math.Sqrt(horizontal * horizontal + vertical * vertical), Math.Atan2(vertical, horizontal) * 180d / Math.PI);
    }
    public static double ChargeTime(double normalTime, double speed) => normalTime / Math.Max(1d, speed);
    public static double ExtraClockStep(double deltaTime, double speed) => Math.Max(0d, deltaTime) * (Math.Max(1d, speed) - 1d);
    public static double HorizontalRange(double force, double angleDegrees, double startHeight, double gravity)
    {
        if (!double.IsFinite(force) || !double.IsFinite(angleDegrees) || !double.IsFinite(startHeight) || !double.IsFinite(gravity) || gravity <= 0)
            return double.NaN;
        var angle = angleDegrees * Math.PI / 180d;
        var vertical = force * Math.Sin(angle);
        var flightTime = (vertical + Math.Sqrt(vertical * vertical + 2d * gravity * Math.Max(0d, startHeight))) / gravity;
        return force * Math.Cos(angle) * flightTime;
    }
}
