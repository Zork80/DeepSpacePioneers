using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// State-driven action sub-panel at the bottom of the left sidebar.
/// Replaces the legacy code that dynamically created and destroyed buttons every turn.
/// Manages sub-panels for Production roll, Trade & Build, Flight, Placement, and AI turns.
/// </summary>
public class ActionPanelUI : MonoBehaviour
{
    public class Callbacks
    {
        public UnityAction OnRollProduction;
        public UnityAction<StarshipType> OnBeginShipPlacement;
        public UnityAction OnBeginSpaceportUpgrade;
        public UnityAction<FlagshipUpgrade> OnBuildUpgrade;
        public UnityAction OnCancelShipPlacement;
        public UnityAction OnCancelSpaceportUpgrade;
        public UnityAction OnCancelSpaceJump;
        public UnityAction OnOpenTrade;
        public UnityAction OnToggleBuildingCosts;
        public UnityAction<Tri> OnFoundColony;
        public UnityAction<Tri> OnEstablishTradeStation;
        public UnityAction OnRollFlightImpulse;
        public UnityAction OnCycleMovableShips;
        public UnityAction OnDeployCapturedTradeShip;
        public UnityAction OnBuyFame;
        public UnityAction OnRichHelpsPoor;
        public System.Action<string, RectTransform> OnShowCostTooltip;
        public System.Action OnHideCostTooltip;
    }

    private TriMap _triMap;
    private Callbacks _callbacks;
    private Font _font;

    // Sub-panels
    private GameObject _aiTurnPanel;
    private Text _aiTurnText;
    private Button _forceAiButton;

    private GameObject _rollPanel;
    private Text _rollPromptText;
    private Button _rollDiceButton;

    private GameObject _placementPanel;
    private Text _placementText;
    private Button _cancelPlacementButton;

    private GameObject _tradeAndBuildPanel;
    private Text _tradeAndBuildPromptText;
    private GameObject _tradeAndBuildContextContainer;
    private Button _tradeAndBuildContextButton;
    private Text _tradeAndBuildContextButtonText;
    private Tri _contextTargetTri;
    private bool _contextIsColony;

    private GameObject _tradeAndBuildToggleRow;
    private Button _buildCategoryToggleBtn;
    private Text _buildCategoryToggleText;
    private Button _tradeCategoryToggleBtn;
    private Text _tradeCategoryToggleText;
    private GameObject _buildContainer;
    private GameObject _tradeContainer;
    private bool _isBuildExpanded = true;
    private bool _isTradeExpanded = true;

    private Button _colonyShipButton;
    private Button _tradeShipButton;
    private Button _spaceportButton;
    private Button _boosterButton;
    private Button _cannonButton;
    private Button _freightPodButton;
    private GameObject _deployCapturedShipRow;
    private Button _deployCapturedShipButton;
    private Text _deployCapturedShipButtonText;
    private Button _tradeButton;
    private Button _buildingCostsButton;

    private GameObject _diplomatRow;
    private Button _buyFameButton;
    private Button _richHelpsPoorButton;

    private GameObject _flightPanel;
    private GameObject _flightPendingShakeContainer;
    private Text _flightPendingPromptText;
    private Button _flightShakeButton;
    private GameObject _flightActiveContainer;
    private Text _flightStatusText;
    private Button _flightContextButton;
    private Text _flightContextButtonText;
    private Button _flightCycleShipsButton;
    private Text _flightCycleShipsButtonText;
    private Tri _flightContextTargetTri;
    private bool _flightContextIsColony;
    private bool _flightContextIsSpaceJumpCancel;

    private LayoutElement _layoutElement;

    public static ActionPanelUI Create(Transform parent, TriMap triMap, Callbacks callbacks, Font font)
    {
        GameObject go = UIFactory.CreateElement("ActionPanel", parent, typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        Image bg = go.GetComponent<Image>();
        bg.color = new Color(0.035f, 0.09f, 0.12f, 1f);
        bg.raycastTarget = true;

        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.minHeight = 40f;
        layout.preferredHeight = 50f;
        layout.flexibleHeight = 0f;

        ActionPanelUI panel = go.AddComponent<ActionPanelUI>();
        panel.Initialize(triMap, callbacks, font);
        return panel;
    }

    public void Initialize(TriMap triMap, Callbacks callbacks, Font font)
    {
        _triMap = triMap;
        _callbacks = callbacks;
        _font = font;

        _layoutElement = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();

        UIFactory.AddVerticalLayout(gameObject, spacing: 4f, padding: 6);

        BuildAiTurnPanel();
        BuildRollPanel();
        BuildPlacementPanel();
        BuildTradeAndBuildPanel();
        BuildFlightPanel();

        UpdateLanguage();
    }

    private void BuildAiTurnPanel()
    {
        _aiTurnPanel = UIFactory.CreateElement("AiTurnPanel", transform);
        UIFactory.AddVerticalLayout(_aiTurnPanel, spacing: 4f, padding: 4);
        _aiTurnText = UIFactory.CreateText(_aiTurnPanel.transform, _font, string.Empty, 13, new Color(0.85f, 0.85f, 0.85f), TextAnchor.MiddleCenter, FontStyle.Italic);

        _forceAiButton = UIFactory.CreateButton(
            _aiTurnPanel.transform,
            _font,
            "⏩ An KI übergeben",
            OnForceAiClicked,
            new Color(0.65f, 0.35f, 0.12f, 1f),
            fontSize: 11,
            preferredHeight: 28f);
        _forceAiButton.gameObject.SetActive(false);

        _aiTurnPanel.SetActive(false);
    }

    private void OnForceAiClicked()
    {
        if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected)
        {
            Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO
            {
                action = "ForceAiTakeover"
            });
        }
    }

    private void BuildRollPanel()
    {
        _rollPanel = UIFactory.CreateElement("RollPanel", transform);
        UIFactory.AddVerticalLayout(_rollPanel, spacing: 8f, padding: 4);

        _rollPromptText = UIFactory.CreateText(_rollPanel.transform, _font, UIStrings.ProductionDiceRollingPrompt.GetText(), 12, Color.white, TextAnchor.MiddleCenter);

        _rollDiceButton = UIFactory.CreateButton(
            _rollPanel.transform,
            _font,
            UIStrings.RollProductionDiceButton.GetText(),
            () => _callbacks?.OnRollProduction?.Invoke(),
            new Color(0.18f, 0.5f, 0.8f, 1f),
            fontSize: 14,
            preferredHeight: 38f);

        _rollPanel.SetActive(false);
    }

    private void BuildPlacementPanel()
    {
        _placementPanel = UIFactory.CreateElement("PlacementPanel", transform);
        UIFactory.AddVerticalLayout(_placementPanel, spacing: 8f, padding: 4);

        _placementText = UIFactory.CreateText(_placementPanel.transform, _font, string.Empty, 12, Color.white, TextAnchor.MiddleCenter);

        _cancelPlacementButton = UIFactory.CreateButton(
            _placementPanel.transform,
            _font,
            UIStrings.Cancel.GetText(),
            () =>
            {
                if (_triMap != null && _triMap.IsPlacingShip)
                {
                    _callbacks?.OnCancelShipPlacement?.Invoke();
                }
                else if (_triMap != null && _triMap.IsUpgradingSpaceport)
                {
                    _callbacks?.OnCancelSpaceportUpgrade?.Invoke();
                }
            },
            new Color(0.55f, 0.2f, 0.2f, 1f),
            fontSize: 13,
            preferredHeight: 34f);

        _placementPanel.SetActive(false);
    }

    private void BuildTradeAndBuildPanel()
    {
        _tradeAndBuildPanel = UIFactory.CreateElement("TradeAndBuildPanel", transform);
        UIFactory.AddVerticalLayout(_tradeAndBuildPanel, spacing: 4f, padding: 2);

        _tradeAndBuildPromptText = UIFactory.CreateText(
            _tradeAndBuildPanel.transform,
            _font,
            UIStrings.TradeAndBuildInstruction.GetText(),
            11,
            Color.white,
            TextAnchor.UpperLeft);
        LayoutElement promptLayout = _tradeAndBuildPromptText.gameObject.AddComponent<LayoutElement>();
        promptLayout.minHeight = 30f;
        promptLayout.preferredHeight = 32f;
        promptLayout.flexibleWidth = 1f;

        // Contextual action button (Found Colony / Establish Trade Station)
        _tradeAndBuildContextContainer = UIFactory.CreateElement("ContextActionContainer", _tradeAndBuildPanel.transform);
        UIFactory.AddVerticalLayout(_tradeAndBuildContextContainer, spacing: 0f, padding: 0);
        _tradeAndBuildContextButton = UIFactory.CreateButton(
            _tradeAndBuildContextContainer.transform,
            _font,
            string.Empty,
            () =>
            {
                if (_contextTargetTri == null) return;
                if (_contextIsColony)
                {
                    _callbacks?.OnFoundColony?.Invoke(_contextTargetTri);
                }
                else
                {
                    _callbacks?.OnEstablishTradeStation?.Invoke(_contextTargetTri);
                }
            },
            new Color(0.1f, 0.55f, 0.28f, 1f),
            fontSize: 12,
            preferredHeight: 28f);
        _tradeAndBuildContextButtonText = _tradeAndBuildContextButton.GetComponentInChildren<Text>();
        _tradeAndBuildContextContainer.SetActive(false);

        // Category Toggle Row: [ Bauen ▲ ] [ Handeln ▲ ]
        _tradeAndBuildToggleRow = UIFactory.CreateElement("CategoryToggleRow", _tradeAndBuildPanel.transform, typeof(LayoutElement));
        LayoutElement toggleRowLayout = _tradeAndBuildToggleRow.GetComponent<LayoutElement>();
        toggleRowLayout.preferredHeight = 24f;
        toggleRowLayout.minHeight = 24f;
        HorizontalLayoutGroup toggleGroup = UIFactory.AddHorizontalLayout(_tradeAndBuildToggleRow, spacing: 4f, padding: 0);
        toggleGroup.childAlignment = TextAnchor.MiddleCenter;
        toggleGroup.childForceExpandHeight = true;

        _buildCategoryToggleBtn = UIFactory.CreateButton(
            _tradeAndBuildToggleRow.transform,
            _font,
            "▲ " + UIStrings.BuildCategory.GetText(),
            ToggleBuildCategory,
            new Color(0.14f, 0.32f, 0.5f, 1f),
            fontSize: 11,
            preferredHeight: 24f);
        _buildCategoryToggleBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        LayoutElement bLayout = _buildCategoryToggleBtn.GetComponent<LayoutElement>();
        bLayout.preferredWidth = 118f;
        bLayout.preferredHeight = 24f;
        bLayout.minHeight = 24f;
        bLayout.flexibleWidth = 1f;
        bLayout.flexibleHeight = 0f;
        _buildCategoryToggleText = _buildCategoryToggleBtn.GetComponentInChildren<Text>();
        _buildCategoryToggleText.fontStyle = FontStyle.Bold;

        _tradeCategoryToggleBtn = UIFactory.CreateButton(
            _tradeAndBuildToggleRow.transform,
            _font,
            "▲ " + UIStrings.TradeCategory.GetText(),
            ToggleTradeCategory,
            new Color(0.14f, 0.32f, 0.5f, 1f),
            fontSize: 11,
            preferredHeight: 24f);
        _tradeCategoryToggleBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        LayoutElement tLayout = _tradeCategoryToggleBtn.GetComponent<LayoutElement>();
        tLayout.preferredWidth = 118f;
        tLayout.preferredHeight = 24f;
        tLayout.minHeight = 24f;
        tLayout.flexibleWidth = 1f;
        tLayout.flexibleHeight = 0f;
        _tradeCategoryToggleText = _tradeCategoryToggleBtn.GetComponentInChildren<Text>();
        _tradeCategoryToggleText.fontStyle = FontStyle.Bold;

        // --- Bau-Container ---
        _buildContainer = UIFactory.CreateElement("BuildContainer", _tradeAndBuildPanel.transform);
        UIFactory.AddVerticalLayout(_buildContainer, spacing: 4f, padding: 0);

        // Row 1: Colony Ship | Trade Ship
        GameObject row1 = UIFactory.CreateElement("BuildRow1", _buildContainer.transform, typeof(LayoutElement));
        LayoutElement row1Layout = row1.GetComponent<LayoutElement>();
        row1Layout.preferredHeight = 28f;
        row1Layout.minHeight = 26f;
        UIFactory.AddHorizontalLayout(row1, spacing: 4f);
        _colonyShipButton = CreateActionBuildButton(row1.transform, UIStrings.ColonyShip.GetText(), () => _callbacks?.OnBeginShipPlacement?.Invoke(StarshipType.ColonyShip));
        AttachTooltipTrigger(_colonyShipButton, () => UIStrings.BuildingCostHeader.GetText() + " " + UIStrings.FormatCost(GameRules.ColonyShipCost));
        _tradeShipButton = CreateActionBuildButton(row1.transform, UIStrings.TradeShip.GetText(), () => _callbacks?.OnBeginShipPlacement?.Invoke(StarshipType.TradeShip));
        AttachTooltipTrigger(_tradeShipButton, () => UIStrings.BuildingCostHeader.GetText() + " " + UIStrings.FormatCost(GameRules.TradeShipCost));

        // Row 2: Spaceport | Booster
        GameObject row2 = UIFactory.CreateElement("BuildRow2", _buildContainer.transform, typeof(LayoutElement));
        LayoutElement row2Layout = row2.GetComponent<LayoutElement>();
        row2Layout.preferredHeight = 28f;
        row2Layout.minHeight = 26f;
        UIFactory.AddHorizontalLayout(row2, spacing: 4f);
        _spaceportButton = CreateActionBuildButton(row2.transform, UIStrings.Spaceport.GetText(), () => _callbacks?.OnBeginSpaceportUpgrade?.Invoke());
        AttachTooltipTrigger(_spaceportButton, () => UIStrings.BuildingCostHeader.GetText() + " " + UIStrings.FormatCost(GameRules.SpaceportCost));
        _boosterButton = CreateActionBuildButton(row2.transform, UIStrings.Booster.GetText(), () => _callbacks?.OnBuildUpgrade?.Invoke(FlagshipUpgrade.Drive));
        AttachTooltipTrigger(_boosterButton, () => UIStrings.BuildingCostHeader.GetText() + " " + UIStrings.FormatCost(GameRules.BoosterCost));

        // Row 3: Cannon | Freight Pod
        GameObject row3 = UIFactory.CreateElement("BuildRow3", _buildContainer.transform, typeof(LayoutElement));
        LayoutElement row3Layout = row3.GetComponent<LayoutElement>();
        row3Layout.preferredHeight = 28f;
        row3Layout.minHeight = 26f;
        UIFactory.AddHorizontalLayout(row3, spacing: 4f);
        _cannonButton = CreateActionBuildButton(row3.transform, UIStrings.Cannon.GetText(), () => _callbacks?.OnBuildUpgrade?.Invoke(FlagshipUpgrade.Cannon));
        AttachTooltipTrigger(_cannonButton, () => UIStrings.BuildingCostHeader.GetText() + " " + UIStrings.FormatCost(GameRules.CannonCost));
        _freightPodButton = CreateActionBuildButton(row3.transform, UIStrings.FreightPod.GetText(), () => _callbacks?.OnBuildUpgrade?.Invoke(FlagshipUpgrade.CargoModule));
        AttachTooltipTrigger(_freightPodButton, () => UIStrings.BuildingCostHeader.GetText() + " " + UIStrings.FormatCost(GameRules.FreightPodCost));

        // Deploy Captured Trade Ship Row (hidden unless reserve > 0)
        _deployCapturedShipRow = UIFactory.CreateElement("DeployCapturedShipRow", _buildContainer.transform, typeof(LayoutElement));
        LayoutElement rowDeployLayout = _deployCapturedShipRow.GetComponent<LayoutElement>();
        rowDeployLayout.preferredHeight = 28f;
        rowDeployLayout.minHeight = 26f;
        UIFactory.AddHorizontalLayout(_deployCapturedShipRow, spacing: 4f);
        _deployCapturedShipButton = CreateActionBuildButton(_deployCapturedShipRow.transform, UIStrings.DeployCapturedTradeShipButton.GetText(), () => _callbacks?.OnDeployCapturedTradeShip?.Invoke());
        _deployCapturedShipButtonText = _deployCapturedShipButton.GetComponentInChildren<Text>();
        AttachTooltipTrigger(_deployCapturedShipButton, () => UIStrings.DeployCapturedShipPlacingInstruction.GetText());
        _deployCapturedShipRow.SetActive(false);

        // --- Handels-Container ---
        _tradeContainer = UIFactory.CreateElement("TradeContainer", _tradeAndBuildPanel.transform);
        UIFactory.AddVerticalLayout(_tradeContainer, spacing: 4f, padding: 0);

        // Row 4: Trade Button | Costs Overview Button
        GameObject row4 = UIFactory.CreateElement("BuildRow4", _tradeContainer.transform, typeof(LayoutElement));
        LayoutElement row4Layout = row4.GetComponent<LayoutElement>();
        row4Layout.preferredHeight = 28f;
        row4Layout.minHeight = 26f;
        UIFactory.AddHorizontalLayout(row4, spacing: 4f);
        _tradeButton = UIFactory.CreateButton(
            row4.transform,
            _font,
            UIStrings.TradeButton.GetText(),
            () => _callbacks?.OnOpenTrade?.Invoke(),
            new Color(0.12f, 0.3f, 0.48f, 1f),
            fontSize: 11,
            preferredHeight: 26f);
        _buildingCostsButton = UIFactory.CreateButton(
            row4.transform,
            _font,
            UIStrings.BuildingCostsOverviewTitle.GetText(),
            () => _callbacks?.OnToggleBuildingCosts?.Invoke(),
            new Color(0.2f, 0.42f, 0.55f, 1f),
            fontSize: 11,
            preferredHeight: 26f);

        // Row 5: Diplomat actions (Buy Fame & Rich Helps Poor)
        _diplomatRow = UIFactory.CreateElement("DiplomatRow", _tradeContainer.transform, typeof(LayoutElement));
        LayoutElement dipLayout = _diplomatRow.GetComponent<LayoutElement>();
        dipLayout.preferredHeight = 26f;
        dipLayout.minHeight = 24f;
        UIFactory.AddHorizontalLayout(_diplomatRow, spacing: 4f);
        _buyFameButton = UIFactory.CreateButton(
            _diplomatRow.transform,
            _font,
            UIStrings.BuyFameButton.GetText(),
            () => _callbacks?.OnBuyFame?.Invoke(),
            new Color(0.55f, 0.2f, 0.6f, 1f),
            fontSize: 10,
            preferredHeight: 24f);
        _richHelpsPoorButton = UIFactory.CreateButton(
            _diplomatRow.transform,
            _font,
            UIStrings.RichHelpsPoorButton.GetText(),
            () => _callbacks?.OnRichHelpsPoor?.Invoke(),
            new Color(0.45f, 0.25f, 0.65f, 1f),
            fontSize: 10,
            preferredHeight: 24f);
        _diplomatRow.SetActive(false);

        _tradeAndBuildPanel.SetActive(false);
    }

    private void ToggleBuildCategory()
    {
        _isBuildExpanded = !_isBuildExpanded;
        UpdateCategoryToggles();
    }

    private void ToggleTradeCategory()
    {
        _isTradeExpanded = !_isTradeExpanded;
        UpdateCategoryToggles();
    }

    private void UpdateCategoryToggles()
    {
        if (_buildCategoryToggleText != null)
        {
            _buildCategoryToggleText.text = (_isBuildExpanded ? "▲ " : "▼ ") + UIStrings.BuildCategory.GetText();
        }
        if (_buildCategoryToggleBtn != null)
        {
            Image img = _buildCategoryToggleBtn.GetComponent<Image>();
            if (img != null)
            {
                img.color = _isBuildExpanded ? new Color(0.16f, 0.38f, 0.58f, 1f) : new Color(0.08f, 0.18f, 0.28f, 0.8f);
            }
        }
        if (_buildContainer != null)
        {
            _buildContainer.SetActive(_isBuildExpanded);
        }

        if (_tradeCategoryToggleText != null)
        {
            _tradeCategoryToggleText.text = (_isTradeExpanded ? "▲ " : "▼ ") + UIStrings.TradeCategory.GetText();
        }
        if (_tradeCategoryToggleBtn != null)
        {
            Image img = _tradeCategoryToggleBtn.GetComponent<Image>();
            if (img != null)
            {
                img.color = _isTradeExpanded ? new Color(0.16f, 0.38f, 0.58f, 1f) : new Color(0.08f, 0.18f, 0.28f, 0.8f);
            }
        }
        if (_tradeContainer != null)
        {
            _tradeContainer.SetActive(_isTradeExpanded);
        }

        UpdateTradeAndBuildHeight();
    }

    private void UpdateTradeAndBuildHeight()
    {
        float h = 68f; // prompt (32f) + togglerow (24f) + spacing (8f) + padding (4f)
        if (_tradeAndBuildContextContainer != null && _tradeAndBuildContextContainer.activeSelf)
        {
            h += 32f;
        }
        if (_isBuildExpanded)
        {
            h += 96f; // 3 rows * ~32f
        }
        if (_isTradeExpanded)
        {
            h += 32f; // trade row
            if (_diplomatRow != null && _diplomatRow.activeSelf)
            {
                h += 30f;
            }
        }
        SetPanelHeight(h, h);
    }

    private void SetPanelHeight(float minH, float prefH)
    {
        if (_layoutElement != null)
        {
            _layoutElement.minHeight = minH;
            _layoutElement.preferredHeight = prefH;
        }
    }

    private Button CreateActionBuildButton(Transform parent, string label, UnityAction onClick)
    {
        return UIFactory.CreateButton(
            parent,
            _font,
            label,
            onClick,
            new Color(0.12f, 0.3f, 0.48f, 1f),
            fontSize: 11,
            preferredHeight: 26f);
    }

    private void AttachTooltipTrigger(Button button, System.Func<string> tooltipProvider)
    {
        if (button == null) return;

        // Overlay transparent child with EventTrigger to catch hover even when button is disabled
        GameObject triggerOverlay = UIFactory.CreateElement("TooltipTriggerOverlay", button.transform, typeof(CanvasRenderer), typeof(Image), typeof(EventTrigger));
        Image img = triggerOverlay.GetComponent<Image>();
        img.color = Color.clear;
        img.raycastTarget = true;
        UIFactory.Stretch(triggerOverlay.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        EventTrigger trigger = triggerOverlay.GetComponent<EventTrigger>();
        RectTransform btnRect = button.GetComponent<RectTransform>();

        var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener((_) =>
        {
            if (_callbacks?.OnShowCostTooltip != null && tooltipProvider != null)
            {
                _callbacks.OnShowCostTooltip(tooltipProvider(), btnRect);
            }
        });
        trigger.triggers.Add(enterEntry);

        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener((_) =>
        {
            _callbacks?.OnHideCostTooltip?.Invoke();
        });
        trigger.triggers.Add(exitEntry);

        var clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        clickEntry.callback.AddListener((data) =>
        {
            if (button.interactable && data is PointerEventData ped && ped.button == PointerEventData.InputButton.Left)
            {
                button.onClick.Invoke();
            }
        });
        trigger.triggers.Add(clickEntry);
    }

    private void BuildFlightPanel()
    {
        _flightPanel = UIFactory.CreateElement("FlightPanel", transform);
        UIFactory.AddVerticalLayout(_flightPanel, spacing: 6f, padding: 2);

        // Shake Mothership container
        _flightPendingShakeContainer = UIFactory.CreateElement("FlightPendingContainer", _flightPanel.transform);
        UIFactory.AddVerticalLayout(_flightPendingShakeContainer, spacing: 6f, padding: 0);

        _flightPendingPromptText = UIFactory.CreateText(
            _flightPendingShakeContainer.transform,
            _font,
            UIStrings.FlightRollPendingPrompt.GetText(),
            12,
            Color.white,
            TextAnchor.MiddleCenter);

        _flightShakeButton = UIFactory.CreateButton(
            _flightPendingShakeContainer.transform,
            _font,
            UIStrings.EngageFlightDrivesButton.GetText(),
            () => _callbacks?.OnRollFlightImpulse?.Invoke(),
            new Color(0.18f, 0.5f, 0.8f, 1f),
            fontSize: 13,
            preferredHeight: 38f);

        // Active Flight container
        _flightActiveContainer = UIFactory.CreateElement("FlightActiveContainer", _flightPanel.transform);
        UIFactory.AddVerticalLayout(_flightActiveContainer, spacing: 4f, padding: 0);

        _flightStatusText = UIFactory.CreateText(
            _flightActiveContainer.transform,
            _font,
            UIStrings.FlightPhaseInstruction.GetText(),
            11,
            Color.white,
            TextAnchor.UpperLeft);

        _flightContextButton = UIFactory.CreateButton(
            _flightActiveContainer.transform,
            _font,
            string.Empty,
            () =>
            {
                if (_flightContextIsSpaceJumpCancel)
                {
                    if (_callbacks?.OnCancelSpaceJump != null)
                    {
                        _callbacks.OnCancelSpaceJump.Invoke();
                    }
                    else
                    {
                        PlayerHandler.Instance?.OpenSpaceJumpShipChoice();
                    }
                    return;
                }
                if (_flightContextTargetTri == null) return;
                if (_flightContextIsColony)
                {
                    _callbacks?.OnFoundColony?.Invoke(_flightContextTargetTri);
                }
                else
                {
                    _callbacks?.OnEstablishTradeStation?.Invoke(_flightContextTargetTri);
                }
            },
            new Color(0.1f, 0.55f, 0.28f, 1f),
            fontSize: 13,
            preferredHeight: 32f);
        _flightContextButtonText = _flightContextButton.GetComponentInChildren<Text>();
        _flightContextButton.gameObject.SetActive(false);

        _flightCycleShipsButton = UIFactory.CreateButton(
            _flightActiveContainer.transform,
            _font,
            string.Empty,
            () => _callbacks?.OnCycleMovableShips?.Invoke(),
            new Color(0.18f, 0.45f, 0.7f, 1f),
            fontSize: 12,
            preferredHeight: 30f);
        _flightCycleShipsButtonText = _flightCycleShipsButton.GetComponentInChildren<Text>();
        _flightCycleShipsButton.gameObject.SetActive(false);

        _flightPanel.SetActive(false);
    }

    public void Refresh(Player player, Unit selectedUnit)
    {
        if (_triMap == null)
        {
            gameObject.SetActive(false);
            return;
        }

        bool isFounding = _triMap.CurrentFoundingPhase != FoundingPhase.Complete;
        bool isFoundingPlacement = isFounding && (
            _triMap.CurrentFoundingPhase == FoundingPhase.SelectSpaceport ||
            _triMap.CurrentFoundingPhase == FoundingPhase.SelectStartingShipPoint ||
            _triMap.CurrentFoundingPhase == FoundingPhase.PlaceFirstColony ||
            _triMap.CurrentFoundingPhase == FoundingPhase.PlaceSecondColony ||
            _triMap.CurrentFoundingPhase == FoundingPhase.PlaceThirdColony);

        bool isProductionPhase = !isFounding && _triMap.CurrentPhase == GameTurnPhase.Production;
        bool isTradeAndBuild = !isFounding && _triMap.CurrentPhase == GameTurnPhase.TradeAndBuild;
        bool isFlightPhase = !isFounding && _triMap.CurrentPhase == GameTurnPhase.Flight;

        bool showPanel = isTradeAndBuild || isFlightPhase || (isProductionPhase && _triMap.LastProductionRoll == null) || isFoundingPlacement;
        gameObject.SetActive(showPanel);
        if (!showPanel)
        {
            return;
        }

        bool isLocalTurn = true;
        if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected)
        {
            isLocalTurn = _triMap.CurrentPlayerIndex == Networking.NetworkManager.Instance.LocalPlayerIndex;
        }

        if (!isLocalTurn)
        {
            ShowAiTurn(_triMap.CurrentPlayer);
            return;
        }

        bool isAiTurn = player != null && player.Type == Player.PlayerType.AI;
        if (isAiTurn)
        {
            ShowAiTurn(player);
            return;
        }

        if (isFoundingPlacement)
        {
            string prompt = _triMap.CurrentFoundingPhase == FoundingPhase.SelectSpaceport
                ? UIStrings.StatusSetupSpaceport.GetText()
                : (_triMap.CurrentFoundingPhase == FoundingPhase.SelectStartingShipPoint
                    ? UIStrings.StatusSetupStartingShip.GetText()
                    : string.Format(UIStrings.StatusSetupColony.GetText(), _triMap.FoundingRound));
            ShowFoundingInstruction(prompt);
            return;
        }

        if (_triMap.IsPlacingShip)
        {
            ShowShipPlacement();
            return;
        }

        if (_triMap.IsUpgradingSpaceport)
        {
            ShowSpaceportPlacement();
            return;
        }

        if (isProductionPhase && _triMap.LastProductionRoll == null)
        {
            ShowProductionRoll();
            return;
        }

        if (isFlightPhase)
        {
            ShowFlightPhase(player, selectedUnit);
            return;
        }

        if (isTradeAndBuild)
        {
            ShowTradeAndBuild(player, selectedUnit);
        }
    }

    private void ShowAiTurn(Player player)
    {
        HideAllSubPanels();
        _aiTurnPanel.SetActive(true);

        bool isHost = Networking.NetworkManager.Instance != null &&
                      Networking.NetworkManager.Instance.IsClientConnected &&
                      Networking.NetworkManager.Instance.Client.IsHost;
        bool isHumanOpponent = player != null && player.Type != Player.PlayerType.AI;

        if (_triMap != null && _triMap.CurrentFoundingPhase != FoundingPhase.Complete)
        {
            _aiTurnText.text = isHumanOpponent
                ? string.Format(UIStrings.PlayerTurnWithPhase.GetText(), player != null ? player.PlayerName : string.Empty, UIStrings.PhaseSetup.GetText())
                : string.Format(UIStrings.AiTurnInProgress.GetText(), player != null ? player.PlayerName : string.Empty);
        }
        else if (isHumanOpponent)
        {
            _aiTurnText.text = string.Format(UIStrings.PlayerTurnInProgress.GetText(), player != null ? player.PlayerName : string.Empty);
        }
        else
        {
            _aiTurnText.text = string.Format(UIStrings.AiTurnInProgress.GetText(), player != null ? player.PlayerName : string.Empty);
        }

        if (_forceAiButton != null)
        {
            bool showForceBtn = isHost && isHumanOpponent;
            _forceAiButton.gameObject.SetActive(showForceBtn);
            SetPanelHeight(showForceBtn ? 68f : 32f, showForceBtn ? 72f : 36f);
        }
        else
        {
            SetPanelHeight(32f, 36f);
        }
    }

    private void ShowFoundingInstruction(string instruction)
    {
        HideAllSubPanels();
        _placementPanel.SetActive(true);
        if (_cancelPlacementButton != null) _cancelPlacementButton.gameObject.SetActive(false);
        _placementText.text = instruction;
        SetPanelHeight(48f, 54f);
    }

    private void ShowProductionRoll()
    {
        HideAllSubPanels();
        _rollPanel.SetActive(true);
        _rollPromptText.text = UIStrings.ProductionDiceRollingPrompt.GetText();
        SetPanelHeight(72f, 76f);
    }

    private void ShowShipPlacement()
    {
        HideAllSubPanels();
        _placementPanel.SetActive(true);
        if (_cancelPlacementButton != null) _cancelPlacementButton.gameObject.SetActive(true);
        if (_triMap != null && _triMap.IsPlacingCapturedShip)
        {
            _placementText.text = UIStrings.DeployCapturedShipPlacingInstruction.GetText();
        }
        else
        {
            StarshipType shipType = _triMap.PendingShipPlacementType.GetValueOrDefault(StarshipType.ColonyShip);
            _placementText.text = string.Format(
                UIStrings.ShipBuildPlacingInstruction.GetText(),
                UIStrings.GetShipName(shipType));
        }
        SetPanelHeight(68f, 72f);
    }

    private void ShowSpaceportPlacement()
    {
        HideAllSubPanels();
        _placementPanel.SetActive(true);
        if (_cancelPlacementButton != null) _cancelPlacementButton.gameObject.SetActive(true);
        _placementText.text = UIStrings.StatusPlacingSpaceport.GetText();
        SetPanelHeight(68f, 72f);
    }

    private void ShowTradeAndBuild(Player player, Unit selectedUnit)
    {
        HideAllSubPanels();
        _tradeAndBuildPanel.SetActive(true);
        _tradeAndBuildPromptText.text = UIStrings.TradeAndBuildInstruction.GetText();

        // Contextual colony / trade station button
        Unit readyColonyShip = (selectedUnit != null && _triMap.CanFoundColonyWith(selectedUnit))
            ? selectedUnit
            : player?.Units.FirstOrDefault(u => _triMap.CanFoundColonyWith(u));
        Unit readyTradeShip = (selectedUnit != null && _triMap.CanEstablishTradeStationWith(selectedUnit))
            ? selectedUnit
            : player?.Units.FirstOrDefault(u => _triMap.CanEstablishTradeStationWith(u));

        if (readyColonyShip != null)
        {
            _contextTargetTri = readyColonyShip.Tri;
            _contextIsColony = true;
            _tradeAndBuildContextButtonText.text = UIStrings.FoundColonyButton.GetText();
            _tradeAndBuildContextContainer.SetActive(true);
        }
        else if (readyTradeShip != null)
        {
            _contextTargetTri = readyTradeShip.Tri;
            _contextIsColony = false;
            _tradeAndBuildContextButtonText.text = UIStrings.EstablishTradeStationButton.GetText();
            _tradeAndBuildContextContainer.SetActive(true);
        }
        else
        {
            _tradeAndBuildContextContainer.SetActive(false);
        }

        // Button interactabilities
        UpdateButtonInteractable(_colonyShipButton, _triMap.CanBuildShip(StarshipType.ColonyShip));
        UpdateButtonInteractable(_tradeShipButton, _triMap.CanBuildShip(StarshipType.TradeShip));
        UpdateButtonInteractable(_spaceportButton, _triMap.CanBuildSpaceport());
        UpdateButtonInteractable(_boosterButton, _triMap.CanBuildUpgrade(FlagshipUpgrade.Drive));
        UpdateButtonInteractable(_cannonButton, _triMap.CanBuildUpgrade(FlagshipUpgrade.Cannon));
        UpdateButtonInteractable(_freightPodButton, _triMap.CanBuildUpgrade(FlagshipUpgrade.CargoModule));

        // Captured trade ship deployment button
        if (_deployCapturedShipRow != null)
        {
            if (player != null && player.CapturedTradeShipsCount > 0)
            {
                _deployCapturedShipRow.SetActive(true);
                if (_deployCapturedShipButtonText != null)
                {
                    _deployCapturedShipButtonText.text = string.Format(UIStrings.DeployCapturedTradeShipButton.GetText(), player.CapturedTradeShipsCount);
                }
                UpdateButtonInteractable(_deployCapturedShipButton, _triMap.CanDeployCapturedTradeShip(player), new Color(0.18f, 0.48f, 0.35f, 1f));
            }
            else
            {
                _deployCapturedShipRow.SetActive(false);
            }
        }

        // Diplomat cards
        if (player != null && player.Type != Player.PlayerType.AI)
        {
            bool hasBoughtFame = player.HasFriendshipCard(FriendshipCardId.DiplomatBoughtFame1) || player.HasFriendshipCard(FriendshipCardId.DiplomatBoughtFame2);
            bool canUseBoughtFame = hasBoughtFame && !player.HasUsedBoughtFameThisTurn && player.GetResourceCount(ResourceType.TradeGoods) >= 1;

            bool hasRichHelpsPoor = player.HasFriendshipCard(FriendshipCardId.DiplomatRichHelpsPoor);
            List<Player> richerPlayers = _triMap.Players != null
                ? _triMap.Players.Where(p => p != player && p.VictoryPoints > player.VictoryPoints && p.TotalResources > 0).ToList()
                : new List<Player>();
            bool canUseRichHelpsPoor = hasRichHelpsPoor && !player.HasUsedRichHelpsPoorThisTurn && richerPlayers.Count > 0;

            if (hasBoughtFame || hasRichHelpsPoor)
            {
                _diplomatRow.SetActive(true);
                _buyFameButton.gameObject.SetActive(hasBoughtFame);
                UpdateButtonInteractable(_buyFameButton, canUseBoughtFame, new Color(0.55f, 0.2f, 0.6f, 1f));

                _richHelpsPoorButton.gameObject.SetActive(hasRichHelpsPoor);
                UpdateButtonInteractable(_richHelpsPoorButton, canUseRichHelpsPoor, new Color(0.45f, 0.25f, 0.65f, 1f));
            }
            else
            {
                _diplomatRow.SetActive(false);
            }
        }
        else
        {
            _diplomatRow.SetActive(false);
        }

        UpdateCategoryToggles();
    }

    private void ShowFlightPhase(Player player, Unit selectedUnit)
    {
        HideAllSubPanels();
        _flightPanel.SetActive(true);

        bool pendingShake = _triMap.LastFlagshipRoll == null;
        _flightPendingShakeContainer.SetActive(pendingShake);
        _flightActiveContainer.SetActive(!pendingShake);

        if (pendingShake)
        {
            _flightPendingPromptText.text = UIStrings.FlightRollPendingPrompt.GetText();
            SetPanelHeight(72f, 78f);
            return;
        }

        int extraLines = 0;

        var movableShips = player?.Units?.Where(u => u != null && !u.IsDestroyed && u.MovementRemaining > 0).ToList();
        bool hasMovableShips = movableShips != null && movableShips.Count > 0;
        if (_flightCycleShipsButton != null)
        {
            _flightCycleShipsButton.gameObject.SetActive(hasMovableShips);
            if (hasMovableShips && _flightCycleShipsButtonText != null)
            {
                _flightCycleShipsButtonText.text = string.Format(UIStrings.CycleMovableShipsButton.GetText(), movableShips.Count);
            }
        }
        float cycleBtnHeight = hasMovableShips ? 34f : 0f;

        if (_triMap.IsExecutingSpaceJump && _triMap.PendingSpaceJumpShip != null)
        {
            _flightStatusText.text = UIStrings.SpaceJumpTargetPrompt.GetText();
            _flightContextTargetTri = null;
            _flightContextIsColony = false;

            var playerShips = _triMap.CurrentPlayer?.Units?.Where(u => u != null && !u.IsDestroyed).ToList();
            bool hasMultipleShips = playerShips != null && playerShips.Count > 1;

            if (hasMultipleShips)
            {
                _flightContextIsSpaceJumpCancel = true;
                _flightContextButtonText.text = UIStrings.ChangeSpaceJumpShipButton.GetText();
                _flightContextButton.gameObject.SetActive(true);
                SetPanelHeight(68f + cycleBtnHeight, 74f + cycleBtnHeight);
            }
            else
            {
                _flightContextIsSpaceJumpCancel = false;
                _flightContextButton.gameObject.SetActive(false);
                SetPanelHeight(34f + cycleBtnHeight, 40f + cycleBtnHeight);
            }
            return;
        }

        _flightContextIsSpaceJumpCancel = false;

        if (selectedUnit != null)
        {
            string status = string.Format(
                UIStrings.ShipStatusInfo.GetText(),
                UIStrings.GetShipName(selectedUnit.ShipType),
                selectedUnit.MovementRemaining,
                selectedUnit.Movement);

            if (selectedUnit.ShipType == StarshipType.TradeShip)
            {
                Player owner = _triMap.GetPlayerForUnit(selectedUnit) ?? player;
                int pods = owner != null ? owner.FlagshipFreightPodCount : 0;
                status += "\n" + string.Format(UIStrings.TradeShipFreightPodsStatus.GetText(), pods);
                extraLines++;
            }

            if (_triMap.CanFoundColonyWith(selectedUnit))
            {
                _flightStatusText.text = status;
                _flightContextTargetTri = selectedUnit.Tri;
                _flightContextIsColony = true;
                _flightContextButtonText.text = UIStrings.FoundColonyButton.GetText();
                _flightContextButton.gameObject.SetActive(true);
                SetPanelHeight(68f + (extraLines * 14f) + cycleBtnHeight, 74f + (extraLines * 14f) + cycleBtnHeight);
                return;
            }

            if (_triMap.CanEstablishTradeStationWith(selectedUnit))
            {
                _flightStatusText.text = status;
                _flightContextTargetTri = selectedUnit.Tri;
                _flightContextIsColony = false;
                _flightContextButtonText.text = UIStrings.EstablishTradeStationButton.GetText();
                _flightContextButton.gameObject.SetActive(true);
                SetPanelHeight(68f + (extraLines * 14f) + cycleBtnHeight, 74f + (extraLines * 14f) + cycleBtnHeight);
                return;
            }

            _flightContextButton.gameObject.SetActive(false);
            string hint = selectedUnit.MovementRemaining > 0
                ? UIStrings.ShipMoveInstruction.GetText()
                : UIStrings.ShipNoMovementRemaining.GetText();
            _flightStatusText.text = status + "\n" + hint;
            extraLines++;
            SetPanelHeight(42f + (extraLines * 14f) + cycleBtnHeight, 46f + (extraLines * 14f) + cycleBtnHeight);
            return;
        }

        Unit readyColonyShip = player?.Units.FirstOrDefault(u => _triMap.CanFoundColonyWith(u));
        Unit readyTradeShip = player?.Units.FirstOrDefault(u => _triMap.CanEstablishTradeStationWith(u));

        if (readyColonyShip != null)
        {
            _flightStatusText.text = UIStrings.ShipReadyToFoundColony.GetText();
            _flightContextTargetTri = readyColonyShip.Tri;
            _flightContextIsColony = true;
            _flightContextButtonText.text = UIStrings.FoundColonyButton.GetText();
            _flightContextButton.gameObject.SetActive(true);
            SetPanelHeight(68f + cycleBtnHeight, 74f + cycleBtnHeight);
            return;
        }

        if (readyTradeShip != null)
        {
            _flightStatusText.text = UIStrings.ShipReadyToEstablishTradeStation.GetText();
            _flightContextTargetTri = readyTradeShip.Tri;
            _flightContextIsColony = false;
            _flightContextButtonText.text = UIStrings.EstablishTradeStationButton.GetText();
            _flightContextButton.gameObject.SetActive(true);
            SetPanelHeight(68f + cycleBtnHeight, 74f + cycleBtnHeight);
            return;
        }

        _flightContextButton.gameObject.SetActive(false);
        _flightStatusText.text = UIStrings.FlightPhaseInstruction.GetText();
        SetPanelHeight(40f + cycleBtnHeight, 44f + cycleBtnHeight);
    }

    private void UpdateButtonInteractable(Button btn, bool interactable, Color? activeColor = null)
    {
        if (btn == null) return;
        btn.interactable = true;
        Image img = btn.GetComponent<Image>();
        if (img != null)
        {
            Color baseColor = activeColor ?? new Color(0.12f, 0.3f, 0.48f, 1f);
            img.color = interactable ? baseColor : new Color(baseColor.r * 0.42f, baseColor.g * 0.42f, baseColor.b * 0.42f, 0.75f);
        }
        Text txt = btn.GetComponentInChildren<Text>();
        if (txt != null)
        {
            txt.color = interactable ? Color.white : new Color(0.65f, 0.68f, 0.72f);
        }
    }

    private void HideAllSubPanels()
    {
        if (_aiTurnPanel != null) _aiTurnPanel.SetActive(false);
        if (_rollPanel != null) _rollPanel.SetActive(false);
        if (_placementPanel != null) _placementPanel.SetActive(false);
        if (_tradeAndBuildPanel != null) _tradeAndBuildPanel.SetActive(false);
        if (_flightPanel != null) _flightPanel.SetActive(false);
    }

    public void UpdateLanguage()
    {
        if (_rollPromptText != null) _rollPromptText.text = UIStrings.ProductionDiceRollingPrompt.GetText();
        if (_rollDiceButton != null) _rollDiceButton.GetComponentInChildren<Text>().text = UIStrings.RollProductionDiceButton.GetText();
        if (_cancelPlacementButton != null) _cancelPlacementButton.GetComponentInChildren<Text>().text = UIStrings.Cancel.GetText();
        if (_tradeAndBuildPromptText != null) _tradeAndBuildPromptText.text = UIStrings.TradeAndBuildInstruction.GetText();

        if (_buildCategoryToggleText != null)
        {
            _buildCategoryToggleText.text = (_isBuildExpanded ? "▲ " : "▼ ") + UIStrings.BuildCategory.GetText();
        }
        if (_tradeCategoryToggleText != null)
        {
            _tradeCategoryToggleText.text = (_isTradeExpanded ? "▲ " : "▼ ") + UIStrings.TradeCategory.GetText();
        }

        if (_colonyShipButton != null) _colonyShipButton.GetComponentInChildren<Text>().text = UIStrings.ColonyShip.GetText();
        if (_tradeShipButton != null) _tradeShipButton.GetComponentInChildren<Text>().text = UIStrings.TradeShip.GetText();
        if (_spaceportButton != null) _spaceportButton.GetComponentInChildren<Text>().text = UIStrings.Spaceport.GetText();
        if (_boosterButton != null) _boosterButton.GetComponentInChildren<Text>().text = UIStrings.Booster.GetText();
        if (_cannonButton != null) _cannonButton.GetComponentInChildren<Text>().text = UIStrings.Cannon.GetText();
        if (_freightPodButton != null) _freightPodButton.GetComponentInChildren<Text>().text = UIStrings.FreightPod.GetText();
        if (_tradeButton != null) _tradeButton.GetComponentInChildren<Text>().text = UIStrings.TradeButton.GetText();
        if (_buildingCostsButton != null) _buildingCostsButton.GetComponentInChildren<Text>().text = UIStrings.BuildingCostsOverviewTitle.GetText();
        if (_buyFameButton != null) _buyFameButton.GetComponentInChildren<Text>().text = UIStrings.BuyFameButton.GetText();
        if (_richHelpsPoorButton != null) _richHelpsPoorButton.GetComponentInChildren<Text>().text = UIStrings.RichHelpsPoorButton.GetText();

        if (_flightPendingPromptText != null) _flightPendingPromptText.text = UIStrings.FlightRollPendingPrompt.GetText();
        if (_flightShakeButton != null) _flightShakeButton.GetComponentInChildren<Text>().text = UIStrings.EngageFlightDrivesButton.GetText();
    }
}
