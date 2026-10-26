# Surgeries

Surgeries join a shared eligible surgeon pool, preserve native payment and ownership history, and can be acquired once per player per run. Each effect includes its tradeoff. Existing saved offers are not rewritten by configuration changes.

| Content / ID | Full scaling and limits | Setting |
|---|---|---|
| Adrenal Graft / 11020 | Stamina regenerates 25% faster for the rest of this run. | `[AdrenalGraft] EnableAdrenalGraft` |
| Anchor Heels / 11021 | Receive 75% less knockback. Dodges travel 20% less distance. | `[Surgeries] EnableAnchorHeels` |
| Spring-Loaded Knees / 11022 | Dodges travel 30% farther but cost 20% more stamina. | `[Surgeries] EnableSpringLoadedKnees` |
| Dense Skeleton / 11023 | Take 15% less damage. Maximum stamina is reduced by 20%. | `[Surgeries] EnableDenseSkeleton` |
| Auxiliary Tendons / 11024 | Regular melee attacks cost 20% less stamina. Maximum health is reduced by 10%. | `[Surgeries] EnableAuxiliaryTendons` |
| Marrow Enrichment / 11025 | Healing is increased by 30%. Maximum health is reduced by 15%. | `[Surgeries] EnableMarrowEnrichment` |
| Pain Converter / 11026 | Enemy damage restores stamina: 1.5 times the fraction of maximum health lost, up to 25% maximum stamina per hit. Maximum stamina is reduced by 10%. | `[Surgeries] EnablePainConverter` |
| Emergency Clotting / 11027 | Once per battle, surviving an enemy hit that removes at least 20% maximum health heals 25% of that loss over 4 seconds. | `[Surgeries] EnableEmergencyClotting` |
| Counterweight Implant / 11028 | After recovering from a stun or trip, your next melee hit within 5 seconds deals 25% more damage and 75% more knockback. | `[Surgeries] EnableCounterweightImplant` |
| Fury Pacemaker / 11029 | Prevent future fury gains. Instead, an eligible fury grant restores 30% maximum stamina, at most once every 8 seconds. | `[Surgeries] EnableFuryPacemaker` |
| Retractable Talons / 11030 | Unarmed hits bleed for 5 damage over 3 seconds. Repeated hits refresh the bleed. Weapon damage is reduced by 10%. | `[Surgeries] EnableRetractableTalons` |

For implementation details, use the [content index](manifest.json), [shared mechanics and events](../foundation/MECHANICS.md) and [adding-content guide](../ADDING_CONTENT.md).

Normal offers are the default. Testing overrides require the developer-offer master and the relevant Force option. Actual gameplay, UI layout and save/resume interactions still need manual verification for your chosen combination.
