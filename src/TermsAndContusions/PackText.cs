using System;

namespace TermsAndContusions;

/// <summary>Mark pack-owned display text without changing names, IDs or localization keys.</summary>
public static class PackText
{
    public const string Prefix = "TC: ";
    public static string Description(string text) =>
        string.IsNullOrWhiteSpace(text) || text.StartsWith(Prefix, StringComparison.Ordinal) ? text : Prefix + text;
}
