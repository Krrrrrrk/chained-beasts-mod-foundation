using System;

namespace TermsAndContusions.Skills;

public enum HitKind { Melee, ThrownWeapon, OtherRanged }

public sealed class SkillStateSnapshot
{
    public int SchemaVersion { get; set; } = 1;
    public double AttackAge { get; set; }
    public int PrimeKind { get; set; } = -1;
    public double PrimeRemaining { get; set; }
    public double Reservoir { get; set; }
    public double ReservoirRemaining { get; set; }
}

public sealed class HitPlan
{
    public string Id { get; set; }
    public HitKind Kind { get; set; }
    public double Multiplier { get; set; }
    public double Payback { get; set; }
    public bool UsesPrime { get; set; }
    public bool Resolved { get; set; }
}

/// <summary>Production charge/reservoir logic. A hit reserves a charge until native HP loss confirms it.</summary>
public sealed class SkillCombatState
{
    private double _lastAttack;
    private int _primeKind = -1;
    private double _primeUntil;
    private string _primeReservation;
    private double _reservoir;
    private double _reservoirUntil;
    private string _paybackReservation;
    public SkillCombatState(double now) => _lastAttack = now;
    public bool Commit(double now, int pickRank)
    {
        var ready = SkillMath.Rank(pickRank) > 0 && now - _lastAttack >= 2d;
        _lastAttack = now;
        return ready;
    }
    public void EnemyDamage(double now, int paybackRank, double actualHpLoss, double maximumHp)
    {
        Expire(now, maximumHp);
        if (paybackRank <= 0 || actualHpLoss <= 0) return;
        _reservoir = Math.Min(SkillMath.PaybackCap(maximumHp), _reservoir + SkillMath.PaybackGain(paybackRank, actualHpLoss));
        _reservoirUntil = now + 5d;
    }
    public HitPlan Plan(string id, HitKind kind, double now, int mixedRank, int paybackRank, double maximumHp)
    {
        Expire(now, maximumHp);
        var plan = new HitPlan { Id = id, Kind = kind, Multiplier = 1d };
        if (mixedRank > 0 && _primeKind == (int)kind && _primeReservation is null)
        { plan.UsesPrime = true; plan.Multiplier = SkillMath.Mixed(mixedRank); _primeReservation = id; }
        if (kind == HitKind.Melee && paybackRank > 0 && _reservoir > 0 && _paybackReservation is null)
        { plan.Payback = _reservoir; _paybackReservation = id; }
        return plan;
    }
    public void Resolve(HitPlan plan, bool actualDirectHpLoss, double now, int mixedRank)
    {
        if (plan is null || plan.Resolved) return;
        plan.Resolved = true;
        if (_primeReservation == plan.Id)
        {
            _primeReservation = null;
            if (actualDirectHpLoss && plan.UsesPrime) _primeKind = -1;
        }
        if (_paybackReservation == plan.Id)
        {
            _paybackReservation = null;
            if (actualDirectHpLoss) _reservoir = Math.Max(0, _reservoir - plan.Payback);
        }
        if (actualDirectHpLoss && mixedRank > 0 && plan.Kind != HitKind.OtherRanged)
        {
            _primeKind = plan.Kind == HitKind.Melee ? (int)HitKind.ThrownWeapon : (int)HitKind.Melee;
            _primeUntil = now + 6d;
        }
    }
    public void OwnershipChanged(int mixedRank, int paybackRank)
    {
        if (mixedRank == 0) { _primeKind = -1; _primeReservation = null; }
        if (paybackRank == 0) { _reservoir = 0; _reservoirUntil = 0; _paybackReservation = null; }
    }
    private void Expire(double now, double maximumHp)
    {
        if (now >= _primeUntil) { _primeKind = -1; _primeReservation = null; }
        if (now >= _reservoirUntil) { _reservoir = 0; _paybackReservation = null; }
        _reservoir = Math.Min(_reservoir, SkillMath.PaybackCap(maximumHp));
    }
    public SkillStateSnapshot Capture(double now, double maximumHp)
    {
        Expire(now, maximumHp);
        if (_primeReservation is not null || _paybackReservation is not null)
            throw new InvalidOperationException("Cannot checkpoint unresolved direct-hit charge reservations; previous save must be preserved");
        return new SkillStateSnapshot { AttackAge = Math.Clamp(now - _lastAttack, 0, 2), PrimeKind = _primeKind,
            PrimeRemaining = Math.Max(0, _primeUntil - now), Reservoir = _reservoir, ReservoirRemaining = Math.Max(0, _reservoirUntil - now) };
    }
    public static SkillCombatState Restore(SkillStateSnapshot snapshot, double now)
    {
        if (snapshot.SchemaVersion != 1 || !double.IsFinite(snapshot.AttackAge) || snapshot.AttackAge < 0 || snapshot.AttackAge > 2 ||
            snapshot.PrimeKind < -1 || snapshot.PrimeKind > 1 || snapshot.PrimeRemaining < 0 || snapshot.PrimeRemaining > 6 ||
            snapshot.Reservoir < 0 || snapshot.ReservoirRemaining < 0 || snapshot.ReservoirRemaining > 5 ||
            !double.IsFinite(snapshot.Reservoir) || !double.IsFinite(snapshot.ReservoirRemaining) || !double.IsFinite(snapshot.PrimeRemaining))
            throw new InvalidOperationException("Unsupported/invalid skill feature state; restore requires the compatible content version");
        return new SkillCombatState(now - snapshot.AttackAge) { _primeKind = snapshot.PrimeRemaining > 0 ? snapshot.PrimeKind : -1,
            _primeUntil = now + snapshot.PrimeRemaining, _reservoir = snapshot.ReservoirRemaining > 0 ? snapshot.Reservoir : 0,
            _reservoirUntil = now + snapshot.ReservoirRemaining };
    }
}
