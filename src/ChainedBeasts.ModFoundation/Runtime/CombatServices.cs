using System;
using System.Collections.Generic;

namespace ChainedBeasts.ModFoundation.Runtime;

public enum DirectAttackKind { Melee, ThrownWeapon, OtherRanged }

public sealed class AttackRecord
{
    public string Id { get; internal set; }
    public Player Player { get; internal set; }
    public DirectAttackKind Kind { get; internal set; }
    public double BeforeCostStamina01 { get; internal set; }
    public PhysicalWeapon Weapon { get; internal set; }
    // Snapshot before the attack pays native durability costs (throws pay at launch).
    // The mutable PhysicalWeapon remains authoritative for later spending/identity.
    public int WeaponDurabilityAtCommit { get; internal set; }
    public int WeaponMaximumDurabilityAtCommit { get; internal set; }
    public bool WeaponIsDefaultAtCommit { get; internal set; } = true;
    public bool IsUnarmed { get; internal set; }
    internal Dictionary<IntPtr, bool> Backstabs { get; } = new();
    internal bool CostCaptured;
    public Dictionary<string, object> Tags { get; } = new(StringComparer.Ordinal);
}

public sealed class DirectHit
{
    public string Id { get; internal set; }
    public AttackRecord Attack { get; internal set; }
    public NetworkHealthObject Target { get; internal set; }
    public string TargetId { get; internal set; }
    public ulong TargetOrder { get; internal set; }
    public DirectAttackKind Kind { get; internal set; }
    public bool TargetIsHostileEnemy { get; internal set; }
    public bool IsBackstab { get; internal set; }
    public int HealthBefore { get; internal set; }
    public int EffectiveMaximumBefore { get; internal set; }
    public double Damage { get; set; }
    public double FlatDamageBonus { get; set; }
    public void MultiplyDamage(double multiplier) => Damage = Logic.DamageComposition.MultiplyExceptFlat(Damage, FlatDamageBonus, multiplier);
    public int ActualHpLoss { get; internal set; }
    public bool Resolved { get; internal set; }
    public Dictionary<string, object> Tags { get; } = new(StringComparer.Ordinal);
}

public sealed class ShieldBlock
{
    public string Id { get; internal set; }
    public Player Player { get; internal set; }
    public PickupData Shield { get; internal set; }
    public int AdditionalDurabilityLoss { get; set; }
}

/// <summary>Owner direct contacts and server-confirmed HP-loss events. No generated-effect recursion.</summary>
public static class CombatServices
{
    public static int ConfirmedHealth(Player player) => Networking.NetworkCombat.Current?.Target == player.NetworkObjectId
        ? Networking.NetworkCombat.Current.After : player.health.Value;
    public static bool ConfirmedAlive(NetworkHealthObject target) => target is not null &&
        (Networking.NetworkCombat.Current?.Target == target.NetworkObjectId
            ? !Networking.NetworkCombat.Current.Dead && Networking.NetworkCombat.Current.After > 0
            : !target.IsDead && target.health.Value > 0);
    public static event Action<Player, string, double> AbilityActivated;
    public static void NotifyActivation(Player player, string contentKey, double actualBenefit = 1)
    {
        if (!NativeCombat.Active || NativePersistence.IsRestoring || player?.IsOwner != true || !ConfirmedAlive(player) ||
            !double.IsFinite(actualBenefit) || actualBenefit <= 0 || string.IsNullOrEmpty(contentKey) ||
            !FoundationServices.Content.TryGet(contentKey, out var content) || !content.Enabled) return;
        Invoke(AbilityActivated, callback => callback(player, contentKey, actualBenefit));
    }
    public static void RestoreHealthGrant(Player player, string contentKey, int rawBaseAmount) => NativeHealthGrants.Request(player, contentKey, rawBaseAmount);
    public static event Action<AttackRecord> AttackCommitted;
    public static event Action<DirectHit> PreparingDirectHit;
    public static event Action<DirectHit> DirectHitResolved;
    public static event Action<AttackRecord, int> AttackHpConfirmed;
    public static event Action<Player, int> EnemyHpLost;
    public static event Action<Player> WeaponBroken;
    public static event Action<Player, Enemy, string> ExecutionConfirmed;
    public static event Action<Player, Weapon, PhysicalWeapon, bool> WeaponRecovered;
    public static event Func<Player, double, double> IncomingDamage;
    public static event Action<Player, double, bool> StaminaSpent;
    public static event Action<Player> RecoveryDelayUpdating;
    public static event Func<AttackRecord, double> MeleePaymentMultiplier;
    public static event Action<ShieldBlock> ShieldBlocked;
    public static event Action<Player> DodgeCompleted;
    public static event Func<Player, double> CrowdFavourMultiplier;
    public static event Func<AttackRecord, NetworkAgent, double> OutgoingKnockbackMultiplier;
    public static bool IsSpecialMeleePayment => NativeCombat.SpecialPayment;
    public static AttackRecord CurrentDirectAttack => NativeCombat.CurrentAttack;
    /// <summary>Genuine held-weapon throw windup, including flails; excludes primary attacks and offhands.</summary>
    public static bool IsPreparingNormalThrow(Player player) => NativeThrowing.IsPreparingNormalThrow(player);
    internal static double KnockbackMultiplier(AttackRecord attack, NetworkAgent target) => Product(OutgoingKnockbackMultiplier, callback => callback(attack, target));
    /// <summary>Explicit generated damage never inherits a direct-contact scope.</summary>
    public static void DealGeneratedDamage(Player source, NetworkHealthObject target, int amount) => NativeCombat.GeneratedDamage(source, target, amount);
    public static NetworkHealthObject FindLiveTarget(string token) => NativeCombat.FindLiveTarget(token);
    public static void RestoreStaminaGrant(Player player, double amount)
        => RestoreStaminaGrantWithReceipt(player, amount);
    public static double RestoreStaminaGrantWithReceipt(Player player, double amount)
    {
        if (!NativeCombat.Active || player is null || !player.IsOwner || !ConfirmedAlive(player) || !double.IsFinite(amount)) return 0;
        var before = player.Stamina;
        var grant = Math.Clamp(amount, 0, Math.Max(0, player.StaminaMax - player.Stamina));
        if (grant > 0) NativeActionEvents.GrantStamina(player, (float)grant);
        return Math.Max(0, player.Stamina - before);
    }
    internal static bool NeedsDirectContacts => PreparingDirectHit is not null || DirectHitResolved is not null;

    internal static void Commit(AttackRecord attack) => Invoke(AttackCommitted, action => action(attack));
    internal static void Prepare(DirectHit hit) => Invoke(PreparingDirectHit, action => action(hit));
    internal static void Resolve(DirectHit hit, int loss)
    {
        if (hit is null || hit.Resolved) return;
        hit.Resolved = true; hit.ActualHpLoss = Math.Max(0, loss);
        Invoke(DirectHitResolved, action => action(hit));
    }
    internal static void ConfirmAttack(AttackRecord attack, int loss)
    { if (attack?.Player?.IsOwner == true && loss > 0) Invoke(AttackHpConfirmed, callback => callback(attack, loss)); }
    internal static void LostHp(Player player, int loss)
    { if (loss > 0) Invoke(EnemyHpLost, action => action(player, loss)); }
    internal static void Broken(Player player) => Invoke(WeaponBroken, action => action(player));
    internal static void Executed(Player player, Enemy enemy, string targetId) => Invoke(ExecutionConfirmed, action => action(player, enemy, targetId));
    internal static void Recovered(Player player, Weapon weapon, PhysicalWeapon physical, bool ownThrow) =>
        Invoke(WeaponRecovered, action => action(player, weapon, physical, ownThrow));
    public static void ConsumeOpeningDurability(PhysicalWeapon weapon) => NativeWeaponIdentity.ExtraDurability(weapon);
    public static string TargetIdentity(NetworkHealthObject target) => NativeCombat.TargetIdentity(target);
    public static bool HasLiveTarget(string token) => NativeCombat.HasLiveTarget(token);
    internal static void Spent(Player player, double actual, bool skipDelay) => Invoke(StaminaSpent, callback => callback(player, actual, skipDelay));
    internal static void UpdatingRecovery(Player player) => Invoke(RecoveryDelayUpdating, callback => callback(player));
    internal static void Blocked(ShieldBlock block) => Invoke(ShieldBlocked, callback => callback(block));
    internal static void Dodged(Player player) => Invoke(DodgeCompleted, callback => callback(player));
    internal static double PaymentMultiplier(AttackRecord attack) => Product(MeleePaymentMultiplier, callback => callback(attack));
    internal static double FavourMultiplier(Player player) => Product(CrowdFavourMultiplier, callback => callback(player));
    private static double Product<T>(T callbacks, Func<T, double> invoke) where T : Delegate
    {
        var value = 1d;
        Invoke(callbacks, callback => { var contribution = invoke(callback); if (!double.IsFinite(contribution) || contribution < 0) throw new InvalidOperationException("Invalid combat multiplier"); value *= contribution; });
        return double.IsFinite(value) ? value : 1d;
    }
    internal static double Reduce(Player player, double amount)
    {
        if (IncomingDamage is null) return amount;
        foreach (Func<Player, double, double> callback in IncomingDamage.GetInvocationList())
            try { amount = callback(player, amount); }
            catch (Exception exception) { FoundationServices.Logger?.LogError("[Foundation] incoming damage subscriber failed: " + exception); }
        return amount;
    }
    private static void Invoke<T>(T callbacks, Action<T> invoke) where T : Delegate
    {
        if (callbacks is null) return;
        foreach (var callback in callbacks.GetInvocationList())
            try { invoke((T)callback); }
            catch (Exception exception) { FoundationServices.Logger?.LogError("[Foundation] combat subscriber failed: " + exception); }
    }
}
