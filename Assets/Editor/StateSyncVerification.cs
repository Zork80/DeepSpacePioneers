using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AgentBridge;

/// <summary>
/// Automated verification and roundtrip tests for Network State Synchronization and DTO integrity.
/// Can be executed via Unity Test Runner or directly from the Editor menu:
/// "Deep Space Pioneers -> Verify State Synchronization Roundtrip"
/// </summary>
public static class StateSyncVerification
{
    [MenuItem("Deep Space Pioneers/Verify State Synchronization Roundtrip", false, 100)]
    public static void RunAllSyncVerificationsFromMenu()
    {
        Debug.Log("==================================================");
        Debug.Log("[StateSyncVerification] Starting State Synchronization Integrity Verification...");
        int passed = 0;
        int failed = 0;

        void RunCheck(string testName, Action check)
        {
            try
            {
                check();
                passed++;
                Debug.Log($"<color=green>[PASS]</color> {testName}");
            }
            catch (Exception ex)
            {
                failed++;
                Debug.LogError($"<color=red>[FAIL]</color> {testName}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        RunCheck("Encounter State Roundtrip & selectedOutcomeIndex", VerifyEncounterStateRoundtrip);
        RunCheck("Star System Map & Resource Roundtrip", VerifyStarSystemRoundtrip);
        RunCheck("Player & Inventory State Roundtrip", VerifyPlayerStateRoundtrip);
        RunCheck("Fog of War Resource Masking", VerifyFogOfWarMasking);
        RunCheck("Dialog State Roundtrip", VerifyDialogStateRoundtrip);
        RunCheck("Action DTO Serialization Roundtrip", VerifyActionDTORoundtrip);

        Debug.Log("--------------------------------------------------");
        if (failed == 0)
        {
            Debug.Log($"<color=green><b>[SUCCESS] All {passed} State Synchronization checks PASSED!</b></color>");
            EditorUtility.DisplayDialog("State Sync Verification", $"All {passed} state synchronization checks PASSED successfully!\n\nDTOs and engine states are fully aligned.", "OK");
        }
        else
        {
            Debug.LogError($"<color=red><b>[FAILED] {failed} of {passed + failed} checks FAILED! See console for details.</b></color>");
            EditorUtility.DisplayDialog("State Sync Verification", $"{failed} check(s) FAILED! Check the console for details.", "OK");
        }
        Debug.Log("==================================================");
    }

    [Test]
    public static void VerifyEncounterStateRoundtrip()
    {
        var original = new EncounterStateDTO
        {
            isPending = true,
            phase = "ApplyOutcome",
            cardId = "10",
            cardType = "MarauderDemand",
            characterIndex = 0,
            title = "Korsarenforderung",
            description = "Ein Void-Korsar verlangt zwei Rohstoffe.",
            selectedOutcomeIndex = 1,
            outcomes = new EncounterOutcomeDTO[]
            {
                new EncounterOutcomeDTO { index = 0, title = "Du gibst ab", description = "Zahle 2 Rohstoffe", canSelect = false },
                new EncounterOutcomeDTO { index = 1, title = "Sieg im Kampf", description = "Du gewinnst", canSelect = false },
                new EncounterOutcomeDTO { index = 2, title = "Niederlage im Kampf", description = "Du verlierst", canSelect = false }
            }
        };

        string json = JsonUtility.ToJson(original);
        Assert.IsFalse(string.IsNullOrEmpty(json), "JSON serialization produced empty string");
        Assert.IsTrue(json.Contains("\"selectedOutcomeIndex\":1"), "JSON does not contain selectedOutcomeIndex");

        var restored = JsonUtility.FromJson<EncounterStateDTO>(json);
        Assert.IsNotNull(restored, "Deserialization returned null");
        Assert.AreEqual(original.isPending, restored.isPending);
        Assert.AreEqual(original.phase, restored.phase);
        Assert.AreEqual(original.cardId, restored.cardId);
        Assert.AreEqual(original.cardType, restored.cardType);
        Assert.AreEqual(original.selectedOutcomeIndex, restored.selectedOutcomeIndex);
        Assert.AreEqual(original.outcomes.Length, restored.outcomes.Length);
        Assert.AreEqual(original.outcomes[1].title, restored.outcomes[1].title);
    }

    [Test]
    public static void VerifyStarSystemRoundtrip()
    {
        var original = new StarSystemStateDTO
        {
            q = 14,
            r = 7,
            systemType = "TwoStars",
            systemKind = "Planets",
            isRevealed = true,
            productionNumbers = new int[] { 6, 8, 9 },
            resources = new string[] { "Ore", "Food", "Carbon" },
            specialTypes = new string[] { "Normal", "IcePlanet", "Normal" },
            specialRequirements = new int[] { 0, 3, 0 },
            specialResolved = new bool[] { true, false, true },
            alienCivilization = "None"
        };

        string json = JsonUtility.ToJson(original);
        Assert.IsTrue(json.Contains("\"productionNumbers\":[6,8,9]"), "Production numbers missing in JSON");

        var restored = JsonUtility.FromJson<StarSystemStateDTO>(json);
        Assert.IsNotNull(restored);
        Assert.AreEqual(original.q, restored.q);
        Assert.AreEqual(original.r, restored.r);
        Assert.AreEqual(original.isRevealed, restored.isRevealed);
        CollectionAssert.AreEqual(original.productionNumbers, restored.productionNumbers);
        CollectionAssert.AreEqual(original.resources, restored.resources);
        CollectionAssert.AreEqual(original.specialTypes, restored.specialTypes);
        CollectionAssert.AreEqual(original.specialRequirements, restored.specialRequirements);
        CollectionAssert.AreEqual(original.specialResolved, restored.specialResolved);
    }

    [Test]
    public static void VerifyPlayerStateRoundtrip()
    {
        var p = new PlayerStateDTO
        {
            playerIndex = 1,
            name = "Spieler 2",
            playerType = "LOCAL",
            victoryPoints = 5,
            halfMedals = 1,
            regularColonies = 2,
            spaceportsCount = 1,
            tradeStationsCount = 0,
            resources = new ResourceBundleDTO
            {
                ore = 1,
                fuel = 2,
                carbon = 0,
                food = 3,
                tradeGoods = 1,
                total = 7
            },
            flagship = new FlagshipDTO
            {
                boosters = 2,
                cannons = 1,
                freightPods = 2
            },
            colonies = new StructureDTO[]
            {
                new StructureDTO { q = 27, r = 5, type = "Spaceport", ownerIndex = 1 },
                new StructureDTO { q = 28, r = 9, type = "Colony", ownerIndex = 1 }
            }
        };

        string json = JsonUtility.ToJson(p);
        var restored = JsonUtility.FromJson<PlayerStateDTO>(json);

        Assert.IsNotNull(restored);
        Assert.AreEqual(p.playerIndex, restored.playerIndex);
        Assert.AreEqual(p.name, restored.name);
        Assert.AreEqual(p.victoryPoints, restored.victoryPoints);
        Assert.AreEqual(p.resources.total, restored.resources.total);
        Assert.AreEqual(p.resources.food, restored.resources.food);
        Assert.AreEqual(p.flagship.boosters, restored.flagship.boosters);
        Assert.AreEqual(2, restored.colonies.Length);
        Assert.AreEqual("Spaceport", restored.colonies[0].type);
    }

    [Test]
    public static void VerifyFogOfWarMasking()
    {
        var obs = new GameStateObservationDTO
        {
            currentPlayerIndex = 0,
            localPlayerIndex = 0,
            players = new PlayerStateDTO[]
            {
                new PlayerStateDTO
                {
                    playerIndex = 0,
                    name = "Spieler 1 (Host)",
                    resources = new ResourceBundleDTO { ore = 3, fuel = 2, carbon = 1, food = 0, tradeGoods = 0, total = 6 }
                },
                new PlayerStateDTO
                {
                    playerIndex = 1,
                    name = "Spieler 2 (Guest)",
                    resources = new ResourceBundleDTO { ore = 0, fuel = 1, carbon = 2, food = 2, tradeGoods = 1, total = 6 }
                }
            }
        };

        // Mask for Guest (recipientPlayerIndex = 1)
        GameStateObservationDTO maskedForGuest = obs.CreateMaskedCopy(1);
        Assert.IsNotNull(maskedForGuest);
        Assert.AreEqual(1, maskedForGuest.localPlayerIndex);

        // Opponent (Player 0) detailed resources MUST be masked to 0, but total preserved
        PlayerStateDTO opponent = maskedForGuest.players[0];
        Assert.AreEqual(6, opponent.resources.total, "Opponent total resource count must be preserved");
        Assert.AreEqual(0, opponent.resources.ore, "Opponent ore must be hidden (Fog of War)");
        Assert.AreEqual(0, opponent.resources.fuel, "Opponent fuel must be hidden (Fog of War)");

        // Recipient's own resources MUST NOT be masked
        PlayerStateDTO me = maskedForGuest.players[1];
        Assert.AreEqual(6, me.resources.total);
        Assert.AreEqual(1, me.resources.fuel);
        Assert.AreEqual(2, me.resources.carbon);
    }

    [Test]
    public static void VerifyDialogStateRoundtrip()
    {
        var original = new DialogStateDTO
        {
            isSevenRollResolving = true,
            isDiscardDialogOpen = true,
            discardPlayerIndex = 1,
            discardRequiredCount = 4,
            isStealDialogOpen = false,
            eligibleStealTargetIndices = new int[] { 0, 2 },
            isFriendshipCardDialogOpen = false,
            friendshipCardOptions = new FriendshipCardOptionDTO[0]
        };

        string json = JsonUtility.ToJson(original);
        var restored = JsonUtility.FromJson<DialogStateDTO>(json);

        Assert.IsNotNull(restored);
        Assert.IsTrue(restored.isSevenRollResolving);
        Assert.IsTrue(restored.isDiscardDialogOpen);
        Assert.AreEqual(1, restored.discardPlayerIndex);
        Assert.AreEqual(4, restored.discardRequiredCount);
        CollectionAssert.AreEqual(new int[] { 0, 2 }, restored.eligibleStealTargetIndices);
    }

    [Test]
    public static void VerifyActionDTORoundtrip()
    {
        var action = new AgentActionDTO
        {
            actionId = "test-1234",
            action = "SelectEncounterOutcome",
            outcomeIndex = 1,
            ore = 1,
            food = 2,
            hasTargetUnit = true,
            targetUnitQ = 23,
            targetUnitR = 7
        };

        string json = JsonUtility.ToJson(action);
        var restored = JsonUtility.FromJson<AgentActionDTO>(json);

        Assert.IsNotNull(restored);
        Assert.AreEqual(action.actionId, restored.actionId);
        Assert.AreEqual(action.action, restored.action);
        Assert.AreEqual(1, restored.outcomeIndex);
        Assert.AreEqual(1, restored.ore);
        Assert.AreEqual(2, restored.food);
        Assert.IsTrue(restored.hasTargetUnit);
        Assert.AreEqual(23, restored.targetUnitQ);
        Assert.AreEqual(7, restored.targetUnitR);
    }
}
