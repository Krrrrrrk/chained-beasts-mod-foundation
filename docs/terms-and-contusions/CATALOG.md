# Terms & Contusions content catalog

The 0.9.7 release contains 47 additions. Iron Grip worked in my manual test; broader interaction and multiplayer cases still need checking. Custom card descriptions start with `TC:`. Twelve skills have four ranks and Iron Grip has one; potion afflictions have three severities. The tables below show the first-rank/severity description; category guides give the full scaling and limits.

These are unbalanced experimental Solo examples. Use the [installation guide](../INSTALLATION.md) and keep matching saves, sidecars and mods together. The [content index](manifest.json) lists stable IDs, settings and source files.

## Skills

[Scaling and mechanics](SKILLS.md)

| Content / ID | First-rank or severity-one effect | Cost |
|---|---|---|
| Bulk Up / 11000 | TC: 2x maximum health. Keep your health percentage. | Native rules |
| Measured Violence / 11001 | TC: +25% melee damage when attacking at 80%+ stamina. | Native rules |
| Running on Fumes / 11002 | TC: +35% melee damage when attacking below 25% stamina. | Native rules |
| Catch Your Breath / 11003 | TC: Stamina recovery delay: 1/2 while standing still. | Native rules |
| Expensive Taste / 11004 | TC: +1% damage per 10 gold. Maximum +25%. | Native rules |
| Fresh Meat / 11005 | TC: +30% damage to full-health enemies. | Native rules |
| Pick Your Moment / 11006 | TC: After 2s without attacking, your next attack deals +50% damage. | Native rules |
| Mixed Martial Arts / 11007 | TC: Alternate melee and thrown hits for +30% damage. Bonus lasts 6s. | Native rules |
| Payback Plan / 11008 | TC: Store 50% of enemy damage for your next melee hit. Maximum 25% max HP; expires after 5s. | Native rules |
| Waste Not / 11009 | TC: Breaking a weapon restores 25% stamina. | Native rules |
| Breathing Room / 11010 | TC: Take 5% less damage per nearby enemy (3m). Maximum 20%. | Native rules |
| Longshot / 11011 | TC: 2x throw range and charge speed. | Native rules |
| Iron Grip / 11012 | TC: Taking hits no longer makes you drop carried enemies. | Native rules; one rank |

## Equipment

[Scaling and mechanics](EQUIPMENT.md)

| Content / ID | First-rank or severity-one effect | Cost |
|---|---|---|
| Runner’s Wraps / 2499 | TC: Dodges use 25% less stamina while equipped. | 40 gold |
| Executioner’s Hood / 2488 | TC: +35% melee damage to enemies below 30% health. | 55 gold |
| Lead-Lined Purse / 2489 | TC: Take 1% less damage per 10 gold. Maximum 20%. | 50 gold |
| Borrower’s Scabbard / 2490 | TC: A full-durability weapon's first hit each battle: +50% damage, +1 durability cost. | 60 gold |
| Butcher’s Apron / 2491 | TC: Executions restore 3% health. | 55 gold |
| Last-Gasp Locket / 2492 | TC: Once per battle: survive an enemy hit below 25% health to restore 50% stamina. | 60 gold |
| Mercenary’s Ledger / 2493 | TC: Pay 5 gold at battle start for +20% damage that battle. | 50 gold |
| Pickpocket’s Mitts / 2494 | TC: Your first backstab against each enemy grants 1 gold. Maximum 10 per battle. | 45 gold |
| Chalk Bag / 2495 | TC: Aim a throw while still for 1.5s to deal +40% damage. | 45 gold |
| Salvager’s Hook / 2496 | TC: Recover your thrown weapon to repair 1 durability. Once per weapon each battle. | 65 gold |
| Duelist’s Spurs / 2497 | TC: +20% melee damage when only one enemy is within 4m. | 50 gold |
| Counterweight Sling / 2498 | TC: Doubles throw range and charge speed. | 45 gold |

## Potion afflictions

[Scaling and mechanics](AFFLICTIONS.md)

| Content / ID | First-rank or severity-one effect | Cost |
|---|---|---|
| Leadfoot / 11010 | TC: Movement speed reduced 10/20/30% at severity 1/2/3. | Native rules |
| Lead Belly / 11011 | TC: Stamina starts recovering 0.25s later. | Native rules |
| Brittle Guard / 11012 | TC: Every 4 shield blocks cost 1 extra durability. | Native rules |
| Shaky Resolve / 11013 | TC: Enemy damage drains 5% stamina (1s cooldown). | Native rules |
| Tin Ear / 11014 | TC: Your actions generate 15% less crowd favor. | Native rules |
| Burning Lungs / 11015 | TC: Stamina recovers 20% slower while moving. | Native rules |
| Heavy Landing / 11016 | TC: Move 20% slower for 0.75s after dodging. | Native rules |
| Loose Change / 11017 | TC: Enemy damage costs 1 gold (3s cooldown). | Native rules |
| Cold Start / 11018 | TC: Deal 10% less damage during the first ten seconds of each battle. | Native rules |
| Obsessive Focus / 11019 | TC: After hitting an enemy, deal 15% less damage to others for 3s. | Native rules |
| Overheating / 11020 | TC: Repeated melee attacks cost +5% stamina (3 stacks). Rest 2s to reset. | Native rules |

## Surgeries

[Scaling and mechanics](SURGERIES.md)

| Content / ID | First-rank or severity-one effect | Cost |
|---|---|---|
| Adrenal Graft / 11020 | TC: Stamina regenerates 25% faster. | 0 gold |
| Anchor Heels / 11021 | TC: Receive 75% less knockback. Dodges travel 20% less distance. | 35 gold |
| Spring-Loaded Knees / 11022 | TC: Dodges travel 30% farther but cost 20% more stamina. | 40 gold |
| Dense Skeleton / 11023 | TC: Take 15% less damage. Maximum stamina is reduced by 20%. | 55 gold |
| Auxiliary Tendons / 11024 | TC: Regular melee attacks cost 20% less stamina. -10% maximum health. | 40 gold |
| Marrow Enrichment / 11025 | TC: +30% healing. -15% maximum health. | 40 gold |
| Pain Converter / 11026 | TC: Enemy damage restores 1.5x its health percentage as stamina (max 25%). -10% maximum stamina. | 50 gold |
| Emergency Clotting / 11027 | TC: Once per battle: survive a 20%+ health hit to heal 25% of that loss over 4s. | 45 gold |
| Counterweight Implant / 11028 | TC: After stun or trip: next melee hit gains +25% damage and +75% knockback (5s). | 45 gold |
| Fury Pacemaker / 11029 | TC: Fury gains instead restore 30% stamina (8s cooldown). | 50 gold |
| Retractable Talons / 11030 | TC: Unarmed hits bleed: 5 damage over 3s (refreshes). -10% weapon damage. | 45 gold |
