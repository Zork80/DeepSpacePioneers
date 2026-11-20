using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal asking the player to confirm ending the turn while movement points remain.
/// </summary>
public class ConfirmEndTurnDialogUI : MonoBehaviour
{
    private Text _promptText;
    private Action _onConfirm;

    /// <summary>Builds the dialog under <paramref name="parent"/> (hidden initially).</summary>
    public static ConfirmEndTurnDialogUI Create(Transform parent, Font font)
    {
        Image background = UIFactory.CreatePanel("ConfirmEndTurnModal", parent,
            new Color(0.015f, 0.045f, 0.08f, 0.98f), true);
        GameObject root = background.gameObject;
        UIFactory.SetAnchors(UIFactory.Rect(root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(50f, 0f), new Vector2(440f, 0f));

        ConfirmEndTurnDialogUI dialog = root.AddComponent<ConfirmEndTurnDialogUI>();

        Outline border = root.AddComponent<Outline>();
        border.effectColor = new Color(0.95f, 0.65f, 0.2f, 0.95f);
        border.effectDistance = new Vector2(2f, -2f);

        UIFactory.AddVerticalLayout(root, 12f, 16);
        UIFactory.AddSizeFitter(root);

        UIFactory.CreateText(root.transform, font, UIStrings.ConfirmEndTurnTitle.GetText(), 17,
            new Color(1f, 0.82f, 0.3f), TextAnchor.MiddleCenter, FontStyle.Bold);

        dialog._promptText = UIFactory.CreateText(root.transform, font, string.Empty, 13,
            new Color(0.92f, 0.95f, 1f), TextAnchor.MiddleCenter);
        dialog._promptText.gameObject.AddComponent<LayoutElement>().minHeight = 60f;

        GameObject row = UIFactory.CreateElement("ButtonRow", root.transform);
        UIFactory.AddHorizontalLayout(row, 20f);
        row.AddComponent<LayoutElement>().minHeight = 38f;

        UIFactory.CreateButton(row.transform, font, UIStrings.ConfirmEndTurnYes.GetText(),
            dialog.OnConfirmClicked, new Color(0.65f, 0.22f, 0.22f, 1f), 13, 38f);
        UIFactory.CreateButton(row.transform, font, UIStrings.ConfirmEndTurnNo.GetText(),
            dialog.Hide, new Color(0.15f, 0.35f, 0.55f, 1f), 13, 38f);

        root.SetActive(false);
        return dialog;
    }

    public void Show(string prompt, Action onConfirm)
    {
        _onConfirm = onConfirm;
        _promptText.text = prompt;
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void OnConfirmClicked()
    {
        Hide();
        _onConfirm?.Invoke();
    }
}

