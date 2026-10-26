using System;

namespace TermsAndContusions.Equipment;

public enum CustomEquipment { ExecutionersHood, LeadLinedPurse, BorrowersScabbard, ButchersApron, LastGaspLocket, MercenarysLedger, PickpocketsMitts, ChalkBag, SalvagersHook, DuelistsSpurs }

public sealed class EquipmentEntry
{
    public CustomEquipment Kind { get; }
    public string Key { get; }
    public int Id { get; }
    public int Price { get; }
    public string Name { get; }
    public string Description { get; }
    public EquipmentEntry(CustomEquipment kind, string slug, int id, int price, string name, string description)
    { Kind = kind; Key = "terms-and-contusions:equipment/" + slug; Id = id; Price = price; Name = name; Description = PackText.Description(description); }
}

public static class EquipmentDefinitions
{
    public static readonly EquipmentEntry[] All = {
        new(CustomEquipment.ExecutionersHood, "executioners-hood", 2488, 55, "Executioner's Hood", "+35% melee damage to enemies below 30% health."),
        new(CustomEquipment.LeadLinedPurse, "lead-lined-purse", 2489, 50, "Lead-Lined Purse", "Take 1% less damage per 10 gold. Maximum 20%."),
        new(CustomEquipment.BorrowersScabbard, "borrowers-scabbard", 2490, 60, "Borrower's Scabbard", "A full-durability weapon's first hit each battle: +50% damage, +1 durability cost."),
        new(CustomEquipment.ButchersApron, "butchers-apron", 2491, 55, "Butcher's Apron", "Executions restore 3% health."),
        new(CustomEquipment.LastGaspLocket, "last-gasp-locket", 2492, 60, "Last-Gasp Locket", "Once per battle: survive an enemy hit below 25% health to restore 50% stamina."),
        new(CustomEquipment.MercenarysLedger, "mercenarys-ledger", 2493, 50, "Mercenary's Ledger", "Pay 5 gold at battle start for +20% damage that battle."),
        new(CustomEquipment.PickpocketsMitts, "pickpockets-mitts", 2494, 45, "Pickpocket's Mitts", "Your first backstab against each enemy grants 1 gold. Maximum 10 per battle."),
        new(CustomEquipment.ChalkBag, "chalk-bag", 2495, 45, "Chalk Bag", "Aim a throw while still for 1.5s to deal +40% damage."),
        new(CustomEquipment.SalvagersHook, "salvagers-hook", 2496, 65, "Salvager's Hook", "Recover your thrown weapon to repair 1 durability. Once per weapon each battle."),
        new(CustomEquipment.DuelistsSpurs, "duelists-spurs", 2497, 50, "Duelist's Spurs", "+20% melee damage when only one enemy is within 4m.")
    };
    public static EquipmentEntry Get(CustomEquipment kind) => All[Array.FindIndex(All, value => value.Kind == kind)];
    public static EquipmentEntry Find(int id) => Array.Find(All, value => value.Id == id);
}
