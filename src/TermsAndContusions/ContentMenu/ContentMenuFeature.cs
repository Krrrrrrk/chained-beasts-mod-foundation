using System;
using ChainedBeasts.ModFoundation;
using ChainedBeasts.ModFoundation.Runtime;
using HarmonyLib;
using Unity.Netcode;

namespace TermsAndContusions.ContentMenu;

internal static class ContentMenuFeature
{
    private static UIMainMenu _menu;
    internal static bool Open;
    internal static int BlockNavigationUntilFrame;
    internal static bool Visible
    {
        get
        {
            try { return _menu != null && _menu.isActiveAndEnabled && _menu.gameObject.activeInHierarchy &&
                _menu.InFocus && (_menu.fadeGroup == null || _menu.fadeGroup.alpha > .9f); }
            catch { return false; } // Main-menu teardown may precede its managed wrapper.
        }
    }
    internal static bool CanEdit => Visible && !ContentOfferServices.SessionLocked && NetworkManager.Singleton?.IsListening != true;
    internal static void Install(Harmony harmony)
    {
        FeaturePatcher.Patch(harmony, typeof(ContentMenuFeature), FeaturePatcher.RequireMethod(typeof(UIMainMenu), nameof(UIMainMenu.Initialise)), postfix: nameof(Initialized));
        FeaturePatcher.Patch(harmony, typeof(ContentMenuFeature), FeaturePatcher.RequireMethod(typeof(UIMainMenu), nameof(UIMainMenu.HandleNavigationButtonSelected),
            typeof(UIClientNavigationButton), typeof(ClientIdentifier)), prefix: nameof(Navigation));
    }
    private static void Initialized(UIMainMenu __instance) { _menu = __instance; Open = false; }
    private static bool Navigation() => !Visible || (!Open && UnityEngine.Time.frameCount > BlockNavigationUntilFrame);
}
