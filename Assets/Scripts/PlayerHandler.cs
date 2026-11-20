using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Central player coordinator. Manages high-level game dialogs (Encounter, Trade,
/// Friendship Cards, Victory Points Track, Founding Phase overlay),
/// while delegating sidebar rendering and dynamic action panels to SidebarViewUI.
/// </summary>
public class PlayerHandler : MonoBehaviour
{
    public static PlayerHandler Instance { get; private set; }

    private TriMap _triMap;
    private SelectionController _selectionController;
    private SidebarViewUI _sidebarView;

    // Overlays & Dialogs
    private EncounterDialogUI _encounterDialog;
    private EncounterController _encounterController;
    private TradeDialogUI _tradeDialog;
    private VictoryPointsTrackUI _victoryPointsTrackBar;
    private CostTooltipUI _costTooltip;
    private OutpostTooltipUI _outpostTooltip;
    private PlanetTooltipUI _planetTooltip;
    private BuildingCostsDialogUI _buildingCostsDialog;
    private FriendshipCardDialogUI _friendshipCardDialog;
    private ConfirmEndTurnDialogUI _confirmEndTurnDialog;
    private DiscardCardsDialogUI _discardCardsDialog;
    private StealCardDialogUI _stealCardDialog;
    private GameOverDialogUI _gameOverDialog;
    private ConfirmQuitDialogUI _confirmQuitDialog;
    private DisconnectDialogUI _disconnectDialog;
    private AiTakeoverWarningDialogUI _aiTakeoverDialog;
    private UpgradeChoiceDialogUI _upgradeChoiceDialog;
    private ShipChoiceDialogUI _shipChoiceDialog;
    private SevenRollController _sevenRollController;
    private GameSetupDialogUI _gameSetupDialog;

    // Turn Inactivity & AI Takeover
    public const float TotalTurnTimeout = 180f;
    public const float WarningCountdownThreshold = 90f;
    private float _turnInactivityTimer = 0f;
    private int _lastActiveTurnPlayer = -1;
    private GameTurnPhase _lastActiveTurnPhase = GameTurnPhase.Production;

    // Founding Phase Overlay
    private FoundingPanelUI _foundingPanel;

    private Text _windowTitleText;
    private Font _font;
    private bool _subscribedToTurnChanges;
    private bool _wasAnimationPlaying;
    private int[] _lastVictoryPoints;

    public SidebarViewUI Sidebar => _sidebarView;
    public DiscardCardsDialogUI DiscardCardsDialog => _discardCardsDialog;
    public StealCardDialogUI StealCardDialog => _stealCardDialog;
    public FriendshipCardDialogUI FriendshipCardDialog => _friendshipCardDialog;
    public GameSetupDialogUI GameSetupDialog => _gameSetupDialog;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Start()
    {
        _triMap = FindFirstObjectByType<TriMap>();
        _selectionController = FindFirstObjectByType<SelectionController>();
        if (_triMap == null)
        {
            Debug.LogError("PlayerHandler could not find a TriMap.");
            enabled = false;
            return;
        }

        Transform nameTransform = transform.Find("PlayerName");
        _font = (nameTransform != null && nameTransform.GetComponent<Text>() != null)
            ? nameTransform.GetComponent<Text>().font
            : null;

        if (_font == null)
        {
            Text fallbackText = GetComponentInParent<Canvas>()?.GetComponentInChildren<Text>(true);
            if (fallbackText != null && fallbackText.font != null)
            {
                _font = fallbackText.font;
            }
        }

        if (_font == null)
        {
            _font = Resources.Load<Font>("Fonts/Roboto-Regular");
        }

        if (_font == null)
        {
            try
            {
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch { }
        }

        if (_font == null)
        {
            try
            {
                _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch { }
        }

        ActionPanelUI.Callbacks actionCallbacks = new ActionPanelUI.Callbacks
        {
            OnRollProduction = OnRollProductionClicked,
            OnBeginShipPlacement = (shipType) => _triMap.TryBeginShipPlacement(shipType),
            OnDeployCapturedTradeShip = () => _triMap.TryBeginShipPlacement(StarshipType.TradeShip, isCapturedFree: true),
            OnBeginSpaceportUpgrade = () => _triMap.TryBeginSpaceportUpgrade(),
            OnBuildUpgrade = (upgrade) =>
            {
                if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected)
                {
                    Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO
                    {
                        action = "BuildUpgrade",
                        upgradeType = upgrade.ToString()
                    });
                    return;
                }
                _triMap.TryBuildUpgrade(upgrade);
                _sidebarView?.UpdateFlagshipInventory();
                _sidebarView?.RefreshActionPanel();
            },
            OnCancelShipPlacement = () => _triMap.CancelShipPlacement(),
            OnCancelSpaceportUpgrade = () => _triMap.CancelSpaceportUpgrade(),
            OnCancelSpaceJump = OpenSpaceJumpShipChoice,
            OnOpenTrade = OpenTradeOverlay,
            OnToggleBuildingCosts = ToggleBuildingCostsPanel,
            OnFoundColony = (targetTri) =>
            {
                if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected)
                {
                    Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO
                    {
                        action = "FoundColony",
                        q = targetTri.Q,
                        r = targetTri.R
                    });
                    return;
                }
                _triMap.TryFoundColonyAt(targetTri);
                _sidebarView?.UpdateResourceInventory();
                _sidebarView?.UpdateFlagshipInventory();
                _sidebarView?.RefreshActionPanel();
            },
            OnEstablishTradeStation = (targetTri) =>
            {
                if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected)
                {
                    Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO
                    {
                        action = "BuildTradeStation",
                        q = targetTri.Q,
                        r = targetTri.R
                    });
                    return;
                }
                _triMap.TryEstablishTradeStationAt(targetTri);
                _sidebarView?.UpdateResourceInventory();
                _sidebarView?.UpdateFlagshipInventory();
                _sidebarView?.RefreshActionPanel();
            },
            OnRollFlightImpulse = () =>
            {
                if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected)
                {
                    Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO
                    {
                        action = "RollFlight"
                    });
                    return;
                }
                _triMap.RollFlightImpulse();
                _sidebarView?.UpdateResourceInventory();
                _sidebarView?.RefreshActionPanel();
            },
            OnCycleMovableShips = () =>
            {
                if (_selectionController != null && _triMap != null && _triMap.CurrentPlayer != null && _triMap.CurrentPlayer.Type == Player.PlayerType.LOCAL)
                {
                    _selectionController.SelectNextUnit(skipDoneUnits: true);
                }
            },
            OnBuyFame = ExecuteBuyFame,
            OnRichHelpsPoor = ExecuteRichHelpsPoor,
            OnShowCostTooltip = ShowCostTooltip,
            OnHideCostTooltip = HideCostTooltip
        };

        _sidebarView = SidebarViewUI.AttachTo(
            gameObject,
            _triMap,
            _selectionController,
            OnNextButtonClicked,
            actionCallbacks,
            _font,
            OnQuitRequested);

        Canvas canvas = GetComponentInParent<Canvas>();
        Transform uiParent = canvas != null ? canvas.transform : transform;

        _discardCardsDialog = DiscardCardsDialogUI.Create(uiParent, _font);
        _stealCardDialog = StealCardDialogUI.Create(uiParent, _font);
        _sevenRollController = new SevenRollController(_triMap, _discardCardsDialog, _stealCardDialog, ShowToast);
        _encounterDialog = EncounterDialogUI.Create(uiParent, _font, () => _encounterController?.OnEncounterButtonClicked());
        _upgradeChoiceDialog = UpgradeChoiceDialogUI.Create(uiParent, _font);
        _shipChoiceDialog = ShipChoiceDialogUI.Create(uiParent, _font);
        _encounterController = new EncounterController(
            _triMap,
            _encounterDialog,
            ShowToast,
            _discardCardsDialog,
            () =>
            {
                _sidebarView?.UpdateResourceInventory();
                _sidebarView?.UpdateFlagshipInventory();
                _sidebarView?.RefreshActionPanel();
            },
            _upgradeChoiceDialog,
            _shipChoiceDialog);
        _tradeDialog = TradeDialogUI.Create(uiParent, _font);
        _victoryPointsTrackBar = VictoryPointsTrackUI.Create(uiParent, _font);
        ToastNotificationUI.Create(uiParent, _font);
        _costTooltip = CostTooltipUI.Create(uiParent, _font);
        _outpostTooltip = OutpostTooltipUI.Create(uiParent, _font);
        _planetTooltip = PlanetTooltipUI.Create(uiParent, _font);
        _buildingCostsDialog = BuildingCostsDialogUI.Create(uiParent, _font);
        _friendshipCardDialog = FriendshipCardDialogUI.Create(uiParent, _font);
        _foundingPanel = FoundingPanelUI.Create(uiParent, _font);
        _gameOverDialog = GameOverDialogUI.Create(uiParent, _font);
        _confirmQuitDialog = ConfirmQuitDialogUI.Create(uiParent, _font);
        _disconnectDialog = DisconnectDialogUI.Create(uiParent, _font);
        _aiTakeoverDialog = AiTakeoverWarningDialogUI.Create(uiParent, _font);
        if (_aiTakeoverDialog != null)
        {
            _aiTakeoverDialog.OnCancelTakeover += HandleCancelAiTakeover;
        }
        _gameSetupDialog = GameSetupDialogUI.Create(uiParent, _font, _triMap, OnGameSetupStarted);
 
        // Register overlays with UIManager to suppress tooltips (Step 3)
        UIManager.Clear();
        UIManager.RegisterOverlayCheck(() => _gameSetupDialog != null && _gameSetupDialog.IsActive);
        UIManager.RegisterOverlayCheck(() => _gameOverDialog != null && _gameOverDialog.IsActive);
        UIManager.RegisterOverlayCheck(() => _confirmQuitDialog != null && _confirmQuitDialog.IsActive);
        UIManager.RegisterOverlayCheck(() => _disconnectDialog != null && _disconnectDialog.IsActive);
        UIManager.RegisterOverlayCheck(() => _aiTakeoverDialog != null && _aiTakeoverDialog.IsActive);
        UIManager.RegisterOverlayCheck(() => _encounterDialog != null && _encounterDialog.IsVisible);
        UIManager.RegisterOverlayCheck(() => _tradeDialog != null && _tradeDialog.gameObject.activeSelf);
        UIManager.RegisterOverlayCheck(() => _confirmEndTurnDialog != null && _confirmEndTurnDialog.gameObject.activeSelf);
        UIManager.RegisterOverlayCheck(() => _friendshipCardDialog != null && _friendshipCardDialog.gameObject.activeSelf);
        UIManager.RegisterOverlayCheck(() => _buildingCostsDialog != null && _buildingCostsDialog.gameObject.activeSelf);
        UIManager.RegisterOverlayCheck(() => _foundingPanel != null && _foundingPanel.IsVisible);
        UIManager.RegisterOverlayCheck(() => _discardCardsDialog != null && _discardCardsDialog.IsActive);
        UIManager.RegisterOverlayCheck(() => _stealCardDialog != null && _stealCardDialog.IsActive);

        FindWindowTitleText();
        SubscribeToTurnChanges();
        RefreshFoundingUI();
        RefreshVictoryPointsTrackUI();

        if (_triMap != null && _triMap.IsGameSetupActive)
        {
            _foundingPanel?.SetVisible(false);
            _gameSetupDialog.Show();
        }
    }

    private void OnGameSetupStarted()
    {
        RefreshFoundingUI();
        RefreshVictoryPointsTrackUI();
        _sidebarView?.UpdateResourceInventory();
        _sidebarView?.UpdateFlagshipInventory();
        _sidebarView?.RefreshActionPanel();
    }

    private void OnQuitRequested()
    {
        _confirmQuitDialog?.Show();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_confirmQuitDialog != null && _confirmQuitDialog.IsActive)
            {
                _confirmQuitDialog.Hide();
            }
            else if (!UIManager.IsAnyOverlayActive)
            {
                _confirmQuitDialog?.Show();
            }
        }

        if (Input.GetKeyDown(KeyCode.Tab) && !UIManager.IsAnyOverlayActive)
        {
            if (_triMap != null && _triMap.CurrentPhase == GameTurnPhase.Flight &&
                _triMap.LastFlagshipRoll != null && !_triMap.EncounterPending)
            {
                bool isLocalTurn = true;
                if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected)
                {
                    isLocalTurn = _triMap.CurrentPlayerIndex == Networking.NetworkManager.Instance.LocalPlayerIndex;
                }
                else
                {
                    isLocalTurn = _triMap.CurrentPlayer != null && _triMap.CurrentPlayer.Type == Player.PlayerType.LOCAL;
                }

                if (isLocalTurn && _selectionController != null)
                {
                    _selectionController.SelectNextUnit(skipDoneUnits: true);
                }
            }
        }

        bool isAnimPlaying = _triMap != null && _triMap.AnimationIsPlaying;
        if (_wasAnimationPlaying && !isAnimPlaying)
        {
            _sidebarView?.RefreshActionPanel();
        }
        _wasAnimationPlaying = isAnimPlaying;

        if (_triMap != null && _triMap.Players != null && _triMap.Players.Length > 0)
        {
            bool vpChanged = false;
            if (_lastVictoryPoints == null || _lastVictoryPoints.Length != _triMap.Players.Length)
            {
                _lastVictoryPoints = new int[_triMap.Players.Length];
                for (int i = 0; i < _triMap.Players.Length; i++)
                {
                    _lastVictoryPoints[i] = _triMap.Players[i] != null ? _triMap.Players[i].VictoryPoints : 0;
                }
                vpChanged = true;
            }
            else
            {
                for (int i = 0; i < _triMap.Players.Length; i++)
                {
                    int currentVp = _triMap.Players[i] != null ? _triMap.Players[i].VictoryPoints : 0;
                    if (_lastVictoryPoints[i] != currentVp)
                    {
                        _lastVictoryPoints[i] = currentVp;
                        vpChanged = true;
                    }
                }
            }

            if (vpChanged)
            {
                RefreshVictoryPointsTrackUI();
            }
        }

        // Turn Inactivity & AI Takeover Monitoring (Only active during regular turn gameplay after founding phase!)
        // Inactivity timeout never applies to the last or only human player (Singleplayer or last human in multiplayer)
        int humanPlayerCount = (_triMap != null && _triMap.Players != null)
            ? _triMap.Players.Count(p => p != null && p.Type != Player.PlayerType.AI)
            : 0;

        if (_triMap != null && !_triMap.IsGameOver && !_triMap.IsGameSetupActive && _triMap.CurrentFoundingPhase == FoundingPhase.Complete && humanPlayerCount > 1)
        {
            if (_triMap.CurrentPlayerIndex != _lastActiveTurnPlayer || _triMap.CurrentPhase != _lastActiveTurnPhase)
            {
                _lastActiveTurnPlayer = _triMap.CurrentPlayerIndex;
                _lastActiveTurnPhase = _triMap.CurrentPhase;
                ResetTurnInactivity();
            }

            bool isLocalHumanTurn = false;
            if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected)
            {
                isLocalHumanTurn = _triMap.CurrentPlayerIndex == Networking.NetworkManager.Instance.LocalPlayerIndex &&
                                   _triMap.CurrentPlayer != null && _triMap.CurrentPlayer.Type != Player.PlayerType.AI;
            }
            else
            {
                isLocalHumanTurn = _triMap.CurrentPlayer != null && _triMap.CurrentPlayer.Type == Player.PlayerType.LOCAL;
            }

            if (isLocalHumanTurn)
            {
                if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
                {
                    if (_aiTakeoverDialog == null || !_aiTakeoverDialog.IsActive)
                    {
                        _turnInactivityTimer = 0f;
                    }
                }

                _turnInactivityTimer += Time.deltaTime;

                if (_turnInactivityTimer >= WarningCountdownThreshold)
                {
                    float remaining = Mathf.Max(0f, TotalTurnTimeout - _turnInactivityTimer);
                    int remainingSeconds = Mathf.CeilToInt(remaining);

                    if (_aiTakeoverDialog != null)
                    {
                        if (!_aiTakeoverDialog.IsActive)
                        {
                            _aiTakeoverDialog.Show(remainingSeconds);
                        }
                        else
                        {
                            _aiTakeoverDialog.UpdateCountdown(remainingSeconds);
                        }
                    }

                    if (remaining <= 0f)
                    {
                        _aiTakeoverDialog?.Hide();
                        _turnInactivityTimer = 0f;
                        ExecuteAiTakeoverForCurrentPlayer();
                    }
                }
                else if (_aiTakeoverDialog != null && _aiTakeoverDialog.IsActive)
                {
                    _aiTakeoverDialog.Hide();
                }
            }
            else
            {
                _turnInactivityTimer = 0f;
                if (_aiTakeoverDialog != null && _aiTakeoverDialog.IsActive)
                {
                    _aiTakeoverDialog.Hide();
                }
            }
        }
        else
        {
            _turnInactivityTimer = 0f;
            if (_aiTakeoverDialog != null && _aiTakeoverDialog.IsActive)
            {
                _aiTakeoverDialog.Hide();
            }
        }
    }

    public void ResetTurnInactivity()
    {
        _turnInactivityTimer = 0f;
        if (_aiTakeoverDialog != null && _aiTakeoverDialog.IsActive)
        {
            _aiTakeoverDialog.Hide();
        }
    }

    private void HandleCancelAiTakeover()
    {
        ResetTurnInactivity();
        if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected)
        {
            Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO
            {
                action = "ResetTurnTimer"
            });
        }
    }

    private void ExecuteAiTakeoverForCurrentPlayer()
    {
        if (_triMap == null || _triMap.CurrentFoundingPhase != FoundingPhase.Complete || _triMap.IsGameOver) return;

        int humanCount = _triMap.Players != null ? _triMap.Players.Count(p => p != null && p.Type != Player.PlayerType.AI) : 0;
        if (humanCount <= 1)
        {
            return;
        }

        if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected)
        {
            Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO
            {
                action = "ForceAiTakeover"
            });
        }
        else
        {
            if (_triMap != null && _triMap.CurrentPlayer != null)
            {
                _triMap.CurrentPlayer.Type = Player.PlayerType.AI;
                _triMap.CurrentPlayer.Difficulty = Player.AiDifficulty.Normal;
                _triMap.GetComponent<AiPlayerController>()?.TriggerAiCheck();
                _sidebarView?.UpdateTurnStatus();
                _sidebarView?.RefreshActionPanel();
            }
        }
    }

    void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += OnLanguageChanged;
        if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.Client != null)
        {
            Networking.NetworkManager.Instance.Client.OnDisconnected -= HandleNetworkDisconnected;
            Networking.NetworkManager.Instance.Client.OnDisconnected += HandleNetworkDisconnected;
        }
        if (_triMap != null)
        {
            SubscribeToTurnChanges();
            if (_triMap.EncounterPending && _triMap.CurrentEncounterCard != null)
            {
                OnEncounterCardDrawn(_triMap.EncounterReader, _triMap.CurrentEncounterCard);
            }
            RefreshFoundingUI();
        }
    }

    void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.Client != null)
        {
            Networking.NetworkManager.Instance.Client.OnDisconnected -= HandleNetworkDisconnected;
        }
        UnsubscribeFromTurnChanges();
    }

    private void HandleNetworkDisconnected(string reason)
    {
        if (_triMap != null && !_triMap.IsGameSetupActive)
        {
            _disconnectDialog?.Show(reason);
        }
    }

    private void SubscribeToTurnChanges()
    {
        if (!_subscribedToTurnChanges && _triMap != null)
        {
            _triMap.OnEncounterStateChanged += OnEncounterStateChanged;
            _triMap.OnEncounterCardDrawn += OnEncounterCardDrawn;
            _triMap.OnEncounterProgressChanged += OnEncounterProgressChanged;
            _triMap.OnFoundingPhaseChanged += OnFoundingPhaseChanged;
            _triMap.OnPromptFriendshipCardSelection += OnPromptFriendshipCardSelection;
            _triMap.OnSevenRollResolutionNeeded += HandleSevenRollResolutionNeeded;
            _triMap.OnSpecialPlanetResolved += HandleSpecialPlanetResolved;
            _triMap.OnGameOver += HandleGameOver;
            _triMap.OnVictoryPointsChanged += HandleVictoryPointsChanged;
            _triMap.OnTurnPhaseChanged += HandleTurnPhaseChanged;

            if (Networking.NetworkManager.Instance != null)
            {
                Networking.NetworkManager.Instance.OnClientGameStateReceived -= HandleClientGameStateReceived;
                Networking.NetworkManager.Instance.OnClientGameStateReceived += HandleClientGameStateReceived;
            }

            _subscribedToTurnChanges = true;
        }
    }

    private void UnsubscribeFromTurnChanges()
    {
        if (_subscribedToTurnChanges && _triMap != null)
        {
            _triMap.OnEncounterStateChanged -= OnEncounterStateChanged;
            _triMap.OnEncounterCardDrawn -= OnEncounterCardDrawn;
            _triMap.OnEncounterProgressChanged -= OnEncounterProgressChanged;
            _triMap.OnFoundingPhaseChanged -= OnFoundingPhaseChanged;
            _triMap.OnPromptFriendshipCardSelection -= OnPromptFriendshipCardSelection;
            _triMap.OnSevenRollResolutionNeeded -= HandleSevenRollResolutionNeeded;
            _triMap.OnSpecialPlanetResolved -= HandleSpecialPlanetResolved;
            _triMap.OnGameOver -= HandleGameOver;
            _triMap.OnVictoryPointsChanged -= HandleVictoryPointsChanged;
            _triMap.OnTurnPhaseChanged -= HandleTurnPhaseChanged;

            if (Networking.NetworkManager.Instance != null)
            {
                Networking.NetworkManager.Instance.OnClientGameStateReceived -= HandleClientGameStateReceived;
            }

            _subscribedToTurnChanges = false;
        }
    }

    private void HandleClientGameStateReceived(AgentBridge.GameStateObservationDTO obs)
    {
        if (obs == null) return;

        if (Networking.NetworkManager.Instance != null &&
            Networking.NetworkManager.Instance.IsClientConnected &&
            !Networking.NetworkManager.Instance.IsHost)
        {
            int localIdx = Networking.NetworkManager.Instance.LocalPlayerIndex;
            var dialogs = obs.dialogs;
            Player localPlayer = (_triMap != null && _triMap.Players != null && localIdx >= 0 && localIdx < _triMap.Players.Length)
                ? _triMap.Players[localIdx]
                : null;

            // 1. Friendship card dialog
            if (_friendshipCardDialog != null)
            {
                if (dialogs != null && dialogs.isFriendshipCardDialogOpen && dialogs.friendshipPlayerIndex == localIdx)
                {
                    if (!_friendshipCardDialog.IsActive && dialogs.friendshipCardOptions != null && dialogs.friendshipCardOptions.Length > 0)
                    {
                        AlienCivilization civ = AlienCivilization.Trader;
                        if (!string.IsNullOrEmpty(dialogs.friendshipCivilization))
                        {
                            System.Enum.TryParse(dialogs.friendshipCivilization, out civ);
                        }

                        List<FriendshipCard> cards = new List<FriendshipCard>();
                        List<FriendshipCard> fullCivDeck = FriendshipCard.CreateDeckForCivilization(civ);
                        foreach (var opt in dialogs.friendshipCardOptions)
                        {
                            if (System.Enum.TryParse(opt.cardId, out FriendshipCardId cid))
                            {
                                FriendshipCard match = fullCivDeck.FirstOrDefault(c => c.Id == cid);
                                if (match != null)
                                {
                                    cards.Add(match);
                                }
                            }
                        }

                        ShowFriendshipCardSelection(localPlayer, civ, cards);
                    }
                }
                else if (_friendshipCardDialog.IsActive && (dialogs == null || !dialogs.isFriendshipCardDialogOpen))
                {
                    _friendshipCardDialog.Hide();
                }
            }

            // 2. Steal card dialog (rolled 7)
            if (_stealCardDialog != null)
            {
                if (dialogs != null && dialogs.isStealDialogOpen && obs.currentPlayerIndex == localIdx)
                {
                    if (!_stealCardDialog.IsActive && dialogs.eligibleStealTargetIndices != null && dialogs.eligibleStealTargetIndices.Length > 0)
                    {
                        List<Player> targets = new List<Player>();
                        if (_triMap != null && _triMap.Players != null)
                        {
                            foreach (int tIdx in dialogs.eligibleStealTargetIndices)
                            {
                                if (tIdx >= 0 && tIdx < _triMap.Players.Length && _triMap.Players[tIdx] != null)
                                {
                                    targets.Add(_triMap.Players[tIdx]);
                                }
                            }
                        }

                        _stealCardDialog.Open(localPlayer, targets, chosenTarget =>
                        {
                            int targetIdx = (_triMap != null && _triMap.Players != null)
                                ? System.Array.IndexOf(_triMap.Players, chosenTarget)
                                : -1;

                            if (targetIdx >= 0 && Networking.NetworkManager.Instance?.Client != null)
                            {
                                Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO
                                {
                                    action = "SevenRollSteal",
                                    partnerIndex = targetIdx
                                });
                            }
                        });
                    }
                }
                else if (_stealCardDialog.IsActive && (dialogs == null || !dialogs.isStealDialogOpen))
                {
                    _stealCardDialog.gameObject.SetActive(false);
                }
            }

            // 3. Discard cards dialog (rolled 7 with > 7 cards)
            if (_discardCardsDialog != null)
            {
                if (dialogs != null && dialogs.isDiscardDialogOpen && dialogs.discardPlayerIndex == localIdx)
                {
                    if (!_discardCardsDialog.IsActive && dialogs.discardRequiredCount > 0)
                    {
                        _discardCardsDialog.Open(localPlayer, dialogs.discardRequiredCount, () =>
                        {
                            var discards = _discardCardsDialog.SelectedDiscards;
                            if (Networking.NetworkManager.Instance?.Client != null)
                            {
                                Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO
                                {
                                    action = "SevenRollDiscard",
                                    ore = (discards != null && discards.TryGetValue(ResourceType.Ore, out int o)) ? o : 0,
                                    fuel = (discards != null && discards.TryGetValue(ResourceType.Fuel, out int f)) ? f : 0,
                                    carbon = (discards != null && discards.TryGetValue(ResourceType.Carbon, out int c)) ? c : 0,
                                    food = (discards != null && discards.TryGetValue(ResourceType.Food, out int fd)) ? fd : 0,
                                    tradeGoods = (discards != null && discards.TryGetValue(ResourceType.TradeGoods, out int tg)) ? tg : 0
                                });
                            }
                        });
                    }
                }
                else if (_discardCardsDialog.IsActive && (dialogs == null || !dialogs.isDiscardDialogOpen))
                {
                    _discardCardsDialog.gameObject.SetActive(false);
                }
            }
        }
    }

    private void HandleVictoryPointsChanged()
    {
        RefreshVictoryPointsTrackUI();
    }

    private void HandleTurnPhaseChanged(Player player, GameTurnPhase phase)
    {
        RefreshVictoryPointsTrackUI();
    }

    private void HandleGameOver(Player winner)
    {
        RefreshVictoryPointsTrackUI();
        _sidebarView?.RefreshActionPanel();
        _gameOverDialog?.Show(winner, _triMap.Players);
    }

    private void HandleSpecialPlanetResolved()
    {
        _sidebarView?.UpdateFlagshipInventory();
        _sidebarView?.RefreshActionPanel();
        RefreshVictoryPointsTrackUI();
    }

    private void HandleSevenRollResolutionNeeded(System.Action onComplete)
    {
        _sevenRollController?.StartResolution(() =>
        {
            onComplete?.Invoke();
            _sidebarView?.UpdateResourceInventory();
            _sidebarView?.RefreshActionPanel();
        });
    }

    private void OnPromptFriendshipCardSelection(Player player, AlienCivilization civ, List<FriendshipCard> cards)
    {
        ShowFriendshipCardSelection(player, civ, cards);
    }

    private void FindWindowTitleText()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Transform windowTitle = canvas.transform.Find("Window/Text");
            if (windowTitle != null)
            {
                _windowTitleText = windowTitle.GetComponent<Text>();
                if (_windowTitleText != null)
                {
                    _windowTitleText.text = UIStrings.GameTitle.GetText();
                }
            }
        }
    }

    private void OnLanguageChanged(GameLanguage language)
    {
        if (_windowTitleText != null)
        {
            _windowTitleText.text = UIStrings.GameTitle.GetText();
        }
        _sidebarView?.UpdateLanguage();
        RefreshFoundingUI();
        RefreshEncounterUI();
        if (_tradeDialog != null)
        {
            _tradeDialog.UpdateLanguage();
        }
        if (_buildingCostsDialog != null)
        {
            _buildingCostsDialog.UpdateLanguage();
        }
        if (_victoryPointsTrackBar != null)
        {
            _victoryPointsTrackBar.UpdateLanguage();
        }
    }

    public void OnNextButtonClicked()
    {
        if (_triMap == null) return;

        if (CheckAndPromptConfirmEndTurn())
        {
            return;
        }

        if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected)
        {
            Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO
            {
                action = "AdvancePhase"
            });
            return;
        }

        _triMap.AdvanceTurnPhase();
    }

    private void OnRollProductionClicked()
    {
        if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected)
        {
            Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO
            {
                action = "RollProduction"
            });
            return;
        }

        _triMap.RollProductionDice();
        _sidebarView?.UpdateResourceInventory();
        _triMap.AdvanceTurnPhase();
        _sidebarView?.RefreshActionPanel();
    }

    private void ExecuteBuyFame()
    {
        if (_triMap == null) return;
        if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected)
        {
            Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO
            {
                action = "BuyFame"
            });
            return;
        }

        if (_triMap.TryBuyFame())
        {
            _sidebarView?.UpdateResourceInventory();
            _sidebarView?.UpdateFlagshipInventory();
            _sidebarView?.RefreshActionPanel();
            RefreshVictoryPointsTrackUI();
            ShowToast("Gekaufter Ruhm: 1 Handelsware -> +1 halbe Medaille!", isWarning: false);
        }
    }

    private void ExecuteRichHelpsPoor()
    {
        if (_triMap == null) return;
        if (_triMap.TryRichHelpsPoor(out string summary))
        {
            _sidebarView?.UpdateResourceInventory();
            _sidebarView?.UpdateFlagshipInventory();
            _sidebarView?.RefreshActionPanel();
            string msg = !string.IsNullOrEmpty(summary) ? summary : "Reich hilft Arm: Rohstoffkarte(n) erhalten!";
            ShowToast(msg, isWarning: false, duration: 4.5f);
        }
    }

    public void ToggleBuildingCostsPanel()
    {
        if (_buildingCostsDialog != null)
        {
            HideCostTooltip();
            HideOutpostTooltip();
            _buildingCostsDialog.Toggle();
        }
    }

    public void ShowCostTooltip(string costDescription, RectTransform buttonRect)
    {
        if (_costTooltip != null)
        {
            _costTooltip.Show(costDescription, buttonRect, _sidebarView != null ? _sidebarView.ActionPanel.transform : null);
        }
    }

    public void HideCostTooltip()
    {
        if (_costTooltip != null)
        {
            _costTooltip.Hide();
        }
    }

    public void ShowOutpostTooltip(Tri tri)
    {
        if (_outpostTooltip != null)
        {
            _outpostTooltip.Show(tri);
        }
    }

    public void HideOutpostTooltip()
    {
        if (_outpostTooltip != null)
        {
            _outpostTooltip.Hide();
        }
    }

    public void ShowPlanetTooltip(Tri tri)
    {
        if (_planetTooltip != null)
        {
            _planetTooltip.Show(tri, _triMap);
        }
    }

    public void HidePlanetTooltip()
    {
        if (_planetTooltip != null)
        {
            _planetTooltip.Hide();
        }
    }

    public void RefreshVictoryPointsTrackUI()
    {
        if (_victoryPointsTrackBar != null && _triMap != null)
        {
            _victoryPointsTrackBar.Refresh(_triMap);
        }
    }

    public static void ShowToast(string message, bool isWarning = true, float duration = 4.5f)
    {
        ToastNotificationUI.Show(message, isWarning, duration);

        if (Networking.NetworkManager.Instance != null &&
            Networking.NetworkManager.Instance.IsHost &&
            Networking.NetworkManager.Instance.Server != null)
        {
            Networking.NetworkManager.Instance.Server.BroadcastToast(message, isWarning, duration);
        }
    }

    public void DisplayToast(string message, bool isWarning = true, float duration = 4.5f)
    {
        ShowToast(message, isWarning, duration);
    }

    public void ShowFriendshipCardSelection(Player player, AlienCivilization civ, List<FriendshipCard> availableCards)
    {
        if (_friendshipCardDialog == null) return;

        int playerIdx = (_triMap != null && _triMap.Players != null && player != null)
            ? System.Array.IndexOf(_triMap.Players, player)
            : -1;

        bool isLocalHuman = false;
        if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected)
        {
            isLocalHuman = (playerIdx == Networking.NetworkManager.Instance.LocalPlayerIndex);
        }
        else
        {
            isLocalHuman = (player != null && player.Type == Player.PlayerType.LOCAL);
        }

        // Only display dialog visually on this machine if the card choice is for the local human player!
        if (!isLocalHuman)
        {
            return;
        }

        _friendshipCardDialog.Show(civ, availableCards, card =>
        {
            if (Networking.NetworkManager.Instance != null &&
                Networking.NetworkManager.Instance.IsClientConnected &&
                !Networking.NetworkManager.Instance.IsHost)
            {
                int cardIndex = availableCards != null ? availableCards.IndexOf(card) : 0;
                Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO
                {
                    action = "SelectFriendshipCard",
                    cardIndex = cardIndex
                });
            }
            else
            {
                _triMap.SelectFriendshipCard(civ, player, card.Id);
                _sidebarView?.UpdateFlagshipInventory();
                _sidebarView?.RefreshActionPanel();
                RefreshVictoryPointsTrackUI();
            }
        });
    }

    public bool CheckAndPromptConfirmEndTurn()
    {
        if (_triMap == null || _triMap.CurrentPhase != GameTurnPhase.Flight)
        {
            return false;
        }

        Player player = _triMap.CurrentPlayer;
        if (player == null || player.Type == Player.PlayerType.AI || player.Units == null)
        {
            return false;
        }

        if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected)
        {
            if (_triMap.CurrentPlayerIndex != Networking.NetworkManager.Instance.LocalPlayerIndex)
            {
                return false;
            }
        }

        var movableShips = player.Units.Where(u => u != null && !u.IsDestroyed && u.MovementRemaining > 0).ToList();
        if (movableShips.Count == 0)
        {
            return false;
        }

        int totalRemaining = movableShips.Sum(u => u.MovementRemaining);
        if (_confirmEndTurnDialog == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : transform;
            _confirmEndTurnDialog = ConfirmEndTurnDialogUI.Create(parent, _font);
        }
        _confirmEndTurnDialog.Show(
            string.Format(UIStrings.ConfirmEndTurnPrompt.GetText(), movableShips.Count, totalRemaining),
            () =>
            {
                if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected)
                {
                    Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO
                    {
                        action = "AdvancePhase"
                    });
                    return;
                }
                if (_triMap != null)
                {
                    _triMap.AdvanceTurnPhase();
                }
            });
        return true;
    }

    public void OpenTradeOverlay()
    {
        HideCostTooltip();
        HideOutpostTooltip();
        if (_tradeDialog == null || _triMap == null) return;
        _tradeDialog.Open(_triMap, () =>
        {
            _sidebarView?.UpdateResourceInventory();
            _sidebarView?.RefreshActionPanel();
        });
    }

    public void CloseTradeOverlay()
    {
        if (_tradeDialog != null)
        {
            _tradeDialog.Close();
        }
    }

    public void OpenSpaceJumpShipChoice()
    {
        if (_triMap == null || _shipChoiceDialog == null) return;
        var allUnits = _triMap.CurrentPlayer?.Units?.Where(u => u != null && !u.IsDestroyed).ToList();
        if (allUnits == null || allUnits.Count <= 1) return;

        _shipChoiceDialog.Open(
            allUnits,
            UIStrings.EncounterSpaceJumpTitle.GetText(),
            UIStrings.EncounterSpaceJumpPrompt.GetText(),
            chosenShip =>
            {
                _triMap.SetSpaceJumpShip(chosenShip);
            },
            buttonColor: new Color(0.18f, 0.42f, 0.65f, 0.95f));
    }

    // --- Encounter Dialog Handling ---
    private void OnEncounterStateChanged(bool pending)
    {
        RefreshEncounterUI();
        RefreshVictoryPointsTrackUI();
        _sidebarView?.UpdateResourceInventory();
        _sidebarView?.UpdateFlagshipInventory();
        _sidebarView?.RefreshActionPanel();
    }

    private void OnEncounterCardDrawn(Player reader, EncounterCard card)
    {
        RefreshEncounterUI();
    }

    private void OnEncounterProgressChanged(EncounterCardPhase phase, EncounterCardOutcome selectedOutcome)
    {
        RefreshEncounterUI();
        RefreshVictoryPointsTrackUI();
        _sidebarView?.UpdateResourceInventory();
        _sidebarView?.UpdateFlagshipInventory();
        _sidebarView?.RefreshActionPanel();
    }

    private void OnFoundingPhaseChanged(Player player, FoundingPhase phase)
    {
        RefreshFoundingUI();
        RefreshVictoryPointsTrackUI();
    }

    public void RefreshEncounterUI()
    {
        _encounterController?.Refresh();
    }


    // --- Founding Phase Overlay Handling ---
    public void RefreshFoundingUI()
    {
        if (_foundingPanel == null || _triMap == null) return;

        if (_triMap.IsGameSetupActive || _triMap.CurrentFoundingPhase == FoundingPhase.Complete)
        {
            _foundingPanel.SetVisible(false);
            return;
        }

        _foundingPanel.ClearButtons();
        bool showDialog = _triMap.CurrentFoundingPhase == FoundingPhase.RollForStartingPlayer ||
            _triMap.CurrentFoundingPhase == FoundingPhase.SelectStartingShip ||
            _triMap.CurrentFoundingPhase == FoundingPhase.SelectFlagshipUpgrade;
        _foundingPanel.SetVisible(showDialog);
        _sidebarView?.UpdateResourceInventory();

        if (!showDialog)
        {
            _foundingPanel.SetText(string.Empty);
            return;
        }

        StringBuilder text = new StringBuilder();
        text.Append(string.Format(UIStrings.SetupPhaseHeading.GetText(), _triMap.CurrentPlayer.PlayerName));
        text.AppendLine();
        text.AppendLine();
        if (_triMap.CurrentFoundingPhase == FoundingPhase.SelectStartingShip)
        {
            text.Append(string.Format(UIStrings.StatusSpaceportUpgraded.GetText(), _triMap.CurrentPlayer.PlayerName));
        }
        else if (_triMap.CurrentFoundingPhase == FoundingPhase.SelectFlagshipUpgrade)
        {
            text.Append(string.Format(UIStrings.StatusPlacedStartingShip.GetText(), _triMap.CurrentPlayer.PlayerName, "Startschiff"));
        }
        else
        {
            text.Append(_triMap.FoundingStatus);
        }
        if (_triMap.CurrentFoundingPhase == FoundingPhase.SelectStartingShipPoint)
        {
            _foundingPanel.SetText(string.Empty);
            return;
        }
        if (_triMap.CurrentFoundingPhase == FoundingPhase.PlaceFirstColony ||
            _triMap.CurrentFoundingPhase == FoundingPhase.PlaceSecondColony ||
            _triMap.CurrentFoundingPhase == FoundingPhase.PlaceThirdColony)
        {
            text.AppendLine();
            text.AppendLine(UIStrings.SetupClickFreeColony.GetText());
        }
        else if (_triMap.CurrentFoundingPhase == FoundingPhase.SelectSpaceport)
        {
            text.AppendLine();
            text.AppendLine(UIStrings.SetupClickOwnColony.GetText());
        }

        Player candidate = _triMap.CurrentFoundingPhase == FoundingPhase.RollForStartingPlayer
            ? _triMap.CurrentStartingPlayerCandidate
            : _triMap.CurrentPlayer;
        bool isAiAction = candidate != null && candidate.Type == Player.PlayerType.AI;

        bool isLocalTurn = true;
        if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected)
        {
            int candidateIdx = -1;
            if (_triMap.Players != null && candidate != null)
            {
                candidateIdx = System.Array.IndexOf(_triMap.Players, candidate);
            }
            isLocalTurn = candidateIdx == Networking.NetworkManager.Instance.LocalPlayerIndex;
        }

        if (isAiAction)
        {
            text.AppendLine();
            text.AppendLine(string.Format(UIStrings.AiTurnInProgress.GetText(), candidate.PlayerName));
        }
        else if (!isLocalTurn && candidate != null)
        {
            text.AppendLine();
            text.AppendLine($"{candidate.PlayerName} ist am Zug...");
        }

        _foundingPanel.SetText(text.ToString());

        if (isAiAction || !isLocalTurn) return;

        switch (_triMap.CurrentFoundingPhase)
        {
            case FoundingPhase.RollForStartingPlayer:
                _foundingPanel.AddActionButton(UIStrings.SetupBtnRollDice.GetText(), () =>
                {
                    if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected && !Networking.NetworkManager.Instance.IsHost)
                    {
                        Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO { action = "RollDice" });
                        return;
                    }
                    _triMap.RollForStartingPlayer();
                });
                break;
            case FoundingPhase.SelectStartingShip:
                _foundingPanel.AddActionButton(
                    UIStrings.SetupBtnFreeColonyShip.GetText(),
                    () =>
                    {
                        if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected && !Networking.NetworkManager.Instance.IsHost)
                        {
                            Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO { action = "SelectStartingShip", shipType = "ColonyShip" });
                            _triMap.RefreshFoundingMarkers();
                            return;
                        }
                        _triMap.SelectStartingShip(StarshipType.ColonyShip);
                    });
                _foundingPanel.AddActionButton(
                    UIStrings.SetupBtnFreeTradeShip.GetText(),
                    () =>
                    {
                        if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected && !Networking.NetworkManager.Instance.IsHost)
                        {
                            Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO { action = "SelectStartingShip", shipType = "TradeShip" });
                            _triMap.RefreshFoundingMarkers();
                            return;
                        }
                        _triMap.SelectStartingShip(StarshipType.TradeShip);
                    });
                break;
            case FoundingPhase.SelectFlagshipUpgrade:
                if (_triMap.RemainingStartingDrives > 0)
                {
                    _foundingPanel.AddActionButton(
                        string.Format(UIStrings.SetupBtnBoosterAvailable.GetText(), _triMap.RemainingStartingDrives),
                        () =>
                        {
                            if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected && !Networking.NetworkManager.Instance.IsHost)
                            {
                                Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO { action = "SelectStartingUpgrade", upgradeType = "Booster" });
                                return;
                            }
                            _triMap.SelectStartingUpgrade(FlagshipUpgrade.Drive);
                        });
                }
                if (_triMap.RemainingStartingCannons > 0)
                {
                    _foundingPanel.AddActionButton(
                        UIStrings.SetupBtnCannonAvailable.GetText(),
                        () =>
                        {
                            if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected && !Networking.NetworkManager.Instance.IsHost)
                            {
                                Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO { action = "SelectStartingUpgrade", upgradeType = "Cannon" });
                                return;
                            }
                            _triMap.SelectStartingUpgrade(FlagshipUpgrade.Cannon);
                        });
                }
                if (_triMap.RemainingStartingCargoModules > 0)
                {
                    _foundingPanel.AddActionButton(
                        UIStrings.SetupBtnFreightPodAvailable.GetText(),
                        () =>
                        {
                            if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected && !Networking.NetworkManager.Instance.IsHost)
                            {
                                Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO { action = "SelectStartingUpgrade", upgradeType = "FreightPod" });
                                return;
                            }
                            _triMap.SelectStartingUpgrade(FlagshipUpgrade.CargoModule);
                        });
                }
                break;
        }
    }
}
