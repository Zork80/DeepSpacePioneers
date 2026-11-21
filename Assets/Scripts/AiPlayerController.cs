using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Controls autonomous decision-making and turn execution for AI players.
/// Implements full strategic scoring and decision engines adapted from the autonomous bot,
/// providing three distinct, scalable difficulty tiers:
///  - Easy (Einfach): Proximity-only navigation, ignores pips/hazards, skips modules, no supply trading.
///  - Normal (Normal): Standard balanced scoring, 2:1 TradeGoods trading for missing ships/spaceports.
///  - Hard (Schwer): Multi-dimensional scoring (pips, resource diversity, +2 VP hazard targeting,
///                  forward spaceport placement, win-rush threshold, dynamic 2:1 & 3:1 supply trading,
///                  and strategic Fame purchase).
/// </summary>
public class AiPlayerController : MonoBehaviour
{
    private TriMap _triMap;
    private Coroutine _activeAiRoutine;
    private bool _isActionInProgress;

    public static AiPlayerController Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        _triMap = GetComponent<TriMap>() ?? FindFirstObjectByType<TriMap>();
        if (_triMap == null)
        {
            Debug.LogError("[AiPlayerController] TriMap not found.");
            enabled = false;
            return;
        }

        _triMap.OnFoundingPhaseChanged += HandleFoundingPhaseChanged;
        _triMap.OnTurnPhaseChanged += HandleTurnPhaseChanged;
        _triMap.OnEncounterProgressChanged += HandleEncounterProgressChanged;

        TriggerAiCheck();
    }

    private void OnDestroy()
    {
        if (_triMap != null)
        {
            _triMap.OnFoundingPhaseChanged -= HandleFoundingPhaseChanged;
            _triMap.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
            _triMap.OnEncounterProgressChanged -= HandleEncounterProgressChanged;
        }
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private float GetActionDelay()
    {
        if (_triMap != null && _triMap.DebugSettings != null)
        {
            return Mathf.Max(0.1f, _triMap.DebugSettings.AiActionDelaySeconds);
        }
        return 0.5f;
    }

    public void TriggerAiCheck()
    {
        if (!isActiveAndEnabled || _triMap == null || _isActionInProgress || _triMap.IsGameOver)
        {
            return;
        }

        // In multiplayer, ONLY the server/host executes AI turns! Non-host clients must never run AI logic.
        if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected && !Networking.NetworkManager.Instance.IsHost)
        {
            return;
        }

        // 1. Founding phase check
        if (_triMap.CurrentFoundingPhase != FoundingPhase.Complete)
        {
            if (_triMap.CurrentFoundingPhase == FoundingPhase.RollForStartingPlayer)
            {
                Player candidate = _triMap.CurrentStartingPlayerCandidate;
                if (candidate != null && candidate.Type == Player.PlayerType.AI)
                {
                    StartAiRoutine(ExecuteAiStartingPlayerRoll());
                }
            }
            else if (_triMap.CurrentPlayer != null && _triMap.CurrentPlayer.Type == Player.PlayerType.AI)
            {
                StartAiRoutine(ExecuteAiFoundingStep());
            }
            return;
        }

        // 2. Regular turn check
        if (_triMap.CurrentPlayer != null && _triMap.CurrentPlayer.Type == Player.PlayerType.AI)
        {
            StartAiRoutine(ExecuteAiTurnStep());
        }
    }

    private void HandleFoundingPhaseChanged(Player player, FoundingPhase phase)
    {
        TriggerAiCheck();
    }

    private void HandleTurnPhaseChanged(Player player, GameTurnPhase phase)
    {
        TriggerAiCheck();
    }

    private void HandleEncounterProgressChanged(EncounterCardPhase phase, EncounterCardOutcome outcome)
    {
        if (_triMap != null && _triMap.CurrentPlayer != null && _triMap.CurrentPlayer.Type == Player.PlayerType.AI)
        {
            TriggerAiCheck();
        }
    }

    private void StartAiRoutine(IEnumerator routine)
    {
        if (_activeAiRoutine != null)
        {
            StopCoroutine(_activeAiRoutine);
        }
        _activeAiRoutine = StartCoroutine(RunAiSequence(routine));
    }

    private IEnumerator RunAiSequence(IEnumerator routine)
    {
        _isActionInProgress = true;
        yield return routine;
        _isActionInProgress = false;
        _activeAiRoutine = null;

        TriggerAiCheck();
    }

    // =========================================================================
    //  FOUNDING PHASE AI LOGIC
    // =========================================================================

    private IEnumerator ExecuteAiStartingPlayerRoll()
    {
        yield return new WaitForSeconds(GetActionDelay());

        if (_triMap.CurrentFoundingPhase == FoundingPhase.RollForStartingPlayer)
        {
            Player candidate = _triMap.CurrentStartingPlayerCandidate;
            if (candidate != null && candidate.Type == Player.PlayerType.AI)
            {
                Debug.Log(string.Format("[AI] Candidate {0} rolls for starting player.", candidate.PlayerName));
                _triMap.RollForStartingPlayer();
            }
        }
    }

    private IEnumerator ExecuteAiFoundingStep()
    {
        yield return new WaitForSeconds(GetActionDelay());

        if (_triMap.CurrentPlayer == null || _triMap.CurrentPlayer.Type != Player.PlayerType.AI)
        {
            yield break;
        }

        switch (_triMap.CurrentFoundingPhase)
        {
            case FoundingPhase.PlaceFirstColony:
            case FoundingPhase.PlaceSecondColony:
            case FoundingPhase.PlaceThirdColony:
                ExecuteAiColonyPlacement();
                break;

            case FoundingPhase.SelectSpaceport:
                ExecuteAiSelectSpaceport();
                break;

            case FoundingPhase.SelectStartingShip:
                ExecuteAiSelectStartingShip();
                break;

            case FoundingPhase.SelectStartingShipPoint:
                ExecuteAiSelectStartingShipPoint();
                break;

            case FoundingPhase.SelectFlagshipUpgrade:
                ExecuteAiSelectFlagshipUpgrade();
                break;
        }
    }

    private void ExecuteAiColonyPlacement()
    {
        Player ai = _triMap.CurrentPlayer;
        var availableSites = _triMap.AllTris
            .Where(t => t.IsColonyPoint && t.Colony == null && TriMap.IsHomeColonyPoint(t))
            .ToList();

        if (availableSites.Count == 0)
        {
            Debug.LogWarning("[AI] No unoccupied home colony site found!");
            return;
        }

        Tri bestSite;
        if (ai.Difficulty == Player.AiDifficulty.Hard)
        {
            bestSite = availableSites
                .OrderByDescending(site => ScoreFoundingColonySiteForHardAi(site, ai))
                .First();
        }
        else if (ai.Difficulty == Player.AiDifficulty.Normal)
        {
            bestSite = availableSites
                .OrderByDescending(site => CalculateIntersectionPips(site))
                .First();
        }
        else // Easy
        {
            bestSite = availableSites.First();
        }

        Debug.Log(string.Format("[AI] {0} ({1}) places founding colony at home site ({2},{3}).",
            ai.PlayerName, ai.Difficulty, bestSite.Q, bestSite.R));

        _triMap.TryPlaceFoundingColonyAt(bestSite);
    }

    private float ScoreFoundingColonySiteForHardAi(Tri site, Player player)
    {
        float score = CalculateIntersectionPips(site) * 5f;

        // Resource diversity bonus for founding phase: reward securing Ore, Fuel, Carbon, and Food
        var existingProduced = GetExistingPlayerResourcesProduced(player);
        var siteResources = GetSiteAdjacentResources(site);

        foreach (ResourceType res in siteResources)
        {
            if (res != ResourceType.None && !existingProduced.Contains(res))
            {
                score += 25f;
            }
        }

        return score;
    }

    private void ExecuteAiSelectSpaceport()
    {
        Player ai = _triMap.CurrentPlayer;
        var eligibleColonies = ai.Colonies
            .Where(c => c != null && !c.IsSpaceport && c.Tri != null && _triMap.GetFreePointsAtSpaceport(c).Any())
            .ToList();

        if (eligibleColonies.Count == 0)
        {
            Debug.LogWarning("[AI] Could not find eligible colony to upgrade to spaceport.");
            return;
        }

        // Identify planned destinations: attractive uncolonized planet colony sites and trade outposts
        var candidateColonySites = _triMap.AllTris
            .Where(t => t.IsColonyPoint && t.Colony == null && t.IsPassable && !_triMap.IsColonySiteBlockedByHazard(t))
            .OrderByDescending(t => CalculateIntersectionPips(t) * 5 - t.Q * 2)
            .Take(3)
            .ToList();

        var candidateTradeSites = _triMap.AllTris
            .Where(t => t.IsTradePoint && t.SystemKind == SystemKind.Trade && t.IsPassable && t.TradeStations.Any(s => s == null))
            .OrderBy(t => t.Q)
            .Take(2)
            .ToList();

        List<Tri> plannedTargets = new List<Tri>();
        plannedTargets.AddRange(candidateColonySites);
        plannedTargets.AddRange(candidateTradeSites);

        Colony bestColony = null;
        float bestScore = float.MinValue;

        foreach (Colony c in eligibleColonies)
        {
            var launchPoints = _triMap.GetFreePointsAtSpaceport(c).ToList();
            if (launchPoints.Count == 0) continue;

            float score = 0f;

            // Forward bonus: Q = 27 faces directly towards deep space/Sector 1; Q = 28 is side; Q = 29 is behind home system
            score += (28 - c.Tri.Q) * 20f;

            // Shortest path distance to primary planned target
            if (candidateColonySites.Count > 0)
            {
                Tri primary = candidateColonySites[0];
                int distToPrimary = launchPoints.Min(lp => _triMap.GetPathDistance(lp, primary));
                if (distToPrimary < 9999)
                {
                    score -= distToPrimary * 12f;
                }
                else
                {
                    score -= 500f;
                }
            }

            // Average path distance to all top planned targets
            if (plannedTargets.Count > 0)
            {
                float totalDist = 0f;
                int reachable = 0;
                foreach (Tri t in plannedTargets)
                {
                    int d = launchPoints.Min(lp => _triMap.GetPathDistance(lp, t));
                    if (d < 9999)
                    {
                        totalDist += d;
                        reachable++;
                    }
                }
                if (reachable > 0)
                {
                    score -= (totalDist / reachable) * 6f;
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestColony = c;
            }
        }

        if (bestColony == null)
        {
            bestColony = eligibleColonies.FirstOrDefault();
        }

        if (bestColony != null && bestColony.Tri != null)
        {
            Debug.Log(string.Format("[AI] {0} ({1}) strategically upgrades colony at ({2},{3}) to spaceport [score: {4:F1}].",
                ai.PlayerName, ai.Difficulty, bestColony.Tri.Q, bestColony.Tri.R, bestScore));
            _triMap.TrySelectFoundingSpaceportAt(bestColony.Tri);
        }
        else
        {
            Debug.LogWarning("[AI] Could not find eligible colony to upgrade to spaceport.");
        }
    }

    private void ExecuteAiSelectStartingShip()
    {
        StarshipType chosenShip = StarshipType.ColonyShip;
        Debug.Log(string.Format("[AI] {0} chooses starting ship: {1}.",
            _triMap.CurrentPlayer.PlayerName, chosenShip));
        _triMap.SelectStartingShip(chosenShip);
    }

    private void ExecuteAiSelectStartingShipPoint()
    {
        Player ai = _triMap.CurrentPlayer;
        Colony spaceport = ai.Colonies.FirstOrDefault(c => c != null && c.IsSpaceport);
        if (spaceport != null)
        {
            var launchPoints = _triMap.GetFreePointsAtSpaceport(spaceport).ToList();
            if (launchPoints.Count > 0)
            {
                Tri primaryTarget = _triMap.AllTris
                    .Where(t => t.IsColonyPoint && t.Colony == null && t.IsPassable && !_triMap.IsColonySiteBlockedByHazard(t))
                    .OrderByDescending(t => CalculateIntersectionPips(t) * 5 - t.Q * 2)
                    .FirstOrDefault();

                Tri bestLaunchPoint = (primaryTarget != null)
                    ? launchPoints.OrderBy(lp => _triMap.GetPathDistance(lp, primaryTarget)).FirstOrDefault()
                    : launchPoints.FirstOrDefault();

                if (bestLaunchPoint != null)
                {
                    Debug.Log(string.Format("[AI] {0} launches starting ship at ({1},{2}) towards planned destination ({3},{4}).",
                        ai.PlayerName, bestLaunchPoint.Q, bestLaunchPoint.R,
                        primaryTarget?.Q ?? -1, primaryTarget?.R ?? -1));
                    _triMap.TrySelectFoundingStartingShipPoint(bestLaunchPoint);
                    return;
                }
            }
        }
        Debug.LogWarning("[AI] Could not find free launch point for starting ship.");
    }

    private void ExecuteAiSelectFlagshipUpgrade()
    {
        Player ai = _triMap.CurrentPlayer;
        FlagshipUpgrade upgrade = FlagshipUpgrade.Drive;

        if (ai.Difficulty == Player.AiDifficulty.Easy)
        {
            var options = new List<FlagshipUpgrade>();
            if (_triMap.RemainingStartingDrives > 0) options.Add(FlagshipUpgrade.Drive);
            if (_triMap.RemainingStartingCannons > 0) options.Add(FlagshipUpgrade.Cannon);
            if (_triMap.RemainingStartingCargoModules > 0) options.Add(FlagshipUpgrade.CargoModule);
            if (options.Count > 0) upgrade = options[Random.Range(0, options.Count)];
        }
        else if (ai.Difficulty == Player.AiDifficulty.Hard)
        {
            // Cargo Module is paramount (enables trade stations and early Ice Planet terraforming (+2 VP)), followed by Drive speed
            if (_triMap.RemainingStartingCargoModules > 0) upgrade = FlagshipUpgrade.CargoModule;
            else if (_triMap.RemainingStartingDrives > 0) upgrade = FlagshipUpgrade.Drive;
            else if (_triMap.RemainingStartingCannons > 0) upgrade = FlagshipUpgrade.Cannon;
        }
        else // Normal
        {
            if (_triMap.RemainingStartingCargoModules > 0) upgrade = FlagshipUpgrade.CargoModule;
            else if (_triMap.RemainingStartingDrives > 0) upgrade = FlagshipUpgrade.Drive;
            else if (_triMap.RemainingStartingCannons > 0) upgrade = FlagshipUpgrade.Cannon;
        }

        Debug.Log(string.Format("[AI] {0} chooses starting upgrade: {1}.",
            ai.PlayerName, upgrade));
        _triMap.SelectStartingUpgrade(upgrade);
    }

    // =========================================================================
    //  REGULAR GAME TURN AI LOGIC
    // =========================================================================

    private IEnumerator ExecuteAiTurnStep()
    {
        float delay = GetActionDelay();
        yield return new WaitForSeconds(delay);

        if (_triMap.CurrentPlayer == null || _triMap.CurrentPlayer.Type != Player.PlayerType.AI)
        {
            yield break;
        }

        switch (_triMap.CurrentPhase)
        {
            case GameTurnPhase.Production:
                if (_triMap.LastProductionRoll == null)
                {
                    _triMap.RollProductionDice();
                    yield return new WaitForSeconds(delay);
                }
                _triMap.AdvanceTurnPhase();
                break;

            case GameTurnPhase.TradeAndBuild:
                yield return ExecuteAiTradeAndBuildPhase();
                yield return new WaitForSeconds(delay);
                _triMap.AdvanceTurnPhase();
                break;

            case GameTurnPhase.Flight:
                yield return ExecuteAiFlightPhase();
                break;
        }
    }

    private class BuildGoal
    {
        public string Name;
        public System.Action ExecuteAction;
        public Dictionary<ResourceType, int> Cost;
        public float Score;
        public bool CanAfford;
        public bool IsLegal;
    }

    private IEnumerator ExecuteAiTradeAndBuildPhase()
    {
        float delay = GetActionDelay();
        Player ai = _triMap.CurrentPlayer;

        // 1. Immediately found colonies or establish trade posts for any ships already parked on valid sites
        foreach (Unit u in ai.Units.ToArray())
        {
            if (_triMap.CanFoundColonyWith(u))
            {
                Debug.Log(string.Format("[AI] {0} founds colony with ship at ({1},{2}).",
                    ai.PlayerName, u.Tri.Q, u.Tri.R));
                _triMap.TryFoundColonyAt(u.Tri);
                yield return new WaitForSeconds(delay);
            }
            else if (_triMap.CanEstablishTradeStationWith(u))
            {
                Debug.Log(string.Format("[AI] {0} establishes trade station at ({1},{2}).",
                    ai.PlayerName, u.Tri.Q, u.Tri.R));
                _triMap.TryEstablishTradeStationAt(u.Tri);
                yield return new WaitForSeconds(delay);
            }
        }

        // 2. Rich Helps Poor (Reich hilft Arm) if applicable
        if (_triMap.TryRichHelpsPoor(ai))
        {
            Debug.Log(string.Format("[AI] {0} used RichHelpsPoor.", ai.PlayerName));
            yield return new WaitForSeconds(delay);
        }

        // 3. Execution based on Difficulty Tier
        if (ai.Difficulty == Player.AiDifficulty.Easy)
        {
            yield return ExecuteEasyBuildStrategy(ai, delay);
        }
        else if (ai.Difficulty == Player.AiDifficulty.Normal)
        {
            yield return ExecuteNormalBuildStrategy(ai, delay);
        }
        else // Hard
        {
            yield return ExecuteHardBuildStrategy(ai, delay);
        }
    }

    private IEnumerator ExecuteEasyBuildStrategy(Player ai, float delay)
    {
        // Easy: 40% chance to skip building upgrades
        bool skipUpgrade = Random.value < 0.4f;

        // Deploy captured trade ship if available (Free)
        if (_triMap.CanDeployCapturedTradeShip(ai))
        {
            Tri launchPoint = _triMap.GetFreeSpaceportPoints(ai).FirstOrDefault();
            if (launchPoint != null)
            {
                _triMap.TryBeginShipPlacement(StarshipType.TradeShip, isCapturedFree: true);
                _triMap.TryPlacePendingShipAt(launchPoint);
                yield return new WaitForSeconds(delay);
            }
        }

        // Colony Ship (limit 2 on board)
        if (ai.ColonyShipsOnBoard < 2 && _triMap.CanBuildShip(StarshipType.ColonyShip) && _triMap.GetFreeSpaceportPoints(ai).Any())
        {
            Tri launchPoint = _triMap.GetFreeSpaceportPoints(ai).FirstOrDefault();
            if (launchPoint != null)
            {
                _triMap.TryBeginShipPlacement(StarshipType.ColonyShip);
                _triMap.TryPlacePendingShipAt(launchPoint);
                yield return new WaitForSeconds(delay);
            }
        }

        // Spaceport
        if (_triMap.CanBuildSpaceport())
        {
            var eligibleColony = _triMap.GetEligibleColoniesForSpaceportUpgrade(ai).FirstOrDefault();
            if (eligibleColony != null && eligibleColony.Tri != null)
            {
                _triMap.TryBeginSpaceportUpgrade();
                _triMap.TryUpgradeSpaceportAt(eligibleColony.Tri);
                yield return new WaitForSeconds(delay);
            }
        }

        // CargoModule if 0 freight pods and trade stations available (needed to establish any trade station)
        if (ai.FlagshipCargoModuleCount == 0 && ai.AvailableTradeStationsCount > 0 && _triMap.CanBuildUpgrade(FlagshipUpgrade.CargoModule))
        {
            _triMap.TryBuildUpgrade(FlagshipUpgrade.CargoModule);
            yield return new WaitForSeconds(delay);
        }

        // Trade Ship (limit to at most 1 in flight, checked before upgrades so Ore/Fuel are not exhausted)
        if (ai.TradeShipsOnBoard == 0 && ai.AvailableTradeStationsCount > 0 && _triMap.CanBuildShip(StarshipType.TradeShip) && _triMap.GetFreeSpaceportPoints(ai).Any())
        {
            Tri launchPoint = _triMap.GetFreeSpaceportPoints(ai).FirstOrDefault();
            if (launchPoint != null)
            {
                _triMap.TryBeginShipPlacement(StarshipType.TradeShip);
                _triMap.TryPlacePendingShipAt(launchPoint);
                yield return new WaitForSeconds(delay);
            }
        }

        // Upgrades (random order)
        if (!skipUpgrade)
        {
            if (_triMap.CanBuildUpgrade(FlagshipUpgrade.Drive))
            {
                _triMap.TryBuildUpgrade(FlagshipUpgrade.Drive);
                yield return new WaitForSeconds(delay);
            }
            if (_triMap.CanBuildUpgrade(FlagshipUpgrade.CargoModule))
            {
                _triMap.TryBuildUpgrade(FlagshipUpgrade.CargoModule);
                yield return new WaitForSeconds(delay);
            }
            if (_triMap.CanBuildUpgrade(FlagshipUpgrade.Cannon))
            {
                _triMap.TryBuildUpgrade(FlagshipUpgrade.Cannon);
                yield return new WaitForSeconds(delay);
            }
        }
    }

    private IEnumerator ExecuteNormalBuildStrategy(Player ai, float delay)
    {
        // Deploy captured trade ship if available (Free)
        if (_triMap.CanDeployCapturedTradeShip(ai))
        {
            Tri launchPoint = _triMap.GetFreeSpaceportPoints(ai).FirstOrDefault();
            if (launchPoint != null)
            {
                _triMap.TryBeginShipPlacement(StarshipType.TradeShip, isCapturedFree: true);
                _triMap.TryPlacePendingShipAt(launchPoint);
                yield return new WaitForSeconds(delay);
            }
        }

        int goods = ai.GetResourceCount(ResourceType.TradeGoods);
        bool hasOpenTradeOutposts = _triMap.AllTris.Any(t => t.IsTradePoint && t.SystemKind == SystemKind.Trade && t.IsPassable && t.TradeStations.Any(s => s == null));
        bool shouldBuildTradeShip = ai.AvailableTradeStationsCount > 0 && ai.TradeShipsOnBoard == 0 && hasOpenTradeOutposts && ai.AvailableTransportShips > 0;

        // If AI has 0 freight pods, CargoModule is an absolute requirement to establish trade stations!
        if (ai.FlagshipCargoModuleCount == 0 && ai.AvailableTradeStationsCount > 0)
        {
            if (_triMap.CanBuildUpgrade(FlagshipUpgrade.CargoModule))
            {
                _triMap.TryBuildUpgrade(FlagshipUpgrade.CargoModule);
                yield return new WaitForSeconds(delay);
            }
            else if (ai.GetResourceCount(ResourceType.Ore) == 1)
            {
                // Try 3:1 supply trade for missing 2nd Ore
                ResourceType surplus3to1 = GameRules.Resources
                    .FirstOrDefault(r => r != ResourceType.Ore && r != ResourceType.TradeGoods && ai.GetResourceCount(r) >= 3);
                if (surplus3to1 != ResourceType.None)
                {
                    _triMap.TryTradeWithSupply(surplus3to1, ResourceType.Ore);
                    yield return new WaitForSeconds(delay);
                    if (_triMap.CanBuildUpgrade(FlagshipUpgrade.CargoModule))
                    {
                        _triMap.TryBuildUpgrade(FlagshipUpgrade.CargoModule);
                        yield return new WaitForSeconds(delay);
                    }
                }
            }
        }

        // 1. Build Spaceport if affordable (+1 VP and launch docks)
        if (_triMap.CanBuildSpaceport())
        {
            var eligibleColony = _triMap.GetEligibleColoniesForSpaceportUpgrade(ai).FirstOrDefault();
            if (eligibleColony != null && eligibleColony.Tri != null)
            {
                _triMap.TryBeginSpaceportUpgrade();
                _triMap.TryUpgradeSpaceportAt(eligibleColony.Tri);
                yield return new WaitForSeconds(delay);
            }
        }

        // 2. Trade Ship Strategy (High Priority: claim alien outposts & friendship cards!)
        if (shouldBuildTradeShip)
        {
            // If missing 1 resource for Trade Ship (cost: 1 Ore, 1 Fuel, 2 TradeGoods), try trading with supply
            if (!_triMap.CanBuildShip(StarshipType.TradeShip))
            {
                ResourceType missingRes = ResourceType.None;
                if (ai.GetResourceCount(ResourceType.Ore) == 0 && ai.GetResourceCount(ResourceType.Fuel) >= 1 && goods >= 2)
                    missingRes = ResourceType.Ore;
                else if (ai.GetResourceCount(ResourceType.Fuel) == 0 && ai.GetResourceCount(ResourceType.Ore) >= 1 && goods >= 2)
                    missingRes = ResourceType.Fuel;
                else if (goods == 1 && ai.GetResourceCount(ResourceType.Ore) >= 1 && ai.GetResourceCount(ResourceType.Fuel) >= 1)
                    missingRes = ResourceType.TradeGoods;

                if (missingRes != ResourceType.None)
                {
                    ResourceType surplus3to1 = GameRules.Resources
                        .FirstOrDefault(r => r != ResourceType.Ore && r != ResourceType.Fuel && r != ResourceType.TradeGoods && ai.GetResourceCount(r) >= 3);
                    if (surplus3to1 != ResourceType.None)
                    {
                        _triMap.TryTradeWithSupply(surplus3to1, missingRes);
                        yield return new WaitForSeconds(delay);
                    }
                }
            }

            if (_triMap.CanBuildShip(StarshipType.TradeShip) && _triMap.GetFreeSpaceportPoints(ai).Any())
            {
                Tri launchPoint = _triMap.GetFreeSpaceportPoints(ai).FirstOrDefault();
                if (launchPoint != null)
                {
                    _triMap.TryBeginShipPlacement(StarshipType.TradeShip);
                    _triMap.TryPlacePendingShipAt(launchPoint);
                    yield return new WaitForSeconds(delay);
                }
            }
        }

        // 3. Trade with supply if 1 resource away from Spaceport (preserve TradeGoods if trade ship is desired)
        if (ai.AvailableSpaceportsCount > 0 && ai.RegularColoniesCount > 0 && !_triMap.CanBuildSpaceport())
        {
            ResourceType missingRes = ResourceType.None;
            if (ai.GetResourceCount(ResourceType.Carbon) == 2 && ai.GetResourceCount(ResourceType.Food) >= 2)
            {
                missingRes = ResourceType.Carbon;
            }
            else if (ai.GetResourceCount(ResourceType.Carbon) >= 3 && ai.GetResourceCount(ResourceType.Food) == 1)
            {
                missingRes = ResourceType.Food;
            }

            if (missingRes != ResourceType.None)
            {
                if (goods >= 2 && !shouldBuildTradeShip)
                {
                    _triMap.TryTradeWithSupply(ResourceType.TradeGoods, missingRes);
                    yield return new WaitForSeconds(delay);
                }
                else
                {
                    // Check for 3:1 trade with surplus Ore or Fuel
                    ResourceType surplus3to1 = GameRules.Resources
                        .FirstOrDefault(r => r != ResourceType.Carbon && r != ResourceType.Food && r != ResourceType.TradeGoods && ai.GetResourceCount(r) >= 3);
                    if (surplus3to1 != ResourceType.None)
                    {
                        _triMap.TryTradeWithSupply(surplus3to1, missingRes);
                        yield return new WaitForSeconds(delay);
                    }
                }
            }
        }

        // 4. Colony Ship (limit 2 on board)
        if (ai.ColonyShipsOnBoard < 2 && _triMap.CanBuildShip(StarshipType.ColonyShip) && _triMap.GetFreeSpaceportPoints(ai).Any())
        {
            Tri launchPoint = _triMap.GetFreeSpaceportPoints(ai).FirstOrDefault();
            if (launchPoint != null)
            {
                _triMap.TryBeginShipPlacement(StarshipType.ColonyShip);
                _triMap.TryPlacePendingShipAt(launchPoint);
                yield return new WaitForSeconds(delay);
            }
        }
        else if (ai.AvailableColoniesCount > 0 && ai.AvailableTransportShips > 0 && ai.ColonyShipsOnBoard < 2 && !_triMap.CanBuildShip(StarshipType.ColonyShip))
        {
            // Trade with supply if 1 resource away from Colony Ship (preserving TradeGoods)
            var missing = new List<ResourceType>();
            if (ai.GetResourceCount(ResourceType.Ore) == 0) missing.Add(ResourceType.Ore);
            if (ai.GetResourceCount(ResourceType.Fuel) == 0) missing.Add(ResourceType.Fuel);
            if (ai.GetResourceCount(ResourceType.Carbon) == 0) missing.Add(ResourceType.Carbon);
            if (ai.GetResourceCount(ResourceType.Food) == 0) missing.Add(ResourceType.Food);

            if (missing.Count == 1)
            {
                ResourceType missingRes = missing[0];
                if (goods >= 2 && !shouldBuildTradeShip)
                {
                    _triMap.TryTradeWithSupply(ResourceType.TradeGoods, missingRes);
                    yield return new WaitForSeconds(delay);
                }
                else
                {
                    ResourceType surplus3to1 = GameRules.Resources
                        .FirstOrDefault(r => !missing.Contains(r) && r != ResourceType.TradeGoods && ai.GetResourceCount(r) >= 4);
                    if (surplus3to1 != ResourceType.None)
                    {
                        _triMap.TryTradeWithSupply(surplus3to1, missingRes);
                        yield return new WaitForSeconds(delay);
                    }
                }
            }

            if (_triMap.CanBuildShip(StarshipType.ColonyShip) && _triMap.GetFreeSpaceportPoints(ai).Any())
            {
                Tri launchPoint = _triMap.GetFreeSpaceportPoints(ai).FirstOrDefault();
                if (launchPoint != null)
                {
                    _triMap.TryBeginShipPlacement(StarshipType.ColonyShip);
                    _triMap.TryPlacePendingShipAt(launchPoint);
                    yield return new WaitForSeconds(delay);
                }
            }
        }

        // 5. Upgrades (Drives, CargoModules, Cannons - evaluated after ships so ships have component priority)
        if (_triMap.CanBuildUpgrade(FlagshipUpgrade.Drive))
        {
            _triMap.TryBuildUpgrade(FlagshipUpgrade.Drive);
            yield return new WaitForSeconds(delay);
        }
        if (_triMap.CanBuildUpgrade(FlagshipUpgrade.CargoModule))
        {
            _triMap.TryBuildUpgrade(FlagshipUpgrade.CargoModule);
            yield return new WaitForSeconds(delay);
        }
        if (_triMap.CanBuildUpgrade(FlagshipUpgrade.Cannon))
        {
            _triMap.TryBuildUpgrade(FlagshipUpgrade.Cannon);
            yield return new WaitForSeconds(delay);
        }
    }

    private IEnumerator ExecuteHardBuildStrategy(Player ai, float delay)
    {
        int maxActions = 10;
        while (maxActions-- > 0)
        {
            var goals = EvaluateBuildGoalsForHardAi(ai);
            if (goals == null || goals.Count == 0) break;

            // 1. Direct builds: execute the highest-scored affordable and legal goal
            BuildGoal topAffordable = goals.FirstOrDefault(g => g.IsLegal && g.CanAfford);
            if (topAffordable != null)
            {
                Debug.Log(string.Format("[AI] {0} executes top build goal: '{1}' (Score: {2:F1}).",
                    ai.PlayerName, topAffordable.Name, topAffordable.Score));
                topAffordable.ExecuteAction?.Invoke();
                yield return new WaitForSeconds(delay);
                continue; // Re-evaluate goals after building
            }

            // 2. Goal-directed supply trading: convert surplus resources to unlock the highest-scored goal
            bool didTrade = false;
            foreach (var goal in goals)
            {
                if (goal.IsLegal && !goal.CanAfford)
                {
                    if (TryTradeWithSupplyForGoal(ai, goal))
                    {
                        yield return new WaitForSeconds(delay);
                        didTrade = true;
                        break; // Re-evaluate after successful trade
                    }
                }
            }

            if (!didTrade)
            {
                break; // No more actions possible this turn
            }
        }
    }

    private List<BuildGoal> EvaluateBuildGoalsForHardAi(Player ai)
    {
        var goals = new List<BuildGoal>();
        bool isWinRush = ai.VictoryPoints >= 13;
        bool hasFreeSpaceportPoint = _triMap.GetFreeSpaceportPoints(ai).Any();

        // 0. Deploy Captured Trade Ship (Free!)
        if (_triMap.CanDeployCapturedTradeShip(ai))
        {
            Tri launchPoint = _triMap.GetFreeSpaceportPoints(ai).FirstOrDefault();
            if (launchPoint != null)
            {
                goals.Add(new BuildGoal
                {
                    Name = "DeployCapturedTradeShip",
                    Cost = new Dictionary<ResourceType, int>(),
                    Score = 280f,
                    CanAfford = true,
                    IsLegal = true,
                    ExecuteAction = () =>
                    {
                        if (_triMap.TryBeginShipPlacement(StarshipType.TradeShip, isCapturedFree: true))
                        {
                            _triMap.TryPlacePendingShipAt(launchPoint);
                        }
                    }
                });
            }
        }

        // 1. Buy Fame (Gekaufter Ruhm)
        bool hasFameCard = ai.HasFriendshipCard(FriendshipCardId.DiplomatBoughtFame1) ||
                           ai.HasFriendshipCard(FriendshipCardId.DiplomatBoughtFame2);
        if (hasFameCard && !ai.HasUsedBoughtFameThisTurn)
        {
            float score = (ai.HalfMedals % 2 == 1 || isWinRush) ? 320f : 170f;
            goals.Add(new BuildGoal
            {
                Name = "BuyFame",
                Cost = new Dictionary<ResourceType, int> { { ResourceType.TradeGoods, 1 } },
                Score = score,
                CanAfford = ai.GetResourceCount(ResourceType.TradeGoods) >= 1,
                IsLegal = true,
                ExecuteAction = () => _triMap.TryBuyFame(ai)
            });
        }

        // 2. Build Spaceport (+1 VP and forward dock)
        if (ai.AvailableSpaceportsCount > 0 && ai.RegularColoniesCount > 0)
        {
            var eligibleColonies = _triMap.GetEligibleColoniesForSpaceportUpgrade(ai);
            var bestColony = eligibleColonies.OrderBy(c => c.Tri != null ? c.Tri.Q : 999).FirstOrDefault();
            if (bestColony != null && bestColony.Tri != null)
            {
                var cost = new Dictionary<ResourceType, int> { { ResourceType.Carbon, 3 }, { ResourceType.Food, 2 } };
                goals.Add(new BuildGoal
                {
                    Name = string.Format("Spaceport at ({0},{1})", bestColony.Tri.Q, bestColony.Tri.R),
                    Cost = cost,
                    Score = isWinRush ? 360f : 200f,
                    CanAfford = CheckCanAffordCost(ai, cost),
                    IsLegal = true,
                    ExecuteAction = () =>
                    {
                        var eligible = _triMap.GetEligibleColoniesForSpaceportUpgrade(ai).OrderBy(c => c.Tri != null ? c.Tri.Q : 999).FirstOrDefault();
                        if (eligible != null && eligible.Tri != null)
                        {
                            _triMap.TryBeginSpaceportUpgrade();
                            _triMap.TryUpgradeSpaceportAt(eligible.Tri);
                        }
                    }
                });
            }
        }

        // 3. Cannon (Unlocks Marauder Base combat for +2 VP)
        if (ai.FlagshipCannonCount < GameRules.MaxCannonsPerPlayer)
        {
            var cost = new Dictionary<ResourceType, int> { { ResourceType.Carbon, 2 } };
            bool hasNearMarauders = ai.Units != null && ai.Units.Any(u => u.ShipType == StarshipType.ColonyShip && u.Tri != null &&
                _triMap.AllTris.Any(t => t.IsColonyPoint && _triMap.IsColonySiteBlockedByHazard(t) && _triMap.HasColonySiteHazardType(t, PlanetSpecialType.MarauderBase) && Distance(u.Tri, t) <= 8 && !_triMap.CanPlayerResolveColonySiteHazards(ai, t)));
            float score;
            if (hasNearMarauders) score = isWinRush ? 240f : 190f;
            else if (ai.FlagshipCannonCount < 2) score = 80f;
            else if (ai.FlagshipCannonCount < 3) score = 45f;
            else score = 25f;

            goals.Add(new BuildGoal
            {
                Name = "Cannon",
                Cost = cost,
                Score = score,
                CanAfford = CheckCanAffordCost(ai, cost),
                IsLegal = true,
                ExecuteAction = () => _triMap.TryBuildUpgrade(FlagshipUpgrade.Cannon)
            });
        }

        // 4. Freight Pod (MANDATORY for trade stations + unlocks Ice Planet terraforming for +2 VP)
        if (ai.FlagshipFreightPodCount < GameRules.MaxFreightPodsPerPlayer)
        {
            var cost = new Dictionary<ResourceType, int> { { ResourceType.Ore, 2 } };
            bool hasNearIce = ai.Units != null && ai.Units.Any(u => u.ShipType == StarshipType.ColonyShip && u.Tri != null &&
                _triMap.AllTris.Any(t => t.IsColonyPoint && _triMap.IsColonySiteBlockedByHazard(t) && _triMap.HasColonySiteHazardType(t, PlanetSpecialType.IcePlanet) && Distance(u.Tri, t) <= 8 && !_triMap.CanPlayerResolveColonySiteHazards(ai, t)));
            float score;
            if (hasNearIce) score = isWinRush ? 240f : 190f;
            else if (ai.TradeShipsOnBoard > 0 && ai.FlagshipFreightPodCount == 0)
            {
                // Active trade ship with 0 pods cannot dock anywhere!
                score = 250f;
            }
            else if (ai.FlagshipFreightPodCount == 0 && ai.AvailableTradeStationsCount > 0)
            {
                // 0 pods blocks all trade stations!
                score = 225f;
            }
            else if (ai.FlagshipFreightPodCount == 1 && ai.AvailableTradeStationsCount > 0 &&
                     _triMap.AllTris.Any(t => t.IsTradePoint && t.SystemKind == SystemKind.Trade && t.TradeStations.Count(s => s != null) >= 1 && t.TradeStations.Any(s => s == null)))
            {
                score = 165f;
            }
            else if (ai.FlagshipFreightPodCount < 2) score = 85f;
            else if (ai.FlagshipFreightPodCount < 3) score = 45f;
            else score = 20f;

            goals.Add(new BuildGoal
            {
                Name = "FreightPod",
                Cost = cost,
                Score = score,
                CanAfford = CheckCanAffordCost(ai, cost),
                IsLegal = true,
                ExecuteAction = () => _triMap.TryBuildUpgrade(FlagshipUpgrade.CargoModule)
            });
        }

        // 5. Booster (Speed velocity)
        if (ai.FlagshipDriveCount < GameRules.MaxBoostersPerPlayer && !isWinRush)
        {
            var cost = new Dictionary<ResourceType, int> { { ResourceType.Fuel, 2 } };
            float score;
            if (ai.FlagshipDriveCount < 3) score = 160f - ai.FlagshipDriveCount * 20f;
            else if (ai.FlagshipDriveCount < 6) score = 75f;
            else score = 30f;

            goals.Add(new BuildGoal
            {
                Name = "Booster",
                Cost = cost,
                Score = score,
                CanAfford = CheckCanAffordCost(ai, cost),
                IsLegal = true,
                ExecuteAction = () => _triMap.TryBuildUpgrade(FlagshipUpgrade.Drive)
            });
        }

        // 6. Colony Ship
        if (ai.AvailableTransportShips > 0 && ai.AvailableColoniesCount > 0 && hasFreeSpaceportPoint)
        {
            var cost = new Dictionary<ResourceType, int>
            {
                { ResourceType.Ore, 1 }, { ResourceType.Fuel, 1 },
                { ResourceType.Carbon, 1 }, { ResourceType.Food, 1 }
            };
            var freeColonies = _triMap.AllTris
                .Where(t => t.IsColonyPoint && t.Colony == null && t.IsPassable && (!_triMap.IsColonySiteBlockedByHazard(t) || _triMap.CanPlayerResolveColonySiteHazards(ai, t)))
                .ToList();

            float bestColonyScore = freeColonies.Count > 0 ? freeColonies.Max(t => ScoreColonySiteForHardAi(t, null, ai)) : 0f;
            float colonyNeedBonus = (ai.Colonies != null && ai.Colonies.Length < 3) ? 40f : 0f;
            float penaltyPerShip = ai.ColonyShipsOnBoard * 60f;
            float score = 145f + Mathf.Max(0f, bestColonyScore * 0.45f) + colonyNeedBonus - penaltyPerShip + (isWinRush ? 70f : 0f);

            goals.Add(new BuildGoal
            {
                Name = "ColonyShip",
                Cost = cost,
                Score = score,
                CanAfford = CheckCanAffordCost(ai, cost),
                IsLegal = true,
                ExecuteAction = () =>
                {
                    Tri launchPoint = _triMap.GetFreeSpaceportPoints(ai).FirstOrDefault();
                    if (launchPoint != null)
                    {
                        _triMap.TryBeginShipPlacement(StarshipType.ColonyShip);
                        _triMap.TryPlacePendingShipAt(launchPoint);
                    }
                }
            });
        }

        // 7. Trade Ship (Crucial for alien outposts, friendship cards, and victory points!)
        if (ai.AvailableTransportShips > 0 && ai.AvailableTradeStationsCount > 0 && hasFreeSpaceportPoint)
        {
            var cost = new Dictionary<ResourceType, int>
            {
                { ResourceType.Ore, 1 }, { ResourceType.Fuel, 1 }, { ResourceType.TradeGoods, 2 }
            };
            var freeTrade = _triMap.AllTris
                .Where(t => t.IsTradePoint && t.SystemKind == SystemKind.Trade && t.IsPassable && t.TradeStations.Any(s => s == null))
                .ToList();

            if (freeTrade.Count > 0)
            {
                float bestTradeScore = freeTrade.Max(t => ScoreTradeSiteForHardAi(t, null, ai));
                float score;
                if (ai.TradeShipsOnBoard == 0)
                {
                    score = 220f + Mathf.Max(0f, bestTradeScore * 0.5f);
                }
                else if (ai.TradeShipsOnBoard == 1 && ai.AvailableTradeStationsCount > 1)
                {
                    score = 140f + Mathf.Max(0f, bestTradeScore * 0.4f);
                }
                else
                {
                    score = 60f;
                }

                goals.Add(new BuildGoal
                {
                    Name = "TradeShip",
                    Cost = cost,
                    Score = score,
                    CanAfford = CheckCanAffordCost(ai, cost),
                    IsLegal = true,
                    ExecuteAction = () =>
                    {
                        Tri launchPoint = _triMap.GetFreeSpaceportPoints(ai).FirstOrDefault();
                        if (launchPoint != null)
                        {
                            _triMap.TryBeginShipPlacement(StarshipType.TradeShip);
                            _triMap.TryPlacePendingShipAt(launchPoint);
                        }
                    }
                });
            }
        }

        return goals.OrderByDescending(g => g.Score).ToList();
    }

    private bool CheckCanAffordCost(Player player, Dictionary<ResourceType, int> cost)
    {
        if (player == null || cost == null) return false;
        foreach (var kvp in cost)
        {
            if (player.GetResourceCount(kvp.Key) < kvp.Value) return false;
        }
        return true;
    }

    private bool TryTradeWithSupplyForGoal(Player ai, BuildGoal goal)
    {
        if (ai == null || goal == null || goal.Cost == null) return false;

        var missing = new Dictionary<ResourceType, int>();
        foreach (var kvp in goal.Cost)
        {
            int have = ai.GetResourceCount(kvp.Key);
            if (have < kvp.Value)
            {
                missing[kvp.Key] = kvp.Value - have;
            }
        }

        if (missing.Count == 0) return false;

        var surplus = new Dictionary<ResourceType, int>();
        foreach (ResourceType r in GameRules.Resources)
        {
            int needed = goal.Cost.TryGetValue(r, out int n) ? n : 0;
            int have = ai.GetResourceCount(r);
            if (have > needed)
            {
                surplus[r] = have - needed;
            }
        }

        ResourceType targetRes = missing.Keys.First();

        // 1. Prefer trading surplus TradeGoods first, but protect the 2 TradeGoods needed for TradeShip unless this goal IS TradeShip!
        int goodsRate = ai.GetSupplyTradeRate(ResourceType.TradeGoods);
        int surplusGoods = surplus.TryGetValue(ResourceType.TradeGoods, out int sg) ? sg : 0;
        int tradeGoodsReserve = (ai.AvailableTradeStationsCount > 0 && goal.Name != "TradeShip") ? 2 : 0;
        int spendableGoods = Mathf.Max(0, surplusGoods - tradeGoodsReserve);

        if (spendableGoods >= goodsRate && targetRes != ResourceType.TradeGoods)
        {
            Debug.Log(string.Format("[AI] {0} trades with supply: {1} TradeGoods -> 1 {2} (Goal: {3}).",
                ai.PlayerName, goodsRate, targetRes, goal.Name));
            return _triMap.TryTradeWithSupply(ResourceType.TradeGoods, targetRes);
        }

        // 2. Otherwise trade other surplus resources with at least 3 cards (typically 3:1, or 2:1 with specific Merchant card)
        var otherSurplus = surplus
            .Where(kvp => kvp.Key != ResourceType.TradeGoods && kvp.Key != targetRes && kvp.Value >= ai.GetSupplyTradeRate(kvp.Key))
            .OrderByDescending(kvp => kvp.Value)
            .ToList();

        if (otherSurplus.Count > 0)
        {
            ResourceType spendRes = otherSurplus[0].Key;
            int rate = ai.GetSupplyTradeRate(spendRes);
            Debug.Log(string.Format("[AI] {0} trades with supply: {1} {2} -> 1 {3} (Goal: {4}).",
                ai.PlayerName, rate, spendRes, targetRes, goal.Name));
            return _triMap.TryTradeWithSupply(spendRes, targetRes);
        }

        return false;
    }

    private IEnumerator ExecuteAiFlightPhase()
    {
        float delay = GetActionDelay();

        // 1. Roll flight impulse if not rolled
        if (_triMap.LastFlagshipRoll == null)
        {
            _triMap.RollFlightImpulse();
            yield return new WaitForSeconds(delay);
        }

        // 2. Resolve Encounter if pending
        if (_triMap.EncounterPending && _triMap.CurrentEncounterCard != null)
        {
            yield return ResolveAiEncounter();
        }

        // 3. Move units
        yield return MoveAiUnits();

        // 4. Check for colonies / trade posts formed after movement
        foreach (Unit u in _triMap.CurrentPlayer.Units.ToArray())
        {
            if (_triMap.CanFoundColonyWith(u))
            {
                _triMap.TryFoundColonyAt(u.Tri);
                yield return new WaitForSeconds(delay);
            }
            else if (_triMap.CanEstablishTradeStationWith(u))
            {
                _triMap.TryEstablishTradeStationAt(u.Tri);
                yield return new WaitForSeconds(delay);
            }
        }

        yield return new WaitForSeconds(delay);

        // 5. End Flight phase (which advances to next player)
        _triMap.AdvanceTurnPhase();
    }

    private IEnumerator ResolveAiEncounter()
    {
        float delay = GetActionDelay();

        while (_triMap.EncounterPending && _triMap.CurrentEncounterCard != null)
        {
            EncounterCard card = _triMap.CurrentEncounterCard;

            if (card.RefreshesEncounterDeck)
            {
                _triMap.ConfirmEncounterResolved();
                yield return new WaitForSeconds(delay);
                continue;
            }

            if (_triMap.CurrentEncounterPhase == EncounterCardPhase.ReadPrompt)
            {
                if (card.InteractionType == EncounterInteractionType.Donation)
                {
                    _triMap.PendingEncounterDonationAmount = 0;
                    _triMap.PendingEncounterDonatedResources = new Dictionary<ResourceType, int>();
                    _triMap.AdvanceEncounterAfterPlayerResponse();
                    _triMap.SelectEncounterOutcome(0);
                }
                else
                {
                    _triMap.AdvanceEncounterAfterPlayerResponse();
                    int targetIndex = 0;
                    if (card.Type == EncounterCardType.SpaceDistortion && card.Outcomes != null && card.Outcomes.Count > 1)
                    {
                        targetIndex = 1; // Attempt tactical space jump
                    }
                    else if (card.Type == EncounterCardType.MarauderAttack && card.Outcomes != null && card.Outcomes.Count > 1)
                    {
                        int aiCannons = _triMap.CurrentPlayer?.EffectiveCannonCount ?? 0;
                        if (aiCannons >= 1) targetIndex = 1;
                    }
                    _triMap.SelectEncounterOutcome(targetIndex);
                }
                yield return new WaitForSeconds(delay);
            }
            else if (_triMap.CurrentEncounterPhase == EncounterCardPhase.SelectOutcome)
            {
                int targetIndex = 0;
                if (card.Type == EncounterCardType.SpaceDistortion && card.Outcomes != null && card.Outcomes.Count > 1)
                {
                    targetIndex = 1;
                }
                else if (card.Type == EncounterCardType.MarauderAttack && card.Outcomes != null && card.Outcomes.Count > 1)
                {
                    int aiCannons = _triMap.CurrentPlayer?.EffectiveCannonCount ?? 0;
                    if (aiCannons >= 1) targetIndex = 1;
                }
                _triMap.SelectEncounterOutcome(targetIndex);
                yield return new WaitForSeconds(delay);
            }
            else if (_triMap.CurrentEncounterPhase == EncounterCardPhase.ApplyOutcome)
            {
                _triMap.ConfirmEncounterResolved();
                yield return new WaitForSeconds(delay);
            }
            else
            {
                break;
            }
        }
    }

    private IEnumerator MoveAiUnits()
    {
        float delay = GetActionDelay();
        Player ai = _triMap.CurrentPlayer;

        foreach (Unit unit in ai.Units.ToArray())
        {
            if (unit == null || unit.IsDestroyed || unit.MovementRemaining <= 0 || !_triMap.CanMoveUnit(unit))
            {
                continue;
            }

            List<Tri> candidates = FindRankedDestinationsForUnit(unit, ai);
            foreach (Tri target in candidates)
            {
                if (target == null || target == unit.Tri) continue;

                Tri[] fullPath = _triMap.FindShipPath(unit, target);
                if (fullPath == null || fullPath.Length < 2) continue;

                Tri[] legalSubPath = GetLegalSubPath(unit, fullPath);
                if (legalSubPath == null || legalSubPath.Length < 2 || !_triMap.CanMoveShipAlongPath(unit, legalSubPath)) continue;

                Debug.Log(string.Format("[AI] {0} ({1}) moves {2} from ({3},{4}) towards ({5},{6}) [{7} steps].",
                    ai.PlayerName, ai.Difficulty, unit.ShipType, unit.Tri.Q, unit.Tri.R, target.Q, target.R, legalSubPath.Length - 1));

                unit.SetTriPath(legalSubPath);
                yield return _triMap.DoUnitMoves(unit);
                yield return new WaitForSeconds(delay);

                // If ship arrived directly at target, immediately establish colony or trade station
                if (unit.Tri == target)
                {
                    if (_triMap.CanFoundColonyWith(unit))
                    {
                        Debug.Log(string.Format("[AI] {0} immediately founds colony upon arrival at ({1},{2}).",
                            ai.PlayerName, unit.Tri.Q, unit.Tri.R));
                        _triMap.TryFoundColonyAt(unit.Tri);
                        yield return new WaitForSeconds(delay);
                    }
                    else if (_triMap.CanEstablishTradeStationWith(unit))
                    {
                        Debug.Log(string.Format("[AI] {0} immediately establishes trade station upon arrival at ({1},{2}).",
                            ai.PlayerName, unit.Tri.Q, unit.Tri.R));
                        _triMap.TryEstablishTradeStationAt(unit.Tri);
                        yield return new WaitForSeconds(delay);
                    }
                }
                break;
            }
        }
    }

    private Tri[] GetLegalSubPath(Unit unit, Tri[] fullPath)
    {
        int maxSteps = Mathf.Min(unit.MovementRemaining, fullPath.Length - 1);
        for (int step = maxSteps; step >= 1; step--)
        {
            Tri dest = fullPath[step];
            if (dest.IsPassable && !dest.Units.Any(u => u != unit) && !_triMap.IsSpaceportPointOwnedByOpponent(dest, _triMap.CurrentPlayer))
            {
                Tri[] subPath = new Tri[step + 1];
                System.Array.Copy(fullPath, subPath, step + 1);
                return subPath;
            }
        }
        return null;
    }

    private List<Tri> FindRankedDestinationsForUnit(Unit unit, Player owner, int maxCandidates = 5)
    {
        if (owner == null) owner = _triMap.CurrentPlayer;
        Player.AiDifficulty difficulty = owner != null ? owner.Difficulty : Player.AiDifficulty.Normal;

        if (unit.ShipType == StarshipType.ColonyShip)
        {
            if (difficulty == Player.AiDifficulty.Easy)
            {
                // Easy: closest unblocked colony point by Manhattan distance, ignores pips and hazards
                return _triMap.AllTris
                    .Where(t => t.IsColonyPoint && t.Colony == null && t.IsPassable && !_triMap.IsColonySiteBlockedByHazard(t))
                    .OrderBy(t => Distance(unit.Tri, t))
                    .Take(maxCandidates)
                    .ToList();
            }
            else if (difficulty == Player.AiDifficulty.Hard)
            {
                // Hard: full strategic PioneerScorer evaluation
                return _triMap.AllTris
                    .Where(t => t.IsColonyPoint && t.Colony == null && t.IsPassable)
                    .OrderByDescending(t => ScoreColonySiteForHardAi(t, unit, owner))
                    .Take(maxCandidates)
                    .ToList();
            }
            else // Normal
            {
                // Normal: balanced pips vs distance
                return _triMap.AllTris
                    .Where(t => t.IsColonyPoint && t.Colony == null && t.IsPassable &&
                                (!_triMap.IsColonySiteBlockedByHazard(t) || (owner != null && _triMap.CanPlayerResolveColonySiteHazards(owner, t))))
                    .OrderByDescending(t => ScoreColonySiteForNormalAi(t, unit, owner))
                    .Take(maxCandidates)
                    .ToList();
            }
        }
        else if (unit.ShipType == StarshipType.TradeShip)
        {
            if (difficulty == Player.AiDifficulty.Easy)
            {
                return _triMap.AllTris
                    .Where(t => t.IsTradePoint && t.SystemKind == SystemKind.Trade && t.IsPassable && t.TradeStations.Any(s => s == null))
                    .OrderBy(t => Distance(unit.Tri, t))
                    .Take(maxCandidates)
                    .ToList();
            }
            else if (difficulty == Player.AiDifficulty.Hard)
            {
                return _triMap.AllTris
                    .Where(t => t.IsTradePoint && t.SystemKind == SystemKind.Trade && t.IsPassable && t.TradeStations.Any(s => s == null))
                    .OrderByDescending(t => ScoreTradeSiteForHardAi(t, unit, owner))
                    .Take(maxCandidates)
                    .ToList();
            }
            else // Normal
            {
                return _triMap.AllTris
                    .Where(t => t.IsTradePoint && t.SystemKind == SystemKind.Trade && t.IsPassable && t.TradeStations.Any(s => s == null))
                    .OrderByDescending(t => ScoreTradeSiteForNormalAi(t, unit, owner))
                    .Take(maxCandidates)
                    .ToList();
            }
        }

        return new List<Tri>();
    }

    private float ScoreColonySiteForHardAi(Tri tri, Unit unit, Player owner)
    {
        float score = 0f;
        int dist = (unit != null && unit.Tri != null) ? Distance(unit.Tri, tri) : 0;
        bool isWinRush = owner != null && owner.VictoryPoints >= 13;

        // Hazard resolution: Ice Planet or Marauder Base gives +1 Colony AND +1 Special Medal/Token = +2 VP!
        if (_triMap != null && _triMap.IsColonySiteBlockedByHazard(tri))
        {
            if (owner != null && _triMap.CanPlayerResolveColonySiteHazards(owner, tri))
            {
                score += isWinRush ? 220f : 170f;
            }
            else
            {
                bool hasPeaceful = _triMap.AllTris.Any(t => t.IsColonyPoint && t.Colony == null && !_triMap.IsColonySiteBlockedByHazard(t));
                if (hasPeaceful)
                {
                    return -999999f;
                }
                score += 50f;
            }
        }

        // Production yield (6 points per pip)
        int pips = CalculateIntersectionPips(tri);
        score += pips * 6f;

        // Resource diversity bonus: reward expanding into resource types the AI does not yet harvest
        var existingProduced = GetExistingPlayerResourcesProduced(owner);
        var siteResources = GetSiteAdjacentResources(tri);
        foreach (ResourceType res in siteResources)
        {
            if (res != ResourceType.None && !existingProduced.Contains(res))
            {
                score += 15f;
            }
        }

        // Travel distance penalty taking flagship speed into account
        int boosters = owner != null ? owner.FlagshipDriveCount : 0;
        float effectiveSpeed = 3f + boosters * 0.5f;
        float travelTurns = dist / Mathf.Max(1f, effectiveSpeed);
        score -= travelTurns * 8f;

        return score;
    }

    private float ScoreColonySiteForNormalAi(Tri tri, Unit unit, Player owner)
    {
        int dist = (unit != null && unit.Tri != null) ? Distance(unit.Tri, tri) : 0;
        int pips = CalculateIntersectionPips(tri);
        float score = pips * 5f - dist * 1.5f;

        if (_triMap != null && _triMap.IsColonySiteBlockedByHazard(tri))
        {
            if (owner != null && _triMap.CanPlayerResolveColonySiteHazards(owner, tri))
            {
                score += 150f;
            }
            else
            {
                return -9999f;
            }
        }

        return score;
    }

    private float ScoreTradeSiteForHardAi(Tri tri, Unit unit, Player owner)
    {
        float score = 70f; // Base value of establishing a trade outpost
        int dist = (unit != null && unit.Tri != null) ? Distance(unit.Tri, tri) : 0;
        bool isWinRush = owner != null && owner.VictoryPoints >= 13;

        int myStations = owner != null ? owner.TradeStations.Count(ts => ts.Tri != null && ts.Tri.AlienCivilization == tri.AlienCivilization) : 0;
        int maxOpponent = 0;

        if (_triMap != null && _triMap.Players != null && owner != null)
        {
            foreach (Player p in _triMap.Players)
            {
                if (p != owner)
                {
                    int cnt = p.TradeStations.Count(ts => ts.Tri != null && ts.Tri.AlienCivilization == tri.AlienCivilization);
                    if (cnt > maxOpponent) maxOpponent = cnt;
                }
            }
        }

        // First contact bonus (grants a valuable Friendship card)
        if (myStations == 0)
        {
            score += 50f;
        }

        // Folk majority bonus (controlling majority awards +2 VP Standee)
        if (myStations + 1 >= maxOpponent)
        {
            score += isWinRush ? 130f : 70f;
        }

        int boosters = owner != null ? owner.FlagshipDriveCount : 0;
        float effectiveSpeed = 3f + boosters * 0.5f;
        float travelTurns = dist / Mathf.Max(1f, effectiveSpeed);
        score -= travelTurns * 8f;

        // Freight pod requirement: docking requires FlagshipFreightPodCount > currentStations!
        int currentStations = tri.TradeStations != null ? tri.TradeStations.Count(s => s != null) : 0;
        if (owner != null && owner.FlagshipFreightPodCount <= currentStations)
        {
            score -= 500f;
        }

        // Prefer outposts where we do not already have a station
        bool alreadyHasStationHere = owner != null && owner.TradeStations != null && owner.TradeStations.Any(ts => ts != null && ts.Tri == tri);
        if (alreadyHasStationHere)
        {
            score -= 200f;
        }

        return score;
    }

    private float ScoreTradeSiteForNormalAi(Tri tri, Unit unit, Player owner)
    {
        float score = 60f;
        int dist = (unit != null && unit.Tri != null) ? Distance(unit.Tri, tri) : 0;
        int myStations = owner != null ? owner.TradeStations.Count(ts => ts.Tri != null && ts.Tri.AlienCivilization == tri.AlienCivilization) : 0;
        if (myStations == 0) score += 30f;
        score -= dist * 1.5f;

        int currentStations = tri.TradeStations != null ? tri.TradeStations.Count(s => s != null) : 0;
        if (owner != null && owner.FlagshipFreightPodCount <= currentStations)
        {
            score -= 500f;
        }

        bool alreadyHasStationHere = owner != null && owner.TradeStations != null && owner.TradeStations.Any(ts => ts != null && ts.Tri == tri);
        if (alreadyHasStationHere)
        {
            score -= 200f;
        }

        return score;
    }

    private HashSet<ResourceType> GetExistingPlayerResourcesProduced(Player player)
    {
        var produced = new HashSet<ResourceType>();
        if (player == null || player.Colonies == null) return produced;

        foreach (Colony c in player.Colonies)
        {
            if (c == null || c.Tri == null) continue;
            var resList = GetSiteAdjacentResources(c.Tri);
            foreach (ResourceType r in resList)
            {
                if (r != ResourceType.None) produced.Add(r);
            }
        }
        return produced;
    }

    private List<ResourceType> GetSiteAdjacentResources(Tri site)
    {
        var list = new List<ResourceType>();
        if (site == null || site.AdjacentPlanetSystems == null) return list;

        foreach (var sys in site.AdjacentPlanetSystems)
        {
            if (sys == null || !sys.ArePlanetProductionNumbersRevealed || sys.PlanetResources == null) continue;
            int[] indices = sys.GetAdjacentPlanetIndicesForIntersection(site);
            if (indices == null) continue;
            foreach (int idx in indices)
            {
                if (idx >= 0 && idx < sys.PlanetResources.Length)
                {
                    list.Add(sys.PlanetResources[idx]);
                }
            }
        }
        return list;
    }

    private int CalculateIntersectionPips(Tri tri)
    {
        if (tri == null) return 0;
        int totalPips = 0;
        if (tri.AdjacentPlanetSystems != null)
        {
            foreach (var sys in tri.AdjacentPlanetSystems)
            {
                if (sys == null) continue;
                int[] indices = sys.GetAdjacentPlanetIndicesForIntersection(tri);
                if (indices == null) continue;
                foreach (int idx in indices)
                {
                    if (sys.ArePlanetProductionNumbersRevealed && sys.PlanetProductionNumbers != null)
                    {
                        if (idx >= 0 && idx < sys.PlanetProductionNumbers.Length)
                        {
                            int num = sys.PlanetProductionNumbers[idx];
                            if (num >= 2 && num <= 12 && num != 7)
                            {
                                totalPips += 6 - Mathf.Abs(7 - num);
                            }
                        }
                    }
                    else if (sys.GetPlanetSpecialType(idx) == PlanetSpecialType.Normal)
                    {
                        // Unrevealed standard planet: expected value is approx 3 pips
                        totalPips += 3;
                    }
                }
            }
        }
        return totalPips;
    }

    private int Distance(Tri a, Tri b)
    {
        if (a == null || b == null) return int.MaxValue;
        return Mathf.Abs(a.Q - b.Q) + Mathf.Abs(a.R - b.R);
    }

    /// <summary>
    /// Intelligently chooses the best Friendship Card for an AI player upon founding a trade station.
    /// </summary>
    public static FriendshipCard ChooseBestFriendshipCard(Player player, AlienCivilization civ, List<FriendshipCard> deck)
    {
        if (deck == null || deck.Count == 0) return null;
        if (player == null || player.Difficulty == Player.AiDifficulty.Easy)
        {
            return deck[Random.Range(0, deck.Count)];
        }
        if (player.Difficulty == Player.AiDifficulty.Normal)
        {
            return deck[0];
        }

        // Hard AI: prioritize high-impact cards
        var priority = new[]
        {
            FriendshipCardId.DiplomatBoughtFame1,
            FriendshipCardId.DiplomatBoughtFame2,
            FriendshipCardId.TraderTradeGoods11,
            FriendshipCardId.DiplomatTributeDiscount,
            FriendshipCardId.ScientistUpgradeCannonAndDrive1,
            FriendshipCardId.ScientistUpgradeCannonAndDrive2,
            FriendshipCardId.ScientistUpgradeCannonAndDrive3,
            FriendshipCardId.ScientistUpgradeDrives2,
            FriendshipCardId.ScientistUpgradeCannons2,
            FriendshipCardId.DiplomatRichHelpsPoor
        };

        foreach (var id in priority)
        {
            var card = deck.FirstOrDefault(c => c.Id == id);
            if (card != null) return card;
        }

        return deck[0];
    }

    /// <summary>
    /// Chooses which cards to discard when rolling a 7 with > 7 cards.
    /// Prefers discarding resources with the highest surplus.
    /// </summary>
    public static Dictionary<ResourceType, int> ChooseDiscardCards(Player player, int countToDiscard)
    {
        var discardDict = new Dictionary<ResourceType, int>();
        foreach (ResourceType r in GameRules.Resources)
        {
            discardDict[r] = 0;
        }

        if (player == null || countToDiscard <= 0) return discardDict;

        var currentCounts = new Dictionary<ResourceType, int>();
        foreach (ResourceType r in GameRules.Resources)
        {
            currentCounts[r] = player.GetResourceCount(r);
        }

        int discarded = 0;
        while (discarded < countToDiscard)
        {
            ResourceType highestType = ResourceType.None;
            int maxCount = 0;
            foreach (var kvp in currentCounts)
            {
                if (kvp.Value > maxCount)
                {
                    maxCount = kvp.Value;
                    highestType = kvp.Key;
                }
            }

            if (highestType == ResourceType.None) break;

            discardDict[highestType]++;
            currentCounts[highestType]--;
            discarded++;
        }

        return discardDict;
    }

    /// <summary>
    /// Chooses an opponent to steal 1 hidden resource card from when rolling a 7.
    /// Prefers opponent with highest Victory Points, then highest card count.
    /// </summary>
    public static Player ChooseStealTarget(Player activePlayer, List<Player> candidateTargets)
    {
        if (candidateTargets == null || candidateTargets.Count == 0) return null;

        return candidateTargets
            .OrderByDescending(p => p.VictoryPoints)
            .ThenByDescending(p => p.TotalResources)
            .FirstOrDefault();
    }
}
