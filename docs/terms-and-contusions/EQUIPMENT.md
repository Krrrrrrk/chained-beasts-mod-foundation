# Equipment

These entries use the normal single equipment slot and eligible shop pool. Bonuses apply while equipped. Per-battle or per-weapon allowances are not refreshed by swapping equipment. Counterweight Sling and Longshot multiply their range and charge-speed factors.

| Content / ID | Full scaling and limits | Setting |
|---|---|---|
| Runner’s Wraps / 2499 | Dodge stamina cost x0.75 while equipped. | `[RunnersWraps] EnableRunnersWraps` |
| Executioner’s Hood / 2488 | Deal 35% more melee damage to enemies below 30% health before the hit. | `[Equipment] EnableExecutionersHood` |
| Lead-Lined Purse / 2489 | Take 1% less damage per 10 gold carried, up to 20% less. Spending gold updates this immediately. | `[Equipment] EnableLeadLinedPurse` |
| Borrower’s Scabbard / 2490 | A full-durability weapon's first successful hit each battle deals 50% more damage and costs one additional durability. Repairing or re-equipping does not reset it. | `[Equipment] EnableBorrowersScabbard` |
| Butcher’s Apron / 2491 | A confirmed execution heals 3% of maximum HP, within your attainable health limit. | `[Equipment] EnableButchersApron` |
| Last-Gasp Locket / 2492 | Once per battle, enemy damage leaving you alive below 25% HP restores 50% of maximum stamina. | `[Equipment] EnableLastGaspLocket` |
| Mercenary’s Ledger / 2493 | If equipped at battle start, pay 5 gold for 20% more damage that battle while equipped. No funds means no bonus; re-equipping never charges again. | `[Equipment] EnableMercenarysLedger` |
| Pickpocket’s Mitts / 2494 | The first successful backstab against each enemy grants 1 gold, up to 10 gold per battle. Re-equipping does not reset rewards. | `[Equipment] EnablePickpocketsMitts` |
| Chalk Bag / 2495 | Aim a normal throw while stationary for 1.5 seconds to deal 40% more damage with that throw. Movement cancels preparation; releasing consumes it. | `[Equipment] EnableChalkBag` |
| Salvager’s Hook / 2496 | Recovering a weapon you previously threw repairs one durability, once per physical weapon per battle, up to its normal maximum. | `[Equipment] EnableSalvagersHook` |
| Duelist’s Spurs / 2497 | Deal 20% more melee damage when your target is the only living hostile enemy within 4 metres of you. | `[Equipment] EnableDuelistsSpurs` |
| Counterweight Sling / 2498 | Normal throw range and charge speed x2 while equipped; charge time /2. Multiplies Longshot's 1+r contribution. | `[CounterweightSling] EnableCounterweightSling` |

For implementation details, use the [content index](manifest.json), [shared mechanics and events](../foundation/MECHANICS.md) and [adding-content guide](../ADDING_CONTENT.md).

Normal offers are the default. Testing overrides require the developer-offer master and the relevant Force option. Actual gameplay, UI layout and save/resume interactions still need manual verification for your chosen combination.
