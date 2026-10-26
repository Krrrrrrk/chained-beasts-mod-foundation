using System.IO;
using BepInEx.Configuration;

namespace ChainedBeasts.ModFoundation.Configuration;

/// <summary>One-time legacy defaults; BepInEx retains explicit destination choices.</summary>
public static class LegacyConfigurationMigration
{
    public static LegacyConfigurationSnapshot Read(string legacyPath, bool migrationCompleted)
    {
        return LegacyConfigurationSnapshot.Parse(
            !migrationCompleted && File.Exists(legacyPath) ? File.ReadAllText(legacyPath) : string.Empty);
    }

    public static ConfigEntry<bool> BindBoolean(ConfigFile destination, LegacyConfigurationSnapshot legacy,
        bool migrationCompleted, string section, string key, bool normalDefault, string description)
    {
        return destination.Bind(section, key,
            legacy.BooleanDefault(section, key, normalDefault, migrationCompleted), description);
    }
}
