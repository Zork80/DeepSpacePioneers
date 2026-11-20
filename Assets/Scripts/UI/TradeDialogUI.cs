using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal dialog for Bank/Supply trade and Player-to-Player trade.
/// Arranged using layout groups with no hardcoded offsets.
/// </summary>
public class TradeDialogUI : MonoBehaviour
{
    private Font _font;
    private Text _titleText;
    private Text _statusText;
    private Button _supplyTabBtn;
    private Button _playersTabBtn;
    private RectTransform _tabContent;
    private readonly List<GameObject> _dynamicElements = new List<GameObject>();

    private int _activeTradeTab = 0; // 0 = Supply, 1 = Player
    private ResourceType _supplyOfferedResource = ResourceType.Ore;
    private ResourceType _supplyRequestedResource = ResourceType.Fuel;
    private int _playerTradePartnerIndex = 0;
    private readonly Dictionary<ResourceType, int> _playerTradeOffer = new Dictionary<ResourceType, int>();
    private readonly Dictionary<ResourceType, int> _playerTradeRequest = new Dictionary<ResourceType, int>();

    private TriMap _triMap;
    private Action _onTradeExecuted;

    private static readonly Color TabActiveColor = new Color(0.18f, 0.45f, 0.72f, 1f);
    private static readonly Color TabInactiveColor = new Color(0.08f, 0.16f, 0.24f, 1f);

    public static TradeDialogUI Create(Transform parent, Font font)
    {
        Image background = UIFactory.CreatePanel("TradeOverlay", parent,
            new Color(0.015f, 0.04f, 0.06f, 0.98f), true);
        GameObject root = background.gameObject;

        RectTransform rect = UIFactory.Rect(root);
        UIFactory.SetAnchors(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580f, 0f));

        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = new Color(0.2f, 0.5f, 0.7f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);

        TradeDialogUI dialog = root.AddComponent<TradeDialogUI>();
        dialog._font = font;

        UIFactory.AddVerticalLayout(root, 10f, 16);
        UIFactory.AddSizeFitter(root);

        // Title
        dialog._titleText = UIFactory.CreateText(root.transform, font, UIStrings.TradeOverlayTitle.GetText(),
            20, new Color(0.38f, 0.82f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);

        // Tabs Row
        GameObject tabsRow = UIFactory.CreateElement("TabsRow", root.transform);
        UIFactory.AddHorizontalLayout(tabsRow, 10f);
        tabsRow.AddComponent<LayoutElement>().minHeight = 34f;

        dialog._supplyTabBtn = UIFactory.CreateButton(tabsRow.transform, font,
            UIStrings.TradeWithSupplyTab.GetText(), () => dialog.SelectTab(0),
            TabActiveColor, 12, 34f);

        dialog._playersTabBtn = UIFactory.CreateButton(tabsRow.transform, font,
            UIStrings.TradeWithPlayersTab.GetText(), () => dialog.SelectTab(1),
            TabInactiveColor, 12, 34f);

        // Dynamic Tab Content Area
        GameObject contentArea = UIFactory.CreateElement("TradeTabContent", root.transform);
        UIFactory.AddVerticalLayout(contentArea, 8f, 0);
        dialog._tabContent = UIFactory.Rect(contentArea);

        // Status Text
        dialog._statusText = UIFactory.CreateText(root.transform, font, string.Empty, 13,
            Color.white, TextAnchor.MiddleCenter);

        // Bottom Row: Close Button
        GameObject bottomRow = UIFactory.CreateElement("BottomRow", root.transform);
        HorizontalLayoutGroup botLayout = UIFactory.AddHorizontalLayout(bottomRow, 0f);
        botLayout.childAlignment = TextAnchor.MiddleCenter;
        botLayout.childForceExpandWidth = false;

        Button closeBtn = UIFactory.CreateButton(bottomRow.transform, font, UIStrings.Close.GetText(),
            dialog.Close, new Color(0.24f, 0.28f, 0.32f, 1f), 13, 34f);
        EncounterDialogUI.SetPreferredSize(closeBtn, 160f, 34f);

        root.SetActive(false);
        return dialog;
    }

    public void Open(TriMap triMap, Action onTradeExecuted)
    {
        _triMap = triMap;
        _onTradeExecuted = onTradeExecuted;

        if (!GameRules.IsResource(_supplyOfferedResource))
        {
            _supplyOfferedResource = ResourceType.Ore;
        }
        if (!GameRules.IsResource(_supplyRequestedResource))
        {
            _supplyRequestedResource = ResourceType.Fuel;
        }

        InitPlayerTradeSelections(false);
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void UpdateLanguage()
    {
        if (_titleText != null)
        {
            _titleText.text = UIStrings.TradeOverlayTitle.GetText();
        }
        if (_supplyTabBtn != null)
        {
            _supplyTabBtn.GetComponentInChildren<Text>().text = UIStrings.TradeWithSupplyTab.GetText();
        }
        if (_playersTabBtn != null)
        {
            _playersTabBtn.GetComponentInChildren<Text>().text = UIStrings.TradeWithPlayersTab.GetText();
        }
        if (gameObject.activeSelf)
        {
            Refresh();
        }
    }

    private void SelectTab(int tab)
    {
        _activeTradeTab = tab;
        Refresh();
    }

    public void Refresh()
    {
        if (_triMap == null || !gameObject.activeSelf)
        {
            return;
        }

        ClearContent();

        // Update tab button colors
        _supplyTabBtn.GetComponent<Image>().color = _activeTradeTab == 0 ? TabActiveColor : TabInactiveColor;
        _playersTabBtn.GetComponent<Image>().color = _activeTradeTab == 1 ? TabActiveColor : TabInactiveColor;

        if (_activeTradeTab == 0)
        {
            RenderSupplyTradeView();
        }
        else
        {
            RenderPlayerTradeView();
        }
    }

    private void ClearContent()
    {
        foreach (GameObject elem in _dynamicElements)
        {
            if (elem != null)
            {
                Destroy(elem);
            }
        }
        _dynamicElements.Clear();
    }

    private void RenderSupplyTradeView()
    {
        Player player = _triMap.CurrentPlayer;

        // Give prompt
        AddText(UIStrings.SupplyTradeGivePrompt.GetText(), 13, new Color(0.85f, 0.9f, 0.95f));

        if (player != null && player.HasFriendshipCard(FriendshipCardId.TraderTradeGoods11))
        {
            if (!player.HasUsedGoods11TradeThisTurn)
            {
                AddText(UIStrings.SupplyTradeGoods11Active.GetText(), 11, new Color(0.35f, 0.95f, 0.6f));
            }
            else
            {
                AddText(UIStrings.SupplyTradeGoods11Used.GetText(), 11, new Color(1f, 0.78f, 0.35f));
            }
        }

        // Row of resources to give
        ResourceType[] resources = new ResourceType[]
        {
            ResourceType.Ore,
            ResourceType.Fuel,
            ResourceType.Carbon,
            ResourceType.Food,
            ResourceType.TradeGoods
        };

        RectTransform giveRow = AddRow(8f);
        foreach (ResourceType res in resources)
        {
            ResourceType r = res;
            int requiredRate = player != null ? player.GetSupplyTradeRate(r) : GameRules.GetSupplyTradeRate(r);
            int count = player != null ? player.GetResourceCount(r) : 0;
            bool selected = r == _supplyOfferedResource;
            bool canAfford = count >= requiredRate;

            Color btnColor = selected
                ? new Color(0.1f, 0.55f, 0.3f, 1f)
                : (canAfford ? new Color(0.12f, 0.3f, 0.48f, 1f) : new Color(0.15f, 0.18f, 0.22f, 1f));

            string label = string.Format("{0}\n({1}:1) [{2}]", UIStrings.GetResourceName(r), requiredRate, count);
            Button btn = UIFactory.CreateButton(giveRow, _font, label, () =>
            {
                _supplyOfferedResource = r;
                Refresh();
            }, btnColor, 11, 44f);
            EncounterDialogUI.SetPreferredSize(btn, 100f, 44f);
        }

        // Receive prompt
        AddText(UIStrings.SupplyTradeReceivePrompt.GetText(), 13, new Color(0.85f, 0.9f, 0.95f));

        // Row of resources to receive
        RectTransform receiveRow = AddRow(8f);
        foreach (ResourceType res in resources)
        {
            ResourceType r = res;
            bool selected = r == _supplyRequestedResource;
            Color btnColor = selected
                ? new Color(0.1f, 0.55f, 0.3f, 1f)
                : new Color(0.12f, 0.3f, 0.48f, 1f);

            string label = string.Format("{0}\n(1)", UIStrings.GetResourceName(r));
            Button btn = UIFactory.CreateButton(receiveRow, _font, label, () =>
            {
                _supplyRequestedResource = r;
                Refresh();
            }, btnColor, 11, 40f);
            EncounterDialogUI.SetPreferredSize(btn, 100f, 40f);
        }

        // Evaluation & Status
        int rate = player != null ? player.GetSupplyTradeRate(_supplyOfferedResource) : GameRules.GetSupplyTradeRate(_supplyOfferedResource);
        bool canTrade = _triMap.CanTradeWithSupply(_supplyOfferedResource, _supplyRequestedResource);

        if (_supplyOfferedResource == _supplyRequestedResource)
        {
            _statusText.text = "Cannot trade for the same resource.";
            _statusText.color = new Color(1f, 0.4f, 0.4f);
        }
        else if (!canTrade)
        {
            _statusText.text = string.Format("Need at least {0}x {1}.", rate, UIStrings.GetResourceName(_supplyOfferedResource));
            _statusText.color = new Color(1f, 0.75f, 0.3f);
        }
        else
        {
            if (_supplyOfferedResource == ResourceType.TradeGoods && player != null &&
                player.HasFriendshipCard(FriendshipCardId.TraderTradeGoods11) && player.HasUsedGoods11TradeThisTurn)
            {
                _statusText.text = string.Format(
                    UIStrings.SupplyTradeSummaryGoods11Used.GetText(),
                    rate,
                    UIStrings.GetResourceName(_supplyOfferedResource),
                    UIStrings.GetResourceName(_supplyRequestedResource));
            }
            else
            {
                _statusText.text = string.Format(
                    UIStrings.SupplyTradeSummary.GetText(),
                    rate,
                    UIStrings.GetResourceName(_supplyOfferedResource),
                    UIStrings.GetResourceName(_supplyRequestedResource));
            }
            _statusText.color = new Color(0.4f, 1f, 0.6f);
        }

        // Execute Button
        GameObject execRow = AddRow(0f).gameObject;
        Button executeBtn = UIFactory.CreateButton(execRow.transform, _font,
            UIStrings.ExecuteSupplyTradeButton.GetText(), () =>
            {
                if (_triMap.TryTradeWithSupply(_supplyOfferedResource, _supplyRequestedResource))
                {
                    _onTradeExecuted?.Invoke();
                    Refresh();
                }
            },
            canTrade ? new Color(0.18f, 0.55f, 0.32f, 1f) : new Color(0.15f, 0.18f, 0.22f, 0.7f),
            13, 38f);
        executeBtn.interactable = canTrade;
        EncounterDialogUI.SetPreferredSize(executeBtn, 250f, 38f);
    }

    private void RenderPlayerTradeView()
    {
        Player player = _triMap.CurrentPlayer;
        List<Player> otherPlayers = _triMap.Players != null
            ? _triMap.Players.Where(p => p != player).ToList()
            : new List<Player>();

        if (otherPlayers.Count == 0)
        {
            _statusText.text = "No other players in game to trade with.";
            _statusText.color = new Color(1f, 0.75f, 0.3f);
            return;
        }

        if (_playerTradePartnerIndex < 0 || _playerTradePartnerIndex >= otherPlayers.Count)
        {
            _playerTradePartnerIndex = 0;
        }
        Player partner = otherPlayers[_playerTradePartnerIndex];

        // Partner selection prompt
        AddText(UIStrings.PlayerTradePartnerPrompt.GetText(), 12, new Color(0.85f, 0.9f, 0.95f));

        RectTransform partnerRow = AddRow(8f);
        for (int p = 0; p < otherPlayers.Count; p++)
        {
            int idx = p;
            Player other = otherPlayers[p];
            bool selected = idx == _playerTradePartnerIndex;
            Color btnColor = selected
                ? new Color(0.18f, 0.55f, 0.75f, 1f)
                : new Color(0.1f, 0.2f, 0.28f, 1f);

            Button pBtn = UIFactory.CreateButton(partnerRow, _font, other.PlayerName, () =>
            {
                _playerTradePartnerIndex = idx;
                Refresh();
            }, btnColor, 11, 28f);
            EncounterDialogUI.SetPreferredSize(pBtn, 120f, 28f);
        }

        ResourceType[] resources = new ResourceType[]
        {
            ResourceType.Ore,
            ResourceType.Fuel,
            ResourceType.Carbon,
            ResourceType.Food,
            ResourceType.TradeGoods
        };

        // You Offer Prompt
        AddText(UIStrings.PlayerTradeYouOfferPrompt.GetText(), 12, new Color(0.85f, 0.9f, 0.95f));

        RectTransform offerRow = AddRow(8f);
        foreach (ResourceType res in resources)
        {
            ResourceType r = res;
            int currentOffer = _playerTradeOffer.ContainsKey(r) ? _playerTradeOffer[r] : 0;
            int maxPlayerHas = player != null ? player.GetResourceCount(r) : 0;

            GameObject stepper = UIFactory.CreateElement("OfferStepper", offerRow);
            UIFactory.AddHorizontalLayout(stepper, 2f).childForceExpandWidth = false;

            Button minus = UIFactory.CreateButton(stepper.transform, _font, "-", () =>
            {
                if (currentOffer > 0)
                {
                    _playerTradeOffer[r] = currentOffer - 1;
                    Refresh();
                }
            }, new Color(0.2f, 0.25f, 0.3f, 1f), 12, 26f);
            minus.interactable = currentOffer > 0;
            EncounterDialogUI.SetPreferredSize(minus, 26f, 26f);

            Text lbl = UIFactory.CreateText(stepper.transform, _font,
                string.Format("{0}\n{1}/{2}", UIStrings.GetResourceName(r), currentOffer, maxPlayerHas),
                10, Color.white, TextAnchor.MiddleCenter);
            EncounterDialogUI.SetPreferredSize(lbl, 48f, 28f);

            Button plus = UIFactory.CreateButton(stepper.transform, _font, "+", () =>
            {
                if (currentOffer < maxPlayerHas)
                {
                    _playerTradeOffer[r] = currentOffer + 1;
                    Refresh();
                }
            }, new Color(0.2f, 0.25f, 0.3f, 1f), 12, 26f);
            plus.interactable = currentOffer < maxPlayerHas;
            EncounterDialogUI.SetPreferredSize(plus, 26f, 26f);
        }

        // You Receive Prompt
        AddText(UIStrings.PlayerTradeYouReceivePrompt.GetText(), 12, new Color(0.85f, 0.9f, 0.95f));

        RectTransform reqRow = AddRow(8f);
        foreach (ResourceType res in resources)
        {
            ResourceType r = res;
            int currentReq = _playerTradeRequest.ContainsKey(r) ? _playerTradeRequest[r] : 0;
            int maxPartnerHas = partner != null ? partner.GetResourceCount(r) : 0;

            GameObject stepper = UIFactory.CreateElement("ReqStepper", reqRow);
            UIFactory.AddHorizontalLayout(stepper, 2f).childForceExpandWidth = false;

            Button minus = UIFactory.CreateButton(stepper.transform, _font, "-", () =>
            {
                if (currentReq > 0)
                {
                    _playerTradeRequest[r] = currentReq - 1;
                    Refresh();
                }
            }, new Color(0.2f, 0.25f, 0.3f, 1f), 12, 26f);
            minus.interactable = currentReq > 0;
            EncounterDialogUI.SetPreferredSize(minus, 26f, 26f);

            Text lbl = UIFactory.CreateText(stepper.transform, _font,
                string.Format("{0}\n{1}/{2}", UIStrings.GetResourceName(r), currentReq, maxPartnerHas),
                10, Color.white, TextAnchor.MiddleCenter);
            EncounterDialogUI.SetPreferredSize(lbl, 48f, 28f);

            Button plus = UIFactory.CreateButton(stepper.transform, _font, "+", () =>
            {
                if (currentReq < maxPartnerHas)
                {
                    _playerTradeRequest[r] = currentReq + 1;
                    Refresh();
                }
            }, new Color(0.2f, 0.25f, 0.3f, 1f), 12, 26f);
            plus.interactable = currentReq < maxPartnerHas;
            EncounterDialogUI.SetPreferredSize(plus, 26f, 26f);
        }

        // Validation & Summary
        int totalOffered = _playerTradeOffer.Values.Sum();
        int totalRequested = _playerTradeRequest.Values.Sum();
        bool canTrade = partner != null && _triMap.CanTradeWithPlayer(partner, _playerTradeOffer, _playerTradeRequest);

        if (totalOffered < 1 || totalRequested < 1)
        {
            _statusText.text = UIStrings.PlayerTradeInvalidPrompt.GetText();
            _statusText.color = new Color(1f, 0.75f, 0.3f);
        }
        else if (player != null && !player.HasResources(_playerTradeOffer))
        {
            _statusText.text = "You do not have the offered cards.";
            _statusText.color = new Color(1f, 0.4f, 0.4f);
        }
        else if (partner != null && !partner.HasResources(_playerTradeRequest))
        {
            _statusText.text = string.Format("{0} does not have the requested cards.", partner.PlayerName);
            _statusText.color = new Color(1f, 0.4f, 0.4f);
        }
        else
        {
            _statusText.text = string.Format("Trade {0} card(s) with {1} for {2} card(s).", totalOffered, partner.PlayerName, totalRequested);
            _statusText.color = new Color(0.4f, 1f, 0.6f);
        }

        // Execute Player Trade Button
        GameObject execRow = AddRow(0f).gameObject;
        Button executeBtn = UIFactory.CreateButton(execRow.transform, _font,
            UIStrings.ExecutePlayerTradeButton.GetText(), () =>
            {
                if (_triMap.TryTradeWithPlayer(partner, _playerTradeOffer, _playerTradeRequest))
                {
                    InitPlayerTradeSelections(true);
                    _onTradeExecuted?.Invoke();
                    Refresh();
                }
            },
            canTrade ? new Color(0.18f, 0.55f, 0.32f, 1f) : new Color(0.15f, 0.18f, 0.22f, 0.7f),
            13, 38f);
        executeBtn.interactable = canTrade;
        EncounterDialogUI.SetPreferredSize(executeBtn, 250f, 38f);
    }

    private void InitPlayerTradeSelections(bool clearAll = false)
    {
        foreach (ResourceType r in GameRules.Resources)
        {
            if (clearAll || !_playerTradeOffer.ContainsKey(r))
            {
                _playerTradeOffer[r] = 0;
            }
            if (clearAll || !_playerTradeRequest.ContainsKey(r))
            {
                _playerTradeRequest[r] = 0;
            }
        }
    }

    private Text AddText(string content, int fontSize, Color color)
    {
        Text text = UIFactory.CreateText(_tabContent, _font, content, fontSize, color, TextAnchor.MiddleCenter);
        _dynamicElements.Add(text.gameObject);
        return text;
    }

    private RectTransform AddRow(float spacing = 6f)
    {
        GameObject row = UIFactory.CreateElement("Row", _tabContent);
        HorizontalLayoutGroup group = UIFactory.AddHorizontalLayout(row, spacing);
        group.childAlignment = TextAnchor.MiddleCenter;
        group.childForceExpandWidth = false;
        _dynamicElements.Add(row);
        return UIFactory.Rect(row);
    }
}

