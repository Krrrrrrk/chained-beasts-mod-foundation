using System;
using System.Linq;
using ChainedBeasts.ModFoundation.Stats;

namespace TermsAndContusions.Surgeries;

public enum CustomSurgery { AdrenalGraft, AnchorHeels, SpringLoadedKnees, DenseSkeleton, AuxiliaryTendons, MarrowEnrichment, PainConverter, EmergencyClotting, CounterweightImplant, FuryPacemaker, RetractableTalons }
public sealed class SurgeryEntry
{
    public CustomSurgery Kind { get; }
    public string Key { get; }
    public int Id { get; }
    public string Name { get; }
    public string Description { get; }
    public int DefaultPrice { get; }
    public SurgeryEntry(CustomSurgery kind, string slug, int id, string name, string description, int price)
    { Kind = kind; Key = "terms-and-contusions:surgery/" + slug; Id = id; Name = name; Description = PackText.Description(description); DefaultPrice = price; }
}
public static class SurgeryDefinitions
{
    public static readonly SurgeryEntry[] All = {
        new(CustomSurgery.AdrenalGraft, "adrenal-graft", 11020, "Adrenal Graft", "Stamina regenerates 25% faster.", 0),
        new(CustomSurgery.AnchorHeels, "anchor-heels", 11021, "Anchor Heels", "Receive 75% less knockback. Dodges travel 20% less distance.", 35),
        new(CustomSurgery.SpringLoadedKnees, "spring-loaded-knees", 11022, "Spring-Loaded Knees", "Dodges travel 30% farther but cost 20% more stamina.", 40),
        new(CustomSurgery.DenseSkeleton, "dense-skeleton", 11023, "Dense Skeleton", "Take 15% less damage. Maximum stamina is reduced by 20%.", 55),
        new(CustomSurgery.AuxiliaryTendons, "auxiliary-tendons", 11024, "Auxiliary Tendons", "Regular melee attacks cost 20% less stamina. -10% maximum health.", 40),
        new(CustomSurgery.MarrowEnrichment, "marrow-enrichment", 11025, "Marrow Enrichment", "+30% healing. -15% maximum health.", 40),
        new(CustomSurgery.PainConverter, "pain-converter", 11026, "Pain Converter", "Enemy damage restores 1.5x its health percentage as stamina (max 25%). -10% maximum stamina.", 50),
        new(CustomSurgery.EmergencyClotting, "emergency-clotting", 11027, "Emergency Clotting", "Once per battle: survive a 20%+ health hit to heal 25% of that loss over 4s.", 45),
        new(CustomSurgery.CounterweightImplant, "counterweight-implant", 11028, "Counterweight Implant", "After stun or trip: next melee hit gains +25% damage and +75% knockback (5s).", 45),
        new(CustomSurgery.FuryPacemaker, "fury-pacemaker", 11029, "Fury Pacemaker", "Fury gains instead restore 30% stamina (8s cooldown).", 50),
        new(CustomSurgery.RetractableTalons, "retractable-talons", 11030, "Retractable Talons", "Unarmed hits bleed: 5 damage over 3s (refreshes). -10% weapon damage.", 45)
    };
    public static SurgeryEntry Get(CustomSurgery kind) => All.Single(entry => entry.Kind == kind);
    public static SurgeryEntry Find(int id) => All.FirstOrDefault(entry => entry.Id == id);
}
public static class SurgeryMath
{
    public static double Pain(int loss, int maximum, double staminaMaximum) => maximum <= 0 || loss <= 0 ? 0 : Math.Max(0, staminaMaximum) * Math.Min(.25, loss / (double)maximum * 1.5);
    public static double Factor(CustomSurgery kind, StatChannel channel) => (kind, channel) switch {
        (CustomSurgery.AdrenalGraft, StatChannel.StaminaRecoveryRate) => 1.25,
        (CustomSurgery.AnchorHeels, StatChannel.IncomingKnockback) => .25,
        (CustomSurgery.AnchorHeels, StatChannel.DodgeDistance) => .8,
        (CustomSurgery.SpringLoadedKnees, StatChannel.DodgeDistance) => 1.3,
        (CustomSurgery.SpringLoadedKnees, StatChannel.DodgeStaminaCost) => 1.2,
        (CustomSurgery.DenseSkeleton, StatChannel.MaximumStamina) => .8,
        (CustomSurgery.AuxiliaryTendons, StatChannel.MaximumHealth) => .9,
        (CustomSurgery.MarrowEnrichment, StatChannel.MaximumHealth) => .85,
        (CustomSurgery.MarrowEnrichment, StatChannel.HealingAmount) => 1.3,
        (CustomSurgery.PainConverter, StatChannel.MaximumStamina) => .9,
        _ => 1 };
    public static double RegularMelee(bool owned, bool special) => owned && !special ? .8 : 1;
    public static double WeaponDamage(bool talons, bool unarmed) => talons && !unarmed ? .9 : 1;
}
