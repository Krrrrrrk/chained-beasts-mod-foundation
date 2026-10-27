using System;
using System.Collections.Generic;
using System.Linq;

namespace TermsAndContusions.Equipment;

public sealed class EquipmentSnapshot
{
    public int SchemaVersion { get; set; } = 1;
    public bool LedgerEvaluated { get; set; }
    public bool LedgerPaid { get; set; }
    public bool LocketSpent { get; set; }
    public int PocketGold { get; set; }
    public string[] Backstabbed { get; set; } = Array.Empty<string>();
    public string[] OpenedWeapons { get; set; } = Array.Empty<string>();
    public string[] RepairedWeapons { get; set; } = Array.Empty<string>();
    public string[] Executed { get; set; } = Array.Empty<string>();
    public double ChalkElapsed { get; set; }
}

public sealed class OpeningPlan
{
    public string HitId { get; internal set; }
    public string WeaponId { get; internal set; }
    public bool Bonus { get; internal set; }
    internal bool Resolved;
}

/// <summary>Per player/battle. Equipment changes never reconstruct this ledger.</summary>
public sealed class EquipmentBattleState
{
    private EquipmentSnapshot _state = new();
    private readonly Dictionary<string, string> _pendingOpeners = new(StringComparer.Ordinal);
    private double? _aimSince;
    private double _restoredChalk;
    private bool _ledgerPaymentPending;
    public bool LedgerPaid => _state.LedgerPaid;

    // Returns the proposed payment once. Caller verifies native payment before confirming it.
    public int BeginBattle(bool ledgerEquipped, int gold)
    {
        if (_state.LedgerEvaluated) return 0;
        _state.LedgerEvaluated = true;
        _ledgerPaymentPending = ledgerEquipped && gold >= 5;
        return _ledgerPaymentPending ? 5 : 0;
    }
    public void ConfirmPayment(int before, int after)
    {
        if (!_ledgerPaymentPending) return;
        _ledgerPaymentPending = false;
        _state.LedgerPaid = (long)before - after == 5;
    }
    public bool ClaimLocket(bool equipped, int enemyLoss, double hp, double maximum)
    {
        if (_state.LocketSpent || !equipped || EquipmentMath.LocketStamina(hp, maximum, 1, enemyLoss) == 0) return false;
        _state.LocketSpent = true; return true;
    }
    public bool ClaimBackstab(bool equipped, bool successfulBackstab, string enemy)
    {
        if (!equipped || !successfulBackstab || string.IsNullOrEmpty(enemy) || _state.PocketGold >= 10 || _state.Backstabbed.Contains(enemy)) return false;
        _state.Backstabbed = _state.Backstabbed.Append(enemy).ToArray(); _state.PocketGold++; return true;
    }
    public bool ClaimExecution(bool equipped, bool confirmedKill, string enemy)
    {
        if (!equipped || !confirmedKill || string.IsNullOrEmpty(enemy) || _state.Executed.Contains(enemy)) return false;
        _state.Executed = _state.Executed.Append(enemy).ToArray(); return true;
    }
    public OpeningPlan PlanOpening(string hit, string weapon, bool equipped, bool full)
    {
        var plan = new OpeningPlan { HitId = hit, WeaponId = weapon };
        if (string.IsNullOrEmpty(weapon) || _state.OpenedWeapons.Contains(weapon) || _pendingOpeners.ContainsKey(weapon)) return plan;
        _pendingOpeners[weapon] = hit; plan.Bonus = equipped && full; return plan;
    }
    // First real successful contact consumes the physical weapon's opener, even without the scabbard.
    public bool ResolveOpening(OpeningPlan plan, bool successful)
    {
        if (plan is null || plan.Resolved) return false;
        plan.Resolved = true;
        if (plan.WeaponId is null || !_pendingOpeners.TryGetValue(plan.WeaponId, out var id) || id != plan.HitId) return false;
        _pendingOpeners.Remove(plan.WeaponId);
        if (!successful) return false;
        _state.OpenedWeapons = _state.OpenedWeapons.Append(plan.WeaponId).ToArray(); return plan.Bonus;
    }
    public bool ClaimRepair(bool equipped, bool recoveredOwnThrow, string weapon, int current, int maximum)
    {
        if (!equipped || !recoveredOwnThrow || string.IsNullOrEmpty(weapon) || _state.RepairedWeapons.Contains(weapon) || EquipmentMath.Repair(current, maximum) <= current) return false;
        _state.RepairedWeapons = _state.RepairedWeapons.Append(weapon).ToArray(); return true;
    }
    public void Aim(double now, bool eligible)
    {
        if (!eligible) { _aimSince = null; _restoredChalk = 0; return; }
        _aimSince ??= now - _restoredChalk; _restoredChalk = 0;
    }
    public bool ReleaseThrow(double now, bool equipped, bool stationaryAndAiming)
    {
        var ready = equipped && stationaryAndAiming && _aimSince.HasValue && now - _aimSince.Value >= 1.5d;
        _aimSince = null; _restoredChalk = 0; return ready;
    }
    public double ChalkPreparationSeconds(double now) => _aimSince.HasValue ? Math.Clamp(now - _aimSince.Value, 0d, 1.5d) : _restoredChalk;
    public void EquipmentChanged() { _aimSince = null; _restoredChalk = 0; }
    public EquipmentSnapshot Capture(double now)
    {
        if (_pendingOpeners.Count != 0 || _ledgerPaymentPending) throw new InvalidOperationException("Equipment checkpoint awaits opening-hit/payment confirmation");
        return new EquipmentSnapshot { LedgerEvaluated = _state.LedgerEvaluated, LedgerPaid = _state.LedgerPaid,
            LocketSpent = _state.LocketSpent, PocketGold = _state.PocketGold, Backstabbed = _state.Backstabbed.ToArray(),
            OpenedWeapons = _state.OpenedWeapons.ToArray(), RepairedWeapons = _state.RepairedWeapons.ToArray(), Executed = _state.Executed.ToArray(),
            ChalkElapsed = ChalkPreparationSeconds(now) };
    }
    public static EquipmentBattleState Restore(EquipmentSnapshot value)
    {
        if (value is null || value.SchemaVersion != 1 || value.PocketGold < 0 || value.PocketGold > 10 ||
            value.LedgerPaid && !value.LedgerEvaluated || !double.IsFinite(value.ChalkElapsed) || value.ChalkElapsed < 0 || value.ChalkElapsed > 1.5 ||
            new[] { value.Backstabbed, value.OpenedWeapons, value.RepairedWeapons, value.Executed }.Any(list => list is null || list.Length > 10000 || list.Any(string.IsNullOrWhiteSpace) || list.Distinct(StringComparer.Ordinal).Count() != list.Length) ||
            value.Backstabbed.Length != value.PocketGold)
            throw new InvalidOperationException("Unsupported or invalid equipment feature state");
        return new EquipmentBattleState { _state = new EquipmentSnapshot { LedgerEvaluated = value.LedgerEvaluated, LedgerPaid = value.LedgerPaid,
            LocketSpent = value.LocketSpent, PocketGold = value.PocketGold, Backstabbed = value.Backstabbed.ToArray(),
            OpenedWeapons = value.OpenedWeapons.ToArray(), RepairedWeapons = value.RepairedWeapons.ToArray(), Executed = value.Executed.ToArray() }, _restoredChalk = value.ChalkElapsed };
    }
}
