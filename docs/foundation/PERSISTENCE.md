# Checkpoint persistence

Implemented locally for the investigated Windows Steam build, BepInEx `6.0.0-be.791+b782da4`, and `net6.0`. The adapter has not been run in the game. Intermediate-stage deployment remains blocked.

## Identities and ownership

Register one `ContentDescriptor` per stable namespaced key. A descriptor binds the owner plugin GUID, category, explicit numeric ID, supported maximum rank/severity, startup enable choice, and optional **legacy import** rule. Registration order and process pointers are never persistent identities. Duplicate keys, same-category IDs, installed native enum IDs, and duplicate legacy-slot claims are rejected. Disabled content still reserves its identity. `RegisterMany` validates a whole plugin batch before publishing any claim, so a later collision cannot leave earlier orphan IDs behind. Native-definition factories also check the live library for foreign definitions before adding anything.

New skills and afflictions do **not** allocate individual native checkpoint slots. Their rank/severity and acquisition order are captured from `PlayerData` dictionaries into one versioned envelope. Equipment and surgery histories already carry numeric IDs: those native fields remain authoritative. Sidecar entries for those categories are identity/availability records, never a second ownership source. Feature authors can store extra JSON through `FoundationServices.PlayerState(player).SetFeature("your.namespace:feature/key", value)`; choose stable keys and retain your own feature-format compatibility.

The four pack identities remain:

| Key | Numeric identity | Current ownership source |
|---|---|---|
| `terms-and-contusions:skill/bulk-up` | Skill `11000` | Skill dictionary; external checkpoint rank/order |
| `terms-and-contusions:equipment/runners-wraps` | Purchase `2499` | Native equipment/history |
| `terms-and-contusions:affliction/leadfoot` | Injury `11010` | Injury dictionary; external checkpoint severity/order |
| `terms-and-contusions:surgery/adrenal-graft` | Surgery `11020` | Native `previousSurgeries` |

## Exact checkpoint association

Schema 1 records mode, native `runSeed`, native `runStartTime`, ordered native player IDs, a generation token, the canonical SHA-256 of the complete native run checkpoint, and player state keyed by native `playerID`. The current game adapter supports Solo only; it never guesses a player from a process pointer or remaps a multiplayer roster.

One shared discriminator uses the already-reserved `SkillType.None` array index (135 in the inspected build): `skillLevels[index] = -20301`, `skillOrder[index] = positive generation token`. This is **checkpoint metadata, not a content rank or an additional per-feature save slot**. A unique token is needed because two otherwise identical native checkpoints can have different external state. Merely hashing the unmarked native JSON could not distinguish them. The occupied-slot check refuses conflicts, and tokens check both disk archives and outstanding captures before allocation.

All ranks, severities, order values, and feature JSON live outside that slot. The adapter hides its metadata around native `PlayerData.Checkpoint.Validate`, `PlayerData.RestoreCheckpoint`, and `OnCheckpointSynced`, restoring it in finalizers. It does not add custom IDs to native enum-sized arrays. Foundation alone leaves vanilla checkpoints unmarked when there is no external content or feature state to save.

Storage is under:

```text
<Application.persistentDataPath>/ModFoundation/checkpoints/<run-key>/<generation>.json
```

The investigated user path is `C:\Users\Krrrrrrk\AppData\LocalLow\Featherweight Games\ChainedBeasts\ModFoundation\checkpoints`. A run key hashes mode, seed and start time; it is not the seed alone. Native JSON object-property order and whitespace are canonicalized; array order and values remain significant. Restoration selects **the exact token and fingerprint**, never the newest sidecar. Restoring an older native checkpoint therefore selects its older immutable custom snapshot.

## Native write success and failure

Native `PersistentData.Save()` calls `IPlatform.WriteFile("SaveData", bytes, DEFAULT)` and does not await its returned task. A Save postfix is therefore not a success signal. The installed Steam and Standalone adapters await `File.WriteAllBytesAsync`; the Foundation observes those returned tasks from its managed update component.

1. Capture custom state when `RunDataManager.CreateCheckpoint()` finishes.
2. At the platform write prefix, extract the exact serialized `CheckpointSolo.runData` from the bytes the game will write. Atomically stage the corresponding envelope as `.pending`, using a flushed temporary file and same-directory rename.
3. If custom preparation/validation fails, return a faulted native task and **skip that SaveData write**, preserving the previous native file. Log `SaveData write BLOCKED`. Do not write a native checkpoint that requires an unavailable custom snapshot. Other native files are not substituted.
4. Publish `.pending` as `.json` only after that native task completes without fault/cancellation. Failed native tasks leave the pending snapshot uncommitted.
5. If the process exits between native success and the managed commit, pending recovery requires the actual on-disk `SaveData` to contain that exact run/token/fingerprint. An in-memory guess or an unrelated newer save is insufficient.

The native save and sidecar cannot form one filesystem transaction; this recovery protocol handles the gap. It does not claim to make the game's own file writes atomic or to synchronize the sidecars through Steam Cloud. Both success detection and the native adapter still require manual runtime verification.

## Restore, missing content, and schemas

Before `NetworkSession.RestoreCheckpoint()` begins a Solo restore, validate the matching envelope, player roster, known key/ID mappings, supported levels, and availability of every owned content entry. Repeat validation at the run restore boundary. Missing files, mismatches, newer schemas or discriminator protocols, changed mappings, and missing/disabled owned content refuse restoration with an explicit log. Restore the matching backup or enable the compatible owning plugin; do not delete a sidecar to bypass the check.

Unknown and disabled records remain dormant and round-trip in storage; they are not erased, renumbered, or guessed. **This is preservation, not permission to resume an incomplete run.** Refusing a live restore avoids missing native equipment definitions or continuing with saved HP but absent health modifiers. A character-preview `PlayerData.RestoreCheckpoint` can project only available entries from an exact matching saved root; it never performs an acquisition. Unsupported schema files remain unchanged.

Actual restoration assigns custom dictionaries/order after the native restore. It never calls AddSkill, purchase, surgery, heal, save, or initialization methods to replay ownership. Native saved HP, currency, equipment and surgery history are left to the native checkpoint. No acquisition event is emitted during restore. A modern legacy-import flag is retained per checkpoint; a previously committed legacy snapshot is reused rather than imported again.

## Legacy import and cleanup

Only the supplied Bulk Up and Leadfoot register legacy import rules: positive rank/order in `SkillType.None`, and severity/order in `InjuryType.EmptySlot1`. These are old example-specific meanings, not extension points for new copied content. The pack registers those rules even if an example is disabled, allowing dormant state preservation. An occupied legacy slot with no registered migration owner blocks import; Foundation alone cannot silently discard an old content-bearing run.

Import assigns the original rank-one Bulk Up and Leadfoot severity/order once without granting resources. Bulk Up supports four ranks while imported rank one retains its original 2x behavior. Future captures use the shared discriminator and sidecar; they stop writing Leadfoot's old injury slot and stop using the None slot for Bulk Up's rank. Runner's Wraps and Adrenal Graft retain native histories.

Reset, explicit checkpoint clearing, and a different seed/start-time run clear live player state, mutation guards, transient callbacks and battle identity. Battle-start keys use the exact run and completed-level count; duplicate scene/state callbacks do not rearm a scope, and a saved battle key restores without rearming it. More detailed combat/weapon/enemy events will be added only when later mechanics require them.

Immutable committed/pending files are archives, retained so older native-save backups remain recoverable. They are never selected after a run reset without an exact matching native checkpoint. Remove archives only after retiring every native backup that may reference them. There is no automatic archive garbage collector.

## Backup and removal

Back up the **entire user-data directory**, including `SaveData`, settings, and `ModFoundation/checkpoints`, with the corresponding plugin DLLs/configuration. Cloud-restoring only SaveData or copying it to another machine without sidecars can block a custom run. Never manually edit a save to migrate these values.

For rollback from a future deployed expansion: close the game, remove its pack/example DLLs from every loader scan directory, restore the matching pre-expansion Foundation DLL and configuration, and restore the matching pre-expansion user-data backup as a unit. The old v0.7.3 loader/plugin cannot interpret the new shared discriminator. Do not downgrade against a new-format checkpoint. No such runtime migration or deployment occurred during this stage.

At authorized final manual testing:

Feature capture/validation is fail-closed: unresolved HP receipts, unresolved charge reservations, invalid feature schemas or capture failure refuse the corresponding checkpoint operation and preserve the prior native save. Validated timed state stores remaining gameplay seconds, not process clock timestamps. A late damage receipt keeps its bonus reserved until confirmation or battle/run reset; a timeout does not free it for another hit. See [skill state](../terms-and-contusions/SKILLS.md).

Equipment uses schema-1 paid/spent/per-enemy/per-physical-weapon/aim state under `terms-and-contusions:equipment/battle-v1`. Duplicate/re-equipping paths do not recreate these allowances. Validation rejects unknown/malformed state; capture refuses pending opening/payment confirmations and flushes extra durability before saving. Resource grants/payments/repairs are never replayed on restore. Physical GUIDs are independent tokens; native object addresses are transient transfer aliases, never serialized identities. Native run/player checkpoints do not serialize arena objects, so native-created resume weapons/enemies are new physical spawns. This is not a complete arena snapshot. See [equipment persistence](../terms-and-contusions/EQUIPMENT.md).

Afflictions use schema-1 `terms-and-contusions:afflictions/battle-v1` timers/counters: elapsed battle age, remaining cooldown/landing/focus times, target token, shield count, heat stacks and attack age. Effective severity is always read from the native suppression/reduction path. Restoration assigns state without stamina/gold/injury effects; malformed feature data blocks native restore. Native checkpoints do not contain live stamina/recovery timers or arena instances. An unavailable focus is explicitly cleared; Lead Belly's owned live timer tail is removed on reset/restore and starts again only on genuine eligible exertion. See [affliction persistence](../terms-and-contusions/AFFLICTIONS.md).

Nongame checks exercise the production store, identity projection, migration, state session, transition gate, health calculation, modifiers, equipment/affliction state and solo policy. They do not establish actual checkpoint serialization, IL2CPP callbacks, HUD behavior, or gameplay correctness.

Surgery state uses schema-1 `terms-and-contusions:surgeries/battle-v1`: Clot's used flag/remaining integer budget, Counterweight and Fury remaining durations, recovery observation and attacker-target bleed cadence. Ownership remains exclusively native `previousSurgeries`. Capture refuses an unfinished custom surgery, an unresolved contact or an outstanding HP RPC dispatch. Native damage dispatch ordering includes generated/unrelated damage so its receipt cannot settle another direct bonus. Restore never buys, grants, heals, damages or adjusts current resources. Vanished arena targets are dropped and logged; a reconstructed player's recovery observation is rebased without granting a charge. The full native save/sidecar pair and corresponding content remain required. See [surgery state](../terms-and-contusions/SURGERIES.md).
