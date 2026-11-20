using System;
using System.Collections.Generic;
using UnityEngine;

namespace AgentBridge
{
    [Serializable]
    public class ResourceBundleDTO
    {
        public int ore;
        public int fuel;
        public int carbon;
        public int food;
        public int tradeGoods;
        public int total;

        public static ResourceBundleDTO FromPlayer(Player p)
        {
            if (p == null) return new ResourceBundleDTO();
            return new ResourceBundleDTO
            {
                ore = p.GetResourceCount(ResourceType.Ore),
                fuel = p.GetResourceCount(ResourceType.Fuel),
                carbon = p.GetResourceCount(ResourceType.Carbon),
                food = p.GetResourceCount(ResourceType.Food),
                tradeGoods = p.GetResourceCount(ResourceType.TradeGoods),
                total = p.TotalResources
            };
        }
    }

    [Serializable]
    public class FlagshipDTO
    {
        public int boosters;
        public int cannons;
        public int freightPods;

        public static FlagshipDTO FromPlayer(Player p)
        {
            if (p == null) return new FlagshipDTO();
            return new FlagshipDTO
            {
                boosters = p.FlagshipDriveCount,
                cannons = p.FlagshipCannonCount,
                freightPods = p.FlagshipCargoModuleCount
            };
        }
    }

    [Serializable]
    public class UnitDTO
    {
        public int id;
        public string shipType;
        public int q;
        public int r;
        public int movementRemaining;
        public int movementMax;
        public bool canMove;

        public static string FastStarshipTypeToString(StarshipType type)
        {
            return type == StarshipType.TradeShip ? "TradeShip" : "ColonyShip";
        }

        public static UnitDTO FromUnit(Unit u, TriMap map)
        {
            if (u == null) return null;
            return new UnitDTO
            {
                id = u.GetHashCode(),
                shipType = FastStarshipTypeToString(u.ShipType),
                q = u.Tri != null ? u.Tri.Q : 0,
                r = u.Tri != null ? u.Tri.R : 0,
                movementRemaining = u.MovementRemaining,
                movementMax = u.Movement,
                canMove = map != null && map.CanMoveUnit(u)
            };
        }
    }

    [Serializable]
    public class StructureDTO
    {
        public int q;
        public int r;
        public string type;
        public int ownerIndex;
        public string folkType;
    }

    [Serializable]
    public class PlayerStateDTO
    {
        public int playerIndex;
        public string name;
        public string playerType;
        public int victoryPoints;
        public int halfMedals;
        public ResourceBundleDTO resources;
        public FlagshipDTO flagship;
        public int totalColonies;
        public int regularColonies;
        public int spaceportsCount;
        public int tradeStationsCount;
        public int availableShips;
        public int availableColonies;
        public int availableSpaceports;
        public int availableTradeStations;
        public int availableBoosters;
        public int availableCannons;
        public int availableFreightPods;
        public int capturedTradeShips;
        public UnitDTO[] units;
        public StructureDTO[] colonies;
        public StructureDTO[] tradeStations;
        public string[] friendshipCards;

        public static string FastPlayerTypeToString(Player.PlayerType type)
        {
            switch (type)
            {
                case Player.PlayerType.AI: return "AI";
                case Player.PlayerType.REMOTE: return "REMOTE";
                default: return "LOCAL";
            }
        }

        public static string FastAlienCivilizationToString(AlienCivilization civ)
        {
            switch (civ)
            {
                case AlienCivilization.Scientist: return "Scientist";
                case AlienCivilization.Diplomat: return "Diplomat";
                case AlienCivilization.Trader: return "Trader";
                case AlienCivilization.Cultivator: return "Cultivator";
                default: return string.Empty;
            }
        }

        public static PlayerStateDTO FromPlayer(Player p, int index, TriMap map)
        {
            if (p == null) return null;

            List<UnitDTO> unitList = new List<UnitDTO>(p.Units != null ? p.Units.Length : 0);
            if (p.Units != null)
            {
                foreach (Unit u in p.Units)
                {
                    if (u != null && !u.IsDestroyed)
                    {
                        unitList.Add(UnitDTO.FromUnit(u, map));
                    }
                }
            }

            List<StructureDTO> colonyList = new List<StructureDTO>(p.Colonies != null ? p.Colonies.Length : 0);
            if (p.Colonies != null)
            {
                foreach (Colony c in p.Colonies)
                {
                    if (c != null && c.Tri != null)
                    {
                        colonyList.Add(new StructureDTO
                        {
                            q = c.Tri.Q,
                            r = c.Tri.R,
                            type = c.IsSpaceport ? "Spaceport" : "Colony",
                            ownerIndex = index,
                            folkType = string.Empty
                        });
                    }
                }
            }

            List<StructureDTO> stationList = new List<StructureDTO>(p.TradeStations != null ? p.TradeStations.Length : 0);
            if (p.TradeStations != null)
            {
                foreach (TradeStation s in p.TradeStations)
                {
                    if (s != null && s.Tri != null)
                    {
                        stationList.Add(new StructureDTO
                        {
                            q = s.Tri.Q,
                            r = s.Tri.R,
                            type = "TradeStation",
                            ownerIndex = index,
                            folkType = FastAlienCivilizationToString(s.Tri.AlienCivilization)
                        });
                    }
                }
            }

            List<string> cards = new List<string>(p.FriendshipCards != null ? p.FriendshipCards.Count : 0);
            if (p.FriendshipCards != null)
            {
                foreach (FriendshipCard card in p.FriendshipCards)
                {
                    if (card != null)
                    {
                        cards.Add(card.Id.ToString());
                    }
                }
            }

            FlagshipDTO flagship = FlagshipDTO.FromPlayer(p);

            return new PlayerStateDTO
            {
                playerIndex = index,
                name = p.PlayerName,
                playerType = FastPlayerTypeToString(p.Type),
                victoryPoints = p.VictoryPoints,
                halfMedals = p.HalfMedalCount,
                resources = ResourceBundleDTO.FromPlayer(p),
                flagship = flagship,
                totalColonies = p.TotalColoniesCount,
                regularColonies = p.RegularColoniesCount,
                spaceportsCount = p.BuiltSpaceportsCount,
                tradeStationsCount = p.BuiltTradeStationsCount,
                availableShips = p.AvailableTransportShips,
                availableColonies = p.AvailableColoniesCount,
                availableSpaceports = p.AvailableSpaceportsCount,
                availableTradeStations = p.AvailableTradeStationsCount,
                availableBoosters = p.AvailableBoostersCount,
                availableCannons = p.AvailableCannonsCount,
                availableFreightPods = p.AvailableFreightPodsCount,
                capturedTradeShips = p.CapturedTradeShipsCount,
                units = unitList.Count == 0 ? Array.Empty<UnitDTO>() : unitList.ToArray(),
                colonies = colonyList.Count == 0 ? Array.Empty<StructureDTO>() : colonyList.ToArray(),
                tradeStations = stationList.Count == 0 ? Array.Empty<StructureDTO>() : stationList.ToArray(),
                friendshipCards = cards.Count == 0 ? Array.Empty<string>() : cards.ToArray()
            };
        }
    }

    [Serializable]
    public class EncounterOutcomeDTO
    {
        public int index;
        public string title;
        public string description;
        public bool canSelect;
        public string requirements;
    }

    [Serializable]
    public class EncounterStateDTO
    {
        public bool isPending;
        public string phase;
        public string cardId;
        public string cardType;
        public int characterIndex;
        public string title;
        public string description;
        public EncounterOutcomeDTO[] outcomes;
        public int selectedOutcomeIndex;

        public static EncounterStateDTO FromTriMap(TriMap map)
        {
            if (map == null || !map.EncounterPending || map.CurrentEncounterCard == null)
            {
                return new EncounterStateDTO
                {
                    isPending = false,
                    phase = "None",
                    cardId = string.Empty,
                    cardType = string.Empty,
                    characterIndex = 0,
                    title = string.Empty,
                    description = string.Empty,
                    outcomes = new EncounterOutcomeDTO[0],
                    selectedOutcomeIndex = -1
                };
            }

            EncounterCard card = map.CurrentEncounterCard;
            List<EncounterOutcomeDTO> outcomeList = new List<EncounterOutcomeDTO>();
            if (card.Outcomes != null)
            {
                for (int i = 0; i < card.Outcomes.Count; i++)
                {
                    EncounterCardOutcome outcome = card.Outcomes[i];
                    outcomeList.Add(new EncounterOutcomeDTO
                    {
                        index = i,
                        title = outcome.Condition,
                        description = outcome.EffectSummary,
                        canSelect = map.CurrentEncounterPhase == EncounterCardPhase.SelectOutcome,
                        requirements = string.Empty
                    });
                }
            }

            int selectedIdx = -1;
            if (map.SelectedEncounterOutcome != null && card.Outcomes != null)
            {
                selectedIdx = card.Outcomes.IndexOf(map.SelectedEncounterOutcome);
            }

            return new EncounterStateDTO
            {
                isPending = true,
                phase = map.CurrentEncounterPhase.ToString(),
                cardId = card.Number.ToString(),
                cardType = card.Type.ToString(),
                characterIndex = card.CharacterIndex,
                title = card.Title != null ? card.Title.GetText() : card.Type.ToString(),
                description = card.Situation,
                outcomes = outcomeList.ToArray(),
                selectedOutcomeIndex = selectedIdx
            };
        }
    }

    [Serializable]
    public class FriendshipCardOptionDTO
    {
        public int index;
        public string cardId;
        public string name;
        public string description;
    }

    [Serializable]
    public class DialogStateDTO
    {
        public bool isSevenRollResolving;
        public bool isDiscardDialogOpen;
        public int discardPlayerIndex;
        public int discardRequiredCount;
        public bool isStealDialogOpen;
        public int[] eligibleStealTargetIndices;
        public bool isGameOverDialogOpen;
        public bool isFriendshipCardDialogOpen;
        public int friendshipPlayerIndex = -1;
        public string friendshipCivilization = string.Empty;
        public FriendshipCardOptionDTO[] friendshipCardOptions;
    }

    [Serializable]
    public class BoardSiteDTO
    {
        public int q;
        public int r;
        public bool isColonyPoint;
        public bool isTradePoint;
        public bool isSpaceportPoint;
        public bool isOccupied;
        public int ownerIndex;
        public string folkType;
        public bool isBlockedByHazard;
        public string hazardKind;
        public int hazardStrength;
        public bool canResolveHazard;
        public bool isDiscovered;
        public int productionProbability;
        public int tradeStationCount;
        public int[] adjacentNumbers;
        public string[] adjacentResources;
    }

    [Serializable]
    public class StarSystemStateDTO
    {
        public int q;
        public int r;
        public string systemType;
        public string systemKind;
        public bool isRevealed;
        public int[] productionNumbers;
        public string[] resources;
        public string[] specialTypes;
        public int[] specialRequirements;
        public bool[] specialResolved;
        public string alienCivilization;
    }

    [Serializable]
    public class BoardStateDTO
    {
        public BoardSiteDTO[] sites;
        public StarSystemStateDTO[] starSystems;
    }

    [Serializable]
    public class GameStateObservationDTO
    {
        public string timestamp;
        public long step;
        public int boardSeed;
        public bool isGameOver;
        public int winnerIndex;
        public string winnerName;
        public string foundingPhase;
        public string turnPhase;
        public int currentPlayerIndex;
        public string currentPlayerName;
        public int startingPlayerCandidateIndex = -1;
        public int turnNumber;
        public int lastProductionRoll;
        public int lastFlightSpeed;
        public bool lastFlightHadBlackBall;
        public bool lastFlightHadBlackCore;
        public PlayerStateDTO[] players;
        public EncounterStateDTO encounter;
        public DialogStateDTO dialogs;
        public BoardStateDTO board;
        public string[] legalActions;
        public int foundingRemainingBoosters;
        public int foundingRemainingCannons;
        public int foundingRemainingFreightPods;
        public int localPlayerIndex;
        public bool isCurrentPlayerLocal;
        public bool isWaitingForAgent;
        public PlayerRankingDTO[] rankings;
    }

    [Serializable]
    public class PlayerRankingDTO
    {
        public int rank;
        public int playerIndex;
        public string playerName;
        public string playerType;
        public int victoryPoints;
        public bool isWinner;
        public int coloniesCount;
        public int spaceportsCount;
        public int tradeStationsCount;
        public int totalResources;
    }

    [Serializable]
    public class LobbySlotDTO
    {
        public int slotIndex;
        public string playerName = string.Empty;
        public string playerType = "Human"; // "Human" or "AI"
        public string aiDifficulty = "Normal"; // "Easy", "Normal", "Hard"
        public bool isConnected;
        public bool isHost;
    }

    [Serializable]
    public class LobbyStateDTO
    {
        public string serverName = "Deep Space Pioneers Server";
        public List<LobbySlotDTO> slots = new List<LobbySlotDTO>();
        public bool canStart;
        public int hostSlotIndex;
        public int seed;
        public bool useFixedSeed;
    }

    public static class GameStateMaskingExtensions
    {
        /// <summary>
        /// Masks secret information (Fog of War) such as opponent hand cards for a specific player.
        /// </summary>
        public static GameStateObservationDTO CreateMaskedCopy(this GameStateObservationDTO source, int recipientPlayerIndex)
        {
            if (source == null) return null;

            // Deep clone via JSON to preserve original unmodified on server
            string json = JsonUtility.ToJson(source);
            GameStateObservationDTO copy = JsonUtility.FromJson<GameStateObservationDTO>(json);

            if (copy.players != null)
            {
                for (int i = 0; i < copy.players.Length; i++)
                {
                    PlayerStateDTO p = copy.players[i];
                    if (p != null && p.playerIndex != recipientPlayerIndex && p.resources != null)
                    {
                        // Mask detailed resources for opponents; only total is shared
                        int total = p.resources.total;
                        p.resources = new ResourceBundleDTO
                        {
                            ore = 0,
                            fuel = 0,
                            carbon = 0,
                            food = 0,
                            tradeGoods = 0,
                            total = total
                        };
                    }
                }
            }

            copy.localPlayerIndex = recipientPlayerIndex;
            copy.isCurrentPlayerLocal = copy.currentPlayerIndex == recipientPlayerIndex;

            return copy;
        }
    }

    [Serializable]
    public class AgentActionDTO
    {
        public string actionId;
        public string action;

        // Coordinates
        public int q;
        public int r;
        public int fromQ;
        public int fromR;
        public int toQ;
        public int toR;

        // Types & Choices
        public string shipType;
        public string upgradeType;
        public string resource;
        public string targetResource;
        public int partnerIndex;

        // Resource counts for trade or discard
        public int ore;
        public int fuel;
        public int carbon;
        public int food;
        public int tradeGoods;

        // Choice index for encounter
        public int outcomeIndex;

        // Choice index for friendship cards
        public int cardIndex;

        // Optional unit ID
        public int unitId;

        // Targeted ship for encounters (e.g. BlockOneShip, SpaceJump)
        public bool hasTargetUnit;
        public int targetUnitQ;
        public int targetUnitR;
    }

    [Serializable]
    public class AgentResultDTO
    {
        public string actionId;
        public bool success;
        public string message;
        public string error;
        public string timestamp;
    }
}
