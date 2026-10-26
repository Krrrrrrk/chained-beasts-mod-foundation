using System;

namespace TermsAndContusions.Afflictions;

public enum CustomAffliction { LeadBelly, BrittleGuard, ShakyResolve, TinEar, BurningLungs, HeavyLanding, LooseChange, ColdStart, ObsessiveFocus, Overheating }

public sealed class AfflictionEntry
{
    public CustomAffliction Kind { get; }
    public string Key { get; }
    public int Id { get; }
    public string Name { get; }
    public AfflictionEntry(CustomAffliction kind, string slug, int id, string name)
    { Kind = kind; Key = "terms-and-contusions:affliction/" + slug; Id = id; Name = name; }
}

public static class AfflictionDefinitions
{
    public static readonly AfflictionEntry[] All = {
        new(CustomAffliction.LeadBelly, "lead-belly", 11011, "Lead Belly"),
        new(CustomAffliction.BrittleGuard, "brittle-guard", 11012, "Brittle Guard"),
        new(CustomAffliction.ShakyResolve, "shaky-resolve", 11013, "Shaky Resolve"),
        new(CustomAffliction.TinEar, "tin-ear", 11014, "Tin Ear"),
        new(CustomAffliction.BurningLungs, "burning-lungs", 11015, "Burning Lungs"),
        new(CustomAffliction.HeavyLanding, "heavy-landing", 11016, "Heavy Landing"),
        new(CustomAffliction.LooseChange, "loose-change", 11017, "Loose Change"),
        new(CustomAffliction.ColdStart, "cold-start", 11018, "Cold Start"),
        new(CustomAffliction.ObsessiveFocus, "obsessive-focus", 11019, "Obsessive Focus"),
        new(CustomAffliction.Overheating, "overheating", 11020, "Overheating")
    };
    public static AfflictionEntry Get(CustomAffliction kind) => Array.Find(All, entry => entry.Kind == kind);
    public static AfflictionEntry Find(int id) => Array.Find(All, entry => entry.Id == id);
}
