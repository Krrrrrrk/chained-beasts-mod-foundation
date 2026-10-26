using System;
using System.Collections.Generic;
using System.Linq;

namespace TermsAndContusions.Surgeries;

public sealed class BleedSnapshot
{
    public string Target { get; set; }
    public double Remaining { get; set; }
    public double NextTick { get; set; }
}
public sealed class SurgerySnapshot
{
    public int SchemaVersion { get; set; } = 1;
    public bool ClotUsed { get; set; }
    public int ClotTotal { get; set; }
    public int ClotPaid { get; set; }
    public int ClotTicks { get; set; }
    public double ClotNext { get; set; }
    public double CounterweightRemaining { get; set; }
    public double FuryRemaining { get; set; }
    public bool WasIncapacitated { get; set; }
    public BleedSnapshot[] Bleeds { get; set; } = Array.Empty<BleedSnapshot>();
}
/// <summary>One player's timers/charges. Ownership is exclusively native previousSurgeries, never this object.</summary>
public sealed class SurgeryBattleState
{
    private sealed class Bleed { internal double Until, Next; }
    private readonly Dictionary<string, Bleed> _bleeds = new(StringComparer.Ordinal);
    private double _counterUntil, _furyUntil, _clotNext;
    private string _counterReservation;
    private bool _clotUsed, _wasIncapacitated;
    private int _clotTotal, _clotPaid, _clotTicks;
    public bool HasPendingContact => _counterReservation is not null;
    public bool HasBleeds => _bleeds.Count > 0;
    public void RebaseIncapacitation(bool incapacitated) => _wasIncapacitated = incapacitated;
    public void ResetBattle() { DiscardLiveEffects(); _clotUsed = false; }
    public void Recovery(bool incapacitated, bool owned, double now, bool fullyRecovered = true)
    {
        if (incapacitated) { _wasIncapacitated = true; return; }
        if (!_wasIncapacitated || !fullyRecovered) return;
        _wasIncapacitated = false; if (owned) _counterUntil = now + 5;
    }
    public bool ReserveCounterweight(string hit, bool owned, double now)
    {
        if (!owned || string.IsNullOrEmpty(hit) || now >= _counterUntil || _counterReservation is not null) return false;
        _counterReservation = hit; return true;
    }
    public bool ResolveCounterweight(string hit, int actualLoss)
    {
        if (_counterReservation != hit || hit is null) return false;
        _counterReservation = null;
        if (actualLoss <= 0) return false;
        _counterUntil = 0; return true;
    }
    public bool Fury(bool owned, double now)
    { if (!owned || now < _furyUntil) return false; _furyUntil = now + 8; return true; }
    public bool Clot(bool owned, int actualLoss, int maximum, bool alive, double now)
    {
        if (!owned || _clotUsed || !alive || maximum <= 0 || actualLoss <= 0 || actualLoss < maximum * .2d) return false;
        _clotUsed = true; _clotTotal = (int)Math.Round(actualLoss * .25d, MidpointRounding.AwayFromZero); _clotPaid = 0; _clotTicks = 0; _clotNext = now + 1;
        return true;
    }
    public int ClotTick(double now)
    {
        var grant = 0;
        while (_clotTotal > 0 && _clotTicks < 4 && now + 1e-8 >= _clotNext)
        {
            _clotTicks++; _clotNext += 1;
            var cumulative = (int)Math.Floor(_clotTotal * _clotTicks / 4d);
            grant += cumulative - _clotPaid; _clotPaid = cumulative;
        }
        return grant;
    }
    public void BleedHit(string target, double now)
    {
        if (string.IsNullOrWhiteSpace(target)) return;
        if (_bleeds.TryGetValue(target, out var bleed) && bleed.Until > now)
        { bleed.Until = now + 3; return; } // Keep cadence; refresh never emits a tick.
        _bleeds[target] = new Bleed { Until = now + 3, Next = now + .6 };
    }
    public Dictionary<string, int> BleedTick(double now)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in _bleeds.ToArray())
        {
            var amount = 0; var bleed = pair.Value;
            while (bleed.Next <= Math.Min(now, bleed.Until) + 1e-8 && amount < 5) { amount++; bleed.Next += .6; }
            if (amount > 0) result.Add(pair.Key, amount);
            if (now >= bleed.Until) _bleeds.Remove(pair.Key);
        }
        return result;
    }
    public void RemoveBleed(string target) => _bleeds.Remove(target);
    public void DiscardLiveEffects() { _bleeds.Clear(); _clotTotal = _clotPaid = _clotTicks = 0; _counterUntil = 0; _counterReservation = null; _wasIncapacitated = false; }
    public SurgerySnapshot Capture(double now)
    {
        if (HasPendingContact) throw new InvalidOperationException("Unsettled surgery contact cannot be checkpointed.");
        return new SurgerySnapshot { ClotUsed = _clotUsed, ClotTotal = _clotTotal, ClotPaid = _clotPaid, ClotTicks = _clotTicks,
            ClotNext = _clotTicks < 4 && _clotTotal > 0 ? Math.Clamp(_clotNext - now, 0, 1) : 0,
            CounterweightRemaining = Math.Clamp(_counterUntil - now, 0, 5), FuryRemaining = Math.Clamp(_furyUntil - now, 0, 8), WasIncapacitated = _wasIncapacitated,
            Bleeds = _bleeds.Where(pair => pair.Value.Until > now).Select(pair => new BleedSnapshot { Target = pair.Key,
                Remaining = Math.Clamp(pair.Value.Until - now, 0, 3), NextTick = Math.Clamp(pair.Value.Next - now, 0, .6) }).ToArray() };
    }
    public static SurgeryBattleState Restore(SurgerySnapshot value, double now)
    {
        static bool Range(double number, double maximum) => double.IsFinite(number) && number >= 0 && number <= maximum + 1e-8;
        if (value is null || value.SchemaVersion != 1 || !Range(value.ClotNext, 1) || !Range(value.CounterweightRemaining, 5) || !Range(value.FuryRemaining, 8) ||
            value.ClotTotal < 0 || value.ClotPaid < 0 || value.ClotPaid > value.ClotTotal || value.ClotTicks < 0 || value.ClotTicks > 4 ||
            (!value.ClotUsed && (value.ClotTotal != 0 || value.ClotPaid != 0 || value.ClotTicks != 0)) ||
            value.ClotPaid != (int)Math.Floor(value.ClotTotal * (double)value.ClotTicks / 4d) || value.Bleeds is null || value.Bleeds.Length > 4096 ||
            value.Bleeds.Any(bleed => bleed is null || string.IsNullOrWhiteSpace(bleed.Target) || !Range(bleed.Remaining, 3) || bleed.Remaining <= 0 || !Range(bleed.NextTick, .6)) ||
            value.Bleeds.Select(bleed => bleed.Target).Distinct(StringComparer.Ordinal).Count() != value.Bleeds.Length)
            throw new InvalidOperationException("Unsupported or malformed surgery state; restore refused.");
        var state = new SurgeryBattleState { _clotUsed = value.ClotUsed, _clotTotal = value.ClotTotal, _clotPaid = value.ClotPaid,
            _clotTicks = value.ClotTicks, _clotNext = now + value.ClotNext, _counterUntil = now + value.CounterweightRemaining,
            _furyUntil = now + value.FuryRemaining, _wasIncapacitated = value.WasIncapacitated };
        foreach (var bleed in value.Bleeds) state._bleeds.Add(bleed.Target, new Bleed { Until = now + bleed.Remaining, Next = now + bleed.NextTick });
        return state;
    }
}
