using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Networking;
using AgentBridge;

/// <summary>
/// Pre-game configuration modal allowing players to choose between Local Hotseat play,
/// browsing active LAN servers, hosting an authoritative LAN game server, or waiting in a multiplayer lobby.
/// </summary>
public class GameSetupDialogUI : MonoBehaviour
{
    private static readonly Color[] PlayerColors = new Color[]
    {
        new Color(0f, 0.05884123f, 1f, 1f),      // Color 0: Blue (Player1Material)
        new Color(0.00787749f, 0.7735849f, 0f, 1f), // Color 1: Green (Player2Material)
        new Color(1f, 1f, 0f, 1f),               // Color 2: Yellow (Player3Material)
        new Color(1f, 0f, 0f, 1f)                // Color 3: Red (Player4Material)
    };

    private Color GetPlayerColor(int colorIndex)
    {
        int idx = Mathf.Clamp(colorIndex, 0, PlayerColors.Length - 1);
        if (_triMap != null && _triMap.PlayerMaterials != null && idx < _triMap.PlayerMaterials.Length && _triMap.PlayerMaterials[idx] != null)
        {
            return _triMap.PlayerMaterials[idx].color;
        }
        return PlayerColors[idx];
    }

    public enum SetupMode
    {
        Local,
        ServerBrowser,
        HostLAN,
        Lobby
    }

    private TriMap _triMap;
    private Font _font;
    private Action _onGameStarted;
    private SetupMode _currentMode = SetupMode.Local;

    // Header & Bottom Bar Localized Texts
    private Text _titleText;
    private Text _subtitleText;
    private Text _languageBtnText;
    private Text _headerQuitBtnText;
    private Text _playersHeadingText;
    private Text _seedHeadingText;
    private Text _bottomQuitBtnText;
    private Text _resetBtnText;
    private Text _launchBtnText;
    private Text _rerollBtnText;

    // Top Navigation Tabs
    private GameObject _tabBar;
    private Button _tabLocalBtn;
    private Text _tabLocalBtnText;
    private Button _tabBrowserBtn;
    private Text _tabBrowserBtnText;
    private Button _tabHostBtn;
    private Text _tabHostBtnText;

    // View Containers
    private GameObject _localContainer;
    private GameObject _browserContainer;
    private GameObject _hostContainer;
    private GameObject _lobbyContainer;

    // Local Mode UI Controls
    private Text _seedModeBtnText;
    private Text _seedLabelText;
    private InputField _seedInputField;
    private Image _seedInputBg;
    private Button _rerollBtn;
    private bool _useFixedSeed;
    private int _currentSeed;
    private Button _debugLabelsBtn;
    private Text _debugLabelsBtnText;
    private bool _showDebugLabels;
    private readonly SlotUI[] _slots = new SlotUI[4];

    // Server Browser Controls
    private Text _browserTitleText;
    private Text _discoveryStatusText;
    private GameObject _serverListContent;
    private InputField _manualIpInput;
    private InputField _manualPortInput;
    private InputField _clientPlayerNameInput;
    private Text _manualPlayerLabel;
    private Text _manualConnectBtnText;
    private Text _manualSpectateBtnText;

    // Host Controls
    private Text _hostTitleText;
    private Text _hostServerNameLabel;
    private Text _hostPlayerNameLabel;
    private InputField _hostServerNameInput;
    private InputField _hostPortInput;
    private InputField _hostPlayerNameInput;
    private Text _hostIpInfoText;
    private Text _hostQuitBtnText;
    private Text _hostStartBtnText;

    // Lobby Controls
    private Text _lobbyTitleText;
    private Text _lobbyStatusText;
    private Text _lobbyMyNameLabel;
    private Text _lobbySpectatorModeLabel;
    private Button _lobbyStartBtn;
    private Text _lobbyStartBtnText;
    private Button _lobbyLeaveBtn;
    private Text _lobbyLeaveBtnText;
    private Button _lobbySpectatorModeBtn;
    private Text _lobbySpectatorModeText;
    private Text _lobbySpectatorCountText;
    private Button _lobbySeedModeBtn;
    private Text _lobbySeedModeBtnText;
    private InputField _lobbySeedInputField;
    private Button _lobbyRerollBtn;
    private Text _lobbyRerollBtnText;
    private readonly Text[] _lobbySlotTexts = new Text[4];
    private readonly Image[] _lobbySlotSwatches = new Image[4];
    private readonly Button[] _lobbySlotColorBtns = new Button[4];
    private InputField _lobbyMyNameInput;
    private int _lobbyMyNameSlot = -1;
    private LobbyStateDTO _latestLobbyState;

    private class SlotUI
    {
        public int SlotIndex;
        public InputField NameInput;
        public Button ColorButton;
        public Image ColorSwatch;
        public int ColorIndex;
        public Button TypeButton;
        public Text TypeButtonText;
        public Image TypeButtonImage;
        public Button DiffButton;
        public Text DiffButtonText;
        public Image DiffButtonImage;
        public GameObject DiffContainer;

        public Player.PlayerType CurrentType;
        public Player.AiDifficulty CurrentDifficulty;
    }

    public bool IsActive => gameObject != null && gameObject.activeSelf;

    public static GameSetupDialogUI Create(Transform parent, Font font, TriMap triMap, Action onGameStarted)
    {
        Image dimmer = UIFactory.CreatePanel("GameSetupStartScreenModal", parent,
            new Color(0.01f, 0.025f, 0.06f, 0.96f), true);
        GameObject root = dimmer.gameObject;
        UIFactory.Stretch(UIFactory.Rect(root), Vector2.zero, Vector2.zero);

        // Center card window
        GameObject window = UIFactory.CreateElement("SetupWindow", root.transform, typeof(CanvasRenderer), typeof(Image));
        Image winImg = window.GetComponent<Image>();
        winImg.color = new Color(0.025f, 0.07f, 0.12f, 0.98f);
        RectTransform winRect = UIFactory.Rect(window);
        UIFactory.SetAnchors(winRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(700f, 620f));

        Outline border = window.AddComponent<Outline>();
        border.effectColor = new Color(0.85f, 0.68f, 0.22f, 0.95f);
        border.effectDistance = new Vector2(2f, -2f);

        UIFactory.AddVerticalLayout(window, 8f, 16);
        UIFactory.AddSizeFitter(window);

        GameSetupDialogUI dialog = root.AddComponent<GameSetupDialogUI>();
        dialog._triMap = triMap;
        dialog._font = font;
        dialog._onGameStarted = onGameStarted;

        dialog.BuildHeader(window.transform);
        dialog.BuildModeTabBar(window.transform);

        dialog.BuildLocalContainer(window.transform);
        dialog.BuildServerBrowserContainer(window.transform);
        dialog.BuildHostLanContainer(window.transform);
        dialog.BuildMultiplayerLobbyContainer(window.transform);

        dialog.LoadFromConfig();
        dialog.SetSetupMode(SetupMode.Local);

        root.SetActive(false);
        return dialog;
    }

    private void BuildHeader(Transform parent)
    {
        GameObject header = UIFactory.CreateElement("Header", parent, typeof(LayoutElement));
        LayoutElement headerLay = header.GetComponent<LayoutElement>();
        headerLay.preferredHeight = 42f;
        headerLay.minHeight = 38f;
        UIFactory.AddHorizontalLayout(header, 10f, 0);

        GameObject titleCol = UIFactory.CreateElement("TitleCol", header.transform, typeof(LayoutElement));
        titleCol.GetComponent<LayoutElement>().flexibleWidth = 1f;
        UIFactory.AddVerticalLayout(titleCol, 2f, 0);

        _titleText = UIFactory.CreateText(titleCol.transform, _font, UIStrings.GameSetupTitle.GetText(),
            18, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleLeft, FontStyle.Bold);

        _subtitleText = UIFactory.CreateText(titleCol.transform, _font, UIStrings.GameSetupSubtitle.GetText(),
            11, new Color(0.7f, 0.88f, 1f), TextAnchor.MiddleLeft);

        Button langBtn = UIFactory.CreateButton(header.transform, _font, UIStrings.LanguageShortButton.GetText(),
            () => LocalizationManager.ToggleLanguage(), new Color(0.14f, 0.28f, 0.44f, 0.95f), 11, 32f);
        langBtn.GetComponent<LayoutElement>().preferredWidth = 72f;
        _languageBtnText = langBtn.GetComponentInChildren<Text>();

        Button quitBtn = UIFactory.CreateButton(header.transform, _font, "✕ " + UIStrings.Quit.GetText(),
            ConfirmQuitDialogUI.QuitApplication, new Color(0.55f, 0.18f, 0.18f, 0.95f), 11, 32f);
        quitBtn.GetComponent<LayoutElement>().preferredWidth = 95f;
        _headerQuitBtnText = quitBtn.GetComponentInChildren<Text>();
    }

    private void BuildModeTabBar(Transform parent)
    {
        _tabBar = UIFactory.CreateElement("ModeTabBar", parent, typeof(LayoutElement));
        LayoutElement barLayout = _tabBar.GetComponent<LayoutElement>();
        barLayout.preferredHeight = 36f;
        barLayout.minHeight = 36f;
        UIFactory.AddHorizontalLayout(_tabBar, 8f, 0);

        _tabLocalBtn = UIFactory.CreateButton(_tabBar.transform, _font, UIStrings.GameSetupTabLocal.GetText(),
            () => SetSetupMode(SetupMode.Local), new Color(0.15f, 0.35f, 0.55f, 1f), 12, 34f);
        _tabLocalBtnText = _tabLocalBtn.GetComponentInChildren<Text>();

        _tabBrowserBtn = UIFactory.CreateButton(_tabBar.transform, _font, UIStrings.GameSetupTabBrowser.GetText(),
            () => SetSetupMode(SetupMode.ServerBrowser), new Color(0.08f, 0.2f, 0.3f, 1f), 12, 34f);
        _tabBrowserBtnText = _tabBrowserBtn.GetComponentInChildren<Text>();

        _tabHostBtn = UIFactory.CreateButton(_tabBar.transform, _font, UIStrings.GameSetupTabHost.GetText(),
            () => SetSetupMode(SetupMode.HostLAN), new Color(0.08f, 0.2f, 0.3f, 1f), 12, 34f);
        _tabHostBtnText = _tabHostBtn.GetComponentInChildren<Text>();
    }

    public void SetSetupMode(SetupMode mode)
    {
        if (mode != SetupMode.Lobby &&
            NetworkManager.Instance != null &&
            (NetworkManager.Instance.IsServerRunning || NetworkManager.Instance.IsClientConnected))
        {
            NetworkManager.Instance.DisconnectAll();
        }

        _currentMode = mode;

        if (_localContainer != null) _localContainer.SetActive(mode == SetupMode.Local);
        if (_browserContainer != null) _browserContainer.SetActive(mode == SetupMode.ServerBrowser);
        if (_hostContainer != null) _hostContainer.SetActive(mode == SetupMode.HostLAN);
        if (_lobbyContainer != null) _lobbyContainer.SetActive(mode == SetupMode.Lobby);

        // Update Tab Button Colors
        Color activeTabColor = new Color(0.18f, 0.52f, 0.78f, 1f);
        Color inactiveTabColor = new Color(0.06f, 0.16f, 0.24f, 0.85f);

        if (_tabLocalBtn != null) _tabLocalBtn.GetComponent<Image>().color = mode == SetupMode.Local ? activeTabColor : inactiveTabColor;
        if (_tabBrowserBtn != null) _tabBrowserBtn.GetComponent<Image>().color = mode == SetupMode.ServerBrowser ? activeTabColor : inactiveTabColor;
        if (_tabHostBtn != null) _tabHostBtn.GetComponent<Image>().color = mode == SetupMode.HostLAN ? activeTabColor : inactiveTabColor;

        if (mode == SetupMode.ServerBrowser)
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.StartDiscovery();
                NetworkManager.Instance.Discovery.OnServersChanged -= HandleDiscoveredServersChanged;
                NetworkManager.Instance.Discovery.OnServersChanged += HandleDiscoveredServersChanged;
                HandleDiscoveredServersChanged(NetworkManager.Instance.Discovery.GetCurrentServers());
            }
        }
        else
        {
            if (NetworkManager.Instance != null && !NetworkManager.Instance.IsClientConnected)
            {
                NetworkManager.Instance.StopDiscovery();
                if (NetworkManager.Instance.Discovery != null)
                {
                    NetworkManager.Instance.Discovery.OnServersChanged -= HandleDiscoveredServersChanged;
                }
            }
        }
    }

    // =========================================================================
    //  1. LOCAL HOTSEAT SETUP CONTAINER
    // =========================================================================

    private void BuildLocalContainer(Transform parent)
    {
        _localContainer = UIFactory.CreateElement("LocalContainer", parent);
        UIFactory.AddVerticalLayout(_localContainer, 6f, 0);

        BuildPlayerSlotsSection(_localContainer.transform);
        BuildSeedSection(_localContainer.transform);
        BuildBottomActionBar(_localContainer.transform);
    }

    private void BuildPlayerSlotsSection(Transform parent)
    {
        GameObject section = UIFactory.CreateElement("PlayersSection", parent);
        UIFactory.AddVerticalLayout(section, 6f, 4);

        _playersHeadingText = UIFactory.CreateText(section.transform, _font, UIStrings.GameSetupPlayersHeading.GetText(),
            12, new Color(0.35f, 0.85f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);

        for (int i = 0; i < 4; i++)
        {
            int slotIdx = i;
            GameObject row = UIFactory.CreateElement("PlayerRow_" + (i + 1), section.transform, typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            Image rowBg = row.GetComponent<Image>();
            rowBg.color = new Color(0.04f, 0.11f, 0.18f, 0.85f);
            LayoutElement rowLayout = row.GetComponent<LayoutElement>();
            rowLayout.preferredHeight = 44f;
            rowLayout.minHeight = 44f;

            UIFactory.AddHorizontalLayout(row, 10f, 6);

            // Clickable Color Swatch Button
            GameObject swatch = UIFactory.CreateElement("ColorSwatch", row.transform, typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            Image swatchImg = swatch.GetComponent<Image>();
            swatchImg.color = GetPlayerColor(i);
            Button colorBtn = swatch.GetComponent<Button>();
            colorBtn.targetGraphic = swatchImg;
            colorBtn.onClick.AddListener(() => CycleLocalSlotColor(slotIdx));
            LayoutElement swatchLayout = swatch.GetComponent<LayoutElement>();
            swatchLayout.preferredWidth = 26f;
            swatchLayout.preferredHeight = 30f;

            // Editable Player Name InputField
            InputField nameInput = UIFactory.CreateInputField(row.transform, _font, "Spieler " + (i + 1),
                null, null,
                new Color(0.02f, 0.06f, 0.11f, 0.95f), Color.white, 13, 30f, 18, "Name");
            LayoutElement nameLayout = nameInput.GetComponent<LayoutElement>();
            nameLayout.preferredWidth = 170f;
            nameLayout.minWidth = 140f;
            nameLayout.preferredHeight = 30f;

            // Type Toggle Button: Human vs AI
            Button typeBtn = UIFactory.CreateButton(row.transform, _font, UIStrings.GameSetupPlayerLocal.GetText(),
                () => TogglePlayerType(slotIdx), new Color(0.12f, 0.35f, 0.55f, 1f), 11, 32f);
            LayoutElement typeBtnLayout = typeBtn.GetComponent<LayoutElement>();
            typeBtnLayout.preferredWidth = 120f;
            typeBtnLayout.minWidth = 110f;

            // Difficulty Button Container (only shown for AI)
            GameObject diffContainer = UIFactory.CreateElement("DiffContainer", row.transform, typeof(LayoutElement));
            LayoutElement diffLayout = diffContainer.GetComponent<LayoutElement>();
            diffLayout.preferredWidth = 160f;
            diffLayout.minWidth = 140f;
            diffLayout.preferredHeight = 32f;
            UIFactory.AddHorizontalLayout(diffContainer, 4f, 0);

            Button diffBtn = UIFactory.CreateButton(diffContainer.transform, _font, UIStrings.GameSetupDifficultyNormal.GetText(),
                () => CycleAiDifficulty(slotIdx), GetDifficultyColor(Player.AiDifficulty.Normal), 11, 32f);

            _slots[i] = new SlotUI
            {
                SlotIndex = i,
                NameInput = nameInput,
                ColorButton = colorBtn,
                ColorSwatch = swatchImg,
                ColorIndex = i,
                TypeButton = typeBtn,
                TypeButtonText = typeBtn.GetComponentInChildren<Text>(),
                TypeButtonImage = typeBtn.GetComponent<Image>(),
                DiffButton = diffBtn,
                DiffButtonText = diffBtn.GetComponentInChildren<Text>(),
                DiffButtonImage = diffBtn.GetComponent<Image>(),
                DiffContainer = diffContainer,
                CurrentType = i == 0 ? Player.PlayerType.LOCAL : Player.PlayerType.AI,
                CurrentDifficulty = Player.AiDifficulty.Normal
            };
        }
    }

    private void BuildSeedSection(Transform parent)
    {
        GameObject section = UIFactory.CreateElement("SeedSection", parent);
        UIFactory.AddVerticalLayout(section, 6f, 4);

        _seedHeadingText = UIFactory.CreateText(section.transform, _font, UIStrings.GameSetupSeedHeading.GetText(),
            12, new Color(0.35f, 0.85f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);

        GameObject row = UIFactory.CreateElement("SeedRow", section.transform, typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        row.GetComponent<Image>().color = new Color(0.04f, 0.11f, 0.18f, 0.85f);
        LayoutElement rowLayout = row.GetComponent<LayoutElement>();
        rowLayout.preferredHeight = 44f;
        rowLayout.minHeight = 44f;
        UIFactory.AddHorizontalLayout(row, 10f, 6);

        // Seed mode toggle button
        Button modeBtn = UIFactory.CreateButton(row.transform, _font, UIStrings.GameSetupSeedRandom.GetText(),
            ToggleSeedMode, new Color(0.15f, 0.3f, 0.45f, 1f), 11, 32f);
        LayoutElement modeLayout = modeBtn.GetComponent<LayoutElement>();
        modeLayout.preferredWidth = 180f;
        _seedModeBtnText = modeBtn.GetComponentInChildren<Text>();

        // Label: "Seed:"
        _seedLabelText = UIFactory.CreateText(row.transform, _font, "Seed:", 12, new Color(0.75f, 0.85f, 0.95f), TextAnchor.MiddleRight);
        LayoutElement labelLayout = _seedLabelText.gameObject.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 45f;

        // Current seed editable InputField
        _seedInputField = UIFactory.CreateInputField(row.transform, _font, _currentSeed.ToString(),
            OnSeedValueChanged, OnSeedEndEdit,
            new Color(0.02f, 0.05f, 0.09f, 1f), new Color(1f, 0.9f, 0.4f), 12, 32f,
            10, UIStrings.GameSetupSeedPlaceholder.GetText());
        _seedInputField.onValidateInput = (string input, int charIndex, char addedChar) =>
        {
            if (!_useFixedSeed) return '\0';
            return char.IsDigit(addedChar) ? addedChar : '\0';
        };
        LayoutElement seedInputLay = _seedInputField.GetComponent<LayoutElement>();
        seedInputLay.preferredWidth = 160f;
        _seedInputBg = _seedInputField.GetComponent<Image>();

        // Reroll button
        _rerollBtn = UIFactory.CreateButton(row.transform, _font, UIStrings.GameSetupRerollSeed.GetText(),
            RerollSeed, new Color(0.18f, 0.42f, 0.3f, 1f), 11, 32f);
        LayoutElement rerollLay = _rerollBtn.GetComponent<LayoutElement>();
        rerollLay.preferredWidth = 120f;
        _rerollBtnText = _rerollBtn.GetComponentInChildren<Text>();
    }

    private void BuildBottomActionBar(Transform parent)
    {
        GameObject bar = UIFactory.CreateElement("BottomActionBar", parent, typeof(LayoutElement));
        LayoutElement barLayout = bar.GetComponent<LayoutElement>();
        barLayout.preferredHeight = 48f;
        barLayout.minHeight = 48f;
        UIFactory.AddHorizontalLayout(bar, 10f, 4);

        // Beenden button
        Button quitBtn = UIFactory.CreateButton(bar.transform, _font, UIStrings.Quit.GetText(),
            ConfirmQuitDialogUI.QuitApplication, new Color(0.55f, 0.18f, 0.18f, 0.95f), 12, 40f);
        LayoutElement quitLay = quitBtn.GetComponent<LayoutElement>();
        quitLay.preferredWidth = 95f;
        _bottomQuitBtnText = quitBtn.GetComponentInChildren<Text>();

        // Reset button
        Button resetBtn = UIFactory.CreateButton(bar.transform, _font, UIStrings.GameSetupResetButton.GetText(),
            ResetToDefaults, new Color(0.35f, 0.2f, 0.2f, 0.9f), 11, 40f);
        LayoutElement resetLay = resetBtn.GetComponent<LayoutElement>();
        resetLay.preferredWidth = 110f;
        _resetBtnText = resetBtn.GetComponentInChildren<Text>();

        // Quadrant Debug Labels Toggle button
        _debugLabelsBtn = UIFactory.CreateButton(bar.transform, _font,
            _showDebugLabels ? UIStrings.GameSetupDebugLabelsOn.GetText() : UIStrings.GameSetupDebugLabelsOff.GetText(),
            ToggleDebugLabels, new Color(0.22f, 0.28f, 0.38f, 0.95f), 11, 40f);
        LayoutElement debugLay = _debugLabelsBtn.GetComponent<LayoutElement>();
        debugLay.preferredWidth = 160f;
        _debugLabelsBtnText = _debugLabelsBtn.GetComponentInChildren<Text>();

        // Launch Game Button
        Button launchBtn = UIFactory.CreateButton(bar.transform, _font, UIStrings.GameSetupStartButton.GetText(),
            LaunchGame, new Color(0.12f, 0.58f, 0.35f, 1f), 13, 42f);
        LayoutElement launchLay = launchBtn.GetComponent<LayoutElement>();
        launchLay.preferredWidth = 220f;
        launchLay.flexibleWidth = 1f;

        _launchBtnText = launchBtn.GetComponentInChildren<Text>();
        if (_launchBtnText != null) _launchBtnText.fontStyle = FontStyle.Bold;
    }

    // =========================================================================
    //  2. SERVER BROWSER CONTAINER (LAN DISCOVERY)
    // =========================================================================

    private void BuildServerBrowserContainer(Transform parent)
    {
        _browserContainer = UIFactory.CreateElement("BrowserContainer", parent);
        UIFactory.AddVerticalLayout(_browserContainer, 8f, 4);

        _browserTitleText = UIFactory.CreateText(_browserContainer.transform, _font, UIStrings.LanBrowserTitle.GetText(),
            13, new Color(0.35f, 0.85f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);

        _discoveryStatusText = UIFactory.CreateText(_browserContainer.transform, _font, UIStrings.LanBrowserSearching.GetText(),
            11, new Color(0.75f, 0.85f, 0.95f), TextAnchor.MiddleLeft);

        // Server list container
        _serverListContent = UIFactory.CreateElement("ServerListContent", _browserContainer.transform, typeof(LayoutElement));
        LayoutElement listLayout = _serverListContent.GetComponent<LayoutElement>();
        listLayout.preferredHeight = 220f;
        listLayout.minHeight = 160f;
        UIFactory.AddVerticalLayout(_serverListContent, 6f, 4);

        // Manual connect row
        GameObject manualBox = UIFactory.CreateElement("ManualBox", _browserContainer.transform, typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        manualBox.GetComponent<Image>().color = new Color(0.04f, 0.11f, 0.18f, 0.85f);
        LayoutElement manualLayout = manualBox.GetComponent<LayoutElement>();
        manualLayout.preferredHeight = 46f;
        UIFactory.AddHorizontalLayout(manualBox, 8f, 6);

        _manualPlayerLabel = UIFactory.CreateText(manualBox.transform, _font, UIStrings.LanPlayerLabel.GetText(), 11, Color.white, TextAnchor.MiddleRight);
        _clientPlayerNameInput = UIFactory.CreateInputField(manualBox.transform, _font, "", null, null,
            new Color(0.02f, 0.05f, 0.09f, 1f), Color.white, 11, 30f, 16, "Auto (Player 1-4)");
        _clientPlayerNameInput.GetComponent<LayoutElement>().preferredWidth = 115f;

        UIFactory.CreateText(manualBox.transform, _font, "IP:", 11, Color.white, TextAnchor.MiddleRight);
        _manualIpInput = UIFactory.CreateInputField(manualBox.transform, _font, "127.0.0.1", null, null,
            new Color(0.02f, 0.05f, 0.09f, 1f), Color.white, 11, 30f, 20, "192.168.x.x");
        _manualIpInput.GetComponent<LayoutElement>().preferredWidth = 140f;

        UIFactory.CreateText(manualBox.transform, _font, "Port:", 11, Color.white, TextAnchor.MiddleRight);
        _manualPortInput = UIFactory.CreateInputField(manualBox.transform, _font, "7777", null, null,
            new Color(0.02f, 0.05f, 0.09f, 1f), Color.white, 11, 30f, 6, "7777");
        _manualPortInput.GetComponent<LayoutElement>().preferredWidth = 65f;

        Button manualConnectBtn = UIFactory.CreateButton(manualBox.transform, _font, UIStrings.LanConnectButton.GetText(),
            () => OnManualConnectClicked(false), new Color(0.12f, 0.55f, 0.35f, 1f), 11, 32f);
        manualConnectBtn.GetComponent<LayoutElement>().preferredWidth = 95f;
        _manualConnectBtnText = manualConnectBtn.GetComponentInChildren<Text>();

        Button manualSpectateBtn = UIFactory.CreateButton(manualBox.transform, _font, UIStrings.LanSpectateButton.GetText(),
            () => OnManualConnectClicked(true), new Color(0.15f, 0.38f, 0.62f, 1f), 11, 32f);
        manualSpectateBtn.GetComponent<LayoutElement>().preferredWidth = 95f;
        _manualSpectateBtnText = manualSpectateBtn.GetComponentInChildren<Text>();

        _browserContainer.SetActive(false);
    }

    private void HandleDiscoveredServersChanged(List<DiscoveredServerInfo> servers)
    {
        if (_serverListContent == null) return;

        // Clear previous rows
        for (int i = _serverListContent.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(_serverListContent.transform.GetChild(i).gameObject);
        }

        if (servers == null || servers.Count == 0)
        {
            if (_discoveryStatusText != null)
            {
                _discoveryStatusText.text = UIStrings.LanBrowserNoServers.GetText();
            }
            return;
        }

        if (_discoveryStatusText != null)
        {
            _discoveryStatusText.text = string.Format(UIStrings.LanBrowserServersFound.GetText(), servers.Count);
        }

        for (int i = 0; i < servers.Count; i++)
        {
            DiscoveredServerInfo server = servers[i];
            GameObject row = UIFactory.CreateElement("ServerItem_" + i, _serverListContent.transform, typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            row.GetComponent<Image>().color = new Color(0.05f, 0.14f, 0.22f, 0.95f);
            LayoutElement rowLayout = row.GetComponent<LayoutElement>();
            rowLayout.preferredHeight = 44f;
            rowLayout.minHeight = 44f;
            UIFactory.AddHorizontalLayout(row, 10f, 6);

            // Server Name
            Text nameText = UIFactory.CreateText(row.transform, _font, server.Announcement.ServerName, 12, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            nameText.gameObject.AddComponent<LayoutElement>().preferredWidth = 220f;

            // IP & Port
            Text epText = UIFactory.CreateText(row.transform, _font, server.EndpointString, 11, new Color(0.7f, 0.85f, 1f), TextAnchor.MiddleLeft);
            epText.gameObject.AddComponent<LayoutElement>().preferredWidth = 160f;

            // Players & State
            string info = string.Format(UIStrings.LanPlayersStateFormat.GetText(), server.Announcement.CurrentPlayers, server.Announcement.MaxPlayers, server.Announcement.State);
            Text infoText = UIFactory.CreateText(row.transform, _font, info, 11, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);
            infoText.gameObject.AddComponent<LayoutElement>().preferredWidth = 140f;

            // Join & Spectate Buttons
            Button joinBtn = UIFactory.CreateButton(row.transform, _font, UIStrings.LanJoinButton.GetText(),
                () => ConnectToSelectedServer(server.RemoteIp, server.TcpPort, false), new Color(0.12f, 0.58f, 0.35f, 1f), 11, 32f);
            joinBtn.GetComponent<LayoutElement>().preferredWidth = 90f;

            Button spectateBtn = UIFactory.CreateButton(row.transform, _font, UIStrings.LanSpectateButton.GetText(),
                () => ConnectToSelectedServer(server.RemoteIp, server.TcpPort, true), new Color(0.15f, 0.38f, 0.62f, 1f), 11, 32f);
            spectateBtn.GetComponent<LayoutElement>().preferredWidth = 90f;
        }
    }

    private void ConnectToSelectedServer(string hostIp, int port, bool asSpectator)
    {
        string pName = _clientPlayerNameInput != null ? _clientPlayerNameInput.text.Trim() : string.Empty;
        _ = ConnectToServerAsync(hostIp, port, pName, asSpectator);
    }

    private void OnManualConnectClicked(bool asSpectator)
    {
        string ip = _manualIpInput != null ? _manualIpInput.text.Trim() : "127.0.0.1";
        int port = 7777;
        if (_manualPortInput != null && int.TryParse(_manualPortInput.text, out int p)) port = p;
        string pName = _clientPlayerNameInput != null ? _clientPlayerNameInput.text.Trim() : string.Empty;

        _ = ConnectToServerAsync(ip, port, pName, asSpectator);
    }

    private async System.Threading.Tasks.Task ConnectToServerAsync(string ip, int port, string pName, bool asSpectator = false)
    {
        if (NetworkManager.Instance == null) return;

        try
        {
            if (_discoveryStatusText != null) _discoveryStatusText.text = $"Verbinde zu {ip}:{port}...";

            bool ok = await NetworkManager.Instance.ConnectToServerAsync(ip, port, pName, asSpectator);
            if (ok)
            {
                HookClientEvents();
                SetSetupMode(SetupMode.Lobby);
            }
            else
            {
                string reason = NetworkManager.Instance.LastConnectionError;
                string msg = !string.IsNullOrEmpty(reason)
                    ? $"Verbindung zu {ip}:{port} fehlgeschlagen:\n{reason}"
                    : $"Verbindung zu {ip}:{port} fehlgeschlagen (Server nicht erreichbar).";

                if (_discoveryStatusText != null) _discoveryStatusText.text = msg;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameSetupDialogUI] Connection attempt to {ip}:{port} failed: {ex.Message}");
            if (_discoveryStatusText != null)
            {
                _discoveryStatusText.text = $"Verbindung zu {ip}:{port} fehlgeschlagen:\n{ex.Message}";
            }
        }
    }

    // =========================================================================
    //  3. HOST LAN CONTAINER
    // =========================================================================

    private void BuildHostLanContainer(Transform parent)
    {
        _hostContainer = UIFactory.CreateElement("HostContainer", parent);
        UIFactory.AddVerticalLayout(_hostContainer, 10f, 6);

        _hostTitleText = UIFactory.CreateText(_hostContainer.transform, _font, UIStrings.LanHostTitle.GetText(),
            13, new Color(0.35f, 0.85f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);

        string localIp = NetworkManager.GetLocalIPAddress();
        _hostIpInfoText = UIFactory.CreateText(_hostContainer.transform, _font, string.Format(UIStrings.LanHostLocalIpFormat.GetText(), localIp),
            12, new Color(0.85f, 0.9f, 0.4f), TextAnchor.MiddleLeft);

        // Server Name Field
        GameObject nameRow = UIFactory.CreateElement("HostNameRow", _hostContainer.transform);
        UIFactory.AddHorizontalLayout(nameRow, 8f, 0);
        _hostServerNameLabel = UIFactory.CreateText(nameRow.transform, _font, UIStrings.LanHostServerNameLabel.GetText(), 12, Color.white, TextAnchor.MiddleLeft);
        _hostServerNameLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 160f;
        _hostServerNameInput = UIFactory.CreateInputField(nameRow.transform, _font, "Deep Space Pioneers Galaxy", null, null,
            new Color(0.02f, 0.05f, 0.09f, 1f), Color.white, 12, 34f, 32, "Servername");
        _hostServerNameInput.GetComponent<LayoutElement>().preferredWidth = 320f;

        // Player Name Field
        GameObject pRow = UIFactory.CreateElement("HostPlayerRow", _hostContainer.transform);
        UIFactory.AddHorizontalLayout(pRow, 8f, 0);
        _hostPlayerNameLabel = UIFactory.CreateText(pRow.transform, _font, UIStrings.LanHostPlayerNameLabel.GetText(), 12, Color.white, TextAnchor.MiddleLeft);
        _hostPlayerNameLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 160f;
        _hostPlayerNameInput = UIFactory.CreateInputField(pRow.transform, _font, "", null, null,
            new Color(0.02f, 0.05f, 0.09f, 1f), Color.white, 12, 34f, 20, "Auto (Player 1)");
        _hostPlayerNameInput.GetComponent<LayoutElement>().preferredWidth = 320f;

        // Port Field
        GameObject portRow = UIFactory.CreateElement("HostPortRow", _hostContainer.transform);
        UIFactory.AddHorizontalLayout(portRow, 8f, 0);
        UIFactory.CreateText(portRow.transform, _font, "TCP Port:", 12, Color.white, TextAnchor.MiddleLeft).gameObject.AddComponent<LayoutElement>().preferredWidth = 160f;
        _hostPortInput = UIFactory.CreateInputField(portRow.transform, _font, "7777", null, null,
            new Color(0.02f, 0.05f, 0.09f, 1f), Color.white, 12, 34f, 6, "7777");
        _hostPortInput.GetComponent<LayoutElement>().preferredWidth = 100f;

        // Host Action Bar
        GameObject hostBar = UIFactory.CreateElement("HostBar", _hostContainer.transform, typeof(LayoutElement));
        hostBar.GetComponent<LayoutElement>().preferredHeight = 44f;
        UIFactory.AddHorizontalLayout(hostBar, 10f, 0);

        Button hostQuitBtn = UIFactory.CreateButton(hostBar.transform, _font, UIStrings.Quit.GetText(),
            ConfirmQuitDialogUI.QuitApplication, new Color(0.55f, 0.18f, 0.18f, 0.95f), 12, 44f);
        hostQuitBtn.GetComponent<LayoutElement>().preferredWidth = 95f;
        _hostQuitBtnText = hostQuitBtn.GetComponentInChildren<Text>();

        Button startHostBtn = UIFactory.CreateButton(hostBar.transform, _font, UIStrings.LanHostStartButton.GetText(),
            OnStartHostClicked, new Color(0.12f, 0.58f, 0.35f, 1f), 13, 44f);
        startHostBtn.GetComponent<LayoutElement>().flexibleWidth = 1f;
        _hostStartBtnText = startHostBtn.GetComponentInChildren<Text>();
        _hostStartBtnText.fontStyle = FontStyle.Bold;

        _hostContainer.SetActive(false);
    }

    private async void OnStartHostClicked()
    {
        if (NetworkManager.Instance == null) return;

        string sName = _hostServerNameInput != null && !string.IsNullOrEmpty(_hostServerNameInput.text)
            ? _hostServerNameInput.text
            : "Deep Space Pioneers Server";
        int port = 7777;
        if (_hostPortInput != null && int.TryParse(_hostPortInput.text, out int p)) port = p;
        string hName = _hostPlayerNameInput != null ? _hostPlayerNameInput.text.Trim() : string.Empty;

        bool ok = await NetworkManager.Instance.StartHostAsync(sName, port, hName);
        if (ok)
        {
            HookClientEvents();
            SetSetupMode(SetupMode.Lobby);
        }
    }

    // =========================================================================
    //  4. MULTIPLAYER LOBBY CONTAINER
    // =========================================================================

    private void BuildMultiplayerLobbyContainer(Transform parent)
    {
        _lobbyContainer = UIFactory.CreateElement("LobbyContainer", parent);
        UIFactory.AddVerticalLayout(_lobbyContainer, 8f, 6);

        _lobbyTitleText = UIFactory.CreateText(_lobbyContainer.transform, _font, UIStrings.LobbyTitleDefault.GetText(),
            14, new Color(0.35f, 0.85f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);

        _lobbyStatusText = UIFactory.CreateText(_lobbyContainer.transform, _font, "...",
            11, new Color(0.75f, 0.85f, 0.95f), TextAnchor.MiddleLeft);

        // 4 Slots
        for (int i = 0; i < 4; i++)
        {
            int slotIdx = i;
            GameObject row = UIFactory.CreateElement("LobbySlotRow_" + (i + 1), _lobbyContainer.transform, typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            row.GetComponent<Image>().color = new Color(0.04f, 0.11f, 0.18f, 0.85f);
            LayoutElement rowLayout = row.GetComponent<LayoutElement>();
            rowLayout.preferredHeight = 44f;
            rowLayout.minHeight = 44f;
            UIFactory.AddHorizontalLayout(row, 10f, 6);

            // Clickable Swatch in Lobby
            GameObject swatch = UIFactory.CreateElement("ColorSwatch", row.transform, typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            _lobbySlotSwatches[i] = swatch.GetComponent<Image>();
            _lobbySlotSwatches[i].color = GetPlayerColor(i);
            _lobbySlotColorBtns[i] = swatch.GetComponent<Button>();
            _lobbySlotColorBtns[i].targetGraphic = _lobbySlotSwatches[i];
            _lobbySlotColorBtns[i].onClick.AddListener(() => CycleLobbySlotColor(slotIdx));
            LayoutElement lobbySwatchLay = swatch.GetComponent<LayoutElement>();
            lobbySwatchLay.minWidth = 26f;
            lobbySwatchLay.preferredWidth = 26f;
            lobbySwatchLay.flexibleWidth = 0f;
            lobbySwatchLay.preferredHeight = 30f;

            // Text
            _lobbySlotTexts[i] = UIFactory.CreateText(row.transform, _font, $"Slot {i + 1}: ...", 12, Color.white, TextAnchor.MiddleLeft);
            LayoutElement lobbySlotTxtLay = _lobbySlotTexts[i].gameObject.AddComponent<LayoutElement>();
            lobbySlotTxtLay.preferredWidth = 360f;
            lobbySlotTxtLay.flexibleWidth = 1f;
        }

        // Personal Name Edit Row in Lobby (for connected player's own slot)
        GameObject myNameRow = UIFactory.CreateElement("LobbyMyNameRow", _lobbyContainer.transform, typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        myNameRow.GetComponent<Image>().color = new Color(0.04f, 0.11f, 0.18f, 0.85f);
        myNameRow.GetComponent<LayoutElement>().preferredHeight = 38f;
        UIFactory.AddHorizontalLayout(myNameRow, 10f, 6);

        _lobbyMyNameLabel = UIFactory.CreateText(myNameRow.transform, _font, UIStrings.LobbyYourNameLabel.GetText(), 12, new Color(0.75f, 0.88f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
        _lobbyMyNameLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 100f;

        _lobbyMyNameInput = UIFactory.CreateInputField(myNameRow.transform, _font, "Player",
            null, OnLobbyMyNameEndEdit,
            new Color(0.02f, 0.06f, 0.11f, 0.95f), Color.white, 12, 28f, 18, "Name");
        _lobbyMyNameInput.GetComponent<LayoutElement>().preferredWidth = 220f;

        // Spectator Mode Row (configurable by Host)
        GameObject specRow = UIFactory.CreateElement("LobbySpectatorRow", _lobbyContainer.transform, typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        specRow.GetComponent<Image>().color = new Color(0.05f, 0.13f, 0.22f, 0.90f);
        specRow.GetComponent<LayoutElement>().preferredHeight = 40f;
        UIFactory.AddHorizontalLayout(specRow, 10f, 6);

        _lobbySpectatorModeLabel = UIFactory.CreateText(specRow.transform, _font, UIStrings.LobbySpectatorModeLabel.GetText(), 12, new Color(0.75f, 0.88f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
        _lobbySpectatorModeLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 135f;

        _lobbySpectatorModeBtn = UIFactory.CreateButton(specRow.transform, _font, UIStrings.LobbySpectatorFair.GetText(),
            OnToggleLobbySpectatorModeClicked, new Color(0.14f, 0.34f, 0.54f, 1f), 11, 30f);
        _lobbySpectatorModeBtn.GetComponent<LayoutElement>().preferredWidth = 220f;
        _lobbySpectatorModeText = _lobbySpectatorModeBtn.GetComponentInChildren<Text>();

        _lobbySpectatorCountText = UIFactory.CreateText(specRow.transform, _font, string.Format(UIStrings.LobbySpectatorCountFormat.GetText(), 0), 11, new Color(0.7f, 0.85f, 0.95f), TextAnchor.MiddleRight);
        _lobbySpectatorCountText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        // Seed Row in Lobby (editable by Host, synced to all clients)
        GameObject lobbySeedRow = UIFactory.CreateElement("LobbySeedRow", _lobbyContainer.transform, typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        lobbySeedRow.GetComponent<Image>().color = new Color(0.04f, 0.11f, 0.18f, 0.85f);
        lobbySeedRow.GetComponent<LayoutElement>().preferredHeight = 40f;
        UIFactory.AddHorizontalLayout(lobbySeedRow, 8f, 6);

        _lobbySeedModeBtn = UIFactory.CreateButton(lobbySeedRow.transform, _font, UIStrings.GameSetupSeedRandom.GetText(),
            OnToggleLobbySeedModeClicked, new Color(0.15f, 0.3f, 0.45f, 1f), 11, 30f);
        _lobbySeedModeBtn.GetComponent<LayoutElement>().preferredWidth = 165f;
        _lobbySeedModeBtnText = _lobbySeedModeBtn.GetComponentInChildren<Text>();

        UIFactory.CreateText(lobbySeedRow.transform, _font, "Seed:", 11, new Color(0.75f, 0.85f, 0.95f), TextAnchor.MiddleRight)
            .gameObject.AddComponent<LayoutElement>().preferredWidth = 42f;

        _lobbySeedInputField = UIFactory.CreateInputField(lobbySeedRow.transform, _font, "",
            null, OnLobbySeedEndEdit,
            new Color(0.02f, 0.05f, 0.09f, 1f), new Color(1f, 0.9f, 0.4f), 11, 30f, 10, UIStrings.GameSetupSeedPlaceholder.GetText());
        _lobbySeedInputField.onValidateInput = (string input, int charIndex, char addedChar) =>
        {
            if (_latestLobbyState == null || !_latestLobbyState.useFixedSeed) return '\0';
            return char.IsDigit(addedChar) ? addedChar : '\0';
        };
        _lobbySeedInputField.GetComponent<LayoutElement>().preferredWidth = 140f;

        _lobbyRerollBtn = UIFactory.CreateButton(lobbySeedRow.transform, _font, UIStrings.GameSetupRerollSeed.GetText(),
            OnLobbyRerollSeedClicked, new Color(0.18f, 0.42f, 0.3f, 1f), 11, 30f);
        _lobbyRerollBtn.GetComponent<LayoutElement>().preferredWidth = 120f;
        _lobbyRerollBtnText = _lobbyRerollBtn.GetComponentInChildren<Text>();

        // Action Bar for Lobby
        GameObject lobbyActionBar = UIFactory.CreateElement("LobbyActionBar", _lobbyContainer.transform);
        UIFactory.AddHorizontalLayout(lobbyActionBar, 14f, 4);

        _lobbyLeaveBtn = UIFactory.CreateButton(lobbyActionBar.transform, _font, UIStrings.LobbyLeaveButton.GetText(),
            OnLeaveLobbyClicked, new Color(0.45f, 0.2f, 0.2f, 1f), 12, 42f);
        _lobbyLeaveBtn.GetComponent<LayoutElement>().preferredWidth = 160f;
        _lobbyLeaveBtnText = _lobbyLeaveBtn.GetComponentInChildren<Text>();

        _lobbyStartBtn = UIFactory.CreateButton(lobbyActionBar.transform, _font, UIStrings.LobbyStartButton.GetText(),
            OnStartNetworkGameClicked, new Color(0.12f, 0.58f, 0.35f, 1f), 13, 42f);
        _lobbyStartBtn.GetComponent<LayoutElement>().preferredWidth = 260f;
        _lobbyStartBtnText = _lobbyStartBtn.GetComponentInChildren<Text>();
        _lobbyStartBtnText.fontStyle = FontStyle.Bold;

        _lobbyContainer.SetActive(false);
    }

    private void HookClientEvents()
    {
        if (NetworkManager.Instance?.Client == null) return;
        var client = NetworkManager.Instance.Client;

        client.OnLobbyUpdated -= HandleNetworkLobbyUpdated;
        client.OnLobbyUpdated += HandleNetworkLobbyUpdated;

        client.OnGameStarted -= HandleGameStartedFromNetwork;
        client.OnGameStarted += HandleGameStartedFromNetwork;

        client.OnDisconnected -= HandleNetworkDisconnected;
        client.OnDisconnected += HandleNetworkDisconnected;
    }

    private void HandleNetworkLobbyUpdated(LobbyStateDTO lobby)
    {
        if (lobby == null) return;
        _latestLobbyState = lobby;

        if (_lobbyTitleText != null)
        {
            _lobbyTitleText.text = string.Format(UIStrings.LobbyTitleFormat.GetText(), lobby.serverName, lobby.seed);
        }

        bool isHost = NetworkManager.Instance != null && NetworkManager.Instance.IsHost;
        if (lobby.seed != 0 && _triMap != null && _triMap.CurrentBoardSeed != lobby.seed)
        {
            Debug.Log($"[GameSetupDialogUI] Syncing board with lobby seed: {lobby.seed}");
            _currentSeed = lobby.seed;
            _useFixedSeed = lobby.useFixedSeed;
            _triMap.RebuildMap(lobby.seed);
        }
        if (_lobbySeedModeBtn != null) _lobbySeedModeBtn.interactable = isHost;
        if (_lobbySeedModeBtnText != null)
        {
            _lobbySeedModeBtnText.text = lobby.useFixedSeed
                ? UIStrings.GameSetupSeedFixed.GetText()
                : UIStrings.GameSetupSeedRandom.GetText();
        }
        if (_lobbySeedInputField != null)
        {
            _lobbySeedInputField.interactable = isHost && lobby.useFixedSeed;
            if (!_lobbySeedInputField.isFocused)
            {
                _lobbySeedInputField.text = lobby.seed > 0 ? lobby.seed.ToString() : string.Empty;
            }
        }
        if (_lobbyRerollBtn != null) _lobbyRerollBtn.interactable = isHost;
        bool isSpectator = NetworkManager.Instance != null && NetworkManager.Instance.IsSpectator;
        if (_lobbyStatusText != null)
        {
            if (isSpectator)
            {
                _lobbyStatusText.text = string.Format(UIStrings.LobbyStatusSpectator.GetText(), lobby.spectatorMode);
            }
            else
            {
                _lobbyStatusText.text = isHost
                    ? UIStrings.LobbyStatusHost.GetText()
                    : UIStrings.LobbyStatusClient.GetText();
            }
        }

        if (_lobbySpectatorModeBtn != null)
        {
            _lobbySpectatorModeBtn.interactable = isHost;
        }
        if (_lobbySpectatorModeText != null)
        {
            _lobbySpectatorModeText.text = lobby.spectatorMode == "Caster"
                ? UIStrings.LobbySpectatorCaster.GetText()
                : UIStrings.LobbySpectatorFair.GetText();
        }
        if (_lobbySpectatorCountText != null)
        {
            _lobbySpectatorCountText.text = string.Format(UIStrings.LobbySpectatorCountFormat.GetText(), lobby.spectatorCount);
        }

        if (_lobbyStartBtn != null)
        {
            _lobbyStartBtn.gameObject.SetActive(isHost);
            _lobbyStartBtn.interactable = lobby.canStart;
        }

        if (_autostartEnabled && isHost)
        {
            int connectedCount = 0;
            if (lobby.slots != null)
            {
                foreach (var s in lobby.slots)
                {
                    if (s.isConnected) connectedCount++;
                }
            }

            if (connectedCount >= _autostartMinPlayers && lobby.canStart)
            {
                Debug.Log($"[GameSetupDialogUI] Autostart condition met ({connectedCount}/{_autostartMinPlayers} players). Launching network game...");
                _autostartEnabled = false;
                OnStartNetworkGameClicked();
            }
        }

        for (int i = 0; i < lobby.slots.Count && i < 4; i++)
        {
            var slot = lobby.slots[i];
            if (i < GameSetupConfig.Slots.Length)
            {
                GameSetupConfig.Slots[i].Name = slot.playerName;
                GameSetupConfig.Slots[i].ColorIndex = Mathf.Clamp(slot.colorIndex, 0, PlayerColors.Length - 1);
                if (slot.playerType == "AI")
                {
                    GameSetupConfig.Slots[i].Type = Player.PlayerType.AI;
                    if (System.Enum.TryParse(slot.aiDifficulty, out Player.AiDifficulty diff))
                    {
                        GameSetupConfig.Slots[i].Difficulty = diff;
                    }
                }
                else
                {
                    int localIdx = NetworkManager.Instance != null ? NetworkManager.Instance.LocalPlayerIndex : -1;
                    GameSetupConfig.Slots[i].Type = (localIdx == i) ? Player.PlayerType.LOCAL : Player.PlayerType.REMOTE;
                }
            }

            if (_lobbySlotSwatches[i] != null)
            {
                _lobbySlotSwatches[i].color = GetPlayerColor(slot.colorIndex);
            }
            if (_lobbySlotColorBtns[i] != null)
            {
                int localIdx = NetworkManager.Instance != null ? NetworkManager.Instance.LocalPlayerIndex : -1;
                _lobbySlotColorBtns[i].interactable = !isSpectator && (localIdx == i || isHost);
            }

            if (_lobbyMyNameInput != null && !isSpectator)
            {
                int localIdx = NetworkManager.Instance != null ? NetworkManager.Instance.LocalPlayerIndex : -1;
                if (localIdx == i && _lobbyMyNameSlot != localIdx)
                {
                    _lobbyMyNameSlot = localIdx;
                    _lobbyMyNameInput.text = slot.playerName;
                }
                _lobbyMyNameInput.interactable = localIdx >= 0;
            }
            else if (_lobbyMyNameInput != null && isSpectator)
            {
                _lobbyMyNameInput.interactable = false;
            }

            if (_lobbySlotTexts[i] != null)
            {
                string status = slot.isConnected
                    ? (slot.isHost ? UIStrings.LobbySlotHostHuman.GetText() : UIStrings.LobbySlotConnectedHuman.GetText())
                    : (slot.playerType == "AI" ? string.Format(UIStrings.LobbySlotAiFormat.GetText(), slot.aiDifficulty) : UIStrings.LobbySlotWaiting.GetText());

                _lobbySlotTexts[i].text = $"Slot {i + 1}: {slot.playerName} – {status}";
                _lobbySlotTexts[i].color = slot.isConnected ? new Color(0.5f, 1f, 0.6f) : (slot.playerType == "AI" ? new Color(1f, 0.9f, 0.4f) : Color.gray);
            }
        }
    }

    private void OnLobbyMyNameEndEdit(string newName)
    {
        if (NetworkManager.Instance?.Client == null || NetworkManager.Instance.IsSpectator) return;
        int localIdx = NetworkManager.Instance.LocalPlayerIndex;
        if (localIdx < 0) return;
        string trimmed = newName != null ? newName.Trim() : string.Empty;
        if (string.IsNullOrEmpty(trimmed)) return;
        NetworkManager.Instance.Client.SendSetSlot(localIdx, trimmed, null, null, -1);
    }

    private void CycleLobbySlotColor(int slotIdx)
    {
        if (NetworkManager.Instance?.Client == null || _latestLobbyState?.slots == null) return;
        if (slotIdx < 0 || slotIdx >= _latestLobbyState.slots.Count) return;

        bool isHost = NetworkManager.Instance.IsHost;
        int localIdx = NetworkManager.Instance.LocalPlayerIndex;
        if (!isHost && localIdx != slotIdx) return;

        int currentColor = _latestLobbyState.slots[slotIdx].colorIndex;
        for (int step = 1; step <= 4; step++)
        {
            int candidate = (currentColor + step) % 4;
            bool usedByOtherHuman = false;
            for (int j = 0; j < _latestLobbyState.slots.Count; j++)
            {
                if (j != slotIdx && _latestLobbyState.slots[j].playerType == "Human" && _latestLobbyState.slots[j].colorIndex == candidate)
                {
                    usedByOtherHuman = true;
                    break;
                }
            }
            if (!usedByOtherHuman)
            {
                NetworkManager.Instance.Client.SendSetSlot(slotIdx, null, null, null, candidate);
                return;
            }
        }
    }

    private void OnToggleLobbySeedModeClicked()
    {
        if (NetworkManager.Instance?.Client == null || !NetworkManager.Instance.IsHost || _latestLobbyState == null) return;
        bool nextFixed = !_latestLobbyState.useFixedSeed;
        int seedToSend = _latestLobbyState.seed > 0 ? _latestLobbyState.seed : UnityEngine.Random.Range(100000, 999999);
        if (!nextFixed)
        {
            seedToSend = UnityEngine.Random.Range(100000, 999999);
        }
        NetworkManager.Instance.Client.SendSetSeed(seedToSend, nextFixed);
    }

    private void OnLobbySeedEndEdit(string text)
    {
        if (NetworkManager.Instance?.Client == null || !NetworkManager.Instance.IsHost || _latestLobbyState == null) return;
        if (!_latestLobbyState.useFixedSeed) return;
        if (int.TryParse(text, out int parsedSeed) && parsedSeed > 0)
        {
            NetworkManager.Instance.Client.SendSetSeed(parsedSeed, true);
        }
        else if (_lobbySeedInputField != null)
        {
            _lobbySeedInputField.text = _latestLobbyState.seed.ToString();
        }
    }

    private void OnLobbyRerollSeedClicked()
    {
        if (NetworkManager.Instance?.Client == null || !NetworkManager.Instance.IsHost || _latestLobbyState == null) return;
        int newSeed = UnityEngine.Random.Range(100000, 999999);
        NetworkManager.Instance.Client.SendSetSeed(newSeed, _latestLobbyState.useFixedSeed);
    }

    private void OnToggleLobbySpectatorModeClicked()
    {
        if (NetworkManager.Instance?.Client == null || !NetworkManager.Instance.IsHost) return;
        string current = NetworkManager.Instance.CurrentSpectatorMode;
        string next = current == "Caster" ? "Fair" : "Caster";
        NetworkManager.Instance.Client.SendSetSpectatorMode(next);
    }

    private void OnStartNetworkGameClicked()
    {
        if (NetworkManager.Instance?.Client != null && NetworkManager.Instance.IsHost)
        {
            int seedToSend = (_triMap != null && _triMap.CurrentBoardSeed != 0)
                ? _triMap.CurrentBoardSeed
                : _currentSeed;
            NetworkManager.Instance.Client.SendStartGame(seedToSend, _useFixedSeed);
        }
    }

    private void OnLeaveLobbyClicked()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.DisconnectAll();
        }
        SetSetupMode(SetupMode.Local);
    }

    private void HandleGameStartedFromNetwork(GameStartedDTO dto)
    {
        Debug.Log($"[GameSetupDialogUI] Game started over network with seed {dto.Seed}");
        _useFixedSeed = true;
        _currentSeed = dto.Seed;

        if (_triMap != null)
        {
            if (dto.Seed != 0 && _triMap.CurrentBoardSeed != dto.Seed)
            {
                Debug.Log($"[GameSetupDialogUI] Rebuilding client board with GameStarted seed: {dto.Seed}");
                _triMap.RebuildMap(dto.Seed);
            }
            _triMap.StartGameFromSetup();
        }

        Hide();
        _onGameStarted?.Invoke();
    }

    private void HandleNetworkDisconnected(string reason)
    {
        Debug.LogWarning($"[GameSetupDialogUI] Disconnected: {reason}");
        SetSetupMode(SetupMode.ServerBrowser);
    }

    // =========================================================================
    //  INTERACTIVE ACTIONS & STATE (LOCAL)
    // =========================================================================

    private bool _hasProcessedCommandLine = false;
    private bool _autostartEnabled = false;
    private int _autostartMinPlayers = 2;

    public void Show()
    {
        LoadFromConfig();
        gameObject.SetActive(true);
        CheckCommandLineAutoConnect();
    }

    private async void CheckCommandLineAutoConnect()
    {
        if (_hasProcessedCommandLine) return;
        _hasProcessedCommandLine = true;

        string[] args = System.Environment.GetCommandLineArgs();
        bool isHost = false;
        string connectAddress = null;
        string playerName = null;
        int port = 7777;
        string serverName = "Deep Space Pioneers Server";
        bool autostart = false;
        int autostartMinPlayers = 2;

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            string lower = a.ToLowerInvariant();

            if (lower == "-host" || lower == "-host-game")
            {
                isHost = true;
            }
            else if (lower.StartsWith("-connect:") || lower.StartsWith("-join:"))
            {
                connectAddress = a.Substring(a.IndexOf(':') + 1);
            }
            else if (lower.StartsWith("-player-name:") || lower.StartsWith("-name:"))
            {
                playerName = a.Substring(a.IndexOf(':') + 1);
            }
            else if (lower.StartsWith("-server-name:"))
            {
                serverName = a.Substring(a.IndexOf(':') + 1);
            }
            else if (lower.StartsWith("-port:") && int.TryParse(a.Substring(6), out int p))
            {
                port = p;
            }
            else if (lower.StartsWith("-seed:") && int.TryParse(a.Substring(6), out int s))
            {
                _currentSeed = s;
                _useFixedSeed = true;
                GameSetupConfig.UseFixedSeed = true;
                GameSetupConfig.Seed = s;
            }
            else if (lower == "-autostart")
            {
                autostart = true;
            }
            else if (lower.StartsWith("-autostart:") && int.TryParse(a.Substring(11), out int minP))
            {
                autostart = true;
                autostartMinPlayers = minP;
            }
        }

        if (NetworkManager.Instance == null) return;

        if (isHost)
        {
            if (string.IsNullOrEmpty(playerName)) playerName = "Spieler 1";
            _autostartEnabled = autostart;
            _autostartMinPlayers = autostartMinPlayers;
            Debug.Log($"[GameSetupDialogUI] Auto-hosting on port {port} as '{playerName}' (autostart={autostart}, minPlayers={autostartMinPlayers})...");
            bool ok = await NetworkManager.Instance.StartHostAsync(serverName, port, playerName);
            if (ok)
            {
                HookClientEvents();
                SetSetupMode(SetupMode.Lobby);
            }
        }
        else if (!string.IsNullOrEmpty(connectAddress))
        {
            if (string.IsNullOrEmpty(playerName)) playerName = "Spieler 2";
            string ip = connectAddress;
            if (ip.Contains(":"))
            {
                string[] parts = ip.Split(':');
                ip = parts[0];
                if (int.TryParse(parts[1], out int p)) port = p;
            }
            Debug.Log($"[GameSetupDialogUI] Auto-connecting to {ip}:{port} as '{playerName}'...");
            bool ok = await NetworkManager.Instance.ConnectToServerAsync(ip, port, playerName);
            if (ok)
            {
                HookClientEvents();
                SetSetupMode(SetupMode.Lobby);
            }
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void LoadFromConfig()
    {
        for (int i = 0; i < 4; i++)
        {
            var configSlot = GameSetupConfig.Slots[i];
            var uiSlot = _slots[i];
            if (uiSlot != null && configSlot != null)
            {
                uiSlot.CurrentType = configSlot.Type;
                uiSlot.CurrentDifficulty = configSlot.Difficulty;
                uiSlot.ColorIndex = Mathf.Clamp(configSlot.ColorIndex, 0, PlayerColors.Length - 1);
                if (uiSlot.NameInput != null) uiSlot.NameInput.text = configSlot.Name;
                UpdateSlotUI(uiSlot);
            }
        }

        if (_triMap != null && _triMap.CurrentBoardSeed != 0)
        {
            _currentSeed = _triMap.CurrentBoardSeed;
            _useFixedSeed = _triMap.IsCurrentBoardSeedFixed;
        }
        else
        {
            _useFixedSeed = GameSetupConfig.UseFixedSeed;
            _currentSeed = GameSetupConfig.Seed;
        }
        _showDebugLabels = GameSetupConfig.ShowSystemDebugLabels;

        if (_triMap != null && _triMap.DebugSettings != null)
        {
            _triMap.DebugSettings.ShowSystemDebugLabels = _showDebugLabels;
        }

        UpdateSeedUI();
        UpdateDebugLabelsBtnUI();

        // Update LAN Browser, Host & Lobby strings
        if (_browserTitleText != null) _browserTitleText.text = UIStrings.LanBrowserTitle.GetText();
        if (_manualPlayerLabel != null) _manualPlayerLabel.text = UIStrings.LanPlayerLabel.GetText();
        if (_manualConnectBtnText != null) _manualConnectBtnText.text = UIStrings.LanConnectButton.GetText();
        if (_manualSpectateBtnText != null) _manualSpectateBtnText.text = UIStrings.LanSpectateButton.GetText();

        if (_hostTitleText != null) _hostTitleText.text = UIStrings.LanHostTitle.GetText();
        if (_hostIpInfoText != null) _hostIpInfoText.text = string.Format(UIStrings.LanHostLocalIpFormat.GetText(), NetworkManager.GetLocalIPAddress());
        if (_hostServerNameLabel != null) _hostServerNameLabel.text = UIStrings.LanHostServerNameLabel.GetText();
        if (_hostPlayerNameLabel != null) _hostPlayerNameLabel.text = UIStrings.LanHostPlayerNameLabel.GetText();
        if (_hostQuitBtnText != null) _hostQuitBtnText.text = UIStrings.Quit.GetText();
        if (_hostStartBtnText != null) _hostStartBtnText.text = UIStrings.LanHostStartButton.GetText();

        if (_lobbyMyNameLabel != null) _lobbyMyNameLabel.text = UIStrings.LobbyYourNameLabel.GetText();
        if (_lobbySpectatorModeLabel != null) _lobbySpectatorModeLabel.text = UIStrings.LobbySpectatorModeLabel.GetText();
        if (_lobbyRerollBtnText != null) _lobbyRerollBtnText.text = UIStrings.GameSetupRerollSeed.GetText();
        if (_lobbyLeaveBtnText != null) _lobbyLeaveBtnText.text = UIStrings.LobbyLeaveButton.GetText();
        if (_lobbyStartBtnText != null) _lobbyStartBtnText.text = UIStrings.LobbyStartButton.GetText();

        if (_latestLobbyState != null)
        {
            HandleNetworkLobbyUpdated(_latestLobbyState);
        }
    }

    private void CycleLocalSlotColor(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 4) return;
        var slot = _slots[slotIndex];
        if (slot == null) return;

        for (int step = 1; step <= 4; step++)
        {
            int candidate = (slot.ColorIndex + step) % 4;
            bool usedByOtherHuman = false;
            int aiHolder = -1;
            for (int j = 0; j < 4; j++)
            {
                if (j != slotIndex && _slots[j] != null && _slots[j].ColorIndex == candidate)
                {
                    if (_slots[j].CurrentType != Player.PlayerType.AI)
                    {
                        usedByOtherHuman = true;
                    }
                    else
                    {
                        aiHolder = j;
                    }
                    break;
                }
            }

            if (!usedByOtherHuman)
            {
                int oldColor = slot.ColorIndex;
                slot.ColorIndex = candidate;
                if (aiHolder >= 0)
                {
                    _slots[aiHolder].ColorIndex = oldColor;
                    UpdateSlotUI(_slots[aiHolder]);
                }
                UpdateSlotUI(slot);
                return;
            }
        }
    }

    private void TogglePlayerType(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 4) return;
        var slot = _slots[slotIndex];
        if (slot == null) return;

        slot.CurrentType = (slot.CurrentType == Player.PlayerType.LOCAL)
            ? Player.PlayerType.AI
            : Player.PlayerType.LOCAL;

        UpdateSlotUI(slot);
    }

    private void CycleAiDifficulty(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 4) return;
        var slot = _slots[slotIndex];
        if (slot == null) return;

        switch (slot.CurrentDifficulty)
        {
            case Player.AiDifficulty.Easy:
                slot.CurrentDifficulty = Player.AiDifficulty.Normal;
                break;
            case Player.AiDifficulty.Normal:
                slot.CurrentDifficulty = Player.AiDifficulty.Hard;
                break;
            case Player.AiDifficulty.Hard:
                slot.CurrentDifficulty = Player.AiDifficulty.Easy;
                break;
        }

        UpdateSlotUI(slot);
    }

    private void UpdateSlotUI(SlotUI slot)
    {
        if (slot == null) return;

        bool isAi = slot.CurrentType == Player.PlayerType.AI;
        slot.TypeButtonText.text = isAi
            ? UIStrings.GameSetupPlayerAi.GetText()
            : UIStrings.GameSetupPlayerLocal.GetText();

        slot.TypeButtonImage.color = isAi
            ? new Color(0.55f, 0.28f, 0.12f, 1f)
            : new Color(0.12f, 0.35f, 0.55f, 1f);

        slot.DiffContainer.SetActive(isAi);
        slot.DiffButtonText.text = GetDifficultyLabel(slot.CurrentDifficulty);
        slot.DiffButtonImage.color = GetDifficultyColor(slot.CurrentDifficulty);
        if (slot.ColorSwatch != null)
        {
            slot.ColorSwatch.color = GetPlayerColor(slot.ColorIndex);
        }
    }

    private string GetDifficultyLabel(Player.AiDifficulty difficulty)
    {
        switch (difficulty)
        {
            case Player.AiDifficulty.Easy: return UIStrings.GameSetupDifficultyEasy.GetText();
            case Player.AiDifficulty.Normal: return UIStrings.GameSetupDifficultyNormal.GetText();
            case Player.AiDifficulty.Hard: return UIStrings.GameSetupDifficultyHard.GetText();
            default: return difficulty.ToString();
        }
    }

    private Color GetDifficultyColor(Player.AiDifficulty difficulty)
    {
        switch (difficulty)
        {
            case Player.AiDifficulty.Easy: return new Color(0.2f, 0.6f, 0.3f, 1f);
            case Player.AiDifficulty.Normal: return new Color(0.7f, 0.55f, 0.1f, 1f);
            case Player.AiDifficulty.Hard: return new Color(0.75f, 0.2f, 0.2f, 1f);
            default: return Color.gray;
        }
    }

    private void OnSeedValueChanged(string text) { }

    private void OnSeedEndEdit(string text)
    {
        if (!_useFixedSeed) return;

        if (int.TryParse(text, out int parsedSeed) && parsedSeed > 0)
        {
            _currentSeed = parsedSeed;
        }
        else
        {
            if (_seedInputField != null) _seedInputField.text = _currentSeed.ToString();
        }
        UpdateSeedUI();
    }

    private void ToggleSeedMode()
    {
        _useFixedSeed = !_useFixedSeed;
        if (!_useFixedSeed && _triMap != null)
        {
            _currentSeed = UnityEngine.Random.Range(100000, 999999);
        }
        UpdateSeedUI();
    }

    private void RerollSeed()
    {
        _currentSeed = UnityEngine.Random.Range(100000, 999999);
        UpdateSeedUI();
    }

    private void UpdateSeedUI()
    {
        if (_seedModeBtnText != null)
        {
            _seedModeBtnText.text = _useFixedSeed
                ? UIStrings.GameSetupSeedFixed.GetText()
                : UIStrings.GameSetupSeedRandom.GetText();
        }

        if (_seedInputField != null)
        {
            _seedInputField.interactable = _useFixedSeed;
            _seedInputField.text = _currentSeed.ToString();
        }

        if (_seedInputBg != null)
        {
            _seedInputBg.color = _useFixedSeed
                ? new Color(0.02f, 0.08f, 0.14f, 1f)
                : new Color(0.05f, 0.08f, 0.1f, 0.45f);
        }

        if (_rerollBtn != null)
        {
            _rerollBtn.gameObject.SetActive(_useFixedSeed);
        }
    }

    private void ToggleDebugLabels()
    {
        _showDebugLabels = !_showDebugLabels;
        UpdateDebugLabelsBtnUI();

        if (_triMap != null && _triMap.DebugSettings != null)
        {
            _triMap.DebugSettings.ShowSystemDebugLabels = _showDebugLabels;
            _triMap.UpdateSystemLabelsVisibility();
        }
    }

    private void UpdateDebugLabelsBtnUI()
    {
        if (_debugLabelsBtn != null && _debugLabelsBtnText != null)
        {
            _debugLabelsBtnText.text = _showDebugLabels
                ? UIStrings.GameSetupDebugLabelsOn.GetText()
                : UIStrings.GameSetupDebugLabelsOff.GetText();

            Image btnImg = _debugLabelsBtn.GetComponent<Image>();
            if (btnImg != null)
            {
                btnImg.color = _showDebugLabels
                    ? new Color(0.18f, 0.45f, 0.28f, 1f)
                    : new Color(0.22f, 0.28f, 0.38f, 0.95f);
            }
        }
    }

    private void ResetToDefaults()
    {
        GameSetupConfig.ResetToDefaults();
        if (_triMap != null && _triMap.DebugSettings != null)
        {
            _triMap.DebugSettings.ShowSystemDebugLabels = false;
            _triMap.UpdateSystemLabelsVisibility();
        }
        LoadFromConfig();
    }

    private void LaunchGame()
    {
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < 4; i++)
        {
            var slot = _slots[i];
            if (slot != null)
            {
                GameSetupConfig.Slots[i].Type = slot.CurrentType;
                GameSetupConfig.Slots[i].Difficulty = slot.CurrentDifficulty;
                GameSetupConfig.Slots[i].ColorIndex = slot.ColorIndex;
                string enteredName = slot.NameInput != null ? slot.NameInput.text.Trim() : string.Empty;
                string candidate = !string.IsNullOrEmpty(enteredName) ? enteredName : $"Player {i + 1}";
                if (usedNames.Contains(candidate))
                {
                    candidate = $"Player {i + 1}";
                    if (usedNames.Contains(candidate)) candidate = $"{enteredName} ({i + 1})";
                }
                usedNames.Add(candidate);
                GameSetupConfig.Slots[i].Name = candidate;
            }
        }

        if (_useFixedSeed && _seedInputField != null)
        {
            if (int.TryParse(_seedInputField.text, out int parsedSeed) && parsedSeed > 0)
            {
                _currentSeed = parsedSeed;
            }
        }

        GameSetupConfig.UseFixedSeed = _useFixedSeed;
        GameSetupConfig.Seed = _currentSeed;
        GameSetupConfig.ShowSystemDebugLabels = _showDebugLabels;

        if (_triMap != null && _triMap.DebugSettings != null)
        {
            _triMap.DebugSettings.ShowSystemDebugLabels = _showDebugLabels;
            _triMap.UpdateSystemLabelsVisibility();
        }

        bool seedChanged = (_currentSeed != _triMap.CurrentBoardSeed) || (_useFixedSeed != _triMap.IsCurrentBoardSeedFixed);
        if (seedChanged)
        {
            GameSetupConfig.HasPendingGameStart = true;
            if (_triMap != null && _triMap.DebugSettings != null)
            {
                _triMap.DebugSettings.UseFixedBoardSeed = _useFixedSeed;
                _triMap.DebugSettings.FixedBoardSeed = _currentSeed;
            }
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        if (_triMap != null)
        {
            _triMap.StartGameFromSetup();
        }

        Hide();
        _onGameStarted?.Invoke();
    }

    public void TryStartGame()
    {
        if (_currentMode == SetupMode.Lobby)
        {
            if (_autostartEnabled)
            {
                Debug.Log("[GameSetupDialogUI] TryStartGame ignored: Waiting for autostart conditions.");
                return;
            }
            OnStartNetworkGameClicked();
        }
        else
        {
            LaunchGame();
        }
    }

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
        RefreshLanguage();
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
    }

    private void HandleLanguageChanged(GameLanguage language)
    {
        RefreshLanguage();
    }

    public void RefreshLanguage()
    {
        if (_titleText != null) _titleText.text = UIStrings.GameSetupTitle.GetText();
        if (_subtitleText != null) _subtitleText.text = UIStrings.GameSetupSubtitle.GetText();
        if (_languageBtnText != null) _languageBtnText.text = UIStrings.LanguageShortButton.GetText();
        if (_headerQuitBtnText != null) _headerQuitBtnText.text = "✕ " + UIStrings.Quit.GetText();

        if (_tabLocalBtnText != null) _tabLocalBtnText.text = UIStrings.GameSetupTabLocal.GetText();
        if (_tabBrowserBtnText != null) _tabBrowserBtnText.text = UIStrings.GameSetupTabBrowser.GetText();
        if (_tabHostBtnText != null) _tabHostBtnText.text = UIStrings.GameSetupTabHost.GetText();

        if (_playersHeadingText != null) _playersHeadingText.text = UIStrings.GameSetupPlayersHeading.GetText();
        if (_seedHeadingText != null) _seedHeadingText.text = UIStrings.GameSetupSeedHeading.GetText();
        if (_rerollBtnText != null) _rerollBtnText.text = UIStrings.GameSetupRerollSeed.GetText();
        if (_bottomQuitBtnText != null) _bottomQuitBtnText.text = UIStrings.Quit.GetText();
        if (_resetBtnText != null) _resetBtnText.text = UIStrings.GameSetupResetButton.GetText();
        if (_launchBtnText != null) _launchBtnText.text = UIStrings.GameSetupStartButton.GetText();

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] != null)
            {
                if (_slots[i].NameInput != null)
                {
                    string cur = _slots[i].NameInput.text;
                    if (string.IsNullOrEmpty(cur) || cur == $"Spieler {i + 1}" || cur == $"Player {i + 1}")
                    {
                        _slots[i].NameInput.text = string.Format(UIStrings.DefaultPlayerName.GetText(), i + 1);
                    }
                }
                UpdateSlotUI(_slots[i]);
            }
        }

        UpdateSeedUI();
        UpdateDebugLabelsBtnUI();

        // Update LAN Browser, Host & Lobby strings
        if (_browserTitleText != null) _browserTitleText.text = UIStrings.LanBrowserTitle.GetText();
        if (_manualPlayerLabel != null) _manualPlayerLabel.text = UIStrings.LanPlayerLabel.GetText();
        if (_manualConnectBtnText != null) _manualConnectBtnText.text = UIStrings.LanConnectButton.GetText();
        if (_manualSpectateBtnText != null) _manualSpectateBtnText.text = UIStrings.LanSpectateButton.GetText();

        if (_hostTitleText != null) _hostTitleText.text = UIStrings.LanHostTitle.GetText();
        if (_hostIpInfoText != null) _hostIpInfoText.text = string.Format(UIStrings.LanHostLocalIpFormat.GetText(), NetworkManager.GetLocalIPAddress());
        if (_hostServerNameLabel != null) _hostServerNameLabel.text = UIStrings.LanHostServerNameLabel.GetText();
        if (_hostPlayerNameLabel != null) _hostPlayerNameLabel.text = UIStrings.LanHostPlayerNameLabel.GetText();
        if (_hostQuitBtnText != null) _hostQuitBtnText.text = UIStrings.Quit.GetText();
        if (_hostStartBtnText != null) _hostStartBtnText.text = UIStrings.LanHostStartButton.GetText();

        if (_lobbyMyNameLabel != null) _lobbyMyNameLabel.text = UIStrings.LobbyYourNameLabel.GetText();
        if (_lobbySpectatorModeLabel != null) _lobbySpectatorModeLabel.text = UIStrings.LobbySpectatorModeLabel.GetText();
        if (_lobbyRerollBtnText != null) _lobbyRerollBtnText.text = UIStrings.GameSetupRerollSeed.GetText();
        if (_lobbyLeaveBtnText != null) _lobbyLeaveBtnText.text = UIStrings.LobbyLeaveButton.GetText();
        if (_lobbyStartBtnText != null) _lobbyStartBtnText.text = UIStrings.LobbyStartButton.GetText();

        if (_latestLobbyState != null)
        {
            HandleNetworkLobbyUpdated(_latestLobbyState);
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.Discovery != null)
        {
            NetworkManager.Instance.Discovery.OnServersChanged -= HandleDiscoveredServersChanged;
        }
        if (NetworkManager.Instance?.Client != null)
        {
            NetworkManager.Instance.Client.OnLobbyUpdated -= HandleNetworkLobbyUpdated;
            NetworkManager.Instance.Client.OnGameStarted -= HandleGameStartedFromNetwork;
            NetworkManager.Instance.Client.OnDisconnected -= HandleNetworkDisconnected;
        }
    }
}
