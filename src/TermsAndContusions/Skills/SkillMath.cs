using System;
using System.Globalization;

namespace TermsAndContusions.Skills;

public enum CustomSkill
{
    BulkUp, MeasuredViolence, RunningOnFumes, CatchYourBreath, ExpensiveTaste, FreshMeat,
    PickYourMoment, MixedMartialArts, PaybackPlan, WasteNot, BreathingRoom, Longshot, IronGrip
}

public static class SkillMath
{
    public static int Rank(int rank) => Math.Clamp(rank, 0, 4);
    public static double BulkUp(int rank) => 1d + Rank(rank);
    public static double Measured(int rank, double beforeCostStamina01) => beforeCostStamina01 >= 0.8d ? 1d + .25d * Rank(rank) : 1d;
    public static double Fumes(int rank, double beforeCostStamina01) => beforeCostStamina01 < .25d ? 1d + .35d * Rank(rank) : 1d;
    public static double StationaryDelayRate(int rank, bool stationary) => stationary ? 1d + Rank(rank) : 1d;
    public static double GoldDamage(int rank, int gold) => 1d + Rank(rank) * Math.Min(.01d * Math.Floor(Math.Max(0, gold) / 10d), .25d);
    public static double Fresh(int rank, int beforeHealth, int effectiveMaximum) => effectiveMaximum > 0 && beforeHealth >= effectiveMaximum ? 1d + .30d * Rank(rank) : 1d;
    public static double Pick(int rank, bool ready) => ready ? 1d + .50d * Rank(rank) : 1d;
    public static double CommittedPick(int rank, bool ready, int capturedEpoch, int currentEpoch) => Pick(rank, ready && capturedEpoch == currentEpoch);
    public static double Mixed(int rank) => 1d + .30d * Rank(rank);
    public static double PaybackGain(int rank, double actualHpLoss) => .50d * Rank(rank) * Math.Max(0, actualHpLoss);
    public static double PaybackCap(double maximumHp) => .25d * Math.Max(0, maximumHp);
    public static double BreakStamina(int rank, double maximumStamina) => .25d * Rank(rank) * Math.Max(0, maximumStamina);
    public static double Incoming(int rank, int livingHostilesWithinThreeMetres) => 1d - Math.Min(.05d * Rank(rank) * Math.Max(0, livingHostilesWithinThreeMetres), .20d * Rank(rank));
    public static double ThrowMultiplier(int rank, bool sling) => (1d + Rank(rank)) * (sling ? 2d : 1d);
    public static double DirectDamage(double otherwiseNormal, bool melee, double beforeCostStamina01, int targetHp, int effectiveMaximum,
        int measuredRank, int fumesRank, int freshRank, int pickRank, bool pickReady, double mixedFactor, double payback)
    {
        var multiplier = Fresh(freshRank, targetHp, effectiveMaximum) * Pick(pickRank, pickReady) * mixedFactor;
        if (melee) multiplier *= Measured(measuredRank, beforeCostStamina01) * Fumes(fumesRank, beforeCostStamina01);
        return otherwiseNormal * multiplier + (melee ? payback : 0d);
    }

    public static string Effect(CustomSkill skill, int rank)
    {
        var r = Rank(rank);
        if (r == 0) return PackText.Description("No effect.");
        string Percent(double value) => (value * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
        return PackText.Description(skill switch
        {
            CustomSkill.BulkUp => $"{1 + r}x maximum health. Keep your health percentage.",
            CustomSkill.MeasuredViolence => $"+{25 * r}% melee damage when attacking at 80%+ stamina.",
            CustomSkill.RunningOnFumes => $"+{35 * r}% melee damage when attacking below 25% stamina.",
            CustomSkill.CatchYourBreath => $"Stamina recovery delay: 1/{1 + r} while standing still.",
            CustomSkill.ExpensiveTaste => $"+{r}% damage per 10 gold. Maximum +{25 * r}%.",
            CustomSkill.FreshMeat => $"+{30 * r}% damage to full-health enemies.",
            CustomSkill.PickYourMoment => $"After 2s without attacking, your next attack deals +{50 * r}% damage.",
            CustomSkill.MixedMartialArts => $"Alternate melee and thrown hits for +{30 * r}% damage. Bonus lasts 6s.",
            CustomSkill.PaybackPlan => $"Store {50 * r}% of enemy damage for your next melee hit. Maximum 25% max HP; expires after 5s.",
            CustomSkill.WasteNot => $"Breaking a weapon restores {Percent(.25d * r)} stamina.",
            CustomSkill.BreathingRoom => $"Take {5 * r}% less damage per nearby enemy (3m). Maximum {20 * r}%.",
            CustomSkill.Longshot => $"{1 + r}x throw range and charge speed.",
            CustomSkill.IronGrip => "Taking hits no longer makes you drop carried enemies.",
            _ => throw new ArgumentOutOfRangeException(nameof(skill))
        });
    }
    // A description advertises what an unowned/unavailable skill does. This
    // presentation rank never participates in gameplay ownership or modifiers.
    public static int DescriptionRank(int? ownedRank) => Math.Max(1, Rank(ownedRank ?? 0));
    public static string Description(CustomSkill skill, int ownedRank) => Effect(skill, DescriptionRank(ownedRank));
    // Offers describe the rank being purchased; owned/detail/reveal views show
    // the owned rank, or the first-rank effect when there is no owned rank yet.
    public static string OfferDescription(CustomSkill skill, int ownedRank) => Effect(skill, Math.Min(4, Rank(ownedRank) + 1));
}
