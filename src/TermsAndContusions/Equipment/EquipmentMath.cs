using System;

namespace TermsAndContusions.Equipment;

public static class EquipmentMath
{
    public static double Hood(bool equipped, bool melee, bool hostile, double hp, double maximum) =>
        equipped && melee && hostile && hp > 0 && maximum > 0 && hp < .30d * maximum ? 1.35d : 1d;
    public static double Purse(bool equipped, int gold) => equipped ? 1d - Math.Min(.20d, .01d * (Math.Max(0, gold) / 10)) : 1d;
    public static double Spurs(bool equipped, bool melee, bool targetIsSoleNearbyHostile) => equipped && melee && targetIsSoleNearbyHostile ? 1.2d : 1d;
    public static double Ledger(bool equipped, bool paid) => equipped && paid ? 1.2d : 1d;
    public static bool Full(int durability, int maximum, bool isDefault) => !isDefault && maximum > 0 && durability >= maximum;
    public static int Repair(int current, int maximum) => current > 0 && maximum > 0 ? Math.Min(maximum, current >= maximum ? current : current + 1) : Math.Max(0, current);
    public static int ExecutionHeal(int maximum, int current, int attainable) => (int)Math.Min(Math.Max(0L, (long)attainable - current), Math.Ceiling(Math.Max(0, maximum) * .03d));
    public static double LocketStamina(double hp, double maxHp, double maxStamina, int enemyLoss) => enemyLoss > 0 && hp > 0 && maxHp > 0 && hp < .25d * maxHp ? Math.Max(0, maxStamina) * .5d : 0d;
}
