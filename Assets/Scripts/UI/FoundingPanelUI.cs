using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Dedicated modal dialog for the initial setup phase (Founding Phase).
/// Displays setup status, current player prompt, and interactive choice buttons
/// (Roll for starting player, pick colony ship / trade ship, select flagship upgrade).
/// Arranged using Auto-Layout groups (VerticalLayoutGroup, ContentSizeFitter).
/// </summary>
public class FoundingPanelUI : MonoBehaviour
{
    private Font _font;
    private Text _statusText;
    private RectTransform _buttonContainer;
    private readonly List<GameObject> _buttons = new List<GameObject>();

    private static readonly Color BackgroundColor = new Color(0.015f, 0.04f, 0.06f, 0.98f);
    private static readonly Color OutlineColor = new Color(0.2f, 0.5f, 0.7f, 0.8f);
    private static readonly Color ButtonColor = new Color(0.12f, 0.35f, 0.55f, 0.95f);

    public static FoundingPanelUI Create(Transform parent, Font font)
    {
        Image background = UIFactory.CreatePanel("FoundingOverlay", parent, BackgroundColor, true);
        GameObject root = background.gameObject;

        RectTransform rect = UIFactory.Rect(root);
        UIFactory.SetAnchors(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 0f));

        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = OutlineColor;
        outline.effectDistance = new Vector2(2f, -2f);

        FoundingPanelUI panel = root.AddComponent<FoundingPanelUI>();
        panel._font = font;

        UIFactory.AddVerticalLayout(root, 14f, 20);
        UIFactory.AddSizeFitter(root);

        // Status / prompt text
        panel._statusText = UIFactory.CreateText(root.transform, font, string.Empty, 17, Color.white, TextAnchor.UpperLeft);
        panel._statusText.lineSpacing = 1.15f;
        panel._statusText.name = "FoundingPhaseText";

        // Buttons container
        GameObject buttonContainerObj = UIFactory.CreateElement("FoundingButtons", root.transform);
        UIFactory.AddVerticalLayout(buttonContainerObj, 8f, 0);
        panel._buttonContainer = UIFactory.Rect(buttonContainerObj);

        root.SetActive(false);
        return panel;
    }

    public bool IsVisible => gameObject.activeSelf;

    public void SetVisible(bool visible)
    {
        if (visible)
        {
            transform.SetAsLastSibling();
        }
        gameObject.SetActive(visible);
    }

    public void SetText(string text)
    {
        if (_statusText != null)
        {
            _statusText.text = text;
        }
    }

    public void ClearButtons()
    {
        for (int i = 0; i < _buttons.Count; i++)
        {
            if (_buttons[i] != null)
            {
                Destroy(_buttons[i]);
            }
        }
        _buttons.Clear();
    }

    public Button AddActionButton(string label, UnityAction action)
    {
        Button btn = UIFactory.CreateButton(_buttonContainer, _font, label, action, ButtonColor, 15, 42f);
        Text text = btn.GetComponentInChildren<Text>();
        if (text != null)
        {
            text.fontStyle = FontStyle.Bold;
        }
        _buttons.Add(btn.gameObject);
        return btn;
    }
}

