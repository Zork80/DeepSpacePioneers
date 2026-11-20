using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal dialog allowing the active player to choose one of their ships
/// (e.g. to be grounded by a combat loss / BlockOneShip penalty, or to receive a space jump).
/// </summary>
public class ShipChoiceDialogUI : MonoBehaviour
{
    private Font _font;
    private Action<Unit> _onChosen;

    private Text _titleText;
    private Text _promptText;
    private Transform _buttonsContainer;

    public bool IsActive => gameObject != null && gameObject.activeSelf;

    public static ShipChoiceDialogUI Create(Transform parent, Font font)
    {
        Image background = UIFactory.CreatePanel("ShipChoiceOverlay", parent,
            new Color(0.02f, 0.04f, 0.08f, 0.98f), true);
        GameObject root = background.gameObject;

        UIFactory.SetAnchors(UIFactory.Rect(root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480f, 0f));

        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = new Color(0.9f, 0.35f, 0.25f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        ShipChoiceDialogUI dialog = root.AddComponent<ShipChoiceDialogUI>();
        dialog._font = font;

        UIFactory.AddVerticalLayout(root, 12f, 16);
        UIFactory.AddSizeFitter(root);

        dialog._titleText = UIFactory.CreateText(root.transform, font, string.Empty, 20,
            new Color(1f, 0.55f, 0.35f), TextAnchor.MiddleCenter, FontStyle.Bold);
        dialog._promptText = UIFactory.CreateText(root.transform, font, string.Empty, 13,
            Color.white, TextAnchor.MiddleCenter);

        GameObject btnContainer = UIFactory.CreateElement("ButtonsContainer", root.transform);
        UIFactory.AddVerticalLayout(btnContainer, 8f, 4);
        dialog._buttonsContainer = btnContainer.transform;

        UIManager.RegisterOverlayCheck(() => dialog != null && dialog.IsActive);
        root.SetActive(false);
        return dialog;
    }

    public void Open(IList<Unit> units, string title, string prompt, Action<Unit> onChosen, Color? buttonColor = null)
    {
        _onChosen = onChosen;
        _titleText.text = title;
        _promptText.text = prompt;

        foreach (Transform child in _buttonsContainer)
        {
            Destroy(child.gameObject);
        }

        Color btnColor = buttonColor ?? new Color(0.45f, 0.18f, 0.15f, 0.95f);

        if (units != null)
        {
            foreach (Unit unit in units)
            {
                if (unit == null || unit.IsDestroyed) continue;
                Unit currentUnit = unit;
                string shipName = UIStrings.GetShipName(currentUnit.ShipType);
                string coords = currentUnit.Tri != null ? $"({currentUnit.Tri.Q}, {currentUnit.Tri.R})" : "?";
                string label = $"{shipName} bei {coords}";

                UIFactory.CreateButton(_buttonsContainer, _font, label,
                    () => Choose(currentUnit), btnColor, 13, 36f);
            }
        }

        transform.SetAsLastSibling();
        gameObject.SetActive(true);
    }

    private void Choose(Unit unit)
    {
        gameObject.SetActive(false);
        _onChosen?.Invoke(unit);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}

