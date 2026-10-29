using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace TermsAndContusions.ContentMenu;

internal sealed class ContentMenuTheme
{
    // Match Matchmaking's BrowserTheme and ChatView without a dependency on that optional plugin.
    internal static readonly Color Ink = new(.051f, .043f, .094f, 1);
    internal static readonly Color Header = new(.078f, .063f, .133f, 1);
    internal static readonly Color Raised = new(.106f, .094f, .165f, 1);
    internal static readonly Color Detail = new(.075f, .067f, .125f, 1);
    internal static readonly Color Border = new(.235f, .208f, .302f, 1);
    internal static readonly Color Hover = new(.157f, .141f, .220f, 1);
    internal static readonly Color Selected = new(.094f, .180f, .196f, 1);
    internal static readonly Color Gold = new(.949f, .788f, .365f, 1);
    internal static readonly Color Teal = new(.333f, .890f, .800f, 1);
    internal static readonly Color TealHover = new(.490f, .980f, .894f, 1);
    internal static readonly Color Coral = new(1, .537f, .529f, 1);
    internal static readonly Color Cream = new(.953f, .929f, .886f, 1);
    internal static readonly Color Faded = new(.678f, .663f, .753f, 1);
    private readonly List<Texture2D> _textures = new();
    internal GUIStyle Panel, Sidebar, Card, CardOn, Nav, NavSelected, Title, Eyebrow, Heading, Body, Muted, Caption, Right, Field,
        Button, Primary, Danger, ChipOn, ChipOff, ChipLocked, Dirty;
    internal Texture2D Scrim, Divider;
    internal void Initialize()
    {
        Scrim = Texture(new Color(.025f, .016f, .055f, .80f), Color.clear);
        Divider = Texture(Border, Color.clear);
        Panel = Box(Ink, Border); Sidebar = Box(Detail, Border);
        Card = ButtonStyle(Raised, Border, Cream);
        CardOn = ButtonStyle(Selected, Teal, Cream);
        Nav = ButtonStyle(Detail, Color.clear, Faded);
        NavSelected = ButtonStyle(Selected, Teal, Cream);
        Button = ButtonStyle(Raised, Border, Cream);
        Primary = ButtonStyle(Teal, Teal, Ink, TealHover);
        Danger = ButtonStyle(Raised, Border, Coral);
        ChipOn = Box(Teal, Color.clear); ChipOff = Box(Coral, Color.clear);
        ChipLocked = Box(new Color(.129f, .122f, .176f), Border);
        Dirty = Box(Gold, Color.clear);
        ConfigureChip(ChipOn, Ink); ConfigureChip(ChipOff, Ink); ConfigureChip(ChipLocked, Faded);
        ConfigureChip(Dirty, Ink);
        Title = Label(34, Cream, true); Heading = Label(18, Cream, true);
        Eyebrow = Label(11, Gold, true);
        Body = Label(15, Cream); Body.wordWrap = true; Body.alignment = TextAnchor.UpperLeft;
        Muted = Label(15, Faded); Muted.wordWrap = true;
        Caption = Label(11, Faded); Right = Label(13, Faded); Right.alignment = TextAnchor.MiddleRight;
        Field = Label(16, Cream);
        Field.padding = new RectOffset(12, 12, 7, 7); Field.border = new RectOffset(1, 1, 1, 1);
        Field.alignment = TextAnchor.MiddleLeft;
        Field.normal.background = Texture(Detail, Border);
        Field.focused.background = Texture(Detail, Teal);
    }
    private GUIStyle Label(int size, Color color, bool bold = false)
    {
        var style = new GUIStyle { font = GUI.skin.label.font ?? GUI.skin.font, fontSize = size,
            fontStyle = bold ? FontStyle.Bold : FontStyle.Normal, richText = false,
            alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip,
            padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
        foreach (var state in new[] { style.normal, style.hover, style.active, style.focused,
                     style.onNormal, style.onHover, style.onActive, style.onFocused }) state.textColor = color;
        return style;
    }
    private void ConfigureChip(GUIStyle style, Color color)
    { style.font = GUI.skin.label.font ?? GUI.skin.font; style.fontSize = 11; style.fontStyle = FontStyle.Bold; style.alignment = TextAnchor.MiddleCenter; style.normal.textColor = color; }
    private GUIStyle Box(Color fill, Color border)
    {
        var style = Label(14, Cream);
        style.normal.background = Texture(fill, border); style.border = new RectOffset(1, 1, 1, 1);
        return style;
    }
    private GUIStyle ButtonStyle(Color fill, Color border, Color text, Color? hover = null)
    {
        var style = Label(14, text, true);
        style.alignment = TextAnchor.MiddleCenter; style.border = new RectOffset(1, 1, 1, 1);
        style.normal.background = Texture(fill, border);
        style.hover.background = Texture(hover ?? Hover, border);
        style.active.background = Texture(Color.Lerp(hover ?? Hover, Color.white, .08f), border);
        style.focused.background = Texture(fill, Teal);
        return style;
    }
    internal static void Fill(Rect rect, Color color)
    {
        var before = GUI.color;
        try { GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true); }
        finally { GUI.color = before; }
    }
    private Texture2D Texture(Color fill, Color edge)
    {
        const int size = 16;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        _textures.Add(texture);
        texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Il2CppStructArray<Color32>(size * size);
        for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
        {
            var isEdge = edge.a > 0 && (x == 0 || y == 0 || x == size - 1 || y == size - 1);
            var c = isEdge ? edge : fill;
            pixels[y * size + x] = new Color32 { r = (byte)(c.r * 255), g = (byte)(c.g * 255), b = (byte)(c.b * 255), a = (byte)(c.a * 255) };
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true); return texture;
    }
    internal void Dispose()
    { foreach (var texture in _textures) if (texture != null) Object.Destroy(texture); _textures.Clear(); }
}
