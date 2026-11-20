using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal dialog asking the player to confirm quitting the application.
/// </summary>
public class ConfirmQuitDialogUI : MonoBehaviour
{
    private Image _dimmer;

    public bool IsActive => gameObject != null && gameObject.activeSelf;

    /// <summary>Builds the dialog under <paramref name="parent"/> (hidden initially).</summary>
    public static ConfirmQuitDialogUI Create(Transform parent, Font font)
    {
        // Fullscreen dark dimmer
        Image dimmer = UIFactory.CreatePanel("ConfirmQuitModal", parent,
            new Color(0.015f, 0.045f, 0.08f, 0.98f), true);
        GameObject root = dimmer.gameObject;
        UIFactory.SetAnchors(UIFactory.Rect(root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440f, 0f));

        ConfirmQuitDialogUI dialog = root.AddComponent<ConfirmQuitDialogUI>();
        dialog._dimmer = dimmer;

        Outline border = root.AddComponent<Outline>();
        border.effectColor = new Color(0.95f, 0.35f, 0.25f, 0.95f);
        border.effectDistance = new Vector2(2f, -2f);

        UIFactory.AddVerticalLayout(root, 14f, 20);
        UIFactory.AddSizeFitter(root);

        UIFactory.CreateText(root.transform, font, UIStrings.QuitGame.GetText(), 18,
            new Color(1f, 0.82f, 0.3f), TextAnchor.MiddleCenter, FontStyle.Bold);

        Text prompt = UIFactory.CreateText(root.transform, font, UIStrings.ConfirmQuitPrompt.GetText(), 13,
            new Color(0.92f, 0.95f, 1f), TextAnchor.MiddleCenter);
        prompt.gameObject.AddComponent<LayoutElement>().minHeight = 50f;

        GameObject row = UIFactory.CreateElement("ButtonRow", root.transform);
        UIFactory.AddHorizontalLayout(row, 20f);
        row.AddComponent<LayoutElement>().minHeight = 38f;

        UIFactory.CreateButton(row.transform, font, UIStrings.Quit.GetText(),
            QuitApplication, new Color(0.7f, 0.22f, 0.22f, 1f), 13, 38f);
        UIFactory.CreateButton(row.transform, font, UIStrings.Cancel.GetText(),
            dialog.Hide, new Color(0.15f, 0.35f, 0.55f, 1f), 13, 38f);

        root.SetActive(false);
        return dialog;
    }

    public void Show()
    {
        if (_dimmer != null) _dimmer.raycastTarget = true;
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (_dimmer != null) _dimmer.raycastTarget = false;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Exits play mode in the Unity Editor or terminates the application in standalone builds.
    /// </summary>
    public static void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

