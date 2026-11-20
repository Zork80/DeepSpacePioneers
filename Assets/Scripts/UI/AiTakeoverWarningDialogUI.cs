using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal countdown dialog shown after 90s of inactivity to warn the active player
/// of impending AI takeover. Can be cancelled by the player clicking the button or interacting.
/// </summary>
public class AiTakeoverWarningDialogUI : MonoBehaviour
{
    private Text _titleText;
    private Text _noticeText;
    private Text _countdownText;
    private Button _cancelBtn;
    private Image _dimmer;
    private Outline _border;

    public bool IsActive => gameObject != null && gameObject.activeSelf;
    public event Action OnCancelTakeover;

    /// <summary>Builds the dialog under <paramref name="parent"/> (hidden initially).</summary>
    public static AiTakeoverWarningDialogUI Create(Transform parent, Font font)
    {
        Image dimmer = UIFactory.CreatePanel("AiTakeoverModal", parent,
            new Color(0.02f, 0.04f, 0.07f, 0.95f), true);
        GameObject root = dimmer.gameObject;
        UIFactory.SetAnchors(UIFactory.Rect(root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 0f));

        AiTakeoverWarningDialogUI dialog = root.AddComponent<AiTakeoverWarningDialogUI>();
        dialog._dimmer = dimmer;

        Outline border = root.AddComponent<Outline>();
        border.effectColor = new Color(1f, 0.65f, 0.15f, 0.95f);
        border.effectDistance = new Vector2(2f, -2f);
        dialog._border = border;

        UIFactory.AddVerticalLayout(root, 16f, 22);
        UIFactory.AddSizeFitter(root);

        dialog._titleText = UIFactory.CreateText(root.transform, font,
            UIStrings.AiTakeoverTitle.GetText(), 18,
            new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, FontStyle.Bold);

        dialog._noticeText = UIFactory.CreateText(root.transform, font,
            UIStrings.AiTakeoverNotice.GetText(), 13,
            new Color(0.92f, 0.95f, 1f), TextAnchor.MiddleCenter);

        dialog._countdownText = UIFactory.CreateText(root.transform, font,
            string.Format(UIStrings.AiTakeoverCountdownFormat.GetText(), 90), 22,
            new Color(1f, 0.55f, 0.2f), TextAnchor.MiddleCenter, FontStyle.Bold);

        dialog._cancelBtn = UIFactory.CreateButton(root.transform, font,
            UIStrings.AiTakeoverCancelButton.GetText(),
            () => dialog.HandleCancelClicked(),
            new Color(0.12f, 0.55f, 0.32f, 1f), 14, 44f);
        dialog._cancelBtn.GetComponent<LayoutElement>().preferredWidth = 260f;

        root.SetActive(false);
        return dialog;
    }

    public void Show(int remainingSeconds)
    {
        if (_dimmer != null) _dimmer.raycastTarget = true;
        UpdateCountdown(remainingSeconds);
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
    }

    public void UpdateCountdown(int remainingSeconds)
    {
        if (_countdownText != null)
        {
            _countdownText.text = string.Format(UIStrings.AiTakeoverCountdownFormat.GetText(), Mathf.Max(0, remainingSeconds));
            if (remainingSeconds <= 15)
            {
                _countdownText.color = new Color(1f, 0.22f, 0.22f);
            }
            else
            {
                _countdownText.color = new Color(1f, 0.65f, 0.2f);
            }
        }
    }

    public void Hide()
    {
        if (_dimmer != null) _dimmer.raycastTarget = false;
        gameObject.SetActive(false);
    }

    private void HandleCancelClicked()
    {
        Hide();
        OnCancelTakeover?.Invoke();
    }
}

