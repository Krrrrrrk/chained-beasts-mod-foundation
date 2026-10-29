using System;
using System.Collections.Generic;
using System.Linq;
using ChainedBeasts.ModFoundation.Content;

namespace TermsAndContusions.ContentMenu;

internal readonly struct ContentMenuFrame
{
    internal const float Width = 1100, Height = 780;
    internal const float RowTop = 216, RowStep = 86, RowHeight = 78;
    internal readonly float Scale, Left, Top;
    internal ContentMenuFrame(int width, int height)
    {
        Scale = Math.Clamp(Math.Min((width - 36) / Width, (height - 36) / Height), .1f, 1.4f);
        Left = (width - Width * Scale) / 2; Top = (height - Height * Scale) / 2;
    }
}

internal sealed class MenuEntry
{
    internal string Key, Name, Description;
    internal ContentCategory Category;
    internal MenuEntry(string key, ContentCategory category, string name, string description)
    { Key = key; Category = category; Name = name; Description = description; }
}

internal sealed class ContentMenuModel
{
    internal const int PageSize = 5;
    internal readonly MenuEntry[] Entries;
    private readonly Dictionary<string, bool> _original;
    internal readonly Dictionary<string, bool> Draft;
    internal int Category = -1, Page;
    internal string Search = "";
    internal ContentMenuModel(MenuEntry[] entries, IReadOnlyDictionary<string, bool> values)
    {
        Entries = entries;
        _original = entries.ToDictionary(e => e.Key, e => values[e.Key]);
        Draft = new(_original, StringComparer.Ordinal);
    }
    internal int DirtyCount => Draft.Count(e => e.Value != _original[e.Key]);
    internal int EnabledCount(int category = -1) => Entries.Count(e => (category < 0 || (int)e.Category == category) && Draft[e.Key]);
    internal int Total(int category = -1) => Entries.Count(e => category < 0 || (int)e.Category == category);
    internal MenuEntry[] Matches() => Entries.Where(e => (Category < 0 || (int)e.Category == Category) &&
        (string.IsNullOrWhiteSpace(Search) || e.Name.IndexOf(Search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0 ||
         e.Description.IndexOf(Search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
    internal int Pages => Math.Max(1, (Matches().Length + PageSize - 1) / PageSize);
    internal MenuEntry[] VisibleRows()
    { Page = Math.Clamp(Page, 0, Pages - 1); return Matches().Skip(Page * PageSize).Take(PageSize).ToArray(); }
    internal void SelectCategory(int category) { Category = Math.Clamp(category, -1, 3); Page = 0; }
    internal void SetSearch(string value) { Search = (value ?? ""); if (Search.Length > 128) Search = Search.Substring(0, 128); Page = 0; }
    internal void SetAll(bool value, bool categoryOnly = false)
    { foreach (var e in Entries) if (!categoryOnly || Category < 0 || (int)e.Category == Category) Draft[e.Key] = value; }
}
