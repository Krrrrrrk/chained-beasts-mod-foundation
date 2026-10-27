# TC activation feedback and mechanics review

The **0.9.3 experimental prerelease** includes a small dust puff and swish around my local
character when a custom proc produces a confirmed benefit. It reuses an existing
game effect; there are no new assets. The cue never grants health, stamina,
damage or gold itself.

## Fresh Meat, Chalk Bag and Borrower's Scabbard — 0.9.3

This update adds a few targeted fixes and clearer feedback. My quick Solo check
looks good, but these mechanics and their interactions still need more testing.
Build and nongame checks alone don't prove a proc fired.

- **Borrower's Scabbard:** full durability is checked at attack commitment,
  before the normal throw spends durability at launch. A fresh thrown weapon can
  now qualify. Its first successful contact still has one allowance per physical
  weapon per battle; repair, re-equipping and recovery don't rearm it. Native
  default weapons don't qualify. Extra durability loss uses the normal break cap.
- **Chalk Bag:** preparation follows a genuine normal held-weapon throw windup,
  including a flail throw. Primary ranged/flail attacks and offhand throws don't
  prepare it. Stand still and aim for at least **1.5 seconds**, then release.
  Moving cancels preparation; releasing consumes it even on a miss.
- **Fresh Meat:** its existing **+30% per rank** calculation is unchanged. The
  target must be a hostile enemy at full effective HP immediately before contact.
  A qualifying hit now gives the same confirmed dust/swish cue as other procs.

Battle Numbers shows actual HP lost, so overkill can hide extra damage. A target
with 5 HP left can display only 5 even if the attack was much stronger. Some
native effects also deliberately wound enemies before the first player hit.

The log now records the first three Chalk releases and confirmed Chalk/Scabbard
procs per launch. Fresh Meat logs up to three qualifying and three nonqualifying
positive-HP hits while owned. Look for `Fresh Meat hit confirmed`, `Chalk Bag
release`, `Chalk Bag prepared throw hit confirmed`, or `Borrower's Scabbard
opening hit confirmed`. Entries include eligibility, multiplier, prepared damage
and actual HP loss where applicable. Prepared damage includes other active
bonuses; native blocking, rounding and remaining HP can change the final loss.

For a manual comparison, use comparable healthy enemies and the same picked-up
weapon/charge duration. Check Fresh Meat on the first hit, Chalk after a stationary
hold a little longer than 1.5s, and Scabbard on a newly collected full-durability
weapon's first contact. Also try an explicit flail throw. Keep both `Player.log`
and `BepInEx/LogOutput.log` if the results still look wrong. There is only one
equipment slot, so test Chalk and Scabbard separately.

## Butcher's Apron

Executions request **3% of modified maximum HP, rounded up**. For example,
60/100 HP becomes 63/100 without other healing modifiers; at 99/100 only one HP
can be restored. The request goes through the normal native healing RPC, with
Marrow Enrichment applied once and the server's actual current/attainable HP cap.
It cannot revive a dead character.

The swish plays only after a positive actual HP increase. An execution at full
attainable health produces no healing cue. In multiplayer the host confirms the
grant before the owner gets the cue. This avoids claiming healing merely because
the execution callback ran. Native execution sound/UI behavior is preserved.

## Other cues

Successful bonus hits can cue Fresh Meat, Pick Your Moment, Mixed Martial Arts, Payback Plan,
Borrower's Scabbard, Chalk Bag, Executioner's Hood, Duelist's Spurs,
Counterweight Implant and Retractable Talons. Waste Not, Last-Gasp Locket,
Pain Converter and Fury Pacemaker cue only actual stamina gains. Salvager's Hook
cues a real durability repair; Pickpocket's Mitts cues actual gold gained.
Emergency Clotting cues actual healing ticks. A fully capped resource or a
blocked/zero-damage attempt does not generate its benefit cue.

There is at most one cue per character every **0.75 seconds**, so several procs
can share one visible acknowledgement. Always-on stats such as maximum HP,
movement, recovery rate and range do not emit particles every frame. Native
acquisition cards and banners still show when passive content is acquired.
Effects are local to the owner; this doesn't broadcast particles to the party.

## Turn it off

After the first launch, close the game and edit
`BepInEx/config/local.chainedbeasts.termsandcontusions.cfg`:

```ini
[Feedback]
EnableActivationFeedback = false
```

Restart to apply. Disabling the cue does not disable any TC mechanics. The single
native effect includes its swish sound, so this switch controls both together.

## Review scope and remaining checks

All **46 entries** were reviewed against their definitions, rank/severity math,
native integration, ownership and checkpoint code. The review corrected Chalk
Bag's release-phase check, throw serialization/provenance, thrown Waste Not
break coverage, stale-client healing/survival checks, Loose Change's host
cooldown and saved guard state, and delayed Focus confirmations after cure.
The intended balance values and content IDs were preserved.

The review is based on source/native evidence and nongame checks, with a quick
Solo check of the latest fixes. **Broader Solo and host/client gameplay tests
are still needed.** Start with a damaged player
wearing Butcher's Apron, execute an NPC, and compare the real HP/HUD change and
cue. Also test full HP, Marrow Enrichment, a client's execution and the final
enemy before a transition. Then test the other procs with a missing resource;
F1 god mode/full resources can hide their benefits.

Two multiplayer limits remain explicit:

- Fresh Meat and Executioner's Hood choose their HP threshold on the attacking
  owner's target replica. Simultaneous hits under latency can change the server
  HP before the attack arrives; the later HP confirmation does not rewind that
  threshold decision.
- Physical weapon opener/repair allowances are tracked per player using local
  physical-object tokens. Solo drop/recovery and repeated contacts are covered,
  but transferring a repaired weapon between players can provide another
  player's opener. Global party-wide physical-weapon accounting is not claimed.

The [multiplayer guide](MULTIPLAYER.md) retains the matching-install, lobby,
checkpoint and rollback requirements. These changes are in the [0.9.3 experimental prerelease](https://github.com/Krrrrrrk/chained-beasts-mod-foundation/releases/tag/v0.9.3).
