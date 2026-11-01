#if TC_EMBEDDED_CAMERA
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
#endif
using BepInEx.Configuration;
using UnityEngine.InputSystem;

#if TC_EMBEDDED_CAMERA
namespace TermsAndContusions.FirstPerson;
#else
namespace ChainedBeasts.FirstPerson;
#endif

internal sealed class Settings
{
    internal readonly bool Enabled, StartFirstPerson, InvertY, HideHead, HideBody, Crosshair, PadToggle;
    internal readonly Key ToggleKey;
    internal readonly float Fov, MouseSensitivity, StickSpeed, NearClip, HeightOffset, ForwardOffset;

    internal Settings(ConfigFile config, string sectionPrefix = "")
    {
        Enabled = config.Bind(sectionPrefix + "General", "Enabled", true, "Enable the manual first-person toggle on next launch. Temporary potion locks work independently.").Value;
        StartFirstPerson = config.Bind(sectionPrefix + "General", "StartInFirstPerson", false,
            "Enter first person automatically during arena play. Otherwise press the toggle to opt in.").Value;
        ToggleKey = config.Bind(sectionPrefix + "Controls", "ToggleKey", Key.F9, "Keyboard key to switch first/third person.").Value;
        PadToggle = config.Bind(sectionPrefix + "Controls", "RightStickClickToggle", false,
            "Optional paired-gamepad right-stick click toggle. Off by default to avoid conflicting bindings.").Value;
        InvertY = config.Bind(sectionPrefix + "Controls", "InvertY", false, "Invert vertical mouse and stick look.").Value;
        MouseSensitivity = ViewMath.Bounded(config.Bind(sectionPrefix + "Controls", "MouseSensitivity", 0.12f,
            "Degrees per mouse pixel; 0.01 to 2. Mouse look does not depend on frame rate.").Value, .12f, .01f, 2);
        StickSpeed = ViewMath.Bounded(config.Bind(sectionPrefix + "Controls", "StickDegreesPerSecond", 150f,
            "Paired gamepad right-stick look speed; 20 to 360 degrees/second.").Value, 150, 20, 360);
        Fov = ViewMath.Bounded(config.Bind(sectionPrefix + "Camera", "FieldOfView", 85f,
            "Vertical first-person field of view; 50 to 110.").Value, 85, 50, 110);
        NearClip = ViewMath.Bounded(config.Bind(sectionPrefix + "Camera", "NearClip", .04f,
            "First-person near clipping plane; 0.02 to 0.2.").Value, .04f, .02f, .2f);
        HeightOffset = ViewMath.Bounded(config.Bind(sectionPrefix + "Camera", "EyeHeightOffset", .06f,
            "World units above the animated head anchor; -0.5 to 0.5.").Value, .06f, -.5f, .5f);
        ForwardOffset = ViewMath.Bounded(config.Bind(sectionPrefix + "Camera", "EyeForwardOffset", .08f,
            "World units in front of the head along horizontal view direction; 0 to 0.3.").Value, .08f, 0, .3f);
        HideHead = config.Bind(sectionPrefix + "Camera", "HideOwnHead", true,
            "Hide the viewed player's head, hair, beard, brows, mask and crown while in first person.").Value;
        HideBody = config.Bind(sectionPrefix + "Camera", "HideOwnBody", true,
            "Hide your torso, limbs and body cosmetics in first person. Renderers under hand, bow and back equipment attachments stay visible. Unusual equipment may still be hidden.").Value;
        Crosshair = config.Bind(sectionPrefix + "Camera", "ShowCrosshair", true,
            "Show a small center crosshair. Combat remains horizontal under the game's native rules.").Value;
    }
}
