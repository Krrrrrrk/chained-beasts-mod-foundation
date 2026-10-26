using System;
using System.Collections.Generic;
using System.IO;

namespace ChainedBeasts.ModFoundation.Configuration;

/// <summary>
/// Read-only legacy boolean settings used as defaults when a content plugin
/// first binds its own configuration. Existing destination values must be bound
/// normally through BepInEx, which gives them precedence over these defaults.
/// </summary>
public sealed class LegacyConfigurationSnapshot
{
    private readonly Dictionary<(string Section, string Key), string> _values = new();

    public static LegacyConfigurationSnapshot Parse(string text)
    {
        var result = new LegacyConfigurationSnapshot();
        using var reader = new StringReader(text ?? string.Empty);
        var section = string.Empty;
        string line;
        while ((line = reader.ReadLine()) is not null)
        {
            line = line.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal) ||
                line.StartsWith(";", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
            {
                section = line.Substring(1, line.Length - 2).Trim();
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator > 0)
            {
                result._values[(section, line.Substring(0, separator).Trim())] =
                    line.Substring(separator + 1).Trim();
            }
        }

        return result;
    }

    public bool BooleanDefault(string section, string key, bool normalDefault, bool migrationCompleted)
    {
        return !migrationCompleted && _values.TryGetValue((section, key), out var value) &&
               bool.TryParse(value, out var parsed)
            ? parsed
            : normalDefault;
    }
}
