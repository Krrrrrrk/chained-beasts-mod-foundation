using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TermsAndContusions.Skills;
using TermsAndContusions.Equipment;
using TermsAndContusions.Afflictions;
using TermsAndContusions.Surgeries;
using ChainedBeasts.ModFoundation.Logic;
using TermsAndContusions;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var root=Path.GetFullPath(args[0]);
var productVersion=Regex.Match(File.ReadAllText(Path.Combine(root,"src/ChainedBeasts.ModFoundation/ChainedBeasts.ModFoundation.csproj")),"<Version>([^<]+)</Version>").Groups[1].Value;
var configuration=args.Skip(2).FirstOrDefault(value=>value is "Debug" or "Release")??"Release";
var manifestPath=Path.Combine(root,"docs/terms-and-contusions/manifest.json");
var manifest=JsonNode.Parse(File.ReadAllText(manifestPath));
var entries=manifest["entries"].AsArray();
var rows=new Dictionary<string,Row>();
foreach(var item in SkillDefinitions.All) rows[item.Key]=new(item.Id,
    string.Join(" / ",Enumerable.Range(1,4).Select(rank=>"Rank "+rank+": "+SkillMath.Effect(item.Kind,rank))),null,
    item.Kind==CustomSkill.BulkUp?"[BulkUp] EnableBulkUp":"[Skills] Enable"+item.Kind);
foreach(var item in EquipmentDefinitions.All)rows[item.Key]=new(item.Id,item.Description,item.Price,"[Equipment] Enable"+item.Kind);
rows["terms-and-contusions:equipment/runners-wraps"]=new(2499,PackText.Description("Dodges use 25% less stamina while equipped."),40,"[RunnersWraps] EnableRunnersWraps");
rows["terms-and-contusions:equipment/counterweight-sling"]=new(2498,PackText.Description("Doubles throw range and charge speed."),45,"[CounterweightSling] EnableCounterweightSling");
foreach(var item in AfflictionDefinitions.All)rows[item.Key]=new(item.Id,
    string.Join(" / ",Enumerable.Range(1,3).Select(severity=>severity+" skull(s): "+AfflictionMath.Description(item.Kind,severity))),null,"[Afflictions] Enable"+item.Kind);
rows["terms-and-contusions:affliction/leadfoot"]=new(11010,PackText.Description("Movement speed reduced 10/20/30% at severity 1/2/3."),null,"[Leadfoot] EnableLeadfoot");
foreach(var item in SurgeryDefinitions.All)rows[item.Key]=new(item.Id,item.Description,item.DefaultPrice,
    item.Kind==CustomSurgery.AdrenalGraft?"[AdrenalGraft] EnableAdrenalGraft":"[Surgeries] Enable"+item.Kind);
string Category(string value)=>value switch {"skill"=>"Skills","equipment"=>"Equipment","affliction"=>"Potion afflictions",_=>"Surgeries"};
string Prefix(string value)=>value switch {"skill"=>"SK","equipment"=>"EQ","affliction"=>"AF",_=>"SU"};
string Removal(string value)=>value switch {"skill"=>"Canonical AddSkill/RemoveSkill changes rank immediately; restore assigns without acquisition effects.",
    "equipment"=>"Native single equipment slot; replacing/unequipping removes active factors, preserves spent per-battle allowances.",
    "affliction"=>"Native potion AddInjury, effective HasInjury, reduction/suppression/cure; normal potion healing preserved.",
    _=>"Once per player/run in native previousSurgeries; no mid-run remove/rebuy API. Fresh run clears history."};
string Registration(string value)=>value=="surgery"?"SurgeryServices.Register: shared eligible pool and confirmed native completion, native payment/history":"RegisterLibraryCallbacks before GameLibrary.Init; native dictionary/normal generator; canonical ownership";
if(args.Contains("--write-docs")) {
    manifest["stage"]=7;
    manifest["note"]="All 46 entries are implemented and built; gameplay remains unverified. Stage 7 provides compiled tutorials, local packages, guarded deployment and manual checks. Local delivery metadata is recorded separately; no publication or runtime verification is implied.";
    foreach(var item in entries) {
        var key=item["key"].GetValue<string>();var row=rows[key];var category=item["category"].GetValue<string>();
        item["implementationStatus"]="implemented-runtime-unverified";item["effect"]=row.Effect;item["configuration"]=row.Configuration;
        item["registration"]=Registration(category);item["acquisitionAndRemoval"]=Removal(category);
        item["runtimeVerified"]=false;item["verification"]="Built and nongame checked; split chainloader, native callbacks, presentation, mechanics and save/restore await the user's manual Solo test.";
        item["manualTestId"]=Prefix(category)+(Array.IndexOf(entries.Where(value=>value["category"].GetValue<string>()==category).ToArray(),item)+1).ToString("00");
        item["manualTest"]="docs/MANUAL_TESTS.md#"+item["manualTestId"].GetValue<string>().ToLowerInvariant();
        if(row.Price.HasValue)item["price"]=row.Price.Value;
        if(key.EndsWith("/pick-your-moment"))item["stage7Integration"]="Attempt-wide StatChannel.AttackDamage contribution includes attributed area attacks; direct-hit path does not apply it twice; removal epoch still invalidates cached prime.";
        if(key.EndsWith("/waste-not")||key.EndsWith("/last-gasp-locket"))item["stage7Integration"]="One-time RestoreStaminaGrant uses actual owner, normal clamp and explicit exclusion from passive regeneration modifiers.";
    }
    manifest["delivery"]??=new JsonObject();
    // Delivery describes the actual installed/published artifact, not a future source build.
    manifest["sourceVersion"]=productVersion;
    manifest["delivery"]["version"]??=productVersion;manifest["delivery"]["runtimeVerified"]=false;manifest["delivery"]["published"]??=false;
    manifest["delivery"]["report"]??="docs/STAGE7_HANDOFF.md";manifest["delivery"]["installation"]="docs/INSTALLATION.md";
    File.WriteAllText(manifestPath,manifest.ToJsonString(new JsonSerializerOptions{WriteIndented=true})+"\n");
    var catalog=new StringBuilder("# Complete content catalog — "+productVersion+"\n\nAll **46 entries** have implemented code. **Runtime unverified**: successful builds/automated checks do not establish gameplay correctness. This is an unbalanced technical example pack. Existing native icons/models are reused; new text has English fallback.\n\nGenerated from the actual pure definitions/formulas and [manifest](manifest.json) by `scripts/update-content-docs.ps1`. The manifest maps each row to its exact source, registration, state, removal and manual check. See [installation](../INSTALLATION.md), [manual tests](../MANUAL_TESTS.md), [skills/throwing](STAGE3_SKILLS.md), [equipment](STAGE4_EQUIPMENT.md), [afflictions](STAGE5_AFFLICTIONS.md) and [surgeries](STAGE6_SURGERIES.md).\n\n");
    var manual=new StringBuilder("# Manual Solo verification — "+productVersion+"\n\n**Pending. Do not automate these checks.** The author reported a crash-free 0.8.2 session, but individual cases below remain pending. The 0.8.3 tag/normal-offer changes need a fresh check; see [current notes](COMMUNITY_UPDATE_0.8.3.md). Back up the complete native user-data/sidecar directory and matching DLL/configuration set first. Follow [installation and rollback](INSTALLATION.md). Use a disposable Solo run; multiplayer is unsupported. For normal-offer tests keep [DeveloperOffers] EnableForcedOffers=false. Deliberate testing overrides require that master switch plus the relevant Force option; migrated Force flags alone cannot force offers. F1 supports skills/injuries, ranks/severity, gold and temporary cheats. God mode prevents real damage tests: turn it off for damage-dependent entries. Debug grants are conveniences, not proof of normal acquisition. Equipment/surgeries must be bought through normal selection/cost paths.\n\nFor every row: check normal card/name/description and character/detail view where available, native eligibility/cost, actual effect and absence after removal/cure/unequip, plus the corresponding checkpoint. For every skill: ranks 0–4, short next-rank offer, owned-rank detail text and banner, no fifth rank, normal removal/reacquisition and restore. For every affliction: severity 1–3, suppression/reduction/cure and unchanged potion healing. Surgeries: genuine successful payment/completion and once-per-run ownership, no duplicate/replayed grants. Record the log, before/after resource values and pass/fail; no row below is already passed.\n\n");
    foreach(var category in new[]{"skill","equipment","affliction","surgery"}) {
        catalog.AppendLine("## "+Category(category)).AppendLine();
        catalog.AppendLine("| Content / numeric ID | Stable identity | Effect and limits | Price / setting | Source, state, manual check |").AppendLine("|---|---|---|---|---|");
        manual.AppendLine("## "+Category(category)).AppendLine();
        foreach(var item in entries.Where(value=>value["category"].GetValue<string>()==category)) {
            var key=item["key"].GetValue<string>();var row=rows[key];var name=item["name"].GetValue<string>();var id=item["manualTestId"].GetValue<string>();
            var source=item["sourceFile"].GetValue<string>();var state=item["persistence"].GetValue<string>();
            catalog.AppendLine($"| {name} / {row.Id} | `{key}` | {row.Effect.Replace("|","/")} | {(row.Price.HasValue?row.Price+" gold":"Native trainer/potion rules")}<br>`{row.Configuration}` | [Implementation](../../{source})<br>{state}<br>[{id}](../MANUAL_TESTS.md#{id.ToLowerInvariant()}) |");
            manual.AppendLine($"<a id=\"{id.ToLowerInvariant()}\"></a>\n- [ ] **{id}: {name}** ({row.Id}): {row.Effect} Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.\n");
        }
        catalog.AppendLine();
    }
    manual.AppendLine(File.ReadAllText(Path.Combine(root,"docs/MANUAL_TEST_MATRIX.md")));
    File.WriteAllText(Path.Combine(root,"docs/terms-and-contusions/CATALOG.md"),catalog.ToString().TrimEnd()+"\n");
    File.WriteAllText(Path.Combine(root,"docs/MANUAL_TESTS.md"),manual.ToString().TrimEnd()+"\n");
    foreach(var tutorial in Directory.GetFiles(Path.Combine(root,"docs/foundation/tutorials"),"*.md")) {
        var body=Regex.Replace(File.ReadAllText(tutorial),@"(?s)<!-- source: (.*?) -->.*?<!-- /source -->",match=>
            "<!-- source: "+match.Groups[1].Value+" -->\n```csharp\n"+File.ReadAllText(Path.Combine(root,match.Groups[1].Value)).TrimEnd()+"\n```\n<!-- /source -->");
        File.WriteAllText(tutorial,body);
    }
    Console.WriteLine("Updated 46-entry manifest/catalog/manual rows and compiled-source tutorial excerpts. No game initialized.");return;
}
int passed=0;void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);passed++;Console.WriteLine("PASS: "+name);}
Check(PackText.Description("Short effect.")=="TC: Short effect." && PackText.Description(PackText.Description("Short effect."))=="TC: Short effect.","pack description marker appears exactly once across layered lookup paths");
Check(PackText.Description(null) is null && PackText.Description("")=="","missing text does not become a misleading standalone marker");
foreach(var entry in SkillDefinitions.All)Check(Enumerable.Range(0,5).All(rank=>SkillMath.Description(entry.Kind,rank).StartsWith(PackText.Prefix) &&
    SkillMath.OfferDescription(entry.Kind,rank).StartsWith(PackText.Prefix)),"TC marker in every owned/offer rank: "+entry.Name);
foreach(var entry in AfflictionDefinitions.All)Check(Enumerable.Range(0,4).All(level=>AfflictionMath.Description(entry.Kind,level).StartsWith(PackText.Prefix)),"TC marker in every affliction severity: "+entry.Name);
foreach(var entry in EquipmentDefinitions.All)Check(entry.Description.StartsWith(PackText.Prefix),"TC marker in equipment: "+entry.Name);
foreach(var entry in SurgeryDefinitions.All)Check(entry.Description.StartsWith(PackText.Prefix),"TC marker in surgery: "+entry.Name);
var nativeEntries=new NativeEntryClaims<string>();
nativeEntries.RequireCompatible(123,"first-wrapper");nativeEntries.RequireCompatible(123,"different-uncommitted-wrapper");
Check(true,"uncommitted/failed native patch attempts do not reserve addresses");
nativeEntries.Commit(123,"first-wrapper");nativeEntries.RequireCompatible(123,"first-wrapper");
Check(true,"multiple feature patches on the same native wrapper remain allowed");
var duplicateEntryBlocked=false;try{nativeEntries.RequireCompatible(123,"second-wrapper");}catch(InvalidOperationException){duplicateEntryBlocked=true;}
Check(duplicateEntryBlocked,"distinct wrappers sharing one native entry are rejected before detouring");
nativeEntries.Commit(456,"second-wrapper");Check(true,"independent native entries can have independent wrappers");
var nullEntryBlocked=false;try{nativeEntries.RequireCompatible(0,"null-wrapper");}catch(InvalidOperationException){nullEntryBlocked=true;}
Check(nullEntryBlocked,"null native entry is refused");
var noopBlocked=false;try{NativeHookPolicy.RequireSafe("Projectile","OnDespawn");}catch(InvalidOperationException){noopBlocked=true;}
Check(noopBlocked,"unsafe folded native OnDespawn rejected by actual production patch policy");
foreach (var type in new[]{"WeaponCharge","WeaponAxeCharge"})
{
    Check(!NativeHookPolicy.ObserveWeaponMethod(type,"Attack"),"shared charged Attack delegates are not detoured: "+type);
    var rejected=false;try{NativeHookPolicy.RequireSafe(type,"Attack");}catch(InvalidOperationException){rejected=true;}
    Check(rejected,"shared charged Attack cannot bypass patch safety guard: "+type);
}
Check(NativeHookPolicy.ObserveWeaponMethod("Weapon","Attack") && NativeHookPolicy.ObserveWeaponMethod("WeaponCharge","AttackEffect"),"unique base commit and effect remain observable");
Check(AfflictionMath.DisplayName("Lead Belly",1)=="Lead Belly I" && AfflictionMath.DisplayName("Lead Belly",2)=="Lead Belly II" &&
    AfflictionMath.DisplayName("Lead Belly",3)=="Lead Belly III" && AfflictionMath.DisplayName("Lead Belly",-1)=="Lead Belly", "custom reveal names preserve identity and Roman severity without a stale native key");
foreach(var entry in SkillDefinitions.All)
    Check(Enumerable.Range(0,5).All(rank=>SkillMath.Description(entry.Kind,rank).Length<=125 && SkillMath.OfferDescription(entry.Kind,rank).Length<=125),"bounded card/owned text across all ranks: "+entry.Name);
foreach(var entry in AfflictionDefinitions.All)
    Check(Enumerable.Range(1,3).All(level=>AfflictionMath.Description(entry.Kind,level).Length<=125),"bounded potion text at all severities: "+entry.Name);
foreach(var entry in EquipmentDefinitions.All)Check(entry.Description.Length<=125,"bounded equipment card: "+entry.Name);
foreach(var entry in SurgeryDefinitions.All)Check(entry.Description.Length<=125,"bounded surgeon card: "+entry.Name);
NativeHookPolicy.RequireSafe("Projectile","OnDestroy");NativeHookPolicy.RequireSafe("Projectile","Launch");
Check(true,"inspected substantial destruction/launch hooks remain allowed");
var visuals=new VisualReferenceSelection<string>();visuals.Consider(null,true);visuals.Consider("native-fallback",false);
Check(visuals.Selected=="native-fallback","missing/null preferred skill visual uses actual valid fallback");
visuals.Consider(null,true);Check(visuals.Selected=="native-fallback","later null preferred sprite cannot erase fallback");
visuals.Consider("preferred-native",true);visuals.Consider("later-fallback",false);
Check(visuals.Selected=="preferred-native","available preferred icon takes precedence without losing it");
Check(new VisualReferenceSelection<string>().Selected is null,"no valid native sprite remains explicit failure rather than copying gameplay identity");
for(var rank=0;rank<=4;rank++) {
    Check(SkillMath.CommittedPick(rank,true,3,3)==1+.5*rank,"committed attempt/AOE Pick factor rank "+rank);
    Check(SkillMath.CommittedPick(rank,false,3,3)==1&&SkillMath.CommittedPick(rank,true,2,3)==1,"miss/unprimed and stale-removal epoch neutrality rank "+rank);
}
Check(SkillMath.DirectDamage(100*SkillMath.CommittedPick(2,true,3,3),true,.8,50,100,2,0,0,0,false,1,10)==310,
    "attempt-wide Pick applied once before direct percentage factors and flat Payback");
Check(manifest["stage"].GetValue<int>()==7 && entries.Count==46 && rows.Count==46,"Stage 7 complete content accounting");
foreach(var item in entries){var key=item["key"].GetValue<string>();var row=rows[key];
    Check(item["numericId"].GetValue<int>()==row.Id && item["effect"].GetValue<string>()==row.Effect && item["configuration"].GetValue<string>()==row.Configuration,"manifest matches real production identity/formula/config "+key);
    Check(item["implementationStatus"].GetValue<string>()=="implemented-runtime-unverified" && !item["runtimeVerified"].GetValue<bool>() && File.Exists(Path.Combine(root,item["sourceFile"].GetValue<string>())),"implementation exists and runtime status honest "+key);
    Check(File.ReadAllText(Path.Combine(root,"docs/MANUAL_TESTS.md")).Contains("id=\""+item["manualTestId"].GetValue<string>().ToLowerInvariant()+"\""),"individual manual test exists "+key);
}
foreach(var tutorial in Directory.GetFiles(Path.Combine(root,"docs/foundation/tutorials"),"*.md"))foreach(Match match in Regex.Matches(File.ReadAllText(tutorial),@"(?s)<!-- source: (.*?) -->\s*```csharp\s*(.*?)\s*```\s*<!-- /source -->"))
    Check(match.Groups[2].Value.Trim()==File.ReadAllText(Path.Combine(root,match.Groups[1].Value)).Trim(),"tutorial contains exact compiled example "+match.Groups[1].Value);
foreach(var product in new[]{"ChainedBeasts.ModFoundation","TermsAndContusions","StandaloneContentExample","CategoryExamples"}){
    var path=Path.Combine(root,product is "StandaloneContentExample" or "CategoryExamples"?"examples":"src",product,"bin",configuration,"net6.0",product+".dll");
    using var pe=new PEReader(File.OpenRead(path));var md=pe.GetMetadataReader();
    var refs=md.AssemblyReferences.Select(h=>md.GetString(md.GetAssemblyReference(h).Name)).ToArray();
    Check(product=="ChainedBeasts.ModFoundation"?!refs.Contains("TermsAndContusions"):refs.Contains("ChainedBeasts.ModFoundation"),"independent dependency direction "+product);
    Check(Directory.GetFiles(Path.GetDirectoryName(path),"*.dll").Select(Path.GetFileName).SequenceEqual(new[]{product+".dll"}),"no copied game/runtime DLLs in output "+product);
}
using(var pe=new PEReader(File.OpenRead(Path.Combine(args[1],"BepInEx/interop/Assembly-CSharp.dll")))){
    var md=pe.GetMetadataReader();
    var stringManager=md.TypeDefinitions.Select(h=>md.GetTypeDefinition(h)).Single(t=>md.GetString(t.Name)=="StringManager");
    Check(stringManager.GetMethods().Select(h=>md.GetMethodDefinition(h)).Count(m=>md.GetString(m.Name)=="SetInjuryNameTermAndNumerals" &&
        m.GetParameters().Count(h=>md.GetParameter(h).SequenceNumber>0)==3)==1,"installed custom reveal helper has the inspected unique three-argument signature");
    foreach(var vacancy in new[]{("PurchaseType",2470),("InjuryType",12010),("SurgeryType",12020)}){
        var type=md.TypeDefinitions.Select(h=>md.GetTypeDefinition(h)).Single(t=>md.GetString(t.Name)==vacancy.Item1);
        var ids=type.GetFields().Select(h=>md.GetFieldDefinition(h).GetDefaultValue()).Where(h=>!h.IsNil).Select(h=>md.GetBlobReader(md.GetConstant(h).Value).ReadInt32());
        Check(!ids.Contains(vacancy.Item2),"tutorial native ID vacancy "+vacancy);
        Check(!entries.Any(entry=>entry["category"].GetValue<string>()==(vacancy.Item1=="PurchaseType"?"equipment":vacancy.Item1=="InjuryType"?"affliction":"surgery")&&entry["numericId"].GetValue<int>()==vacancy.Item2),"tutorial independent pack identity "+vacancy);
    }
}
Check(!manifest["delivery"]["runtimeVerified"].GetValue<bool>(),"release publication does not imply exhaustive gameplay verification");
Console.WriteLine($"Stage 7: {passed} catalog, tutorial, identity and metadata checks passed. No game initialized.");
sealed record Row(int Id,string Effect,int? Price,string Configuration);
