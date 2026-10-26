using System;
using BepInEx.Logging;

namespace ChainedBeasts.ModFoundation.DevTools;

/// <summary>
/// Notifications from developer tools. These are not general gameplay events:
/// SkillRemoved fires only after this menu calls the native RemoveSkill method.
/// </summary>
public static class DeveloperToolsEvents
{
    internal static ManualLogSource Logger;

    public static event Action<PlayerData, SkillType> SkillRemoved;

    internal static void NotifySkillRemoved(PlayerData playerData, SkillType skillType)
    {
        var handlers = SkillRemoved;
        if (handlers is null)
        {
            return;
        }

        foreach (Action<PlayerData, SkillType> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(playerData, skillType);
            }
            catch (Exception exception)
            {
                Logger?.LogError(
                    $"[ChainedBeasts.ModFoundation] developer skill-removal subscriber failed: {exception}");
            }
        }
    }
}
