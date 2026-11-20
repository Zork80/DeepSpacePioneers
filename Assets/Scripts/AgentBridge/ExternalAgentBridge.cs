using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AgentBridge
{
    /// <summary>
    /// File-based bidirectional IPC bridge for external automated test agents.
    /// Exports continuous game state observations to state.json and consumes action commands from action.json.
    /// Threading model: Polling and executions run strictly on Unity's main thread.
    /// </summary>
    public class ExternalAgentBridge : MonoBehaviour
    {
        public static ExternalAgentBridge Instance { get; private set; }
        public bool IsFileBridgeEnabled { get; private set; }

        public event Action<AgentResultDTO> OnActionResult;
        public event Action<GameStateObservationDTO> OnStateObservationChanged;

        private TriMap _triMap;
        private PlayerHandler _playerHandler;
        private DiscardCardsDialogUI _discardDialog;
        private StealCardDialogUI _stealDialog;
        private FriendshipCardDialogUI _friendshipDialog;
        private bool _lastDiscardActive = false;
        private bool _lastStealActive = false;
        private bool _lastFriendshipActive = false;

        private PlayerHandler PlayerHandlerComponent
        {
            get
            {
                if (_playerHandler == null)
                {
                    _playerHandler = UnityEngine.Object.FindAnyObjectByType<PlayerHandler>(FindObjectsInactive.Include);
                }
                return _playerHandler;
            }
        }

        private bool _searchedDiscard = false;
        private DiscardCardsDialogUI DiscardDialog
        {
            get
            {
                if (PlayerHandlerComponent != null && PlayerHandlerComponent.DiscardCardsDialog != null)
                {
                    return PlayerHandlerComponent.DiscardCardsDialog;
                }
                if (_discardDialog == null && !_searchedDiscard)
                {
                    _discardDialog = UnityEngine.Object.FindAnyObjectByType<DiscardCardsDialogUI>(FindObjectsInactive.Include);
                    _searchedDiscard = true;
                }
                return _discardDialog;
            }
        }

        private bool _searchedSteal = false;
        private StealCardDialogUI StealDialog
        {
            get
            {
                if (PlayerHandlerComponent != null && PlayerHandlerComponent.StealCardDialog != null)
                {
                    return PlayerHandlerComponent.StealCardDialog;
                }
                if (_stealDialog == null && !_searchedSteal)
                {
                    _stealDialog = UnityEngine.Object.FindAnyObjectByType<StealCardDialogUI>(FindObjectsInactive.Include);
                    _searchedSteal = true;
                }
                return _stealDialog;
            }
        }

        private bool _searchedFriendship = false;
        private FriendshipCardDialogUI FriendshipDialog
        {
            get
            {
                if (PlayerHandlerComponent != null && PlayerHandlerComponent.FriendshipCardDialog != null)
                {
                    return PlayerHandlerComponent.FriendshipCardDialog;
                }
                if (_friendshipDialog == null && !_searchedFriendship)
                {
                    _friendshipDialog = UnityEngine.Object.FindAnyObjectByType<FriendshipCardDialogUI>(FindObjectsInactive.Include);
                    _searchedFriendship = true;
                }
                return _friendshipDialog;
            }
        }

        private string _bridgeDirectory;
        private string _stateFilePath;
        private string _actionFilePath;
        private string _resultFilePath;

        private long _stepCount = 0;
        private bool _isActionInProgress = false;
        private bool _isStateDirty = true;
        private float _lastStateWriteTime = 0f;
        private const float MinStateWriteInterval = 0.2f;

        private class CachedSiteMetadata
        {
            public Tri tri;
            public int q;
            public int r;
            public bool isColonySite;
            public bool isTradeSite;
            public string tradeCivilization;
            public struct AdjPlanet
            {
                public Tri system;
                public int planetIndex;
            }
            public AdjPlanet[] adjPlanets;
            public BoardSiteDTO dto = new BoardSiteDTO();
        }

        private class CachedStarSystem
        {
            public Tri tri;
            public StarSystemStateDTO dto;
        }

        private List<CachedSiteMetadata> _cachedSites = null;
        private BoardSiteDTO[] _cachedSiteArray = null;
        private List<CachedStarSystem> _cachedStarSystems = null;
        private StarSystemStateDTO[] _cachedStarSystemArray = null;
        private readonly List<int> _reusableNumbers = new List<int>(6);
        private readonly List<string> _reusableResources = new List<string>(6);

        private static readonly string[] ResourceTypeStrings = new string[]
        {
            "None", "Ore", "Fuel", "Carbon", "Food", "TradeGoods"
        };

        public static string FastResourceTypeToString(ResourceType type)
        {
            int i = (int)type;
            if (i >= 0 && i < ResourceTypeStrings.Length) return ResourceTypeStrings[i];
            return "None";
        }

        public static string FastTurnPhaseToString(GameTurnPhase phase)
        {
            switch (phase)
            {
                case GameTurnPhase.Production: return "Production";
                case GameTurnPhase.TradeAndBuild: return "TradeAndBuild";
                case GameTurnPhase.Flight: return "Flight";
                default: return phase.ToString();
            }
        }

        public static string FastFoundingPhaseToString(FoundingPhase phase)
        {
            switch (phase)
            {
                case FoundingPhase.RollForStartingPlayer: return "RollForStartingPlayer";
                case FoundingPhase.PlaceFirstColony: return "PlaceFirstColony";
                case FoundingPhase.PlaceSecondColony: return "PlaceSecondColony";
                case FoundingPhase.PlaceThirdColony: return "PlaceThirdColony";
                case FoundingPhase.SelectSpaceport: return "SelectSpaceport";
                case FoundingPhase.SelectStartingShip: return "SelectStartingShip";
                case FoundingPhase.SelectStartingShipPoint: return "SelectStartingShipPoint";
                case FoundingPhase.SelectFlagshipUpgrade: return "SelectFlagshipUpgrade";
                case FoundingPhase.Complete: return "Complete";
                default: return phase.ToString();
            }
        }

        private readonly object _fileWriteLock = new object();
        private string _pendingStateJson = null;
        private string _pendingResultJson = null;
        private System.Threading.Thread _fileWriterThread = null;
        private volatile bool _isFileWriterRunning = false;
        private readonly System.Threading.AutoResetEvent _fileWriteEvent = new System.Threading.AutoResetEvent(false);

        private void Awake()
        {
            Application.runInBackground = true;
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            _triMap = GetComponent<TriMap>() ?? UnityEngine.Object.FindObjectOfType<TriMap>();
            if (_triMap == null)
            {
                Debug.LogError("[ExternalAgentBridge] TriMap not found. Bridge disabled.");
                enabled = false;
                return;
            }

            _playerHandler = UnityEngine.Object.FindObjectOfType<PlayerHandler>();
            _discardDialog = UnityEngine.Object.FindObjectOfType<DiscardCardsDialogUI>();
            _stealDialog = UnityEngine.Object.FindObjectOfType<StealCardDialogUI>();
            _friendshipDialog = UnityEngine.Object.FindObjectOfType<FriendshipCardDialogUI>();

            SubscribeToEvents();

            IsFileBridgeEnabled = CheckFileBridgeEnabled();
            if (IsFileBridgeEnabled)
            {
                ResolveBridgePaths();
                StartFileWriterThread();
                Debug.Log($"[ExternalAgentBridge] Initialized with File I/O enabled. Monitoring directory: {_bridgeDirectory}");
            }
            else
            {
                Debug.Log("[ExternalAgentBridge] Initialized in Memory-Only mode (Disk File I/O disabled). Use --enable-bridge or -bridge-dir to enable file monitoring.");
            }

            WriteStateObservation();
        }

        private void OnDisable()
        {
            StopFileWriterThread();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorUpdate;
#endif
        }

        private void OnDestroy()
        {
            StopFileWriterThread();
            UnsubscribeFromEvents();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void StartFileWriterThread()
        {
            if (_fileWriterThread != null && _fileWriterThread.IsAlive) return;
            _isFileWriterRunning = true;
            _fileWriterThread = new System.Threading.Thread(FileWriterLoop)
            {
                IsBackground = true,
                Name = "ExternalAgentBridge_Writer"
            };
            _fileWriterThread.Start();
        }

        private void StopFileWriterThread()
        {
            _isFileWriterRunning = false;
            _fileWriteEvent.Set();
            if (_fileWriterThread != null && _fileWriterThread.IsAlive)
            {
                _fileWriterThread.Join(500);
            }
            _fileWriterThread = null;
        }

        private void FileWriterLoop()
        {
            while (_isFileWriterRunning)
            {
                _fileWriteEvent.WaitOne(250);
                string stateJson = null;
                string resultJson = null;

                lock (_fileWriteLock)
                {
                    stateJson = _pendingStateJson;
                    _pendingStateJson = null;
                    resultJson = _pendingResultJson;
                    _pendingResultJson = null;
                }

                if (!string.IsNullOrEmpty(stateJson) && !string.IsNullOrEmpty(_stateFilePath))
                {
                    WriteFileAtomic(_stateFilePath, stateJson);
                }
                if (!string.IsNullOrEmpty(resultJson) && !string.IsNullOrEmpty(_resultFilePath))
                {
                    WriteFileAtomic(_resultFilePath, resultJson);
                }
            }
        }

        private static void WriteFileAtomic(string destinationPath, string json)
        {
            try
            {
                string tmpPath = destinationPath + ".tmp";
                File.WriteAllText(tmpPath, json);
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
                File.Move(tmpPath, destinationPath);
            }
            catch
            {
                try { File.WriteAllText(destinationPath, json); } catch { }
            }
        }

        private bool CheckFileBridgeEnabled()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            bool? explicitCliChoice = null;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a.Equals("-no-bridge", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("--no-bridge", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("-disable-bridge", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("--disable-bridge", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("-no-file-bridge", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("--no-file-bridge", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (a.Equals("-enable-bridge", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("--enable-bridge", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("-file-bridge", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("--file-bridge", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("-bridge", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("--bridge", StringComparison.OrdinalIgnoreCase) ||
                    a.StartsWith("-bridge-dir:", StringComparison.OrdinalIgnoreCase) ||
                    a.StartsWith("--bridge-dir:", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("-bridge-dir", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("--bridge-dir", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("--dir", StringComparison.OrdinalIgnoreCase))
                {
                    explicitCliChoice = true;
                }
            }

            if (explicitCliChoice.HasValue)
            {
                return explicitCliChoice.Value;
            }

            return _triMap?.DebugSettings != null && _triMap.DebugSettings.EnableFileBridge;
        }

        private void ResolveBridgePaths()
        {
            string configuredDir = _triMap?.DebugSettings != null
                ? _triMap.DebugSettings.AgentBridgeDirectory
                : "agent_bridge";

            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a.StartsWith("-bridge-dir:", StringComparison.OrdinalIgnoreCase))
                {
                    configuredDir = a.Substring(12).Trim('"', '\'');
                }
                else if (a.StartsWith("--bridge-dir:", StringComparison.OrdinalIgnoreCase))
                {
                    configuredDir = a.Substring(13).Trim('"', '\'');
                }
                else if ((a.Equals("-bridge-dir", StringComparison.OrdinalIgnoreCase) ||
                          a.Equals("--bridge-dir", StringComparison.OrdinalIgnoreCase) ||
                          a.Equals("--dir", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                {
                    configuredDir = args[i + 1].Trim('"', '\'');
                }
            }

            if (string.IsNullOrEmpty(configuredDir))
            {
                configuredDir = "agent_bridge";
            }

            if (Path.IsPathRooted(configuredDir))
            {
                _bridgeDirectory = configuredDir;
            }
            else
            {
                // In standalone builds under Builds/*, resolve to project root if running inside workspace
                string candidateWorkspace = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
                if (Directory.Exists(Path.Combine(candidateWorkspace, "Assets")))
                {
                    _bridgeDirectory = Path.GetFullPath(Path.Combine(candidateWorkspace, configuredDir));
                }
                else
                {
                    _bridgeDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", configuredDir));
                }
            }

            if (!Directory.Exists(_bridgeDirectory))
            {
                Directory.CreateDirectory(_bridgeDirectory);
            }

            _stateFilePath = Path.Combine(_bridgeDirectory, "state.json");
            _actionFilePath = Path.Combine(_bridgeDirectory, "action.json");
            _resultFilePath = Path.Combine(_bridgeDirectory, "result.json");
        }

        private void SubscribeToEvents()
        {
            if (_triMap == null) return;
            _triMap.OnFoundingPhaseChanged += HandleFoundingPhaseChanged;
            _triMap.OnTurnPhaseChanged += HandleTurnPhaseChanged;
            _triMap.OnPlayerChanged += HandlePlayerChanged;
            _triMap.OnEncounterProgressChanged += HandleEncounterProgressChanged;
            _triMap.OnEncounterStateChanged += HandleEncounterStateChanged;
            _triMap.OnGameOver += HandleGameOver;
            _triMap.OnProductionRoll += HandleProductionRoll;
            _triMap.OnFlagshipRoll += HandleFlagshipRoll;
            _triMap.OnSpecialPlanetResolved += HandleSpecialPlanetResolved;
            _triMap.OnColonyCreated += HandleColonyCreated;
            _triMap.OnTradeStationCreated += HandleTradeStationCreated;
            _triMap.OnUnitMoveInitiated += HandleUnitMoveInitiated;
            _triMap.OnUnitMoveCompleted += HandleUnitMoveCompleted;

            if (Networking.NetworkManager.Instance != null)
            {
                Networking.NetworkManager.Instance.OnClientActionResultReceived -= HandleNetworkActionResult;
                Networking.NetworkManager.Instance.OnClientActionResultReceived += HandleNetworkActionResult;
                Networking.NetworkManager.Instance.OnClientGameStateReceived -= HandleNetworkGameStateReceived;
                Networking.NetworkManager.Instance.OnClientGameStateReceived += HandleNetworkGameStateReceived;
                Networking.NetworkManager.Instance.Server?.HookAgentBridge();
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (Networking.NetworkManager.Instance != null)
            {
                Networking.NetworkManager.Instance.OnClientActionResultReceived -= HandleNetworkActionResult;
                Networking.NetworkManager.Instance.OnClientGameStateReceived -= HandleNetworkGameStateReceived;
            }
            if (_triMap == null) return;
            _triMap.OnFoundingPhaseChanged -= HandleFoundingPhaseChanged;
            _triMap.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
            _triMap.OnPlayerChanged -= HandlePlayerChanged;
            _triMap.OnEncounterProgressChanged -= HandleEncounterProgressChanged;
            _triMap.OnEncounterStateChanged -= HandleEncounterStateChanged;
            _triMap.OnGameOver -= HandleGameOver;
            _triMap.OnProductionRoll -= HandleProductionRoll;
            _triMap.OnFlagshipRoll -= HandleFlagshipRoll;
            _triMap.OnSpecialPlanetResolved -= HandleSpecialPlanetResolved;
            _triMap.OnColonyCreated -= HandleColonyCreated;
            _triMap.OnTradeStationCreated -= HandleTradeStationCreated;
            _triMap.OnUnitMoveInitiated -= HandleUnitMoveInitiated;
            _triMap.OnUnitMoveCompleted -= HandleUnitMoveCompleted;
        }

        private void HandleNetworkGameStateReceived(GameStateObservationDTO obs)
        {
            // On Host, local TriMap is authoritative and already dispatches state dirty events.
            // Re-dirtying state upon receiving its own network broadcast causes a continuous 5Hz serialization loop.
            if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsHost)
            {
                return;
            }
            MarkStateDirty();
        }

        private void HandleNetworkActionResult(AgentResultDTO res)
        {
            if (res != null)
            {
                WriteResult(res);
            }
        }

        private void HandleFoundingPhaseChanged(Player p, FoundingPhase phase) => MarkStateDirty();
        private void HandleTurnPhaseChanged(Player p, GameTurnPhase phase) => MarkStateDirty();
        private void HandlePlayerChanged(Player p) => MarkStateDirty();
        private void HandleEncounterProgressChanged(EncounterCardPhase phase, EncounterCardOutcome outcome) => MarkStateDirty();
        private void HandleEncounterStateChanged(bool active) => MarkStateDirty();
        private void HandleGameOver(Player winner) => MarkStateDirty();
        private void HandleProductionRoll(Player p, ProductionRollResult roll) => MarkStateDirty();
        private void HandleFlagshipRoll(Player p, FlagshipRollResult roll) => MarkStateDirty();
        private void HandleSpecialPlanetResolved() => MarkStateDirty();
        private void HandleColonyCreated(Colony colony, GameObject colonyGO) => WriteStateObservation(force: true);
        private void HandleTradeStationCreated(TradeStation tradeStation, GameObject colonyGO) => WriteStateObservation(force: true);
        private void HandleUnitMoveInitiated(Unit unit) => WriteStateObservation(force: true);
        private void HandleUnitMoveCompleted(Unit unit) => WriteStateObservation(force: true);

        public void MarkStateDirty()
        {
            _isStateDirty = true;
        }

#if UNITY_EDITOR
        private void OnEnable()
        {
            UnityEditor.EditorApplication.update += EditorUpdate;
        }

        private void EditorUpdate()
        {
            if (Application.isPlaying && isActiveAndEnabled)
            {
                UpdateBridge();
            }
        }
#endif

        private void Update()
        {
            UpdateBridge();
        }

        private float _lastActionCheckTime = 0f;

        private void UpdateBridge()
        {
            // Detect dialog open/close changes dynamically
            bool discardActive = DiscardDialog != null && DiscardDialog.IsActive;
            bool stealActive = StealDialog != null && StealDialog.IsActive;
            bool friendshipActive = FriendshipDialog != null && FriendshipDialog.IsActive;
            if (discardActive != _lastDiscardActive || stealActive != _lastStealActive || friendshipActive != _lastFriendshipActive)
            {
                _lastDiscardActive = discardActive;
                _lastStealActive = stealActive;
                _lastFriendshipActive = friendshipActive;
                _isStateDirty = true;
            }

            // 1. Process pending action if not currently executing an asynchronous action (throttled to 20Hz)
            if (IsFileBridgeEnabled && !_isActionInProgress && (Time.unscaledTime - _lastActionCheckTime) >= 0.05f)
            {
                _lastActionCheckTime = Time.unscaledTime;
                if (!string.IsNullOrEmpty(_actionFilePath) && File.Exists(_actionFilePath))
                {
                    TryConsumeAction();
                }
            }

            // 2. Flush dirty state
            if (_isStateDirty && (Time.unscaledTime - _lastStateWriteTime) >= MinStateWriteInterval)
            {
                WriteStateObservation();
            }
        }

        private void TryConsumeAction()
        {
            string actionJson = null;
            try
            {
                using (var fs = new FileStream(_actionFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fs))
                {
                    actionJson = reader.ReadToEnd();
                }
                File.Delete(_actionFilePath);
            }
            catch (IOException)
            {
                // File may be locked by writing process; retry next frame
                return;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ExternalAgentBridge] Failed to read action.json: {ex.Message}");
                return;
            }

            if (string.IsNullOrWhiteSpace(actionJson))
            {
                return;
            }

            AgentActionDTO action = null;
            try
            {
                action = JsonUtility.FromJson<AgentActionDTO>(actionJson);
            }
            catch (Exception ex)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = "unknown",
                    success = false,
                    error = $"Failed to parse JSON action: {ex.Message}",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            if (action == null || string.IsNullOrEmpty(action.action))
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action?.actionId ?? "unknown",
                    success = false,
                    error = "Invalid action: action field is missing or empty.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            DispatchAction(action);
        }

        public void DispatchAction(AgentActionDTO action)
        {
            _playerHandler?.ResetTurnInactivity();
            string cmd = action.action.Trim();
            if (cmd != "Ping" && cmd != "GetState")
            {
                if (Networking.NetworkManager.Instance != null &&
                    Networking.NetworkManager.Instance.IsClientConnected &&
                    !Networking.NetworkManager.Instance.IsHost)
                {
                    Networking.NetworkManager.Instance.Client.SendAction(action);
                    return;
                }
            }

            try
            {
                switch (cmd)
                {
                    case "Ping":
                    case "GetState":
                        ExecutePing(action);
                        break;

                    case "StartGame":
                        ExecuteStartGame(action);
                        break;

                    case "RollDice":
                    case "RollProduction":
                    case "RollProductionDice":
                        ExecuteRollDice(action);
                        break;

                    case "RollFlight":
                    case "RollFlightImpulse":
                    case "RollFlightMothership":
                        ExecuteRollFlight(action);
                        break;

                    case "AdvancePhase":
                    case "EndTurn":
                        ExecuteAdvancePhase(action);
                        break;

                    case "PlaceFoundingColony":
                        ExecutePlaceFoundingColony(action);
                        break;

                    case "SelectFoundingSpaceport":
                        ExecuteSelectFoundingSpaceport(action);
                        break;

                    case "SelectFoundingShipPoint":
                        ExecuteSelectFoundingShipPoint(action);
                        break;

                    case "SelectStartingUpgrade":
                        ExecuteSelectStartingUpgrade(action);
                        break;

                    case "SelectStartingShip":
                        ExecuteSelectStartingShip(action);
                        break;

                    case "MoveShip":
                        StartCoroutine(ExecuteMoveShipRoutine(action));
                        break;

                    case "FoundColony":
                        ExecuteFoundColony(action);
                        break;

                    case "UpgradeToSpaceport":
                    case "BuildSpaceport":
                        ExecuteBuildSpaceport(action);
                        break;

                    case "BuildTradeStation":
                        ExecuteBuildTradeStation(action);
                        break;

                    case "BuildShip":
                        ExecuteBuildShip(action);
                        break;

                    case "DeployCapturedShip":
                        ExecuteDeployCapturedShip(action);
                        break;

                    case "BuildUpgrade":
                        ExecuteBuildUpgrade(action);
                        break;

                    case "TradeWithSupply":
                        ExecuteTradeWithSupply(action);
                        break;

                    case "TradeWithPlayer":
                        ExecuteTradeWithPlayer(action);
                        break;

                    case "SelectEncounterOutcome":
                        ExecuteSelectEncounterOutcome(action);
                        break;

                    case "AdvanceEncounter":
                        ExecuteAdvanceEncounter(action);
                        break;

                    case "ConfirmEncounter":
                        ExecuteConfirmEncounter(action);
                        break;

                    case "ExecuteSpaceJump":
                        ExecuteSpaceJump(action);
                        break;

                    case "CancelSpaceJump":
                        ExecuteCancelSpaceJump(action);
                        break;

                    case "TriggerDebugEncounter":
                        ExecuteTriggerDebugEncounter(action);
                        break;

                    case "SevenRollDiscard":
                        ExecuteSevenRollDiscard(action);
                        break;

                    case "SevenRollSteal":
                        ExecuteSevenRollSteal(action);
                        break;

                    case "SelectFriendshipCard":
                        ExecuteSelectFriendshipCard(action);
                        break;

                    case "BuyFame":
                        ExecuteBuyFame(action);
                        break;

                    default:
                        WriteResult(new AgentResultDTO
                        {
                            actionId = action.actionId,
                            success = false,
                            error = $"Unrecognized action command: '{cmd}'",
                            timestamp = DateTime.UtcNow.ToString("o")
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ExternalAgentBridge] Exception executing '{cmd}': {ex}");
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = $"Execution exception: {ex.Message}",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
            }
        }

        #region Action Implementations

        private void ExecutePing(AgentActionDTO action)
        {
            WriteResult(new AgentResultDTO
            {
                actionId = action.actionId,
                success = true,
                message = "Pong. Game state refreshed.",
                timestamp = DateTime.UtcNow.ToString("o")
            });
            WriteStateObservation();
        }

        private void ExecuteStartGame(AgentActionDTO action)
        {
            if (_triMap != null && _triMap.IsGameSetupActive)
            {
                if (PlayerHandlerComponent != null && PlayerHandlerComponent.GameSetupDialog != null && PlayerHandlerComponent.GameSetupDialog.IsActive)
                {
                    PlayerHandlerComponent.GameSetupDialog.TryStartGame();
                }
                else
                {
                    _triMap.StartGameFromSetup();
                }

                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = true,
                    message = "Game setup completed. Game started.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                WriteStateObservation();
                return;
            }

            WriteResult(new AgentResultDTO
            {
                actionId = action.actionId,
                success = false,
                error = "Game setup is not active.",
                timestamp = DateTime.UtcNow.ToString("o")
            });
        }


        private void ExecuteRollDice(AgentActionDTO action)
        {
            if (_triMap.CurrentFoundingPhase == FoundingPhase.RollForStartingPlayer)
            {
                _triMap.RollForStartingPlayer();
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = true,
                    message = "Rolled for starting player candidate.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                WriteStateObservation();
                return;
            }

            if (_triMap.CurrentFoundingPhase != FoundingPhase.Complete)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = $"Cannot roll production dice during founding phase: {_triMap.CurrentFoundingPhase}",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            if (_triMap.CurrentPhase != GameTurnPhase.Production || _triMap.LastProductionRoll != null)
            {
                int rolled = _triMap.LastProductionRoll?.Total ?? 0;
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = true,
                    message = $"Production dice already rolled ({rolled}); current phase is {_triMap.CurrentPhase}.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                WriteStateObservation();
                return;
            }

            _triMap.RollProductionDice();
            int total = _triMap.LastProductionRoll?.Total ?? 0;
            if (!_triMap.IsResolvingSevenRoll)
            {
                _triMap.AdvanceTurnPhase();
            }
            WriteResult(new AgentResultDTO
            {
                actionId = action.actionId,
                success = true,
                message = $"Production dice rolled: {total}",
                timestamp = DateTime.UtcNow.ToString("o")
            });
            WriteStateObservation();
        }

        private void ExecuteRollFlight(AgentActionDTO action)
        {
            if (_triMap.CurrentPhase != GameTurnPhase.Flight)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = $"Cannot roll flight impulse during phase: {_triMap.CurrentPhase}",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            if (_triMap.LastFlagshipRoll != null)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = true,
                    message = $"Flight impulse already rolled. Speed: {_triMap.LastFlagshipRoll?.Speed ?? 0}, Encounter: {_triMap.LastFlagshipRoll?.RequiresEncounter ?? false}",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                WriteStateObservation();
                return;
            }

            _triMap.RollFlightImpulse();
            FlagshipRollResult roll = _triMap.LastFlagshipRoll;
            WriteResult(new AgentResultDTO
            {
                actionId = action.actionId,
                success = true,
                message = $"Flight impulse rolled. Speed: {roll?.Speed ?? 0}, Encounter: {roll?.RequiresEncounter ?? false}",
                timestamp = DateTime.UtcNow.ToString("o")
            });
            WriteStateObservation();
        }

        private void ExecuteAdvancePhase(AgentActionDTO action)
        {
            if (_triMap.IsGameOver)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = "Game is over.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            if (_triMap.CurrentFoundingPhase != FoundingPhase.Complete)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = "Cannot advance turn while founding phase is incomplete.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            GameTurnPhase oldPhase = _triMap.CurrentPhase;

            _triMap.AdvanceTurnPhase();

            WriteResult(new AgentResultDTO
            {
                actionId = action.actionId,
                success = true,
                message = $"Turn advanced. Phase: {oldPhase} -> {_triMap.CurrentPhase}, Active Player: {_triMap.CurrentPlayer?.PlayerName}",
                timestamp = DateTime.UtcNow.ToString("o")
            });
            WriteStateObservation();
        }

        private void ExecutePlaceFoundingColony(AgentActionDTO action)
        {
            Tri tri = _triMap.GetTriAt(action.q, action.r);
            if (tri == null)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = $"No intersection found at coordinates ({action.q}, {action.r}).",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            bool success = _triMap.TryPlaceFoundingColonyAt(tri);
            if (!success && tri.Colony != null && _triMap.CurrentPlayer != null && _triMap.CurrentPlayer.Colonies != null && _triMap.CurrentPlayer.Colonies.Contains(tri.Colony))
            {
                success = true;
            }
            WriteResult(new AgentResultDTO
            {
                actionId = action.actionId,
                success = success,
                message = success ? $"Founding colony placed at ({action.q}, {action.r})." : "Failed to place founding colony at specified site.",
                error = success ? string.Empty : "Placement requirements not met.",
                timestamp = DateTime.UtcNow.ToString("o")
            });
            WriteStateObservation();
        }

        private void ExecuteSelectFoundingSpaceport(AgentActionDTO action)
        {
            Tri tri = _triMap.GetTriAt(action.q, action.r);
            if (tri == null)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = $"No intersection found at coordinates ({action.q}, {action.r}).",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            bool success = _triMap.TrySelectFoundingSpaceportAt(tri);
            if (!success && tri.Colony != null && tri.Colony.IsSpaceport && _triMap.CurrentPlayer != null && _triMap.CurrentPlayer.Colonies != null && _triMap.CurrentPlayer.Colonies.Contains(tri.Colony))
            {
                success = true;
            }
            WriteResult(new AgentResultDTO
            {
                actionId = action.actionId,
                success = success,
                message = success ? $"Colony at ({action.q}, {action.r}) upgraded to founding spaceport." : "Failed to upgrade colony to founding spaceport.",
                error = success ? string.Empty : "Spaceport upgrade requirements not met.",
                timestamp = DateTime.UtcNow.ToString("o")
            });
            WriteStateObservation();
        }

        private void ExecuteSelectFoundingShipPoint(AgentActionDTO action)
        {
            Tri tri = _triMap.GetTriAt(action.q, action.r);
            if (tri == null)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = $"No intersection found at coordinates ({action.q}, {action.r}).",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            bool success = _triMap.TrySelectFoundingStartingShipPoint(tri);
            WriteResult(new AgentResultDTO
            {
                actionId = action.actionId,
                success = success,
                message = success ? $"Founding ship spawned at ({action.q}, {action.r})." : "Failed to spawn founding ship at site.",
                error = success ? string.Empty : "Site is not a valid free spaceport point.",
                timestamp = DateTime.UtcNow.ToString("o")
            });
            WriteStateObservation();
        }

        private void ExecuteSelectStartingUpgrade(AgentActionDTO action)
        {
            if (!TryParseUpgrade(action.upgradeType, out FlagshipUpgrade upgrade))
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = $"Unknown upgrade type: '{action.upgradeType}'. Supported: Booster, Cannon, FreightPod.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            int remaining = upgrade switch
            {
                FlagshipUpgrade.Drive => _triMap.RemainingStartingDrives,
                FlagshipUpgrade.Cannon => _triMap.RemainingStartingCannons,
                FlagshipUpgrade.CargoModule => _triMap.RemainingStartingCargoModules,
                _ => 0
            };

            if (remaining <= 0)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = $"No starting upgrades of type {upgrade} remain (Boosters: {_triMap.RemainingStartingDrives}, Cannons: {_triMap.RemainingStartingCannons}, FreightPods: {_triMap.RemainingStartingCargoModules}).",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            try
            {
                _triMap.SelectStartingUpgrade(upgrade);
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = true,
                    message = $"Selected starting upgrade: {upgrade}.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
            }
            catch (Exception ex)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow.ToString("o")
                });
            }
            WriteStateObservation();
        }

        private void ExecuteSelectStartingShip(AgentActionDTO action)
        {
            if (!TryParseShipType(action.shipType, out StarshipType shipType))
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = $"Unknown ship type: '{action.shipType}'. Supported: ColonyShip, TradeShip.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            if (_triMap.CurrentFoundingPhase != FoundingPhase.SelectStartingShip)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = $"A starting ship cannot be selected now. Current founding phase is '{_triMap.CurrentFoundingPhase}'.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            try
            {
                _triMap.SelectStartingShip(shipType);
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = true,
                    message = $"Selected starting ship: {shipType}.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
            }
            catch (Exception ex)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow.ToString("o")
                });
            }
            WriteStateObservation();
        }

        private IEnumerator ExecuteMoveShipRoutine(AgentActionDTO action)
        {
            _isActionInProgress = true;
            try
            {
                Unit unit = FindUnit(action.fromQ, action.fromR, action.unitId);
                if (unit == null)
                {
                    WriteResult(new AgentResultDTO
                    {
                        actionId = action.actionId,
                        success = false,
                        error = $"No movable ship found belonging to active player at ({action.fromQ}, {action.fromR}) or with id {action.unitId}.",
                        timestamp = DateTime.UtcNow.ToString("o")
                    });
                    yield break;
                }

                Tri destTri = _triMap.GetTriAt(action.toQ, action.toR);
                if (destTri == null)
                {
                    WriteResult(new AgentResultDTO
                    {
                        actionId = action.actionId,
                        success = false,
                        error = $"Target intersection ({action.toQ}, {action.toR}) not found.",
                        timestamp = DateTime.UtcNow.ToString("o")
                    });
                    yield break;
                }

                Tri[] fullPath = _triMap.FindShipPath(unit, destTri);
                if (fullPath == null || fullPath.Length < 2)
                {
                    WriteResult(new AgentResultDTO
                    {
                        actionId = action.actionId,
                        success = false,
                        error = $"No reachable path found from ({unit.Tri?.Q}, {unit.Tri?.R}) to ({action.toQ}, {action.toR}).",
                        timestamp = DateTime.UtcNow.ToString("o")
                    });
                    yield break;
                }

                if (!_triMap.CanMoveShipAlongPath(unit, fullPath))
                {
                    WriteResult(new AgentResultDTO
                    {
                        actionId = action.actionId,
                        success = false,
                        error = $"Path is not legally passable or destination ({action.toQ}, {action.toR}) is occupied.",
                        timestamp = DateTime.UtcNow.ToString("o")
                    });
                    yield break;
                }

                unit.SetTriPath(fullPath);
                yield return _triMap.DoUnitMoves(unit);

                float waitTime = 0f;
                while (_triMap.AnimationIsPlaying && waitTime < 3.0f)
                {
                    waitTime += Time.unscaledDeltaTime;
                    yield return null;
                }

                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = true,
                    message = $"Ship moved to ({unit.Tri?.Q}, {unit.Tri?.R}). Movement remaining: {unit.MovementRemaining}.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                WriteStateObservation();
            }
            finally
            {
                _isActionInProgress = false;
            }
        }

        private void ExecuteFoundColony(AgentActionDTO action)
        {
            Tri tri = _triMap.GetTriAt(action.q, action.r);
            Unit readyUnit = _triMap.CurrentPlayer?.Units?.FirstOrDefault(u => _triMap.CanFoundColonyWith(u));
            if (tri == null || readyUnit == null || readyUnit.Tri != tri)
            {
                if (readyUnit != null && readyUnit.Tri != null)
                {
                    tri = readyUnit.Tri;
                }
            }

            if (tri == null)
            {
                Unit shipOnColonyPoint = _triMap.CurrentPlayer?.Units?.FirstOrDefault(u =>
                    u.ShipType == StarshipType.ColonyShip && u.Tri != null && u.Tri.IsColonyPoint && u.Tri.Colony == null);
                if (shipOnColonyPoint != null)
                {
                    tri = shipOnColonyPoint.Tri;
                }
            }

            if (tri == null)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = $"No colony ship ready to found a colony at ({action.q}, {action.r}).",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            if (_triMap.AnimationIsPlaying)
            {
                _triMap.AnimationIsPlaying = false;
            }

            bool success = _triMap.TryFoundColonyAt(tri);
            WriteResult(new AgentResultDTO
            {
                actionId = action.actionId,
                success = success,
                message = success ? $"Colony founded at ({tri.Q}, {tri.R})." : "Failed to found colony.",
                error = success ? string.Empty : "Requirements not met: must have colony ship on site and free hazards.",
                timestamp = DateTime.UtcNow.ToString("o")
            });
            WriteStateObservation();
        }

        private void ExecuteBuildSpaceport(AgentActionDTO action)
        {
            if (!_triMap.CanBuildSpaceport())
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = "Cannot build spaceport: resource requirements or component limits not met.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            Tri tri = _triMap.GetTriAt(action.q, action.r);
            var eligibleColonies = _triMap.GetEligibleColoniesForSpaceportUpgrade(_triMap.CurrentPlayer)?.ToList();
            if (tri == null || eligibleColonies == null || !eligibleColonies.Any(c => c.Tri == tri))
            {
                if (eligibleColonies != null && eligibleColonies.Count > 0)
                {
                    tri = eligibleColonies.OrderBy(c => c.Tri.Q).First().Tri;
                }
            }

            if (tri == null)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = "No eligible colony found to upgrade to spaceport.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            if (!_triMap.TryBeginSpaceportUpgrade())
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = "Failed to initiate spaceport upgrade.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            bool success = _triMap.TryUpgradeSpaceportAt(tri);
            WriteResult(new AgentResultDTO
            {
                actionId = action.actionId,
                success = success,
                message = success ? $"Spaceport built at ({tri.Q}, {tri.R})." : "Failed to place spaceport.",
                error = success ? string.Empty : "Colony at site could not be upgraded.",
                timestamp = DateTime.UtcNow.ToString("o")
            });
            WriteStateObservation();
        }

        private void ExecuteBuildTradeStation(AgentActionDTO action)
        {
            Tri tri = _triMap.GetTriAt(action.q, action.r);
            Unit readyUnit = _triMap.CurrentPlayer?.Units?.FirstOrDefault(u => _triMap.CanEstablishTradeStationWith(u));
            if (tri == null || readyUnit == null || readyUnit.Tri != tri)
            {
                if (readyUnit != null && readyUnit.Tri != null)
                {
                    tri = readyUnit.Tri;
                }
            }

            if (tri == null)
            {
                Unit shipOnTradePoint = _triMap.CurrentPlayer?.Units?.FirstOrDefault(u =>
                    u.ShipType == StarshipType.TradeShip && u.Tri != null && u.Tri.IsTradePoint);
                if (shipOnTradePoint != null)
                {
                    tri = shipOnTradePoint.Tri;
                }
            }

            if (tri == null)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = $"No trade ship ready to establish trade station at ({action.q}, {action.r}).",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            if (_triMap.AnimationIsPlaying)
            {
                _triMap.AnimationIsPlaying = false;
            }

            bool success = _triMap.TryEstablishTradeStationAt(tri);
            WriteResult(new AgentResultDTO
            {
                actionId = action.actionId,
                success = success,
                message = success ? $"Trade station built at ({tri.Q}, {tri.R})." : "Failed to establish trade station.",
                error = success ? string.Empty : "Requirements not met: must have trade ship on dock, freight pods, and free dock.",
                timestamp = DateTime.UtcNow.ToString("o")
            });
            WriteStateObservation();
        }

        private void ExecuteBuildShip(AgentActionDTO action)
        {
            if (!TryParseShipType(action.shipType, out StarshipType shipType))
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = $"Unknown ship type: '{action.shipType}'. Supported: ColonyShip, TradeShip.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            if (!_triMap.CanBuildShip(shipType))
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = $"Cannot build {shipType}: resources or ship limits not met.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            Tri tri = _triMap.GetTriAt(action.q, action.r);
            List<Tri> freePoints = _triMap.GetFreeSpaceportPoints(_triMap.CurrentPlayer)?.ToList();
            if (tri == null || (freePoints != null && !freePoints.Contains(tri)))
            {
                if (freePoints != null && freePoints.Count > 0)
                {
                    tri = freePoints.OrderBy(p => p.Q).First();
                }
            }

            if (tri == null)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = $"No available free spaceport point found to launch {shipType}.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            if (!_triMap.TryBeginShipPlacement(shipType))
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = $"Failed to initiate placement of {shipType}.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            bool success = _triMap.TryPlacePendingShipAt(tri);
            WriteResult(new AgentResultDTO
            {
                actionId = action.actionId,
                success = success,
                message = success ? $"Built {shipType} at ({tri.Q}, {tri.R})." : $"Failed to place {shipType} at ({tri.Q}, {tri.R}).",
                error = success ? string.Empty : "Target site is not an unoccupied spaceport point owned by active player.",
                timestamp = DateTime.UtcNow.ToString("o")
            });
            WriteStateObservation();
        }

        private void ExecuteDeployCapturedShip(AgentActionDTO action)
        {
            if (!_triMap.CanDeployCapturedTradeShip())
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = "Cannot deploy captured trade ship: no captured ships in reserve or limits not met.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            Tri tri = _triMap.GetTriAt(action.q, action.r);
            List<Tri> freePoints = _triMap.GetFreeSpaceportPoints(_triMap.CurrentPlayer)?.ToList();
            if (tri == null || (freePoints != null && !freePoints.Contains(tri)))
            {
                if (freePoints != null && freePoints.Count > 0)
                {
                    tri = freePoints.OrderBy(p => p.Q).First();
                }
            }

            if (tri == null)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = "No available free spaceport point found to launch captured TradeShip.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            if (!_triMap.TryBeginShipPlacement(StarshipType.TradeShip, isCapturedFree: true))
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = "Failed to initiate placement of captured TradeShip.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            bool success = _triMap.TryPlacePendingShipAt(tri);
            WriteResult(new AgentResultDTO
            {
                actionId = action.actionId,
                success = success,
                message = success ? $"Deployed captured TradeShip at ({tri.Q}, {tri.R})." : $"Failed to place captured TradeShip at ({tri.Q}, {tri.R}).",
                error = success ? string.Empty : "Target site is not an unoccupied spaceport point owned by active player.",
                timestamp = DateTime.UtcNow.ToString("o")
            });
            WriteStateObservation();
        }

        private void ExecuteBuildUpgrade(AgentActionDTO action)
        {
            if (!TryParseUpgrade(action.upgradeType, out FlagshipUpgrade upgrade))
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = $"Unknown upgrade: '{action.upgradeType}'. Supported: Booster, Cannon, FreightPod.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            bool success = _triMap.TryBuildUpgrade(upgrade);
            WriteResult(new AgentResultDTO
            {
                actionId = action.actionId,
                success = success,
                message = success ? $"Upgraded flagship: {upgrade}." : "Failed to build upgrade.",
                error = success ? string.Empty : "Requirements not met: check resources or maximum upgrades reached.",
                timestamp = DateTime.UtcNow.ToString("o")
            });
            WriteStateObservation();
        }

        private void ExecuteTradeWithSupply(AgentActionDTO action)
        {
            if (!TryParseResourceType(action.resource, out ResourceType giveRes) ||
                !TryParseResourceType(action.targetResource, out ResourceType getRes))
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = $"Invalid resource names: resource='{action.resource}', targetResource='{action.targetResource}'.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            bool success = _triMap.TryTradeWithSupply(giveRes, getRes);
            WriteResult(new AgentResultDTO
            {
                actionId = action.actionId,
                success = success,
                message = success ? $"Traded {giveRes} for {getRes} with supply." : "Supply trade failed.",
                error = success ? string.Empty : "Cannot trade for same resource or insufficient resource cards.",
                timestamp = DateTime.UtcNow.ToString("o")
            });
            WriteStateObservation();
        }

        private void ExecuteTradeWithPlayer(AgentActionDTO action)
        {
            if (_triMap.Players == null || action.partnerIndex < 0 || action.partnerIndex >= _triMap.Players.Length)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = $"Invalid partner index: {action.partnerIndex}.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            Player partner = _triMap.Players[action.partnerIndex];
            var offer = new Dictionary<ResourceType, int>
            {
                { ResourceType.Ore, action.ore },
                { ResourceType.Fuel, action.fuel },
                { ResourceType.Carbon, action.carbon },
                { ResourceType.Food, action.food },
                { ResourceType.TradeGoods, action.tradeGoods }
            };

            var request = new Dictionary<ResourceType, int>();
            if (TryParseResourceType(action.targetResource, out ResourceType reqRes))
            {
                request[reqRes] = 1;
            }

            bool success = _triMap.TryTradeWithPlayer(partner, offer, request);
            WriteResult(new AgentResultDTO
            {
                actionId = action.actionId,
                success = success,
                message = success ? $"Executed player trade with {partner.PlayerName}." : "Player trade rejected.",
                error = success ? string.Empty : "Players lack offered/requested cards or invalid trade amounts.",
                timestamp = DateTime.UtcNow.ToString("o")
            });
            WriteStateObservation();
        }

        private void ExecuteSelectEncounterOutcome(AgentActionDTO action)
        {
            if (!_triMap.EncounterPending || _triMap.CurrentEncounterCard == null)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = "No encounter is currently pending.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            try
            {
                if (_triMap.CurrentEncounterPhase == EncounterCardPhase.ApplyOutcome)
                {
                    WriteResult(new AgentResultDTO
                    {
                        actionId = action.actionId,
                        success = true,
                        message = $"Encounter outcome already applied; awaiting confirmation.",
                        timestamp = DateTime.UtcNow.ToString("o")
                    });
                    WriteStateObservation();
                    return;
                }

                if (_triMap.CurrentEncounterPhase == EncounterCardPhase.ReadPrompt)
                {
                    _triMap.AdvanceEncounterAfterPlayerResponse();
                }

                if (action.hasTargetUnit)
                {
                    _triMap.PendingBlockedShipCoord = new Vector2Int(action.targetUnitQ, action.targetUnitR);
                    _triMap.PendingSpaceJumpShipCoord = new Vector2Int(action.targetUnitQ, action.targetUnitR);
                }

                if (action.ore > 0 || action.fuel > 0 || action.carbon > 0 || action.food > 0 || action.tradeGoods > 0)
                {
                    var dict = new Dictionary<ResourceType, int>();
                    if (action.ore > 0) dict[ResourceType.Ore] = action.ore;
                    if (action.fuel > 0) dict[ResourceType.Fuel] = action.fuel;
                    if (action.carbon > 0) dict[ResourceType.Carbon] = action.carbon;
                    if (action.food > 0) dict[ResourceType.Food] = action.food;
                    if (action.tradeGoods > 0) dict[ResourceType.TradeGoods] = action.tradeGoods;
                    _triMap.PendingEncounterDonatedResources = dict;
                    _triMap.PendingEncounterDonationAmount = dict.Values.Sum();
                    _triMap.PendingEncounterRewardResources = dict;
                }

                _triMap.SelectEncounterOutcome(action.outcomeIndex);
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = true,
                    message = $"Selected encounter outcome index: {action.outcomeIndex}.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
            }
            catch (Exception ex)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow.ToString("o")
                });
            }
            WriteStateObservation();
        }

        private void ExecuteAdvanceEncounter(AgentActionDTO action)
        {
            if (!_triMap.EncounterPending)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = "No encounter pending.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            try
            {
                if (_triMap.CurrentEncounterPhase == EncounterCardPhase.ReadPrompt)
                {
                    _triMap.AdvanceEncounterAfterPlayerResponse();
                }
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = true,
                    message = "Advanced encounter sequence.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
            }
            catch (Exception ex)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow.ToString("o")
                });
            }
            WriteStateObservation();
        }

        private void ExecuteConfirmEncounter(AgentActionDTO action)
        {
            if (!_triMap.EncounterPending)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = true,
                    message = "Encounter already confirmed and closed.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                WriteStateObservation();
                return;
            }

            try
            {
                if (_triMap.CurrentEncounterPhase == EncounterCardPhase.ReadPrompt)
                {
                    _triMap.AdvanceEncounterAfterPlayerResponse();
                    int targetIndex = _triMap.CurrentEncounterCard != null && _triMap.CurrentEncounterCard.Outcomes != null && _triMap.CurrentEncounterCard.Outcomes.Count > 0 ? 0 : 0;
                    _triMap.SelectEncounterOutcome(targetIndex);
                }
                else if (_triMap.CurrentEncounterPhase == EncounterCardPhase.SelectOutcome)
                {
                    int targetIndex = _triMap.CurrentEncounterCard != null && _triMap.CurrentEncounterCard.Outcomes != null && _triMap.CurrentEncounterCard.Outcomes.Count > 0 ? 0 : 0;
                    _triMap.SelectEncounterOutcome(targetIndex);
                }

                _triMap.ConfirmEncounterResolved();
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = true,
                    message = "Confirmed and closed encounter.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
            }
            catch (Exception ex)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow.ToString("o")
                });
            }
            WriteStateObservation();
        }

        private void ExecuteSpaceJump(AgentActionDTO action)
        {
            try
            {
                Tri target = _triMap.GetTriAt(action.q, action.r);
                bool ok = _triMap.TryExecuteSpaceJumpAt(target);
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = ok,
                    message = ok ? $"Space jump executed to ({action.q}, {action.r})." : "Failed to execute space jump to specified coordinates.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
            }
            catch (Exception ex)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow.ToString("o")
                });
            }
            WriteStateObservation();
        }

        private void ExecuteCancelSpaceJump(AgentActionDTO action)
        {
            try
            {
                _triMap.CancelSpaceJump();
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = true,
                    message = "Space jump cancelled.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
            }
            catch (Exception ex)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow.ToString("o")
                });
            }
            WriteStateObservation();
        }

        private void ExecuteTriggerDebugEncounter(AgentActionDTO action)
        {
            try
            {
                int cardNum = action.outcomeIndex > 0 ? action.outcomeIndex : 26;
                bool ok = _triMap.TriggerDebugEncounter(cardNum);
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = ok,
                    message = ok ? $"Triggered debug encounter card #{cardNum}." : "Debug encounter actions are disabled in release mode without debug flags.",
                    error = ok ? string.Empty : "Debug encounter actions are disabled in release mode.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
            }
            catch (Exception ex)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow.ToString("o")
                });
            }
            WriteStateObservation();
        }

        private void ExecuteSevenRollDiscard(AgentActionDTO action)
        {
            DiscardCardsDialogUI dialog = DiscardDialog;
            if (dialog == null || !dialog.IsActive)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = true,
                    message = "Discard dialog is not currently active (already resolved).",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                WriteStateObservation();
                return;
            }

            var toDiscard = new Dictionary<ResourceType, int>
            {
                { ResourceType.Ore, action.ore },
                { ResourceType.Fuel, action.fuel },
                { ResourceType.Carbon, action.carbon },
                { ResourceType.Food, action.food },
                { ResourceType.TradeGoods, action.tradeGoods }
            };

            bool success = dialog.TrySubmitExternalDiscard(toDiscard);
            WriteResult(new AgentResultDTO
            {
                actionId = action.actionId,
                success = success,
                message = success ? "Discard submitted successfully." : "Failed to discard specified cards.",
                error = success ? string.Empty : $"Discard must match required count ({dialog.RequiredDiscardCount}) and player hand cards.",
                timestamp = DateTime.UtcNow.ToString("o")
            });
            WriteStateObservation();
        }

        private void ExecuteSevenRollSteal(AgentActionDTO action)
        {
            StealCardDialogUI dialog = StealDialog;
            if (dialog == null || !dialog.IsActive)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = true,
                    message = "Steal dialog is not currently active (already resolved).",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                WriteStateObservation();
                return;
            }

            Player target = null;
            if (_triMap.Players != null && action.partnerIndex >= 0 && action.partnerIndex < _triMap.Players.Length)
            {
                target = _triMap.Players[action.partnerIndex];
            }

            if (target == null && dialog.CandidateTargets != null && dialog.CandidateTargets.Count > 0)
            {
                target = dialog.CandidateTargets[0];
            }

            if (target == null)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = $"No valid steal target available (requested partnerIndex: {action.partnerIndex}).",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            bool success = dialog.TrySelectTarget(target);
            WriteResult(new AgentResultDTO
            {
                actionId = action.actionId,
                success = success,
                message = success ? $"Selected steal target: {target.PlayerName}." : "Failed to select steal target.",
                timestamp = DateTime.UtcNow.ToString("o")
            });
            WriteStateObservation();
        }

        private void ExecuteSelectFriendshipCard(AgentActionDTO action)
        {
            FriendshipCardDialogUI dialog = FriendshipDialog;

            if (_triMap != null && _triMap.FriendshipCardSelectionPending && _triMap.PendingFriendshipCards != null)
            {
                if (action.cardIndex < 0 || action.cardIndex >= _triMap.PendingFriendshipCards.Count)
                {
                    WriteResult(new AgentResultDTO
                    {
                        actionId = action.actionId,
                        success = false,
                        error = $"Card index {action.cardIndex} out of range (available: {_triMap.PendingFriendshipCards.Count}).",
                        timestamp = DateTime.UtcNow.ToString("o")
                    });
                    return;
                }

                FriendshipCard card = _triMap.PendingFriendshipCards[action.cardIndex];
                bool success = _triMap.SelectFriendshipCard(_triMap.PendingFriendshipCiv, _triMap.PendingFriendshipPlayer, card.Id);
                if (dialog != null && dialog.IsActive)
                {
                    dialog.Hide();
                }
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = success,
                    message = success ? $"Selected friendship card index: {action.cardIndex} ({card.GetName()})." : "Failed to select friendship card.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                WriteStateObservation();
                return;
            }

            if (dialog == null || !dialog.IsActive)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = "Friendship card dialog is not currently active.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            bool dlgSuccess = dialog.TrySelectCard(action.cardIndex);
            WriteResult(new AgentResultDTO
            {
                actionId = action.actionId,
                success = dlgSuccess,
                message = dlgSuccess ? $"Selected friendship card index: {action.cardIndex}." : "Failed to select friendship card.",
                error = dlgSuccess ? string.Empty : $"Card index {action.cardIndex} out of range (available: {dialog.AvailableCards?.Count ?? 0}).",
                timestamp = DateTime.UtcNow.ToString("o")
            });
            WriteStateObservation();
        }

        private void ExecuteBuyFame(AgentActionDTO action)
        {
            Player current = _triMap?.CurrentPlayer;
            if (current == null || current.Type != Player.PlayerType.LOCAL)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = "Active player is not local.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            bool hasCard = current.HasFriendshipCard(FriendshipCardId.DiplomatBoughtFame1) ||
                           current.HasFriendshipCard(FriendshipCardId.DiplomatBoughtFame2);
            if (!hasCard || current.HasUsedBoughtFameThisTurn || current.GetResourceCount(ResourceType.TradeGoods) < 1)
            {
                WriteResult(new AgentResultDTO
                {
                    actionId = action.actionId,
                    success = false,
                    error = "Cannot use Bought Fame: card missing, already used this turn, or insufficient TradeGoods.",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            current.RemoveResource(ResourceType.TradeGoods, 1);
            current.AddHalfMedal(1);
            current.HasUsedBoughtFameThisTurn = true;
            WriteResult(new AgentResultDTO
            {
                actionId = action.actionId,
                success = true,
                message = "Used Bought Fame: paid 1 TradeGood for +1 half-medal.",
                timestamp = DateTime.UtcNow.ToString("o")
            });
            WriteStateObservation();
        }

        #endregion

        #region State Observation Export

        public void RequestStateExport() => WriteStateObservation(force: true);

        public void WriteStateObservation(bool force = false)
        {
            if (_triMap == null) return;

            float now = Time.unscaledTime;
            if (!force && (now - _lastStateWriteTime) < MinStateWriteInterval)
            {
                _isStateDirty = true;
                return;
            }

            _stepCount++;
            _lastStateWriteTime = now;
            _isStateDirty = false;

            GameStateObservationDTO obs = BuildObservation();
            if (IsFileBridgeEnabled)
            {
                string json = JsonUtility.ToJson(obs, false);
                SafeWriteJsonFile(_stateFilePath, json);
            }
            OnStateObservationChanged?.Invoke(obs);
        }

        private void WriteResult(AgentResultDTO result)
        {
            if (IsFileBridgeEnabled)
            {
                string json = JsonUtility.ToJson(result, false);
                SafeWriteJsonFile(_resultFilePath, json);
            }
            OnActionResult?.Invoke(result);
        }

        private void SafeWriteJsonFile(string destinationPath, string json)
        {
            if (!IsFileBridgeEnabled || string.IsNullOrEmpty(destinationPath) || string.IsNullOrEmpty(json)) return;
            lock (_fileWriteLock)
            {
                if (destinationPath == _stateFilePath)
                {
                    _pendingStateJson = json;
                }
                else if (destinationPath == _resultFilePath)
                {
                    _pendingResultJson = json;
                }
            }
            _fileWriteEvent.Set();
        }

        public GameStateObservationDTO BuildObservation()
        {
            Player winner = _triMap.Winner;
            int winnerIdx = -1;
            if (_triMap.Players != null && winner != null)
            {
                winnerIdx = Array.IndexOf(_triMap.Players, winner);
            }

            List<PlayerStateDTO> playerDTOs = new List<PlayerStateDTO>();
            if (_triMap.Players != null)
            {
                for (int i = 0; i < _triMap.Players.Length; i++)
                {
                    playerDTOs.Add(PlayerStateDTO.FromPlayer(_triMap.Players[i], i, _triMap));
                }
            }

            // Dialogs
            DiscardCardsDialogUI activeDiscard = DiscardDialog;
            StealCardDialogUI activeSteal = StealDialog;
            FriendshipCardDialogUI activeFriendship = FriendshipDialog;

            bool isFriendshipActive = (_triMap != null && _triMap.FriendshipCardSelectionPending) || (activeFriendship != null && activeFriendship.IsActive);
            int friendshipPlayerIdx = -1;
            string friendshipCivStr = string.Empty;
            List<FriendshipCardOptionDTO> friendshipCards = new List<FriendshipCardOptionDTO>();

            if (_triMap != null && _triMap.FriendshipCardSelectionPending && _triMap.PendingFriendshipCards != null)
            {
                friendshipPlayerIdx = (_triMap.Players != null && _triMap.PendingFriendshipPlayer != null)
                    ? Array.IndexOf(_triMap.Players, _triMap.PendingFriendshipPlayer)
                    : -1;
                friendshipCivStr = _triMap.PendingFriendshipCiv.ToString();
                for (int i = 0; i < _triMap.PendingFriendshipCards.Count; i++)
                {
                    var c = _triMap.PendingFriendshipCards[i];
                    friendshipCards.Add(new FriendshipCardOptionDTO
                    {
                        index = i,
                        cardId = c != null ? c.Id.ToString() : string.Empty,
                        name = c != null ? c.GetName() : string.Empty,
                        description = c != null ? c.GetDescription() : string.Empty
                    });
                }
            }
            else if (activeFriendship != null && activeFriendship.IsActive && activeFriendship.AvailableCards != null)
            {
                friendshipPlayerIdx = _triMap?.CurrentPlayerIndex ?? -1;
                friendshipCivStr = activeFriendship.CurrentCiv.ToString();
                for (int i = 0; i < activeFriendship.AvailableCards.Count; i++)
                {
                    var c = activeFriendship.AvailableCards[i];
                    friendshipCards.Add(new FriendshipCardOptionDTO
                    {
                        index = i,
                        cardId = c != null ? c.Id.ToString() : string.Empty,
                        name = c != null ? c.GetName() : string.Empty,
                        description = c != null ? c.GetDescription() : string.Empty
                    });
                }
            }

            DialogStateDTO dialogState;
            if (Networking.NetworkManager.Instance != null &&
                Networking.NetworkManager.Instance.IsClientConnected &&
                !Networking.NetworkManager.Instance.IsHost &&
                _triMap.NetworkDialogs != null)
            {
                dialogState = _triMap.NetworkDialogs;
            }
            else
            {
                dialogState = new DialogStateDTO
                {
                    isSevenRollResolving = _triMap.IsResolvingSevenRoll || (activeDiscard != null && activeDiscard.IsActive) || (activeSteal != null && activeSteal.IsActive),
                    isDiscardDialogOpen = activeDiscard != null && activeDiscard.IsActive,
                    discardPlayerIndex = (activeDiscard != null && activeDiscard.IsActive && activeDiscard.CurrentDiscardPlayer != null && _triMap.Players != null)
                        ? Array.IndexOf(_triMap.Players, activeDiscard.CurrentDiscardPlayer)
                        : -1,
                    discardRequiredCount = (activeDiscard != null && activeDiscard.IsActive) ? activeDiscard.RequiredDiscardCount : 0,
                    isStealDialogOpen = activeSteal != null && activeSteal.IsActive,
                    eligibleStealTargetIndices = (activeSteal != null && activeSteal.IsActive && activeSteal.CandidateTargets != null && _triMap.Players != null)
                        ? activeSteal.CandidateTargets.Select(t => Array.IndexOf(_triMap.Players, t)).ToArray()
                        : new int[0],
                    isFriendshipCardDialogOpen = isFriendshipActive,
                    friendshipPlayerIndex = friendshipPlayerIdx,
                    friendshipCivilization = friendshipCivStr,
                    friendshipCardOptions = friendshipCards.ToArray(),
                    isGameOverDialogOpen = _triMap.IsGameOver
                };
            }

            // Board sites
            if (_cachedSites == null || _cachedSites.Count == 0)
            {
                InitializeSiteCache();
            }

            if (_cachedSites != null)
            {
                for (int i = 0; i < _cachedSites.Count; i++)
                {
                    UpdateBoardSiteDTO(_cachedSites[i]);
                }
            }

            int candidateIdx = -1;
            if (_triMap.CurrentFoundingPhase == FoundingPhase.RollForStartingPlayer)
            {
                candidateIdx = _triMap.StartingPlayerCandidateIndex;
            }

            int activePlayerIdx = (_triMap.CurrentFoundingPhase == FoundingPhase.RollForStartingPlayer && candidateIdx >= 0)
                ? candidateIdx
                : _triMap.CurrentPlayerIndex;
            string activePlayerName = (activePlayerIdx >= 0 && _triMap.Players != null && activePlayerIdx < _triMap.Players.Length && _triMap.Players[activePlayerIdx] != null)
                ? _triMap.Players[activePlayerIdx].PlayerName
                : (_triMap.CurrentPlayer?.PlayerName ?? string.Empty);

            // Legal action hints (filtered for external agent controlling LOCAL player)
            List<string> legal = ComputeLegalActions();
            int localIdx = GetLocalPlayerIndex();
            bool isCurrentLocal = (localIdx >= 0)
                ? (activePlayerIdx == localIdx)
                : (_triMap.CurrentPlayer != null && _triMap.CurrentPlayer.Type == Player.PlayerType.LOCAL);
            bool waitingForAgent = legal.Count > 0;

            // Player rankings (sorted by winner first, then victory points, colonies, etc.)
            List<PlayerRankingDTO> rankingList = new List<PlayerRankingDTO>(playerDTOs != null ? playerDTOs.Count : 0);
            if (playerDTOs != null && playerDTOs.Count > 0)
            {
                for (int i = 0; i < playerDTOs.Count; i++)
                {
                    PlayerStateDTO p = playerDTOs[i];
                    rankingList.Add(new PlayerRankingDTO
                    {
                        rank = 0,
                        playerIndex = p.playerIndex,
                        playerName = p.name,
                        playerType = p.playerType,
                        victoryPoints = p.victoryPoints,
                        isWinner = p.playerIndex == winnerIdx,
                        coloniesCount = p.regularColonies,
                        spaceportsCount = p.spaceportsCount,
                        tradeStationsCount = p.tradeStationsCount,
                        totalResources = p.resources != null ? p.resources.total : 0
                    });
                }

                rankingList.Sort((a, b) =>
                {
                    int winA = a.isWinner ? 1 : 0;
                    int winB = b.isWinner ? 1 : 0;
                    if (winA != winB) return winB.CompareTo(winA);
                    int vpComp = b.victoryPoints.CompareTo(a.victoryPoints);
                    if (vpComp != 0) return vpComp;
                    int colA = a.coloniesCount + a.spaceportsCount;
                    int colB = b.coloniesCount + b.spaceportsCount;
                    return colB.CompareTo(colA);
                });

                for (int i = 0; i < rankingList.Count; i++)
                {
                    rankingList[i].rank = i + 1;
                }
            }

            if (_cachedStarSystems == null || _cachedStarSystems.Count == 0)
            {
                InitializeStarSystemCache();
            }

            if (_cachedStarSystems != null)
            {
                for (int i = 0; i < _cachedStarSystems.Count; i++)
                {
                    var css = _cachedStarSystems[i];
                    css.dto.isRevealed = css.tri.ArePlanetProductionNumbersRevealed;
                    css.dto.productionNumbers = css.tri.PlanetProductionNumbers ?? Array.Empty<int>();
                    css.dto.specialResolved[0] = !css.tri.HasUnresolvedHazard(0);
                    css.dto.specialResolved[1] = !css.tri.HasUnresolvedHazard(1);
                    css.dto.specialResolved[2] = !css.tri.HasUnresolvedHazard(2);
                }
            }

            return new GameStateObservationDTO
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                step = _stepCount,
                boardSeed = _triMap != null ? _triMap.CurrentBoardSeed : 0,
                isGameOver = _triMap.IsGameOver,
                winnerIndex = winnerIdx,
                winnerName = winner?.PlayerName ?? string.Empty,
                foundingPhase = FastFoundingPhaseToString(_triMap.CurrentFoundingPhase),
                foundingRemainingBoosters = _triMap.RemainingStartingDrives,
                foundingRemainingCannons = _triMap.RemainingStartingCannons,
                foundingRemainingFreightPods = _triMap.RemainingStartingCargoModules,
                turnPhase = FastTurnPhaseToString(_triMap.CurrentPhase),
                currentPlayerIndex = activePlayerIdx,
                currentPlayerName = activePlayerName,
                startingPlayerCandidateIndex = candidateIdx,
                turnNumber = _triMap.TurnNumber,
                lastProductionRoll = _triMap.LastProductionRoll?.Total ?? 0,
                lastFlightSpeed = _triMap.LastFlagshipRoll?.Speed ?? 0,
                lastFlightHadBlackBall = _triMap.LastFlagshipRoll?.RequiresEncounter ?? false,
                lastFlightHadBlackCore = _triMap.LastFlagshipRoll?.RequiresEncounter ?? false,
                players = playerDTOs != null ? playerDTOs.ToArray() : Array.Empty<PlayerStateDTO>(),
                encounter = (Networking.NetworkManager.Instance != null &&
                             Networking.NetworkManager.Instance.IsClientConnected &&
                             !Networking.NetworkManager.Instance.IsHost &&
                             _triMap.NetworkEncounter != null)
                            ? _triMap.NetworkEncounter
                            : EncounterStateDTO.FromTriMap(_triMap),
                dialogs = dialogState,
                board = new BoardStateDTO
                {
                    sites = _cachedSiteArray ?? Array.Empty<BoardSiteDTO>(),
                    starSystems = _cachedStarSystemArray ?? Array.Empty<StarSystemStateDTO>()
                },
                legalActions = legal.ToArray(),
                localPlayerIndex = localIdx,
                isCurrentPlayerLocal = isCurrentLocal,
                isWaitingForAgent = waitingForAgent,
                rankings = rankingList.ToArray()
            };
        }

        private Player GetLocalPlayer()
        {
            if (Networking.NetworkManager.Instance != null &&
                Networking.NetworkManager.Instance.IsClientConnected &&
                Networking.NetworkManager.Instance.LocalPlayerIndex >= 0 &&
                _triMap?.Players != null &&
                Networking.NetworkManager.Instance.LocalPlayerIndex < _triMap.Players.Length)
            {
                return _triMap.Players[Networking.NetworkManager.Instance.LocalPlayerIndex];
            }

            if (_triMap?.Players == null) return null;
            return _triMap.Players.FirstOrDefault(p => p.Type == Player.PlayerType.LOCAL);
        }

        private int GetLocalPlayerIndex()
        {
            if (Networking.NetworkManager.Instance != null &&
                Networking.NetworkManager.Instance.IsClientConnected &&
                Networking.NetworkManager.Instance.LocalPlayerIndex >= 0)
            {
                return Networking.NetworkManager.Instance.LocalPlayerIndex;
            }

            Player local = GetLocalPlayer();
            if (local == null || _triMap?.Players == null) return -1;
            return Array.IndexOf(_triMap.Players, local);
        }

        private bool IsPlayerLocal(Player p)
        {
            if (p == null || _triMap?.Players == null) return false;
            int localIdx = GetLocalPlayerIndex();
            if (localIdx >= 0 && localIdx < _triMap.Players.Length)
            {
                return _triMap.Players[localIdx] == p;
            }
            return p.Type == Player.PlayerType.LOCAL;
        }

        private bool IsCurrentPlayerLocal()
        {
            return _triMap != null && IsPlayerLocal(_triMap.CurrentPlayer);
        }

        public void ClearSiteCache()
        {
            _cachedSites = null;
            _cachedSiteArray = null;
            _cachedStarSystems = null;
            _cachedStarSystemArray = null;
        }

        public void UpdateFromObservation(GameStateObservationDTO obs)
        {
            if (obs == null) return;
            if (IsFileBridgeEnabled)
            {
                string json = JsonUtility.ToJson(obs, false);
                SafeWriteJsonFile(_stateFilePath, json);
            }
            OnStateObservationChanged?.Invoke(obs);
        }

        private void InitializeSiteCache()
        {
            if (_triMap == null || _triMap.AllTris == null) return;
            _cachedSites = new List<CachedSiteMetadata>();

            foreach (Tri tri in _triMap.AllTris)
            {
                if (!tri.IsColonyPoint && !tri.IsTradePoint && (tri.Colony == null || !tri.Colony.IsSpaceport))
                    continue;

                List<Tri> adjacentSystems = new List<Tri>();
                if (tri.AdjacentPlanetSystems != null && tri.AdjacentPlanetSystems.Length > 0)
                {
                    for (int i = 0; i < tri.AdjacentPlanetSystems.Length; i++)
                    {
                        var s = tri.AdjacentPlanetSystems[i];
                        if (s != null && !adjacentSystems.Contains(s)) adjacentSystems.Add(s);
                    }
                }
                else if (tri.GetAllNeighbours() != null)
                {
                    foreach (var n in tri.GetAllNeighbours())
                    {
                        if (n is Tri pt && pt.SystemKind == SystemKind.Planets && !adjacentSystems.Contains(pt))
                        {
                            adjacentSystems.Add(pt);
                        }
                    }
                }

                List<CachedSiteMetadata.AdjPlanet> planets = new List<CachedSiteMetadata.AdjPlanet>();
                for (int s = 0; s < adjacentSystems.Count; s++)
                {
                    Tri sys = adjacentSystems[s];
                    int[] planetIndices = sys.GetAdjacentPlanetIndicesForIntersection(tri);
                    if (planetIndices != null)
                    {
                        for (int i = 0; i < planetIndices.Length; i++)
                        {
                            planets.Add(new CachedSiteMetadata.AdjPlanet { system = sys, planetIndex = planetIndices[i] });
                        }
                    }
                }

                _cachedSites.Add(new CachedSiteMetadata
                {
                    tri = tri,
                    q = tri.Q,
                    r = tri.R,
                    isColonySite = tri.IsColonyPoint,
                    isTradeSite = tri.IsTradePoint,
                    tradeCivilization = tri.AlienCivilization != AlienCivilization.None ? PlayerStateDTO.FastAlienCivilizationToString(tri.AlienCivilization) : string.Empty,
                    adjPlanets = planets.ToArray(),
                    dto = new BoardSiteDTO()
                });
            }

            _cachedSiteArray = new BoardSiteDTO[_cachedSites.Count];
            for (int i = 0; i < _cachedSites.Count; i++)
            {
                _cachedSiteArray[i] = _cachedSites[i].dto;
            }
        }

        private void InitializeStarSystemCache()
        {
            if (_triMap == null || _triMap.StarSystemViews == null) return;
            _cachedStarSystems = new List<CachedStarSystem>();

            foreach (var kvp in _triMap.StarSystemViews)
            {
                Tri tri = kvp.Key;
                if (tri == null) continue;

                string[] resNames;
                if (tri.PlanetResources != null)
                {
                    resNames = new string[tri.PlanetResources.Length];
                    for (int r = 0; r < tri.PlanetResources.Length; r++)
                    {
                        resNames[r] = FastResourceTypeToString(tri.PlanetResources[r]);
                    }
                }
                else
                {
                    resNames = Array.Empty<string>();
                }

                string[] specTypes = new string[3];
                int[] reqs = new int[3];
                for (int s = 0; s < 3; s++)
                {
                    specTypes[s] = tri.GetPlanetSpecialType(s).ToString();
                    reqs[s] = tri.GetPlanetSpecialRequirement(s);
                }

                bool[] specResolved = new bool[3];
                specResolved[0] = !tri.HasUnresolvedHazard(0);
                specResolved[1] = !tri.HasUnresolvedHazard(1);
                specResolved[2] = !tri.HasUnresolvedHazard(2);

                var dto = new StarSystemStateDTO
                {
                    q = tri.Q,
                    r = tri.R,
                    systemType = tri.SystemType.ToString(),
                    systemKind = tri.SystemKind.ToString(),
                    isRevealed = tri.ArePlanetProductionNumbersRevealed,
                    productionNumbers = tri.PlanetProductionNumbers ?? Array.Empty<int>(),
                    resources = resNames,
                    specialTypes = specTypes,
                    specialRequirements = reqs,
                    specialResolved = specResolved,
                    alienCivilization = PlayerStateDTO.FastAlienCivilizationToString(tri.AlienCivilization)
                };

                _cachedStarSystems.Add(new CachedStarSystem { tri = tri, dto = dto });
            }

            _cachedStarSystemArray = new StarSystemStateDTO[_cachedStarSystems.Count];
            for (int i = 0; i < _cachedStarSystems.Count; i++)
            {
                _cachedStarSystemArray[i] = _cachedStarSystems[i].dto;
            }
        }

        private void UpdateBoardSiteDTO(CachedSiteMetadata meta)
        {
            Tri tri = meta.tri;
            int owner = -1;
            bool occupied = false;
            bool isSpaceport = false;

            if (tri.Colony != null && _triMap.Players != null)
            {
                occupied = true;
                isSpaceport = tri.Colony.IsSpaceport;
                for (int i = 0; i < _triMap.Players.Length; i++)
                {
                    if (_triMap.Players[i].Colonies != null && _triMap.Players[i].Colonies.Contains(tri.Colony))
                    {
                        owner = i;
                        break;
                    }
                }
            }
            else if (tri.TradeStations != null && tri.TradeStations.Length > 0 && _triMap.Players != null)
            {
                int stationCount = 0;
                TradeStation firstStation = null;
                for (int s = 0; s < tri.TradeStations.Length; s++)
                {
                    if (tri.TradeStations[s] != null)
                    {
                        stationCount++;
                        if (firstStation == null) firstStation = tri.TradeStations[s];
                    }
                }
                occupied = stationCount >= 5;
                if (firstStation != null)
                {
                    for (int i = 0; i < _triMap.Players.Length; i++)
                    {
                        if (_triMap.Players[i].TradeStations != null && _triMap.Players[i].TradeStations.Contains(firstStation))
                        {
                            owner = i;
                            break;
                        }
                    }
                }
            }
            else if (tri.Units != null && tri.Units.Length > 0)
            {
                occupied = true;
            }

            bool blocked = meta.isColonySite && _triMap.IsColonySiteBlockedByHazard(tri);
            string hazardKind = "None";
            int hazardReq = 0;
            bool canResolve = false;
            Player localPlayer = GetLocalPlayer();

            _reusableNumbers.Clear();
            _reusableResources.Clear();
            int totalPips = 0;

            if (meta.adjPlanets != null)
            {
                for (int pIdx = 0; pIdx < meta.adjPlanets.Length; pIdx++)
                {
                    Tri sys = meta.adjPlanets[pIdx].system;
                    int idx = meta.adjPlanets[pIdx].planetIndex;
                    if (sys == null) continue;

                    if (sys.HasUnresolvedHazard(idx))
                    {
                        PlanetSpecialType specialType = sys.GetPlanetSpecialType(idx);
                        int req = sys.GetPlanetSpecialRequirement(idx);
                        if (specialType == PlanetSpecialType.IcePlanet)
                        {
                            hazardKind = "IcePlanet";
                            hazardReq = req;
                        }
                        else if (specialType == PlanetSpecialType.MarauderBase)
                        {
                            hazardKind = "MarauderBase";
                            hazardReq = req;
                        }
                    }
                    else
                    {
                        if (sys.ArePlanetProductionNumbersRevealed && sys.PlanetProductionNumbers != null && idx < sys.PlanetProductionNumbers.Length)
                        {
                            int num = sys.PlanetProductionNumbers[idx];
                            if (num >= 2 && num <= 12 && num != 7)
                            {
                                _reusableNumbers.Add(num);
                                int pips = 6 - Math.Abs(7 - num);
                                totalPips += pips;
                            }
                        }
                        if (sys.PlanetResources != null && idx < sys.PlanetResources.Length)
                        {
                            _reusableResources.Add(FastResourceTypeToString(sys.PlanetResources[idx]));
                        }
                    }
                }
            }

            if (blocked && hazardKind == "None")
            {
                hazardKind = "Hazard";
            }

            if (blocked && localPlayer != null)
            {
                canResolve = _triMap.CanPlayerResolveColonySiteHazards(localPlayer, tri);
            }

            bool discovered = true;
            if (tri.SystemKind == SystemKind.Planets)
            {
                discovered = tri.ArePlanetProductionNumbersRevealed;
            }
            else if (tri.AdjacentPlanetSystems != null && tri.AdjacentPlanetSystems.Length > 0)
            {
                for (int s = 0; s < tri.AdjacentPlanetSystems.Length; s++)
                {
                    if (tri.AdjacentPlanetSystems[s] != null && !tri.AdjacentPlanetSystems[s].ArePlanetProductionNumbersRevealed)
                    {
                        discovered = false;
                        break;
                    }
                }
            }

            BoardSiteDTO dto = meta.dto;
            dto.q = meta.q;
            dto.r = meta.r;
            dto.isColonyPoint = meta.isColonySite;
            dto.isTradePoint = meta.isTradeSite;
            dto.isSpaceportPoint = isSpaceport;
            dto.isOccupied = occupied;
            dto.ownerIndex = owner;
            dto.folkType = meta.tradeCivilization;
            dto.isBlockedByHazard = blocked;
            dto.hazardKind = hazardKind;
            dto.hazardStrength = hazardReq;
            dto.canResolveHazard = canResolve;
            dto.isDiscovered = discovered;
            dto.productionProbability = totalPips;
            dto.tradeStationCount = meta.isTradeSite && tri.TradeStations != null ? tri.TradeStations.Count(s => s != null) : 0;
            dto.adjacentNumbers = _reusableNumbers.Count == 0 ? Array.Empty<int>() : _reusableNumbers.ToArray();
            dto.adjacentResources = _reusableResources.Count == 0 ? Array.Empty<string>() : _reusableResources.ToArray();
        }

        private List<string> ComputeLegalActions()
        {
            var list = new List<string>();
            if (_triMap == null || _triMap.IsGameOver) return list;

            if (_triMap.IsGameSetupActive)
            {
                if (Networking.NetworkManager.Instance != null &&
                    Networking.NetworkManager.Instance.IsClientConnected)
                {
                    // In a network session, game start is controlled by the lobby / -autostart,
                    // not prematurely triggered by an autonomous bot before remote players connect.
                    return list;
                }
                list.Add("StartGame");
                return list;
            }

            // 1. Modal dialogs: Friendship card and Seven-roll dialogs
            DialogStateDTO netDlg = (Networking.NetworkManager.Instance != null &&
                                     Networking.NetworkManager.Instance.IsClientConnected &&
                                     !Networking.NetworkManager.Instance.IsHost)
                                    ? _triMap.NetworkDialogs
                                    : null;

            FriendshipCardDialogUI friendshipDlg = FriendshipDialog;
            bool isFriendshipActive = (_triMap != null && _triMap.FriendshipCardSelectionPending) || (friendshipDlg != null && friendshipDlg.IsActive) || (netDlg != null && netDlg.isFriendshipCardDialogOpen);
            if (isFriendshipActive)
            {
                if (IsCurrentPlayerLocal())
                {
                    list.Add("SelectFriendshipCard");
                }
                return list;
            }

            DiscardCardsDialogUI discardDlg = DiscardDialog;
            StealCardDialogUI stealDlg = StealDialog;

            bool isDiscardActive = (discardDlg != null && discardDlg.IsActive) || (netDlg != null && netDlg.isDiscardDialogOpen);
            bool isStealActive = (stealDlg != null && stealDlg.IsActive) || (netDlg != null && netDlg.isStealDialogOpen);
            bool isSevenRollResolving = _triMap.IsResolvingSevenRoll || (netDlg != null && netDlg.isSevenRollResolving) || isDiscardActive || isStealActive;

            if (isSevenRollResolving)
            {
                if (isDiscardActive)
                {
                    int discardPIdx = (discardDlg != null && discardDlg.IsActive && discardDlg.CurrentDiscardPlayer != null && _triMap.Players != null)
                        ? Array.IndexOf(_triMap.Players, discardDlg.CurrentDiscardPlayer)
                        : (netDlg != null ? netDlg.discardPlayerIndex : -1);

                    int localPIdx = GetLocalPlayerIndex();
                    if (discardPIdx >= 0 && discardPIdx == localPIdx)
                    {
                        list.Add("SevenRollDiscard");
                    }
                }

                if (isStealActive && IsCurrentPlayerLocal())
                {
                    list.Add("SevenRollSteal");
                }

                return list;
            }

            // 2. Founding phase: only provide actions if the current candidate/player is LOCAL
            if (_triMap.CurrentFoundingPhase != FoundingPhase.Complete)
            {
                if (_triMap.CurrentFoundingPhase == FoundingPhase.RollForStartingPlayer)
                {
                    if (IsPlayerLocal(_triMap.CurrentStartingPlayerCandidate))
                    {
                        list.Add("RollDice");
                    }
                    return list;
                }

                if (!IsCurrentPlayerLocal())
                {
                    return list;
                }

                switch (_triMap.CurrentFoundingPhase)
                {
                    case FoundingPhase.PlaceFirstColony:
                    case FoundingPhase.PlaceSecondColony:
                    case FoundingPhase.PlaceThirdColony:
                        list.Add("PlaceFoundingColony");
                        break;
                    case FoundingPhase.SelectSpaceport:
                        list.Add("SelectFoundingSpaceport");
                        break;
                    case FoundingPhase.SelectStartingShip:
                        list.Add("SelectStartingShip");
                        break;
                    case FoundingPhase.SelectStartingShipPoint:
                        list.Add("SelectFoundingShipPoint");
                        break;
                    case FoundingPhase.SelectFlagshipUpgrade:
                        list.Add("SelectStartingUpgrade");
                        break;
                }
                return list;
            }

            // 3. Regular turns: if current player is not local to this instance, wait
            if (!IsCurrentPlayerLocal())
            {
                return list;
            }

            switch (_triMap.CurrentPhase)
            {
                case GameTurnPhase.Production:
                    if (_triMap.LastProductionRoll == null) list.Add("RollDice");
                    else list.Add("AdvancePhase");
                    break;

                case GameTurnPhase.TradeAndBuild:
                    list.Add("AdvancePhase");
                    list.Add("TradeWithSupply");
                    list.Add("TradeWithPlayer");
                    if (_triMap.CurrentPlayer != null && _triMap.CurrentPlayer.Units != null)
                    {
                        if (_triMap.CurrentPlayer.Units.Any(u => _triMap.CanFoundColonyWith(u)))
                            list.Add("FoundColony");
                        if (_triMap.CurrentPlayer.Units.Any(u => _triMap.CanEstablishTradeStationWith(u)))
                            list.Add("BuildTradeStation");
                    }
                    if (_triMap.CanBuildShip(StarshipType.ColonyShip) || _triMap.CanBuildShip(StarshipType.TradeShip))
                        list.Add("BuildShip");
                    if (_triMap.CanDeployCapturedTradeShip())
                        list.Add("DeployCapturedShip");
                    if (_triMap.CanBuildSpaceport())
                        list.Add("BuildSpaceport");
                    if (_triMap.CanBuildUpgrade(FlagshipUpgrade.Drive) ||
                        _triMap.CanBuildUpgrade(FlagshipUpgrade.Cannon) ||
                        _triMap.CanBuildUpgrade(FlagshipUpgrade.CargoModule))
                        list.Add("BuildUpgrade");
                    if (_triMap.CurrentPlayer != null)
                    {
                        bool hasBoughtFame = _triMap.CurrentPlayer.HasFriendshipCard(FriendshipCardId.DiplomatBoughtFame1) ||
                                             _triMap.CurrentPlayer.HasFriendshipCard(FriendshipCardId.DiplomatBoughtFame2);
                        if (hasBoughtFame && !_triMap.CurrentPlayer.HasUsedBoughtFameThisTurn && _triMap.CurrentPlayer.GetResourceCount(ResourceType.TradeGoods) >= 1)
                        {
                            list.Add("BuyFame");
                        }
                    }
                    break;

                case GameTurnPhase.Flight:
                    if (_triMap.LastFlagshipRoll == null)
                    {
                        list.Add("RollFlight");
                    }
                    else if (_triMap.EncounterPending || (_triMap.NetworkEncounter != null && _triMap.NetworkEncounter.isPending))
                    {
                        EncounterCardPhase effPhase = (_triMap.NetworkEncounter != null && System.Enum.TryParse(_triMap.NetworkEncounter.phase, out EncounterCardPhase netP))
                            ? netP
                            : _triMap.CurrentEncounterPhase;

                        if (effPhase == EncounterCardPhase.ReadPrompt)
                        {
                            list.Add("AdvanceEncounter");
                            list.Add("SelectEncounterOutcome");
                        }
                        else if (effPhase == EncounterCardPhase.SelectOutcome)
                        {
                            list.Add("SelectEncounterOutcome");
                        }
                        else if (effPhase == EncounterCardPhase.ApplyOutcome)
                        {
                            list.Add("ConfirmEncounter");
                        }
                    }
                    else
                    {
                        if (_triMap.IsExecutingSpaceJump)
                        {
                            list.Add("ExecuteSpaceJump");
                            list.Add("CancelSpaceJump");
                        }
                        else
                        {
                            list.Add("EndTurn");
                            if (_triMap.CurrentPlayer.Units.Any(u => u.MovementRemaining > 0 && _triMap.CanMoveUnit(u)))
                                list.Add("MoveShip");
                            if (_triMap.CurrentPlayer.Units.Any(u => _triMap.CanFoundColonyWith(u)))
                                list.Add("FoundColony");
                            if (_triMap.CurrentPlayer.Units.Any(u => _triMap.CanEstablishTradeStationWith(u)))
                                list.Add("BuildTradeStation");
                        }
                    }
                    break;
            }

            return list;
        }

        #endregion

        #region Helpers

        private Unit FindUnit(int fromQ, int fromR, int unitId)
        {
            Player current = _triMap.CurrentPlayer;
            if (current == null || current.Units == null) return null;

            if (unitId != 0)
            {
                Unit match = current.Units.FirstOrDefault(u => u.GetHashCode() == unitId);
                if (match != null) return match;
            }

            Unit byCoord = current.Units.FirstOrDefault(u => u.Tri != null && u.Tri.Q == fromQ && u.Tri.R == fromR);
            if (byCoord != null) return byCoord;

            // Fallback for networked clients: if coordinates drifted slightly, pick first movable unit
            return current.Units.FirstOrDefault(u => u.MovementRemaining > 0 && _triMap.CanMoveUnit(u));
        }

        private static bool TryParseResourceType(string name, out ResourceType result)
        {
            result = ResourceType.Ore;
            if (string.IsNullOrEmpty(name)) return false;

            string lower = name.Trim().ToLowerInvariant();
            switch (lower)
            {
                case "ore":
                case "erz":
                    result = ResourceType.Ore;
                    return true;
                case "fuel":
                case "treibstoff":
                    result = ResourceType.Fuel;
                    return true;
                case "carbon":
                    result = ResourceType.Carbon;
                    return true;
                case "food":
                case "nahrung":
                    result = ResourceType.Food;
                    return true;
                case "tradegoods":
                case "goods":
                case "handelsware":
                case "ware":
                    result = ResourceType.TradeGoods;
                    return true;
                default:
                    return Enum.TryParse(name, true, out result);
            }
        }

        private static bool TryParseUpgrade(string name, out FlagshipUpgrade result)
        {
            result = FlagshipUpgrade.Drive;
            if (string.IsNullOrEmpty(name)) return false;

            string lower = name.Trim().ToLowerInvariant();
            switch (lower)
            {
                case "drive":
                case "booster":
                case "antrieb":
                case "triebwerk":
                    result = FlagshipUpgrade.Drive;
                    return true;
                case "cannon":
                case "kanone":
                case "bordkanone":
                    result = FlagshipUpgrade.Cannon;
                    return true;
                case "cargomodule":
                case "freightpod":
                case "frachtmodul":
                case "fracht":
                    result = FlagshipUpgrade.CargoModule;
                    return true;
                default:
                    return Enum.TryParse(name, true, out result);
            }
        }

        private static bool TryParseShipType(string name, out StarshipType result)
        {
            result = StarshipType.ColonyShip;
            if (string.IsNullOrEmpty(name)) return false;

            string lower = name.Trim().ToLowerInvariant();
            switch (lower)
            {
                case "colonyship":
                case "colony":
                case "kolonieschiff":
                    result = StarshipType.ColonyShip;
                    return true;
                case "tradeship":
                case "trade":
                case "handelsschiff":
                    result = StarshipType.TradeShip;
                    return true;
                default:
                    return Enum.TryParse(name, true, out result);
            }
        }

        #endregion
    }
}

