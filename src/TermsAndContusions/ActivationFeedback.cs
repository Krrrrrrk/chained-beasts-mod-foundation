using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using ChainedBeasts.ModFoundation.Runtime;
using UnityEngine;

namespace TermsAndContusions;

/// <summary>Local cosmetic acknowledgement of actual benefits; never grants or repeats gameplay effects.</summary>
internal static class ActivationFeedback
{
    private static readonly FeedbackGate Gate = new();
    private static readonly HashSet<string> Errors = new(StringComparer.Ordinal);
    private static ManualLogSource _logger;
    private static bool _installed;
    internal static void Install(ConfigFile config, ManualLogSource logger)
    {
        if (_installed) return;
        _installed = true; _logger = logger;
        var enabled = config.Bind("Feedback", "EnableActivationFeedback", true,
            "Play a brief local dust/swish cue when a TC ability grants a confirmed benefit. Cosmetic only; restart to apply.").Value;
        logger.LogInfo("[Terms & Contusions] EnableActivationFeedback=" + enabled);
        if (!enabled) return;
        CombatServices.AbilityActivated += Activated;
        FoundationServices.RunReset += Gate.Clear;
    }
    private static void Activated(Player player, string contentKey, double actualBenefit)
    {
        try
        {
            if (!SessionAccess.GameplayReady || player is null || !player.IsOwner || player.IsDead ||
                player.PlayerData is null || !PFXManager.InstanceExists) return;
            if (!Gate.TryActivate(true, true, player.PlayerData.playerID, contentKey, actualBenefit, Time.unscaledTimeAsDouble)) return;
            // The inspected native dust puff already includes its short swish sound.
            // Reuse that single local API; do not replay healing, combat, or a second sound RPC.
            PFXManager.Instance.EmitDustPuff(player.transform.position);
        }
        catch (Exception exception)
        {
            var error = exception.GetType().Name + ": " + exception.Message;
            if (Errors.Count < 4 && Errors.Add(error))
                _logger?.LogWarning("[TC Feedback] cosmetic cue unavailable; gameplay continues: " + error);
        }
    }
}
