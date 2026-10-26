# Localization, presentation and visuals

Register the content descriptor before `FoundationText.Register(key,suffix,english,translations)`. The returned namespaced term derives from the independent content identity. Translation dictionaries use the installed I2 language names; missing/unprovided values fall back explicitly to English. `FoundationText.TryGet` reads the current language. Duplicate owned terms fail instead of taking over someone else's strings. Runtime I2 registration has targeted lookup/pooled-label fallbacks if a source isn't ready.

For example, a separate plugin can pass a `Dictionary<string,string>` of reviewed translations to the same API; use a unique content key and keep the original English fallback. No automatic translator or native-speaker review is supplied. The existing pack preserves twelve older language tables/aliases, including Bulk Up's updated cumulative rank text, but **new catalog and dynamic rank/severity text currently use English fallback**. Contributions should include the full 0–4 or 0–3 text set, fixed caps/timers and the correct context; a translated name alone isn't a fully translated mechanic.

| Inspected consumer | Return form | Why |
|---|---|---|
| GetSkillName | Final text | Direct display name |
| GetSkillNameTerm / GetSkillDescTerm | Term | Native localizable skill widgets |
| GetPurchaseName / GetPurchaseDesc | Term | Native shop/purchase pooled fields localize terms |
| GetInjuryName | Final text | Name includes the viewed severity/context |
| GetInjuryNameTerm / GetInjuryDescTerm | Term | Native localizable injury views |
| GetSurgeryName | Term | Surgeon's name widget localizes |
| GetSurgeryDesc | **Final text** | Surgeon description does not localize the result again |

All prefixes match only their enabled content ID and call the original for unrelated content. Don't globally replace a common word or return a literal to a term consumer: earlier pooled cards retained the previous skill/debuff text when the new key couldn't resolve. Conversely, returning a term to the surgery-description consumer displayed the raw key.

Four-rank skill text must use the **viewed player's actual current rank**, not an arbitrary local maximum. The pack registers short owned-rank descriptions and next-rank offer descriptions and patches trainer Refresh/SetSkill, character skill detail and tooltip methods in SkillsFeature. Affliction names/descriptions use actual severity; active mechanics use native effective severity after suppression. Acquisition banners must agree with the backing selected training row/ID. Bulk Up's earlier backing-row fix is retained, with no native memory scanner or blanket banner override. The new split's banners/cards still require manual verification after deployment. The custom injury reveal helper formats native keys directly, so 0.8.2 intercepts SetInjuryNameTermAndNumerals for the ten new custom injuries and assigns owned name/severity terms. See [release notes](https://github.com/Krrrrrrk/chained-beasts-mod-foundation/releases/tag/v0.8.3).

Visual references come from the user's installed game at library initialization. Create a fresh definition/identity and reuse only the sprite/model fields appropriate to its category. Surgery enhancement icons use the native category; these examples do not add original assets. **No original-icon import/asset-bundle workflow has been implemented or verified.** Don't redistribute extracted textures/models/Unity containers or generated bindings. Existing screenshots remain labeled as earlier manually tested v0.7.3-era examples and don't show the untested 46-entry split. New screenshots will come from the user's manual testing.

Complete working source and exact view signatures are embedded in the [four category tutorials](SEPARATE_MOD.md). Verify language switches, pooled reuse after viewing another entry, long descriptions/card fit, rank/severity transitions, character detail and acquisition banner manually. The source-generation checks keep tutorial code aligned with compiled examples; they do not render game UI.

## Terms & Contusions identification

From 0.8.3, PackText marks pack-owned descriptions with `TC: ` once. Only actual display text is tagged, including legacy translated descriptions. Names, localization keys, IDs, vanilla strings and text registered by independent Foundation plugins are unchanged. Short card limits include the marker.
