using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Overlay that shows the current encounter card: folk banner, card text, a dynamic
/// content area for choices/outcomes, and the "resolve" button. Everything is laid out with
/// layout groups so the panel grows with its content.
/// </summary>
public class EncounterDialogUI : MonoBehaviour
{
    private static readonly Color ActionColor = new Color(0.12f, 0.35f, 0.55f, 0.95f);
    private static readonly Color DisabledColor = new Color(0.15f, 0.18f, 0.22f, 0.7f);
    private static readonly Color DisabledTextColor = new Color(0.6f, 0.65f, 0.68f);

    private Font _font;
    private Image _folkImage;
    private Text _cardText;
    private RectTransform _content;
    private Button _resolveButton;
    private Text _resolveLabel;
    private readonly List<GameObject> _dynamicElements = new List<GameObject>();

    /// <summary>Builds the overlay under <paramref name="parent"/> (hidden initially).</summary>
    public static EncounterDialogUI Create(Transform parent, Font font, UnityAction onResolveClicked)
    {
        Image background = UIFactory.CreatePanel("EncounterOverlay", parent,
            new Color(0.015f, 0.04f, 0.055f, 0.97f), true);
        GameObject root = background.gameObject;
        UIFactory.SetAnchors(UIFactory.Rect(root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 0f));

        EncounterDialogUI dialog = root.AddComponent<EncounterDialogUI>();
        dialog._font = font;

        UIFactory.AddVerticalLayout(root, 10f, 16);
        UIFactory.AddSizeFitter(root);

        GameObject banner = UIFactory.CreateElement("EncounterFolkBanner", root.transform,
            typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        dialog._folkImage = banner.GetComponent<Image>();
        dialog._folkImage.preserveAspect = true;
        dialog._folkImage.raycastTarget = false;
        LayoutElement bannerLayout = banner.GetComponent<LayoutElement>();
        bannerLayout.preferredHeight = 160f;
        bannerLayout.minHeight = 160f;
        banner.SetActive(false);

        dialog._cardText = UIFactory.CreateText(root.transform, font, string.Empty, 16, Color.white);
        dialog._cardText.lineSpacing = 1.1f;
        dialog._cardText.name = "EncounterCard";

        GameObject content = UIFactory.CreateElement("EncounterContent", root.transform);
        UIFactory.AddVerticalLayout(content, 6f, 0);
        dialog._content = UIFactory.Rect(content);

        dialog._resolveButton = UIFactory.CreateButton(root.transform, font,
            UIStrings.EncounterAnswerGivenButton.GetText(), onResolveClicked,
            new Color(0.15f, 0.2f, 0.28f, 0.95f), 13, 42f);
        dialog._resolveButton.name = "ResolveEncounterButton";
        dialog._resolveLabel = dialog._resolveButton.GetComponentInChildren<Text>();

        root.SetActive(false);
        return dialog;
    }

    public bool IsVisible { get { return gameObject.activeSelf; } }

    public void SetVisible(bool visible)
    {
        if (visible)
        {
            transform.SetAsLastSibling();
        }
        gameObject.SetActive(visible);
    }

    public void SetFolkSprite(Sprite sprite)
    {
        _folkImage.sprite = sprite;
        _folkImage.gameObject.SetActive(sprite != null);
    }

    public void SetCardText(string text)
    {
        _cardText.text = text;
    }

    public void SetResolveButton(bool visible, string label = null)
    {
        _resolveButton.gameObject.SetActive(visible);
        if (label != null)
        {
            _resolveLabel.text = label;
        }
    }

    public void ClearContent()
    {
        foreach (GameObject element in _dynamicElements)
        {
            if (element != null)
            {
                Destroy(element);
            }
        }
        _dynamicElements.Clear();
    }

    /// <summary>Adds a full-width button to the content area (or to <paramref name="parent"/>).</summary>
    public Button AddButton(string label, bool interactable, UnityAction onClick, Color color, int fontSize = 13,
        float height = 36f, Transform parent = null)
    {
        Button button = UIFactory.CreateButton(parent != null ? parent : _content, _font, label, onClick,
            interactable ? color : DisabledColor, fontSize, height);
        button.interactable = interactable;
        Text text = button.GetComponentInChildren<Text>();
        text.color = interactable ? Color.white : DisabledTextColor;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        UIFactory.Stretch(text.rectTransform, new Vector2(4f, 0f), new Vector2(-4f, 0f));
        if (parent == null)
        {
            _dynamicElements.Add(button.gameObject);
        }
        return button;
    }

    public Button AddActionButton(string label, bool interactable, UnityAction onClick)
    {
        return AddButton(label, interactable, onClick, ActionColor);
    }

    public Text AddText(string content, int fontSize, Color color, Transform parent = null)
    {
        Text text = UIFactory.CreateText(parent != null ? parent : _content, _font, content, fontSize, color,
            TextAnchor.MiddleCenter);
        if (parent == null)
        {
            _dynamicElements.Add(text.gameObject);
        }
        return text;
    }

    /// <summary>Adds a centered horizontal row to the content area; children are laid out left to right.</summary>
    public RectTransform AddRow(float spacing = 6f)
    {
        GameObject row = UIFactory.CreateElement("Row", _content);
        HorizontalLayoutGroup group = UIFactory.AddHorizontalLayout(row, spacing);
        group.childAlignment = TextAnchor.MiddleCenter;
        group.childForceExpandWidth = false;
        _dynamicElements.Add(row);
        return UIFactory.Rect(row);
    }

    /// <summary>Adds a fixed-width, fixed-height small element (e.g. +/- button) to a row.</summary>
    public static void SetPreferredSize(Component element, float width, float height)
    {
        LayoutElement layout = element.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = element.gameObject.AddComponent<LayoutElement>();
        }
        layout.preferredWidth = width;
        layout.minWidth = width;
        layout.preferredHeight = height;
        layout.minHeight = height;
    }
}

