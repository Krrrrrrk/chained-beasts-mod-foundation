using System;
using System.Linq;
using ChainedBeasts.ModFoundation.Runtime;
using UnityEngine;

namespace TermsAndContusions.ContentMenu;

public sealed class TcContentMenuController : MonoBehaviour
{
    private static readonly string[] Categories = { "All content", "Skills", "Equipment", "Potions", "Surgeries" };
    private ContentMenuModel _view;
    private ContentMenuTheme _theme;
    private string _status = "";
    private bool _cursorBefore, _cursorCaptured, _failed, _simple, _focusSearch, _searchUnavailable;
    private GUIContent _searchContent;
    private CursorLockMode _lockBefore;
    private double _savedUntil;
    public TcContentMenuController(IntPtr pointer) : base(pointer) { }
    public void Update()
    {
        if (!ContentMenuFeature.Visible || (!ContentMenuFeature.Open && _cursorCaptured)) Close();
        if (ContentMenuFeature.Open) { Cursor.visible = true; Cursor.lockState = CursorLockMode.None; }
    }
    public void OnDestroy() { Close(); _theme?.Dispose(); }
    private void Close()
    {
        if (ContentMenuFeature.Open) ContentMenuFeature.BlockNavigationUntilFrame = Time.frameCount + 2;
        ContentMenuFeature.Open = false; _view = null;
        if (!_cursorCaptured) return;
        _cursorCaptured = false; Cursor.visible = _cursorBefore; Cursor.lockState = _lockBefore;
    }
    private void Open()
    {
        _view = new ContentMenuModel(ContentMenuSettings.Entries, ContentOfferServices.EffectivePreferences());
        _cursorBefore = Cursor.visible; _lockBefore = Cursor.lockState; _cursorCaptured = true;
        ContentMenuFeature.Open = true; _status = ""; _focusSearch = true;
    }
    private void Save()
    {
        try { ContentMenuSettings.Save(_view.Draft); _savedUntil = Time.unscaledTimeAsDouble + 4; Close(); }
        catch (Exception e) { _status = e.Message; ContentMenuSettings.Logger?.LogError("[Terms & Contusions] settings save failed: " + e); }
    }
    public void OnGUI()
    {
        if (ContentMenuSettings.Entries is null || !ContentMenuFeature.Visible) return;
        var enabled = GUI.enabled; var matrix = GUI.matrix; var depth = GUI.depth;
        var color = GUI.color; var background = GUI.backgroundColor; var content = GUI.contentColor;
        try
        {
            GUI.depth = -900; GUI.color = GUI.backgroundColor = GUI.contentColor = Color.white;
            if (_simple) { SimplePanel(); return; }
            if (_theme is null) { _theme = new ContentMenuTheme(); _theme.Initialize(); }
            var frame = new ContentMenuFrame(Screen.width, Screen.height);
            if (!ContentMenuFeature.Open)
            {
                var button = new Rect(Screen.width - 274, Screen.height - 78, 250, 50);
                var count = ContentMenuSettings.Entries.Count(e => ContentOfferServices.Allows(e.Key));
                ContentMenuTheme.Fill(new Rect(button.x + 4, button.y + 5, button.width, button.height), new Color(0, 0, 0, .35f));
                if (GUI.Button(button, $"TC CONTENT   {count}/{ContentMenuSettings.Entries.Length}", _theme.Button)) Open();
                ContentMenuTheme.Fill(new Rect(button.x, button.y, 4, button.height), ContentMenuTheme.Teal);
                if (Time.unscaledTimeAsDouble < _savedUntil) GUI.Label(new Rect(button.x - 160, button.y + 14, 145, 24), "Selection saved", _theme.Right);
                return;
            }
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _theme.Scrim);
            GUI.matrix = Matrix4x4.TRS(new Vector3(frame.Left, frame.Top, 0), Quaternion.identity, new Vector3(frame.Scale, frame.Scale, 1));
            ContentMenuTheme.Fill(new Rect(10, 12, ContentMenuFrame.Width, ContentMenuFrame.Height), new Color(0, 0, 0, .38f));
            GUI.Box(new Rect(0, 0, ContentMenuFrame.Width, ContentMenuFrame.Height), "", _theme.Panel);
            Draw(); _failed = false;
        }
        catch (Exception e)
        {
            // Keep the proven simple controls available if a stripped IMGUI
            // function prevents the styled renderer from running.
            _simple = true;
            _view?.SetSearch("");
            if (!_failed) { _failed = true; ContentMenuSettings.Logger?.LogError("[Terms & Contusions] styled menu unavailable; using simple view: " + e); }
        }
        finally
        {
            GUI.enabled = enabled; GUI.matrix = matrix; GUI.depth = depth;
            GUI.color = color; GUI.backgroundColor = background; GUI.contentColor = content;
        }
    }
    private void Draw()
    {
        var t = _theme; var editable = ContentMenuFeature.CanEdit;
        ContentMenuTheme.Fill(new Rect(1, 1, 1098, 115), ContentMenuTheme.Header);
        ContentMenuTheme.Fill(new Rect(0, 0, 5, 118), ContentMenuTheme.Gold);
        GUI.Label(new Rect(28, 18, 740, 18), "CHAINED BEASTS  /  CUSTOM CONTENT", t.Eyebrow);
        GUI.Label(new Rect(28, 39, 770, 44), "TERMS & CONTUSIONS", t.Title);
        GUI.Label(new Rect(30, 86, 740, 24), "Choose what appears in your next run.", t.Muted);
        GUI.Box(new Rect(828, 34, 188, 28), _view.DirtyCount > 0 ? $"{_view.DirtyCount} UNSAVED CHANGES" : $"{_view.EnabledCount()}/{_view.Total()} ENABLED", _view.DirtyCount > 0 ? t.Dirty : t.ChipOn);
        if (GUI.Button(new Rect(1034, 28, 38, 38), "X", t.Button)) { Close(); return; }
        GUI.Label(new Rect(812, 86, 260, 22), "SELECT  /  SAVE  /  START YOUR RUN", t.Right);
        GUI.DrawTexture(new Rect(24, 117, 1052, 1), t.Divider);
        GUI.Box(new Rect(24, 122, 220, 570), "", t.Sidebar);
        GUI.Label(new Rect(42, 140, 180, 24), "CONTENT LIBRARY", t.Eyebrow);
        for (var i = 0; i < Categories.Length; i++)
        {
            var category = i - 1;
            var nav = new Rect(36, 176 + i * 52, 196, 44);
            if (GUI.Button(nav, $"{Categories[i]}   {_view.EnabledCount(category)}/{_view.Total(category)}",
                _view.Category == category ? t.NavSelected : t.Nav)) { _view.SelectCategory(category); GUIUtility.keyboardControl = 0; }
            if (_view.Category == category) ContentMenuTheme.Fill(new Rect(nav.x, nav.y, 4, nav.height), ContentMenuTheme.Teal);
        }
        GUI.Label(new Rect(42, 460, 178, 58), "New installs start with everything enabled.", t.Muted);
        GUI.enabled = editable;
        if (GUI.Button(new Rect(40, 536, 188, 38), "ENABLE ALL CONTENT", t.Button)) _view.SetAll(true);
        if (GUI.Button(new Rect(40, 584, 188, 38), "DISABLE ALL CONTENT", t.Danger)) _view.SetAll(false);
        GUI.enabled = true;
        GUI.Label(new Rect(42, 642, 176, 34), "Only saved changes apply.", t.Caption);
        GUI.Label(new Rect(264, 130, 510, 24), Categories[_view.Category + 1].ToUpperInvariant(), t.Eyebrow);
        GUI.Label(new Rect(800, 130, 272, 24), $"{_view.Matches().Length} RESULTS", t.Right);
        DrawSearch();
        var rows = _view.VisibleRows();
        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i]; var r = new Rect(264, ContentMenuFrame.RowTop + i * ContentMenuFrame.RowStep, 810, ContentMenuFrame.RowHeight);
            var available = FoundationServices.Content.TryGet(row.Key, out var definition) && definition.Enabled;
            var active = _view.Draft[row.Key];
            GUI.enabled = editable && available;
            if (GUI.Button(r, "", active ? t.CardOn : t.Card)) _view.Draft[row.Key] = !active;
            GUI.enabled = true;
            if (active && available) ContentMenuTheme.Fill(new Rect(r.x, r.y, 4, r.height), ContentMenuTheme.Teal);
            GUI.Label(new Rect(r.x + 18, r.y + 8, 510, 28), row.Name, t.Heading);
            var description = row.Description.StartsWith("TC: ", StringComparison.Ordinal) ? row.Description.Substring(4) : row.Description;
            GUI.Label(new Rect(r.x + 18, r.y + 35, 654, 38), description, t.Body);
            GUI.Label(new Rect(r.x + 560, r.y + 10, 110, 20), Categories[(int)row.Category + 1], t.Right);
            GUI.Box(new Rect(r.x + r.width - 116, r.y + 22, 96, 32), available ? (active ? "ON" : "OFF") : "UNAVAILABLE",
                available ? (active ? t.ChipOn : t.ChipOff) : t.ChipLocked);
        }
        if (rows.Length == 0)
        {
            GUI.Box(new Rect(264, 216, 810, 164), "", t.Sidebar);
            GUI.Label(new Rect(292, 246, 700, 32), "No matching content", t.Heading);
            GUI.Label(new Rect(292, 288, 700, 28), "Try another name, effect or category.", t.Muted);
        }
        GUI.enabled = _view.Page > 0;
        if (GUI.Button(new Rect(264, 656, 112, 34), "< PREVIOUS", t.Button)) _view.Page--;
        GUI.enabled = _view.Page + 1 < _view.Pages;
        if (GUI.Button(new Rect(962, 656, 112, 34), "NEXT >", t.Button)) _view.Page++;
        GUI.enabled = true;
        GUI.Label(new Rect(492, 660, 394, 24), $"Page {_view.Page + 1} of {_view.Pages}   |   " + (editable ? "Click a card to toggle" : "Read-only selection"), t.Muted);
        GUI.DrawTexture(new Rect(24, 706, 1052, 1), t.Divider);
        var note = !string.IsNullOrEmpty(_status) ? _status : editable ? "New offers only. Owned content and saved offers are kept." : "Session locked. The host controls this party's selection.";
        GUI.Label(new Rect(30, 724, 632, 44), note, t.Body);
        if (GUI.Button(new Rect(696, 724, 178, 38), _view.DirtyCount > 0 ? "DISCARD CHANGES" : "CLOSE", t.Button)) { Close(); return; }
        GUI.enabled = editable && _view.DirtyCount > 0;
        if (GUI.Button(new Rect(888, 724, 186, 38), "SAVE CHANGES  >", t.Primary)) Save();
        GUI.enabled = true;
    }
    private void DrawSearch()
    {
        var position = new Rect(264, 164, 764, 38);
        if (_searchUnavailable)
        {
            GUI.Box(position, "Search unavailable — use categories and pages", _theme.Field);
            return;
        }
        try
        {
            // Unity 6's named-focus wrappers use an unavailable ReadOnlySpan
            // member in this game's generated bindings. Use the same numeric
            // control path as TextField, without those string wrappers.
            var id = GUIUtility.GetControlID(0x54435345, FocusType.Keyboard, position);
            if (_focusSearch) { GUIUtility.keyboardControl = id; _focusSearch = false; }
            _searchContent ??= new GUIContent();
            _searchContent.text = _view.Search;
            GUI.DoTextField(position, id, _searchContent, false, 128, _theme.Field);
            if (_searchContent.text != _view.Search) _view.SetSearch(_searchContent.text);
            if (_view.Search.Length == 0 && GUIUtility.keyboardControl != id)
                GUI.Label(new Rect(276, 173, 720, 24), "Search names or effects...", _theme.Muted);
            if (GUI.Button(new Rect(1036, 164, 38, 38), "X", _theme.Button))
            { _view.SetSearch(""); _focusSearch = true; }
        }
        catch (Exception e)
        {
            // Search is optional: an editor-binding failure must not discard
            // the styled renderer, categories, paging or existing draft.
            _searchUnavailable = true; _focusSearch = false;
            _view.SetSearch(""); GUIUtility.keyboardControl = 0;
            ContentMenuSettings.Logger?.LogWarning("[Terms & Contusions] search unavailable; styled menu retained: " + e);
        }
    }
    private void SimplePanel()
    {
        if (!ContentMenuFeature.Open)
        {
            GUILayout.BeginArea(new Rect(Screen.width - 240, Screen.height - 70, 220, 50));
            try { if (GUILayout.Button("TC Content")) Open(); } finally { GUILayout.EndArea(); }
            return;
        }
        GUILayout.BeginArea(new Rect((Screen.width - 640) / 2, 20, 640, Math.Min(740, Screen.height - 40)), "TC Content — simple view", GUI.skin.window);
        try
        {
            GUILayout.Space(24);
            GUILayout.Label("Choose before hosting/joining. Click Save to apply.");
            GUILayout.BeginHorizontal();
            for (var i = 0; i < Categories.Length; i++) if (GUILayout.Button(Categories[i])) _view.SelectCategory(i - 1);
            GUILayout.EndHorizontal();
            foreach (var row in _view.VisibleRows())
            {
                GUI.enabled = ContentMenuFeature.CanEdit;
                _view.Draft[row.Key] = GUILayout.Toggle(_view.Draft[row.Key], row.Name);
                GUI.enabled = true; GUILayout.Label(row.Description); GUILayout.Space(8);
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Previous")) _view.Page = Math.Max(0, _view.Page - 1);
            GUILayout.Label($"Page {_view.Page + 1}/{_view.Pages}");
            if (GUILayout.Button("Next")) _view.Page = Math.Min(_view.Pages - 1, _view.Page + 1);
            GUILayout.EndHorizontal();
            GUI.enabled = ContentMenuFeature.CanEdit;
            if (GUILayout.Button("Enable all")) _view.SetAll(true);
            if (GUILayout.Button("Disable all")) _view.SetAll(false);
            if (GUILayout.Button("Save changes")) { Save(); return; }
            GUI.enabled = true;
            if (GUILayout.Button("Close / discard")) Close();
            if (!string.IsNullOrEmpty(_status)) GUILayout.Label(_status);
        }
        finally { GUILayout.EndArea(); }
    }
}
