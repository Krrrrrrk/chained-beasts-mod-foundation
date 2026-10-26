using System;
using System.Collections.Generic;
using System.Linq;

namespace ChainedBeasts.ModFoundation.Stats;

public enum StatChannel { MaximumHealth, MovementSpeed, DodgeStaminaCost, StaminaRecoveryRate, AttackDamage, RecoveryDelayClockRate, ThrowRange, ThrowChargeSpeed, RegenerationAmount, MaximumStamina, DodgeDistance, IncomingKnockback, HealingAmount }

/// <summary>Contributions are recomputed from current state and an otherwise-normal value.</summary>
public sealed class ModifierPipeline<TContext>
{
    private readonly Dictionary<string, (StatChannel Channel, Func<TContext, double> Factor)> _rules = new(StringComparer.Ordinal);
    public void Register(string key, StatChannel channel, Func<TContext, double> factor)
    {
        if (string.IsNullOrWhiteSpace(key) || !key.Contains(':') || factor is null) throw new ArgumentException("Invalid modifier identity.");
        if (!_rules.TryAdd(key, (channel, factor))) throw new InvalidOperationException("Modifier key collision: " + key);
    }
    public bool Remove(string key) => _rules.Remove(key);
    public double Evaluate(StatChannel channel, double otherwiseNormal, TContext context)
    {
        var result = otherwiseNormal;
        foreach (var rule in _rules.OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            if (rule.Value.Channel != channel) continue;
            var factor = rule.Value.Factor(context);
            if (!double.IsFinite(factor) || factor < 0) throw new InvalidOperationException("Invalid modifier factor: " + rule.Key);
            result *= factor;
            if (!double.IsFinite(result)) throw new InvalidOperationException("Modifier result overflow: " + rule.Key);
        }
        return result;
    }
}
