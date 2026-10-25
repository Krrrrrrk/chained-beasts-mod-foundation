using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChainedBeasts.ModFoundation.DevTools;

/// <summary>
/// Solo-host offer rerolls for the lobby choice screens. Only the
/// backing list for the visible screen is changed; the run-wide option
/// generator is deliberately not called because it also prepares unrelated
/// future rooms.
/// </summary>
internal static class OfferRerollFeature
{
    private const int SkillSeedSalt = 0x534B494C;
    private const int PotionSeedSalt = 0x504F544E;
    private const int ShopSeedSalt = 0x53484F50;
    private const int SurgerySeedSalt = 0x53555247;

    internal static bool TryRerollActiveScreen(
        RunDataManager manager,
        int rerollSequence,
        out string message)
    {
        var trainer = FindActiveTrainer();
        if (trainer is not null)
        {
            return TryRerollSkills(manager, trainer, rerollSequence, out message);
        }

        var doctor = FindActiveDoctor();
        if (doctor is not null)
        {
            return TryRerollPotions(manager, doctor, rerollSequence, out message);
        }

        var shop = FindActiveShop();
        if (shop is not null)
        {
            return TryRerollShop(manager, shop, rerollSequence, out message);
        }

        var surgeon = FindActiveSurgeon();
        if (surgeon is not null)
        {
            return TryRerollSurgeries(manager, surgeon, rerollSequence, out message);
        }

        message = "Open a skill, potion, shop, or surgery offer screen before rerolling";
        return false;
    }

    private static bool TryRerollSkills(
        RunDataManager manager,
        UIGameLobbyTrainerRpc trainer,
        int rerollSequence,
        out string message)
    {
        var state = trainer.uiState;
        if (state is null || (UIGameLobbyTrainerRpc.eState)state.state != UIGameLobbyTrainerRpc.eState.LEVEL_UP)
        {
            message = "The trainer is open, but the skill-choice screen is not active";
            return false;
        }

        var layer = state.trainingLayer;
        var current = manager.trainingSkills;
        if (current is null || layer < 0 || layer >= current.Count)
        {
            message = $"Skill reroll cannot find training layer {layer}";
            return false;
        }

        var seed = BuildSeed(manager, rerollSequence, SkillSeedSalt);
        var generated = SkillGenerator.GenerateTrainingSkills(seed);
        if (generated is null || layer >= generated.Count || generated[layer] is null)
        {
            message = "The game did not generate a replacement skill set";
            return false;
        }

        var previous = current[layer];
        try
        {
            current[layer] = generated[layer];
            state.ResetUpgradeSelections();
            trainer.OnTrainingSkillsChanged(layer);
            message = $"Rerolled the current skill offers (layer {layer}, seed {seed})";
            return true;
        }
        catch
        {
            current[layer] = previous;
            throw;
        }
    }

    private static bool TryRerollPotions(
        RunDataManager manager,
        UIGameLobbyDoctorRpc doctor,
        int rerollSequence,
        out string message)
    {
        if (doctor.potionsConsumed || doctor.executeSelectionsScheduled || doctor.executed)
        {
            message = "Potion reroll is unavailable after the room has started resolving selections";
            return false;
        }

        var controller = doctor.uiLobbyDoctor;
        var state = doctor.uiState;
        if (controller is null || state is null || controller.selectionPages is null)
        {
            message = "The potion screen is not ready to reroll";
            return false;
        }

        var seed = BuildSeed(manager, rerollSequence, PotionSeedSalt);
        var playerCount = manager.playerDatas is null ? 1 : Math.Max(1, manager.playerDatas.Count);
        var generated = InjuryGenerator.GeneratePotions(
            manager,
            playerCount,
            seed,
            out _);
        if (generated is null || generated.Count != controller.selectionPages.Count)
        {
            message = "The replacement potion count did not match the visible potion cards";
            return false;
        }

        var previous = manager.potions;
        manager.potions = generated;
        try
        {
            ReplaceDoctorPages(controller, generated);
        }
        catch
        {
            manager.potions = previous;
            throw;
        }

        ResetDoctorSelections(state, generated.Count);
        controller.state = state;
        controller.Refresh(state);
        message = $"Rerolled the current potion offers (seed {seed})";
        return true;
    }

    private static bool TryRerollShop(
        RunDataManager manager,
        UIGameLobbyShopRpc shop,
        int rerollSequence,
        out string message)
    {
        if (shop.executed || shop.waitingForTransitionOut)
        {
            message = "Shop reroll is unavailable after purchases have started resolving";
            return false;
        }

        var controller = shop.ui;
        var state = shop.uiState;
        if (controller is null || state is null || controller.selectionPages is null)
        {
            message = "The shop screen is not ready to reroll";
            return false;
        }

        var seed = BuildSeed(manager, rerollSequence, ShopSeedSalt);
        var shopNumber = (int)GladiatorConsts.GetAct(manager.completedLevels);
        var generated = PurchaseGenerator.GenerateShopOptions(seed, shopNumber);
        if (generated is null || generated.Count != controller.selectionPages.Count)
        {
            message = "The replacement shop-offer count did not match the visible cards";
            return false;
        }

        var previous = manager.currentShopOptions;
        manager.currentShopOptions = generated;
        try
        {
            ReplaceShopPages(controller, generated.Count);
        }
        catch
        {
            manager.currentShopOptions = previous;
            throw;
        }

        ResetShopSelections(state, generated.Count);
        controller.state = state;
        controller.previousCost = 0;
        controller.previousCostAboveZero = false;
        controller.Refresh(state);
        message = $"Rerolled the current shop offers (seed {seed})";
        return true;
    }

    private static bool TryRerollSurgeries(
        RunDataManager manager,
        UIGameLobbySurgeonRpc surgeon,
        int rerollSequence,
        out string message)
    {
        var controller = surgeon.uiLobbySurgeon;
        var state = surgeon.uiState;
        if (controller is null || state is null || controller.playerPages is null)
        {
            message = "The surgery screen is not ready to reroll";
            return false;
        }

        if ((UIGameLobbySurgeonRpc.eState)state.state != UIGameLobbySurgeonRpc.eState.SELECTION ||
            controller.executeCoroutine is not null || surgeon.tutorialDialogueActive ||
            surgeon.senateDialogueActive || controller.isTutorialActive)
        {
            message = "Surgery reroll is unavailable during dialogue or while surgery is resolving";
            return false;
        }

        // Each surgeon panel has its own offers and option-selection arrays.
        // Keep this testing tool limited to one local solo player, so rerolling
        // cannot invalidate another player's money or donation reservation.
        if (state.clientCount != 1 || controller.playerPages.Count != 1 ||
            state.ready is null || state.ready.Length != 1 ||
            state.surgeryIndex is null || state.surgeryIndex.Length != 1 ||
            state.surgeryCount is null || state.surgeryCount.Length != 1 ||
            state.surgeryData is null || state.surgeryData.Length != 1)
        {
            message = "Surgery reroll currently supports one solo player";
            return false;
        }

        if (state.ready[0])
        {
            message = "Unconfirm the surgery selection before rerolling";
            return false;
        }

        var playerData = manager.GetPlayerDataByIndex(0);
        var oldPage = controller.playerPages[0];
        var previousOffers = playerData?.surgeryOptions;
        if (previousOffers is null || previousOffers.Count == 0 || oldPage is null ||
            controller.playerPagePrefab is null || oldPage.playerCursor is null)
        {
            message = "The current surgery offers or player panel are unavailable";
            return false;
        }

        if (!oldPage.finishedEntryAnimation)
        {
            message = "Wait for the surgery panel to finish opening before rerolling";
            return false;
        }

        var seed = BuildSeed(manager, rerollSequence, SurgerySeedSalt);
        Il2CppSystem.Collections.Generic.List<Surgery> generated;
        // GetSurgeries uses RandomUtils rather than accepting a seed. Restore
        // the original RNG object so the reroll does not advance other systems.
        var previousRandom = RandomUtils.rand;
        try
        {
            RandomUtils.SetSeed(seed);
            generated = SurgeryGenerator.GetSurgeries(
                playerData, previousOffers.Count, manager.Money, true, 0, manager, null);
        }
        finally
        {
            RandomUtils.rand = previousRandom;
        }

        if (generated is null || generated.Count != previousOffers.Count)
        {
            message = "The replacement surgery count did not match the visible options";
            return false;
        }

        var previousIndex = state.surgeryIndex[0];
        var previousCount = state.surgeryCount[0];
        var previousData = state.surgeryData[0];
        var previousMoneyOptions = controller.hasMoneyOptions;
        UILobbySurgeonPlayerPage replacement = null;
        try
        {
            playerData.surgeryOptions = generated;
            state.surgeryIndex[0] = 0;
            state.surgeryCount[0] = generated.Count;
            state.surgeryData[0] = new UIGameLobbySurgeonRpc.SurgeryData(playerData);
            surgeon.UpdateStateSelectedOption(0);

            // Action widgets retain Surgery references, so Refresh alone would
            // keep the old labels/choices. Recreate the one visible panel via
            // the same initializer used when the surgeon screen first opens.
            replacement = UnityEngine.Object.Instantiate(
                controller.playerPagePrefab, oldPage.transform.parent);
            replacement.transform.SetSiblingIndex(oldPage.transform.GetSiblingIndex());
            replacement.Initialise(surgeon, 0, oldPage.playerCursor);
            replacement.gameObject.SetActive(oldPage.gameObject.activeSelf);
            controller.playerPages[0] = replacement;

            controller.hasMoneyOptions = false;
            for (var index = 0; index < generated.Count; index++)
            {
                if (generated[index].moneyCost != 0)
                {
                    controller.hasMoneyOptions = true;
                    break;
                }
            }

            controller.uiMoneyPanel?.SetMoneyOptions(controller.hasMoneyOptions);
            controller.Refresh(state);
            replacement.AnimateIn(0f);
            oldPage.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(oldPage.gameObject);
        }
        catch
        {
            playerData.surgeryOptions = previousOffers;
            state.surgeryIndex[0] = previousIndex;
            state.surgeryCount[0] = previousCount;
            state.surgeryData[0] = previousData;
            controller.playerPages[0] = oldPage;
            controller.hasMoneyOptions = previousMoneyOptions;
            controller.uiMoneyPanel?.SetMoneyOptions(previousMoneyOptions);
            if (replacement is not null)
            {
                UnityEngine.Object.Destroy(replacement.gameObject);
            }

            throw;
        }

        message = $"Rerolled the current surgery offers (seed {seed})";
        return true;
    }

    private static void ReplaceDoctorPages(
        UILobbyDoctor controller,
        Il2CppSystem.Collections.Generic.List<Potion> offers)
    {
        if (controller.selectionPagePrefab is null)
        {
            throw new InvalidOperationException("Potion selection-page prefab is unavailable.");
        }

        var replacements = new List<UILobbyDoctorSelectionPage>(offers.Count);
        try
        {
            for (var index = 0; index < offers.Count; index++)
            {
                var oldPage = controller.selectionPages[index];
                if (oldPage is null)
                {
                    throw new InvalidOperationException($"Potion card {index} is unavailable.");
                }

                var replacement = UnityEngine.Object.Instantiate(
                    controller.selectionPagePrefab,
                    oldPage.transform.parent);
                replacement.transform.SetSiblingIndex(oldPage.transform.GetSiblingIndex());
                replacement.Initialise(
                    controller,
                    index,
                    offers[index],
                    oldPage.potionPosition,
                    oldPage.cameraPosition);
                replacement.gameObject.SetActive(oldPage.gameObject.activeSelf);

                replacements.Add(replacement);
            }

            for (var index = 0; index < replacements.Count; index++)
            {
                var oldPage = controller.selectionPages[index];
                oldPage.gameObject.SetActive(false);
                controller.selectionPages[index] = replacements[index];
                UnityEngine.Object.Destroy(oldPage.gameObject);
            }
        }
        catch
        {
            foreach (var replacement in replacements)
            {
                if (replacement is not null)
                {
                    UnityEngine.Object.Destroy(replacement.gameObject);
                }
            }

            throw;
        }
    }

    private static void ReplaceShopPages(UILobbyShop controller, int offerCount)
    {
        if (controller.selectionPagePrefab is null)
        {
            throw new InvalidOperationException("Shop selection-page prefab is unavailable.");
        }

        var replacements = new List<UILobbyShopSelectionPage>(offerCount);
        try
        {
            for (var index = 0; index < offerCount; index++)
            {
                var oldPage = controller.selectionPages[index];
                if (oldPage is null)
                {
                    throw new InvalidOperationException($"Shop card {index} is unavailable.");
                }

                var replacement = UnityEngine.Object.Instantiate(
                    controller.selectionPagePrefab,
                    oldPage.transform.parent);
                replacement.transform.SetSiblingIndex(oldPage.transform.GetSiblingIndex());
                replacement.Initialise(
                    controller,
                    index,
                    oldPage.cameraPosition,
                    oldPage.worldAnchor);
                replacement.gameObject.SetActive(oldPage.gameObject.activeSelf);

                replacements.Add(replacement);
            }

            for (var index = 0; index < replacements.Count; index++)
            {
                var oldPage = controller.selectionPages[index];
                oldPage.gameObject.SetActive(false);
                controller.selectionPages[index] = replacements[index];
                UnityEngine.Object.Destroy(oldPage.gameObject);
            }
        }
        catch
        {
            foreach (var replacement in replacements)
            {
                if (replacement is not null)
                {
                    UnityEngine.Object.Destroy(replacement.gameObject);
                }
            }

            throw;
        }
    }

    private static void ResetDoctorSelections(UIGameLobbyDoctorRpc.UIState state, int offerCount)
    {
        for (var index = 0; index < state.selected.Length; index++)
        {
            state.selected[index] = false;
            state.selectedIndex[index] = Math.Max(0, Math.Min(offerCount - 1, state.selectedIndex[index]));
            state.autoSelect[index] = false;
            state.waitingForAutoSelect[index] = false;
            state.twitchIsSelected[index] = false;
            state.twitchNav[index] = false;
        }
    }

    private static void ResetShopSelections(UIGameLobbyShopRpc.UIState state, int offerCount)
    {
        state.itemCount = offerCount;
        for (var index = 0; index < state.purchased.Length; index++)
        {
            state.purchased[index] = false;
            state.locked[index] = false;
        }

        for (var index = 0; index < state.ready.Length; index++)
        {
            state.ready[index] = false;
            state.navigatePurchase[index] = false;
            state.navigatePurchaseIndex[index] = Math.Max(
                0,
                Math.Min(offerCount - 1, state.navigatePurchaseIndex[index]));
        }

        state.canAfford = true;
    }

    private static int BuildSeed(RunDataManager manager, int rerollSequence, int salt)
    {
        unchecked
        {
            var seed = manager.RunSeed;
            seed = (seed * 397) ^ manager.CompletedLevels;
            seed = (seed * 397) ^ Math.Max(1, rerollSequence);
            return seed ^ salt;
        }
    }

    private static UIGameLobbyTrainerRpc FindActiveTrainer()
    {
        var instances = Resources.FindObjectsOfTypeAll<UIGameLobbyTrainerRpc>();
        for (var index = 0; index < instances.Length; index++)
        {
            var instance = instances[index];
            if (instance is not null && instance.uiLobbyTrainer is not null &&
                instance.uiLobbyTrainer.gameObject.activeInHierarchy && instance.uiLobbyTrainer.InFocus)
            {
                return instance;
            }
        }

        return null;
    }

    private static UIGameLobbyDoctorRpc FindActiveDoctor()
    {
        var instances = Resources.FindObjectsOfTypeAll<UIGameLobbyDoctorRpc>();
        for (var index = 0; index < instances.Length; index++)
        {
            var instance = instances[index];
            if (instance is not null && instance.uiLobbyDoctor is not null &&
                instance.uiLobbyDoctor.gameObject.activeInHierarchy && instance.uiLobbyDoctor.InFocus)
            {
                return instance;
            }
        }

        return null;
    }

    private static UIGameLobbyShopRpc FindActiveShop()
    {
        var instances = Resources.FindObjectsOfTypeAll<UIGameLobbyShopRpc>();
        for (var index = 0; index < instances.Length; index++)
        {
            var instance = instances[index];
            if (instance is not null && instance.ui is not null && instance.ui.gameObject.activeInHierarchy &&
                instance.ui.InFocus)
            {
                return instance;
            }
        }

        return null;
    }

    private static UIGameLobbySurgeonRpc FindActiveSurgeon()
    {
        var instances = Resources.FindObjectsOfTypeAll<UIGameLobbySurgeonRpc>();
        for (var index = 0; index < instances.Length; index++)
        {
            var instance = instances[index];
            if (instance is not null && instance.uiLobbySurgeon is not null &&
                instance.uiLobbySurgeon.gameObject.activeInHierarchy && instance.uiLobbySurgeon.InFocus)
            {
                return instance;
            }
        }

        return null;
    }
}
