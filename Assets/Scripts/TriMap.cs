using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using QPath;
using System.Linq;

public class TriMap : MonoBehaviour, IQPathWorld
{
    [Header("=== Central Debug & Developer Settings ===")]
    public GameDebugSettings DebugSettings = new GameDebugSettings();

    [Header("Encounter Card Assets")]
    [Tooltip("Optional EncounterCardCatalogAsset. If left unassigned, cards load automatically from Resources or JSON data.")]
    public EncounterCardCatalogAsset EncounterCardsCatalogAsset;

    public int CurrentBoardSeed { get; set; }
    public bool IsCurrentBoardSeedFixed { get; set; }

    public void SetBoardSeed(int seed, bool fixedSeed = true)
    {
        CurrentBoardSeed = seed;
        IsCurrentBoardSeedFixed = fixedSeed;
        UnityEngine.Random.InitState(seed);
    }

    /// <summary>
    /// Completely rebuilds the board (mesh tiles, star systems, trade outposts, discovery markers)
    /// using the authoritative network seed provided by the game Host.
    /// </summary>
    public void RebuildMap(int seed)
    {
        if (seed == 0) return;
        Debug.Log($"[TriMap] Rebuilding map from network seed: {seed} (previous seed was: {CurrentBoardSeed})");

        // 1. Clear visuals and markers
        ClearPlanetDiscoveryMarkers();
        ClearShipPlacementMarkers();
        ClearSpaceportUpgradeMarkers();

        // 2. Destroy all current child GameObjects of TriMap
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null)
            {
                child.gameObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        // 3. Clear all cached collections and lookup tables
        triToGameObjectMap?.Clear();
        gameObjectToTriMap?.Clear();
        starSystemViews?.Clear();
        tradeOutpostDockMarkers?.Clear();
        tradeOutpostRoots?.Clear();
        colonyToGameObjectMap?.Clear();
        tradeStationToGameObjectMap?.Clear();
        unitToGameObjectMap?.Clear();
        _activeMovingViews.Clear();
        _animatingUnitsOnClient.Clear();
        _animatingUnitTargets.Clear();
        _unitsPendingDestructionAfterAnimation.Clear();
        if (createdOutpostMaterials != null)
        {
            foreach (var mat in createdOutpostMaterials)
            {
                if (mat != null)
                {
                    if (Application.isPlaying) Destroy(mat);
                    else DestroyImmediate(mat);
                }
            }
            createdOutpostMaterials.Clear();
        }
        AgentBridge.ExternalAgentBridge.Instance?.ClearSiteCache();

        // 4. Update seed settings centrally
        SetBoardSeed(seed, fixedSeed: true);
        if (DebugSettings != null)
        {
            DebugSettings.UseFixedBoardSeed = true;
            DebugSettings.FixedBoardSeed = seed;
        }
        GameSetupConfig.UseFixedSeed = true;
        GameSetupConfig.Seed = seed;

        if (this is TriMapSFnew sfNew)
        {
            sfNew.BoardSeed = seed;
        }

        // 5. Re-shuffle encounter card deck deterministically with the seed
        if (EncounterCardCatalog.Cards != null && EncounterCardCatalog.Cards.Count > 0)
        {
            encounterDeck = new EncounterCardDeck(EncounterCardCatalog.Cards, seed);
        }

        // 6. Generate the new map layout
        GenerateMap();

        Debug.Log($"[TriMap] Successfully rebuilt map with Host seed: {CurrentBoardSeed}");
    }

    public bool IsGameSetupActive { get; set; } = false;

    public bool PlanetDiscoveryTestMode
    {
        get => DebugSettings != null && DebugSettings.PlanetDiscoveryTestMode;
        set { if (DebugSettings != null) DebugSettings.PlanetDiscoveryTestMode = value; }
    }

    public bool HighlightPlanetDiscoveryPoints
    {
        get => DebugSettings != null && DebugSettings.HighlightPlanetDiscoveryPoints;
        set { if (DebugSettings != null) DebugSettings.HighlightPlanetDiscoveryPoints = value; }
    }

    public bool PlanetDiscoveryDiagnosticsEnabled
    {
        get => DebugSettings != null && DebugSettings.PlanetDiscoveryDiagnostics;
        set { if (DebugSettings != null) DebugSettings.PlanetDiscoveryDiagnostics = value; }
    }

    public bool IsDebug
    {
        get => DebugSettings != null && DebugSettings.ShowCoordinateLabels;
        set
        {
            if (DebugSettings != null) DebugSettings.ShowCoordinateLabels = value;
            UpdateCoordinateLabelsVisibility();
        }
    }

    public bool ShowSystemDebugLabels
    {
        get => DebugSettings != null && DebugSettings.ShowSystemDebugLabels;
        set
        {
            if (DebugSettings != null) DebugSettings.ShowSystemDebugLabels = value;
            UpdateSystemLabelsVisibility();
        }
    }

    protected virtual void OnEnable()
    {
        if (DebugSettings != null && !DebugSettings.HighlightPlanetDiscoveryPoints)
        {
            ClearPlanetDiscoveryMarkers();
        }
        if (Networking.NetworkManager.Instance != null)
        {
            Networking.NetworkManager.Instance.OnClientGameStateReceived -= HandleNetworkGameStateReceived;
            Networking.NetworkManager.Instance.OnClientGameStateReceived += HandleNetworkGameStateReceived;
        }
    }

    protected virtual void OnDisable()
    {
        if (Networking.NetworkManager.Instance != null)
        {
            Networking.NetworkManager.Instance.OnClientGameStateReceived -= HandleNetworkGameStateReceived;
        }
    }

    private void HandleNetworkGameStateReceived(AgentBridge.GameStateObservationDTO obs)
    {
        if (Networking.NetworkManager.Instance != null &&
            Networking.NetworkManager.Instance.IsClientConnected &&
            !Networking.NetworkManager.Instance.IsHost)
        {
            if (obs != null && obs.boardSeed != 0 && CurrentBoardSeed != obs.boardSeed)
            {
                Debug.Log($"[TriMap] Board seed mismatch detected in GameStateObservation (Client={CurrentBoardSeed}, Host={obs.boardSeed}). Rebuilding map to match Host...");
                RebuildMap(obs.boardSeed);
            }
            SyncFromObservation(obs);
        }
    }

    public static TriMap Instance { get; private set; }

    protected virtual void Awake()
    {
        Instance = this;
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 60;
        Physics.simulationMode = SimulationMode.Script;
        Audio.PhaseAudioManager.EnsureInitialized();

        if (DebugSettings == null)
        {
            DebugSettings = new GameDebugSettings();
        }
        GameDebugSettings.Active = DebugSettings;
        LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
        LocalizationManager.SetLanguage(DebugSettings.Language);

        string[] cliArgs = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < cliArgs.Length; i++)
        {
            string a = cliArgs[i];
            if (a.ToLowerInvariant().StartsWith("-seed:") && int.TryParse(a.Substring(6), out int cliSeed))
            {
                DebugSettings.UseFixedBoardSeed = true;
                DebugSettings.FixedBoardSeed = cliSeed;
                GameSetupConfig.UseFixedSeed = true;
                GameSetupConfig.Seed = cliSeed;
                break;
            }
        }

        if (GameSetupConfig.HasPendingGameStart)
        {
            DebugSettings.UseFixedBoardSeed = GameSetupConfig.UseFixedSeed;
            DebugSettings.FixedBoardSeed = GameSetupConfig.Seed;
        }

        CurrentBoardSeed = DebugSettings.ResolveBoardSeed(out bool wasFixed);
        IsCurrentBoardSeedFixed = wasFixed;
        UnityEngine.Random.InitState(CurrentBoardSeed);
        Debug.Log(string.Format("[TriMap] Board initialized with {0} seed: {1}",
            wasFixed ? "fixed" : "random", CurrentBoardSeed));

        if (!DebugSettings.HighlightPlanetDiscoveryPoints)
        {
            ClearPlanetDiscoveryMarkers();
        }

        RuntimeLog.WriteLine(
            "Planet discovery diagnostics enabled: " + PlanetDiscoveryDiagnosticsEnabled +
            "; discovery test mode: " + PlanetDiscoveryTestMode +
            "; board seed: " + CurrentBoardSeed + " (fixed=" + wasFixed + ").");

        if (DebugSettings.EnableAgentBridge)
        {
            if (GetComponent<AgentBridge.ExternalAgentBridge>() == null)
            {
                gameObject.AddComponent<AgentBridge.ExternalAgentBridge>();
            }
        }

        if (EncounterCardsCatalogAsset != null)
        {
            EncounterCardCatalog.Cards = EncounterCardsCatalogAsset.CreateCards();
        }
        else
        {
            EncounterCardCatalog.Cards = EncounterCardDataLoader.LoadCards();
        }

        encounterDeck = new EncounterCardDeck(
            EncounterCardCatalog.Cards,
            UnityEngine.Random.Range(0, int.MaxValue));
        GeneratePlayers(4);

        GenerateMap();
        if (GetComponent<AiPlayerController>() == null)
        {
            gameObject.AddComponent<AiPlayerController>();
        }

        if (PlanetDiscoveryTestMode)
        {
            BeginPlanetDiscoveryTestSetup();
        }
        else if (DebugSettings.ShowGameSetupOnLaunch && !GameSetupConfig.HasPendingGameStart)
        {
            IsGameSetupActive = true;
            ApplyGameSetupConfig();
        }
        else
        {
            IsGameSetupActive = false;
            GameSetupConfig.HasPendingGameStart = false;
            ApplyGameSetupConfig();
            BeginFoundingPhase();
        }
    }

    public void ApplyGameSetupConfig()
    {
        if (Players == null) return;
        int count = Mathf.Clamp(GameSetupConfig.ActivePlayerCount, 2, Players.Length);
        for (int i = 0; i < Players.Length; i++)
        {
            if (i < count && i < GameSetupConfig.Slots.Length)
            {
                var slot = GameSetupConfig.Slots[i];
                if (!string.IsNullOrEmpty(slot.Name))
                {
                    Players[i].PlayerName = slot.Name;
                }
                Players[i].Type = slot.Type;
                Players[i].Difficulty = slot.Difficulty;
            }
        }

        if (DebugSettings != null)
        {
            DebugSettings.ShowSystemDebugLabels = GameSetupConfig.ShowSystemDebugLabels;
        }
        UpdateSystemLabelsVisibility();
    }

    public void StartGameFromSetup()
    {
        if (!IsGameSetupActive) return;
        IsGameSetupActive = false;
        ApplyGameSetupConfig();
        BeginFoundingPhase();
        if (PlayerHandler.Instance != null)
        {
            PlayerHandler.Instance.RefreshFoundingUI();
            PlayerHandler.Instance.RefreshVictoryPointsTrackUI();
        }
        if (GetComponent<AiPlayerController>() != null)
        {
            GetComponent<AiPlayerController>().TriggerAiCheck();
        }
    }

    private readonly HashSet<UnitView> _activeMovingViews = new HashSet<UnitView>();
    private readonly HashSet<Unit> _animatingUnitsOnClient = new HashSet<Unit>();
    private readonly Dictionary<Unit, Tri> _animatingUnitTargets = new Dictionary<Unit, Tri>();
    private readonly HashSet<Unit> _unitsPendingDestructionAfterAnimation = new HashSet<Unit>();

    public void RegisterUnitViewMoving(UnitView view)
    {
        if (view != null) _activeMovingViews.Add(view);
    }

    public void UnregisterUnitViewMoving(UnitView view)
    {
        if (view != null) _activeMovingViews.Remove(view);
    }

    public void RegisterUnitAnimating(Unit unit, Tri destTri)
    {
        if (unit == null) return;
        _animatingUnitsOnClient.Add(unit);
        if (destTri != null)
        {
            _animatingUnitTargets[unit] = destTri;
        }
    }

    public void DeregisterUnitAnimating(Unit unit)
    {
        if (unit == null) return;
        _animatingUnitsOnClient.Remove(unit);
        _animatingUnitTargets.Remove(unit);
        if (_unitsPendingDestructionAfterAnimation.Remove(unit))
        {
            unit.Destroy();
        }
    }

    public bool AnimationIsPlaying
    {
        get
        {
            if (_activeMovingViews.Count == 0) return false;
            List<UnitView> toRemove = null;
            foreach (var v in _activeMovingViews)
            {
                if (v == null || !v.gameObject.activeInHierarchy)
                {
                    if (toRemove == null) toRemove = new List<UnitView>();
                    toRemove.Add(v);
                }
            }
            if (toRemove != null)
            {
                for (int i = 0; i < toRemove.Count; i++) _activeMovingViews.Remove(toRemove[i]);
            }
            return _activeMovingViews.Count > 0;
        }
        set
        {
            if (!value) _activeMovingViews.Clear();
        }
    }

    public GameObject TriPrefab; 
    public GameObject ColonyPrefab;
    public GameObject TradeBasePrefab;
    public GameObject SpaceportPrefab;
    public GameObject ColonyShipPrefab;
    public GameObject TradeShipPrefab;

    public Material FoodMaterial;
    public Material FuelMaterial;
    public Material CarbonMaterial;
    public Material OreMaterial;
    public Material TradeMaterial;

    public GameObject StarSystemPrefab;

    public Material[] TriMaterials;

    public Material[] PlayerMaterials;

    public Player.PlayerType[] PlayerTypes;

    public int TurnNumber = 0;
    public FoundingPhase CurrentFoundingPhase { get; private set; }
    public int FoundingRound { get; private set; }
    public string FoundingStatus { get; private set; }
    private System.Func<string> foundingStatusFormatter;

    public void SetFoundingStatus(System.Func<string> formatter)
    {
        foundingStatusFormatter = formatter;
        FoundingStatus = formatter != null ? formatter() : string.Empty;
    }
    public int RemainingStartingDrives { get; private set; }
    public int RemainingStartingCannons { get; private set; }
    public int RemainingStartingCargoModules { get; private set; }
    public string PlanetDiscoveryTestInstruction { get; private set; }

    int numRows = 13;
    int numColumns = 32;

    public int NumRows 
    {
        get { return numRows; }
        protected set { numRows = value; }
    }

    public int NumColumns 
    {
        get { return numColumns; }
        protected set { numColumns = value; }
    }

    [System.NonSerialized]
    public bool AllowWrapNorthSouth = false;
    [System.NonSerialized]
    public bool AllowWrapEastWest = false;

    protected Tri[,] tris;
    public GameBoardModel BoardModel { get; private set; }
    private Dictionary<Tri, GameObject> triToGameObjectMap;
    private Dictionary<Unit, GameObject> unitToGameObjectMap;
    private Dictionary<Colony, GameObject> colonyToGameObjectMap;
    private Dictionary<TradeStation, GameObject> tradeStationToGameObjectMap;
    private Dictionary<Tri, GameObject[]> tradeOutpostDockMarkers;
    private Dictionary<Tri, Transform> tradeOutpostRoots;
    private readonly Dictionary<Tri, TextMesh> tradeOutpostLabels = new Dictionary<Tri, TextMesh>();
    private Dictionary<TradeStation, int> tradeStationSlots;
    private readonly Dictionary<Colony, GameObject> spaceportVisuals =
        new Dictionary<Colony, GameObject>();
    private readonly Dictionary<Tri, StarSystem> starSystemViews =
        new Dictionary<Tri, StarSystem>();
    public Dictionary<Tri, StarSystem> StarSystemViews => starSystemViews;
    private GameObject planetDiscoveryTestMarker;
    private GameObject planetDiscoveryTestStartMarker;
    private readonly Dictionary<Tri, GameObject> planetDiscoveryMarkers =
        new Dictionary<Tri, GameObject>();
    private GameObject planetDiscoveryMarkersContainer;
    private Material tradeOutpostMaterial;
    private Material tradeDockMaterial;
    private readonly List<Material> createdOutpostMaterials = new List<Material>();
    private StarshipType? pendingShipPlacementType;
    private readonly List<GameObject> shipPlacementMarkers = new List<GameObject>();
    private readonly HashSet<Tri> currentMarkerTris = new HashSet<Tri>();
    private readonly List<GameObject> spaceportUpgradeMarkers = new List<GameObject>();
    private readonly List<GameObject> spaceJumpMarkers = new List<GameObject>();
    private readonly HashSet<Tri> currentSpaceJumpMarkerTris = new HashSet<Tri>();

    private Dictionary<GameObject, Tri> gameObjectToTriMap;
    private EncounterCardDeck encounterDeck;

    public delegate void ColonyCreatedDelegate ( Colony colony, GameObject colonyGO );
    public event ColonyCreatedDelegate OnColonyCreated;
    public delegate void TradeStationCreatedDelegate ( TradeStation tradeStation, GameObject colonyGO );
    public event TradeStationCreatedDelegate OnTradeStationCreated;


    public Player[] Players;
    int currentPlayerIndex = 0;
    public int CurrentPlayerIndex => currentPlayerIndex;
    public Player CurrentPlayer 
    {
        get { return Players[currentPlayerIndex]; }
    }

    public void NextPlayer()
    {
        if (PlayerHandler.Instance != null &&
            CurrentPhase == GameTurnPhase.Flight &&
            CurrentPlayer != null &&
            CurrentPlayer.Type != Player.PlayerType.AI)
        {
            if (PlayerHandler.Instance.CheckAndPromptConfirmEndTurn())
            {
                return;
            }
        }

        AdvanceTurnPhase();
    }

    private void SetCurrentPlayerIndex(int index)
    {
        currentPlayerIndex = index;
        if (Players != null && index >= 0 && index < Players.Length)
        {
            OnPlayerChanged?.Invoke(Players[index]);
        }
    }

    public GameTurnPhase CurrentPhase { get; private set; }
    public ProductionRollResult LastProductionRoll { get; private set; }
    public FlagshipRollResult LastFlagshipRoll { get; private set; }
    public bool EncounterPending { get; private set; }
    public EncounterCard CurrentEncounterCard { get; private set; }
    public Player EncounterReader { get; private set; }
    public EncounterCardPhase CurrentEncounterPhase { get; private set; }
    public EncounterCardOutcome SelectedEncounterOutcome { get; private set; }

    /// <summary>Victory points a player must own at the end of their own turn to win.</summary>
    public const int WinningVictoryPoints = 15;

    public bool IsGameOver { get; private set; }
    public Player Winner { get; private set; }

    /// <summary>Raised once when the game ends. Argument: the winning player.</summary>
    public event System.Action<Player> OnGameOver;

    public event System.Action<Player> OnPlayerChanged;
    public event System.Action<Player, GameTurnPhase> OnTurnPhaseChanged;
    public event System.Action<Player, ProductionRollResult> OnProductionRoll;
    public event System.Action<Player, FlagshipRollResult> OnFlagshipRoll;
    public event System.Action<bool> OnEncounterStateChanged;
    public event System.Action<Player, EncounterCard> OnEncounterCardDrawn;
    public event System.Action<EncounterCardPhase, EncounterCardOutcome> OnEncounterProgressChanged;
    public event System.Action<Player, FoundingPhase> OnFoundingPhaseChanged;
    public event System.Action OnShipPlacementStateChanged;
    public event System.Action OnSpaceportUpgradeStateChanged;
    public event System.Action OnVictoryPointsChanged;
    public event System.Action<Unit> OnUnitMoveInitiated;
    public event System.Action<Unit> OnUnitMoveCompleted;

    // --- Friendship Decks & Standee Majority (Freundschafts-Aufsteller) ---
    public Dictionary<AlienCivilization, Player> FolkStandeeHolders { get; private set; } = new Dictionary<AlienCivilization, Player>();
    private Dictionary<AlienCivilization, List<FriendshipCard>> alienFriendshipDecks;
    public event System.Action<Player, AlienCivilization, FriendshipCard> OnFriendshipCardSelected;
    public event System.Action<Player, AlienCivilization, List<FriendshipCard>> OnPromptFriendshipCardSelection;

    public bool IsPlacingShip { get { return pendingShipPlacementType.HasValue; } }
    public StarshipType? PendingShipPlacementType { get { return pendingShipPlacementType; } }
    public bool IsUpgradingSpaceport { get; private set; }
    public ReserveDeck ReserveDeck { get; } = new ReserveDeck();
    public ReserveNumberDeck ReserveNumberDeck { get; } = new ReserveNumberDeck();
    public bool IsResolvingSevenRoll { get; private set; }
    public AgentBridge.DialogStateDTO NetworkDialogs { get; set; }
    public AgentBridge.EncounterStateDTO NetworkEncounter { get; set; }
    public event System.Action<System.Action> OnSevenRollResolutionNeeded;
    public event System.Action OnSpecialPlanetResolved;
    
    private List<int> startingPlayerCandidates;
    private Dictionary<int, int> startingPlayerRolls;
    private int startingPlayerRollIndex;

    public Player CurrentStartingPlayerCandidate
    {
        get
        {
            if (CurrentFoundingPhase == FoundingPhase.RollForStartingPlayer &&
                startingPlayerCandidates != null &&
                startingPlayerRollIndex >= 0 &&
                startingPlayerRollIndex < startingPlayerCandidates.Count)
            {
                return Players[startingPlayerCandidates[startingPlayerRollIndex]];
            }
            return null;
        }
    }

    public int StartingPlayerCandidateIndex
    {
        get
        {
            if (CurrentFoundingPhase == FoundingPhase.RollForStartingPlayer &&
                startingPlayerCandidates != null &&
                startingPlayerRollIndex >= 0 &&
                startingPlayerRollIndex < startingPlayerCandidates.Count)
            {
                return startingPlayerCandidates[startingPlayerRollIndex];
            }
            return -1;
        }
    }

    public void SyncStartingPlayerCandidateIndex(int candidatePlayerIndex)
    {
        if (startingPlayerCandidates == null)
        {
            startingPlayerCandidates = Enumerable.Range(0, Players != null ? Players.Length : 4).ToList();
        }
        int idx = startingPlayerCandidates.IndexOf(candidatePlayerIndex);
        if (idx >= 0)
        {
            startingPlayerRollIndex = idx;
        }
        else
        {
            startingPlayerCandidates.Clear();
            startingPlayerCandidates.Add(candidatePlayerIndex);
            startingPlayerRollIndex = 0;
        }
        OnFoundingPhaseChanged?.Invoke(CurrentPlayer, CurrentFoundingPhase);
    }

    public System.Collections.Generic.IEnumerable<Tri> AllTris
    {
        get
        {
            if (tris == null) yield break;
            int cols = tris.GetLength(0);
            int rows = tris.GetLength(1);
            for (int x = 0; x < cols; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    if (tris[x, y] != null)
                    {
                        yield return tris[x, y];
                    }
                }
            }
        }
    }
    private int startingPlayerIndex;
    private List<int> foundingPlayerOrder;
    private int foundingPlayerOrderIndex;
    private Colony selectedFoundingSpaceport;
    private StarshipType? selectedFoundingShipType;
    private const float ShipHoverHeight = 0.55f;

    public Player GetPlayerForUnit(Unit unit)
    {
        if (unit == null || Players == null)
        {
            return CurrentPlayer;
        }

        for (int i = 0; i < Players.Length; i++)
        {
            if (Players[i] != null && Players[i].Units != null && System.Array.IndexOf(Players[i].Units, unit) >= 0)
            {
                return Players[i];
            }
        }

        return CurrentPlayer;
    }

    public Player GetPlayerForColony(Colony colony)
    {
        if (colony == null || Players == null)
        {
            return CurrentPlayer;
        }

        for (int i = 0; i < Players.Length; i++)
        {
            if (Players[i] != null && Players[i].Colonies != null && System.Array.IndexOf(Players[i].Colonies, colony) >= 0)
            {
                return Players[i];
            }
        }

        return CurrentPlayer;
    }

    public bool CanSelectUnit(Unit unit)
    {
        if (unit == null || unit.IsDestroyed || CurrentFoundingPhase != FoundingPhase.Complete || AnimationIsPlaying)
        {
            return false;
        }

        if (System.Array.IndexOf(CurrentPlayer.Units, unit) < 0)
        {
            return false;
        }

        if (CurrentPhase == GameTurnPhase.TradeAndBuild)
        {
            return true;
        }

        if (CurrentPhase == GameTurnPhase.Flight)
        {
            return LastFlagshipRoll != null && !EncounterPending;
        }

        return false;
    }

    public bool CanMoveUnit(Unit unit)
    {
        return CanSelectUnit(unit) &&
            CurrentPhase == GameTurnPhase.Flight &&
            LastFlagshipRoll != null &&
            !EncounterPending &&
            unit.MovementRemaining > 0;
    }

    public Tri[] FindShipPath(Unit unit, Tri destination)
    {
        if (!CanMoveUnit(unit) || destination == null)
        {
            return null;
        }

        Tri target = destination;
        if (!target.IsPassable)
        {
            target = target.GetNeighbours()
                .OfType<Tri>()
                .Where(n => n.IsPassable)
                .OrderBy(n => (n.PositionFromCamera() - target.PositionFromCamera()).sqrMagnitude)
                .FirstOrDefault();
        }

        if (target == null || target == unit.Tri)
        {
            return null;
        }

        Queue<Tri> frontier = new Queue<Tri>();
        Dictionary<Tri, Tri> previous = new Dictionary<Tri, Tri>();
        frontier.Enqueue(unit.Tri);
        previous.Add(unit.Tri, null);

        while (frontier.Count > 0)
        {
            Tri current = frontier.Dequeue();
            if (current == target)
            {
                List<Tri> fullPath = new List<Tri>();
                Tri pathNode = target;
                while (pathNode != null)
                {
                    fullPath.Add(pathNode);
                    pathNode = previous[pathNode];
                }
                fullPath.Reverse();

                if (fullPath.Count < 2)
                {
                    return null;
                }

                // The player may select any point on the board.
                // Truncate path to what the ship can legally reach this turn.
                int maxSteps = Mathf.Min(fullPath.Count - 1, unit.MovementRemaining);
                while (maxSteps > 0 && !CanEndShipMoveAt(unit, fullPath[maxSteps]))
                {
                    maxSteps--;
                }

                if (maxSteps <= 0)
                {
                    return null;
                }

                return fullPath.GetRange(0, maxSteps + 1).ToArray();
            }

            foreach (IQPathTile neighborTile in current.GetNeighbours())
            {
                Tri neighbor = neighborTile as Tri;
                if (neighbor == null || !neighbor.IsPassable || previous.ContainsKey(neighbor))
                {
                    continue;
                }

                previous.Add(neighbor, current);
                frontier.Enqueue(neighbor);
            }
        }

        return null;
    }

    public Tri[] FindDirectShipPath(Tri start, Tri target)
    {
        if (start == null || target == null || start == target) return null;

        Queue<Tri> frontier = new Queue<Tri>();
        Dictionary<Tri, Tri> previous = new Dictionary<Tri, Tri>();
        frontier.Enqueue(start);
        previous.Add(start, null);

        int maxVisits = 2500;
        int visits = 0;

        while (frontier.Count > 0 && visits++ < maxVisits)
        {
            Tri current = frontier.Dequeue();
            if (current == target)
            {
                List<Tri> fullPath = new List<Tri>();
                Tri pathNode = target;
                while (pathNode != null)
                {
                    fullPath.Add(pathNode);
                    pathNode = previous[pathNode];
                }
                fullPath.Reverse();
                return fullPath.ToArray();
            }

            foreach (IQPathTile neighborTile in current.GetNeighbours())
            {
                Tri neighbor = neighborTile as Tri;
                if (neighbor == null || !neighbor.IsPassable || previous.ContainsKey(neighbor))
                {
                    continue;
                }

                previous.Add(neighbor, current);
                frontier.Enqueue(neighbor);
            }
        }

        return null;
    }

    /// <summary>
    /// Computes the exact shortest path step distance between two locations using BFS.
    /// Returns int.MaxValue if no path exists.
    /// </summary>
    public int GetPathDistance(Tri start, Tri destination)
    {
        if (start == null || destination == null) return int.MaxValue;
        if (start == destination) return 0;
        Tri dest = destination;
        if (!dest.IsPassable)
        {
            dest = dest.GetNeighbours()
                .OfType<Tri>()
                .Where(n => n.IsPassable)
                .OrderBy(n => (n.PositionFromCamera() - dest.PositionFromCamera()).sqrMagnitude)
                .FirstOrDefault();
        }
        if (dest == null) return int.MaxValue;
        if (start == dest) return 0;
        Tri[] path = FindDirectShipPath(start, dest);
        return path != null ? path.Length - 1 : int.MaxValue;
    }

    private System.Collections.IEnumerator AnimateUnitMovementOnClient(Unit unit, Tri[] path, Tri destTri, int finalMovementRemaining)
    {
        if (unit == null || path == null || path.Length < 2) yield break;
        if (_animatingUnitsOnClient.Contains(unit)) yield break;

        RegisterUnitAnimating(unit, destTri);
        try
        {
            unit.MovementRemaining = path.Length + 5;
            unit.SetTriPath(path);
            yield return DoUnitMoves(unit);
        }
        finally
        {
            unit.MovementRemaining = finalMovementRemaining;
            DeregisterUnitAnimating(unit);
        }
    }

    public bool CanMoveShipAlongPath(Unit unit, Tri[] path)
    {
        if (!CanMoveUnit(unit) || path == null || path.Length < 2 ||
            path[0] != unit.Tri ||
            path.Length - 1 > unit.MovementRemaining ||
            !CanEndShipMoveAt(unit, path[path.Length - 1]))
        {
            return false;
        }

        for (int i = 1; i < path.Length; i++)
        {
            if (path[i] == null || !path[i].IsPassable ||
                !path[i - 1].GetNeighbours().Contains(path[i]))
            {
                return false;
            }
        }

        return true;
    }

    private bool CanEndShipMoveAt(Unit unit, Tri destination)
    {
        return CanShipEndMoveAt(unit, destination, out _);
    }

    public bool CanShipEndMoveAt(Unit unit, Tri destination, out string failureReason)
    {
        failureReason = null;
        if (unit == null || destination == null)
        {
            failureReason = UIStrings.DestinationNotReachable.GetText();
            return false;
        }

        if (!destination.IsPassable)
        {
            failureReason = UIStrings.DestinationNotPassable.GetText();
            return false;
        }

        if (destination.Units.Any(occupant => occupant != unit))
        {
            failureReason = UIStrings.DestinationOccupied.GetText();
            return false;
        }

        if (IsSpaceportPointOwnedByOpponent(destination, CurrentPlayer))
        {
            failureReason = UIStrings.DestinationOpponentSpaceport.GetText();
            return false;
        }

        if (unit.ShipType == StarshipType.ColonyShip)
        {
            if (destination.IsTradePoint)
            {
                failureReason = UIStrings.ColonyShipCannotDock.GetText();
                return false;
            }

            if (destination.Colony != null)
            {
                failureReason = UIStrings.DestinationAlreadyHasColony.GetText();
                return false;
            }

            return true;
        }

        if (unit.ShipType == StarshipType.TradeShip)
        {
            if (destination.Colony != null || destination.IsColonyPoint)
            {
                failureReason = UIStrings.TradeShipCannotColonize.GetText();
                return false;
            }

            if (destination.IsTradePoint)
            {
                if (destination.SystemKind != SystemKind.Trade)
                {
                    failureReason = UIStrings.DestinationInvalidOutpost.GetText();
                    return false;
                }

                int currentStations = destination.TradeStations != null
                    ? destination.TradeStations.Count(station => station != null)
                    : 0;

                if (currentStations >= 5)
                {
                    failureReason = UIStrings.OutpostDocksFull.GetText();
                    return false;
                }

                Player player = GetPlayerForUnit(unit);
                if (player != null)
                {
                    int freightPods = player.FlagshipFreightPodCount;
                    if (freightPods <= currentStations)
                    {
                        failureReason = string.Format(
                            UIStrings.TradeShipNeedsFreightPods.GetText(),
                            freightPods,
                            currentStations + 1,
                            currentStations);
                        return false;
                    }
                }

                return true;
            }

            return true;
        }

        if (destination.Colony != null || destination.IsColonyPoint)
        {
            failureReason = UIStrings.DestinationAlreadyHasColony.GetText();
            return false;
        }

        if (destination.IsTradePoint)
        {
            failureReason = UIStrings.ColonyShipCannotDock.GetText();
            return false;
        }

        return true;
    }

    public bool IsSpaceportPointOwnedByOpponent(Tri point, Player player)
    {
        foreach (Player otherPlayer in Players)
        {
            if (otherPlayer == player)
            {
                continue;
            }

            foreach (Colony colony in otherPlayer.Colonies)
            {
                if (colony.IsSpaceport && GetSpaceportPoints(colony).Contains(point))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private IEnumerable<Tri> GetSpaceportPoints(Colony spaceport)
    {
        if (spaceport == null || spaceport.Tri == null)
        {
            return Enumerable.Empty<Tri>();
        }

        return spaceport.Tri.GetNeighbours()
            .Cast<Tri>()
            .Where(point => point.IsPassable)
            .OrderBy(point => point.Q)
            .ThenBy(point => point.R)
            .Take(2);
    }

    public Vector3 GetShipHoverPosition(Tri tri)
    {
        if (tri == null)
        {
            throw new System.ArgumentNullException("tri");
        }

        if (triToGameObjectMap != null && triToGameObjectMap.TryGetValue(tri, out GameObject triObject) && triObject != null)
        {
            return triObject.transform.position + Vector3.up * ShipHoverHeight;
        }

        Vector3 position = tri.PositionFromCamera();
        return new Vector3(position.x, ShipHoverHeight, position.z);
    }

    void GeneratePlayers( int numPlayers )
    {
        Players = new Player[numPlayers];
        bool singleHuman = DebugSettings != null ? DebugSettings.SingleHumanPlayerMode : true;

        for (int i = 0; i < numPlayers; i++)
        {
            Players[i] = new Player( "Player " + (i+1).ToString() );
            Players[i].PlayerNumber = i + 1;

            if (PlayerTypes != null && i < PlayerTypes.Length)
            {
                Players[i].Type = PlayerTypes[i];
            }
            else if (singleHuman)
            {
                // In single-human mode, Player 1 is LOCAL and all subsequent players are AI
                Players[i].Type = i == 0 ? Player.PlayerType.LOCAL : Player.PlayerType.AI;
            }
            else
            {
                Players[i].Type = Player.PlayerType.LOCAL;
            }

            Players[i].Material = PlayerMaterials[i];
        }
        currentPlayerIndex = 0;

        InitializeFriendshipDecks();
    }

    private void InitializeFriendshipDecks()
    {
        alienFriendshipDecks = new Dictionary<AlienCivilization, List<FriendshipCard>>
        {
            { AlienCivilization.Trader, FriendshipCard.CreateDeckForCivilization(AlienCivilization.Trader) },
            { AlienCivilization.Scientist, FriendshipCard.CreateDeckForCivilization(AlienCivilization.Scientist) },
            { AlienCivilization.Diplomat, FriendshipCard.CreateDeckForCivilization(AlienCivilization.Diplomat) },
            { AlienCivilization.Cultivator, FriendshipCard.CreateDeckForCivilization(AlienCivilization.Cultivator) }
        };
        FolkStandeeHolders = new Dictionary<AlienCivilization, Player>
        {
            { AlienCivilization.Trader, null },
            { AlienCivilization.Scientist, null },
            { AlienCivilization.Diplomat, null },
            { AlienCivilization.Cultivator, null }
        };
    }

    public void AdvanceToNextPlayer()
    {
        AdvanceTurnPhase();
    }

    public void AdvanceTurnPhase()
    {
        if (IsGameOver)
        {
            return;
        }

        if (CurrentFoundingPhase != FoundingPhase.Complete)
        {
            Debug.LogWarning("Complete the founding phase before advancing the turn.");
            return;
        }

        switch(CurrentPhase)
        {
            case GameTurnPhase.Production:
                if (LastProductionRoll == null)
                {
                    Debug.LogWarning("Roll the production dice before advancing to trade and build.");
                    break;
                }
                if (IsResolvingSevenRoll)
                {
                    Debug.LogWarning("Resolve seven roll sequence before advancing to trade and build.");
                    break;
                }
                SetTurnPhase(GameTurnPhase.TradeAndBuild);
                break;
            case GameTurnPhase.TradeAndBuild:
                if (IsPlacingShip)
                {
                    Debug.LogWarning("Cancel or place the selected ship before ending the build phase.");
                    break;
                }
                if(CurrentPlayer.Units.Length == 0)
                {
                    Debug.Log("No ships on the board; skipping the flight phase.");
                    EndCurrentTurn();
                }
                else
                {
                    BeginFlightPhase();
                }
                break;
            case GameTurnPhase.Flight:
                if (LastFlagshipRoll == null)
                {
                    Debug.LogWarning("Determine flagship flight impulse before ending the flight phase.");
                    break;
                }
                if (EncounterPending)
                {
                    Debug.LogWarning("Resolve the encounter before ending the flight phase.");
                    break;
                }
                if (IsExecutingSpaceJump)
                {
                    CancelSpaceJump(showToast: false);
                }
                EndCurrentTurn();
                break;
            default:
                throw new System.InvalidOperationException("The current turn phase is invalid.");
        }
    }

    private void BeginFoundingPhase()
    {
        RemainingStartingDrives = 2;
        RemainingStartingCannons = 1;
        RemainingStartingCargoModules = 1;
        startingPlayerCandidates = Enumerable.Range(0, Players.Length).ToList();
        startingPlayerRolls = new Dictionary<int, int>();
        startingPlayerRollIndex = 0;
        FoundingRound = 0;
        SetFoundingStatus(() => UIStrings.StatusRollForStartingPlayer.GetText());
        SetFoundingPhase(FoundingPhase.RollForStartingPlayer);
    }

    private void BeginPlanetDiscoveryTestSetup()
    {
        Tri planetSystem = tris
            .Cast<Tri>()
            .Where(tri => tri.SystemKind == SystemKind.Planets &&
                !tri.ArePlanetProductionNumbersRevealed)
            .OrderBy(tri =>
                Mathf.Abs(tri.Q - NumColumns / 2) + Mathf.Abs(tri.R - NumRows / 2))
            .ThenBy(tri => tri.Q)
            .ThenBy(tri => tri.R)
            .FirstOrDefault();

        if (planetSystem == null)
        {
            throw new System.InvalidOperationException(
                "Planet-discovery test mode requires an unrevealed planet system.");
        }

        Tri target = null;
        Tri start = null;
        foreach (Tri intersection in planetSystem.GetPlanetSystemAdjacentIntersections()
            .OrderBy(point => point.Q)
            .ThenBy(point => point.R))
        {
            if (!intersection.IsPassable ||
                intersection.IsTradePoint ||
                intersection.Colony != null)
            {
                continue;
            }

            Tri approach = tris
                .Cast<Tri>()
                .Where(point => point.IsPassable &&
                    point.Units.Length == 0 &&
                    !planetSystem.IsAdjacentToPlanetSystem(point) &&
                    point.GetNeighbours().Contains(intersection))
                .OrderBy(point => point.Q)
                .ThenBy(point => point.R)
                .FirstOrDefault();

            if (approach != null)
            {
                target = intersection;
                start = approach;
                break;
            }
        }

        if (target == null || start == null)
        {
            throw new System.InvalidOperationException(
                "Could not find a legal one-step approach to planet system " +
                planetSystem + " for the discovery test.");
        }

        CurrentPlayer.PlayerName = UIStrings.DiscoveryTestPlayerName.GetText();
        Unit testShip = new Unit
        {
            ShipType = StarshipType.ColonyShip
        };
        SpawnShipAt(testShip, start);
        testShip.SetMovementAllowance(1);

        Tri discoveryStart = start;
        Tri discoveryTarget = target;
        Tri discoverySystem = planetSystem;
        SetFoundingStatus(() => string.Format(
            UIStrings.DiscoveryTestInstruction.GetText(),
            discoveryStart,
            discoveryTarget,
            discoverySystem));
        PlanetDiscoveryTestInstruction = FoundingStatus;
        planetDiscoveryTestMarker = CreateSpaceportPart(
            transform,
            PrimitiveType.Cylinder,
            "PlanetDiscoveryTestTarget",
            Vector3.zero,
            new Vector3(0.72f, 0.035f, 0.72f),
            new Color(1f, 0.45f, 0.05f));
        planetDiscoveryTestMarker.transform.position =
            target.PositionFromCamera() + Vector3.up * 0.08f;
        planetDiscoveryTestStartMarker = CreateSpaceportPart(
            transform,
            PrimitiveType.Cylinder,
            "PlanetDiscoveryTestStart",
            Vector3.zero,
            new Vector3(0.54f, 0.035f, 0.54f),
            new Color(0.05f, 0.8f, 1f));
        planetDiscoveryTestStartMarker.transform.position =
            start.PositionFromCamera() + Vector3.up * 0.08f;
        TurnNumber = 1;
        SetFoundingPhase(FoundingPhase.Complete);
        SetTurnPhase(GameTurnPhase.Flight);
        Debug.Log(FoundingStatus, this);
    }

    public void RollForStartingPlayer()
    {
        if (CurrentFoundingPhase != FoundingPhase.RollForStartingPlayer)
        {
            throw new System.InvalidOperationException("The starting-player roll is not active.");
        }

        int playerIndex = startingPlayerCandidates[startingPlayerRollIndex];
        int firstDie = UnityEngine.Random.Range(1, 7);
        int secondDie = UnityEngine.Random.Range(1, 7);
        int total = firstDie + secondDie;
        startingPlayerRolls[playerIndex] = total;
        Player rollPlayer = Players[playerIndex];
        startingPlayerRollIndex++;

        if (startingPlayerRollIndex < startingPlayerCandidates.Count)
        {
            Player nextPlayer = Players[startingPlayerCandidates[startingPlayerRollIndex]];
            SetFoundingStatus(() => string.Format(UIStrings.StatusPlayerRolled.GetText(), rollPlayer.PlayerName, firstDie, secondDie, total) +
                string.Format(UIStrings.StatusNextRoll.GetText(), nextPlayer.PlayerName));
            OnFoundingPhaseChanged?.Invoke(CurrentPlayer, CurrentFoundingPhase);
            return;
        }

        int highestRoll = startingPlayerRolls.Values.Max();
        List<int> winners = startingPlayerRolls
            .Where(roll => roll.Value == highestRoll)
            .Select(roll => roll.Key)
            .ToList();
        if (winners.Count == 1)
        {
            int firstPlayerIndex = winners[0];
            startingPlayerIndex = firstPlayerIndex;
            SetCurrentPlayerIndex(firstPlayerIndex);
            Player winner = CurrentPlayer;
            SetFoundingStatus(() => string.Format(UIStrings.StatusPlayerRolled.GetText(), rollPlayer.PlayerName, firstDie, secondDie, total) +
                string.Format(UIStrings.StatusStartingPlayerStarts.GetText(), winner.PlayerName));
            BeginColonyPlacementRound(1, firstPlayerIndex, 1);
            return;
        }

        startingPlayerCandidates = winners;
        startingPlayerRolls.Clear();
        startingPlayerRollIndex = 0;
        SetFoundingStatus(() => string.Format(UIStrings.StatusPlayerRolled.GetText(), rollPlayer.PlayerName, firstDie, secondDie, total) +
            UIStrings.StatusTieRollAgain.GetText());
        OnFoundingPhaseChanged?.Invoke(CurrentPlayer, CurrentFoundingPhase);
    }

    public bool TryPlaceFoundingColonyAt(Tri tri)
    {
        if (!IsColonyPlacementPhase(CurrentFoundingPhase))
        {
            return false;
        }
        if (tri == null || !tri.IsColonyPoint || tri.Colony != null)
        {
            Debug.LogWarning("Choose an unoccupied colony point.");
            return false;
        }
        if (!IsHomeColonyPoint(tri))
        {
            Debug.LogWarning("Founding colonies must be placed at home systems (Alpha, Beta, Gamma, Delta).");
            return false;
        }

        SpawnColonyAt(new Colony(), ColonyPrefab, tri.Q, tri.R);
        if (tri.Colony == null)
        {
            return false;
        }

        Player colonyPlacer = CurrentPlayer;
        int colonyRound = FoundingRound;
        SetFoundingStatus(() => string.Format(
            UIStrings.StatusColonyPlaced.GetText(),
            colonyPlacer.PlayerName,
            colonyRound));
        AdvanceFoundingPlacement();
        return true;
    }

    public bool TrySelectFoundingSpaceportAt(Tri tri)
    {
        bool isOwnedByCurrent = CurrentPlayer != null && CurrentPlayer.Colonies != null &&
            (System.Array.IndexOf(CurrentPlayer.Colonies, tri?.Colony) >= 0 || CurrentPlayer.Colonies.Any(c => c == tri?.Colony || (c.Tri != null && c.Tri == tri)));

        if (CurrentFoundingPhase != FoundingPhase.SelectSpaceport ||
            tri == null ||
            tri.Colony == null ||
            !isOwnedByCurrent)
        {
            Debug.LogWarning("Choose one of the current player's colonies as the spaceport.");
            return false;
        }

        if (!GetFreePointsAtSpaceport(tri.Colony).Any())
        {
            Debug.LogWarning("The selected colony has no free adjacent spaceport point.");
            return false;
        }
        selectedFoundingSpaceport = tri.Colony;
        selectedFoundingSpaceport.UpgradeToSpaceport();
        MarkSpaceportVisual(selectedFoundingSpaceport);
        CurrentPlayer.NotifyUpgradesChanged();
        Player spaceportPlayer = CurrentPlayer;
        SetFoundingStatus(() => string.Format(
            UIStrings.StatusSpaceportUpgraded.GetText(),
            spaceportPlayer.PlayerName));
        SetFoundingPhase(FoundingPhase.SelectStartingShip);
        return true;
    }

    public void SelectStartingShip(StarshipType shipType)
    {
        if (selectedFoundingSpaceport == null)
        {
            selectedFoundingSpaceport = CurrentPlayer?.Colonies?.FirstOrDefault(c => c != null && c.IsSpaceport);
        }

        if (CurrentFoundingPhase != FoundingPhase.SelectStartingShip ||
            selectedFoundingSpaceport == null)
        {
            throw new System.InvalidOperationException("A starting ship cannot be selected now.");
        }

        if (!GetFreePointsAtSpaceport(selectedFoundingSpaceport).Any())
        {
            throw new System.InvalidOperationException(
                "The selected spaceport has no free adjacent spaceport point for its starting ship.");
        }
        selectedFoundingShipType = shipType;
        UpdateShipPlacementMarkers(GetFreePointsAtSpaceport(selectedFoundingSpaceport));
        Player shipChooser = CurrentPlayer;
        StarshipType chosenType = shipType;
        SetFoundingStatus(() => string.Format(
            UIStrings.StatusSelectedStartingShip.GetText(),
            shipChooser.PlayerName,
            UIStrings.GetShipName(chosenType)));
        SetFoundingPhase(FoundingPhase.SelectStartingShipPoint);
    }

    public bool TrySelectFoundingStartingShipPoint(Tri tri)
    {
        if (selectedFoundingSpaceport == null)
        {
            selectedFoundingSpaceport = CurrentPlayer?.Colonies?.FirstOrDefault(c => c != null && c.IsSpaceport);
        }
        if (!selectedFoundingShipType.HasValue)
        {
            selectedFoundingShipType = StarshipType.ColonyShip;
        }

        if (CurrentFoundingPhase != FoundingPhase.SelectStartingShipPoint ||
            selectedFoundingSpaceport == null ||
            !selectedFoundingShipType.HasValue ||
            tri == null ||
            !GetFreePointsAtSpaceport(selectedFoundingSpaceport).Contains(tri))
        {
            Debug.LogWarning("Choose one of the free spaceport points highlighted for the starting ship.");
            return false;
        }

        Unit ship = new Unit();
        ship.ShipType = selectedFoundingShipType.Value;
        SpawnShipAt(ship, tri);
        selectedFoundingShipType = null;
        ClearShipPlacementMarkers();
        Player launchedPlayer = CurrentPlayer;
        StarshipType launchedType = ship.ShipType;
        SetFoundingStatus(() => string.Format(
            UIStrings.StatusPlacedStartingShip.GetText(),
            launchedPlayer.PlayerName,
            UIStrings.GetShipName(launchedType)));
        SetFoundingPhase(FoundingPhase.SelectFlagshipUpgrade);
        return true;
    }

    public bool CanBuildShip(StarshipType shipType)
    {
        if (CurrentFoundingPhase != FoundingPhase.Complete ||
            CurrentPhase != GameTurnPhase.TradeAndBuild ||
            IsPlacingShip ||
            IsUpgradingSpaceport ||
            !GetFreeSpaceportPoints(CurrentPlayer).Any())
        {
            return false;
        }

        // Validate personal component supply limits
        if (CurrentPlayer.AvailableTransportShips <= 0)
        {
            return false;
        }

        if (shipType == StarshipType.ColonyShip && CurrentPlayer.AvailableColoniesCount <= 0)
        {
            return false;
        }

        if (shipType == StarshipType.TradeShip && CurrentPlayer.AvailableTradeStationsCount <= 0)
        {
            return false;
        }

        IDictionary<ResourceType, int> cost = GetShipCost(shipType);
        return CurrentPlayer.HasResources(cost);
    }

    public bool IsPlacingCapturedShip { get; set; }

    public bool CanDeployCapturedTradeShip(Player player = null)
    {
        Player p = player ?? CurrentPlayer;
        if (p == null || p.CapturedTradeShipsCount <= 0) return false;
        if (CurrentFoundingPhase != FoundingPhase.Complete || CurrentPhase != GameTurnPhase.TradeAndBuild) return false;
        if (IsPlacingShip || IsUpgradingSpaceport) return false;
        if (p.AvailableTransportShips <= 0) return false;
        if (p.AvailableTradeStationsCount <= 0) return false;
        return GetFreeSpaceportPoints(p).Any();
    }

    public bool TryBeginShipPlacement(StarshipType shipType, bool isCapturedFree = false)
    {
        if (CurrentFoundingPhase != FoundingPhase.Complete ||
            CurrentPhase != GameTurnPhase.TradeAndBuild)
        {
            Debug.LogWarning("Ships can only be built during the trade and build phase.");
            return false;
        }
        if (IsPlacingShip || IsUpgradingSpaceport)
        {
            Debug.LogWarning("Finish or cancel the current placement first.");
            return false;
        }
        if (CurrentPlayer.AvailableTransportShips <= 0)
        {
            PlayerHandler.ShowToast(UIStrings.LimitTransportShipsReached.GetText(), isWarning: true);
            Debug.LogWarning(UIStrings.LimitTransportShipsReached.GetText());
            return false;
        }
        if (shipType == StarshipType.ColonyShip && CurrentPlayer.AvailableColoniesCount <= 0)
        {
            PlayerHandler.ShowToast(UIStrings.LimitColoniesReached.GetText(), isWarning: true);
            Debug.LogWarning(UIStrings.LimitColoniesReached.GetText());
            return false;
        }
        if (shipType == StarshipType.TradeShip && CurrentPlayer.AvailableTradeStationsCount <= 0)
        {
            PlayerHandler.ShowToast(UIStrings.LimitTradeStationsReached.GetText(), isWarning: true);
            Debug.LogWarning(UIStrings.LimitTradeStationsReached.GetText());
            return false;
        }
        if (!GetFreeSpaceportPoints(CurrentPlayer).Any())
        {
            PlayerHandler.ShowToast(UIStrings.NoFreeSpaceportPoints.GetText(), isWarning: true);
            Debug.LogWarning(UIStrings.NoFreeSpaceportPoints.GetText());
            return false;
        }

        if (!isCapturedFree)
        {
            IDictionary<ResourceType, int> cost = GetShipCost(shipType);
            if (!CurrentPlayer.HasResources(cost))
            {
                PlayerHandler.ShowToast(string.Format("{0} ({1})", UIStrings.NotEnoughResources.GetText(), UIStrings.FormatCost(cost)), isWarning: true);
                Debug.LogWarning(UIStrings.NotEnoughResources.GetText());
                return false;
            }
        }
        else
        {
            if (CurrentPlayer.CapturedTradeShipsCount <= 0)
            {
                Debug.LogWarning("No captured trade ships in reserve.");
                return false;
            }
        }

        IsPlacingCapturedShip = isCapturedFree;
        pendingShipPlacementType = shipType;
        UpdateShipPlacementMarkers();
        OnShipPlacementStateChanged?.Invoke();
        return true;
    }

    public bool TryPlacePendingShipAt(Tri tri)
    {
        if (!IsPlacingShip || tri == null || !GetFreeSpaceportPoints(CurrentPlayer).Contains(tri))
        {
            Debug.LogWarning("Choose a free spaceport point next to one of your spaceports.");
            return false;
        }

        StarshipType shipType = pendingShipPlacementType.Value;
        if (!IsPlacingCapturedShip)
        {
            IDictionary<ResourceType, int> cost = GetShipCost(shipType);
            if (!CurrentPlayer.SpendResources(cost))
            {
                Debug.LogWarning("You no longer have enough resources to build this ship.");
                return false;
            }
        }
        else
        {
            CurrentPlayer.CapturedTradeShipsCount = Mathf.Max(0, CurrentPlayer.CapturedTradeShipsCount - 1);
            IsPlacingCapturedShip = false;
        }

        Unit ship = new Unit();
        ship.ShipType = shipType;
        SpawnShipAt(ship, tri);
        pendingShipPlacementType = null;
        ClearShipPlacementMarkers();
        OnShipPlacementStateChanged?.Invoke();
        RuntimeLog.WriteLine(string.Format(
            "[ShipBuilding] Player '{0}' successfully placed {1} at spaceport point ({2},{3}).",
            CurrentPlayer.PlayerName,
            UIStrings.GetShipName(shipType),
            tri.Q,
            tri.R));
        Debug.Log(string.Format(
            UIStrings.StatusShipBuilt.GetText(),
            CurrentPlayer.PlayerName,
            UIStrings.GetShipName(shipType)));
        return true;
    }

    public bool CanFoundColonyWith(Unit ship)
    {
        if (ship == null || ship.IsDestroyed || ship.ShipType != StarshipType.ColonyShip || ship.Tri == null)
        {
            return false;
        }

        if (CurrentFoundingPhase != FoundingPhase.Complete ||
            (CurrentPhase != GameTurnPhase.TradeAndBuild &&
             CurrentPhase != GameTurnPhase.Flight) ||
            AnimationIsPlaying ||
            IsPlacingShip ||
            IsUpgradingSpaceport)
        {
            return false;
        }

        if (!ship.Tri.IsColonyPoint || ship.Tri.Colony != null)
        {
            return false;
        }

        if (IsColonySiteBlockedByHazard(ship.Tri))
        {
            Player owner = GetPlayerForUnit(ship) ?? CurrentPlayer;
            if (owner == null || !CanPlayerResolveColonySiteHazards(owner, ship.Tri))
            {
                return false;
            }
        }

        return true;
    }

    public bool IsColonySiteBlockedByHazard(Tri colonySite)
    {
        if (colonySite == null) return false;
        if (colonySite.AdjacentPlanetSystems != null && colonySite.AdjacentPlanetSystems.Length > 0)
        {
            foreach (Tri planetSystem in colonySite.AdjacentPlanetSystems)
            {
                if (planetSystem != null && planetSystem.IsIntersectionBlockedByHazard(colonySite))
                {
                    return true;
                }
            }
        }
        else
        {
            foreach (var n in colonySite.GetAllNeighbours())
            {
                if (n is Tri planetSystem && planetSystem.SystemKind == SystemKind.Planets)
                {
                    if (planetSystem.IsIntersectionBlockedByHazard(colonySite))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    public bool CanPlayerResolveColonySiteHazards(Player player, Tri colonySite)
    {
        if (player == null || colonySite == null) return false;
        if (!IsColonySiteBlockedByHazard(colonySite)) return true;

        List<Tri> systems = new List<Tri>();
        if (colonySite.AdjacentPlanetSystems != null && colonySite.AdjacentPlanetSystems.Length > 0)
        {
            systems.AddRange(colonySite.AdjacentPlanetSystems);
        }
        foreach (var n in colonySite.GetAllNeighbours())
        {
            if (n is Tri pt && pt.SystemKind == SystemKind.Planets && !systems.Contains(pt))
            {
                systems.Add(pt);
            }
        }

        foreach (Tri planetSystem in systems)
        {
            if (planetSystem == null) continue;
            int[] indices = planetSystem.GetAdjacentPlanetIndicesForIntersection(colonySite);
            foreach (int idx in indices)
            {
                if (planetSystem.HasUnresolvedHazard(idx))
                {
                    PlanetSpecialType type = planetSystem.GetPlanetSpecialType(idx);
                    int req = planetSystem.GetPlanetSpecialRequirement(idx);
                    if (type == PlanetSpecialType.IcePlanet)
                    {
                        if (player.FlagshipFreightPodCount < req)
                        {
                            return false;
                        }
                    }
                    else if (type == PlanetSpecialType.MarauderBase)
                    {
                        if (player.EffectiveCannonCount < req)
                        {
                            return false;
                        }
                    }
                }
            }
        }
        return true;
    }

    public bool HasColonySiteHazardType(Tri colonySite, PlanetSpecialType hazardType)
    {
        if (colonySite == null || !IsColonySiteBlockedByHazard(colonySite)) return false;
        List<Tri> systems = new List<Tri>();
        if (colonySite.AdjacentPlanetSystems != null && colonySite.AdjacentPlanetSystems.Length > 0)
        {
            systems.AddRange(colonySite.AdjacentPlanetSystems);
        }
        foreach (var n in colonySite.GetAllNeighbours())
        {
            if (n is Tri pt && pt.SystemKind == SystemKind.Planets && !systems.Contains(pt))
            {
                systems.Add(pt);
            }
        }
        foreach (Tri planetSystem in systems)
        {
            if (planetSystem == null) continue;
            int[] indices = planetSystem.GetAdjacentPlanetIndicesForIntersection(colonySite);
            foreach (int idx in indices)
            {
                if (planetSystem.HasUnresolvedHazard(idx) && planetSystem.GetPlanetSpecialType(idx) == hazardType)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public bool CanEstablishTradeStationWith(Unit ship)
    {
        if (ship == null || ship.IsDestroyed || ship.ShipType != StarshipType.TradeShip || ship.Tri == null)
        {
            return false;
        }

        if (CurrentFoundingPhase != FoundingPhase.Complete ||
            (CurrentPhase != GameTurnPhase.TradeAndBuild &&
             CurrentPhase != GameTurnPhase.Flight) ||
            AnimationIsPlaying ||
            IsPlacingShip ||
            IsUpgradingSpaceport)
        {
            return false;
        }

        Tri tri = ship.Tri;
        if (!tri.IsTradePoint || tri.SystemKind != SystemKind.Trade)
        {
            return false;
        }

        int currentStations = tri.TradeStations != null
            ? tri.TradeStations.Count(station => station != null)
            : 0;

        if (currentStations >= 5)
        {
            return false;
        }

        Player player = GetPlayerForUnit(ship);
        if (player == null || player.BuiltTradeStationsCount >= GameRules.MaxTradeStationsPerPlayer)
        {
            return false;
        }

        return player.FlagshipFreightPodCount > currentStations;
    }

    public bool TryFoundColonyAt(Tri tri)
    {
        if (CurrentFoundingPhase != FoundingPhase.Complete ||
            (CurrentPhase != GameTurnPhase.TradeAndBuild &&
             CurrentPhase != GameTurnPhase.Flight) ||
            AnimationIsPlaying ||
            IsPlacingShip ||
            IsUpgradingSpaceport ||
            tri == null ||
            !tri.IsColonyPoint ||
            tri.Colony != null)
        {
            return false;
        }

        Unit colonyShip = CurrentPlayer.Units.FirstOrDefault(unit =>
            unit.ShipType == StarshipType.ColonyShip &&
            unit.Tri == tri);
        if (colonyShip == null)
        {
            return false;
        }

        if (IsColonySiteBlockedByHazard(tri))
        {
            if (CanPlayerResolveColonySiteHazards(CurrentPlayer, tri))
            {
                CheckAndResolveSpecialPlanetsAt(colonyShip, tri);
            }

            if (IsColonySiteBlockedByHazard(tri))
            {
                return false;
            }
        }

        SpawnColonyAt(new Colony(), ColonyPrefab, tri.Q, tri.R);
        if (tri.Colony == null)
        {
            return false;
        }

        colonyShip.Destroy();
        RuntimeLog.WriteLine(string.Format(
            "[Colonization] Player '{0}' successfully founded a colony at ({1},{2}). Colony ship consumed.",
            CurrentPlayer.PlayerName,
            tri.Q,
            tri.R));
        Debug.Log(string.Format(
            UIStrings.StatusColonyFounded.GetText(),
            CurrentPlayer.PlayerName,
            tri));
        NotifyVictoryPointsChanged();
        return true;
    }

    public void NotifyVictoryPointsChanged()
    {
        OnVictoryPointsChanged?.Invoke();
        if (PlayerHandler.Instance != null)
        {
            PlayerHandler.Instance.RefreshVictoryPointsTrackUI();
        }
    }

    public bool TryEstablishTradeStationAt(Tri tri)
    {
        if (CurrentFoundingPhase != FoundingPhase.Complete ||
            (CurrentPhase != GameTurnPhase.TradeAndBuild &&
             CurrentPhase != GameTurnPhase.Flight) ||
            AnimationIsPlaying ||
            IsPlacingShip ||
            IsUpgradingSpaceport ||
            tri == null ||
            !tri.IsTradePoint ||
            tri.SystemKind != SystemKind.Trade)
        {
            return false;
        }

        Unit tradeShip = CurrentPlayer.Units.FirstOrDefault(unit =>
            unit.ShipType == StarshipType.TradeShip &&
            unit.Tri == tri);
        if (tradeShip == null)
        {
            return false;
        }

        int currentStations = tri.TradeStations != null
            ? tri.TradeStations.Count(station => station != null)
            : 0;

        if (currentStations >= 5)
        {
            string msg = UIStrings.OutpostDocksFull.GetText();
            Debug.LogWarning(msg);
            PlayerHandler.ShowToast(msg, isWarning: true);
            return false;
        }

        if (CurrentPlayer.BuiltTradeStationsCount >= GameRules.MaxTradeStationsPerPlayer)
        {
            string msg = UIStrings.LimitTradeStationsReached.GetText();
            Debug.LogWarning(msg);
            PlayerHandler.ShowToast(msg, isWarning: true);
            return false;
        }

        if (CurrentPlayer.FlagshipFreightPodCount <= currentStations)
        {
            string msg = string.Format(
                UIStrings.TradeShipNeedsFreightPods.GetText(),
                CurrentPlayer.FlagshipFreightPodCount,
                currentStations + 1,
                currentStations);
            Debug.LogWarning(msg);
            PlayerHandler.ShowToast(msg, isWarning: true);
            return false;
        }

        TradeStation tradeStation = new TradeStation();
        SpawnTradeStationAt(tradeStation, TradeBasePrefab, tri.Q, tri.R);
        tradeShip.Destroy();
        RuntimeLog.WriteLine(string.Format(
            "[TradeStation] Player '{0}' successfully established a trade station at outpost ({1},{2}) (dock slot {3}/5). Trade ship consumed.",
            CurrentPlayer.PlayerName,
            tri.Q,
            tri.R,
            currentStations + 1));
        Debug.Log(string.Format(
            UIStrings.StatusTradeStationEstablished.GetText(),
            CurrentPlayer.PlayerName,
            tri));

        // Update folk standee majority (+2 VP)
        UpdateFolkMajority(tri.AlienCivilization, CurrentPlayer);

        // Award Friendship Card from the civilization's deck
        AwardFriendshipCardFromCivilization(tri.AlienCivilization, CurrentPlayer);

        return true;
    }

    public void UpdateFolkMajority(AlienCivilization civ, Player actingPlayer)
    {
        if (Players == null || Players.Length == 0) return;

        // Count how many trade stations each player has at outposts of this civilization
        Dictionary<Player, int> stationCounts = new Dictionary<Player, int>();
        foreach (Player p in Players)
        {
            int count = p.TradeStations.Count(ts => ts.Tri != null && ts.Tri.AlienCivilization == civ);
            stationCounts[p] = count;
        }

        Player currentHolder = null;
        if (FolkStandeeHolders != null && FolkStandeeHolders.TryGetValue(civ, out Player holder))
        {
            currentHolder = holder;
        }

        int actingCount = stationCounts.ContainsKey(actingPlayer) ? stationCounts[actingPlayer] : 0;

        if (currentHolder == null)
        {
            // First player to establish a station gains the standee (+2 VP)
            if (actingCount > 0)
            {
                FolkStandeeHolders[civ] = actingPlayer;
                actingPlayer.FriendshipVictoryPoints += 2;
                string msg = string.Format(UIStrings.FriendshipStandeeAcquired.GetText(), actingPlayer.PlayerName, UIStrings.GetAlienCivilizationName(civ));
                RuntimeLog.WriteLine("[FriendshipStandee] " + msg);
                PlayerHandler.ShowToast(msg, isWarning: false, duration: 4.5f);
                NotifyVictoryPointsChanged();
            }
        }
        else if (currentHolder != actingPlayer)
        {
            int currentHolderCount = stationCounts.ContainsKey(currentHolder) ? stationCounts[currentHolder] : 0;
            // The standee can be taken if another player has MORE trade stations (strict majority)
            if (actingCount > currentHolderCount)
            {
                currentHolder.FriendshipVictoryPoints = Mathf.Max(0, currentHolder.FriendshipVictoryPoints - 2);
                actingPlayer.FriendshipVictoryPoints += 2;
                FolkStandeeHolders[civ] = actingPlayer;
                string msg = string.Format(UIStrings.FriendshipStandeeLost.GetText(), currentHolder.PlayerName, UIStrings.GetAlienCivilizationName(civ), actingPlayer.PlayerName);
                RuntimeLog.WriteLine("[FriendshipStandee] " + msg);
                PlayerHandler.ShowToast(msg, isWarning: false, duration: 4.5f);
                NotifyVictoryPointsChanged();
            }
        }
    }

    public List<FriendshipCard> GetAvailableFriendshipCards(AlienCivilization civ)
    {
        if (alienFriendshipDecks != null && alienFriendshipDecks.TryGetValue(civ, out List<FriendshipCard> deck))
        {
            return new List<FriendshipCard>(deck);
        }
        return new List<FriendshipCard>();
    }

    public bool FriendshipCardSelectionPending { get; private set; }
    public Player PendingFriendshipPlayer { get; private set; }
    public AlienCivilization PendingFriendshipCiv { get; private set; }
    public List<FriendshipCard> PendingFriendshipCards { get; private set; }

    public void AwardFriendshipCardFromCivilization(AlienCivilization civ, Player player)
    {
        if (alienFriendshipDecks == null || !alienFriendshipDecks.TryGetValue(civ, out List<FriendshipCard> deck) || deck.Count == 0)
        {
            return;
        }

        if (player.Type == Player.PlayerType.AI)
        {
            // AI chooses the best available card based on difficulty
            FriendshipCard chosen = AiPlayerController.ChooseBestFriendshipCard(player, civ, deck) ?? deck[0];
            deck.Remove(chosen);
            player.AddFriendshipCard(chosen);
            RuntimeLog.WriteLine(string.Format("[FriendshipCard] AI '{0}' chose '{1}' ({2}).", player.PlayerName, chosen.GetName(), civ));
            OnFriendshipCardSelected?.Invoke(player, civ, chosen);
        }
        else
        {
            FriendshipCardSelectionPending = true;
            PendingFriendshipPlayer = player;
            PendingFriendshipCiv = civ;
            PendingFriendshipCards = new List<FriendshipCard>(deck);

            // Human player: trigger selection UI
            OnPromptFriendshipCardSelection?.Invoke(player, civ, PendingFriendshipCards);
        }
    }

    public bool SelectFriendshipCard(AlienCivilization civ, Player player, FriendshipCardId cardId)
    {
        if (alienFriendshipDecks == null || !alienFriendshipDecks.TryGetValue(civ, out List<FriendshipCard> deck))
        {
            return false;
        }

        FriendshipCard card = deck.FirstOrDefault(c => c.Id == cardId);
        if (card == null)
        {
            return false;
        }

        deck.Remove(card);
        player.AddFriendshipCard(card);
        FriendshipCardSelectionPending = false;
        PendingFriendshipPlayer = null;
        PendingFriendshipCards = null;
        RuntimeLog.WriteLine(string.Format("[FriendshipCard] Player '{0}' selected '{1}' ({2}).", player.PlayerName, card.GetName(), civ));
        OnFriendshipCardSelected?.Invoke(player, civ, card);
        return true;
    }

    public void CancelShipPlacement()
    {
        if (!IsPlacingShip)
        {
            return;
        }

        IsPlacingCapturedShip = false;
        pendingShipPlacementType = null;
        ClearShipPlacementMarkers();
        OnShipPlacementStateChanged?.Invoke();
    }

    private void UpdateShipPlacementMarkers()
    {
        UpdateShipPlacementMarkers(GetFreeSpaceportPoints(CurrentPlayer));
    }

    private void UpdateShipPlacementMarkers(IEnumerable<Tri> points)
    {
        List<Tri> pointList = points?.Where(p => p != null).ToList() ?? new List<Tri>();
        if (shipPlacementMarkers.Count > 0 && currentMarkerTris.SetEquals(pointList))
        {
            return;
        }

        ClearShipPlacementMarkers();
        foreach (Tri tri in pointList)
        {
            GameObject triGO = GetTriGO(tri);
            Transform parentTransform = triGO != null ? triGO.transform : transform;

            GameObject marker = CreateSpaceportPart(
                parentTransform,
                PrimitiveType.Cylinder,
                "ShipPlacementPoint",
                Vector3.zero,
                new Vector3(0.55f, 0.035f, 0.55f),
                new Color(0.2f, 0.85f, 1f, 0.85f));
            Vector3 worldPos = triGO != null
                ? triGO.transform.position + Vector3.up * 0.1f
                : tri.PositionFromCamera() + Vector3.up * 0.1f;
            marker.transform.position = worldPos;
            shipPlacementMarkers.Add(marker);
            currentMarkerTris.Add(tri);
        }
    }

    private void ClearShipPlacementMarkers()
    {
        foreach (GameObject marker in shipPlacementMarkers)
        {
            if (marker != null)
            {
                Destroy(marker);
            }
        }
        shipPlacementMarkers.Clear();
        currentMarkerTris.Clear();
    }

    public void UpdateSpaceJumpMarkers()
    {
        if (PendingSpaceJumpShip == null)
        {
            ClearSpaceJumpMarkers();
            return;
        }

        List<Tri> candidateTris = new List<Tri>();
        if (PendingSpaceJumpShip.ShipType == StarshipType.ColonyShip)
        {
            candidateTris = AllTris
                .Where(t => t != null && t.IsColonyPoint && t.Colony == null && t.IsPassable && (t.Units == null || t.Units.Length == 0))
                .ToList();
        }
        else if (PendingSpaceJumpShip.ShipType == StarshipType.TradeShip)
        {
            candidateTris = AllTris
                .Where(t => t != null && t.IsTradePoint && t.IsPassable && (t.Units == null || t.Units.Length == 0))
                .ToList();
        }

        if (spaceJumpMarkers.Count > 0 && currentSpaceJumpMarkerTris.SetEquals(candidateTris))
        {
            return;
        }

        ClearSpaceJumpMarkers();
        foreach (Tri tri in candidateTris)
        {
            GameObject triGO = GetTriGO(tri);
            Transform parentTransform = triGO != null ? triGO.transform : transform;

            Color markerColor = PendingSpaceJumpShip.ShipType == StarshipType.ColonyShip
                ? new Color(0.2f, 0.9f, 1f, 0.85f)
                : new Color(1f, 0.75f, 0.2f, 0.85f);

            GameObject marker = CreateSpaceportPart(
                parentTransform,
                PrimitiveType.Cylinder,
                "SpaceJumpPoint",
                Vector3.zero,
                new Vector3(0.55f, 0.035f, 0.55f),
                markerColor);
            Vector3 worldPos = triGO != null
                ? triGO.transform.position + Vector3.up * 0.1f
                : tri.PositionFromCamera() + Vector3.up * 0.1f;
            marker.transform.position = worldPos;
            spaceJumpMarkers.Add(marker);
            currentSpaceJumpMarkerTris.Add(tri);
        }
    }

    public void ClearSpaceJumpMarkers()
    {
        foreach (GameObject marker in spaceJumpMarkers)
        {
            if (marker != null)
            {
                Destroy(marker);
            }
        }
        spaceJumpMarkers.Clear();
        currentSpaceJumpMarkerTris.Clear();
    }

    public bool TryExecuteSpaceJumpAt(Tri destination)
    {
        if (!IsExecutingSpaceJump || PendingSpaceJumpShip == null)
        {
            return false;
        }

        Unit ship = PendingSpaceJumpShip;
        if (destination == null)
        {
            PlayerHandler.ShowToast(UIStrings.DestinationNotReachable.GetText(), isWarning: true);
            return false;
        }

        if (destination == ship.Tri)
        {
            PlayerHandler.ShowToast(UIStrings.SpaceJumpSamePositionError.GetText(), isWarning: true);
            return false;
        }

        if (!CanShipEndMoveAt(ship, destination, out string failureReason))
        {
            PlayerHandler.ShowToast(failureReason ?? UIStrings.DestinationNotPassable.GetText(), isWarning: true);
            return false;
        }

        ExecuteSpaceJumpAt(ship, destination);
        PendingSpaceJumpShip = null;
        ClearSpaceJumpMarkers();
        OnSpaceJumpStateChanged?.Invoke();

        PlayerHandler.ShowToast(
            string.Format(UIStrings.SpaceJumpSuccessToast.GetText(), destination.Q, destination.R),
            isWarning: false,
            duration: 4.5f);

        SelectionController sc = FindFirstObjectByType<SelectionController>();
        if (sc != null)
        {
            sc.SelectedUnit = ship;
        }
        Camera.main?.GetComponent<CameraMotion>()?.PanToTri(destination, onlyIfOffscreen: true);

        return true;
    }

    public void ExecuteSpaceJumpAt(Unit ship, Tri destination)
    {
        if (ship == null || destination == null) return;

        Tri oldTri = ship.Tri;
        GameObject go = GetUnitGO(ship);
        if (go != null)
        {
            Vector3 newPos = GetShipHoverPosition(destination);
            go.GetComponent<UnitView>()?.TeleportTo(newPos);
        }

        ship.ClearTriPath();
        ship.SetTri(destination);
        ship.MovementRemaining = 0;

        RevealAdjacentPlanetSystems(destination);
        CheckAndResolveSpecialPlanetsAt(ship, destination);

        RuntimeLog.WriteLine(string.Format(
            "[SpaceJump] {0} of player '{1}' jumped from ({2},{3}) to ({4},{5}). Movement ended.",
            ship.ShipType,
            GetPlayerForUnit(ship)?.PlayerName ?? "Unknown",
            oldTri != null ? oldTri.Q.ToString() : "?",
            oldTri != null ? oldTri.R.ToString() : "?",
            destination.Q,
            destination.R));

        bool isAuthoritative = Networking.NetworkManager.Instance == null ||
                               !Networking.NetworkManager.Instance.IsClientConnected ||
                               Networking.NetworkManager.Instance.IsHost;
        if (isAuthoritative)
        {
            OnUnitMoveCompleted?.Invoke(ship);
        }
    }

    public void SetSpaceJumpShip(Unit ship)
    {
        if (ship == null || ship.IsDestroyed) return;
        if (CurrentPlayer != null && System.Array.IndexOf(CurrentPlayer.Units, ship) < 0) return;

        PendingSpaceJumpShip = ship;
        UpdateSpaceJumpMarkers();
        OnSpaceJumpStateChanged?.Invoke();

        SelectionController sc = FindFirstObjectByType<SelectionController>();
        if (sc != null)
        {
            sc.SelectedUnit = ship;
        }
        if (ship.Tri != null)
        {
            Camera.main?.GetComponent<CameraMotion>()?.PanToTri(ship.Tri, onlyIfOffscreen: true);
        }
    }

    public void CancelSpaceJump(bool showToast = true)
    {
        if (!IsExecutingSpaceJump) return;
        PendingSpaceJumpShip = null;
        ClearSpaceJumpMarkers();
        OnSpaceJumpStateChanged?.Invoke();
        if (showToast)
        {
            PlayerHandler.ShowToast(UIStrings.SpaceJumpCancelledToast.GetText(), isWarning: false, duration: 3.5f);
        }
    }

    private void ExecuteAiSpaceJump(Player aiPlayer, Unit ship)
    {
        if (aiPlayer == null || ship == null || tris == null) return;

        Tri bestDestination = null;

        if (ship.ShipType == StarshipType.ColonyShip)
        {
            var candidateSites = AllTris
                .Where(t => t != null && t != ship.Tri && CanShipEndMoveAt(ship, t, out _))
                .ToList();

            var colonyPoints = candidateSites
                .Where(t => t.IsColonyPoint && !IsColonySiteBlockedByHazard(t))
                .ToList();

            if (colonyPoints.Count > 0)
            {
                bestDestination = colonyPoints[Random.Range(0, colonyPoints.Count)];
            }
            else
            {
                var resolvable = candidateSites
                    .Where(t => t.IsColonyPoint && CanPlayerResolveColonySiteHazards(aiPlayer, t))
                    .ToList();
                if (resolvable.Count > 0)
                {
                    bestDestination = resolvable[Random.Range(0, resolvable.Count)];
                }
                else if (candidateSites.Count > 0)
                {
                    bestDestination = candidateSites[Random.Range(0, candidateSites.Count)];
                }
            }
        }
        else if (ship.ShipType == StarshipType.TradeShip)
        {
            var candidateSites = AllTris
                .Where(t => t != null && t != ship.Tri && CanShipEndMoveAt(ship, t, out _))
                .ToList();

            var tradePoints = candidateSites
                .Where(t => t.IsTradePoint)
                .ToList();

            if (tradePoints.Count > 0)
            {
                bestDestination = tradePoints[Random.Range(0, tradePoints.Count)];
            }
            else if (candidateSites.Count > 0)
            {
                bestDestination = candidateSites[Random.Range(0, candidateSites.Count)];
            }
        }

        if (bestDestination != null)
        {
            ExecuteSpaceJumpAt(ship, bestDestination);
        }
    }

    // --- Flagship Upgrades Construction ---
    public bool CanBuildUpgrade(FlagshipUpgrade upgrade)
    {
        if (CurrentFoundingPhase != FoundingPhase.Complete ||
            CurrentPhase != GameTurnPhase.TradeAndBuild ||
            IsPlacingShip ||
            IsUpgradingSpaceport)
        {
            return false;
        }

        switch (upgrade)
        {
            case FlagshipUpgrade.Drive:
                if (CurrentPlayer.FlagshipDriveCount >= GameRules.MaxBoostersPerPlayer) return false;
                break;
            case FlagshipUpgrade.Cannon:
                if (CurrentPlayer.FlagshipCannonCount >= GameRules.MaxCannonsPerPlayer) return false;
                break;
            case FlagshipUpgrade.CargoModule:
                if (CurrentPlayer.FlagshipCargoModuleCount >= GameRules.MaxFreightPodsPerPlayer) return false;
                break;
            default:
                return false;
        }

        IDictionary<ResourceType, int> cost = GameRules.GetUpgradeCost(upgrade);
        return CurrentPlayer.HasResources(cost);
    }

    public bool TryBuildUpgrade(FlagshipUpgrade upgrade)
    {
        if (!CanBuildUpgrade(upgrade))
        {
            IDictionary<ResourceType, int> upgradeCost = GameRules.GetUpgradeCost(upgrade);
            if (CurrentPlayer != null && !CurrentPlayer.HasResources(upgradeCost))
            {
                PlayerHandler.ShowToast(string.Format("Nicht genügend Rohstoffe für {0} ({1}).", UIStrings.GetUpgradeSingularName(upgrade), UIStrings.FormatCost(upgradeCost)), isWarning: true);
            }
            else
            {
                PlayerHandler.ShowToast(string.Format("Maximales Upgrade-Limit für {0} erreicht.", UIStrings.GetUpgradeSingularName(upgrade)), isWarning: true);
            }
            Debug.LogWarning("Cannot build upgrade: requirements not met or component limit reached.");
            return false;
        }

        IDictionary<ResourceType, int> cost = GameRules.GetUpgradeCost(upgrade);
        if (!CurrentPlayer.SpendResources(cost))
        {
            Debug.LogWarning("You do not have enough resources to build this upgrade.");
            return false;
        }

        int newTotal = 0;
        switch (upgrade)
        {
            case FlagshipUpgrade.Drive:
                CurrentPlayer.AddFlagshipDrive();
                newTotal = CurrentPlayer.FlagshipDriveCount;
                break;
            case FlagshipUpgrade.Cannon:
                CurrentPlayer.AddFlagshipCannon();
                newTotal = CurrentPlayer.FlagshipCannonCount;
                break;
            case FlagshipUpgrade.CargoModule:
                CurrentPlayer.AddFlagshipCargoModule();
                newTotal = CurrentPlayer.FlagshipCargoModuleCount;
                break;
        }

        Debug.Log(string.Format(
            UIStrings.StatusUpgradeBuilt.GetText(),
            CurrentPlayer.PlayerName,
            UIStrings.GetUpgradeSingularName(upgrade),
            newTotal));
        CheckSpecialPlanetsForPlayer(CurrentPlayer);
        return true;
    }

    // --- Spaceport Construction / Upgrade ---
    public IEnumerable<Colony> GetEligibleColoniesForSpaceportUpgrade(Player player)
    {
        if (player == null || player.Colonies == null)
        {
            return Enumerable.Empty<Colony>();
        }

        return player.Colonies
            .Where(colony => colony != null && !colony.IsSpaceport && colony.Tri != null);
    }

    public bool CanBuildSpaceport()
    {
        if (CurrentFoundingPhase != FoundingPhase.Complete ||
            CurrentPhase != GameTurnPhase.TradeAndBuild ||
            IsPlacingShip ||
            IsUpgradingSpaceport)
        {
            return false;
        }

        if (CurrentPlayer == null || CurrentPlayer.AvailableSpaceportsCount <= 0)
        {
            return false;
        }

        if (!CurrentPlayer.HasResources(GameRules.SpaceportCost))
        {
            return false;
        }

        return GetEligibleColoniesForSpaceportUpgrade(CurrentPlayer).Any();
    }

    public bool TryBeginSpaceportUpgrade()
    {
        if (CurrentFoundingPhase != FoundingPhase.Complete || CurrentPhase != GameTurnPhase.TradeAndBuild)
        {
            PlayerHandler.ShowToast("Raumhäfen können nur in der Handels- und Bauphase gebaut werden.", isWarning: true);
            return false;
        }

        if (IsPlacingShip)
        {
            return false;
        }

        if (IsUpgradingSpaceport)
        {
            return true;
        }

        if (CurrentPlayer == null)
        {
            return false;
        }

        if (CurrentPlayer.AvailableSpaceportsCount <= 0)
        {
            PlayerHandler.ShowToast($"Maximale Anzahl an Raumhäfen erreicht ({GameRules.MaxSpaceportsPerPlayer}).", isWarning: true);
            return false;
        }

        if (!CurrentPlayer.HasResources(GameRules.SpaceportCost))
        {
            PlayerHandler.ShowToast($"Nicht genügend Rohstoffe für einen Raumhafen ({UIStrings.FormatCost(GameRules.SpaceportCost)}).", isWarning: true);
            return false;
        }

        if (!GetEligibleColoniesForSpaceportUpgrade(CurrentPlayer).Any())
        {
            PlayerHandler.ShowToast("Du besitzt keine Kolonie, die zum Raumhafen ausgebaut werden kann.", isWarning: true);
            return false;
        }

        IsUpgradingSpaceport = true;
        UpdateSpaceportUpgradeMarkers();
        OnSpaceportUpgradeStateChanged?.Invoke();
        PlayerHandler.ShowToast("Wähle eine Kolonie aus, um sie zum Raumhafen auszubauen.");
        return true;
    }

    public bool TryUpgradeSpaceportAt(Tri tri)
    {
        if (!IsUpgradingSpaceport || tri == null || tri.Colony == null)
        {
            return false;
        }

        Colony colony = tri.Colony;
        bool isOwnedByCurrent = CurrentPlayer != null && CurrentPlayer.Colonies != null &&
            (System.Array.IndexOf(CurrentPlayer.Colonies, colony) >= 0 || CurrentPlayer.Colonies.Any(c => c == colony || (c.Tri != null && c.Tri == tri)));

        if (!isOwnedByCurrent || colony.IsSpaceport)
        {
            PlayerHandler.ShowToast("Wähle eine deiner Kolonien aus, die noch kein Raumhafen ist.", isWarning: true);
            Debug.LogWarning("Select one of your eligible colonies to upgrade to a spaceport.");
            return false;
        }

        if (CurrentPlayer.AvailableSpaceportsCount <= 0)
        {
            PlayerHandler.ShowToast($"Maximale Anzahl an Raumhäfen erreicht ({GameRules.MaxSpaceportsPerPlayer}).", isWarning: true);
            return false;
        }

        if (!CurrentPlayer.SpendResources(GameRules.SpaceportCost))
        {
            PlayerHandler.ShowToast($"Nicht genügend Rohstoffe für einen Raumhafen ({UIStrings.FormatCost(GameRules.SpaceportCost)}).", isWarning: true);
            Debug.LogWarning("You do not have enough resources to build a spaceport.");
            return false;
        }

        colony.UpgradeToSpaceport();
        MarkSpaceportVisual(colony);
        IsUpgradingSpaceport = false;
        ClearSpaceportUpgradeMarkers();
        CurrentPlayer.NotifyUpgradesChanged();
        OnSpaceportUpgradeStateChanged?.Invoke();
        NotifyVictoryPointsChanged();
        string toastMsg = string.Format(UIStrings.StatusSpaceportBuilt.GetText(), CurrentPlayer.PlayerName, tri.Q, tri.R);
        PlayerHandler.ShowToast(toastMsg);
        Debug.Log(toastMsg);
        return true;
    }

    public void CancelSpaceportUpgrade()
    {
        if (!IsUpgradingSpaceport)
        {
            return;
        }

        IsUpgradingSpaceport = false;
        ClearSpaceportUpgradeMarkers();
        OnSpaceportUpgradeStateChanged?.Invoke();
    }

    private void UpdateSpaceportUpgradeMarkers()
    {
        ClearSpaceportUpgradeMarkers();
        foreach (Colony colony in GetEligibleColoniesForSpaceportUpgrade(CurrentPlayer))
        {
            if (colony.Tri == null) continue;
            GameObject marker = CreateSpaceportPart(
                transform,
                PrimitiveType.Cylinder,
                "EligibleSpaceportColony",
                Vector3.zero,
                new Vector3(0.6f, 0.035f, 0.6f),
                new Color(1f, 0.85f, 0.2f, 0.8f));
            marker.transform.position = colony.Tri.PositionFromCamera() + Vector3.up * 0.12f;
            spaceportUpgradeMarkers.Add(marker);
        }
    }

    private void ClearSpaceportUpgradeMarkers()
    {
        foreach (GameObject marker in spaceportUpgradeMarkers)
        {
            if (marker != null)
            {
                Destroy(marker);
            }
        }
        spaceportUpgradeMarkers.Clear();
    }

    // --- Supply Trading (3:1 / Goods 2:1) ---
    public bool CanTradeWithSupply(ResourceType offered, ResourceType requested)
    {
        if (CurrentFoundingPhase != FoundingPhase.Complete ||
            CurrentPhase != GameTurnPhase.TradeAndBuild)
        {
            return false;
        }

        return CurrentPlayer.CanTradeWithSupply(offered, requested);
    }

    public bool TryTradeWithSupply(ResourceType offered, ResourceType requested)
    {
        if (!CanTradeWithSupply(offered, requested))
        {
            Debug.LogWarning("Cannot trade with supply: insufficient resources or invalid selection.");
            return false;
        }

        int rate = CurrentPlayer != null ? CurrentPlayer.GetSupplyTradeRate(offered) : GameRules.GetSupplyTradeRate(offered);
        if (!CurrentPlayer.ExecuteSupplyTrade(offered, requested))
        {
            return false;
        }

        Debug.Log(string.Format(
            UIStrings.StatusSupplyTradeExecuted.GetText(),
            CurrentPlayer.PlayerName,
            rate,
            UIStrings.GetResourceName(offered),
            UIStrings.GetResourceName(requested)));
        return true;
    }

    public bool TryBuyFame(Player player = null)
    {
        Player current = player ?? CurrentPlayer;
        if (current == null) return false;

        bool hasCard = current.HasFriendshipCard(FriendshipCardId.DiplomatBoughtFame1) ||
                       current.HasFriendshipCard(FriendshipCardId.DiplomatBoughtFame2);
        if (!hasCard || current.HasUsedBoughtFameThisTurn || current.GetResourceCount(ResourceType.TradeGoods) < 1)
        {
            return false;
        }

        current.RemoveResource(ResourceType.TradeGoods, 1);
        current.AddHalfMedal(1);
        current.HasUsedBoughtFameThisTurn = true;
        RuntimeLog.WriteLine(string.Format("[BuyFame] {0} bought 1 Fame Medal Piece for 1 Trade Goods (Total Half-Medals: {1}, VP: {2}).",
            current.PlayerName, current.HalfMedals, current.VictoryPoints));
        return true;
    }

    public bool TryRichHelpsPoor(Player player = null)
    {
        return TryRichHelpsPoor(out _, player);
    }

    public bool TryRichHelpsPoor(out string summary, Player player = null)
    {
        summary = null;
        Player current = player ?? CurrentPlayer;
        if (current == null || current.HasUsedRichHelpsPoorThisTurn) return false;

        bool hasCard = current.HasFriendshipCard(FriendshipCardId.DiplomatRichHelpsPoor);
        if (!hasCard) return false;

        List<Player> richerPlayers = Players != null
            ? Players.Where(p => p != current && p.VictoryPoints > current.VictoryPoints && p.TotalResources > 0).ToList()
            : new List<Player>();
        if (richerPlayers.Count == 0) return false;

        List<string> drawnEntries = new List<string>();
        int drawn = 0;
        foreach (Player richer in richerPlayers.Take(2))
        {
            ResourceType stolen = richer.DrawRandomResourceCard();
            if (stolen != ResourceType.None)
            {
                current.AddResource(stolen);
                drawn++;
                string cardDesc = "1 " + UIStrings.GetResourceName(stolen);
                drawnEntries.Add(string.Format(UIStrings.EncounterStealFromPlayerEntry.GetText(), cardDesc, richer.PlayerName));
                RuntimeLog.WriteLine(string.Format("[RichHelpsPoor] {0} drew 1 {1} from {2}.", current.PlayerName, stolen, richer.PlayerName));
            }
        }

        current.HasUsedRichHelpsPoorThisTurn = true;
        if (drawnEntries.Count > 0)
        {
            string list = string.Join(", ", drawnEntries);
            summary = string.Format(UIStrings.DiplomatRichHelpsPoorToast.GetText(), list);
        }
        return drawn > 0;
    }

    // --- Player Trading ---
    public bool CanTradeWithPlayer(
        Player targetPlayer,
        IDictionary<ResourceType, int> offered,
        IDictionary<ResourceType, int> requested)
    {
        if (CurrentFoundingPhase != FoundingPhase.Complete ||
            CurrentPhase != GameTurnPhase.TradeAndBuild ||
            targetPlayer == null ||
            targetPlayer == CurrentPlayer)
        {
            return false;
        }

        int offerCount = offered != null ? offered.Where(kv => GameRules.IsResource(kv.Key) && kv.Value > 0).Sum(kv => kv.Value) : 0;
        int reqCount = requested != null ? requested.Where(kv => GameRules.IsResource(kv.Key) && kv.Value > 0).Sum(kv => kv.Value) : 0;
        if (offerCount < 1 || reqCount < 1)
        {
            return false;
        }

        return CurrentPlayer.HasResources(offered) && targetPlayer.HasResources(requested);
    }

    public bool TryTradeWithPlayer(
        Player targetPlayer,
        IDictionary<ResourceType, int> offered,
        IDictionary<ResourceType, int> requested)
    {
        if (!CanTradeWithPlayer(targetPlayer, offered, requested))
        {
            Debug.LogWarning("Cannot execute player trade: conditions not satisfied.");
            return false;
        }

        if (targetPlayer.Type == Player.PlayerType.AI)
        {
            int offerTotal = offered.Where(kv => GameRules.IsResource(kv.Key)).Sum(kv => kv.Value);
            int reqTotal = requested.Where(kv => GameRules.IsResource(kv.Key)).Sum(kv => kv.Value);
            if (offerTotal < reqTotal)
            {
                Debug.Log(string.Format("{0} (AI) politely declined the trade proposal.", targetPlayer.PlayerName));
                return false;
            }
        }

        if (!Player.ExecutePlayerTrade(CurrentPlayer, offered, targetPlayer, requested))
        {
            return false;
        }

        Debug.Log(string.Format(
            UIStrings.StatusPlayerTradeExecuted.GetText(),
            CurrentPlayer.PlayerName,
            targetPlayer.PlayerName));
        return true;
    }

    private static IDictionary<ResourceType, int> GetShipCost(StarshipType shipType)
    {
        switch (shipType)
        {
            case StarshipType.ColonyShip:
                return GameRules.ColonyShipCost;
            case StarshipType.TradeShip:
                return GameRules.TradeShipCost;
            default:
                throw new System.ArgumentOutOfRangeException("shipType", shipType, "Unknown starship type.");
        }
    }

    public IEnumerable<Tri> GetFreeSpaceportPoints(Player player)
    {
        return player.Colonies
            .Where(colony => colony.IsSpaceport && colony.Tri != null)
            .SelectMany(GetFreePointsAtSpaceport)
            .Distinct()
            .Where(point => point.IsPassable && point.Units.Length == 0);
    }

    public IEnumerable<Tri> GetFreePointsAtSpaceport(Colony spaceport)
    {
        if (spaceport == null || spaceport.Tri == null)
        {
            return Enumerable.Empty<Tri>();
        }

        return spaceport.Tri.GetNeighbours()
            .Cast<Tri>()
            .Where(point => point != null && point.IsPassable && point.Units.Length == 0);
    }

    public void SelectStartingUpgrade(FlagshipUpgrade upgrade)
    {
        if (CurrentFoundingPhase != FoundingPhase.SelectFlagshipUpgrade)
        {
            throw new System.InvalidOperationException("A starting upgrade cannot be selected now.");
        }

        switch (upgrade)
        {
            case FlagshipUpgrade.Drive:
                if (RemainingStartingDrives == 0)
                {
                    throw new System.InvalidOperationException("No starting drives remain.");
                }
                RemainingStartingDrives--;
                CurrentPlayer.AddFlagshipDrive();
                break;
            case FlagshipUpgrade.Cannon:
                if (RemainingStartingCannons == 0)
                {
                    throw new System.InvalidOperationException("The starting cannon has already been taken.");
                }
                RemainingStartingCannons--;
                CurrentPlayer.AddFlagshipCannon();
                break;
            case FlagshipUpgrade.CargoModule:
                if (RemainingStartingCargoModules == 0)
                {
                    throw new System.InvalidOperationException("The starting cargo module has already been taken.");
                }
                RemainingStartingCargoModules--;
                CurrentPlayer.AddFlagshipCargoModule();
                break;
            default:
                throw new System.ArgumentOutOfRangeException("upgrade");
        }

        selectedFoundingSpaceport = null;
        AdvanceFoundingPlacement();
    }

    private static bool IsColonyPlacementPhase(FoundingPhase phase)
    {
        return phase == FoundingPhase.PlaceFirstColony ||
            phase == FoundingPhase.PlaceSecondColony ||
            phase == FoundingPhase.PlaceThirdColony;
    }

    private void BeginColonyPlacementRound(int round, int firstPlayerIndex, int direction)
    {
        FoundingRound = round;
        foundingPlayerOrder = CreatePlayerOrder(firstPlayerIndex, direction);
        foundingPlayerOrderIndex = 0;
        SetCurrentPlayerIndex(foundingPlayerOrder[0]);
        FoundingPhase phase = round == 1 ? FoundingPhase.PlaceFirstColony :
            round == 2 ? FoundingPhase.PlaceSecondColony : FoundingPhase.PlaceThirdColony;
        int currentRound = round;
        Player roundPlacer = CurrentPlayer;
        SetFoundingStatus(() => string.Format(
            UIStrings.StatusRoundNextPlacement.GetText(),
            currentRound,
            roundPlacer.PlayerName));
        SetFoundingPhase(phase);
    }

    private List<int> CreatePlayerOrder(int firstPlayerIndex, int direction)
    {
        List<int> order = new List<int>(Players.Length);
        for (int offset = 0; offset < Players.Length; offset++)
        {
            int playerIndex = (firstPlayerIndex + direction * offset) % Players.Length;
            if (playerIndex < 0)
            {
                playerIndex += Players.Length;
            }
            order.Add(playerIndex);
        }
        return order;
    }

    private void BeginSpaceportRound(int firstPlayerIndex)
    {
        FoundingRound = 4;
        foundingPlayerOrder = CreatePlayerOrder(firstPlayerIndex, -1);
        foundingPlayerOrderIndex = 0;
        SetCurrentPlayerIndex(foundingPlayerOrder[0]);
        Player round4Placer = CurrentPlayer;
        SetFoundingStatus(() => string.Format(
            UIStrings.StatusRoundSpaceportPrompt.GetText(),
            round4Placer.PlayerName));
        SetFoundingPhase(FoundingPhase.SelectSpaceport);
    }

    private void AdvanceFoundingPlacement()
    {
        foundingPlayerOrderIndex++;
        if (foundingPlayerOrderIndex < foundingPlayerOrder.Count)
        {
            SetCurrentPlayerIndex(foundingPlayerOrder[foundingPlayerOrderIndex]);
            FoundingPhase nextPhase = CurrentFoundingPhase;
            if (CurrentFoundingPhase == FoundingPhase.SelectFlagshipUpgrade)
            {
                nextPhase = FoundingPhase.SelectSpaceport;
                Player promptPlayer = CurrentPlayer;
                SetFoundingStatus(() => string.Format(
                    UIStrings.StatusSpaceportChoosePrompt.GetText(),
                    promptPlayer.PlayerName));
            }
            else
            {
                Player nextPlacer = CurrentPlayer;
                int currentRound = FoundingRound;
                SetFoundingStatus(() => string.Format(
                    UIStrings.StatusNextColonyPlacement.GetText(),
                    nextPlacer.PlayerName,
                    currentRound));
            }
            SetFoundingPhase(nextPhase);
            return;
        }

        if (CurrentFoundingPhase == FoundingPhase.PlaceFirstColony)
        {
            int lastPlayerIndex = foundingPlayerOrder[foundingPlayerOrder.Count - 1];
            BeginColonyPlacementRound(2, lastPlayerIndex, -1);
        }
        else if (CurrentFoundingPhase == FoundingPhase.PlaceSecondColony)
        {
            int lastPlayerIndex = foundingPlayerOrder[foundingPlayerOrder.Count - 1];
            BeginColonyPlacementRound(3, lastPlayerIndex, 1);
        }
        else if (CurrentFoundingPhase == FoundingPhase.PlaceThirdColony)
        {
            int lastPlayerIndex = foundingPlayerOrder[foundingPlayerOrder.Count - 1];
            BeginSpaceportRound(lastPlayerIndex);
        }
        else
        {
            CompleteFoundingPhase();
        }
    }

    private void CompleteFoundingPhase()
    {
        foreach (Player player in Players)
        {
            for (int card = 0; card < 3; card++)
            {
                player.AddResource(ReserveDeck.DrawCard());
            }
            player.AddHalfMedal();
        }

        SetCurrentPlayerIndex(startingPlayerIndex);
        FoundingRound = 0;
        SetFoundingStatus(() => UIStrings.StatusSetupComplete.GetText());
        SetFoundingPhase(FoundingPhase.Complete);
        TurnNumber = 1;
        BeginPlayerTurn();
    }

    public void RefreshFoundingMarkers()
    {
        if (CurrentFoundingPhase == FoundingPhase.SelectStartingShipPoint)
        {
            if (selectedFoundingSpaceport == null)
            {
                selectedFoundingSpaceport = CurrentPlayer?.Colonies?.FirstOrDefault(c => c != null && c.IsSpaceport);
            }
            IEnumerable<Tri> points = selectedFoundingSpaceport != null
                ? GetFreePointsAtSpaceport(selectedFoundingSpaceport)
                : GetFreeSpaceportPoints(CurrentPlayer);

            List<Tri> pointList = points?.Where(p => p != null && p.IsPassable && p.Units.Length == 0).ToList();
            if (pointList != null && pointList.Count > 0)
            {
                UpdateShipPlacementMarkers(pointList);
                bool isLocalHuman = CurrentPlayer != null && (CurrentPlayer.Type == Player.PlayerType.LOCAL ||
                    (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected &&
                     Networking.NetworkManager.Instance.LocalPlayerIndex == currentPlayerIndex &&
                     CurrentPlayer.Type != Player.PlayerType.AI));
                if (isLocalHuman && Camera.main != null && selectedFoundingSpaceport != null && selectedFoundingSpaceport.Tri != null)
                {
                    Camera.main.GetComponent<CameraMotion>()?.PanToTri(selectedFoundingSpaceport.Tri, onlyIfOffscreen: true);
                }
            }
            else
            {
                ClearShipPlacementMarkers();
            }
        }
        else if (CurrentFoundingPhase != FoundingPhase.SelectStartingShipPoint && !IsPlacingShip)
        {
            ClearShipPlacementMarkers();
        }
    }

    private void SetFoundingPhase(FoundingPhase phase)
    {
        CurrentFoundingPhase = phase;
        RefreshFoundingMarkers();
        OnFoundingPhaseChanged?.Invoke(CurrentPlayer, phase);
    }

    private void MarkSpaceportVisual(Colony colony)
    {
        if (colony == null) return;
        if (spaceportVisuals.TryGetValue(colony, out GameObject existingVisual) && existingVisual != null)
        {
            return;
        }

        GameObject colonyObject = null;
        if (colonyToGameObjectMap != null)
        {
            colonyToGameObjectMap.TryGetValue(colony, out colonyObject);
        }

        Vector3 pos = colonyObject != null ? colonyObject.transform.position : (colony.Tri != null ? colony.Tri.PositionFromCamera() : Vector3.zero);
        GameObject spaceport = new GameObject("SpaceportVisual");
        spaceport.transform.SetParent(transform, true);
        spaceport.transform.position = pos;
        spaceport.transform.rotation = Quaternion.identity;
        spaceportVisuals[colony] = spaceport;

        if (SpaceportPrefab != null)
        {
            GameObject upgradedModel = Instantiate(SpaceportPrefab, spaceport.transform);
            upgradedModel.transform.localPosition = Vector3.zero;
            upgradedModel.transform.localRotation = SpaceportPrefab.transform.rotation;
            Player owner = GetPlayerForColony(colony) ?? CurrentPlayer;
            ApplyPlayerMaterial(upgradedModel, owner?.Material, owner?.Material != null ? owner.Material.color : Color.white);
            if (colonyObject != null)
            {
                colonyObject.SetActive(false);
            }
            return;
        }

        Color structureColor = new Color(0.035f, 0.09f, 0.13f);
        Color accentColor = Color.cyan;

        CreateSpaceportPart(
            spaceport.transform,
            PrimitiveType.Cylinder,
            "LandingPlatform",
            new Vector3(0f, -0.04f, 0f),
            new Vector3(1.2f, 0.06f, 1.2f),
            structureColor);
        const int ringSegmentCount = 16;
        const float ringRadius = 0.52f;
        for (int i = 0; i < ringSegmentCount; i++)
        {
            float angle = i * Mathf.PI * 2f / ringSegmentCount;
            Color ringColor = i % 2 == 0 ? Color.cyan : new Color(0.7f, 1f, 1f);
            GameObject ringSegment = CreateSpaceportPart(
                spaceport.transform,
                PrimitiveType.Cube,
                "PlatformRingSegment",
                new Vector3(
                    Mathf.Sin(angle) * ringRadius,
                    0.045f,
                    Mathf.Cos(angle) * ringRadius),
                new Vector3(0.24f, 0.055f, 0.12f),
                ringColor);
            ringSegment.transform.localRotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
        }

        CreateSpaceportPart(
            spaceport.transform,
            PrimitiveType.Cube,
            "PylonLeft",
            new Vector3(-0.31f, 0.34f, 0f),
            new Vector3(0.12f, 0.68f, 0.14f),
            structureColor);
        CreateSpaceportPart(
            spaceport.transform,
            PrimitiveType.Cube,
            "PylonRight",
            new Vector3(0.31f, 0.34f, 0f),
            new Vector3(0.12f, 0.68f, 0.14f),
            structureColor);
        CreateSpaceportPart(
            spaceport.transform,
            PrimitiveType.Cube,
            "DockingGate",
            new Vector3(0f, 0.68f, 0f),
            new Vector3(0.78f, 0.12f, 0.16f),
            accentColor);
        CreateSpaceportPart(
            spaceport.transform,
            PrimitiveType.Sphere,
            "Beacon",
            new Vector3(0f, 0.86f, 0f),
            new Vector3(0.23f, 0.23f, 0.23f),
            Color.cyan);
    }

    private static GameObject CreateSpaceportPart(
        Transform parent,
        PrimitiveType primitiveType,
        string partName,
        Vector3 localPosition,
        Vector3 localScale,
        Color color)
    {
        GameObject part = GameObject.CreatePrimitive(primitiveType);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = localScale;

        Renderer renderer = part.GetComponent<Renderer>();
        renderer.material.color = color;
        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
        return part;
    }

    private void SpawnShipAt(Unit unit, Tri tri)
    {
        if (unitToGameObjectMap == null)
        {
            unitToGameObjectMap = new Dictionary<Unit, GameObject>();
        }

        GameObject shipPrefab = unit.ShipType == StarshipType.ColonyShip ? ColonyShipPrefab : TradeShipPrefab;
        Player owner = GetPlayerForUnit(unit) ?? CurrentPlayer;
        Color hullColor = owner?.Material != null
            ? owner.Material.color
            : Color.white;
        Color detailColor = unit.ShipType == StarshipType.ColonyShip
            ? new Color(0.2f, 0.9f, 1f)
            : new Color(1f, 0.72f, 0.15f);

        Vector3 spawnPos = GetShipHoverPosition(tri);
        GameObject shipObject;
        if (shipPrefab != null)
        {
            shipObject = Instantiate(
                shipPrefab,
                spawnPos,
                shipPrefab.transform.rotation,
                transform);
            shipObject.transform.position = spawnPos;
            shipObject.name = unit.ShipType == StarshipType.ColonyShip ? "ColonyShip" : "TradeShip";
            ApplyPlayerMaterial(shipObject, owner?.Material, hullColor);
        }
        else
        {
            shipObject = new GameObject(
                unit.ShipType == StarshipType.ColonyShip ? "ColonyShip" : "TradeShip");
            shipObject.transform.SetParent(transform, false);
            shipObject.transform.position = spawnPos;

            CreateShipPart(
                shipObject.transform,
                PrimitiveType.Capsule,
                "Hull",
                new Vector3(0f, 0.12f, 0f),
                new Vector3(0.19f, unit.ShipType == StarshipType.ColonyShip ? 0.37f : 0.34f, 0.15f),
                hullColor,
                Quaternion.Euler(90f, 0f, 0f));

            if (unit.ShipType == StarshipType.ColonyShip)
        {
            CreateShipPart(
                shipObject.transform,
                PrimitiveType.Cube,
                "LeftWing",
                new Vector3(-0.2f, 0.1f, -0.02f),
                new Vector3(0.2f, 0.055f, 0.17f),
                hullColor,
                Quaternion.identity);
            CreateShipPart(
                shipObject.transform,
                PrimitiveType.Cube,
                "RightWing",
                new Vector3(0.2f, 0.1f, -0.02f),
                new Vector3(0.2f, 0.055f, 0.17f),
                hullColor,
                Quaternion.identity);
            CreateShipPart(
                shipObject.transform,
                PrimitiveType.Sphere,
                "Cockpit",
                new Vector3(0f, 0.23f, 0.16f),
                new Vector3(0.15f, 0.12f, 0.16f),
                detailColor,
                Quaternion.identity);
            CreateShipPart(
                shipObject.transform,
                PrimitiveType.Cylinder,
                "Engine",
                new Vector3(0f, 0.08f, -0.24f),
                new Vector3(0.09f, 0.1f, 0.09f),
                detailColor,
                Quaternion.identity);
        }
        else
        {
            // Structural pylons connecting central hull to the side pods in player color
            CreateShipPart(
                shipObject.transform,
                PrimitiveType.Cube,
                "PylonLeft",
                new Vector3(-0.08f, 0.16f, 0.01f),
                new Vector3(0.09f, 0.05f, 0.16f),
                hullColor,
                Quaternion.identity);
            CreateShipPart(
                shipObject.transform,
                PrimitiveType.Cube,
                "PylonRight",
                new Vector3(0.08f, 0.16f, 0.01f),
                new Vector3(0.09f, 0.05f, 0.16f),
                hullColor,
                Quaternion.identity);

            // Left and Right cargo pods prominently in player color
            CreateShipPart(
                shipObject.transform,
                PrimitiveType.Cube,
                "CargoPodLeft",
                new Vector3(-0.14f, 0.22f, 0.01f),
                new Vector3(0.16f, 0.18f, 0.24f),
                hullColor,
                Quaternion.identity);
            CreateShipPart(
                shipObject.transform,
                PrimitiveType.Cube,
                "CargoPodRight",
                new Vector3(0.14f, 0.22f, 0.01f),
                new Vector3(0.16f, 0.18f, 0.24f),
                hullColor,
                Quaternion.identity);

            // Forward bridge cabin in player color
            CreateShipPart(
                shipObject.transform,
                PrimitiveType.Sphere,
                "Bridge",
                new Vector3(0f, 0.22f, 0.18f),
                new Vector3(0.13f, 0.12f, 0.13f),
                hullColor,
                Quaternion.identity);

            // Bridge sensor visor
            CreateShipPart(
                shipObject.transform,
                PrimitiveType.Sphere,
                "CockpitVisor",
                new Vector3(0f, 0.23f, 0.23f),
                new Vector3(0.08f, 0.06f, 0.06f),
                new Color(0.2f, 0.9f, 1f),
                Quaternion.identity);

            // Trade accents: cargo hatches on top of each pod
            CreateShipPart(
                shipObject.transform,
                PrimitiveType.Cube,
                "CargoHatchLeft",
                new Vector3(-0.14f, 0.315f, 0.01f),
                new Vector3(0.11f, 0.035f, 0.17f),
                detailColor,
                Quaternion.identity);
            CreateShipPart(
                shipObject.transform,
                PrimitiveType.Cube,
                "CargoHatchRight",
                new Vector3(0.14f, 0.315f, 0.01f),
                new Vector3(0.11f, 0.035f, 0.17f),
                detailColor,
                Quaternion.identity);

            // Central trade station docking collar between the pods
            CreateShipPart(
                shipObject.transform,
                PrimitiveType.Cylinder,
                "TradeDock",
                new Vector3(0f, 0.24f, -0.02f),
                new Vector3(0.13f, 0.05f, 0.13f),
                detailColor,
                Quaternion.identity);
            CreateShipPart(
                shipObject.transform,
                PrimitiveType.Cylinder,
                "DockAntenna",
                new Vector3(0f, 0.29f, -0.02f),
                new Vector3(0.03f, 0.07f, 0.03f),
                Color.white,
                Quaternion.identity);

            // Twin heavy freighter engines at rear of pods
            CreateShipPart(
                shipObject.transform,
                PrimitiveType.Cylinder,
                "EngineLeft",
                new Vector3(-0.14f, 0.19f, -0.13f),
                new Vector3(0.08f, 0.08f, 0.08f),
                new Color(0.24f, 0.27f, 0.32f),
                Quaternion.Euler(90f, 0f, 0f));
            CreateShipPart(
                shipObject.transform,
                PrimitiveType.Cylinder,
                "EngineRight",
                new Vector3(0.14f, 0.19f, -0.13f),
                new Vector3(0.08f, 0.08f, 0.08f),
                new Color(0.24f, 0.27f, 0.32f),
                Quaternion.Euler(90f, 0f, 0f));
            CreateShipPart(
                shipObject.transform,
                PrimitiveType.Cylinder,
                "ExhaustLeft",
                new Vector3(-0.14f, 0.19f, -0.175f),
                new Vector3(0.05f, 0.015f, 0.05f),
                detailColor,
                Quaternion.Euler(90f, 0f, 0f));
            CreateShipPart(
                shipObject.transform,
                PrimitiveType.Cylinder,
                "ExhaustRight",
                new Vector3(0.14f, 0.19f, -0.175f),
                new Vector3(0.05f, 0.015f, 0.05f),
                detailColor,
                Quaternion.Euler(90f, 0f, 0f));
        }
    }

        UnitView unitView = shipObject.AddComponent<UnitView>();
        unitView.TriMap = this;
        unitView.InitializePosition(spawnPos);
        unit.SetTri(tri);
        unit.OnObjectMoved += unitView.OnUnitMoved;
        unit.OnObjectMoved += (oldTri, newTri) => OnUnitMovedForPlanetDiscoveryWithUnit(unit, oldTri, newTri);
        if (owner != null && !owner.Units.Contains(unit))
        {
            owner.AddUnit(unit);
        }
        else if (owner == null)
        {
            CurrentPlayer.AddUnit(unit);
        }
        unit.OnObjectDestroyed += OnUnitDestroyed;
        unitToGameObjectMap.Add(unit, shipObject);
    }

    private static GameObject CreateShipPart(
        Transform parent,
        PrimitiveType primitiveType,
        string partName,
        Vector3 localPosition,
        Vector3 localScale,
        Color color,
        Quaternion localRotation)
    {
        GameObject part = CreateSpaceportPart(
            parent,
            primitiveType,
            partName,
            localPosition,
            localScale,
            color);
        part.transform.localRotation = localRotation;
        return part;
    }

    private void BeginPlayerTurn()
    {
        LastProductionRoll = null;
        LastFlagshipRoll = null;
        CurrentEncounterCard = null;
        EncounterReader = null;
        SelectedEncounterOutcome = null;
        SetEncounterPending(false);
        CancelSpaceJump();
        if (CurrentPlayer != null)
        {
            CurrentPlayer.HasUsedBoughtFameThisTurn = false;
            CurrentPlayer.HasUsedRichHelpsPoorThisTurn = false;
            CurrentPlayer.HasUsedGoods11TradeThisTurn = false;
        }
        foreach(Unit unit in CurrentPlayer.Units)
        {
            unit.RefreshMovement();
        }

        SetTurnPhase(GameTurnPhase.Production);

        // If player is AI, automatically roll immediately
        if (CurrentPlayer != null && CurrentPlayer.Type == Player.PlayerType.AI)
        {
            RollProductionDice();
        }
    }

    public void RollProductionDice()
    {
        if (CurrentPhase != GameTurnPhase.Production || LastProductionRoll != null)
        {
            return;
        }

        int firstDie = UnityEngine.Random.Range(1, 7);
        int secondDie = UnityEngine.Random.Range(1, 7);
        LastProductionRoll = new ProductionRollResult(firstDie, secondDie);
        DistributePlanetProduction(LastProductionRoll.Total);
        Debug.Log(string.Format("{0} rolled {1} ({2} + {3}) for production.",
            CurrentPlayer.PlayerName, LastProductionRoll.Total, firstDie, secondDie));
        OnProductionRoll?.Invoke(CurrentPlayer, LastProductionRoll);
    }

    private void DistributePlanetProduction(int productionNumber)
    {
        if (productionNumber == 7)
        {
            RuntimeLog.WriteLine(
                "Production roll was 7; no colonies or spaceports receive planet resources.");
            IsResolvingSevenRoll = true;
            if (OnSevenRollResolutionNeeded != null)
            {
                OnSevenRollResolutionNeeded.Invoke(() =>
                {
                    IsResolvingSevenRoll = false;
                    AdvanceTurnPhase();
                    AgentBridge.ExternalAgentBridge.Instance?.MarkStateDirty();
                });
            }
            else
            {
                IsResolvingSevenRoll = false;
                AdvanceTurnPhase();
                AgentBridge.ExternalAgentBridge.Instance?.MarkStateDirty();
            }
            return;
        }

        Dictionary<Player, HashSet<ResourceType>> producedTypesPerPlayer = new Dictionary<Player, HashSet<ResourceType>>();
        int totalCardsDistributed = 0;
        foreach (Player player in Players)
        {
            producedTypesPerPlayer[player] = new HashSet<ResourceType>();
            foreach (Colony colony in player.Colonies)
            {
                if (colony.Tri == null)
                {
                    throw new System.InvalidOperationException(
                        "Cannot produce resources for a colony without a board position.");
                }

                foreach (Tri planetSystem in colony.Tri.GetAllNeighbours()
                    .Where(tri => tri.SystemKind == SystemKind.Planets))
                {
                    ResourceType[] resources = planetSystem.PlanetResources;
                    int[] productionNumbers = planetSystem.PlanetProductionNumbers;
                    if (resources.Length != 3 || productionNumbers.Length != 3)
                    {
                        throw new System.InvalidOperationException(
                            "Planet system " + planetSystem +
                            " must have three resources and three production numbers.");
                    }

                    for (int planet = 0; planet < resources.Length; planet++)
                    {
                        if (productionNumbers[planet] == productionNumber)
                        {
                            if (planetSystem.HasUnresolvedHazard(planet))
                            {
                                continue;
                            }
                            player.AddResource(resources[planet]);
                            producedTypesPerPlayer[player].Add(resources[planet]);
                            totalCardsDistributed++;
                            RuntimeLog.WriteLine(string.Format(
                                "Production: {0} receives 1 {1} from planet system {2} ({3}).",
                                player.PlayerName,
                                resources[planet],
                                planetSystem,
                                colony.IsSpaceport ? "spaceport" : "colony"));
                        }
                    }
                }
            }

            // --- Cultivators (Terra-Symbionten von Veda) Friendship Card Effect: +1 resource if at least 1 produced ---
            if (producedTypesPerPlayer[player].Contains(ResourceType.Ore) && player.HasFriendshipCard(FriendshipCardId.CultivatorProductionOre))
            {
                player.AddResource(ResourceType.Ore);
                totalCardsDistributed++;
                RuntimeLog.WriteLine(string.Format("Friendship Card Bonus: {0} receives +1 Ore from Cultivator bonus.", player.PlayerName));
            }
            if (producedTypesPerPlayer[player].Contains(ResourceType.Fuel) && player.HasFriendshipCard(FriendshipCardId.CultivatorProductionFuel))
            {
                player.AddResource(ResourceType.Fuel);
                totalCardsDistributed++;
                RuntimeLog.WriteLine(string.Format("Friendship Card Bonus: {0} receives +1 Fuel from Cultivator bonus.", player.PlayerName));
            }
            if (producedTypesPerPlayer[player].Contains(ResourceType.Carbon) && player.HasFriendshipCard(FriendshipCardId.CultivatorProductionCarbon))
            {
                player.AddResource(ResourceType.Carbon);
                totalCardsDistributed++;
                RuntimeLog.WriteLine(string.Format("Friendship Card Bonus: {0} receives +1 Carbon from Cultivator bonus.", player.PlayerName));
            }
            if (producedTypesPerPlayer[player].Contains(ResourceType.Food) && player.HasFriendshipCard(FriendshipCardId.CultivatorProductionFood))
            {
                player.AddResource(ResourceType.Food);
                totalCardsDistributed++;
                RuntimeLog.WriteLine(string.Format("Friendship Card Bonus: {0} receives +1 Food from Cultivator bonus.", player.PlayerName));
            }
            if (producedTypesPerPlayer[player].Contains(ResourceType.TradeGoods) && player.HasFriendshipCard(FriendshipCardId.CultivatorProductionGoods))
            {
                player.AddResource(ResourceType.TradeGoods);
                totalCardsDistributed++;
                RuntimeLog.WriteLine(string.Format("Friendship Card Bonus: {0} receives +1 Trade Goods from Cultivator bonus.", player.PlayerName));
            }

            // --- Diplomats (Diplomaten) Friendship Card Effect: Galaktische Hilfe (0=1) ---
            // If the player received 0 resources from this production roll and holds Galaktische Hilfe, grant 1 resource
            if (producedTypesPerPlayer[player].Count == 0 && player.HasFriendshipCard(FriendshipCardId.DiplomatGalacticAid))
            {
                // For AI, pick Fuel or Ore; for Human, give Fuel/Ore or show toast
                ResourceType aidRes = ResourceType.Fuel;
                player.AddResource(aidRes);
                totalCardsDistributed++;
                string aidMsg = string.Format(UIStrings.GalacticAidNotification.GetText(), UIStrings.GetResourceName(aidRes));
                RuntimeLog.WriteLine(string.Format("[GalacticAid] {0}: {1}", player.PlayerName, aidMsg));
                if (player == CurrentPlayer)
                {
                    PlayerHandler.ShowToast(aidMsg, isWarning: false, duration: 4.5f);
                }
            }
        }

        RuntimeLog.WriteLine(string.Format(
            "Production roll {0} distributed {1} resource card(s) to all players.",
            productionNumber,
            totalCardsDistributed));

        DistributeActivePlayerReserveCards();
    }

    public void DistributeActivePlayerReserveCards()
    {
        if (CurrentPlayer == null) return;
        int vp = CurrentPlayer.VictoryPoints;
        int count = (vp <= 7) ? 2 : ((vp <= 9) ? 1 : 0);
        if (count > 0)
        {
            List<string> drawn = new List<string>();
            for (int i = 0; i < count; i++)
            {
                ResourceType res = ReserveDeck.DrawCard();
                CurrentPlayer.AddResource(res, 1);
                drawn.Add(UIStrings.GetResourceName(res));
            }
            string msg = string.Format(UIStrings.SevenRollActiveReserveToast.GetText(),
                CurrentPlayer.PlayerName, count, vp) + " [" + string.Join(", ", drawn) + "]";
            RuntimeLog.WriteLine("[ReservePile] " + msg);
            PlayerHandler.ShowToast(msg, isWarning: false, duration: 4.0f);
        }
    }

    private void BeginFlightPhase()
    {
        LastFlagshipRoll = null;
        SetTurnPhase(GameTurnPhase.Flight);

        // If player is AI, automatically roll immediately
        if (CurrentPlayer != null && CurrentPlayer.Type == Player.PlayerType.AI)
        {
            RollFlightImpulse();
        }
    }

    public void RollFlightImpulse()
    {
        if (CurrentPhase != GameTurnPhase.Flight || LastFlagshipRoll != null)
        {
            return;
        }

        LastFlagshipRoll = RollFlagship(CurrentPlayer.EffectiveDriveCount);
        if (LastFlagshipRoll.RequiresEncounter)
        {
            EncounterReader = GetLeftNeighbor(CurrentPlayer);
            CurrentEncounterCard = encounterDeck.DrawNext();
            BeginEncounterCard();
            OnEncounterCardDrawn?.Invoke(EncounterReader, CurrentEncounterCard);
        }
        SetEncounterPending(LastFlagshipRoll.RequiresEncounter);
        foreach (Unit unit in CurrentPlayer.Units)
        {
            unit.SetMovementAllowance(LastFlagshipRoll.Speed);
        }

        string flightSummary = string.Format(
            "{0} rolled {1} + {2}: base speed {3}, {4} drive(s), movement speed {5}{6}.",
            CurrentPlayer.PlayerName,
            LastFlagshipRoll.FirstCore,
            LastFlagshipRoll.SecondCore,
            LastFlagshipRoll.BaseSpeed,
            LastFlagshipRoll.DriveCount,
            LastFlagshipRoll.Speed,
            LastFlagshipRoll.RequiresEncounter ? "; encounter required before movement" : string.Empty);
        RuntimeLog.WriteLine(flightSummary);
        Debug.Log(flightSummary);
        OnFlagshipRoll?.Invoke(CurrentPlayer, LastFlagshipRoll);
    }

    public bool TriggerDebugEncounter(int cardNumber = 26)
    {
        bool isDebugAllowed = Application.isEditor || Debug.isDebugBuild ||
                              (DebugSettings != null && DebugSettings.EnableKeyboardShortcuts) ||
                              System.Array.Exists(System.Environment.GetCommandLineArgs(), a => a.Equals("-debug", System.StringComparison.OrdinalIgnoreCase) || a.Equals("-test", System.StringComparison.OrdinalIgnoreCase));

        if (!isDebugAllowed)
        {
            Debug.LogWarning("[TriMap] TriggerDebugEncounter rejected: Debug actions are disabled in release mode without debug flags.");
            return false;
        }

        if (LastFlagshipRoll == null && CurrentPlayer != null)
        {
            LastFlagshipRoll = new FlagshipRollResult(FlightImpulseCore.Black, FlightImpulseCore.Blue, CurrentPlayer.EffectiveDriveCount);
            if (CurrentPlayer.Units != null)
            {
                foreach (Unit unit in CurrentPlayer.Units)
                {
                    unit.SetMovementAllowance(LastFlagshipRoll.Speed);
                }
            }
        }

        if (EncounterCardCatalog.Cards != null)
        {
            CurrentEncounterCard = EncounterCardCatalog.Cards.FirstOrDefault(c => c.Number == cardNumber);
        }
        if (CurrentEncounterCard == null && encounterDeck != null)
        {
            CurrentEncounterCard = encounterDeck.DrawNext();
        }
        if (CurrentEncounterCard != null)
        {
            encounterDeck?.ForceCurrentCard(CurrentEncounterCard);
            EncounterReader = GetLeftNeighbor(CurrentPlayer);
            BeginEncounterCard();
            SetEncounterPending(true);
            OnEncounterCardDrawn?.Invoke(EncounterReader, CurrentEncounterCard);
            OnEncounterStateChanged?.Invoke(true);
            return true;
        }

        return false;
    }

    public void AdvanceEncounterAfterPlayerResponse()
    {
        if (CurrentPhase != GameTurnPhase.Flight ||
            !EncounterPending ||
            CurrentEncounterCard == null ||
            CurrentEncounterCard.RefreshesEncounterDeck ||
            CurrentEncounterPhase != EncounterCardPhase.ReadPrompt)
        {
            throw new System.InvalidOperationException(
                "The current encounter card is not waiting for the player's response.");
        }

        CurrentEncounterPhase = EncounterCardPhase.SelectOutcome;
        OnEncounterProgressChanged?.Invoke(CurrentEncounterPhase, null);
    }

    public int PendingEncounterDonationAmount { get; set; } = 0;
    public Dictionary<ResourceType, int> PendingEncounterDonatedResources { get; set; } = new Dictionary<ResourceType, int>();
    public Dictionary<ResourceType, int> PendingEncounterRewardResources { get; set; } = new Dictionary<ResourceType, int>();
    public bool SkipNextOutcomeResourceDeduction { get; set; }

    /// <summary>Set once the player has already removed the upgrade of a "lose upgrade" outcome via dialog.</summary>
    public bool SkipNextOutcomeUpgradeLoss { get; set; }

    /// <summary>Set once the player has already selected the upgrade of a "gain upgrade" outcome via dialog.</summary>
    public bool SkipNextOutcomeUpgradeGain { get; set; }

    /// <summary>Summary of cards drawn from opponents during the last encounter outcome (e.g. StealAll:1).</summary>
    public string LastEncounterStealSummary { get; private set; }

    /// <summary>Target ship coordinates chosen by the active player for BlockOneShip outcome.</summary>
    public Vector2Int? PendingBlockedShipCoord { get; set; }

    /// <summary>Target ship coordinates chosen by the active player for SpaceJump outcome.</summary>
    public Vector2Int? PendingSpaceJumpShipCoord { get; set; }

    /// <summary>Ship currently executing a Space Jump bonus, awaiting destination selection.</summary>
    public Unit PendingSpaceJumpShip { get; private set; }
    public bool IsExecutingSpaceJump => PendingSpaceJumpShip != null;
    public event System.Action OnSpaceJumpStateChanged;

    public void SelectEncounterOutcome(int outcomeIndex)
    {
        if (CurrentPhase != GameTurnPhase.Flight ||
            !EncounterPending ||
            CurrentEncounterCard == null ||
            CurrentEncounterPhase != EncounterCardPhase.SelectOutcome)
        {
            throw new System.InvalidOperationException(
                "The current encounter card is not waiting for an outcome.");
        }
        if (outcomeIndex < 0 || outcomeIndex >= CurrentEncounterCard.Outcomes.Count)
        {
            throw new System.ArgumentOutOfRangeException("outcomeIndex");
        }

        SelectedEncounterOutcome = CurrentEncounterCard.Outcomes[outcomeIndex];
        ApplyEncounterOutcomeEffects(CurrentEncounterCard, SelectedEncounterOutcome);
        CurrentEncounterPhase = EncounterCardPhase.ApplyOutcome;
        OnEncounterProgressChanged?.Invoke(CurrentEncounterPhase, SelectedEncounterOutcome);
    }

    public void ApplyEncounterOutcomeEffects(EncounterCard card, EncounterCardOutcome outcome)
    {
        if (card == null || outcome == null || outcome.Effect == null) return;
        EncounterOutcomeEffect effect = outcome.Effect;
        Player player = CurrentPlayer;

        // 1. Half Medals
        if (effect.HalfMedals > 0)
        {
            for (int i = 0; i < effect.HalfMedals; i++) player.AddHalfMedal();
        }
        else if (effect.HalfMedals < 0)
        {
            player.RemoveHalfMedal(-effect.HalfMedals);
        }

        // 2. Resource Costs
        if (!string.IsNullOrEmpty(effect.ResourceCost))
        {
            if (SkipNextOutcomeResourceDeduction)
            {
                SkipNextOutcomeResourceDeduction = false;
            }
            else if (effect.ResourceCost == "Donation")
            {
                if (PendingEncounterDonatedResources != null && PendingEncounterDonatedResources.Count > 0)
                {
                    var positiveSpend = new Dictionary<ResourceType, int>();
                    foreach (var kvp in PendingEncounterDonatedResources)
                    {
                        int toTake = Mathf.Min(kvp.Value, player.GetResourceCount(kvp.Key));
                        if (toTake > 0)
                        {
                            positiveSpend[kvp.Key] = toTake;
                        }
                    }
                    if (positiveSpend.Count > 0)
                    {
                        player.SpendResources(positiveSpend);
                    }
                }
                else if (PendingEncounterDonationAmount > 0)
                {
                    DeductAnyResources(player, PendingEncounterDonationAmount);
                }
            }
            else if (effect.ResourceCost.StartsWith("Choice:"))
            {
                if (int.TryParse(effect.ResourceCost.Substring(7), out int amt) && amt > 0)
                {
                    DeductAnyResources(player, amt);
                }
            }
        }

        // 3. Resource Rewards
        if (!string.IsNullOrEmpty(effect.ResourceReward))
        {
            if (effect.ResourceReward == "DonationRefund")
            {
                // Refund exact donated resources if available, else refund Ore
                if (PendingEncounterDonatedResources != null && PendingEncounterDonatedResources.Count > 0)
                {
                    foreach (var kvp in PendingEncounterDonatedResources)
                    {
                        if (kvp.Value > 0)
                        {
                            player.AddResource(kvp.Key, kvp.Value);
                        }
                    }
                }
                else if (PendingEncounterDonationAmount > 0)
                {
                    player.AddResource(ResourceType.Ore, PendingEncounterDonationAmount);
                }
            }
            else if (effect.ResourceReward.StartsWith("Choice:"))
            {
                if (int.TryParse(effect.ResourceReward.Substring(7), out int amt) && amt > 0)
                {
                    int chosenTotal = PendingEncounterRewardResources != null ? PendingEncounterRewardResources.Values.Sum() : 0;
                    if (chosenTotal == amt)
                    {
                        foreach (var kvp in PendingEncounterRewardResources)
                        {
                            if (kvp.Value > 0)
                            {
                                player.AddResource(kvp.Key, kvp.Value);
                            }
                        }
                    }
                    else
                    {
                        ResourceType[] resChoices = { ResourceType.Ore, ResourceType.Fuel, ResourceType.Carbon, ResourceType.Food, ResourceType.TradeGoods };
                        for (int i = 0; i < amt; i++)
                        {
                            player.AddResource(resChoices[UnityEngine.Random.Range(0, resChoices.Length)], 1);
                        }
                    }
                }
            }
            else if (effect.ResourceReward.StartsWith("StealAll:"))
            {
                if (int.TryParse(effect.ResourceReward.Substring(9), out int stealAmt) && stealAmt > 0)
                {
                    List<string> stolenFromOpponents = new List<string>();
                    List<string> emptyOpponents = new List<string>();

                    if (Players != null)
                    {
                        foreach (Player other in Players)
                        {
                            if (other != player)
                            {
                                List<ResourceType> cardsFromOther = new List<ResourceType>();
                                for (int i = 0; i < stealAmt; i++)
                                {
                                    ResourceType stolen = other.DrawRandomResourceCard();
                                    if (stolen != ResourceType.None)
                                    {
                                        player.AddResource(stolen, 1);
                                        cardsFromOther.Add(stolen);
                                    }
                                }

                                if (cardsFromOther.Count > 0)
                                {
                                    var grouped = cardsFromOther
                                        .GroupBy(r => r)
                                        .Select(g => g.Count() + " " + UIStrings.GetResourceName(g.Key));
                                    string cardsStr = string.Join(", ", grouped);
                                    stolenFromOpponents.Add(string.Format(UIStrings.EncounterStealFromPlayerEntry.GetText(), cardsStr, other.PlayerName));
                                }
                                else
                                {
                                    emptyOpponents.Add(other.PlayerName);
                                }
                            }
                        }
                    }

                    string summary;
                    if (stolenFromOpponents.Count > 0)
                    {
                        string stolenList = string.Join(", ", stolenFromOpponents);
                        if (emptyOpponents.Count > 0)
                        {
                            string emptyList = string.Join(", ", emptyOpponents);
                            summary = string.Format(UIStrings.EncounterStealAllPartialToast.GetText(), stolenList, emptyList);
                        }
                        else
                        {
                            summary = string.Format(UIStrings.EncounterStealAllSuccessToast.GetText(), stolenList);
                        }
                    }
                    else
                    {
                        summary = UIStrings.EncounterStealAllNoneToast.GetText();
                    }

                    LastEncounterStealSummary = summary;
                    RuntimeLog.WriteLine("[Encounter] " + summary);
                    PlayerHandler.ShowToast(summary, isWarning: false, duration: 5.0f);
                }
            }
            else
            {
                string[] parts = effect.ResourceReward.Split(':');
                if (parts.Length == 2 && System.Enum.TryParse(parts[0], out ResourceType rType) && int.TryParse(parts[1], out int amt) && amt > 0)
                {
                    player.AddResource(rType, amt);
                }
            }
        }

        // 4. Block One Ship for current flight phase
        if (effect.BlockOneShip)
        {
            Unit shipToBlock = null;
            if (PendingBlockedShipCoord.HasValue)
            {
                shipToBlock = player.Units?.FirstOrDefault(u => u.MovementRemaining > 0 &&
                    u.Tri != null && u.Tri.Q == PendingBlockedShipCoord.Value.x && u.Tri.R == PendingBlockedShipCoord.Value.y);
            }
            if (shipToBlock == null)
            {
                shipToBlock = player.Units?.FirstOrDefault(u => u.MovementRemaining > 0);
            }
            if (shipToBlock != null)
            {
                shipToBlock.SetMovementAllowance(0);
                string blockMsg = string.Format(UIStrings.EncounterShipBlockedToast.GetText(), shipToBlock.Tri.Q, shipToBlock.Tri.R);
                PlayerHandler.ShowToast(blockMsg, isWarning: true);
            }
            PendingBlockedShipCoord = null;
        }

        // 5. Flagship Upgrades
        if (effect.LoseUpgrade)
        {
            if (SkipNextOutcomeUpgradeLoss)
            {
                SkipNextOutcomeUpgradeLoss = false;
            }
            else
            {
                player.RemoveRandomUpgrade();
            }
        }
        if (effect.GainUpgrade)
        {
            if (SkipNextOutcomeUpgradeGain)
            {
                SkipNextOutcomeUpgradeGain = false;
            }
            else
            {
                // Fallback (e.g. if resolved outside controller)
                FlagshipUpgrade upgrade;
                if (player.AvailableCannonsCount > 0) upgrade = FlagshipUpgrade.Cannon;
                else if (player.AvailableBoostersCount > 0) upgrade = FlagshipUpgrade.Drive;
                else if (player.AvailableFreightPodsCount > 0) upgrade = FlagshipUpgrade.CargoModule;
                else upgrade = (FlagshipUpgrade)(-1);

                if ((int)upgrade >= 0)
                {
                    if (upgrade == FlagshipUpgrade.Cannon) player.AddFlagshipCannon();
                    else if (upgrade == FlagshipUpgrade.Drive) player.AddFlagshipDrive();
                    else if (upgrade == FlagshipUpgrade.CargoModule) player.AddFlagshipCargoModule();

                    string toast = string.Format(UIStrings.EncounterGainUpgradeToast.GetText(),
                        player.PlayerName, UIStrings.GetUpgradeSingularName(upgrade));
                    PlayerHandler.ShowToast(toast, isWarning: false);
                }
            }
        }

        // 6. Free Trade Ship
        if (effect.GainTradeShip)
        {
            var freeLaunchPoints = GetFreeSpaceportPoints(player).ToList();
            if (player.AvailableTradeStationsCount > 0 && player.AvailableTransportShips > 0 && freeLaunchPoints.Count > 0)
            {
                Unit freeShip = new Unit { ShipType = StarshipType.TradeShip };
                SpawnShipAt(freeShip, freeLaunchPoints[0]);
                freeShip.SetMovementAllowance(LastFlagshipRoll != null ? LastFlagshipRoll.Speed : 3);
                PlayerHandler.ShowToast(string.Format(UIStrings.CapturedShipDeployedToast.GetText(), freeLaunchPoints[0].Q, freeLaunchPoints[0].R));
                if (player != null && player.Type == Player.PlayerType.LOCAL)
                {
                    Camera.main?.GetComponent<CameraMotion>()?.PanToTri(freeLaunchPoints[0], onlyIfOffscreen: true);
                }
            }
            else
            {
                player.CapturedTradeShipsCount++;
                string reason = (player.AvailableTransportShips <= 0)
                    ? UIStrings.CapturedShipStoredMaxShipsReason.GetText()
                    : UIStrings.CapturedShipStoredNoSpaceportReason.GetText();
                PlayerHandler.ShowToast(string.Format(UIStrings.CapturedShipStoredToast.GetText(), reason));
            }
        }

        // 7. Space Jump
        if (effect.SpaceJump)
        {
            Unit shipToJump = null;
            if (PendingSpaceJumpShipCoord.HasValue)
            {
                shipToJump = player.Units?.FirstOrDefault(u =>
                    u.Tri != null && u.Tri.Q == PendingSpaceJumpShipCoord.Value.x && u.Tri.R == PendingSpaceJumpShipCoord.Value.y);
            }
            if (shipToJump == null)
            {
                shipToJump = player.Units?.FirstOrDefault(u => u != null && !u.IsDestroyed);
            }
            if (shipToJump != null)
            {
                if (player != null && player.Type == Player.PlayerType.AI)
                {
                    ExecuteAiSpaceJump(player, shipToJump);
                }
                else
                {
                    PendingSpaceJumpShip = shipToJump;
                    UpdateSpaceJumpMarkers();
                    OnSpaceJumpStateChanged?.Invoke();
                }
            }
            PendingSpaceJumpShipCoord = null;
        }

        // 8. Event card effects: Reshuffle and Redraw
        if (effect.ReshuffleDeck)
        {
            // Reshuffle deck will happen on CompleteCurrentCard
        }
    }

    private void DeductAnyResources(Player player, int totalAmount)
    {
        if (player == null || totalAmount <= 0) return;
        int remaining = Mathf.Min(totalAmount, player.TotalResources);
        if (remaining <= 0) return;

        ResourceType[] types = { ResourceType.Ore, ResourceType.Fuel, ResourceType.Carbon, ResourceType.Food, ResourceType.TradeGoods };
        foreach (ResourceType t in types)
        {
            int count = player.GetResourceCount(t);
            int take = Mathf.Min(count, remaining);
            if (take > 0)
            {
                var dict = new Dictionary<ResourceType, int> { { t, take } };
                player.SpendResources(dict);
                remaining -= take;
                if (remaining <= 0) break;
            }
        }
    }


    public void ConfirmEncounterResolved()
    {
        if (CurrentPhase != GameTurnPhase.Flight ||
            !EncounterPending ||
            CurrentEncounterCard == null ||
            (!CurrentEncounterCard.RefreshesEncounterDeck &&
             CurrentEncounterPhase != EncounterCardPhase.ApplyOutcome))
        {
            throw new System.InvalidOperationException(
                "Select and apply an encounter outcome before completing the card.");
        }

        EncounterCard replacementCard = null;
        if (encounterDeck != null && encounterDeck.CurrentCard != null)
        {
            replacementCard = encounterDeck.CompleteCurrentCard();
        }
        if (replacementCard != null)
        {
            CurrentEncounterCard = replacementCard;
            EncounterReader = GetLeftNeighbor(CurrentPlayer);
            BeginEncounterCard();
            OnEncounterCardDrawn?.Invoke(EncounterReader, CurrentEncounterCard);
            return;
        }

        CurrentEncounterCard = null;
        EncounterReader = null;
        SelectedEncounterOutcome = null;
        LastEncounterStealSummary = null;
        SkipNextOutcomeResourceDeduction = false;
        SkipNextOutcomeUpgradeLoss = false;
        SkipNextOutcomeUpgradeGain = false;
        PendingBlockedShipCoord = null;
        PendingSpaceJumpShipCoord = null;
        PendingEncounterDonationAmount = 0;
        PendingEncounterDonatedResources = new Dictionary<ResourceType, int>();
        PendingEncounterRewardResources = new Dictionary<ResourceType, int>();
        SetEncounterPending(false);

        if (IsExecutingSpaceJump && CurrentPlayer != null && CurrentPlayer.Type != Player.PlayerType.AI)
        {
            SelectionController sc = FindFirstObjectByType<SelectionController>();
            if (sc != null && PendingSpaceJumpShip != null)
            {
                sc.SelectedUnit = PendingSpaceJumpShip;
            }
            UpdateSpaceJumpMarkers();
            PlayerHandler.ShowToast(UIStrings.SpaceJumpTargetPrompt.GetText(), isWarning: false, duration: 5.5f);
        }
    }

    private void BeginEncounterCard()
    {
        SelectedEncounterOutcome = null;
        LastEncounterStealSummary = null;
        SkipNextOutcomeResourceDeduction = false;
        SkipNextOutcomeUpgradeLoss = false;
        SkipNextOutcomeUpgradeGain = false;
        PendingBlockedShipCoord = null;
        PendingSpaceJumpShipCoord = null;
        PendingEncounterDonationAmount = 0;
        PendingEncounterDonatedResources = new Dictionary<ResourceType, int>();
        PendingEncounterRewardResources = new Dictionary<ResourceType, int>();
        CurrentEncounterPhase = CurrentEncounterCard.RefreshesEncounterDeck
            ? EncounterCardPhase.ApplyOutcome
            : EncounterCardPhase.ReadPrompt;
        OnEncounterProgressChanged?.Invoke(CurrentEncounterPhase, null);
    }

    private void SetEncounterPending(bool pending)
    {
        if (EncounterPending == pending)
        {
            return;
        }

        EncounterPending = pending;
        OnEncounterStateChanged?.Invoke(pending);
    }

    private static FlagshipRollResult RollFlagship(int driveCount)
    {
        FlightImpulseCore[] cores =
        {
            FlightImpulseCore.Yellow,
            FlightImpulseCore.Yellow,
            FlightImpulseCore.Blue,
            FlightImpulseCore.Red,
            FlightImpulseCore.Black
        };

        int firstIndex = UnityEngine.Random.Range(0, cores.Length);
        int secondIndex = UnityEngine.Random.Range(0, cores.Length - 1);
        if (secondIndex >= firstIndex)
        {
            secondIndex++;
        }

        return new FlagshipRollResult(
            cores[firstIndex],
            cores[secondIndex],
            driveCount);
    }

    public Player GetOpponentPlayer(Player player, EncounterOpponentRole role)
    {
        if (Players == null || Players.Length == 0) return null;
        int playerIndex = System.Array.IndexOf(Players, player);
        if (playerIndex < 0) return null;

        int n = Players.Length;
        switch (role)
        {
            case EncounterOpponentRole.Right:
                // Clockwise/right neighbor
                return Players[(playerIndex - 1 + n) % n];
            case EncounterOpponentRole.Left:
                // Counter-clockwise/left neighbor
                return Players[(playerIndex + 1) % n];
            case EncounterOpponentRole.SecondRight:
                return Players[(playerIndex - 2 + n) % n];
            case EncounterOpponentRole.SecondLeft:
                return Players[(playerIndex + 2) % n];
            default:
                return null;
        }
    }

    public static int GetFlagshipCombatRoll(out FlagshipRollResult rollResult, int cannons = 0)
    {
        rollResult = RollFlagship(0);
        return rollResult.BaseSpeed + cannons;
    }

    public Player GetLeftNeighbor(Player player)
    {
        int playerIndex = System.Array.IndexOf(Players, player);
        if (playerIndex < 0)
        {
            throw new System.ArgumentException("The player is not part of this map.", "player");
        }

        return Players[(playerIndex + 1) % Players.Length];
    }

    private void SetTurnPhase(GameTurnPhase phase)
    {
        CurrentPhase = phase;
        RuntimeLog.WriteLine(string.Format(
            "Turn phase changed to {0} for player {1} (turn {2}).",
            phase,
            CurrentPlayer != null ? CurrentPlayer.PlayerName : "<none>",
            TurnNumber));
        if (phase == GameTurnPhase.TradeAndBuild || phase == GameTurnPhase.Flight)
        {
            CheckSpecialPlanetsForPlayer(CurrentPlayer);
        }
        OnTurnPhaseChanged?.Invoke(CurrentPlayer, phase);
    }

    private void EndGame(Player winner)
    {
        IsGameOver = true;
        Winner = winner;
        RuntimeLog.WriteLine(string.Format(
            "[GameOver] {0} wins with {1} victory points in turn {2}.",
            winner.PlayerName, winner.VictoryPoints, TurnNumber));
        OnGameOver?.Invoke(winner);
    }

    /// <summary>
    /// Synchronizes player states, turn phase, and active player from an authoritative network snapshot.
    /// </summary>
    public void SyncFromObservation(AgentBridge.GameStateObservationDTO obs)
    {
        if (obs == null) return;

        if (obs.turnNumber > 0)
        {
            TurnNumber = obs.turnNumber;
        }

        GameTurnPhase oldPhase = CurrentPhase;
        if (System.Enum.TryParse(obs.turnPhase, out GameTurnPhase phase))
        {
            CurrentPhase = phase;
        }

        if (obs.dialogs != null)
        {
            NetworkDialogs = obs.dialogs;
            IsResolvingSevenRoll = obs.dialogs.isSevenRollResolving;
        }
        else
        {
            NetworkDialogs = null;
            IsResolvingSevenRoll = false;
        }

        if (obs.lastProductionRoll > 0)
        {
            int tot = Mathf.Clamp(obs.lastProductionRoll, 2, 12);
            int d1 = Mathf.Clamp(tot / 2, 1, 6);
            int d2 = Mathf.Clamp(tot - d1, 1, 6);
            if (LastProductionRoll == null || LastProductionRoll.Total != tot)
            {
                LastProductionRoll = new ProductionRollResult(d1, d2);
                OnProductionRoll?.Invoke(CurrentPlayer, LastProductionRoll);
            }
        }
        else if (phase == GameTurnPhase.Production)
        {
            LastProductionRoll = null;
        }

        if (phase == GameTurnPhase.Flight)
        {
            if (obs.lastFlightSpeed > 0)
            {
                bool hadBlackCore = obs.lastFlightHadBlackCore || obs.lastFlightHadBlackBall;
                FlightImpulseCore c1 = FlightImpulseCore.Yellow;
                FlightImpulseCore c2 = hadBlackCore ? FlightImpulseCore.Black : FlightImpulseCore.Blue;
                int baseSpeed = 2 + (hadBlackCore ? 0 : 1);
                int driveCount = Mathf.Max(0, obs.lastFlightSpeed - baseSpeed);
                var roll = new FlagshipRollResult(c1, c2, driveCount);
                if (LastFlagshipRoll == null || LastFlagshipRoll.Speed != obs.lastFlightSpeed || LastFlagshipRoll.RequiresEncounter != hadBlackCore)
                {
                    LastFlagshipRoll = roll;
                    OnFlagshipRoll?.Invoke(CurrentPlayer, roll);
                }
            }
            else
            {
                LastFlagshipRoll = null;
            }
        }
        else
        {
            LastFlagshipRoll = null;
        }

        if (obs.encounter != null && obs.encounter.isPending)
        {
            NetworkEncounter = obs.encounter;
            SetEncounterPending(true);
            EncounterCardPhase oldEncPhase = CurrentEncounterPhase;
            if (System.Enum.TryParse(obs.encounter.phase, out EncounterCardPhase encPhase))
            {
                CurrentEncounterPhase = encPhase;
            }

            if (CurrentEncounterCard == null || CurrentEncounterCard.Number.ToString() != obs.encounter.cardId)
            {
                CurrentEncounterCard = EncounterCardCatalog.Cards?.FirstOrDefault(c => c != null && c.Number.ToString() == obs.encounter.cardId)
                    ?? EncounterCardCatalog.Cards?.FirstOrDefault(c => c != null && !string.IsNullOrEmpty(obs.encounter.title) && c.Title != null && c.Title.GetText() == obs.encounter.title);
            }

            EncounterCardOutcome oldOutcome = SelectedEncounterOutcome;
            if (obs.encounter.selectedOutcomeIndex >= 0 && CurrentEncounterCard != null && CurrentEncounterCard.Outcomes != null && obs.encounter.selectedOutcomeIndex < CurrentEncounterCard.Outcomes.Count)
            {
                SelectedEncounterOutcome = CurrentEncounterCard.Outcomes[obs.encounter.selectedOutcomeIndex];
            }
            else
            {
                SelectedEncounterOutcome = null;
            }

            if (oldEncPhase != CurrentEncounterPhase || oldOutcome != SelectedEncounterOutcome)
            {
                OnEncounterProgressChanged?.Invoke(CurrentEncounterPhase, SelectedEncounterOutcome);
            }

            EncounterReader = GetLeftNeighbor(CurrentPlayer);
            if (PlayerHandler.Instance != null)
            {
                PlayerHandler.Instance.RefreshEncounterUI();
            }
        }
        else
        {
            NetworkEncounter = null;
            if (EncounterPending || CurrentEncounterCard != null)
            {
                SetEncounterPending(false);
                CurrentEncounterCard = null;
                EncounterReader = null;
                CurrentEncounterPhase = EncounterCardPhase.ReadPrompt;
                if (PlayerHandler.Instance != null)
                {
                    PlayerHandler.Instance.RefreshEncounterUI();
                }
            }
        }

        if (!string.IsNullOrEmpty(obs.foundingPhase) && System.Enum.TryParse(obs.foundingPhase, out FoundingPhase fPhase))
        {
            if (CurrentFoundingPhase != fPhase)
            {
                SetFoundingPhase(fPhase);
                if (PlayerHandler.Instance != null)
                {
                    PlayerHandler.Instance.RefreshFoundingUI();
                }
            }
        }

        RemainingStartingDrives = obs.foundingRemainingBoosters;
        RemainingStartingCannons = obs.foundingRemainingCannons;
        RemainingStartingCargoModules = obs.foundingRemainingFreightPods;

        if (CurrentFoundingPhase == FoundingPhase.SelectFlagshipUpgrade && PlayerHandler.Instance != null)
        {
            PlayerHandler.Instance.RefreshFoundingUI();
        }

        if (obs.startingPlayerCandidateIndex >= 0)
        {
            SyncStartingPlayerCandidateIndex(obs.startingPlayerCandidateIndex);
        }

        if (obs.currentPlayerIndex >= 0 && Players != null && obs.currentPlayerIndex < Players.Length)
        {
            if (currentPlayerIndex != obs.currentPlayerIndex)
            {
                SetCurrentPlayerIndex(obs.currentPlayerIndex);
            }
        }

        if (obs.players != null && Players != null)
        {
            for (int i = 0; i < obs.players.Length && i < Players.Length; i++)
            {
                var pDto = obs.players[i];
                var p = Players[i];
                if (pDto != null && p != null)
                {
                    p.NetworkVictoryPoints = pDto.victoryPoints;
                    p.NetworkRegularColoniesCount = pDto.regularColonies;
                    p.NetworkBuiltSpaceportsCount = pDto.spaceportsCount;
                    p.NetworkBuiltTradeStationsCount = pDto.tradeStationsCount;
                    p.HalfMedalCount = pDto.halfMedals;
                    p.SyncResourcesFromBundle(pDto.resources);
                    p.SyncUpgradesFromDTO(pDto.flagship);
                    p.CapturedTradeShipsCount = pDto.capturedTradeShips;
                    p.SyncFriendshipCardsFromDTO(pDto.friendshipCards);
                    if (!string.IsNullOrEmpty(pDto.playerType) && System.Enum.TryParse(pDto.playerType, out Player.PlayerType pType))
                    {
                        if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected)
                        {
                            int localIdx = Networking.NetworkManager.Instance.LocalPlayerIndex;
                            if (i == localIdx)
                            {
                                p.Type = (pType == Player.PlayerType.AI) ? Player.PlayerType.AI : Player.PlayerType.LOCAL;
                            }
                            else
                            {
                                p.Type = (pType == Player.PlayerType.AI) ? Player.PlayerType.AI : Player.PlayerType.REMOTE;
                            }
                        }
                        else
                        {
                            p.Type = pType;
                        }
                    }

                    if (pDto.colonies != null)
                    {
                        for (int cIdx = 0; cIdx < pDto.colonies.Length; cIdx++)
                        {
                            var cDto = pDto.colonies[cIdx];
                            Tri tri = GetTriAt(cDto.q, cDto.r);
                            if (tri != null)
                            {
                                if (tri.Colony == null && tri.IsColonyPoint)
                                {
                                    Colony col = new Colony();
                                    p.AddColony(col);
                                    SpawnColonyAt(col, ColonyPrefab, tri.Q, tri.R, p);
                                }
                                else if (tri.Colony != null)
                                {
                                    // Ensure this colony belongs to p and NOT other players
                                    p.AddColony(tri.Colony);
                                    if (Players != null)
                                    {
                                        for (int otherIdx = 0; otherIdx < Players.Length; otherIdx++)
                                        {
                                            if (otherIdx != i && Players[otherIdx] != null)
                                            {
                                                Players[otherIdx].RemoveColony(tri.Colony);
                                            }
                                        }
                                    }
                                }
                                if (cDto.type == "Spaceport" && tri.Colony != null && !tri.Colony.IsSpaceport)
                                {
                                    tri.Colony.UpgradeToSpaceport();
                                    MarkSpaceportVisual(tri.Colony);
                                }
                            }
                        }
                    }

                    if (pDto.tradeStations != null)
                    {
                        for (int tsIdx = 0; tsIdx < pDto.tradeStations.Length; tsIdx++)
                        {
                            var tsDto = pDto.tradeStations[tsIdx];
                            Tri tri = GetTriAt(tsDto.q, tsDto.r);
                            if (tri != null && tri.IsTradePoint && tri.SystemKind == SystemKind.Trade)
                            {
                                bool alreadyHasStation = p.TradeStations != null && p.TradeStations.Any(ts => ts != null && ts.Tri == tri);
                                if (!alreadyHasStation)
                                {
                                    TradeStation ts = new TradeStation();
                                    SpawnTradeStationAt(ts, TradeBasePrefab, tri.Q, tri.R, p);
                                    if (tri.AlienCivilization != AlienCivilization.None)
                                    {
                                        UpdateFolkMajority(tri.AlienCivilization, p);
                                    }
                                }
                            }
                        }
                    }

                    if (pDto.units != null)
                    {
                        var currentUnits = p.Units != null ? p.Units.Where(u => u != null && !u.IsDestroyed).ToList() : new List<Unit>();
                        var matchedUnits = new HashSet<Unit>();
                        var matchedDtoIndices = new HashSet<int>();

                        // --- Phase 1: Exact matches (Units that did not move or are already animating to target) ---
                        for (int uIdx = 0; uIdx < pDto.units.Length; uIdx++)
                        {
                            var uDto = pDto.units[uIdx];
                            Tri destTri = GetTriAt(uDto.q, uDto.r);
                            if (destTri == null) continue;

                            StarshipType st = uDto.shipType == "ColonyShip" ? StarshipType.ColonyShip : StarshipType.TradeShip;

                            Unit match = currentUnits.FirstOrDefault(u => !matchedUnits.Contains(u) && u.ShipType == st &&
                                (u.Tri == destTri ||
                                 (_animatingUnitTargets.TryGetValue(u, out Tri target) && target == destTri) ||
                                 (u.TriPath != null && u.TriPath.Count > 0 && u.TriPath[u.TriPath.Count - 1] == destTri)));

                            if (match != null)
                            {
                                matchedUnits.Add(match);
                                matchedDtoIndices.Add(uIdx);
                                if (!_animatingUnitsOnClient.Contains(match) && (match.TriPath == null || match.TriPath.Count == 0))
                                {
                                    match.MovementRemaining = uDto.movementRemaining;
                                }
                            }
                        }

                        // --- Phase 2: Movement matches (Units that moved along a path within valid flight distance) ---
                        var remainingLocal = currentUnits.Where(u => !matchedUnits.Contains(u) && !_animatingUnitsOnClient.Contains(u)).ToList();
                        foreach (Unit localUnit in remainingLocal)
                        {
                            if (localUnit.Tri == null) continue;

                            int bestDtoIdx = -1;
                            Tri[] bestPath = null;
                            int bestDist = int.MaxValue;

                            for (int uIdx = 0; uIdx < pDto.units.Length; uIdx++)
                            {
                                if (matchedDtoIndices.Contains(uIdx)) continue;

                                var uDto = pDto.units[uIdx];
                                StarshipType st = uDto.shipType == "ColonyShip" ? StarshipType.ColonyShip : StarshipType.TradeShip;
                                if (localUnit.ShipType != st) continue;

                                Tri destTri = GetTriAt(uDto.q, uDto.r);
                                if (destTri == null || destTri == localUnit.Tri) continue;

                                Tri[] path = FindDirectShipPath(localUnit.Tri, destTri);
                                // Normal flight is at most ~5-6 steps; allow up to 16 steps for network movement (boosters + rolls)
                                if (path != null && path.Length >= 2 && path[path.Length - 1] == destTri && path.Length <= 16)
                                {
                                    if (path.Length < bestDist)
                                    {
                                        bestDist = path.Length;
                                        bestPath = path;
                                        bestDtoIdx = uIdx;
                                    }
                                }
                            }

                            if (bestDtoIdx >= 0 && bestPath != null)
                            {
                                var uDto = pDto.units[bestDtoIdx];
                                Tri destTri = GetTriAt(uDto.q, uDto.r);
                                matchedUnits.Add(localUnit);
                                matchedDtoIndices.Add(bestDtoIdx);
                                StartCoroutine(AnimateUnitMovementOnClient(localUnit, bestPath, destTri, uDto.movementRemaining));
                            }
                        }

                        // --- Phase 3: Newly built / spawned ships (Remaining unmatched observed DTOs) ---
                        for (int uIdx = 0; uIdx < pDto.units.Length; uIdx++)
                        {
                            if (matchedDtoIndices.Contains(uIdx)) continue;

                            var uDto = pDto.units[uIdx];
                            Tri destTri = GetTriAt(uDto.q, uDto.r);
                            if (destTri == null) continue;

                            StarshipType st = uDto.shipType == "ColonyShip" ? StarshipType.ColonyShip : StarshipType.TradeShip;
                            Unit newUnit = new Unit { ShipType = st, MovementRemaining = uDto.movementRemaining };
                            p.AddUnit(newUnit);
                            SpawnShipAt(newUnit, destTri);
                            matchedUnits.Add(newUnit);
                            matchedDtoIndices.Add(uIdx);
                        }

                        // --- Phase 4: Destroy units that no longer exist on server (e.g. founded colony or destroyed) ---
                        foreach (Unit remaining in currentUnits)
                        {
                            if (!matchedUnits.Contains(remaining))
                            {
                                if (_animatingUnitsOnClient.Contains(remaining))
                                {
                                    _unitsPendingDestructionAfterAnimation.Add(remaining);
                                }
                                else
                                {
                                    remaining.Destroy();
                                }
                            }
                        }
                    }
                }
            }
        }

        if (obs.board != null && obs.board.starSystems != null && obs.board.starSystems.Length > 0)
        {
            SyncStarSystemsFromObservation(obs.board.starSystems);
        }

        if (obs.isGameOver && !IsGameOver)
        {
            Player winner = (obs.winnerIndex >= 0 && Players != null && obs.winnerIndex < Players.Length)
                ? Players[obs.winnerIndex]
                : CurrentPlayer;
            if (winner != null)
            {
                EndGame(winner);
            }
        }

        OnTurnPhaseChanged?.Invoke(CurrentPlayer, CurrentPhase);
        NotifyVictoryPointsChanged();

        if (PlayerHandler.Instance != null)
        {
            PlayerHandler.Instance.RefreshVictoryPointsTrackUI();
            PlayerHandler.Instance.RefreshFoundingUI();
            PlayerHandler.Instance.Sidebar?.UpdateResourceInventory();
            PlayerHandler.Instance.Sidebar?.UpdateFlagshipInventory();
            PlayerHandler.Instance.Sidebar?.UpdateTurnStatus();
            PlayerHandler.Instance.Sidebar?.UpdateNextButtonState();
            PlayerHandler.Instance.Sidebar?.RefreshActionPanel();
        }

        RefreshFoundingMarkers();

        AgentBridge.ExternalAgentBridge.Instance?.UpdateFromObservation(obs);
    }

    public void SyncStarSystemsFromObservation(AgentBridge.StarSystemStateDTO[] systems)
    {
        if (systems == null) return;

        foreach (var sysDto in systems)
        {
            if (sysDto == null) continue;
            Tri tri = GetTriAt(sysDto.q, sysDto.r);
            if (tri == null) continue;

            starSystemViews.TryGetValue(tri, out StarSystem view);

            // 1. Sync Resources & Planet Materials
            if (sysDto.resources != null && sysDto.resources.Length == 3)
            {
                if (System.Enum.TryParse(sysDto.resources[0], out ResourceType r0) &&
                    System.Enum.TryParse(sysDto.resources[1], out ResourceType r1) &&
                    System.Enum.TryParse(sysDto.resources[2], out ResourceType r2))
                {
                    ResourceType[] curRes = tri.PlanetResources;
                    if (curRes == null || curRes.Length != 3 || curRes[0] != r0 || curRes[1] != r1 || curRes[2] != r2)
                    {
                        tri.SetPlanetResources(r0, r1, r2);
                        if (view != null)
                        {
                            view.Planet1Material = GetResourceMaterial(r0);
                            view.Planet2Material = GetResourceMaterial(r1);
                            view.Planet3Material = GetResourceMaterial(r2);
                        }
                    }
                }
            }

            // 2. Sync Production Numbers
            if (sysDto.productionNumbers != null && sysDto.productionNumbers.Length == 3)
            {
                int[] curNums = tri.PlanetProductionNumbers;
                bool numsDiffer = curNums == null || curNums.Length != 3 ||
                    curNums[0] != sysDto.productionNumbers[0] ||
                    curNums[1] != sysDto.productionNumbers[1] ||
                    curNums[2] != sysDto.productionNumbers[2];

                if (numsDiffer &&
                    sysDto.productionNumbers[0] >= 2 && sysDto.productionNumbers[0] <= 12 && sysDto.productionNumbers[0] != 7 &&
                    sysDto.productionNumbers[1] >= 2 && sysDto.productionNumbers[1] <= 12 && sysDto.productionNumbers[1] != 7 &&
                    sysDto.productionNumbers[2] >= 2 && sysDto.productionNumbers[2] <= 12 && sysDto.productionNumbers[2] != 7)
                {
                    tri.SetPlanetProductionNumbers(
                        sysDto.productionNumbers[0],
                        sysDto.productionNumbers[1],
                        sysDto.productionNumbers[2]);

                    if (view != null)
                    {
                        view.SetPlanetProductionNumbers(
                            sysDto.productionNumbers[0],
                            sysDto.productionNumbers[1],
                            sysDto.productionNumbers[2],
                            sysDto.isRevealed);
                    }
                }
            }

            // 3. Sync Special Hazards
            if (sysDto.specialTypes != null && sysDto.specialTypes.Length == 3)
            {
                for (int s = 0; s < 3; s++)
                {
                    if (System.Enum.TryParse(sysDto.specialTypes[s], out PlanetSpecialType st))
                    {
                        int req = sysDto.specialRequirements != null && s < sysDto.specialRequirements.Length ? sysDto.specialRequirements[s] : 0;
                        bool resolved = sysDto.specialResolved != null && s < sysDto.specialResolved.Length && sysDto.specialResolved[s];

                        if (tri.GetPlanetSpecialType(s) != st || tri.GetPlanetSpecialRequirement(s) != req)
                        {
                            tri.SetPlanetSpecialType(s, st, req);
                        }

                        if (resolved && tri.HasUnresolvedHazard(s))
                        {
                            int currentNum = (sysDto.productionNumbers != null && s < sysDto.productionNumbers.Length) ? sysDto.productionNumbers[s] : 0;
                            tri.ResolveSpecialPlanet(s, currentNum);
                            if (view != null)
                            {
                                view.UpdatePlanetSpecialState(s, st, req, true, currentNum);
                            }
                        }
                    }
                }
            }

            // 4. Sync Revealed State
            if (sysDto.isRevealed && !tri.ArePlanetProductionNumbersRevealed)
            {
                tri.RevealPlanetProductionNumbers();
                if (view != null)
                {
                    view.RevealPlanetProductionNumbers();
                }
                BoardModel?.RevealPlanetProductionNumbers(tri);
            }

            // 5. Sync Alien Civilization
            if (!string.IsNullOrEmpty(sysDto.alienCivilization) &&
                System.Enum.TryParse(sysDto.alienCivilization, out AlienCivilization civ) &&
                civ != AlienCivilization.None)
            {
                tri.AlienCivilization = civ;
                tri.IsTradePoint = true;
                tri.IsPassable = true;
            }
        }
    }

    private void EndCurrentTurn()
    {
        if (IsGameOver) return;

        // The game ends with the turn in which the active player
        // owns 15 or more victory points; only the player whose turn it is can win.
        if (CurrentPlayer != null && CurrentPlayer.VictoryPoints >= WinningVictoryPoints)
        {
            EndGame(CurrentPlayer);
            return;
        }

        string previousPlayerName = CurrentPlayer != null ? CurrentPlayer.PlayerName : "Unknown";
        SetCurrentPlayerIndex((currentPlayerIndex + 1) % Players.Length);

        if(currentPlayerIndex == 0)
        {
            // New turn has begun!
            TurnNumber++;
            RuntimeLog.WriteLine(string.Format("[Turn] Turn {0} started.", TurnNumber));
            Debug.Log("STARTING TURN: " + TurnNumber);
        }

        RuntimeLog.WriteLine(string.Format(
            "[Turn] Switched active player from '{0}' to '{1}' (Player {2}/{3}).",
            previousPlayerName,
            CurrentPlayer.PlayerName,
            currentPlayerIndex + 1,
            Players.Length));
        Debug.Log("Starting turn for player index: " + currentPlayerIndex + " -- " + CurrentPlayer.PlayerName + " -- " + CurrentPlayer.Type.ToString());
        BeginPlayerTurn();
    }

    IEnumerator DoAllUnitMoves()
    {
        foreach(Unit u in CurrentPlayer.Units)
        {
            yield return DoUnitMoves( u );
        }
    }

    public IEnumerator DoUnitMoves( Unit u )
    {
        Tri originalTri = u != null ? u.Tri : null;
        bool isAuthoritative = Networking.NetworkManager.Instance == null ||
                               !Networking.NetworkManager.Instance.IsClientConnected ||
                               Networking.NetworkManager.Instance.IsHost;
        bool didMove = false;
        try
        {
            float maxWaitTime = 10.0f; // Safety timeout to prevent permanent lockup if animation gets desynced
            while( u.DoMove() )
            {
                // Each step in the path seamlessly enqueues the waypoint in UnitView
            }
            didMove = (u != null && u.Tri != null && u.Tri != originalTri);
            if (didMove && isAuthoritative)
            {
                OnUnitMoveInitiated?.Invoke(u);
            }
            float finalWaited = 0f;
            while (AnimationIsPlaying && finalWaited < maxWaitTime) {
                finalWaited += Time.deltaTime;
                yield return null; // Wait for smooth flight animation to reach final destination
            }
            u.ClearTriPath();
            if (u != null && u.Tri != null)
            {
                RevealAdjacentPlanetSystems(u.Tri);
                CheckAndResolveSpecialPlanetsAt(u, u.Tri);
            }
            RuntimeLog.WriteLine(string.Format(
                "[FlightMovement] Completed move for {0} (Player: '{1}'). Current position: ({2},{3}), Movement remaining: {4}.",
                u.ShipType,
                GetPlayerForUnit(u)?.PlayerName ?? "Unknown",
                u.Tri != null ? u.Tri.Q.ToString() : "?",
                u.Tri != null ? u.Tri.R.ToString() : "?",
                u.MovementRemaining));
        }
        finally
        {
            DeregisterUnitAnimating(u);
            if (didMove && isAuthoritative)
            {
                OnUnitMoveCompleted?.Invoke(u);
            }
        }
    }

    public Tri GetTriAt(int x, int y)
    {
        if(tris == null)
        {
            Debug.LogError("Tris array is not yet instatiated");
            return null;
        }
        if(AllowWrapEastWest)
        {
            x = x % numColumns;
            if(x < 0)
            {
                x += numColumns;
            }
        }
        if(AllowWrapNorthSouth)
        {
            y = y % numRows;
            if(y < 0)
            {
                y += numRows;
            }
        }   

        return tris[x, y];
    }

    public Vector3 GetTriPosition(int q, int r)
    {
        Tri tri = GetTriAt(q, r);

        return GetTriPosition(tri);
    }

    public Vector3 GetTriPosition(Tri tri)
    {
        return tri.PositionFromCamera( Camera.main.transform.position, NumRows, NumColumns );
    }

    public virtual void GenerateMap()
    {        
        if (CurrentBoardSeed == 0)
        {
            if (DebugSettings == null)
            {
                DebugSettings = new GameDebugSettings();
            }
            CurrentBoardSeed = DebugSettings.ResolveBoardSeed(out bool wasFixed);
            IsCurrentBoardSeedFixed = wasFixed;
            UnityEngine.Random.InitState(CurrentBoardSeed);
        }

        tris = new Tri[NumColumns, NumRows];
        triToGameObjectMap = new Dictionary<Tri, GameObject>();
        gameObjectToTriMap = new Dictionary<GameObject, Tri>();

        for(int c = 0; c < numColumns; c++)
        {
            for(int r = 0; r < numRows; r++)
            {
                Tri t = new Tri(this, c, r);
                t.TriMap.AllowWrapEastWest = AllowWrapEastWest;
                t.TriMap.AllowWrapNorthSouth = AllowWrapNorthSouth;
                tris[c, r] = t;

                bool isUpsideDown = false;
                Quaternion quaternion;
                if(c % 2 == 1)
                {
                    if(r % 2 == 0)
                    {
                        isUpsideDown = true;
                        quaternion = new Quaternion(180, 0, 0, 1);
                    }
                    else
                        quaternion = Quaternion.identity;
                }
                else
                {
                    if(r % 2 == 1)
                    {
                        isUpsideDown = true;
                        quaternion = new Quaternion(180, 0, 0, 1);
                    }
                    else
                        quaternion = Quaternion.identity;
                }
                t.IsUpsideDown = isUpsideDown;

                //Instatiate a Tri
                GameObject triGO = (GameObject)Instantiate(TriPrefab, 
                    t.Position(), //new Vector3(c * 0.866f, 0, r * 1.5), 
                    quaternion,
                    this.transform
                );

                triToGameObjectMap[t] = triGO;
                gameObjectToTriMap[triGO] = t;

                triGO.GetComponent<TriComponent>().Tri = t;
                triGO.GetComponent<TriComponent>().TriMap = this;

                // var triCoordLabel = triGO.GetComponentInChildren<TextMesh>();
                var triCoordLabel = t.GetLabelByName("TriCoordLabel");

                triCoordLabel.text = string.Format("{0},{1}", c, r);
                if(isUpsideDown)
                    triCoordLabel.transform.Rotate(180, 0, 0);
                triCoordLabel.gameObject.SetActive(IsDebug);

                MeshRenderer mr = triGO.GetComponentInChildren<MeshRenderer>();
                mr.material = TriMaterials[0];

                var systemLabel = t.GetLabelByName("SystemLabel");
                if(isUpsideDown)
                {
                    systemLabel.transform.Rotate(180, 0, 0);
                    systemLabel.transform.position = t.Position() - new Vector3(0, 0, -t.TriHeight() / 1.8f);
                }
                else
                {
                    systemLabel.transform.position = t.Position() + new Vector3(0, 0, t.TriHeight() / 1.8f);
                }

                t.SetSystemText();
            }
        }
    }

    protected void CaptureBoardModel(GameBoardVariant variant)
    {
        RegisterPlanetSystemAdjacency();
        BoardModel = new GameBoardModel(variant, tris);
    }

    private void RegisterPlanetSystemAdjacency()
    {
        foreach (Tri planetSystem in tris.Cast<Tri>()
            .Where(tri => tri.SystemKind == SystemKind.Planets))
        {
            Tri[] discoveryIntersections = planetSystem.GetPlanetSystemAdjacentIntersections();
            if (discoveryIntersections.Length != 9)
            {
                throw new System.InvalidOperationException(
                    "Planet system " + planetSystem + " must have nine adjacent " +
                    "intersections, but has " + discoveryIntersections.Length + ".");
            }

            foreach (Tri intersection in discoveryIntersections)
            {
                intersection.RegisterAdjacentPlanetSystem(planetSystem);
            }

            foreach (Tri colonySite in planetSystem.GetAllNeighbours().Where(n => n != null && n.IsColonyPoint))
            {
                colonySite.RegisterAdjacentPlanetSystem(planetSystem);
            }

            int[][] colonyOffsets = !planetSystem.IsUpsideDown
                ? new int[][] { new[] { 0, 1 }, new[] { -1, 0 }, new[] { 1, 0 } }
                : new int[][] { new[] { 0, -1 }, new[] { -1, 0 }, new[] { 1, 0 } };
            foreach (var off in colonyOffsets)
            {
                Tri cs = GetTriAt(planetSystem.Q + off[0], planetSystem.R + off[1]);
                if (cs != null)
                {
                    cs.RegisterAdjacentPlanetSystem(planetSystem);
                }
            }

            if (PlanetDiscoveryDiagnosticsEnabled)
            {
                LogPlanetDiscovery(string.Format(
                    "Board registration: planet system {0} ({1}) has discovery points [{2}].",
                    planetSystem,
                    planetSystem.SystemType,
                    string.Join(
                        "; ",
                        discoveryIntersections
                            .OrderBy(point => point.Q)
                            .ThenBy(point => point.R)
                            .Select(point => point + " passable=" + point.IsPassable)
                            .ToArray())));
            }
        }

        UpdatePlanetDiscoveryVisuals();
    }

    public void UpdatePlanetDiscoveryVisuals()
    {
        ClearPlanetDiscoveryMarkers();

        if (tris == null)
        {
            return;
        }

        var discoveryPoints = tris.Cast<Tri>()
            .Where(tri => tri != null && tri.AdjacentPlanetSystems != null && tri.AdjacentPlanetSystems.Length > 0)
            .OrderBy(tri => tri.Q)
            .ThenBy(tri => tri.R);

        if (HighlightPlanetDiscoveryPoints)
        {
            if (planetDiscoveryMarkersContainer == null)
            {
                planetDiscoveryMarkersContainer = new GameObject("PlanetDiscoveryMarkers");
                planetDiscoveryMarkersContainer.transform.SetParent(transform, false);
            }

            foreach (Tri intersection in discoveryPoints)
            {
                bool hasUnrevealedSystem = intersection.AdjacentPlanetSystems
                    .Any(system => !system.ArePlanetProductionNumbersRevealed);

                GameObject marker = CreatePlanetDiscoveryMarker(intersection, hasUnrevealedSystem);
                planetDiscoveryMarkers[intersection] = marker;

                ShowPlanetDiscoveryPointCoordinates(intersection);
            }
        }
        else
        {
            foreach (Tri intersection in discoveryPoints)
            {
                TextMesh coordinateLabel = intersection.GetLabelByName("TriCoordLabel");
                if (coordinateLabel != null)
                {
                    coordinateLabel.gameObject.SetActive(IsDebug);
                }
            }
        }
    }

    private GameObject CreatePlanetDiscoveryMarker(Tri intersection, bool isUnrevealed)
    {
        GameObject marker = new GameObject(
            string.Format("DiscoveryMarker_{0}_{1}_{2}{3}",
                intersection.Q,
                intersection.R,
                isUnrevealed ? "Unrevealed" : "Revealed",
                intersection.IsColonyPoint ? "_ColonySite" : "_Outer"));
        marker.transform.SetParent(planetDiscoveryMarkersContainer.transform, false);
        marker.transform.position = intersection.PositionFromCamera() + Vector3.up * 0.045f;

        Color ringColor = isUnrevealed
            ? new Color(0.12f, 0.95f, 0.25f, 1f)
            : new Color(0.25f, 0.55f, 0.35f, 0.6f);

        Color innerColor = new Color(0.04f, 0.07f, 0.12f, 1f);
        Color textColor = isUnrevealed ? Color.white : new Color(0.75f, 0.85f, 0.75f, 0.8f);

        // Outer green ring (disc)
        CreateSpaceportPart(
            marker.transform,
            PrimitiveType.Cylinder,
            "OuterRing",
            Vector3.zero,
            new Vector3(0.52f, 0.015f, 0.52f),
            ringColor);

        // Inner cutout creating ring appearance
        CreateSpaceportPart(
            marker.transform,
            PrimitiveType.Cylinder,
            "InnerCutout",
            new Vector3(0f, 0.002f, 0f),
            new Vector3(0.36f, 0.016f, 0.36f),
            innerColor);

        // Label with coordinates
        GameObject labelObject = new GameObject("CoordLabel");
        labelObject.transform.SetParent(marker.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 0.015f, 0f);
        labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = intersection.Q + "," + intersection.R;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 48;
        label.characterSize = 0.08f;
        label.fontStyle = FontStyle.Bold;
        label.color = textColor;

        Renderer labelRenderer = label.GetComponent<Renderer>();
        if (labelRenderer != null)
        {
            labelRenderer.sortingOrder = 15;
        }

        return marker;
    }

    public void ClearPlanetDiscoveryMarkers()
    {
        foreach (KeyValuePair<Tri, GameObject> entry in planetDiscoveryMarkers)
        {
            if (entry.Value != null)
            {
                entry.Value.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(entry.Value);
                }
                else
                {
                    DestroyImmediate(entry.Value);
                }
            }
        }
        planetDiscoveryMarkers.Clear();

        if (planetDiscoveryMarkersContainer != null)
        {
            planetDiscoveryMarkersContainer.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(planetDiscoveryMarkersContainer);
            }
            else
            {
                DestroyImmediate(planetDiscoveryMarkersContainer);
            }
            planetDiscoveryMarkersContainer = null;
        }

        List<GameObject> toDestroy = new List<GameObject>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null && (child.name == "PlanetDiscoveryMarkers" || child.name.StartsWith("DiscoveryMarker_")))
            {
                toDestroy.Add(child.gameObject);
            }
        }

        foreach (GameObject go in toDestroy)
        {
            go.SetActive(false);
            go.name = "PlanetDiscoveryMarkers_Destroyed";
            if (Application.isPlaying)
            {
                Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
        }

        if (tris != null)
        {
            foreach (Tri tri in tris)
            {
                if (tri != null && tri.AdjacentPlanetSystems != null && tri.AdjacentPlanetSystems.Length > 0)
                {
                    TextMesh label = tri.GetLabelByName("TriCoordLabel");
                    if (label != null)
                    {
                        label.gameObject.SetActive(IsDebug);
                    }
                }
            }
        }
    }

    private void ShowPlanetDiscoveryPointCoordinates(Tri point)
    {
        TextMesh coordinateLabel = point.GetLabelByName("TriCoordLabel");
        if (coordinateLabel != null)
        {
            if (HighlightPlanetDiscoveryPoints)
            {
                coordinateLabel.gameObject.SetActive(false);
            }
            else
            {
                coordinateLabel.gameObject.SetActive(IsDebug);
            }
        }
    }

    public void UpdateCoordinateLabelsVisibility()
    {
        if (tris == null)
        {
            return;
        }

        bool show = DebugSettings != null && DebugSettings.ShowCoordinateLabels;
        bool highlightPoints = DebugSettings != null && DebugSettings.HighlightPlanetDiscoveryPoints;

        foreach (Tri tri in tris)
        {
            if (tri == null)
            {
                continue;
            }
            TextMesh label = tri.GetLabelByName("TriCoordLabel");
            if (label != null)
            {
                bool isDiscoveryPoint = highlightPoints &&
                    tri.AdjacentPlanetSystems != null &&
                    tri.AdjacentPlanetSystems.Length > 0;
                label.gameObject.SetActive(show && !isDiscoveryPoint);
            }
        }
    }

    public void UpdateSystemLabelsVisibility()
    {
        if (tris == null)
        {
            return;
        }

        foreach (Tri tri in tris)
        {
            if (tri != null)
            {
                tri.SetSystemText();
            }
        }
    }

    public void UpdateTriVisuals()
    {
        for(int c = 0; c < numColumns; c++)
        {
            for(int r = 0; r < numRows; r++)
            {
                var t = tris[c, r];
                // if(!t.IsPassable)
                // {
                //     var triGO = GetTriGO(t);
                //     MeshRenderer mr = triGO.GetComponentInChildren<MeshRenderer>();
                //     mr.material = TriMaterials[1];
                // }
            }
        }
    }

    public Tri[] GetTrisWithinRangeOf(Tri centerTri, int radius)
    {
        List<Tri> results = new List<Tri>();
        for(int dx = -radius; dx <= radius; dx++)
        {
            int start = Mathf.Abs(dx) - radius;
            if(radius % 2 == 1)
            {
                int end = -start;
                for(int dy = Mathf.Max(-radius, start - 1); dy <= end; dy++)
                {
                    results.Add(tris[centerTri.Q+dx, centerTri.R +dy]);            
                }
            }
            else
            {
                int end = -start;
                for(int dy = start; dy <= Mathf.Min(radius, end + 1); dy++)
                {
                    results.Add(tris[centerTri.Q+dx, centerTri.R +dy]);            
                }
            }
        }
        return results.ToArray();
    }

    public Tri GetTriFromGameObject(GameObject triGO)
    {
        if( gameObjectToTriMap.ContainsKey(triGO) )
        {
            return gameObjectToTriMap[triGO];
        }

        return null;
    }

    public GameObject GetTriGO(Tri t)
    {
        if( triToGameObjectMap.ContainsKey(t) )
        {
            return triToGameObjectMap[t];
        }

        return null;
    }

    public GameObject GetUnitGO(Unit c)
    {
        if( unitToGameObjectMap.ContainsKey(c) )
        {
            return unitToGameObjectMap[c];
        }

        return null;
    }

    public void SpawnUnitAt( Unit unit, GameObject prefab, int q, int r )
    {
        if(unitToGameObjectMap == null)
        {
            unitToGameObjectMap = new Dictionary<Unit, GameObject>();
        }

        Tri myTri = GetTriAt(q, r);
        GameObject myTriGO = triToGameObjectMap[myTri];
        unit.SetTri(myTri);

        GameObject unitGO = (GameObject)Instantiate(prefab, myTriGO.transform.position, Quaternion.identity, myTriGO.transform);
        unit.OnObjectMoved += unitGO.GetComponent<UnitView>().OnUnitMoved;
        unit.OnObjectMoved += (oldTri, newTri) => OnUnitMovedForPlanetDiscoveryWithUnit(unit, oldTri, newTri);

        CurrentPlayer.AddUnit(unit);
        unit.OnObjectDestroyed += OnUnitDestroyed;
        unitToGameObjectMap.Add(unit, unitGO);
    }
    
    public void OnUnitDestroyed( MapObject mo )
    {
        Unit u = mo as Unit;
        if (u != null && unitToGameObjectMap != null && unitToGameObjectMap.TryGetValue(u, out GameObject go))
        {
            unitToGameObjectMap.Remove(u);
            if (go != null)
            {
                Destroy(go);
            }
        }
    }


    public void SpawnColonyAt( Colony colony, GameObject prefab, int q, int r, Player owner = null )
    {
        Debug.Log("SpawnColonyAt");
        if(colonyToGameObjectMap == null)
        {
            colonyToGameObjectMap = new Dictionary<Colony, GameObject>();
        }

        Tri myTri = GetTriAt(q, r);


        if(myTri.Colony != null)
        {
            throw new UnityException("Trying to add a colony to a tri that already has one!");
        }
        if(prefab == ColonyPrefab && !myTri.IsColonyPoint)
        {
            throw new UnityException("Trying to add a colony to a tri is not beneath a system!");
        }

        GameObject myTriGO = triToGameObjectMap[myTri];

        try
        {
            colony.SetTri(myTri);
        }
        catch(UnityException e)
        {
            Debug.LogError(e.Message);
            return;
        }

        GameObject colonyGO;
        if (prefab != null)
        {
            Quaternion initialRotation = prefab.transform.rotation;
            colonyGO = (GameObject)Instantiate(prefab, myTriGO.transform.position, initialRotation, this.transform);
        }
        else
        {
            Debug.LogWarning("[TriMap] ColonyPrefab is not assigned, using fallback primitive.");
            colonyGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            colonyGO.transform.SetParent(this.transform);
            colonyGO.transform.position = myTriGO.transform.position;
            colonyGO.transform.localScale = new Vector3(0.4f, 0.15f, 0.4f);
        }

        var zOffset = 0.30f;
        if (myTri.IsUpsideDown)
            colonyGO.transform.position = colonyGO.transform.position + new Vector3(0, 0.1f, -zOffset);
        else
            colonyGO.transform.position = colonyGO.transform.position + new Vector3(0, 0.1f, zOffset);

        Player effectiveOwner = owner ?? (colony != null ? Players?.FirstOrDefault(pl => pl?.Colonies != null && pl.Colonies.Contains(colony)) : null) ?? CurrentPlayer;
        ApplyPlayerMaterial(colonyGO, effectiveOwner?.Material, effectiveOwner?.Material != null ? effectiveOwner.Material.color : Color.white);

        if (effectiveOwner != null && (effectiveOwner.Colonies == null || !effectiveOwner.Colonies.Contains(colony)))
        {
            effectiveOwner.AddColony(colony);
        }
        colony.OnObjectDestroyed += OnColonyDestroyed;
        colonyToGameObjectMap.Add(colony, colonyGO);

        if(OnColonyCreated != null)
        {
            OnColonyCreated(colony, colonyGO);
        }

    }

    public void SpawnTradeStationAt( TradeStation tradeStation, GameObject prefab, int q, int r, Player owner = null )
    {
        if (tradeStation == null)
        {
            throw new System.ArgumentNullException("tradeStation");
        }
        if (prefab == null)
        {
            throw new System.ArgumentNullException("prefab");
        }

        if(tradeStationToGameObjectMap == null)
        {
            tradeStationToGameObjectMap = new Dictionary<TradeStation, GameObject>();
        }
        if(tradeStationSlots == null)
        {
            tradeStationSlots = new Dictionary<TradeStation, int>();
        }

        Tri myTri = GetTriAt(q, r);
        if(myTri == null || !myTri.IsTradePoint || myTri.SystemKind != SystemKind.Trade)
        {
            throw new UnityException("Trying to place a trade station outside a foreign trade outpost!");
        }

        if(myTri.GetTradeStationSlot(tradeStation) >= 0)
        {
            throw new UnityException("This trade station is already placed at the outpost!");
        }

        int i = System.Array.IndexOf(myTri.TradeStations, null);
        if(i < 0)
        {
            throw new UnityException("Trying to add a trade station to an outpost where all five docks are full!");
        }

        Player effectiveOwner = owner ?? (tradeStation != null ? Players?.FirstOrDefault(pl => pl?.TradeStations != null && pl.TradeStations.Contains(tradeStation)) : null) ?? CurrentPlayer;
        if(effectiveOwner?.Material == null && CurrentPlayer?.Material == null)
        {
            throw new UnityException("The current player's station color has not been configured!");
        }

        GameObject myTriGO = triToGameObjectMap[myTri];
        Transform outpostRoot = tradeOutpostRoots != null &&
            tradeOutpostRoots.TryGetValue(myTri, out Transform existingOutpostRoot)
                ? existingOutpostRoot
                : myTriGO.transform;

        try
        {
            tradeStation.SetTri(myTri);
        }
        catch(UnityException e)
        {
            Debug.LogError(e.Message);
            return;
        }

        GameObject tradeStationGO;
        if (prefab != null)
        {
            tradeStationGO = (GameObject)Instantiate(prefab, outpostRoot);
            tradeStationGO.transform.localRotation = prefab.transform.rotation;
        }
        else
        {
            Debug.LogWarning("[TriMap] TradeBasePrefab is not assigned, using fallback primitive.");
            tradeStationGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tradeStationGO.transform.SetParent(outpostRoot);
            tradeStationGO.transform.localScale = new Vector3(0.36f, 0.22f, 0.36f);
        }
        tradeStationGO.transform.localPosition = GetTradeDockPosition(i, 0.12f);

        MeshRenderer mr = tradeStationGO.GetComponentInChildren<MeshRenderer>();
        if(mr == null)
        {
            Destroy(tradeStationGO);
            tradeStation.Destroy();
            throw new UnityException("The trade station prefab has no MeshRenderer.");
        }
        Material effectiveMat = effectiveOwner?.Material ?? CurrentPlayer.Material;
        ApplyPlayerMaterial(tradeStationGO, effectiveMat, effectiveMat != null ? effectiveMat.color : Color.white);

        if (effectiveOwner != null && (effectiveOwner.TradeStations == null || !effectiveOwner.TradeStations.Contains(tradeStation)))
        {
            effectiveOwner.AddTradeStation(tradeStation);
        }
        tradeStation.OnObjectDestroyed += OnTradeStationDestroyed;
        tradeStationToGameObjectMap.Add(tradeStation, tradeStationGO);
        tradeStationSlots.Add(tradeStation, i);

        if(tradeOutpostDockMarkers != null &&
            tradeOutpostDockMarkers.TryGetValue(myTri, out GameObject[] dockMarkers))
        {
            dockMarkers[i].SetActive(false);
        }

        if(OnTradeStationCreated != null)
        {
            OnTradeStationCreated(tradeStation, tradeStationGO);
        }
    }

    protected void CreateTradeOutpostVisuals()
    {
        tradeOutpostDockMarkers = new Dictionary<Tri, GameObject[]>();
        tradeOutpostRoots = new Dictionary<Tri, Transform>();

        Shader standardShader = Shader.Find("Standard");
        if(standardShader == null)
        {
            throw new UnityException("The Standard shader is required to render trade outposts.");
        }

        Material stationMetalMaterial = CreateTrackedMaterial(standardShader, new Color(0.24f, 0.27f, 0.32f));
        Material dockPadMaterial = CreateTrackedMaterial(standardShader, new Color(0.42f, 0.46f, 0.52f));
        Material dockMarkerMaterial = CreateTrackedMaterial(standardShader, new Color(0.25f, 0.85f, 0.95f));
        Material pylonMaterial = CreateTrackedMaterial(standardShader, new Color(0.18f, 0.20f, 0.24f));

        tradeDockMaterial = dockPadMaterial;
        tradeOutpostMaterial = CreateTrackedMaterial(standardShader, new Color(0.16f, 0.48f, 0.62f));

        Dictionary<AlienCivilization, Material> civMaterials = new Dictionary<AlienCivilization, Material>
        {
            { AlienCivilization.Trader, CreateTrackedMaterial(standardShader, new Color(0.15f, 0.55f, 0.85f)) },
            { AlienCivilization.Scientist, CreateTrackedMaterial(standardShader, new Color(0.95f, 0.72f, 0.12f)) },
            { AlienCivilization.Diplomat, CreateTrackedMaterial(standardShader, new Color(0.75f, 0.25f, 0.70f)) },
            { AlienCivilization.Cultivator, CreateTrackedMaterial(standardShader, new Color(0.20f, 0.72f, 0.35f)) },
            { AlienCivilization.None, tradeOutpostMaterial }
        };

        foreach(Tri tri in tris)
        {
            if(tri.SystemKind != SystemKind.Trade || !tri.IsTradePoint)
            {
                continue;
            }

            GameObject triGO = GetTriGO(tri);
            GameObject outpost = new GameObject("ForeignTradeOutpost");
            outpost.transform.SetParent(triGO.transform, false);
            outpost.transform.position = tri.CenterPosition();
            outpost.transform.rotation = Quaternion.identity;
            tradeOutpostRoots.Add(tri, outpost.transform);

            Material civMat = civMaterials.TryGetValue(tri.AlienCivilization, out Material mat)
                ? mat
                : tradeOutpostMaterial;

            // 1. Central Docking Ring / Platform (Andockpunkt)
            // Low-profile circular landing area that ships can hover over or fly through without mesh collision.
            CreateOutpostPart(outpost.transform, PrimitiveType.Cylinder,
                new Vector3(0f, 0.012f, 0f), new Vector3(0.50f, 0.012f, 0.50f), stationMetalMaterial);
            CreateOutpostPart(outpost.transform, PrimitiveType.Cylinder,
                new Vector3(0f, 0.018f, 0f), new Vector3(0.38f, 0.010f, 0.38f), civMat);
            CreateOutpostPart(outpost.transform, PrimitiveType.Cylinder,
                new Vector3(0f, 0.024f, 0f), new Vector3(0.16f, 0.008f, 0.16f), dockMarkerMaterial);

            // 2. 5 Radial structural arms connecting the center to the 5 docking pads
            const float dockRadius = 0.72f;
            for (int slot = 0; slot < 5; slot++)
            {
                float angle = Mathf.PI * 2f * slot / 5f;
                Vector3 armPos = new Vector3(Mathf.Cos(angle) * (dockRadius * 0.5f), 0.015f, Mathf.Sin(angle) * (dockRadius * 0.5f));
                GameObject arm = CreateOutpostPart(outpost.transform, PrimitiveType.Cube,
                    armPos, new Vector3(0.06f, 0.015f, dockRadius * 0.85f), stationMetalMaterial);
                arm.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg + 90f, 0f);
            }

            // 3. 5 Docking pads around the perimeter
            GameObject[] dockMarkers = new GameObject[5];
            for(int slot = 0; slot < dockMarkers.Length; slot++)
            {
                Vector3 dockPos = GetTradeDockPosition(slot, 0.025f);

                CreateOutpostPart(outpost.transform, PrimitiveType.Cylinder,
                    dockPos, new Vector3(0.26f, 0.025f, 0.26f), dockPadMaterial);

                dockMarkers[slot] = CreateOutpostPart(outpost.transform, PrimitiveType.Cylinder,
                    dockPos + new Vector3(0f, 0.015f, 0f),
                    new Vector3(0.18f, 0.015f, 0.18f), dockMarkerMaterial);
                dockMarkers[slot].name = "Dock " + (slot + 1);
            }
            tradeOutpostDockMarkers.Add(tri, dockMarkers);

            // 4. Perimeter Gateway Pylons (placed around outer rim between docks, leaving the center clear)
            for (int pylon = 0; pylon < 5; pylon++)
            {
                float pylonAngle = (Mathf.PI * 2f * pylon / 5f) + (Mathf.PI / 5f);
                const float pylonRadius = 0.78f;
                Vector3 pylonPos = new Vector3(Mathf.Cos(pylonAngle) * pylonRadius, 0.15f, Mathf.Sin(pylonAngle) * pylonRadius);

                CreateOutpostPart(outpost.transform, PrimitiveType.Cylinder,
                    pylonPos, new Vector3(0.04f, 0.15f, 0.04f), pylonMaterial);

                CreateOutpostPart(outpost.transform, PrimitiveType.Sphere,
                    pylonPos + new Vector3(0f, 0.16f, 0f), new Vector3(0.07f, 0.07f, 0.07f), civMat);
            }

            // 5. 3D World Space Civilization Name Label
            GameObject labelObject = new GameObject("OutpostCivLabel");
            labelObject.transform.SetParent(outpost.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.035f, -0.65f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = UIStrings.GetAlienCivilizationName(tri.AlienCivilization).ToUpper();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 36;
            label.characterSize = 0.045f;
            label.fontStyle = FontStyle.Bold;
            label.color = civMat.color;

            Renderer labelRenderer = label.GetComponent<Renderer>();
            if (labelRenderer != null)
            {
                labelRenderer.sortingOrder = 16;
            }

            tradeOutpostLabels[tri] = label;
        }
    }

    public Transform GetTradeOutpostRoot(Tri tri)
    {
        if (tri != null && tradeOutpostRoots != null && tradeOutpostRoots.TryGetValue(tri, out Transform root))
        {
            return root;
        }
        return null;
    }

    private Material CreateTrackedMaterial(Shader shader, Color color)
    {
        Material mat = new Material(shader) { color = color };
        createdOutpostMaterials.Add(mat);
        return mat;
    }

    private static GameObject CreateOutpostPart(Transform parent, PrimitiveType primitiveType,
        Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(primitiveType);
        part.name = primitiveType.ToString();
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        Destroy(part.GetComponent<Collider>());
        return part;
    }

    private static Vector3 GetTradeDockPosition(int slot, float height)
    {
        float angle = Mathf.PI * 2f * slot / 5f;
        const float radius = 0.72f;
        return new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
    }

    protected void SpawnStarSystemAt(Tri tri, ResourceType planet1, ResourceType planet2, ResourceType planet3, System.Random rng = null)
    {
        tri.SetPlanetResources(planet1, planet2, planet3);
        int planet1Number = RollPlanetProductionNumber(rng);
        int planet2Number = RollPlanetProductionNumber(rng);
        int planet3Number = RollPlanetProductionNumber(rng);
        tri.SetPlanetProductionNumbers(planet1Number, planet2Number, planet3Number);
        bool numbersInitiallyRevealed = IsHomeSystem(tri.SystemType);
        if (numbersInitiallyRevealed)
        {
            tri.RevealPlanetProductionNumbers();
        }

        Quaternion quaternion;
        if(tri.IsUpsideDown)
        {
            quaternion = new Quaternion(180, 0, 0, 1);
        }
        else
            quaternion = Quaternion.identity;

        GameObject starSystemGo = (GameObject)Instantiate(StarSystemPrefab, 
            tri.Position() + (tri.IsUpsideDown ? - new Vector3(0,0f,tri.TriHeight() * 0.23f): new Vector3(0,0f,tri.TriHeight() * 0.23f)), 
            quaternion,
            this.transform
        );
        var starSystem = starSystemGo.GetComponent<StarSystem>();
        starSystem.Planet1Material = GetResourceMaterial(planet1);
        starSystem.Planet2Material = GetResourceMaterial(planet2);
        starSystem.Planet3Material = GetResourceMaterial(planet3);
        starSystem.SetPlanetProductionNumbers(
            planet1Number,
            planet2Number,
            planet3Number,
            numbersInitiallyRevealed);
        starSystem.SetPlanetSpecialTypes(
            tri.GetPlanetSpecialType(0), tri.GetPlanetSpecialRequirement(0),
            tri.GetPlanetSpecialType(1), tri.GetPlanetSpecialRequirement(1),
            tri.GetPlanetSpecialType(2), tri.GetPlanetSpecialRequirement(2));
        starSystemViews[tri] = starSystem;
    }

    public static bool IsHomeSystem(SystemTypes systemType)
    {
        return systemType == SystemTypes.Alpha ||
            systemType == SystemTypes.Beta ||
            systemType == SystemTypes.Gamma ||
            systemType == SystemTypes.Delta;
    }

    public static bool IsHomeColonyPoint(Tri tri)
    {
        if (tri == null || !tri.IsColonyPoint)
        {
            return false;
        }

        // Check adjacent registered planet systems
        if (tri.AdjacentPlanetSystems != null && tri.AdjacentPlanetSystems.Length > 0)
        {
            return tri.AdjacentPlanetSystems.Any(s => s != null && IsHomeSystem(s.SystemType));
        }

        // Or direct neighbors with planets
        return tri.GetNeighbours()
            .OfType<Tri>()
            .Any(n => n.SystemKind == SystemKind.Planets && IsHomeSystem(n.SystemType));
    }

    private void OnUnitMovedForPlanetDiscoveryWithUnit(Unit unit, Tri oldTri, Tri newTri)
    {
        string shipDescription = unit == null
            ? "<ship not resolved>"
            : unit.ShipType.ToString();
        string playerName = CurrentPlayer != null ? CurrentPlayer.PlayerName : "<none>";
        string oldPosition = oldTri == null ? "<none>" : oldTri.ToString();
        string newPosition = newTri == null ? "<none>" : newTri.ToString();

        if (PlanetDiscoveryDiagnosticsEnabled)
        {
            LogPlanetDiscovery(string.Format(
                "Movement callback: player={0}, ship={1}, phase={2}, from={3}, to={4}.",
                playerName,
                shipDescription,
                CurrentPhase,
                oldPosition,
                newPosition));
        }

        RevealAdjacentPlanetSystems(newTri);
        if (unit != null)
        {
            CheckAndResolveSpecialPlanetsAt(unit, newTri);
        }

        if (PlanetDiscoveryDiagnosticsEnabled)
        {
            string adjacentSystems = newTri == null || newTri.AdjacentPlanetSystems.Length == 0
                ? "<none>"
                : string.Join("; ", newTri.AdjacentPlanetSystems
                    .OrderBy(system => system.Q)
                    .ThenBy(system => system.R)
                    .Select(DescribePlanetSystemDiscoveryState)
                    .ToArray());
            string computedSystems = newTri == null
                ? "<none>"
                : string.Join("; ", tris.Cast<Tri>()
                    .Where(system => system.SystemKind == SystemKind.Planets &&
                        system.GetPlanetSystemAdjacentIntersections().Contains(newTri))
                    .OrderBy(system => system.Q)
                    .ThenBy(system => system.R)
                    .Select(DescribePlanetSystemDiscoveryState)
                    .ToArray());
            LogPlanetDiscovery(string.Format(
                "Movement result: to={0}, registered planet systems=[{1}], " +
                "independently computed systems=[{2}].",
                newPosition,
                adjacentSystems,
                string.IsNullOrEmpty(computedSystems) ? "<none>" : computedSystems));
        }
    }

    private void OnUnitMovedForPlanetDiscovery(Tri oldTri, Tri newTri)
    {
        Unit unit = unitToGameObjectMap != null
            ? unitToGameObjectMap.FirstOrDefault(entry => entry.Key.Tri == newTri).Key
            : null;
        OnUnitMovedForPlanetDiscoveryWithUnit(unit, oldTri, newTri);
    }

    private void RevealAdjacentPlanetSystems(Tri tri)
    {
        if (tri == null)
        {
            return;
        }

        foreach (Tri planetSystem in tri.AdjacentPlanetSystems)
        {
            RevealPlanetProductionNumbers(planetSystem);
        }

        if (tri.IsColonyPoint)
        {
            foreach (IQPathTile neighbour in tri.GetNeighbours())
            {
                Tri neighborTri = neighbour as Tri;
                if (neighborTri != null && neighborTri.SystemKind == SystemKind.Planets)
                {
                    RevealPlanetProductionNumbers(neighborTri);
                }
            }
        }
    }

    public void CheckSpecialPlanetsForPlayer(Player player)
    {
        if (player == null || player.Units == null) return;
        foreach (Unit unit in player.Units.ToArray())
        {
            if (unit != null && !unit.IsDestroyed && unit.Tri != null)
            {
                CheckAndResolveSpecialPlanetsAt(unit, unit.Tri);
            }
        }
    }

    public void CheckAndResolveSpecialPlanetsAt(Unit unit, Tri intersection)
    {
        if (unit == null || unit.IsDestroyed || intersection == null) return;
        if (Networking.NetworkManager.Instance != null &&
            Networking.NetworkManager.Instance.IsClientConnected &&
            !Networking.NetworkManager.Instance.IsHost)
        {
            return; // Host is authoritative for special planet resolution
        }
        Player player = GetPlayerForUnit(unit);
        if (player == null) return;

        List<Tri> candidateSystems = new List<Tri>();
        if (intersection.AdjacentPlanetSystems != null)
        {
            candidateSystems.AddRange(intersection.AdjacentPlanetSystems);
        }
        foreach (var n in intersection.GetAllNeighbours())
        {
            if (n is Tri pt && pt.SystemKind == SystemKind.Planets && !candidateSystems.Contains(pt))
            {
                candidateSystems.Add(pt);
            }
        }
        if (tris != null)
        {
            foreach (Tri pt in tris)
            {
                if (pt != null && pt.SystemKind == SystemKind.Planets && !candidateSystems.Contains(pt))
                {
                    Tri[] disc = pt.GetPlanetSystemAdjacentIntersections();
                    if (disc != null && disc.Contains(intersection))
                    {
                        candidateSystems.Add(pt);
                        continue;
                    }
                    int[] idxs = pt.GetAdjacentPlanetIndicesForIntersection(intersection);
                    if (idxs != null && idxs.Length > 0)
                    {
                        candidateSystems.Add(pt);
                    }
                }
            }
        }

        foreach (Tri planetSystem in candidateSystems)
        {
            int[] adjacentPlanetIndices = planetSystem.GetAdjacentPlanetIndicesForIntersection(intersection);
            if (adjacentPlanetIndices == null || adjacentPlanetIndices.Length == 0)
            {
                continue;
            }

            foreach (int planetIndex in adjacentPlanetIndices)
            {
                if (planetSystem.HasUnresolvedHazard(planetIndex))
                {
                    PlanetSpecialType type = planetSystem.GetPlanetSpecialType(planetIndex);
                    int requirement = planetSystem.GetPlanetSpecialRequirement(planetIndex);

                    if (type == PlanetSpecialType.IcePlanet)
                    {
                        if (player.FlagshipFreightPodCount >= requirement)
                        {
                            ResolveIcePlanet(player, planetSystem, planetIndex);
                        }
                        else
                        {
                            string msg = string.Format(UIStrings.CannotColonizeIcePlanet.GetText(), requirement);
                            RuntimeLog.WriteLine(string.Format("[IcePlanet] {0} reached Ice Planet at {1} (planet {2}, req {3} pods), but only has {4} pods.",
                                player.PlayerName, planetSystem, planetIndex + 1, requirement, player.FlagshipFreightPodCount));
                            PlayerHandler.ShowToast(msg, isWarning: true);
                        }
                    }
                    else if (type == PlanetSpecialType.MarauderBase)
                    {
                        if (player.EffectiveCannonCount >= requirement)
                        {
                            ResolveMarauderBase(player, planetSystem, planetIndex);
                        }
                        else
                        {
                            string msg = string.Format(UIStrings.CannotColonizeMarauderBase.GetText(), requirement);
                            RuntimeLog.WriteLine(string.Format("[MarauderBase] {0} reached Marauder Base at {1} (planet {2}, req {3} cannons), but only has {4} cannons.",
                                player.PlayerName, planetSystem, planetIndex + 1, requirement, player.EffectiveCannonCount));
                            PlayerHandler.ShowToast(msg, isWarning: true);
                        }
                    }
                }
            }
        }
    }

    public void ResolveIcePlanet(Player player, Tri planetSystem, int planetIndex)
    {
        int newNumber = ReserveNumberDeck.DrawNumber();
        planetSystem.ResolveSpecialPlanet(planetIndex, newNumber);
        player.AddIcePlanetToken();

        if (starSystemViews.TryGetValue(planetSystem, out StarSystem view) && view != null)
        {
            view.UpdatePlanetSpecialState(planetIndex, PlanetSpecialType.IcePlanet, planetSystem.GetPlanetSpecialRequirement(planetIndex), true, newNumber);
        }

        string toast = string.Format(UIStrings.TerraformSuccessToast.GetText(), player.PlayerName, newNumber);
        RuntimeLog.WriteLine(string.Format("[IcePlanet] {0} successfully terraformed Ice Planet at {1} (planet {2}). Drawn reserve number: {3}. (+1 Victory Point)",
            player.PlayerName, planetSystem, planetIndex + 1, newNumber));
        PlayerHandler.ShowToast(toast, isWarning: false);
        OnSpecialPlanetResolved?.Invoke();
        NotifyVictoryPointsChanged();
    }

    public void ResolveMarauderBase(Player player, Tri planetSystem, int planetIndex)
    {
        int newNumber = ReserveNumberDeck.DrawNumber();
        planetSystem.ResolveSpecialPlanet(planetIndex, newNumber);
        player.AddMarauderBaseToken();

        if (starSystemViews.TryGetValue(planetSystem, out StarSystem view) && view != null)
        {
            view.UpdatePlanetSpecialState(planetIndex, PlanetSpecialType.MarauderBase, planetSystem.GetPlanetSpecialRequirement(planetIndex), true, newNumber);
        }

        string toast = string.Format(UIStrings.MarauderBaseDefeatedToast.GetText(), player.PlayerName, newNumber);
        RuntimeLog.WriteLine(string.Format("[MarauderBase] {0} defeated Marauder Base at {1} (planet {2}). Drawn reserve number: {3}. (+1 Victory Point)",
            player.PlayerName, planetSystem, planetIndex + 1, newNumber));
        PlayerHandler.ShowToast(toast, isWarning: false);
        OnSpecialPlanetResolved?.Invoke();
        NotifyVictoryPointsChanged();
    }

    private void RevealPlanetProductionNumbers(Tri tri)
    {
        if (tri.ArePlanetProductionNumbersRevealed)
        {
            if (PlanetDiscoveryDiagnosticsEnabled)
            {
                LogPlanetDiscovery(
                    "Reveal skipped: system " + tri + " was already marked revealed; " +
                    DescribePlanetSystemDiscoveryState(tri));
            }
            return;
        }

        if (PlanetDiscoveryDiagnosticsEnabled)
        {
            LogPlanetDiscovery("Reveal requested: " + DescribePlanetSystemDiscoveryState(tri));
        }

        tri.RevealPlanetProductionNumbers();
        bool hasStarSystemView = starSystemViews.TryGetValue(tri, out StarSystem starSystem) &&
            starSystem != null;
        if (hasStarSystemView)
        {
            starSystem.RevealPlanetProductionNumbers();
        }
        BoardModel?.RevealPlanetProductionNumbers(tri);
        UpdatePlanetDiscoveryVisuals();

        if (PlanetDiscoveryDiagnosticsEnabled)
        {
            LogPlanetDiscovery(
                "Reveal completed: " + DescribePlanetSystemDiscoveryState(tri) +
                "; boardModelPresent=" + (BoardModel != null));
        }

        if (!hasStarSystemView)
        {
            Debug.LogError(
                "[PlanetDiscovery] Planet system " + tri +
                " has no StarSystem view; its numbers cannot be shown.",
                this);
        }
    }

    private string DescribePlanetSystemDiscoveryState(Tri planetSystem)
    {
        ResourceType[] resources = planetSystem.PlanetResources;
        int[] numbers = planetSystem.PlanetProductionNumbers;
        string[] planetStates = new string[Mathf.Max(resources.Length, numbers.Length)];
        for (int i = 0; i < planetStates.Length; i++)
        {
            string resource = i < resources.Length ? resources[i].ToString() : "<missing>";
            string number = i < numbers.Length ? numbers[i].ToString() : "<missing>";
            planetStates[i] = "planet" + (i + 1) + "=" + resource + "/" +
                (planetSystem.ArePlanetProductionNumbersRevealed ? number : "?");
        }

        bool hasView = starSystemViews.TryGetValue(planetSystem, out StarSystem view) &&
            view != null;
        string displayedNumbers = hasView
            ? view.GetProductionNumberDisplayState()
            : "<no StarSystem view>";
        return string.Format(
            "system {0} ({1}, {2}), revealed={3}, data=[{4}], viewLabels=[{5}]",
            planetSystem,
            planetSystem.SystemType,
            planetSystem.SystemKind,
            planetSystem.ArePlanetProductionNumbersRevealed,
            string.Join("; ", planetStates),
            displayedNumbers);
    }

    private void LogPlanetDiscovery(string message)
    {
        string entry = "[PlanetDiscovery] " + message;
        Debug.Log(entry, this);
    }

    private static int RollPlanetProductionNumber(System.Random rng = null)
    {
        int firstDie;
        int secondDie;
        int total;
        do
        {
            firstDie = rng != null ? rng.Next(1, 7) : UnityEngine.Random.Range(1, 7);
            secondDie = rng != null ? rng.Next(1, 7) : UnityEngine.Random.Range(1, 7);
            total = firstDie + secondDie;
        }
        while (total == 7);

        return total;
    }

    private Material GetResourceMaterial(ResourceType resource)
    {
        switch(resource)
        {
            case ResourceType.Ore:
                return OreMaterial;
            case ResourceType.Fuel:
                return FuelMaterial;
            case ResourceType.Carbon:
                return CarbonMaterial;
            case ResourceType.Food:
                return FoodMaterial;
            case ResourceType.TradeGoods:
                return TradeMaterial;
            default:
                throw new System.ArgumentOutOfRangeException("resource", resource, "Unknown Deep Space Pioneers resource.");
        }
    }

    public void OnColonyDestroyed( MapObject mo )
    {
        Colony colony = (Colony)mo;
        if (colonyToGameObjectMap.TryGetValue(colony, out GameObject go))
        {
            colonyToGameObjectMap.Remove(colony);
            Destroy(go);
        }
        if (spaceportVisuals.TryGetValue(colony, out GameObject spaceport))
        {
            spaceportVisuals.Remove(colony);
            Destroy(spaceport);
        }
    }

    public void OnTradeStationDestroyed( MapObject mo )
    {
        TradeStation tradeStation = (TradeStation)mo;
        if(tradeStationToGameObjectMap != null &&
            tradeStationToGameObjectMap.TryGetValue(tradeStation, out GameObject go))
        {
            tradeStationToGameObjectMap.Remove(tradeStation);
            Destroy(go);
        }

        if(tradeStationSlots != null &&
            tradeStationSlots.TryGetValue(tradeStation, out int slot))
        {
            tradeStationSlots.Remove(tradeStation);
            if(tradeStation.Tri != null &&
                tradeOutpostDockMarkers != null &&
                tradeOutpostDockMarkers.TryGetValue(tradeStation.Tri, out GameObject[] dockMarkers))
            {
                dockMarkers[slot].SetActive(true);
            }
        }
    }

    protected virtual void OnDestroy()
    {
        if (Instance == this) Instance = null;
        ClearPlanetDiscoveryMarkers();
        if (planetDiscoveryTestMarker != null)
        {
            Destroy(planetDiscoveryTestMarker);
        }
        if (planetDiscoveryTestStartMarker != null)
        {
            Destroy(planetDiscoveryTestStartMarker);
        }
        foreach (Material mat in createdOutpostMaterials)
        {
            if (mat != null)
            {
                Destroy(mat);
            }
        }
        createdOutpostMaterials.Clear();
        LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
    }

    private void HandleLanguageChanged(GameLanguage language)
    {
        if (foundingStatusFormatter != null)
        {
            FoundingStatus = foundingStatusFormatter();
            if (CurrentFoundingPhase == FoundingPhase.Complete && DebugSettings != null && DebugSettings.PlanetDiscoveryTestMode)
            {
                PlanetDiscoveryTestInstruction = FoundingStatus;
            }
            OnFoundingPhaseChanged?.Invoke(CurrentPlayer, CurrentFoundingPhase);
        }

        foreach (var kvp in tradeOutpostLabels)
        {
            if (kvp.Key != null && kvp.Value != null)
            {
                kvp.Value.text = UIStrings.GetAlienCivilizationName(kvp.Key.AlienCivilization).ToUpper();
            }
        }

        foreach (var view in starSystemViews.Values)
        {
            if (view != null)
            {
                view.UpdateChipsDisplay();
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (tris == null || DebugSettings == null || !DebugSettings.DrawDiscoveryGizmos)
        {
            return;
        }

        foreach (Tri t in tris)
        {
            if (t != null && t.AdjacentPlanetSystems != null && t.AdjacentPlanetSystems.Length > 0)
            {
                bool hasUnrevealed = t.AdjacentPlanetSystems
                    .Any(s => !s.ArePlanetProductionNumbersRevealed);
                Gizmos.color = hasUnrevealed
                    ? new Color(0.12f, 0.95f, 0.25f, 0.9f)
                    : new Color(0.25f, 0.55f, 0.35f, 0.5f);
                Gizmos.DrawWireSphere(t.Position() + Vector3.up * 0.05f, 0.25f);
            }
        }
    }

    protected virtual void Update()
    {
        HandleDebugKeyboardShortcuts();

        if (DebugSettings != null && !DebugSettings.HighlightPlanetDiscoveryPoints)
        {
            if (planetDiscoveryMarkers.Count > 0)
            {
                ClearPlanetDiscoveryMarkers();
            }
        }
    }

    private void HandleDebugKeyboardShortcuts()
    {
        if (DebugSettings == null || !DebugSettings.EnableKeyboardShortcuts)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.F1))
        {
            DebugSettings.ShowCoordinateLabels = !DebugSettings.ShowCoordinateLabels;
            UpdateCoordinateLabelsVisibility();
            Debug.Log("[DebugSettings] Coordinate labels " + (DebugSettings.ShowCoordinateLabels ? "ENABLED" : "DISABLED"));
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            DebugSettings.HighlightPlanetDiscoveryPoints = !DebugSettings.HighlightPlanetDiscoveryPoints;
            if (DebugSettings.HighlightPlanetDiscoveryPoints)
            {
                UpdatePlanetDiscoveryVisuals();
            }
            else
            {
                ClearPlanetDiscoveryMarkers();
            }
            Debug.Log("[DebugSettings] Planet discovery highlight " + (DebugSettings.HighlightPlanetDiscoveryPoints ? "ENABLED" : "DISABLED"));
        }

        if (Input.GetKeyDown(KeyCode.F3))
        {
            DebugSettings.DrawDiscoveryGizmos = !DebugSettings.DrawDiscoveryGizmos;
            Debug.Log("[DebugSettings] Discovery gizmos " + (DebugSettings.DrawDiscoveryGizmos ? "ENABLED" : "DISABLED"));
        }

        if (Input.GetKeyDown(KeyCode.F4))
        {
            DebugSettings.Language = DebugSettings.Language == GameLanguage.English
                ? GameLanguage.German
                : GameLanguage.English;
            LocalizationManager.SetLanguage(DebugSettings.Language);
            Debug.Log("[DebugSettings] Language switched to: " + DebugSettings.Language);
        }

        if (Input.GetKeyDown(KeyCode.F5))
        {
            DebugSettings.ShowSystemDebugLabels = !DebugSettings.ShowSystemDebugLabels;
            UpdateSystemLabelsVisibility();
            Debug.Log("[DebugSettings] System debug labels " + (DebugSettings.ShowSystemDebugLabels ? "ENABLED" : "DISABLED"));
        }
    }

    public static void ApplyPlayerMaterial(GameObject go, Material playerMaterial, Color fallbackColor)
    {
        if (go == null) return;
        Color pColor = playerMaterial != null ? playerMaterial.color : fallbackColor;
        MeshRenderer[] renderers = go.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var mr in renderers)
        {
            if (mr == null) continue;
            Material[] mats = mr.materials;
            bool replacedAny = false;

            // Dedicated accent colors for upgraded spaceport structures
            bool isGantry = mr.name.Equals("LaunchGantry", System.StringComparison.OrdinalIgnoreCase);
            bool isRadar = mr.name.Equals("TrackingRadar", System.StringComparison.OrdinalIgnoreCase);
            bool isRingDome = mr.name.StartsWith("RingDome", System.StringComparison.OrdinalIgnoreCase);

            if (isGantry)
            {
                Color gantryAccent = new Color(1.0f, 0.55f, 0.05f); // High-contrast industrial warm amber / gold
                Color gantryDark = new Color(0.18f, 0.18f, 0.22f);   // Gunmetal support frame
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string matName = mats[i].name;
                    if (matName.StartsWith("metalDark", System.StringComparison.OrdinalIgnoreCase) ||
                        matName.StartsWith("dark", System.StringComparison.OrdinalIgnoreCase))
                    {
                        mats[i].color = gantryDark;
                    }
                    else
                    {
                        mats[i].color = gantryAccent;
                    }
                    replacedAny = true;
                }
                if (!replacedAny && mats.Length > 0 && mats[0] != null)
                {
                    mats[0].color = gantryAccent;
                }
                mr.materials = mats;
                continue;
            }
            else if (isRadar)
            {
                Color radarAccent = new Color(0.1f, 0.85f, 1.0f);  // Luminous cyan dish and sensor
                Color radarDark = new Color(0.14f, 0.14f, 0.18f);   // Charcoal pivot mount
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string matName = mats[i].name;
                    if (matName.StartsWith("metalDark", System.StringComparison.OrdinalIgnoreCase) ||
                        matName.StartsWith("dark", System.StringComparison.OrdinalIgnoreCase))
                    {
                        mats[i].color = radarDark;
                    }
                    else
                    {
                        mats[i].color = radarAccent;
                    }
                    replacedAny = true;
                }
                if (!replacedAny && mats.Length > 0 && mats[0] != null)
                {
                    mats[0].color = radarAccent;
                }
                mr.materials = mats;
                continue;
            }
            else if (isRingDome)
            {
                Color domeAccent = new Color(0.15f, 0.85f, 1.0f);  // Luminous cyan observation dome / windows
                Color domeTrim = new Color(1.0f, 0.65f, 0.10f);    // Warm gold collar trim
                Color domeDark = new Color(0.14f, 0.14f, 0.18f);   // Charcoal base mount
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string matName = mats[i].name;
                    if (matName.StartsWith("metalDark", System.StringComparison.OrdinalIgnoreCase) ||
                        matName.StartsWith("dark", System.StringComparison.OrdinalIgnoreCase))
                    {
                        mats[i].color = domeDark;
                    }
                    else if (matName.StartsWith("metalRed", System.StringComparison.OrdinalIgnoreCase))
                    {
                        mats[i].color = domeTrim;
                    }
                    else
                    {
                        mats[i].color = domeAccent;
                    }
                    replacedAny = true;
                }
                if (!replacedAny && mats.Length > 0 && mats[0] != null)
                {
                    mats[0].color = domeAccent;
                }
                mr.materials = mats;
                continue;
            }
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string matName = mats[i].name;

                // 1. Primary main hull (metal) and accent stripes (metalRed) -> Full player material
                if (matName.StartsWith("metalRed", System.StringComparison.OrdinalIgnoreCase) ||
                    (matName.StartsWith("metal", System.StringComparison.OrdinalIgnoreCase) && 
                     !matName.StartsWith("metalDark", System.StringComparison.OrdinalIgnoreCase)))
                {
                    if (playerMaterial != null)
                    {
                        mats[i] = playerMaterial;
                    }
                    else
                    {
                        mats[i].color = pColor;
                    }
                    replacedAny = true;
                }
                // 2. Chassis / lower base frame (metalDark) -> Rich darkened tone of player color
                else if (matName.StartsWith("metalDark", System.StringComparison.OrdinalIgnoreCase))
                {
                    mats[i].color = Color.Lerp(pColor, Color.black, 0.45f);
                    replacedAny = true;
                }
                // 3. 'dark' stays black/charcoal for cockpits, exhausts, and mechanical vents
            }

            // Fallback for single-material models or custom primitives
            if (!replacedAny)
            {
                if (playerMaterial != null)
                {
                    if (mats.Length <= 1)
                    {
                        mats = new Material[] { playerMaterial };
                    }
                    else
                    {
                        mats[0] = playerMaterial;
                    }
                }
                else if (mats.Length > 0 && mats[0] != null)
                {
                    mats[0].color = fallbackColor;
                }
            }

            mr.materials = mats;
        }
    }

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        if (DebugSettings == null)
        {
            DebugSettings = new GameDebugSettings();
        }
        GameDebugSettings.Active = DebugSettings;
        LocalizationManager.SetLanguage(DebugSettings.Language);

        if (!DebugSettings.HighlightPlanetDiscoveryPoints)
        {
            ClearPlanetDiscoveryMarkers();
        }
        else if (Application.isPlaying && tris != null)
        {
            UpdatePlanetDiscoveryVisuals();
        }

        if (Application.isPlaying && tris != null)
        {
            UpdateCoordinateLabelsVisibility();
            UpdateSystemLabelsVisibility();
        }
    }
#endif
}
