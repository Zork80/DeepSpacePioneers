using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Dedicated view component for the persistent left player HUD sidebar.
/// Manages nested containers for PlayerHeader, ResourcesPanel, FlagshipPanel, and ActionPanel.
/// Subscribes directly to player and turn phase events to update dirty sections reactively.
/// </summary>
public class SidebarViewUI : MonoBehaviour
{
    [Header("Hierarchy Containers")]
    [SerializeField] private RectTransform _playerHeader;
    [SerializeField] private RectTransform _resourcesPanel;
    [SerializeField] private RectTransform _mothershipPanel;
    [SerializeField] private ActionPanelUI _actionPanel;

    [Header("Player Header UI")]
    [SerializeField] private Image _playerColorSwatch;
    [SerializeField] private Text _playerNameText;
    [SerializeField] private Button _nextButton;
    [SerializeField] private Image _nextButtonImage;
    [SerializeField] private Text _nextButtonLabel;
    [SerializeField] private Button _quitButton;
    [SerializeField] private Text _turnStatusText;

    [Header("Resources Panel UI")]
    [SerializeField] private Text _resourceHeadingText;
    [SerializeField] private Text _resourceInventoryText;
    private Text _resourceToggleText;
    private LayoutElement _resourceLayout;
    private bool _showResourceDetails = true;

    [Header("Mothership Panel UI")]
    [SerializeField] private Text _mothershipHeadingText;
    [SerializeField] private Text _mothershipToggleText;
    [SerializeField] private Text _mothershipInventoryText;

    private TriMap _triMap;
    private SelectionController _selectionController;
    private UnityAction _onNextClicked;
    private UnityAction _onQuitClicked;
    private Font _font;

    private Player _displayedPlayer;
    private bool _showFriendshipDetails = false;
    private LayoutElement _mothershipLayout;
    private bool _subscribedToTriMap = false;
    private GameObject _spacer;

    public ActionPanelUI ActionPanel => _actionPanel;

    public static SidebarViewUI AttachTo(
        GameObject targetObject,
        TriMap triMap,
        SelectionController selectionController,
        UnityAction onNextClicked,
        ActionPanelUI.Callbacks actionCallbacks,
        Font font,
        UnityAction onQuitClicked = null)
    {
        SidebarViewUI sidebar = targetObject.GetComponent<SidebarViewUI>() ?? targetObject.AddComponent<SidebarViewUI>();
        sidebar.Initialize(triMap, selectionController, onNextClicked, actionCallbacks, font, onQuitClicked);
        return sidebar;
    }

    public void Initialize(
        TriMap triMap,
        SelectionController selectionController,
        UnityAction onNextClicked,
        ActionPanelUI.Callbacks actionCallbacks,
        Font font,
        UnityAction onQuitClicked = null)
    {
        _triMap = triMap;
        _selectionController = selectionController;
        _onNextClicked = onNextClicked;
        _onQuitClicked = onQuitClicked;
        _font = font;

        EnsureHierarchy(actionCallbacks);
        SubscribeToTriMap();
        UpdateLanguage();

        if (_triMap != null && _triMap.CurrentPlayer != null)
        {
            SetDisplayedPlayer(ResolvePlayerForView(_triMap.CurrentPlayer));
        }
    }

    private void EnsureHierarchy(ActionPanelUI.Callbacks actionCallbacks)
    {
        // Configure left sidebar width and positioning
        if (transform.name == "LeftBar")
        {
            RectTransform selfRect = transform as RectTransform;
            if (selfRect != null)
            {
                selfRect.anchorMin = new Vector2(0f, 0f);
                selfRect.anchorMax = new Vector2(0f, 1f);
                selfRect.pivot = new Vector2(0.5f, 0.5f);
                selfRect.anchoredPosition = new Vector2(130f, 0f);
                selfRect.sizeDelta = new Vector2(260f, 0f);
            }
        }
        else
        {
            RectTransform sidebarRect = transform.parent as RectTransform;
            if (sidebarRect != null)
            {
                sidebarRect.sizeDelta = new Vector2(260f, sidebarRect.sizeDelta.y);
                sidebarRect.anchoredPosition = new Vector2(130f, sidebarRect.anchoredPosition.y);
            }

            RectTransform panelRect = transform as RectTransform;
            if (panelRect != null)
            {
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
            }
        }

        UIManager.RegisterHudElement(transform as RectTransform);

        Image panelBg = GetComponent<Image>();
        if (panelBg != null)
        {
            panelBg.enabled = true;
            panelBg.material = null;
            panelBg.color = new Color(0.015f, 0.035f, 0.045f, 0.97f);
            panelBg.raycastTarget = true;
        }

        // Configure vertical auto-layout on sidebar root
        VerticalLayoutGroup rootLayout = GetComponent<VerticalLayoutGroup>();
        if (rootLayout == null)
        {
            rootLayout = UIFactory.AddVerticalLayout(gameObject, spacing: 6f, padding: 8, controlWidth: true, controlHeight: true);
        }
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        // 1. Player Header Container
        if (_playerHeader == null)
        {
            BuildPlayerHeader();
        }

        // 2. Resources Panel Container
        if (_resourcesPanel == null)
        {
            BuildResourcesPanel();
        }

        // 3. Mothership Panel Container
        if (_mothershipPanel == null)
        {
            BuildMothershipPanel();
        }

        // 4. Action Panel Container
        if (_actionPanel == null)
        {
            _actionPanel = ActionPanelUI.Create(transform, _triMap, actionCallbacks, _font);
        }

        // 5. Spacer (pushes Next Turn Button to the bottom)
        if (_spacer == null)
        {
            BuildSpacer();
        }

        // 6. Next Turn Button (positioned at the bottom of the sidebar)
        if (_nextButton == null)
        {
            BuildNextTurnButton();
        }

        if (_nextButton != null)
        {
            _nextButton.transform.SetAsLastSibling();
        }
        if (_quitButton != null)
        {
            _quitButton.transform.SetAsLastSibling();
        }
        if (_spacer != null && _nextButton != null)
        {
            _spacer.transform.SetSiblingIndex(_nextButton.transform.GetSiblingIndex() - 1);
        }
    }

    private void BuildSpacer()
    {
        Transform existingSpacer = transform.Find("SidebarSpacer");
        if (existingSpacer != null)
        {
            _spacer = existingSpacer.gameObject;
        }
        else
        {
            _spacer = UIFactory.CreateElement("SidebarSpacer", transform, typeof(LayoutElement));
        }

        LayoutElement spacerLayout = _spacer.GetComponent<LayoutElement>() ?? _spacer.AddComponent<LayoutElement>();
        spacerLayout.flexibleHeight = 1f;
        spacerLayout.flexibleWidth = 1f;
        spacerLayout.minHeight = 0f;
        spacerLayout.preferredHeight = 0f;
    }

    private void BuildPlayerHeader()
    {
        GameObject headerObj = UIFactory.CreateElement("PlayerHeader", transform, typeof(LayoutElement));
        _playerHeader = headerObj.GetComponent<RectTransform>();

        LayoutElement layout = headerObj.GetComponent<LayoutElement>();
        layout.minHeight = 60f;
        layout.preferredHeight = 65f;
        layout.flexibleHeight = 0f;

        UIFactory.AddVerticalLayout(headerObj, spacing: 4f, padding: 2);

        // Name & Swatch Row
        GameObject nameRow = UIFactory.CreateElement("NameRow", headerObj.transform, typeof(LayoutElement));
        LayoutElement nameRowLayout = nameRow.GetComponent<LayoutElement>();
        nameRowLayout.preferredHeight = 32f;
        nameRowLayout.minHeight = 30f;
        HorizontalLayoutGroup nameLayout = UIFactory.AddHorizontalLayout(nameRow, spacing: 10f, padding: 0);
        nameLayout.childForceExpandWidth = false;
        nameLayout.childForceExpandHeight = false;
        nameLayout.childControlWidth = true;
        nameLayout.childControlHeight = true;

        // Color Swatch (fixed 26x26 square)
        GameObject swatchBorder = UIFactory.CreateElement("SwatchBorder", nameRow.transform, typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        LayoutElement swatchLayout = swatchBorder.GetComponent<LayoutElement>();
        swatchLayout.preferredWidth = 26f;
        swatchLayout.preferredHeight = 26f;
        swatchLayout.minWidth = 26f;
        swatchLayout.minHeight = 26f;
        swatchLayout.flexibleWidth = 0f;
        swatchLayout.flexibleHeight = 0f;

        Image borderImg = swatchBorder.GetComponent<Image>();
        borderImg.color = new Color(0.85f, 0.9f, 0.92f, 1f);
        borderImg.raycastTarget = false;

        GameObject swatchInner = UIFactory.CreateElement("SwatchInner", swatchBorder.transform, typeof(CanvasRenderer), typeof(Image));
        UIFactory.Stretch(swatchInner.GetComponent<RectTransform>(), new Vector2(3f, 3f), new Vector2(-3f, -3f));
        _playerColorSwatch = swatchInner.GetComponent<Image>();
        _playerColorSwatch.raycastTarget = false;

        // Player Name
        Transform existingName = transform.Find("PlayerName");
        if (existingName != null)
        {
            _playerNameText = existingName.GetComponent<Text>();
            existingName.SetParent(nameRow.transform, false);
        }
        else
        {
            _playerNameText = UIFactory.CreateText(nameRow.transform, _font, string.Empty, 22, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        LayoutElement nameTextLayout = _playerNameText.GetComponent<LayoutElement>() ?? _playerNameText.gameObject.AddComponent<LayoutElement>();
        nameTextLayout.preferredWidth = 190f;
        nameTextLayout.preferredHeight = 30f;
        nameTextLayout.flexibleWidth = 1f;

        _playerNameText.fontSize = 22;
        _playerNameText.fontStyle = FontStyle.Bold;
        _playerNameText.alignment = TextAnchor.MiddleLeft;
        _playerNameText.material = null; // Clear default scene font material which causes blurring
        _playerNameText.color = Color.white;
        _playerNameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _playerNameText.verticalOverflow = VerticalWrapMode.Overflow;
        _playerNameText.raycastTarget = false;

        // Turn Status Text
        _turnStatusText = UIFactory.CreateText(headerObj.transform, _font, string.Empty, 12, new Color(1f, 0.82f, 0.32f), TextAnchor.MiddleLeft, FontStyle.Bold);
        LayoutElement statusLayout = _turnStatusText.gameObject.AddComponent<LayoutElement>();
        statusLayout.preferredHeight = 26f;
        statusLayout.minHeight = 22f;
    }

    private void BuildNextTurnButton()
    {
        Transform existingButton = transform.Find("Button");
        GameObject buttonObj;
        if (existingButton != null)
        {
            buttonObj = existingButton.gameObject;
            buttonObj.transform.SetParent(transform, false);
            _nextButton = buttonObj.GetComponent<Button>();
            _nextButtonImage = buttonObj.GetComponent<Image>();
            _nextButtonLabel = buttonObj.GetComponentInChildren<Text>();
        }
        else
        {
            buttonObj = UIFactory.CreateElement("NextTurnButton", transform, typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            _nextButtonImage = buttonObj.GetComponent<Image>();
            _nextButton = buttonObj.GetComponent<Button>();
            _nextButton.targetGraphic = _nextButtonImage;
            _nextButtonLabel = UIFactory.CreateText(buttonObj.transform, _font, UIStrings.NextTurn.GetText(), 15, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(_nextButtonLabel.rectTransform, Vector2.zero, Vector2.zero);
        }

        buttonObj.transform.SetAsLastSibling();
        if (_spacer != null)
        {
            _spacer.transform.SetSiblingIndex(buttonObj.transform.GetSiblingIndex() - 1);
        }

        LayoutElement btnLayout = buttonObj.GetComponent<LayoutElement>() ?? buttonObj.AddComponent<LayoutElement>();
        btnLayout.preferredHeight = 40f;
        btnLayout.minHeight = 36f;
        btnLayout.flexibleWidth = 1f;
        btnLayout.flexibleHeight = 0f;

        if (_nextButtonLabel != null)
        {
            _nextButtonLabel.fontSize = 15;
            _nextButtonLabel.fontStyle = FontStyle.Bold;
            _nextButtonLabel.alignment = TextAnchor.MiddleCenter;
            _nextButtonLabel.text = UIStrings.NextTurn.GetText();
        }

        if (_nextButton != null)
        {
            _nextButton.onClick.RemoveAllListeners();
            for (int i = 0; i < _nextButton.onClick.GetPersistentEventCount(); i++)
            {
                _nextButton.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
            }
            _nextButton.onClick.AddListener(() =>
            {
                _onNextClicked?.Invoke();
            });
        }

        UpdateNextButtonState();

        if (_quitButton == null)
        {
            _quitButton = UIFactory.CreateButton(transform, _font, UIStrings.QuitGame.GetText(),
                () => _onQuitClicked?.Invoke(), new Color(0.38f, 0.12f, 0.12f, 0.85f), 11, 26f);
            LayoutElement quitLayout = _quitButton.GetComponent<LayoutElement>();
            quitLayout.preferredHeight = 26f;
            quitLayout.minHeight = 24f;
            quitLayout.flexibleWidth = 1f;
            quitLayout.flexibleHeight = 0f;
        }

        buttonObj.transform.SetAsLastSibling();
        _quitButton.transform.SetAsLastSibling();

        if (_spacer != null)
        {
            _spacer.transform.SetSiblingIndex(buttonObj.transform.GetSiblingIndex() - 1);
        }
    }

    private void BuildResourcesPanel()
    {
        GameObject panelObj = UIFactory.CreateElement("ResourcesPanel", transform, typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        _resourcesPanel = panelObj.GetComponent<RectTransform>();

        Image bg = panelObj.GetComponent<Image>();
        bg.color = new Color(0.035f, 0.09f, 0.12f, 1f);
        bg.raycastTarget = true;

        _resourceLayout = panelObj.GetComponent<LayoutElement>();
        _resourceLayout.minHeight = 135f;
        _resourceLayout.preferredHeight = 145f;
        _resourceLayout.flexibleHeight = 0f;

        UIFactory.AddVerticalLayout(panelObj, spacing: 2f, padding: 8);

        // Header Row: Heading + Toggle Button
        GameObject headerRow = UIFactory.CreateElement("ResourceHeaderRow", panelObj.transform, typeof(LayoutElement));
        LayoutElement headerRowLayout = headerRow.GetComponent<LayoutElement>();
        headerRowLayout.preferredHeight = 26f;
        headerRowLayout.minHeight = 24f;
        UIFactory.AddHorizontalLayout(headerRow, spacing: 6f, padding: 0);

        _resourceHeadingText = UIFactory.CreateText(
            headerRow.transform,
            _font,
            UIStrings.ResourcesHeading.GetText(),
            16,
            new Color(0.38f, 0.82f, 1f),
            TextAnchor.MiddleLeft,
            FontStyle.Bold);

        Button toggleBtn = UIFactory.CreateButton(
            headerRow.transform,
            _font,
            _showResourceDetails ? "▲" : "▼",
            () =>
            {
                _showResourceDetails = !_showResourceDetails;
                if (_resourceToggleText != null)
                {
                    _resourceToggleText.text = _showResourceDetails ? "▲" : "▼";
                }
                UpdateResourceInventory();
            },
            new Color(0.12f, 0.28f, 0.42f, 0.95f),
            fontSize: 11,
            preferredHeight: 22f);

        LayoutElement toggleLayout = toggleBtn.GetComponent<LayoutElement>();
        toggleLayout.preferredWidth = 34f;
        toggleLayout.minWidth = 28f;
        _resourceToggleText = toggleBtn.GetComponentInChildren<Text>();
        _resourceToggleText.color = new Color(0.45f, 0.85f, 1f);
        _resourceToggleText.fontStyle = FontStyle.Bold;

        _resourceInventoryText = UIFactory.CreateText(
            panelObj.transform,
            _font,
            string.Empty,
            12,
            Color.white,
            TextAnchor.UpperLeft);
        _resourceInventoryText.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private void BuildMothershipPanel()
    {
        GameObject panelObj = UIFactory.CreateElement("MothershipPanel", transform, typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        _mothershipPanel = panelObj.GetComponent<RectTransform>();

        Image bg = panelObj.GetComponent<Image>();
        bg.color = new Color(0.035f, 0.09f, 0.12f, 1f);
        bg.raycastTarget = true;

        _mothershipLayout = panelObj.GetComponent<LayoutElement>();
        _mothershipLayout.minHeight = 205f;
        _mothershipLayout.preferredHeight = 225f;
        _mothershipLayout.flexibleHeight = 0f;

        UIFactory.AddVerticalLayout(panelObj, spacing: 2f, padding: 8);

        // Header Row: Heading + Toggle Button
        GameObject headerRow = UIFactory.CreateElement("HeaderRow", panelObj.transform, typeof(LayoutElement));
        LayoutElement headerRowLayout = headerRow.GetComponent<LayoutElement>();
        headerRowLayout.preferredHeight = 26f;
        headerRowLayout.minHeight = 24f;
        UIFactory.AddHorizontalLayout(headerRow, spacing: 6f, padding: 0);

        _mothershipHeadingText = UIFactory.CreateText(
            headerRow.transform,
            _font,
            UIStrings.FlagshipHeading.GetText(),
            16,
            new Color(0.38f, 0.82f, 1f),
            TextAnchor.MiddleLeft,
            FontStyle.Bold);

        Button toggleBtn = UIFactory.CreateButton(
            headerRow.transform,
            _font,
            _showFriendshipDetails ? "▲ Basis" : "▼ Mehr",
            () =>
            {
                _showFriendshipDetails = !_showFriendshipDetails;
                if (_mothershipToggleText != null)
                {
                    _mothershipToggleText.text = _showFriendshipDetails ? "▲ Basis" : "▼ Mehr";
                }
                UpdateFlagshipInventory();
            },
            new Color(0.12f, 0.28f, 0.42f, 0.95f),
            fontSize: 11,
            preferredHeight: 22f);

        LayoutElement toggleLayout = toggleBtn.GetComponent<LayoutElement>();
        toggleLayout.preferredWidth = 68f;
        toggleLayout.minWidth = 60f;
        _mothershipToggleText = toggleBtn.GetComponentInChildren<Text>();
        _mothershipToggleText.color = new Color(0.45f, 0.85f, 1f);
        _mothershipToggleText.fontStyle = FontStyle.Bold;

        _mothershipInventoryText = UIFactory.CreateText(
            panelObj.transform,
            _font,
            string.Empty,
            11,
            Color.white,
            TextAnchor.UpperLeft);
    }

    public void SetDisplayedPlayer(Player player)
    {
        if (_displayedPlayer != null)
        {
            _displayedPlayer.ResourcesChanged -= UpdateResourceInventory;
            _displayedPlayer.UpgradesChanged -= UpdateFlagshipInventory;
            _displayedPlayer.FriendshipCardsChanged -= UpdateFlagshipInventory;
        }

        _displayedPlayer = player;

        if (_displayedPlayer != null)
        {
            _displayedPlayer.ResourcesChanged += UpdateResourceInventory;
            _displayedPlayer.UpgradesChanged += UpdateFlagshipInventory;
            _displayedPlayer.FriendshipCardsChanged += UpdateFlagshipInventory;

            if (_playerNameText != null)
            {
                _playerNameText.text = _displayedPlayer.PlayerName;
                _playerNameText.color = Color.white;
                _playerNameText.material = null;
            }

            if (_playerColorSwatch != null)
            {
                _playerColorSwatch.color = _displayedPlayer.Material != null
                    ? _displayedPlayer.Material.color
                    : Color.gray;
            }
        }
        else
        {
            if (_playerNameText != null) _playerNameText.text = string.Empty;
            if (_resourceInventoryText != null) _resourceInventoryText.text = string.Empty;
            if (_mothershipInventoryText != null) _mothershipInventoryText.text = string.Empty;
        }

        UpdateResourceInventory();
        UpdateFlagshipInventory();
        UpdateTurnStatus();
        UpdateNextButtonState();
        RefreshActionPanel();
    }

    public void UpdateResourceInventory()
    {
        if (_displayedPlayer == null || _resourceInventoryText == null) return;

        StringBuilder sb = new StringBuilder();

        if (!_showResourceDetails)
        {
            int totalCards = 0;
            foreach (ResourceType resource in GameRules.Resources)
            {
                totalCards += _displayedPlayer.GetResourceCount(resource);
            }
            sb.Append(string.Format("Karten: {0} · {1}{2}", totalCards, UIStrings.HalfMedals.GetText(), _displayedPlayer.HalfMedalCount));

            if (_resourceLayout != null)
            {
                _resourceLayout.preferredHeight = 56f;
                _resourceLayout.minHeight = 52f;
            }
        }
        else
        {
            foreach (ResourceType resource in GameRules.Resources)
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.Append(UIStrings.GetResourceName(resource));
                sb.Append(": ");
                sb.Append(_displayedPlayer.GetResourceCount(resource));
            }

            sb.AppendLine();
            sb.Append(UIStrings.HalfMedals.GetText());
            sb.Append(_displayedPlayer.HalfMedalCount);

            if (_resourceLayout != null)
            {
                _resourceLayout.preferredHeight = 145f;
                _resourceLayout.minHeight = 135f;
            }
        }

        _resourceInventoryText.text = sb.ToString();

        UpdateTurnStatus();
        RefreshActionPanel();
    }

    public void UpdateFlagshipInventory()
    {
        if (_displayedPlayer == null || _mothershipInventoryText == null) return;

        StringBuilder sb = new StringBuilder();

        sb.Append(UIStrings.Boosters.GetText());
        sb.Append(": ");
        sb.Append(_displayedPlayer.FlagshipDriveCount);
        if (_displayedPlayer.FriendshipSpeedBonus > 0)
        {
            sb.Append(string.Format(" (+{0} Bonus)", _displayedPlayer.FriendshipSpeedBonus));
        }
        sb.Append(" / ");
        sb.Append(GameRules.MaxBoostersPerPlayer);
        sb.AppendLine();

        sb.Append(UIStrings.Cannons.GetText());
        sb.Append(": ");
        sb.Append(_displayedPlayer.FlagshipCannonCount);
        if (_displayedPlayer.FriendshipCannonBonus > 0)
        {
            sb.Append(string.Format(" (+{0} Bonus)", _displayedPlayer.FriendshipCannonBonus));
        }
        sb.Append(" / ");
        sb.Append(GameRules.MaxCannonsPerPlayer);
        sb.AppendLine();

        sb.Append(UIStrings.FreightPods.GetText());
        sb.Append(": ");
        sb.Append(_displayedPlayer.FlagshipCargoModuleCount);
        sb.Append("/");
        sb.Append(GameRules.MaxFreightPodsPerPlayer);
        sb.AppendLine();

        sb.Append(UIStrings.Colonies.GetText());
        sb.Append(": ");
        sb.Append(_displayedPlayer.BuiltColoniesCount);
        sb.Append("/");
        sb.Append(GameRules.MaxColoniesPerPlayer);
        sb.AppendLine();

        sb.Append(UIStrings.Spaceports.GetText());
        sb.Append(": ");
        sb.Append(_displayedPlayer.BuiltSpaceportsCount);
        sb.Append("/");
        sb.Append(GameRules.MaxSpaceportsPerPlayer);
        sb.AppendLine();

        sb.Append(UIStrings.TradeStations.GetText());
        sb.Append(": ");
        sb.Append(_displayedPlayer.BuiltTradeStationsCount);
        sb.Append("/");
        sb.Append(GameRules.MaxTradeStationsPerPlayer);
        sb.AppendLine();

        sb.Append(UIStrings.TransportShips.GetText());
        sb.Append(": ");
        sb.Append(_displayedPlayer.ActiveTransportShips);
        sb.Append("/");
        sb.Append(GameRules.MaxTransportShipsPerPlayer);

        if (_displayedPlayer.CapturedTradeShipsCount > 0)
        {
            sb.AppendLine();
            sb.Append(UIStrings.CapturedTradeShipsLabel.GetText());
            sb.Append(": ");
            sb.Append(_displayedPlayer.CapturedTradeShipsCount);
        }

        List<AlienCivilization> heldStandees = new List<AlienCivilization>();
        if (_triMap != null && _triMap.FolkStandeeHolders != null)
        {
            foreach (var kvp in _triMap.FolkStandeeHolders)
            {
                if (kvp.Value == _displayedPlayer) heldStandees.Add(kvp.Key);
            }
        }

        int cardsCount = _displayedPlayer.FriendshipCards != null ? _displayedPlayer.FriendshipCards.Count : 0;
        int standeesCount = heldStandees.Count;

        if (!_showFriendshipDetails)
        {
            if (standeesCount > 0 || cardsCount > 0)
            {
                sb.AppendLine();
                string standeesText = standeesCount == 1 ? "1 Aufsteller" : standeesCount + " Aufsteller";
                string cardsText = cardsCount == 1 ? "1 Karte" : cardsCount + " Karten";
                sb.Append(string.Format("★ Boni: {0} (+{1} VP), {2}", standeesText, standeesCount * 2, cardsText));
            }
        }
        else
        {
            if (standeesCount > 0)
            {
                sb.AppendLine();
                sb.Append("🏆 " + UIStrings.FriendshipStandeesHeading.GetText() + " (+" + (standeesCount * 2) + " " + UIStrings.VictoryPointsHeading.GetText() + "):");
                foreach (AlienCivilization civ in heldStandees)
                {
                    sb.AppendLine();
                    sb.Append("• " + UIStrings.GetAlienCivilizationName(civ) + " (+2 VP)");
                }
            }

            if (cardsCount > 0)
            {
                sb.AppendLine();
                sb.Append("★ " + UIStrings.FriendshipCardsHeading.GetText() + " (" + cardsCount + "):");
                foreach (FriendshipCard card in _displayedPlayer.FriendshipCards)
                {
                    sb.AppendLine();
                    string benefit = card.GetShortBenefit();
                    if (!string.IsNullOrEmpty(benefit))
                    {
                        sb.Append("• " + card.GetName() + " (" + benefit + ")");
                    }
                    else
                    {
                        sb.Append("• " + card.GetName());
                    }
                }
            }
        }

        int specialTokensCount = _displayedPlayer.IcePlanetTokensCount + _displayedPlayer.MarauderBaseTokensCount;
        if (specialTokensCount > 0)
        {
            sb.AppendLine();
            sb.Append(string.Format("🎖 {0} (+{1} VP): {2}x {3}, {4}x {5}",
                UIStrings.SpecialTokensHeading.GetText(),
                specialTokensCount,
                _displayedPlayer.IcePlanetTokensCount,
                UIStrings.IcePlanet.GetText(),
                _displayedPlayer.MarauderBaseTokensCount,
                UIStrings.MarauderBase.GetText()));
        }

        if (_triMap != null &&
            _triMap.CurrentPlayer == _displayedPlayer &&
            _triMap.CurrentFoundingPhase == FoundingPhase.Complete &&
            _triMap.CurrentPhase == GameTurnPhase.Flight &&
            _triMap.LastFlagshipRoll != null)
        {
            FlagshipRollResult roll = _triMap.LastFlagshipRoll;
            sb.AppendLine();
            sb.Append(string.Format(
                UIStrings.FlightImpulseSummary.GetText(),
                UIStrings.GetCoreName(roll.FirstCore),
                UIStrings.GetCoreName(roll.SecondCore),
                roll.BaseSpeed,
                roll.DriveCount,
                roll.Speed));
            if (roll.RequiresEncounter)
            {
                sb.AppendLine();
                sb.Append(_triMap.EncounterPending
                    ? UIStrings.EncounterPending.GetText()
                    : UIStrings.EncounterResolved.GetText());
            }
        }

        _mothershipInventoryText.text = sb.ToString();

        if (_mothershipLayout != null)
        {
            float textPreferredHeight = _mothershipInventoryText.preferredHeight;
            float totalNeededHeight = 26f /*header*/ + 16f /*padding*/ + 4f /*spacing*/ + textPreferredHeight + 4f;
            float minH = _showFriendshipDetails ? 200f : 160f;
            _mothershipLayout.preferredHeight = Mathf.Max(minH, totalNeededHeight);
            _mothershipLayout.minHeight = Mathf.Min(minH, _mothershipLayout.preferredHeight);
        }

        RefreshActionPanel();
    }

    public void UpdateTurnStatus()
    {
        if (_turnStatusText == null || _triMap == null) return;

        bool isCurrentPlayerDisplayed = _triMap.CurrentPlayer != null && _triMap.CurrentPlayer == _displayedPlayer;

        if (!isCurrentPlayerDisplayed && _triMap.CurrentPlayer != null)
        {
            if (_triMap.CurrentFoundingPhase != FoundingPhase.Complete)
            {
                _turnStatusText.text = $"{_triMap.CurrentPlayer.PlayerName} ist am Zug (Gründung)...";
            }
            else
            {
                _turnStatusText.text = $"{_triMap.CurrentPlayer.PlayerName} ist am Zug ({UIStrings.GetTurnPhaseName(_triMap.CurrentPhase)})...";
            }
            return;
        }

        string text = _triMap.CurrentFoundingPhase == FoundingPhase.Complete
            ? UIStrings.PhasePrefix.GetText() + UIStrings.GetTurnPhaseName(_triMap.CurrentPhase)
            : UIStrings.PhasePrefix.GetText() + UIStrings.PhaseSetup.GetText();

        if (_triMap.CurrentFoundingPhase == FoundingPhase.PlaceFirstColony ||
            _triMap.CurrentFoundingPhase == FoundingPhase.PlaceSecondColony ||
            _triMap.CurrentFoundingPhase == FoundingPhase.PlaceThirdColony)
        {
            text = string.Format(UIStrings.StatusSetupColony.GetText(), _triMap.FoundingRound);
        }
        else if (_triMap.CurrentFoundingPhase == FoundingPhase.SelectSpaceport)
        {
            text = UIStrings.StatusSetupSpaceport.GetText();
        }
        else if (_triMap.CurrentFoundingPhase == FoundingPhase.SelectStartingShipPoint)
        {
            text = UIStrings.StatusSetupStartingShip.GetText();
        }

        if (_triMap.CurrentFoundingPhase == FoundingPhase.Complete &&
            _triMap.CurrentPhase == GameTurnPhase.Production &&
            _triMap.LastProductionRoll != null)
        {
            text += string.Format(UIStrings.ProductionRollSummary.GetText(), _triMap.LastProductionRoll.Total);
        }

        if (_triMap.IsExecutingSpaceJump && _triMap.PendingSpaceJumpShip != null)
        {
            text = UIStrings.SpaceJumpTargetPrompt.GetText();
        }
        else if (_triMap.IsPlacingShip && _triMap.PendingShipPlacementType.HasValue)
        {
            text = string.Format(UIStrings.StatusPlacingShip.GetText(), UIStrings.GetShipName(_triMap.PendingShipPlacementType.Value));
        }
        else if (!string.IsNullOrEmpty(_triMap.PlanetDiscoveryTestInstruction))
        {
            text = _triMap.PlanetDiscoveryTestInstruction;
        }

        _turnStatusText.text = text;
    }

    public void UpdateNextButtonState()
    {
        if (_nextButton == null || _triMap == null) return;

        bool isTurnOfDisplayedPlayer = _triMap.CurrentPlayer != null && _triMap.CurrentPlayer == _displayedPlayer;
        bool isLocalTurn = isTurnOfDisplayedPlayer;
        if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected)
        {
            isLocalTurn = _triMap.CurrentPlayerIndex == Networking.NetworkManager.Instance.LocalPlayerIndex;
        }

        bool isAiTurn = _triMap.CurrentPlayer != null && _triMap.CurrentPlayer.Type == Player.PlayerType.AI;
        bool isFoundingComplete = _triMap.CurrentFoundingPhase == FoundingPhase.Complete;
        bool canAdvance = isTurnOfDisplayedPlayer && isLocalTurn && !isAiTurn && isFoundingComplete &&
            !_triMap.IsPlacingShip && !_triMap.IsUpgradingSpaceport && !_triMap.IsExecutingSpaceJump &&
            !_triMap.IsResolvingSevenRoll;

        bool isProductionPhase = isFoundingComplete && _triMap.CurrentPhase == GameTurnPhase.Production;
        bool isFlightPhase = isFoundingComplete && _triMap.CurrentPhase == GameTurnPhase.Flight;

        if (isProductionPhase && (_triMap.LastProductionRoll == null || _triMap.IsResolvingSevenRoll))
        {
            canAdvance = false;
        }
        else if (isFlightPhase && _triMap.LastFlagshipRoll == null)
        {
            canAdvance = false;
        }

        _nextButton.interactable = canAdvance;

        if (_nextButtonImage != null)
        {
            _nextButtonImage.color = canAdvance
                ? new Color(0.12f, 0.35f, 0.55f, 1f)
                : new Color(0.08f, 0.12f, 0.16f, 0.6f);
        }

        if (_nextButtonLabel != null)
        {
            _nextButtonLabel.color = canAdvance
                ? Color.white
                : new Color(0.55f, 0.58f, 0.62f, 0.6f);
        }
    }

    public void RefreshActionPanel()
    {
        if (_actionPanel != null && _triMap != null)
        {
            Unit selectedUnit = _selectionController != null ? _selectionController.SelectedUnit : null;
            _actionPanel.Refresh(_triMap.CurrentPlayer, selectedUnit);
        }
        UpdateNextButtonState();
    }

    public void UpdateLanguage()
    {
        if (_nextButtonLabel != null) _nextButtonLabel.text = UIStrings.NextTurn.GetText();
        if (_quitButton != null)
        {
            Text qTxt = _quitButton.GetComponentInChildren<Text>();
            if (qTxt != null) qTxt.text = UIStrings.QuitGame.GetText();
        }
        if (_resourceHeadingText != null) _resourceHeadingText.text = UIStrings.ResourcesHeading.GetText();
        if (_mothershipHeadingText != null) _mothershipHeadingText.text = UIStrings.FlagshipHeading.GetText();
        if (_mothershipToggleText != null) _mothershipToggleText.text = _showFriendshipDetails ? "▲ Basis" : "▼ Mehr";

        if (_displayedPlayer != null && _playerNameText != null)
        {
            _playerNameText.text = _displayedPlayer.PlayerName;
        }

        UpdateResourceInventory();
        UpdateFlagshipInventory();
        UpdateTurnStatus();
        if (_actionPanel != null)
        {
            _actionPanel.UpdateLanguage();
        }
    }

    private void SubscribeToTriMap()
    {
        if (_triMap != null && !_subscribedToTriMap)
        {
            _triMap.OnPlayerChanged += OnPlayerChanged;
            _triMap.OnTurnPhaseChanged += OnTurnPhaseChanged;
            _triMap.OnFoundingPhaseChanged += OnFoundingPhaseChanged;
            _triMap.OnShipPlacementStateChanged += OnShipPlacementStateChanged;
            _triMap.OnSpaceportUpgradeStateChanged += OnSpaceportUpgradeStateChanged;
            _triMap.OnSpaceJumpStateChanged += OnSpaceJumpStateChanged;
            _triMap.OnEncounterStateChanged += OnEncounterStateChanged;
            _triMap.OnEncounterProgressChanged += OnEncounterProgressChanged;
            _subscribedToTriMap = true;
        }
    }

    private void UnsubscribeFromTriMap()
    {
        if (_triMap != null && _subscribedToTriMap)
        {
            _triMap.OnPlayerChanged -= OnPlayerChanged;
            _triMap.OnTurnPhaseChanged -= OnTurnPhaseChanged;
            _triMap.OnFoundingPhaseChanged -= OnFoundingPhaseChanged;
            _triMap.OnShipPlacementStateChanged -= OnShipPlacementStateChanged;
            _triMap.OnSpaceportUpgradeStateChanged -= OnSpaceportUpgradeStateChanged;
            _triMap.OnSpaceJumpStateChanged -= OnSpaceJumpStateChanged;
            _triMap.OnEncounterStateChanged -= OnEncounterStateChanged;
            _triMap.OnEncounterProgressChanged -= OnEncounterProgressChanged;
            _subscribedToTriMap = false;
        }

        if (_displayedPlayer != null)
        {
            _displayedPlayer.ResourcesChanged -= UpdateResourceInventory;
            _displayedPlayer.UpgradesChanged -= UpdateFlagshipInventory;
            _displayedPlayer.FriendshipCardsChanged -= UpdateFlagshipInventory;
            _displayedPlayer = null;
        }
    }

    private void OnLanguageChanged(GameLanguage language)
    {
        UpdateLanguage();
    }

    void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += OnLanguageChanged;
        SelectionController.OnUnitSelectionChanged += OnUnitSelectionChanged;
        SubscribeToTriMap();
    }

    void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        SelectionController.OnUnitSelectionChanged -= OnUnitSelectionChanged;
        UnsubscribeFromTriMap();
    }

    private Player ResolvePlayerForView(Player incomingPlayer)
    {
        if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected)
        {
            int localIdx = Networking.NetworkManager.Instance.LocalPlayerIndex;
            if (_triMap != null && _triMap.Players != null && localIdx >= 0 && localIdx < _triMap.Players.Length)
            {
                return _triMap.Players[localIdx];
            }
        }

        // If the active player is not a local human player, prefer viewing the local human player's stats
        if (incomingPlayer != null && incomingPlayer.Type != Player.PlayerType.LOCAL)
        {
            if (_displayedPlayer != null && _displayedPlayer.Type == Player.PlayerType.LOCAL)
            {
                return _displayedPlayer;
            }

            if (_triMap != null && _triMap.Players != null)
            {
                for (int i = 0; i < _triMap.Players.Length; i++)
                {
                    if (_triMap.Players[i] != null && _triMap.Players[i].Type == Player.PlayerType.LOCAL)
                    {
                        return _triMap.Players[i];
                    }
                }
            }
        }

        return incomingPlayer;
    }

    private void OnPlayerChanged(Player player)
    {
        SetDisplayedPlayer(ResolvePlayerForView(player));
        UpdateTurnStatus();
        UpdateNextButtonState();
        RefreshActionPanel();
    }

    private void OnTurnPhaseChanged(Player player, GameTurnPhase phase)
    {
        Player targetPlayer = ResolvePlayerForView(player);
        if (targetPlayer != _displayedPlayer)
        {
            SetDisplayedPlayer(targetPlayer);
        }
        else
        {
            UpdateResourceInventory();
            UpdateFlagshipInventory();
            UpdateTurnStatus();
            UpdateNextButtonState();
            RefreshActionPanel();
        }
    }

    private void OnFoundingPhaseChanged(Player player, FoundingPhase phase)
    {
        Player targetPlayer = ResolvePlayerForView(player);
        if (targetPlayer != _displayedPlayer)
        {
            SetDisplayedPlayer(targetPlayer);
        }
        else
        {
            UpdateResourceInventory();
            UpdateFlagshipInventory();
            UpdateTurnStatus();
            UpdateNextButtonState();
            RefreshActionPanel();
        }
    }

    private void OnShipPlacementStateChanged()
    {
        UpdateTurnStatus();
        UpdateNextButtonState();
        RefreshActionPanel();
    }

    private void OnSpaceportUpgradeStateChanged()
    {
        UpdateTurnStatus();
        UpdateNextButtonState();
        RefreshActionPanel();
    }

    private void OnSpaceJumpStateChanged()
    {
        UpdateTurnStatus();
        UpdateNextButtonState();
        RefreshActionPanel();
    }

    private void OnEncounterStateChanged(bool pending)
    {
        UpdateFlagshipInventory();
        RefreshActionPanel();
        UpdateNextButtonState();
    }

    private void OnEncounterProgressChanged(EncounterCardPhase phase, EncounterCardOutcome outcome)
    {
        UpdateFlagshipInventory();
    }

    private void OnUnitSelectionChanged(Unit unit)
    {
        RefreshActionPanel();
    }

    private void OnDestroy()
    {
        UIManager.UnregisterHudElement(transform as RectTransform);
    }
}
