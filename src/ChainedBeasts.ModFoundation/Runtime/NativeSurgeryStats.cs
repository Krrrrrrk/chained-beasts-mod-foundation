using System;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using ChainedBeasts.ModFoundation.Stats;

namespace ChainedBeasts.ModFoundation.Runtime;

/// <summary>Fresh native stats and pre-clamp healing amounts. No resource grants on getters or restore.</summary>
internal static class NativeSurgeryStats
{
    internal static void Install(Harmony harmony)
    {
        void Patch(Type type, string name, string before = null, string after = null, params Type[] args) =>
            FeaturePatcher.Patch(harmony, typeof(NativeSurgeryStats), FeaturePatcher.RequireMethod(type, name, args), before, after);
        FeaturePatcher.Patch(harmony, typeof(NativeSurgeryStats), FeaturePatcher.RequirePropertyGetter(typeof(Player), nameof(Player.StaminaMax)), postfix: nameof(MaximumStamina));
        Patch(typeof(Player), nameof(Player.GetVelocity), after: nameof(Velocity), args: new[] { typeof(Vector3) });
        Patch(typeof(NetworkAgent), nameof(NetworkAgent.TryKnockback), nameof(Knockback), args: new[] { typeof(Vector3), typeof(float) });
        Patch(typeof(NetworkHealthObject), nameof(NetworkHealthObject.HealRpc), nameof(Heal), args: new[] { typeof(int) });
        Patch(typeof(RunDataManager), nameof(RunDataManager.HealRpc), nameof(LobbyHeal), args: new[] { typeof(ClientIdentifier), typeof(int) });
        Patch(typeof(RunDataManager), nameof(RunDataManager.GetPotionHealAmount), after: nameof(PotionHeal), args: new[] { typeof(PlayerData), typeof(Potion) });
        Patch(typeof(GladiatorConsts), nameof(GladiatorConsts.SurgeriesHealAmount), after: nameof(SoapHeal), args: new[] { typeof(int) });
        Patch(typeof(SurgeryGenerator), nameof(SurgeryGenerator.GetSurgeries), after: nameof(SurgeryHeal), args: new[] { typeof(PlayerData), typeof(int), typeof(int), typeof(bool), typeof(int), typeof(RunDataManager), typeof(Il2CppSystem.Collections.Generic.Dictionary<InjuryType, int>) });
    }
    private static double Factor(StatChannel channel, PlayerData data, Player player = null) => SessionAccess.CanUseDeveloperTools
        ? NativeCatalogAndStats.Evaluate(channel, 1, data, player) : 1;
    private static int Healing(int amount, PlayerData data, Player player = null)
    {
        var factor = Factor(StatChannel.HealingAmount, data, player);
        return amount <= 0 || factor == 1 ? amount : (int)Math.Clamp(Math.Round(amount * factor, MidpointRounding.AwayFromZero), 0, int.MaxValue / 4);
    }
    private static void MaximumStamina(Player __instance, ref float __result)
    { var factor = Factor(StatChannel.MaximumStamina, __instance.PlayerData, __instance); if (factor != 1) __result = (float)Math.Clamp(__result * factor, .001, float.MaxValue); }
    private static void Velocity(Player __instance, ref Vector3 __result)
    {
        if (!__instance.IsDashing) return;
        var factor = (float)Factor(StatChannel.DodgeDistance, __instance.PlayerData, __instance);
        __result.x *= factor; __result.z *= factor;
    }
    private static void Knockback(NetworkAgent __instance, ref float distance)
    {
        if (!SessionAccess.CanUseDeveloperTools || distance <= 0) return;
        var player = __instance.TryCast<Player>();
        if (player is not null) distance *= (float)Factor(StatChannel.IncomingKnockback, player.PlayerData, player);
        var attack = NativeCombat.CurrentAttack;
        if (attack is not null && attack.Player?.Pointer != __instance.Pointer)
            distance *= (float)CombatServices.KnockbackMultiplier(attack, __instance);
    }
    private static void Heal(NetworkHealthObject __instance, ref int healAmount)
    {
        if (__instance.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute) return;
        var player = __instance.TryCast<Player>();
        if (player is not null) healAmount = Healing(healAmount, player.PlayerData, player);
    }
    private static void LobbyHeal(RunDataManager __instance, ClientIdentifier clientID, ref int amount)
    { if (SessionAccess.CanUseDeveloperTools && clientID is not null && __instance.__rpc_exec_stage == NetworkBehaviour.__RpcExecStage.Execute) amount = Healing(amount, __instance.GetPlayerDataByIndex(checked((int)clientID.PlayerIndex))); }
    private static void PotionHeal(PlayerData playerData, ref int __result) => __result = Healing(__result, playerData);
    private static void SoapHeal(ref int __result) { if (NativeSurgeries.OperatingPlayer is not null) __result = Healing(__result, NativeSurgeries.OperatingPlayer); }
    private static void SurgeryHeal(PlayerData playerData, Il2CppSystem.Collections.Generic.List<Surgery> __result)
    { if (__result is not null) for (var i = 0; i < __result.Count; i++) { var surgery = __result[i]; if (surgery?.damage < 0) surgery.damage = -Healing(-surgery.damage, playerData); } }
}
