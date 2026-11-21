using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal that lets the player pick one friendship card after an encounter with an alien civilization.
/// </summary>
public class FriendshipCardDialogUI : MonoBehaviour
{
    private const float BannerHeight = 65f;
    private const float StandardCardButtonHeight = 52f;
    private const float CultivatorCardButtonHeight = 66f;

    private static readonly ResourceType[] AllProductionResources = new ResourceType[]
    {
        ResourceType.Ore,
        ResourceType.Fuel,
        ResourceType.Carbon,
        ResourceType.Food,
        ResourceType.TradeGoods
    };

    private Font _font;
    private RectTransform _rootRect;
    private Text _titleText;
    private Button _peekBoardButton;
    private Text _peekBoardButtonText;
    private Text _promptText;
    private Text _summaryText;
    private Image _folkImage;
    private RectTransform _cardContainer;
    private readonly List<GameObject> _cardButtons = new List<GameObject>();

    private bool _isPeekingBoard;
    private bool _hasFolkSprite;
    private bool _hasSummary;

    /// <summary>Builds the dialog under <paramref name="parent"/> (hidden initially).</summary>
    public static FriendshipCardDialogUI Create(Transform parent, Font font)
    {
        Image background = UIFactory.CreatePanel("FriendshipCardSelectionModal", parent,
            new Color(0.015f, 0.045f, 0.08f, 0.98f), true);
        GameObject root = background.gameObject;
        RectTransform rootRect = UIFactory.Rect(root);
        UIFactory.SetAnchors(rootRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(50f, 0f), new Vector2(540f, 0f));

        FriendshipCardDialogUI dialog = root.AddComponent<FriendshipCardDialogUI>();
        dialog._font = font;
        dialog._rootRect = rootRect;

        Outline border = root.AddComponent<Outline>();
        border.effectColor = new Color(0.85f, 0.65f, 0.2f, 0.95f);
        border.effectDistance = new Vector2(2f, -2f);

        UIFactory.AddVerticalLayout(root, 8f, 15);
        UIFactory.AddSizeFitter(root);

        GameObject headerRow = UIFactory.CreateElement("HeaderRow", root.transform);
        UIFactory.AddHorizontalLayout(headerRow, 8f, 0);
        LayoutElement headerLayout = headerRow.AddComponent<LayoutElement>();
        headerLayout.minHeight = 28f;
        headerLayout.preferredHeight = 28f;

        dialog._titleText = UIFactory.CreateText(headerRow.transform, font, UIStrings.ChooseFriendshipCardTitle.GetText(),
            15, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleLeft, FontStyle.Bold);
        LayoutElement titleLayout = dialog._titleText.gameObject.AddComponent<LayoutElement>();
        titleLayout.flexibleWidth = 1f;

        dialog._peekBoardButton = UIFactory.CreateButton(headerRow.transform, font,
            UIStrings.FriendshipViewBoardButton.GetText(),
            dialog.TogglePeekBoard,
            new Color(0.16f, 0.32f, 0.50f, 1f), 11, 26f);
        dialog._peekBoardButtonText = dialog._peekBoardButton.GetComponentInChildren<Text>();
        LayoutElement peekLayout = dialog._peekBoardButton.GetComponent<LayoutElement>();
        if (peekLayout != null)
        {
            peekLayout.minWidth = 140f;
            peekLayout.preferredWidth = 145f;
        }

        GameObject banner = UIFactory.CreateElement("FolkBanner", root.transform, typeof(CanvasRenderer),
            typeof(Image), typeof(LayoutElement));
        dialog._folkImage = banner.GetComponent<Image>();
        dialog._folkImage.preserveAspect = true;
        dialog._folkImage.raycastTarget = false;
        LayoutElement bannerLayout = banner.GetComponent<LayoutElement>();
        bannerLayout.preferredHeight = BannerHeight;
        bannerLayout.minHeight = BannerHeight;

        dialog._promptText = UIFactory.CreateText(root.transform, font, string.Empty, 11,
            new Color(0.8f, 0.92f, 1f), TextAnchor.MiddleCenter);

        dialog._summaryText = UIFactory.CreateText(root.transform, font, string.Empty, 11,
            new Color(0.88f, 0.94f, 1f), TextAnchor.MiddleCenter);
        dialog._summaryText.gameObject.SetActive(false);

        GameObject container = UIFactory.CreateElement("Cards", root.transform);
        UIFactory.AddVerticalLayout(container, 4f, 0);
        dialog._cardContainer = UIFactory.Rect(container);

        root.SetActive(false);
        return dialog;
    }

    public bool IsActive => gameObject != null && gameObject.activeSelf;
    private List<FriendshipCard> _availableCards;
    public IReadOnlyList<FriendshipCard> AvailableCards => _availableCards != null ? _availableCards.AsReadOnly() : null;
    private AlienCivilization _currentCiv;
    public AlienCivilization CurrentCiv => _currentCiv;
    private Action<FriendshipCard> _onSelected;

    public bool TrySelectCard(int cardIndex)
    {
        if (!IsActive || _availableCards == null || cardIndex < 0 || cardIndex >= _availableCards.Count)
        {
            return false;
        }

        FriendshipCard chosen = _availableCards[cardIndex];
        Action<FriendshipCard> callback = _onSelected;
        Hide();
        callback?.Invoke(chosen);
        return true;
    }

    /// <summary>Shows the cards without player production context.</summary>
    public void Show(AlienCivilization civ, List<FriendshipCard> cards, Action<FriendshipCard> onSelected)
    {
        Show(null, civ, cards, onSelected);
    }

    /// <summary>Shows the cards; <paramref name="onSelected"/> is invoked with the chosen card.</summary>
    public void Show(Player player, AlienCivilization civ, List<FriendshipCard> cards, Action<FriendshipCard> onSelected)
    {
        Clear();
        _availableCards = cards;
        _currentCiv = civ;
        _onSelected = onSelected;
        _isPeekingBoard = false;

        _titleText.text = UIStrings.ChooseFriendshipCardTitle.GetText();
        _promptText.text = string.Format(
            UIStrings.ChooseFriendshipCardPrompt.GetText(),
            UIStrings.GetAlienCivilizationName(civ));

        string spriteName = GetCivilizationImageName(civ);
        Sprite folkSprite = !string.IsNullOrEmpty(spriteName)
            ? Resources.Load<Sprite>("FolkImages/" + spriteName)
            : null;
        _folkImage.sprite = folkSprite;
        _hasFolkSprite = folkSprite != null;

        Dictionary<ResourceType, List<int>> productionByResource = BuildPlayerProductionMap(player);
        _hasSummary = civ == AlienCivilization.Cultivator && player != null;
        if (_hasSummary && _summaryText != null)
        {
            _summaryText.text = BuildProductionSummaryText(productionByResource);
        }

        foreach (FriendshipCard card in cards)
        {
            FriendshipCard captured = card;
            ResourceType? bonusResource = GetCultivatorBonusResource(card.Id);
            bool hasProductionLine = bonusResource.HasValue && player != null;
            float buttonHeight = hasProductionLine ? CultivatorCardButtonHeight : StandardCardButtonHeight;

            Button button = UIFactory.CreateButton(_cardContainer, _font, string.Empty, () =>
            {
                Hide();
                onSelected?.Invoke(captured);
            }, new Color(0.12f, 0.28f, 0.44f, 1f), 11, buttonHeight);

            Text label = button.GetComponentInChildren<Text>();
            UIFactory.Stretch(label.rectTransform, new Vector2(10f, 4f), new Vector2(-10f, -4f));
            label.alignment = TextAnchor.MiddleLeft;
            label.verticalOverflow = VerticalWrapMode.Truncate;

            if (hasProductionLine)
            {
                string prodLine = FormatCardProductionLine(productionByResource, bonusResource.Value);
                label.text = string.Format("<b>{0}</b>\n<color=#D4EDDA>{1}</color>\n{2}",
                    card.GetName(), card.GetDescription(), prodLine);
            }
            else
            {
                label.text = string.Format("<b>{0}</b>\n<color=#D4EDDA>{1}</color>",
                    card.GetName(), card.GetDescription());
            }

            _cardButtons.Add(button.gameObject);
        }

        ApplyPeekState();
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        _isPeekingBoard = false;
        gameObject.SetActive(false);
        _availableCards = null;
        _onSelected = null;
    }

    private void TogglePeekBoard()
    {
        _isPeekingBoard = !_isPeekingBoard;
        ApplyPeekState();
    }

    private void ApplyPeekState()
    {
        if (_folkImage != null)
        {
            _folkImage.gameObject.SetActive(!_isPeekingBoard && _hasFolkSprite);
        }
        if (_promptText != null)
        {
            _promptText.gameObject.SetActive(!_isPeekingBoard);
        }
        if (_summaryText != null)
        {
            _summaryText.gameObject.SetActive(!_isPeekingBoard && _hasSummary);
        }
        if (_cardContainer != null)
        {
            _cardContainer.gameObject.SetActive(!_isPeekingBoard);
        }
        if (_peekBoardButtonText != null)
        {
            _peekBoardButtonText.text = _isPeekingBoard
                ? UIStrings.FriendshipBackToSelectionButton.GetText()
                : UIStrings.FriendshipViewBoardButton.GetText();
        }
        if (_rootRect != null)
        {
            if (_isPeekingBoard)
            {
                UIFactory.SetAnchors(_rootRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(540f, 0f));
            }
            else
            {
                UIFactory.SetAnchors(_rootRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(50f, 0f), new Vector2(540f, 0f));
            }
        }
    }

    private static Dictionary<ResourceType, List<int>> BuildPlayerProductionMap(Player player)
    {
        var map = new Dictionary<ResourceType, List<int>>();
        foreach (ResourceType res in AllProductionResources)
        {
            map[res] = new List<int>();
        }

        if (player == null || player.Colonies == null)
        {
            return map;
        }

        foreach (Colony colony in player.Colonies)
        {
            if (colony == null || colony.Tri == null) continue;

            foreach (Tri planetSystem in colony.Tri.GetAllNeighbours()
                .Where(tri => tri != null && tri.SystemKind == SystemKind.Planets))
            {
                ResourceType[] resources = planetSystem.PlanetResources;
                int[] productionNumbers = planetSystem.PlanetProductionNumbers;
                if (resources == null || productionNumbers == null) continue;

                int count = Mathf.Min(resources.Length, productionNumbers.Length);
                for (int planet = 0; planet < count; planet++)
                {
                    if (planetSystem.HasUnresolvedHazard(planet)) continue;

                    ResourceType res = resources[planet];
                    if (map.TryGetValue(res, out List<int> list))
                    {
                        list.Add(productionNumbers[planet]);
                    }
                }
            }
        }

        return map;
    }

    private static string BuildProductionSummaryText(Dictionary<ResourceType, List<int>> productionByResource)
    {
        var parts = new List<string>(AllProductionResources.Length);
        foreach (ResourceType res in AllProductionResources)
        {
            List<int> nums = productionByResource[res];
            string resName = UIStrings.GetResourceName(res);
            if (nums.Count > 0)
            {
                string joined = string.Join(", ", nums.Distinct().OrderBy(n => n));
                parts.Add(string.Format("<color=#90EE90>{0}: {1}</color>", resName, joined));
            }
            else
            {
                parts.Add(string.Format("<color=#8899AA>{0}: –</color>", resName));
            }
        }

        return string.Format("<b>{0}</b>\n{1}",
            UIStrings.FriendshipProductionSummaryHeader.GetText(),
            string.Join("  |  ", parts));
    }

    private static string FormatCardProductionLine(Dictionary<ResourceType, List<int>> productionByResource, ResourceType resource)
    {
        if (productionByResource != null &&
            productionByResource.TryGetValue(resource, out List<int> nums) &&
            nums.Count > 0)
        {
            string joined = string.Join(", ", nums.Distinct().OrderBy(n => n));
            string text = string.Format(UIStrings.FriendshipCardProductionMatchFormat.GetText(), joined, nums.Count);
            return string.Format("<color=#90EE90>{0}</color>", text);
        }

        return string.Format("<color=#99AABB>{0}</color>", UIStrings.FriendshipCardProductionNone.GetText());
    }

    private static ResourceType? GetCultivatorBonusResource(FriendshipCardId id)
    {
        switch (id)
        {
            case FriendshipCardId.CultivatorProductionOre: return ResourceType.Ore;
            case FriendshipCardId.CultivatorProductionFuel: return ResourceType.Fuel;
            case FriendshipCardId.CultivatorProductionCarbon: return ResourceType.Carbon;
            case FriendshipCardId.CultivatorProductionFood: return ResourceType.Food;
            case FriendshipCardId.CultivatorProductionGoods: return ResourceType.TradeGoods;
            default: return null;
        }
    }

    private void Clear()
    {
        foreach (GameObject button in _cardButtons)
        {
            if (button != null)
            {
                Destroy(button);
            }
        }
        _cardButtons.Clear();
    }

    private static string GetCivilizationImageName(AlienCivilization civ)
    {
        switch (civ)
        {
            case AlienCivilization.Scientist:
                return "Scientists";
            case AlienCivilization.Diplomat:
                return "Diplomats";
            case AlienCivilization.Trader:
                return "Traders";
            case AlienCivilization.Cultivator:
                return "Cultivators";
            default:
                return null;
        }
    }
}
