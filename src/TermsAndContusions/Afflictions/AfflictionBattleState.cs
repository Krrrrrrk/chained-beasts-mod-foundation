using System;

namespace TermsAndContusions.Afflictions;

public sealed class AfflictionSnapshot
{
    public int SchemaVersion { get; set; } = 1;
    public double BattleAge { get; set; }
    public double ShakyRemaining { get; set; }
    public double LooseRemaining { get; set; }
    public double LandingRemaining { get; set; }
    public string FocusTarget { get; set; }
    public double FocusRemaining { get; set; }
    public int BlockCount { get; set; }
    public int OverheatStacks { get; set; }
    public double AttackAge { get; set; } = 2;
    public bool LastWasMelee { get; set; }
}

public sealed class AfflictionAttack
{
    public int OverheatStacks { get; internal set; }
    internal int OverheatEpoch;
    internal bool FocusCaptured;
    internal string PreviousFocus;
    internal double PreviousExpiry;
    internal int FocusEpoch;
    internal string ChosenTarget;
    internal ulong ChosenOrder = ulong.MaxValue;
}

/// <summary>Per player/battle. No native resource writes, grants or effects occur during capture/restore.</summary>
public sealed class AfflictionBattleState
{
    private readonly double _battleStart;
    private double _shakyUntil, _looseUntil, _landingUntil, _focusUntil;
    private string _focus;
    private int _blocks, _overheat, _focusEpoch, _overheatEpoch;
    private double _lastAttack = double.NegativeInfinity;
    private bool _lastMelee;
    public string FocusTarget => _focus;
    public AfflictionBattleState(double battleStart) { _battleStart = battleStart; }
    public double BattleAge(double now) => Math.Clamp(now - _battleStart, 0, 10);
    public double Movement(double now, int severity) => AfflictionMath.Landing(severity, now < _landingUntil);
    public void Sync(int brittle, int heavy, int focus, int overheat)
    {
        _blocks = brittle <= 0 ? 0 : Math.Min(_blocks, AfflictionMath.BlockInterval(brittle) - 1);
        if (heavy <= 0) _landingUntil = 0;
        if (focus <= 0 && _focus is not null) ClearFocus();
        if (overheat <= 0) { if (_lastMelee || _overheat > 0) _overheatEpoch++; _overheat = 0; _lastMelee = false; }
    }
    public void ClearFocus() { _focus = null; _focusUntil = 0; _focusEpoch++; }
    public bool Block(int severity)
    {
        var interval = AfflictionMath.BlockInterval(severity);
        if (interval == 0) { _blocks = 0; return false; }
        if (++_blocks < interval) return false;
        _blocks = 0; return true;
    }
    public bool Shaky(double now, int severity, int actualEnemyLoss)
    { if (severity <= 0 || actualEnemyLoss <= 0 || now < _shakyUntil) return false; _shakyUntil = now + 1; return true; }
    public bool Loose(double now, int severity, int actualEnemyLoss)
    { if (severity <= 0 || actualEnemyLoss <= 0 || now < _looseUntil) return false; _looseUntil = now + 3; return true; }
    public void DodgeCompleted(double now, int severity) { if (severity > 0) _landingUntil = now + .75d; }
    public AfflictionAttack Commit(double now, bool melee, int overheatSeverity)
    {
        _overheat = melee && overheatSeverity > 0 ? _lastMelee && now - _lastAttack < 2d ? Math.Min(3, _overheat + 1) : 0 : 0;
        _lastAttack = now; _lastMelee = melee && overheatSeverity > 0;
        return new AfflictionAttack { OverheatStacks = _overheat, OverheatEpoch = _overheatEpoch };
    }
    public double MeleeMultiplier(AfflictionAttack attack, int severity) => attack.OverheatEpoch == _overheatEpoch ? AfflictionMath.MeleeCost(severity, attack.OverheatStacks) : 1d;
    public double FocusMultiplier(AfflictionAttack attack, string target, double now, int severity)
    {
        if (!attack.FocusCaptured)
        { attack.FocusCaptured = true; attack.PreviousFocus = _focus; attack.PreviousExpiry = _focusUntil; attack.FocusEpoch = _focusEpoch; }
        return severity > 0 && target is not null && attack.FocusEpoch == _focusEpoch && attack.PreviousFocus is not null && now < attack.PreviousExpiry && target != attack.PreviousFocus
            ? AfflictionMath.Focus(severity) : 1d;
    }
    public void SuccessfulDirectHit(AfflictionAttack attack, string target, ulong stableOrder, double now, int severity, int actualHpLoss)
    {
        if (severity <= 0 || actualHpLoss <= 0 || string.IsNullOrEmpty(target)) return;
        // All targets of one action used the same prior focus. Select its next focus independently of callback order.
        if (attack.ChosenTarget is null || stableOrder < attack.ChosenOrder || stableOrder == attack.ChosenOrder && string.CompareOrdinal(target, attack.ChosenTarget) < 0)
        { attack.ChosenOrder = stableOrder; attack.ChosenTarget = target; }
        _focus = attack.ChosenTarget; _focusUntil = now + 3d;
    }
    public AfflictionSnapshot Capture(double now) => new() { BattleAge = BattleAge(now), ShakyRemaining = Math.Clamp(_shakyUntil - now, 0, 1),
        LooseRemaining = Math.Clamp(_looseUntil - now, 0, 3), LandingRemaining = Math.Clamp(_landingUntil - now, 0, .75),
        FocusTarget = now < _focusUntil ? _focus : null, FocusRemaining = Math.Clamp(_focusUntil - now, 0, 3), BlockCount = _blocks,
        OverheatStacks = now - _lastAttack < 2d ? _overheat : 0, AttackAge = Math.Clamp(now - _lastAttack, 0, 2), LastWasMelee = _lastMelee && now - _lastAttack < 2d };
    public static AfflictionBattleState Restore(AfflictionSnapshot value, double now)
    {
        static bool Valid(double value, double maximum) => double.IsFinite(value) && value >= 0 && value <= maximum;
        if (value is null || value.SchemaVersion != 1 || !Valid(value.BattleAge, 10) || !Valid(value.ShakyRemaining, 1) || !Valid(value.LooseRemaining, 3) ||
            !Valid(value.LandingRemaining, .75) || !Valid(value.FocusRemaining, 3) || !Valid(value.AttackAge, 2) || value.BlockCount < 0 || value.BlockCount > 3 ||
            value.OverheatStacks < 0 || value.OverheatStacks > 3 || value.FocusTarget is not null && (string.IsNullOrWhiteSpace(value.FocusTarget) || value.FocusTarget.Length > 200) ||
            value.FocusRemaining > 0 && value.FocusTarget is null || !value.LastWasMelee && value.OverheatStacks != 0)
            throw new InvalidOperationException("Unsupported or invalid affliction feature state");
        return new AfflictionBattleState(now - value.BattleAge) { _shakyUntil = now + value.ShakyRemaining, _looseUntil = now + value.LooseRemaining,
            _landingUntil = now + value.LandingRemaining, _focus = value.FocusTarget, _focusUntil = now + value.FocusRemaining, _blocks = value.BlockCount,
            _overheat = value.OverheatStacks, _lastAttack = now - value.AttackAge, _lastMelee = value.LastWasMelee };
    }
}

/// <summary>The extra delay is a tail of the native recovery timer; its elapsed part is never re-added by a read.</summary>
public sealed class ExertionDelayState
{
    private double _remaining, _consumed;
    private bool _armed;
    public double Begin(double nativeTimer, double extra)
    { _remaining = Math.Max(0, extra); _consumed = 0; _armed = true; return Math.Max(0, nativeTimer) + _remaining; }
    public double Reconcile(double timer, double currentExtra)
    {
        timer = Math.Max(0, timer);
        if (!_armed) return timer;
        var remaining = Math.Min(_remaining, timer);
        _consumed += _remaining - remaining;
        if (timer == 0) { _armed = false; _remaining = 0; return 0; }
        var updated = Math.Max(0, currentExtra - _consumed);
        var result = Math.Max(0, timer - remaining + updated);
        _remaining = updated;
        if (currentExtra <= 0) _armed = false; // Cure/suppression removes this exertion's pending addon permanently.
        return result;
    }
}
