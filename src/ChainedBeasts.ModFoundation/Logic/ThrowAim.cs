namespace ChainedBeasts.ModFoundation.Logic;

// Transient provenance from the actual normal-throw coroutine, never persisted.
// Ranged windup alone also describes a bow/flail primary attack.
public readonly record struct ThrowAim(long Weapon, double AttackStarted)
{
    public bool Matches(long currentWeapon, double currentAttackStarted, bool windup, bool ranged, bool offhand) =>
        Weapon != 0 && Weapon == currentWeapon && AttackStarted == currentAttackStarted && windup && ranged && !offhand;
}
