using System;
using System.Linq;

namespace TermsAndContusions.Skills;

public sealed class SkillEntry
{
    public CustomSkill Kind { get; }
    public string Key { get; }
    public int Id { get; }
    public string Name { get; }
    public int MaxRank { get; }
    public SkillEntry(CustomSkill kind, string key, int id, string name, int maxRank = 4)
    { Kind = kind; Key = key; Id = id; Name = name; MaxRank = maxRank; }
}

public static class SkillDefinitions
{
    public static readonly SkillEntry[] All =
    {
        new(CustomSkill.BulkUp, CustomContentIds.BulkUpKey, CustomContentIds.BulkUpSkill, "Bulk Up"),
        new(CustomSkill.MeasuredViolence, "terms-and-contusions:skill/measured-violence", 11001, "Measured Violence"),
        new(CustomSkill.RunningOnFumes, "terms-and-contusions:skill/running-on-fumes", 11002, "Running on Fumes"),
        new(CustomSkill.CatchYourBreath, "terms-and-contusions:skill/catch-your-breath", 11003, "Catch Your Breath"),
        new(CustomSkill.ExpensiveTaste, "terms-and-contusions:skill/expensive-taste", 11004, "Expensive Taste"),
        new(CustomSkill.FreshMeat, "terms-and-contusions:skill/fresh-meat", 11005, "Fresh Meat"),
        new(CustomSkill.PickYourMoment, "terms-and-contusions:skill/pick-your-moment", 11006, "Pick Your Moment"),
        new(CustomSkill.MixedMartialArts, "terms-and-contusions:skill/mixed-martial-arts", 11007, "Mixed Martial Arts"),
        new(CustomSkill.PaybackPlan, "terms-and-contusions:skill/payback-plan", 11008, "Payback Plan"),
        new(CustomSkill.WasteNot, "terms-and-contusions:skill/waste-not", 11009, "Waste Not"),
        new(CustomSkill.BreathingRoom, "terms-and-contusions:skill/breathing-room", 11010, "Breathing Room"),
        new(CustomSkill.Longshot, "terms-and-contusions:skill/longshot", 11011, "Longshot"),
        new(CustomSkill.IronGrip, "terms-and-contusions:skill/iron-grip", 11012, "Iron Grip", 1)
    };
    public static SkillEntry Get(CustomSkill kind) => All.Single(value => value.Kind == kind);
    public static SkillEntry Find(int id) => All.FirstOrDefault(value => value.Id == id);
}
