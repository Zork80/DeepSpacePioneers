using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal that lets the player pick one friendship card after an encounter with an alien civilization.
/// </summary>
public class FriendshipCardDialogUI : MonoBehaviour
{
    private const float BannerHeight = 65f;
    private const float CardButtonHeight = 52f;

    private Font _font;
    private Text _titleText;
    private Text _promptText;
    private Image _folkImage;
    private RectTransform _cardContainer;
    private readonly List<GameObject> _cardButtons = new List<GameObject>();

    /// <summary>Builds the dialog under <paramref name="parent"/> (hidden initially).</summary>
    public static FriendshipCardDialogUI Create(Transform parent, Font font)
    {
        Image background = UIFactory.CreatePanel("FriendshipCardSelectionModal", parent,
            new Color(0.015f, 0.045f, 0.08f, 0.98f), true);
        GameObject root = background.gameObject;
        UIFactory.SetAnchors(UIFactory.Rect(root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(50f, 0f), new Vector2(520f, 0f));

        FriendshipCardDialogUI dialog = root.AddComponent<FriendshipCardDialogUI>();
        dialog._font = font;

        Outline border = root.AddComponent<Outline>();
        border.effectColor = new Color(0.85f, 0.65f, 0.2f, 0.95f);
        border.effectDistance = new Vector2(2f, -2f);

        UIFactory.AddVerticalLayout(root, 8f, 15);
        UIFactory.AddSizeFitter(root);

        dialog._titleText = UIFactory.CreateText(root.transform, font, UIStrings.ChooseFriendshipCardTitle.GetText(),
            15, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleCenter, FontStyle.Bold);

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

    /// <summary>Shows the cards; <paramref name="onSelected"/> is invoked with the chosen card.</summary>
    public void Show(AlienCivilization civ, List<FriendshipCard> cards, Action<FriendshipCard> onSelected)
    {
        Clear();
        _availableCards = cards;
        _currentCiv = civ;
        _onSelected = onSelected;
        _titleText.text = UIStrings.ChooseFriendshipCardTitle.GetText();
        _promptText.text = string.Format(
            UIStrings.ChooseFriendshipCardPrompt.GetText(),
            UIStrings.GetAlienCivilizationName(civ));

        string spriteName = GetCivilizationImageName(civ);
        Sprite folkSprite = !string.IsNullOrEmpty(spriteName)
            ? Resources.Load<Sprite>("FolkImages/" + spriteName)
            : null;
        _folkImage.sprite = folkSprite;
        _folkImage.gameObject.SetActive(folkSprite != null);

        foreach (FriendshipCard card in cards)
        {
            FriendshipCard captured = card;
            Button button = UIFactory.CreateButton(_cardContainer, _font, string.Empty, () =>
            {
                Hide();
                onSelected?.Invoke(captured);
            }, new Color(0.12f, 0.28f, 0.44f, 1f), 11, CardButtonHeight);

            Text label = button.GetComponentInChildren<Text>();
            UIFactory.Stretch(label.rectTransform, new Vector2(10f, 4f), new Vector2(-10f, -4f));
            label.alignment = TextAnchor.MiddleLeft;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.text = string.Format("<b>{0}</b>\n<color=#D4EDDA>{1}</color>", card.GetName(), card.GetDescription());

            _cardButtons.Add(button.gameObject);
        }

        transform.SetAsLastSibling();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        _availableCards = null;
        _onSelected = null;
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

