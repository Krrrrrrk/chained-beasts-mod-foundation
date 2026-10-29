# TC Content menu — v1.0.0

The main menu has a **TC Content** button in the bottom-right corner, with an enabled-count badge. Version 1.0.0 includes the dark panel, content cards, category counts and unsaved-changes indicator developed in 0.9.9. The styled panel is visible in the [README screenshots](../README.md#choose-your-tc-content), with search unavailable. Broader selection, gameplay and host-client checks are still needed.

1. Open **TC Content** before starting Solo or creating/joining a multiplayer party.
2. Use **All content** or choose a category in the left sidebar, then use Previous/Next for more results. Category counts show how many entries are enabled. Search by name or effect is optional; if the panel says **Search unavailable**, use categories and pages instead.
3. Click a card to switch its **ON/OFF** badge, then click **Save changes**. There is no restart requirement. **Discard changes** or the top-right X closes without saving. Enable all and Disable all affect the entire draft, including entries hidden by a search.

The panel scales to the screen. It matches the public lobby browser and party chat: plum panels, cream text, gold headings, teal selection accents and square controls. These styles are included in TC itself; Matchmaking is optional. Save activates only for pending edits, and a confirmation appears after saving. A search-editor failure leaves the styled cards, categories and page controls available. If the styled renderer itself fails, a simple fallback panel remains accessible and the failure is logged. A panel titled **TC Content — simple view** indicates that fallback; include the logged error when reporting it.

All 47 entries default to enabled. Existing saved Enable* preferences are retained. These are the same settings in `BepInEx/config/local.chainedbeasts.termsandcontusions.cfg`; the menu does not add a second configuration file.

## Multiplayer

The **host's TC offer selection controls the party**. Clients temporarily adopt it before the party becomes ready, even if their personal checkboxes are different. Their saved preferences are not overwritten; leaving the party restores them.

Every PC needs matching **Foundation + TC 1.0.0** DLLs. The host sends the complete selection, clients validate and acknowledge it, and the normal ready barrier must finish before the host can start. Clients cannot submit a replacement host selection. Missing, malformed or mismatched profiles are refused.

Editing is locked once connected or in a run. To change the selection, leave the party, save choices at the main menu, then host again. Surgery prices and other gameplay settings outside this menu still need to match. Optional cosmetic number mods are separate from TC content selection.

## Saves and effects

The checkboxes control **newly generated offers**. An unchecked entry leaves the offer pool, while normal eligibility, rank limits, costs and selection slots remain under the game’s rules. Turning off one entry does not turn off its category.

Owned content in resumed runs keeps its effects and descriptions. Already-generated saved offers are preserved rather than rewritten; changing the selection does not erase ownership, remove debuffs, reroll a checkpoint or refund purchases. For a run entirely without Iron Grip, turn it off before starting a fresh run. Solo developer tools can still deliberately grant registered content for testing.

All definitions, names and mechanics remain registered so saves and UI can resolve them. This changes the meaning of the Enable* settings in 0.9.8: they select future offers. In published 0.9.7 and older versions, those settings are startup registration switches, and disabling owned content can block restore.

Back up DLLs, configuration and complete save data with Foundation sidecars before updating or downgrading. The menu does not edit saves. Returning to 0.9.7 also returns to its startup-only setting behavior.

## Direct training injuries

The 0.9.9 filter also covers `InjuryGenerator.GetRandomInjury`, used by training events such as Clever Monkey and by other native random injury rolls. Those do not always pass through potion generation. Vanilla injuries and skills remain eligible; for example, **Iron Stance** is a vanilla skill, not TC Iron Grip.

## Manual checks

- Turn off Iron Grip, save, reopen the panel and start a fresh Solo run. Check new trainer offers and rerolls; other skills should remain available. Re-enable it without restarting and repeat in a new run.
- Test one entry per category and each category entirely off. Vanilla offers must remain. Check Save, Cancel, paging, Enable all/Disable all, normal menu navigation and UI fit at your resolution.
- Have host/client choose conflicting personal settings. Join, confirm the client log reports the host selection adopted before ready, then check offers on both PCs. Leave the party and verify the client's personal selection returns without file changes.
- Resume a checkpoint with owned content or stored offers that the new profile disables. Owned effects/descriptions and stored offer indices must remain intact; new generations use the selected pool.

Builds and nongame checks cannot establish the UI, actual native generators or two-PC synchronization work in a live game. Include both PCs' Player.log and BepInEx/LogOutput.log when reporting a problem.
