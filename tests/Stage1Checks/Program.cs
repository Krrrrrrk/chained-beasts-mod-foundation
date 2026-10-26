using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using BepInEx.Configuration;
using ChainedBeasts.ModFoundation.Configuration;

var repository = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var configuration = args.Length > 1 ? args[1] : "Release";
var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    passed++;
    Console.WriteLine("PASS: " + name);
}

var legacy = LegacyConfigurationSnapshot.Parse("""
    # old BepInEx file
    [BulkUp]
    EnableBulkUp = false
    ForceBulkUpFirstOffer = true
    [Leadfoot]
    EnableLeadfoot = TRUE
    [RunnersWraps]
    ForceRunnersWrapsNextOffer = not-a-boolean
    [AdrenalGraft]
    EnableAdrenalGraft = true
    EnableAdrenalGraft = false
    """);
Check(!legacy.BooleanDefault("BulkUp", "EnableBulkUp", true, false), "legacy disabled content stays disabled");
Check(legacy.BooleanDefault("BulkUp", "ForceBulkUpFirstOffer", false, false), "explicit legacy testing override is preserved");
Check(legacy.BooleanDefault("Leadfoot", "EnableLeadfoot", false, false), "boolean parsing matches BepInEx case tolerance");
Check(!legacy.BooleanDefault("RunnersWraps", "ForceRunnersWrapsNextOffer", false, false), "invalid legacy force value does not enable testing");
Check(!legacy.BooleanDefault("AdrenalGraft", "EnableAdrenalGraft", true, false), "last duplicate setting wins");
Check(!legacy.BooleanDefault("BulkUp", "ForceBulkUpFirstOffer", false, true), "completed migration ignores legacy overrides");
Check(!LegacyConfigurationSnapshot.Parse(null).BooleanDefault("BulkUp", "ForceBulkUpFirstOffer", false, false), "fresh config keeps forced offers off");
Check(legacy.BooleanDefault("BulkUp", "UnknownSetting", true, false), "missing keys use normal defaults");

var testRoot = Path.Combine(Path.GetTempPath(), "ChainedBeasts-Stage1Checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(testRoot);
try
{
    var destinationPath = Path.Combine(testRoot, "pack.cfg");
    File.WriteAllText(destinationPath, "[BulkUp]\nEnableBulkUp = true\nForceBulkUpFirstOffer = false\n");
    var destination = new ConfigFile(destinationPath, false) { SaveOnConfigSet = false };
    Check(LegacyConfigurationMigration.BindBoolean(destination, legacy, false, "BulkUp", "EnableBulkUp", false, "test").Value,
        "explicit new pack enable choice overrides disabled legacy choice");
    Check(!LegacyConfigurationMigration.BindBoolean(destination, legacy, false, "BulkUp", "ForceBulkUpFirstOffer", false, "test").Value,
        "explicit new pack force choice overrides legacy true");
    Check(!LegacyConfigurationMigration.BindBoolean(destination, legacy, false, "AdrenalGraft", "EnableAdrenalGraft", true, "test").Value,
        "missing destination entry imports the actual legacy value");
    var oldPath = Path.Combine(testRoot, "old.cfg");
    File.WriteAllText(oldPath, "[BulkUp]\nForceBulkUpFirstOffer = true\n");
    var oldBytes = File.ReadAllBytes(oldPath);
    Check(LegacyConfigurationMigration.Read(oldPath, false).BooleanDefault("BulkUp", "ForceBulkUpFirstOffer", false, false),
        "first migration reads legacy file");
    Check(!LegacyConfigurationMigration.Read(oldPath, true).BooleanDefault("BulkUp", "ForceBulkUpFirstOffer", false, false),
        "migration marker prevents reimport on later launches");
    Check(File.ReadAllBytes(oldPath).SequenceEqual(oldBytes), "legacy config remains byte-identical");
    Check(!LegacyConfigurationMigration.Read(Path.Combine(testRoot, "absent.cfg"), false)
        .BooleanDefault("BulkUp", "ForceBulkUpFirstOffer", false, false), "absent legacy file keeps safe defaults");

    var foundationConfig = new ConfigFile(oldPath, false) { SaveOnConfigSet = false };
    foundationConfig.Bind("Diagnostics", "EnableDiagnostics", false, "test");
    foundationConfig.Save();
    Check(LegacyConfigurationMigration.Read(oldPath, false).BooleanDefault("BulkUp", "ForceBulkUpFirstOffer", false, false),
        "Foundation config save preserves unbound legacy content entries for pack migration");

    var importedPath = Path.Combine(testRoot, "imported.cfg");
    var imported = new ConfigFile(importedPath, false) { SaveOnConfigSet = false };
    LegacyConfigurationMigration.BindBoolean(imported, legacy, false, "BulkUp", "EnableBulkUp", true, "test");
    LegacyConfigurationMigration.BindBoolean(imported, legacy, false, "BulkUp", "ForceBulkUpFirstOffer", false, "test");
    imported.Bind("Migration", "LegacyConfigurationImported", false, "test").Value = true;
    imported.Save();
    var later = new ConfigFile(importedPath, false) { SaveOnConfigSet = false };
    var completed = later.Bind("Migration", "LegacyConfigurationImported", false, "test").Value;
    var changedLegacy = LegacyConfigurationSnapshot.Parse("[BulkUp]\nEnableBulkUp = true\nForceBulkUpFirstOffer = false\n");
    Check(completed, "migration completion survives a configuration reload");
    Check(!LegacyConfigurationMigration.BindBoolean(later, changedLegacy, completed, "BulkUp", "EnableBulkUp", true, "test").Value &&
        LegacyConfigurationMigration.BindBoolean(later, changedLegacy, completed, "BulkUp", "ForceBulkUpFirstOffer", false, "test").Value,
        "later legacy changes cannot overwrite migrated destination choices");
}
finally
{
    // Only this uniquely created test directory is removed; no game data is opened.
    Directory.Delete(testRoot, true);
}

using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(repository, "docs/terms-and-contusions/manifest.json")));
var entries = manifest.RootElement.GetProperty("entries").EnumerateArray().ToArray();
Check(entries.Length == 46, "manifest accounts for all 46 required entries");
foreach (var expected in new[] { ("skill", 12), ("equipment", 12), ("affliction", 11), ("surgery", 11) })
    Check(entries.Count(entry => entry.GetProperty("category").GetString() == expected.Item1) == expected.Item2,
        $"manifest contains {expected.Item2} {expected.Item1} entries");
Check(entries.Select(entry => entry.GetProperty("key").GetString()).Distinct().Count() == 46, "planned content keys are unique");
var stage = manifest.RootElement.GetProperty("stage").GetInt32();
Check(entries.Count(entry => entry.GetProperty("implementationStatus").GetString() == "moved-legacy") == (stage >= 7 ? 0 : stage >= 6 ? 2 : stage >= 3 ? 3 : 4),
    "legacy status matches completed stage boundaries");
Check(entries.Count(entry => entry.GetProperty("implementationStatus").GetString() == "planned") == (stage >= 6 ? 0 : stage >= 5 ? 10 : stage >= 4 ? 20 : stage >= 3 ? 30 : 42),
    "later-stage entries remain explicitly planned");
foreach (var excluded in new[] { "Recall Ring", "Debt Collector", "Bowling for Beasts", "Blood Bank Flask" })
    Check(!entries.Any(entry => entry.GetProperty("name").GetString() == excluded), $"deferred idea excluded: {excluded}");

var foundationPath = Path.Combine(repository, $"src/ChainedBeasts.ModFoundation/bin/{configuration}/net6.0/ChainedBeasts.ModFoundation.dll");
var packPath = Path.Combine(repository, $"src/TermsAndContusions/bin/{configuration}/net6.0/TermsAndContusions.dll");
using var foundationPe = new PEReader(File.OpenRead(foundationPath));
using var packPe = new PEReader(File.OpenRead(packPath));
var foundation = foundationPe.GetMetadataReader();
var pack = packPe.GetMetadataReader();
string[] Types(MetadataReader reader) => reader.TypeDefinitions.Select(handle => reader.GetTypeDefinition(handle))
    .Select(type => reader.GetString(type.Name)).ToArray();
string[] References(MetadataReader reader) => reader.AssemblyReferences.Select(handle =>
    reader.GetString(reader.GetAssemblyReference(handle).Name)).ToArray();
string AttributeName(MetadataReader reader, CustomAttribute attribute)
{
    if (attribute.Constructor.Kind != HandleKind.MemberReference) return string.Empty;
    var parent = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent;
    return parent.Kind == HandleKind.TypeReference ? reader.GetString(reader.GetTypeReference((TypeReferenceHandle)parent).Name) : string.Empty;
}
string[] AttributeStrings(MetadataReader reader, string attributeName, int count)
{
    var pluginType = reader.TypeDefinitions.Select(handle => reader.GetTypeDefinition(handle))
        .Single(type => reader.GetString(type.Name) == "Plugin");
    var attribute = pluginType.GetCustomAttributes().Select(handle => reader.GetCustomAttribute(handle))
        .Single(value => AttributeName(reader, value) == attributeName);
    var blob = reader.GetBlobReader(attribute.Value);
    if (blob.ReadUInt16() != 1) throw new InvalidOperationException("Invalid custom attribute prolog");
    return Enumerable.Range(0, count).Select(_ => blob.ReadSerializedString() ?? string.Empty).ToArray();
}
Check(foundation.GetString(foundation.GetAssemblyDefinition().Name) == "ChainedBeasts.ModFoundation", "Foundation assembly identity retained");
Check(pack.GetString(pack.GetAssemblyDefinition().Name) == "TermsAndContusions", "pack has a distinct assembly identity");
Check(!References(foundation).Contains("TermsAndContusions"), "Foundation assembly has no pack dependency");
Check(References(pack).Contains("ChainedBeasts.ModFoundation"), "pack references the actual Foundation assembly");
var foundationPlugin = AttributeStrings(foundation, "BepInPlugin", 3);
var packPlugin = AttributeStrings(pack, "BepInPlugin", 3);
Check(foundationPlugin[0] == "local.chainedbeasts.modfoundation", "Foundation GUID retained");
Check(packPlugin[0] == "local.chainedbeasts.termsandcontusions" && packPlugin[0] != foundationPlugin[0], "pack GUID/configuration identity distinct");
var dependency = AttributeStrings(pack, "BepInDependency", 2);
Check(dependency[0] == foundationPlugin[0] && dependency[1] == ">=0.8.0 <0.9.0", "pack declares verified BepInEx hard version-range dependency");
foreach (var feature in new[] { "BulkUpFeature", "RunnersWrapsFeature", "LeadfootFeature", "AdrenalGraftFeature", "CustomContentIds", "CustomLocalization", "CustomTranslations" })
{
    Check(!Types(foundation).Contains(feature) && Types(pack).Count(type => type == feature) == 1,
        $"content compiled exactly once in pack: {feature}");
}
foreach (var expected in new[] { ("BulkUpSkill", 11000), ("RunnersWrapsPurchase", 2499), ("LeadfootInjury", 11010), ("AdrenalGraftSurgery", 11020) })
{
    var idsType = pack.TypeDefinitions.Select(handle => pack.GetTypeDefinition(handle))
        .Single(type => pack.GetString(type.Name) == "CustomContentIds");
    var field = idsType.GetFields().Select(handle => pack.GetFieldDefinition(handle))
        .Single(value => pack.GetString(value.Name) == expected.Item1);
    var constant = pack.GetConstant(field.GetDefaultValue());
    Check(pack.GetBlobReader(constant.Value).ReadInt32() == expected.Item2, $"legacy numeric identity retained: {expected.Item1}");
}
foreach (var output in new[] { foundationPath, packPath })
    Check(Directory.GetFiles(Path.GetDirectoryName(output)!, "*.dll").Select(Path.GetFileName).SequenceEqual(new[] { Path.GetFileName(output) }),
        $"no copied runtime DLLs in output: {Path.GetFileName(output)}");
Console.WriteLine($"Stage 1: {passed} nongame checks passed. No Unity/gameplay methods were invoked.");
