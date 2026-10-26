using System;

namespace TermsAndContusions.Afflictions;

public static class AfflictionMath
{
    public static int Severity(int value) => Math.Clamp(value, 0, 3);
    public static double LeadDelay(int severity) => .25d * Severity(severity);
    public static int BlockInterval(int severity) => severity <= 0 ? 0 : 5 - Severity(severity);
    public static double ShakyDrain(int severity, double maximumStamina, double current) => Math.Min(Math.Max(0, current), Math.Max(0, maximumStamina) * .05d * Severity(severity));
    public static double Favour(int severity) => 1d - .15d * Severity(severity);
    public static double FavourDelta(double before, double after, double factor) => ChainedBeasts.ModFoundation.Logic.ContributionMath.ScalePositiveGain(before, after, factor);
    public static double Recovery(int severity, bool moving) => moving ? 1d - new[] { 0d, .20d, .35d, .50d }[Severity(severity)] : 1d;
    public static double Landing(int severity, bool pending) => pending && severity > 0 ? 1d - (.1d + .1d * Severity(severity)) : 1d;
    public static int GoldLoss(int severity, int carried) => Math.Min(Severity(severity), Math.Max(0, carried));
    public static double Cold(int severity, double battleAge) => battleAge >= 0 && battleAge < 10d ? 1d - .10d * Severity(severity) : 1d;
    public static double Focus(int severity) => 1d - new[] { 0d, .15d, .25d, .35d }[Severity(severity)];
    public static double MeleeCost(int severity, int stacks) => 1d + .05d * Severity(severity) * Math.Clamp(stacks, 0, 3);
    public static string DisplayName(string name, int severity) => name + (Severity(severity) switch { 1 => " I", 2 => " II", 3 => " III", _ => "" });
    public static string Description(CustomAffliction kind, int severity)
    {
        var s = Severity(severity);
        if (s == 0) return PackText.Description("No active effect (cured or suppressed).");
        return PackText.Description(kind switch {
            CustomAffliction.LeadBelly => $"Stamina starts recovering {LeadDelay(s):0.##}s later.",
            CustomAffliction.BrittleGuard => $"Every {BlockInterval(s)} shield blocks cost 1 extra durability.",
            CustomAffliction.ShakyResolve => $"Enemy damage drains {5 * s}% stamina (1s cooldown).",
            CustomAffliction.TinEar => $"Your actions generate {15 * s}% less crowd favor.",
            CustomAffliction.BurningLungs => $"Stamina recovers {new[] { 0, 20, 35, 50 }[s]}% slower while moving.",
            CustomAffliction.HeavyLanding => $"Move {10 + 10 * s}% slower for 0.75s after dodging.",
            CustomAffliction.LooseChange => $"Enemy damage costs {s} gold (3s cooldown).",
            CustomAffliction.ColdStart => $"Deal {10 * s}% less damage during the first ten seconds of each battle.",
            CustomAffliction.ObsessiveFocus => $"After hitting an enemy, deal {new[] { 0, 15, 25, 35 }[s]}% less damage to others for 3s.",
            CustomAffliction.Overheating => $"Repeated melee attacks cost +{5 * s}% stamina (3 stacks). Rest 2s to reset.",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        });
    }
}
